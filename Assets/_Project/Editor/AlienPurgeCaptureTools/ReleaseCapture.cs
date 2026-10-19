// Editor-only recording and input replay. Never saved into a game scene or player build.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using Pathfinding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienPurge.Capture.Editor
{
    [InitializeOnLoad]
    public static class ReleaseCapture
    {
        public const string Root = "TapTap_ReleaseAssets";
        public const string Level = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity";
        static RecorderController recorder;
        static string take;
        static double nextCommand;
        static ReleaseInputReplay replay;
        static ReleaseCapture()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            Application.logMessageReceived += (message, stack, type) => {
                if (SessionState.GetBool("ReleaseCapture.Running", false) && (type == LogType.Error || type == LogType.Exception))
                    File.AppendAllText(Root + "/10_QA/RuntimeErrors.log", message + "\n" + stack + "\n");
            };
        }
        [MenuItem("AlienPurge/Capture/Audit and Open Integrated World")]
        public static void Open()
        {
            EnsureFolders();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
            var audit = new List<string>();
            foreach (string path in new[]{AlienPurge.Editor.WindowsDemoBuilder.Menu, Level})
            {
                var scene = EditorSceneManager.OpenScene(path);
                var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                int missing=components.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                audit.Add(path+" | objects="+components.Length+" | missingScripts="+missing);
                if(missing>0)throw new InvalidOperationException("Missing script in "+path);
            }
            File.WriteAllLines(Root+"/10_QA/UnitySceneAudit.txt",audit);
            SessionState.SetBool("ReleaseCapture.Running",true);
            SessionState.SetBool("ReleaseCapture.Recorded",false);
            SessionState.SetString("ReleaseCapture.Take", "Gameplay_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            view.Show(); view.Focus();
            EditorApplication.isPlaying=true;
        }
        [MenuItem("AlienPurge/Capture/Open Integrated World for Manual Recording")]
        public static void OpenManual(){SessionState.SetBool("ReleaseCapture.Manual",true);Open();}
        public static void EnsureFolders()
        {
            foreach(string folder in new[]{"06_Screenshots/Originals","07_Video/Raw","09_Source","10_QA"})Directory.CreateDirectory(Root+"/"+folder);
        }
        [MenuItem("AlienPurge/Capture/Capture Game View Screenshot")]
        public static void Screenshot()
        {
            if(!EditorApplication.isPlaying||recorder==null)throw new InvalidOperationException("Start a capture first to ensure the 1920x1080 Game View.");
            EnsureFolders();ScreenCapture.CaptureScreenshot(Root+"/06_Screenshots/Originals/Manual_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png");
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("ReleaseCapture.Running",false))return;
            if(state==PlayModeStateChange.ExitingPlayMode) Stop();
            if(state==PlayModeStateChange.EnteredEditMode) { replay=null; }
        }
        static void Tick()
        {
            if(SessionState.GetBool("ReleaseCapture.Running",false) && EditorApplication.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if(replay==null && Object.FindObjectOfType<PlayerMovement>()!=null)
                {
                    var go=new GameObject("EDITOR_ONLY_ReleaseInputReplay");go.hideFlags=HideFlags.DontSave;
                    replay=go.AddComponent<ReleaseInputReplay>(); Application.runInBackground=true;
                    take=SessionState.GetString("ReleaseCapture.Take","Gameplay");
                    File.WriteAllText(Root+"/10_QA/PlayModeEntered.txt",DateTime.Now.ToString("O")+"\n"+Level+"\nActual Unity Play Mode; native input APIs; no health, damage, cooldown, reward or spawn overrides.\n");
                }
                if(replay!=null && recorder==null && replay.Ready && !SessionState.GetBool("ReleaseCapture.Recorded",false))StartRecording();
                if(replay!=null&&replay.Dead&&recorder!=null)Stop();
            }
            if(EditorApplication.timeSinceStartup<nextCommand)return;
            nextCommand=EditorApplication.timeSinceStartup+.3;
            string file=Root+"/09_Source/capture_command.txt";
            if(!File.Exists(file))return;
            string command=File.ReadAllText(file).Trim(); File.Move(file,file+".used-"+DateTime.Now.Ticks);
            if(command=="open")Open();
            if(command=="background")CaptureEnvironment();
            if(command=="audit")ReleaseAudit.Run();
            if(command=="stop")Stop();
            if(command=="exit") { Stop(); SessionState.SetBool("ReleaseCapture.Running",false); EditorApplication.isPlaying=false; }
            if(command=="quit") { Stop(); SessionState.SetBool("ReleaseCapture.Running",false); EditorApplication.isPlaying=false; EditorApplication.delayCall+=()=>EditorApplication.Exit(0); }
            if(command=="pausebot" && replay!=null)replay.SetAutomaticInput(false);
            if(command=="resumebot" && replay!=null)replay.SetAutomaticInput(true);
            if(command=="backpack" && BackpackPanelController.Instance!=null)BackpackPanelController.Instance.OpenPanel();
            if(command=="closebackpack" && BackpackPanelController.Instance!=null)BackpackPanelController.Instance.ClosePanel();
            if(command=="closeloot" && LootSearchPanelController.Instance!=null)LootSearchPanelController.Instance.CloseActiveSearch();
            if(command=="loot" && replay!=null)replay.SeekLoot=true;
            if(command=="selectitem" && BackpackPanelController.IsOpen)
            {var item=Object.FindObjectOfType<InventoryItemView>();if(item!=null)item.GetComponent<Button>()?.onClick.Invoke();}
            if(command.StartsWith("shot "))ScreenCapture.CaptureScreenshot(Root+"/06_Screenshots/Originals/"+command.Substring(5)+".png");
            if(command.StartsWith("record ")) {Stop();take=command.Substring(7);StartRecording();}
        }
        static void StartRecording()
        {
            var settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();settings.FrameRate=30;settings.CapFrameRate=true;
            var movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();movie.name="AlienPurge real Game View";movie.Enabled=true;
            movie.EncoderSettings=new CoreEncoderSettings {EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High,Codec=CoreEncoderSettings.OutputCodec.MP4};
            movie.ImageInputSettings=new GameViewInputSettings {OutputWidth=1920,OutputHeight=1080};
            movie.AudioInputSettings.PreserveAudio=true;movie.CaptureAlpha=false;
            movie.OutputFile=System.IO.Path.GetFullPath(Root+"/07_Video/Raw/"+take);
            settings.AddRecorderSettings(movie);recorder=new RecorderController(settings);recorder.PrepareRecording();
            if(!recorder.StartRecording())throw new InvalidOperationException("Recorder refused to start.");
            SessionState.SetBool("ReleaseCapture.Recorded",true);
            File.AppendAllText(Root+"/07_Video/CAPTURE_EVENTS.log",DateTime.Now.ToString("O")+" START "+take+"\n");
        }
        [MenuItem("AlienPurge/Capture/Stop Recording")]
        public static void Stop()
        {
            if(recorder==null)return;recorder.StopRecording();recorder=null;
            File.AppendAllText(Root+"/07_Video/CAPTURE_EVENTS.log",DateTime.Now.ToString("O")+" STOP "+take+"\n");
        }
        [MenuItem("AlienPurge/Capture/Render Library Environment (Edit Mode)")]
        public static void CaptureEnvironment()
        {
            EnsureFolders();
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Environment artwork is captured only in Edit Mode, separate from real gameplay footage.");
            var loop=Object.FindObjectOfType<RegionLoopController>();
            if(loop==null)throw new InvalidOperationException("Open the integrated scene first.");
            var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.enabled).ToArray();
            var text=Object.FindObjectsOfType<TMP_Text>().Where(t=>t.enabled).ToArray();
            try
            {
                foreach(var c in canvases)c.enabled=false;foreach(var t in text)t.enabled=false;
                Vector3 p=loop.world.boss.transform.position;
                AlienPurge.Level01.Editor.IntegratedFeedbackAudit.Render(p,20,Root+"/09_Source/Arena_Environment_3840x1240.png",3840,1240);
                AlienPurge.Level01.Editor.IntegratedFeedbackAudit.Render(p,15,Root+"/09_Source/Arena_Environment_Close_3840x1240.png",3840,1240);
                File.WriteAllText(Root+"/10_QA/EnvironmentRender.txt",DateTime.Now.ToString("O")+"\nNative 3840x1240 camera render of existing integrated Arena; no screenshot enlargement. UI hidden only for non-gameplay library artwork; original scene not saved. Center="+p);
            }
            finally{foreach(var c in canvases)c.enabled=true;foreach(var t in text)t.enabled=true;}
        }
    }

    [DefaultExecutionOrder(30000)]
    public class ReleaseInputReplay : MonoBehaviour
    {
        public bool Autoplay=true,SeekLoot,AutoLoot;
        public bool Ready=>player!=null && Time.frameCount>10;
        public bool Dead=>health!=null&&health.IsDead;
        PlayerMovement player;PlayerHealth health;PlayerShooting shooting;WeaponManager weapons;PlayerInteractor interactor;
        RegionLoopController loop;EnemyHealth[] enemies=Array.Empty<EnemyHealth>();
        SafeZoneController safe;LootContainer looting;float lootStarted,backpackUntil;
        HiveTyrantCombat boss;
        bool lootedOnce,lootShot,backpackShot;
        Vector3[] path=Array.Empty<Vector3>();int pathIndex;Vector3 pathGoal;bool pathBusy;
        float nextSense,nextPath,nextState,nextUI,nextAbility,started;
        readonly Dictionary<string,MethodInfo> methods=new Dictionary<string,MethodInfo>();
        object Call(object obj,string name,params object[] args)
        {
            if(obj==null)return null;string key=obj.GetType().FullName+"."+name;
            if(!methods.TryGetValue(key,out var method)) {method=obj.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);methods[key]=method;}
            return method?.Invoke(obj,args);
        }
        void Start()
        {
            player=FindObjectOfType<PlayerMovement>();health=player.GetComponent<PlayerHealth>();shooting=player.GetComponent<PlayerShooting>();
            weapons=player.GetComponent<WeaponManager>();interactor=player.GetComponent<PlayerInteractor>();loop=FindObjectOfType<RegionLoopController>();
            safe=FindObjectOfType<SafeZoneController>();
            boss=loop!=null&&loop.world.boss!=null?loop.world.boss.GetComponent<HiveTyrantCombat>():null;
            SetAutomaticInput(!SessionState.GetBool("ReleaseCapture.Manual",false));SessionState.SetBool("ReleaseCapture.Manual",false);started=Time.unscaledTime;
        }
        public void SetAutomaticInput(bool active){Autoplay=active;if(player!=null){player.SetMovementInput(Vector2.zero);player.UseExternalInput=active;}}
        void LateUpdate()
        {
            if(player==null)return;
            if(Time.unscaledTime>=nextState){nextState=Time.unscaledTime+1;WriteState();}
            if(!Autoplay||health.IsDead){player.SetMovementInput(Vector2.zero);return;}
            if(backpackUntil>0)
            {
                player.SetMovementInput(Vector2.zero);
                if(!backpackShot && Time.unscaledTime>backpackUntil-2){ScreenCapture.CaptureScreenshot(ReleaseCapture.Root+"/06_Screenshots/Originals/Backpack_"+DateTime.Now.ToString("HHmmss")+".png");backpackShot=true;}
                if(Time.unscaledTime>backpackUntil){BackpackPanelController.Instance.ClosePanel();backpackUntil=0;}
                return;
            }
            if(LootSearchPanelController.IsOpen)
            {
                player.SetMovementInput(Vector2.zero);
                if(looting!=null)
                {
                    foreach(var entry in looting.SearchEntries.Where(e=>e.State==LootSearchState.Identified).ToArray())looting.TryTakeEntry(entry);
                    if(!lootShot&&Time.unscaledTime-lootStarted>1){ScreenCapture.CaptureScreenshot(ReleaseCapture.Root+"/06_Screenshots/Originals/Loot_"+DateTime.Now.ToString("HHmmss")+".png");lootShot=true;}
                    bool danger=FindObjectsOfType<EnemyHealth>().Any(e=>!e.IsDead&&Vector2.Distance(e.transform.position,player.transform.position)<5);
                    if(Time.unscaledTime-lootStarted>7||danger)
                    {
                        LootSearchPanelController.Instance.CloseActiveSearch();SeekLoot=false;looting=null;
                        BackpackPanelController.Instance.OpenPanel();backpackUntil=Time.unscaledTime+4;
                    }
                }
                return;
            }
            if(UpgradeManager.IsChoosingUpgrade||WeaponModuleSelectionManager.IsChoosingModule)
            {
                player.SetMovementInput(Vector2.zero);
                if(Time.unscaledTime>=nextUI)
                {
                    nextUI=Time.unscaledTime+2.5f;
                    var cards=FindObjectsOfType<Button>().Where(b=>b.IsActive()&&b.interactable && b.GetComponentsInChildren<TMP_Text>().Any(t=>t.name.Contains("Title"))).ToArray();
                    if(cards.Length>0)
                    {
                        string Text(Button b)=>string.Join(" ",b.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
                        int Score(Button b){string s=Text(b);return (s.Contains("闪电")?120:0)+(s.Contains("爆炸")?100:0)+(s.Contains("伤害")?40:0)+(s.Contains("射速")?30:0)+(health.CurrentHealth<health.MaxHealth && (s.Contains("恢复")||s.Contains("生命"))?160:0);}
                        var choice=cards.OrderByDescending(Score).First();
                        File.AppendAllText(ReleaseCapture.Root+"/07_Video/INPUT_REPLAY.log",Time.unscaledTime.ToString("F2")+" CHOOSE "+Text(choice)+"\n");
                        choice.onClick.Invoke();
                    }
                }
                return;
            }
            nextUI=Time.unscaledTime+1.8f;
            if(Time.timeScale<=0 || !GameManager.Instance.IsPlaying){player.SetMovementInput(Vector2.zero);return;}
            if(Time.time>=nextSense){nextSense=Time.time+.2f;enemies=FindObjectsOfType<EnemyHealth>().Where(e=>e.gameObject.activeInHierarchy&&!e.IsDead).ToArray();}
            if(boss!=null&&!boss.Health.IsDead&&loop.world.arenaBridge.encounterStarted&&!loop.world.arenaBridge.encounterFinished)
            {PlayBossInputs();return;}
            Vector2 p=player.transform.position;
            var close=enemies.Where(e=>e!=null&&!e.IsDead).OrderBy(e=>((Vector2)e.transform.position-p).sqrMagnitude).ToArray();
            EnemyHealth target=close.FirstOrDefault(e=>Vector2.Distance(p,e.transform.position)<16 && Visible(p,e.transform.position));
            Vector3 goal=Goal(close);
            Vector2 desired=Navigate(goal);
            float nearest=close.Length>0?Vector2.Distance(p,close[0].transform.position):999;
            if(nearest<5.8f)
            {
                float best=-9999;Vector2 bestDir=Vector2.zero;
                for(int i=0;i<16;i++)
                {
                    Vector2 dir=new Vector2(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8));Vector2 future=p+dir*2;
                    if(!Clear(p,future))continue;
                    float space=close.Take(8).Min(e=>Vector2.Distance(future,e.transform.position));
                    float score=space*4+Vector2.Dot(dir,desired)*1.5f;
                    if(safe!=null&&safe.HasActiveZone&&!safe.IsPositionSafe(future))score-=30;
                    if(score>best){best=score;bestDir=dir;}
                }
                desired=bestDir;
            }
            player.SetMovementInput(desired);
            if(target!=null && (bool)Call(shooting,"CanProcessShootingInput"))
            {weapons.AimAt(target.transform.position);Call(shooting,"TryShoot");}
            if(Time.time>=nextAbility)
            {
                nextAbility=Time.time+.4f;
                if(nearest<3.8f)Call(player.GetComponent<PlayerPulseSkill>(),"TryStartPulse");
                if(nearest<3.1f)Call(player.GetComponent<PlayerJetJump>(),"TryStartJump");
                if(nearest<1.9f)Call(player.GetComponent<PlayerDash>(),"TryStartDash");
            }
            if(interactor!=null && (bool)Call(interactor,"CanPlayerInteract"))
            {
                Call(interactor,"FindClosestInteractable");var current=interactor.CurrentInteractable;
                if(current is RegionTerminal terminal && loop.CanActivate(terminal.regionId) && current.CanInteract(interactor) && !(bool)Call(interactor,"IsInteractionBlocked",current))
                {File.AppendAllText(ReleaseCapture.Root+"/07_Video/INPUT_REPLAY.log",Time.unscaledTime.ToString("F2")+" INTERACT "+terminal.regionId+"\n");current.Interact(interactor);}
                else if(SeekLoot&&current is LootContainer crate&&current.CanInteract(interactor)&&!(bool)Call(interactor,"IsInteractionBlocked",current))
                {looting=crate;lootStarted=Time.unscaledTime;current.Interact(interactor);}
            }
        }
        bool Clear(Vector2 from,Vector2 to)
        {
            var hits=Physics2D.CircleCastAll(from,.35f,(to-from).normalized,Vector2.Distance(from,to));
            return !hits.Any(h=>h.collider!=null&&!h.collider.isTrigger && h.collider.GetComponentInParent<PlayerHealth>()==null && h.collider.GetComponentInParent<EnemyHealth>()==null && h.collider.GetComponentInParent<Bullet>()==null && h.collider.GetComponentInParent<EnemyProjectile>()==null);
        }
        bool Visible(Vector2 from,Vector2 to)
        {
            return !Physics2D.LinecastAll(from,to).Any(h=>h.collider!=null&&!h.collider.isTrigger && h.collider.GetComponentInParent<PlayerHealth>()==null && h.collider.GetComponentInParent<EnemyHealth>()==null && h.collider.GetComponentInParent<Bullet>()==null && h.collider.GetComponentInParent<EnemyProjectile>()==null);
        }
        Vector3 Goal(EnemyHealth[] close)
        {
            if(safe!=null&&safe.HasActiveZone)return safe.CurrentCenter;
            if(AutoLoot&&!lootedOnce&&loop!=null&&loop.CurrentRegion=="ColonyEntry"&&loop.Regions[0].objective==RegionObjectiveState.Completed)
            {lootedOnce=true;SeekLoot=true;}
            if(SeekLoot)
            {
                var crate=FindObjectsOfType<LootContainer>().Where(c=>c.State==LootContainerState.Closed||c.SearchEntries.Any(e=>!e.IsTaken)).OrderBy(c=>Vector3.Distance(c.transform.position,player.transform.position)).FirstOrDefault();
                if(crate!=null)return crate.transform.position;
            }
            if(loop==null)return player.transform.position;
            var state=loop.Regions.FirstOrDefault(s=>s.objective!=RegionObjectiveState.Completed);
            if(state!=null)
            {
                if(state.config.v02.extractionRegion&&!loop.HasMilestone("BossDefeated"))return loop.world.boss.transform.position;
                var guard=state.enemies.Where(t=>!t.dead&&t.keyGuard&&t.lease!=null).OrderBy(t=>Vector3.Distance(player.transform.position,t.lease.transform.position)).FirstOrDefault();
                if(guard!=null)return guard.lease.transform.position;
                return loop.Terminal(state.config.regionId).transform.position;
            }
            if(loop.extraction!=null&&loop.extraction.HasActiveExtractionTarget)return loop.extraction.CurrentExtractionPosition;
            return player.transform.position;
        }
        // Read visible telegraph geometry and submit ordinary movement/ability inputs.
        // Adapted from the project's existing playtest pilot; no encounter or combat state is changed.
        void PlayBossInputs()
        {
            Vector2 current=player.transform.position,center=boss.Motor.CombatOrigin;
            var adds=enemies.Where(e=>e!=null&&!e.IsDead&&e!=boss.Health).OrderBy(e=>Vector2.Distance(current,e.transform.position)).ToArray();
            Vector2 target=adds.Length>0&&Vector2.Distance(current,adds[0].transform.position)<6?(Vector2)adds[0].transform.position:center;
            if((bool)Call(shooting,"CanProcessShootingInput")){weapons.AimAt(target);Call(shooting,"TryShoot");}
            Vector2 radial=current-center;if(radial.sqrMagnitude<.01f)radial=Vector2.down;
            float angle=Mathf.Atan2(radial.y,radial.x)+.10f;
            Vector2 destination=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*8.2f;
            if(!boss.InsideArena(destination))destination=center+Vector2.down*8.2f;
            var wave=boss.Wave;
            if(wave.Plan!=null&&(wave.IsWarning||wave.IsTravelling))
            {
                var plan=wave.Plan;float z=Vector2.Dot(current-plan.origin,plan.forward),x=Vector2.Dot(current-plan.origin,plan.right);
                int gap=-1;float distance=float.PositiveInfinity;
                for(int i=0;i<plan.GapCount;i++)if(plan.reachableGaps[i]&&Mathf.Abs(x-plan.GapCenter(i))<distance)
                {gap=i;distance=Mathf.Abs(x-plan.GapCenter(i));}
                if(gap>=0)destination=plan.Point(plan.GapCenter(gap),Mathf.Clamp(z,2,10));
            }
            Vector2 desired=(destination-current).normalized,chosen=desired;float best=float.NegativeInfinity;
            for(int i=0;i<20;i++)
            {
                Vector2 direction=new Vector2(Mathf.Cos(i*Mathf.PI*.1f),Mathf.Sin(i*Mathf.PI*.1f));Vector2 point=current+direction*1.25f;
                if(!boss.InsideArena(point)||Physics2D.CircleCast(current,.35f,direction,1.4f,boss.Config.environmentLayers).collider!=null)continue;
                float score=Vector2.Dot(direction,desired)*2;
                if(boss.Pressure.SporeDanger(point,.55f))score-=15;
                if(boss.Pressure.SporeDanger(current+direction*2.5f,.35f))score-=4;
                foreach(var add in adds)score-=Mathf.Max(0,2-Vector2.Distance(point,add.transform.position))*5;
                if(Vector2.Distance(point,center)<3.5f)score-=15;
                if(score>best){best=score;chosen=direction;}
            }
            player.SetMovementInput(chosen);
            bool danger=boss.State==HiveTyrantState.SweepWindup&&boss.SweepReleaseAt-Time.time<.18f&&
                HiveTyrantCombat.ContainsPoint(boss.SweepOrigin,boss.SweepDirection,current,boss.Config.sweepRadius+.3f,boss.Config.sweepAngle);
            foreach(var arc in boss.Pressure.Crescents)
            {
                if(!arc.active)continue;Vector2 relative=current-arc.forward*(arc.distance+arc.speed*.16f);
                for(int i=0;i<HiveTyrantPressureAttacks.ArcSegments;i++)
                {
                    if(arc.stops[i]<arc.distance)continue;Vector2 edge=arc.b[i]-arc.a[i];
                    Vector2 point=arc.a[i]+edge*Mathf.Clamp01(Vector2.Dot(relative-arc.a[i],edge)/edge.sqrMagnitude);
                    if(Vector2.Distance(relative,point)<.85f){danger=true;break;}
                }
            }
            if(danger)Call(player.GetComponent<PlayerJetJump>(),"TryStartJump");
            if(adds.Length>0&&Vector2.Distance(current,adds[0].transform.position)<3.8f)Call(player.GetComponent<PlayerPulseSkill>(),"TryStartPulse");
        }
        Vector2 Navigate(Vector3 goal)
        {
            Vector3 p=player.transform.position;
            if(Vector2.Distance(p,goal)<1.2f)return Vector2.zero;
            if(AstarPath.active!=null&&!pathBusy&&Time.time>=nextPath&&(Vector3.Distance(goal,pathGoal)>1||pathIndex>=path.Length||Time.time>=nextPath+1))
            {
                nextPath=Time.time+.6f;pathGoal=goal;pathBusy=true;
                AstarPath.StartPath(ABPath.Construct(p,goal,result=>{pathBusy=false;path=result.error?Array.Empty<Vector3>():result.vectorPath.ToArray();pathIndex=0;}));
            }
            while(pathIndex<path.Length&&Vector2.Distance(p,path[pathIndex])<.65f)pathIndex++;
            Vector2 dir=pathIndex<path.Length?(Vector2)(path[pathIndex]-p):(Vector2)(goal-p);
            dir=dir.normalized;
            if(Clear(p,(Vector2)p+dir*1.6f))return dir;
            // Slide around real wall colliders using the same motor; never move a Transform directly.
            float best=-999;Vector2 chosen=Vector2.zero,toward=((Vector2)(goal-p)).normalized;
            for(int i=0;i<16;i++)
            {
                Vector2 candidate=new Vector2(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8));
                if(!Clear(p,(Vector2)p+candidate*1.6f))continue;
                float score=Vector2.Dot(candidate,toward)*3+Vector2.Dot(candidate,dir);
                if(score>best){best=score;chosen=candidate;}
            }
            return chosen;
        }
        void WriteState()
        {
            var data=new StateData{elapsed=Time.unscaledTime-started,gameTime=Time.time,hp=health.CurrentHealth,maxHp=health.MaxHealth,dead=health.IsDead,
                player=player.transform.position,region=loop!=null?loop.CurrentRegion:"",enemies=enemies.Count(e=>e!=null&&!e.IsDead),
                selecting=UpgradeManager.IsChoosingUpgrade||WeaponModuleSelectionManager.IsChoosingModule,
                levels=player.GetComponent<PlayerExperience>().CurrentLevel,frame=Time.frameCount,
                bossHp=boss!=null?boss.Health.CurrentHealth:0,bossMaxHp=boss!=null?boss.Health.MaxHealth:0,bossState=boss!=null?boss.State.ToString():"",phase2=boss!=null&&boss.Phase2Entered,
                extraction=loop!=null&&loop.extraction!=null?loop.extraction.CurrentState.ToString():"",
                regions=loop!=null?loop.Regions.Select(s=>s.config.regionId+":"+s.objective+":"+s.enemies.Count(t=>!t.dead&&t.keyGuard)).ToArray():Array.Empty<string>()};
            File.WriteAllText(ReleaseCapture.Root+"/10_QA/LiveState.json",JsonUtility.ToJson(data,true));
            File.AppendAllText(ReleaseCapture.Root+"/07_Video/Raw/"+SessionState.GetString("ReleaseCapture.Take","Gameplay")+".jsonl",JsonUtility.ToJson(data)+"\n");
        }
        [Serializable] class StateData {public float elapsed,gameTime;public int hp,maxHp,enemies,levels,frame,bossHp,bossMaxHp;public bool dead,selecting,phase2;public Vector3 player;public string region,bossState,extraction;public string[] regions;}
        void OnDisable(){if(player!=null){player.SetMovementInput(Vector2.zero);player.UseExternalInput=false;}}
    }
}
