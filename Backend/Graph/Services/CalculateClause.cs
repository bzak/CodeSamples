using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.ClearScript;
using WebPerspective.Areas.Graph.Metrics;
using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Services
{
    public class CalculateClause : IGraphTransformation
    {
        public List<IMetric> Metrics { get; set; } 

        public PropertyGraphModel Transform(PropertyGraphModel graph)
        {
            foreach (var metric in Metrics)
            {
                graph = metric.Calculate(graph);
            }
            return graph;
        }
    }
}