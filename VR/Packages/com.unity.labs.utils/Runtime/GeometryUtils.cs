using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Labs.Utils
{
    public static class GeometryUtils
    {
        // a constant used in approximate equality checks
        static readonly float k_EpsilonScaled = Mathf.Epsilon * 8;
        static readonly float TwoPi = Mathf.PI * 2f;


        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly double[] k_BoxLineAngles = new double[4];
        static readonly List<Vector3> k_HullEdgeDirections = new List<Vector3>();

        /// <summary>
        /// Finds the two closest adjacent vertices in a polygon, to a separate world space position
        /// </summary>
        /// <param name="vertices">An outline of a polygon defined by vertices, each one connected to the next</param>
        /// <param name="point">The position in space to find the two closest outline vertices to</param>
        /// <param name="vertexA">One vertex of the nearest edge</param>
        /// <param name="vertexB">The other vertex of the nearest edge</param>
        /// <returns>True if a nearest edge could be found</returns>
        public static bool FindClosestEdge(List<Vector3> vertices, Vector3 point,
            out Vector3 vertexA, out Vector3 vertexB)
        {
            var vertexCount = vertices.Count;
            if (vertexCount < 1)
            {
                vertexA = Vector3.zero;
                vertexB = Vector3.zero;
                return false;
            }

            var shortestSqrDistance = float.MaxValue;
            var closestVertA = Vector3.zero;
            var closestVertB = Vector3.zero;
            for (var i = 0; i < vertexCount; i++)
            {
                var vert = vertices[i];
                var nextVert = vertices[(i + 1) % vertices.Count];

                var closestPointOnEdge = ClosestPointOnLineSegment(point, vert, nextVert);
                var sqrDistanceToEdge = Vector3.SqrMagnitude(point - closestPointOnEdge);
                if (sqrDistanceToEdge < shortestSqrDistance)
                {
                    shortestSqrDistance = sqrDistanceToEdge;
                    closestVertA = vert;
                    closestVertB = nextVert;
                }
            }

            vertexA = closestVertA;
            vertexB = closestVertB;
            return true;
        }

        /// <summary>
        /// Finds the furthest intersection point on a polygon from a point in space
        /// </summary>
        /// <param name="vertices">An outline of a polygon defined by vertices, each one connected to the next</param>
        /// <param name="point">The position in world space to find the furthest intersection point </param>
        /// <returns>A world space position of a point on the polygon that is as far from the input point as possible</returns>
        public static Vector3 PointOnOppositeSideOfPolygon(List<Vector3> vertices, Vector3 point)
        {
            const float oppositeSideBufferScale = 100.0f;

            var vertexCount = vertices.Count;
            if (vertexCount < 3)
                return Vector3.zero;

            var a = vertices[0];
            var b = vertices[1];
            var c = vertices[2];
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var center = Vector3.zero;
            foreach (var vertex in vertices)
            {
                center += vertex;
            }

            center *= 1f / vertexCount;
            var toPoint = Vector3.ProjectOnPlane(point - center, normal);

            var lengthMinusOne = vertexCount - 1;
            for (var i = 0; i < vertexCount; i++)
            {
                var vertexA = vertices[i];
                var aNeighbor = i == lengthMinusOne ? a : vertices[i + 1];
                var aLineVector = aNeighbor - vertexA;

                float s, t;
                ClosestTimesOnTwoLines(vertexA, aLineVector, center, -toPoint * oppositeSideBufferScale, out s, out t);
                if (t >= 0 && s >= 0 && s <= 1)
                {
                    return vertexA + aLineVector * s;
                }
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Determine if a point is within a certain distance from the edge of a polygonal surface
        /// </summary>
        /// <param name="point">the point to test</param>
        /// <param name="center">the center of the surface</param>
        /// <param name="vertices">all vertices of the outer boundary polygon</param>
        /// <param name="edgeWidthSquare">the square of the defined width of the edge</param>
        /// <returns>True if the point is range of the surface</returns>
        public static bool WithinEdgeZone(Vector3 point, Vector3 center, List<Vector3> vertices, float edgeWidthSquare)
        {
            Vector3 outerVertexA;
            Vector3 outerVertexB;
            if (!FindClosestEdge(vertices, point, out outerVertexA, out outerVertexB))
            {
                // Not enough points means there is no edge to be close to
                return false;
            }

            var innerVertexA = outerVertexA - (outerVertexA - center).normalized * edgeWidthSquare;
            var innerVertexB = outerVertexB - (outerVertexB - center).normalized * edgeWidthSquare;

            var withinOuter = PointInHorizontalTriangle(point, outerVertexA, outerVertexB, center);
            var withinInner = PointInHorizontalTriangle(point, innerVertexA, innerVertexB, center);

            return withinOuter && !withinInner;
        }

        /// <summary>
        /// Determine if a point is within a horizontal (x,z) triangle
        /// </summary>
        /// <param name="point">the point to test</param>
        /// <param name="v0">the first vertex of the triangle</param>
        /// <param name="v1">the second vertex of the triangle</param>
        /// <param name="v2">the third vertex of the triangle</param>
        /// <returns>True if the point is inside the triangle, false otherwise</returns>
        public static bool PointInHorizontalTriangle(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            var dX = point.x - v2.x;
            var dZ = point.z - v2.z;

            var dX21 = v2.x - v1.x;
            var dZ12 = v1.z - v2.z;
            var dX02 = v0.x - v2.x;

            // calculate barycentric coordinates of this point.
            var d = dZ12 * dX02 + dX21 * (v0.z - v2.z);
            var s = dZ12 * dX + dX21 * dZ;
            var t = (v2.z - v0.z) * dX + dX02 * dZ;

            return d < 0 ? s <= 0 && t <= 0 && s + t >= d : s >= 0 && t >= 0 && s + t <= d;
        }

        /// <summary>
        /// Given a number of perimeter vertices, generate a triangle buffer and add it to the given list
        /// The winding order is reversible. Example winding orders:
        /// Normal:   Reverse:
        /// 0, 1, 2,  0, 2, 1,
        /// 0, 2, 3,  0, 3, 2,
        /// 0, 3, 4,  0, 4, 3,
        /// --etc--
        /// </summary>
        /// <param name="indices">The list to which the triangle buffer will be added</param>
        /// <param name="vertCount">The number of perimeter vertices</param>
        /// <param name="reverse">(Optional) Whether to reverse the winding order of the vertices</param>
        public static void TriangulatePolygon(List<int> indices, int vertCount, bool reverse = false)
        {
            vertCount-= 2;
            indices.Capacity = Math.Max(indices.Capacity, vertCount * 3);
            if (reverse)
            {
                for (var i = 0; i < vertCount; i++)
                {
                    indices.Add(0);
                    indices.Add(i + 2);
                    indices.Add(i + 1);
                }
            }
            else
            {
                for (var i = 0; i < vertCount; i++)
                {
                    indices.Add(0);
                    indices.Add(i + 1);
                    indices.Add(i + 2);
                }
            }
        }

        /// <summary>
        /// Two trajectories which may or may not intersect have a time along each path which minimizes the distance
        /// between trajectories. This function finds those two times. The same logic applies to line segments, where
        /// the one point is the starting position, and the second point is the position at t = 1.
        /// </summary>
        /// <param name="positionA">Starting point of object a</param>
        /// <param name="velocityA">Velocity (direction and magnitude) of object a</param>
        /// <param name="positionB">Starting point of object b</param>
        /// <param name="velocityB">Velocity (direction and magnitude) of object b</param>
        /// <param name="s">The time along trajectory a</param>
        /// <param name="t">The time along trajectory b</param>
        /// <param name="parallelTest">(Optional) epsilon value for parallel lines test</param>
        /// <returns>False if the lines are parallel, otherwise true</returns>
        public static bool ClosestTimesOnTwoLines(Vector3 positionA, Vector3 velocityA, Vector3 positionB, Vector3 velocityB,
            out float s, out float t, double parallelTest = double.Epsilon)
        {
            // Cast dot products to doubles because parallel test can fail on some hardware (iOS)
            var a = (double)Vector3.Dot(velocityA, velocityA);
            var b = (double)Vector3.Dot(velocityA, velocityB);
            var e = (double)Vector3.Dot(velocityB, velocityB);

            var d = a * e - b * b;

            //lines are parallel
            if (Math.Abs(d) < parallelTest)
            {
                s = 0;
                t = 0;
                return false;
            }

            var r = positionA - positionB;
            var c = Vector3.Dot(velocityA, r);
            var f = Vector3.Dot(velocityB, r);

            s = (float)((b * f - c * e) / d);
            t = (float)((a * f - c * b) / d);

            return true;
        }

        /// <summary>
        /// Finds the points along two line segments which are closest together
        /// </summary>
        /// <param name="a">Starting point of segment A</param>
        /// <param name="aLineVector">Vector from point a to the end point of segment A</param>
        /// <param name="b">Starting point of segment B</param>
        /// <param name="bLineVector">Vector from point b to the end point of segment B</param>
        /// <param name="resultA">The resulting point along segment A</param>
        /// <param name="resultB">The resulting point along segment B</param>
        /// <param name="parallelTest">(Optional) epsilon value for parallel lines test</param>
        public static bool ClosestPointsOnTwoLineSegments(Vector3 a, Vector3 aLineVector, Vector3 b, Vector3 bLineVector,
            out Vector3 resultA, out Vector3 resultB, double parallelTest = double.Epsilon)
        {
            float s;
            float t;
            var parallel = !ClosestTimesOnTwoLines(a, aLineVector, b, bLineVector, out s, out t, parallelTest);

            if (s > 0 && s <= 1 && t > 0 && t <= 1)
            {
                resultA = a + aLineVector * s;
                resultB = b + bLineVector * t;
            }
            else
            {
                // Edge cases (literally--we are checking each of the four endpoints against the opposite segment)
                var bNeighbor = b + bLineVector;
                var aNeighbor = a + aLineVector;
                var aOnB = ClosestPointOnLineSegment(a, b, bNeighbor);
                var aNeighborOnB = ClosestPointOnLineSegment(aNeighbor, b, bNeighbor);
                var minDist = Vector3.Distance(a, aOnB);
                resultA = a;
                resultB = aOnB;

                var nextDist = Vector3.Distance(aNeighbor, aNeighborOnB);
                if (nextDist < minDist)
                {
                    resultA = aNeighbor;
                    resultB = aNeighborOnB;
                    minDist = nextDist;
                }

                var bOnA = ClosestPointOnLineSegment(b, a, aNeighbor);
                nextDist = Vector3.Distance(b, bOnA);
                if (nextDist < minDist)
                {
                    resultA = bOnA;
                    resultB = b;
                    minDist = nextDist;
                }

                var bNeighborOnA = ClosestPointOnLineSegment(bNeighbor, a, aNeighbor);
                nextDist = Vector3.Distance(bNeighbor, bNeighborOnA);
                if (nextDist < minDist)
                {
                    resultA = bNeighborOnA;
                    resultB = bNeighbor;
                }

                if (parallel)
                {
                    if (Vector3.Dot(aLineVector, bLineVector) > 0)
                    {
                        t = Vector3.Dot(bNeighbor - a, aLineVector.normalized) * 0.5f;
                        var midA = a + aLineVector.normalized * t;
                        var midB = bNeighbor + bLineVector.normalized * -t;
                        if (t > 0 && t < aLineVector.magnitude)
                        {
                            resultA = midA;
                            resultB = midB;
                        }
                    }
                    else
                    {
                        t = Vector3.Dot(aNeighbor - bNeighbor, aLineVector.normalized) * 0.5f;
                        var midA = aNeighbor + aLineVector.normalized * -t;
                        var midB = bNeighbor + bLineVector.normalized * -t;
                        if (t > 0 && t < aLineVector.magnitude)
                        {
                            resultA = midA;
                            resultB = midB;
                        }
                    }
                }
            }

            return parallel;
        }

        /// <summary>
        /// Returns the closest point along a line segment to a given point
        /// </summary>
        /// <param name="point">The point to test against the line segment</param>
        /// <param name="a">The first point of the line segment</param>
        /// <param name="b">The second point of the line segment</param>
        /// <returns>The closest point along the line segment to <paramref name="point"/></returns>
        public static Vector3 ClosestPointOnLineSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var segment = b - a;
            var direction = segment.normalized;
            var projection = Vector3.Dot(point - a, direction);
            if (projection < 0)
                return a;

            if (projection*projection > segment.sqrMagnitude)
                return b;

            return a + projection * direction;
        }

        /// <summary>
        /// Find the closest points on the perimeter of a pair of polygons
        /// </summary>
        /// <param name="verticesA">The vertex list of polygon A</param>
        /// <param name="verticesB">The vertex list of polygon B</param>
        /// <param name="pointA">The point on polygon A's closest to an edge of polygon B</param>
        /// <param name="pointB">The point on polygon B's closest to an edge of polygon A</param>
        /// <param name="parallelTest">The minimum distance between closest approaches used to detect parallel line segments</param>
        public static void ClosestPolygonApproach(List<Vector3> verticesA, List<Vector3> verticesB,
            out Vector3 pointA, out Vector3 pointB, float parallelTest = 0f)
        {
            pointA = default(Vector3);
            pointB = default(Vector3);
            var closest = float.MaxValue;
            var aCount = verticesA.Count;
            var bCount = verticesB.Count;
            var aCountMinusOne = aCount - 1;
            var bCountMinusOne = bCount - 1;
            var firstVertexA = verticesA[0];
            var firstVertexB = verticesB[0];
            for (var i = 0; i < aCount; i++)
            {
                var vertexA = verticesA[i];
                var aNeighbor = i == aCountMinusOne ? firstVertexA : verticesA[i + 1];
                var aLineVector = aNeighbor - vertexA;

                for (var j = 0; j < bCount; j++)
                {
                    var vertexB = verticesB[j];
                    var bNeighbor = j == bCountMinusOne ? firstVertexB : verticesB[j + 1];
                    var bLineVector = bNeighbor - vertexB;

                    Vector3 a;
                    Vector3 b;
                    var parallel = ClosestPointsOnTwoLineSegments(vertexA, aLineVector, vertexB, bLineVector,
                        out a, out b, parallelTest);

                    var dist = Vector3.Distance(a, b);

                    if (parallel)
                    {
                        var delta = dist - closest;
                        if (delta < parallelTest)
                        {
                            closest = dist - parallelTest;
                            pointA = a;
                            pointB = b;
                        }
                    }
                    else if (dist < closest)
                    {
                        closest = dist;
                        pointA = a;
                        pointB = b;
                    }
                }
            }
        }

        /// <summary>
        /// Determines if a point is inside of a polygon on the XZ plane, the y value is not used
        /// </summary>
        /// <param name="testPoint">The point to test</param>
        /// <param name="vertices">The vertices that make up the bounds of the polygon</param>
        /// <returns>True if the point is inside the polygon, false otherwise</returns>
        public static bool PointInPolygon(Vector3 testPoint, List<Vector3> vertices)
        {
            // Sanity check - not enough bounds vertices = nothing to be inside of
            if (vertices.Count < 3)
                return false;

            // Check how many lines this test point collides with going in one direction
            // Odd = Inside, Even = Outside
            var collisions = 0;
            var vertexCounter = 0;
            var startPoint = vertices[vertices.Count - 1];

            // We recenter the test point around the origin to simplify the math a bit
            startPoint.x -= testPoint.x;
            startPoint.z -= testPoint.z;

            while (vertexCounter < vertices.Count)
            {
                var endPoint = vertices[vertexCounter];
                endPoint.x -= testPoint.x;
                endPoint.z -= testPoint.z;

                // Ignore nearly horizontal lines, lines that end at the origin, or lines on the same side of the origin
                if (!Mathf.Approximately(startPoint.z - endPoint.z, 0.0f)
                    && !Mathf.Approximately(endPoint.z, 0.0f)
                    && endPoint.z * startPoint.z <= 0)
                {
                    if ((startPoint.x * endPoint.z - startPoint.z * endPoint.x) / -(startPoint.z - endPoint.z) > 0)
                        collisions++;
                }

                startPoint = endPoint;
                vertexCounter++;
            }

            return collisions % 2 > 0;
        }

        /// <summary>
        /// Determines if a point is inside of a convex polygon and lies on the surface
        /// </summary>
        /// <param name="testPoint">The point to test</param>
        /// <param name="vertices">The vertices that make up the bounds of the polygon, these should be convex and coplanar but can have any normal</param>
        /// <returns>True if the point is inside the polygon and coplanar, false otherwise</returns>
        public static bool PointInPolygon3D(Vector3 testPoint, List<Vector3> vertices)
        {
            // Not enough bounds vertices = nothing to be inside of
            if (vertices.Count < 3)
                return false;

            // Compute the sum of the angles between the test point and each pair of edge points
            double angleSum = 0;
            for (var vertIndex = 0; vertIndex < vertices.Count; vertIndex++)
            {
                var toA = vertices[vertIndex] - testPoint;
                var toB = vertices[(vertIndex + 1) % vertices.Count] - testPoint;
                var sqrDistances = toA.sqrMagnitude * toB.sqrMagnitude; // Use sqrMagnitude, take sqrt of result later
                if (sqrDistances <= k_EpsilonScaled) // On a vertex
                {
                    return true;
                }

                double cosTheta = Vector3.Dot(toA, toB) / Mathf.Sqrt(sqrDistances);
                var angle = Math.Acos(cosTheta);
                angleSum += angle;
            }
            // The sum will only be 2*PI if the point is on the plane of the polygon and on the interior
            const float radiansCompareThreshold = 0.01f;
            return Mathf.Abs((float)angleSum - TwoPi) < radiansCompareThreshold;
        }


        /// <summary>
        /// Returns the closest point on a plane to another point
        /// </summary>
        /// <param name="planeNormal">The plane normal</param>
        /// <param name="planePoint">A point on the plane</param>
        /// <param name="point">The other point</param>
        /// <returns>The closest point on the plane to the other point</returns>
        public static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
        {
            var distance = -Vector3.Dot(planeNormal.normalized, point - planePoint);
            return point + planeNormal.normalized * distance;
        }

        /// <summary>
        /// Finds the smallest convex polygon in the xz plane that contains <paramref name="points"/>
        /// Based on algorithm outlined in https://www.bitshiftprogrammer.com/2018/01/gift-wrapping-convex-hull-algorithm.html
        /// </summary>
        /// <param name="points">Points used to find the convex hull. The y values of these points are ignored.</param>
        /// <param name="hull">List that will be filled out with vertices that define a convex polygon</param>
        /// <returns>True if <paramref name="points"/> has at least 3 entries, false otherwise</returns>
        public static bool ConvexHull2D(List<Vector3> points, List<Vector3> hull)
        {
            if (points.Count < 3)
                return false;

            var pointsCount = points.Count;
            var leftmostPointIndex = 0;
            for (var i = 1; i < pointsCount; ++i)
            {
                var pointX = points[i].x;
                var pointZ = points[i].z;
                var leftmostX = points[leftmostPointIndex].x;
                var leftmostZ = points[leftmostPointIndex].z;

                // As we traverse the outermost points, if we find 3 or more collinear points then we skip points that
                // fall in the middle. So if our starting point falls in the middle of a line, it will always be skipped
                // and our loop's end condition will never be met. So if there are multiple leftmost points, we want to
                // use the point that has the minimum Z.
                if (pointX < leftmostX || Mathf.Approximately(pointX, leftmostX) && pointZ < leftmostZ)
                    leftmostPointIndex = i;
            }

            // Starting from the leftmost point, move clockwise along outermost points until we are back at the starting point.
            var currentIndex = leftmostPointIndex;
            do
            {
                var currentPoint = points[currentIndex];
                hull.Add(currentPoint);

                // This loop is where we find the next outermost point (next point on the hull clockwise).
                // To do this we start with a point "p" which is an arbitrary entry in "points".
                // We iterate through each point "q" in "points". If "q" is to the left of the line from
                // the current point to "p", then "p" takes on the value of "q" and we continue iterating.
                // By the end of iteration, "p" will be the next point on the hull because no point is more to the left.
                var pIndex = 0;
                for (var qIndex = 1; qIndex < pointsCount; ++qIndex)
                {
                    if (qIndex == currentIndex)
                        continue;

                    var p = points[pIndex];
                    var q = points[qIndex];
                    var currentToP = p - currentPoint;
                    var currentToQ = q - currentPoint;

                    // The y value of the cross product of (current -> p) and (current -> q) tells us where q is
                    // in relation to the line (current -> p).
                    // If y is zero, q is on the line.
                    var crossY = currentToP.z * currentToQ.x - currentToP.x * currentToQ.z;

                    // next few lines are mostly an inlined ` Mathf.Approximately(crossY, 0f) `,
                    // because we sometimes call this equality check many thousands of times in a frame
                    var absCrossY = crossY > 0f ? crossY : -crossY;
                    var yCrossMinus = 0f - crossY;
                    var yIsNegative = yCrossMinus > 0f;
                    yCrossMinus = yIsNegative ? yCrossMinus : -yCrossMinus;
                    var scaledCrossY = 0.000001f * absCrossY;
                    var maxOfCrossYAndZero = scaledCrossY > 0f ? scaledCrossY : 0f;
                    var max = maxOfCrossYAndZero > k_EpsilonScaled ? maxOfCrossYAndZero : k_EpsilonScaled;

                    var approximatelyEqual = yCrossMinus < max;
                    if (approximatelyEqual)
                    {
                        // If current, p, and q are collinear, then we want p to be the point that is furthest from current.
                        if (Vector3.SqrMagnitude(currentPoint - p) < Vector3.SqrMagnitude(currentPoint - q))
                            pIndex = qIndex;
                    }
                    // If y is negative, q is to the left.
                    else if (yIsNegative)
                    {
                        pIndex = qIndex;
                    }
                }

                currentIndex = pIndex;
            } while (currentIndex != leftmostPointIndex);

            return true;
        }

        /// <summary>
        /// Given a list of vertices of a 2d convex polygon, find the centroid of the polygon.
        /// This implementation operates only on the X and Z axes
        /// </summary>
        /// <param name="vertices">The vertices of the 2D polygon</param>
        /// <returns>The centroid point for the polygon</returns>
        public static Vector3 PolygonCentroid2D(List<Vector3> vertices)
        {
            var vertexCount = vertices.Count;
            double partialSignedArea, signedArea = 0;
            double centroidX = 0, centroidZ = 0;
            double currentX, currentZ;
            double nextX, nextZ;

            int i;
            for (i = 0; i < vertexCount - 1; i++)
            {
                var vertex = vertices[i];
                currentX = vertex.x;
                currentZ = vertex.z;
                var nextVertex = vertices[i+1];
                nextX = nextVertex.x;
                nextZ = nextVertex.z;

                partialSignedArea = currentX * nextZ - nextX * currentZ;
                signedArea += partialSignedArea;

                centroidX += (currentX + nextX) * partialSignedArea;
                centroidZ += (currentZ + nextZ) * partialSignedArea;
            }

            // Do last vertex separately so we don't check indexes via modulo every iteration
            var vertexI = vertices[i];
            currentX = vertexI.x;
            currentZ = vertexI.z;
            var vertex0 = vertices[0];
            nextX = vertex0.x;
            nextZ = vertex0.z;

            partialSignedArea = currentX * nextZ - nextX * currentZ;
            signedArea += partialSignedArea;

            centroidX += (currentX + nextX) * partialSignedArea;
            centroidZ += (currentZ + nextZ) * partialSignedArea;

            signedArea *= 0.5;
            var signedAreaMultiple = 6.0 * signedArea;
            centroidX /= signedAreaMultiple;
            centroidZ /= signedAreaMultiple;

            return new Vector3((float)centroidX, 0f, (float)centroidZ);
        }

        /// <summary>
        /// Find the oriented minimum bounding box for a 2D convex hull.
        /// This implements the 'rotating calipers' algorithm and operates in linear time.
        /// Operates only on the X and Z axes of the input.
        /// </summary>
        /// <param name="convexHull">The list of all points in a 2D convex hull on the x and z axes, in a clockwise winding order</param>
        /// <param name="boundingBox">An array of length 4 to fill with the vertex positions of the bounding box,
        /// in the order { top left, bottom left, bottom right, top right }</param>
        /// <returns>The size of the bounding box on each axis. Y here maps to the Z axis</returns>
        public static Vector2 OrientedMinimumBoundingBox2D(List<Vector3> convexHull, Vector3[] boundingBox)
        {
            // Box lines start axis-aligned as shown before we orient
            //        top
            //      <------^
            //      |      | right
            // left |      |
            //      V------>
            //       bottom

            var left = new Vector3(0f, 0f, -1f);
            var right = new Vector3(0f, 0f, 1f);
            var top = new Vector3(-1f, 0f, 0f);
            var bottom = new Vector3(1f, 0f, 0f);

            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            int leftIndex = 0, rightIndex = 0, topIndex = 0, bottomIndex = 0;

            // find the indices of the 'extreme points' in the hull to use as starting edge indices
            var vertexCount = convexHull.Count;
            for (var i = 0; i < vertexCount; i++)
            {
                var vertex = convexHull[i];
                var x = vertex.x;
                if (x < xMin)
                {
                    xMin = x;
                    leftIndex = i;
                }

                if (x > xMax)
                {
                    xMax = x;
                    rightIndex = i;
                }

                var z = vertex.z;
                if (z < yMin)
                {
                    yMin = z;
                    bottomIndex = i;
                }

                if (z > yMax)
                {
                    yMax = z;
                    topIndex = i;
                }
            }

            // compute & store the direction of every edge in the hull
            k_HullEdgeDirections.Clear();
            var lastVertexIndex = vertexCount - 1;
            for (var i = 0; i < lastVertexIndex; i++)
            {
                var edgeDirection = convexHull[i] - convexHull[i + 1];
                edgeDirection.Normalize();
                k_HullEdgeDirections.Add(edgeDirection);
            }

            // by doing the last vertex on its own, we can skip checking indices while iterating above
            var lastEdgeDirection = convexHull[lastVertexIndex] - convexHull[0];
            lastEdgeDirection.Normalize();
            k_HullEdgeDirections.Add(lastEdgeDirection);

            var bestOrientedBoundingBoxArea = double.MaxValue;
            // for every vertex in the hull, try aligning a box edge with an edge the vertex lies on
            for (var i = 0; i < vertexCount; i++)
            {
                var leftEdge = k_HullEdgeDirections[leftIndex];
                var rightEdge = k_HullEdgeDirections[rightIndex];
                var topEdge = k_HullEdgeDirections[topIndex];
                var bottomEdge = k_HullEdgeDirections[bottomIndex];

                // find the angles between our box lines and the polygon edges, by doing
                // ` arccosine(boxEdge · hullEdge) ` for each pair of bounding box edge & polygon edge
                k_BoxLineAngles[0] = Math.Acos(left.x * leftEdge.x + left.z * leftEdge.z);
                k_BoxLineAngles[1] = Math.Acos(right.x * rightEdge.x + right.z * rightEdge.z);
                k_BoxLineAngles[2] = Math.Acos(top.x * topEdge.x + top.z * topEdge.z);
                k_BoxLineAngles[3] = Math.Acos(bottom.x * bottomEdge.x + bottom.z * bottomEdge.z);

                // find smallest angle among the lines
                var smallestAngleIndex = 0;
                var smallestAngle = double.MaxValue;
                for (var l = 0; l < k_BoxLineAngles.Length; l++)
                {
                    var lineAngle = k_BoxLineAngles[l];
                    if (lineAngle < smallestAngle)
                    {
                        smallestAngle = lineAngle;
                        smallestAngleIndex = l;
                    }
                }

                // based on which box edge had the smallest angle between it & the polygon, rotate our rectangle
                switch (smallestAngleIndex)
                {
                    // left
                    case 0:
                        left = leftEdge;
                        right = -left;
                        // orthogonal to left
                        top = new Vector3(left.z, 0f, -left.x);
                        bottom = -top;
                        // set left start vertex to next point
                        leftIndex = (leftIndex + 1) % vertexCount;
                        break;
                    // right
                    case 1:
                        right = rightEdge;
                        left = -right;
                        top = new Vector3(left.z, 0f, -left.x);
                        bottom = -top;
                        rightIndex = (rightIndex + 1) % vertexCount;
                        break;
                    // top
                    case 2:
                        top = topEdge;
                        bottom = -top;
                        // orthogonal to bottom
                        left = new Vector3(bottom.z, 0f, -bottom.x);
                        right = -left;
                        topIndex = (topIndex + 1) % vertexCount;
                        break;
                    // bottom
                    case 3:
                        bottom = bottomEdge;
                        top = -bottom;
                        left = new Vector3(bottom.z, 0f, -bottom.x);
                        right = -left;
                        bottomIndex = (bottomIndex + 1) % vertexCount;
                        break;
                }

                // since we've modified one of our indices, get our starting points again
                var leftStart = convexHull[leftIndex];
                var rightStart = convexHull[rightIndex];
                var topStart = convexHull[topIndex];
                var bottomStart = convexHull[bottomIndex];

                // find bounding box side starting positions for our new set of vertices
                var upperLeft = IntersectTwoLinesXz(leftStart, left, topStart, top);
                var upperRight = IntersectTwoLinesXz(rightStart, right, topStart, top);
                var bottomLeft = IntersectTwoLinesXz(bottomStart, bottom, leftStart, left);
                var bottomRight = IntersectTwoLinesXz(bottomStart, bottom, rightStart, right);

                // usually with rotating calipers, this comparison is talked about in terms of distance,
                // but since we just want to know which is bigger it works to use square magnitudes
                var sqrDistanceX = (upperLeft - upperRight).sqrMagnitude;
                var sqrDistanceZ = (upperLeft - bottomLeft).sqrMagnitude;
                var sqrDistanceProduct = sqrDistanceX * sqrDistanceZ;

                // if this is a smaller box than any we've found before, it's our new candidate
                if (sqrDistanceProduct < bestOrientedBoundingBoxArea)
                {
                    bestOrientedBoundingBoxArea = sqrDistanceProduct;

                    // fill out the array of bounding box vertex positions
                    // The variables not preceded by "box" refer to the "calipers" that we rotated
                    // What we assign to the "box" variables is arbitrary as long as we maintain the same winding order as the calipers
                    var boxTopLeft = bottomLeft;
                    var boxBottomLeft = bottomRight;
                    var boxBottomRight = upperRight;
                    var boxTopRight = upperLeft;
                    boundingBox[0] = boxTopLeft;
                    boundingBox[1] = boxBottomLeft;
                    boundingBox[2] = boxBottomRight;
                    boundingBox[3] = boxTopRight;
                }
            }

            // compute the size of the 2d bounds
            var topLeft = boundingBox[0];
            var leftRightDistance = Vector3.Distance(topLeft, boundingBox[3]);
            var topBottomDistance = Vector3.Distance(topLeft, boundingBox[1]);
            return new Vector2(leftRightDistance, topBottomDistance);
        }

        static Vector3 IntersectTwoLinesXz(Vector3 startA, Vector3 directionA,
            Vector3 startB, Vector3 directionB)
        {
            var directionDiff = directionA.x * directionB.z - directionA.z * directionB.x;

            // if there's no difference in direction, the lines are parallel
            if (directionDiff == 0)
                return Vector3.zero;

            var diffX = startB.x - startA.x;
            var diffZ = startB.z - startA.z;
            var t = (diffX * directionB.z - diffZ * directionB.x) / directionDiff;

            return new Vector3(startA.x + t * directionA.x, 0f, startA.z + t * directionA.z);
        }

        /// <summary>
        /// Given a 2D bounding box's vertices, find the rotation of the box
        /// </summary>
        /// <param name="vertices">The 4 vertices of the bounding box, in the order
        /// { top left, bottom left, bottom right, top right }</param>
        /// <returns>The rotation of the box, with the horizontal side aligned to the x axis and the
        /// vertical side aligned to the z axis</returns>
        public static Quaternion RotationForBox(Vector3[] vertices)
        {
            var topLeft = vertices[0];
            var topRight = vertices[3];
            var leftToRight = topRight - topLeft;
            return Quaternion.FromToRotation(Vector3.right, leftToRight);
        }

        /// <summary>
        /// Finds the area of a convex polygon
        /// </summary>
        /// <param name="vertices">The vertices that make up the bounds of the polygon.
        /// These must be convex but can be in either winding order.</param>
        /// <returns>The area of the polygon</returns>
        public static float ConvexPolygonArea(List<Vector3> vertices)
        {
            var count = vertices.Count;
            if (count < 3)
                return 0f;

            var firstVertex = vertices[0];
            var lastIndex = count - 1;
            var lastVertex = vertices[lastIndex];
            var area = lastVertex.x * firstVertex.z - firstVertex.x * lastVertex.z;
            for (var i = 0; i < lastIndex; i++)
            {
                var currentVertex = vertices[i];
                var nextVertex = vertices[i + 1];
                area += currentVertex.x * nextVertex.z - nextVertex.x * currentVertex.z;
            }

            // Take absolute value because area is negative if vertices are clockwise
            return Math.Abs(area * 0.5f);
        }

        /// <summary>
        /// Determines if one polygon lies completely inside a coplanar polygon
        /// </summary>
        /// <param name="polygonA">The polygon to test for lying inside <paramref name="polygonB"/></param>
        /// <param name="polygonB">The polygon to test for containing <paramref name="polygonA"/>.
        /// Must be convex and coplanar with <paramref name="polygonA"/></param>
        /// <returns>True if <paramref name="polygonA"/> lies completely inside <paramref name="polygonB"/>, false otherwise</returns>
        public static bool PolygonInPolygon(List<Vector3> polygonA, List<Vector3> polygonB)
        {
            if (polygonA.Count < 1)
                return false;

            foreach (var vertex in polygonA)
            {
                if (!PointInPolygon3D(vertex, polygonB))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines if two convex coplanar polygons are within a certain distance from each other.
        /// This includes the polygon perimeters as well as their interiors.
        /// </summary>
        /// <param name="polygonA">The first polygon to test. Must be convex and coplanar with <paramref name="polygonB"/></param>
        /// <param name="polygonB">The second polygon to test. Must be convex and coplanar with <paramref name="polygonA"/></param>
        /// <param name="maxDistance">The maximum distance allowed between the two polygons</param>
        /// <returns>True if the polygons are within the specified distance from each other, false otherwise</returns>
        public static bool PolygonsWithinRange(List<Vector3> polygonA, List<Vector3> polygonB, float maxDistance)
        {
            return PolygonsWithinSqRange(polygonA, polygonB, maxDistance * maxDistance);
        }

        /// <summary>
        /// Determines if two convex coplanar polygons are within a certain distance from each other.
        /// This includes the polygon perimeters as well as their interiors.
        /// </summary>
        /// <param name="polygonA">The first polygon to test. Must be convex and coplanar with <paramref name="polygonB"/></param>
        /// <param name="polygonB">The second polygon to test. Must be convex and coplanar with <paramref name="polygonA"/></param>
        /// <param name="maxSqDistance">The square of the maximum distance allowed between the two polygons</param>
        /// <returns>True if the polygons are within the specified distance from each other, false otherwise</returns>
        public static bool PolygonsWithinSqRange(List<Vector3> polygonA, List<Vector3> polygonB, float maxSqDistance)
        {
            Vector3 pointA, pointB;
            ClosestPolygonApproach(polygonA, polygonB, out pointA, out pointB);
            return Vector3.SqrMagnitude(pointB - pointA) <= maxSqDistance ||
                PolygonInPolygon(polygonA, polygonB) || PolygonInPolygon(polygonB, polygonA);
        }
    }
}
