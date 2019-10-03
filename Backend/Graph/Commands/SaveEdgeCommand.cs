using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Commons.Extensions;
using WebPerspective.Commons.Services;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Commands
{
    public class SaveEdgeCommand : ICommand
    {
        public Guid SourceVertexId { get; set; }
        public Guid TargetVertexId { get; set; }
        public string SchemaUri { get; set; }
        public IEnumerable<PropertyDescription> Props { get; set; }
    }

    [DispatchAfterCommit]
    public class SaveEdgeCompletedEvent : IEvent
    {
        public SaveEdgeCommand Command { get; set; }
    }

    public class SaveEdgeCommandHandler : SecureCommandHandler<SaveEdgeCommand>
    {
        private readonly IClock _clock;        

        public SaveEdgeCommandHandler(IClock clock)
        {
            _clock = clock;
        }

        public override void Authorize(SaveEdgeCommand cmd)
        {
            AssertAlreadyAuthorized();
        }

        public async override Task Execute(SaveEdgeCommand cmd)
        {
            // is the relationship already there?
            var edge = await UnitOfWork.Db.Edges.Where(
                e => e.SourceVertexId == cmd.SourceVertexId && e.TargetVertexId == cmd.TargetVertexId
                     && e.SchemaUri == cmd.SchemaUri
                     && e.Deleted == null).FirstOrDefaultAsync();

            var edgeExists = (edge != null);
            if (!edgeExists)
            {
                // relationship not found - create edge
                edge = new Entities.Edge()
                {
                    Id = Guid.NewGuid(),
                    SourceVertexId = cmd.SourceVertexId,
                    TargetVertexId = cmd.TargetVertexId,
                    SchemaUri = cmd.SchemaUri,
                    Created = _clock.TimeStamp,
                };
                UnitOfWork.Db.Edges.Add(edge);
            }

            if (cmd.Props != null)
            {
                var edgeProps = await UnitOfWork.Db.EdgeProperties.Where(
                    p => p.EdgeId == edge.Id && p.Deleted == null)
                    .ToListAsync();

                foreach (var propModel in cmd.Props)
                {
                    var jsonValue = JsonConvention.SerializeObject(propModel.Value);

                    if (edgeExists)
                    {
                        // relationship found - find if the proeprty isn't already there
                        var prop = edgeProps.SingleOrDefault(p => p.SchemaUri == propModel.SchemaUri);

                        if (prop != null)
                        {
                            if (prop.JsonValue == jsonValue)
                            {
                                // nothing to update
                                continue;
                            }
                            else
                            {
                                // different value - close old property and continue
                                prop.Deleted = _clock.TimeStamp;
                            }
                        }
                    }

                    // if value is null do not add anything
                    if (propModel.Value == null) continue;

                    // create property
                    var newProp = new EdgeProperty()
                    {
                        Id = Guid.NewGuid(),
                        EdgeId = edge.Id,
                        SchemaUri = propModel.SchemaUri,
                        Created = _clock.TimeStamp,
                        JsonValue = jsonValue
                    };
                    UnitOfWork.Db.EdgeProperties.Add(newProp);

                    // prevent inserting duplicate props
                    edgeProps.Add(newProp);
                }
            }

            await Events.OnNext(new SaveEdgeCompletedEvent()
            {
                Command = cmd
            });
        }
    }
}