using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienPurge.ArenaArtV02
{
    [Serializable] public class ArenaArtEntry
    {
        public string id, file, kind, parent;
        public float width, visiblePixels;
        public float[] position, pivot;
        public int order;
    }
    [Serializable] public class ArenaArtManifest { public ArenaArtEntry[] entries; }

    // Editor-only: makes new art objects; never changes the selected Boss.
    public class HiveTyrantArenaV02Window : EditorWindow
    {
        const string RootPath = "Assets/AlienPurge/HiveTyrantArenaV02";
        const float ReferenceBossWidth = 8f;
        float bossWidth = ReferenceBossWidth;
        float tileSize = 8f;
        string sortingLayer = "Default";
        string status = "基准：Boss 占地宽 8u，地面 48 × 30u。";
        bool savePrefab = true;

        [MenuItem("AlienPurge/Art/Hive Tyrant Arena V02")]
        public static void Open() { GetWindow<HiveTyrantArenaV02Window>("Hive Tyrant V02"); }

        void OnGUI()
        {
            GUILayout.Label("Hive Tyrant · 大块场景 V02", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("6 块大建筑 + 重复纹理地面。创建新的场景根物体；Boss 和 Player 保持各自现有层级。", MessageType.Info);
            bossWidth = Mathf.Max(0.1f, EditorGUILayout.FloatField("Boss 占地宽度 (u)", bossWidth));
            if (GUILayout.Button("读取选中 Boss 的实体碰撞体宽度")) ReadSelectedBoss();
            tileSize = Mathf.Max(1f, EditorGUILayout.FloatField("每次地面重复宽度 (u)", tileSize));
            sortingLayer = EditorGUILayout.TextField("Sorting Layer", sortingLayer);
            savePrefab = EditorGUILayout.Toggle("同时保存新场景 Prefab", savePrefab);
            float factor = bossWidth / ReferenceBossWidth;
            EditorGUILayout.LabelField("战斗区基准", string.Format("{0:F1} × {1:F1} u", 48f * factor, 30f * factor));
            EditorGUILayout.LabelField("含压边的实际地面", string.Format("{0:F1} × {1:F1} u", 54f * factor, 36f * factor));
            EditorGUILayout.HelpBox(status, MessageType.None);
            if (GUILayout.Button("创建大块 Arena", GUILayout.Height(38)))
            {
                try { BuildArena(); }
                catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
                finally { EditorUtility.ClearProgressBar(); }
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("地面使用单个椭圆网格与镜像纹理，不需要手工拼地砖。每次创建一个新 Arena，可用 Undo 撤销场景物体；新建资产保留在 Generated 中。", MessageType.None);
        }

        void ReadSelectedBoss()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) { status = "先在场景中选中 Boss 根物体。"; return; }
            bool found = false;
            Bounds bounds = new Bounds();
            foreach (Collider2D c in selected.GetComponentsInChildren<Collider2D>())
            {
                if (!c.enabled || c.isTrigger || c.bounds.size.sqrMagnitude < 0.0001f) continue;
                if (!found) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
            if (!found) { status = "没有找到启用的非 Trigger Collider2D。可手动填写实际占地宽度。"; return; }
            bossWidth = Mathf.Max(0.1f, bounds.size.x);
            status = string.Format("读取 {0}：实体碰撞体合并宽度 {1:F2}u；未修改 Boss。", selected.name, bossWidth);
        }

        void BuildArena()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(RootPath + "/arena_manifest.json");
            if (json == null) throw new InvalidOperationException("请把包内 Assets 文件夹合并到项目根目录，保留 HiveTyrantArenaV02 路径。");
            ArenaArtManifest manifest = JsonUtility.FromJson<ArenaArtManifest>(json.text);
            if (manifest == null || manifest.entries == null) throw new InvalidOperationException("素材表无法读取。");
            bool layerExists = false;
            foreach (SortingLayer layer in SortingLayer.layers) if (layer.name == sortingLayer) layerExists = true;
            if (!layerExists) throw new InvalidOperationException("Sorting Layer 不存在，请填写项目中已有的层。");
            Shader floorShader = Shader.Find("AlienPurge/ArenaV02/MirroredGround");
            Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (floorShader == null || spriteShader == null) throw new InvalidOperationException("找不到地面或 URP 2D Sprite Unlit Shader，请确认已安装并启用 2D URP。");
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                ArenaArtEntry a = manifest.entries[i];
                EditorUtility.DisplayProgressBar("Hive Tyrant V02", "导入 " + a.id, (float)i / manifest.entries.Length);
                ConfigureImporter(a);
            }
            EditorUtility.ClearProgressBar();
            string generated = RootPath + "/Generated";
            if (!AssetDatabase.IsValidFolder(generated)) AssetDatabase.CreateFolder(RootPath, "Generated");
            Material spriteMaterial = new Material(spriteShader) { name = "ArenaV02_SpriteUnlit" };
            AssetDatabase.CreateAsset(spriteMaterial, AssetDatabase.GenerateUniqueAssetPath(generated + "/ArenaV02_SpriteUnlit.mat"));
            float factor = bossWidth / ReferenceBossWidth;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Hive Tyrant Arena V02");
            GameObject root = new GameObject("ArenaRoot_LargeBlocks_V02");
            Undo.RegisterCreatedObjectUndo(root, "Create Arena");
            Transform blocks = new GameObject("LargeBlocks").transform;
            blocks.SetParent(root.transform, false);
            Dictionary<string, Transform> placed = new Dictionary<string, Transform>();
            foreach (ArenaArtEntry a in manifest.entries)
            {
                if (a.kind == "texture") continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RootPath + "/" + a.file);
                if (sprite == null) throw new InvalidOperationException("Sprite 未成功导入：" + a.file);
                Transform parent = blocks;
                if (!string.IsNullOrEmpty(a.parent) && !placed.TryGetValue(a.parent, out parent))
                    throw new InvalidOperationException("父组合件不存在：" + a.parent);
                GameObject anchor = new GameObject(a.id);
                anchor.transform.SetParent(parent, false);
                anchor.transform.localPosition = new Vector3(a.position[0] * factor, a.position[1] * factor, 0f);
                GameObject art = new GameObject("Artwork");
                art.transform.SetParent(anchor.transform, false);
                float scale = a.width * factor * 100f / a.visiblePixels;
                art.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer sr = art.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = spriteMaterial;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = a.order;
                placed[a.id] = anchor.transform;
                if (a.kind == "door_leaf") anchor.SetActive(false); // Entrance starts open.
            }
            MakeFloor(root.transform, manifest, generated, floorShader, factor);
            Transform markers = new GameObject("GameplayAnchors").transform;
            markers.SetParent(root.transform, false);
            AddMarker(markers, "Brood_NW", -14f, 8.5f, factor);
            AddMarker(markers, "Brood_NE", 14f, 8.5f, factor);
            AddMarker(markers, "Brood_SW", -16f, -9f, factor);
            AddMarker(markers, "Brood_SE", 16f, -9f, factor);
            AddMarker(markers, "Entry", 0f, -15.5f, factor);
            AddMarker(markers, "Exit", 22f, 3f, factor);
            AssetDatabase.SaveAssets();
            if (savePrefab)
                PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GenerateUniqueAssetPath(generated + "/HiveTyrantArenaV02.prefab"));
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            status = string.Format("已创建：战斗区基准 {0:F1}×{1:F1}u，铺装含 3u 压边。请用真实 Boss 检查绕行、孵化点及门通道。", 48f * factor, 30f * factor);
        }

        static void ConfigureImporter(ArenaArtEntry a)
        {
            string path = RootPath + "/" + a.file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少图片：" + path);
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            if (a.kind == "texture")
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(a.pivot[0], a.pivot[1]);
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }

        void MakeFloor(Transform parent, ArenaArtManifest manifest, string generated, Shader shader, float factor)
        {
            ArenaArtEntry textureEntry = Array.Find(manifest.entries, e => e.kind == "texture");
            if (textureEntry == null) throw new InvalidOperationException("缺少地面材质条目。");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RootPath + "/" + textureEntry.file);
            float width = 54f * factor, height = 36f * factor;
            const int segments = 160;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float theta = i * 2f * Mathf.PI / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(theta) * width * 0.5f, Mathf.Sin(theta) * height * 0.5f, 0f);
                uv[i + 1] = new Vector2(vertices[i + 1].x / tileSize, vertices[i + 1].y / tileSize);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            Mesh mesh = new Mesh { name = "ArenaV02_Ellipse" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(generated + "/ArenaV02_GroundMesh.asset"));
            Material material = new Material(shader) { name = "ArenaV02_MirroredGround" };
            material.SetTexture("_BaseMap", texture);
            material.SetVector("_FloorSize", new Vector4(48f * factor, 30f * factor, 0f, 0f));
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(generated + "/ArenaV02_Ground.mat"));
            GameObject floor = new GameObject("Ground_54x36_IncludingEdgeUnderlap");
            floor.transform.SetParent(parent, false);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = floor.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingLayerName = sortingLayer; mr.sortingOrder = -200;
            // Same coordinate-space UVs under the open entrance; identical colors at overlap.
            Mesh approachMesh = new Mesh { name = "ArenaV02_EntryApproach" };
            approachMesh.vertices = new Vector3[] {
                new Vector3(-3f * factor, -22f * factor, 0), new Vector3(3f * factor, -22f * factor, 0),
                new Vector3(3f * factor, -14f * factor, 0), new Vector3(-3f * factor, -14f * factor, 0) };
            Vector2[] approachUV = new Vector2[4];
            for (int i = 0; i < 4; i++) approachUV[i] = (Vector2)approachMesh.vertices[i] / tileSize;
            approachMesh.uv = approachUV;
            approachMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            approachMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(approachMesh, AssetDatabase.GenerateUniqueAssetPath(generated + "/ArenaV02_ApproachMesh.asset"));
            GameObject approach = new GameObject("Ground_EntryApproach");
            approach.transform.SetParent(parent, false);
            approach.AddComponent<MeshFilter>().sharedMesh = approachMesh;
            MeshRenderer approachRenderer = approach.AddComponent<MeshRenderer>();
            approachRenderer.sharedMaterial = material;
            approachRenderer.sortingLayerName = sortingLayer; approachRenderer.sortingOrder = -201;
        }

        static void AddMarker(Transform parent, string name, float x, float y, float factor)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(x * factor, y * factor, 0f);
        }
    }
}
