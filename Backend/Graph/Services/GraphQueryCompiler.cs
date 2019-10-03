using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using Irony.Parsing;
using NetworkPerspective.Parsers;
using WebPerspective.Areas.Graph.Metrics;

namespace WebPerspective.Areas.Graph.Services
{  

    public interface IGraphQueryCompiler
    {
        List<CompiledGraphQuery> Compile(string query);
    }

    public class GraphQueryCompiler : IGraphQueryCompiler
    {
        private static readonly LanguageData Language = new LanguageData(new GraphQueryGrammar());            

        public List<CompiledGraphQuery> Compile(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new GraphQuerySyntaxError("Query is an empty string");

            var parser = new Parser(Language);
            var parseTree = parser.Parse(query);
            if (parseTree == null || parseTree.Status == ParseTreeStatus.Error)
                throw new GraphQuerySyntaxError(parseTree?.ParserMessages?.Select(l=>l.Message + " (line:"+(l.Location.Line+1)+", char:"+ l.Location.Position+")").FirstOrDefault());

            var result = CompileQueryList(parseTree);

            return result;
        }

        private List<CompiledGraphQuery> CompileQueryList(ParseTree parseTree)
        {
            var result = new List<CompiledGraphQuery>();
            foreach (var node in parseTree.Root.ChildNodes)
            {
                var query = CompileQuery(node);
                result.Add(query);
            }
            return result;
        }

        private CompiledGraphQuery CompileQuery(ParseTreeNode parseTree)
        {
            var result = new CompiledGraphQuery();
            foreach (var node in parseTree.ChildNodes)
            {
                switch (node.Term.Name)
                {
                    case "props":
                        result.SelectPropsClause = ParseSelectPropsClause(node);
                        break;

                    case "whereClauseOpt":
                        result.WhereClause = ParseWhereClause(node);
                        break;

                    case "calculateClauseOpt":
                        result.CalculateClause = ParseCalculateClause(node);
                        break;

                    case "groupByClauseOpt":
                        result.GroupByClause = ParseGroupByClause(node);
                        break;

                    case "layoutClauseOpt":
                        result.LayoutClause = ParseLayoutClause(node);
                        break;                        
                }
            }
            return result;
        }

        private LayoutClause ParseLayoutClause(ParseTreeNode node)
        {
            if (node.ChildNodes.Count == 0) return null;
            var result = new LayoutClause();
            var props = ParseLayoutProps(node.ChildNodes[1]);
            foreach (var prop in props)
            {
                result.SetProperty(prop.Key, prop.Value);
            }
            return result;
        }

        private Dictionary<string,string> ParseLayoutProps(ParseTreeNode propsNode)
        {
            var result = new Dictionary<string, string>();
            foreach (var node in propsNode.ChildNodes)
            {
                var prop = ParseLayoutProp(node);
                result.Add(prop.Key, prop.Value);
            }
            return result;
        }

        private KeyValuePair<string,string> ParseLayoutProp(ParseTreeNode node)
        {
            return new KeyValuePair<string, string>(
                node.ChildNodes[0].FindTokenAndGetText(),
                node.ChildNodes[2].FindTokenAndGetText()
            );
        }

        private GroupByClause ParseGroupByClause(ParseTreeNode node)
        {
            if (node.ChildNodes.Count == 0) return null;

            // the clause is now limited to a sinlge props
            var result = new GroupByClause()
            {
                GroupingProp = node.ChildNodes[1].ChildNodes[0].FindToken().Value.ToString()
            };
            return result;
        }

        private CalculateClause ParseCalculateClause(ParseTreeNode node)
        {
            if (node.ChildNodes.Count == 0) return null;

            var result = new CalculateClause()
            {
                Metrics = ParseMetricList(node.ChildNodes[1])
            };
            return result;
        }

        private List<IMetric> ParseMetricList(ParseTreeNode metricListNode)
        {
            var result = new List<IMetric>();
            foreach (var node in metricListNode.ChildNodes)
            {
                IMetric metric = ParseMetric(node);
                result.Add(metric);
            }
            return result;
        }

        private IMetric ParseMetric(ParseTreeNode metricNode)
        {
            switch (metricNode.ChildNodes[0].FindTokenAndGetText().ToLower())
            {
                case "degree":
                    return new DegreeMetric()
                    {
                        Expression = FirstExpressionParam(metricNode)
                    };
                case "in_degree":
                    return new InDegreeMetric()
                    {
                        Expression = FirstExpressionParam(metricNode)
                    };
                case "out_degree":
                    return new OutDegreeMetric()
                    {
                        Expression = FirstExpressionParam(metricNode)
                    };
                case "path_length":
                    return new PathLengthMetric()
                    {
                        StartCondition = FirstExpressionParam(metricNode),
                        EdgeCondition = SecondExpressionParam(metricNode),
                    };
                case "eigenvector":
                    return new EigenvectorMetric()
                    {
                        EdgeCondition = FirstExpressionParam(metricNode),
                        LengthPropIdentifier = SecondIdentifier(metricNode)
                        
                    };
                case "betweenness":
                    return new BetweennessMetric()
                    {
                        EdgeCondition = FirstExpressionParam(metricNode),
                        LengthPropIdentifier = SecondIdentifier(metricNode)
                    };
                //case "js":
                //    not sure if this is safe (but working)
                //    return new JsScriptMetric()
                //    {
                //        Script = metricNode.ChildNodes[1].ChildNodes[0].Token.ValueString
                //    };
            }

            throw new GraphQuerySyntaxError("Invalid metric or algorithm name");
        }

        private IIdentifier SecondIdentifier(ParseTreeNode metricNode)
        {
            return metricNode.ChildNodes.Count > 1 && metricNode.ChildNodes[1].ChildNodes.Count > 1
                ? ParseIdentifier(metricNode.ChildNodes[1].ChildNodes[1]) : null;
        }

        private IExpression FirstExpressionParam(ParseTreeNode metricNode)
        {
            return metricNode.ChildNodes.Count > 1 ? ParseExpression(metricNode.ChildNodes[1].ChildNodes[0]) : null;
        }
        private IExpression SecondExpressionParam(ParseTreeNode metricNode)
        {
            return metricNode.ChildNodes.Count > 2 ? ParseExpression(metricNode.ChildNodes[2].ChildNodes[0]) : null;
        }

        private SelectPropsClause ParseSelectPropsClause(ParseTreeNode node)
        {
            var result = new SelectPropsClause();
            foreach (var prop in node.ChildNodes)
            {
               
                string alias = null;
                if (prop.ChildNodes[1].ChildNodes.Count > 0)
                    alias = prop.ChildNodes[1].ChildNodes[1].ChildNodes[0].Token.ValueString;

                if (string.Equals(prop.ChildNodes[0]?.Term?.Name,
                    "wildcardIdentifier", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (prop.ChildNodes[0].ChildNodes.Count == 1)
                    {
                        result.AddProp(
                            ParseWildcardIdentifier(prop.ChildNodes[0], alias)
                        );
                    }
                    else
                    {
                        ParseEdgeIdentifier(prop.ChildNodes[0], alias, result);
                    }
                }
                else
                {
                    result.AddProp(
                        ParseSelectExpression(prop.ChildNodes[0], alias)
                    );
                }
            }
            return result;
        }

        private IProp ParseSelectExpression(ParseTreeNode node, string alias)
        {
            if (string.Equals(node.Term.Name, "string", StringComparison.InvariantCultureIgnoreCase))
                return new ValueProp()
                {
                    Value = node.Token.ValueString
                };
            if (string.Equals(node.Term.Name, "wildcardIdentifier", StringComparison.InvariantCultureIgnoreCase))
                return ParseWildcardIdentifier(node, alias);

            var left = ParseSelectExpression(node.ChildNodes[0], alias);
            var right = ParseSelectExpression(node.ChildNodes[2], alias);
            if (string.Equals("union", node.ChildNodes[1].Term.Name, StringComparison.InvariantCultureIgnoreCase))
                return new UnionExpressionProp(left, right, alias);
            if (string.Equals("like", node.ChildNodes[1].Term.Name, StringComparison.InvariantCultureIgnoreCase))
                return new LikeExpressionProp(left, right, alias);
            return null;
        }

        private IProp ParseWildcardIdentifier(ParseTreeNode id, string alias)
        {

            // name prop
            var name = id.ChildNodes[0].Token.ValueString;
            if (name == "*")
                return (new WildcardProp());
            else
                return (new NameProp() {Name = name, Alias = alias});
        }

        private void ParseEdgeIdentifier(ParseTreeNode id, string alias, SelectPropsClause result)
        {
            //edge prop
            string firstToken = id.ChildNodes[0].Token.ValueString.ToLower();
            if (firstToken == "edge" || firstToken == "edges")
            {
                if (id.ChildNodes.Count == 2)
                {
                    result.AddEdgeFilter(WildcardOrName(id.ChildNodes[1]), alias);
                    return;
                }
                if (id.ChildNodes.Count == 3)
                {
                    result.AddEdgePropFilter(WildcardOrName(id.ChildNodes[1]),
                        WildcardOrName(id.ChildNodes[2]), alias);
                    return;
                }
            }
            
            throw new GraphQuerySyntaxError("unknown property identifier");
        }

        private string WildcardOrName(ParseTreeNode node)
        {
            var result = node.Token?.ValueString ?? node.FindTokenAndGetText();
            return result.Trim('"').TrimStart('[').TrimEnd(']');
        }



        private WhereClause ParseWhereClause(ParseTreeNode node)
        {
            if (node.ChildNodes.Count == 0) return null;

            var result = new WhereClause
            {
                Expression = ParseExpression(node.ChildNodes[1])
            };
            return result;
        }

        private IExpression ParseExpression(ParseTreeNode node)
        {
            switch (node.Term.Name)
            {
                case "edgeTraversalExpression":
                    return ParseEdgeTraversalExpression(node);
                    
                case "valueExpression":
                    return ParseValueExpression(node);
                    
                case "booleanTerminal":
                    return ParseBooleanTerminal(node);                    

                case "binaryExpression":
                    return ParseBinaryExpression(node);
                    
            }
            throw new GraphQuerySyntaxError("unknown expression: "+node.Term.Name);
        }

        private IExpression ParseBooleanTerminal(ParseTreeNode node)
        {
            switch (node.ChildNodes[0].Token.ValueString.ToLower())
            {
                case "any":
                    //return new BooleanValueExpression() {Value = true};
                    return new ValueExpression()
                    {
                        Left = new EdgeTraversalIdentifier(EdgeDirection.Source, "id"),
                        Right = new EdgeTraversalIdentifier(EdgeDirection.Target, "id"),
                        ValueOperator = ValueOperator.NotEquals
                    };
                case "true":
                    return new BooleanValueExpression() { Value = true };
                case "false":
                    return new BooleanValueExpression() { Value = false };
            }
            throw new GraphQuerySyntaxError("boolean terminal");
        }

        private IExpression ParseBinaryExpression(ParseTreeNode node)
        {
            BinaryOperator op;
            switch (node.ChildNodes[1].Token.ValueString.ToLower())
            {
                case "or":
                    op = BinaryOperator.Or;
                    break;                    
                case "and":
                    op = BinaryOperator.And;
                    break;

                default:
                    throw new GraphQuerySyntaxError("unknown binary operator");
            }
            return new BinaryExpression()
            {
                Left = ParseExpression(node.ChildNodes[0]),
                BinaryOperator = op,
                Right = ParseExpression(node.ChildNodes[2])
            };
        }

        private IExpression ParseEdgeTraversalExpression(ParseTreeNode node)
        {
            EdgeTraversalScope scope;
            switch (node.ChildNodes[0].FindTokenAndGetText().ToLower())
            {
                case "edge":
                    scope = EdgeTraversalScope.All;
                    break;
                case "in_edge":
                    scope = EdgeTraversalScope.In;
                    break;
                case "out_edge":
                    scope = EdgeTraversalScope.Out;
                    break;
                case "mutual_edge":
                    scope = EdgeTraversalScope.Mutual;
                    break;
                default:
                    throw new GraphQuerySyntaxError("invalid edge traversal expression");
            }
            return new EdgeTraversalExpression()
            {
                Scope = scope,
                Expression = ParseExpression(node.ChildNodes[1])
            };
        }

        private IExpression ParseValueExpression(ParseTreeNode node)
        {
            return new ValueExpression()
            {
                Left = ParseIdentifier(node.ChildNodes[0]),
                ValueOperator = ParseValueOperator(node.ChildNodes[1]),
                Right = ParseIdentifier(node.ChildNodes[2]),
            };
        }

        private ValueOperator ParseValueOperator(ParseTreeNode node)
        {
            switch (node.ChildNodes[0].Token.ValueString.ToLower())
            {
                case "=":
                    return ValueOperator.Equals;
                case "!=":
                    return ValueOperator.NotEquals;
                case ">":
                    return ValueOperator.GreaterThan;
                case "<":
                    return ValueOperator.SmallerThan;
                case ">=":
                    return ValueOperator.GreaterOrEqual;
                case "<=":
                    return ValueOperator.SmallerOrEqual;
                case "like":
                    return ValueOperator.Like;
                case "not":
                    if (node.ChildNodes[1].Token.ValueString.ToLower() == "like")
                        return ValueOperator.NotLike;
                    break;
                case "intersects":
                    return ValueOperator.Intersects;
            }
            throw new GraphQuerySyntaxError("unknown operator");
        }

        private IIdentifier ParseIdentifier(ParseTreeNode node)
        {
            switch (node.Term.Name)
            {
                case "string":
                    return new Value(node.Token.ValueString);

                case "number":
                    var s = node.Token.ValueString;
                    if (s.Contains("."))
                        return new Value(Convert.ToDouble(s));
                    else 
                        return new Value(Convert.ToInt64(s));

                case "identifier":
                    if (node.ChildNodes.Count == 1)
                        return new PropIdentifier(node.ChildNodes[0].Token.ValueString);
                    if (node.ChildNodes.Count == 2)
                    {
                        switch (node.ChildNodes[0].Token.ValueString.ToLower())
                        {
                            case "source":
                                return new EdgeTraversalIdentifier(EdgeDirection.Source, node.ChildNodes[1].Token.ValueString);
                            case "target":
                                return new EdgeTraversalIdentifier(EdgeDirection.Target, node.ChildNodes[1].Token.ValueString);
                        }                        
                    }
                    break;
            }
            throw new GraphQuerySyntaxError("unknown identifier");
        }
    }

    public class GraphQuerySyntaxError : Exception
    {
        public GraphQuerySyntaxError(string msg) : base(msg) { }
    }

}