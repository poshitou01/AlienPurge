using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class IntegratedWorldFeedback
    {
        // Small openings at the already-authored road bends. Clip only wall overhangs,
        // keeping terrain, the main hand-edited collision polygon and all gate blockers intact.
        static Dictionary<string,Rect[]> CornerOpenings()=>new Dictionary<string,Rect[]>
        {
            ["Boss_SouthApproach"]=new[]{new Rect(4.2f,-33.4f,2.8f,2.8f)},
            ["G2_CargoToMaintenance"]=new[]{new Rect(-158.4f,33.6f,2.8f,6),new Rect(-158.4f,-3.4f,2.8f,6),new Rect(-154.5f,-3.4f,4.5f,2.8f),new Rect(-148.97812f,-6.8f,2.8f,6.2f)},
            ["G4_CommonsToResearch"]=new[]{new Rect(-58.4f,43.4f,2.8f,6),new Rect(-61.2f,43.4125f,4.2f,2.8f),new Rect(-58.4f,62.9f,2.8f,5.2f),new Rect(-57,65.38125f,4.8f,2.8f)},
            ["G5_ResearchToApproach"]=new[]{new Rect(15.13906f,31.6f,2.8f,6.4f),new Rect(17.9f,31.6f,4.2f,2.8f),new Rect(46.6f,28.1f,2.8f,6.3f),new Rect(42.9f,31.6f,5.1f,2.8f),new Rect(46.6f,-33.4f,2.8f,6.3f),new Rect(42.9f,-33.4f,5.1f,2.8f),new Rect(-4.1f,-30,8.2f,3),new Rect(14.6f,-37,2.8f,3)},
            ["G7_ApproachToExtraction"]=new[]{new Rect(14.6f,-56.5f,2.8f,2.8f),new Rect(16,-57.88438f,3.5f,2.8f)}
        };
        static void RepairRoads(RegionLoopController loop,List<string> lines)
        {
            var roads=loop.world.transform.Find("Connected_Roads");var openings=CornerOpenings();
            int environment=LayerMask.NameToLayer("Environment");int mask=1<<environment;
            var material=loop.world.transform.Find("Collision_Footprints").GetComponent<Collider2D>().sharedMaterial;
            string meshFolder=IntegratedWorldInstaller.Data+"/WallMeshes";Directory.CreateDirectory(meshFolder);AssetDatabase.Refresh();
            int added=0,covered=0,trimmed=0,removed=0;
            foreach(Transform road in roads)
            foreach(var wall in road.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Wall_H"||t.name=="Wall_V").ToArray())
            {
                if(road.name=="G3_MaintenanceToCommons"&&wall.name=="Wall_H"&&Mathf.Abs(wall.localPosition.x+100.79297f)<.01f)
                {var shift=wall.localPosition;shift.x-=1.4f;wall.localPosition=shift;lines.Add("Shifted G3 freestanding wall 1.4 units left to retain a navigable right-hand aisle.");}
                var p=loop.world.Local(wall.position);bool horizontal=wall.name=="Wall_H";
                string objectName=IntegratedFeedbackAudit.PathOf(wall);var filter=wall.GetComponentInChildren<MeshFilter>();
                Require(filter!=null&&filter.sharedMesh.vertexCount==4,"Unexpected authored road mesh: "+objectName);
                // The reported extraction entrance wall is an ordinary art piece, never G7.
                if(road.name=="G5_ResearchToApproach"&&horizontal&&Vector2.Distance(p,new Vector2(16.61538f,-35.5f))<.02f)
                {lines.Add("REMOVED extraction entrance wall: "+objectName+" at "+p);Object.DestroyImmediate(wall.gameObject);removed++;continue;}
                var bounds=filter.sharedMesh.bounds;float originalMin=horizontal?bounds.min.x:bounds.min.y,originalMax=horizontal?bounds.max.x:bounds.max.y;
                float axis=horizontal?p.x:p.y;float low=originalMin+axis,high=originalMax+axis;
                if(openings.TryGetValue(road.name,out var cuts))foreach(var cut in cuts)
                {
                    float transverse=horizontal?p.y:p.x;
                    if(transverse<=(horizontal?cut.yMin:cut.xMin)+.01f||transverse>=(horizontal?cut.yMax:cut.xMax)-.01f)continue;
                    float a=Mathf.Max(low,horizontal?cut.xMin:cut.yMin),b=Mathf.Min(high,horizontal?cut.xMax:cut.yMax);
                    if(b<=a)continue;
                    if(a-low>=high-b)high=a;else low=b;
                }
                if(high-low<.65f)
                {lines.Add("REMOVED overlapping corner cap: "+objectName+" at "+p);Object.DestroyImmediate(wall.gameObject);removed++;continue;}
                low-=axis;high-=axis;
                if(Mathf.Abs(low-originalMin)>.001f||Mathf.Abs(high-originalMax)>.001f)
                {
                    var mesh=Object.Instantiate(filter.sharedMesh);mesh.name=road.name+"_"+wall.name+"_"+p.x.ToString("F5")+"_"+p.y.ToString("F5");
                    var vertices=mesh.vertices;var uv=mesh.uv;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        float value=horizontal?vertices[i].x:vertices[i].y;float next=Mathf.Clamp(value,low,high);
                        if(horizontal){vertices[i].x=next;uv[i].x=Mathf.InverseLerp(originalMin,originalMax,next);}
                        else {vertices[i].y=next;uv[i].y=Mathf.InverseLerp(originalMin,originalMax,next);}
                    }
                    mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateBounds();
                    string path=meshFolder+"/"+mesh.name+".asset";
                    Require(!File.Exists(path),"An existing wall crop needs inspection: "+path);
                    AssetDatabase.CreateAsset(mesh,path);filter.sharedMesh=mesh;EditorUtility.SetDirty(filter);
                    lines.Add("CROPPED visual and footprint to keep existing bend open: "+objectName+" local interval="+low+".."+high);trimmed++;
                }
                Vector2 center=horizontal?new Vector2((low+high)/2,.02f):new Vector2(0,(low+high)/2);
                Vector2 size=horizontal?new Vector2(high-low,.4f):new Vector2(.46f,high-low);
                var existing=wall.Find("IntegratedWallFootprint")?.GetComponent<BoxCollider2D>();
                if(existing!=null)
                {existing.offset=center;existing.size=size;lines.Add("Updated existing footprint to the trimmed visual: "+objectName);continue;}
                Physics2D.SyncTransforms();bool full=true;
                for(int i=0;i<9;i++)for(int j=-1;j<=1;j++)
                {
                    var sample=center+(horizontal?new Vector2(Mathf.Lerp(-size.x/2+.03f,size.x/2-.03f,i/8f),j*size.y*.45f):new Vector2(j*size.x*.45f,Mathf.Lerp(-size.y/2+.03f,size.y/2-.03f,i/8f)));
                    if(!Physics2D.OverlapPointAll(wall.TransformPoint(sample),mask).Any(c=>c.enabled&&!c.isTrigger))full=false;
                }
                if(full){covered++;continue;}
                var child=new GameObject("IntegratedWallFootprint");child.transform.SetParent(wall,false);child.layer=environment;
                var collider=child.AddComponent<BoxCollider2D>();collider.offset=center;collider.size=size;collider.sharedMaterial=material;
                lines.Add("ADDED solid wall footprint: "+objectName+" center="+center+" size="+size);added++;
            }
            Physics2D.SyncTransforms();
            lines.Add("Road totals: added footprints="+added+", already covered="+covered+", trimmed corner overhangs="+trimmed+", removed caps="+removed+". Accepted Arena collision and all seven progression gates preserved.");
        }
    }
}
