using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Swashbuckle.Swagger.Annotations;
using WebPerspective.Areas.Graph.Commands;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.Areas.Perspectives.Commands;
using WebPerspective.Areas.Perspectives.Models;
using WebPerspective.Areas.Perspectives.Queries;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Queries;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Controllers
{
    [RoutePrefix("api/graph")]
    [Authorize]
    public class GraphController : ApiController
    {
        private readonly ICommandDispatcher _dispatcher;
        private readonly IQueryService _queryService;        

        public GraphController(ICommandDispatcher dispatcher, IQueryService queryService)
        {
            _dispatcher = dispatcher;
            _queryService = queryService;            
        }

        /// <summary>
        /// Schema
        /// </summary>
        [Route("{networkId}/schema")]
        [SwaggerResponse(HttpStatusCode.OK, "Success"/*, typeof(GraphSchemaModel)*/)]
        [HttpGet]
        public async Task<IHttpActionResult> Schema(Guid networkId)
        {
            var schema = await _queryService.Execute(new SchemaQuery()
            {
                NetworkId = networkId                
            }, User);
            return Ok(schema);            
        }


        /// <summary>
        /// Perspectives
        /// </summary>
        [SwaggerResponse(HttpStatusCode.OK, "Success", typeof(GraphPerspectivesResult))]
        [Route("{networkId}/perspectives")]
        [HttpPost]
        public async Task<IHttpActionResult> Perspectives(Guid networkId, bool doNotCache = false)
        {
            if (doNotCache)
            {
                await _dispatcher.Execute(new ClearGraphPerspectivesCacheCommand() { NetworkId = networkId }, User);
            }
            var query = new GraphPerspectivesQuery()
            {
                NetworkId = networkId
            };
            var result = await _queryService.Execute(query, User);
            return Ok(result);
        }

        /// <summary>
        /// Query
        /// </summary>
        /// <remarks>
        /// Select whole graph:
        /// <code>SELECT *</code>
        ///
        /// Select a particular vertex:
        /// <code>
        /// SELECT * WHERE id = '396d78df-a6bf-40cf-812e-6537cd1ff5de'
        /// </code>
        /// 
        /// Select one department:
        /// <code>SELECT * WHERE department = "Marketing"</code>
        ///
        /// Select all directors (director, director of operations, etc.):
        /// <code>SELECT * WHERE position LIKE '%director%'</code>
        ///
        /// Limit result to specyfic vertex props, and only two types of edges:
        /// <code>
        /// SELECT name, position AS description, department, <br/>
        /// edge.cooperation, edge.[knowledge sharing]<br/>
        /// WHERE department = "Marketing"
        /// </code>
        ///
        /// Conditions can be combined with AND and OR operators. 
        /// This query will return specific vertex props props and only two types of edges
        /// limiting results to only directors from two departments (IT or Marketing)
        /// <code>
        /// SELECT name, position, department, <br/>
        /// edge.cooperation, edge.[knowledge sharing]<br/>
        /// WHERE (department = "IT" OR department = "Marketing")<br/>
        ///    AND position LIKE '%directors%'
        /// </code>
        /// 
        /// It is possible to query nodes according to their connectivity.
        /// 
        /// For instance you can query all neighbours of some vertex (including the vertex itself)
        /// <code>
        /// SELECT * WHERE edge(source.id = '396d78df-a6bf-40cf-812e-6537cd1ff5de')
        /// </code>
        /// 
        /// The query below will return all employees that are cooperating with "Marketing" department.
        /// The keyword <samp>edge</samp> matches all incoming and outgoing edges so this will include 
        /// nodes on both ends of the replationships. 
        /// <code>
        /// SELECT * WHERE edge(Name="Cooperation" AND Source.Dept = "Marketing")
        /// </code>
        /// 
        /// On the other hand keyword <samp>in_edge</samp> matches only incoming edges. So
        /// Hence the query below will return only people connected to Marketing department 
        /// but without the Marketing department itself 
        /// (unless the Marketing department is internaly connected what is usually the case)
        /// <code>
        /// SELECT * WHERE in_edge(Source.Dept = 'Marketing')
        /// </code>
        /// 
        /// If we'd like to explicitly exclude the Marketing department by adding an extra condition.
        /// <code>
        /// SELECT * WHERE in_edge(Source.Dept = 'Marketing') AND Dept != 'Marketing'
        /// </code>
        /// 
        /// We can also limit results to people that often reach out for knowledge witn <samp>out_edge</samp> keyword,
        /// that matches only outgoing relationships
        /// <code>
        /// SELECT * WHERE out_edge(Name = 'Knowledge' AND Frequency > 3)
        /// </code>
        /// 
        /// The last edge traversal operator is <samp>mutual_edge</samp> and it matches nodes connected with
        /// mutual relationship.
        /// <code>
        /// SELECT * WHERE mutual_edge(name='Cooperation')
        /// </code>
        ///
        /// Query language supports calculating basic graph metrics such as:
        /// <ul>
        ///     <li>degree[(edge_condition)]</li>
        ///     <li>in_degree[(edge_condition)]</li>
        ///     <li>out_degree[(edge_condition)]</li>
        ///     <li>eigenvector[(edge_condition, edge_dist_prop)]</li>
        ///     <li>betweeness[(edge_condition, edge_dist_prop)]</li>
        /// 
        ///     <li>path_length(target_condition, [edge_condition], [path_dist_prop])</li>
        ///     <li>shortest_path(source_condition, target_condition)</li>
        /// </ul>
        /// 
        /// A simple query that will calculate indegree looks as follows 
        /// <code>
        /// SELECT * CALCULATE indegree
        /// </code>
        /// 
        /// A query that will calculate degree for a particular edge and out_degree for all edges
        /// <code>
        /// SELECT * CALCULATE degree(Name = "Cooperation"), out_degree
        /// </code>
        /// 
        /// This query will return a shortest path between two nodes a and b 
        /// the result will contain all vertices and edges along the path sorted accoring to order in the path
        /// <code>
        /// SELECT * CALCULATE shortest_path(Id='a', Id='b')
        /// </code>
        /// 
        /// </remarks>
        [Route("{networkId}/query")]
        [SwaggerResponse(HttpStatusCode.OK, "Success", typeof(PropertyGraphModel))]
        public async Task<IHttpActionResult> Query([FromBody]string query, Guid networkId)
        {
            try
            {
                //if (networkId == null && perspectiveId == null) return BadRequest("networkId or perspectiveId is required");
                //networkId = networkId ?? (await _repo.Db.Perspectives.Where(p => p.Id == perspectiveId.Value).FirstAsync()).NetworkId;

                var graphModel = await _queryService.Execute(new GraphQuery()
                {
                    NetworkId = networkId,              
                    QueryText = query
                }, User);
                return Ok(graphModel);
            }
            catch (GraphQuerySyntaxError error)
            {
                return BadRequest(error.Message);
            }            
        }

        /// <summary>
        /// Sigma
        /// </summary>
        /// <remarks>
        /// For query syntax see method above (Query)
        /// </remarks>
        [Route("{networkId}/sigma")]
        [SwaggerResponse(HttpStatusCode.OK, "Success", typeof(SigmaGraphModel))]
        public async Task<IHttpActionResult> Sigma([FromBody]string query, Guid networkId)
        {
            try
            {
                var graphModel = await _queryService.Execute(new SigmaQuery()
                {
                    NetworkId = networkId,
                    DoLayout = true,
                    QueryText = query
                }, User);
                return Ok(graphModel);
            }
            catch (GraphQuerySyntaxError error)
            {
                return BadRequest(error.Message);
            }
        }


        /// <summary>
        /// Uri query
        /// </summary>
        [Route("{networkId}/uri/{*uri}")]
        [SwaggerResponse(HttpStatusCode.OK, "Success", typeof(SigmaGraphModel))]
        public async Task<IHttpActionResult> Uri(Guid networkId, string uri, [FromBody] Dictionary<string, object> parameters, [FromUri] TimeSpan? cache = null)
        {
            try
            {
                var graphModel = await _queryService.Execute(new UriQuery()
                {
                    NetworkId = networkId,
                    DoLayout = true,
                    Uri = uri,
                    Parameters = parameters,
                    Cache = cache
                }, User);
                return Ok(graphModel);
            }
            catch (GraphQuerySyntaxError error)
            {
                return BadRequest(error.Message);
            }
        }

        /// <summary>
        /// Do layout
        /// </summary>
        /// <remarks>
        /// For query syntax see method above (Query)
        /// </remarks>
        [Route("{networkId}/do-layout")]
        [SwaggerResponse(HttpStatusCode.OK, "Success")]
        [HttpPut]
        public async Task<IHttpActionResult> DoLayout([FromBody]string query, Guid networkId, int durationMs)
        {
            try
            {
                await _queryService.Execute(new SigmaQuery()
                {
                    NetworkId = networkId,
                    DoLayout = true,
                    DurationMs = durationMs,
                    QueryText = query
                }, User);
                return Ok();
            }
            catch (GraphQuerySyntaxError error)
            {
                return BadRequest(error.Message);
            }
        }

        /// <summary>
        /// Save layout
        /// </summary>
        [Route("{networkId}/layout")]
        [HttpPost]
        [SwaggerResponse(HttpStatusCode.OK, "Success")]
        public async Task<IHttpActionResult> SaveLayout([FromBody]GraphLayout layout, Guid networkId, string key = null)
        {
            await _dispatcher.Execute(new SaveLayoutCommand()
            {
                NetworkId = networkId,
                Key = key,
                GraphLayout = layout
            }, User);
            return Ok();
        }

        /// <summary>
        /// Vertex
        /// </summary>        
        /// <remarks>
        /// use "me" as vertex Id to query own vertex
        /// </remarks>
        [Route("{networkId}/vertices/{vertexId}")]
        [SwaggerResponse(HttpStatusCode.OK, "Success", typeof(PropertyVertexModel))]
        [SwaggerResponse(HttpStatusCode.NotFound, "Vertex not found")]
        [HttpGet]
        public async Task<IHttpActionResult> Vertex(Guid networkId, string vertexId)
        {
            var query = new VertexQuery()
            {
                NetworkId = networkId,
                VertexId = vertexId                
            };
            try
            {
                var result = await _queryService.Execute(query, this.User);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();                
            }
        }

        /// <summary>
        /// Merge duplicates
        /// </summary>
        [Route("{networkId}/duplicates")]
        [HttpPost]
        [SwaggerResponse(HttpStatusCode.OK, "Success")]
        public async Task<IHttpActionResult> MergeDuplicates(Guid networkId, Guid vertexId, Guid duplicateId)
        {
            await _dispatcher.Execute(new MergeDuplicatesCommand()
            {
                NetworkId = networkId,
                VertexId = vertexId,
                DuplicateId = duplicateId
            }, User);
            return Ok();
        }

        /// <summary>
        /// List duplicates
        /// </summary>
        [Route("{networkId}/duplicates")]
        [HttpGet]
        [SwaggerResponse(HttpStatusCode.OK, "Success", typeof(List<DuplicateResult>))]
        public async Task<IHttpActionResult> ListDuplicates(Guid networkId)
        {
            var result = await _queryService.Execute(new DuplicatesQuery() 
            {
                NetworkId = networkId,
            }, User);
            return Ok(result);
        }

        /// <summary>
        /// Clear cache
        /// </summary>
        [Route("{networkId}/cache")]
        [SwaggerResponse(HttpStatusCode.OK, "Success")]
        [HttpDelete]
        public async Task<IHttpActionResult> ClearCache(Guid networkId)
        {
            await _dispatcher.Execute(new ClearNetworkCacheCommand()
            {
                NetworkId = networkId
            }, User);
            return Ok();
        }
    }


}