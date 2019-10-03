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
    /// Delete all associated accounts with a vertex
    /// </summary>
    public class DeleteVertexAccountsCommand : ICommand
    {
        public Guid VertexId { get; set; }
    }

    public class DeleteVertexAccountsCommandHandler : SecureCommandHandler<DeleteVertexAccountsCommand>
    {
        public override void Authorize(DeleteVertexAccountsCommand cmd)
        {
            AssertAlreadyAuthorized();
        }

        public override async Task Execute(DeleteVertexAccountsCommand command)
        {
            var accounts = await (
                from account in UnitOfWork.Db.Accounts
                join user in UnitOfWork.Db.Users on account.ApplicationUserId equals user.Id
                where account.VertexId == command.VertexId
                select account
            ).ToListAsync();

            UnitOfWork.Db.Accounts.RemoveRange(accounts);
        }
    }
}