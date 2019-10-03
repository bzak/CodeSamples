using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS.Commands;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Commands
{
    public class SaveLayoutCommand : ICommand
    {
        public Guid NetworkId { get; set; }
        public string Key { get; set; }
        public GraphLayout GraphLayout { get; set; }
    }

    public class SaveLayoutCommandHandler : SecureCommandHandler<SaveLayoutCommand>
    {
        public override async Task Execute(SaveLayoutCommand command)
        {
            var layout = await UnitOfWork.Db.Layouts.SingleOrDefaultAsync(l => l.NetworkId == command.NetworkId && l.Key == command.Key);
            if (layout == null)
            {
                layout = new Entities.Layout()
                {
                    Id = Guid.NewGuid(),
                    NetworkId = command.NetworkId,
                    Key = command.Key
                };
                UnitOfWork.Db.Layouts.Add(layout);
            }
            layout.GraphLayout = command.GraphLayout;
        }

        public override void Authorize(SaveLayoutCommand cmd)
        {
            Auth.AssertPermission(Resources.AdminGraph, cmd.NetworkId);
        }
    }
}