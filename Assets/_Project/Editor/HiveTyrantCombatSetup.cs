using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AlienPurge.ArenaArtV03;

public static class HiveTyrantCombatSetup
{
    public const string BossPath = "Assets/_Project/Prefabs/Enemies/Boss/HiveTyrant.prefab";
    public const string TestScene = "Assets/_Project/Scenes/HiveTyrant_CombatTest.unity";
    public const string WorldScene = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld.unity";
    public const string ConfigPath = "Assets/_Project/Data/Boss/HiveTyrant_Combat.asset";
    private const string AnimationRoot = "Assets/_Project/Animations/Boss/HiveTyrant/";
    private static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    private static readonly List<string> report = new List<string>();

    [MenuItem("AlienPurge/Hive Tyrant/Open Combat Test Scene")]
    public static void OpenCombatTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before opening the test scene.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(TestScene);
        Debug.Log("Hive Tyrant combat test scene opened in Edit mode; press Play to try F1-F6/F9.");
    }

    [MenuItem("AlienPurge/Hive Tyrant/Configure and Validate Combat")]
    public static void ConfigureAndValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before configuration.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        report.Clear();
        Directory.CreateDirectory("Docs/Boss");
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var config = AssetDatabase.LoadAssetAtPath<HiveTyrantCombatConfig>(ConfigPath);
            if (config == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Boss")) AssetDatabase.CreateFolder("Assets/_Project/Data", "Boss");
                config = ScriptableObject.CreateInstance<HiveTyrantCombatConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            if (config.contentVersion < 2)
            {
                if (Mathf.Approximately(config.sweepRadius, 6)) config.sweepRadius = 7;
                if (Mathf.Approximately(config.sweepAngle, 110)) config.sweepAngle = 120;
                config.contentVersion = 2; EditorUtility.SetDirty(config);
            }
            if (config.contentVersion < 3)
            {
                if (config.sweepDamage == 12) config.sweepDamage = 1;
                if (config.slamDamage == 18) config.slamDamage = 2;
                config.contentVersion = 3; EditorUtility.SetDirty(config);
            }
            var poseLibrary = HiveTyrantPoseAuthoring.GetOrCreate();
            const string materialPath = "Assets/_Project/Art/Boss/HiveTyrant/FX/HiveTyrant_VertexTint.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string[] states = ConfigureAnimations();
            GameObject root = PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                var combat = Ensure<HiveTyrantCombat>(root);
                Set(combat, "config", config);
                // User owns all HUD components and references. Never add or configure UI here.
                Set(Ensure<HiveTyrantEffects>(root), "materialTemplate", material); Ensure<HiveTyrantGroundWave>(root); Ensure<HiveTyrantSummoning>(root);
                var presentation = Ensure<HiveTyrantPresentation>(root);
                Set(presentation, "library", poseLibrary);
                Set(presentation, "materialTemplate", material);
                Set(presentation, "body", root.GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>());
                var motor = root.GetComponent<HiveTyrantBossMotor>();
                var motorData = new SerializedObject(motor);
                motorData.FindProperty("solidBody").objectReferenceValue = root.GetComponent<Collider2D>();
                motorData.FindProperty("stoppingDistance").floatValue = 4.2f;
                SerializedProperty names = motorData.FindProperty("stateNames"); names.arraySize = states.Length;
                for (int i = 0; i < states.Length; i++) names.GetArrayElementAtIndex(i).stringValue = states[i] ?? "";
                motorData.ApplyModifiedPropertiesWithoutUndo();
                var healthData = new SerializedObject(root.GetComponent<EnemyHealth>());
                healthData.FindProperty("maxHealth").intValue = config.maxHealth;
                healthData.FindProperty("experienceAmount").intValue = 30;
                healthData.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<Rigidbody2D>().gravityScale = 0;
                PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            BuildTestScene();
            ValidateScene(TestScene);
            IntegrateWorld();
            ValidateScene(WorldScene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(TestScene);
            report.Add("CONFIGURATION PASSED. Play Mode verification is recorded separately.");
        }
        catch (Exception exception)
        { report.Add("FAILED: " + exception); Debug.LogException(exception); throw; }
        finally
        { File.WriteAllLines("Docs/Boss/HiveTyrant_EditorSetup.txt", report); }
    }
    private static string[] ConfigureAnimations()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationRoot + "Controller/HiveTyrant_Boss_Animator_Phase2.controller");
        if (controller == null) throw new InvalidOperationException("Missing existing HiveTyrant_Boss_Animator_Phase2.controller");
        var machine = controller.layers[0].stateMachine;
        var names = new string[80];
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            int action = -1, direction = -1;
            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.mode != AnimatorConditionMode.Equals) continue;
                if (condition.parameter == "Action") action = Mathf.RoundToInt(condition.threshold);
                if (condition.parameter == "Direction") direction = Mathf.RoundToInt(condition.threshold);
            }
            if (action < 0 || action >= 6 || direction < 0 || direction >= 8 || transition.destinationState == null) continue;
            AnimatorState state = transition.destinationState;
            names[action * 8 + direction] = state.name;
            if (action == 4)
            {
                string clipPath = AnimationRoot + "Clips/Idle_Phase2_RiftWing/HiveTyrant_Idle_Phase2(1)_" + Directions[direction] + ".anim";
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (state.motion == null) throw new InvalidOperationException("Missing confirmed RiftWing idle: " + clipPath);
            }
            transition.hasExitTime = false; transition.duration = 0; transition.canTransitionToSelf = false;
            var clip = state.motion as AnimationClip;
            if (clip == null) throw new InvalidOperationException("Missing motion in " + state.name);
            Retiming(clip, action == 3 ? 8 : action == 0 || action == 4 ? 6 : 12, action != 2 && action != 3);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                {
                    if (key.value == null) throw new InvalidOperationException("Missing sprite in " + AssetDatabase.GetAssetPath(clip));
                    var sprite = key.value as Sprite;
                    if (sprite == null) continue;
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
                    if (importer == null) continue;
                    // Preserve canvas and pivot. Normalize only the established import parameters.
                    if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 256) || importer.mipmapEnabled)
                    { importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 256; importer.mipmapEnabled = false; importer.SaveAndReimport(); }
                }
            }
        }
        for (int i = 0; i < 48; i++) if (string.IsNullOrEmpty(names[i])) throw new InvalidOperationException("Missing Action/Direction transition " + i / 8 + "/" + Directions[i % 8]);
        EditorUtility.SetDirty(controller);
        report.Add("Existing controller and clip GUIDs retained; 48 action/direction states checked. RiftWing idle linked. Death retimed to 8fps; locomotion 12fps, idle 6fps.");
        report.Add("Eight original camera views feed the authored local-joint pose library. Existing locomotion/Animator identities retained; UI untouched.");
        return names;
    }
    private static void Retiming(AnimationClip clip, float fps, bool loop)
    {
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            for (int i = 0; i < keys.Length; i++) keys[i].time = i / fps;
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }
        clip.frameRate = fps;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }
    private static void BuildTestScene()
    {
        Scene test;
        if (File.Exists(TestScene)) test = EditorSceneManager.OpenScene(TestScene);
        else
        {
            test = EditorSceneManager.OpenScene("Assets/_Project/Scenes/HiveTyrant_Arena.unity");
            EditorSceneManager.SaveScene(test, TestScene);
        }
        var world = EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(test);
        string[] dependencies = { "GameManager", "BulletPool", "HitEffectPool", "ExperienceOrbPool", "EnemyPool", "EnemyProjectilePool", "ChainLigthningEffectPool", "ExplosionEffectPool", "ExplosionEffect", "AudioManager" };
        foreach (string name in dependencies)
        {
            if (test.GetRootGameObjects().Any(go => go.name == name)) continue;
            GameObject source = world.GetRootGameObjects().FirstOrDefault(go => go.name == name);
            if (source == null) throw new InvalidOperationException("Missing dependency in Level01_FullWorld: " + name);
            GameObject copy = UnityEngine.Object.Instantiate(source); copy.name = name;
            SceneManager.MoveGameObjectToScene(copy, test);
        }
        if (!test.GetRootGameObjects().Any(go => go.CompareTag("Player")))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab");
            if (prefab == null) throw new InvalidOperationException("Missing existing Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, test);
            player.name = "Player"; player.transform.position = new Vector3(0, -10, 0);
        }
        EditorSceneManager.CloseScene(world, true);
        var boss = SceneComponents<HiveTyrantCombat>(test).Single();
        var playerTransform = SceneComponents<PlayerHealth>(test).Single().transform;
        Set(playerTransform.GetComponent<WeaponManager>(), "bulletPool", SceneComponents<BulletPool>(test).Single());
        Set(SceneComponents<ExplosionEffectPool>(test).Single(), "effectPrefab", SceneComponents<ExplosionEffect>(test).Single());
        var playerHealthData = new SerializedObject(playerTransform.GetComponent<PlayerHealth>());
        // Remove only the previous prototype's 100-HP override. Keep subsequent user tuning.
        if (playerHealthData.FindProperty("maxHealth").intValue == 100)
            playerHealthData.FindProperty("maxHealth").intValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab").GetComponent<PlayerHealth>().MaxHealth;
        playerHealthData.ApplyModifiedPropertiesWithoutUndo();
        Set(boss.Motor != null ? boss.Motor : boss.GetComponent<HiveTyrantBossMotor>(), "target", playerTransform);
        Set(boss, "automaticSkills", false);
        var debugRoot = test.GetRootGameObjects().FirstOrDefault(go => go.name == "HiveTyrantCombatTest");
        if (debugRoot == null) debugRoot = new GameObject("HiveTyrantCombatTest");
        Set(Ensure<HiveTyrantCombatDebug>(debugRoot), "boss", boss);
        var camera = SceneComponents<Camera>(test).First();
        camera.orthographic = true; camera.orthographicSize = 14;
        if (!SceneComponents<UnityEngine.Rendering.Universal.Light2D>(test).Any(light => light.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global))
        {
            var light = new GameObject("HiveTyrantTestLight").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global; light.intensity = 1;
        }
        var follow = Ensure<CameraFollow>(camera.gameObject);
        Set(follow, "target", playerTransform); Set(follow, "limitToMapBounds", false);
        BindArena(test, boss);
        Set(Ensure<HiveTyrantArenaCamera>(camera.gameObject), "boss", boss);
        var navigation = test.GetRootGameObjects().FirstOrDefault(go => go.name == "HiveTyrantTestNavigation");
        if (navigation == null) navigation = new GameObject("HiveTyrantTestNavigation");
        var astar = Ensure<AstarPath>(navigation); astar.scanOnStartup = false;
        astar.ConfigureReferencesInternal();
        if (astar.data.GetData() != null && astar.data.GetData().Length == 0) astar.data.SetData(null);
        AlienPurge.Level01ConnectedV01.L01Navigation.EnsureGrid(astar);
        astar.data.SetData(astar.data.SerializeGraphs());
        EditorUtility.SetDirty(astar);
        Set(Ensure<HiveTyrantTestNavigation>(navigation), "astar", astar);
        // A dedicated pool uses the already integrated Swarmer. No global prefab changes.
        var pool = SceneComponents<EnemyPool>(test).Single();
        var poolData = new SerializedObject(pool);
        var entries = poolData.FindProperty("poolEntries");
        bool foundNormal = false;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("enemyType").enumValueIndex != 0) continue;
            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Animated/Enemy_Normal_Swarmer.prefab");
            foundNormal = true;
        }
        if (!foundNormal) throw new InvalidOperationException("Test EnemyPool has no Normal entry.");
        poolData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(test); EditorSceneManager.SaveScene(test);
        report.Add("Dedicated test scene configured with existing Player, camera, arena, Boss, managers and pools; F1-F6/F9 debug controls. Auto AI starts OFF for isolated tests.");
    }
    private static void IntegrateWorld()
    {
        Scene world = EditorSceneManager.OpenScene(WorldScene);
        var worldData = SceneComponents<AlienPurge.Level01ConnectedV01.L01World>(world).Single();
        var boss = worldData.boss != null ? worldData.boss.GetComponent<HiveTyrantCombat>() : null;
        if (boss == null || !boss.gameObject.activeInHierarchy) throw new InvalidOperationException("Level01 L01World.boss does not reference an active HiveTyrant.");
        BindArena(world, boss);
        Set(boss, "automaticSkills", true);
        Set(Ensure<HiveTyrantArenaCamera>(SceneComponents<Camera>(world).First(camera => camera.CompareTag("MainCamera")).gameObject), "boss", boss);
        report.Add("Selected L01World.boss: " + boss.name + "; existing inactive HiveTyrant_Boss preview retained.");
        // Existing L01ArenaBridge already observes EnemyHealth.IsDead and unlocks progression.
        // Never wire a new win/scene transition or change map transforms.
        EditorSceneManager.MarkSceneDirty(world); EditorSceneManager.SaveScene(world);
        report.Add("Level01_FullWorld: existing Boss instance configured in place; original transforms, boundaries and L01ArenaBridge progression retained.");
    }
    private static void BindArena(Scene scene, HiveTyrantCombat boss)
    {
        var boundaries = SceneComponents<ArenaV03Boundary>(scene);
        var nearest = boundaries.OrderBy(boundary => Vector2.Distance(boundary.transform.position, boss.transform.position)).FirstOrDefault();
        if (nearest == null) throw new InvalidOperationException(scene.path + ": no existing ArenaV03Boundary footprint for HiveTyrant");
        Set(boss, "arenaBoundary", nearest);
        report.Add(scene.name + ": bound existing arena footprint " + nearest.name);
    }
    public static T[] SceneComponents<T>(Scene scene) where T : Component
    { return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray(); }
    public static void ValidateScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path);
        var boss = SceneComponents<HiveTyrantCombat>(scene).Single(candidate => candidate.gameObject.activeInHierarchy);
        foreach (Transform child in boss.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                throw new InvalidOperationException(path + ": missing script on " + child.name);
        var serialized = new SerializedObject(boss);
        if (serialized.FindProperty("config").objectReferenceValue == null || serialized.FindProperty("arenaBoundary").objectReferenceValue == null)
            throw new InvalidOperationException(path + ": missing HiveTyrant config/arena reference");
        var animator = boss.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) throw new InvalidOperationException(path + ": missing body Animator");
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip == null) throw new InvalidOperationException(path + ": missing animation clip");
        if (path == TestScene)
        {
            foreach (var pool in SceneComponents<ExplosionEffectPool>(scene))
                if (new SerializedObject(pool).FindProperty("effectPrefab").objectReferenceValue == null)
                    throw new InvalidOperationException(path + ": ExplosionEffectPool.effectPrefab missing");
            foreach (var weapon in SceneComponents<WeaponManager>(scene))
                if (new SerializedObject(weapon).FindProperty("bulletPool").objectReferenceValue == null)
                    throw new InvalidOperationException(path + ": " + weapon.name + ".WeaponManager.bulletPool missing");
        }
        report.Add(path + ": Boss scripts/config/arena/Animator references valid.");
    }
    private static T Ensure<T>(GameObject root) where T : Component
    { T component = root.GetComponent<T>(); return component != null ? component : root.AddComponent<T>(); }
    public static void Set(UnityEngine.Object target, string property, object value)
    {
        var data = new SerializedObject(target);
        var field = data.FindProperty(property);
        if (field == null) throw new InvalidOperationException(target.name + ": no serialized property " + property);
        if (value is bool b) field.boolValue = b;
        else if (value is float f) field.floatValue = f;
        else if (value is int n) field.intValue = n;
        else field.objectReferenceValue = value as UnityEngine.Object;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (target is Component component && PrefabUtility.IsPartOfPrefabInstance(component))
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }
}
