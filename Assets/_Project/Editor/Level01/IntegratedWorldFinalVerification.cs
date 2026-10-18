using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using AlienPurge.Level01ConnectedV01.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static class IntegratedWorldFinalVerification
    {
        public static void Run()
        {
            var lines=new List<string>();
            IntegratedWorldInstaller.Install();
            var before=File.ReadAllBytes(IntegratedWorldInstaller.Target);
            IntegratedWorldInstaller.Install();
            Require(before.SequenceEqual(File.ReadAllBytes(IntegratedWorldInstaller.Target)),"Repeated installation retains identical integrated scene bytes",lines);
            var scene=EditorSceneManager.GetActiveScene();
            var marker=new GameObject("UnsavedInstallerSafetyFixture");EditorSceneManager.MarkSceneDirty(scene);
            bool refused=false;
            try{IntegratedWorldInstaller.Install();}catch(InvalidOperationException e){refused=e.Message.Contains("Save your scene edits");}
            Require(refused&&marker!=null&&EditorSceneManager.GetActiveScene()==scene,"Installer refuses to discard unsaved scene changes",lines);
            Object.DestroyImmediate(marker);EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
            var loop=Object.FindObjectOfType<RegionLoopController>();
            var installed=Snapshot(loop.world.existingArena.gameObject);
            var arena=EditorSceneManager.OpenScene("Assets/_Project/Scenes/HiveTyrant_Arena.unity");
            var source=L01ArenaImport.FindRoot(arena);
            if(source==null)throw new InvalidOperationException("Cannot identify accepted Arena root.");
            var accepted=Snapshot(source);
            var missing=accepted.Except(installed).ToArray();var extra=installed.Except(accepted).ToArray();
            File.WriteAllLines(IntegratedWorldInstaller.Reports+"/ArenaGeometryDifferences.txt",new[]{"Accepted entries="+accepted.Count,"Integrated entries="+installed.Count,"Missing/changed from accepted:"}.Concat(missing).Concat(new[]{"Extra/changed in integrated:"}).Concat(extra));
            EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
            Require(missing.Length==0&&extra.Length==0,"Accepted Arena relative layout, collision shapes and occlusion sorting match the integrated copy",lines);
            IntegratedWorldInstaller.CheckProtectedScenes();
            File.WriteAllLines(IntegratedWorldInstaller.Reports+"/InstallerAndArena.txt",lines);
            RegionLoopV02Regression.RunIntegratedLifecycle();
        }
        static void Require(bool condition,string text,List<string> lines)
        {if(!condition)throw new InvalidOperationException(text);lines.Add("PASS: "+text);}
        static List<string> Snapshot(GameObject root)
        {
            var lines=new List<string>();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                string path=AnimationUtility.CalculateTransformPath(t,root.transform);
                // Root placement belongs to the connected world; all authored relative placement remains exact.
                if(t!=root.transform)lines.Add(path+"|TRS|"+t.localPosition.ToString("R")+"|"+t.localRotation.ToString("R")+"|"+t.localScale.ToString("R"));
                foreach(var c in t.GetComponents<Collider2D>())
                {
                    string shape=c is BoxCollider2D b?b.size.ToString("R")+"/"+b.edgeRadius:
                        c is CircleCollider2D circle?circle.radius.ToString("R"):
                        c is PolygonCollider2D poly?string.Join("/",Enumerable.Range(0,poly.pathCount).Select(i=>string.Join(";",poly.GetPath(i).Select(v=>v.ToString("R"))))):
                        c is EdgeCollider2D edge?string.Join(";",edge.points.Select(v=>v.ToString("R"))):"";
                    // Entry_Blocker enabled state is intentionally controlled by the existing G6 progression.
                    lines.Add(path+"|"+c.GetType().Name+"|"+(c.name=="Entry_Blocker"?"gate-owned":c.enabled.ToString())+"|"+c.isTrigger+"|"+c.offset.ToString("R")+"|"+shape+"|layer="+c.gameObject.layer);
                }
                foreach(var r in t.GetComponents<Renderer>())lines.Add(path+"|sorting|"+r.sortingLayerID+"|"+r.sortingOrder);
            }
            return lines;
        }
    }
}
