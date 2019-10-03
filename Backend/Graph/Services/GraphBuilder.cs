using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Web;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Commons.Extensions;

namespace WebPerspective.Areas.Graph.Services
{
    public interface IGraphBuilder
    {
        Dictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel> GenerateConcurrentEdgeModel(GraphSchemaModel schema,
            InternalGraphEdgesResult edges);

        Dictionary<Guid, ConcurrentVertexModel> GenerateConcurrentVertexModel(GraphSchemaModel schema,
            InternalGraphVerticesResult vertices);
    }

    public class GraphBuilder : IGraphBuilder
    {
        public Dictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel> GenerateConcurrentEdgeModel(GraphSchemaModel schema, InternalGraphEdgesResult edges)
        {
            // 
            var uriNameDictionary = schema.EdgeSchema.ToDictionary(e => e.Uri, e => e.Name);

            // source, target, edgeName, propName => timestamp
            var propTimestamps = new Dictionary<Tuple<Guid, Guid, string, string>, DateTime>();

            // edges
            var edgesResult = new Dictionary<Tuple<Guid, Guid, string>, ConcurrentEdgeModel>();
            foreach (var relationshipSchema in schema.EdgeSchema)
            {
                // możliwe jest wiele relacji do różnych węzłów (a każda może mieć wiele propsów)
                Dictionary<Tuple<Guid, Guid>, InternalGraphEdgesResult.InternalEdgeModel> edgeBucket;
                if (!edges.Edges.TryGetValue(relationshipSchema.Uri, out edgeBucket)) continue;

                foreach (var edge in edgeBucket)
                {
                    var edgeKey = new Tuple<Guid, Guid, string>(edge.Key.Item1, edge.Key.Item2, uriNameDictionary[relationshipSchema.Uri]);
                    // find model 
                    ConcurrentEdgeModel edgeModel;
                    if (!edgesResult.TryGetValue(edgeKey, out edgeModel))
                    {
                        edgeModel = new ConcurrentEdgeModel()
                        {
                            Id = edge.Value.Id,
                            SourceVertexId = edge.Value.SourceId,
                            TargetVertexId = edge.Value.TargetId,
                            Name = relationshipSchema.Name,
                            Props = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        };
                        edgesResult.Add(edgeKey, edgeModel);
                    }

                    if (relationshipSchema.PropsSchema != null)
                    {
                        foreach (var propSchema in relationshipSchema.PropsSchema)
                        {
                            if (!edge.Value.PropsJson.ContainsKey(propSchema.Uri)) continue;
                            if (edgeModel.Props.ContainsKey(propSchema.Name)) continue;

                            var prop = edge.Value.PropsJson[propSchema.Uri];
                            if (!edgeModel.Props.ContainsKey(propSchema.Name))
                            {
                                var value = JsonConvention.DeserializeObject(prop.JsonValue);
                                edgeModel.Props.TryAdd(propSchema.Name, value);
                            }
                            else
                            {
                                // check timestamp for update (newer props override older)
                                var timestampKey = new Tuple<Guid, Guid, string, string>(edgeModel.SourceVertexId,
                                    edgeModel.TargetVertexId, edgeModel.Name, propSchema.Name);
                                var timestamp = propTimestamps[timestampKey];
                                if (timestamp < prop.TimeStamp)
                                {
                                    var value = JsonConvention.DeserializeObject(prop.JsonValue);
                                    edgeModel.Props.TryAdd(propSchema.Name, value);
                                }
                            }
                        }
                    }
                }
            }
            return edgesResult;
        }

        class TupeGuidStringComparer : IEqualityComparer<Tuple<Guid, string>>
        {
            public bool Equals(Tuple<Guid, string> x, Tuple<Guid, string> y)
            {
                return x?.Item1 == y?.Item1 &&
                       string.Equals(x?.Item2, y?.Item2, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(Tuple<Guid, string> obj)
            {
                return obj.GetHashCode();
            }
        }
        public Dictionary<Guid, ConcurrentVertexModel> GenerateConcurrentVertexModel(GraphSchemaModel schema, InternalGraphVerticesResult vertices)
        {
            var vertexIdPropNameTimeStamp = new Dictionary<Tuple<Guid, String>, DateTime>(new TupeGuidStringComparer());
            var verticesResult = new Dictionary<Guid, ConcurrentVertexModel>();
            var processedPropUris = new HashSet<string>();

            foreach (var sectionSchema in schema.VertexSchema)
            {
                if (sectionSchema.PropsSchema != null)
                foreach (var propSchema in sectionSchema.PropsSchema)
                {
                    List<InternalGraphVerticesResult.InternalVertexPropModel> propsBucket;
                    if (!vertices.WithProps.TryGetValue(propSchema.Uri, out propsBucket)) continue;

                    processedPropUris.Add(propSchema.Uri);
                    foreach (var prop in propsBucket)
                    {
                        ConcurrentVertexModel vertex;
                        if (!verticesResult.TryGetValue(prop.VertexId, out vertex))
                        {
                            vertex = new ConcurrentVertexModel()
                            {
                                Id = prop.VertexId,
                                Props = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            };
                            verticesResult.Add(prop.VertexId, vertex);
                        }
                        
                        if (!vertex.Props.ContainsKey(propSchema.Name))
                        {
                            var value = JsonConvention.DeserializeObject(prop.JsonValue);
                            vertex.Props.TryAdd(propSchema.Name, value);
                            vertexIdPropNameTimeStamp.Add(new Tuple<Guid, string>(vertex.Id, propSchema.Name),
                                prop.TimeStamp);
                        }
                        else
                        {
                            // should we override the value?
                            DateTime lastTimeStamp =
                                vertexIdPropNameTimeStamp[new Tuple<Guid, string>(vertex.Id, propSchema.Name)];
                            if (lastTimeStamp < prop.TimeStamp)
                            {
                                var value = JsonConvention.DeserializeObject(prop.JsonValue);
                                vertex.Props[propSchema.Name] = value;
                            }
                        }
                    }
                }
            }

            // handle props outside schema
            var outsideSchema = vertices.WithProps.Where(v => ! processedPropUris.Contains(v.Key));
            foreach (var propsBucket in outsideSchema)
            {
                var name = propsBucket.Key.Split('/').Last();
                foreach (var prop in propsBucket.Value)
                {
                    ConcurrentVertexModel vertex;
                    if (!verticesResult.TryGetValue(prop.VertexId, out vertex))
                    {
                        vertex = new ConcurrentVertexModel()
                        {
                            Id = prop.VertexId,
                            Props = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        };
                        verticesResult.Add(prop.VertexId, vertex);
                    }

                    if (!vertex.Props.ContainsKey(name))
                    {
                        var value = JsonConvention.DeserializeObject(prop.JsonValue);
                        vertex.Props.TryAdd(name, value);
                        vertexIdPropNameTimeStamp.Add(new Tuple<Guid, string>(vertex.Id, name),
                            prop.TimeStamp);
                    }
                    else
                    {
                        // should we override the value?
                        DateTime lastTimeStamp =
                            vertexIdPropNameTimeStamp[new Tuple<Guid, string>(vertex.Id, name)];
                        if (lastTimeStamp < prop.TimeStamp)
                        {
                            var value = JsonConvention.DeserializeObject(prop.JsonValue);
                            vertex.Props[name] = value;
                        }
                    }
                }
            }

            // vertices witour props
            foreach (var withoutProp in vertices.WithoutProps)
            {
                verticesResult.Add(withoutProp, new ConcurrentVertexModel()
                {
                    Id = withoutProp,
                    Props = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                });
            }
            return verticesResult;
        }
    }
}