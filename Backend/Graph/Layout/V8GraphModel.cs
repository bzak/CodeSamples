using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebPerspective.Areas.Graph.Models;

// ReSharper disable InconsistentNaming

namespace WebPerspective.Areas.Graph.Layout
{
    public class V8GraphModel
    {
        public V8NodeModel[] nodes { get; set; }
        public V8EdgeModel[] edges { get; set; }

        public void LoadSigma(SigmaGraphModel graph)
        {            
            nodes = graph.Nodes?.Select(n => new V8NodeModel() {id = n.Id, x = n.X, y = n.Y, size = n.Size}).ToArray() ?? new V8NodeModel[0];             
            edges = graph.Edges?.Select(e => new V8EdgeModel() {id = e.Id, source = e.Source, target = e.Target}).ToArray() ?? new V8EdgeModel[0];
        }
    }

    public class V8NodeModel
    {
        public string id { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double? size { get; set; }
    }

    public class V8EdgeModel
    {
        public string id { get; set; }
        public string source { get; set; }
        public string target { get; set; }
    }
}