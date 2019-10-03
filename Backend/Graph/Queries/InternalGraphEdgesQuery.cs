using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Queries;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Queries
{
    public class InternalGraphEdgesQuery : IQuery<InternalGraphEdgesResult>
    {
        public Guid NetworkId { get; set; }

        /// <summary>
        /// if specified load only a specific edge (and all its props)
        /// </summary>
        public Tuple<Guid,Guid,string> SourceIdTargetIdUri;        
    }

    public class InternalGraphEdgesResult
    {
        // edgeUri => source, target => edgeModel
        public Dictionary<string, Dictionary<Tuple<Guid, Guid>, InternalEdgeModel>> Edges { get; set; }

        public struct InternalEdgeModel
        {
            public Guid Id;
            public Guid SourceId;
            public Guid TargetId;
            public Dictionary<string, InternalEdgePropModel> PropsJson; // uri => jsonValue
        }

        public struct InternalEdgePropModel
        {
            public string JsonValue;
            public DateTime TimeStamp { get; set; }
        }
    }



    public class InternalGraphEdgesQueryHandler : SecureQueryHandler<InternalGraphEdgesQuery, InternalGraphEdgesResult>        
    {
        private readonly IRepository _repo;

        public InternalGraphEdgesQueryHandler(IRepository repo)
        {
            _repo = repo;
        }

        public class InternalGraphEdgePropSqlItem
        {
            public Guid Id { get; set; }
            public Guid SourceVertexId { get; set; }
            public Guid TargetVertexId { get; set; }
            public string EdgeSchemaUri { get; set; }
            public string JsonValue { get; set; }
            public string PropSchemaUri { get; set; }
            public DateTime? Created { get; set; }
        }

        public override void Authorize(InternalGraphEdgesQuery query)
        {
            Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);
        }

        public async override Task<InternalGraphEdgesResult> Execute(InternalGraphEdgesQuery query)
        {
            var edgeConn = _repo.Db;
            List<InternalGraphEdgePropSqlItem> sql;

            if (query.SourceIdTargetIdUri == null)
            {
                // query to load all edges of a give network
                sql = await
                    (from edge in edgeConn.Edges
                        join edgeProp in edgeConn.EdgeProperties on edge.Id equals edgeProp.EdgeId into gj
                        from propOrNull in gj.DefaultIfEmpty()
                        where edge.Deleted == null
                              &&
                              (edge.SourceVertex.NetworkId == query.NetworkId &&
                               edge.TargetVertex.NetworkId == query.NetworkId)
                              && (propOrNull == null || propOrNull.Deleted == null)
                        select new InternalGraphEdgePropSqlItem()
                        {                            
                            Id = edge.Id,
                            SourceVertexId = edge.SourceVertexId,
                            TargetVertexId = edge.TargetVertexId,
                            EdgeSchemaUri = edge.SchemaUri,
                            JsonValue = propOrNull.JsonValue,
                            PropSchemaUri = propOrNull.SchemaUri,
                            Created = propOrNull.Created.Value
                        })
                        .ToListAsync();
            }
            else
            {
                var sourceVertexId = query.SourceIdTargetIdUri.Item1;
                var targetVertexId = query.SourceIdTargetIdUri.Item2;
                var schemaUri = query.SourceIdTargetIdUri.Item3;
                // query to load a specific edge
                sql = await
                    (from edge in edgeConn.Edges
                     join edgeProp in edgeConn.EdgeProperties on edge.Id equals edgeProp.EdgeId into gj
                     from propOrNull in gj.DefaultIfEmpty()
                     where edge.Deleted == null
                           &&
                           (edge.SourceVertexId == sourceVertexId &&
                            edge.TargetVertexId == targetVertexId &&
                            (schemaUri == null || edge.SchemaUri == schemaUri))
                           && (propOrNull == null || propOrNull.Deleted == null)
                     select new InternalGraphEdgePropSqlItem()
                     {
                         Id = edge.Id,
                         SourceVertexId = edge.SourceVertexId,
                         TargetVertexId = edge.TargetVertexId,
                         EdgeSchemaUri = edge.SchemaUri,
                         JsonValue = propOrNull.JsonValue,
                         PropSchemaUri = propOrNull.SchemaUri,
                         Created = propOrNull.Created.Value
                     })
                        .ToListAsync();
            }

            // prepare edge dictionary
            var result = new InternalGraphEdgesResult()
            {
                Edges =  new Dictionary<string, Dictionary<Tuple<Guid, Guid>, InternalGraphEdgesResult.InternalEdgeModel>>()
            };

            foreach (var edgeProp in sql)
            {
                Dictionary<Tuple<Guid, Guid>, InternalGraphEdgesResult.InternalEdgeModel> bucket;
                if (!result.Edges.TryGetValue(edgeProp.EdgeSchemaUri, out bucket))
                {
                    bucket = new Dictionary<Tuple<Guid, Guid>, InternalGraphEdgesResult.InternalEdgeModel>();
                    result.Edges.Add(edgeProp.EdgeSchemaUri, bucket);
                }

                InternalGraphEdgesResult.InternalEdgeModel edge;
                var edgeKey = new Tuple<Guid, Guid>(edgeProp.SourceVertexId, edgeProp.TargetVertexId);
                if (!bucket.TryGetValue(edgeKey, out edge))
                {
                    edge = new InternalGraphEdgesResult.InternalEdgeModel()
                    {
                        Id = edgeProp.Id,
                        SourceId = edgeProp.SourceVertexId,
                        TargetId = edgeProp.TargetVertexId,
                        PropsJson = new Dictionary<string, InternalGraphEdgesResult.InternalEdgePropModel>()
                    };
                    bucket.Add(edgeKey, edge);
                }

                if (edgeProp.PropSchemaUri != null)
                {
                    edge.PropsJson.Add(edgeProp.PropSchemaUri, new InternalGraphEdgesResult.InternalEdgePropModel() {
                        JsonValue = edgeProp.JsonValue,
                        TimeStamp = edgeProp.Created.Value
                    });
                }
            }
            return result;

        }
    }

}