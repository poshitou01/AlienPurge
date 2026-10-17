using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pathfinding;

namespace AlienPurge.Level01ConnectedV01.Editor
{
    public static class L01SceneValidation
    {
        public static string GeometryFingerprint(GameObject arena)
        {
            StringBuilder s=new StringBuilder();
            foreach(Transform t in arena.GetComponentsInChildren<Transform>(true))
            {
                s.Append(AnimationUtility.CalculateTransformPath(t,arena.transform)).Append('|');
                Append(s,t.position);Append(s,t.lossyScale);Append(s,t.eulerAngles);
                MeshFilter filter=t.GetComponent<MeshFilter>();
                if(filter!=null)s.Append(filter.sharedMesh!=null?AssetDatabase.GetAssetPath(filter.sharedMesh):"null").Append('|');
                foreach(Collider2D c in t.GetComponents<Collider2D>())
                {
                    s.Append(c.GetType().Name).Append('|');Append(s,c.offset);
                    BoxCollider2D box=c as BoxCollider2D;if(box!=null)Append(s,box.size);
                    PolygonCollider2D polygon=c as PolygonCollider2D;
                    if(polygon!=null)for(int i=0;i<polygon.pathCount;i++)foreach(Vector2 p in polygon.GetPath(i))Append(s,p);
                }
            }
            return s.ToString();
        }
        static void Append(StringBuilder s,Vector3 p)
        {
            s.Append(p.x.ToString("F4",CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("F4",CultureInfo.InvariantCulture)).Append(',').Append(p.z.ToString("F4",CultureInfo.InvariantCulture)).Append('|');
        }
        public static string Check(L01World world)
        {
            if(world==null)return "当前场景没有启用的 Level01_Connected_V01。";
            List<string> errors=new List<string>();
            if(world.existingArena==null)errors.Add("缺少原 Arena");
            if(world.player==null||world.footAnchor==null)errors.Add("Player / SortAnchor 未绑定");
            if(world.progression==null||world.progression.gates==null||world.progression.gates.Length!=7)errors.Add("G1～G7 门禁不完整");
            if(world.navigation==null||world.navigation.astar==null)errors.Add("A* 导航未绑定");
            Transform collision=world.transform.Find("Collision_Footprints");
            PolygonCollider2D solid=collision!=null?collision.GetComponent<PolygonCollider2D>():null;
            if(solid==null||solid.pathCount<50)errors.Add("边界实体碰撞缺失");
            Transform area=world.transform.Find("Spawn_Playable_Area");
            if(area==null||area.GetComponent<PolygonCollider2D>()==null)errors.Add("刷怪可行走区缺失");
            int cases=world.GetComponentsInChildren<L01LootVisual>(true).Length;
            if(world.Data!=null&&cases!=world.Data.loot.Length)errors.Add("容器数量不符："+cases);
            foreach(L01DepthSort sort in world.GetComponentsInChildren<L01DepthSort>(true))
                if(sort.target==null){errors.Add("有遮挡层 Renderer 未绑定");break;}
            if(world.progression!=null&&world.progression.gates!=null)foreach(L01Gate gate in world.progression.gates)
                if(gate==null||gate.blocker==null){errors.Add("有门未绑定实际碰撞体");break;}
            if(errors.Count>0)return "需要检查：\n"+string.Join("\n",errors);
            return "结构检查通过：6 个新区域 + 原 Arena；7 道门；"+cases+" 个容器；边界、脚点和前景已绑定。请 Play 验证移动、遮挡和任务。";
        }
    }
    [CustomEditor(typeof(L01CollisionAuthoring))]
    public sealed class L01CollisionAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();EditorGUILayout.PropertyField(serializedObject.FindProperty("solid"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("editPoints"),new GUIContent("显示脚点精调手柄"));
            EditorGUILayout.HelpBox("选择此物体后开启手柄，沿建筑落地边缘精调。相邻碰撞三角形的同一顶点会一起移动。原 Arena 的点不在此处编辑。",MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }
        void OnSceneGUI()
        {
            L01CollisionAuthoring author=(L01CollisionAuthoring)target;
            if(!author.editPoints||author.outlines==null)return;
            Handles.color=new Color(1,.35f,.45f);
            for(int ring=0;ring<author.outlines.Length;ring++)for(int i=0;i<author.outlines[ring].points.Length;i++)
            {
                Vector2 old=author.outlines[ring].points[i];
                if(Mathf.Abs(old.x)<36.05f&&Mathf.Abs(old.y)<24.05f)continue;
                Vector3 position=author.transform.TransformPoint(old);
                EditorGUI.BeginChangeCheck();
                Vector3 next=Handles.FreeMoveHandle(position,HandleUtility.GetHandleSize(position)*.035f,Vector3.zero,Handles.DotHandleCap);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(author,"Adjust Level01 foot boundary");Undo.RecordObject(author.solid,"Adjust Level01 foot boundary");
                    if(author.spawnArea!=null)Undo.RecordObject(author.spawnArea,"Adjust Level01 spawn boundary");
                    if(author.MovePoint(ring,i,author.transform.InverseTransformPoint(next))){EditorUtility.SetDirty(author);EditorUtility.SetDirty(author.solid);}
                }
            }
        }
    }
}
