using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienPurge.ArenaArtV021
{
    // Additive repair for existing V02 scenes. The six buildings and actors are untouched.
    public class ArenaBoundaryFixV021 : EditorWindow
    {
        const string AssetRoot = "Assets/AlienPurge/HiveTyrantArenaV02/BoundaryFix";
        const string RepairName = "Boundary_Continuous_V02_1";
        Transform arena;
        float radiusX = 23.4f, radiusY = 14.4f, centerY = -2f;
        float wallWidth = 0.8f, wallHeight = 0.65f, gateWidth = 3.8f;
        bool addCollision;
        string status = "在 Hierarchy 选中 ArenaRoot_LargeBlocks_V02，然后应用补丁。";
        const int Segments = 256;

        [MenuItem("AlienPurge/Art/Repair Arena Boundary V02")]
        static void Open() { GetWindow<ArenaBoundaryFixV021>("Arena Boundary Fix"); }
        void OnEnable() { arena = FindArena(Selection.activeTransform); }
        void OnGUI()
        {
            GUILayout.Label("V02.1 · 连续围墙修复", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("沿一条连续曲线补齐低墙。保留当前建筑、角色、场地比例；南侧入口留通道。", MessageType.Info);
            arena = (Transform)EditorGUILayout.ObjectField("当前 ArenaRoot", arena, typeof(Transform), true);
            if (GUILayout.Button("使用 Hierarchy 当前选择")) arena = FindArena(Selection.activeTransform);
            radiusX = Mathf.Max(2f, EditorGUILayout.FloatField("基准半宽", radiusX));
            radiusY = Mathf.Max(2f, EditorGUILayout.FloatField("基准半高", radiusY));
            centerY = EditorGUILayout.FloatField("基准中心 Y", centerY);
            wallWidth = Mathf.Clamp(EditorGUILayout.FloatField("低墙厚度", wallWidth), 0.15f, 2f);
            wallHeight = Mathf.Clamp(EditorGUILayout.FloatField("低墙视觉高度", wallHeight), 0.15f, 1.5f);
            gateWidth = Mathf.Clamp(EditorGUILayout.FloatField("入口通道宽度", gateWidth), 1f, radiusX * 1.8f);
            addCollision = EditorGUILayout.Toggle("附加静态碰撞边界", addCollision);
            EditorGUILayout.HelpBox("参数按 Boss 宽 8u 的基准填写；工具会自动乘已有地面的比例。初次应用保持默认参数。碰撞默认不添加。", MessageType.None);
            if (GUILayout.Button("给当前 Arena 补齐连续边界", GUILayout.Height(38)))
            {
                try { Repair(); }
                catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
            }
            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        static Transform FindArena(Transform selected)
        {
            for (Transform t = selected; t != null; t = t.parent)
                if (t.Find("LargeBlocks") != null) return t;
            return null;
        }

        void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play 模式，在编辑模式应用补丁。");
            if (arena == null || EditorUtility.IsPersistent(arena) || !arena.gameObject.scene.IsValid())
                throw new InvalidOperationException("请指定场景中的 ArenaRoot，不能选择 Project 中的 Prefab 资产。");
            Transform blocks = arena.Find("LargeBlocks");
            if (blocks == null) throw new InvalidOperationException("这个物体下面没有 LargeBlocks，请选择 V02 的场景根物体。");
            MeshFilter floor = null;
            foreach (MeshFilter candidate in arena.GetComponentsInChildren<MeshFilter>(true))
                if (candidate.name.StartsWith("Ground_54x36", StringComparison.Ordinal)) { floor = candidate; break; }
            if (floor == null || floor.sharedMesh == null)
                throw new InvalidOperationException("没有找到 V02 地面网格 Ground_54x36，请检查选中的场景根物体。");
            Vector3 floorSize = floor.sharedMesh.bounds.size;
            float fx = floorSize.x * Mathf.Abs(floor.transform.localScale.x) / 54f;
            float fy = floorSize.y * Mathf.Abs(floor.transform.localScale.y) / 36f;
            if (fx < 0.001f || fy < 0.001f) throw new InvalidOperationException("地面尺寸无效。");
            Vector3 localFloorCenter = arena.InverseTransformPoint(floor.transform.TransformPoint(floor.sharedMesh.bounds.center));
            Vector2 center = new Vector2(localFloorCenter.x, localFloorCenter.y + centerY * fy);
            float rx = radiusX * fx, ry = radiusY * fy;
            float width = wallWidth * Mathf.Min(fx, fy), height = wallHeight * fy;
            float opening = gateWidth * fx;
            string texturePath = AssetRoot + "/AP_HT_Boundary_WallSurface.png";
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少补丁材质图片，请把补丁的 Assets 文件夹合并到项目根目录。");
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
            Shader shader = Shader.Find("AlienPurge/ArenaV021/ContinuousWall");
            if (shader == null) throw new InvalidOperationException("找不到补丁 Shader，请等待导入完成，并检查 Console。");
            string generated = AssetRoot + "/Generated";
            if (!AssetDatabase.IsValidFolder(generated)) AssetDatabase.CreateFolder(AssetRoot, "Generated");

            // Each arc point is computed once; adjacent panels share the same coordinates and UV.
            Vector2[] points = new Vector2[Segments + 1];
            Vector2[] inner = new Vector2[points.Length], outer = new Vector2[points.Length];
            float[] distances = new float[points.Length];
            float gapAngle = Mathf.Asin(Mathf.Clamp(opening * 0.5f / rx, 0f, 0.99f));
            float start = -Mathf.PI * 0.5f + gapAngle;
            float sweep = 2f * Mathf.PI - 2f * gapAngle;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = start + sweep * i / Segments;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                points[i] = center + new Vector2(rx * c, ry * s);
                Vector2 normal = new Vector2(c / rx, s / ry).normalized;
                inner[i] = points[i] - normal * width * 0.5f;
                outer[i] = points[i] + normal * width * 0.5f;
                if (i > 0)
                    distances[i] = distances[i - 1] + arena.TransformVector((Vector3)(points[i] - points[i - 1])).magnitude;
            }
            int repeats = Mathf.Max(2, Mathf.RoundToInt(distances[Segments] / 4f) * 2);
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Vector2 up = Vector2.up * height;
            Color face = new Color(0.62f, 0.64f, 0.63f, 1f);
            Color top = new Color(0.92f, 0.94f, 0.93f, 1f);
            for (int i = 0; i < Segments; i++)
            {
                float u0 = distances[i] / distances[Segments] * repeats;
                float u1 = distances[i + 1] / distances[Segments] * repeats;
                AddQuad(vertices, uv, colors, triangles, inner[i], inner[i + 1], inner[i + 1] + up, inner[i] + up, u0, u1, face);
                AddQuad(vertices, uv, colors, triangles, outer[i], outer[i + 1], outer[i + 1] + up, outer[i] + up, u0, u1, face);
            }
            // Caps draw after faces for the fixed south-facing 2.5D camera.
            for (int i = 0; i < Segments; i++)
            {
                float u0 = distances[i] / distances[Segments] * repeats;
                float u1 = distances[i + 1] / distances[Segments] * repeats;
                AddQuad(vertices, uv, colors, triangles, inner[i] + up, inner[i + 1] + up, outer[i + 1] + up, outer[i] + up, u0, u1, top);
            }
            // Fixed-screen vertical posts and a fine bronze rail echo the existing containment walls.
            float railRise = height + 0.38f * fy, railHalf = 0.04f * fy;
            for (int i = 0; i <= Segments; i += Segments / 16)
            {
                Vector2 p = inner[i];
                Vector2 left = p - Vector2.right * 0.13f * fx, right = p + Vector2.right * 0.13f * fx;
                AddQuad(vertices, uv, colors, triangles, left, right, right + Vector2.up * (railRise + 0.1f * fy), left + Vector2.up * (railRise + 0.1f * fy), 0f, 1f, face);
            }
            Color bronze = new Color(0.50f, 0.33f, 0.13f, 1f);
            for (int i = 0; i < Segments; i++)
            {
                Vector2 a = inner[i] + Vector2.up * railRise, b = inner[i + 1] + Vector2.up * railRise;
                AddQuad(vertices, uv, colors, triangles, a - Vector2.up * railHalf, b - Vector2.up * railHalf, b + Vector2.up * railHalf, a + Vector2.up * railHalf,
                    distances[i] / distances[Segments] * repeats, distances[i + 1] / distances[Segments] * repeats, bronze);
            }
            Mesh mesh = new Mesh { name = "ArenaV021_ContinuousWall" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(generated + "/ContinuousWall.asset"));
            Material material = new Material(shader) { name = "ArenaV021_Wall" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(generated + "/ContinuousWall.mat"));

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair Arena Continuous Boundary");
            Transform old = arena.Find(RepairName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            GameObject boundary = new GameObject(RepairName);
            Undo.RegisterCreatedObjectUndo(boundary, "Create Continuous Boundary");
            boundary.transform.SetParent(arena, false);
            boundary.layer = floor.gameObject.layer;
            boundary.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = boundary.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            renderer.sortingLayerID = floorRenderer != null ? floorRenderer.sortingLayerID : 0;
            int floorOrder = floorRenderer != null ? floorRenderer.sortingOrder : -200;
            int minBuildingOrder = int.MaxValue;
            foreach (SpriteRenderer sprite in blocks.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.sortingLayerID == renderer.sortingLayerID) minBuildingOrder = Mathf.Min(minBuildingOrder, sprite.sortingOrder);
            renderer.sortingOrder = minBuildingOrder > floorOrder + 1 && minBuildingOrder < int.MaxValue
                ? floorOrder + (minBuildingOrder - floorOrder) / 2 : floorOrder + 1;
            if (addCollision)
            {
                EdgeCollider2D edge = boundary.AddComponent<EdgeCollider2D>();
                edge.points = points; edge.edgeRadius = width * 0.5f; edge.isTrigger = false;
                GameObject blocker = new GameObject("EntryBlocker_Optional");
                blocker.transform.SetParent(boundary.transform, false);
                blocker.layer = boundary.layer;
                blocker.transform.localPosition = center + new Vector2(0f, -ry);
                BoxCollider2D box = blocker.AddComponent<BoxCollider2D>();
                box.size = new Vector2(opening + width, width); box.isTrigger = false;
                blocker.SetActive(false); // Enable with the game's entrance-lock logic when needed.
            }
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);
            Selection.activeGameObject = boundary;
            status = "已补齐连续低墙，保留南侧入口。按 Ctrl+S 保存场景；原建筑、Boss、Player 均保持原位。";
        }

        static void AddQuad(List<Vector3> v, List<Vector2> uv, List<Color> colors, List<int> t,
            Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u0, float u1, Color color)
        {
            int first = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(u0, 0)); uv.Add(new Vector2(u1, 0));
            uv.Add(new Vector2(u1, 1)); uv.Add(new Vector2(u0, 1));
            for (int i = 0; i < 4; i++) colors.Add(color);
            t.Add(first); t.Add(first + 1); t.Add(first + 2);
            t.Add(first); t.Add(first + 2); t.Add(first + 3);
        }
    }
}
