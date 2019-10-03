using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;

namespace WebPerspective.Areas.Graph.Commands
{
    public class ClearNetworkCacheCommand : ICommand
    {
        public Guid NetworkId { get; set; }
    }
    
    [DispatchAfterCommit]
    public class ClearNetworkCacheEvent : IEvent
    {
        public Guid NetworkId { get; set; }
    }

    public class ClearNetworkCacheCommandHandler : SecureCommandHandler<ClearNetworkCacheCommand>
    {
        public override Task Execute(ClearNetworkCacheCommand command)
        {
            Events.OnNext(new ClearNetworkCacheEvent()
            {
                NetworkId = command.NetworkId
            });

            return Task.FromResult(0);
        }

        public override void Authorize(ClearNetworkCacheCommand cmd)
        {
            Auth.AssertPermission(Resources.AdminData, cmd.NetworkId);
        }
    }
}