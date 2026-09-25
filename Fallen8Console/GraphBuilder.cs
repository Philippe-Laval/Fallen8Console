using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fallen8Console;

/// <summary>
/// Tiny fluent builder that creates an isolated Fallen-8 instance, named vertices and
/// weighted, labelled, directed edges. Edge weights are stored as <see cref="Double"/> so
/// both the in-process cost delegates and the runtime-compiled cost fragments can read them.
/// </summary>
public sealed class GraphBuilder : IDisposable
{
    private readonly string _savePath;
    private readonly string _wallPath;
    private readonly ILogger _logger;
    private readonly Fallen8 _fallen8;
    private readonly Dictionary<String, Int32> _ids = new Dictionary<String, Int32>();
    private readonly Dictionary<Int32, String> _names = new Dictionary<Int32, String>();

    public Fallen8 Fallen8 => _fallen8;

    public GraphBuilder(bool inMemory, string savePath, string walPath, ILoggerFactory loggerFactory)
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

    public void AddVertices(params String[] vertexNames)
    {
        var tx = new CreateVerticesTransaction();
        foreach (var name in vertexNames)
        {
            tx.AddVertex(0, "node", new Dictionary<String, Object> { { "name", name } });
        }

        Fallen8.EnqueueTransaction(tx).WaitUntilFinished();

        var created = tx.GetCreatedVertices();
        for (var i = 0; i < vertexNames.Length; i++)
        {
            _ids[vertexNames[i]] = created[i].Id;
            _names[created[i].Id] = vertexNames[i];
        }
    }


    public (TransactionState State, Exception Error) Load()
    {
        var tx = new LoadTransaction { Path = _savePath };
        var info = _fallen8.EnqueueTransaction(tx);
        info.WaitUntilFinished();

        var vertices = _fallen8.GetAllVertices();
        for (var i = 0; i < vertices.Count; i++)
        {
            var vertice = vertices[i];
            vertice.TryGetProperty(out string name, "name");

            _ids[name] = vertice.Id;
            _names[vertice.Id] = name;
        }

        return (info.TransactionState, info.Error);
    }

    public string Save(int partitions = 1)
    {
        var tx = new SaveTransaction { Path = _savePath, SavePartitions = partitions };
        var info = _fallen8.EnqueueTransaction(tx);
        info.WaitUntilFinished();
        return tx.ActualPath;
    }

    public Int32 Id(String name)
    {
        return _ids[name];
    }

    public String Name(Int32 id)
    {
        return _names[id];
    }

    public GraphBuilder Edge(String from, String to, Double weight, String label = "edge", String edgePropertyId = "e")
    {
        var tx = new CreateEdgesTransaction();
        tx.AddEdge(_ids[from], edgePropertyId, _ids[to], 0, label, new Dictionary<String, Object> { { "weight", weight } });
        Fallen8.EnqueueTransaction(tx).WaitUntilFinished();

        return this;
    }



    
}
