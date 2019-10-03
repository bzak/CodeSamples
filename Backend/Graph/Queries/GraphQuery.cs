using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;
using Irony.Parsing;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Queries
{
    

    public class GraphQuery : IQuery<PropertyGraphModel>
    {
        [Required]
        public Guid NetworkId { get; set; }

        [Required]
        public string QueryText { get; set; }
    }

    public class GraphQueryHandler : SecureQueryHandler<GraphQuery, PropertyGraphModel>
        
    {
        private readonly IQueryService _queryService;
        private readonly IGraphQueryCompiler _compiler;
        private readonly ILog _log;

        public GraphQueryHandler(IQueryService queryService, IGraphQueryCompiler compiler, ILog log)
        {
            _queryService = queryService;
            _compiler = compiler;
            _log = log;
        }
        public override void Authorize(GraphQuery query)
        {            
            Auth.AssertPermission(Resources.AdminGraph, query.NetworkId);            
            Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);            
        }

        public async override Task<PropertyGraphModel> Execute(GraphQuery graphQuery)
        {
            if (graphQuery.NetworkId == null) throw new ArgumentNullException(nameof(graphQuery.NetworkId));

            _log.Debug("starting GraphQuery");
            var graph = (await _queryService.Execute(new InternalGraphQuery()
                        {
                            NetworkId = graphQuery.NetworkId,                            
                        }, this))
                        .Snapshot();

            _log.Debug("compiling query");
            var queryList = _compiler.Compile(graphQuery.QueryText);

            _log.Debug("transforming graph");
            foreach (var query in queryList)
            {
                graph = query.Transform(graph);
            }

            _log.Debug("done GraphQuery");
            return graph;
        }


    }
}