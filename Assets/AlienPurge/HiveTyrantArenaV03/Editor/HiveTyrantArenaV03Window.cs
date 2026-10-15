using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using AlienPurge.ArenaArtV031;

namespace AlienPurge.ArenaArtV03.Editor
{
    public class HiveTyrantArenaV03Window : EditorWindow
    {
        const string AssetRoot = "Assets/AlienPurge/HiveTyrantArenaV03";
        const string Action = "Build Complete Hive Tyrant Arena V03.1";
        GameObject previousArena, player, boss;
        float worldWidth = 72f, detailAmount = .10f;
        bool replacePrevious = true, bindOcclusion = true, savePrefab = true;
        string status = "完整 V03.1：连续环境、精细墙脚边界、Player 脚底碰撞和南侧前景一次生成。";
        Vector2 scroll;
        int worldLayer, groundLayer, orderPerUnit = 100, baseOrder, offset;

        [MenuItem("AlienPurge/Art/Hive Tyrant Arena V03.1 - Complete")]
        public static void Open() { GetWindow<HiveTyrantArenaV03Window>("Hive Tyrant V03.1"); }

        void OnEnable()
        {
            minSize = new Vector2(420, 470);
            FindSceneObjects();
        }

        void FindSceneObjects()
        {
            Scene active = SceneManager.GetActiveScene();
            if (Selection.activeGameObject != null && Selection.activeGameObject.scene == active)
                for (Transform t = Selection.activeTransform; t != null; t = t.parent)
                    if (IsArena(t.gameObject)) { previousArena = t.gameObject; break; }
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (EditorUtility.IsPersistent(t) || t.gameObject.scene != active) continue;
                GameObject g = t.gameObject;
                if (previousArena == null && g.activeInHierarchy && IsArena(g)) previousArena = g;
                if (player == null && g.activeInHierarchy && g.CompareTag("Player")) player = g;
                if (boss == null && g.activeInHierarchy && g.name.StartsWith("HiveTyrant", StringComparison.OrdinalIgnoreCase)
                    && g.GetComponent<Rigidbody2D>() != null) boss = g;
            }
            if (previousArena != null)
            {
                HiveTyrantArenaV03 old = previousArena.GetComponent<HiveTyrantArenaV03>();
                if (old != null) worldWidth = old.worldCanvasWidth;
                else
                    foreach (MeshFilter floor in previousArena.GetComponentsInChildren<MeshFilter>(true))
                        if (floor.name.StartsWith("Ground_54x36") && floor.sharedMesh != null)
                        {
                            float s = floor.sharedMesh.bounds.size.x * floor.transform.localScale.x / 54f;
                            worldWidth = 72f * Mathf.Abs(s); break;
                        }
            }
        }

        static bool IsArena(GameObject g)
        {
            return g.GetComponent<HiveTyrantArenaV03>() != null ||
                g.name.StartsWith("ArenaRoot_LargeBlocks_V02", StringComparison.Ordinal) ||
                g.name.StartsWith("ArenaRoot_Continuous_V03", StringComparison.Ordinal);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label("Hive Tyrant · V03.1 完整搭建", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("所有图片、材质、碰撞和遮挡规则均已集成。北侧身体盖住背景，南侧建筑遮住身体，脚底受墙脚边界约束，右侧设备内部封闭。", MessageType.Info);
            previousArena = (GameObject)EditorGUILayout.ObjectField("替换的旧 Arena", previousArena, typeof(GameObject), true);
            player = (GameObject)EditorGUILayout.ObjectField("现有 Player（自动查找）", player, typeof(GameObject), true);
            boss = (GameObject)EditorGUILayout.ObjectField("现有 Boss（自动查找）", boss, typeof(GameObject), true);
            if (GUILayout.Button("重新识别当前场景")) FindSceneObjects();
            worldWidth = Mathf.Clamp(EditorGUILayout.FloatField("整幅环境宽度 (u)", worldWidth), 10f, 240f);
            EditorGUILayout.LabelField("整幅高度（固定原图比例）", (worldWidth * 2f / 3f).ToString("F1") + " u");
            EditorGUILayout.LabelField("中央可活动范围（约）", (worldWidth * .69f).ToString("F1") + " × " + (worldWidth * .40f).ToString("F1") + " u");
            detailAmount = EditorGUILayout.Slider("地面近景微纹理", detailAmount, 0f, .25f);
            replacePrevious = EditorGUILayout.Toggle("生成成功后停用旧场景美术", replacePrevious);
            bindOcclusion = EditorGUILayout.Toggle("对接 Player / Boss 前后遮挡", bindOcclusion);
            savePrefab = EditorGUILayout.Toggle("保存场景美术 Prefab", savePrefab);
            EditorGUILayout.HelpBox("所有比例由这一处宽度统一计算。Boss、Player 的位置和缩放保持当前值。南门默认打开，青色出口默认封闭。", MessageType.None);
            GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
            if (GUILayout.Button("一键搭建完整 Arena V03.1", GUILayout.Height(42)))
            {
                try { Build(); }
                catch (Exception ex) { status = "构建未完成：" + ex.Message; Debug.LogException(ex); }
                finally { EditorUtility.ClearProgressBar(); }
            }
            GUI.enabled = true;
            if (GUILayout.Button("检查当前 Arena 场景"))
            {
                HiveTyrantArenaV03 arena = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<HiveTyrantArenaV03>() : null;
                if (arena == null) arena = FindObjectOfType<HiveTyrantArenaV03>();
                status = arena != null ? ArenaV03Validation.Check(arena) : "当前场景中没有启用的 Arena。";
            }
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在退出 Play 后生成。");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("当前场景未加载。");
            foreach (GameObject g in new GameObject[] { previousArena, player, boss })
                if (g != null && (EditorUtility.IsPersistent(g) || g.scene != scene))
                    throw new InvalidOperationException("窗口中的对象必须是当前场景实例，不能是 Project 中的 Prefab 资产。");
            if (previousArena != null && !IsArena(previousArena)) throw new InvalidOperationException("旧 Arena 只能选择 V02 / V03 的环境根物体。");
            if (previousArena != null)
            {
                Vector3 scale = previousArena.transform.lossyScale;
                if (scale.x <= 0 || scale.y <= 0 || Mathf.Abs(scale.x - scale.y) > Mathf.Max(scale.x, scale.y) * .01f)
                    throw new InvalidOperationException("旧 Arena 存在负数或不等比缩放。请将窗口中的旧 Arena 清空后生成，或先让它的 X/Y 缩放一致。");
                Vector3 angles = previousArena.transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(angles.z, 0f)) > .1f || Mathf.Abs(Mathf.DeltaAngle(angles.x, 0f)) > .1f || Mathf.Abs(Mathf.DeltaAngle(angles.y, 0f)) > .1f)
                    throw new InvalidOperationException("顶视角 Y 排序要求 Arena 不旋转；当前旧 Arena 有旋转。");
            }
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetRoot + "/arena_layout.json");
            if (text == null) throw new InvalidOperationException("缺少 arena_layout.json，请保留压缩包内 Assets 路径。");
            ArenaV03Layout layout = JsonUtility.FromJson<ArenaV03Layout>(text.text);
            if (layout == null || layout.schemaVersion != 3 || layout.canvasWidth != 1536 || layout.canvasHeight != 1024)
                throw new InvalidOperationException("不支持的画布或素材版本。");
            Shader shader = Shader.Find("AlienPurge/ArenaV03/ContinuousAtlas");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("V03 Shader 不可用，请检查 URP 2D Shader 导入错误。");
            Texture2D atlas = ImportTexture("AP_HT_Arena_Continuous_Open.png");
            Texture2D doorAtlas = ImportTexture("AP_HT_DoorSource.png");
            Texture2D detail = ImportTexture("AP_HT_GroundMicrodetail.png");
            if (atlas.width != 1536 || atlas.height != 1024 || doorAtlas.width != 1536 || doorAtlas.height != 1024)
                throw new InvalidOperationException("导入后画布尺寸被平台设置缩小，请移除这两张纹理的平台尺寸覆盖。");
            ReadProjectSorting();
            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0) throw new InvalidOperationException("项目缺少 Environment 物理层；请使用 AlienPurge 原工程的层配置。");
            EnsureFolder(AssetRoot + "/Generated");
            string folder = AssetDatabase.GenerateUniqueAssetPath(AssetRoot + "/Generated/ArenaV03_1");
            AssetDatabase.CreateFolder(AssetRoot + "/Generated", System.IO.Path.GetFileName(folder));
            Material shared = MakeMaterial(shader, atlas, detail, 0f, folder, "Continuous_Environment");
            Material ground = MakeMaterial(shader, atlas, detail, detailAmount, folder, "Ground_Detail");
            Material closed = MakeMaterial(shader, doorAtlas, detail, 0f, folder, "Closed_Entry_Leaf");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Action);
            GameObject root = new GameObject("ArenaRoot_Continuous_V03_1");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, Action);
            try
            {
                if (previousArena != null)
                {
                    root.transform.position = previousArena.transform.position;
                    root.transform.localScale = previousArena.transform.lossyScale;
                }
                HiveTyrantArenaV03 arena = root.AddComponent<HiveTyrantArenaV03>();
                arena.worldCanvasWidth = worldWidth;
                Transform visuals = Child(root.transform, "Environment_Continuous");
                PutMesh(visuals, "Ground_And_Connected_Perimeter", ArenaV03Geometry.FullAtlas(worldWidth), shared, groundLayer, -30000, folder);
                PutMesh(visuals, "Ground_Closeup_Microdetail", ArenaV03Geometry.GroundDetail(worldWidth, 6f), ground, groundLayer, -29999, folder);
                Transform foreground = Child(root.transform, "Foreground_Occlusion");
                foreach (AtlasRegion region in layout.occluders)
                {
                    EditorUtility.DisplayProgressBar("Arena V03.1", "建立遮挡：" + region.name, .4f);
                    MeshRenderer renderer = PutMesh(foreground, region.name,
                        ArenaV03Geometry.AtlasPolygon(region.name, region.points, worldWidth), shared, worldLayer, 0, folder);
                    AddDepth(renderer.gameObject, renderer, null, region.baselineY, worldWidth);
                }
                Transform gates = Child(root.transform, "Gates");
                MeshRenderer leaf = PutMesh(gates, layout.entryLeaf.name,
                    ArenaV03Geometry.AtlasPolygon(layout.entryLeaf.name, layout.entryLeaf.points, worldWidth), closed, worldLayer, 0, folder);
                AddDepth(leaf.gameObject, leaf, null, layout.entryLeaf.baselineY, worldWidth);
                arena.entryClosedLeaf = leaf.gameObject;
                Transform collision = Child(root.transform, "Collision_Wall_Footprints");
                collision.gameObject.layer = environmentLayer;
                GameObject wall = Child(collision, "Solid_Perimeter_And_Pods").gameObject;
                wall.layer = environmentLayer;
                ArenaV031SolidBoundary boundary = wall.AddComponent<ArenaV031SolidBoundary>();
                boundary.footprint = new Vector2[layout.boundaryPoints.Length];
                for (int i = 0; i < boundary.footprint.Length; i++) boundary.footprint[i] = ArenaV03Geometry.LocalPoint(layout.boundaryPoints[i].Value, worldWidth);
                boundary.canvasHalfSize = new Vector2(worldWidth * .5f, worldWidth / 3f);
                boundary.outsideMargin = worldWidth * 24f / 1536f;
                boundary.RebuildSolid();
                PhysicsMaterial2D physicsMaterial = new PhysicsMaterial2D("ArenaV03_SolidWalls") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(physicsMaterial, folder + "/WallPhysics.physicsMaterial2D");
                wall.GetComponent<PolygonCollider2D>().sharedMaterial = physicsMaterial;
                GameObject gateBlock = Child(collision, "Entry_Blocker").gameObject;
                gateBlock.layer = environmentLayer;
                gateBlock.transform.localPosition = ArenaV03Geometry.LocalPoint(layout.entryBlocker.center.Value, worldWidth);
                BoxCollider2D gateCollider = gateBlock.AddComponent<BoxCollider2D>();
                gateCollider.size = layout.entryBlocker.size.Value * (worldWidth / 1536f);
                gateCollider.sharedMaterial = physicsMaterial;
                arena.entryBlocker = gateCollider;
                // The approach ends inside this map; it is not an endless rectangle.
                GameObject approachEnd = Child(collision, "Approach_End_Stop").gameObject;
                approachEnd.layer = environmentLayer;
                approachEnd.transform.localPosition = ArenaV03Geometry.LocalPoint(new Vector2(768, 1033), worldWidth);
                BoxCollider2D stop = approachEnd.AddComponent<BoxCollider2D>();
                stop.size = new Vector2(320, 30) * (worldWidth / 1536f);
                stop.sharedMaterial = physicsMaterial;
                AddMarkers(root.transform, arena, layout);
                arena.SetEntranceLocked(false);
                AlienPurge.ArenaArtV031.Editor.ArenaV031PrecisionUpgrade.ConfigureOcclusion(root, player);
                AssetDatabase.SaveAssets();
                if (savePrefab)
                {
                    bool success;
                    PrefabUtility.SaveAsPrefabAsset(root, folder + "/HiveTyrantArenaV03_1.prefab", out success);
                    if (!success) throw new InvalidOperationException("Prefab 保存失败。");
                }
                // External scene references are added AFTER saving the reusable art prefab.
                if (replacePrevious && previousArena != null)
                {
                    PreserveSceneActors(previousArena);
                    Undo.RecordObject(previousArena, Action);
                    previousArena.SetActive(false);
                }
                if (bindOcclusion)
                {
                    if (!HasUsablePlayerYSort()) BindVisualRoot(root.transform, player, "Player_Depth_Link");
                    BindVisualRoot(root.transform, boss, "Boss_Depth_Link");
                }
                AddBossWallGuard(root.transform, collision);
                AlienPurge.ArenaArtV031.Editor.ArenaV031PrecisionUpgrade.BindPlayerFeet(root, player);
                Undo.RegisterFullObjectHierarchyUndo(root, Action);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = root;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
                previousArena = root;
                Physics2D.SyncTransforms();
                status = "已生成完整 V03.1。" + ArenaV03Validation.Check(arena);
                if (player == null) status += " 尚未指定 Player，脚底碰撞未绑定；加入 Player 后重新搭建或运行 Tools 中的刷新工具。";
                Undo.CollapseUndoOperations(undo);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        void ReadProjectSorting()
        {
            worldLayer = ExistingLayer("World", "Player", "Default");
            groundLayer = ExistingLayer("Ground", "Default", "Default");
            orderPerUnit = 100; baseOrder = 0; offset = 0;
            MonoBehaviour sort = FindPlayerYSort();
            if (sort == null) return;
            SerializedObject data = new SerializedObject(sort);
            SerializedProperty groupProperty = data.FindProperty("sortingGroup");
            SortingGroup group = groupProperty != null ? groupProperty.objectReferenceValue as SortingGroup : null;
            if (group == null) group = sort.GetComponent<SortingGroup>();
            if (group != null) worldLayer = group.sortingLayerID;
            orderPerUnit = Mathf.Max(1, IntProperty(data, "orderPerUnit", 100));
            baseOrder = IntProperty(data, "baseOrder", 0);
            offset = IntProperty(data, "orderOffset", 0);
        }

        MonoBehaviour FindPlayerYSort()
        {
            if (player == null) return null;
            foreach (MonoBehaviour m in player.GetComponentsInChildren<MonoBehaviour>(true))
                if (m != null && m.enabled && m.GetType().Name == "YSortRenderer") return m;
            return null;
        }
        bool HasUsablePlayerYSort()
        {
            MonoBehaviour sort = FindPlayerYSort();
            return sort != null && (sort.GetComponent<SortingGroup>() != null || sort.GetComponent<SpriteRenderer>() != null);
        }
        static int IntProperty(SerializedObject o, string name, int fallback)
        {
            SerializedProperty p = o.FindProperty(name); return p != null ? p.intValue : fallback;
        }
        static int ExistingLayer(string first, string second, string fallback)
        {
            foreach (string n in new string[] { first, second, fallback })
                foreach (SortingLayer layer in SortingLayer.layers) if (layer.name == n) return layer.id;
            return 0;
        }

        void BindVisualRoot(Transform arenaRoot, GameObject actor, string name)
        {
            if (actor == null) return;
            Transform visual = actor.transform.Find("VisualRoot");
            if (visual == null) visual = actor.transform.Find("BossVisualRoot");
            if (visual == null) visual = actor.transform;
            SortingGroup group = visual.GetComponent<SortingGroup>();
            bool existed = group != null;
            if (group == null) group = Undo.AddComponent<SortingGroup>(visual.gameObject);
            Undo.RecordObject(group, Action);
            GameObject link = Child(arenaRoot, name).gameObject;
            ArenaV03YSort relay = link.AddComponent<ArenaV03YSort>();
            relay.targetGroup = group;
            relay.restoreGroupOnDisable = true;
            relay.originalEnabled = existed && group.enabled;
            relay.originalLayerId = group.sortingLayerID;
            relay.originalOrder = group.sortingOrder;
            Transform anchor = actor.transform.Find("SortAnchor");
            relay.sortAnchor = anchor != null ? anchor : actor.transform;
            relay.sortingLayerId = worldLayer;
            relay.orderPerUnit = orderPerUnit; relay.baseOrder = baseOrder; relay.orderOffset = offset;
            relay.Refresh();
        }

        void AddBossWallGuard(Transform arenaRoot, Transform collisionRoot)
        {
            if (boss == null) return;
            Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
            if (body == null || body.bodyType != RigidbodyType2D.Kinematic) return;
            Collider2D solid = null;
            foreach (Collider2D c in boss.GetComponentsInChildren<Collider2D>())
                if (c.enabled && !c.isTrigger && c.attachedRigidbody == body)
                {
                    if (solid == null || c.bounds.size.sqrMagnitude > solid.bounds.size.sqrMagnitude) solid = c;
                }
            if (solid == null) return;
            ArenaV03KinematicWallGuard guard = Child(arenaRoot, "Boss_Kinematic_Wall_Guard").gameObject.AddComponent<ArenaV03KinematicWallGuard>();
            guard.body = body; guard.solidBody = solid; guard.collisionRoot = collisionRoot;
        }

        void AddDepth(GameObject go, Renderer renderer, SortingGroup group, float pixelY, float width)
        {
            Transform anchor = Child(go.transform, "Ground_Sort_Anchor");
            anchor.localPosition = ArenaV03Geometry.LocalPoint(new Vector2(768f, pixelY), width);
            ArenaV03YSort sort = go.AddComponent<ArenaV03YSort>();
            sort.targetRenderer = renderer; sort.targetGroup = group;
            sort.sortAnchor = anchor; sort.sortingLayerId = worldLayer;
            sort.orderPerUnit = orderPerUnit; sort.baseOrder = baseOrder; sort.orderOffset = offset + 1;
            sort.Refresh();
        }

        void AddMarkers(Transform root, HiveTyrantArenaV03 arena, ArenaV03Layout layout)
        {
            Transform parent = Child(root, "Gameplay_Anchors");
            List<Transform> brood = new List<Transform>();
            foreach (ArenaMarker m in layout.markers)
            {
                Transform t = Child(parent, m.name);
                t.localPosition = ArenaV03Geometry.LocalPoint(new Vector2(m.x, m.y), worldWidth);
                if (m.name.StartsWith("Brood_")) brood.Add(t);
                if (m.name == "Boss_Start") arena.bossStart = t;
                if (m.name == "Player_Start_Inside") arena.playerStart = t;
                if (m.name == "Entry_Approach") arena.entryApproach = t;
                if (m.name == "Exit_Reward") arena.exitReward = t;
            }
            arena.broodPoints = brood.ToArray();
        }

        void PreserveSceneActors(GameObject old)
        {
            HashSet<Transform> keep = new HashSet<Transform>();
            foreach (GameObject actor in new GameObject[] { player, boss })
                if (actor != null && actor != old && actor.transform.IsChildOf(old.transform))
                {
                    Transform top = actor.transform;
                    while (top.parent != old.transform) top = top.parent;
                    keep.Add(top);
                }
            foreach (Camera camera in old.GetComponentsInChildren<Camera>(true))
            {
                Transform top = camera.transform;
                if (top == old.transform) continue;
                while (top.parent != old.transform) top = top.parent;
                keep.Add(top);
            }
            foreach (Transform t in keep) Undo.SetTransformParent(t, old.transform.parent, Action);
        }

        static Transform Child(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        static MeshRenderer PutMesh(Transform parent, string name, Mesh mesh, Material mat, int sortingLayer, int order, string folder)
        {
            AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset");
            GameObject g = Child(parent, name).gameObject;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat; r.sortingLayerID = sortingLayer; r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }
        static Material MakeMaterial(Shader shader, Texture2D atlas, Texture2D detail, float amount, string folder, string name)
        {
            Material m = new Material(shader) { name = name };
            m.SetTexture("_BaseMap", atlas); m.SetTexture("_DetailMap", detail); m.SetFloat("_DetailAmount", amount);
            AssetDatabase.CreateAsset(m, folder + "/" + name + ".mat"); return m;
        }
        static Texture2D ImportTexture(string name)
        {
            string path = AssetRoot + "/Art/" + name;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少纹理：" + name);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true; importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("纹理导入失败：" + name);
            return texture;
        }
        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
