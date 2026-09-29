using Microsoft.Extensions.Logging;
using NoSQL.GraphDB.Core;
using NoSQL.GraphDB.Core.Index.Spatial;
using NoSQL.GraphDB.Core.Index.Spatial.Implementation.RTree;
using NoSQL.GraphDB.Core.Index.Spatial.Implementation.SpatialContainer;
using NoSQL.GraphDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

// See SpatialIndexTest.cs for more information on how to use spatial indexes in Fallen-8.

namespace Fallen8Console
{
    public class SpatialIndexManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public SpatialIndexManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SpatialIndexManager>();
        }

        #region Helper Methods

        private ISpatialIndex CreateAndInitializeRTree()
        {
            // Create a new RTree instance
            var rTree = new RTree();

            // Create required parameters for initialization
            var parameters = new Dictionary<string, object>();

            // Add metric for distance calculations
            parameters["IMetric"] = new EuclideanMetric();

            // Set minimum and maximum node counts
            parameters["MinCount"] = 2;
            parameters["MaxCount"] = 5;

            // Create space dimensions for 2D space
            var spaceDimensions = new List<IDimension>
            {
                new NoSQL.GraphDB.Core.Index.Spatial.Implementation.Geometry.RealDimension(),
                new NoSQL.GraphDB.Core.Index.Spatial.Implementation.Geometry.RealDimension(),
            };
            parameters["Space"] = spaceDimensions;

            // Initialize the RTree with the parameters
            rTree.Initialize(new Fallen8(_loggerFactory), parameters);

            return rTree;
        }

        private void InitializeSpatialIndex(ISpatialIndex spatialIndex)
        {
            // Only initialize if it's an RTree (our test implementation)
            if (spatialIndex is RTree)
            {
                // Add sample vertices with spatial data to index
                var vertices = new List<AGraphElementModel>
                {
                    CreateVertexWithGeometry(1, CreatePointGeometry(1.0f, 1.0f)),
                    CreateVertexWithGeometry(2, CreatePointGeometry(2.0f, 2.0f)),
                    CreateVertexWithGeometry(3, CreatePointGeometry(3.0f, 3.0f)),
                    CreateVertexWithGeometry(4, CreatePointGeometry(4.0f, 4.0f)),
                    CreateVertexWithGeometry(5, CreateRectangleGeometry(1.5f, 1.5f, 3.5f, 3.5f))
                };

                // Add vertices to the spatial index
                foreach (var vertex in vertices)
                {
                    AddToSpatialIndex(spatialIndex, vertex);
                }
            }
        }

        private void AddToSpatialIndex(ISpatialIndex spatialIndex, AGraphElementModel element)
        {
            // Get the geometry property from the element
            IGeometry? geometry;
            if (TryGetGeometry(element, out geometry))
            {
                // Add the element to the index with its geometry
                spatialIndex.AddOrUpdate(geometry, element);
            }
        }

        private bool TryGetGeometry(AGraphElementModel element, out IGeometry? geometry)
        {
            // Try to get geometry property using reflection since SetProperty is internal
            object? propertyValue;
            var properties = GetProperties(element);

            if (properties != null && properties.TryGetValue("geometry", out propertyValue) && propertyValue is IGeometry)
            {
                geometry = (IGeometry)propertyValue;
                return true;
            }

            geometry = null;
            return false;
        }

        private ImmutableDictionary<string, object> GetProperties(AGraphElementModel element)
        {
            // Read the properties through the public accessor. (This formerly reflected the private
            // _properties field and cast it to ImmutableDictionary; the memory-footprint compaction
            // changed that field's concrete type to a compact array, so we go through the accessor,
            // which still returns the same ImmutableDictionary snapshot.)
            return element.GetAllProperties();
        }

        private IGeometry CreatePointGeometry(float x, float y)
        {
            // Create a point geometry using a custom implementation
            return CreatePoint(x, y);
        }

        private IPoint CreatePoint(float x, float y)
        {
            // Create a point implementation using a custom implementation
            return new NoSQL.GraphDB.Core.Index.Spatial.Point(x, y);
        }

        private IGeometry CreateRectangleGeometry(float x1, float y1, float x2, float y2)
        {
            // Create a rectangle geometry using a custom implementation
            var lower = new NoSQL.GraphDB.Core.Index.Spatial.Point(x1, y1);
            var upper = new NoSQL.GraphDB.Core.Index.Spatial.Point(x2, y2);
            return new Rectangle(lower, upper);
        }

        private IMBR CreateMBR(float[] lower, float[] upper)
        {
            // Create a MBR (Minimum Bounding Rectangle) implementation using our custom class
            return new MBR(lower, upper);
        }

        private AGraphElementModel CreateVertexWithGeometry(int id, IGeometry geometry)
        {
            // Create a vertex model with spatial geometry
            var vertex = new VertexModel(
                id,
                Convert.ToUInt32(DateTimeOffset.Now.ToUnixTimeSeconds()), // Creation date
                "SpatialVertex", // Label
                new Dictionary<string, object>() // Properties
            );

            // Attach geometry to the vertex
            // This depends on how geometries are attached to graph elements in your implementation
            AttachGeometryToVertex(vertex, geometry);

            return vertex;
        }

        private void AttachGeometryToVertex(VertexModel vertex, IGeometry geometry)
        {
            // Since we don't have direct access to add properties (SetProperty is internal)
            // For testing purposes, we'll use reflection to set the property
            // In a real implementation, you would use the appropriate API calls

            // Get the SetProperty method via reflection
            var method = typeof(AGraphElementModel).GetMethod("SetProperty",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(vertex, new object[] { "geometry", geometry });
            }
        }

        #endregion


        public void Test_Distance()
        {
            var spatialIndex = CreateAndInitializeRTree();

            var point1 = CreatePointGeometry(0, 0);
            var point2 = CreatePointGeometry(3, 4);
            var element1 = CreateVertexWithGeometry(1, point1);
            var element2 = CreateVertexWithGeometry(2, point2);

            spatialIndex.AddOrUpdate(point1, element1);
            spatialIndex.AddOrUpdate(point2, element2);

            float distance = spatialIndex.Distance(point1, point2);
            _logger.LogInformation($"sqrt(3*3 + 4*4) = 5 => {distance}");
        }

        public void Test_SearchRegion()
        {
            var spatialIndex = CreateAndInitializeRTree();
            InitializeSpatialIndex(spatialIndex);

            // Define search region (MBR)
            float[] lower = new float[] { 1.0f, 1.0f };
            float[] upper = new float[] { 4.0f, 4.0f };
            var mbr = CreateMBR(lower, upper);

            ImmutableList<AGraphElementModel> result;
            var found = spatialIndex.SearchRegion(out result, mbr);
        }

        public void Test_Overlap()
        {
            var spatialIndex = CreateAndInitializeRTree();
            InitializeSpatialIndex(spatialIndex);

            // Create a geometry that overlaps with existing geometries
            var overlapGeometry = CreateRectangleGeometry(2.0f, 2.0f, 4.0f, 4.0f);

            ImmutableList<AGraphElementModel> result;
            var found = spatialIndex.Overlap(out result, overlapGeometry);
        }

        public void Test_GetNextNeighbors()
        {
            var spatialIndex = CreateAndInitializeRTree();
            InitializeSpatialIndex(spatialIndex);

            var point = CreatePointGeometry(2.5f, 2.5f);
            int neighborCount = 2;

            ImmutableList<AGraphElementModel> result;
            var found = spatialIndex.GetNextNeighbors(out result, point, neighborCount);
        }

        public void Test_SearchDistance()
        {
            var spatialIndex = CreateAndInitializeRTree();
            InitializeSpatialIndex(spatialIndex);

            var point = CreatePointGeometry(2.5f, 2.5f);
            float searchDistance = 2.0f;

            ImmutableList<AGraphElementModel> result;
            var found = spatialIndex.SearchDistance(out result, searchDistance, point);
        }

        public void Test_SearchPoint()
        {
            var spatialIndex = CreateAndInitializeRTree();
            InitializeSpatialIndex(spatialIndex);

            var point = CreatePoint(2.0f, 2.0f);

            // exact point match
            ImmutableList<AGraphElementModel> result;
            var found = spatialIndex.SearchPoint(out result, point);
        }

    }
}
