using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    public class InternalGraphVerticesQuery : IQuery<InternalGraphVerticesResult>
    {        
        public Guid NetworkId { get; set; }

        /// <summary>
        /// if specified load only a specific vertex (and all its props)
        /// </summary>
        public Guid? VertexId;
    }

    public class InternalGraphVerticesResult
    {
        public List<Guid> WithoutProps { get; set; }
        public Dictionary<string, List<InternalVertexPropModel>> WithProps { get; set; }  // prop_schema_uri => propModel

        public struct InternalVertexPropModel
        {
            public Guid VertexId;
            public string JsonValue; 
            public DateTime TimeStamp { get; set; }
        }
    }

    public class InternalGraphVerticesQueryHandler : SecureQueryHandler<InternalGraphVerticesQuery, InternalGraphVerticesResult>
        
    {
        private readonly IRepository _repo;

        public InternalGraphVerticesQueryHandler(IRepository repo)
        {
            _repo = repo;
        }

        public override void Authorize(InternalGraphVerticesQuery query)
        {
            Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);
        }

        public class InternalGraphVertexPropSqlItem
        {
            public Guid VertexId { get; set; }
            public string JsonValue { get; set; }
            public string SchemaUri { get; set; }
            public DateTime? Created { get; set; }
        }

        public async override Task<InternalGraphVerticesResult> Execute(InternalGraphVerticesQuery query)
        {
            var db = _repo.Db;
            List<InternalGraphVertexPropSqlItem> vertexProps;

            if (query.VertexId == null)
            {
                vertexProps = await
                    (from v in db.Vertices
                        join p in db.VertexProperties.Where(prop => prop.Deleted == null) on v.Id equals p.VertexId into gj
                        from propOrNull in gj.DefaultIfEmpty()
                        where v.Deleted == null && v.NetworkId == query.NetworkId
                              && (propOrNull == null || propOrNull.Deleted == null)
                        select new InternalGraphVertexPropSqlItem()
                        {
                            VertexId = v.Id,
                            SchemaUri = propOrNull.SchemaUri,
                            JsonValue = propOrNull.JsonValue,
                            Created = propOrNull.Created
                        })
                        .ToListAsync();
            }
            else
            {
                vertexProps = await
                    (from v in db.Vertices
                     join p in db.VertexProperties.Where(prop=>prop.Deleted == null) on v.Id equals p.VertexId into gj
                     from propOrNull in gj.DefaultIfEmpty()
                     where v.Deleted == null && v.NetworkId == query.NetworkId && v.Id == query.VertexId
                           && (propOrNull == null || propOrNull.Deleted == null)
                     select new InternalGraphVertexPropSqlItem()
                     {
                         VertexId = v.Id,
                         SchemaUri = propOrNull.SchemaUri,
                         JsonValue = propOrNull.JsonValue,
                         Created = propOrNull.Created
                     })
                    .ToListAsync();
            }
            // group by prop uri
            var taskResult = new InternalGraphVerticesResult()
            {
                WithoutProps = new List<Guid>(),
                WithProps = new Dictionary<string, List<InternalGraphVerticesResult.InternalVertexPropModel>>()
                // prop_schema_uri => propModel
            };
            foreach (var vertexProp in vertexProps)
            {
                if (vertexProp.SchemaUri == null)
                {
                    taskResult.WithoutProps.Add(vertexProp.VertexId);
                    continue;
                }

                List<InternalGraphVerticesResult.InternalVertexPropModel> bucket;
                if (!taskResult.WithProps.TryGetValue(vertexProp.SchemaUri, out bucket))
                {
                    bucket = new List<InternalGraphVerticesResult.InternalVertexPropModel>();
                    taskResult.WithProps.Add(vertexProp.SchemaUri, bucket);
                }
                bucket.Add(new InternalGraphVerticesResult.InternalVertexPropModel()
                {
                    VertexId = vertexProp.VertexId,
                    JsonValue = vertexProp.JsonValue,
                    TimeStamp = vertexProp.Created.Value
                });
            }
            return taskResult;

        }
    }
}