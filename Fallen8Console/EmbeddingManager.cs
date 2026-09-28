using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

// See ElementEmbeddingTest.cs for more information on how to use embeddings in Fallen-8.

namespace Fallen8Console
{
    public class EmbeddingManager : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        public EmbeddingManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<EmbeddingManager>();
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

        private void SetEmbedding(int elementId, string name, float[]? vector)
        {
            var tx = new SetEmbeddingsTransaction().SetEmbedding(elementId, name, vector);
            _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
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

        public void PopulateGraphWithEmbeddings()
        {
            int vertex1 = Vertex("person", "Alice");
            int vertex2 = Vertex("person", "Bob");
            int edge = Edge(vertex1, vertex2);

            // Valid name for embedding: "default", "Title_2-v",
            // max 64 characters, no spaces, no special characters, only letters, numbers, and underscores and hyphens.

            var maxDimension = NoSQL.GraphDB.Core.Index.Vector.VectorIndex.MaxDimension;
            _logger.LogInformation($"Max embedding dimension: {maxDimension}");

            SetEmbedding(vertex1, "default", new float[] { 0.1f, 0.2f, 0.3f });
            SetEmbedding(vertex1, "embedding1", new float[] { 0.3f, 0.2f, 0.1f });

            SetEmbedding(vertex2, "default", new float[] { 0.4f, 0.5f, 0.6f });
            SetEmbedding(vertex2, "embedding2", new float[] { 0.6f, 0.5f, 0.4f });

            SetEmbedding(edge, "default", new float[] { 0.7f, 0.8f, 0.9f });
            SetEmbedding(edge, "embedding3", new float[] { 0.9f, 0.8f, 0.7f });
        }

        public void Test1()
        {
            var vertex1 = Element(0);
            var vertex2 = Element(1);
            var edge = Element(2);

            // Get the "default" embedding
            bool result = vertex1.TryGetEmbedding(out var vector1);
            result = vertex2.TryGetEmbedding(out var vector2);
            result = edge.TryGetEmbedding(out var vector3);

            result = vertex1.TryGetEmbedding(out var embedding1, "embedding1");
            result = vertex2.TryGetEmbedding(out var embedding2, "embedding2");
            result = edge.TryGetEmbedding(out var embedding3, "embedding3");

            var properties = vertex1.GetAllProperties();
            result = properties.ContainsKey("$embedding:default");
        }

        public void Test2()
        {
            var vertex1 = Element(0);
            var vertex2 = Element(1);
            var edge = Element(2);

            // Null vector removes the embedding
            SetEmbedding(vertex1.Id, "default", new[] { 1f, 2f });
            SetEmbedding(vertex1.Id, "default", null);

            bool result = vertex1.TryGetEmbedding(out var vector1);

            // Replaces The Prior Vector
            SetEmbedding(vertex2.Id, "default", new[] { 0.41f, 0.51f, 0.61f });
            SetEmbedding(vertex2.Id, "default", new[] { 0.42f, 0.52f, 0.62f });

            result = vertex2.TryGetEmbedding(out var vector2);
        }


    }
}
