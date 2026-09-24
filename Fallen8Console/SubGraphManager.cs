using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.App.Controllers.Model;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.App.Helper;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

// See SubGraphCodeGenerationTest.cs for an example of how to use this class.

namespace Fallen8Console
{
    public class SubGraphManager
    {
        private readonly string _savePath;
        private readonly string _wallPath;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        public Fallen8 Fallen8 => _fallen8;

        public SubGraphManager(bool inMemory, string savePath, string walPath, ILoggerFactory loggerFactory)
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

        public void Test1()
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

        public void Test2()
        {
            var spec = new SubGraphSpecification
            {
                Name = "typed-slots",
                VertexFilter = "return (v) => v.GetOutDegree() >= 1;",
                EdgeFilter = "return (e) => e.SourceVertex.Label == \"person\";"
            };

            var error = CodeGenerationHelper.TryGenerateSubGraphDefinition(spec, out var definition);
        }

        public void Test3()
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




    }
}
