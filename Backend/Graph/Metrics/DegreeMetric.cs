using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Areas.Graph.Metrics
{
    public class DegreeMetric : IMetric
    {
        public IExpression Expression { get; set; }        
        public PropertyGraphModel Calculate(PropertyGraphModel graph)
        {
            if (graph?.Vertices == null) return graph;

            var counter = new int[graph.Vertices.Count];

            if (graph.Edges != null)
                foreach (var edge in graph.Edges)
                {
                    if (Expression == null || Expression.Evaluate(null, edge))
                    {
                        counter[edge.Source]++;
                        counter[edge.Target]++;
                    }
                }

            for (int i = 0; i < graph.Vertices.Count; i++)
            {
                graph.Vertices[i].Props["degree"] = counter[i];
            }
            return graph;
        }
    }
}