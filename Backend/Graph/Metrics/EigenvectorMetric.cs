using System;
using System.Linq;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Areas.Graph.Metrics
{
    public class EigenvectorMetric : IMetric
    {
        public IExpression EdgeCondition { get; set; }
        public IIdentifier LengthPropIdentifier { get; set; }

        void normalize(double[] vector)
        {
            double sum = vector.Sum();
            double w = (double) 1.0 / (sum > 0 ? sum : 1);
            for (var i=0; i < vector.Length; i++)
            {
                vector[i] *= w;
            }
        }

        public PropertyGraphModel Calculate(PropertyGraphModel graph)
        {
            if (graph?.Vertices == null || graph.Vertices.Count == 0
                || graph?.Edges == null || graph.Edges.Count == 0) return graph;

            var config = new
            {
                iterations = 100,
                tolerance = 0.0001
            };

            var nodesCount = graph.Vertices.Count;
            double start = 1.0 / (double) nodesCount;
            var metric = new double[nodesCount]; 
            for (var n = 0; n < nodesCount; n++)
            {
                metric[n] = start;
            }

            normalize(metric);

            for (var iter = 0; iter < config.iterations; iter++)
            {
                var v0 = metric;
                metric = new double[nodesCount];

                for (var source = 0; source < metric.Length; source++)
                {
                    foreach (var edge in graph.Vertices[source].Edges)
                    {
                        if (edge.Target == source) continue;

                        if (EdgeCondition != null && !EdgeCondition.Evaluate(null, edge)) continue;

                        double weight = 1.0;
                        if (LengthPropIdentifier != null)
                            weight = Convert.ToDouble(LengthPropIdentifier.Evaluate(null, edge) ?? 1.0) ;

                        metric[source] += v0[edge.Target] * weight;
                    }
                }
                normalize(metric);
                double energy = metric.Select((t, n) => Math.Abs(t - v0[n])).Sum();
                if (energy < nodesCount * config.tolerance)
                {
                    // Normalize between 0.0 and 1.0.
                    var max = metric.Max();
                    for (var id = 0; id < metric.Length; id++)
                    {                        
                        graph.Vertices[id].Props["eigenvector"] = metric[id] / max;
                    }
                    return graph;
                }                
            }

            // did not converge
            return graph;
        }
    }
}