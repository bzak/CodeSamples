using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.Commons.Services;
using WebPerspective.CQRS.Commands;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Commands
{
    /// <summary>
    /// Copy relationships from Duplictae to the Vertex 
    ///  - skip relationships the Vertex already has
    /// Mark duplicate as deleted
    /// Duplicate profile is not copied
    /// 
    /// Warning: command alters graph history
    /// </summary>
    public class MergeDuplicatesCommand : ICommand
    {
        public Guid NetworkId { get; set; }
        public Guid VertexId { get; set; }
        public Guid DuplicateId { get; set; }
    }

    public class MergeDuplicatesCommandHandler : SecureCommandHandler<MergeDuplicatesCommand>
    {
        private readonly IClock _clock;
        private readonly ILog _log;

        public MergeDuplicatesCommandHandler(IClock clock, ILog log)
        {
            _clock = clock;
            _log = log;
        }

        public override async Task Execute(MergeDuplicatesCommand cmd)
        {
            var vertex = await 
                ((from v in UnitOfWork.Db.Vertices
                  where v.Id == cmd.VertexId && v.NetworkId == cmd.NetworkId && v.Deleted == null
                  select v).FirstOrDefaultAsync());
            if (vertex == null) 
                throw new ArgumentException("Vertex not found");

            var duplicate = await 
                ((from v in UnitOfWork.Db.Vertices
                  where v.Id == cmd.DuplicateId  && v.NetworkId == cmd.NetworkId && v.Deleted == null
                  select v).FirstOrDefaultAsync());
            if (duplicate == null)
                throw new ArgumentException("Duplicate not found");

            _log.Info($"Merging {duplicate.Id} duplicate of {vertex.Id}");

            var vertexEdges = await 
                ((from edge in UnitOfWork.Db.Edges
                  where edge.SourceVertexId == cmd.VertexId || edge.TargetVertexId == cmd.VertexId
                  select edge).ToListAsync());
            var vertexEdgesSet = vertexEdges.Select(
                    e => new Tuple<Guid, Guid, string>(e.SourceVertexId,  e.TargetVertexId,  e.SchemaUri))
                .Distinct().ToImmutableHashSet();

            var duplicateEdges = await 
                ((from edge in UnitOfWork.Db.Edges
                  where edge.SourceVertexId == cmd.DuplicateId || edge.TargetVertexId == cmd.DuplicateId
                  select edge).ToListAsync());

            // relink relationships
            foreach (var edge in duplicateEdges)
            {                
                var relinkedEdge = new Tuple<Guid, Guid, string>(
                    edge.SourceVertexId == cmd.DuplicateId ? cmd.VertexId : edge.SourceVertexId,
                    edge.TargetVertexId == cmd.DuplicateId ? cmd.VertexId : edge.TargetVertexId,
                    edge.SchemaUri);

                if (vertexEdgesSet.Contains(relinkedEdge))
                {
                    // main vertex already has this edge - hence delete it
                    _log.Info($"Deleting edge {edge.Id}");
                    edge.Deleted = _clock.TimeStamp;
                }
                else if (relinkedEdge.Item1 == relinkedEdge.Item2)
                {
                    // edge would become a loop after merge - hence delete it
                    _log.Info($"Deleting edge {edge.Id} to prevent loops");
                    edge.Deleted = _clock.TimeStamp;
                }
                else
                {
                    _log.Info($"Relinking edge {edge.Id}");
                    if (edge.SourceVertexId == cmd.DuplicateId)
                    {
                        edge.SourceVertexId = cmd.VertexId;
                    }
                    else
                    {
                        edge.TargetVertexId = cmd.VertexId;
                    }                    
                }
            }            

            // delete duplicate vertex
            duplicate.Deleted = duplicate.Created;
            _log.Info($"Deleting vertex {duplicate.Id}");

            await Events.OnNext(new ClearNetworkCacheEvent() {NetworkId = cmd.NetworkId});
        }

        public override void Authorize(MergeDuplicatesCommand cmd)
        {
            AssertPermission(Resources.AdminData, cmd.NetworkId);
        }
    }
}