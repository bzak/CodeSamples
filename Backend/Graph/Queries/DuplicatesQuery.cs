using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.Areas.Settings.Queries;
using WebPerspective.Commons.Extensions;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Queries;

namespace WebPerspective.Areas.Graph.Queries
{
    public class DuplicatesQuery : IQuery<List<DuplicateResult>>
    {
        public Guid NetworkId { get; set; }
    }

    public class DuplicateResult
    {
        public PropertyVertexModel Vertex { get; set; }
        public List<PropertyVertexModel> Duplicates { get; set; } 
    }

    public class DuplicatesQueryHandler : SecureQueryHandler<DuplicatesQuery, List<DuplicateResult>>
    {
        private readonly IQueryService _queryService;
        private readonly IRepository _repo;

        public DuplicatesQueryHandler(IQueryService queryService, IRepository repo)
        {
            _queryService = queryService;
            _repo = repo;
        }

        private class Duplicate
        {
            public double Score { get; set; }
            public Guid[] Key { get; set; }
            public IEnumerable<PropertyVertexModel> Vertices { get; set; } 
        }

        

        public override async Task<List<DuplicateResult>> Execute(DuplicatesQuery query)
        {
            var graph = (await _queryService.Execute(new InternalGraphQuery() { NetworkId = query.NetworkId }, this))
                .Snapshot();
            var settings = await _queryService.Execute(new NetworkSettingsQuery() { NetworkId = query.NetworkId }, this);
            var requiredProps = settings.Invitations.RequiredProps;

            var duplicatesByRequiredProps =
                graph.Vertices.Select(
                    v => new {
                            Key = string.Join("::", requiredProps.Select(prop => v.Props.ContainsKey(prop) ? JsonConvention.SerializeObject(v.Props[prop]) : "")),
                            Vertex = v
                        })
                        .GroupBy(g=>g.Key)
                        .Where(g=>g.Count(v=>true) > 1)
                        .Select(g=>new Duplicate()
                        {
                            Score = 1,
                            Key = g.Select(a => a.Vertex.Id).OrderBy(id=>id).ToArray(),
                            Vertices = g.Select(a=>a.Vertex)
                        });

            var labelProp = settings.ProfileCard.LabelProp;

            var duplicatesByLabel =
                graph.Vertices.Select(
                    v => new {
                        Label = v.Props.ContainsKey(labelProp) ? v.Props[labelProp].ToString().ToLowerInvariant().Trim() : "",
                        Vertex = v
                    })
                    .GroupBy(g => g.Label)
                    .Where(g => g.Count(v => true) > 1)
                    .Select(g => new Duplicate()
                    {
                        Score = 0.5,
                        Key = g.Select(a => a.Vertex.Id).OrderBy(id => id).ToArray(),
                        Vertices = g.Select(a => a.Vertex)
                    });

            var duplicates = duplicatesByRequiredProps.ToDictionary(d => d.Key, d => d, new ArrayEqualityComparer());

            foreach (var duplicate in duplicatesByLabel)
            {
                if (!duplicates.ContainsKey(duplicate.Key)) duplicates[duplicate.Key] = duplicate;
            }

            var accounts = await
                (from account in _repo.Db.Accounts
                            join user in _repo.Db.Users on account.ApplicationUserId equals user.Id
                            join vertex in _repo.Db.Vertices on account.VertexId equals vertex.Id
                            where vertex.NetworkId == query.NetworkId
                            group new { user.Email, account.InvitationDate } by vertex.Id into g
                            select new
                            {
                                VertexId = g.Key,
                                Accounts = g.ToList()
                            }
                ).ToDictionaryAsync(g => g.VertexId, g => g.Accounts.OrderBy(a=>a.InvitationDate));

            var result = new List<DuplicateResult>();
            foreach (var duplicate in duplicates.Values.OrderByDescending(d=>d.Score))
            {
                foreach (var vertex in duplicate.Vertices)
                {
                    vertex.Props = vertex.Props.ToProfileCardProps(settings.ProfileCard);
                    if (accounts.ContainsKey(vertex.Id))
                    {
                        vertex.Props["Account email"] = accounts[vertex.Id].First().Email;
                    }
                }
                var firstAccountVertex = duplicate.Vertices.Select(v => new
                {
                    Vertex = v,
                    InvitationDate =
                        accounts.ContainsKey(v.Id) ? accounts[v.Id].First().InvitationDate : DateTime.MaxValue
                }).OrderBy(a => a.InvitationDate).First();
                result.Add(new DuplicateResult()
                {
                    Vertex = firstAccountVertex.Vertex,
                    Duplicates = duplicate.Vertices.Where(v => v != firstAccountVertex.Vertex).ToList()
                });                                
            }

            return result;            
        }

        public override void Authorize(DuplicatesQuery query)
        {
            Auth.AssertPermission(Resources.AdminDuplicates, query.NetworkId);
        }



        private class ArrayEqualityComparer : IEqualityComparer<Guid[]>
        {
            public bool Equals(Guid[] x, Guid[] y)
            {
                if (x.Length != y.Length)
                {
                    return false;
                }
                for (int i = 0; i < x.Length; i++)
                {
                    if (x[i] != y[i])
                    {
                        return false;
                    }
                }
                return true;
            }

            public int GetHashCode(Guid[] obj)
            {
                int result = 17;
                foreach (var guid in obj)
                    foreach (var b in guid.ToByteArray())
                        result = result*23 + b;
                return result;
            }
        }
    }
}