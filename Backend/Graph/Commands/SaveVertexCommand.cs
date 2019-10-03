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
    public class SaveVertexCommand : ICommand
    {
        public Guid VertexId { get; set; }
        public IEnumerable<PropertyDescription> Props { get; set; }
    }

    [DispatchAfterCommit]
    public class SaveVertexCompletedEvent : IEvent
    {
        public SaveVertexCommand Command { get; set; }
    }

    public class SaveVertexCommandHandler : SecureCommandHandler<SaveVertexCommand>
    {
        private readonly IClock _clock;        

        public SaveVertexCommandHandler(IClock clock)
        {
            _clock = clock;
        }

        public override void Authorize(SaveVertexCommand cmd)
        {
            AssertAlreadyAuthorized();
        }

        public async override Task Execute(SaveVertexCommand cmd)
        {
            var vertexProps = await UnitOfWork.Db.VertexProperties.Where(
                p => p.VertexId == cmd.VertexId && p.Deleted == null).ToListAsync();

            foreach (var propModel in cmd.Props)
            {
                // close old value if present
                var prop = vertexProps.FirstOrDefault(p => p.SchemaUri == propModel.SchemaUri);

                // serialize value to json
                var jsonValue = JsonConvention.SerializeObject(propModel.Value);

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

                // if value is null do not add anything
                if (propModel.Value == null) continue;

                // create property
                var newProp = new VertexProperty()
                {
                    Id = Guid.NewGuid(),
                    VertexId = cmd.VertexId,
                    SchemaUri = propModel.SchemaUri,
                    Created = _clock.TimeStamp,
                    JsonValue = jsonValue
                };
                UnitOfWork.Db.VertexProperties.Add(newProp);

                // prevent inserting duplicates
                vertexProps.Add(newProp);
            }

            await Events.OnNext(new SaveVertexCompletedEvent()
            {
                Command = cmd
            });
        }
    }
}