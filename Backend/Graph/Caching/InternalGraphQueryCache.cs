using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;
using WebPerspective.Areas.Graph.Commands;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.Areas.Settings.Queries;
using WebPerspective.Areas.Settings.Services;
using WebPerspective.Commons.Cache;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Caching
{
   
    public class InternalGraphQueryCache 
        : CachingHandler<InternalGraphQuery, ConcurrentGraphModel>,
        IEventSubscriber<SaveVertexCompletedEvent>,
        IEventSubscriber<SaveEdgeCompletedEvent>,
        IEventSubscriber<DeleteEdgeCompletedEvent>,
        IEventSubscriber<DeleteVertexCompletedEvent>,
        IEventSubscriber<ClearNetworkCacheEvent>
    {
        public override CachingPolicy CachePolicy() => CachingPolicy.LongPreemptiveCachePolicy;

        private readonly IQueryService _queryService;
        private readonly IRepository _repo;
        private readonly IGraphBuilder _graphBuilder;
        private readonly ILog _log;
        private readonly ISiteSettingsProvider _siteSettings;

        public InternalGraphQueryCache(IQueryService queryService, IRepository repo, IGraphBuilder graphBuilder, ILog log, ISiteSettingsProvider siteSettings)
        {
            _queryService = queryService;
            _repo = repo;
            _graphBuilder = graphBuilder;
            _log = log;
            _siteSettings = siteSettings;
        }


        public override string CacheKey(InternalGraphQuery query)
        {
            return query.NetworkId.ToString() + "/" + _siteSettings.Current.Locale;
        }

        private string FullCacheKey(Guid networkId, string locale)
        {
            return this.GetType().Name + "_" + networkId.ToString() + "/" + locale;
        }

        private ConcurrentGraphModel CachedGraph(Guid networkId, string locale)
        {
            return this.Cache.GetValue<ConcurrentGraphModel>(this.FullCacheKey(networkId, locale));            
        }

        public async Task Handle(SaveEdgeCompletedEvent e, ICommandExecutionContext context)
        {
            _log.Debug("Invalidate cache (modify) for edge " + e.Command.SourceVertexId + " => " + e.Command.TargetVertexId);

            var networkId =
                _repo.Db.Vertices.Where(v => v.Id == e.Command.SourceVertexId).Select(v => v.NetworkId).FirstOrDefault();

            var locales = await _queryService.Execute(new NetworkLanguagesQuery()
            {
                NetworkId = networkId
            }, context);
            
            foreach (var locale in locales)
            {
                var cachedGraph = CachedGraph(networkId, locale);
                if (cachedGraph == null) continue; // graph not cached... continue
                
                // reload the edge
                var edgesInternal = await _queryService.Execute(new InternalGraphEdgesQuery()
                {
                    NetworkId = networkId,
                    SourceIdTargetIdUri =
                        new Tuple<Guid, Guid, string>(e.Command.SourceVertexId, e.Command.TargetVertexId,
                            e.Command.SchemaUri)
                }, context);

                // load graph schema
                var schema = await _queryService.Execute(new SchemaQuery()
                {
                    NetworkId = networkId,                    
                }, context);

                // generate property edge objects
                var edges = _graphBuilder.GenerateConcurrentEdgeModel(schema, edgesInternal);

                // update edge and source and target vertices
                foreach (var edge in edges)
                {
                    cachedGraph.Edges.AddOrUpdate(edge.Key, edge.Value, (k, oldEdge) => edge.Value);
                }
                
            }
        }

        public async Task Handle(DeleteEdgeCompletedEvent e, ICommandExecutionContext context)
        {
            _log.Debug("Invalidate cache (delete) for edge" + e.Command.SourceVertexId + " => "+ e.Command.TargetVertexId);

            var networkId =
                _repo.Db.Vertices.Where(v => v.Id == e.Command.SourceVertexId).Select(v => v.NetworkId).FirstOrDefault();

            var locales = await _queryService.Execute(new NetworkLanguagesQuery()
            {
                NetworkId = networkId
            }, context);

            foreach (var locale in locales)
            {                             
                var cachedGraph = CachedGraph(networkId, locale);
                if (cachedGraph == null) continue; // graph not cached... continue

                // load graph schema
                var schema = await _queryService.Execute(new SchemaQuery()
                {
                    NetworkId = networkId,                    
                }, context);                
                var relationshipName = schema.EdgeSchema?.FirstOrDefault(s => s.Uri == e.Command.SchemaUris.First())?.Name; 
                var edgeKey = new Tuple<Guid, Guid, string>(e.Command.SourceVertexId, e.Command.TargetVertexId, relationshipName);

                ConcurrentEdgeModel deleted;
                cachedGraph.Edges.TryRemove(edgeKey, out deleted);
            }
        }

        public async Task Handle(SaveVertexCompletedEvent e, ICommandExecutionContext context)
        {
            _log.Debug("Invalidate cache (modify) for vertex " + e.Command.VertexId);

            var networkId =
                _repo.Db.Vertices.Where(v => v.Id == e.Command.VertexId).Select(v => v.NetworkId).FirstOrDefault();

            var locales = await _queryService.Execute(new NetworkLanguagesQuery()
            {
                NetworkId = networkId
            }, context);

            foreach (var locale in locales)
            {
                var cachedGraph = CachedGraph(networkId, locale);
                if (cachedGraph == null) continue; // graph not cached... continue

                // reload the edge
                var vertexInternal = await _queryService.Execute(new InternalGraphVerticesQuery()
                {
                    NetworkId = networkId,
                    VertexId = e.Command.VertexId                    
                }, context);

                // load graph schema
                var schema = await _queryService.Execute(new SchemaQuery()
                {
                    NetworkId = networkId,                    
                }, context);

                // generate property vertex objects
                var vertices = _graphBuilder.GenerateConcurrentVertexModel(schema, vertexInternal);

                // update vertex
                foreach (var vertex in vertices)
                {
                    cachedGraph.Vertices.AddOrUpdate(vertex.Key, vertex.Value, (k, oldVertex) => vertex.Value);
                }

            }
        }
         
        public async Task Handle(DeleteVertexCompletedEvent e, ICommandExecutionContext context)
        {
            _log.Debug("Invalidate cache (modify) for vertex " + e.Command.VertexId);

            var networkId =
                 _repo.Db.Vertices.Where(v => v.Id == e.Command.VertexId).Select(v => v.NetworkId).FirstOrDefault();

            var locales = await _queryService.Execute(new NetworkLanguagesQuery()
            {
                NetworkId = networkId
            }, context);

            foreach (var locale in locales)
            {
                var cachedGraph = CachedGraph(networkId, locale);
                if (cachedGraph == null) continue; // graph not cached... continue

                _log.Debug($"removing (delete/{locale}) for vertex {e.Command.VertexId}");

                // first delete connected edges
                var edgesToDelete = cachedGraph.Edges.Keys
                    .Where(edgeKey => edgeKey.Item1 == e.Command.VertexId || edgeKey.Item2 == e.Command.VertexId);

                foreach (var edgeToDelete in edgesToDelete)
                {
                    ConcurrentEdgeModel edge;
                    cachedGraph.Edges.TryRemove(edgeToDelete, out edge);
                }

                // finaly remove vertex
                ConcurrentVertexModel deleted;
                cachedGraph.Vertices.TryRemove(e.Command.VertexId, out deleted);
            }
        }

        public async Task Handle(ClearNetworkCacheEvent e, ICommandExecutionContext context)
        {
            _log.Debug("Invalidate cache for network " + e.NetworkId);

            var locales = await _queryService.Execute(new NetworkLanguagesQuery()
            {
                NetworkId = e.NetworkId
            }, context);

            foreach (var locale in locales)
            {
                this.Cache.Remove(this.FullCacheKey(new InternalGraphQuery()
                {
                    NetworkId = e.NetworkId                    
                }));                
            }
        }
    } 
 
}