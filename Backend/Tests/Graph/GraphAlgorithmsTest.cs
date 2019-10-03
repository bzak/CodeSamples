using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Graph.Services;
using WebPerspective.CQRS.Queries;
using WebPerspective.Tests.Fakes;

namespace WebPerspective.Tests.Areas.Graph
{
    [TestClass]
    public class GraphAlgorithmsTest
    {
        private Fake _fake;
        private string _graph;
        private GraphQueryHandler _handler;
        private delegate void GraphInitialize(PropertyGraphModel graph);


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
                    var graph = JsonConvert.DeserializeObject<PropertyGraphModel>(_graph);
                    _graph = JsonConvert.SerializeObject(graph);
                    foreach (var vertex in graph.Vertices)
                    {
                        vertex.Edges = new List<PropertyEdgeModel>();
                        if (vertex.Props == null)
                            vertex.Props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        else
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
                         'Id':'00000000-0000-0000-0000-000000000001', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002', 'Props':{ } 
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000005', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000006', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000007', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000008', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000009', 'Props':{ }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000010', 'Props':{ }
                      }                   
                    ],
                   'Edges':[
                        { Source: 0, Target: 1 },
                        { Source: 0, Target: 6 },
                        { Source: 0, Target: 2 },
                        { Source: 0, Target: 3 },
                        { Source: 0, Target: 5 },
                        { Source: 0, Target: 4 },

                        { Source: 1, Target: 4 },
                        { Source: 1, Target: 6 },
                        { Source: 1, Target: 2 },
                        { Source: 1, Target: 0 },

                        { Source: 2, Target: 6 },
                        { Source: 2, Target: 7 },
                        { Source: 2, Target: 3 },
                        { Source: 2, Target: 0 },
                        { Source: 2, Target: 1 },

                        { Source: 3, Target: 7 },
                        { Source: 3, Target: 5 },
                        { Source: 3, Target: 4 },
                        { Source: 3, Target: 0 },
                        { Source: 3, Target: 2 },

                        { Source: 4, Target: 1 },
                        { Source: 4, Target: 0 },
                        { Source: 4, Target: 3 },
                        { Source: 4, Target: 5 },

                        { Source: 5, Target: 4 },
                        { Source: 5, Target: 0 },
                        { Source: 5, Target: 3 },

                        { Source: 6, Target: 1 },
                        { Source: 6, Target: 0 },
                        { Source: 6, Target: 2 },

                        { Source: 7, Target: 2 },
                        { Source: 7, Target: 3 },
                        { Source: 7, Target: 8 },

                        { Source: 8, Target: 7 },
                        { Source: 8, Target: 9 },

                        { Source: 9, Target: 8 }                     
                   ]
                }
            ";

        }


        [TestMethod]
        public async Task ShouldCalculateDegree()
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
                         'Source':0, 'Target':2,
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
                QueryText = @"SELECT * CALCULATE degree SELECT degree"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'degree': 3 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'degree': 1  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'degree': 2 }
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), result);
        }


        [TestMethod]
        public async Task ShouldCalculateDegreeWithFilter()
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
                         'Source':0, 'Target':2,
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
                QueryText = @"SELECT * CALCULATE degree(Name = 'Knowledge') SELECT degree"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'degree': 2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'degree': 0  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'degree': 2 }
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), result);
        }


        [TestMethod]
        public async Task ShouldCalculateInDegree()
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
                QueryText = @"SELECT * CALCULATE in_degree SELECT in_degree"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'in_degree': 1 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'in_degree': 2  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'in_degree': 0 }
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), result);
        }


        [TestMethod]
        public async Task ShouldCalculateOutDegree()
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
                QueryText = @"SELECT * CALCULATE out_degree SELECT out_degree"
            };
            var result = JsonConvert.SerializeObject(await _handler.Execute(query));

            var expected = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'out_degree': 2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'out_degree': 0  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'out_degree': 1 }
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), result);
        }



        [TestMethod]
        [ExpectedException(typeof(GraphQuerySyntaxError), "not sure if executing arbitrary code server side is safe. make sure it is disabled for now.")]
        public async Task ShouldNotCalculateJsScriptUntilWeKnowItIsSafe()
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
                QueryText = @"SELECT * CALCULATE js('graph.Data = graph.Vertices.Count')"
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
                   ],
                   Data:3
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), result);
        }


        [TestMethod]
        public async Task ShouldCalculateEigenvector()
        {
            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"SELECT * CALCULATE eigenvector"
            };
            var result = 
                (await _handler.Execute(query)).Vertices.Select(v=>(double)v.Props["eigenvector"]).ToArray();
            var expected = new[]
            {
                0.171,
                0.125,
                0.142,
                0.142,
                0.125,
                0.101,
                0.101,
                0.070,
                0.017,
                0.004
            };

            var maxValue = expected.Max();
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = Math.Round(100 * expected[i] / maxValue) / 100;
                result[i] = (double) Math.Round(100 * result[i]) / 100;
            }
            

            Assert.AreEqual(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(result));
        }


        [TestMethod]
        public async Task ShouldCalculateEigenvectorWithFilter()
        {
            _graph = @"
                {
                   'Vertices':[
                      {  'Id':'00000000-0000-0000-0000-000000000001' },
                      {  'Id':'00000000-0000-0000-0000-000000000002' },
                      {  'Id':'00000000-0000-0000-0000-000000000003' },
                      {  'Id':'00000000-0000-0000-0000-000000000004' },
                      {  'Id':'00000000-0000-0000-0000-000000000005' },
                      {  'Id':'00000000-0000-0000-0000-000000000006' },
                      {  'Id':'00000000-0000-0000-0000-000000000007' },
                      {  'Id':'00000000-0000-0000-0000-000000000008' },
                      {  'Id':'00000000-0000-0000-0000-000000000009' },
                      {  'Id':'00000000-0000-0000-0000-000000000010' }                      
                    ],
                   'Edges':[
                        { Source: 0, Target: 1 },
                        { Source: 0, Target: 6 },
                        { Source: 0, Target: 2 },
                        { Source: 0, Target: 3 },
                        { Source: 0, Target: 5 },
                        { Source: 0, Target: 4 },

                        { Source: 1, Target: 4 },
                        { Source: 1, Target: 6 },
                        { Source: 1, Target: 2 },
                        { Source: 1, Target: 0 },

                        { Source: 2, Target: 6 },
                        { Source: 2, Target: 7 },
                        { Source: 2, Target: 3 },
                        { Source: 2, Target: 0 },
                        { Source: 2, Target: 1 },

                        { Source: 3, Target: 7 },
                        { Source: 3, Target: 5 },
                        { Source: 3, Target: 4 },
                        { Source: 3, Target: 0 },
                        { Source: 3, Target: 2 },

                        { Source: 4, Target: 1 },
                        { Source: 4, Target: 0 },
                        { Source: 4, Target: 3 },
                        { Source: 4, Target: 5 },

                        { Source: 5, Target: 4 },
                        { Source: 5, Target: 0 },
                        { Source: 5, Target: 3 },

                        { Source: 6, Target: 1 },
                        { Source: 6, Target: 0 },
                        { Source: 6, Target: 2 },

                        { Source: 7, Target: 2 },
                        { Source: 7, Target: 3 },
                        { Source: 7, Target: 8 },

                        { Source: 8, Target: 7 },
                        { Source: 8, Target: 9 },

                        { Source: 9, Target: 8 },

                        { Source: 4, Target: 2, Name: 'Ignore' },
                        { Source: 5, Target: 2, Name: 'Ignore' },
                        { Source: 6, Target: 3, Name: 'Ignore' },
                        { Source: 7, Target: 4, Name: 'Ignore' },
                   ]
                }";

            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"SELECT * CALCULATE eigenvector(Name != 'Ignore', length)"
            };
            var result =
                (await _handler.Execute(query)).Vertices.Select(v => (double)v.Props["eigenvector"]).ToArray();
            var expected = new[]
            {
                0.171,
                0.125,
                0.142,
                0.142,
                0.125,
                0.101,
                0.101,
                0.070,
                0.017,
                0.004
            };

            var maxValue = expected.Max();
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = Math.Round(100 * expected[i] / maxValue) / 100;
                result[i] = (double)Math.Round(100 * result[i]) / 100;
            }


            Assert.AreEqual(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(result));

            // add weights to edges and execute the same query
            var graph = JsonConvert.DeserializeObject<PropertyGraphModel>(_graph);
            var l = 0;
            foreach (var edges in graph.Edges)
            {
                edges.Props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["length"] = l++
                };
            }
            _graph = JsonConvert.SerializeObject(graph);
            
            result = (await _handler.Execute(query)).Vertices.Select(v => (double)v.Props["eigenvector"]).ToArray();
            
            for (int i = 0; i < expected.Length; i++)
            {
                result[i] = (double)Math.Round(100 * result[i]) / 100;
            }
            Assert.AreNotEqual(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(result));
        }

        [TestMethod]
        public async Task ShouldCalculateBetweennessMetric()
        {

            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"SELECT * CALCULATE betweenness"
            };
            var result =
                (await _handler.Execute(query)).Vertices.Select(v => (double)v.Props["betweenness"]).ToArray();
            var expected = new[]
            {
                3.66666,
                0.83333,
                8.33333,
                8.33333,
                0.83333,
                0,
                0,
                14,
                8,
                0
            };

            var maxValue = expected.Max();
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = Math.Round(1000 * expected[i] / maxValue) / 1000;
                result[i] = (double)Math.Round(1000 * result[i]) / 1000;
            }

            Assert.AreEqual(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(result));
        }


        [TestMethod]
        public async Task ShouldCalculateBetweennessMetricWithParams()
        {
            _graph = @"
                {
                   'Vertices':[
                      {  'Id':'00000000-0000-0000-0000-000000000001' },
                      {  'Id':'00000000-0000-0000-0000-000000000002' },
                      {  'Id':'00000000-0000-0000-0000-000000000003' },
                      {  'Id':'00000000-0000-0000-0000-000000000004' },
                      {  'Id':'00000000-0000-0000-0000-000000000005' },
                      {  'Id':'00000000-0000-0000-0000-000000000006' },
                      {  'Id':'00000000-0000-0000-0000-000000000007' },
                      {  'Id':'00000000-0000-0000-0000-000000000008' },
                      {  'Id':'00000000-0000-0000-0000-000000000009' },
                      {  'Id':'00000000-0000-0000-0000-000000000010' }                      
                    ],
                   'Edges':[
                        { Source: 0, Target: 1 },
                        { Source: 0, Target: 6 },
                        { Source: 0, Target: 2 },
                        { Source: 0, Target: 3 },
                        { Source: 0, Target: 5 },
                        { Source: 0, Target: 4 },

                        { Source: 1, Target: 4 },
                        { Source: 1, Target: 6 },
                        { Source: 1, Target: 2 },
                        { Source: 1, Target: 0 },

                        { Source: 2, Target: 6 },
                        { Source: 2, Target: 7 },
                        { Source: 2, Target: 3 },
                        { Source: 2, Target: 0 },
                        { Source: 2, Target: 1 },

                        { Source: 3, Target: 7 },
                        { Source: 3, Target: 5 },
                        { Source: 3, Target: 4 },
                        { Source: 3, Target: 0 },
                        { Source: 3, Target: 2 },

                        { Source: 4, Target: 1 },
                        { Source: 4, Target: 0 },
                        { Source: 4, Target: 3 },
                        { Source: 4, Target: 5 },

                        { Source: 5, Target: 4 },
                        { Source: 5, Target: 0 },
                        { Source: 5, Target: 3 },

                        { Source: 6, Target: 1 },
                        { Source: 6, Target: 0 },
                        { Source: 6, Target: 2 },

                        { Source: 7, Target: 2 },
                        { Source: 7, Target: 3 },
                        { Source: 7, Target: 8 },

                        { Source: 8, Target: 7 },
                        { Source: 8, Target: 9 },

                        { Source: 9, Target: 8 },

                        { Source: 4, Target: 2, Name: 'Ignore' },
                        { Source: 5, Target: 2, Name: 'Ignore' },
                        { Source: 6, Target: 3, Name: 'Ignore' },
                        { Source: 7, Target: 4, Name: 'Ignore' },
                   ]
                }";

            var query = new GraphQuery()
            {
                NetworkId = _fake.Network.Id,
                QueryText = @"SELECT * CALCULATE betweenness(Name != 'Ignore', length)"
            };
            var result =
                (await _handler.Execute(query)).Vertices.Select(v => (double)v.Props["betweenness"]).ToArray();
            var expected = new[]
            {
                3.66666,
                0.83333,
                8.33333,
                8.33333,
                0.83333,
                0,
                0,
                14,
                8,
                0
            };

            var maxValue = expected.Max();
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = Math.Round(1000 * expected[i] / maxValue) / 1000;
                result[i] = (double)Math.Round(1000 * result[i]) / 1000;
            }

            Assert.AreEqual(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(result));


            // add weights to edges and execute the same query
            var graph = JsonConvert.DeserializeObject<PropertyGraphModel>(_graph);
            var l = 0;
            foreach (var edges in graph.Edges)
            {
                edges.Props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["length"] = l++
                };
            }
            _graph = JsonConvert.SerializeObject(graph);

            result = (await _handler.Execute(query)).Vertices.Select(v => (double)v.Props["betweenness"]).ToArray();

            for (int i = 0; i < expected.Length; i++)
            {
                result[i] = (double)Math.Round(1000 * result[i]) / 1000;
            }
            Assert.AreNotEqual(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(result));
        }
    }
}
