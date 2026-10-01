using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Algorithms;
using NoSQL.GraphDB.Core.Algorithms.Analytics;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Plugin;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

// See GraphAnalyticsTest.cs for more information on how to use graph analytics in Fallen-8.

namespace Fallen8Console;

public class GraphAnalyticsManager
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Fallen8 _fallen8;

    public GraphAnalyticsManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GraphAnalyticsManager>();
        _fallen8 = new Fallen8(loggerFactory);
    }

    public void Dispose()
    {
        _fallen8.Dispose();
    }

    private Int32 Vertex(string label = "person")
    {
        var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

        var tx = new CreateVertexTransaction
        {
            Definition = new VertexDefinition { CreationDate = creationDate, Label = label }
        };
        _fallen8.EnqueueTransaction(tx).WaitUntilFinished();

        return tx.VertexCreated.Id;
    }

    private void Edge(Int32 source, Int32 target, string edgePropertyId = "link")
    {
        var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

        var tx = new CreateEdgeTransaction
        {
            Definition = new EdgeDefinition
            {
                SourceVertexId = source,
                TargetVertexId = target,
                EdgePropertyId = edgePropertyId,
                CreationDate = creationDate
            }
        };
        _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
    }

    private GraphAnalyticsResult Run(string algorithm, GraphAnalyticsDefinition? definition = null)
    {
        // Run the specified graph analytics algorithm with the provided definition (or a default one if null).
        // The default definition will use the whole graph and default parameters for the algorithm
        _fallen8.TryRunAnalytics(out var result, algorithm, definition ?? new GraphAnalyticsDefinition());
        return result;
    }

    public void GetAvailablePlugins()
    {
        PluginFactory.TryGetAvailablePlugins<IGraphAnalyticsAlgorithm>(out var names);
        var set = names.ToHashSet();
        // execpted : "PAGERANK", "WCC", "LABELPROPAGATION", "DEGREE", "TRIANGLECOUNT"
        foreach (var name in set)
        {
            _logger.LogInformation($"Found graph analytics algorithm: {name}");
        }
    }

    #region degree

    public void Degree_Star_HubAndLeaves_InOutBoth()
    {
        var hub = Vertex();
        var leaves = new[] { Vertex(), Vertex(), Vertex() };
        foreach (var leaf in leaves)
        {
            Edge(hub, leaf);
        }

        // Run the degree algorithm in both directions (default)
        var both = Run("DEGREE");
        _logger.LogInformation($"Both directions - Hub 3 : {both.VertexScores[hub]}");
        _logger.LogInformation($"Both directions - Leaf 1 : {both.VertexScores[leaves[0]]}");
        _logger.LogInformation($"Both directions - Max 3 : {both.Statistics["Max"]}");
        _logger.LogInformation($"Both directions - Mean 1.5 : {both.Statistics["Mean"]}");
        _logger.LogInformation($"Both directions - Min 1 : {both.Statistics["Min"]}");
        _logger.LogInformation($"Both directions - Converged: {both.Converged}");

        // Run the degree algorithm in outgoing direction only
        var outOnly = Run("DEGREE", new GraphAnalyticsDefinition { Direction = Direction.OutgoingEdge });
        _logger.LogInformation($"OutOnly - Hub 3 : {outOnly.VertexScores[hub]}");
        _logger.LogInformation($"OutOnly - Leaf 0: {outOnly.VertexScores[leaves[0]]}");
        _logger.LogInformation($"OutOnly - Max: {outOnly.Statistics["Max"]}");
        _logger.LogInformation($"OutOnly - Mean 0.75: {outOnly.Statistics["Mean"]}");
        _logger.LogInformation($"OutOnly - Min 0 : {outOnly.Statistics["Min"]}");
        _logger.LogInformation($"OutOnly - Converged: {outOnly.Converged}");

        // Run the degree algorithm in incoming direction only
        var inOnly = Run("DEGREE", new GraphAnalyticsDefinition { Direction = Direction.IncomingEdge });
        _logger.LogInformation($"InOnly - Hub 0 : {inOnly.VertexScores[hub]}");
        _logger.LogInformation($"InOnly - Leaf 1 : {inOnly.VertexScores[leaves[0]]}");
        _logger.LogInformation($"InOnly - Max 1: {inOnly.Statistics["Max"]}");
        _logger.LogInformation($"InOnly - Mean 0.75 : {inOnly.Statistics["Mean"]}");
        _logger.LogInformation($"InOnly - Min 0 : {inOnly.Statistics["Min"]}");
        _logger.LogInformation($"InOnly - Converged: {inOnly.Converged}");
    }

    public void Degree_ParallelEdges_CountMultiply_AndSelfLoopContributesToBoth()
    {
        var a = Vertex();
        var b = Vertex();
        Edge(a, b);
        Edge(a, b);
        var c = Vertex();
        Edge(c, c);

        var result = Run("DEGREE");
        _logger.LogInformation($"Parallel Edges Count Multiply - a 2 : {result.VertexScores[a]}");
        _logger.LogInformation($"Parallel Edges Count Multiply - b 2 : {result.VertexScores[b]}");
        _logger.LogInformation($"Self Loop Contributes To Both - c 2 : {result.VertexScores[c]}");
    }

    #endregion

    #region pagerank

    public void PageRank_TwoVertexCycle_IsHalfHalf()
    {
        var a = Vertex();
        var b = Vertex();
        Edge(a, b);
        Edge(b, a);

        var result = Run("PAGERANK");
        _logger.LogInformation($"Converged: {result.Converged}");
        _logger.LogInformation($"Vertex Scores - A = 0.5 : {result.VertexScores[a]}");
        _logger.LogInformation($"Vertex Scores - B = 0.5 : {result.VertexScores[b]}");
    }

    public void PageRank_FourVertexGraph_MatchesIndependentlyComputedValues()
    {
        // The classic small fixture: A->B, A->C, B->C, C->A, D->C (d = 0.85).
        // Values computed independently by power iteration to convergence.
        var a = Vertex();
        var b = Vertex();
        var c = Vertex();
        var d = Vertex();
        Edge(a, b);
        Edge(a, c);
        Edge(b, c);
        Edge(c, a);
        Edge(d, c);

        var result = Run("PAGERANK");
        _logger.LogInformation($"Converged: {result.Converged}");

        // Fixed point of: rA = base + d*rC; rB = base + d*rA/2; rC = base + d*(rA/2 + rB + rD);
        // rD = base (dangling D has out-degree 1 to C - no wait, D->C so D is not dangling).
        // With every vertex having out-degree >= 1 there is no dangling mass:
        // rD = (1-d)/4 = 0.0375; solving the linear system gives:
        _logger.LogInformation($"Vertex Scores - A = 0.372526 : {result.VertexScores[a]}");
        _logger.LogInformation($"Vertex Scores - B = 0.195824 : {result.VertexScores[b]}");
        _logger.LogInformation($"Vertex Scores - C = 0.394150 : {result.VertexScores[c]}");
        _logger.LogInformation($"Vertex Scores - D = 0.0375 : {result.VertexScores[d]}");

        var sum = result.VertexScores.Values.Sum();
        _logger.LogInformation($"ranks sum to 1 over in-scope vertices: {sum}");
    }

    public void PageRank_DanglingVertex_RanksStillSumToOne()
    {
        var a = Vertex();
        var b = Vertex();
        var dangling = Vertex();
        Edge(a, b);
        Edge(b, dangling);

        var result = Run("PAGERANK");
        _logger.LogInformation($"Converged: {result.Converged}");

        var sum = result.VertexScores.Values.Sum();
        _logger.LogInformation($"ranks sum to 1 over in-scope vertices: {sum}");
    }

    public void PageRank_DampingZero_IsUniform()
    {
        var a = Vertex();
        var b = Vertex();
        var c = Vertex();
        Edge(a, b);
        Edge(b, c);

        var result = Run("PAGERANK", new GraphAnalyticsDefinition
        {
            Parameters = new Dictionary<string, object> { { "DampingFactor", 0d } }
        });
        _logger.LogInformation($"Converged: {result.Converged}");
        _logger.LogInformation($"Vertex Scores - A = 0.333333 : {result.VertexScores[a]}");
        _logger.LogInformation($"Vertex Scores - B = 0.333333 : {result.VertexScores[b]}");
        _logger.LogInformation($"Vertex Scores - C = 0.333333 : {result.VertexScores[c]}");

        var sum = result.VertexScores.Values.Sum();
        _logger.LogInformation($"ranks sum to 1 over in-scope vertices: {sum}");
    }

    #endregion


    #region Weakly Connected Components (WCC)

    public void Wcc_TwoDisjointChains_TwoComponents_SmallestMemberIds()
    {
        var a1 = Vertex();
        var a2 = Vertex();
        var a3 = Vertex();
        Edge(a1, a2);
        Edge(a2, a3);

        var b1 = Vertex();
        var b2 = Vertex();
        Edge(b2, b1); // direction must not matter

        var result = Run("WCC");

        // Two disjoint chains should yield two components, with the smallest member id as the component id.
        _logger.LogInformation($"Component Count: {result.Statistics["ComponentCount"]}");

        _logger.LogInformation($"Vertex Partition - A1: {result.VertexPartitions[a1]}");
        _logger.LogInformation($"Vertex Partition - A2: {result.VertexPartitions[a2]}");
        _logger.LogInformation($"Vertex Partition - A3: {result.VertexPartitions[a3]}");

        // smallest member id, direction-blind
        _logger.LogInformation($"Vertex Partition - B1: {result.VertexPartitions[b1]}");
        _logger.LogInformation($"Vertex Partition - B2: {result.VertexPartitions[b2]}");
  
        _logger.LogInformation($"Converged: {result.Converged}");
    }

    public void Wcc_SingletonVertex_IsItsOwnComponent()
    {
        var lonely = Vertex();

        var result = Run("WCC");

        // A single vertex with no edges should be its own component.
        _logger.LogInformation($"Component Count: {result.Statistics["ComponentCount"]}");
        _logger.LogInformation($"Vertex Partition - Lonely: {result.VertexPartitions[lonely]}");
    }

    #endregion

    #region label propagation

    public void LabelPropagation_TwoCliquesWithBridge_TwoCommunities_Deterministic()
    {
        // Two triangles joined by one bridge edge.
        var clique1 = new[] { Vertex(), Vertex(), Vertex() };
        var clique2 = new[] { Vertex(), Vertex(), Vertex() };
        foreach (var clique in new[] { clique1, clique2 })
        {
            Edge(clique[0], clique[1]);
            Edge(clique[1], clique[2]);
            Edge(clique[2], clique[0]);
        }
        Edge(clique1[2], clique2[0]);

        var first = Run("LABELPROPAGATION");

        _logger.LogInformation($"Community Count: {first.Statistics["CommunityCount"]}");
        _logger.LogInformation($"Converged: {first.Converged}");

        var community1 = first.VertexPartitions[clique1[0]];
        _logger.LogInformation($"Community 1: {community1}");
        _logger.LogInformation($"Vertex Partition - Clique1[1]: {first.VertexPartitions[clique1[1]]}");
        _logger.LogInformation($"Vertex Partition - Clique1[2]: {first.VertexPartitions[clique1[2]]}");
        
        var community2 = first.VertexPartitions[clique2[0]];
        _logger.LogInformation($"Community 2: {community2}");
        _logger.LogInformation($"Vertex Partition - Clique2[1]: {first.VertexPartitions[clique2[1]]}");
        _logger.LogInformation($"Vertex Partition - Clique2[2]: {first.VertexPartitions[clique2[2]]}");
        
        _logger.LogInformation($"Communities are not equal: {community1}, {community2}");
    }

    public void LabelPropagation_IsolatedVertices_KeepTheirOwnLabels_AndConvergeInOneRound()
    {
        var a = Vertex();
        var b = Vertex();

        var result = Run("LABELPROPAGATION");

        _logger.LogInformation($"Converged: {result.Converged}");

        _logger.LogInformation($"Vertex Partition - A: {result.VertexPartitions[a]}");
        _logger.LogInformation($"Vertex Partition - B: {result.VertexPartitions[b]}");
        _logger.LogInformation($"Community Count: {result.Statistics["CommunityCount"]}");
    }

    #endregion

    #region triangles

    public void Triangles_K4_HasFourTriangles_ThreePerVertex()
    {
        var v = new[] { Vertex(), Vertex(), Vertex(), Vertex() };
        for (var i = 0; i < 4; i++)
        {
            for (var j = i + 1; j < 4; j++)
            {
                Edge(v[i], v[j]);
            }
        }

        var result = Run("TRIANGLECOUNT");
        _logger.LogInformation($"Triangle Count: {result.Statistics["TriangleCount"]}");
        foreach (var id in v)
        {
            _logger.LogInformation($"Vertex {id} has {result.VertexScores[id]} triangles");
        }
    }

    public void Triangles_FourCycle_HasNone()
    {
        var v = new[] { Vertex(), Vertex(), Vertex(), Vertex() };
        Edge(v[0], v[1]);
        Edge(v[1], v[2]);
        Edge(v[2], v[3]);
        Edge(v[3], v[0]);

        var result = Run("TRIANGLECOUNT");
        _logger.LogInformation($"Triangle Count: {result.Statistics["TriangleCount"]}");
    }

    public void Triangles_ParallelEdgesDeduplicated_SelfLoopsIgnored()
    {
        var a = Vertex();
        var b = Vertex();
        var c = Vertex();
        Edge(a, b);
        Edge(a, b); // doubled edge still counts one triangle
        Edge(b, c);
        Edge(c, a);
        Edge(a, a); // self-loop ignored

        var result = Run("TRIANGLECOUNT");
        _logger.LogInformation($"Triangle Count: {result.Statistics["TriangleCount"]}");
        _logger.LogInformation($"Vertex {a} has {result.VertexScores[a]} triangles");
    }

    #endregion

    #region scoping, removal, budgets

    public void LabelScoping_IsInducedSubgraph_OutOfScopeNeighboursInvisible()
    {
        var p1 = Vertex("person");
        var p2 = Vertex("person");
        var robot = Vertex("robot");
        Edge(p1, p2);
        Edge(p2, robot); // leaves the induced subgraph

        var result = Run("DEGREE", new GraphAnalyticsDefinition { VertexLabel = "person" });

        // only persons participate
        _logger.LogInformation($"Vertex Count: {result.VertexScores.Count}");
        // the edge to the robot is invisible
        _logger.LogInformation($"Vertex {p2} has {result.VertexScores[p2]} edges");
        _logger.LogInformation($"Vertex {robot} is not included");
    }

    public void EdgePropertyScoping_OnlyTheNamedGroupIsTraversed()
    {
        var a = Vertex();
        var b = Vertex();
        var c = Vertex();
        Edge(a, b, "knows");
        Edge(a, c, "owns");

        var result = Run("DEGREE", new GraphAnalyticsDefinition { EdgePropertyId = "knows" });

        _logger.LogInformation($"Vertex {a} has {result.VertexScores[a]} edges");
        _logger.LogInformation($"Vertex {b} has {result.VertexScores[b]} edges");
        // the owns edge is out of scope
        _logger.LogInformation($"Vertex {c} has {result.VertexScores[c]} edges");

        var wcc = Run("WCC", new GraphAnalyticsDefinition { EdgePropertyId = "knows" });
        // {a,b} and {c}
        _logger.LogInformation($"Component Count: {wcc.Statistics["ComponentCount"]}");
    }

    public void RemovedElements_AreSkipped()
    {
        var a = Vertex();
        var b = Vertex();
        var doomed = Vertex();
        Edge(a, b);
        Edge(b, doomed);

        _fallen8.EnqueueTransaction(new RemoveGraphElementTransaction { GraphElementId = doomed })
            .WaitUntilFinished();

        var degree = Run("DEGREE");
        _logger.LogInformation($"Vertex Count : {degree.VertexScores.Count}");
        _logger.LogInformation($"Vertex {b} has {degree.VertexScores[b]} edges");

        var wcc = Run("WCC");
        _logger.LogInformation($"Component Count: {wcc.Statistics["ComponentCount"]}");
    }

    public void Budget_NearZero_SinglePassReturnsFalse_IterativeKeepsLastCompletedPass()
    {
        for (var i = 0; i < 200; i++)
        {
            Vertex();
        }

        // A budget that is already exhausted when checked: single-pass algorithms
        // return false (partial single-pass values are meaningless).
        var exhausted = new GraphAnalyticsDefinition { TimeBudget = TimeSpan.FromTicks(1) };
        _fallen8.TryRunAnalytics(out _, "DEGREE", exhausted));
        _fallen8.TryRunAnalytics(out _, "WCC",  new GraphAnalyticsDefinition { TimeBudget = TimeSpan.FromTicks(1) }));
        _fallen8.TryRunAnalytics(out _, "PAGERANK", new GraphAnalyticsDefinition { TimeBudget = TimeSpan.FromTicks(1) });
    }

    // More exemples of scoping, removal, and budget tests can be added here as needed (see GraphAnalyticsTest.cs for inspiration).
    #endregion
}