using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Connectors.Models;
using WebPerspective.Areas.Connectors.Services;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Perspectives.Services;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Queries
{

    /// <summary>
    /// Merges information about schema from all surveys and data sources
    /// </summary>
    public class SchemaQuery : IQuery<GraphSchemaModel>
    {
        public Guid NetworkId { get; set; }        
    }   

    public class SchemaQueryHandler : SecureQueryHandler<SchemaQuery, GraphSchemaModel>        
    {
        private readonly IRepository _repo;
        private readonly IPerspectiveGraphBridge _perspectiveBridge;
        private readonly IConnectorGraphBridge _connectorBridge;        

        public SchemaQueryHandler(IRepository repo, IPerspectiveGraphBridge perspectiveBridge, IConnectorGraphBridge connectorBridge)
        {
            _repo = repo;
            _perspectiveBridge = perspectiveBridge;
            _connectorBridge = connectorBridge;            
        }

        public override void Authorize(SchemaQuery query)
        {
            Auth.AssertPermission(Resources.AdminData, query.NetworkId);
        }

        public async override Task<GraphSchemaModel> Execute(SchemaQuery query)
        {
            var result = new GraphSchemaModel()
            {
                VertexSchema = new List<GraphProfileSectionModel>(),
                EdgeSchema = new List<GraphRelationshipSchemaModel>()
            };

            // load all surveys
            var surveys = await _repo.Db.Perspectives
                .Include(p=>p.Code)
                .Where(s => s.NetworkId == query.NetworkId).ToListAsync();
            foreach (var survey in surveys)
            {                
                if (survey.Code == null) continue;                
                var surveySchema = _perspectiveBridge.ConvertToGraphSchemaModel(survey);
                MergeSchema(result, surveySchema);
            }

            // load all data connectors
            var connectors = await _repo.Db.DataConnectors.Where(c => c.NetworkId == query.NetworkId).ToListAsync();
            foreach (var connector in connectors)
            {
                var connectorSchema = _connectorBridge.ConvertToGraphSchemaModel(connector);
                MergeSchema(result, connectorSchema);
            }

            return result;
        }

        public void MergeSchema(GraphSchemaModel result, GraphSchemaModel other)
        {
            // todo translate "other" schema
            if (other.VertexSchema != null)
                result.VertexSchema.AddRange(other.VertexSchema);
            if (other.EdgeSchema != null)
                result.EdgeSchema.AddRange(other.EdgeSchema);
        }
    }
}