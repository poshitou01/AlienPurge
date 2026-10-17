using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pathfinding;

namespace AlienPurge.Level01ConnectedV01.Editor
{
    // Operates on scene instances, including prefab instance overrides. This
    // command does not regenerate artwork, move objects or scan the A* graph.
    public static class L01SceneRepair
    {
        const string Operation="Repair copied Level01 scene references";

        [MenuItem("AlienPurge/Art/Level01 Connected V01 - Tools/Repair Current Scene References")]
        public static void MenuRepair()
        {
            try{Debug.Log(RepairCurrentScene());}
            catch(Exception e){Debug.LogError("Level01 修复未完成："+e.Message);}
        }

        static IEnumerable<T> InScene<T>(Scene scene) where T:Component
        {
            foreach(GameObject root in scene.GetRootGameObjects())
                foreach(T component in root.GetComponentsInChildren<T>(true))yield return component;
        }
        static bool InScene(Component component,Scene scene)
        {return component!=null&&!EditorUtility.IsPersistent(component)&&component.gameObject.scene==scene;}
        static bool InScene(GameObject value,Scene scene)
        {return value!=null&&!EditorUtility.IsPersistent(value)&&value.scene==scene;}
        static string Hierarchy(GameObject value)
        {return value.transform.parent!=null?Hierarchy(value.transform.parent.gameObject)+"/"+value.name:value.name;}
        static void SaveOverride(UnityEngine.Object value)
        {
            if(value==null)return;
            EditorUtility.SetDirty(value);
            if(PrefabUtility.IsPartOfPrefabInstance(value))PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
        static T Ensure<T>(GameObject root) where T:Component
        {
            T component=root.GetComponent<T>();
            return component!=null?component:Undo.AddComponent<T>(root);
        }
        static GameObject ArenaRoot(L01World world,Scene scene)
        {
            if(InScene(world.existingArena,scene)&&world.existingArena.gameObject.activeInHierarchy
                &&L01ArenaImport.Component(world.existingArena.gameObject)!=null)return world.existingArena.gameObject;
            // Only the selected object's ancestors count as an explicit choice;
            // selecting Environment must not pick an arbitrary child Arena.
            GameObject selected=Selection.activeGameObject;
            if(InScene(selected,scene))
                for(Transform t=selected.transform;t!=null;t=t.parent)
                    if(t.gameObject.activeInHierarchy&&(L01ArenaImport.Component(t.gameObject)!=null
                        ||L01ArenaImport.LooksLikeRoot(t.gameObject)))return t.gameObject;
            List<GameObject> candidates=InScene<Transform>(scene)
                .Where(t=>t.gameObject.activeInHierarchy&&(L01ArenaImport.Component(t.gameObject)!=null
                    ||L01ArenaImport.LooksLikeRoot(t.gameObject))).Select(t=>t.gameObject).Distinct().ToList();
            if(candidates.Count==1)return candidates[0];
            if(candidates.Count==0)throw new InvalidOperationException("当前副本没有启用的 Arena 根物体。请保留实际 Boss 场地，再运行此命令。");
            throw new InvalidOperationException("找到多个启用的 Arena："+string.Join("，",candidates.Select(Hierarchy))
                +"。请选择实际使用的 ArenaRoot 根物体再修复；工具不会替你删除或选择备用场地。");
        }

        public static string RepairCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play Mode。");
            Scene scene=SceneManager.GetActiveScene();
            if(!scene.IsValid()||!scene.isLoaded||string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("请先把当前副本保存为 Level_Connected_Test 等新场景。");
            L01World[] worlds=InScene<L01World>(scene).Where(w=>w.gameObject.activeInHierarchy).ToArray();
            if(worlds.Length!=1)throw new InvalidOperationException("当前场景需要且只能启用一个 L01World，实际找到 "+worlds.Length+" 个。");
            L01World world=worlds[0];
            if(scene.path==world.sourceArenaScene||scene.path==world.sourceGameplayScene
                ||scene.name=="HiveTyrant_Arena")
                throw new InvalidOperationException("这是原始场景。请打开已另存的 Level_Connected_Test 副本再修复。");
            if(world.Data==null)throw new InvalidOperationException("L01World 的 Layout Asset 缺失，请先指定 level01_layout。");
            GameObject player=InScene(world.player,scene)&&world.player.activeInHierarchy?world.player:null;
            if(player==null)
            {
                GameObject[] players=InScene<Transform>(scene).Where(t=>t.gameObject.activeInHierarchy&&t.CompareTag("Player"))
                    .Select(t=>t.gameObject).ToArray();
                if(players.Length!=1)throw new InvalidOperationException("请将当前场景实际使用的 Player 拖入 L01World.Player，再修复。");
                player=players[0];
            }
            GameObject boss=InScene(world.boss,scene)?world.boss:null;
            if(boss==null)
            {
                MonoBehaviour motor=L01ProjectAccess.Find(scene,"HiveTyrantBossMotor");
                if(motor!=null)boss=motor.gameObject;
            }
            GameObject arena=ArenaRoot(world,scene);
            L01ArenaImport.Preflight(arena);
            float width=L01ArenaImport.ReadWidth(arena);
            AstarPath[] activeAstar=InScene<AstarPath>(scene).Where(a=>a.isActiveAndEnabled).ToArray();
            if(activeAstar.Length>1)throw new InvalidOperationException("当前副本启用了多个 AstarPath。请保留实际使用的一套导航后再修复。");
            MonoBehaviour mission=L01ProjectAccess.Find(scene,"MissionManager");

            Undo.IncrementCurrentGroup();int undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(Operation);
            try
            {
                Undo.RecordObject(world,Operation);
                world.player=player;world.boss=boss;
                world.footAnchor=L01ProjectAccess.Feet(player);
                world.existingArena=L01ArenaImport.ConnectCopy(arena,width,true);
                world.progression=Ensure<L01Progression>(world.gameObject);
                world.arenaBridge=Ensure<L01ArenaBridge>(world.gameObject);
                world.navigation=Ensure<L01Navigation>(world.gameObject);
                Undo.RecordObjects(new UnityEngine.Object[]{world.progression,world.arenaBridge,world.navigation},Operation);
                world.progression.world=world;world.progression.missionManager=mission;
                world.progression.gates=world.GetComponentsInChildren<L01Gate>(true);
                world.arenaBridge.world=world;world.navigation.world=world;
                // Drop stale external health references left by saving only the
                // map root as a prefab. The bridge resolves this scene's Boss in Awake.
                world.arenaBridge.bossHealth=null;world.arenaBridge.bossMotor=null;
                foreach(L01Gate gate in world.progression.gates)
                {
                    Undo.RecordObject(gate,Operation);gate.world=world;
                    if(gate.gateId=="G6")
                    {gate.externalArena=true;gate.blocker=L01ArenaImport.EntryBlocker(arena);}
                    SaveOverride(gate);
                }
                foreach(L01Terminal terminal in world.GetComponentsInChildren<L01Terminal>(true))
                {Undo.RecordObject(terminal,Operation);terminal.progression=world.progression;SaveOverride(terminal);}
                foreach(L01SafeZoneBridge bridge in world.GetComponentsInChildren<L01SafeZoneBridge>(true))
                {
                    Undo.RecordObject(bridge,Operation);bridge.world=world;
                    bridge.safeZone=L01ProjectAccess.Find(scene,"SafeZoneController");
                    Transform area=world.transform.Find("Spawn_Playable_Area");
                    if(area!=null)bridge.playableArea=area.GetComponent<PolygonCollider2D>();
                    SaveOverride(bridge);
                }
                foreach(MonoBehaviour component in world.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component!=null&&component.GetType().Name=="CargoYardMissionEntryGate")
                    {
                        Undo.RecordObject(component,Operation);
                        L01ProjectAccess.Set(component,"missionManager",mission);
                        L01ProjectAccess.Set(component,"playerRoot",player.transform);SaveOverride(component);
                    }
                AstarPath astar=activeAstar.Length==1?activeAstar[0]:null;
                if(astar==null)
                {
                    GameObject root=new GameObject("AstarPath_Level01");SceneManager.MoveGameObjectToScene(root,scene);
                    Undo.RegisterCreatedObjectUndo(root,Operation);astar=Undo.AddComponent<AstarPath>(root);
                }
                Undo.RecordObject(astar,Operation);
                astar.showNavGraphs=false;astar.showUnwalkableNodes=false;astar.scanOnStartup=false;
                if(astar.data==null)astar.data=new AstarData();
                astar.data.cacheStartup=false;
                world.navigation.astar=astar;world.navigation.scanOnStart=true;
                SaveOverride(astar);SaveOverride(world.existingArena);SaveOverride(world);
                SaveOverride(world.progression);SaveOverride(world.arenaBridge);SaveOverride(world.navigation);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch{Undo.RevertAllDownToGroup(undoGroup);throw;}
            // Scene-view drawing is an editor preference, separate from the
            // gameplay colliders and navigation graph. Re-enable via Gizmos.
            foreach(SceneView view in SceneView.sceneViews)view.drawGizmos=false;
            Selection.activeGameObject=null;SceneView.RepaintAll();
            string result="Fix05 已修复当前副本："+scene.name+"\nExisting Arena = "+Hierarchy(arena)+" / "+world.existingArena.GetType().Name
                +"\nPlayer = "+player.name+"；Mission Manager = "+(mission!=null?mission.name:"未找到，请检查玩法管理器")
                +"\n已关闭 Scene/A* 调试绘制；未重建地图、未移动物体、未扫描导航。按 Ctrl+S 保存，再测试 Play。";
            if(boss==null)result+="\n未找到 Hive Tyrant，请检查 L01World.Boss。";
            if(mission!=null&&!mission.isActiveAndEnabled)result+="\nMission Manager 当前未启用，请检查其物体与组件。";
            return result;
        }
    }
}
