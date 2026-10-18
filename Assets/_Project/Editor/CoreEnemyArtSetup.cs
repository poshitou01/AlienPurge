using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Updates the four existing prefabs in place. No data, pool implementation or
// production scene is replaced; Authored resources keep the old art available.
public static class CoreEnemyArtSetup
{
    public const string ScenePath = "Assets/_Project/Scenes/CoreEnemies_Art_Test.unity";
    public const string Art = "Assets/_Project/Art/Enemies/Animated/";
    public const string Report = "Docs/Enemies/CoreEnemies";
    public static readonly string[] Enemies = { "Swarmer", "Rusher", "Tank", "Spitter" };
    public static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    static readonly string[] Prefabs = { "Enemy_Normal_Swarmer", "Enemy_Fast_Rusher", "Enemy_Heavy_Tank", "Enemy_Ranged_Spitter" };
    public static string PrefabPath(int i) => "Assets/_Project/Prefabs/Enemies/Animated/" + Prefabs[i] + ".prefab";
    public static string ControllerPath(string enemy) => "Assets/_Project/Animations/" + enemy + "/Authored/" + enemy + "_Animator.controller";
    struct Spec
    {
        public string pose, source; public int[] frames; public float seconds, fps; public bool loop;
        public Spec(string pose, string source, int[] frames, float seconds, float fps, bool loop = false)
        { this.pose=pose; this.source=source; this.frames=frames; this.seconds=seconds; this.fps=fps; this.loop=loop; }
    }
    static Spec[] Specs(int index)
    {
        int[] four={1,2,3,4}, eight={1,2,3,4,5,6,7,8};
        var common = new[] { new Spec("Idle","Idle",four,1,4,true), new Spec("Move","Move",eight,8f/12,12,true),
            new Spec("Death","Death",four,CoreEnemyAnimation.CollapseSeconds,8) };
        if (index==0) return common;
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(index));
        Component ability=index==1 ? (Component)root.GetComponent<FastChargeAttack>() : index==2 ? (Component)root.GetComponent<HeavyShockwaveAttack>() : root.GetComponent<RangedEnemyAttack>();
        var so=new SerializedObject(ability);
        float windup=so.FindProperty("windupDuration").floatValue, recovery=so.FindProperty("recoveryDuration").floatValue;
        float active=index==1 ? so.FindProperty("chargeDuration").floatValue : recovery*.5f;
        return common.Concat(new[] { new Spec("Windup","Windup",four,windup,12),
            new Spec("Attack","Attack",index==1 ? eight : four,active,12),
            index==1 ? new Spec("Recover","Windup",new[]{4,3,2,1},recovery,12) : new Spec("Recover","Attack",new[]{3,4},recovery*.5f,12) }).ToArray();
    }
    public static Sprite Frame(string enemy,string state,string direction,int frame)
    {
        string path=Art+enemy+"/Authored/"+state+"/"+direction+"/"+enemy+"_"+state+"_"+direction+"_"+frame.ToString("00")+".png";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite==null) throw new InvalidOperationException("Missing authored frame: "+path);
        return sprite;
    }
    static AnimatorController Controller(int index)
    {
        string enemy=Enemies[index], path=ControllerPath(enemy), folder=Path.GetDirectoryName(path).Replace('\\','/');
        EnsureFolder(folder);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        if (!controller.parameters.Any(p=>p.name=="Direction")) controller.AddParameter("Direction",AnimatorControllerParameterType.Int);
        var machine=controller.layers[0].stateMachine;
        foreach (var spec in Specs(index)) foreach (var direction in Directions)
        {
            string name=enemy+"_"+spec.pose+"_"+direction, clipPath=folder+"/"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip==null) { clip=new AnimationClip(); AssetDatabase.CreateAsset(clip,clipPath); }
            clip.name=name; clip.frameRate=spec.fps;
            var keys=new ObjectReferenceKeyframe[spec.frames.Length+1];
            for(int f=0;f<spec.frames.Length;f++) keys[f]=new ObjectReferenceKeyframe { time=f*spec.seconds/spec.frames.Length,value=Frame(enemy,spec.source,direction,spec.frames[f]) };
            keys[keys.Length-1]=new ObjectReferenceKeyframe { time=spec.seconds,value=keys[spec.loop ? 0 : keys.Length-2].value };
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
            AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=spec.loop; settings.stopTime=spec.seconds;
            AnimationUtility.SetAnimationClipSettings(clip,settings); EditorUtility.SetDirty(clip);
            string stateName=spec.pose+"_"+direction;
            var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==stateName) ?? machine.AddState(stateName);
            state.motion=clip; state.writeDefaultValues=false;
            if(stateName=="Idle_DR") machine.defaultState=state;
        }
        EditorUtility.SetDirty(controller); return controller;
    }
    [MenuItem("AlienPurge/Core Enemies/1 Configure and Validate")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var enemy in Enemies) foreach(var path in Directory.GetFiles(Art+enemy+"/Authored","*.png",SearchOption.AllDirectories))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            if(importer.textureType==TextureImporterType.Sprite && importer.spritePixelsPerUnit==256 && importer.maxTextureSize==512 && importer.alphaIsTransparency && !importer.mipmapEnabled && importer.textureCompression==TextureImporterCompression.Uncompressed) continue;
            SupportExploderArtSetup.ApplyImport(importer); importer.SaveAndReimport();
        }
        for(int i=0;i<4;i++)
        {
            var controller=Controller(i); var root=PrefabUtility.LoadPrefabContents(PrefabPath(i));
            try
            {
                string protectedBefore=ProtectedSnapshot(root);
                var animator=root.GetComponent<Animator>(); animator.runtimeAnimatorController=controller;
                animator.updateMode=AnimatorUpdateMode.Normal; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                root.GetComponent<SpriteRenderer>().sprite=Frame(Enemies[i],"Idle","DR",1);
                var visual=SupportExploderSetup.Ensure<CoreEnemyAnimation>(root);
                SupportExploderSetup.Set(visual,"impactMaterial",AssetDatabase.LoadAssetAtPath<Material>(Art+"AbilityTest/EnemyAbility_VertexTint.mat"));
                var health=new SerializedObject(root.GetComponent<EnemyHealth>());
                health.FindProperty("hitColor").colorValue=new Color(2,2,2,1); health.ApplyModifiedPropertiesWithoutUndo();
                Require(ProtectedSnapshot(root)==protectedBefore,"Art setup preserves data, gameplay components, root/colliders, material and sorting: "+Enemies[i]);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(i));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        BuildScene(); AssetDatabase.SaveAssets(); Validate();
    }
    static void BuildScene()
    {
        // Open the already delivered native-manager lab in memory; save to a NEW
        // scene path. Its file and production arenas are never changed.
        var scene=EditorSceneManager.OpenScene(SupportExploderSetup.ScenePath);
        var old=UnityEngine.Object.FindObjectOfType<EnemyAbilityTestLab>();
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        new GameObject("CoreEnemies_ArtLab").AddComponent<CoreEnemyArtLab>();
        var pool=new SerializedObject(UnityEngine.Object.FindObjectOfType<EnemyPool>());
        var entries=pool.FindProperty("poolEntries");
        for(int i=0;i<entries.arraySize;i++)
        {
            var entry=entries.GetArrayElementAtIndex(i); int type=entry.FindPropertyRelative("enemyType").intValue;
            entry.FindPropertyRelative("initialPoolSize").intValue=type==0 ? 4 : type==5 ? 3 : 2;
            entry.FindPropertyRelative("allowExpansion").boolValue=type<4;
        }
        pool.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene,ScenePath);
        Require(UnityEngine.Object.FindObjectsOfType<EnemyPool>().Length==1 && UnityEngine.Object.FindObjectsOfType<CoreEnemyArtLab>().Length==1,"One native pool and lab");
    }
    [MenuItem("AlienPurge/Core Enemies/2 Open Test Scene")]
    public static void Open() { TryOpen(); }
    static bool TryOpen()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(ScenePath);
        return true;
    }
    [MenuItem("AlienPurge/Core Enemies/5 Play Animation Preview")]
    public static void Preview()
    {
        if(!TryOpen()) return;
        var lab=UnityEngine.Object.FindObjectOfType<CoreEnemyArtLab>(); lab.previewOnStart=true; lab.spawnOnStart=false;
        EditorApplication.isPlaying=true;
    }
    [MenuItem("AlienPurge/Core Enemies/3 Validate Assets")]
    public static void Validate()
    {
        int clips=0, frames=0;
        var audit=new System.Collections.Generic.List<string>();
        for(int i=0;i<4;i++)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i));
            Require((int)prefab.GetComponent<PooledEnemy>().Type==i && (int)prefab.GetComponent<EnemyDefinition>().Data.Type==i,"Existing type and data identity");
            Require(prefab.GetComponent<CoreEnemyAnimation>()!=null && prefab.GetComponent<EnemyPathNavigator>()!=null && prefab.GetComponent<Pathfinding.Seeker>()!=null,"Native animation / pool / navigation components");
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab)==0,"No missing scripts");
            var controller=prefab.GetComponent<Animator>().runtimeAnimatorController;
            Require(controller.animationClips.Length==(i==0?24:48),"Complete eight-direction state set");
            foreach(var clip in controller.animationClips)
            {
                clips++;
                Require(AnimationUtility.GetAnimationEvents(clip).Length==0 && AnimationUtility.GetCurveBindings(clip).Length==0,"No animation gameplay events or root curves");
                var bindings=AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Require(bindings.Length==1 && bindings[0].path=="" && bindings[0].type==typeof(SpriteRenderer),"Only native root SpriteRenderer sprite changes");
                foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,bindings[0]))
                {
                    var sprite=key.value as Sprite;
                    Require(sprite!=null && AssetDatabase.GetAssetPath(sprite).StartsWith(Art+Enemies[i]+"/Authored/",StringComparison.Ordinal),"Own authored sprite");
                    Require(sprite.pixelsPerUnit==256 && sprite.rect.size==new Vector2(512,512) && sprite.pivot==new Vector2(256,256),"512 / PPU256 / center pivot");
                }
            }
            frames+=Directory.GetFiles(Art+Enemies[i]+"/Authored","*.png",SearchOption.AllDirectories).Length;
            var renderer=prefab.GetComponent<SpriteRenderer>(); var collider=prefab.GetComponent<CircleCollider2D>();
            audit.Add(Enemies[i]+"\nPrefab="+PrefabPath(i)+"\nGUID="+AssetDatabase.AssetPathToGUID(PrefabPath(i))+"\nData="+AssetDatabase.GetAssetPath(prefab.GetComponent<EnemyDefinition>().Data)+
                "\nController="+AssetDatabase.GetAssetPath(controller)+"\nRootScale="+prefab.transform.localScale+"\nColliderRadius="+collider.radius+" Offset="+collider.offset+
                "\nMaterial="+AssetDatabase.GetAssetPath(renderer.sharedMaterial)+"\nSorting="+renderer.sortingLayerName+" / "+renderer.sortingOrder+
                "\nClips="+controller.animationClips.Length+"\nTimings="+string.Join(", ",Specs(i).Select(s=>s.pose+"="+s.seconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"s"))+"\n");
        }
        Require(frames==736 && clips==168,"736 independent PNG frames / 168 Sprite-only clips");
        var boss=AssetDatabase.LoadAssetAtPath<HiveTyrantCombatConfig>(HiveTyrantCombatSetup.ConfigPath);
        Require(boss.summonChoices.Length==4 && boss.summonChoices.All(c=>(int)c.type>=0 && (int)c.type<=3),"Boss existing four-type whitelist");
        Directory.CreateDirectory(Report);
        File.WriteAllText(Report+"/AssetValidation.txt","PASS: 736 authored PNGs / 168 clips / 8 directions / 512 RGBA / PPU256 / center Pivot.\nSprite-only curves, no Animation Events, native enemy identity and navigation, Boss original whitelist.\n");
        File.WriteAllLines(Report+"/PrefabAudit.txt",audit);
        Debug.Log("Core enemy authored art asset validation PASSED.");
    }
    static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static string ProtectedSnapshot(GameObject root)
    {
        var renderer=root.GetComponent<SpriteRenderer>();
        return string.Join("\n",root.GetComponents<Component>().Where(c=>!(c is Animator) && !(c is SpriteRenderer) && !(c is CoreEnemyAnimation) && !(c is EnemyHealth)).Select(c=>EditorJsonUtility.ToJson(c)))+
            "\n"+EditorJsonUtility.ToJson(root.GetComponent<EnemyDefinition>().Data)+"\n"+renderer.sharedMaterial.GetInstanceID()+"|"+renderer.sortingLayerID+"|"+renderer.sortingOrder+"|"+renderer.color;
    }
    static void Require(bool condition,string text) { if(!condition) throw new InvalidOperationException(text); }
}

public sealed class CoreEnemyArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(CoreEnemyArtSetup.Enemies.Any(e=>assetPath.StartsWith(CoreEnemyArtSetup.Art+e+"/Authored/",StringComparison.Ordinal)))
            SupportExploderArtSetup.ApplyImport((TextureImporter)assetImporter);
    }
}
