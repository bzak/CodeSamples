using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using WebPerspective.Commons.Services;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Commands
{
    public class DeleteEdgeCommand : ICommand
    {
        public Guid SourceVertexId { get; set; }
        public Guid TargetVertexId { get; set; }
        public List<string> SchemaUris { get; set; }
    }

    [DispatchAfterCommit]
    public class DeleteEdgeCompletedEvent : IEvent
    {
        public DeleteEdgeCommand Command { get; set; }
    }

    public class DeleteEdgeCommandHandler : SecureCommandHandler<DeleteEdgeCommand>
    {
        private readonly IClock _clock;        

        public DeleteEdgeCommandHandler(IClock clock)
        {
            _clock = clock;
        }

        public override void Authorize(DeleteEdgeCommand cmd)
        {
            AssertAlreadyAuthorized();
        }

        public async override Task Execute(DeleteEdgeCommand cmd)
        {
            // is the relationship already there?
            var edges = await UnitOfWork.Db.Edges.Where(
                e => e.SourceVertexId == cmd.SourceVertexId && e.TargetVertexId == cmd.TargetVertexId
                     && cmd.SchemaUris.Contains(e.SchemaUri)
                     && e.Deleted == null).ToListAsync();

            foreach (var edge in edges)
            {
                edge.Deleted = _clock.TimeStamp;
            }
            
            await Events.OnNext(new DeleteEdgeCompletedEvent()
            {
                Command = cmd
            });
        }
    }
}