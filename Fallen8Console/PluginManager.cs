using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Vector;
using NoSQL.GraphDB.Core.Plugin;
using NoSQL.GraphDB.Core.Plugins;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fallen8Console
{
    public class PluginManager
    {
        private readonly string _savePath;
        private readonly string _wallPath;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        public Fallen8 Fallen8 => _fallen8;

        public PluginManager(bool inMemory, string savePath, string walPath, ILoggerFactory loggerFactory)
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


        public void RegisterPlugins()
        {
            _fallen8.RegisterPluginType<DictionaryIndex>();
            _fallen8.RegisterPluginType<VectorIndex>();
        }

        public void GetPluginInfo()
        {
            _logger.LogInformation($"Max plugin count: {_fallen8.Plugins.MaxCount}");
            _logger.LogInformation($"Current plugin count: {_fallen8.Plugins.Count}");

            _logger.LogInformation("Registered plugins:");

            //foreach (var plugin in _fallen8.Plugins.GetAll())
            //{
            //    _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            //}

            _logger.LogInformation("IShortestPathAlgorithm:");
            foreach (var plugin in _fallen8.Plugins.EntriesForContract(PluginContract.Path))
            {
                _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            }

            _logger.LogInformation("ISubGraphAlgorithm:");
            foreach (var plugin in _fallen8.Plugins.EntriesForContract(PluginContract.SubGraph))
            {
                _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            }

            _logger.LogInformation("IGraphAnalyticsAlgorithm:");
            foreach (var plugin in _fallen8.Plugins.EntriesForContract(PluginContract.Analytics))
            {
                _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            }

            _logger.LogInformation("IGraphFunction:");
            foreach (var plugin in _fallen8.Plugins.EntriesForContract(PluginContract.GraphFunction))
            {
                _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            }

            _logger.LogInformation("IIndex:");
            foreach (var plugin in _fallen8.Plugins.EntriesForContract(PluginContract.Index))
            {
                _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            }

            _logger.LogInformation("IService:");
            foreach (var plugin in _fallen8.Plugins.EntriesForContract(PluginContract.Service))
            {
                _logger.LogInformation($"  - {plugin.Definition.Name} {plugin.Definition.Category} {plugin.Definition.Contract} {plugin.Definition.Description}");
            }
        }

        public void Register(PluginEntry entry)
        {
            var info = _fallen8.EnqueueTransaction(new RegisterPluginTransaction { Entry = entry });
            info.WaitUntilFinished();
        }

        public void Remove(string name)
        {
            var info = _fallen8.EnqueueTransaction(new RemovePluginTransaction { Name = name });
            info.WaitUntilFinished();
        }

        public void Test()
        {
            var names = PluginFactory.AvailableBuiltInNames(PluginContract.Analytics).ToList();
            foreach (var name in names)
            {
                _logger.LogInformation($"Built-in plugin: {name}");
            }
        }

        public void Test2()
        {
             //private readonly PluginCompiler _compiler = new PluginCompiler();
        }


    }
}
