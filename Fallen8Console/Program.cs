using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Transaction;

using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;

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
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            string savePath = Path.Combine(Environment.CurrentDirectory, "wpt.f8s");
            string walPath = Path.Combine(Environment.CurrentDirectory, "wpt.f8s.wal");

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

        }
    }
}
