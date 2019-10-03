using WebPerspective.Areas.Graph.Models;

namespace WebPerspective.Areas.Graph.Metrics
{
    public interface IMetric
    {
        PropertyGraphModel Calculate(PropertyGraphModel result);
    }
}