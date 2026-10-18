using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static class IntegratedFeedbackAudit
    {
        public const string Folder="Docs/Integration/WorldFeedback";
        public static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name+"["+t.GetSiblingIndex()+"]";
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var scene=EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
            var loop=HiveTyrantCombatSetup.SceneComponents<RegionLoopController>(scene).Single();
            var world=loop.world;var roads=world.transform.Find("Connected_Roads");
            Physics2D.SyncTransforms();
            var lines=new List<string>{"World "+world.transform.position+" scale "+world.transform.lossyScale};
            foreach(Transform road in roads)
            {
                lines.Add("ROAD: "+PathOf(road)+" active="+road.gameObject.activeInHierarchy+" descendants="+road.GetComponentsInChildren<Transform>(true).Length);
                var walls=road.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Wall_")||r.transform.parent.name.StartsWith("Wall_")).ToArray();
                if(walls.Length==0)continue;
                var bounds=walls[0].bounds;
                foreach(var wall in walls)
                {
                    bounds.Encapsulate(wall.bounds);
                    var p=(Vector2)wall.transform.position;
                    lines.Add(PathOf(wall.transform)+" | local="+world.Local(p)+" | colliders="+wall.GetComponentsInChildren<Collider2D>().Length+" | foot overlaps="+string.Join(",",Physics2D.OverlapPointAll(p,1<<LayerMask.NameToLayer("Environment")).Select(c=>c.name)));
                }
                Render(bounds.center,Mathf.Max(bounds.extents.y+3,(bounds.extents.x+3)/1.5f),Folder+"/Before_"+road.name+".png",1500,1000);
            }
            foreach(var d in loop.definitions)
                lines.Add(d.regionId+" cap="+d.aliveLimit+" special="+d.specialLimit+" ambient="+d.v02.ambientTarget+" profiles="+string.Join(";",d.enemies.Select(p=>p.type+":"+p.simultaneousLimit+"/"+p.globalSimultaneousLimit))+" residents="+string.Join(";",d.residents.Select(g=>g.type+":"+g.count+" guard="+g.keyGuard)));
            File.WriteAllLines(Folder+"/BeforeWorld.txt",lines);
            scene=EditorSceneManager.OpenScene(IntegratedWorldInstaller.Menu);lines.Clear();
            foreach(var t in HiveTyrantCombatSetup.SceneComponents<RectTransform>(scene))
            {
                var img=t.GetComponent<Image>();var txt=t.GetComponent<TMP_Text>();var b=t.GetComponent<Button>();
                lines.Add(PathOf(t)+" active="+t.gameObject.activeSelf+" pos="+t.anchoredPosition+" size="+t.sizeDelta+" anchor="+t.anchorMin+"/"+t.anchorMax+
                    (img==null?"":" sprite="+AssetDatabase.GetAssetPath(img.sprite)+" color="+img.color)+
                    (txt==null?"":" text="+txt.text+" font="+AssetDatabase.GetAssetPath(txt.font)+" size="+txt.fontSize+" color="+txt.color)+
                    (b==null?"":" click="+string.Join(",",Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>b.onClick.GetPersistentMethodName(i)))));
            }
            File.WriteAllLines(Folder+"/BeforeMenu.txt",lines);
            EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
        }
        public static void Render(Vector3 center,float halfHeight,string path,int width=1500,int height=1000)
        {
            var obj=new GameObject("Feedback audit camera");var camera=obj.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=halfHeight;camera.transform.position=new Vector3(center.x,center.y,-100);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.055f);camera.farClipPlane=200;
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
            File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
            Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(obj);
        }
    }
}
