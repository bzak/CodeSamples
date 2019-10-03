using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Queries;
using WebPerspective.Entities;
using WebPerspective.Tests.Fakes;

namespace WebPerspective.Tests.Areas.Graph
{
    [TestClass]
    public class InternalGraphQueryTest
    {
        private InternalGraphQueryHandler _handler;
        private FakeRepository _repo;
        private Fake _fake;
        private GraphSchemaModel _graphSchema;

        [TestInitialize]
        public void Setup()
        {
            _fake = new Fake();

            var mocker = _fake.CreateMocker();
            _repo = mocker.CreateInstance<FakeRepository>();
            mocker.Use<IRepository>(_repo);
            mocker.Use<IParallelRepository>(_repo);
            mocker.Use<IGraphBuilder>(new GraphBuilder());

            _graphSchema = new GraphSchemaModel()
            {
                VertexSchema = new List<GraphProfileSectionModel>(),
                EdgeSchema = new List<GraphRelationshipSchemaModel>()
            };

            var queryService = new Mock<IQueryService>();
            mocker.Use<IQueryService>(queryService);
            queryService.Setup(q => q.Execute(It.IsAny<SchemaQuery>(), It.IsAny<IQueryExecutionContext>()))
                .Returns(() => Task.FromResult(this._graphSchema));

            queryService.Setup(q => q.Execute(It.IsAny<InternalGraphVerticesQuery>(), It.IsAny<IQueryExecutionContext>()))
                .Returns<InternalGraphVerticesQuery, IQueryExecutionContext>(
                    (query, user) => new InternalGraphVerticesQueryHandler(_repo).Execute(query));

            queryService.Setup(q => q.Execute(It.IsAny<InternalGraphEdgesQuery>(), It.IsAny<IQueryExecutionContext>()))
                .Returns<InternalGraphEdgesQuery, IQueryExecutionContext>(
                    (query, user) => new InternalGraphEdgesQueryHandler(_repo).Execute(query));

            _handler = mocker.CreateInstance<InternalGraphQueryHandler>();
            
        }

        [TestMethod]
        public async Task ShouldReturnEmptyVertices()
        {
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,                
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }

        [TestMethod]
        public async Task ShouldReturnVerticesWithProps()
        {
            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"Jan\"",
                SchemaUri = "first_name"
            });

            _repo.Db.SaveChanges();

            _graphSchema.VertexSchema.Add(new GraphProfileSectionModel()
            {
                Name = "Wizytowka",
                Uri = "wizytowka",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "first_name"
                    }
                }
            });

            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel()
                    {
                        Id = _fake.AdminVertex.Id,
                        Props = new Dictionary<string, object>()
                        {
                            { "First name", "Jan"}                         
                        }
                    },
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }

        [TestMethod]
        public async Task ShouldReturnVerticesWithPropsAndIgnoreRedundantSchema()
        {
            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"Jan\"",
                SchemaUri = "first_name"
            });

            _repo.Db.SaveChanges();

            _graphSchema.VertexSchema.Add(new GraphProfileSectionModel()
            {
                Name = "Wizytowka",
                Uri = "wizytowka",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "first_name"
                    },
                    new GraphPropertySchemaModel()
                    {
                        Name = "Last name",
                        Uri = "last_name"
                    }
                }
            });

            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel()
                    {
                        Id = _fake.AdminVertex.Id,
                        Props = new Dictionary<string, object>()
                        {
                            { "First name", "Jan"}
                        }
                    },
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }

        [TestMethod]
        public async Task ShouldExtractNameFromVertexPropsOutsideSchema()
        {
            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"Jan\"",
                SchemaUri = "first_name"
            });

            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"Kowalski\"",
                SchemaUri = "Last name"
            });

            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"Marketing\"",
                SchemaUri = "e8fc9157-c17e-49d8-b62b-1a0c6000955d/Dział"
            });

            _repo.Db.SaveChanges();

            _graphSchema.VertexSchema.Add(new GraphProfileSectionModel()
            {
                Name = "Wizytowka",
                Uri = "wizytowka",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "first_name"
                    }
                }
            });

            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel()
                    {
                        Id = _fake.AdminVertex.Id,
                        Props = new Dictionary<string, object>()
                        {
                            { "First name", "Jan"},
                            { "Last name", "Kowalski"},
                            { "Dział", "Marketing"}
                        }
                    },
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }

        [TestMethod]
        public async Task ShouldReturnPropValuesFirstFoundInSchemaUri()
        {
            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"John\"",
                SchemaUri = "/connector/first_name"
            });

            _repo.Db.VertexProperties.Add(new VertexProperty()
            {
                VertexId = _fake.AdminVertex.Id,
                Created = _fake.Clock.TimeStamp,
                Id = Guid.NewGuid(),
                JsonValue = "\"Jan\"",
                SchemaUri = "/survey/first_name"
            });

            _repo.Db.SaveChanges();

            _graphSchema.VertexSchema.Add(new GraphProfileSectionModel()
            {
                Name = "Wizytowka",
                Uri = "wizytowka",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "/survey/first_name"
                    },
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "/connector/first_name"
                    }
                }
            });

            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel()
                    {
                        Id = _fake.AdminVertex.Id,
                        Props = new Dictionary<string, object>()
                        {
                            { "First name", "Jan"}
                        }
                    },
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());

            _graphSchema.VertexSchema[0]
                .PropsSchema = new List<GraphPropertySchemaModel>()
                {   // order matters
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "/connector/first_name"
                    },
                    new GraphPropertySchemaModel()
                    {
                        Name = "First name",
                        Uri = "/survey/first_name"
                    },
                };
            expected.Vertices.First().Props = new Dictionary<string, object>() {{"First name", "John"}};
        }

        [TestMethod]
        public async Task ShouldReturnVerticesAndEdges()
        {
            var edge = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = _fake.AdminVertex.Id,
                TargetVertexId = _fake.OtherVertex.Id,
                Created = DateTime.Now,
                SchemaUri = "wspolpraca"
            };
            _repo.Db.Edges.Add(edge);

            _repo.Db.SaveChanges();

            _graphSchema.EdgeSchema.Add(new GraphRelationshipSchemaModel()
            {
                Name = "Współpraca",
                Uri = "wspolpraca"
            });
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
                {
                    new PropertyEdgeModel()
                    {
                        Name = "Współpraca",
                        Source = 0,
                        Target = 1
                    }
                }
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }


        [TestMethod]
        public async Task ShouldReturnEdgeProps()
        {
            var edge = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = _fake.AdminVertex.Id,
                TargetVertexId = _fake.OtherVertex.Id,
                Created = DateTime.Now,
                SchemaUri = "wspolpraca"
            };
            _repo.Db.Edges.Add(edge);

            _repo.Db.EdgeProperties.Add(new EdgeProperty()
            {
                Id = Guid.NewGuid(),
                EdgeId = edge.Id,
                SchemaUri = "wspolpraca/value",
                JsonValue = "5",
                Created = DateTime.Now,
                Deleted = null
            });

            _repo.Db.SaveChanges();

            _graphSchema.EdgeSchema.Add(new GraphRelationshipSchemaModel()
            {
                Name = "Współpraca",
                Uri = "wspolpraca",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "Value",
                        Uri = "wspolpraca/value"
                    }
                }
            });
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
                {
                    new PropertyEdgeModel()
                    {
                        Name = "Współpraca",
                        Source = 0,
                        Target = 1,
                        Props = new Dictionary<string, object>() { { "Value", 5 } }
                    }
                }
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());            
        }

        [TestMethod]
        public async Task ShouldReturnEdgePropsAndIgnoreRedundantSchema()
        {
            var edge = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = _fake.AdminVertex.Id,
                TargetVertexId = _fake.OtherVertex.Id,
                Created = DateTime.Now,
                SchemaUri = "wspolpraca"
            };
            _repo.Db.Edges.Add(edge);

            _repo.Db.EdgeProperties.Add(new EdgeProperty()
            {
                Id = Guid.NewGuid(),
                EdgeId = edge.Id,
                SchemaUri = "wspolpraca/value",
                JsonValue = "5",
                Created = DateTime.Now,
                Deleted = null
            });

            _repo.Db.SaveChanges();

            _graphSchema.EdgeSchema.Add(new GraphRelationshipSchemaModel()
            {
                Name = "Współpraca",
                Uri = "wspolpraca",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "Value",
                        Uri = "wspolpraca/value"
                    },
                    new GraphPropertySchemaModel()
                    {
                        Name = "Frequency",
                        Uri = "wspolpraca/frequency"
                    }
                }
            });
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
                {
                    new PropertyEdgeModel()
                    {
                        Name = "Współpraca",
                        Source = 0,
                        Target = 1,
                        Props = new Dictionary<string, object>() { { "Value", 5 } }
                    }
                }
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }

        [TestMethod]
        public async Task ShouldReturnEdgePropsFirstFoundInSchema()
        {
            var edge = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = _fake.AdminVertex.Id,
                TargetVertexId = _fake.OtherVertex.Id,
                Created = DateTime.Now,
                SchemaUri = "wspolpraca"
            };
            _repo.Db.Edges.Add(edge);

            _repo.Db.EdgeProperties.Add(new EdgeProperty()
            {
                Id = Guid.NewGuid(),
                EdgeId = edge.Id,
                SchemaUri = "survey/wspolpraca/value",
                JsonValue = "5",
                Created = DateTime.Now,
                Deleted = null
            });

            _repo.Db.EdgeProperties.Add(new EdgeProperty()
            {
                Id = Guid.NewGuid(),
                EdgeId = edge.Id,
                SchemaUri = "connector/wspolpraca/value",
                JsonValue = "0",
                Created = DateTime.Now,
                Deleted = null
            });

            _repo.Db.SaveChanges();

            _graphSchema.EdgeSchema.Add(new GraphRelationshipSchemaModel()
            {
                Name = "Współpraca",
                Uri = "wspolpraca",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "Value",
                        Uri = "survey/wspolpraca/value"
                    },
                    new GraphPropertySchemaModel()
                    {
                        Name = "Value",
                        Uri = "connector/wspolpraca/value"
                    }
                }
            });
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
                {
                    new PropertyEdgeModel()
                    {
                        Name = "Współpraca",
                        Source = 0,
                        Target = 1,
                        Props = new Dictionary<string, object>() { { "Value", 5 } }
                    }
                }
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());

            _graphSchema.EdgeSchema[0].PropsSchema = new List<GraphPropertySchemaModel>()
            {
                new GraphPropertySchemaModel()
                {
                    Name = "Value",
                    Uri = "connector/wspolpraca/value"
                },
                new GraphPropertySchemaModel()
                {
                    Name = "Value",
                    Uri = "survey/wspolpraca/value"
                }
            };

            expected.Edges.First().Props = new Dictionary<string, object>() {{"Value", 0}};
        }

        [TestMethod]
        public async Task ShouldSkipEdgesOutsideSchema()
        {
            var edge = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = _fake.AdminVertex.Id,
                TargetVertexId = _fake.OtherVertex.Id,
                Created = DateTime.Now,
                SchemaUri = "wiedza"
            };
            _repo.Db.Edges.Add(edge);
            
            _repo.Db.SaveChanges();

            _graphSchema.EdgeSchema.Add(new GraphRelationshipSchemaModel()
            {
                Name = "Współpraca",
                Uri = "wspolpraca",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "Value",
                        Uri = "wspolpraca/value"
                    }
                }
            });
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }

        [TestMethod]
        public async Task ShouldSkipEdgePropsOutsideSchema()
        {
            var edge = new Edge()
            {
                Id = Guid.NewGuid(),
                SourceVertexId = _fake.AdminVertex.Id,
                TargetVertexId = _fake.OtherVertex.Id,
                Created = DateTime.Now,
                SchemaUri = "wspolpraca"
            };
            _repo.Db.Edges.Add(edge);

            _repo.Db.EdgeProperties.Add(new EdgeProperty()
            {
                Id = Guid.NewGuid(),
                EdgeId = edge.Id,
                SchemaUri = "wspolpraca/value",
                JsonValue = "5",
                Created = DateTime.Now,
                Deleted = null
            });

            _repo.Db.EdgeProperties.Add(new EdgeProperty()
            {
                Id = Guid.NewGuid(),
                EdgeId = edge.Id,
                SchemaUri = "unexpected",
                JsonValue = "false",
                Created = DateTime.Now,
                Deleted = null
            });

            _repo.Db.SaveChanges();

            _graphSchema.EdgeSchema.Add(new GraphRelationshipSchemaModel()
            {
                Name = "Współpraca",
                Uri = "wspolpraca",
                PropsSchema = new List<GraphPropertySchemaModel>()
                {
                    new GraphPropertySchemaModel()
                    {
                        Name = "Value",
                        Uri = "wspolpraca/value"
                    }
                }
            });
            var query = new InternalGraphQuery()
            {
                NetworkId = _fake.Network.Id,
            };
            var result = await _handler.Execute(query);

            var expected = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>()
                {
                    new PropertyVertexModel() {Id = _fake.AdminVertex.Id},
                    new PropertyVertexModel() {Id = _fake.OtherVertex.Id},
                },
                Edges = new List<PropertyEdgeModel>()
                {
                    new PropertyEdgeModel()
                    {
                        Name = "Współpraca",
                        Source = 0,
                        Target = 1,
                        Props = new Dictionary<string, object>() { { "Value", 5 } }
                    }
                }
            };

            PropertyGraphAssertions.AssertGraphsEqual(expected, result.Snapshot());
        }
    }
}
