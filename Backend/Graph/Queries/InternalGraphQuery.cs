using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebPerspective.Areas.Connectors.Models;
using WebPerspective.Areas.Features.Models;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.Areas.Settings.Services;
using WebPerspective.Commons.Extensions;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Queries;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Queries
{
    public class InternalGraphQuery : IQuery<ConcurrentGraphModel>
    {
        [Required]
        public Guid? NetworkId { get; set; }        

        public override string ToString()
        {
            return $"InternalGraphQuery {NetworkId}";
        }
    }

    public class InternalGraphQueryHandler : SecureQueryHandler<InternalGraphQuery, ConcurrentGraphModel>
        
    {
        private readonly IQueryService _queryService;
        private readonly IGraphBuilder _graphBuilder;
        private readonly ILog _log;
        private readonly ISiteSettingsProvider _siteSettings;
        private readonly IRepository _repo;

        public InternalGraphQueryHandler(IQueryService queryService, IGraphBuilder graphBuilder, ILog log, ISiteSettingsProvider siteSettings,
            IRepository repo)
        {
            _queryService = queryService;
            _graphBuilder = graphBuilder;
            _log = log;
            _siteSettings = siteSettings;
            _repo = repo;
        }

        public override void Authorize(InternalGraphQuery query)
        {
            if (Auth.HasPermission(AppFeatures.SyncApi))
            {
                Auth.AssertPermission(AppFeatures.SyncApi);
            }
            else
            {
                Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);
            }
        }

        public async override Task<ConcurrentGraphModel> Execute(InternalGraphQuery query)
        {
            if (query.NetworkId == null) throw new ArgumentNullException(nameof(query.NetworkId));

            var network = await _repo.Db.Networks.FirstAsync(n => n.Id == query.NetworkId.Value);
            if (network.Deleted != null)
                throw new SecurityException("Network not found");

            _log.Debug("querying db");

            var schemaTask = _queryService.Execute(new SchemaQuery()
            {
                NetworkId = query.NetworkId.Value
            }, this);

            var verticesTask = _queryService.Execute(new InternalGraphVerticesQuery()
            {
                NetworkId = query.NetworkId.Value                
            }, this);

            var edgesTask = _queryService.Execute(new InternalGraphEdgesQuery()
            {
                NetworkId = query.NetworkId.Value
            }, this);            
            
            // paralelize tasks
            await Task.WhenAll(verticesTask, edgesTask, schemaTask);

            _log.Debug("transforming db results");

            var schema = schemaTask.Result;
            var vertices = verticesTask.Result;
            var edges = edgesTask.Result;

            var concurrentVertexModels = _graphBuilder.GenerateConcurrentVertexModel(schema, vertices);

            // link it up
            var concurrentEdgeModels = _graphBuilder.GenerateConcurrentEdgeModel(schema, edges);

            var result = new ConcurrentGraphModel(
                new ConcurrentDictionary<Guid, ConcurrentVertexModel>(concurrentVertexModels),
                new ConcurrentDictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel>(concurrentEdgeModels));            

            _log.Debug("finished internal graph query");
            return result;
        }


        
    }
}