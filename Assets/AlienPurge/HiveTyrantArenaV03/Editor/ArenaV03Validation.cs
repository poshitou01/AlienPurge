using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AlienPurge.ArenaArtV03.Editor
{
    public static class ArenaV03Validation
    {
        public static string Check(HiveTyrantArenaV03 arena)
        {
            List<string> problems = new List<string>();
            if (arena == null) return "没有 V03 根物体。";
            MeshFilter[] meshes = arena.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshes)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) { problems.Add("网格缺失：" + mf.name); continue; }
                Vector3[] vertices = mesh.vertices; Vector2[] uv = mesh.uv;
                if (vertices.Length != uv.Length) { problems.Add("UV 数量错误：" + mf.name); continue; }
                foreach (int index in mesh.triangles)
                    if (index < 0 || index >= vertices.Length) { problems.Add("三角形索引错误：" + mf.name); break; }
                float u = arena.worldCanvasWidth / 1536f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 expected = new Vector3((uv[i].x * 1536f - 768f) * u, (uv[i].y * 1024f - 512f) * u, 0f);
                    if ((vertices[i] - expected).sqrMagnitude > .00001f)
                    { problems.Add("共享画布坐标错位：" + mf.name); break; }
                }
                MeshRenderer renderer = mf.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null || !renderer.sharedMaterial.shader.isSupported)
                    problems.Add("材质 / Shader 不可用：" + mf.name);
            }
            ArenaV03Boundary wall = arena.GetComponentInChildren<ArenaV03Boundary>();
            if (wall == null) problems.Add("实体边界缺失。");
            else
            {
                PolygonCollider2D collider = wall.GetComponent<PolygonCollider2D>();
                if (collider == null || !collider.enabled || collider.isTrigger || collider.GetTotalPointCount() < 8 || collider.shapeCount == 0)
                    problems.Add("边界 Collider2D 未正确启用。");
                if (collider != null && arena.broodPoints != null)
                    foreach (Transform t in arena.broodPoints)
                        if (t != null && collider.OverlapPoint(t.position)) problems.Add("召唤点位于实体墙内：" + t.name);
            }
            if (arena.broodPoints == null || arena.broodPoints.Length != 4) problems.Add("召唤点应为四个。");
            if (arena.entryBlocker == null || arena.entryClosedLeaf == null) problems.Add("入口门组件不完整。");
            else if (arena.entryBlocker.enabled != arena.entryClosedLeaf.activeSelf) problems.Add("门的显示和碰撞状态不一致。");
            int layer = wall != null ? wall.gameObject.layer : 0;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && Physics2D.GetIgnoreLayerCollision(layer, player.layer)) problems.Add("项目碰撞矩阵忽略了 Player 与边界的碰撞。");
            if (player != null && Camera.main != null && (Camera.main.cullingMask & (1 << arena.gameObject.layer)) == 0)
                problems.Add("主相机没有渲染 Arena 所在 Layer。");
            if (problems.Count > 0)
            {
                string message = string.Join("\n", problems.ToArray()); Debug.LogWarning("Arena V03 检查：\n" + message, arena); return message;
            }
            string success = "结构检查通过：连续画布、" + meshes.Length + " 个图层网格、实体边界、4 个召唤点及门状态正常。遮挡和移动仍以 Play 实测为准。";
            Debug.Log(success, arena); return success;
        }
    }

    [CustomEditor(typeof(ArenaV03Boundary))]
    public class ArenaV03BoundaryInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("按轮廓重建实体 Collider"))
            {
                ArenaV03Boundary boundary = (ArenaV03Boundary)target;
                Undo.RecordObject(boundary.GetComponent<PolygonCollider2D>(), "Rebuild Arena Collider");
                boundary.RebuildCollider(); EditorUtility.SetDirty(boundary);
            }
        }
        void OnSceneGUI()
        {
            ArenaV03Boundary b = (ArenaV03Boundary)target;
            if (!b.showAuthoringOutline || b.footprint == null) return;
            Handles.color = new Color(1f, .3f, .5f);
            for (int i = 0; i < b.footprint.Length; i++)
            {
                Vector3 world = b.transform.TransformPoint(b.footprint[i]);
                EditorGUI.BeginChangeCheck();
                Vector3 changed = Handles.FreeMoveHandle(world, HandleUtility.GetHandleSize(world) * .035f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(b, "Adjust Arena Footprint");
                    Undo.RecordObject(b.GetComponent<PolygonCollider2D>(), "Adjust Arena Footprint");
                    Vector3 local = b.transform.InverseTransformPoint(changed);
                    b.footprint[i] = new Vector2(local.x, local.y);
                    b.RebuildCollider(); EditorUtility.SetDirty(b);
                }
            }
        }
    }

    [CustomEditor(typeof(HiveTyrantArenaV03))]
    public class HiveTyrantArenaV03Inspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            HiveTyrantArenaV03 arena = (HiveTyrantArenaV03)target;
            EditorGUILayout.Space();
            if (GUILayout.Button(arena.EntranceLocked ? "打开入口（图像 + Collider）" : "关闭入口（图像 + Collider）"))
            {
                Undo.RecordObject(arena, "Change Arena Entrance");
                if (arena.entryClosedLeaf != null) Undo.RecordObject(arena.entryClosedLeaf, "Change Arena Entrance");
                if (arena.entryBlocker != null) Undo.RecordObject(arena.entryBlocker, "Change Arena Entrance");
                arena.SetEntranceLocked(!arena.EntranceLocked); EditorUtility.SetDirty(arena);
            }
            if (GUILayout.Button("检查场景结构")) ArenaV03Validation.Check(arena);
        }
    }
}
