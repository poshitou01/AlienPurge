using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;
using Pathfinding;

namespace AlienPurge.Level01ConnectedV01.Editor
{
    public sealed class L01WorldBuilderWindow : EditorWindow
    {
        // Fix05 adds in-place scene reference repair; Fix04 A* initialization is retained.
        const string Root="Assets/AlienPurge/Level01ConnectedV01";
        const string NativePrefabs="Assets/_Project/Prefabs/Loot/";
        const string Operation="Build AlienPurge Connected Level01";
        SceneAsset gameplayScene,arenaScene;
        GameObject arenaInScene;
        bool previewDoors,registerBuildEntry=true;
        string status="从原玩法场景创建副本，导入你现有的 Arena，再一次生成六个新区域、道路、门禁、碰撞、遮挡与容器。";
        Vector2 scroll;
        L01Layout layout;
        L01World world;
        L01Progression progression;
        string generated;
        string generatedOverride=string.Empty;
        Shader shader;
        Material ground,kit,gates,loot;
        PhysicsMaterial2D wallPhysics;
        int environmentLayer,interactableLayer,worldLayer,groundLayer,orderPerUnit=100,baseOrder,orderOffset;
        string preservedArenaFingerprint;
        readonly Dictionary<string,Mesh> quadCache=new Dictionary<string,Mesh>();

        [MenuItem("AlienPurge/Art/Level01 Connected V01 - Complete")]
        public static void Open(){GetWindow<L01WorldBuilderWindow>("Level01 Complete");}
        void OnEnable()
        {
            minSize=new Vector2(470,430);
            gameplayScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/Day01_TestGround.unity");
            arenaScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/HiveTyrant_Arena.unity");
            FindArena();
        }
        void FindArena()
        {
            Scene preferred=arenaScene!=null?SceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(arenaScene)):default(Scene);
            arenaInScene=L01ArenaImport.FindRoot(preferred);
            if(arenaInScene==null)arenaInScene=L01ArenaImport.FindRoot(SceneManager.GetActiveScene());
        }
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label("AlienPurge · Level01 完整场景 · Fix05",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("保留你现有 Arena 的图片、尺寸、位置、Collider 和遮挡结构。新场景使用独立目录；原场景资产不被覆盖。",MessageType.Info);
            gameplayScene=(SceneAsset)EditorGUILayout.ObjectField("原玩法场景（管理器/UI）",gameplayScene,typeof(SceneAsset),false);
            arenaScene=(SceneAsset)EditorGUILayout.ObjectField("现有 Arena 场景",arenaScene,typeof(SceneAsset),false);
            arenaInScene=(GameObject)EditorGUILayout.ObjectField("当前场景的 Arena（优先）",arenaInScene,typeof(GameObject),true);
            if(arenaInScene!=null&&L01ArenaImport.ResolveRoot(arenaInScene)!=null&&L01ArenaImport.Component(L01ArenaImport.ResolveRoot(arenaInScene))==null)
                EditorGUILayout.HelpBox("已识别现有 Arena 图片与碰撞。缺少旧运行组件时，只在新场景副本补充连接脚本，原 Arena 保留。",MessageType.Info);
            if(GUILayout.Button("识别当前 Arena"))FindArena();
            previewDoors=EditorGUILayout.Toggle("测试用：全部探索门打开",previewDoors);
            registerBuildEntry=EditorGUILayout.Toggle("将新副本设为 Day01 游戏入口",registerBuildEntry);
            EditorGUILayout.HelpBox("默认：E 确认入口 → Beacon → 恢复供电 → 隔离扫描 → 回收数据 → 挑战 Boss / 前往撤离。保留 315 / 330 / 360 秒撤离时间。",MessageType.None);
            GUI.enabled=!EditorApplication.isPlayingOrWillChangePlaymode;
            if(GUILayout.Button("修复当前场景引用并关闭调试绘制（保留布局）",GUILayout.Height(34)))
            {
                try{status=L01SceneRepair.RepairCurrentScene();}
                catch(Exception e){status="修复未完成："+e.Message;Debug.LogException(e);}
            }
            if(GUILayout.Button("一键生成完整 Level01（另存新场景）",GUILayout.Height(44)))
            {
                try{Build();}catch(Exception e){status="生成未完成："+e.Message;Debug.LogException(e);}
                finally{EditorUtility.ClearProgressBar();}
            }
            if(GUILayout.Button("检查当前完整场景"))status=L01SceneValidation.Check(FindWorld());
            GUI.enabled=true;
            EditorGUILayout.HelpBox(status,MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        L01World FindWorld()
        {
            foreach(GameObject r in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach(L01World w in r.GetComponentsInChildren<L01World>(true))
                    if(w.gameObject.activeInHierarchy)return w;
            return null;
        }

        void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play。");
            quadCache.Clear();
            if(gameplayScene==null)throw new InvalidOperationException("未找到原 Day01_TestGround 场景，请在窗口指定。");
            if(arenaInScene==null&&arenaScene==null)throw new InvalidOperationException("请指定你已经测试好的 Arena 场景或当前场景里的 Arena。");
            environmentLayer=LayerMask.NameToLayer("Environment");interactableLayer=LayerMask.NameToLayer("Interactable");
            if(environmentLayer<0||interactableLayer<0)throw new InvalidOperationException("原工程需要 Environment 和 Interactable 层。");
            TextAsset asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/level01_layout.json");
            if(asset==null)throw new InvalidOperationException("level01_layout.json 缺失，请完整解压 Assets 目录。");
            layout=JsonUtility.FromJson<L01Layout>(asset.text);
            if(layout==null||layout.schemaVersion!=1||layout.regions.Length!=6)throw new InvalidOperationException("场景布局数据版本不匹配。");
            shader=Shader.Find("AlienPurge/Level01ConnectedV01/Atlas");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Level01 URP2D Shader 未正常导入，请查看 Console。");
            foreach(L01Region r in layout.regions)Texture(r.texture,false);
            foreach(string file in new[]{"AP_L01_LootStates.png","AP_L01_GateStates.png","AP_L01_ConnectorKit.png","AP_L01_RoadGround.png","AP_L01_OutsideGround.png"})Texture(file,file!="AP_L01_RoadGround.png"&&file!="AP_L01_OutsideGround.png");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;

            SceneSetup[] previousSetup=EditorSceneManager.GetSceneManagerSetup();
            EditorBuildSettingsScene[] previousBuildScenes=EditorBuildSettings.scenes;
            try
            {
            if(!string.IsNullOrEmpty(generatedOverride))
            {
                if(!generatedOverride.StartsWith("Assets/AlienPurge/ArenaLevel01RebuildV01/Generated/",StringComparison.Ordinal)
                    ||generatedOverride.Contains("..")||AssetDatabase.IsValidFolder(generatedOverride))
                    throw new InvalidOperationException("统一重建的 Level01 输出目录需要是新的专用目录。");
                generated=generatedOverride;EnsureFolder(generated);
            }
            else
            {
                EnsureFolder(Root+"/Generated");
                generated=AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated/Level01_Run");
                AssetDatabase.CreateFolder(Root+"/Generated",System.IO.Path.GetFileName(generated));
            }
            EnsureFolder(generated+"/Meshes");EnsureFolder(generated+"/Materials");EnsureFolder(generated+"/Prefabs");EnsureFolder(generated+"/Scenes");
            Scene sourceArenaScene;bool loadedForSnapshot=false;
            if(arenaInScene!=null)
            {
                if(EditorUtility.IsPersistent(arenaInScene)||!arenaInScene.scene.IsValid())throw new InvalidOperationException("Arena 要指定场景实例。");
                arenaInScene=L01ArenaImport.ResolveRoot(arenaInScene);
                if(arenaInScene==null)throw new InvalidOperationException("未识别出完整 Arena，请指定 ArenaRoot_Continuous 根物体。");
                sourceArenaScene=arenaInScene.scene;
            }
            else
            {
                string path=AssetDatabase.GetAssetPath(arenaScene);sourceArenaScene=SceneManager.GetSceneByPath(path);
                if(!sourceArenaScene.IsValid()||!sourceArenaScene.isLoaded){sourceArenaScene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);loadedForSnapshot=true;}
                arenaInScene=L01ArenaImport.FindRoot(sourceArenaScene);
                if(arenaInScene==null)throw new InvalidOperationException("指定场景没有找到连续 Arena 的图片与碰撞层级；请选择原 HiveTyrant_Arena 场景。");
            }
            string arenaPath=sourceArenaScene.path,arenaObjectPath=HierarchyPath(arenaInScene.transform);
            ValidateArenaTransform(arenaInScene);
            L01ArenaImport.Preflight(arenaInScene);
            float originalArenaWidth=L01ArenaImport.ReadWidth(arenaInScene);
            string snapshot=generated+"/Scenes/Source_Arena_Snapshot.unity";
            if(!EditorSceneManager.SaveScene(sourceArenaScene,snapshot,true))throw new InvalidOperationException("Arena 副本保存失败。");
            if(loadedForSnapshot)EditorSceneManager.CloseScene(sourceArenaScene,true);
            string basePath=AssetDatabase.GetAssetPath(gameplayScene);
            EditorUtility.DisplayProgressBar("Level01","复制原玩法场景与现有 Arena",.10f);
            Scene gameplay=EditorSceneManager.OpenScene(basePath,OpenSceneMode.Single);
            string outputPath=generated+"/Scenes/Day01_TestGround.unity";
            if(!EditorSceneManager.SaveScene(gameplay,outputPath,true))throw new InvalidOperationException("玩法场景副本保存失败。");
            Scene target=EditorSceneManager.OpenScene(outputPath,OpenSceneMode.Single);
            DisablePreviousEnvironment(target);
            GameObject player=FindPlayer(target);
            if(player==null||player.GetComponent<Rigidbody2D>()==null)throw new InvalidOperationException("玩法场景缺少带 Rigidbody2D 的 Player。");

            Scene arenaCopy=EditorSceneManager.OpenScene(snapshot,OpenSceneMode.Additive);
            GameObject importedArena=FindHierarchy(arenaCopy,arenaObjectPath);
            if(importedArena==null)throw new InvalidOperationException("Arena 副本中的环境根物体未找到。");
            preservedArenaFingerprint=L01SceneValidation.GeometryFingerprint(importedArena);
            MonoBehaviour arena=L01ArenaImport.ConnectCopy(importedArena,originalArenaWidth);
            GameObject sourcePlayer=FindPlayer(arenaCopy),boss=FindBoss(arenaCopy,importedArena);
            CapsuleCollider2D copiedFoot=CopyExistingFoot(sourcePlayer,player);
            bool preventCrossSceneReferences=EditorSceneManager.preventCrossSceneReferences;
            try
            {
                EditorSceneManager.preventCrossSceneReferences=false;
                MoveRoot(importedArena,target);
                if(boss!=null&&!boss.transform.IsChildOf(importedArena.transform))MoveRoot(boss,target);
                RemapActorReferences(importedArena,sourcePlayer,player,copiedFoot);
                if(boss!=null)RemapActorReferences(boss,sourcePlayer,player,copiedFoot);
            }
            finally{EditorSceneManager.preventCrossSceneReferences=preventCrossSceneReferences;}
            EditorSceneManager.CloseScene(arenaCopy,true);
            SceneManager.SetActiveScene(target);

            float arenaWidth=originalArenaWidth;
            float scale=arenaWidth*importedArena.transform.lossyScale.x/layout.referenceArenaWidth;
            if(scale<=0||float.IsNaN(scale)||float.IsInfinity(scale))throw new InvalidOperationException("现有 Arena 的世界尺寸无法读取。");
            GameObject root=new GameObject("Level01_Connected_V01");SceneManager.MoveGameObjectToScene(root,target);
            root.transform.position=importedArena.transform.position;root.transform.localScale=Vector3.one*scale;
            world=root.AddComponent<L01World>();world.layoutAsset=asset;world.layout=layout;world.player=player;world.boss=boss;world.existingArena=arena;
            world.sourceArenaScene=arenaPath;world.sourceGameplayScene=basePath;world.footAnchor=L01ProjectAccess.Feet(player);
            ReadSorting(player);
            wallPhysics=new PhysicsMaterial2D("L01_Walls_ZeroFriction"){friction=0,bounciness=0};
            AssetDatabase.CreateAsset(wallPhysics,generated+"/Materials/Walls.physicsMaterial2D");
            ground=Material("RoadGround","AP_L01_RoadGround.png");ground.SetFloat("_FloorMode",1);ground.SetFloat("_WorldRepeat",12*scale);
            kit=Material("ConnectorKit","AP_L01_ConnectorKit.png");
            gates=Material("GateStates","AP_L01_GateStates.png");
            loot=Material("LootStates","AP_L01_LootStates.png");loot.SetFloat("_OutlinePixels",4);
            progression=root.AddComponent<L01Progression>();world.progression=progression;progression.world=world;progression.previewAllDoorsOpen=previewDoors;
            progression.missionManager=L01ProjectAccess.Find(target,"MissionManager");
            BuildBackground();BuildRegions();BuildRoads();BuildCollision();
            Transform gateRoot=Child(root.transform,"Gates_G1_G7");List<L01Gate> allGates=new List<L01Gate>();
            foreach(L01GateSpec spec in layout.gates)allGates.Add(BuildGate(gateRoot,spec));
            progression.gates=allGates.ToArray();
            BuildServiceDoor();BuildArrivalDoor();BuildTerminals();
            EditorUtility.DisplayProgressBar("Level01","放置搜索容器、任务点和撤离点",.66f);
            BuildLoot();ConfigureProject(target,player);
            bool nativeFeet=false;
            if(copiedFoot!=null)foreach(MonoBehaviour m in importedArena.GetComponentsInChildren<MonoBehaviour>(true))
                if(m!=null&&m.GetType().Name=="ArenaV031PlayerFeet"){L01ProjectAccess.Call(m,"Apply");nativeFeet=true;}
            if(!nativeFeet)InstallFeetFallback(player,copiedFoot);
            world.arenaBridge=root.AddComponent<L01ArenaBridge>();world.arenaBridge.world=world;
            Vector3 feetOffset=world.footAnchor.position-player.transform.position;
            player.transform.position=world.World(layout.playerStart.V)-feetOffset;
            Rigidbody2D playerBody=player.GetComponent<Rigidbody2D>();playerBody.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            playerBody.velocity=Vector2.zero;
            world.MoveMissionPoints("CargoYard");
            if(preservedArenaFingerprint!=L01SceneValidation.GeometryFingerprint(importedArena))
                throw new InvalidOperationException("原 Arena 的几何检查不一致；未保存新场景，请保留原场景并检查 Console。");
            Camera camera=FindCamera(target);if(camera!=null)camera.transform.position=new Vector3(player.transform.position.x,player.transform.position.y,camera.transform.position.z);
            Physics2D.SyncTransforms();AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(target);
            if(!EditorSceneManager.SaveScene(target))throw new InvalidOperationException("完整场景保存失败。");
            if(registerBuildEntry)RegisterBuildEntry(outputPath);
            WriteBuildReport(outputPath,importedArena,scale);
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.FrameSelected();
            status="已生成："+outputPath+"\n"+L01SceneValidation.Check(world);
            arenaInScene=importedArena;
            }
            catch
            {
                EditorBuildSettings.scenes=previousBuildScenes;
                try
                {
                    if(previousSetup.Length>0&&previousSetup.All(s=>!string.IsNullOrEmpty(s.path)))
                        EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                catch(Exception recovery){Debug.LogWarning("场景恢复需要检查："+recovery.Message);}
                world=null;progression=null;arenaInScene=null;FindArena();
                throw;
            }
        }

        void BuildBackground()
        {
            Material terrain=Material("OutsideGround","AP_L01_OutsideGround.png");terrain.SetFloat("_FloorMode",1);
            terrain.SetFloat("_WorldRepeat",22*world.transform.lossyScale.x);
            Vector2 size=layout.boundsMax.V-layout.boundsMin.V;
            MeshRenderer renderer=PutMesh(world.transform,"Outside_Rocky_Ground",L01Geometry.Quad(size.x+20,size.y+20,new Vector2(.5f,.5f)),terrain,groundLayer,-31000);
            renderer.transform.localPosition=(layout.boundsMin.V+layout.boundsMax.V)*.5f;
        }
        void BuildRegions()
        {
            Transform regionRoot=Child(world.transform,"Regions");
            foreach(L01Region r in layout.regions)
            {
                EditorUtility.DisplayProgressBar("Level01","创建区域："+r.label,.28f);
                Transform region=Child(regionRoot,r.id);region.localPosition=r.center.V;
                Material atlas=Material(r.id,r.texture);atlas.SetTexture("_DetailMap",Texture("AP_L01_RoadGround.png",false));atlas.SetFloat("_DetailAmount",.035f);
                PutMesh(region,"Continuous_Environment",L01Geometry.Quad(r.width,r.height,new Vector2(.5f,.5f)),atlas,groundLayer,-30020);
                Transform foreground=Child(region,"Foreground_Occlusion");
                foreach(L01Occluder o in r.occluders)
                {
                    MeshRenderer renderer=PutMesh(foreground,o.name,L01Geometry.PixelPolygon(r,o.points,o.name),atlas,worldLayer,0);
                    L01DepthSort sort=Depth(renderer,null,new Vector3(0,(.5f-o.baselineY/r.pixelHeight)*r.height,0));
                    sort.alwaysForeground=o.alwaysForeground;sort.Apply();
                }
            }
        }
        void BuildRoads()
        {
            Transform parent=Child(world.transform,"Connected_Roads");
            foreach(L01Road road in layout.roads)
            {
                Transform group=Child(parent,road.id);
                foreach(L01Polygon p in road.renderPolygons)
                {
                    Vector2[] v=L01Geometry.Points(p.points);
                    PutMesh(group,"RoadSurface",L01Geometry.Polygon(v,v,"Road_"+road.id),ground,groundLayer,-30010);
                }
                foreach(L01Point p in road.wallH)Part(group,"Wall_H",p.V,new Vector2(4.5f,1.8f),new Vector2(.5f,.115f),kit,new Vector4(0,684f/1254f,850f/1254f,340f/1254f));
                foreach(L01Point p in road.wallV)
                {
                    MeshRenderer v=Part(group,"Wall_V",p.V,new Vector2(1.7f,4.6f),new Vector2(.5f,.5f),kit,new Vector4(974f/1254f,674f/1254f,181f/1254f,490f/1254f));
                    L01DepthSort depth=v.GetComponent<L01DepthSort>();depth.anchor=null;depth.localAnchor=new Vector3(0,-1.98f,0);depth.Apply();
                }
            }
            Material threshold=Material("RoadExpansionJoint","AP_L01_RoadGround.png");threshold.SetFloat("_FloorMode",1);
            threshold.SetFloat("_WorldRepeat",12*world.transform.lossyScale.x);threshold.SetColor("_Color",new Color(.61f,.66f,.70f,1));
            foreach(L01Threshold seam in layout.thresholds)
            {
                MeshRenderer plate=PutMesh(parent,"Metal_Expansion_Joint",L01Geometry.Quad(seam.size.x,seam.size.y,new Vector2(.5f,.5f)),threshold,groundLayer,-29990);
                plate.transform.localPosition=seam.center.V;
            }
        }
        void BuildCollision()
        {
            Transform parent=Child(world.transform,"Collision_Footprints");parent.gameObject.layer=environmentLayer;
            PolygonCollider2D solid=parent.gameObject.AddComponent<PolygonCollider2D>();solid.sharedMaterial=wallPhysics;
            solid.pathCount=layout.solidPaths.Length;
            for(int i=0;i<solid.pathCount;i++)solid.SetPath(i,L01Geometry.Points(layout.solidPaths[i].points));
            L01CollisionAuthoring edit=parent.gameObject.AddComponent<L01CollisionAuthoring>();edit.solid=solid;
            edit.outlines=layout.collisionOutlines.Select(p=>new L01BoundaryLoop{points=L01Geometry.Points(p.points)}).ToArray();
            Transform spawn=Child(world.transform,"Spawn_Playable_Area");spawn.gameObject.layer=2;
            PolygonCollider2D area=spawn.gameObject.AddComponent<PolygonCollider2D>();area.isTrigger=true;area.pathCount=layout.spawnPaths.Length;
            for(int i=0;i<area.pathCount;i++)area.SetPath(i,L01Geometry.Points(layout.spawnPaths[i].points));
            edit.spawnArea=area;
        }
        L01Gate BuildGate(Transform parent,L01GateSpec spec)
        {
            Transform gate=Child(parent,spec.id+"_"+spec.from+"_"+spec.to);gate.localPosition=spec.center.V;
            L01Gate controller=gate.gameObject.AddComponent<L01Gate>();controller.gateId=spec.id;controller.requirement=spec.requirement;
            controller.world=world;controller.vertical=spec.vertical;
            if(spec.id=="G6")
            {
                controller.externalArena=true;
                controller.blocker=L01ProjectAccess.Get(world.existingArena,"entryBlocker") as BoxCollider2D;
                if(controller.blocker==null)throw new InvalidOperationException("现有 Arena 没有可识别的南门 Entry Blocker；已停止以保护旧碰撞。");
            }
            else
            {
                float gateWidth=spec.opening*2.2f;
                Vector2 size=spec.vertical?new Vector2(3.8f,spec.opening*1.9f):new Vector2(gateWidth,gateWidth*420f/627f);
                MeshRenderer visual=PutMesh(gate,"Door_Visual",L01Geometry.Quad(size.x,size.y,new Vector2(.5f,.04f)),gates,worldLayer,0);
                controller.visual=visual;Depth(visual,gate,Vector3.zero);
                Transform collision=Child(gate,"Closed_Door_Collision");collision.gameObject.layer=environmentLayer;
                BoxCollider2D box=collision.gameObject.AddComponent<BoxCollider2D>();box.sharedMaterial=wallPhysics;
                box.size=spec.vertical?new Vector2(.7f,spec.opening+.6f):new Vector2(spec.opening+.6f,.7f);
                controller.blocker=box;
            }
            string[] destinations={"CARGO YARD","MAINTENANCE","MEDICAL","RESEARCH","CONTAINMENT","HIVE TYRANT","EVAC"};
            int index=Mathf.Clamp(int.Parse(spec.id.Substring(1))-1,0,6);
            float signY=spec.id=="G6"?4.4f:spec.opening*2.2f*420f/627f*.60f;
            controller.sign=Label(gate,spec.id+"  "+destinations[index],new Vector3(0,signY,0),.75f);
            controller.SetLocked(false,true);return controller;
        }
        void BuildServiceDoor()
        {
            L01Region r=world.Region("Extraction");
            Vector2 p=r.center.V+new Vector2((880f/1536-.5f)*r.width,(.5f-830f/1024)*r.height);
            MeshRenderer v=Part(world.transform,"Extraction_Service_Door_Sealed",p,new Vector2(4.4f,2.67f),new Vector2(.5f,.06f),gates,new Vector4(807f/1254f,844f/1254f,280f/1254f,170f/1254f));
            Depth(v,null,Vector3.zero);
        }
        void BuildArrivalDoor()
        {
            L01Region r=world.Region("ColonyEntry");
            Vector2 p=r.center.V+new Vector2(0,(.5f-818f/1024)*r.height);
            Part(world.transform,"Arrival_Door_SealedBehindPlayer",p,new Vector2(5.5f,3.34f),new Vector2(.5f,.06f),gates,new Vector4(807f/1254f,844f/1254f,280f/1254f,170f/1254f));
            Transform t=Child(world.transform,"Arrival_Closed_Collision");t.localPosition=p;t.gameObject.layer=environmentLayer;
            BoxCollider2D c=t.gameObject.AddComponent<BoxCollider2D>();c.size=new Vector2(6.1f,.7f);c.sharedMaterial=wallPhysics;
        }
        void BuildTerminals()
        {
            Transform root=Child(world.transform,"Progression_Terminals");
            foreach(L01TerminalSpec spec in layout.terminals)
            {
                Transform terminal=Child(root,spec.id);terminal.localPosition=spec.position.V;terminal.gameObject.layer=interactableLayer;
                MeshRenderer visual=Part(terminal,"Console",Vector2.zero,new Vector2(2.4f,2.88f),new Vector2(.5f,.10f),kit,new Vector4(790f/1254f,14f/1254f,450f/1254f,540f/1254f));
                Depth(visual,terminal,Vector3.zero);
                CircleCollider2D trigger=terminal.gameObject.AddComponent<CircleCollider2D>();trigger.radius=.75f;trigger.isTrigger=true;
                L01Terminal t=terminal.gameObject.AddComponent<L01Terminal>();t.progression=progression;t.requiredFlag=spec.requiredFlag;t.completesFlag=spec.completesFlag;t.duration=spec.duration;t.label=spec.label;
                Set(t,"interactionPrompt",spec.prompt);
                t.status=Label(terminal,spec.label,new Vector3(0,3.1f,0),.65f);
                Transform collision=Child(terminal,"Console_Footprint");collision.gameObject.layer=environmentLayer;
                BoxCollider2D solid=collision.gameObject.AddComponent<BoxCollider2D>();solid.size=new Vector2(1.1f,.6f);solid.offset=new Vector2(0,.3f);solid.sharedMaterial=wallPhysics;
            }
        }

        void BuildLoot()
        {
            string[] names={"LootContainer_FieldSupply.prefab","LootContainer_MedicalCache.prefab","LootContainer_ResearchCase 1.prefab","LootContainer_HighRiskCase.prefab"};
            GameObject[] variants=new GameObject[4];
            for(int i=0;i<names.Length;i++)
            {
                GameObject native=AssetDatabase.LoadAssetAtPath<GameObject>(NativePrefabs+names[i]);
                if(native==null)throw new InvalidOperationException("找不到原容器 Prefab："+names[i]);
                GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(native,world.gameObject.scene);
                instance.transform.localScale=Vector3.one;
                instance.name="L01_"+native.name;
                foreach(Renderer old in instance.GetComponentsInChildren<Renderer>(true))old.enabled=false;
                foreach(MonoBehaviour old in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if(old!=null&&old.GetType().Name.Contains("YSort"))old.enabled=false;
                MeshRenderer visual=PutMesh(instance.transform,"World_Case_Visual",L01Geometry.Quad(2.4f,3.2f,new Vector2(.5f,.16f)),loot,worldLayer,0);
                Depth(visual,instance.transform,Vector3.zero);
                L01LootVisual bind=instance.AddComponent<L01LootVisual>();bind.kind=i;bind.container=instance.GetComponent<LootContainer>();bind.visual=visual;
                bind.openedYOffset=new[]{-.2375f,-.2f,-.1625f,-.2f}[i];
                if(bind.container==null)throw new InvalidOperationException("原容器没有 LootContainer："+native.name);
                Transform footprint=Child(instance.transform,"Case_Footprint");footprint.gameObject.layer=environmentLayer;
                BoxCollider2D box=footprint.gameObject.AddComponent<BoxCollider2D>();box.size=new Vector2(2.1f,.8f);box.offset=new Vector2(0,.4f);box.sharedMaterial=wallPhysics;
                bool ok;variants[i]=PrefabUtility.SaveAsPrefabAsset(instance,generated+"/Prefabs/"+instance.name+".prefab",out ok);
                UnityEngine.Object.DestroyImmediate(instance);
                if(!ok||variants[i]==null)throw new InvalidOperationException("容器美术变体保存失败。");
            }
            Transform root=Child(world.transform,"Loot_Containers_76");
            foreach(L01LootSpot spot in layout.loot)
            {
                GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(variants[spot.kind],world.gameObject.scene);
                instance.transform.SetParent(root,false);instance.transform.localPosition=spot.position.V;
                instance.name=spot.region+"_"+instance.name;
            }
            MonoBehaviour extraction=L01ProjectAccess.Find(world.gameObject.scene,"ExtractionController");
            if(extraction!=null)Set(extraction,"highRiskCachePrefab",variants[3]);
        }

        void ConfigureProject(Scene scene,GameObject player)
        {
            Vector2 min=world.World(layout.boundsMin.V),max=world.World(layout.boundsMax.V);
            Camera camera=FindCamera(scene);
            MonoBehaviour cameraFollow=L01ProjectAccess.Find(scene,"CameraFollow");
            if(cameraFollow!=null){Set(cameraFollow,"mapMin",min);Set(cameraFollow,"mapMax",max);Set(cameraFollow,"limitToMapBounds",true);Set(cameraFollow,"target",player.transform);}
            Transform markerRoot=Child(world.transform,"Gameplay_Markers");
            Transform missionRoot=Child(markerRoot,"Mission_SpawnPoints");world.missionPoints=new Transform[6];
            for(int i=0;i<6;i++){Transform p=Child(missionRoot,"Mission_"+i);AddByName(p.gameObject,"MissionSpawnPoint");world.missionPoints[i]=p;}
            MonoBehaviour mission=L01ProjectAccess.Find(scene,"MissionManager");if(mission!=null)Set(mission,"spawnPointRoot",missionRoot);
            foreach(GameObject sceneRoot in scene.GetRootGameObjects())foreach(MonoBehaviour m in sceneRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(m==null)continue;
                if(m.GetType().Name=="CargoYardMissionEntryGate")UnityEngine.Object.DestroyImmediate(m);
                else if(m.GetType().Name=="CargoYardExitGuide"||m.GetType().Name=="Level01RegionProgressionController")m.enabled=false;
            }
            if(mission!=null)
            {
                Transform entry=Child(markerRoot,"Cargo_Mission_Entry");entry.localPosition=world.Region("CargoYard").center.V;entry.gameObject.layer=2;
                BoxCollider2D trigger=entry.gameObject.AddComponent<BoxCollider2D>();trigger.isTrigger=true;trigger.size=new Vector2(60,36);
                Component missionEntry=AddByName(entry.gameObject,"CargoYardMissionEntryGate");Set(missionEntry,"missionManager",mission);Set(missionEntry,"playerRoot",player.transform);
            }
            Transform extractionRoot=Child(markerRoot,"Extraction_SpawnPoints");
            Transform point=Child(extractionRoot,"Extraction_Pad");point.localPosition=layout.extractionPoint.V;
            Component extractionPoint=AddByName(point.gameObject,"ExtractionSpawnPoint");
            MonoBehaviour extraction=L01ProjectAccess.Find(scene,"ExtractionController");
            if(extraction!=null)
            {
                SetObjectArray(extraction,"spawnPoints",new[]{extractionPoint});
                Set(extraction,"autoFindSpawnPointsIfEmpty",false);
                // The fixed pad deliberately uses the existing reachable-point fallback
                // when the Player is farther than the old 12-25u selection preference.
                Transform highRiskRoot=Child(markerRoot,"HighRisk_SpawnPoints");
                world.highRiskAnchors=new Transform[6];Component[] cachePoints=new Component[6];
                for(int i=0;i<6;i++){Transform t=Child(highRiskRoot,"HighRisk_"+i);world.highRiskAnchors[i]=t;cachePoints[i]=AddByName(t.gameObject,"HighRiskCacheSpawnPoint");}
                SetObjectArray(extraction,"highRiskCacheSpawnPoints",cachePoints);Set(extraction,"autoFindHighRiskCacheSpawnPointsIfEmpty",false);
            }
            world.safeZoneAnchors=new Transform[6];Transform safeRoot=Child(markerRoot,"SafeZone_Anchors");
            for(int i=0;i<6;i++)world.safeZoneAnchors[i]=Child(safeRoot,"Safe_"+i);
            ConfigureNavigation(scene,min,max);
            MonoBehaviour spawner=L01ProjectAccess.Find(scene,"EnemySpawner");
            if(spawner!=null)
            {
                Set(spawner,"spawnMapMin",min);Set(spawner,"spawnMapMax",max);Set(spawner,"limitSpawnToMapBounds",true);
                Set(spawner,"useValidatedSpawnPositions",true);
                Component validator=spawner.GetComponent(FindType("EnemySpawnPositionValidator"));
                if(validator==null)validator=AddByName(spawner.gameObject,"EnemySpawnPositionValidator");
                Set(validator,"spawnPlayableArea",world.transform.Find("Spawn_Playable_Area").GetComponent<PolygonCollider2D>());
                Set(validator,"logFailedSpawnSearches",false);
                float node=world.navigation.astar.data.gridGraph.nodeSize;
                Set(validator,"maxDistanceToNode",Mathf.Max(.35f,node*.8f));
                Set(validator,"maxPlayerNodeDistance",Mathf.Max(1.2f,Vector3.Distance(player.transform.position,world.footAnchor.position)+node*2+.2f));
                Set(spawner,"spawnPositionValidator",validator);
            }
            ConfigureFogAndSafeZone(scene);
            Transform bounds=Child(markerRoot,"MapBounds");bounds.gameObject.layer=2;
            BoxCollider2D map=bounds.gameObject.AddComponent<BoxCollider2D>();map.isTrigger=true;
            map.offset=(layout.boundsMin.V+layout.boundsMax.V)*.5f;map.size=layout.boundsMax.V-layout.boundsMin.V;
        }
        void ConfigureNavigation(Scene scene,Vector2 min,Vector2 max)
        {
            AstarPath astar=null;
            foreach(GameObject r in scene.GetRootGameObjects())
            {
                foreach(AstarPath candidate in r.GetComponentsInChildren<AstarPath>(true))
                    if(candidate.gameObject.activeInHierarchy){astar=candidate;break;}
                if(astar!=null)break;
            }
            if(astar==null){GameObject g=new GameObject("AstarPath_Level01");SceneManager.MoveGameObjectToScene(g,scene);astar=g.AddComponent<AstarPath>();}
            astar.enabled=true;astar.gameObject.SetActive(true);
            astar.showNavGraphs=false;astar.showUnwalkableNodes=false;
            GridGraph graph=L01Navigation.EnsureGrid(astar);
            float node=.6f*Mathf.Max(.75f,world.transform.lossyScale.x);
            graph.center=(Vector3)((min+max)*.5f);graph.rotation=new Vector3(90,0,0);
            graph.SetDimensions(Mathf.CeilToInt((max.x-min.x)/node),Mathf.CeilToInt((max.y-min.y)/node),node);
            if(graph.collision==null)graph.collision=new GraphCollision();
            graph.collision.use2D=true;graph.collision.type=Pathfinding.ColliderType.Sphere;graph.collision.heightCheck=false;graph.collision.unwalkableWhenNoGround=false;
            graph.collision.collisionCheck=true;graph.collision.mask=1<<environmentLayer;graph.collision.diameter=1.1f;
            graph.erodeIterations=1;
            astar.scanOnStartup=false;
            astar.data.cacheStartup=false;
            astar.data.SetData(astar.data.SerializeGraphs());
            world.navigation=world.gameObject.AddComponent<L01Navigation>();world.navigation.astar=astar;world.navigation.world=world;
            // The runtime scan occurs after all doors and the original Arena bridge
            // have initialized, so closed-gate reachability is correct from frame one.
            EditorUtility.SetDirty(astar);
        }
        void ConfigureFogAndSafeZone(Scene scene)
        {
            MonoBehaviour safe=L01ProjectAccess.Find(scene,"SafeZoneController");
            MonoBehaviour route=L01ProjectAccess.Find(scene,"ToxicFogRouteController");
            MonoBehaviour area=L01ProjectAccess.Find(scene,"ToxicFogAreaController");
            if(route!=null&&area!=null)
            {
                Transform parent=Child(world.transform,"Fog_Route");List<Component> sections=new List<Component>();List<UnityEngine.Object> centers=new List<UnityEngine.Object>();
                for(int i=0;i<layout.fogRoute.Length;i++)
                {
                    Vector2 center=layout.fogRoute[i].V;
                    Vector2 before=layout.fogRoute[Mathf.Max(0,i-1)].V,after=layout.fogRoute[Mathf.Min(layout.fogRoute.Length-1,i+1)].V;
                    Vector2 direction=(after-before).normalized,normal=new Vector2(-direction.y,direction.x);
                    Transform section=Child(parent,"Route_"+i.ToString("00"));section.localPosition=center;
                    Transform left=Child(section,"Left"),right=Child(section,"Right");left.localPosition=normal*3;right.localPosition=-normal*3;
                    Component c=AddByName(section.gameObject,"ToxicFogCrossSection");Set(c,"leftBoundary",left);Set(c,"rightBoundary",right);sections.Add(c);centers.Add(section);
                }
                SetObjectArray(route,"routePoints",centers.ToArray());SetObjectArray(area,"crossSections",sections.Cast<UnityEngine.Object>().ToArray());
                if(safe!=null)
                {
                    Set(safe,"toxicFogRoute",route);Set(safe,"toxicFogArea",area);Set(safe,"useLevel01FogAwareAnchors",true);Set(safe,"requireForwardAnchor",false);
                    SetObjectArray(safe,"level01SafeZoneAnchors",world.safeZoneAnchors.Cast<UnityEngine.Object>().ToArray());
                }
            }
            // Legacy Day01 retains its original Storm rules. L01SafeZoneBridge
            // relocates any generated center outside the playable area to a local bay.
            if(safe!=null){L01SafeZoneBridge bridge=world.gameObject.AddComponent<L01SafeZoneBridge>();bridge.world=world;bridge.safeZone=safe;}
        }

        void InstallFeetFallback(GameObject player,CapsuleCollider2D existingFeet=null)
        {
            Rigidbody2D body=player.GetComponent<Rigidbody2D>();Transform anchor=L01ProjectAccess.Feet(player);
            List<L01ColliderFilter> snapshots=new List<L01ColliderFilter>();float width=0;
            foreach(Collider2D c in player.GetComponentsInChildren<Collider2D>(true))
                if(c!=existingFeet&&c.enabled&&!c.isTrigger&&c.attachedRigidbody==body)
                {width=Mathf.Max(width,c.bounds.size.x);snapshots.Add(new L01ColliderFilter{collider=c,includeLayers=c.includeLayers,excludeLayers=c.excludeLayers,priority=c.layerOverridePriority});}
            width=Mathf.Clamp(width*.44f,.16f,1.5f);float height=Mathf.Clamp(width*.22f,.035f,.20f);
            CapsuleCollider2D foot=existingFeet;
            if(foot==null)
            {
                Transform t=Child(player.transform,"L01_EnvironmentFeet");t.gameObject.layer=player.layer;
                t.position=anchor.position+Vector3.up*(height*.5f);
                foot=t.gameObject.AddComponent<CapsuleCollider2D>();foot.direction=CapsuleDirection2D.Horizontal;
                foot.size=new Vector2(width/Mathf.Abs(player.transform.lossyScale.x),height/Mathf.Abs(player.transform.lossyScale.y));
                foot.density=.0001f;foot.sharedMaterial=wallPhysics;
            }
            L01FeetContact bridge=world.gameObject.AddComponent<L01FeetContact>();bridge.feet=foot;bridge.environmentMask=1<<environmentLayer;bridge.original=snapshots.ToArray();
            bridge.Apply();
        }

        void ReadSorting(GameObject player)
        {
            worldLayer=SortingLayer.NameToID("World");groundLayer=SortingLayer.NameToID("Ground");
            foreach(SortingGroup group in player.GetComponentsInChildren<SortingGroup>(true))
                if(group.name=="VisualRoot"){worldLayer=group.sortingLayerID;break;}
            foreach(MonoBehaviour component in player.GetComponentsInChildren<MonoBehaviour>(true))
                if(component!=null&&component.GetType().Name=="YSortRenderer")
                {
                    object a=L01ProjectAccess.Get(component,"orderPerUnit"),b=L01ProjectAccess.Get(component,"baseOrder"),c=L01ProjectAccess.Get(component,"orderOffset");
                    if(a is int)orderPerUnit=Mathf.Max(1,(int)a);if(b is int)baseOrder=(int)b;if(c is int)orderOffset=(int)c;break;
                }
        }
        L01DepthSort Depth(MeshRenderer renderer,Transform anchor,Vector3 local)
        {
            L01DepthSort sort=renderer.GetComponent<L01DepthSort>();if(sort==null)sort=renderer.gameObject.AddComponent<L01DepthSort>();
            sort.target=renderer;sort.anchor=anchor;sort.localAnchor=local;sort.sortingLayer=worldLayer;
            sort.orderPerUnit=orderPerUnit;sort.baseOrder=baseOrder;sort.offset=orderOffset;sort.Apply();return sort;
        }
        MeshRenderer Part(Transform parent,string name,Vector2 position,Vector2 size,Vector2 pivot,Material material,Vector4 cell)
        {
            Transform t=Child(parent,name);t.localPosition=position;
            string key=size.x.ToString("R")+"_"+size.y.ToString("R")+"_"+pivot.x.ToString("R")+"_"+pivot.y.ToString("R");
            Mesh mesh;if(!quadCache.TryGetValue(key,out mesh)){mesh=L01Geometry.Quad(size.x,size.y,pivot);quadCache[key]=mesh;}
            MeshRenderer renderer=PutMesh(t,"Visual",mesh,material,worldLayer,0);
            MaterialPropertyBlock p=new MaterialPropertyBlock();p.SetVector("_Cell",cell);renderer.SetPropertyBlock(p);
            L01AtlasCell persistent=renderer.gameObject.AddComponent<L01AtlasCell>();persistent.cell=cell;persistent.target=renderer;
            Depth(renderer,t,Vector3.zero);return renderer;
        }
        MeshRenderer PutMesh(Transform parent,string name,Mesh mesh,Material material,int layer,int order)
        {
            if(!AssetDatabase.Contains(mesh))
            {
                string path=AssetDatabase.GenerateUniqueAssetPath(generated+"/Meshes/"+Sanitize(name)+".asset");AssetDatabase.CreateAsset(mesh,path);
            }
            Transform t=Child(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.sortingLayerID=layer;renderer.sortingOrder=order;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        Material Material(string name,string texture)
        {
            Material m=new Material(shader){name="L01_"+name};m.SetTexture("_BaseMap",Texture(texture,texture!="AP_L01_RoadGround.png"&&!layout.regions.Any(r=>r.texture==texture)));
            AssetDatabase.CreateAsset(m,generated+"/Materials/"+name+".mat");return m;
        }
        Texture2D Texture(string file,bool alpha)
        {
            string path=Root+"/Art/"+file;TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("缺少场景图片："+file);
            bool change=importer.textureType!=TextureImporterType.Default||importer.maxTextureSize<2048||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.npotScale!=TextureImporterNPOTScale.None||importer.alphaIsTransparency!=alpha;
            if(change)
            {
                importer.textureType=TextureImporterType.Default;importer.maxTextureSize=4096;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=alpha;importer.sRGBTexture=true;
                importer.npotScale=TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null)throw new InvalidOperationException("图片导入失败："+file);return texture;
        }
        TMP_Text Label(Transform parent,string text,Vector3 position,float size)
        {
            Transform t=Child(parent,"Sign_"+Sanitize(text));t.localPosition=position;
            TextMeshPro label=t.gameObject.AddComponent<TextMeshPro>();label.text=text;label.fontSize=4;label.alignment=TextAlignmentOptions.Center;
            label.enableWordWrapping=false;label.rectTransform.sizeDelta=new Vector2(12,2);label.transform.localScale=Vector3.one*size;
            if(TMP_Settings.defaultFontAsset!=null)label.font=TMP_Settings.defaultFontAsset;
            MeshRenderer renderer=label.GetComponent<MeshRenderer>();renderer.sortingLayerID=worldLayer;renderer.sortingOrder=27900;
            return label;
        }
        static Transform Child(Transform parent,string name){GameObject g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
        static string Sanitize(string s){foreach(char c in System.IO.Path.GetInvalidFileNameChars())s=s.Replace(c,'_');return s.Replace(' ','_').Replace('/','_');}
        static void EnsureFolder(string path)
        {
            string[] parts=path.Split('/');string current=parts[0];for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
        static Type FindType(string name)
        {
            foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t=a.GetType(name,false);if(t!=null)return t;
            }
            throw new InvalidOperationException("项目缺少组件类型："+name);
        }
        static Component AddByName(GameObject g,string name){return g.AddComponent(FindType(name));}
        static void Set(UnityEngine.Object target,string field,object value)
        {
            if(target==null)return;SerializedObject so=new SerializedObject(target);SerializedProperty p=so.FindProperty(field);
            if(p==null)return;
            if(value is UnityEngine.Object)p.objectReferenceValue=(UnityEngine.Object)value;
            else if(value is bool)p.boolValue=(bool)value;
            else if(value is string)p.stringValue=(string)value;
            else if(value is int)p.intValue=(int)value;
            else if(value is float)p.floatValue=(float)value;
            else if(value is Vector2)p.vector2Value=(Vector2)value;
            else if(value is Vector3)p.vector3Value=(Vector3)value;
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
        }
        static void SetObjectArray(UnityEngine.Object target,string field,UnityEngine.Object[] values)
        {
            if(target==null)return;SerializedObject so=new SerializedObject(target);SerializedProperty p=so.FindProperty(field);if(p==null||!p.isArray)return;
            p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();
        }
        static string HierarchyPath(Transform t){return t.parent!=null?HierarchyPath(t.parent)+"/"+t.name:t.name;}
        static GameObject FindHierarchy(Scene scene,string path)
        {
            string[] parts=path.Split('/');foreach(GameObject r in scene.GetRootGameObjects())if(r.name==parts[0])
            {if(parts.Length==1)return r;Transform t=r.transform.Find(string.Join("/",parts.Skip(1)));return t!=null?t.gameObject:null;}return null;
        }
        static void MoveRoot(GameObject g,Scene scene){if(g.transform.parent!=null)g.transform.SetParent(null,true);SceneManager.MoveGameObjectToScene(g,scene);}
        static GameObject FindPlayer(Scene scene)
        {
            foreach(GameObject r in scene.GetRootGameObjects())foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t.gameObject.CompareTag("Player"))return t.gameObject;return null;
        }
        static GameObject FindBoss(Scene scene,GameObject arena)
        {
            MonoBehaviour motor=L01ProjectAccess.Find(scene,"HiveTyrantBossMotor");if(motor!=null)return motor.gameObject;
            foreach(GameObject r in scene.GetRootGameObjects())if(r!=arena&&r.name.StartsWith("HiveTyrant",StringComparison.OrdinalIgnoreCase))return r;return null;
        }
        static Camera FindCamera(Scene scene)
        {
            foreach(GameObject r in scene.GetRootGameObjects())foreach(Camera c in r.GetComponentsInChildren<Camera>(true))if(c.CompareTag("MainCamera"))return c;return null;
        }
        static void ValidateArenaTransform(GameObject arena)
        {
            Vector3 s=arena.transform.lossyScale,a=arena.transform.eulerAngles;
            if(s.x<=0||s.y<=0||Mathf.Abs(s.x-s.y)>.01f*Mathf.Max(s.x,s.y))throw new InvalidOperationException("现有 Arena 需要正数等比 X/Y 缩放。");
            if(Quaternion.Angle(Quaternion.Euler(a),Quaternion.identity)>.1f)throw new InvalidOperationException("现有 Arena 需要保持原始无旋转朝向。");
        }
        static void DisablePreviousEnvironment(Scene scene)
        {
            // Keep gameplay services that were parented under the old scenery.
            string[] services={"PlanetStormController","SafeZoneController","ToxicFogRouteController","ToxicFogAreaController","EnemySpawner","MissionManager","ExtractionController","AstarPath"};
            foreach(GameObject root in scene.GetRootGameObjects())
                foreach(MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(m==null||!services.Contains(m.GetType().Name))continue;
                    Transform oldArt=null,keep=m.transform;
                    for(Transform p=m.transform.parent;p!=null;p=p.parent)
                    {
                        if(p.name=="StormSystem")keep=p;
                        if(p.name=="Environment"||p.name.StartsWith("ArenaRoot_Continuous_",StringComparison.Ordinal)){oldArt=p;break;}
                    }
                    if(oldArt!=null)keep.SetParent(null,true);
                }
            string[] names={"Environment","Ground_Test","GroundTilemap","GroundDetailsTilemap","LeftBoundary","RightBoundary","TopBoundary","BottomBoundary","Research","Medical","LootContainers","MissionSpawnPoints","ExtractionSpawnPoints","HIghRiskCacheSpawnPoints","MapBounds","Level01_Connected_V01"};
            foreach(GameObject root in scene.GetRootGameObjects())
                foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                    if(names.Contains(t.name)||t.name.StartsWith("ArenaRoot_Continuous_",StringComparison.Ordinal))t.gameObject.SetActive(false);
        }
        static CapsuleCollider2D CopyExistingFoot(GameObject sourcePlayer,GameObject targetPlayer)
        {
            if(sourcePlayer==null)return null;
            CapsuleCollider2D source=sourcePlayer.GetComponentsInChildren<CapsuleCollider2D>(true).FirstOrDefault(c=>c.name.Contains("EnvironmentFeet"));
            if(source==null)return null;
            Transform old=targetPlayer.transform.Find(source.name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject copy=UnityEngine.Object.Instantiate(source.gameObject,targetPlayer.transform,false);copy.name=source.name;return copy.GetComponent<CapsuleCollider2D>();
        }
        static void RemapActorReferences(GameObject root,GameObject sourcePlayer,GameObject targetPlayer,CapsuleCollider2D feet)
        {
            if(sourcePlayer==null)return;
            foreach(Component c in root.GetComponentsInChildren<Component>(true))
            {
                if(c==null)continue;SerializedObject so;
                try{so=new SerializedObject(c);}catch{continue;}
                SerializedProperty p=so.GetIterator();bool changed=false;
                while(p.Next(true))
                {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference||p.objectReferenceValue==null)continue;
                    UnityEngine.Object v=p.objectReferenceValue;Transform from=v is GameObject?((GameObject)v).transform:v is Component?((Component)v).transform:null;
                    if(from==null||!(from==sourcePlayer.transform||from.IsChildOf(sourcePlayer.transform)))continue;
                    UnityEngine.Object mapped=null;
                    if(feet!=null&&v is CapsuleCollider2D&&from.name.Contains("EnvironmentFeet"))mapped=feet;
                    else
                    {
                        string path=AnimationUtility.CalculateTransformPath(from,sourcePlayer.transform);
                        Transform target=path.Length==0?targetPlayer.transform:targetPlayer.transform.Find(path);
                        if(target!=null)mapped=v is GameObject?(UnityEngine.Object)target.gameObject:target.GetComponent(v.GetType());
                    }
                    p.objectReferenceValue=mapped;changed=true;
                }
                if(changed)so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        void RegisterBuildEntry(string outputPath)
        {
            string backup=generated+"/Original_BuildSettings.json";
            File.WriteAllText(backup,JsonUtility.ToJson(new L01BuildScenes{paths=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),enabled=EditorBuildSettings.scenes.Select(s=>s.enabled).ToArray()},true));
            List<EditorBuildSettingsScene> list=new List<EditorBuildSettingsScene>();
            foreach(EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if(System.IO.Path.GetFileNameWithoutExtension(s.path)!="Day01_TestGround")list.Add(s);
            list.Add(new EditorBuildSettingsScene(outputPath,true));EditorBuildSettings.scenes=list.ToArray();
        }
        void WriteBuildReport(string scene,GameObject arena,float scale)
        {
            string text="Scene: "+scene+"\nOriginal arena source: "+world.sourceArenaScene+"\nOriginal gameplay source: "+world.sourceGameplayScene+
                "\nArena copied without image/geometry/transform changes.\nReference world scale: "+scale+"\nContainers: "+layout.loot.Length+
                "\nArena geometry fingerprint before/after assembly: MATCHED\n"+
                "\nOriginal Arena south approach end-stop is disabled ONLY during Play by L01ArenaBridge and restored on disable.\n";
            File.WriteAllText(generated+"/Build_Report.txt",text);AssetDatabase.Refresh();
        }
        [Serializable]class L01BuildScenes{public string[] paths;public bool[] enabled;}
        [MenuItem("AlienPurge/Art/Level01 Connected V01 - Tools/Restore Previous Build Settings")]
        static void RestoreBuildSettings()
        {
            string path=EditorUtility.OpenFilePanel("选择生成目录中的 Original_BuildSettings.json",Root+"/Generated","json");
            if(string.IsNullOrEmpty(path))return;
            L01BuildScenes data=JsonUtility.FromJson<L01BuildScenes>(File.ReadAllText(path));
            if(data==null||data.paths==null||data.enabled==null||data.paths.Length!=data.enabled.Length)throw new InvalidOperationException("BuildSettings 备份格式不正确。");
            EditorBuildSettings.scenes=data.paths.Select((p,i)=>new EditorBuildSettingsScene(p,data.enabled[i])).ToArray();
        }
    }
}
