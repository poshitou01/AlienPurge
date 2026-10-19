using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Compilation;
using UnityEngine;

namespace AlienPurge.Capture.Editor
{
    public static class ReleaseAudit
    {
        [MenuItem("AlienPurge/Capture/Validate Capture Sources")]
        public static void Run()
        {
            ReleaseCapture.EnsureFolders();
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before the source audit.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save existing edits before auditing.");
            var failures=new List<string>();var lines=new List<string>();var dependencies=new HashSet<string>();int components=0,missingScripts=0;
            foreach(string path in new[]{AlienPurge.Editor.WindowsDemoBuilder.Menu,ReleaseCapture.Level})
            {
                var scene=EditorSceneManager.OpenScene(path);
                foreach(string dependency in AssetDatabase.GetDependencies(path,true))dependencies.Add(dependency);
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    foreach(var c in root.GetComponentsInChildren<Component>(true))
                    {
                        if(c==null)continue;components++;
                        using(var so=new SerializedObject(c))
                        {
                            var p=so.GetIterator();
                            while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue==null&&p.objectReferenceInstanceIDValue!=0)
                                failures.Add(path+" | "+c.name+" | "+c.GetType().Name+"."+p.propertyPath+" | missing object reference");
                        }
                    }
                }
                lines.Add("LOADED: "+path);
            }
            var guidCache=new Dictionary<string,bool>();int scanned=0;
            foreach(string path in dependencies)
            {
                string ext=Path.GetExtension(path);
                if(!File.Exists(path)||(ext!=".unity"&&ext!=".prefab"&&ext!=".asset"))continue;
                string content=File.ReadAllText(path);if(!content.StartsWith("%YAML"))continue;scanned++;
                foreach(Match match in Regex.Matches(content,@"guid: ([0-9a-f]{32})"))
                {
                    string guid=match.Groups[1].Value;if(guid.StartsWith("0000000000000000"))continue;
                    if(!guidCache.TryGetValue(guid,out bool resolved)){resolved=!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));guidCache[guid]=resolved;}
                    if(!resolved)failures.Add(path+" | unresolved GUID "+guid);
                }
            }
            var playerAssemblies=CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var included=playerAssemblies.SelectMany(a=>a.sourceFiles.Select(f=>new{assembly=a.name,file=f})).Where(x=>x.file.Replace('\\','/').Contains("/AlienPurgeCaptureTools/")).ToArray();
            foreach(var item in included)failures.Add("Editor capture script in Player compilation: "+item.file);
            lines.Add("Components inspected: "+components);lines.Add("Missing scripts: "+missingScripts);
            lines.Add("Dependency YAML files inspected: "+scanned);lines.Add("Unique non-builtin GUIDs checked: "+guidCache.Count);
            lines.Add("Own capture sources in Player compilation: "+included.Length);
            lines.Add("Recorder package may contain runtime support assemblies; no recording helper object was saved in a formal scene and no existing player build was rebuilt.");
            if(missingScripts>0)failures.Add("Missing scripts: "+missingScripts);
            lines.Add("Failures: "+failures.Count);lines.AddRange(failures.Distinct());
            File.WriteAllLines(ReleaseCapture.Root+"/10_QA/UNITY_REFERENCE_AUDIT.txt",lines);
            File.WriteAllText(ReleaseCapture.Root+"/10_QA/UNITY_REFERENCE_AUDIT_STATUS.txt",failures.Count==0?"PASS":"FAIL");
            Debug.Log("[ReleaseCapture] Reference audit: "+failures.Count+" failures; formal scenes not saved.");
        }
    }
}
