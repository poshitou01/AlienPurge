using System;
using UnityEngine;

namespace AlienPurge.Level01ConnectedV01
{
    [DefaultExecutionOrder(-18000),DisallowMultipleComponent]
    public sealed class L01World : MonoBehaviour
    {
        public TextAsset layoutAsset;
        public GameObject player,boss;
        public MonoBehaviour existingArena;
        public Transform footAnchor;
        public L01Progression progression;
        public L01ArenaBridge arenaBridge;
        public L01Navigation navigation;
        public Transform[] missionPoints,safeZoneAnchors;
        public Transform[] highRiskAnchors;
        public string sourceArenaScene,sourceGameplayScene;
        [NonSerialized] public L01Layout layout;
        public L01Layout Data
        {
            get
            {
                if(layout==null && layoutAsset!=null)layout=JsonUtility.FromJson<L01Layout>(layoutAsset.text);
                return layout;
            }
        }
        void Awake()
        {
            if(Data==null){Debug.LogError("Level01 layout is missing.",this);enabled=false;return;}
            if(player!=null && footAnchor==null)footAnchor=L01ProjectAccess.Feet(player);
            MoveMissionPoints("CargoYard");
        }
        public Vector3 World(Vector2 p){return transform.TransformPoint(p);}
        public Vector2 Local(Vector3 p){return transform.InverseTransformPoint(p);}
        public Vector3 PlayerFeet {get{return footAnchor!=null?footAnchor.position:player!=null?player.transform.position:transform.position;}}
        public L01Region Region(string id)
        {
            if(Data==null)return null;
            foreach(L01Region r in Data.regions)if(r.id==id)return r;return null;
        }
        public string ResolveRegion(Vector2 localPoint)
        {
            if(Data==null)return null;
            foreach(L01Region r in Data.regions)
                foreach(L01Polygon polygon in r.walk)
                    if(Contains(polygon.points,localPoint))return r.id;
            if(localPoint.x>-36 && localPoint.x<36 && localPoint.y>-24 && localPoint.y<24)return "HiveTyrant";
            return null;
        }
        public void MoveMissionPoints(string regionId)
        {
            L01Region r=Region(regionId);if(r==null)return;
            Place(missionPoints,r.missionPoints);Place(safeZoneAnchors,r.safePoints);
            // Existing ExtractionController still owns the one timed High-Risk Cache.
            Place(highRiskAnchors,r.missionPoints);
        }
        void Place(Transform[] targets,L01Point[] points)
        {
            if(targets==null||points==null||points.Length==0)return;
            for(int i=0;i<targets.Length;i++)if(targets[i]!=null)targets[i].position=World(points[i%points.Length].V);
        }
        static bool Contains(L01Point[] p,Vector2 q)
        {
            bool inside=false;
            for(int i=0,j=p.Length-1;i<p.Length;j=i++)
                if(((p[i].y>q.y)!=(p[j].y>q.y)) &&
                    q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;
            return inside;
        }
    }
}
