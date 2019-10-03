using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Roles.Models;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.CQRS.Queries;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Layout
{
    public class ImproveLayoutCommand : ICommand
    {
        public LayoutTask Task { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ImproveLayoutCommandHandler : SecureCommandHandler<ImproveLayoutCommand>
    {
        private readonly IGraphLayoutService _layoutService;
        private readonly IQueryService _queryService;        

        public ImproveLayoutCommandHandler(IGraphLayoutService layoutService, IQueryService queryService)
        {
            _layoutService = layoutService;
            _queryService = queryService;
        }

        public override void Authorize(ImproveLayoutCommand cmd)
        {
            AssertPermission(Resources.BasicLogin, cmd.Task.Query.NetworkId);
        }

        public async override Task Execute(ImproveLayoutCommand command)
        {
            command.Task.Query.DoLayout = false;
            var graphModel = await _queryService.Execute(command.Task.Query, this);

            _layoutService.Auth = this.Auth;
            _layoutService.Background = null;            
            await _layoutService.LayoutGraph(graphModel, command.Task, command.Duration);
        }
    }
}