using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Reads a temporary copy of the source PNG. Does not edit the PNG or its importer.
// Connected opaque arm pixels exclude disconnected rear feet/arms accidentally inside a hull.
public static class HiveTyrantSkinAuthoring
{
    private const int Size = 1024, Grid = 96;
    public static void Bake(HiveTyrantDirectionalRig rig)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(rig.source)));
            if (texture.width != Size || texture.height != Size) throw new InvalidOperationException("Pose master canvas changed");
            var pixels = texture.GetPixels32();
            var scythe = Component(pixels, rig.scytheRegion, rig.scytheJoint, rig.scytheTip);
            var fist = Component(pixels, rig.fistRegion, rig.fistJoint, rig.fistTip);
            rig.skinCells = new byte[Grid * Grid];
            for (int cy = 0; cy < Grid; cy++) for (int cx = 0; cx < Grid; cx++)
            {
                int a = 0, b = 0, other = 0;
                for (int y = cy * Size / Grid; y <= Mathf.Min(Size - 1, (cy + 1) * Size / Grid); y++)
                    for (int x = cx * Size / Grid; x <= Mathf.Min(Size - 1, (cx + 1) * Size / Grid); x++)
                    {
                        int index = y * Size + x;
                        if (pixels[(Size - y - 1) * Size + x].a <= 8) continue;
                        if (scythe[index]) a++; else if (fist[index]) b++; else other++;
                    }
                rig.skinCells[cy * Grid + cx] = (byte)(a > other && a >= b ? 1 : b > other && b > a ? 2 : 0);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static bool[] Component(Color32[] pixels, Vector2[] hull, Vector2 joint, Vector2 tip)
    {
        var possible = new bool[Size * Size]; var selected = new bool[Size * Size];
        int seed = -1; float nearest = float.MaxValue;
        Vector2 axis = tip - joint;
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            if (pixels[(Size - y - 1) * Size + x].a <= 8) continue;
            Vector2 p = new Vector2(x, y);
            if (Vector2.Dot(p - joint, axis) / Mathf.Max(1, axis.sqrMagnitude) < -.08f || !Inside(p, hull)) continue;
            int index = y * Size + x; possible[index] = true;
            float distance = (p - tip).sqrMagnitude;
            if (distance < nearest) { nearest = distance; seed = index; }
        }
        if (seed < 0) throw new InvalidOperationException("Arm hull does not contain its source pixels");
        var queue = new Queue<int>(); queue.Enqueue(seed); selected[seed] = true;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue(); int x = current % Size, y = current / Size;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= Size || ny < 0 || ny >= Size) continue;
                int next = ny * Size + nx;
                if (!possible[next] || selected[next]) continue;
                selected[next] = true; queue.Enqueue(next);
            }
        }
        return selected;
    }
    private static bool Inside(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].y > p.y) != (polygon[j].y > p.y) && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
        return inside;
    }
}
