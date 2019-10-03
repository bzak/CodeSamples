using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Priority_Queue;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Areas.Graph.Metrics
{
    public class PathLengthMetric : IMetric
    {
        public IExpression StartCondition { get; set; }
        public IExpression EdgeCondition { get; set; }
        public IIdentifier LengthPropIdentifier { get; set; }
        public bool DirectedGraph { get; set; } = false;

        public PropertyGraphModel Calculate(PropertyGraphModel graph)
        {
            if (graph?.Vertices == null || graph.Vertices.Count == 0
                || graph?.Edges == null || graph.Edges.Count == 0) return graph;
            
            var nodes = graph.Vertices.Count;           

            var stack = new Stack<int>();
            var paths = new List<int>[nodes];
            for (var v = 0; v < nodes; v++)
            {
                paths[v] = new List<int>();
            }

            var distances = new Dictionary<int, double>();
            var seen = new Dictionary<int, double>();
            var queue = new SimplePriorityQueue<Path, double>();

            // start nodes
            for (var start = 0; start < nodes; start++)
            {
                if (StartCondition.Evaluate(graph.Vertices[start], null))
                {
                    queue.Enqueue(new Path() {Dist = 0, Start = start, End = start}, 0);
                    seen.Add(start, 0);
                }
            }

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var dist = path.Dist;
                var vertex = path.End;

                if (distances.ContainsKey(vertex)) continue; // already searched this node

                stack.Push(vertex);
                distances[vertex] = dist;

                foreach (var edge in graph.Vertices[vertex].Edges)
                {
                    var target = edge.Target;
                    if (edge.Target == vertex)
                    {
                        if (DirectedGraph) continue;
                        else target = edge.Source;
                    }

                    if (EdgeCondition != null && !EdgeCondition.Evaluate(null, edge)) continue;

                    double weight = 1.0;
                    if (LengthPropIdentifier != null)
                        weight = Convert.ToDouble(LengthPropIdentifier.Evaluate(null, edge) ?? 1.0);


                    var edgeDist = distances[vertex] + weight;
                    if (!(distances.ContainsKey(target)) && (!(seen.ContainsKey(target)) || edgeDist < seen[target]))
                    {
                        seen[target] = edgeDist;
                        queue.Enqueue(new Path() {Dist = edgeDist, Start = vertex, End = target}, edgeDist);
                        paths[target] = new List<int>() {vertex};
                    }
                    else if (Math.Abs(edgeDist - seen[target]) < 0.001)
                    {
                        // handle equal paths
                        paths[target].Add(vertex);
                    }
                }
            }

            for (int i = 0; i < nodes; i++)
            {
                double dist;
                if (distances.TryGetValue(i, out dist))
                {
                    if (graph.Vertices[i].Props == null)
                    {
                        graph.Vertices[i].Props = new Dictionary<string, object>();
                    }
                    graph.Vertices[i].Props["pathLength"] = dist;
                    if (paths[i].Count > 0)
                    {
                        graph.Vertices[i].Props["pathNext"] = paths[i];
                    }
                }
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