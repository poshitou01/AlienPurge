using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Applies only to the existing six authored enemy prefabs/configs and isolated lab.
public static class EnemyPolishSetup
{
    const string Data="Assets/_Project/Data/Enemies/";
    public static string Prefab(int i) => i<4 ? CoreEnemyArtSetup.PrefabPath(i) :
        "Assets/_Project/Prefabs/Enemies/Animated/Enemy_"+(i==4 ? "Support" : "Exploder")+".prefab";
    [MenuItem("AlienPurge/Enemy Polish V2/1 Configure and Validate")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnemyMotionArtRegistration.Reimport();
        var support=AssetDatabase.LoadAssetAtPath<SupportEnemyConfig>(Data+"Support_Ability.asset");
        support.supportRadius=14; support.maximumTargets=8; support.allySearchRadius=18;
        support.keepAwayFromPlayer=6; support.behindAllyDistance=2.5f; support.bonusDamage=1;
        EditorUtility.SetDirty(support);
        var exploder=AssetDatabase.LoadAssetAtPath<ExploderEnemyConfig>(Data+"Exploder_Ability.asset");
        exploder.triggerDistance=2.2f; exploder.explosionRadius=3.6f; exploder.warningDuration=1.15f;
        exploder.spawnGraceDuration=1.2f; exploder.safeArmingDistance=4.8f;
        EditorUtility.SetDirty(exploder);
        var data=new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyData>(Data+"Enemy_Exploder_Data.asset"));
        data.FindProperty("moveSpeedMultiplier").floatValue=1.3f; data.ApplyModifiedPropertiesWithoutUndo();
        var material=AssetDatabase.LoadAssetAtPath<Material>(CoreEnemyArtSetup.Art+"AbilityTest/EnemyAbility_VertexTint.mat");
        for(int i=0;i<6;i++)
        {
            var root=PrefabUtility.LoadPrefabContents(Prefab(i));
            try
            {
                root.GetComponent<EnemyAnimationController>().enabled=false;
                root.GetComponent<Rigidbody2D>().interpolation=RigidbodyInterpolation2D.Interpolate;
                var motion=SupportExploderSetup.Ensure<EnemyMotionPresentation>(root);
                motion.strideDistance=i==2 ? .9f : i==1 ? 1.35f : 1.1f;
                if(i>=1 && i<=3)
                {
                    var telegraph=SupportExploderSetup.Ensure<EnemyGroundTelegraph>(root);
                    SupportExploderSetup.Set(telegraph,"materialTemplate",material);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                        if(renderer.gameObject.name.Contains("Warning")) renderer.enabled=false;
                }
                if(i==1) Values(root.GetComponent<FastChargeAttack>(),("windupDuration",.85f),("attackCooldown",3.8f));
                if(i==2)
                {
                    var attack=root.GetComponent<HeavyShockwaveAttack>();
                    Values(attack,("triggerDistance",3f),("shockwaveRadius",3.8f),("windupDuration",1f),
                        ("waveDuration",.6f),("waveWidth",.3f),("recoveryDuration",.7f),("attackCooldown",4.8f));
                    var so=new SerializedObject(attack); so.FindProperty("expandingShockwave").boolValue=true; so.ApplyModifiedPropertiesWithoutUndo();
                }
                if(i==3) Values(root.GetComponent<RangedEnemyAttack>(),("windupDuration",.95f),("projectileSpeed",8f));
                PrefabUtility.SaveAsPrefabAsset(root,Prefab(i));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        SupportExploderArtSetup.MakeController("Exploder");
        AssetDatabase.SaveAssets();
        CoreEnemyArtSetup.Configure();
        Debug.Log("Enemy Polish V2 configured: six native pooled enemies; original Boss whitelist retained.");
    }
    static void Values(UnityEngine.Object target, params (string field,float value)[] values)
    {
        var so=new SerializedObject(target);
        foreach(var value in values)
        {
            var property=so.FindProperty(value.field);
            if(property==null) throw new InvalidOperationException(target.name+" missing "+value.field);
            property.floatValue=value.value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    [MenuItem("AlienPurge/Enemy Polish V2/2 Open Six Enemy Test Scene")]
    public static void Open() => CoreEnemyArtSetup.Open();
    [MenuItem("AlienPurge/Enemy Polish V2/3 Play Six Enemy Test Scene")]
    public static void Play()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(CoreEnemyArtSetup.ScenePath);
        var lab=UnityEngine.Object.FindObjectOfType<CoreEnemyArtLab>(); lab.spawnOnStart=true; lab.previewOnStart=false;
        EditorApplication.isPlaying=true;
    }
}
