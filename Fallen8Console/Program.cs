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

            string savePath = Path.Combine(Environment.CurrentDirectory, "wpt.f8s");
            string walPath = Path.Combine(Environment.CurrentDirectory, "wpt.f8s.wal");


            Fallen8Database fallen8Database = new Fallen8Database(false, savePath, walPath, loggerFactory);

            fallen8Database.PopulateDB();
            fallen8Database.ShowInfo();
            fallen8Database.Save(1);

            fallen8Database.Dispose();

            fallen8Database = new Fallen8Database(false, savePath, walPath, loggerFactory);
            fallen8Database.Load();
            fallen8Database.ShowInfo();
        }
    }
}
