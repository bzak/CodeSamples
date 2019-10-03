using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NMemory.Modularity;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.Commons.Extensions;
using WebPerspective.CQRS.Queries;
using WebPerspective.Tests.Fakes;

namespace WebPerspective.Tests.Areas.Graph
{
    [TestClass]
    public class GraphQueryTest
    {
        private Fake _fake;
        private string _graph;
        private GraphQueryHandler _handler;

        [TestInitialize]
        public void Setup()
        {
            _fake = new Fake();
            var mocker = _fake.CreateMocker();

            var queryService = new Mock<IQueryService>();
            mocker.Use<IQueryService>(queryService);
            queryService.Setup(q => q.Execute(It.IsAny<InternalGraphQuery>(), It.IsAny<IQueryExecutionContext>()))
                .Returns(() =>
                {
                    var graph = JsonConvention.DeserializeObject<PropertyGraphModel>(_graph);
                    _graph = JsonConvention.SerializeObject(graph);
                    foreach (var vertex in graph.Vertices)
                    {
                        vertex.Edges = new List<PropertyEdgeModel>();
                        foreach (var key in vertex.Props.Keys.ToList())
                        {
                            if (vertex.Props[key] is JArray)
                                vertex.Props[key] = (vertex.Props[key] as JArray).ToObject<object[]>();
                        }
                        vertex.Props = new Dictionary<string, object>(vertex.Props, StringComparer.OrdinalIgnoreCase);    
                        
                    }

                    graph.CreateLinks();

                    return Task.FromResult(new ConcurrentGraphModel(graph));
                });

            mocker.Use<IGraphQueryCompiler>(new GraphQueryCompiler());

            _handler = mocker.CreateInstance<GraphQueryHandler>();

            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";

        }


        [TestMethod]
        public async Task ShouldReturnWholeGraph()
        {
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText =  @"select *"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));            
            var expected = _graph;

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectVertexProp()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name]"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John'  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectVertexProps()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name], Dept"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectVertexPropsWithAlias()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name] as Label"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'Label':'John'  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'Label':'John'  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'Label':'Anna'  }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name], edge.Cooperation"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation'                         
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectAllEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name], edge.*"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectEdgesWithAlias()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name], edge.Cooperation AS Link"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Link'                         
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldSelectEdgeWithProps()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name], edge.Cooperation.Frequency"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }           
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldSelectEdgeWithPropsAndAlias()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge'
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select [First name], edge.Cooperation.Frequency AS Value"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Value : 3 }           
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesWithEqualsProp()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where [Last name] = 'Kowalski'"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldFilterVerticesWithNotNull()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak' }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where [Dept] != null"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterArraysWithEqualsProp()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Skill': ['A','B'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Skill': ['B','C'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Skill': ['C'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'First name':'Suzan', 'Last name':'Nowak' }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where [Skill] = 'B'"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Skill': ['A','B'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Skill': ['B','C'] }
                      },
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldFilterArraysWithNotNull()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Skill': ['A','B'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Skill': ['B','C'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Skill': ['C'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'First name':'Suzan', 'Last name':'Nowak' }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where [Skill] != null"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Skill': ['A','B'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Skill': ['B','C'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Skill': ['C'] }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesWithEqualsPropNumber()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where Dept = 2"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesWithNotEqual()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where Dept != 2"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesWithLike()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where (Dept = 1 OR [Last name] LIKE 'now%')"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesGreaterThan()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where (Dept > 1)"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesGreaterOrEqual()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where (Dept >= 1)"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesSmallerThan()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where (Dept < 2)"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesSmallerOrEqual()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where (Dept <= 2)"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesSmallerThenAndIgnoreCastErrors()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where ('First name' < 2)"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {                   
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterVerticesWithNotPresentProps()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where unexpected != 'unknown'"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = _graph;

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                // all people that are connected with cooperation edge (and return all edges and props)
                QueryText = @"select * where edge(Name = 'Cooperation')"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      }
                   ],
                    'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);            
        }


        [TestMethod]
        public async Task ShouldFilterEdges2()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                //all people that are connected with cooperation edge (and return only cooperation edges)
                QueryText = @"select edge.Cooperation where edge(Name = 'Cooperation')"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001'                         
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002'                         
                      }
                   ],
                    'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation'                         
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldFilterEdges3()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                //all people that are connected with cooperation edge (and return only cooperation edges)
                QueryText = @"select edge.[Cooperation] where edge(Name = 'Cooperation')"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001'                         
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002'                         
                      }
                   ],
                    'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation'                         
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterInEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                // all people that cooperate with dept 1
                QueryText = @"select * where in_edge(Name = 'Cooperation' AND Source.Dept = 1)"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterOutEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                // all people that cooperate with dept 1
                QueryText = @"select * where out_edge(Name = 'Knowledge' AND Source.Dept = 2)"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                       {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFilterMutualEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,                
                QueryText = @"select * where mutual_edge(Name = 'Knowledge')"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldFilterAnyEdges()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * where edge(any)"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },                      
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':1, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldCombineQueries()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select * where edge(any)\n"+
                            "select [First name], edge.Knowledge"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      },                      
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':1, 'Target':0,
                         'Name':'Knowledge'
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldQueryById()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select [First name] where id = '00000000-0000-0000-0000-000000000001'"                         

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John' }
                      } 
                   ] 
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldIgnorePropsCasing()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2 }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select [first NAME] where id = '00000000-0000-0000-0000-000000000001'"

            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'first NAME':'John' }
                      } 
                   ] 
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }



        [TestMethod]
        public async Task ShouldGroupVertices()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':'IT' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * GROUP BY Dept"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000000',
                         'Props':{ 'label':'IT', 'size': 1, 'members': [0] }
                      },
                      {
                         'Id':'00000001-0000-0000-0000-000000000000',
                         'Props':{ 'label':'Marketing', 'size': 2, 'members': [1,2] }
                      },
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props':{'size': 1, 'connectors': {'0':[1]} }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props':{'size': 1, 'connectors': {'0':[1]} }
                      },
                      {
                         'Source':1, 'Target':1,
                         'Name':'Knowledge',
                         'Props':{'size': 1, 'connectors': {'1':[2]} }
                      },
                      {
                         'Source':1, 'Target':0,
                         'Name':'Knowledge',
                         'Props':{'size': 1, 'connectors': {'2':[0]} }
                      }
                   ],
                   'Data': {
                      'grouped_vertices': [
                          {
                             'Id':'00000000-0000-0000-0000-000000000001',
                             'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':'IT' }
                          },
                          {
                             'Id':'00000000-0000-0000-0000-000000000002',
                             'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                          },
                          {
                             'Id':'00000000-0000-0000-0000-000000000003',
                             'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                          }
                       ]
                   }
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldGroupVerticesByLongPropName()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':'IT' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * GROUP BY [First name]"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000000',
                         'Props':{ 'label':'John', 'size': 2, 'members': [0,1] }
                      },
                      {
                         'Id':'00000001-0000-0000-0000-000000000000',
                         'Props':{ 'label':'Anna', 'size': 1, 'members': [2] }
                      },
                   ],
                   'Edges':[
                      {
			            'Name': 'Cooperation',
			            'Props': {
				            'connectors': {'0': [1] },
				            'size': 1
			            },
			            'Source': 0,
			            'Target': 0
		            }, {
			            'Name': 'Knowledge',
			            'Props': {
				            'connectors': { '0': [1] },
				            'size': 1
			            },
			            'Source': 0,
			            'Target': 0
		            }, {
			            'Name': 'Knowledge',
			            'Props': {
				            'connectors': { '1': [2] },
				            'size': 1
			            },
			            'Source': 0,
			            'Target': 1
		            }, {
			            'Name': 'Knowledge',
			            'Props': {
				            'connectors': { '2': [0] },
				            'size': 1
			            },
			            'Source': 1,
			            'Target': 0
		            }
                   ],
                   'Data': {
                      'grouped_vertices': [
                          {
                             'Id':'00000000-0000-0000-0000-000000000001',
                             'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':'IT' }
                          },
                          {
                             'Id':'00000000-0000-0000-0000-000000000002',
                             'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                          },
                          {
                             'Id':'00000000-0000-0000-0000-000000000003',
                             'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                          }
                       ]
                   }
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldGroupMultivalueVertices()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':['IT'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':['Marketing'] }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':1,
                         'Name':'Cooperation',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':0, 'Target':1,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':1, 'Target':2,
                         'Name':'Knowledge',
                         'Props': { Frequency : 3 }
                      },
                      {
                         'Source':2, 'Target':0,
                         'Name':'Knowledge',
                         'Props': { Frequency : 2 }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"select * GROUP BY Dept"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                  'Vertices': [
                    {
                      'Id': '00000000-0000-0000-0000-000000000000',
                      'Props': {
                        'label': 'IT',
                        'size': 1,
                        'members': [0]
                      }
                    },
                    {
                      'Id': '00000001-0000-0000-0000-000000000000',
                      'Props': {
                        'label': 'Marketing',
                        'size': 2,
                        'members': [ 1, 2 ]
                      }
                    },
                    {
                      'Id': '00000002-0000-0000-0000-000000000000',
                      'Props': {
                        'label': 'Legal',
                        'size': 1,
                        'members': [ 1 ]
                      }
                    }
                  ],
                  'Edges': [
                    {
                      'Source': 0,
                      'Target': 1,
                      'Name': 'Cooperation',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '0': [ 1 ]
                        }
                      }
                    },
                    {
                      'Source': 0,
                      'Target': 2,
                      'Name': 'Cooperation',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '0': [ 1 ]
                        }
                      }
                    },
                    {
                      'Source': 0,
                      'Target': 1,
                      'Name': 'Knowledge',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '0': [ 1 ]
                        }
                      }
                    },
                    {
                      'Source': 0,
                      'Target': 2,
                      'Name': 'Knowledge',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '0': [ 1 ]
                        }
                      }
                    },
                    {
                      'Source': 1,
                      'Target': 1,
                      'Name': 'Knowledge',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '1': [ 2 ]
                        }
                      }
                    },
                    {
                      'Source': 2,
                      'Target': 1,
                      'Name': 'Knowledge',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '1': [ 2 ]
                        }
                      }
                    },
                    {
                      'Source': 1,
                      'Target': 0,
                      'Name': 'Knowledge',
                      'Props': {
                        'size': 1,
                        'connectors': {
                          '2': [ 0 ]
                        }
                      }
                    }
                  ],
                  'Data': {
                    'grouped_vertices': [
                      {
                        'Id': '00000000-0000-0000-0000-000000000001',
                        'Props': {
                          'First name': 'John',
                          'Last name': 'Kowalski',
                          'Dept': [ 'IT' ]
                        }
                      },
                      {
                        'Id': '00000000-0000-0000-0000-000000000002',
                        'Props': {
                          'First name': 'John',
                          'Last name': 'Malkovitch',
                          'Dept': [ 'Marketing', 'Legal' ]
                        }
                      },
                      {
                        'Id': '00000000-0000-0000-0000-000000000003',
                        'Props': {
                          'First name': 'Anna',
                          'Last name': 'Nowak',
                          'Dept': [ 'Marketing' ]
                        }
                      }
                    ]
                  }
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }



        [TestMethod]
        public async Task ShouldIntersectWithArrayVertices()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':['IT'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':['Marketing'] }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select * WHERE Dept intersects '[\"Marketing\", \"Accounting\"]'"
            };            
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':['Marketing'] }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }

        [TestMethod]
        public async Task ShouldFallbackIntersectToEqualWhenNotArray()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':'IT' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select * WHERE Dept intersects 'Marketing'"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                       {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldFallbackIntersectToEqualWhenNotArray2()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':['IT'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':['Marketing'] }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select * WHERE Dept intersects 'Marketing'"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':['Marketing'] }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task ShouldFallbackIntersectToEqualWhenNotArray3()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':'IT' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                      }
                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select * WHERE Dept intersects '[\"Marketing\",\"Legal\"]'"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                       {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':'Marketing' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing' }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }




        [TestMethod]
        public async Task SelectUnion()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':['IT'], 'skill': ['java'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'], 'skill': ['a','b','c'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':['Marketing'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'Marketing', 'skill': ['a'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000005',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'a', 'skill': ['a','b'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000006',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'a', 'skill': 'b' }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000007',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':'a'  }
                      }

                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select (dept union skill) as expertise"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'expertise':['IT', 'java'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'expertise':['Marketing','Legal','a','b','c'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'expertise':['Marketing'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'expertise':['Marketing','a'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000005',
                         'Props':{ 'expertise':['a','b'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000006',
                         'Props':{ 'expertise':['a','b'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000007',
                         'Props':{ 'expertise':['a'] }
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }


        [TestMethod]
        public async Task SelectLike()
        {
            _graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':['IT'], 'skill': ['java'] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':['Marketing','Legal'], 'skill': ['a','b','c'] }
                      }

                   ]
                }
            ";
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = "select (dept like 'Mark%') as Dept"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'Dept':[] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'Dept':['Marketing']}
                      }
                   ]
                }
            ";

            PropertyGraphAssertions.AssertGraphsEqual(expected, result);
        }
    }

}





