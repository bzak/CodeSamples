using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using WebPerspective.Areas.Settings.Queries;

namespace WebPerspective.Areas.Graph.Models
{
    public partial class PropertyGraphModel
    {
        public List<PropertyVertexModel> Vertices { get; set; }
        public List<PropertyEdgeModel> Edges { get; set; }
        public Dictionary<string,object> Data { get; set; } = new Dictionary<string, object>();
    }

    public class PropertyVertexModel
    {
        public Guid Id { get; set; }
        public IDictionary<string, object> Props { get; set; }

        [JsonIgnore]
        public List<PropertyEdgeModel> Edges { get; set; }
    }


    public class PropertyEdgeModel
    {
        [JsonIgnore]
        public PropertyVertexModel SourceVertex { get; set; }

        [JsonIgnore]
        public PropertyVertexModel TargetVertex { get; set; }

        public int Source { get; set; }
        public int Target { get; set; }
        public string Name { get; set; }
        public IDictionary<string, object> Props { get; set; }
    }


    public static class PropertyGraphModelExtensions
    {
        public static PropertyGraphModel DeepCopy(this PropertyGraphModel model)
        {
            var result = new PropertyGraphModel();
            if (model.Vertices != null)
                result.Vertices = model.Vertices.Select(DeepCopy).ToList();
            if (model.Edges != null)
                result.Edges = model.Edges.Select(DeepCopy).ToList();
            result.CreateLinks();
            result.CreateIndex();
            return result;
        }

        public static PropertyVertexModel DeepCopy(this PropertyVertexModel model)
        {
            var result = new PropertyVertexModel()
            {
                Id = model.Id
            };
            if (model.Props != null)
                result.Props = new Dictionary<string, object>(model.Props, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static PropertyVertexModel ToEdgelessPropertyVertexModel(this ConcurrentVertexModel cVertex)
        {
            return new PropertyVertexModel()
            {
                Id = cVertex.Id,
                Props = new Dictionary<string, object>(cVertex.Props, StringComparer.OrdinalIgnoreCase),
                Edges = new List<PropertyEdgeModel>()
            };
        }

        public static PropertyEdgeModel DeepCopy(this PropertyEdgeModel model)
        {
            var result = new PropertyEdgeModel()
            {
                Source = model.Source,
                Target = model.Target,
                SourceVertex = model.SourceVertex,
                TargetVertex = model.TargetVertex,
                Name = model.Name
            };
            if (model.Props != null)
                result.Props = new Dictionary<string, object>(model.Props, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static void CreateLinks(this PropertyGraphModel model)
        {
            if (model.Vertices == null || model.Edges == null) return;

            foreach (var vertex in model.Vertices)
            {
                vertex.Edges = new List<PropertyEdgeModel>();
            }
            foreach (var edge in model.Edges)
            {
                var source = model.Vertices[edge.Source];
                var target = model.Vertices[edge.Target];
                edge.SourceVertex = source;
                edge.TargetVertex = target;
                source.Edges.Add(edge);
                target.Edges.Add(edge);
            }
        }

        public static void ClearIfEmpty(this PropertyGraphModel model)
        {
            model.Vertices = model.Vertices?.Count > 0 ? model.Vertices : null;
            model.Edges = model.Edges?.Count > 0 ? model.Edges : null;

            if (model.Vertices != null)
                foreach (var vertex in model.Vertices)
                {
                    if (vertex.Props != null && vertex.Props.Count == 0)
                        vertex.Props = null;
                }

            if (model.Edges != null)
                foreach (var edge in model.Edges)
                {
                    if (edge.Props != null && edge.Props.Count == 0)
                        edge.Props = null;
                }
        }

        public static IDictionary<string, object> ToProfileCardProps(this IDictionary<string, object> props,
            ProfileCardSettings profileCard)
        {
            var result = new Dictionary<string, object>();
            if (props != null)
            {
                object prop;
                if (props.TryGetValue(profileCard.LabelProp, out prop))
                {
                    result["label"] = prop;
                }
                if (props.TryGetValue(profileCard.DetailsProp, out prop))
                {
                    result["details"] = prop;
                }
                if (props.TryGetValue(profileCard.PhotoProp, out prop))
                {
                    result["photo"] = prop;
                }
            }
            return result;
        }

    }

    public partial class PropertyGraphModel
    {
        [JsonIgnore]
        internal Dictionary<Guid, int> VertexIdLookupIndex { get; set; }

        [JsonIgnore]
        internal Dictionary<Tuple<Guid, Guid, string>, int> EdgeSourceIdTargetIdNameLookupIndex { get; set; }

        public void CreateIndex()
        {
            var newIndex = new Dictionary<Guid, int>(this.Vertices.Count);
            for (int i = 0; i < this.Vertices.Count; i++)
            {
                var v = this.Vertices[i];
                newIndex.Add(v.Id, i);
            }
            this.VertexIdLookupIndex = newIndex;
        }

        public void CreateEdgeIndex()
        {
            var newIndex = new Dictionary<Tuple<Guid, Guid, string>, int>(this.Edges.Count);
            for (int i = 0; i < this.Edges.Count; i++)
            {
                var e = this.Edges[i];
                newIndex.Add(new Tuple<Guid, Guid, string>(e.SourceVertex.Id, e.TargetVertex.Id, e.Name), i);
            }
            this.EdgeSourceIdTargetIdNameLookupIndex = newIndex;
        }

        public PropertyVertexModel FindById(Guid vertexId)
        {
            int? vertexIndex = FindIndex(vertexId);
            if (vertexIndex.HasValue)
            {
                return this.Vertices[vertexIndex.Value];
            }
            return null;
        }

        public int? FindIndex(Guid vertexId)
        {
            if (this.VertexIdLookupIndex == null)
                CreateIndex();
            
            int vertexIndex;
            if (this.VertexIdLookupIndex.TryGetValue(vertexId, out vertexIndex))
            {
                return vertexIndex;
            }
            return null;
        }

        public int? FindEdgeIndex(Guid sourceVertexId, Guid targetVertexId, string name)
        {
            if (this.EdgeSourceIdTargetIdNameLookupIndex == null)
                CreateEdgeIndex();

            int edgeIndex;
            if (this.EdgeSourceIdTargetIdNameLookupIndex.TryGetValue(new Tuple<Guid, Guid, string>(sourceVertexId, targetVertexId, name), out edgeIndex))
            {
                return edgeIndex;
            }
            return null;
        }
    }
}