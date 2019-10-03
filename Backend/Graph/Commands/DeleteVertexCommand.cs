using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Commons.Services;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;

namespace WebPerspective.Areas.Graph.Commands
{
    /// <summary>
    /// Delete vertex and all its edges
    /// </summary>
    public class DeleteVertexCommand : ICommand
    {
        public Guid VertexId { get; set; }
    }

    [DispatchAfterCommit]
    public class DeleteVertexCompletedEvent : IEvent
    {
        public DeleteVertexCommand Command { get; set; }
    }

    public class DeleteVertexCommandHandler : SecureCommandHandler<DeleteVertexCommand>
    {
        private readonly IClock _clock;        

        public DeleteVertexCommandHandler(IClock clock)
        {
            _clock = clock;
        }

        public override void Authorize(DeleteVertexCommand cmd)
        {
            AssertAlreadyAuthorized();
        }

        public override async Task Execute(DeleteVertexCommand command)
        {
            var vertex = await UnitOfWork.Db.Vertices.FirstAsync(v => v.Id == command.VertexId);

            var timestamp = _clock.TimeStamp;
            
            // mark connected edges as deleted
            var vertexEdges = await UnitOfWork.Db.Edges.Where(e => e.SourceVertexId == command.VertexId || e.TargetVertexId == command.VertexId).ToListAsync();
            foreach (var edge in vertexEdges)
            {
                edge.Deleted = timestamp;
            }

            // mark vertex as deleted
            vertex.Deleted = timestamp;

            await Events.OnNext(new DeleteVertexCompletedEvent()
            {
                Command = command
            });            
        }
    }
}