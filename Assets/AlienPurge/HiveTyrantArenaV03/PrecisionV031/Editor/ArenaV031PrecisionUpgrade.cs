using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using AlienPurge.ArenaArtV03;

namespace AlienPurge.ArenaArtV031.Editor
{
    public static class ArenaV031PrecisionUpgrade
    {
        const string Action = "Refine Arena V03.1 Feet and Boundary";
        const string DataPath = "Assets/AlienPurge/HiveTyrantArenaV03/PrecisionV031/fine_layout.json";

        public static void Apply(GameObject root, GameObject player, float footWidth = 0f, float footHeight = 0f, bool bindFeet = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后应用。");
            HiveTyrantArenaV03 arena = root != null ? root.GetComponent<HiveTyrantArenaV03>() : null;
            if (arena == null || EditorUtility.IsPersistent(root) || !root.scene.IsValid())
                throw new InvalidOperationException("请选择场景里的 ArenaRoot_Continuous_V03。");
            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0) throw new InvalidOperationException("项目缺少 Environment 物理层；此工具使用 AlienPurge 现有层配置。");
            Vector3 scale = root.transform.lossyScale;
            if (scale.x <= 0f || scale.y <= 0f || Mathf.Abs(scale.x-scale.y) > .01f*Mathf.Max(scale.x,scale.y))
                throw new InvalidOperationException("Arena 的 X/Y 缩放需要保持正数且一致。");
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            if (text == null) throw new InvalidOperationException("缺少 fine_layout.json，请保留完整包内的目录结构。");
            ArenaV03Layout layout = JsonUtility.FromJson<ArenaV03Layout>(text.text);
            if (layout == null || layout.boundaryPoints == null || layout.boundaryPoints.Length < 20)
                throw new InvalidOperationException("精细轮廓数据无法读取。");
            float width = arena.worldCanvasWidth;
            bool locked = arena.EntranceLocked;
            Transform oldCollision = root.transform.Find("Collision_Wall_Footprints");
            Collider2D oldCollider = oldCollision != null ? oldCollision.GetComponentInChildren<Collider2D>(true) : null;
            PhysicsMaterial2D material = oldCollider != null ? oldCollider.sharedMaterial : null;
            if (oldCollision != null) Backup(oldCollision);
            Transform collision = Child(root.transform,"Collision_Wall_Footprints");
            collision.gameObject.layer = environmentLayer;
            GameObject wall = Child(collision,"Solid_Perimeter_And_Pods").gameObject;
            wall.layer = environmentLayer;
            ArenaV031SolidBoundary boundary = wall.AddComponent<ArenaV031SolidBoundary>();
            boundary.footprint = new Vector2[layout.boundaryPoints.Length];
            for (int i=0;i<boundary.footprint.Length;i++) boundary.footprint[i] = ArenaV03Geometry.LocalPoint(layout.boundaryPoints[i].Value,width);
            boundary.canvasHalfSize = new Vector2(width*.5f,width/3f);
            boundary.outsideMargin = width*24f/1536f;
            boundary.RebuildSolid();
            wall.GetComponent<PolygonCollider2D>().sharedMaterial = material;
            GameObject gate = Child(collision,"Entry_Blocker").gameObject;
            gate.layer = environmentLayer;
            gate.transform.localPosition = ArenaV03Geometry.LocalPoint(layout.entryBlocker.center.Value,width);
            BoxCollider2D gateCollider = gate.AddComponent<BoxCollider2D>();
            gateCollider.size = layout.entryBlocker.size.Value*(width/1536f);
            gateCollider.sharedMaterial = material;
            GameObject end = Child(collision,"Approach_End_Stop").gameObject;
            end.layer = environmentLayer;
            end.transform.localPosition = ArenaV03Geometry.LocalPoint(new Vector2(768,1033),width);
            BoxCollider2D endCollider = end.AddComponent<BoxCollider2D>();
            endCollider.size = new Vector2(320,30)*(width/1536f);
            endCollider.sharedMaterial = material;
            Undo.RecordObject(arena,Action);
            if (arena.entryClosedLeaf != null) Undo.RecordObject(arena.entryClosedLeaf,Action);
            arena.entryBlocker = gateCollider;
            arena.SetEntranceLocked(locked);
            foreach (ArenaV03KinematicWallGuard guard in root.GetComponentsInChildren<ArenaV03KinematicWallGuard>(true))
            {
                if (!guard.gameObject.activeInHierarchy) continue;
                Undo.RecordObject(guard,Action); guard.collisionRoot = collision;
            }
            ConfigureOcclusion(root, player);
            if (arena.exitReward != null)
            {
                Undo.RecordObject(arena.exitReward,Action);
                arena.exitReward.localPosition = ArenaV03Geometry.LocalPoint(new Vector2(1180,360),width);
            }
            if (arena.broodPoints != null)
                foreach (Transform point in arena.broodPoints)
                    if (point != null && point.name == "Brood_NE")
                    {
                        Undo.RecordObject(point,Action);
                        point.localPosition = ArenaV03Geometry.LocalPoint(new Vector2(1147,335),width);
                    }
            if (bindFeet) BindPlayerFeet(root,player,footWidth,footHeight);
            Physics2D.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        // Shared by clean builds and the optional scene maintenance tool.
        public static void ConfigureOcclusion(GameObject root, GameObject player)
        {
            Transform north = root.transform.Find("Foreground_Occlusion/North_Hive_Facade");
            if (north != null) { Undo.RecordObject(north.gameObject,Action); north.gameObject.SetActive(false); }
            // South structures are intentional foreground. A single constant Y=840
            // could switch them behind a player whose feet touch a lower wall base.
            int foregroundId = ForegroundLayer(player);
            foreach (string name in new string[] {"SouthWest_Wall_Glass_Hatchery","SouthEast_Wall_Glass_Hatchery"})
            {
                Transform t = root.transform.Find("Foreground_Occlusion/"+name);
                if (t == null) continue;
                ArenaV03YSort sorter = t.GetComponent<ArenaV03YSort>();
                if (sorter != null) { Undo.RecordObject(sorter,Action); sorter.enabled = false; }
                MeshRenderer renderer = t.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer,Action); renderer.sortingLayerID=foregroundId; renderer.sortingOrder=28000;
                }
            }
        }

        public static void BindPlayerFeet(GameObject root, GameObject player, float footWidth=0f, float footHeight=0f)
        {
            if (player == null) return;
            if (EditorUtility.IsPersistent(player) || player.scene != root.scene)
                throw new InvalidOperationException("Player 必须是当前 Arena 所在场景里的实例。");
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
                throw new InvalidOperationException("当前 Player 需要使用已有的 Dynamic Rigidbody2D 才能由脚底碰撞体阻挡。");
            Transform anchor = ResolveGroundAnchor(player);
            if (anchor == null) throw new InvalidOperationException("Player 缺少 SortAnchor 脚点，未改动角色碰撞。");
            int layer = LayerMask.NameToLayer("Environment");
            if (layer < 0) throw new InvalidOperationException("Environment 物理层不存在。");
            foreach (ArenaV031PlayerFeet prior in root.GetComponentsInChildren<ArenaV031PlayerFeet>(true))
            {
                if (!prior.gameObject.activeSelf) continue;
                RecordFilterObjects(prior);
                prior.Restore();
                Backup(prior.transform);
            }
            Transform footTransform = player.transform.Find("Arena_EnvironmentFeet_V031");
            if (footTransform == null) footTransform = Child(player.transform,"Arena_EnvironmentFeet_V031");
            Undo.RecordObject(footTransform,Action);
            Undo.RecordObject(footTransform.gameObject,Action);
            footTransform.gameObject.SetActive(true);
            footTransform.gameObject.layer = player.layer;
            CapsuleCollider2D foot = footTransform.GetComponent<CapsuleCollider2D>();
            if (foot == null) foot = Undo.AddComponent<CapsuleCollider2D>(footTransform.gameObject);
            Undo.RecordObject(foot,Action);
            List<ArenaBodyFilterSnapshot> snapshots = new List<ArenaBodyFilterSnapshot>();
            float bodyWidth = 0f;
            foreach (Collider2D c in player.GetComponentsInChildren<Collider2D>(true))
            {
                if (c == foot || !c.enabled || c.isTrigger || c.attachedRigidbody != body) continue;
                bodyWidth = Mathf.Max(bodyWidth,c.bounds.size.x);
                Undo.RecordObject(c,Action);
                snapshots.Add(new ArenaBodyFilterSnapshot {collider=c,includeLayers=c.includeLayers,excludeLayers=c.excludeLayers,priority=c.layerOverridePriority});
            }
            if (snapshots.Count==0) throw new InvalidOperationException("未找到 Player 原有的实体 Collider2D。");
            if (footWidth<=0f) footWidth=Mathf.Clamp(bodyWidth*.44f,.16f,1.5f);
            if (footHeight<=0f) footHeight=Mathf.Clamp(footWidth*.22f,.035f,.20f);
            footHeight=Mathf.Min(footHeight,footWidth*.5f);
            Vector3 actorScale=player.transform.lossyScale;
            float sx=Mathf.Max(.0001f,Mathf.Abs(actorScale.x)),sy=Mathf.Max(.0001f,Mathf.Abs(actorScale.y));
            footTransform.localRotation=Quaternion.identity;
            footTransform.localScale=Vector3.one;
            // Bottom of this short sole capsule, not body center, is the ground anchor.
            footTransform.position=anchor.position+Vector3.up*(footHeight*.5f);
            foot.direction=CapsuleDirection2D.Horizontal;
            foot.offset=Vector2.zero;
            foot.size=new Vector2(footWidth/sx,footHeight/sy);
            foot.isTrigger=false;
            foot.density=.0001f;
            Collider2D wall = root.transform.Find("Collision_Wall_Footprints").GetComponentInChildren<Collider2D>(true);
            if (wall!=null) foot.sharedMaterial=wall.sharedMaterial;
            GameObject link = Child(root.transform,"Player_Feet_Link_V031").gameObject;
            ArenaV031PlayerFeet binding=link.AddComponent<ArenaV031PlayerFeet>();
            binding.playerBody=body; binding.groundAnchor=anchor; binding.feet=foot;
            binding.environmentMask=1<<layer; binding.originalBodyFilters=snapshots.ToArray();
            binding.Apply();
            foreach (ArenaBodyFilterSnapshot state in snapshots)
                if (PrefabUtility.IsPartOfPrefabInstance(state.collider))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(state.collider);
            if (PrefabUtility.IsPartOfPrefabInstance(foot))
                PrefabUtility.RecordPrefabInstancePropertyModifications(foot);
            if (foot.attachedRigidbody!=body) throw new InvalidOperationException("脚底碰撞体未连接到 Player 的刚体。");
        }

        public static Transform ResolveGroundAnchor(GameObject player)
        {
            if (player==null)return null;
            foreach (MonoBehaviour m in player.GetComponentsInChildren<MonoBehaviour>(true))
                if (m!=null && m.GetType().Name=="YSortRenderer")
                {
                    SerializedProperty p=new SerializedObject(m).FindProperty("sortAnchor");
                    if (p!=null && p.objectReferenceValue!=null) return p.objectReferenceValue as Transform;
                }
            return player.transform.Find("SortAnchor");
        }

        static int ForegroundLayer(GameObject player)
        {
            int world=SortingLayer.NameToID("World");
            if (player!=null)
                foreach (SortingGroup group in player.GetComponentsInChildren<SortingGroup>(true))
                    if (group.name=="VisualRoot") {world=group.sortingLayerID;break;}
            foreach (SortingLayer layer in SortingLayer.layers)
                if (layer.name=="Foreground" && layer.value>SortingLayer.GetLayerValueFromID(world)) return layer.id;
            return world;
        }
        static void RecordFilterObjects(ArenaV031PlayerFeet binding)
        {
            Undo.RecordObject(binding,Action);
            if (binding.feet!=null)Undo.RecordObject(binding.feet,Action);
            if (binding.originalBodyFilters!=null)
                foreach (ArenaBodyFilterSnapshot s in binding.originalBodyFilters)
                    if (s!=null && s.collider!=null)Undo.RecordObject(s.collider,Action);
        }
        static void Backup(Transform t)
        {
            Undo.RecordObject(t.gameObject,Action);
            string prefix=t.name+"_Backup_V031";
            string name=prefix;int i=1;
            while(t.parent!=null && t.parent.Find(name)!=null)name=prefix+"_"+(i++);
            t.name=name;t.gameObject.SetActive(false);
        }
        static Transform Child(Transform parent,string name)
        {
            GameObject g=new GameObject(name);
            Undo.RegisterCreatedObjectUndo(g,Action);
            g.transform.SetParent(parent,false); return g.transform;
        }
    }

    public class ArenaV031PrecisionWindow:EditorWindow
    {
        GameObject arenaRoot,player;
        string status="修复当前 V03：贴合建筑的脚点边界、上侧背景、下侧遮挡与封闭右侧区域。";
        [MenuItem("AlienPurge/Art/Hive Tyrant Arena V03.1 - Tools/Refresh Boundary and Feet")]
        public static void Open(){GetWindow<ArenaV031PrecisionWindow>("Arena V03.1");}
        void OnEnable(){minSize=new Vector2(440,270);FindScene();}
        void FindScene()
        {
            if (Selection.activeGameObject!=null)
            {
                HiveTyrantArenaV03 a=Selection.activeGameObject.GetComponentInParent<HiveTyrantArenaV03>();
                if (a!=null)arenaRoot=a.gameObject;
            }
            if(arenaRoot==null){HiveTyrantArenaV03 a=FindObjectOfType<HiveTyrantArenaV03>();if(a!=null)arenaRoot=a.gameObject;}
            player=GameObject.FindGameObjectWithTag("Player");
        }
        void OnGUI()
        {
            GUILayout.Label("V03.1 · 脚点与精细边界",EditorStyles.boldLabel);
            arenaRoot=(GameObject)EditorGUILayout.ObjectField("当前 V03 Arena",arenaRoot,typeof(GameObject),true);
            player=(GameObject)EditorGUILayout.ObjectField("Player",player,typeof(GameObject),true);
            if(GUILayout.Button("重新识别场景"))FindScene();
            EditorGUILayout.HelpBox("不重建场景美术。旧碰撞会停用并保留；自动读取实际 SortAnchor，将环境接触交给脚底小碰撞体。",MessageType.Info);
            GUI.enabled=!EditorApplication.isPlayingOrWillChangePlaymode;
            if(GUILayout.Button("修复当前场景的边界、脚点和遮挡",GUILayout.Height(40)))
            {
                Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Arena V03.1 Precision Fix");
                try
                {
                    if(player==null)throw new InvalidOperationException("当前场景没有找到 Player，请指定场景里的 Player 根物体。");
                    ArenaV031PrecisionUpgrade.Apply(arenaRoot,player);
                    Physics2D.SyncTransforms();
                    status="已升级 V03.1：脚底接触环境，上方背景不挡身体，下方建筑前景遮挡，右侧内部封闭。请保存场景并 Play 检查。";
                    Undo.CollapseUndoOperations(undo);
                    Selection.activeGameObject=arenaRoot;
                }
                catch(Exception ex){Undo.RevertAllDownToGroup(undo);status=ex.Message;Debug.LogException(ex);}
            }
            GUI.enabled=true;EditorGUILayout.HelpBox(status,MessageType.None);
        }
    }

    [CustomEditor(typeof(ArenaV031SolidBoundary))]
    public class ArenaV031SolidBoundaryInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("编辑这一条脚点线即可；外侧实体区自动生成。不要在 PolygonCollider2D 里同时拖内外两圈。",MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showAuthoringOutline"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("footprint"),true);
            serializedObject.ApplyModifiedProperties();
            if(GUILayout.Button("按脚点线更新实体碰撞"))
            {
                ArenaV031SolidBoundary b=(ArenaV031SolidBoundary)target;
                Undo.RecordObject(b.GetComponent<PolygonCollider2D>(),"Update Foot Boundary");
                try{b.RebuildSolid();}catch(Exception ex){Debug.LogWarning(ex.Message,b);}
            }
        }
        void OnSceneGUI()
        {
            ArenaV031SolidBoundary b=(ArenaV031SolidBoundary)target;
            if(!b.showAuthoringOutline || b.footprint==null)return;
            Handles.color=new Color(1f,.22f,.4f);
            for(int i=0;i<b.footprint.Length;i++)
            {
                Vector3 position=b.transform.TransformPoint(b.footprint[i]);
                if(i>0)Handles.DrawLine(b.transform.TransformPoint(b.footprint[i-1]),position);
                EditorGUI.BeginChangeCheck();
                Vector3 next=Handles.FreeMoveHandle(position,HandleUtility.GetHandleSize(position)*.035f,Vector3.zero,Handles.DotHandleCap);
                if(EditorGUI.EndChangeCheck())
                {
                    Vector2 old=b.footprint[i];
                    Undo.RecordObject(b,"Adjust Foot Boundary");
                    Undo.RecordObject(b.GetComponent<PolygonCollider2D>(),"Adjust Foot Boundary");
                    b.footprint[i]=b.transform.InverseTransformPoint(next);
                    try{b.RebuildSolid();EditorUtility.SetDirty(b);}catch{b.footprint[i]=old;}
                }
            }
        }
    }
}
