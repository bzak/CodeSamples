using Microsoft.ClearScript.V8;
using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Metrics
{
    public class JsScriptMetric : IMetric
    {
        public string Script { get; set; }

        public PropertyGraphModel Calculate(PropertyGraphModel graph)
        {
            using (var engine = new V8ScriptEngine())
            {
                engine.AddHostObject("graph", graph);
                engine.Execute(Script);
                return engine.Script.graph;
            }
        }
    }
}