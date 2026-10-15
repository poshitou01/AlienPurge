using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.ArenaArtV03
{
    [Serializable] public struct ImagePoint
    {
        public float x, y;
        public Vector2 Value { get { return new Vector2(x, y); } }
    }
    [Serializable] public class AtlasRegion
    {
        public string name;
        public float baselineY;
        public ImagePoint[] points;
    }
    [Serializable] public class ArenaMarker { public string name; public float x, y; }
    [Serializable] public class EntryBlockerDefinition { public ImagePoint center, size; }
    [Serializable] public class ArenaV03Layout
    {
        public int schemaVersion, canvasWidth, canvasHeight;
        public float defaultWorldWidth;
        public ImagePoint[] boundaryPoints;
        public AtlasRegion[] occluders;
        public ArenaMarker[] markers;
        public AtlasRegion entryLeaf;
        public EntryBlockerDefinition entryBlocker;
    }

    public static class ArenaV03Geometry
    {
        public const float CanvasWidth = 1536f, CanvasHeight = 1024f;

        public static Vector3 LocalPoint(Vector2 pixel, float worldWidth)
        {
            float units = worldWidth / CanvasWidth;
            return new Vector3((pixel.x - CanvasWidth * .5f) * units,
                (CanvasHeight * .5f - pixel.y) * units, 0f);
        }

        // Every region samples the unchanged full atlas. No independent sprite cuts,
        // trimming, pivot offsets or different pixels-per-unit can open an art seam.
        public static Mesh AtlasPolygon(string name, ImagePoint[] points, float worldWidth)
        {
            if (points == null || points.Length < 3) throw new ArgumentException("区域轮廓至少需要三个点：" + name);
            Vector3[] vertices = new Vector3[points.Length];
            Vector2[] uv = new Vector2[points.Length];
            List<Vector2> outline = new List<Vector2>();
            for (int i = 0; i < points.Length; i++)
            {
                vertices[i] = LocalPoint(points[i].Value, worldWidth);
                uv[i] = new Vector2(points[i].x / CanvasWidth, 1f - points[i].y / CanvasHeight);
                outline.Add(new Vector2(vertices[i].x, vertices[i].y));
            }
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = Triangulate(outline);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh FullAtlas(float width)
        {
            return AtlasPolygon("ArenaV03_ContinuousPlate", new ImagePoint[] {
                new ImagePoint {x=0,y=0}, new ImagePoint {x=1536,y=0},
                new ImagePoint {x=1536,y=1024}, new ImagePoint {x=0,y=1024}}, width);
        }

        public static Mesh GroundDetail(float width, float tileWorldSize)
        {
            const int rings = 5, segments = 96;
            int count = 1 + rings * segments;
            Vector3[] v = new Vector3[count];
            Vector2[] uv = new Vector2[count], uv2 = new Vector2[count];
            Color[] colors = new Color[count];
            Vector2 center = new Vector2(768f, 491f);
            v[0] = LocalPoint(center, width);
            uv[0] = new Vector2(center.x / CanvasWidth, 1f - center.y / CanvasHeight);
            uv2[0] = (Vector2)v[0] / tileWorldSize;
            colors[0] = Color.white;
            for (int ring = 1; ring <= rings; ring++)
            {
                float r = ring / (float)rings;
                for (int s = 0; s < segments; s++)
                {
                    int i = 1 + (ring - 1) * segments + s;
                    float a = s * Mathf.PI * 2f / segments;
                    Vector2 p = center + new Vector2(Mathf.Cos(a) * 454f * r, Mathf.Sin(a) * 249f * r);
                    v[i] = LocalPoint(p, width);
                    uv[i] = new Vector2(p.x / CanvasWidth, 1f - p.y / CanvasHeight);
                    uv2[i] = (Vector2)v[i] / tileWorldSize;
                    colors[i] = new Color(1, 1, 1, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.60f, 1f, r)));
                }
            }
            List<int> t = new List<int>();
            for (int s = 0; s < segments; s++)
            {
                t.Add(0); t.Add(1 + s); t.Add(1 + (s + 1) % segments);
                for (int ring = 1; ring < rings; ring++)
                {
                    int a = 1 + (ring - 1) * segments + s;
                    int b = 1 + (ring - 1) * segments + (s + 1) % segments;
                    int c = a + segments, d = b + segments;
                    t.Add(a); t.Add(c); t.Add(d); t.Add(a); t.Add(d); t.Add(b);
                }
            }
            Mesh mesh = new Mesh { name = "ArenaV03_Ground_Microdetail" };
            mesh.vertices = v; mesh.uv = uv; mesh.uv2 = uv2; mesh.colors = colors;
            mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            return mesh;
        }

        // Ear clipping handles the concave greenhouse and vegetation contours.
        public static int[] Triangulate(IList<Vector2> p)
        {
            int n = p.Count;
            float area = 0f;
            for (int i = 0; i < n; i++) area += Cross(p[i], p[(i + 1) % n]);
            if (Mathf.Abs(area) < .000001f) throw new ArgumentException("区域面积为零。");
            List<int> remaining = new List<int>();
            for (int i = 0; i < n; i++) remaining.Add(area > 0 ? i : n - 1 - i);
            List<int> triangles = new List<int>();
            int guard = n * n;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int k = 0; k < remaining.Count; k++)
                {
                    int ia = remaining[(k + remaining.Count - 1) % remaining.Count];
                    int ib = remaining[k], ic = remaining[(k + 1) % remaining.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (Cross(b - a, c - a) <= .000001f) continue;
                    bool contains = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        int index = remaining[j];
                        if (index == ia || index == ib || index == ic) continue;
                        if (InsideTriangle(p[index], a, b, c)) { contains = true; break; }
                    }
                    if (contains) continue;
                    triangles.Add(ia); triangles.Add(ib); triangles.Add(ic);
                    remaining.RemoveAt(k); clipped = true; break;
                }
                if (!clipped) throw new ArgumentException("轮廓无法三角化，请检查重复点或交叉线。");
            }
            if (remaining.Count != 3) throw new ArgumentException("轮廓三角化未完成。");
            triangles.Add(remaining[0]); triangles.Add(remaining[1]); triangles.Add(remaining[2]);
            return triangles.ToArray();
        }

        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        static bool InsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            const float eps = .000001f;
            return Cross(b - a, p - a) >= -eps && Cross(c - b, p - b) >= -eps && Cross(a - c, p - c) >= -eps;
        }

        // A solid ribbon lies on the non-walkable side of each footprint segment.
        // It is thicker than an edge line, so the existing MovePosition Dash has a
        // useful physical wall volume. The open path leaves only the south entry.
        public static Vector2[] BoundaryRibbon(Vector2[] p, float thickness)
        {
            if (p == null || p.Length < 2) throw new ArgumentException("边界至少需要两个点。");
            Vector2[] result = new Vector2[p.Length * 2];
            for (int i = 0; i < p.Length; i++)
            {
                result[i] = p[i];
                Vector2 t0 = i > 0 ? (p[i] - p[i - 1]).normalized : (p[1] - p[0]).normalized;
                Vector2 t1 = i + 1 < p.Length ? (p[i + 1] - p[i]).normalized : t0;
                // The input wall-foot path has its walkable area on the RIGHT in world XY.
                Vector2 n0 = new Vector2(-t0.y, t0.x), n1 = new Vector2(-t1.y, t1.x);
                Vector2 miter = (n0 + n1).normalized;
                float dot = Mathf.Max(.35f, Vector2.Dot(miter, n1));
                result[result.Length - 1 - i] = p[i] + miter * thickness / dot;
            }
            return result;
        }
    }
}
