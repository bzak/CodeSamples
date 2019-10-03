using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Irony.Parsing;

namespace NetworkPerspective.Parsers
{
    [Language("GraphQueryGrammar", "1.0", "Graph query grammar")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class GraphQueryGrammar : Grammar
    {
        public GraphQueryGrammar() : base(false)
        {
            // comments
            var comment = new CommentTerminal("comment", "/*", "*/");
            //var lineComment = new CommentTerminal("line_comment", "--", "\n", "\r\n");
            var lineComment = new CommentTerminal("line_comment", "//", "\r", "\n", "\u2085", "\u2028", "\u2029");
            NonGrammarTerminals.Add(comment);
            NonGrammarTerminals.Add(lineComment);

            // terminals
            var string_literal = new StringLiteral("string", "'", StringOptions.AllowsDoubledQuote);
            var number = new NumberLiteral("number", NumberOptions.AllowSign);
            var name = TerminalFactory.CreateSqlExtIdentifier(this, "name"); //covers normal identifiers (abc) and quoted id's ([abc d], "abc d")
            var comma = ToTerm(",");
            var dot = ToTerm(".");            
            var SELECT = ToTerm("SELECT");
            var WHERE = ToTerm("WHERE");
            var CALCULATE = ToTerm("CALCULATE");
            var GROUP_BY = ToTerm("GROUP BY");
            var LAYOUT = ToTerm("LAYOUT");
            var NOT = ToTerm("NOT");

            // non terminals
            var identifier = new NonTerminal("identifier");
            var wildcardIdentifier = new NonTerminal("wildcardIdentifier");
            var statementList = new NonTerminal("statementList");
            var query = new NonTerminal("query");
            var selectPropsClause = new NonTerminal("selectPropsClause");
            var whereClauseOpt = new NonTerminal("whereClauseOpt");
            var calculateClauseOpt = new NonTerminal("calculateClauseOpt");
            var groupByClauseOpt = new NonTerminal("groupByClauseOpt");
            var layoutClauseOpt = new NonTerminal("layoutClauseOpt");
            var metricList = new NonTerminal("metricList");
            var metric = new NonTerminal("metric");
            var metricParamsOpt = new NonTerminal("metricParamsOpt");
            var metricParam  = new NonTerminal("metricParam");
            var metricParamsList = new NonTerminal("metricParamsList");


            var props = new NonTerminal("props");
            var prop = new NonTerminal("prop");
            var propAliasOpt = new NonTerminal("propAliasOpt");
            var expression = new NonTerminal("expression");
            var parenthesizedExpression = new NonTerminal("parenthesizedExpression");
            var edgeTraversalExpression = new NonTerminal("edgeTraversalExpression");
            var valueExpression = new NonTerminal("valueExpression");            
            var valueOperator = new NonTerminal("valueOperator");
            var term = new NonTerminal("term");
            var binaryExpression = new NonTerminal("binaryExpression");
            var booleanTerminal = new NonTerminal("booleanTerminal");
            var binaryOperator = new NonTerminal("binaryOperator");

            var assignmentExpression = new NonTerminal("assignmentExpression");
            var layoutProps = new NonTerminal("layoutProps");            

            var selectPropExpression= new NonTerminal("selectPropExpression");
            var parenthesizedSelectExpression = new NonTerminal("parenthesizedSelectExpression");
            var binarySelectExpression= new NonTerminal("binarySelectExpression");
            var binarySelectOperator = new NonTerminal("binarySelectOperator");

            this.Root = statementList;
            statementList.Rule = MakePlusRule(statementList, query);

            // SELECT statement
            query.Rule = SELECT + selectPropsClause + whereClauseOpt + calculateClauseOpt + groupByClauseOpt + layoutClauseOpt;
            
            // select column list
            selectPropsClause.Rule = props;// | "*";
            prop.Rule = selectPropExpression + propAliasOpt;
            propAliasOpt.Rule = (Empty | "AS" + identifier);
            props.Rule = MakePlusRule(props, comma, prop);

            wildcardIdentifier.Rule = MakePlusRule(wildcardIdentifier, dot, (name | "*" ));
            identifier.Rule = MakePlusRule(identifier, dot, name);
            selectPropExpression.Rule = (wildcardIdentifier |  binarySelectExpression | parenthesizedSelectExpression | string_literal | number);
            parenthesizedSelectExpression.Rule = ToTerm("(") + selectPropExpression + ")";
            binarySelectExpression.Rule =  selectPropExpression + binarySelectOperator + selectPropExpression;
            binarySelectOperator.Rule = ToTerm("UNION") | "LIKE";

            // WHERE clause 
            whereClauseOpt.Rule = Empty | WHERE + expression;

            expression.Rule = edgeTraversalExpression | valueExpression | parenthesizedExpression | binaryExpression | booleanTerminal;

            edgeTraversalExpression.Rule = (ToTerm("edge") | "in_edge" | "out_edge" | "mutual_edge") + "(" + expression + ")";

            parenthesizedExpression.Rule = ToTerm("(") + expression + ")";

            valueExpression.Rule = term + valueOperator + term;
            valueOperator.Rule = ToTerm("=") | "!=" | ">" | "<" | ">=" | "<=" | "LIKE" | NOT + "LIKE" | "INTERSECTS";

            term.Rule = identifier | string_literal | number;            

            booleanTerminal.Rule = ToTerm("any") | "true" | "false";

            binaryExpression.Rule = expression + binaryOperator + expression;
            binaryOperator.Rule = ToTerm("AND") | "OR";

            //// CALCULATE clause            
            calculateClauseOpt.Rule = Empty | CALCULATE + metricList;

            metricList.Rule = MakePlusRule(metricList, comma, metric);
            metric.Rule = identifier + metricParamsOpt;
            metricParamsOpt.Rule = Empty | "(" + metricParamsList + ")";
            metricParam.Rule = expression | identifier | string_literal | number;
            metricParamsList.Rule = MakePlusRule(metricParamsList, comma, metricParam);

            //// GROUP BY clause
            groupByClauseOpt.Rule = Empty | GROUP_BY + metricList;

            // LAYOUT clause
            layoutClauseOpt.Rule = Empty | LAYOUT + layoutProps;
            layoutProps.Rule = MakePlusRule(assignmentExpression, comma, assignmentExpression);
            assignmentExpression.Rule = term + "=" + term;

            // OPERATORS
            MarkTransient(expression, parenthesizedExpression, binaryOperator, selectPropsClause, term, metricParamsOpt, metricParam, selectPropExpression, parenthesizedSelectExpression,
                binarySelectOperator);
            MarkPunctuation(",", "(", ")");
            RegisterBracePair("(", ")");

            RegisterOperators(80, "=", ">", "<", ">=", "<=", "<>", "!=", "!<", "!>", "LIKE", "IN");
            RegisterOperators(70, "UNION", "LIKE");
            RegisterOperators(60, NOT);
            RegisterOperators(50, "AND");
            RegisterOperators(40, "OR");

            //AddToNoReportGroup(selectPropsClause, "-");
        }

        //Covers simple identifiers like abcd, and also quoted versions: [abc d], "abc d".
        public static IdentifierTerminal CreateSqlExtIdentifier(Grammar grammar, string name)
        {
            var id = CreateTerm(name);
            StringLiteral term = new StringLiteral(name + "_qouted");
            term.AddStartEnd("[", "]", StringOptions.NoEscapes);
            term.AddStartEnd("\"", StringOptions.NoEscapes);
            term.SetOutputTerminal(grammar, id); //term will be added to NonGrammarTerminals automatically 
            return id;
        }

        // Creates extended identifier terminal that allows international characters
        // Following the pattern used for c# identifier terminal in TerminalFactory.CreateCSharpIdentifier method;
        private static IdentifierTerminal CreateTerm(string name)
        {
            IdentifierTerminal term = new IdentifierTerminal(name, "!@#$%^*_'.?-", "!@#$%^*_'.?0123456789");
            term.CharCategories.AddRange(new UnicodeCategory[]
            {
                UnicodeCategory.UppercaseLetter, //Ul
                UnicodeCategory.LowercaseLetter, //Ll
                UnicodeCategory.TitlecaseLetter, //Lt
                UnicodeCategory.ModifierLetter, //Lm
                UnicodeCategory.OtherLetter, //Lo
                UnicodeCategory.LetterNumber, //Nl
                UnicodeCategory.DecimalDigitNumber, //Nd
                UnicodeCategory.ConnectorPunctuation, //Pc
                UnicodeCategory.SpacingCombiningMark, //Mc
                UnicodeCategory.NonSpacingMark, //Mn
                UnicodeCategory.Format //Cf
            });
            //StartCharCategories are the same
            term.StartCharCategories.AddRange(term.CharCategories);
            return term;
        }
    }
}
