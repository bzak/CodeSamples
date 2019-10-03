using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Swashbuckle.Swagger.Annotations;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Controllers
{
    /*
    [RoutePrefix("api/vertex")]
    [Authorize]
    public class VertexController : ApiController
    {
        private readonly IQueryService _queryService;

        public VertexController(IQueryService queryService)
        {
            _queryService = queryService;
        }


        /// <summary>
        /// Read vertex
        /// </summary>
        [Route("{vertexId}")]
        [SwaggerResponse(HttpStatusCode.OK, "Success")]
        public async Task<IHttpActionResult> GetVertex(Guid vertexId)
        {
            var vertex = await _queryService.Execute(new VertexQuery() { VertexId = vertexId });
            return Ok(vertex);
        }
    }
    */
}
