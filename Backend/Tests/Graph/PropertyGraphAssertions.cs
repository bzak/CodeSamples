using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Commons.Extensions;

namespace WebPerspective.Tests.Areas.Graph
{
    public class PropertyGraphAssertions
    {
        internal static void AssertGraphsEqual(PropertyGraphModel expected, PropertyGraphModel actual)
        {            
            var left = (JObject)JsonConvert.DeserializeObject(JsonConvention.SerializeObject(SortGraph(expected)));
            var right = (JObject)JsonConvert.DeserializeObject(JsonConvention.SerializeObject(SortGraph(actual)));
            Sort(left); Sort(right);
            Assert.AreEqual(left.ToString(Formatting.None), right.ToString(Formatting.None));
            //Assert.AreEqual(JsonConvention.SerializeObject(SortGraph(expected)), JsonConvention.SerializeObject(SortGraph(actual)));                
        }

        static void Sort(JObject jObj)
        {
            var props = jObj.Properties().ToList();
            foreach (var prop in props)
            {
                prop.Remove();
            }

            foreach (var prop in props.OrderBy(p => p.Name))
            {
                jObj.Add(prop);
                if (prop.Value is JObject)
                    Sort((JObject)prop.Value);
                if (prop.Value is JArray)
                {
                    var arr = prop.Value as JArray;
                    arr.ForEach(el =>
                    {
                        if (el is JObject) Sort(el as JObject);
                    });
                }
            }
        }
        internal static PropertyGraphModel SortGraph(PropertyGraphModel graph)
        {
            graph.CreateLinks();
            if (graph.Vertices != null)
                graph.Vertices = graph.Vertices.OrderBy(v => v.Id).ToList();
            if (graph.Edges != null)
                graph.Edges = graph.Edges.OrderBy(e => new Tuple<Guid, Guid, string>(e.SourceVertex.Id, e.TargetVertex.Id, e.Name)).ToList();
            if (graph.Vertices != null)
            foreach (var vertex in graph.Vertices)
            {
                vertex.Props = vertex.Props != null ? new SortedDictionary<string, object>(vertex.Props) : new SortedDictionary<string, object>();
                vertex.Edges = new List<PropertyEdgeModel>();
            }
            if (graph.Edges != null && graph.Vertices != null)
            foreach (var edge in graph.Edges)
            {
                edge.Source = graph.Vertices.IndexOf(edge.SourceVertex);
                edge.Target = graph.Vertices.IndexOf(edge.TargetVertex);                                
                edge.SourceVertex.Edges.Add(edge);
                edge.SourceVertex.Edges.Add(edge);
                edge.Props = edge.Props != null ? new SortedDictionary<string, object>(edge.Props) : new SortedDictionary<string, object>();
            }
            return graph;
        }

        internal static void AssertGraphsEqual(string expected, string actual)
        {
            AssertGraphsEqual(JsonConvention.DeserializeObject<PropertyGraphModel>(expected), JsonConvention.DeserializeObject<PropertyGraphModel>(actual));
        }
    }
}
