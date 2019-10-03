using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Areas.Graph.Metrics
{
    public class BetweennessMetric : IMetric
    {
        public IExpression EdgeCondition { get; set; }
        public IIdentifier LengthPropIdentifier { get; set; }

        public PropertyGraphModel Calculate(PropertyGraphModel graph)
        {
            if (graph?.Vertices == null || graph.Vertices.Count == 0
                || graph?.Edges == null || graph.Edges.Count == 0) return graph;

            var config = new
            {
                samples = 100
            };

            var nodes = graph.Vertices.Count;
            var metric = new double[nodes];

            var moduloSample = Convert.ToInt32(Math.Round((double) (graph.Vertices.Count/(double) config.samples)));
            for (var n = 0; n < nodes; n++)
            {
                if (graph.Vertices.Count > config.samples && n % moduloSample != 0)
                    continue; // sampling: skip some nodes

                var start = n;
                var stack = new Stack<int>(); 
                var paths = new HashSet<int>[nodes];
                var sigma = new double[nodes];
                for (var v = 0; v < nodes; v++)
                {
                    paths[v] = new HashSet<int>();
                    sigma[v] = 0;
                }
                
                var distances = new Dictionary<int, double>();
                sigma[start] = 1;
                var seen = new Dictionary<int, double>() {{start, 0}};

                var queue = new SimplePriorityQueue<Path, double>();

                queue.Enqueue(new Path() {Dist = 0, Start = start, End = start}, 0);

                while (queue.Count > 0)
                {
                    var path = queue.Dequeue();
                    var dist = path.Dist;
                    var pred = path.Start;
                    var vertex = path.End;

                    if (distances.ContainsKey(vertex)) continue; // already searched this node
                    sigma[vertex] = sigma[vertex] + sigma[pred]; // count paths

                    stack.Push(vertex);
                    distances[vertex] = dist;

                    foreach (var edge in graph.Vertices[vertex].Edges)
                    {
                        if (edge.Target == vertex) continue;

                        if (EdgeCondition != null && !EdgeCondition.Evaluate(null, edge)) continue;

                        double weight = 1.0;
                        if (LengthPropIdentifier != null)
                            weight = Convert.ToDouble(LengthPropIdentifier.Evaluate(null, edge) ?? 1.0);

                        var target = edge.Target;

                        var edgeDist = distances[vertex] + weight;
                        if (!(distances.ContainsKey(target)) && (!(seen.ContainsKey(target)) || edgeDist < seen[target]))
                        {
                            seen[target] = edgeDist;
                            queue.Enqueue(new Path() {Dist = edgeDist, Start = vertex, End = target}, edgeDist);
                            sigma[target] = 0;
                            paths[target] = new HashSet<int>() {vertex}; 
                        }
                        else if (Math.Abs(edgeDist - seen[target]) < 0.001)
                        {
                            // handle equal paths
                            sigma[target] += sigma[vertex];
                            paths[target].Add(vertex);
                        }
                    }
                }
                var delta = new double[nodes];
                while (stack.Count > 0)
                {
                    var vertex = stack.Pop(); 
                    foreach (var v in paths[vertex])
                    {
                        delta[v] = delta[v] + (sigma[v] / sigma[vertex]) * (1.0 + delta[vertex]);
                    }                    
                    if (vertex != start)
                    {
                        metric[vertex] = metric[vertex] + delta[vertex];
                    }
                }
            }
            var max = metric.Max();
            for (int i = 0; i < nodes; i++)
            {
                graph.Vertices[i].Props["betweenness"] = metric[i] / max;
            }
            return graph;
        }

        struct Path
        {
            public double Dist;
            public int Start;
            public int End;
        }
    }
}