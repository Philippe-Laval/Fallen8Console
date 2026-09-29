using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index;
using NoSQL.GraphDB.Core.Index.Fulltext;
using NoSQL.GraphDB.Core.Index.Range;
using NoSQL.GraphDB.Core.Model;
using NoSQL.GraphDB.Core.Transaction;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;

// See IndexTest.cs for more information on how to use indexes in Fallen-8.

namespace Fallen8Console
{
    public class IndexManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Fallen8 _fallen8;

        private DictionaryIndex _dictionaryIndex;
        private SingleValueIndex _singleValueIndex;
        private RangeIndex _rangeIndex;
        private RegExIndex _regExIndex;

        public IndexManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<IndexManager>();
            _fallen8 = new Fallen8(loggerFactory);
        }

        public void Dispose()
        {
            _fallen8.Dispose();
        }

        public void Init()
        {
            var testVertices = CreateTestVertices();
            var testVertex1 = testVertices[0];
            var testVertex2 = testVertices[1];
            var testVertex3 = testVertices[2];

            _dictionaryIndex = new DictionaryIndex();
            _dictionaryIndex.Initialize(_fallen8, null);

            _dictionaryIndex.AddOrUpdate("key1", testVertex1);
            _dictionaryIndex.AddOrUpdate("key1", testVertex2);  // Same key, different value
            _dictionaryIndex.AddOrUpdate("key2", testVertex3);

            // Try with another key-value pair
            _dictionaryIndex.AddOrUpdate("key3", testVertex1);

            _singleValueIndex = new SingleValueIndex();
            _singleValueIndex.Initialize(_fallen8, null);

            _singleValueIndex.AddOrUpdate("key1", testVertex1);
            _singleValueIndex.AddOrUpdate("key2", testVertex2);
            _singleValueIndex.AddOrUpdate("key3", testVertex3);

            _rangeIndex = new RangeIndex();
            _rangeIndex.Initialize(_fallen8, null);
            _rangeIndex.AddOrUpdate(10, testVertex1);
            _rangeIndex.AddOrUpdate(20, testVertex2);
            _rangeIndex.AddOrUpdate(30, testVertex3);


            _regExIndex = new RegExIndex();
            _regExIndex.Initialize(_fallen8, null);

            // Act - Add elements to index
            _regExIndex.AddOrUpdate("The quick brown fox jumps over the lazy dog", testVertex1);
            _regExIndex.AddOrUpdate("A fast red fox ran past the sleeping hound", testVertex2);
            _regExIndex.AddOrUpdate("The brown bear went fishing in the river", testVertex3);
        }

        public VertexModel[] CreateTestVertices()
        {
            uint creationDate = Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds());

            // Create test vertex 1
            var vertexTx1 = new CreateVertexTransaction()
            {
                Definition = new VertexDefinition()
                {
                    CreationDate = creationDate,
                    Label = "testPerson",
                    Properties = new Dictionary<string, object>
                    {
                        { "name", "TestPerson1" },
                        { "age", 25 },
                        { "description", "First test person for index testing" }
                    }
                }
            };
            _fallen8.EnqueueTransaction(vertexTx1).WaitUntilFinished();

            // Create test vertex 2
            var vertexTx2 = new CreateVertexTransaction()
            {
                Definition = new VertexDefinition()
                {
                    CreationDate = creationDate,
                    Label = "testPerson",
                    Properties = new Dictionary<string, object>
                    {
                        { "name", "TestPerson2" },
                        { "age", 35 },
                        { "description", "Second test person for index testing" }
                    }
                }
            };
            _fallen8.EnqueueTransaction(vertexTx2).WaitUntilFinished();

            // Create test vertex 3
            var vertexTx3 = new CreateVertexTransaction()
            {
                Definition = new VertexDefinition()
                {
                    CreationDate = creationDate,
                    Label = "testPerson",
                    Properties = new Dictionary<string, object>
                    {
                        { "name", "TestPerson3" },
                        { "age", 45 },
                        { "description", "Third test person for index testing" }
                    }
                }
            };
            _fallen8.EnqueueTransaction(vertexTx3).WaitUntilFinished();

            return new VertexModel[] { vertexTx1.VertexCreated, vertexTx2.VertexCreated, vertexTx3.VertexCreated };
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

        public void Test1()
        {
            // index should have 2 keys
            int keyCount = _dictionaryIndex.CountOfKeys();

            // key1 holds two values, key2 holds one
            int valueCount = _dictionaryIndex.CountOfValues();


            var keys = _dictionaryIndex.GetKeys().ToList();

            ImmutableList<AGraphElementModel> result;
            bool found = _dictionaryIndex.TryGetValue(out result, "key1");


            var testVertex1 = Element(0);
            if (testVertex1 is not null)
            {
                // RemoveValue removes the element from every bucket it appears in.
                // testVertex1 lives under both "key1" (alongside testVertex2) and "key3".
                _dictionaryIndex.RemoveValue(testVertex1);
            }
            else
            {
                _logger.LogInformation("Key not found in index.");
            }

            // After removing testVertex1, key1 should still exist because testVertex2 is still there.
            found = _dictionaryIndex.TryGetValue(out result, "key1");

            // 'key3' should be gone once its only value was removed
            found = _dictionaryIndex.TryGetValue(out result, "key3");

            // Direct key removal
            bool keyRemoved = _dictionaryIndex.TryRemoveKey("key2");

            // Wipe the index completely
            _dictionaryIndex.Wipe();

            // Clean up
            _dictionaryIndex.Dispose();
        }

        public void Test2()
        {
            int keyCount = _singleValueIndex.CountOfKeys();
            int valueCount = _singleValueIndex.CountOfValues();

            var keys = _singleValueIndex.GetKeys().ToList();

            ImmutableList<AGraphElementModel> result;
            bool found = _singleValueIndex.TryGetValue(out result, "key1");


            // Single value override when same key is used
            var testVertex1 = Element(0);
            var testVertex2 = Element(1);
            var testVertex3 = Element(2);

            // Single value override when same key is used
            _singleValueIndex.AddOrUpdate("key1", testVertex2);

            found = _singleValueIndex.TryGetValue(out result, "key1");

            var keyValues = _singleValueIndex.GetKeyValues().ToList();

            _singleValueIndex.RemoveValue(testVertex2);

            bool keyRemoved = _singleValueIndex.TryRemoveKey("key3");


            _singleValueIndex.AddOrUpdate("key4", testVertex1);
            var allValues = _singleValueIndex.Values();

            _singleValueIndex.Wipe();

            keyCount = _singleValueIndex.CountOfKeys();

            _singleValueIndex.Dispose();

        }

        public void Test3()
        {
            int keyCount = _rangeIndex.CountOfKeys();
            int valueCount = _rangeIndex.CountOfValues();

            var keys = _rangeIndex.GetKeys().ToList();

            ImmutableList<AGraphElementModel> result;
            bool found = _rangeIndex.TryGetValue(out result, 10);

            found = _rangeIndex.LowerThan(out result, 25, true);
            found = _rangeIndex.LowerThan(out result, 20, false);

            found = _rangeIndex.GreaterThan(out result, 15, true);
            found = _rangeIndex.GreaterThan(out result, 20, false);

            found = _rangeIndex.Between(out result, 15, 25, true, true);
            found = _rangeIndex.Between(out result, 10, 30, true, true);
            found = _rangeIndex.Between(out result, 10, 30, false, false);

            bool keyRemoved = _rangeIndex.TryRemoveKey(20);

            _rangeIndex.Wipe();

            keyCount = _rangeIndex.CountOfKeys();

            _rangeIndex.Dispose();

        }

        public void Test4()
        {
            IIndex index;
            // Create an index of type "RangeIndex" with name "ageRange"
            _fallen8.IndexFactory.TryCreateIndex(out index, "ageRange", "RangeIndex");

            var testVertex1 = Element(0);
            var testVertex2 = Element(1);
            var testVertex3 = Element(2);

            index.AddOrUpdate(10, testVertex1);
            index.AddOrUpdate(20, testVertex2);
            index.AddOrUpdate(30, testVertex3);

            // Use the index with name "ageRange"
            IReadOnlyList<AGraphElementModel> result;
            bool found = _fallen8.RangeIndexScan(out result, "ageRange", 15, 25, true, true);
        }

        public void Test5()
        {
            int keyCount = _regExIndex.CountOfKeys();
            int valueCount = _regExIndex.CountOfValues();

            ImmutableList<AGraphElementModel> result;
            bool found = _regExIndex.TryGetValue(out result, "The quick brown fox jumps over the lazy dog");

            // Fulltext query with multiple words
            FulltextSearchResult queryResult;
            found = _regExIndex.TryQuery(out queryResult, "fox");

            var count = queryResult.Elements.Count;
            var matchIds = queryResult.Elements.Select(r => r.GraphElement.Id).ToList();

            bool keyRemoved = _regExIndex.TryRemoveKey("The quick brown fox jumps over the lazy dog");

            _regExIndex.Wipe();




            var testVertex1 = Element(0);
            var testVertex2 = Element(1);
            var testVertex3 = Element(2);

            // One key can have several values
            // All three values added under one key must be retained
            _regExIndex.AddOrUpdate("the quick brown fox", testVertex1);
            _regExIndex.AddOrUpdate("the quick brown fox", testVertex2);
            _regExIndex.AddOrUpdate("the quick brown fox", testVertex3);

            found = _regExIndex.TryGetValue(out result, "the quick brown fox");
            count = result.Count;

            _regExIndex.Dispose();
        }

        public void Test6()
        {
            var indexLogger = _loggerFactory.CreateLogger<IndexFactory>();
            var indexFactory = new IndexFactory(_fallen8, indexLogger);
            var availablePlugins = indexFactory.GetAvailableIndexPlugins().ToList();
            foreach(var availablePlugin in availablePlugins)
            {
                _logger.LogInformation($"Available plugin : {availablePlugin}");
            }

            // Create a dictionary index
            IIndex dictionaryIndex;
            bool createdDictionary = indexFactory.TryCreateIndex(out dictionaryIndex, "testDictionaryIndex", "DictionaryIndex");

            IIndex singleValueIndex;
            bool createdSingleValue = indexFactory.TryCreateIndex(out singleValueIndex, "testSingleValueIndex", "SingleValueIndex");

            IIndex rangeIndex;
            bool createdRange = indexFactory.TryCreateIndex(out rangeIndex, "testRangeIndex", "RangeIndex");

            IIndex fulltextIndex;
            bool createdFulltext = indexFactory.TryCreateIndex(out fulltextIndex, "testFulltextIndex", "RegExIndex");

            // Retrieve the created index
            IIndex retrievedIndex;
            bool retrieved = indexFactory.TryGetIndex(out retrievedIndex, "testDictionaryIndex");


            bool deleted = indexFactory.TryDeleteIndex("testDictionaryIndex");
            deleted = indexFactory.TryDeleteIndex("testSingleValueIndex");
            deleted = indexFactory.TryDeleteIndex("testRangeIndex");
            deleted = indexFactory.TryDeleteIndex("testFulltextIndex");


            indexFactory.DeleteAllIndices();
        }

    }
}
