using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebPerspective.Areas.Settings.Queries;
using WebPerspective.Commons.Extensions;

namespace WebPerspective.Areas.Graph.Models
{
    public partial class ConcurrentGraphModel
    {
        public ConcurrentDictionary<Guid, ConcurrentVertexModel> Vertices { get; private set; }
        public ConcurrentDictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel> Edges { get; private set; }
    }

    public class ConcurrentVertexModel
    {
        public Guid Id { get; set; }
        public ConcurrentDictionary<string, object> Props { get; set; }
    }

    public class ConcurrentEdgeModel
    {
        public Guid Id { get; set; }
        public Guid SourceVertexId { get; set; }
        public Guid TargetVertexId { get; set; }
        public string Name { get; set; }
        public ConcurrentDictionary<string, object> Props { get; set; }
    }

    public partial class ConcurrentGraphModel
    {
        public ConcurrentGraphModel(
            ConcurrentDictionary<Guid, ConcurrentVertexModel> vertices,
            ConcurrentDictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel> edges)
        {
            this.Vertices = vertices;
            this.Edges = edges;
        }

        public ConcurrentGraphModel(PropertyGraphModel g)
        {
            if (g.Vertices != null)
                this.Vertices = new ConcurrentDictionary<Guid, ConcurrentVertexModel>(g.Vertices.Select(
                    vertex => new KeyValuePair<Guid, ConcurrentVertexModel>(vertex.Id, 
                        new ConcurrentVertexModel()
                        {
                            Id = vertex.Id,
                            Props = vertex.Props != null 
                            ? new ConcurrentDictionary<string, object>(vertex.Props, StringComparer.OrdinalIgnoreCase) 
                            : new ConcurrentDictionary<string, object>()
                        })                        
                    ));
                    
            else
                this.Vertices = new ConcurrentDictionary<Guid, ConcurrentVertexModel>();

            if (g.Edges != null)
                this.Edges = new ConcurrentDictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel>(g.Edges.Select(
                    edge => new KeyValuePair<Tuple<Guid, Guid, string>, ConcurrentEdgeModel>(
                        new Tuple<Guid, Guid, string>(edge.SourceVertex.Id, edge.TargetVertex.Id, edge.Name),
                            new ConcurrentEdgeModel()
                            {
                                SourceVertexId = g.Vertices[edge.Source].Id,
                                TargetVertexId = g.Vertices[edge.Target].Id,
                                Name = edge.Name,
                                Props = edge.Props != null 
                                ? new ConcurrentDictionary<string, object>(edge.Props, StringComparer.OrdinalIgnoreCase) 
                                : new ConcurrentDictionary<string, object>()
                            }
                        )));
            else
                this.Edges = new ConcurrentDictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel>();
        }

        public PropertyGraphModel Snapshot()
        {
            var vertices = Vertices.Values;
            var edges = Edges.Values;
            var vertexIdx = new Dictionary<Guid, int>(this.Vertices.Count);
            var edgeIdx = new Dictionary<Tuple<Guid, Guid, string>, int>(this.Edges.Count);

            var graph = new PropertyGraphModel()
            {
                Vertices = new List<PropertyVertexModel>(vertices.Count),
                Edges = new List<PropertyEdgeModel>(edges.Count)
            };
            int vIdx = 0;
            foreach (var cVertex in vertices)
            {
                graph.Vertices.Add(new PropertyVertexModel()
                {
                    Id = cVertex.Id,
                    Props = new Dictionary<string, object>(cVertex.Props, StringComparer.OrdinalIgnoreCase),
                    Edges = new List<PropertyEdgeModel>()
                });
                vertexIdx.Add(cVertex.Id, vIdx);
                vIdx++;
            }
            int eIdx = 0;
            foreach (var cEdge in edges)
            {
                var pEdge = new PropertyEdgeModel()
                {
                    Name = cEdge.Name,
                    Props = new Dictionary<string, object>(cEdge.Props, StringComparer.OrdinalIgnoreCase)
                };                
                int sourceIdx;
                if (!vertexIdx.TryGetValue(cEdge.SourceVertexId, out sourceIdx)) continue;
                int targetIdx;
                if (!vertexIdx.TryGetValue(cEdge.TargetVertexId, out targetIdx)) continue;
                pEdge.Source = sourceIdx;
                pEdge.Target = targetIdx;
                pEdge.SourceVertex = graph.Vertices[sourceIdx];
                pEdge.TargetVertex = graph.Vertices[targetIdx];
                graph.Edges.Add(pEdge);
                pEdge.SourceVertex.Edges.Add(pEdge);
                pEdge.TargetVertex.Edges.Add(pEdge);

                edgeIdx.Add(new Tuple<Guid, Guid, string>(cEdge.SourceVertexId, cEdge.TargetVertexId, cEdge.Name), eIdx);
                eIdx++;
            }
            graph.VertexIdLookupIndex = vertexIdx;
            graph.EdgeSourceIdTargetIdNameLookupIndex = edgeIdx;
            return graph;
        }

        public ConcurrentVertexModel FindByPropValue(string propName, object propValue)
        {
            foreach (var vertex in Vertices.Values)
            {
                object value;
                if (!vertex.Props.TryGetValue(propName, out value)) continue;

                if (string.Equals(value.ToString(), propValue.ToString(), StringComparison.InvariantCultureIgnoreCase)) return vertex;
            }
            return null;
        }

        public PropertyVertexModel BasicVertexModel(Guid vertexId, NetworkSettings settings)
        {
            var targetVertex = Vertices[vertexId];

            return new PropertyVertexModel()
            {
                Id = vertexId,
                Props = targetVertex.Props.ToProfileCardProps(settings.ProfileCard)
            };            
        }
    }
}