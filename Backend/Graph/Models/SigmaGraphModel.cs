using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebPerspective.Areas.Graph.Models
{
    /*
     * {
          nodes: [
            { id: 'n0', label: 'Hello', x: 0, y: 0, size: 1, colors: ['#f00'], entity: '586b4c17-7ef1-42dc-90e6-41b44a12d4b5' },
            { id: 'n1', label: 'World', x: 1, y: 1, size: 1, colors: ['#f00'], entity: '586b4c17-7ef1-42dc-90e6-41b44a12d4b5' },
          ],
          edges: [
            {
              id: 'e0',
              source: 'n0',
              target: 'n1'
            }
          ]
        }
    */
    public class SigmaGraphModel
    {
        public List<SigmaNodeModel> Nodes { get; set; }
        public List<SigmaEdgeModel> Edges { get; set; }

        public Dictionary<string,object> Data { get; set; } 
    }

    //public class SigmaLegendItem
    //{
    //    public string Color { get; set; }
    //    public string Label { get; set; }
    //    public int Count { get; set; }
    //}
    
    public class SigmaNodeModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double? Size { get; set; }
        public string[] Colors { get; set; }
        public Guid? Entity { get; set; }
        public Dictionary<string,object> Props { get; set; }
    }

    public class SigmaEdgeModel
    {
        public string Id { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public double? Size { get; set; }
        public Dictionary<string, object> Props { get; set; }
    }
}