using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [Serializable]public class L01BoundaryLoop {public Vector2[] points;}
    public sealed class L01CollisionAuthoring : MonoBehaviour
    {
        public PolygonCollider2D solid;
        public PolygonCollider2D spawnArea;
        public L01BoundaryLoop[] outlines;
        public bool editPoints;
        public bool MovePoint(int loop,int index,Vector2 next)
        {
            if(solid==null||outlines==null)return false;
            Vector2 old=outlines[loop].points[index];
            // The reserved original Arena cannot be edited by this tool.
            if(Mathf.Abs(old.x)<36.05f&&Mathf.Abs(old.y)<24.05f)return false;
            List<Vector2[]> updates=new List<Vector2[]>();bool touched=false;
            for(int i=0;i<solid.pathCount;i++)
            {
                Vector2[] path=solid.GetPath(i);float before=Area(path);
                for(int j=0;j<path.Length;j++)if((path[j]-old).sqrMagnitude<.000004f){path[j]=next;touched=true;}
                if(Area(path)*before<=.00000001f)return false;
                updates.Add(path);
            }
            if(!touched)return false;
            List<Vector2[]> spawnUpdates=new List<Vector2[]>();
            if(spawnArea!=null)for(int i=0;i<spawnArea.pathCount;i++)
            {
                Vector2[] path=spawnArea.GetPath(i);float before=Area(path);
                for(int j=0;j<path.Length;j++)if((path[j]-old).sqrMagnitude<.000004f)path[j]=next;
                if(Area(path)*before<=.00000001f)return false;
                spawnUpdates.Add(path);
            }
            for(int i=0;i<updates.Count;i++)solid.SetPath(i,updates[i]);
            if(spawnArea!=null)for(int i=0;i<spawnUpdates.Count;i++)spawnArea.SetPath(i,spawnUpdates[i]);
            foreach(L01BoundaryLoop boundary in outlines)
                for(int i=0;i<boundary.points.Length;i++)if((boundary.points[i]-old).sqrMagnitude<.000004f)boundary.points[i]=next;
            Physics2D.SyncTransforms();return true;
        }
        static float Area(Vector2[] p)
        {
            float a=0;for(int i=0;i<p.Length;i++){Vector2 q=p[(i+1)%p.Length];a+=p[i].x*q.y-q.x*p[i].y;}return a;
        }
        void OnDrawGizmosSelected()
        {
            if(outlines==null)return;Gizmos.color=new Color(1,.3f,.45f,.8f);
            foreach(L01BoundaryLoop boundary in outlines)
                for(int i=0;i<boundary.points.Length;i++)Gizmos.DrawLine(transform.TransformPoint(boundary.points[i]),transform.TransformPoint(boundary.points[(i+1)%boundary.points.Length]));
        }
    }
}
