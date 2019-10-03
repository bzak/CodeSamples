using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Services
{
    public interface IIdentifier
    {
        object Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge = null);        
    }

    public class PropIdentifier : IIdentifier
    {
        public PropIdentifier(string propName)
        {
            PropName = propName;
        }

        public string PropName { get; }

        public virtual object Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge = null)
        {
            if (PropName.ToLower() == "null") return null;

            if (edge != null)
            {
                if (PropName.ToLower() == "name") return edge.Name;
                if (edge.Props == null) return null;
                object result = null;
                edge.Props.TryGetValue(PropName, out result);
                return result;
            }
            if (vertex != null)
            {
                if (PropName.ToLower() == "id") return vertex.Id.ToString();
                if (vertex.Props == null) return null;
                object result = null;
                vertex.Props.TryGetValue(PropName, out result);
                return result;
            }
            return null;
        }


    }

    public class EdgeTraversalExpression : IExpression
    {
        public EdgeTraversalScope Scope { get; set; }
        public IExpression Expression { get; set; }

        public bool Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge)
        {
            switch (Scope)
            {
                case EdgeTraversalScope.All:
                    return vertex.Edges.Any(edgeIterator => Expression.Evaluate(null, edgeIterator));

                case EdgeTraversalScope.In:
                    return vertex.Edges.Any(edgeIterator => edgeIterator.TargetVertex.Id == vertex.Id && Expression.Evaluate(null, edgeIterator));

                case EdgeTraversalScope.Out:
                    return vertex.Edges.Any(edgeIterator => edgeIterator.SourceVertex.Id == vertex.Id && Expression.Evaluate(null, edgeIterator));

                case EdgeTraversalScope.Mutual:
                    return vertex.Edges.Any(edgeIterator =>
                        Expression.Evaluate(null, edgeIterator) &&
                        vertex.Edges.Any(mutual =>
                            mutual.SourceVertex.Id == edgeIterator.TargetVertex.Id &&
                            mutual.TargetVertex.Id == edgeIterator.SourceVertex.Id &&
                            Expression.Evaluate(null, mutual)
                        ));

                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
    }

    public enum EdgeTraversalScope
    {
        All,
        In,
        Out,
        Mutual
    }

    public class EdgeTraversalIdentifier : IIdentifier
    {
        private readonly PropIdentifier _propIdentifier;
        private readonly EdgeDirection _direction;

        public EdgeTraversalIdentifier(EdgeDirection direction, string propName)
        {
            _propIdentifier = new PropIdentifier(propName);
            _direction = direction;
        }

        public object Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge)
        {
            if (edge == null) return null;

            switch (_direction)
            {
                case EdgeDirection.Source:
                    return _propIdentifier.Evaluate(edge.SourceVertex);

                case EdgeDirection.Target:
                    return _propIdentifier.Evaluate(edge.TargetVertex);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum EdgeDirection
    {
        Source,
        Target
    }

    public class Value : IIdentifier
    {
        public Value(object value)
        {
            Val = value;
        }

        public object Val { get; }

        public object Evaluate(PropertyVertexModel vertex = null, PropertyEdgeModel edge = null)
        {
            return Val;
        }
    }
}