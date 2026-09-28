using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fallen8Console
{
    public class EmbeddingManager
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

        private int Vertex(string label = "person", string name = "A")
        {
            var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            var tx = new CreateVertexTransaction { 
                Definition = new VertexDefinition
                { 
                    CreationDate = creationDate,
                    Label = label,
                    Properties =new Dictionary<string, object>() { { "name", name } }
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

        private void SetEmbedding(int elementId, string name, float[] vector)
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
        }


    }
}
