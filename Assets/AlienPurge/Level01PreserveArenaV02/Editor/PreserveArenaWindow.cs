using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.PreserveArenaV02.Editor
{
    public sealed class PreserveArenaWindow:EditorWindow
    {
        const string Root="Assets/AlienPurge/Level01PreserveArenaV02";
        const string BuilderType="AlienPurge.Level01ConnectedV01.Editor.L01WorldBuilderWindow";
        SceneAsset gameplay,arenaSource;
        bool previewDoors;
        Scene sourceCopy;
        GameObject[] arenas=new GameObject[0];
        MonoBehaviour[] bosses=new MonoBehaviour[0];
        int arenaIndex,bossIndex;
        string run,gameplayCopyPath,arenaCopyPath,status="读取你昨天确认好的实际 Arena Scene，在全新的场景副本中组装关卡。现有场景与启动入口保留。";
        Dictionary<string,string> originals;
        SceneSetup[] originalSetup;
        EditorBuildSettingsScene[] originalBuild;
        Vector2 scroll;

        [MenuItem("AlienPurge/Art/Level01 - Preserve Final Arena V02")]
        public static void Open(){GetWindow<PreserveArenaWindow>("Preserve Final Arena V02");}
        void OnEnable()
        {
            minSize=new Vector2(540,530);
            gameplay=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Day01_TestGround.unity");
            arenaSource=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/HiveTyrant_Arena.unity");
        }
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label("Level01 · 保留最终 Arena · V02",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("只添加新副本；不清理旧 Scene、不覆盖原文件、不重建 Arena 碰撞、不修改 Build Settings。先手动保存你确认正确的来源 Scene。",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            gameplay=(SceneAsset)EditorGUILayout.ObjectField("原玩法 Scene（管理器 / UI）",gameplay,typeof(SceneAsset),false);
            arenaSource=(SceneAsset)EditorGUILayout.ObjectField("昨天最终 Arena 所在 Scene",arenaSource,typeof(SceneAsset),false);
            if(EditorGUI.EndChangeCheck()){arenas=new GameObject[0];bosses=new MonoBehaviour[0];}
            bool idle=!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling;
            GUI.enabled=idle;
            if(GUILayout.Button("1．读取来源副本（原 Scene 保留）",GUILayout.Height(36)))Run(Prepare);
            GUI.enabled=true;
            if(arenas.Length>0&&arenas.All(a=>a!=null)&&sourceCopy.IsValid()&&sourceCopy.isLoaded)
            {
                arenaIndex=EditorGUILayout.Popup("要保留的 Arena",Mathf.Clamp(arenaIndex,0,arenas.Length-1),arenas.Select(a=>PreservedArenaData.PathOf(a.transform)).ToArray());
                EditorGUILayout.HelpBox(PreservedArenaData.Summary(arenas[arenaIndex]),MessageType.None);
                if(bosses.Length>0)
                    bossIndex=EditorGUILayout.Popup("此 Arena 使用的 Boss",Mathf.Clamp(bossIndex,0,bosses.Length-1),bosses.Select(b=>PreservedArenaData.PathOf(b.transform)).ToArray());
                else EditorGUILayout.HelpBox("来源 Scene 没有找到 HiveTyrantBossMotor。请选含实际 Boss 的最终 Arena Scene。",MessageType.Warning);
                previewDoors=EditorGUILayout.Toggle("测试用：全部探索门打开",previewDoors);
                GUI.enabled=idle&&bosses.Length>0;
                if(GUILayout.Button("2．保留此 Arena，生成完整新 Scene",GUILayout.Height(42)))Run(Assemble);
                GUI.enabled=true;
            }
            EditorGUILayout.HelpBox(status,MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        void Run(Action action)
        {try{action();}catch(Exception e){status="未完成："+e.Message;Debug.LogException(e);}finally{EditorUtility.ClearProgressBar();}}
        static void RequireSavedScenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play。");
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("请先退出 Prefab 编辑模式。");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                Scene s=SceneManager.GetSceneAt(i);
                if(s.isDirty||string.IsNullOrEmpty(s.path))throw new InvalidOperationException("请先手动保存或另存当前 Scene："+s.name+"。此入口不会替你保存任何已有场景。");
            }
        }
        void Prepare()
        {
            RequireSavedScenes();
            if(gameplay==null||arenaSource==null)throw new InvalidOperationException("请指定原玩法 Scene 和昨天最终 Arena 所在的 .unity 场景文件。");
            if(PreservedArenaData.FindType(BuilderType)==null)throw new InvalidOperationException("缺少 Level01ConnectedV01 工具。请保留原素材目录。");
            originals=PreservedArenaData.SceneHashes();originalSetup=EditorSceneManager.GetSceneManagerSetup();originalBuild=EditorBuildSettings.scenes;
            PreservedArenaData.Folder(Root+"/Generated");
            run=AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated/Assembly_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            PreservedArenaData.Folder(run);
            gameplayCopyPath=run+"/Gameplay_Copy.unity";arenaCopyPath=run+"/Arena_Final_Copy.unity";
            try
            {
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(gameplay),gameplayCopyPath)
                ||!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(arenaSource),arenaCopyPath))
                throw new InvalidOperationException("来源文件复制失败，原 Scene 未覆盖。");
            Scene gameCopy=EditorSceneManager.OpenScene(gameplayCopyPath,OpenSceneMode.Single);
            PreserveGameplayServices(gameCopy);
            // Old Bosses in the gameplay template stay in this new copy only;
            // the selected Arena copy supplies the actual Boss for the new map.
            foreach(MonoBehaviour m in PreservedArenaData.Components<MonoBehaviour>(gameCopy))
                if(m!=null&&m.GetType().Name=="HiveTyrantBossMotor")m.gameObject.SetActive(false);
            if(!EditorSceneManager.SaveScene(gameCopy))throw new InvalidOperationException("新玩法副本保存失败。");
            sourceCopy=EditorSceneManager.OpenScene(arenaCopyPath,OpenSceneMode.Single);
            arenas=PreservedArenaData.Arenas(sourceCopy);
            bosses=PreservedArenaData.Components<MonoBehaviour>(sourceCopy).Where(m=>m!=null&&m.GetType().Name=="HiveTyrantBossMotor").ToArray();
            arenaIndex=0;bossIndex=0;
            if(arenas.Length==0)throw new InvalidOperationException("这个 Scene 副本中没有找到连续 Arena。请指定昨天正常的场景文件，不能选空白 Prefab。");
            PreservedArenaData.AssertUnchanged(originals);
            status="已打开来源副本："+arenaCopyPath+"\n请在下拉框选择昨天确认的 Arena 和 Boss。这里只查看/修改新副本，原 Scene 文件保持不变。";
            }
            catch{RestoreOriginalSetup();arenas=new GameObject[0];bosses=new MonoBehaviour[0];throw;}
        }
        static void PreserveGameplayServices(Scene scene)
        {
            string[] names={"PlanetStormController","SafeZoneController","ToxicFogRouteController","ToxicFogAreaController","EnemySpawner","MissionManager","ExtractionController","AstarPath","GameManager","CameraFollow"};
            foreach(MonoBehaviour m in PreservedArenaData.Components<MonoBehaviour>(scene).ToArray())
            {
                if(m==null||!names.Contains(m.GetType().Name))continue;
                Transform keep=m.transform;bool oldArt=false;
                for(Transform p=m.transform.parent;p!=null;p=p.parent)
                {if(p.name=="StormSystem")keep=p;if(p.name=="Environment"||p.name.StartsWith("ArenaRoot_",StringComparison.Ordinal)){oldArt=true;break;}}
                if(oldArt)DetachInCopy(keep);
            }
            foreach(Transform t in PreservedArenaData.Components<Transform>(scene).ToArray())
                if(t.CompareTag("Player")||t.GetComponent<Camera>()!=null)
                    for(Transform p=t.parent;p!=null;p=p.parent)
                        if(p.name=="Environment"||p.name.StartsWith("ArenaRoot_",StringComparison.Ordinal)){DetachInCopy(t);break;}
        }
        static void DetachInCopy(Transform t)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
            {
                GameObject outer=PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if(outer!=null)PrefabUtility.UnpackPrefabInstance(outer,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            }
            t.SetParent(null,true);
        }
        void RestoreOriginalSetup()
        {
            if(originalSetup!=null&&originalSetup.Length>0)
            {try{EditorSceneManager.RestoreSceneManagerSetup(originalSetup);}catch(Exception e){Debug.LogWarning(e.Message);}}
        }
        void Assemble()
        {
            if(!sourceCopy.IsValid()||!sourceCopy.isLoaded||arenas.Length==0)throw new InvalidOperationException("请先读取来源副本。");
            PreservedArenaData.AssertUnchanged(originals);
            GameObject arena=arenas[arenaIndex];PreservedArenaData.Validate(arena);
            MonoBehaviour boss=bosses[bossIndex];
            if(boss==null)throw new InvalidOperationException("Boss 来源已失效，请重新读取。");
            foreach(MonoBehaviour m in arena.GetComponentsInChildren<MonoBehaviour>(true))
                if(m!=null&&m.GetType().Name=="ArenaV03KinematicWallGuard")
                {
                    Rigidbody2D body=PreservedArenaData.Get(m,"body") as Rigidbody2D;
                    if(body!=null&&body.transform!=boss.transform&&!body.transform.IsChildOf(boss.transform))
                        throw new InvalidOperationException("所选 Boss 与这个 Arena 已保存的墙体防穿透引用不一致。请在来源副本中选择与它对应的 Boss，不会改写原引用。");
                }
            // Avoid importing a second Player/camera when a user parented these
            // under their Arena. Only the new source copy is reparented.
            foreach(Transform t in PreservedArenaData.Components<Transform>(sourceCopy).ToArray())
                if(t!=arena.transform&&t.IsChildOf(arena.transform)&&(t.CompareTag("Player")||t.GetComponent<Camera>()!=null))DetachInCopy(t);
            foreach(MonoBehaviour m in bosses)if(m!=null&&m!=boss)m.gameObject.SetActive(false);
            for(Transform t=boss.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
            for(Transform t=arena.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
            string signature=PreservedArenaData.ColliderSignature(arena);
            string summary=PreservedArenaData.Summary(arena);
            PreservedArenaData.FootSnapshot feet=PreservedArenaData.FootSnapshot.Capture(arena);
            Vector3 position=arena.transform.position,scale=arena.transform.lossyScale;
            Quaternion rotation=arena.transform.rotation;
            // Only save our own scene copy; never invoke a save on the original.
            EditorSceneManager.MarkSceneDirty(sourceCopy);
            if(!EditorSceneManager.SaveScene(sourceCopy))throw new InvalidOperationException("新来源副本保存失败。");
            EditorWindow builder=null;
            try
            {
                builder=ScriptableObject.CreateInstance(PreservedArenaData.FindType(BuilderType)) as EditorWindow;
                PreservedArenaData.Set(builder,"gameplayScene",AssetDatabase.LoadAssetAtPath<SceneAsset>(gameplayCopyPath));
                PreservedArenaData.Set(builder,"arenaScene",AssetDatabase.LoadAssetAtPath<SceneAsset>(arenaCopyPath));
                PreservedArenaData.Set(builder,"arenaInScene",arena);
                PreservedArenaData.Set(builder,"previewDoors",previewDoors);
                PreservedArenaData.Set(builder,"registerBuildEntry",false);
                PreservedArenaData.Build(builder);
                Scene final=SceneManager.GetActiveScene();
                MonoBehaviour world=PreservedArenaData.Mono(final,"L01World");
                MonoBehaviour imported=PreservedArenaData.Get(world,"existingArena") as MonoBehaviour;
                if(imported==null||string.IsNullOrEmpty(final.path)||final.path==arenaCopyPath||final.path==gameplayCopyPath)throw new InvalidOperationException("完整新 Scene 未成功生成。");
                bool shapes=signature==PreservedArenaData.ColliderSignature(imported.gameObject);
                bool placement=(position-imported.transform.position).sqrMagnitude<.00000001f
                    &&(scale-imported.transform.lossyScale).sqrMagnitude<.00000001f&&Quaternion.Angle(rotation,imported.transform.rotation)<.001f;
                bool feetMatched=feet.Matches(PreservedArenaData.Get(world,"player") as GameObject,PreservedArenaData.Get(world,"footAnchor") as Transform);
                if(!shapes||!placement||!feetMatched)throw new InvalidOperationException("Arena 碰撞、位置或 Player 脚部尺寸/脚锚偏移校验不一致，不能作为已保留最终版交付。原 Scene 仍保留。请核对玩法模板的 Player 缩放和 SortAnchor。");
                PreservedArenaData.AssertUnchanged(originals);
                foreach(MonoBehaviour m in PreservedArenaData.Components<MonoBehaviour>(final))
                    if(m!=null&&m.GetType().Name=="AstarPath")
                    {PreservedArenaData.Set(m,"showNavGraphs",false);PreservedArenaData.Set(m,"showUnwalkableNodes",false);EditorUtility.SetDirty(m);}
                foreach(SceneView view in SceneView.sceneViews)view.drawGizmos=false;
                Selection.activeGameObject=null;
                if(!EditorSceneManager.SaveScene(final))throw new InvalidOperationException("完整新 Scene 保存失败。");
                File.WriteAllText(PreservedArenaData.DiskPath(run+"/Preservation_Report.json"),JsonUtility.ToJson(new Report{
                    gameplaySource=AssetDatabase.GetAssetPath(gameplay),arenaSource=AssetDatabase.GetAssetPath(arenaSource),
                    completeScene=final.path,sourceColliderSummary=summary,colliderSignature=signature,sourceFeet=feet.description,
                    colliderMatched=shapes,worldPlacementMatched=placement,feetMatched=feetMatched,oldSceneFilesVerified=originals.Count,buildSettingsChanged=false},true));
                AssetDatabase.Refresh();
                status="已生成完整新 Scene："+final.path+"\nCollider 数据与所选来源副本一致；原有 "+originals.Count+" 份 Scene 文件校验未变。\n"+summary
                    +"\nPlayer 脚部尺寸与脚锚偏移校验一致。主菜单入口未改。本次直接在新 Scene 按 Play 测试。";
                Debug.Log(status);
                arenas=new GameObject[0];bosses=new MonoBehaviour[0];
            }
            catch
            {
                RestoreOriginalSetup();
                throw;
            }
            finally
            {
                if(originalBuild!=null&&!SameBuildSettings(originalBuild,EditorBuildSettings.scenes))EditorBuildSettings.scenes=originalBuild;
                if(builder!=null)DestroyImmediate(builder);
            }
        }
        static bool SameBuildSettings(EditorBuildSettingsScene[] a,EditorBuildSettingsScene[] b)
        {return a.Length==b.Length&&a.Select((s,i)=>s.path==b[i].path&&s.enabled==b[i].enabled).All(v=>v);}
        [Serializable]sealed class Report
        {
            public string gameplaySource,arenaSource,completeScene,sourceColliderSummary,colliderSignature,sourceFeet;
            public bool colliderMatched,worldPlacementMatched,feetMatched,buildSettingsChanged;
            public int oldSceneFilesVerified;
        }
    }
}
