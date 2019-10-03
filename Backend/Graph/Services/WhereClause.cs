using System;
using System.Collections.Generic;
using System.Linq;
using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Services
{
    public class WhereClause : IGraphTransformation
    {
        public IExpression Expression { get; set; }

        public PropertyGraphModel Transform(PropertyGraphModel graph)
        {
            // no vertices to filter
            if (graph.Vertices == null) return graph;

            var edges = new List<PropertyEdgeModel>();            

            // evaluate each vertex            
            var vertices = graph.Vertices
                .Where(vertex => Expression.Evaluate(vertex, null))
                .ToList();

            // index new set of vertices
            var vertexIndexer = new Dictionary<Guid, int>();
            for (var i = 0; i < vertices.Count; i++)
            {
                vertexIndexer.Add(vertices[i].Id, i);
            }

            // add all edges that have both ends in vertices list
            foreach (var vertex in vertices)
            {
                if (vertex.Edges == null) continue;
                
                var newVertexEdges = new List<PropertyEdgeModel>();
                foreach (var edge in vertex.Edges)
                {
                    if (edge.SourceVertex.Id != vertex.Id) continue;

                    int sourceIndex;
                    int targetIndex;
                    if (vertexIndexer.TryGetValue(edge.SourceVertex.Id, out sourceIndex)
                        && vertexIndexer.TryGetValue(edge.TargetVertex.Id, out targetIndex))
                    {
                        var newEdge = new PropertyEdgeModel()
                        {
                            SourceVertex = edge.SourceVertex,
                            TargetVertex = edge.TargetVertex,
                            Source = sourceIndex,
                            Target = targetIndex,
                            Name = edge.Name,
                            Props = edge.Props
                        };
                        edges.Add(newEdge);
                        newVertexEdges.Add(newEdge);
                    }
                }
                vertex.Edges = newVertexEdges;
            }

            var result = new PropertyGraphModel()
            {
                Vertices = vertices,
                Edges = edges,
                Data = graph.Data
            };
            result.CreateLinks();
            return result;
        }

    }
}