using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.CSharp.RuntimeBinder;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Services;

namespace WebPerspective.Areas.Graph.Services
{
    public interface IGraphTransformation
    {
        PropertyGraphModel Transform(PropertyGraphModel graph);
    }

    public class CompiledGraphQuery : IGraphTransformation
    {
        public WhereClause WhereClause { get; set; }
        public SelectPropsClause SelectPropsClause { get; set; }
        public CalculateClause CalculateClause { get; set; }
        public GroupByClause GroupByClause { get; set; }
        public LayoutClause LayoutClause { get; set; }

        public PropertyGraphModel Transform(PropertyGraphModel graph)
        {
            var result = graph;

            if (WhereClause != null) 
                result = WhereClause.Transform(result);

            if (CalculateClause != null)
                result = CalculateClause.Transform(result);

            if (SelectPropsClause != null)
                result = SelectPropsClause.Transform(result);

            if (GroupByClause != null)
                result = GroupByClause.Transform(result);

            if (LayoutClause != null)
                result = LayoutClause.Transform(result);

            result.ClearIfEmpty();

            return result;
        }
    }
}