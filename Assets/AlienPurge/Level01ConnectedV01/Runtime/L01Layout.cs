using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [Serializable] public class L01Point
    {
        public float x, y;
        public Vector2 V { get { return new Vector2(x, y); } }
    }
    [Serializable] public class L01Polygon { public L01Point[] points; }
    [Serializable] public class L01Occluder
    {
        public string name;
        public L01Point[] points;
        public float baselineY;
        public bool alwaysForeground;
    }
    [Serializable] public class L01Region
    {
        public string id, label, texture;
        public L01Point center;
        public float width, height;
        public int pixelWidth, pixelHeight;
        public L01Polygon[] walk;
        public L01Occluder[] occluders;
        public L01Point[] missionPoints, safePoints;
    }
    [Serializable] public class L01Road
    {
        public string id;
        public float width;
        public L01Point[] centerline;
        public L01Polygon[] renderPolygons;
        public L01Point[] wallH, wallV;
    }
    [Serializable] public class L01Threshold {public L01Point center,size;}
    [Serializable] public class L01GateSpec
    {
        public string id, from, to, requirement;
        public L01Point center;
        public bool vertical;
        public float opening;
    }
    [Serializable] public class L01LootSpot
    {
        public string region;
        public int kind;
        public L01Point position;
    }
    [Serializable] public class L01TerminalSpec
    {
        public string id, label, prompt, region, requiredFlag, completesFlag;
        public float duration;
        public L01Point position;
    }
    [Serializable] public class L01Layout
    {
        public int schemaVersion;
        public float referenceArenaWidth;
        public L01Point boundsMin, boundsMax, playerStart, extractionPoint;
        public L01Region[] regions;
        public L01Road[] roads;
        public L01Threshold[] thresholds;
        public L01GateSpec[] gates;
        public L01LootSpot[] loot;
        public L01TerminalSpec[] terminals;
        public L01Polygon[] solidPaths, spawnPaths, collisionOutlines;
        public L01Point[] fogRoute;
    }

    public static class L01Geometry
    {
        public static Vector2 Pixel(L01Region r, L01Point p)
        {
            return new Vector2((p.x / r.pixelWidth - .5f) * r.width,
                (.5f - p.y / r.pixelHeight) * r.height);
        }

        public static Mesh Quad(float width, float height, Vector2 pivot)
        {
            float left = -pivot.x * width, bottom = -pivot.y * height;
            Mesh mesh = new Mesh { name = "L01_Quad" };
            mesh.vertices = new[] { new Vector3(left,bottom), new Vector3(left+width,bottom),
                new Vector3(left+width,bottom+height), new Vector3(left,bottom+height) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,2,1,0,3,2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh PixelPolygon(L01Region r, L01Point[] points, string name)
        {
            List<Vector2> local = new List<Vector2>();
            List<Vector2> uv = new List<Vector2>();
            foreach (L01Point p in points)
            {
                local.Add(Pixel(r,p));
                uv.Add(new Vector2(p.x/r.pixelWidth,1f-p.y/r.pixelHeight));
            }
            return Polygon(local.ToArray(),uv.ToArray(),name);
        }

        public static Mesh Polygon(Vector2[] points, Vector2[] uv, string name)
        {
            Mesh mesh = new Mesh { name = name };
            Vector3[] vertices = new Vector3[points.Length];
            for (int i=0;i<points.Length;i++) vertices[i]=points[i];
            mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=Triangulate(points);
            mesh.RecalculateBounds(); return mesh;
        }

        public static int[] Triangulate(Vector2[] p)
        {
            if(p==null || p.Length<3)throw new ArgumentException("Polygon requires three points.");
            List<int> order=new List<int>();
            float area=0;
            for(int i=0;i<p.Length;i++)area+=Cross(p[i],p[(i+1)%p.Length]);
            if(area>0)for(int i=0;i<p.Length;i++)order.Add(i);
            else for(int i=p.Length-1;i>=0;i--)order.Add(i);
            List<int> triangles=new List<int>(); int safety=p.Length*p.Length;
            while(order.Count>3 && safety-->0)
            {
                bool found=false;
                for(int i=0;i<order.Count;i++)
                {
                    int a=order[(i+order.Count-1)%order.Count],b=order[i],c=order[(i+1)%order.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])<=.0000001f)continue;
                    bool occupied=false;
                    foreach(int j in order)
                    {
                        if(j==a||j==b||j==c)continue;
                        if(InTriangle(p[j],p[a],p[b],p[c])){occupied=true;break;}
                    }
                    if(occupied)continue;
                    triangles.Add(a);triangles.Add(c);triangles.Add(b);order.RemoveAt(i);found=true;break;
                }
                if(!found)throw new InvalidOperationException("Polygon is self-intersecting or degenerate: "+p.Length);
            }
            if(order.Count==3){triangles.Add(order[0]);triangles.Add(order[2]);triangles.Add(order[1]);}
            if(triangles.Count!=(p.Length-2)*3)throw new InvalidOperationException("Incomplete polygon triangulation.");
            return triangles.ToArray();
        }
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {
            const float e=.0000001f;
            return Cross(b-a,p-a)>=-e && Cross(c-b,p-b)>=-e && Cross(a-c,p-c)>=-e;
        }
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        public static Vector2[] Points(L01Point[] points)
        {
            Vector2[] v=new Vector2[points.Length];for(int i=0;i<v.Length;i++)v[i]=points[i].V;return v;
        }
    }
}
