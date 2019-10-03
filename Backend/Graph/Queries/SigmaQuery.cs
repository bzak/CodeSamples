using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Graph.Commands;
using WebPerspective.Areas.Graph.Layout;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.Areas.Privacy.Services;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.Areas.Settings.Queries;
using WebPerspective.Commons.Cache;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Queries
{
    public class SigmaQuery : IQuery<SigmaGraphModel>
    {
        [Required]
        public Guid NetworkId { get; set; }

        [Required]
        public string QueryText { get; set; }

        public bool DoLayout { get; set; }
        public string LayoutKey { get; set; }
        public int? DurationMs { get; set; }
        public TimeSpan? Cache { get; set; }
    }

    public class SigmaQueryCache
        : CachingHandler<SigmaQuery, SigmaGraphModel>,
            IEventSubscriber<ClearNetworkCacheEvent>
    {
        public override string CacheKey(SigmaQuery query)
        {
            if (query.Cache == null) return null;
            return query.NetworkId + "_" + query.QueryText;
        }

        public override CachingPolicy CachePolicy(SigmaQuery query)
        {
            if (query.Cache == null) return null;

            return new CachingPolicy()
            {
                SlidingExpiration = query.Cache.Value
            };
        }

        public override string CacheArea(SigmaQuery query)
        {
            return query.NetworkId.ToString();
        }

        public Task Handle(ClearNetworkCacheEvent e, ICommandExecutionContext context)
        {
            this.Cache.RemoveArea(this.FullAreaKey(new SigmaQuery()
            {
                NetworkId = e.NetworkId,
            }));
            return Task.FromResult(0);
        }
    }

    public class SigmaQueryHandler : SecureQueryHandler<SigmaQuery, SigmaGraphModel>        
    {
        private readonly IQueryService _queryService;
        private readonly IGraphLayoutService _layoutService;
        private readonly IGraphQueryCompiler _compiler;
        private readonly IPrivacyFilterService _privacyFilter;

        public SigmaQueryHandler(IQueryService queryService, IGraphLayoutService layoutService, IGraphQueryCompiler compiler,
            IPrivacyFilterService privacyFilter)
        {
            _queryService = queryService;
            _layoutService = layoutService;
            _compiler = compiler;
            _privacyFilter = privacyFilter;
        }

        public override void Authorize(SigmaQuery query)
        {
            if (!Auth.ContextAuthorized)
            {
                Auth.AssertPermission(Resources.AdminGraph, query.NetworkId);
            }
            Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);            
        }

        public async override Task<SigmaGraphModel> Execute(SigmaQuery query)
        {
            if (Guid.Empty.Equals(query.NetworkId)) throw new ArgumentNullException(nameof(query.NetworkId));

            var setttings = await _queryService.Execute(new NetworkSettingsQuery()
            {
                NetworkId = query.NetworkId,                
            }, this);

            var graph = (await _queryService.Execute(new InternalGraphQuery()
            {
                NetworkId = query.NetworkId,                
            }, this))
            .Snapshot();

            graph = _privacyFilter.ApplyFilter(graph, setttings);

            var queryList = _compiler.Compile(query.QueryText);
            foreach (var graphQuery in queryList)
            {
                // add default props
                graphQuery.SelectPropsClause?.VertexProps.Insert(0, new NameProp() { Name = setttings.ProfileCard.LabelProp, Alias = "label" });
                graphQuery.SelectPropsClause?.VertexProps.Insert(0, new NameProp() { Name = setttings.ProfileCard.DetailsProp, Alias = "details" });
                graphQuery.SelectPropsClause?.VertexProps.Insert(0, new NameProp() { Name = setttings.ProfileCard.PhotoProp, Alias = "photo" });

                graph = graphQuery.Transform(graph);
            }

            var sigmaGraph = new SigmaGraphModel()
            {
                Nodes = graph.Vertices?.Select(TransformVertex).ToList(),
                Edges = graph.Edges?.Select(TransformEdge).ToList(),
                Data = graph.Data
            };
            
            if (query.DoLayout)
            {
                var layoutClause = queryList.Last().LayoutClause;
                query.LayoutKey = query.LayoutKey ?? layoutClause?.LayoutKey;
                var task = new LayoutTask()
                {
                    Query = query,                    
                    Settings = layoutClause?.Settings,                    
                };
                if (layoutClause?.Modify != null) task.ModifyLayout = (bool) layoutClause?.Modify.Value;

                _layoutService.Auth = this.Auth;
                TimeSpan? duration = null;
                if (query.DurationMs.HasValue) duration = TimeSpan.FromMilliseconds(query.DurationMs.Value);
                await _layoutService.LayoutGraph(sigmaGraph, task, duration);
            }            

            return sigmaGraph;
        }

        private SigmaNodeModel TransformVertex(PropertyVertexModel vertex, int idx)
        {
            object label = null;
            object size = (double) 1.0;
            Dictionary<string, object> props = null;
            if (vertex.Props != null)
            {
                props = new Dictionary<string, object>(vertex.Props, StringComparer.OrdinalIgnoreCase);
                if (vertex.Props.TryGetValue("label", out label))
                    props.Remove("label");

                if (vertex.Props.TryGetValue("size", out size))
                    props.Remove("size");

                if (props.Count == 0)
                    props = null;
            }
            return new SigmaNodeModel()
            {
                Id = "n" + idx,
                Label = label?.ToString(),
                Entity = vertex.Id,
                Props = props,
                Size = Convert.ToDouble(size)
            };
        }

        private SigmaEdgeModel TransformEdge(PropertyEdgeModel edge, int idx)
        {
            Dictionary<string, object> props = null;
            if (edge.Props != null)
            {
                props = new Dictionary<string, object>(edge.Props);
                props.Remove("size");
            }
            return new SigmaEdgeModel()
            {
                Id = "e" + idx,
                Source = "n" + edge.Source,
                Target = "n" + edge.Target,
                Size = (edge.Props != null && edge.Props.ContainsKey("size")) ? Convert.ToDouble(edge.Props["size"]) : (double?) null,
                Props = props
            };
        }
    }
}