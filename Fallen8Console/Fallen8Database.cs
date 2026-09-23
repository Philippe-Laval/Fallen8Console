using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Expression;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Model;
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

    public Fallen8 Fallen8 => _fallen8;

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

    public VertexModel[] AddVertices(params (string Label, string Name)[] specs)
    {
        var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

        var tx = new CreateVerticesTransaction();
        foreach (var spec in specs)
        {
            tx.AddVertex(creationDate, spec.Label, new Dictionary<string, object> { { "name", spec.Name } });
        }
        _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
        return tx.GetCreatedVertices().ToArray();
    }

    public void RemoveVertex(int vertexId)
    {
        var tx = new RemoveGraphElementTransaction { GraphElementId = vertexId };
        _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
    }

    public void AddPropertyToVertex(int vertexId, string propertyId, object propertyValue)
    {
        var tx = new AddPropertyTransaction
        {
            Definition = new PropertyAddDefinition { GraphElementId = vertexId, PropertyId = propertyId, Property = propertyValue }
        };
        _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
    }

    public EdgeModel AddEdge(int sourceId, string edgePropertyId, int targetId, string label)
    {
        var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

        var tx = new CreateEdgesTransaction();
        tx.AddEdge(sourceId, edgePropertyId, targetId, creationDate, label);
        _fallen8.EnqueueTransaction(tx).WaitUntilFinished();
        return tx.GetCreatedEdges().Single();
    }   

   
    public void PopulateDB()
    {
        var creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

        // A -> B -> C, created in one atomic transaction.
        var verticesTx = new CreateVerticesTransaction();
        verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object> { { "name", "Alice" } });
        verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object> { { "name", "Bob" } });
        verticesTx.AddVertex(creationDate, "person", new Dictionary<string, object> { { "name", "Charlie" } });
        _fallen8.EnqueueTransaction(verticesTx).WaitUntilFinished();

        var vertices = verticesTx.GetCreatedVertices();

        var edgesTx = new CreateEdgesTransaction();
        edgesTx.AddEdge(vertices[0].Id, "knows", vertices[1].Id, creationDate, "is aware of");
        edgesTx.AddEdge(vertices[1].Id, "knows", vertices[2].Id, creationDate, "is aware of");
        _fallen8.EnqueueTransaction(edgesTx).WaitUntilFinished();

        var edges = edgesTx.GetCreatedEdges();
    }

    public int CountWithName(string name)
    {
        List<AGraphElementModel> hits;
        _fallen8.GraphScan(out hits, "name", name, BinaryOperator.Equals);
        return hits.Count;
    }

    public string? NameOfVertex(int id)
    {
        _fallen8.TryGetVertex(out var v, id);
        if (v == null)
        {
            return null;
        }

        v.TryGetProperty(out string name, "name");
        return name;
    }
}
