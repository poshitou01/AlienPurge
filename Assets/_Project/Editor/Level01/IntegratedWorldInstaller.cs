using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AlienPurge.Level01.RegionLoop;
using AlienPurge.Level01ConnectedV01;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static class IntegratedWorldInstaller
    {
        public const string Source = RegionLoopV02Installer.Target;
        public const string Target = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity";
        public const string Menu = "Assets/_Project/Scenes/MainMenu_IntegratedV01.unity";
        public const string Data = "Assets/_Project/Data/Level01/IntegratedV01";
        public const string Reports = "Docs/Integration";
        static T[] All<T>(Scene scene) where T : Component => HiveTyrantCombatSetup.SceneComponents<T>(scene);
        static T One<T>(Scene scene) where T : Component
        {
            var items=All<T>(scene);
            if(items.Length!=1)throw new InvalidOperationException(scene.path+": expected one "+typeof(T).Name+", found "+items.Length);
            return items[0];
        }
        static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
        static string Hash(string path)
        { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(); }
        static Dictionary<string,string> SceneFiles() => Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
            .Where(p=>p.Replace('\\','/')!=Target && p.Replace('\\','/')!=Menu).ToDictionary(p=>p,p=>Hash(p));
        static string PathOf(Transform t)
        { string p=t.name+"["+t.GetSiblingIndex()+"]";for(var a=t.parent;a!=null;a=a.parent)p=a.name+"["+a.GetSiblingIndex()+"]/"+p;return p; }
        static string Geometry(Scene scene)
        {
            var lines=new List<string>();
            foreach(var t in All<Transform>(scene))
            {
                string path=PathOf(t);
                lines.Add(path+"|"+t.localPosition.ToString("R")+"|"+t.localRotation.ToString("R")+"|"+t.localScale.ToString("R"));
                foreach(var c in t.GetComponents<Collider2D>())
                {
                    string shape=c is BoxCollider2D b?b.size.ToString("R")+"/"+b.edgeRadius:
                        c is CircleCollider2D circle?circle.radius.ToString("R"):
                        c is PolygonCollider2D poly?string.Join("/",Enumerable.Range(0,poly.pathCount).Select(i=>string.Join(";",poly.GetPath(i).Select(v=>v.ToString("R"))))):
                        c is EdgeCollider2D edge?string.Join(";",edge.points.Select(v=>v.ToString("R"))):"";
                    lines.Add(path+"|"+c.GetType().Name+"|"+c.enabled+"|"+c.isTrigger+"|"+c.offset.ToString("R")+"|"+shape);
                }
                foreach(var r in t.GetComponents<Renderer>())lines.Add(path+"|sorting|"+r.sortingLayerID+"|"+r.sortingOrder);
            }
            return string.Join("\n",lines.OrderBy(s=>s,StringComparer.Ordinal));
        }
        static void RequireSavedScenes()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var loaded=SceneManager.GetSceneAt(i);
                Require(!loaded.isDirty,"Save your scene edits before opening or installing the integrated world: "+loaded.path+" ("+loaded.name+")");
            }
        }
        public static void Open() { RequireSavedScenes(); EditorSceneManager.OpenScene(Target); }

        [MenuItem("AlienPurge/Gameplay/Level01/Install Integrated World V01")]
        public static void Install()
        {
            Require(!Application.isPlaying,"Exit Play Mode before installation.");
            Require(Path.GetFullPath(Application.dataPath).Replace('\\','/').Equals("D:/AlienPurge_IntegratedV01/Assets",StringComparison.OrdinalIgnoreCase),"This installer is restricted to D:/AlienPurge_IntegratedV01.");
            RequireSavedScenes();
            Directory.CreateDirectory(Reports);
            var protectedScenes=SceneFiles();
            if(!File.Exists(Reports+"/ProtectedScenes.tsv"))File.WriteAllLines(Reports+"/ProtectedScenes.tsv",protectedScenes.Select(k=>k.Value+"\t"+k.Key));
            CheckProtectedScenes();
            var sourceScene=EditorSceneManager.OpenScene(Source);
            string originalGeometry=Geometry(sourceScene);
            if(!File.Exists(Target))Require(AssetDatabase.CopyAsset(Source,Target),"Cannot create integrated scene copy.");
            Require(AssetDatabase.AssetPathToGUID(Target)!=AssetDatabase.AssetPathToGUID(Source),"Integrated scene must have an independent GUID.");
            var scene=EditorSceneManager.OpenScene(Target);
            var loop=One<RegionLoopController>(scene);
            bool first=!loop.integratedWorld;
            Require(!first||Geometry(scene)==originalGeometry,"Unrecognized target geometry; refusing to replace manual work.");
            var beforeGeometry=Geometry(scene);
            Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            var clones=new Dictionary<string,RegionDefinition>();
            foreach(var definition in loop.definitions)
            {
                string path=Data+"/"+definition.regionId+".asset";
                if(!File.Exists(path))Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(definition),path),"Cannot isolate "+definition.regionId);
                var clone=AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
                Require(clone!=null&&clone.regionId==definition.regionId,"Region identity conflict at "+path);
                if(first)Configure(clone);
                clones.Add(clone.regionId,clone);
            }
            loop.definitions=loop.definitions.Select(d=>clones[d.regionId]).ToArray();
            foreach(var site in loop.optionalSites) { site.definition=clones[site.definition.regionId]; EditorUtility.SetDirty(site); }
            loop.integratedWorld=true; EditorUtility.SetDirty(loop);
            RegisterPool(loop.pool,EnemyType.Support,"Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab",2);
            RegisterPool(loop.pool,EnemyType.Exploder,"Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab",2);
            // Existing prefab GUID inherits the accepted Boss. Never rebuild the Arena or reapply a source prefab.
            var boss=loop.world.boss.GetComponent<HiveTyrantCombat>();
            Require(boss!=null,"L01World.boss lacks accepted HiveTyrantCombat.");
            Require(boss.Config!=null,"Boss combat configuration is missing.");
            var so=new SerializedObject(boss);
            Require(so.FindProperty("arenaBoundary").objectReferenceValue!=null,"Boss lost its manually authored Arena boundary.");
            Require(boss.Config.summonChoices.All(c=>c.type!=EnemyType.Support&&c.type!=EnemyType.Exploder),"Boss summon list contains a region-only specialist.");
            if(first)
            {
                var exit=loop.world.progression.gates.Single(g=>g.gateId=="G7");
                Require(exit.requirement=="Research","Unexpected G7 requirement: "+exit.requirement);
                exit.requirement="BossDefeated";EditorUtility.SetDirty(exit);
                clones["Extraction"].prerequisite="BossDefeated";EditorUtility.SetDirty(clones["Extraction"]);
            }
            foreach(var component in All<MonoBehaviour>(scene))
            {
                if(component==null)continue;
                var settings=new SerializedObject(component);
                SetString(settings,"mainMenuSceneName","MainMenu_IntegratedV01");
                if(component is AudioManager)SetString(settings,"gameplaySceneName","Level01_FullWorld_IntegratedV01");
            }
            Require(Geometry(scene)==beforeGeometry,"Installer unexpectedly changed transforms, collision or sorting.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,Target);
            if(!File.Exists(Menu))Require(AssetDatabase.CopyAsset("Assets/_Project/Scenes/MainMenu_DeskV1.unity",Menu),"Cannot create isolated menu.");
            var menu=EditorSceneManager.OpenScene(Menu);
            foreach(var component in All<MonoBehaviour>(menu))
            {
                if(component==null)continue;
                var settings=new SerializedObject(component);
                SetString(settings,"gameSceneName","Level01_FullWorld_IntegratedV01");
                SetString(settings,"mainMenuSceneName","MainMenu_IntegratedV01");
                if(component is AudioManager)SetString(settings,"gameplaySceneName","Level01_FullWorld_IntegratedV01");
            }
            EditorSceneManager.MarkSceneDirty(menu);EditorSceneManager.SaveScene(menu,Menu);
            foreach(string path in new[]{Target,Menu})
                if(!EditorBuildSettings.scenes.Any(s=>s.path==path))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(path,true)}).ToArray();
            foreach(var clone in clones.Values)AssetDatabase.SaveAssetIfDirty(clone);
            foreach(var kv in protectedScenes)Require(Hash(kv.Key)==kv.Value,"Protected scene changed: "+kv.Key);
            EditorSceneManager.OpenScene(Target);
            File.WriteAllText(Reports+"/Installation.txt","PASS\nSource="+Source+"\nSource SHA256="+Hash(Source)+"\nTarget="+Target+"\nFirst install="+first+"\nOriginal scene files unchanged="+protectedScenes.Count+"\nTransforms/colliders/sorting unchanged. Reinstall preserves tuning and authored geometry.\n");
            Validate();
        }
        static void SetString(SerializedObject so,string name,string value)
        { var p=so.FindProperty(name);if(p!=null&&p.propertyType==SerializedPropertyType.String){p.stringValue=value;so.ApplyModifiedPropertiesWithoutUndo();} }
        public static void CheckProtectedScenes()
        {
            foreach(var line in File.ReadAllLines(Reports+"/ProtectedScenes.tsv"))
            { var parts=line.Split('\t');Require(Hash(parts[1])==parts[0],"Protected scene changed: "+parts[1]); }
        }
        static void RegisterPool(EnemyPool pool,EnemyType type,string path,int capacity)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab!=null&&prefab.GetComponent<PooledEnemy>().Type==type,"Invalid pool prefab: "+path);
            var so=new SerializedObject(pool);var entries=so.FindProperty("poolEntries");
            var found=new List<int>();
            for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("enemyType").intValue==(int)type)found.Add(i);
            Require(found.Count<=1,"Duplicate pool registration for "+type);
            if(found.Count==1)
            {
                Require(entries.GetArrayElementAtIndex(found[0]).FindPropertyRelative("enemyPrefab").objectReferenceValue==prefab,"Conflicting prefab registered for "+type);
                return;
            }
            int index=entries.arraySize++;var item=entries.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("enemyType").intValue=(int)type;item.FindPropertyRelative("enemyPrefab").objectReferenceValue=prefab;
            item.FindPropertyRelative("initialPoolSize").intValue=capacity;item.FindPropertyRelative("allowExpansion").boolValue=false;item.FindPropertyRelative("expansionAmount").intValue=1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Configure(RegionDefinition definition)
        {
            if(definition.regionId=="ColonyEntry")return;
            var normal=definition.Profile(EnemyType.Normal);
            var profiles=definition.enemies.ToList();
            bool support=definition.regionId!="CargoYard";
            foreach(var type in support?new[]{EnemyType.Exploder,EnemyType.Support}:new[]{EnemyType.Exploder})
            {
                string id=type.ToString();
                var data=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/Data/Enemies/Enemy_"+id+"_Data.asset");
                Require(data!=null,"Missing EnemyData "+id);
                profiles.Add(new RegionEnemyProfile { archetypeId=id,type=type,health=Mathf.CeilToInt(normal.health*data.HealthMultiplier),
                    speed=normal.speed*data.MoveSpeedMultiplier,damage=Mathf.CeilToInt(normal.damage*data.DamageMultiplier),experience=data.ExperienceAmount,
                    clearance=.8f,special=true,simultaneousLimit=type==EnemyType.Support?1:2,globalSimultaneousLimit=2 });
            }
            definition.enemies=profiles.ToArray();
            definition.reservedEnemyIds=definition.reservedEnemyIds.Where(id=>id!="Exploder"&&(!support||id!="Support")).ToArray();
            // One visible first encounter, owned as an ordinary garrison member, never an access guard.
            var residents=definition.residents.ToList();
            if(definition.regionId=="CargoYard"||definition.regionId=="PowerMaintenance")
            {
                var ordinary=residents.First(g=>g.type==EnemyType.Normal&&!g.keyGuard&&g.count>1);ordinary.count--;
                residents.Add(new RegionSpawnGroup {type=definition.regionId=="CargoYard"?EnemyType.Exploder:EnemyType.Support,count=1,keyGuard=false});
            }
            definition.residents=residents.ToArray();
            var waves=definition.v02.environmentSquads.ToList();
            for(int i=0;i<waves.Count;i++)waves[i].weight=i==0?40:30;
            waves.Add(new RegionWave {id="integrated-exploder",weight=12,groups=new[]{new RegionSpawnGroup{type=EnemyType.Exploder,count=1},new RegionSpawnGroup{type=EnemyType.Normal,count=4}}});
            if(support)waves.Add(new RegionWave {id="integrated-support",weight=10,groups=new[]{new RegionSpawnGroup{type=EnemyType.Support,count=1},new RegionSpawnGroup{type=EnemyType.Normal,count=4}}});
            definition.v02.environmentSquads=waves.ToArray();definition.v02.weightedEnvironmentSquads=true;
            EditorUtility.SetDirty(definition);
        }

        [MenuItem("AlienPurge/Gameplay/Level01/Validate Integrated World V01")]
        public static void Validate()
        {
            Require(!Application.isPlaying,"Validate in Edit Mode.");
            Directory.CreateDirectory(Reports);
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=Target){RequireSavedScenes();scene=EditorSceneManager.OpenScene(Target);}
            var loop=One<RegionLoopController>(scene);var pool=One<EnemyPool>(scene);
            One<GameManager>(scene);One<MissionManager>(scene);One<PlanetStormController>(scene);One<ToxicFogRouteController>(scene);One<ExtractionController>(scene);
            Require(loop.integratedWorld&&loop.v02Enabled,"Integrated V02 mode missing.");
            Require(!loop.world.progression.previewAllDoorsOpen,"Door preview bypass must be disabled in the integrated scene.");
            Require(All<EnemySpawner>(scene).All(s=>s.regionDriven),"A legacy time-driven spawner is still active.");
            Require(loop.definitions.All(d=>AssetDatabase.GetAssetPath(d).StartsWith(Data+"/")),"Shared source region definition was not isolated.");
            Require(loop.optionalSites.All(s=>loop.definitions.Contains(s.definition)),"Optional task still references a source region definition.");
            Require(loop.world.progression.gates.Single(g=>g.gateId=="G7").requirement=="BossDefeated","G7 must require the real Boss result.");
            Require(loop.definitions.Single(d=>d.regionId=="Extraction").prerequisite=="BossDefeated","Extraction prerequisite mismatch.");
            Require(All<EnemyAbilityTestLab>(scene).Length==0&&All<CoreEnemyArtLab>(scene).Length==0,"Test spawner found in production scene.");
            foreach(var t in All<Transform>(scene))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+PathOf(t));
            var serialized=new SerializedObject(pool);var entries=serialized.FindProperty("poolEntries");var types=new HashSet<int>();
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);int type=entry.FindPropertyRelative("enemyType").intValue;
                Require(types.Add(type),"Duplicate EnemyPool type "+type);
                var prefab=entry.FindPropertyRelative("enemyPrefab").objectReferenceValue as GameObject;
                Require(prefab!=null&&(int)prefab.GetComponent<PooledEnemy>().Type==type&&(int)prefab.GetComponent<EnemyDefinition>().Data.Type==type,"Pool identity mismatch for "+type);
                if(type>=4)
                {
                    int capacity=entry.FindPropertyRelative("initialPoolSize").intValue;
                    Require(!entry.FindPropertyRelative("allowExpansion").boolValue&&capacity>0,"Specialist pool must have a positive finite capacity: "+type);
                    var profiles=loop.definitions.SelectMany(d=>d.enemies).Where(p=>(int)p.type==type).ToArray();
                    Require(profiles.Length>0&&profiles.All(p=>p.globalSimultaneousLimit>0&&p.globalSimultaneousLimit<=capacity&&p.simultaneousLimit>0&&p.simultaneousLimit<=p.globalSimultaneousLimit),"Specialist local/global caps must fit its finite pool: "+type);
                    Require(profiles.Select(p=>p.globalSimultaneousLimit).Distinct().Count()==1,"Conflicting global caps across regions for specialist: "+type);
                    Require(prefab.GetComponent<SupportExploderAnimation>()!=null,"Formal specialist animation missing: "+type);
                    var animator=prefab.GetComponentInChildren<Animator>();Require(animator!=null&&animator.runtimeAnimatorController!=null,"Missing specialist Animator: "+type);
                    foreach(var clip in animator.runtimeAnimatorController.animationClips)
                        foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                            foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding))Require(key.value!=null,"Missing formal frame: "+clip.name);
                }
            }
            Require(types.SetEquals(new[]{0,1,2,3,4,5}),"Expected exactly six compatible EnemyType registrations.");
            foreach(var definition in loop.definitions.Where(d=>d.v02.weightedEnvironmentSquads))
                Require(definition.v02.environmentSquads.All(w=>w.weight>0),"Environment squad weight must be positive in "+definition.regionId);
            var boss=loop.world.boss.GetComponent<HiveTyrantCombat>();
            Require(boss!=null&&boss.Config!=null&&boss.Config.summonChoices.Select(c=>(int)c.type).ToHashSet().SetEquals(new[]{0,1,2,3}),"Boss summon whitelist changed.");
            foreach(string path in new[]{Target,Menu})Require(EditorBuildSettings.scenes.Any(s=>s.path==path&&s.enabled),"Restart/menu target absent from Build Settings: "+path);
            File.WriteAllText(Reports+"/AssetValidation.txt","PASS\nUnity="+Application.unityVersion+"\nSix unique pool types; formal specialist frames present; isolated region configurations; original Boss whitelist; one set of managers; no missing scene scripts; restart/menu paths registered.\n");
            Debug.Log("[IntegratedWorld] PASS: "+Target);
        }
    }
}
