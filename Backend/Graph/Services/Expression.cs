using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json.Linq;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Commons.Extensions;

namespace WebPerspective.Areas.Graph.Services
{
    public interface IExpression
    {
        bool Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge);
    }


    public class BooleanValueExpression : IExpression
    {
        public bool Value { get; set; }
        public bool Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge)
        {
            return Value;
        }
    }

    public class BinaryExpression : IExpression
    {
        public IExpression Left { get; set; }
        public BinaryOperator BinaryOperator { get; set; }
        public IExpression Right { get; set; }
        public bool Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge)
        {
            switch (BinaryOperator)
            {
                case BinaryOperator.And:
                    return Left.Evaluate(vertex, edge) && Right.Evaluate(vertex, edge);

                case BinaryOperator.Or:
                    return Left.Evaluate(vertex, edge) || Right.Evaluate(vertex, edge);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum BinaryOperator
    {
        And,
        Or
    }

    public class ValueExpression : IExpression
    {
        public IIdentifier Left { get; set; }
        public ValueOperator ValueOperator { get; set; }
        public IIdentifier Right { get; set; }

        public bool Evaluate(PropertyVertexModel vertex, PropertyEdgeModel edge)
        {
            var leftValue = Left.Evaluate(vertex, edge);
            var rightValue = Right.Evaluate(vertex, edge);

            if (leftValue is Array)
            {
                return ArrayEval(leftValue as Array, rightValue);                
            }
            if (rightValue is Array)
            {
                return ArrayEval(rightValue as Array, leftValue);
            }
            return SingleEval(leftValue, rightValue);
        }

        private bool ArrayEval(Array arr, object value)
        {
            var result = false;
            foreach (var element in arr)
            {
                result |= SingleEval(element, value);
            }
            return result;
        }

        private bool SingleEval(object leftValue, object rightValue)
        {
            try
            {
                switch (ValueOperator)
                {
                    case ValueOperator.Equals:
                        return (leftValue == null && rightValue == null) ||
                               (leftValue != null && leftValue.Equals(rightValue));

                    case ValueOperator.NotEquals:
                        return !((leftValue == null && rightValue == null) ||
                               (leftValue != null && leftValue.Equals(rightValue)));

                    case ValueOperator.GreaterThan:
                        return (leftValue != null && rightValue != null) &&
                               ((dynamic) leftValue > (dynamic) rightValue);

                    case ValueOperator.SmallerThan:
                        return (leftValue != null && rightValue != null) &&
                               ((dynamic) leftValue < (dynamic) rightValue);

                    case ValueOperator.GreaterOrEqual:
                        return (leftValue != null && rightValue != null) &&
                               ((dynamic) leftValue >= (dynamic) rightValue);

                    case ValueOperator.SmallerOrEqual:
                        return (leftValue != null && rightValue != null) &&
                               ((dynamic) leftValue <= (dynamic) rightValue);

                    case ValueOperator.Like:
                    {
                        var s = leftValue as string;
                        return s != null && s.ToLower().Like(((string) rightValue).ToLower());
                    }

                    case ValueOperator.NotLike:
                    {
                        var s = leftValue as string;
                        return s != null && !s.ToLower().Like(((string) rightValue).ToLower());
                    }

                    case ValueOperator.Intersects:
                    {
                        return rightValue.AsSet().Contains(leftValue);
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (RuntimeBinderException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// http://stackoverflow.com/questions/5417070/c-sharp-version-of-sql-like
    /// </summary>
    static class ValueExpressionExtensions
    {
        public static bool Like(this string toSearch, string toFind)
        {
            return new Regex(@"\A" + new Regex(@"\.|\$|\^|\{|\[|\(|\||\)|\*|\+|\?|\\").Replace(toFind, ch => @"\" + ch).Replace('_', '.').Replace("%", ".*") + @"\z", RegexOptions.Singleline).IsMatch(toSearch);
        }

        public static HashSet<object> AsSet(this object value)
        {
            var str = value.ToString();
            if (str.StartsWith("[") && str.EndsWith("]"))
            {
                return JsonConvention.DeserializeObject<HashSet<object>>(str);
            }
            return new HashSet<object>() { value };
        }
    }

    public enum ValueOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        SmallerThan,
        GreaterOrEqual,
        SmallerOrEqual,
        Like,
        NotLike,
        Intersects
    }

}