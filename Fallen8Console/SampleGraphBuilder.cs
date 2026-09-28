using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fallen8Console
{
    public class SampleGraphBuilder : IDisposable
    {
        private readonly string _savePath;
        private readonly string _wallPath;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        public Fallen8 Fallen8 => _fallen8;

        public SampleGraphBuilder(bool inMemory, string savePath, string walPath, ILoggerFactory loggerFactory)
        {
            _savePath = savePath;
            _wallPath = walPath;
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

            _fallen8.RegisterPluginType<DictionaryIndex>();
            _fallen8.RegisterPluginType<VectorIndex>();
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

        public Fallen8 CreateEmptyGraph()
        {
            return _fallen8;
        }

        public Fallen8 CreateSingleVertexGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            // Create vertice
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "A" } });
            var verticesInfo = _fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();

            return _fallen8;
        }

        public Fallen8 CreateSingleEdgeGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());
            
            // Create vertices
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "A" } });
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "B" } });
            var verticesInfo = _fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();
            var vertices = verticesTx.GetCreatedVertices();

            // Create edge with properties
            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(vertices[0].Id, "connects", vertices[1].Id, creationDate, "link",
                new Dictionary<string, object> { { "weight", 5 }, { "type", "strong" } });
            var edgesInfo = _fallen8.EnqueueTransaction(edgesTx);
            edgesInfo.WaitUntilFinished();
            
            return _fallen8;
        }

        public Fallen8 CreateSimpleGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            // Create vertices: A -> B -> C -> D
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "A" } });
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "B" } });
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "C" } });
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "D" } });

            var verticesInfo = _fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();

            var vertices = verticesTx.GetCreatedVertices();

            // Create edges
            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(vertices[0].Id, "connects", vertices[1].Id, creationDate, "link");
            edgesTx.AddEdge(vertices[1].Id, "connects", vertices[2].Id, creationDate, "link");
            edgesTx.AddEdge(vertices[2].Id, "connects", vertices[3].Id, creationDate, "link");

            var edgesInfo = _fallen8.EnqueueTransaction(edgesTx);
            edgesInfo.WaitUntilFinished();

            return _fallen8;
        }

        public Fallen8 CreateComplexGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            // Create a more complex graph with multiple paths and labels
            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Alice" }, { "age", 30 } });
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Bob" }, { "age", 25 } });
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Charlie" }, { "age", 35 } });
            verticesTx.AddVertex(creationDate, "company", new Dictionary<string, object>() { { "name", "TechCorp" } });
            verticesTx.AddVertex(creationDate, "company", new Dictionary<string, object>() { { "name", "DataInc" } });

            var verticesInfo = _fallen8.EnqueueTransaction(verticesTx);
            verticesInfo.WaitUntilFinished();

            var vertices = verticesTx.GetCreatedVertices();

            // Create edges with different labels
            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(vertices[0].Id, "knows", vertices[1].Id, creationDate, "knows");
            edgesTx.AddEdge(vertices[1].Id, "knows", vertices[2].Id, creationDate, "knows");
            edgesTx.AddEdge(vertices[0].Id, "works_at", vertices[3].Id, creationDate, "works_at");
            edgesTx.AddEdge(vertices[1].Id, "works_at", vertices[3].Id, creationDate, "works_at");
            edgesTx.AddEdge(vertices[2].Id, "works_at", vertices[4].Id, creationDate, "works_at");

            var edgesInfo = _fallen8.EnqueueTransaction(edgesTx);
            edgesInfo.WaitUntilFinished();

            return _fallen8;
        }

        /// <summary>
        /// Builds a fan-out graph: A has two outgoing edges, to B and to C.
        /// </summary>
        public Fallen8 CreateFanOutGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "A" } });
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "B" } });
            verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", "C" } });
            _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();
            var v = verticesTx.GetCreatedVertices();

            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(v[0].Id, "connects", v[1].Id, creationDate, "link"); // A -> B
            edgesTx.AddEdge(v[0].Id, "connects", v[2].Id, creationDate, "link"); // A -> C
            _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();

            return _fallen8;
        }

        /// <summary>
        /// Builds a Y-shaped graph with two arms of length two:
        /// A -> B -> D and A -> C -> E.
        /// </summary>
        public Fallen8 CreateYGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var verticesTx = new CreateVerticesTransaction();
            foreach (var name in new[] { "A", "B", "C", "D", "E" })
            {
                verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", name } });
            }
            _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();
            var v = verticesTx.GetCreatedVertices(); // 0:A 1:B 2:C 3:D 4:E

            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(v[0].Id, "connects", v[1].Id, creationDate, "link"); // A -> B
            edgesTx.AddEdge(v[0].Id, "connects", v[2].Id, creationDate, "link"); // A -> C
            edgesTx.AddEdge(v[1].Id, "connects", v[3].Id, creationDate, "link"); // B -> D
            edgesTx.AddEdge(v[2].Id, "connects", v[4].Id, creationDate, "link"); // C -> E
            _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();

            return _fallen8;
        }

        /// <summary>
        /// Builds a graph where one arm is shorter than the other:
        /// A -> B (B is a leaf) and A -> C -> D.
        /// </summary>
        public Fallen8 CreateUnevenArmsGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var verticesTx = new CreateVerticesTransaction();
            foreach (var name in new[] { "A", "B", "C", "D" })
            {
                verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object>() { { "name", name } });
            }
            _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();
            var v = verticesTx.GetCreatedVertices(); // 0:A 1:B 2:C 3:D

            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(v[0].Id, "connects", v[1].Id, creationDate, "link"); // A -> B (leaf)
            edgesTx.AddEdge(v[0].Id, "connects", v[2].Id, creationDate, "link"); // A -> C
            edgesTx.AddEdge(v[2].Id, "connects", v[3].Id, creationDate, "link"); // C -> D
            _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();

            return _fallen8;
        }


        /// <summary>
        /// Alice -knows-> Bob -knows-> Charlie, Alice -works_at-> TechCorp, Charlie -works_at-> TechCorp.
        /// The edge LABEL is deliberately different from the edge PROPERTY ID on every edge, so a
        /// test that filters on the property id cannot pass by accidentally matching the label.
        /// </summary>
        public Fallen8 CreateRelationshipGraph()
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var verticesTx = new CreateVerticesTransaction();
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Alice" } });
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Bob" } });
            verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object>() { { "name", "Charlie" } });
            verticesTx.AddVertex(creationDate, "company", new Dictionary<string, object>() { { "name", "TechCorp" } });
            _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();

            var vertices = verticesTx.GetCreatedVertices();

            var edgesTx = new CreateEdgesTransaction();
            edgesTx.AddEdge(vertices[0].Id, "knows", vertices[1].Id, creationDate, "edge-alice-bob");
            edgesTx.AddEdge(vertices[1].Id, "knows", vertices[2].Id, creationDate, "edge-bob-charlie");
            edgesTx.AddEdge(vertices[0].Id, "works_at", vertices[3].Id, creationDate, "edge-alice-techcorp");
            edgesTx.AddEdge(vertices[2].Id, "works_at", vertices[3].Id, creationDate, "edge-charlie-techcorp");
            _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();

            return _fallen8;
        }

    }
}
