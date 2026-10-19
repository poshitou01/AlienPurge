using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    // Editor-only driver: real pooled deaths, physics pickups, native reward buttons, search and firing.
    // Travel and combat damage are deterministic test inputs, not a balance or aiming certification.
    [InitializeOnLoad]
    public static partial class RegionLoopV02Regression
    {
        const string Key="RegionV02.Regression";
        static string Report=>IntegratedTest?IntegratedWorldInstaller.Reports+(SessionState.GetBool(Key+".backpack",false)?"/Backpack/PlayMode.txt":SessionState.GetBool(Key+".upgradeVfx",false)?"/SpecialUpgrades/PlayMode.txt":SessionState.GetBool(Key+".worldFeedback",false)?"/WorldFeedback/PlayMode.txt":SessionState.GetBool(Key+".locomotion",false)?"/PlayerLocomotion.txt":SessionState.GetBool(Key+".lifecycle",false)?"/Lifecycle.txt":"/PlayMode.txt"):RegionLoopV02Installer.Reports+(SessionState.GetBool(Key+".impact",false)?"/ImpactChecks.txt":SessionState.GetBool(Key+".player",false)?"/PlayerChecks.txt":SessionState.GetBool(Key+".abilities",false)?"/AbilityChecks.txt":SessionState.GetBool(Key+".polish",false)?"/CombatUIChecks.txt":SessionState.GetBool(Key+".feedback",false)?"/FeedbackChecks.txt":SessionState.GetBool(Key+".preview",false)?"/TaskPreview.txt":SessionState.GetBool(Key+".presentation",false)?"/Presentation.txt":"/PlayMode.txt");
        static readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
        static readonly List<string> results=new List<string>(), errors=new List<string>();
        static RegionLoopController loop;
        static double deadline,nextDiagnostic;
        static bool choose=true,invulnerable=true,accelerate=true;
        static UpgradeType[] pair;
        static int upgradeClicks,moduleClicks;
        static int lastFrame=-1;
        static RegionLoopV02Regression()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=Mode;
            Application.logMessageReceived+=(message,stack,type)=>{if(routines.Count>0&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors.Add(message+"\n"+stack);};
        }
        [MenuItem("AlienPurge/Gameplay/Level01/Run Region Loop V02 Regression")]
        public static void Run()=>RunFeedback();
        // Focused rerender after scene-only presentation repairs; retains the full behavior report.
        public static void RunPresentation()=>Start(true);
        public static void RunPreview()=>Start(true,true);
        static void Start(bool presentation,bool preview=false,bool feedback=false,bool polish=false,bool abilities=false,bool player=false,bool impact=false)
        {
            SessionState.SetBool(Key+".integrated",false);
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first.");
            SessionState.SetBool(Key+".presentation",presentation);
            SessionState.SetBool(Key+".preview",preview);
            SessionState.SetBool(Key+".feedback",feedback);
            SessionState.SetBool(Key+".polish",polish);
            SessionState.SetBool(Key+".abilities",abilities);
            SessionState.SetBool(Key+".player",player);
            SessionState.SetBool(Key+".impact",impact);
            RegionLoopV02Installer.Install();
            string guid=AssetDatabase.AssetPathToGUID(RegionLoopV02Installer.Target);
            int objects=RegionLoopInstaller.All<Transform>(SceneManager.GetActiveScene()).Length;
            var installedPaths=RegionLoopInstaller.All<Transform>(SceneManager.GetActiveScene()).Select(TransformPath).ToArray();
            RegionLoopV02Installer.Install();
            var reinstalledPaths=RegionLoopInstaller.All<Transform>(SceneManager.GetActiveScene()).Select(TransformPath).ToArray();
            if(objects!=reinstalledPaths.Length||guid!=AssetDatabase.AssetPathToGUID(RegionLoopV02Installer.Target))
                throw new InvalidOperationException("Non-idempotent installer: "+objects+" -> "+reinstalledPaths.Length+
                    " added="+string.Join(",",reinstalledPaths.Except(installedPaths))+" removed="+string.Join(",",installedPaths.Except(reinstalledPaths)));
            RegionLoopV02Installer.Validate();
            // Set before entering Play / domain reload, before SaveManager/bootstrap Awake can run.
            SessionState.SetString(Key+".product",PlayerSettings.productName);
            PlayerSettings.productName="AlienPurge_RegionLoopV02_Regression";
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+".batch",Application.isBatchMode);
            File.WriteAllText(Report,"RUNNING: isolated profile selected before initialization.\n");
            EditorApplication.isPlaying=true;
        }
        static string TransformPath(Transform item)
        { string path=item.name; for(var parent=item.parent;parent!=null;parent=parent.parent)path=parent.name+"/"+path; return path; }
        static void Mode(PlayModeStateChange mode)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(mode==PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear();errors.Clear();routines.Clear();loop=null;
                choose=invulnerable=accelerate=true;lastFrame=-1;upgradeClicks=moduleClicks=0;
                deadline=EditorApplication.timeSinceStartup+1200;
                Application.runInBackground=true;
                var view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Show();view.Focus();
                routines.Push(IntegratedTest?(SessionState.GetBool(Key+".backpack",false)?IntegratedBackpackChecks():SessionState.GetBool(Key+".upgradeVfx",false)?IntegratedUpgradeVfxChecks():SessionState.GetBool(Key+".worldFeedback",false)?IntegratedWorldFeedbackChecks():SessionState.GetBool(Key+".locomotion",false)?IntegratedPlayerLocomotion():SessionState.GetBool(Key+".lifecycle",false)?IntegratedLifecycle():IntegratedSuite()):SessionState.GetBool(Key+".impact",false)?ImpactChecks():SessionState.GetBool(Key+".player",false)?PlayerPresentation():SessionState.GetBool(Key+".abilities",false)?Abilities():SessionState.GetBool(Key+".polish",false)?Polish():SessionState.GetBool(Key+".feedback",false)?Feedback():SessionState.GetBool(Key+".preview",false)?Preview():SessionState.GetBool(Key+".presentation",false)?Presentation():Exercise());
            }
            if(mode==PlayModeStateChange.EnteredEditMode)
            {
                PlayerSettings.productName=SessionState.GetString(Key+".product","AlienPurge");SessionState.SetBool(Key,false);
                try { ValidateTestProtection(); }
                catch(Exception e){File.AppendAllText(Report,"\nPROTECTION FAIL "+e);SessionState.SetBool(Key+".failed",true);}
                if(SessionState.GetBool(Key+".batch",false))EditorApplication.Exit(SessionState.GetBool(Key+".failed",false)?1:0);
            }
        }
        static void Tick()
        {
            if(routines.Count==0||!Application.isPlaying)return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.isPaused)EditorApplication.isPaused=false;
                if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Suite deadline; see Logs/RegionV02State.txt");
                if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
                if(loop!=null&&!loop.ShuttingDown)
                {
                    if (SessionState.GetBool(Key+".feedback",false)) loop.world.player.GetComponent<PlayerShooting>().enabled=false;
                    if(invulnerable)loop.world.player.GetComponent<PlayerHealth>().BeginTemporaryInvulnerability(1000);
                    Check(!(UpgradeManager.IsChoosingUpgrade&&WeaponModuleSelectionManager.IsChoosingModule),"No overlapping choices",false);
                    Check(loop.pool.ActiveCount<=loop.globalAliveLimit,"Global active population bounded",false);
                    if(IntegratedTest)ObserveIntegrated();
                    if(choose)ChooseCard();
                    if(accelerate&&Time.timeScale>0&&!UpgradeManager.IsChoosingUpgrade&&!WeaponModuleSelectionManager.IsChoosingModule)Time.timeScale=8;
                }
                if(EditorApplication.timeSinceStartup>nextDiagnostic)
                {
                    nextDiagnostic=EditorApplication.timeSinceStartup+10;
                    File.WriteAllText("Logs/RegionV02State.txt","t="+Time.time+" scale="+Time.timeScale+" stage="+results.LastOrDefault()+
                        (loop==null?"": " region="+loop.CurrentRegion+" storm="+loop.storm.CurrentPhase+" fog="+loop.fog.DistanceTravelled+"\n"+
                        string.Join("\n",loop.Regions.Select(s=>s.config.regionId+" entered="+s.firstEntered+" wave="+s.environmentWave+" residents="+s.Remaining(RegionEnemyRole.Resident)+" mission="+s.Remaining(RegionEnemyRole.Mission)+" patrol="+s.Remaining(RegionEnemyRole.Patrol)+" diagnostic="+s.diagnostic))));
                }
                var top=routines.Peek();if(!top.MoveNext())routines.Pop();else if(top.Current is IEnumerator nested)routines.Push(nested);
                if(routines.Count==0)Finish(false,null);
            }
            catch(Exception e){Finish(true,e.ToString());}
        }
        static void Finish(bool failed,string message)
        {
            routines.Clear();if(message!=null)results.Add("FAIL: "+message);
            File.WriteAllText(Report,(failed?"FAIL":"PASS")+" | Unity "+Application.unityVersion+" | "+DateTime.UtcNow.ToString("u")+"\n"+string.Join("\n",results)+"\n");
            SessionState.SetBool(Key+".failed",failed);Time.timeScale=1;EditorApplication.isPlaying=false;
        }
        static void Check(bool value,string message,bool record=true)
        {
            if(!value)throw new InvalidOperationException(message);
            if(record){results.Add("PASS: "+message);Debug.Log("[RegionV02Test] "+message);}
        }
        static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                ScreenCapture.CaptureScreenshot(Path.GetFullPath((IntegratedTest?IntegratedWorldInstaller.Reports:RegionLoopV02Installer.Reports) + "/" + name + ".png"));
        }
        static T Read<T>(object obj,string field) => (T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(obj);
        static IEnumerator Until(Func<bool> condition,string label,float timeout=90)
        {
            double end=EditorApplication.timeSinceStartup+timeout;
            while(!condition()){if(EditorApplication.timeSinceStartup>end)throw new TimeoutException(label);yield return null;}
        }
        static IEnumerator Seconds(float seconds)
        {float until=Time.time+seconds;while(Time.time<until)yield return null;}
        static void Move(Vector3 position,bool root=false)
        {
            var p=loop.world.player.transform;p.position=root?position:p.position+position-loop.world.PlayerFeet;Physics2D.SyncTransforms();
        }
        static int TotalXP()=>5*loop.rewards.experience.CurrentLevel*(loop.rewards.experience.CurrentLevel-1)/2+loop.rewards.experience.CurrentExperience;
        static IEnumerator Collect()
        {
            double end=EditorApplication.timeSinceStartup+25;
            yield return Seconds(.3f);
            while(true)
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("Physics XP pickup");
                var orbs=Object.FindObjectsOfType<ExperienceOrb>();if(orbs.Length==0)break;
                Move(orbs[0].transform.position,true);yield return null;
            }
            yield return Until(()=>!loop.rewards.HasPending,"Reward queue drains");
        }
        static IEnumerator Clear(RegionRunState state,RegionEnemyRole role)
        {
            double end=EditorApplication.timeSinceStartup+50;
            while(state.Remaining(role)>0)
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("Clear "+state.config.regionId+" "+role+" "+state.diagnostic);
                foreach(var t in state.enemies.ToArray())if(t.role==role&&t.lease!=null&&!t.dead)t.lease.Health.TakeDamage(int.MaxValue);
                yield return null;
            }
            yield return Collect();
        }
        static void ChooseCard()
        {
            if(UpgradeManager.IsChoosingUpgrade)
            {
                var options=Read<UpgradeOptionData[]>(loop.rewards.upgrades,"displayedOptions");var buttons=Read<Button[]>(loop.rewards.upgrades,"upgradeButtons");
                int index=Array.FindIndex(options,o=>o!=null&&!loop.rewards.modules.IsModuleType(o.Type));
                if(index<0)index=Array.FindIndex(options,o=>o!=null&&pair!=null&&pair.Contains(o.Type));
                if(index<0)throw new InvalidOperationException("No valid non-conflicting normal upgrade");
                buttons[index].onClick.Invoke();upgradeClicks++;
            }
            else if(WeaponModuleSelectionManager.IsChoosingModule)
            {
                var modules=loop.rewards.modules;var choices=Read<UpgradeType[]>(modules,"displayedModules");var valid=Read<bool[]>(modules,"displayedModuleValid");
                var buttons=Read<Button[]>(modules,"moduleButtons");int index=-1;
                if(modules.UnlockedModuleCount<2&&pair!=null)
                {
                    var wanted=pair[modules.UnlockedModuleCount];
                    for(int i=0;i<choices.Length;i++)if(valid[i]&&choices[i]==wanted)index=i;
                    if(index<0)
                    {
                        // The native first selection offers a random three of four. Control only this
                        // test fixture's legal candidate roll; reward issuance and the click stay native.
                        for(int roll=0;roll<32&&index<0;roll++)
                        {
                            modules.GetType().GetMethod("SelectModuleOptions",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(modules,null);
                            choices=Read<UpgradeType[]>(modules,"displayedModules");valid=Read<bool[]>(modules,"displayedModuleValid");
                            for(int i=0;i<choices.Length;i++)if(valid[i]&&choices[i]==wanted)index=i;
                        }
                        modules.GetType().GetMethod("RefreshModuleButtons",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(modules,null);
                        results.Add("FIXTURE: rerolled native legal card candidates to cover "+wanted+"; used the original selection button.");
                        if(index<0)throw new InvalidOperationException("Native candidate set cannot offer "+wanted);
                    }
                }
                else for(int i=0;i<choices.Length;i++)if(valid[i]){index=i;break;}
                if(index<0)throw new InvalidOperationException("No native module/overclock card");
                buttons[index].onClick.Invoke();moduleClicks++;
            }
        }
        static IEnumerator Fresh()
        {
            string old=loop==null?null:loop.RunId;loop=null;
            GameManager.Instance.RestartGame();yield return null;yield return null;
            yield return Until(()=>Object.FindObjectOfType<RegionLoopController>()!=null,"Restart scene");
            loop=Object.FindObjectOfType<RegionLoopController>();
            yield return Until(()=>loop.Regions.Count==loop.definitions.Length&&loop.Regions[0].initialized,"First entry initializes");
            Check(loop.RunId!=old&&!loop.HasMilestone("Arrival")&&loop.rewards.modules.UnlockedModuleCount==0,"Restart resets milestones, modules and run identity");
        }
        static IEnumerator MainRoute(bool sustained,bool simultaneous=false)
        {
            for(int i=0;i<3;i++)
            {
                var state=loop.Regions[i];Move(loop.Terminal(state.config.regionId).transform.position);
                yield return Until(()=>loop.CurrentRegion==state.config.regionId&&state.initialized,"Enter "+state.config.regionId);
                yield return Clear(state,RegionEnemyRole.Resident);
                if(sustained)
                {
                    for(int wave=1;wave<=3;wave++)
                    {
                        Move(loop.Terminal(state.config.regionId).transform.position);
                        yield return Until(()=>state.environmentWave>=wave,"Recurring environment wave "+wave);
                        yield return Clear(state,RegionEnemyRole.Patrol);
                    }
                }
                if(simultaneous&&i==1)
                {
                    while(loop.rewards.experience.ExperienceToNextLevel-loop.rewards.experience.CurrentExperience>state.config.missionExperience)
                    {
                        int next=state.environmentWave+1;Move(loop.Terminal(state.config.regionId).transform.position);
                        yield return Until(()=>state.environmentWave>=next,"Battle supplies XP for concurrent reward boundary");
                        yield return Clear(state,RegionEnemyRole.Patrol);
                    }
                    choose=false;
                }
                Move(loop.Terminal(state.config.regionId).transform.position);
                loop.Terminal(state.config.regionId).Interact(loop.world.player.GetComponent<PlayerInteractor>());
                yield return Until(()=>loop.HasMilestone(state.config.missionId),"Main activation "+state.config.missionId);
                if(simultaneous&&i==1)
                {
                    Check(loop.rewards.PendingCount>=2,"Real main XP crosses a level threshold and queues both level and core rewards");
                    yield return Until(()=>UpgradeManager.IsChoosingUpgrade,"Concurrent level card opens first");
                    Check(!WeaponModuleSelectionManager.IsChoosingModule&&loop.rewards.modules.UnlockedModuleCount==0,"Core never applies or overlays before the level choice");
                    Capture("RewardQueue_LevelBeforeCore");yield return null;yield return null;choose=true;
                }
                yield return Until(()=>!loop.rewards.HasPending,"Main + level queue");
                Check(!loop.world.progression.gates.First(g=>g.gateId==state.config.exitGateId).locked&&!loop.TryActivate(state.config.regionId),state.config.exitGateId+" opens once through main authority");
                results.Add("MEASURE: "+state.config.regionId+" level="+loop.rewards.experience.CurrentLevel+" cumulativeXP="+TotalXP()+" ambientWaves="+state.environmentWave);
                if (sustained) { yield return Seconds(.3f); Capture("RouteA_" + state.config.regionId); yield return null; yield return null; }
            }
            Check(loop.rewards.WasApplied("mechanism/CargoYard")&&loop.rewards.WasApplied("mechanism/PowerMaintenance")&&loop.rewards.modules.UnlockedModuleCount==2,"Cargo and Power each deliver exactly one core choice");
        }
        static IEnumerator VerifyShot(WeaponEvolutionType expected)
        {
            bool wasAccelerated=accelerate;accelerate=false;Time.timeScale=1;
            var evolution=loop.world.player.GetComponent<WeaponEvolutionController>();
            if(evolution==null)evolution=Object.FindObjectOfType<WeaponEvolutionController>();
            Check(evolution.CurrentEvolution==expected,"Immediate actual evolution: "+expected);
            Check(!loop.rewards.WasApplied("power-spike"),"Signature evolution excludes the Overclock entitlement");
            var weapon=loop.world.player.GetComponent<WeaponManager>();if(weapon==null)weapon=Object.FindObjectOfType<WeaponManager>();
            var center=loop.stormCandidates.First(p=>loop.world.ResolveRegion(loop.world.Local(p.position))=="PowerMaintenance").position;
            Move(center,true);
            var targets=new List<PooledEnemy>();
            for(int i=0;i<2;i++)
            {
                var target=loop.pool.GetEnemy(EnemyType.Normal,center+Vector3.right*2.5f+Vector3.up*i*1.5f,Quaternion.identity);
                target.GetComponent<EnemyDefinition>().InitializeInstanceAttributes(100,1,1,1);
                target.GetComponent<EnemyMovement>().SetMovementLocked(true);
                var nav=target.GetComponent<EnemyPathNavigator>();if(nav!=null)nav.enabled=false;
                targets.Add(target);
            }
            Physics2D.SyncTransforms();
            yield return Seconds(1);
            // Native input updates aim every frame; set this deterministic input immediately before firing.
            foreach(var oldBullet in Object.FindObjectsOfType<Bullet>())oldBullet.ReturnToPool();
            weapon.AimAt(targets[0].GetComponentInChildren<Collider2D>().bounds.center);
            Check(weapon.TryFire(),"Native weapon fires after choice closes");
            var bullet=Object.FindObjectsOfType<Bullet>().FirstOrDefault(b=>Read<WeaponEvolutionData>(b,"currentEvolutionVisualData")!=null);
            Check(bullet!=null,"Fired bullet receives native evolution visual data");
            var snapshot=bullet.ModifierSnapshot;var modifiers=loop.world.player.GetComponent<PlayerWeaponModifiers>();
            Check(expected==WeaponEvolutionType.ThunderPiercer?snapshot.PierceCount>modifiers.PierceCount&&snapshot.ChainLightning:snapshot.Explosive&&snapshot.SplitShot&&snapshot.SplitCount>modifiers.SplitCount,"Fired projectile carries evolved damage-mechanism snapshot");
            int chains=0,splits=0,explosions=0;
            float sampleUntil=Time.time+1.5f;
            while(Time.time<sampleUntil)
            {
                chains=Mathf.Max(chains,bullet.ChainTriggerCount);
                splits=Mathf.Max(splits,Read<int>(bullet,"totalSplitSpawnCount"));
                explosions=Mathf.Max(explosions,Read<int>(bullet,"lastExplosionTargetCount"));
                yield return null;
            }
            Check(targets.All(t=>t.GetComponent<EnemyHealth>().CurrentHealth<100),"Real projectile collision and secondary damage affect both targets; HP="+string.Join(",",targets.Select(t=>t.GetComponent<EnemyHealth>().CurrentHealth))+" direct="+bullet.DirectHitEnemyCount+" chain="+bullet.ChainTriggerCount);
            Check(expected==WeaponEvolutionType.ThunderPiercer?chains>0:splits>0&&explosions>0,"Native evolved chain / explosion and split behavior actually fires");
            foreach(var target in targets)target.ReturnToPool();
            Capture("Evolution_"+expected);
            accelerate=wasAccelerated;
        }
        static IEnumerator Exercise()
        {
            yield return null;loop=Object.FindObjectOfType<RegionLoopController>();
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"),"Independent test profile active from initialization: "+Application.persistentDataPath);
            yield return Until(()=>loop.Regions[0].initialized&&loop.State("CargoYard").enemies.Any(t=>t.lease!=null),"Preload neighboring garrison");
            Check(loop.State("CargoYard").firstEntered<0,"Cargo residents exist before first entry; entry clock untouched");
            var entry=loop.Regions[0];var ticket=entry.enemies.First(t=>t.lease!=null);int xp=TotalXP(),kills=GameManager.Instance.KillCount;
            var health=ticket.lease.Health;int amount=health.ExperienceAmount;health.TakeDamage(int.MaxValue);health.TakeDamage(int.MaxValue);
            yield return Seconds(.25f);
            Check(TotalXP()==xp,"Death never directly duplicates orb XP");
            yield return Collect();
            Check(TotalXP()==xp+amount&&GameManager.Instance.KillCount==kills+1,"Repeated damage drops and credits exactly one native orb");
            var recycled=entry.enemies.First(t=>t.lease!=null);recycled.lease.Health.TakeDamage(1);
            int savedHealth=recycled.lease.Health.CurrentHealth;int beforeReturn=TotalXP();int beforeKills=GameManager.Instance.KillCount;
            recycled.lease.PoolMember.ReturnToPool();
            Check(!recycled.dead&&TotalXP()==beforeReturn&&GameManager.Instance.KillCount==beforeKills,"Pool return is not a death, XP or garrison completion");
            yield return Until(()=>recycled.lease!=null,"Resident returns from pool");
            Check(recycled.lease.Health.CurrentHealth==savedHealth&&recycled.lease.RunId==loop.RunId,"Pool reuse restores survivor HP and current ownership");
            amount=recycled.lease.Health.ExperienceAmount;recycled.lease.Health.TakeDamage(int.MaxValue);yield return Collect();
            Check(TotalXP()==beforeReturn+amount,"Reused enemy produces exactly one correctly valued experience orb");
            pair=new[]{UpgradeType.Piercing,UpgradeType.ChainLightning};
            yield return MainRoute(true);yield return VerifyShot(WeaponEvolutionType.ThunderPiercer);
            Check(upgradeClicks>=5,"Skipping all optional tasks still produces real ordinary level choices");
            Move(loop.world.World(new Vector2(-100.8f,21)));
            yield return Until(()=>loop.CurrentRegion=="QuarantineCommons","Reach open G3 destination");
            Check(!loop.missionView.HasActiveMission&&loop.missionView.CurrentMissionState==MissionState.Completed,"After G3, native task arrow stops pointing back into Power");
            Capture("G3_Complete");yield return null;yield return null;
            var combinations=new[]{new[]{UpgradeType.Piercing,UpgradeType.ChainLightning},new[]{UpgradeType.ChainLightning,UpgradeType.Piercing},new[]{UpgradeType.Explosive,UpgradeType.SplitShot},new[]{UpgradeType.SplitShot,UpgradeType.Explosive},new[]{UpgradeType.Piercing,UpgradeType.Explosive}};
            foreach(var combination in combinations)
            {
                pair=combination;yield return Fresh();yield return MainRoute(false,combination==combinations[0]);
                Check(GameManager.Instance.SurvivalTime<240,"Two event rewards obtained at "+Mathf.RoundToInt(GameManager.Instance.SurvivalTime)+"s, before 240 seconds: "+string.Join(" + ",combination));
                if(combination==combinations.Last())Check(loop.rewards.WasApplied("power-spike")&&loop.rewards.modules.RegionPowerSpikeResult=="Overclock","Non-signature pair receives one native Overclock");
                else yield return VerifyShot(combination.Contains(UpgradeType.Explosive)?WeaponEvolutionType.ClusterBurst:WeaponEvolutionType.ThunderPiercer);
            }
            pair=new[]{UpgradeType.Piercing,UpgradeType.ChainLightning};yield return Fresh();
            var modifiers=loop.world.player.GetComponent<PlayerWeaponModifiers>();
            Check(loop.rewards.modules.UnlockModule(UpgradeType.Piercing),"Existing public module unlock entry remains valid");
            while(modifiers.CanUpgradePiercing)modifiers.ApplyPiercingUpgrade();
            Check(loop.rewards.modules.UnlockModule(UpgradeType.Explosive),"Second public module unlock queues non-signature reward after real write");
            while(modifiers.CanUpgradeExplosive)modifiers.ApplyExplosiveUpgrade();
            yield return Until(()=>loop.rewards.WasApplied("power-spike"),"Maxed overclock fallback");
            Check(loop.rewards.modules.RegionPowerSpikeResult=="Maxed Overclock Fallback"&&!WeaponModuleSelectionManager.IsChoosingModule,"Native maxed-module fallback resolves once without an empty panel");
            yield return Fresh();
            yield return CapacityAndSearch();
            yield return OptionalAndEnvironment();
            results.Add("LIMITS: accelerated simulation; direct lethal combat input, teleported travel, controlled native legal card rolls. No direct AddExperience or forced evolution used. Public module API/maxed fallback is a separate boundary fixture. Human balance, aiming and audiovisual feel require playtest.");
        }
        static IEnumerator Preview()
        {
            yield return null;loop=Object.FindObjectOfType<RegionLoopController>();accelerate=false;Time.timeScale=1;
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"),"Task preview uses independent profile from initialization");
            yield return Until(()=>loop.Regions[0].initialized,"Preview scene ready");
            Move(loop.Terminal("ColonyEntry").transform.position);
            yield return Clear(loop.Regions[0],RegionEnemyRole.Resident);yield return Collect();
            Move(loop.optionalSites.Single(s=>s.definition.regionId=="ColonyEntry").transform.position);
            yield return Seconds(1);Capture("Optional_Preview");yield return null;yield return null;
            loop.TryActivate("ColonyEntry");yield return Until(()=>!loop.rewards.HasPending,"Preview main reward");
            Move(loop.optionalSites.Single(s=>s.Config.highRisk).transform.position);
            yield return Seconds(1);Capture("HighRisk_Preview");yield return null;yield return null;
            results.Add("RENDER: acceptance timer, labelled hold/limit, risk and reward use original MissionHUD rows; no layout edits.");
        }
        static IEnumerator Presentation()
        {
            yield return null;loop=Object.FindObjectOfType<RegionLoopController>();
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"),"Presentation check uses independent profile from initialization");
            yield return Until(()=>loop.Regions[0].initialized,"Presentation scene ready");
            var entry=loop.Regions[0];yield return Clear(entry,RegionEnemyRole.Resident);yield return Collect();
            Move(loop.Terminal(entry.config.regionId).transform.position+Vector3.right);
            loop.TryActivate(entry.config.regionId);
            yield return Seconds(1);Capture("Presentation_Main");yield return null;yield return null;
            yield return Until(()=>loop.fog.DistanceTravelled>12,"Native front reaches visible entry floor",100);
            Move(loop.world.World(new Vector2(-213.1875f,-29)));
            yield return Seconds(1);Capture("Fog_Front");yield return null;yield return null;
            yield return Until(()=>entry.retired,"Native fog completes entry coverage",60);
            Move(loop.Terminal(entry.config.regionId).transform.position+Vector3.right);
            yield return Seconds(2);
            var ground=Object.FindObjectOfType<ToxicFogGroundVisual>();
            var mesh=ground.GetComponent<MeshFilter>().sharedMesh;
            var renderer=ground.GetComponent<MeshRenderer>();
            var front=Object.FindObjectOfType<ToxicFogFrontVisual>();
            var mist=Object.FindObjectOfType<ToxicFogMistEmitter>().GetComponent<ParticleSystemRenderer>();
            Check(mesh!=null&&mesh.vertexCount>0&&renderer.enabled&&Read<bool>(ground,"geometryReady"),"Native fog mesh generated from reclaimed geometry");
            Check(front.VisualReady&&front.FrontVisible,"Native fog boundary renderer active");
            Check(mist.sharedMaterial.GetTexture("_MainTex")!=null,"Native soft-mist texture connected");
            results.Add("RENDER: ground="+renderer.sharedMaterial.shader.name+" layer="+renderer.sortingLayerName+" order="+renderer.sortingOrder+" vertices="+mesh.vertexCount+" bounds="+renderer.bounds);
            results.Add("RENDER: mist="+mist.sharedMaterial.shader.name+" texture="+mist.sharedMaterial.GetTexture("_MainTex").name);
            Capture("Fog_Inside");yield return null;yield return null;
            pair=new[]{UpgradeType.Piercing,UpgradeType.ChainLightning};
            foreach(var state in loop.Regions.Skip(1))
            {
                Move(loop.Terminal(state.config.regionId).transform.position);
                yield return Until(()=>state.firstEntered>=0,"Enter downstream fog stage");
                yield return Clear(state,RegionEnemyRole.Resident);yield return Collect();
                Check(loop.TryActivate(state.config.regionId),"Presentation route opens "+state.config.exitGateId+" through normal main reward");
                yield return Until(()=>!loop.rewards.HasPending,"Presentation mechanism selection");
            }
            var power=loop.State("PowerMaintenance");
            yield return Until(()=>power.retired,"Power room and final approach reclaimed",130);
            VerifyRoomFog(loop.definitions);
            foreach(var point in new[]{new Vector2(-103.7f,0),new Vector2(-97.8f,0)})
                Check(loop.fogArea.IsPositionInsideFog(loop.world.World(point)),"Final G3 approach edge reclaimed: "+point);
            Move(loop.Terminal("PowerMaintenance").transform.position);yield return Seconds(1);
            Capture("Fog_Power");yield return null;yield return null;
            results.Add("LIMITS: focused native renderer assertions and saved screenshots; manual image review recorded in Delivery.md.");
        }
        static void VerifyRoomFog(IEnumerable<RegionDefinition> definitions)
        {
            foreach(var d in definitions)
            {
                var region=loop.world.Region(d.regionId);int samples=0;
                for(float x=region.center.x-region.width/2+3;x<region.center.x+region.width/2;x+=6)
                    for(float y=region.center.y-region.height/2+3;y<region.center.y+region.height/2;y+=6)
                    {
                        var point=loop.world.World(new Vector2(x,y));
                        if(!RegionSpawnSpace.Valid(loop.world,d.regionId,point,point,.35f,false,out _))continue;
                        Check(loop.fogArea.IsPositionInsideFog(point),"Reclaimed room has uncovered walkable corner "+point,false);samples++;
                    }
                Check(samples>8,"Native fog covers representative walkable room area: "+d.regionId+" samples="+samples);
            }
        }
        static IEnumerator CapacityAndSearch()
        {
            var entry=loop.Regions[0];var ordinary=loop.optionalSites.Single(s=>s.definition.regionId=="ColonyEntry");
            Move(ordinary.transform.position);var foreign=new List<PooledEnemy>();
            while(loop.pool.ActiveCount<loop.globalAliveLimit-entry.config.v02.missionCapacityReserve)
            {
                var enemy=loop.pool.GetEnemy(EnemyType.Normal,loop.anchors.First(a=>a.regionId=="ColonyEntry").transform.position,Quaternion.identity);
                enemy.GetComponent<EnemyMovement>().SetMovementLocked(true);foreign.Add(enemy);
            }
            Check(loop.pool.ActiveCount==loop.globalAliveLimit-entry.config.v02.missionCapacityReserve,"Ordinary population fills all non-reserved global slots");
            ordinary.beacon.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            Check(ordinary.Active,"Optional task can start with all ordinary capacity occupied");
            bool sawSpawn=false;double end=EditorApplication.timeSinceStartup+50;
            while(ordinary.Active)
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("Reserved task capacity");
                foreach(var t in entry.enemies.ToArray())if(t.role==RegionEnemyRole.Mission&&t.lease!=null&&!t.dead){sawSpawn=true;t.lease.Health.TakeDamage(int.MaxValue);}
                Move(ordinary.transform.position);yield return null;
            }
            Check(sawSpawn&&ordinary.Succeeded&&!loop.HasMilestone("Arrival"),"Necessary mission guards use reserved capacity, optional success leaves main gate closed");
            int kills=GameManager.Instance.KillCount;foreach(var enemy in foreign)enemy.ReturnToPool();
            Check(GameManager.Instance.KillCount==kills,"Capacity-fixture release creates no fake kills");
            yield return Clear(entry,RegionEnemyRole.Resident);yield return Collect();
            var container=Object.FindObjectsOfType<LootContainer>().First(c=>loop.world.ResolveRegion(loop.world.Local(c.transform.position))=="ColonyEntry");
            Move(container.transform.position);int wave=entry.environmentWave;float started=Time.time;
            container.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            yield return Until(()=>container.State==LootContainerState.SearchComplete,"Ordinary container search window",30);
            Check(entry.environmentWave==wave&&Time.time-started<=entry.config.v02.searchSeconds,"A real ordinary container is fully searched inside the configured low-pressure window");
            Check(container.TryTakeEntry(container.SearchEntries.First(r=>r.State==LootSearchState.Identified)),"Ordinary search window yields usable backpack loot");
            Capture("Ordinary_Search_Window");yield return null;yield return null;LootSearchPanelController.Instance.CloseActiveSearch();
        }
        static IEnumerator OptionalAndEnvironment()
        {
            var entry=loop.Regions[0];yield return Clear(entry,RegionEnemyRole.Resident);
            Move(loop.Terminal("ColonyEntry").transform.position);loop.TryActivate("ColonyEntry");yield return Until(()=>!loop.rewards.HasPending,"Entry queue");
            Move(loop.Terminal("CargoYard").transform.position);var cargo=loop.Regions[1];yield return Until(()=>cargo.firstEntered>=0,"Cargo entry");
            yield return Clear(cargo,RegionEnemyRole.Resident);Move(loop.Terminal("CargoYard").transform.position);
            yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Calm,"Previous-region storm finishes before accepting task");
            var ordinary=loop.optionalSites.Single(s=>s.definition.regionId=="CargoYard"&&!s.Config.highRisk);
            Move(ordinary.transform.position);ordinary.beacon.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            Check(ordinary.Active&&!loop.HasMilestone("Beacon"),"Native optional Beacon occupies its own slot and does not open G2");
            yield return Seconds(2);float defended=ordinary.beacon.RegionDefendedSeconds,remaining=loop.missionView.RegionTaskRemaining;
            Move(loop.Terminal("CargoYard").transform.position);yield return Seconds(3);
            Check(ordinary.beacon.RegionDefendedSeconds<defended+1&&loop.missionView.RegionTaskRemaining<remaining-2,"Leaving radius pauses defense while deadline keeps advancing");
            var pause=Object.FindObjectOfType<PauseMenuController>();pause.PauseGame();accelerate=false;
            float paused=loop.missionView.RegionTaskRemaining,fog=loop.fog.DistanceTravelled;yield return null;yield return null;yield return null;
            Check(Mathf.Approximately(paused,loop.missionView.RegionTaskRemaining)&&Mathf.Approximately(fog,loop.fog.DistanceTravelled),"Pause freezes task and accumulated fog consistently");
            pause.ResumeGame();accelerate=true;
            loop.missionView.CancelRegionTask("Regression abandon");
            Check(!ordinary.Succeeded&&!loop.HasMilestone("Beacon")&&!loop.CanAcceptOptional(ordinary),"Failed optional has no reward/gate effect and respects attempt limit");
            var high=loop.optionalSites.Single(s=>s.Config.highRisk);Move(high.transform.position);high.beacon.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            Check(high.Active&&!high.rewardContainer.gameObject.activeSelf,"High-risk cache locked until task success");
            yield return Seconds(.3f);Capture("HighRisk_Defense");
            var fixedPosition=high.transform.position;loop.world.MoveMissionPoints("PowerMaintenance");
            Check(high.transform.position==fixedPosition,"Accepted optional target does not move with region mission-point updates");
            int startedWave=cargo.environmentWave;bool sawRequired=false;
            double end=EditorApplication.timeSinceStartup+60;
            while(high.Active)
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("High risk completion");
                sawRequired|=cargo.enemies.Any(t=>t.role==RegionEnemyRole.Mission&&t.keyGuard&&t.lease!=null);
                foreach(var t in cargo.enemies.ToArray())if(t.lease!=null&&!t.dead)t.lease.Health.TakeDamage(int.MaxValue);
                Move(high.transform.position);yield return null;
            }
            Check(high.Succeeded&&sawRequired&&cargo.environmentWave==startedWave,"Finite mixed task guards spawn and complete while ambient waves are held");
            Check(!loop.HasMilestone("Beacon")&&loop.extraction.HasHighRiskCacheSpawned,"High-risk success unlocks native cache without Cargo main completion");
            yield return Collect();Move(high.rewardContainer.transform.position);
            high.rewardContainer.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            var records=high.rewardContainer.SearchEntries.ToArray();
            yield return Until(()=>high.rewardContainer.State==LootContainerState.SearchComplete,"Native cache search completes",30);
            Capture("HighRisk_Search");
            yield return null;yield return null;
            Check(records.Any(r=>r.Stack.Item.Rarity>=ItemRarity.Rare),"High-risk generated contents include minimum rare guarantee");
            LootSearchPanelController.Instance.CloseActiveSearch();high.rewardContainer.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            Check(records.SequenceEqual(high.rewardContainer.SearchEntries),"Closing/reopening never rerolls generated cache entries");
            Check(high.rewardContainer.TryTakeEntry(records.First(r=>r.State==LootSearchState.Identified)),"Identified high-risk item enters native backpack using normal grid rules");
            LootSearchPanelController.Instance.CloseActiveSearch();
            Check(!loop.CanAcceptOptional(high),"Successful high-risk instance cannot reward twice");
            Move(loop.Terminal("CargoYard").transform.position);Check(loop.TryActivate("CargoYard"),"Cargo main remains completable after optional failure/success");yield return Until(()=>!loop.rewards.HasPending,"Cargo reward queue");
            yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Warning,"Actual regional storm warning",60);
            Capture("Storm_Warning");
            var center=loop.safeZone.CurrentCenter;
            Check(loop.ValidateStormCandidate(cargo,center,out float travel,out string why),"Full storm disk and current-player A* route validate: "+why+" travel="+travel);
            Move(loop.Terminal("PowerMaintenance").transform.position);yield return Seconds(1);
            Check(loop.safeZone.CurrentCenter==center,"Storm safety zone stays fixed across a region change");
            Move(center);yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Active,"Storm Active");
            Capture("Storm_Active");yield return null;yield return null;
            invulnerable=false;var hp=loop.world.player.GetComponent<PlayerHealth>();hp.CancelTemporaryInvulnerability();int before=hp.CurrentHealth;
            yield return Seconds(2.5f);Check(hp.CurrentHealth==before,"Native safe-zone protection prevents storm damage");
            Move(center+Vector2.right*(cargo.config.v02.safeRadius+2));yield return Seconds(2.5f);
            Check(hp.CurrentHealth<before,"Native active storm damages an exposed player");
            invulnerable=true;Move(center);yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Recovery,"Storm Recovery");yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Calm,"Storm Calm");
            Check(!loop.safeZone.HasActiveZone,"Recovery clears the completed native safety zone");
            float first=cargo.firstEntered;Move(loop.Terminal("ColonyEntry").transform.position);yield return Seconds(1);Move(loop.Terminal("CargoYard").transform.position);yield return Seconds(1);
            Check(cargo.firstEntered==first,"Returning never resets first-entry fog clock");
            Move(loop.Terminal("PowerMaintenance").transform.position);
            yield return Until(()=>loop.fog.HasStarted&&loop.fog.DistanceTravelled>1,"Actual cumulative toxic fog advance",100);
            float prior=loop.fog.DistanceTravelled;yield return Seconds(1);
            Check(loop.fog.DistanceTravelled>prior&&loop.fog.DistanceTravelled-prior<8,"Fog advances at controlled speed without a survival-time catch-up jump");
            yield return Until(()=>GameManager.Instance.SurvivalTime>245,"Legacy 240 second boundary",100);
            Check(loop.rewards.modules.UnlockedModuleCount==1&&!loop.rewards.modules.RegionPowerSpikeTriggered,"Passing 240 seconds with one module does not force evolution/overclock");
            yield return Until(()=>entry.retired,"Entry fog retirement",100);
            Check(loop.fogArea.IsPositionInsideFog(loop.Terminal("ColonyEntry").transform.position)&&entry.enemies.All(t=>t.lease==null),"Native fog reclaims entry room and retires its population");
            Capture("Fog_Reclaim");
            yield return Until(()=>cargo.retired,"Cargo room and bent-road retirement",130);
            VerifyRoomFog(loop.definitions.Take(2));
            foreach(var point in new[]{new Vector2(-159.6f,20),new Vector2(-154.4f,20),new Vector2(-151,-4.6f),new Vector2(-151,.6f)})
                Check(loop.fogArea.IsPositionInsideFog(loop.world.World(point)),"Bent-road edge reclaimed: "+point);
            yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Calm,"Fog damage isolated from native storm priority");
            Move(loop.Terminal("ColonyEntry").transform.position);invulnerable=false;hp.CancelTemporaryInvulnerability();
            int beforeFogDamage=hp.CurrentHealth;yield return Seconds(2.5f);
            Check(hp.CurrentHealth<beforeFogDamage&&!hp.IsDead,"Native toxic-fog damage applies inside reclaimed space");
            Capture("Fog_Inside");yield return null;yield return null;invulnerable=true;
            hp.CancelTemporaryInvulnerability();invulnerable=false;hp.TakeDamage(int.MaxValue);yield return null;yield return null;
            Check(loop.ShuttingDown&&loop.rewards.PendingCount==0&&!loop.missionView.RegionTaskActive,"Death clears regional enemies, task and pending choices");
            invulnerable=true;yield return Fresh();
            var timeout=loop.optionalSites.Single(s=>s.definition.regionId=="ColonyEntry");Move(timeout.transform.position);timeout.beacon.Interact(loop.world.player.GetComponent<PlayerInteractor>());
            Check(timeout.Active,"Fresh optional task starts for deadline test");Move(loop.Terminal("ColonyEntry").transform.position);
            yield return Until(()=>!timeout.Active,"Native optional deadline expires",90);
            Check(!timeout.Succeeded&&timeout.LastResult=="Deadline expired"&&!loop.rewards.WasApplied("optional/"+timeout.TaskId),"Deadline failure pays no success reward and clears its task instance");
            yield return Clear(loop.Regions[0],RegionEnemyRole.Resident);Move(loop.Terminal("ColonyEntry").transform.position);
            Check(loop.TryActivate("ColonyEntry"),"Main gate remains usable after an actual optional timeout");
            yield return Until(()=>!loop.rewards.HasPending,"Final reward queue");
        }
    }
}
