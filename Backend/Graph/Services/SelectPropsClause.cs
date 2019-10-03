using System;
using System.Collections.Generic;
using System.Linq;
using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Services
{
    public struct Prop
    {
        public string Name;
        public string Alias;
    }

    public interface IProp
    {
        object Evaluate(PropertyVertexModel vertex);
        string Alias { get; }
    }

    public class WildcardProp : IProp
    {
        public object Evaluate(PropertyVertexModel vertex)
        {
            return vertex.Props;
        }

        public string Alias { get; set; }
    }

    public class NameProp : IProp
    {
        private string _alias;
        public string Name { private get; set; }

        public string Alias
        {
            get { return _alias ?? Name; }
            set { _alias = value; }
        }

        public object Evaluate(PropertyVertexModel vertex)
        {
            object value;
            if (vertex.Props != null && vertex.Props.TryGetValue(Name, out value))
            {
                return value;
            }
            return null;
        }

    }

    public class ValueProp : IProp
    {
        private string _alias;
        public string Value { private get; set; }

        public string Alias
        {
            get { return _alias; }
            set { _alias = value; }
        }

        public object Evaluate(PropertyVertexModel vertex)
        {
            return Value;
        }

    }

    public class UnionExpressionProp : IProp
    {
        private readonly IProp _left;
        private readonly IProp _right;

        public UnionExpressionProp(IProp left, IProp right, string @alias)
        {
            _left = left;
            _right = right;
            this.Alias = @alias;
        }

        public object Evaluate(PropertyVertexModel vertex)
        {
            var left = _left.Evaluate(vertex);
            var right = _right.Evaluate(vertex);
            if (left == null) return ToArray(right);
            if (right == null) return ToArray(left);

            // convert to arrays is props are not arrays
            object[] leftArr = ToArray(left);
            object[] rightArr = ToArray(right);

            // compute union of two 
            Dictionary<string, object> result = leftArr.ToDictionary(e => e.ToString(), e=>e);
            foreach (var e in rightArr)
            {
                var key = e.ToString();
                if (result.ContainsKey(key)) continue;
                result.Add(key, e);
            }

            return result.Values.ToArray();
        }

        private object[] ToArray(object obj)
        {
            if (obj == null) return null;
            return obj is object[] ? obj as object[] : new[] { obj }; ;
        }

        public string Alias { get; }
    }

    public class LikeExpressionProp : IProp
    {
        private readonly IProp _left;
        private readonly IProp _right;

        public LikeExpressionProp(IProp left, IProp right, string @alias)
        {
            _left = left;
            _right = right;
            this.Alias = @alias;
        }

        public object Evaluate(PropertyVertexModel vertex)
        {
            var left = _left.Evaluate(vertex);
            var right = _right.Evaluate(vertex);
            if (left == null) return null;
            if (right == null) return null;

            // convert to arrays is props are not arrays
            object[] leftArr = ToArray(left);
            string filter = right.ToString().Trim(new char[] {'\''});

            return leftArr.Where(e => e.ToString().Like(filter));
        }

        private object[] ToArray(object obj)
        {
            if (obj == null) return null;
            return obj is object[] ? obj as object[] : new[] { obj }; ;
        }

        public string Alias { get; }
    }

    public class SelectPropsClause : IGraphTransformation
    {
        public List<IProp> VertexProps { get; set; } = new List<IProp>();
        public Dictionary<string, List<Prop>> Edges { get; set; } = new Dictionary<string, List<Prop>>(StringComparer.OrdinalIgnoreCase);

        public void AddProp(IProp prop)
        {
            this.VertexProps.Add(prop);
        }

        public void AddEdgeFilter(string edgeName, string alias)
        {
            List<Prop> edgeFilter = null;
            if (alias != null)
            {
                edgeFilter = new List<Prop>();
                edgeFilter.Add(new Prop() { Name = "name", Alias = alias });
            }
            this.Edges.Add(edgeName, edgeFilter);
        }

        public void AddEdgePropFilter(string edgeName, string propName, string alias)
        {
            List<Prop> edgeFilter;
            if (!Edges.TryGetValue(edgeName, out edgeFilter))
            {
                edgeFilter = new List<Prop>();
                Edges.Add(edgeName, edgeFilter);
            }
            edgeFilter.Add(new Prop() { Name = propName, Alias = alias });
        }        

        public PropertyGraphModel Transform(PropertyGraphModel graph)
        {
            if (VertexProps.Any(p => p is WildcardProp)) return graph;

            if (VertexProps != null && graph.Vertices != null)
            {                
                foreach (var vertex in graph.Vertices)
                {
                    var newProps = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in VertexProps)
                    {
                        newProps[prop.Alias] = prop.Evaluate(vertex);
                    }
                    vertex.Props = newProps;
                }
            }
            var filteredEdges = new List<PropertyEdgeModel>();
            var wildcardEdgePropFilter = Edges.ContainsKey("*");
            if (graph.Edges != null)
            {
                foreach (var edge in graph.Edges)
                {
                    List<Prop> edgePropsFilter = null;

                    if (!Edges.TryGetValue(edge.Name, out edgePropsFilter))
                    {
                        if (!wildcardEdgePropFilter)
                        {
                            edge.SourceVertex.Edges.Remove(edge);
                            edge.TargetVertex.Edges.Remove(edge);
                            continue;
                        }
                    }

                    if (edgePropsFilter != null)
                    {
                        var newProps = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in edgePropsFilter)
                        {
                            object value;
                            if (edge.Props.TryGetValue(prop.Name, out value))
                            {
                                newProps.Add(prop.Alias ?? prop.Name, value);
                            }
                            if (prop.Name == "name")
                            {
                                edge.Name = prop.Alias;
                            }
                        }
                        edge.Props = newProps.Count > 0 ? newProps : null;
                    }
                    else if (!wildcardEdgePropFilter)
                    {
                        edge.Props = null;
                    }
                    filteredEdges.Add(edge);
                }
                graph.Edges = filteredEdges;
            }

            return graph;
        }

    }
}