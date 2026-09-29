using NoSQL.GraphDB.Core.Index.Spatial;

// See SpatialIndexTest.cs for more information on how to use spatial indexes in Fallen-8.

namespace Fallen8Console
{
    /// <summary>
    /// Implementation of IMetric for Euclidean distance calculations
    /// </summary>
    public class EuclideanMetric : IMetric
    {
        public float Distance(IMBP point1, IMBP point2)
        {
            var sum = 0.0f;
            for (int i = 0; i < point1.Coordinates.Length; i++)
            {
                var diff = point1.Coordinates[i] - point2.Coordinates[i];
                sum += diff * diff;
            }
            return (float)Math.Sqrt(sum);
        }

        public float[] TransformationOfDistance(float distance, IMBR mbr)
        {
            // Return uniform transformation in all dimensions for Euclidean distance
            return new float[] { distance, distance };
        }
    }

    
}
