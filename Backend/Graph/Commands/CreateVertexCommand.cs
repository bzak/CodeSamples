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
using WebPerspective.Areas.Roles.Models;
using WebPerspective.Commons.Extensions;
using WebPerspective.Commons.Services;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Commands
{
    public class CreateVertexCommand : ICommand
    {
        public Guid NetworkId { get; set; }
        public Guid VertexId { get; set; }
        public IEnumerable<PropertyDescription> Props { get; set; }
    }

    public class CreateVertexCommandHandler : SecureCommandHandler<CreateVertexCommand>
    {
        private readonly IClock _clock;        

        public CreateVertexCommandHandler(IClock clock)
        {
            _clock = clock;
        }

        public override void Authorize(CreateVertexCommand cmd)
        {
            AssertAlreadyAuthorized();
        }

        public async override Task Execute(CreateVertexCommand cmd)
        {            
            UnitOfWork.Db.Vertices.Add(new Vertex()
            {
                Id = cmd.VertexId,
                NetworkId = cmd.NetworkId,
                Created = _clock.TimeStamp
            });

            if (cmd.Props != null)
            {
                foreach (var propModel in cmd.Props)
                {

                    // serialize value to json
                    var jsonValue = JsonConvention.SerializeObject(propModel.Value);

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
                }
            }

            await Events.OnNext(new SaveVertexCompletedEvent()
            {
                Command = new SaveVertexCommand()
                {
                    VertexId = cmd.VertexId,
                    Props = cmd.Props
                }
            });
        }
    }
}