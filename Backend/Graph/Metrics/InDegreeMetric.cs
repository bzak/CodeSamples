using System.Linq;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Areas.Graph.Metrics
{
    public class InDegreeMetric : IMetric
    {
        public IExpression Expression { get; set; }
        public bool Normalized = false;        
        public PropertyGraphModel Calculate(PropertyGraphModel graph)
        {
            if (graph?.Vertices == null) return graph;

            var counter = new int[graph.Vertices.Count];

            if (graph.Edges != null)
                foreach (var edge in graph.Edges)
                {
                    if (Expression == null || Expression.Evaluate(null, edge))
                    {
                        counter[edge.Target]++;
                    }
                }

            if (Normalized)
            {
                var max = counter.Max();
                for (int i = 0; i < graph.Vertices.Count; i++)
                {
                    graph.Vertices[i].Props["in_degree"] = (double) counter[i] / (double) max;
                }
            }
            else
            {
                for (int i = 0; i < graph.Vertices.Count; i++)
                {
                    graph.Vertices[i].Props["in_degree"] = counter[i];
                }
            }            
            return graph;
        }
    }
    
}