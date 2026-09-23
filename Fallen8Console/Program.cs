using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Algorithms;
using NoSQL.GraphDB.Core.Algorithms.Path;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;

// https://docs.fallen-8.com/
// https://docs.fallen-8.com/library/
// code
//https://github.com/cosh/fallen-8-core

namespace Fallen8Console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestFallen8Database();
            TestShortestPath();
        }

        static void TestFallen8Database()
        { 
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            string savePath = System.IO.Path.Combine(Environment.CurrentDirectory, "wpt.f8s");
            string walPath = System.IO.Path.Combine(Environment.CurrentDirectory, "wpt.f8s.wal");

            bool reset = false;
            //bool reset = args.Length > 0 && args[0] == "--reset";
            if (reset)
            {
                File.Delete(savePath);
                File.Delete(walPath);
            }

            if (!File.Exists(savePath))
            {
                var db = new Fallen8Database(false, savePath, walPath, loggerFactory);

                db.PopulateDB();
                db.ShowInfo();

                db.AddPropertyToVertex(0, "age", 30);
                db.AddPropertyToVertex(1, "age", 25);
                db.AddPropertyToVertex(2, "age", 35);

                string actualPath = db.Save(1);

                db.Dispose();
            }

            var fallen8Database = new Fallen8Database(false, savePath, walPath, loggerFactory);
            (TransactionState State, Exception Error) = fallen8Database.Load();
            fallen8Database.ShowInfo();

            int count = fallen8Database.CountWithName("Alice");
            string? name = fallen8Database.NameOfVertex(0);
            name = fallen8Database.NameOfVertex(1);
            name = fallen8Database.NameOfVertex(2);

            fallen8Database.Fallen8.TryGetVertex(out var Alice, 0);
            fallen8Database.Fallen8.TryGetVertex(out var Bob, 1);
            fallen8Database.Fallen8.TryGetVertex(out var Charlie, 2);

            bool found = Alice.TryGetOutEdge(out var outEdges, "knows");
            if (!found)
            {
                logger.LogInformation("No outgoing edges from Alice with label 'knows'");
            }
            else
            {
                logger.LogInformation("Found {0} outgoing edges from Alice with label 'knows'", outEdges.Count);

                logger.LogInformation("ID of the first outgoing edge from Alice: {0}", outEdges[0].Id);
                logger.LogInformation("Target vertex ID: {0}", outEdges[0].TargetVertex.Id);
                logger.LogInformation("Target vertex Label: {0}", outEdges[0].TargetVertex.Label);
            }


            logger.LogInformation("In-degree of Alice: {0}", Alice.GetInDegree());
            logger.LogInformation("In-degree of Bob: {0}", Bob.GetInDegree());
            logger.LogInformation("In-degree of Charlie: {0}", Charlie.GetInDegree());

            logger.LogInformation("Out-degree of Alice: {0}", Alice.GetOutDegree());
            logger.LogInformation("Out-degree of Bob: {0}", Bob.GetOutDegree());
            logger.LogInformation("Out-degree of Charlie: {0}", Charlie.GetOutDegree());

            VertexModel diana = fallen8Database.AddVertice("person", new Dictionary<string, object> {
                { "name", "Diana" },
                { "age", 28 },
                { "maried", true },
                { "size", 1.75d }
            });

            diana.GetAllProperties().ToList().ForEach(p => logger.LogInformation("Diana's property: {0} = {1}", p.Key, p.Value));

            diana.TryGetProperty(out int age, "age");
            logger.LogInformation("Diana's age: {0}", age);
            diana.TryGetProperty(out string n, "name");
            logger.LogInformation("Diana's name: {0}", n);
            diana.TryGetProperty(out bool maried, "maried");
            logger.LogInformation("Diana is married: {0}", maried);
            diana.TryGetProperty(out double size, "size");
            logger.LogInformation("Diana's size: {0}", size);

        }


        /// <summary>Edge cost that reads the numeric "weight" property, defaulting to 1.0.</summary>
        private static Delegates.EdgeCost WeightCost()
        {
            return edge =>
            {
                // Must return a double, so we need to convert the object to double
                Object weight;
                return edge.TryGetProperty(out weight, "weight") ? Convert.ToDouble(weight) : 1.0;
            };
        }

        /// <summary>Vertex cost that returns <paramref name="cost"/> for one vertex id, else 0.0.</summary>
        private static Delegates.VertexCost VertexCostFor(Int32 vertexId, Double cost)
        {
            return vertex => vertex.Id == vertexId ? cost : 0.0;
        }

        /// <summary>The ordered vertex-id sequence of a path (source, then each target).</summary>
        private static List<Int32> VertexIds(NoSQL.GraphDB.Core.Algorithms.Path.Path path)
        {
            var elements = path.GetPathElements();
            var result = new List<Int32>();
            if (elements.Count > 0)
            {
                result.Add(elements[0].SourceVertex.Id);
            }

            foreach (var element in elements)
            {
                result.Add(element.TargetVertex.Id);
            }

            return result;
        }

        private static void TestShortestPath()
        {
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            string savePath = System.IO.Path.Combine(Environment.CurrentDirectory, "graph.f8s");
            string walPath = System.IO.Path.Combine(Environment.CurrentDirectory, "graph.f8s.wal");

            if (!File.Exists(savePath))
            {
                // Arrange - A->B costs 10 in one hop; A->C->B costs 1+1=2 in two hops.
                var gb = new GraphBuilder(false, savePath, walPath, loggerFactory);

                gb.AddVertices("A", "B", "C");
                gb.Edge("A", "B", 10);
                gb.Edge("A", "C", 1);
                gb.Edge("C", "B", 1);

                string actualPath = gb.Save(1);

                gb.Dispose();
            }

            var graph = new GraphBuilder(false, savePath, walPath, loggerFactory);
            graph.Load();

            // Dijkstra's algorithm prefers the path with the least total weight

            var weightedDefinition = new ShortestPathDefinition
            {
                SourceVertexId = graph.Id("A"),
                DestinationVertexId = graph.Id("B"),
                MaxDepth = 5,
                MaxResults = 1,
                EdgeCost = WeightCost()
            };

            var dijkstraFound = graph.Fallen8.TryCalculateShortestPath(out List<NoSQL.GraphDB.Core.Algorithms.Path.Path> dijkstraPaths, "DIJKSTRA", weightedDefinition);

            VertexIds(dijkstraPaths[0]).ForEach(id => logger.LogInformation("Dijkstra path vertex ID: {0} {1}", id, graph.Name(id)));

            // Bidirectional Label Search (BLS) prefers the path with the fewest hops

            var blsDefinition = new ShortestPathDefinition
            {
                SourceVertexId = graph.Id("A"),
                DestinationVertexId = graph.Id("B"),
                MaxDepth = 5,
                MaxResults = 1
            };

            var blsResult = graph.Fallen8.TryCalculateShortestPath(out List<NoSQL.GraphDB.Core.Algorithms.Path.Path> blsPaths, "BLS", blsDefinition);

            VertexIds(blsPaths[0]).ForEach(id => logger.LogInformation("BLS path vertex ID: {0} {1}", id, graph.Name(id)));
        }

    }
}
