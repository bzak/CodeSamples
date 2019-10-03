using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using WebPerspective.Areas.Graph.Metrics;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Tests.Areas.Graph.Metrics
{
    [TestClass]
    public class PathLengthTest
    {
        private PathLengthMetric _metric;

        [TestInitialize]
        public void Setup()
        {
            _metric = new PathLengthMetric();
        }

        [TestMethod]
        public void PathLengthTest1()
        {
            var graph = @"
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
                         'Source':0, 'Target':2
                      }
                   ]
                }
            ";
            var model = JsonConvert.DeserializeObject<PropertyGraphModel>(graph);
            model.CreateLinks();
            _metric.StartCondition = new ValueExpression()
            {
                Left = new PropIdentifier("Id"),
                ValueOperator = ValueOperator.Equals,
                Right = new Value("00000000-0000-0000-0000-000000000001")
            };
            var result = _metric.Calculate(model);
            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1, 'pathLength':0.0 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2, 'pathLength':1.0, 'pathNext':[0] }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':2
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), JsonConvert.SerializeObject(result));
        }

        [TestMethod]
        public void PathLengthTest2()
        {
            var graph = @"
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
                         'Source':0, 'Target':2
                      },
                      {
                         'Source':2, 'Target':1
                      }
                   ]
                }
            ";
            var model = JsonConvert.DeserializeObject<PropertyGraphModel>(graph);
            model.CreateLinks();
            _metric.StartCondition = new ValueExpression()
            {
                Left = new PropIdentifier("Id"),
                ValueOperator = ValueOperator.Equals,
                Right = new Value("00000000-0000-0000-0000-000000000001")
            };
            var result = _metric.Calculate(model);
            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'First name':'John', 'Last name':'Kowalski', 'Dept':1, 'pathLength':0.0 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'First name':'John', 'Last name':'Malkovitch', 'Dept':2, 'pathLength':2.0, 'pathNext':[2]  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'First name':'Anna', 'Last name':'Nowak', 'Dept':2, 'pathLength':1.0, 'pathNext':[0] }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':2
                      },
                      {
                         'Source':2, 'Target':1
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), JsonConvert.SerializeObject(result));
        }



        [TestMethod]
        public void PathLengthTest3()
        {
            var graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':2
                      },
                      {
                         'Source':2, 'Target':1
                      },
                      {
                         'Source':0, 'Target':3
                      },
                      {
                         'Source':3, 'Target':1
                      }
                   ]
                }
            ";
            var model = JsonConvert.DeserializeObject<PropertyGraphModel>(graph);
            model.CreateLinks();
            _metric.StartCondition = new ValueExpression()
            {
                Left = new PropIdentifier("Id"),
                ValueOperator = ValueOperator.Equals,
                Right = new Value("00000000-0000-0000-0000-000000000001")
            };
            var result = _metric.Calculate(model);
            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'pathLength':0.0 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'pathLength':2.0, 'pathNext':[2,3]  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'pathLength':1.0, 'pathNext':[0] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'pathLength':1.0, 'pathNext':[0] }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':0, 'Target':2
                      },
                      {
                         'Source':2, 'Target':1
                      },
                      {
                         'Source':0, 'Target':3
                      },
                      {
                         'Source':3, 'Target':1
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), JsonConvert.SerializeObject(result));
        }


        [TestMethod]
        public void PathLengthTestRevLinks()
        {
            var graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0
                      },
                      {
                         'Source':1, 'Target':2
                      },
                      {
                         'Source':3, 'Target':0
                      },
                      {
                         'Source':1, 'Target':3
                      }
                   ]
                }
            ";
            var model = JsonConvert.DeserializeObject<PropertyGraphModel>(graph);
            model.CreateLinks();
            _metric.StartCondition = new ValueExpression()
            {
                Left = new PropIdentifier("Id"),
                ValueOperator = ValueOperator.Equals,
                Right = new Value("00000000-0000-0000-0000-000000000001")
            };
            _metric.DirectedGraph = false;
            var result = _metric.Calculate(model);
            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'pathLength':0.0 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'pathLength':2.0, 'pathNext':[2,3]  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'pathLength':1.0, 'pathNext':[0] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'pathLength':1.0, 'pathNext':[0] }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0
                      },
                      {
                         'Source':1, 'Target':2
                      },
                      {
                         'Source':3, 'Target':0
                      },
                      {
                         'Source':1, 'Target':3
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), JsonConvert.SerializeObject(result));
        }

        [TestMethod]
        public void PathLengthTestBothDirLinks()
        {
            var graph = @"
                {
                   'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0
                      },
                      {
                         'Source':2, 'Target':1
                      },
                      {
                         'Source':3, 'Target':0
                      },
                      {
                         'Source':1, 'Target':3
                      }
                   ]
                }
            ";
            var model = JsonConvert.DeserializeObject<PropertyGraphModel>(graph);
            model.CreateLinks();
            _metric.StartCondition = new ValueExpression()
            {
                Left = new PropIdentifier("Id"),
                ValueOperator = ValueOperator.Equals,
                Right = new Value("00000000-0000-0000-0000-000000000001")
            };
            _metric.DirectedGraph = false;
            var result = _metric.Calculate(model);
            var expected = @"
                {
                    'Vertices':[
                      {
                         'Id':'00000000-0000-0000-0000-000000000001',
                         'Props':{ 'pathLength':0.0 }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000002',
                         'Props':{ 'pathLength':2.0, 'pathNext':[2,3]  }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000003',
                         'Props':{ 'pathLength':1.0, 'pathNext':[0] }
                      },
                      {
                         'Id':'00000000-0000-0000-0000-000000000004',
                         'Props':{ 'pathLength':1.0, 'pathNext':[0] }
                      }
                   ],
                   'Edges':[
                      {
                         'Source':2, 'Target':0
                      },
                      {
                         'Source':2, 'Target':1
                      },
                      {
                         'Source':3, 'Target':0
                      },
                      {
                         'Source':1, 'Target':3
                      }
                   ]
                }
            ";

            Assert.AreEqual(JsonConvert.SerializeObject(JsonConvert.DeserializeObject<PropertyGraphModel>(expected)), JsonConvert.SerializeObject(result));
        }
    }
}
