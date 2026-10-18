using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SupportExploderSetup
{
    public const string ScenePath = "Assets/_Project/Scenes/SupportExploder_Test.unity";
    public const string SupportPrefab = "Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab";
    public const string ExploderPrefab = "Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab";
    public const string SupportData = "Assets/_Project/Data/Enemies/Enemy_Support_Data.asset";
    public const string ExploderData = "Assets/_Project/Data/Enemies/Enemy_Exploder_Data.asset";
    public const string SupportConfig = "Assets/_Project/Data/Enemies/Support_Ability.asset";
    public const string ExploderConfig = "Assets/_Project/Data/Enemies/Exploder_Ability.asset";
    private const string ArtRoot = "Assets/_Project/Art/Enemies/Animated/AbilityTest";
    private const string NormalPrefab = "Assets/_Project/Prefabs/Enemies/Animated/Enemy_Normal_Swarmer.prefab";
    private static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };

    [MenuItem("AlienPurge/Support + Exploder/1 Configure and Validate")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(ArtRoot);
        SupportExploderArtSetup.PrepareImports();
        var material = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/EnemyAbility_VertexTint.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, ArtRoot + "/EnemyAbility_VertexTint.mat");
        }
        var support = LoadOrCreate<SupportEnemyConfig>(SupportConfig);
        var exploder = LoadOrCreate<ExploderEnemyConfig>(ExploderConfig);
        MakeData(SupportData, EnemyType.Support, "Support", 1.5f, .8f, 3, 1.05f);
        MakeData(ExploderData, EnemyType.Exploder, "Exploder", 1f, 1.1f, 2, 1.05f);
        MakePrefab(SupportPrefab, SupportData, MakeController("Support"), material, support, null);
        MakePrefab(ExploderPrefab, ExploderData, MakeController("Exploder"), material, null, exploder);
        BuildScene(material);
        AssetDatabase.SaveAssets(); Validate();
    }
    [MenuItem("AlienPurge/Support + Exploder/2 Open Test Scene")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }
    [MenuItem("AlienPurge/Support + Exploder/3 Validate Assets")]
    public static void Validate()
    {
        Require((int)EnemyType.Normal == 0 && (int)EnemyType.Fast == 1 && (int)EnemyType.Heavy == 2 && (int)EnemyType.Ranged == 3, "Legacy enum identity");
        foreach (var path in new[] { SupportPrefab, ExploderPrefab })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(prefab != null, path);
            var data = prefab.GetComponent<EnemyDefinition>().Data;
            Require(prefab.GetComponent<PooledEnemy>().Type == data.Type, "Data / pooled identity");
            Require(prefab.GetComponent<EnemyPathNavigator>() != null && prefab.GetComponent<Pathfinding.Seeker>() != null, "Native A* navigation");
            SupportExploderArtSetup.Validate(prefab);
        }
        var boss = AssetDatabase.LoadAssetAtPath<HiveTyrantCombatConfig>(HiveTyrantCombatSetup.ConfigPath);
        Require(boss.summonChoices.Length == 4 && boss.summonChoices.All(c => (int)c.type >= 0 && (int)c.type <= 3), "Boss explicit original four-type whitelist");
        Directory.CreateDirectory("Docs/Enemies/SupportExploder");
        AuditExistingEnemies();
        File.WriteAllText("Docs/Enemies/SupportExploder/AssetValidation.txt", "PASS: enum identities, data/prefab types, native A*, 96 authored directional state clips, own sprites / 512 canvas / PPU 256 / registered torso pivot, no Animation Events, Boss original whitelist.\n424 authored PNG frames (416 body + 8 radial VFX); existing prefab/data identities retained.\n");
        Debug.Log("Support + Exploder authored-art asset validation PASSED.");
    }
    [MenuItem("AlienPurge/Support + Exploder/5 Play Animation Preview")]
    public static void Preview()
    {
        Open();
        var lab = UnityEngine.Object.FindObjectOfType<EnemyAbilityTestLab>();
        lab.previewOnStart = true; lab.spawnOnStart = false; // Unsaved inspection fixture.
        EditorApplication.isPlaying = true;
    }
    private static void AuditExistingEnemies()
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var name in new[] { "Enemy_Normal_Swarmer", "Enemy_Fast_Rusher", "Enemy_Heavy_Tank", "Enemy_Ranged_Spitter" })
        {
            string path = "Assets/_Project/Prefabs/Enemies/Animated/" + name + ".prefab";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderer = root.GetComponentInChildren<SpriteRenderer>(); var animator = root.GetComponentInChildren<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips[0];
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)[0];
            var sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value;
            lines.Add(name + "\nPrefab=" + path + "\nData=" + AssetDatabase.GetAssetPath(root.GetComponent<EnemyDefinition>().Data) +
                "\nAnimator=" + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) + "\nMaterial=" + AssetDatabase.GetAssetPath(renderer.sharedMaterial) +
                "\nSprite=" + AssetDatabase.GetAssetPath(sprite) + "\nPPU=" + sprite.pixelsPerUnit + " Pivot=" + sprite.pivot + " Rect=" + sprite.rect +
                "\nSorting=" + renderer.sortingLayerName + " / " + renderer.sortingOrder + "\nColliders=" + string.Join(",", root.GetComponents<Collider2D>().Select(c => c.GetType().Name + " offset=" + c.offset)) +
                "\nComponents=" + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + "\n");
        }
        File.WriteAllLines("Docs/Enemies/SupportExploder/ExistingEnemyAudit.txt", lines);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static void MakeData(string path, EnemyType type, string name, float hp, float speed, int xp, float scale)
    {
        if (AssetDatabase.LoadAssetAtPath<EnemyData>(path) != null) return;
        var data = LoadOrCreate<EnemyData>(path); var so = new SerializedObject(data);
        so.FindProperty("enemyType").intValue = (int)type; so.FindProperty("displayName").stringValue = name;
        so.FindProperty("healthMultiplier").floatValue = hp; so.FindProperty("moveSpeedMultiplier").floatValue = speed;
        so.FindProperty("damageMultiplier").floatValue = 1; so.FindProperty("experienceAmount").intValue = xp;
        so.FindProperty("visualScale").floatValue = scale; so.FindProperty("visualColor").colorValue = Color.white;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static AnimatorController MakeController(string name)
    {
        return SupportExploderArtSetup.MakeController(name);
    }
    private static void MakePrefab(string path, string dataPath, AnimatorController controller, Material material, SupportEnemyConfig support, ExploderEnemyConfig exploder)
    {
        GameObject root;
        bool exists = File.Exists(path);
        if (exists) root = PrefabUtility.LoadPrefabContents(path);
        else
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NormalPrefab));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // A newly cloned Swarmer now has its own presentation. Specialists
            // must keep their dedicated animation/death presenter instead.
            var corePresentation = root.GetComponent<CoreEnemyAnimation>();
            if (corePresentation != null) UnityEngine.Object.DestroyImmediate(corePresentation);
        }
        try
        {
            root.name = support != null ? "Enemy_Support" : "Enemy_Exploder"; root.transform.position = Vector3.zero;
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            Set(root.GetComponent<EnemyDefinition>(), "enemyData", data);
            SetInt(root.GetComponent<PooledEnemy>(), "enemyType", (int)data.Type);
            var contact = new SerializedObject(root.GetComponent<EnemyContactDamage>());
            contact.FindProperty("canDealContactDamage").boolValue = false; contact.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<Animator>().runtimeAnimatorController = controller;
            root.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root.GetComponent<Animator>().updateMode = AnimatorUpdateMode.Normal;
            root.GetComponent<SpriteRenderer>().sprite = SupportExploderArtSetup.Frame(support != null ? "Support" : "Exploder", "Idle", "DR", 1);
            var healthVisual = new SerializedObject(root.GetComponent<EnemyHealth>());
            // Native white-on-white tint is invisible on white-tinted source art.
            // A brief HDR multiplier makes the existing .08s hit flash readable.
            healthVisual.FindProperty("hitColor").colorValue = new Color(2, 2, 2, 1);
            healthVisual.ApplyModifiedPropertiesWithoutUndo();
            Ensure<SupportExploderAnimation>(root);
            var movement = new SerializedObject(root.GetComponent<EnemyMovement>());
            movement.FindProperty("preventEnvironmentPenetration").boolValue = true;
            movement.FindProperty("stoppingDistance").floatValue = support != null ? .45f : .8f;
            movement.FindProperty("enableSeparation").boolValue = true;
            movement.ApplyModifiedPropertiesWithoutUndo();
            if (support != null) Set(Ensure<SupportEnemyAbility>(root), "config", support);
            if (exploder != null) Set(Ensure<ExploderEnemyAbility>(root), "config", exploder);
            Set(Ensure<EnemyAbilityPresentation>(root), "materialTemplate", material);
            var visual = new SerializedObject(root.GetComponent<EnemyAbilityPresentation>());
            visual.FindProperty("temporaryArt").boolValue = false;
            var frames = visual.FindProperty("explosionFrames"); frames.arraySize = exploder != null ? 8 : 0;
            for (int i = 0; i < frames.arraySize; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SupportExploderArtSetup.Art + "Exploder/Explosion/Exploder_Explosion_" + (i + 1).ToString("00") + ".png");
            visual.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void BuildScene(Material material)
    {
        if (!File.Exists(ScenePath)) Require(AssetDatabase.CopyAsset(HiveTyrantCombatSetup.TestScene, ScenePath), "Copy isolated scene");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        string[] keep = { "Main Camera", "GameManager", "BulletPool", "HitEffectPool", "ExperienceOrbPool", "EnemyPool", "EnemyProjectilePool", "ChainLigthningEffectPool", "ExplosionEffectPool", "ExplosionEffect", "AudioManager", "HiveTyrantTestLight" };
        foreach (var root in scene.GetRootGameObjects())
            if (!keep.Contains(root.name) && !root.CompareTag("Player")) UnityEngine.Object.DestroyImmediate(root);
        var camera = SceneObjects<Camera>(scene).Single();
        foreach (var component in camera.GetComponents<MonoBehaviour>())
            if (component is HiveTyrantArenaCamera || component is CameraFollow) UnityEngine.Object.DestroyImmediate(component);
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 10.5f;
        camera.backgroundColor = new Color(.035f, .045f, .055f); camera.clearFlags = CameraClearFlags.SolidColor;
        var player = SceneObjects<PlayerHealth>(scene).Single(); player.transform.position = new Vector3(0, -6, 0);
        Set(player.GetComponent<WeaponManager>(), "bulletPool", SceneObjects<BulletPool>(scene).Single());
        Set(SceneObjects<GameManager>(scene).Single(), "playerExperience", player.GetComponent<PlayerExperience>());
        var manager = new SerializedObject(SceneObjects<GameManager>(scene).Single()); manager.FindProperty("targetSurvivalTime").floatValue = 3600; manager.ApplyModifiedPropertiesWithoutUndo();
        var pool = new SerializedObject(SceneObjects<EnemyPool>(scene).Single());
        var entries = pool.FindProperty("poolEntries"); entries.arraySize = 6;
        string[] prefabs = { NormalPrefab, "Assets/_Project/Prefabs/Enemies/Animated/Enemy_Fast_Rusher.prefab", "Assets/_Project/Prefabs/Enemies/Animated/Enemy_Heavy_Tank.prefab", "Assets/_Project/Prefabs/Enemies/Animated/Enemy_Ranged_Spitter.prefab", SupportPrefab, ExploderPrefab };
        int[] counts = { 8, 4, 2, 3, 2, 3 };
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i); entry.FindPropertyRelative("enemyType").intValue = i;
            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]);
            entry.FindPropertyRelative("initialPoolSize").intValue = counts[i]; entry.FindPropertyRelative("allowExpansion").boolValue = i < 4;
            entry.FindPropertyRelative("expansionAmount").intValue = 2;
        }
        pool.ApplyModifiedPropertiesWithoutUndo();
        var nav = new GameObject("SupportExploder_TestNavigation"); var astar = nav.AddComponent<AstarPath>();
        astar.scanOnStartup = false; astar.logPathResults = Pathfinding.PathLog.OnlyErrors; astar.ConfigureReferencesInternal();
        AlienPurge.Level01ConnectedV01.L01Navigation.EnsureGrid(astar); astar.data.SetData(astar.data.SerializeGraphs());
        nav.AddComponent<EnemyAbilityTestNavigation>();
        new GameObject("SupportExploder_TestLab").AddComponent<EnemyAbilityTestLab>();
        var floor = new GameObject("SupportExploder_TestGeometry");
        MakeRect(floor.transform, "Floor", Vector2.zero, new Vector2(34, 20), new Color(.075f, .095f, .105f), false, material);
        MakeRect(floor.transform, "West", new Vector2(-17, 0), new Vector2(.8f, 20), new Color(.2f, .24f, .27f), true, material);
        MakeRect(floor.transform, "East", new Vector2(17, 0), new Vector2(.8f, 20), new Color(.2f, .24f, .27f), true, material);
        MakeRect(floor.transform, "North", new Vector2(0, 10), new Vector2(34, .8f), new Color(.2f, .24f, .27f), true, material);
        MakeRect(floor.transform, "South", new Vector2(0, -10), new Vector2(34, .8f), new Color(.2f, .24f, .27f), true, material);
        MakeRect(floor.transform, "NavigationCoverA", new Vector2(-3, 1), new Vector2(1.5f, 6), new Color(.3f, .27f, .23f), true, material);
        MakeRect(floor.transform, "NavigationCoverB", new Vector2(5, 1), new Vector2(1.5f, 4), new Color(.3f, .27f, .23f), true, material);
        for (int x = -16; x <= 16; x += 2) MakeRect(floor.transform, "GridX" + x, new Vector2(x, 0), new Vector2(.018f, 19), new Color(.13f, .17f, .18f), false, material, 1);
        for (int y = -8; y <= 8; y += 2) MakeRect(floor.transform, "GridY" + y, new Vector2(0, y), new Vector2(33, .018f), new Color(.13f, .17f, .18f), false, material, 1);
        EditorSceneManager.SaveScene(scene);
        Require(SceneObjects<EnemyPool>(scene).Count() == 1 && SceneObjects<GameManager>(scene).Count() == 1 && SceneObjects<EnemyAbilityTestLab>(scene).Count() == 1, "Idempotent scene roots");
        Require(!SceneObjects<HiveTyrantCombat>(scene).Any(), "No Boss encounter in lab");
    }
    private static void MakeRect(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool solid, Material material, int order = 0)
    {
        string meshName = solid ? (name.StartsWith("Navigation") ? "CoverQuad" : "BoundaryQuad") : order > 0 ? "GridQuad" : "FloorQuad";
        string path = ArtRoot + "/" + meshName + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = meshName };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.colors = new[] { color, color, color, color }; mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path);
        }
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = new Vector3(size.x, size.y, 1);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Ground"; renderer.sortingOrder = solid ? 2 : order;
        if (solid) { go.layer = 7; go.AddComponent<BoxCollider2D>(); }
    }
    public static T Ensure<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
    public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value) { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetInt(UnityEngine.Object target, string field, int value) { var so = new SerializedObject(target); so.FindProperty(field).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static System.Collections.Generic.IEnumerable<T> SceneObjects<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true));
}
