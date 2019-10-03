using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc.Html;
using Newtonsoft.Json;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Perspectives.Queries;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.Areas.Settings.Queries;
using WebPerspective.Commons.Extensions;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Queries
{
    
    public class VertexQuery : IQuery<PropertyVertexModel>
    {
        public Guid NetworkId { get; set; }        
        public string VertexId { get; set; }

        public VertexQueryScope Scope { get; set; } = VertexQueryScope.Basic;
    }

    public enum VertexQueryScope
    {
        Basic, Full
    }

    public class VertexQueryHandler : SecureQueryHandler<VertexQuery, PropertyVertexModel>
    {
        private readonly IQueryService _queryService;

        public VertexQueryHandler(IQueryService queryService)
        {
            _queryService = queryService;
        }

        public override async Task<PropertyVertexModel> Execute(VertexQuery query)
        {
            var graph = await _queryService.Execute(new InternalGraphQuery()
            {
                NetworkId = query.NetworkId,                
            }, this);
            var settings = await _queryService.Execute(new NetworkSettingsQuery()
            {
                NetworkId = query.NetworkId                
            }, this);

            Guid vertexId;
            if (string.IsNullOrEmpty(query.VertexId) ||query.VertexId.ToLower() == "me")
            {
                vertexId = await _queryService.Execute(new FindUserVertexIdQuery()
                {
                    NetworkId = query.NetworkId,
                    UserName = Auth.UserName
                }, this);
            }
            else
            {
                vertexId = Guid.Parse(query.VertexId);
            }

            switch (query.Scope)
            {
                case VertexQueryScope.Basic:
                    return graph.BasicVertexModel(vertexId, settings);
                case VertexQueryScope.Full:
                    return graph.Vertices[vertexId].ToEdgelessPropertyVertexModel();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Authorize(VertexQuery query)
        {
            Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);
        }
    }    
        
}