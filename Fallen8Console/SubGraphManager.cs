using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.App.Controllers.Model;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Algorithms;
using NoSQL.GraphDB.Core.Algorithms.SubGraph;
using NoSQL.GraphDB.Core.App.Helper;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

// See SubGraphCodeGenerationTest.cs for an example of how to use this class.
// See SubGraphTest.cs has a lot of examples of how to use the subgraph algorithm with different patterns and filters.

namespace Fallen8Console
{
    public class SubGraphManager
    {
        private readonly string _savePath;
        private readonly string _wallPath;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        public Fallen8 Fallen8 => _fallen8;

        public SubGraphManager(bool inMemory, string savePath, string walPath, ILoggerFactory loggerFactory)
        {
            _savePath = savePath;
            _wallPath = walPath;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<Fallen8Database>();

            if (inMemory)
            {
                // A pure in-memory graph: no WAL, no change feed, nothing on disk
                _fallen8 = new Fallen8(loggerFactory);
            }
            else
            {
                // A persistent graph: WAL and change feed are stored on disk
                _fallen8 = new Fallen8(loggerFactory, new WriteAheadLogOptions(_wallPath));
            }
        }

        public void Dispose()
        {
            _fallen8.Dispose();
        }

        public (TransactionState State, Exception Error) Load()
        {
            var tx = new LoadTransaction { Path = _savePath };
            var info = _fallen8.EnqueueTransaction(tx);
            info.WaitUntilFinished();
            return (info.TransactionState, info.Error);
        }

        public string Save(int partitions = 1)
        {
            var tx = new SaveTransaction { Path = _savePath, SavePartitions = partitions };
            var info = _fallen8.EnqueueTransaction(tx);
            info.WaitUntilFinished();
            return tx.ActualPath;
        }

        public void ShowInfo()
        {
            _logger.LogInformation($"{_fallen8.VertexCount} vertices, {_fallen8.EdgeCount} edges");
        }

        public (VertexModel person, VertexModel company, EdgeModel knows) BuildGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Alice" } });
            verticesTx.AddVertex(creationDate, "company", new Dictionary<string, object>() { { "name", "TechCorp" } });
            _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();
            var v = verticesTx.GetCreatedVertices();

            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(v[0].Id, "knows", v[1].Id, creationDate, "knows");
            _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();
            _fallen8.TryGetEdge(out var edge, _fallen8.GetAllEdges().First().Id);

            return (v[0], v[1], edge);
        }

        public void Test100()
        {
            //var (person, company, knows) = BuildGraph();

            var spec = new SubGraphSpecification
            {
                Name = "people-who-know",
                VertexFilter = "return (v) => v.Label == \"person\";",
                EdgeFilter = "return (e) => e.Label == \"knows\";",
                Patterns = new List<PatternSpecification>
                {
                    new PatternSpecification
                    {
                        Type = "Vertex",
                        PatternName = "p",
                        VertexFilter = "return (v) => v.Label == \"person\";"
                    },
                    new PatternSpecification
                    {
                        Type = "Edge",
                        PatternName = "rel",
                        Direction = "OutgoingEdge",
                        EdgePropertyFilter = "return (p) => p == \"knows\";",
                        EdgeFilter = "return (e) => e.Label == \"knows\";"
                    },
                    new PatternSpecification {
                        Type = "Vertex",
                        PatternName = "c"
                    }
                }
            };

            var error = CodeGenerationHelper.TryGenerateSubGraphDefinition(spec, out var definition);
        }

        public void Test101()
        {
            var spec = new SubGraphSpecification
            {
                Name = "typed-slots",
                VertexFilter = "return (v) => v.GetOutDegree() >= 1;",
                EdgeFilter = "return (e) => e.SourceVertex.Label == \"person\";"
            };

            var error = CodeGenerationHelper.TryGenerateSubGraphDefinition(spec, out var definition);
        }

        public void Test102()
        {
            var spec = new SubGraphSpecification
            {
                Name = "var-len",
                Patterns = new List<PatternSpecification>
                {
                    new PatternSpecification { Type = "Vertex" },
                    new PatternSpecification { Type = "VariableLengthEdge", Direction = "OutgoingEdge", MinLength = 1, MaxLength = 3 },
                    new PatternSpecification { Type = "Vertex" }
                }
            };

            var error = CodeGenerationHelper.TryGenerateSubGraphDefinition(spec, out var definition);

        }

        public void Test1()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "single-vertex",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "nodes",
                        Vertex = v => v.Label == "node"
                    }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");
        }

        public void Test2()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "simple-path",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "start", Vertex = v => v.Label == "node" },
                    new EdgePattern { PatternName = "edge", Direction = Direction.OutgoingEdge },
                    new VertexPattern { PatternName = "end", Vertex = v => v.Label == "node" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");
        }

        public void Test3()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateComplexGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "person-only",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "person",
                        Vertex = v => v.Label == "person"
                    },
                    new EdgePattern
                    {
                        PatternName = "knows",
                        Direction = Direction.OutgoingEdge,
                        Edge = e => e.Label == "knows"
                    },
                    new VertexPattern
                    {
                        PatternName = "friend",
                        Vertex = v => v.Label == "person"
                    }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Verify all vertices are persons
            var vertices = subgraphResult.SubGraph.GetAllVertices();
            foreach (var vertex in vertices)
            {
                _logger.LogInformation($"{vertex.Label}");
            }
        }

        public void Test4()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateComplexGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "age-filter",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "person",
                        Vertex = vertex =>
                        {
                            if (vertex.Label != "person")
                            {
                                return false;
                            }

                            object age;
                            if (vertex.TryGetProperty(out age, "age"))
                            {
                                return (int)age >= 30;
                            }
                            return false;
                        }
                    }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Verify all vertices are persons
            var vertices = subgraphResult.SubGraph.GetAllVertices();
            foreach (var vertex in vertices)
            {
                _logger.LogInformation($"{vertex.Label}");

                object age;
                if (vertex.TryGetProperty(out age, "age"))
                {
                    _logger.LogInformation($"Age: {age}");
                }

                if (vertex.TryGetProperty(out string name, "name"))
                {
                    _logger.LogInformation($"Name: {name}");
                }
            }
        }


        public void Test5()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateComplexGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "knows-only",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "p1", Vertex = v => v.Label == "person" },
                    new EdgePattern
                    {
                        PatternName = "relationship",
                        Direction = Direction.OutgoingEdge,
                        EdgeProperty = (propertyId) => propertyId == "knows"
                    },
                    new VertexPattern { PatternName = "p2", Vertex = v => v.Label == "person" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

        }

        public void Test6()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateComplexGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "works-at",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "person", Vertex = v => v.Label == "person" },
                    new EdgePattern
                    {
                        PatternName = "employment",
                        Direction = Direction.OutgoingEdge,
                        Edge = (edge) => edge.Label == "works_at"
                    },
                    new VertexPattern { PatternName = "company", Vertex = v => v.Label == "company" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");
        }

        public void Test7()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            // Find vertex D and traverse backwards to C
            var definition = new SubGraphDefinition
            {
                Name = "reverse-traversal",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "d",
                        Vertex = vertex =>
                        {
                            object name;
                            return vertex.TryGetProperty(out name, "name") && name.ToString() == "D";
                        }
                    },
                    new EdgePattern
                    {
                        PatternName = "back",
                        Direction = Direction.IncomingEdge
                    },
                    new VertexPattern { PatternName = "previous" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Should have 2 vertices (D and C)
            // Should have 1 edge (C -> D)
        }

        public void Test8()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "undirected",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "b",
                        Vertex = vertex =>
                        {
                            object name;
                            return vertex.TryGetProperty(out name, "name") && name.ToString() == "B";
                        }
                    },
                    new EdgePattern
                    {
                        PatternName = "any",
                        Direction = Direction.UndirectedEdge
                    },
                    new VertexPattern { PatternName = "neighbor" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Should have 3 vertices (A, B, C)
            // Should have 2 edges (A -> B and B -> C)
        }

        public void Test9()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "variable-1hop",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "a",
                        Vertex = vertex =>
                        {
                            object name;
                            return vertex.TryGetProperty(out name, "name") && name.ToString() == "A";
                        }
                    },
                    new VariableLengthEdgePattern
                    {
                        PatternName = "path",
                        Direction = Direction.OutgoingEdge,
                        MinLength = 1,
                        MaxLength = 1
                    },
                    new VertexPattern { PatternName = "target" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Should have 2 vertices (A and B)
            // Should have 1 edge (A -> B)
        }

        public void Test10()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "variable-1to3",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "a",
                        Vertex = vertex =>
                        {
                            object name;
                            return vertex.TryGetProperty(out name, "name") && name.ToString() == "A";
                        }
                    },
                    new VariableLengthEdgePattern
                    {
                        PatternName = "path",
                        Direction = Direction.OutgoingEdge,
                        MinLength = 1,
                        MaxLength = 3
                    },
                    new VertexPattern { PatternName = "target" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Should have all 4 vertices
            // Should have all 3 edges
        }

        public void Test11()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "variable-with-target",
                Pattern = new List<APattern>
                {
                    new VertexPattern
                    {
                        PatternName = "a",
                        Vertex = vertex =>
                        {
                            object name;
                            return vertex.TryGetProperty(out name, "name") && name.ToString() == "A";
                        }
                    },
                    new VariableLengthEdgePattern
                    {
                        PatternName = "path",
                        Direction = Direction.OutgoingEdge,
                        MinLength = 1,
                        MaxLength = 3
                    },
                    new VertexPattern
                    {
                        PatternName = "target-c-or-d",
                        Vertex = vertex =>
                        {
                            if (vertex.TryGetProperty(out string name, "name"))
                            {
                                return name == "C" || name == "D";
                            }
                            return false;
                        }
                    }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Should have at least vertices A, C, D
        }

        public void Test12()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSimpleGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            // Find: person -> knows -> person -> works_at -> company
            var definition = new SubGraphDefinition
            {
                Name = "complex",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "p1", Vertex = v => v.Label == "person" },
                    new EdgePattern { PatternName = "knows", Direction = Direction.OutgoingEdge, Edge = e => e.Label == "knows" },
                    new VertexPattern { PatternName = "p2", Vertex = v => v.Label == "person" },
                    new EdgePattern { PatternName = "works", Direction = Direction.OutgoingEdge, Edge = e => e.Label == "works_at" },
                    new VertexPattern { PatternName = "company", Vertex = v => v.Label == "company" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            // Should have at least 3 vertices
            // Should have at least 2 edges
        }

        public void Test13()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSingleEdgeGraph();

            var algorithm = new BreadthFirstSearchSubgraphAlgorithm();
            algorithm.Initialize(fallen8, null);

            var definition = new SubGraphDefinition
            {
                Name = "edge-props",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "a" },
                    new EdgePattern { PatternName = "edge", Direction = Direction.OutgoingEdge },
                    new VertexPattern { PatternName = "b" }
                }
            };

            var result = algorithm.TryCreateSubgraph(out SubGraphResult subgraphResult, definition);

            _logger.LogInformation($"Subgraph created with {subgraphResult.SubGraph.VertexCount} vertices and {subgraphResult.SubGraph.EdgeCount} edges.");

            var edges = subgraphResult.SubGraph.GetAllEdges();
            var edge = edges.FirstOrDefault();

            if (edge != null)
            {
                edge.TryGetProperty(out int weight, "weight");
                edge.TryGetProperty(out string type, "type");
            }
        }

        public void Test14()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSingleVertexGraph();
            var subGraphName = "test-subgraph";

            var definition = new SubGraphDefinition
            {
                Name = subGraphName,
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "node", Vertex = v => v.Label == "node" }
                }
            };

            // Create a subgraph using the typed version
            SubGraphResult originalSubGraph;
            fallen8.SubGraphFactory.TryCreateSubGraph<BreadthFirstSearchSubgraphAlgorithm>(
                out originalSubGraph, subGraphName, definition);

            _logger.LogInformation($"Subgraph created with {originalSubGraph.SubGraph.VertexCount} vertices and {originalSubGraph.SubGraph.EdgeCount} edges.");


            // Modify the graph by adding a new vertex
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "E" } });
            var verticesInfo = fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();

            // Recalculate the subgraph
            var recalculateResult = fallen8.SubGraphFactory.TryRecalculateSubGraph(subGraphName);

            // Get the subgraph
            SubGraphResult recalculatedSubGraph;
            fallen8.SubGraphFactory.TryGetSubGraph(out recalculatedSubGraph, subGraphName);

            _logger.LogInformation($"Subgraph created with {recalculatedSubGraph.SubGraph.VertexCount} vertices and {recalculatedSubGraph.SubGraph.EdgeCount} edges.");
        }

        public void Test15()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateSingleVertexGraph();
            var subGraphName = "test-subgraph";

            var definition = new SubGraphDefinition
            {
                Name = subGraphName,
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "node", Vertex = v => v.Label == "node" }
                }
            };

            // Create a subgraph using the typed version
            SubGraphResult originalSubGraph;
            fallen8.SubGraphFactory.TryCreateSubGraph<BreadthFirstSearchSubgraphAlgorithm>(
                out originalSubGraph, subGraphName, definition);

            _logger.LogInformation($"Subgraph created with {originalSubGraph.SubGraph.VertexCount} vertices and {originalSubGraph.SubGraph.EdgeCount} edges.");


            // Modify the graph by adding a new vertex
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "E" } });
            var verticesInfo = fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();

            // Recalculate the subgraph
            var recalculateResult = fallen8.SubGraphFactory.TryRecalculateSubGraph(subGraphName);

            // Get the subgraph
            SubGraphResult recalculatedSubGraph;
            fallen8.SubGraphFactory.TryGetSubGraph(out recalculatedSubGraph, subGraphName);

            _logger.LogInformation($"Subgraph created with {recalculatedSubGraph.SubGraph.VertexCount} vertices and {recalculatedSubGraph.SubGraph.EdgeCount} edges.");
        }

        public void Test16()
        {
            using SampleGraphBuilder sm = new SampleGraphBuilder(true, string.Empty, string.Empty, _loggerFactory);
            var fallen8 = sm.CreateComplexGraph();

            var definition1 = new SubGraphDefinition
            {
                Name = "persons",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "person", Vertex = v => v.Label == "person" }
                }
            };

            var definition2 = new SubGraphDefinition
            {
                Name = "companies",
                Pattern = new List<APattern>
                {
                    new VertexPattern { PatternName = "company", Vertex = v => v.Label == "company" }
                }
            };

            // Create a subgraphs using the typed version
            SubGraphResult subGraph1, subGraph2;
            fallen8.SubGraphFactory.TryCreateSubGraph<BreadthFirstSearchSubgraphAlgorithm>(
                 out subGraph1, "persons", definition1);
            fallen8.SubGraphFactory.TryCreateSubGraph<BreadthFirstSearchSubgraphAlgorithm>(
                out subGraph2, "companies", definition2);


            // Modify the graph by adding a new vertex
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "David" } });
            verticesTx.AddVertex(creationDate, "company", new Dictionary<string, object>() { { "name", "NewCorp" } });
            var verticesInfo = fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();

            // Recalculate all subgraphs
            var recalculatedCount = fallen8.SubGraphFactory.RecalculateAllSubGraphs();

            // Get the subgraph
            SubGraphResult recalcPersons, recalcCompanies;
            fallen8.SubGraphFactory.TryGetSubGraph(out recalcPersons, "persons");
            fallen8.SubGraphFactory.TryGetSubGraph(out recalcCompanies, "companies");
        }




    }
}
