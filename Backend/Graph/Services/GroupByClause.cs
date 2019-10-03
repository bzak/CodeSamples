using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Services
{
    public class GroupByClause
    {
        public string GroupingProp { get; set; }

        
        public PropertyGraphModel Transform(PropertyGraphModel graph)
        {
            // calculate groups and memberships
            var groupKeyToIndexSet = new Dictionary<object, HashSet<int>>();  // groupKey to vertex index set
            var lpToGroupKeySet = new Dictionary<int, HashSet<object>>();
            for (int index = 0; index < graph.Vertices.Count; index++)
            {
                var vertex = graph.Vertices[index];
                var groupKeyArray = GroupKeyArray(vertex);
                if (groupKeyArray == null) continue;

                foreach (var key in groupKeyArray)
                {
                    if (key == null) continue;
                    if (!groupKeyToIndexSet.ContainsKey(key))
                        groupKeyToIndexSet[key] = new HashSet<int>();
                    groupKeyToIndexSet[key].Add(index);
                    if (!lpToGroupKeySet.ContainsKey(index))
                        lpToGroupKeySet[index] = new HashSet<object>();
                    lpToGroupKeySet[index].Add(key);
                }
            }

            // transform groups to vertices
            var groupVertices = new List<PropertyVertexModel>();
            var groupKeyToGroupIndex = new Dictionary<object, int>();
            var i = 0;
            foreach (var group in groupKeyToIndexSet)
            {
                groupVertices.Add(new PropertyVertexModel()
                {
                    Id = new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                    Props = new Dictionary<string, object>()
                    {
                        {"label", group.Key},
                        {"size", group.Value.Count},
                        {"members", group.Value}
                    }
                });
                groupKeyToGroupIndex[group.Key] = i;
                i++;
            }
            
            // transform edges
            List<PropertyEdgeModel> groupEdgesList = null;
            if (graph.Edges != null)
            {
                var groupEdges = new Dictionary<Tuple<int, int, string>, PropertyEdgeModel>();
                foreach (var edge in graph.Edges)
                {
                    if (!lpToGroupKeySet.ContainsKey(edge.Source)) continue;
                    if (!lpToGroupKeySet.ContainsKey(edge.Target)) continue;

                    var sourceGroupSet = lpToGroupKeySet[edge.Source];
                    var targetGroupSet = lpToGroupKeySet[edge.Target];

                    foreach (var sourceGroup in sourceGroupSet)
                    {
                        var source = groupKeyToGroupIndex[sourceGroup];
                        foreach (var targetGroup in targetGroupSet)
                        {
                            var target = groupKeyToGroupIndex[targetGroup];

                            var name = edge.Name;
                            var key = new Tuple<int, int, string>(source, target, name);
                            if (!groupEdges.ContainsKey(key))
                            {
                                groupEdges[key] = new PropertyEdgeModel()
                                {
                                    Name = name,
                                    Source = source,
                                    Target = target,
                                    SourceVertex = groupVertices[source],
                                    TargetVertex = groupVertices[target],
                                    Props = new Dictionary<string, object>()
                                    {
                                        {"size", 0},
                                        {"connectors", new Dictionary<int, HashSet<int>>()} // member index => other members set
                                    }
                                };
                            }
                            groupEdges[key].Props["size"] = (int)groupEdges[key].Props["size"] + 1;
                            var connectors = groupEdges[key].Props["connectors"] as Dictionary<int, HashSet<int>>;
                            if (!connectors.ContainsKey(edge.Source))
                                connectors[edge.Source] = new HashSet<int>() {edge.Target};
                            else
                                connectors[edge.Source].Add(edge.Target);
                        }
                    }                                                            
                }

                groupEdgesList = groupEdges.Select(kv => kv.Value).ToList();
            }            

            var result = new PropertyGraphModel()
            {
                Vertices = groupVertices,
                Edges = groupEdgesList                
            };
            
            result.CreateLinks();            

            result.Data["grouped_vertices"] = graph.Vertices;

            return result;
        }

        private object[] GroupKeyArray(PropertyVertexModel vertex)
        {
            if (vertex?.Props != null && vertex.Props.ContainsKey(GroupingProp))
                
            {
                var key = vertex.Props[GroupingProp];
                if (key is JArray)
                    return (key as JArray).Select(v => (object) v).ToArray();
                else if (key is Array)
                    return (key as object[]);
                else
                    return new object[] {key};
            }
            else
                return null;
        }
    }
}