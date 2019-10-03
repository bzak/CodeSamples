using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebPerspective.Areas.Graph.Commands;
using WebPerspective.CQRS.Events;
using WebPerspective.Entities;
using WebPerspective.Tests.Fakes;

namespace WebPerspective.Tests.Areas.Graph
{
    [TestClass]
    public class MergeDuplicatesTest
    {
        private MergeDuplicatesCommandHandler _handler;
        private Fake _fake;
        private Vertex a, b, c;
        private ApplicationDbContext _db;

        [TestInitialize]
        public void Setup()
        {
            _fake = new Fake();
            var mocker = _fake.CreateMocker();
            _handler = new MergeDuplicatesCommandHandler(_fake.Clock, mocker.Get<ILog>());
            _handler.UnitOfWork = mocker.UnitOfWork();
            _handler.Events = new AnonymousAsyncObserver<IEvent>(@event => {return Task.FromResult(0);});
            _db = _handler.UnitOfWork.Db;

            a = _fake.AdminVertex;
            b = _fake.OtherVertex;
            c = new Vertex()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                Created = DateTime.UtcNow,
                NetworkId = _fake.Network.Id
            };
            _handler.UnitOfWork.Db.Vertices.Add(c);
        }

        [TestMethod]
        public async Task ShouldRelinkEdgesFromDuplicate()
        {
            // line a->b b->c
            var ab = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = b.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(ab);
            var bc = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = b.Id,
                TargetVertexId = c.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(bc);
            _db.SaveChanges();
            var cmd = new MergeDuplicatesCommand()
            {
                VertexId = a.Id,
                DuplicateId = b.Id,
                NetworkId = _fake.Network.Id
            };
            await _handler.Execute(cmd);
            _db.SaveChanges();

            // expected a->c
            Assert.AreEqual(1, _db.Edges.Count(e=>e.Deleted == null));
            Assert.AreEqual(a.Id, bc.SourceVertexId);
            Assert.IsNotNull(b.Deleted);
        }


        [TestMethod]
        public async Task ShouldRelinkEdgesToDuplicate()
        {
            // line a->b c->b
            var ab = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = b.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(ab);
            var cb = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = c.Id,
                TargetVertexId = b.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(cb);
            _db.SaveChanges();
            var cmd = new MergeDuplicatesCommand()
            {
                VertexId = a.Id,
                DuplicateId = b.Id,
                NetworkId = _fake.Network.Id
            };
            await _handler.Execute(cmd);
            _db.SaveChanges();

            // expected c->a
            Assert.AreEqual(1, _db.Edges.Count(e => e.Deleted == null));
            Assert.AreEqual(a.Id, cb.TargetVertexId);
            Assert.IsNotNull(ab.Deleted);
            Assert.IsNotNull(b.Deleted);
        }


        [TestMethod]
        public async Task ShouldNotCopyEdgesFromDuplicateIfItWouldCreateALoop()
        {
            // line a->b b->c b->a
            var ab = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = b.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(ab);

            var bc = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = b.Id,
                TargetVertexId = c.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(bc);

            var ba = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = b.Id,
                TargetVertexId = a.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(ba);

            _db.SaveChanges();
            var cmd = new MergeDuplicatesCommand()
            {
                VertexId = a.Id,
                DuplicateId = b.Id,
                NetworkId = _fake.Network.Id
            };
            await _handler.Execute(cmd);
            _db.SaveChanges();

            // expected a->c
            Assert.AreEqual(1, _db.Edges.Count(e => e.Deleted == null));
            Assert.AreEqual(a.Id, bc.SourceVertexId);
            Assert.IsNotNull(b.Deleted);
            Assert.IsNotNull(ba.Deleted);
        }

        [TestMethod]
        public async Task ShouldNotCopyEdgesFromDuplicateIfAlreadyPresentInVertex()
        {
            // triangle a->b & b->c & a->c (3->4 4->5 3->5)
            var ab = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = b.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(ab);

            var bc = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = b.Id,
                TargetVertexId = c.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            }; _db.Edges.Add(bc);

            var ac = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = c.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            }; _db.Edges.Add(ac);

            _db.SaveChanges();
            var cmd = new MergeDuplicatesCommand()
            {
                VertexId = a.Id,
                DuplicateId = b.Id,
                NetworkId = _fake.Network.Id
            };
            await _handler.Execute(cmd);
            _db.SaveChanges();

            // expected a->c 
            Assert.AreEqual(1, _db.Edges.Count(e => e.Deleted == null));
            Assert.IsNotNull(b.Deleted);
            Assert.IsNotNull(bc.Deleted);
        }


        [TestMethod]
        public async Task ShouldCopyEdgesFromDuplicateIfAlreadyPresentInVertexButDifferentSchema()
        {
            // triangle a->b & b->c & a->c (3->4 4->5 3->5)
            var ab = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = b.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            };
            _db.Edges.Add(ab);

            var bc = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = b.Id,
                TargetVertexId = c.Id,
                SchemaUri = "coop",
                Created = _fake.Clock.TimeStamp,
            }; _db.Edges.Add(bc);

            var ac = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = a.Id,
                TargetVertexId = c.Id,
                SchemaUri = "coop2",
                Created = _fake.Clock.TimeStamp,
            }; _db.Edges.Add(ac);

            _db.SaveChanges();
            var cmd = new MergeDuplicatesCommand()
            {
                VertexId = a.Id,
                DuplicateId = b.Id,
                NetworkId = _fake.Network.Id
            };
            await _handler.Execute(cmd);
            _db.SaveChanges();

            // expected triangle a->b  & a->c, bc deleted
            Assert.AreEqual(2, _db.Edges.Count(e => e.Deleted == null));
            Assert.IsNotNull(b.Deleted);
            Assert.IsNull(bc.Deleted);
            Assert.AreEqual(a.Id, bc.SourceVertexId);
        }
    }
}
