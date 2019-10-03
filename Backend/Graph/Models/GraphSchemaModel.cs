using System;
using System.Collections.Generic;
using WebPerspective.Areas.Connectors.Models;

namespace WebPerspective.Areas.Graph.Models
{
    public class GraphSchemaModel
    {
        public string Locale { get; set; }
        public List<GraphProfileSectionModel> VertexSchema { get; set; }
        public List<GraphRelationshipSchemaModel> EdgeSchema { get; set; }
    }

    public class GraphPropertySchemaModel
    {
        public string Name { get; set; }
        public string Uri { get; set; }
        public List<Tuple<string, object>> Values { get; set; }
        public PropertySchemaFlags Flags { get; set; }
    }

    public class GraphRelationshipSchemaModel : GraphSchemaSectionModel { }
    public class GraphProfileSectionModel : GraphSchemaSectionModel { }

    public class GraphSchemaSectionModel
    {
        public string Name { get; set; }
        public string Uri { get; set; }
        public List<GraphPropertySchemaModel> PropsSchema { get; set; }
    }    
}