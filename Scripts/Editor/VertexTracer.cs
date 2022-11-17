#if UNITY_EDITOR

using Unity.Mathematics;
using UnityEngine;

namespace AlpacaIT.VertexTracer
{
    public static class VertexTracer
    {
        private static int traces = 0;
        private static float tracingTime = 0f;

        private static VertexPointLight[] pointLights;
        private static VertexAntiLight[] shadowLights;

        private const int lightmapSize = 2048;
        private const float lightmapSizeMin1 = lightmapSize - 1;

        [UnityEditor.MenuItem("Vertex Tracer/Trace")]
        public static void Go()
        {
            tracingTime = 0f;
            traces = 0;

            pointLights = Object.FindObjectsOfType<VertexPointLight>();
            AssignPointLightChannels();

            shadowLights = Object.FindObjectsOfType<VertexAntiLight>();
            var meshFilters = Object.FindObjectsOfType<MeshFilter>();
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.gameObject.isStatic)
                {
                    Raytrace(meshFilter);
                }
            }

            Debug.Log("Raytracing Finished: " + traces + " traces in " + tracingTime + "s!");
        }

        private static void AssignPointLightChannels()
        {
            // first reset all the channels to an invalid value.
            for (int i = 0; i < pointLights.Length; i++)
            {
                var light = pointLights[i];
                light.lightChannel = 255;
            }

            uint channel = 0;
            for (int i = 0; i < pointLights.Length; i++)
            {
                // stupid fixme
                if (channel >= 32) channel = 0;
                var light = pointLights[i];
                light.lightChannel = channel;
                channel++;
            }
        }

        private static void Raytrace(MeshFilter meshFilter)
        {
            MeshBuilder meshBuilder = new MeshBuilder(meshFilter.transform.localToWorldMatrix, meshFilter.sharedMesh);

            var tt1 = Time.realtimeSinceStartup;
            var pixels = new uint[lightmapSize * lightmapSize];
            {
                var vertices = meshBuilder.worldVertices;
                var uv1 = meshBuilder.meshUv1;
                var triangles = meshBuilder.meshTriangles;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var v1 = vertices[triangles[i]];
                    var v2 = vertices[triangles[i + 1]];
                    var v3 = vertices[triangles[i + 2]];

                    var t1 = uv1[triangles[i]];
                    var t2 = uv1[triangles[i + 1]];
                    var t3 = uv1[triangles[i + 2]];

                    RaycastTriangle(ref pixels, v1, v2, v3, t1, t2, t3);
                }
            }
            tracingTime += Time.realtimeSinceStartup - tt1;

            var renderer = meshFilter.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial);
            renderer.sharedMaterial.name = "Vertex Tracer Material";

            Lightmap lightmap;
            if (!renderer.TryGetComponent<Lightmap>(out lightmap))
                lightmap = renderer.gameObject.AddComponent<Lightmap>();
            lightmap.resolution = lightmapSize;
            lightmap.pixels = pixels;
        }

        private static void SetPixel(ref uint[] pixels, int x, int y, uint color)
        {
            if (x < 0 || y < 0 || x >= lightmapSize || y >= lightmapSize) return;
            pixels[y * lightmapSize + x] = color;
        }

        private static Vector3 UvTo3d(Vector2 uv, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 t1, Vector2 t2, Vector2 t3)
        {
            // calculate triangle area - if zero, skip it
            var a = Area(t1, t2, t3); if (a == 0f) return Vector3.zero;

            // calculate barycentric coordinates of u1, u2 and u3
            // if anyone is negative, point is outside the triangle: skip it
            var a1 = Area(t2, t3, uv) / a; if (a1 < 0f) return Vector3.zero;
            var a2 = Area(t3, t1, uv) / a; if (a2 < 0f) return Vector3.zero;
            var a3 = Area(t1, t2, uv) / a; if (a3 < 0f) return Vector3.zero;

            // point inside the triangle - find mesh position by interpolation...
            return a1 * v1 + a2 * v2 + a3 * v3;
        }

        private static void RaycastTriangle(ref uint[] pixels, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 t1, Vector2 t2, Vector2 t3)
        {
            // skip degenerate triangles.
            Vector3 normal = new Plane(v1, v2, v3).normal;
            if (normal.Equals(Vector3.zero)) { return; };

            //Vector3 center = (v1 + v2 + v3) / 3f;
            //v1 -= (center - v1).normalized * ((1.0f / lightmapSizeMin1) * 4f);
            //v2 -= (center - v2).normalized * ((1.0f / lightmapSizeMin1) * 4f);
            //v3 -= (center - v3).normalized * ((1.0f / lightmapSizeMin1) * 4f);

            Vector2 center2 = (t1 + t2 + t3) / 3f;
            t1 -= (center2 - t1).normalized * ((1.0f / lightmapSizeMin1) * 4f);
            t2 -= (center2 - t2).normalized * ((1.0f / lightmapSizeMin1) * 4f);
            t3 -= (center2 - t3).normalized * ((1.0f / lightmapSizeMin1) * 4f);

            // calculate the bounding box of the polygon in UV space.
            // we only have to raycast these pixels and can skip the rest.
            var triangleBoundingBox = ComputeTriangleBoundingBox(t1, t2, t3);
            var minX = Mathf.FloorToInt(triangleBoundingBox.xMin * lightmapSizeMin1);
            var minY = Mathf.FloorToInt(triangleBoundingBox.yMin * lightmapSizeMin1);
            var maxX = Mathf.CeilToInt(triangleBoundingBox.xMax * lightmapSizeMin1);
            var maxY = Mathf.CeilToInt(triangleBoundingBox.yMax * lightmapSizeMin1);

            for (int x = minX; x < maxX; x++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    float xx = x / lightmapSizeMin1;
                    float yy = y / lightmapSizeMin1;

                    var world = UvTo3d(new Vector2(xx, yy), v1, v2, v3, t1, t2, t3);
                    if (world.Equals(Vector3.zero)) continue;

                    uint px = 0;
                    for (int i = 0; i < pointLights.Length; i++)
                    {
                        var pointLight = pointLights[i];
                        px |= Raycast(pointLight, world, normal);
                    }

                    SetPixel(ref pixels, x, y, px);
                }
            }
        }

        private static uint Raycast(VertexPointLight pointLight, Vector3 world, Vector3 normal)
        {
            var radius = pointLight.lightRadius;
            if (radius == 0.0f) return 0; // early out by radius.

            var position = pointLight.transform.position;
            float distance = Vector3.Distance(world, position);
            if (distance > radius) return 0; // early out by distance.

            var direction = (position - world).normalized;
            if (math.dot(normal, direction) < 0f) return 0; // early out by normal.

            // trace from the light to the world position and check whether we hit close to it.
            traces++;
            if (Physics.Raycast(position, -direction, out var hit, radius))
                if (Vector3.Distance(hit.point, world) < 0.01f)
                    return (uint)1 << ((int)pointLight.lightChannel);

            return 0;
        }

        // calculate signed triangle area using a kind of "2D cross product":
        public static float Area(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var v1 = p1 - p3;
            var v2 = p2 - p3;
            return (v1.x * v2.y - v1.y * v2.x) / 2f;
        }

        public static Rect ComputeTriangleBoundingBox(Vector2 a, Vector2 b, Vector2 c)
        {
            float sx1 = a.x;
            float sx2 = b.x;
            float sx3 = c.x;
            float sy1 = a.y;
            float sy2 = b.y;
            float sy3 = c.y;

            float xmax = sx1 > sx2 ? (sx1 > sx3 ? sx1 : sx3) : (sx2 > sx3 ? sx2 : sx3);
            float ymax = sy1 > sy2 ? (sy1 > sy3 ? sy1 : sy3) : (sy2 > sy3 ? sy2 : sy3);
            float xmin = sx1 < sx2 ? (sx1 < sx3 ? sx1 : sx3) : (sx2 < sx3 ? sx2 : sx3);
            float ymin = sy1 < sy2 ? (sy1 < sy3 ? sy1 : sy3) : (sy2 < sy3 ? sy2 : sy3);

            return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        }
    }
}

#endif