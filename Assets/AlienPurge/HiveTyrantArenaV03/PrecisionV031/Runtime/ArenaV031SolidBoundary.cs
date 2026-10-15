using System;
using System.Collections.Generic;
using UnityEngine;
using AlienPurge.ArenaArtV03;

namespace AlienPurge.ArenaArtV031
{
    // Inherits the V03 footprint data for compatibility with its validator.
    // Only the inner footprint is authored. The exterior is one solid C-shaped
    // region, with the south entry open; no duplicated ribbon edge to hand-edit.
    [RequireComponent(typeof(PolygonCollider2D))]
    public class ArenaV031SolidBoundary : ArenaV03Boundary
    {
        public Vector2 canvasHalfSize = new Vector2(36f, 24f);
        public float outsideMargin = 1.125f;

        public Vector2[] SolidPath()
        {
            if (footprint == null || footprint.Length < 3) throw new InvalidOperationException("边界轮廓点不足。");
            List<Vector2> result = new List<Vector2>(footprint);
            Vector2 extent = canvasHalfSize + Vector2.one * outsideMargin;
            result.Add(new Vector2(extent.x, -extent.y));
            result.Add(new Vector2(extent.x, extent.y));
            result.Add(new Vector2(-extent.x, extent.y));
            result.Add(new Vector2(-extent.x, -extent.y));
            return result.ToArray();
        }

        public void RebuildSolid()
        {
            Vector2[] path = SolidPath();
            if (!IsSimple(path)) throw new InvalidOperationException("轮廓产生交叉，保留原 Collider；请移动单条脚点线的控制点。");
            PolygonCollider2D collider = GetComponent<PolygonCollider2D>();
            collider.isTrigger = false;
            collider.pathCount = 1;
            collider.SetPath(0, path);
        }

        public static bool IsSimple(Vector2[] p)
        {
            for (int i = 0; i < p.Length; i++)
            {
                if (float.IsNaN(p[i].x) || float.IsNaN(p[i].y) || float.IsInfinity(p[i].x) || float.IsInfinity(p[i].y)) return false;
                Vector2 a = p[i], b = p[(i + 1) % p.Length];
                if ((a-b).sqrMagnitude < .0000001f) return false;
                for (int j = i + 1; j < p.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == p.Length - 1)) continue;
                    Vector2 c = p[j], d = p[(j+1)%p.Length];
                    if (SegmentsMeet(a,b,c,d)) return false;
                }
            }
            return true;
        }
        static float Cross(Vector2 a,Vector2 b) { return a.x*b.y-a.y*b.x; }
        static bool OnSegment(Vector2 a,Vector2 b,Vector2 p)
        {
            return Mathf.Abs(Cross(b-a,p-a)) < .000001f &&
                p.x >= Mathf.Min(a.x,b.x)-.000001f && p.x <= Mathf.Max(a.x,b.x)+.000001f &&
                p.y >= Mathf.Min(a.y,b.y)-.000001f && p.y <= Mathf.Max(a.y,b.y)+.000001f;
        }
        static bool SegmentsMeet(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float abC=Cross(b-a,c-a),abD=Cross(b-a,d-a),cdA=Cross(d-c,a-c),cdB=Cross(d-c,b-c);
            return (abC*abD < 0 && cdA*cdB < 0) || OnSegment(a,b,c) || OnSegment(a,b,d) || OnSegment(c,d,a) || OnSegment(c,d,b);
        }
    }
}
