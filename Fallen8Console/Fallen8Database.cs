using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fallen8Console;

public class Fallen8Database : IDisposable
{
    private readonly string _savePath;
    private readonly string _wallPath;
    private readonly ILogger _logger;
    private readonly Fallen8 _fallen8;

    public Fallen8Database(bool inMemory, string savePath, string walPath, ILoggerFactory loggerFactory)
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

    public void Load()
    {
        var tx = new LoadTransaction { Path = _savePath };
        var info = _fallen8.EnqueueTransaction(tx);
        info.WaitUntilFinished();
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

    public void PopulateDB()
    {
        var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

        // A -> B -> C, created in one atomic transaction.
        var verticesTx = new CreateVerticesTransaction();
        verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object> { { "name", "A" } });
        verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object> { { "name", "B" } });
        verticesTx.AddVertex(creationDate, "node", new Dictionary<string, object> { { "name", "C" } });
        _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();

        var vertices = verticesTx.GetCreatedVertices();

        var edgesTx = new CreateEdgesTransaction();
        edgesTx.AddEdge(vertices[0].Id, "connects", vertices[1].Id, creationDate, "link");
        edgesTx.AddEdge(vertices[1].Id, "connects", vertices[2].Id, creationDate, "link");
        _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();

        var edges = edgesTx.GetCreatedEdges();
    }
}
