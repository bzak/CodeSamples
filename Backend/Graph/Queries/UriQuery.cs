using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Perspectives.Models;
using WebPerspective.Areas.Perspectives.Queries;
using WebPerspective.Areas.Perspectives.Services;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Queries
{
    public class UriQuery : IQuery<SigmaGraphModel>
    {
        [Required]
        public Guid NetworkId { get; set; }
        
        public string Uri { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public TimeSpan? Cache { get; set; }

        public bool DoLayout { get; set; }
        public string LayoutKey { get; set; }
        public int? DurationMs { get; set; }
    }

    public class UriQueryHandler : SecureQueryHandler<UriQuery, SigmaGraphModel>
    {
        public static readonly ISet<string> QUERY_CLAUSES = new HashSet<string>(new string[] { "select", "where", "calculate", "group", "layout" }).ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

        private readonly IQueryService _queryService;
        private readonly IPerspectiveNamingConvention _perspectiveNamingConvention;
        private readonly ILog _log;

        public UriQueryHandler(IQueryService queryService, IPerspectiveNamingConvention perspectiveNamingConvention, ILog log)
        {
            _queryService = queryService;
            _perspectiveNamingConvention = perspectiveNamingConvention;
            _log = log;
        }

        public override async Task<SigmaGraphModel> Execute(UriQuery query)
        {
            // find query text
            var uri = _perspectiveNamingConvention.Decompose(query.Uri);
            var schema = await _queryService.Execute(new PerspectiveSchemaQuery()
            {
                PerspectiveId = uri.PerspectiveId,
                Substitutions = query.Parameters,
                UserName = Auth.UserName
            }, this);

            if (uri.Page == null || uri.ComponentId == null) return null;
            
            var statements = uri.Section.HasValue 
                ? schema.Pages[uri.Page.Value].Sections[uri.Section.Value].Statements 
                : schema.Pages[uri.Page.Value].Statements;

            ComponentSchema graphComponent = null;            
            foreach (var statement in statements)
            {
                if (
                    statement.Component?.Statements?
                        .FirstOrDefault(
                            s => s.Assignement?.Name == "Uri" 
                            && ((string) s.Assignement?.Value) == uri.ComponentId) != null)
                {
                    graphComponent = statement.Component;
                }
            }

            if (graphComponent == null) return null;            

            var graphQuery = new StringBuilder();
            foreach (var statement in graphComponent.Statements)
            {
                if (statement.Assignement != null && QUERY_CLAUSES.Contains(statement.Assignement.Name))
                {
                    graphQuery.Append(statement.Assignement.Name.ToUpper());
                    graphQuery.Append(" ");
                    graphQuery.Append(statement.Assignement.Value);
                    graphQuery.Append("\n");
                }
            }
            _log.Debug(graphQuery);

            // query graph
            var graphModel = await _queryService.Execute(new SigmaQuery()
            {
                NetworkId = query.NetworkId,
                DoLayout = true,
                QueryText = graphQuery.ToString(),
                Cache = query.Cache
            }, this);

            return graphModel;
        }

        public override void Authorize(UriQuery query)
        {
            Auth.AssertPermission(Resources.BasicLogin, query.NetworkId);
        }
    }
}