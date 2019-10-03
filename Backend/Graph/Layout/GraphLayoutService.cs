using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Common.Logging;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NetworkPerspective.Parsers;
using Quartz.Util;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Areas.Graph.Queries;
using WebPerspective.Areas.Settings.Services;
using WebPerspective.Commons.Extensions;
using WebPerspective.CQRS;
using WebPerspective.CQRS.Authorization;
using WebPerspective.CQRS.Commands;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Layout
{


    public class LayoutTask
    {
        public SigmaQuery Query { get; set; }        
        public LayoutSettings Settings { get; set; }
        public bool ModifyLayout { get; set; } = true;
    }

    public interface IGraphLayoutService
    {
        Task LayoutGraph(SigmaGraphModel graph, LayoutTask layoutTask, TimeSpan? duration = null);
        IBackgroundCommandDispatcher Background { get; set; }
        IAuthContext Auth { get; set; }
    }

    public class GraphLayoutService : IGraphLayoutService
    {
        private static readonly Object LayoutLock = new Object();

        private readonly TimeSpan _firstTimeLayoutDuration = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _missingNodesImproveLayoutDuration = TimeSpan.FromMilliseconds(400);
        private readonly TimeSpan _improveLayoutInBackgroundDuration = TimeSpan.FromMilliseconds(ApplicationSettings.ImproveLayoutInBackgroundDurationMs);

        private readonly IRepository _repository;
        public IBackgroundCommandDispatcher Background { get; set; }
        public IAuthContext Auth { get; set; }
        private readonly ILog _log;

        public GraphLayoutService(IRepository repository, IBackgroundCommandDispatcher background, ILog log)
        {
            _repository = repository;
            Background = background;
            _log = log;
        }

        /// <summary>
        /// 1) jeśli nie layoutu ma to utwórz nowy layout, synchronicznie odpal layout i dodaj do bazy
        /// 2) jeśli jest już layout to go wczytaj
        ///    2a) jeśli któryś z węzłow nie ma współżędnych to synchronicznie odpal layout ale na krótkie 0.5 sekundy
        ///    2b) zakolejkuj kolejny krok rozłożenia z niskim priorytetem
        /// </summary>
        public async Task LayoutGraph(SigmaGraphModel graph, LayoutTask layoutTask, TimeSpan? duration)
        {
            if (Guid.Empty.Equals(layoutTask.Query.NetworkId)) throw new ArgumentNullException(nameof(layoutTask.Query.NetworkId));
            if (graph.Nodes == null) return;

            var networkId = layoutTask.Query.NetworkId;
            var layout = await _repository.Db.Layouts.SingleOrDefaultAsync(l => l.NetworkId == networkId && l.Key == layoutTask.Query.LayoutKey);

            // Ad 1
            if (layout == null)
            {
                lock (LayoutLock)
                {
                    // prevent entering here from many threads
                    layout = _repository.Db.Layouts.SingleOrDefault(
                            l => l.NetworkId == networkId && l.Key == layoutTask.Query.LayoutKey);
                    if (layout == null)
                    {
                        layout = new Entities.Layout()
                        {
                            Id = Guid.NewGuid(),
                            NetworkId = networkId,
                            Key = layoutTask.Query.LayoutKey
                        };
                        _repository.Db.Layouts.Add(layout);
                        _repository.Db.SaveChanges();
                    }
                }
                ApplyLayout(graph, null);

                var graphLayout = new GraphLayout();
                ImproveAppliedLayout(graph, graphLayout, layoutTask.Settings,
                    duration ?? _firstTimeLayoutDuration);

                // save layout
                layout.GraphLayout = graphLayout;
                _repository.Db.SaveChanges();                
            }
            else             
            // Ad 2
            {
                var graphLayout = layout.GraphLayout;
                if (!ApplyLayout(graph, graphLayout))
                {
                    // ad 2a
                    ImproveAppliedLayout(graph, graphLayout, layoutTask.Settings, duration ?? _missingNodesImproveLayoutDuration);
                    layout.GraphLayout = graphLayout;
                    await _repository.Db.SaveChangesAsync();
                }
                else
                {
                    // ad 2b
                    if (Background == null)
                    {
                        ImproveAppliedLayout(graph, graphLayout, layoutTask.Settings, duration ?? _missingNodesImproveLayoutDuration);
                        layout.GraphLayout = graphLayout;
                        await _repository.Db.SaveChangesAsync();
                    }
                    else
                    {
                        _log.Debug("Scheduling background layout");

                        if (layoutTask.ModifyLayout)
                        {
                            Background.Execute(new ImproveLayoutCommand()
                            {
                                Task = layoutTask,
                                Duration = duration ?? _improveLayoutInBackgroundDuration
                            }, new BackgroundPrincipal(Auth.UserName));
                        }
                    }
                }
            }            
        }

        /// <summary>
        /// Assumes layout has been already applied
        /// Note: method mutates graph and graphLayout
        /// </summary>
        public void ImproveAppliedLayout(SigmaGraphModel graph, GraphLayout graphLayout, LayoutSettings layoutSettings, TimeSpan duration)
        {
            using (var engine = new V8ScriptEngine())
            {
                try
                {
                    var v8Graph = new V8GraphModel();
                    v8Graph.LoadSigma(graph);
                    engine.AddHostObject("log", _log);

                    engine.Execute("var graph = " + JsonConvention.SerializeObject(v8Graph) + ";");
                    engine.Execute("var settings = " + JsonConvention.SerializeObject(layoutSettings) + ";");
                    engine.Execute("var duration = " + JsonConvention.SerializeObject(duration.TotalMilliseconds) + ";");

                    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    var forceAtlas2 = engine.Compile(File.ReadAllText($@"{baseDirectory}\App\Graph\Layout\ForceAtlas2.js"));
                    var serviceScript = engine.Compile(File.ReadAllText($@"{baseDirectory}\App\Graph\Layout\GraphLayoutService.js"));
                    engine.Execute(forceAtlas2);
                    engine.Execute(serviceScript);

                    var nodesJson = engine.Evaluate("JSON.stringify(nodes)").ToString();
                    var nodes = JsonConvention.DeserializeObject<V8NodeModel[]>(nodesJson);                    
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var node = graph.Nodes[i];
                        node.X = nodes[i].x;
                        node.Y = nodes[i].y;

                        var id = node.Entity;
                        if (id.HasValue)
                        {
                            graphLayout[id.Value] = new GraphLayout.Coords()
                            {
                                X = nodes[i].x,
                                Y = nodes[i].y
                            };
                        }
                    }    
                                    
                }
                catch (ScriptEngineException e)
                {                    
                    _log.Error("V8 exception: " + e.ErrorDetails);
                    throw;
                }
            }            
        }

        private bool ApplyLayout(SigmaGraphModel graph, GraphLayout graphLayout)
        {            
            var allNodesCovered = true;
            var rnd = new Random();
            foreach (var node in graph.Nodes)
            {
                GraphLayout.Coords coords;

                // should we interpolate vertex position for group by clause
                if (node.Props != null && node.Props.ContainsKey("members")
                    && graph.Data != null && graph.Data.ContainsKey("grouped_vertices"))
                {
                    if (InterpolateCoords(
                        node.Props["members"] as HashSet<int>, 
                        graph.Data["grouped_vertices"] as List<PropertyVertexModel>,
                        graphLayout, rnd, out coords))
                    {
                        node.X = coords.X;
                        node.Y = coords.Y;
                    }
                    else
                    {
                        node.X = rnd.NextDouble();
                        node.Y = rnd.NextDouble();
                        allNodesCovered = false;
                    }
                }
                // no interpolation but layout available
                else if (node.Entity.HasValue && graphLayout != null && graphLayout.TryGetValue(node.Entity.Value, out coords))
                {
                    node.X = coords.X;
                    node.Y = coords.Y;
                }
                else
                {
                    node.X = rnd.NextDouble();
                    node.Y = rnd.NextDouble();
                    allNodesCovered = false;
                }
            }
            return allNodesCovered;
        }

        private bool InterpolateCoords(HashSet<int> members, List<PropertyVertexModel> groupedVertices, GraphLayout graphLayout, Random rnd, out GraphLayout.Coords coords)
        {
            if (members == null || groupedVertices == null || graphLayout == null)
            {
                coords = new GraphLayout.Coords();
                return false;
            }

            coords = new GraphLayout.Coords();
            int count = 0;
            foreach (var member in members)
            {
                var node = groupedVertices[member];
                GraphLayout.Coords nodeCoords;
                if (graphLayout.TryGetValue(node.Id, out nodeCoords))
                {
                    coords.X += nodeCoords.X;
                    coords.Y += nodeCoords.Y;
                    count++;
                }
            }
            if (count > 0)
            {
                coords.X /= (double) count;
                coords.Y /= (double) count;
            }
            else
            {
                coords.X = rnd.NextDouble();
                coords.Y = rnd.NextDouble();
            }
            return true;
        }
    }
}