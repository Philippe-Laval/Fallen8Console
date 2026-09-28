using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.App.Controllers.Model;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

// See VectorIndexTest.cs for more information on how to use vector indexes in Fallen-8.

namespace Fallen8Console
{
    public class VectorIndexManager : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        private IVectorIndex _index1;
        private IVectorIndex _index2;
        private IVectorIndex _index3;

        public VectorIndexManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<VectorIndexManager>();
            _fallen8 = new Fallen8(loggerFactory);
        }

        public void Dispose()
        {
            _fallen8.Dispose();
        }

        private int Vertex(string label = "person", string name = "A")
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var tx = new CreateVertexTransaction
            {
                Definition = new VertexDefinition
                {
                    CreationDate = creationDate,
                    Label = label,
                    Properties = new Dictionary<string, object>() { { "name", name } }
                }
            };

            _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
            return tx.VertexCreated.Id;
        }

        private int Edge(int source, int target)
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var tx = new CreateEdgesTransaction();
            tx.AddEdge(source, "knows", target, creationDate, "knows");
            _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
            return tx.GetCreatedEdges()[0].Id;
        }

        private AGraphElementModel Element(int id)
        {
            bool result = _fallen8.TryGetGraphElement(out var element, id);
            if (false == result)
            {
                _logger.LogError($"Element with ID {id} not found.");
                throw new InvalidOperationException($"Element with ID {id} not found.");
            }

            return element;
        }

        private IVectorIndex CreateIndex(string name, int dimension, string? metric = null)
        {
            var options = new Dictionary<string, object> { { "dimension", dimension } };
            if (metric != null)
            {
                options["metric"] = metric;
            }

            var result = _fallen8.IndexFactory.TryCreateIndex(out var index, name, "VectorIndex", options);

            return (IVectorIndex)index;
        }

        private static List<(int Id, float Score)> Knn(IVectorIndex index,
            float[] query,
            int k,
            VectorSearchConstraint? constraint = null)
        {
            bool result = index.TryNearestNeighbors(out var vectorSearchResult, query, k, constraint);
            return vectorSearchResult.Entries.Select(e => (e.Element.Id, e.Score)).ToList();
        }

        public void CreateIndexes()
        {
            var a = Vertex("person", "A");
            var b = Vertex("person", "B");
            var c = Vertex("person", "C");
            var d = Vertex("person", "D");

            var edge1 = Edge(a, b);
            var edge2 = Edge(b, c);
            var edge3 = Edge(c, d);

            _index1 = CreateIndex("l2_index", 3, "L2");
            _logger.LogInformation($"Index dimension: {_index1.Dimension}");
            _logger.LogInformation($"Index metric: {_index1.Metric}");

            _index2 = CreateIndex("cosine_index", 2, "Cosine");
            _logger.LogInformation($"Index dimension: {_index2.Dimension}");
            _logger.LogInformation($"Index metric: {_index2.Metric}");

            _index3 = CreateIndex("dotproduct_index", 3, "DotProduct");
            _logger.LogInformation($"Index dimension: {_index3.Dimension}");
            _logger.LogInformation($"Index metric: {_index3.Metric}");


            // Available index plugins: DictionaryIndex, SingleValueIndex, VectorIndex, SpatialIndex, RangeIndex, RegExIndex
            _logger.LogInformation($"Available index plugins: {string.Join(", ", _fallen8.IndexFactory.GetAvailableIndexPlugins())}");
        }

        public void Test1()
        {
            var a = Element(0);
            var b = Element(1);
            var c = Element(2);
            var d = Element(3);

            _index2.AddOrUpdate(new[] { 1f, 0f }, a);
            _index2.AddOrUpdate(new[] { 0f, 1f }, b);
            _index2.AddOrUpdate(new[] { -1f, 0f }, c);
            _index2.AddOrUpdate(new[] { 3f, 4f }, d);   // non-unit norm

            var hits = Knn(_index2, new[] { 1f, 0f }, 4);

            // 1.0 : same
            _logger.LogInformation($"Hit 1: {hits[0].Id}, Score: {hits[0].Score}");
            _logger.LogInformation($"Hit 2: {hits[1].Id}, Score: {hits[1].Score}");
            // 0.0 : orthogonal
            _logger.LogInformation($"Hit 3: {hits[2].Id}, Score: {hits[2].Score}");
            // -1.0 : opposite
            _logger.LogInformation($"Hit 4: {hits[3].Id}, Score: {hits[3].Score}");
        }

        public void Test2()
        {
            var a = Element(0);
            var b = Element(1);

            _index3.AddOrUpdate(new[] { 1f, 2f, -3f }, a);
            _index3.AddOrUpdate(new[] { -2f, 0.5f, 1f }, b);

            var hits = Knn(_index3, new[] { 2f, -1f, 0.5f }, 2);

            // a . q = 2 - 2 - 1.5 = -1.5 ; b . q = -4 - 0.5 + 0.5 = -4. Higher is better: a first.
            _logger.LogInformation($"Hit 1: {hits[0].Id}, Score: {hits[0].Score}");
            _logger.LogInformation($"Hit 2: {hits[1].Id}, Score: {hits[1].Score}");
        }

        public void Test3()
        {
            var a = Element(0);
            var b = Element(1);
            var c = Element(2);
            var d = Element(3);

            _index1.AddOrUpdate(new[] { 1f, 0f, 0f }, a);
            _index1.AddOrUpdate(new[] { 0f, 1f, 0f }, b);
            _index1.AddOrUpdate(new[] { 1f, 0f, 1f }, c);
            _index1.AddOrUpdate(new[] { 0.5f, 0.5f, 0f }, d);

            _index1.TryNearestNeighbors(out var result, new[] { 0f, 0f, 0f }, 2);

            // No more than VectorIndex.MaxK
            _index1.TryNearestNeighbors(out _, new[] { 1f, 0f, 0f }, VectorIndex.MaxK);

            // Count of values in the index
            _logger.LogInformation($"Count of values : {_index1.CountOfValues()}");
            _logger.LogInformation($"Count of keys : {_index1.CountOfKeys()}");
        }

        public void Test4()
        {
            var a = Element(0);
            var b = Element(1);
            var c = Element(2);
            var d = Element(3);

            var edge1 = Element(4);
            var edge2 = Element(5);
            var edge3 = Element(6);

            var index = CreateIndex("index", 2, "L2");

            index.AddOrUpdate(new[] { 0f, 0f }, a);
            index.AddOrUpdate(new[] { 0.1f, 0f }, b);
            index.AddOrUpdate(new[] { 0.2f, 0f }, c);
            index.AddOrUpdate(new[] { 0.2f, 0f }, d);

            index.AddOrUpdate(new[] { 0.05f, 0f }, edge1);
            index.AddOrUpdate(new[] { 0.15f, 0f }, edge2);
            index.AddOrUpdate(new[] { 0.25f, 0f }, edge3);

            // kind=vertex excludes the edge even though it scores best.
            var vertexConstraint = new VectorSearchConstraint
            {
                Kind = VectorSearchElementKind.Vertex
            };

            var vertices = Knn(index, new[] { 0f, 0f }, 10, vertexConstraint);

            _logger.LogInformation($"Vertices found: {vertices.Count}");

            // kind=edge excludes the vertex even though it scores best.
            var edgeConstraint = new VectorSearchConstraint
            {
                Kind = VectorSearchElementKind.Edge
            };

            var edges = Knn(index, new[] { 0f, 0f }, 10, edgeConstraint);

            _logger.LogInformation($"Edges found: {edges.Count}");


            // label filtering is exact; an unlabeled element never matches a label.
            var labelConstraint = new VectorSearchConstraint
            {
                Label = "person"
            };

            var persons = Knn(index, new[] { 0f, 0f }, 10, labelConstraint);

            _logger.LogInformation($"Persons found: {persons.Count}");
        }


        public void Test5()
        {
            var a = Element(0);
            var b = Element(1);
            var c = Element(2);
            var d = Element(3);

            var edge1 = Element(4);
            var edge2 = Element(5);
            var edge3 = Element(6);

            var index = CreateIndex("index2", 2, "L2");

            index.AddOrUpdate(new[] { 0f, 0f }, a);
            index.AddOrUpdate(new[] { 0.1f, 0f }, b);
            index.AddOrUpdate(new[] { 0.2f, 0f }, c);
            index.AddOrUpdate(new[] { 0.2f, 0f }, d);

            _logger.LogInformation($"Count of values : {index.CountOfValues()}");

            bool result = index.TryRemoveKey(new[] { 0f, 0f });
            _logger.LogInformation($"Key removed: {result}");

            _logger.LogInformation($"Count of values : {index.CountOfValues()}");

            index.Wipe();

            _logger.LogInformation($"Count of values : {index.CountOfValues()}");
        }


    }
}
