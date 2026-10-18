using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        static bool IntegratedTest=>SessionState.GetBool(Key+".integrated",false);
        static bool specialistChecks=true;
        static int integratedPeak, supportPeak, exploderPeak;
        static void ValidateTestProtection()
        { if(IntegratedTest)IntegratedWorldInstaller.CheckProtectedScenes();else RegionLoopV02Installer.ValidateProtected(); }

        [MenuItem("AlienPurge/Gameplay/Level01/Run Integrated World V01 Regression")]
        public static void RunIntegrated()=>StartIntegrated(false);
        public static void RunIntegratedLifecycle()=>StartIntegrated(true);
        static void StartIntegrated(bool lifecycle,bool locomotion=false)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            IntegratedWorldInstaller.Install();
            string guid=AssetDatabase.AssetPathToGUID(IntegratedWorldInstaller.Target);
            int count=HiveTyrantCombatSetup.SceneComponents<Transform>(SceneManager.GetActiveScene()).Length;
            IntegratedWorldInstaller.Install();
            if(guid!=AssetDatabase.AssetPathToGUID(IntegratedWorldInstaller.Target)||count!=HiveTyrantCombatSetup.SceneComponents<Transform>(SceneManager.GetActiveScene()).Length)
                throw new InvalidOperationException("Integrated installer is not idempotent.");
            foreach(var flag in new[]{"presentation","preview","polish","abilities","player","impact"})SessionState.SetBool(Key+"."+flag,false);
            SessionState.SetBool(Key+".feedback",true);SessionState.SetBool(Key+".integrated",true);
            SessionState.SetBool(Key+".lifecycle",lifecycle);
            SessionState.SetBool(Key+".locomotion",locomotion);
            SessionState.SetString(Key+".product",PlayerSettings.productName);
            PlayerSettings.productName="AlienPurge_IntegratedV01_Regression";
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+".batch",Application.isBatchMode);SessionState.SetBool(Key+".failed",false);
            File.WriteAllText(Report,"RUNNING: actual integrated scene / isolated save profile\n");
            EditorApplication.isPlaying=true;
        }
        static void ObserveIntegrated()
        {
            integratedPeak=Math.Max(integratedPeak,loop.pool.ActiveCount);
            supportPeak=Math.Max(supportPeak,loop.pool.GetActiveCount(EnemyType.Support));
            exploderPeak=Math.Max(exploderPeak,loop.pool.GetActiveCount(EnemyType.Exploder));
            foreach(var type in new[]{EnemyType.Support,EnemyType.Exploder})
            {
                int limit=loop.definitions.SelectMany(d=>d.enemies).Where(p=>p.type==type).Min(p=>p.globalSimultaneousLimit);
                Check(loop.pool.GetActiveCount(type)<=limit,"Global specialists stay within their configured hard limits",false);
            }
        }
        static IEnumerator IntegratedSuite()
        {
            specialistChecks=true;integratedPeak=supportPeak=exploderPeak=0;
            yield return Feedback();
            specialistChecks=false;
            pair=new[]{UpgradeType.Explosive,UpgradeType.SplitShot};
            yield return CompleteMain(0);yield return CompleteMain(1);yield return CompleteMain(2);
            yield return VerifyShot(WeaponEvolutionType.ClusterBurst);
            Check(moduleClicks>=4,"Both mechanism combinations applied through native module buttons across fresh runs");
            yield return Fresh();
            var pause=Object.FindObjectOfType<PauseMenuController>();
            pause.PauseGame();yield return null;
            Check(PauseMenuController.IsPaused&&Time.timeScale==0,"Native Pause pauses integrated run");
            pause.ResumeGame();yield return null;
            Check(!PauseMenuController.IsPaused,"Native Resume restores integrated run");
            string oldId=loop.RunId;pause.RestartGame();loop=null;yield return null;yield return null;
            yield return Until(()=>Object.FindObjectOfType<RegionLoopController>()!=null,"Pause restart integrated scene");
            loop=Object.FindObjectOfType<RegionLoopController>();
            Check(loop.RunId!=oldId&&SceneManager.GetActiveScene().path==IntegratedWorldInstaller.Target,"Restart stays in the fourth checkout's integrated scene");
            pause=Object.FindObjectOfType<PauseMenuController>();pause.LoadMainMenu();loop=null;yield return null;yield return null;
            yield return Until(()=>SceneManager.GetActiveScene().name=="MainMenu_IntegratedV01","Isolated menu return");
            var menu=Object.FindObjectOfType<MainMenuManager>();
            menu.GetType().GetMethod("StartGame").Invoke(menu,null);yield return null;yield return null;
            yield return Until(()=>Object.FindObjectOfType<RegionLoopController>()!=null,"Isolated menu re-enters integrated world");
            loop=Object.FindObjectOfType<RegionLoopController>();
            Check(SceneManager.GetActiveScene().path==IntegratedWorldInstaller.Target,"Menu Start does not return to the old V02 scene");
            ValidateTestProtection();
            results.Add("MEASURE: overall peak active="+integratedPeak+" Support="+supportPeak+" Exploder="+exploderPeak);
            results.Add("LIMITS: accelerated routes, teleported traversal and deterministic kill fixtures; Boss killed through real native weapon fire with invulnerability. Not human balance or a full hardware benchmark.");
        }
        static void PlaceEnemy(PooledEnemy enemy,Vector2 position)
        {
            enemy.transform.position=position;
            var body=enemy.GetComponent<Rigidbody2D>();body.position=position;body.velocity=Vector2.zero;
            enemy.GetComponent<EnemyMovement>().SetMovementLocked(true);
            var nav=enemy.GetComponent<EnemyPathNavigator>();if(nav!=null)nav.enabled=false;
            Physics2D.SyncTransforms();
        }
        static PooledEnemy RentSame(PooledEnemy previous,RegionEnemyProfile profile,Vector2 position)
        {
            var held=new List<PooledEnemy>();PooledEnemy same=null;
            for(int i=0;i<2;i++)
            {
                var actor=loop.pool.GetEnemy(previous.Type,position,Quaternion.identity);if(actor==null)break;
                if(actor==previous){same=actor;break;}held.Add(actor);
            }
            foreach(var actor in held)actor.ReturnToPool();
            Check(same!=null,"Same pooled specialist is available for lifecycle reuse");
            same.GetComponent<EnemyDefinition>().InitializeInstanceAttributes(profile.health,profile.speed,profile.damage,profile.experience);
            var lease=same.GetComponent<RegionEnemyLease>();
            Check(lease==null||lease.Ticket==null&&lease.Region==null&&lease.RunId==null,"Region ownership detached before non-region pool reuse");
            return same;
        }
        static IEnumerator IntegratedSpecialist(RegionRunState state)
        {
            if(!specialistChecks)yield break;
            EnemyType type=state.config.regionId=="CargoYard"?EnemyType.Exploder:EnemyType.Support;
            yield return Until(()=>state.enemies.Any(t=>t.type==type&&t.lease!=null&&!t.dead),"Authored "+type+" actually appears in "+state.config.regionId,30);
            var ticket=state.enemies.First(t=>t.type==type&&t.lease!=null&&!t.dead);var actor=ticket.lease.PoolMember;
            Check(!ticket.keyGuard&&ticket.role==RegionEnemyRole.Resident,"First "+type+" is ordinary garrison, not an access guard");
            Check(actor.GetComponent<SupportExploderAnimation>()!=null&&actor.GetComponent<Animator>().runtimeAnimatorController.name.Contains(type.ToString()),"Natural "+type+" uses its formal Animator");
            bool oldAcceleration=accelerate;accelerate=false;Time.timeScale=1;
            if(type==EnemyType.Support)
            {
                var ability=actor.GetComponent<SupportEnemyAbility>();
                yield return Until(()=>ability.TargetCount>0,"Support actually buffs a native regional enemy",12);
                var buff=ability.TargetAt(0);var contact=buff.GetComponent<EnemyContactDamage>();
                Check(contact.Damage==contact.BaseDamage+ability.Config.bonusDamage,"Support bonus reaches the shared damage getter");
                Vector2 position=actor.transform.position;int kills=GameManager.Instance.KillCount;
                ticket.lease.Release();
                Check(contact.Damage==contact.BaseDamage&&ability.TargetCount==0,"Region unload revokes the aura without turning it into base damage");
                var reused=RentSame(actor,state.config.Profile(type),position);
                Check(ability.TargetCount==0&&reused.GetComponent<EnemyContactDamage>().BaseDamage==state.config.Profile(type).damage,"Support reuse starts with clean targets and base attributes");
                reused.ReturnToPool();
                Check(GameManager.Instance.KillCount==kills,"Support recycle does not award a false kill");
            }
            else
            {
                var ability=actor.GetComponent<ExploderEnemyAbility>();Vector2 origin=actor.transform.position;
                PlaceEnemy(actor,origin);Move(origin+Vector2.right*7,true);
                yield return Seconds(ability.Config.spawnGraceDuration+.3f);
                Move(origin+Vector2.right*1.6f,true);
                yield return Until(()=>ability.State==ExploderState.Warning,"Exploder enters its real warning state",8);
                yield return Seconds(.2f);float progress=ability.WarningProgress;
                Time.timeScale=0;double wait=EditorApplication.timeSinceStartup+.2;while(EditorApplication.timeSinceStartup<wait)yield return null;
                Check(ability.WarningProgress==progress,"Pause freezes the live regional Exploder warning");Time.timeScale=1;
                int kills=GameManager.Instance.KillCount,guards=loop.RemainingAccessGuards(state);
                actor.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);yield return null;yield return null;
                Check(ability.ExplosionCount==0&&ticket.dead&&GameManager.Instance.KillCount==kills+1,"Killing warning Exploder cancels blast and records one native death");
                Check(loop.RemainingAccessGuards(state)==guards,"Ordinary Exploder kill does not satisfy an access-guard objective");
                yield return Until(()=>actor.IsReturned,"Exploder death presentation returns to pool",5);
                var reused=RentSame(actor,state.config.Profile(type),origin);
                Check(ability.State==ExploderState.Safe&&ability.ExplosionCount==0,"Same Exploder re-arms from Safe, with no old timer or blast");
                PlaceEnemy(reused,origin);Move(origin+Vector2.right*7,true);yield return Seconds(ability.Config.spawnGraceDuration+.2f);
                Move(origin+Vector2.right*1.6f,true);yield return Until(()=>ability.State==ExploderState.Warning,"Reused Exploder warning",8);
                var hp=loop.world.player.GetComponent<PlayerHealth>();hp.IncreaseMaxHealth(10);hp.RestoreHealth(hp.MaxHealth);
                invulnerable=false;hp.CancelTemporaryInvulnerability();int before=hp.CurrentHealth;
                yield return Until(()=>ability.ExplosionCount==1,"Real integrated Exploder detonation",5);
                Check(ability.DamageApplications==1&&hp.CurrentHealth<before,"Exploder deals one real player hit through native health");
                invulnerable=true;yield return Until(()=>actor.IsReturned,"Burst animation returns to pool",5);
                reused=RentSame(actor,state.config.Profile(type),origin);
                reused.ReturnToPool();
                Check(ability.ExplosionCount==0&&ability.State==ExploderState.Cancelled,"Third-life recycle cancels without an extra explosion");
            }
            Capture("Integrated_"+type);yield return null;yield return null;
            accelerate=oldAcceleration;
        }
        static IEnumerator IntegratedStress()
        {
            var state=loop.State("PowerMaintenance");var task=loop.optionalSites.First(s=>s.definition.regionId==state.config.regionId);
            Move(task.transform.position);yield return Seconds(.5f);loop.storm.EndRegionStorm();
            task.Interact();Check(task.Active,"Power optional task starts with integrated enemy configuration");
            var point=loop.stormCandidates.First(p=>loop.world.ResolveRegion(loop.world.Local(p.position))==state.config.regionId&&loop.ValidateStormCandidate(state,p.position,out _,out _)).position;
            Check(loop.storm.TryBeginRegionStorm(point,state.config.v02.safeRadius,2,20,3),"Combined-load fixture starts native reachable Storm");
            state.AddGroups(new[]{new RegionSpawnGroup{type=EnemyType.Support,count=1},new RegionSpawnGroup{type=EnemyType.Exploder,count=1}},RegionEnemyRole.Patrol,"integration-pressure-fixture",Time.time);
            double timeout=EditorApplication.timeSinceStartup+60;
            while(loop.pool.GetActiveCount(EnemyType.Support)==0||loop.pool.GetActiveCount(EnemyType.Exploder)==0)
            {
                if(EditorApplication.timeSinceStartup>timeout)throw new TimeoutException("Specialists share existing regional scheduler");
                KillLive(state,t=>t.type==EnemyType.Fast||t.type==EnemyType.Heavy);yield return null;
            }
            yield return Until(()=>loop.storm.IsStormActive,"Combined real Storm becomes active");
            bool oldAcceleration=accelerate;accelerate=false;Time.timeScale=1;
            var durations=new List<double>();double last=EditorApplication.timeSinceStartup,end=last+8;long memory=Profiler.GetTotalAllocatedMemoryLong();
            while(EditorApplication.timeSinceStartup<end)
            {
                double now=EditorApplication.timeSinceStartup;durations.Add((now-last)*1000);last=now;
                Check(loop.pool.ActiveCount<=loop.globalAliveLimit,"Combined pressure stays bounded",false);
                yield return null;
            }
            durations.Sort();
            results.Add("MEASURE combined Storm/task/specialists: frames="+durations.Count+" P50="+durations[durations.Count/2].ToString("F2")+"ms P95="+durations[(int)(durations.Count*.95)].ToString("F2")+"ms managed+native allocated delta="+(Profiler.GetTotalAllocatedMemoryLong()-memory)+" bytes; Windows Editor batch/D3D11, other source Editors remain open.");
            Check(state.enemies.Count<state.config.residents.Sum(g=>g.count)+40,"Storm/task overlap does not leak an unbounded ticket queue");
            Capture("Integrated_CombinedPressure");yield return null;yield return null;
            loop.missionView.CancelRegionTask("Pressure fixture complete");loop.storm.EndRegionStorm();accelerate=oldAcceleration;
        }
        static IEnumerator IntegratedBoss()
        {
            Check(loop.world.progression.gates.First(g=>g.gateId=="G7").locked&&!loop.CanActivate("Extraction"),"Research completion cannot skip Boss to unlock extraction");
            var boss=loop.world.boss.GetComponent<HiveTyrantCombat>();
            Check(!loop.world.arenaBridge.encounterStarted&&boss.Motor.IsMovementLocked,"Accepted Boss stays dormant before Arena entrance");
            Move(loop.world.World(new Vector2(0,-10)));yield return Until(()=>loop.world.arenaBridge.encounterStarted,"Research-gated actual Arena entrance");
            yield return Seconds(.4f);
            Check(loop.storm.CurrentPhase==StormPhase.Calm&&loop.Regions.All(s=>s.enemies.All(t=>t.lease==null)),"Boss handoff suspends only regional pressure and ends the previous Storm");
            float fog=loop.fog.DistanceTravelled, maximumBossFog=fog;
            bool oldAcceleration=accelerate;accelerate=false;Time.timeScale=1;
            var player=loop.world.player.GetComponent<PlayerHealth>();
            var pilot=player.gameObject.AddComponent<HiveTyrantPlaytestPilot>();pilot.Boss=boss;pilot.Battle=true;pilot.Dodge=true;
            double until=EditorApplication.timeSinceStartup+240;bool p2=false;int maxSummons=0;
            while(!boss.Health.IsDead)
            {
                if(EditorApplication.timeSinceStartup>until)throw new TimeoutException("Integrated native-weapon Boss combat");
                maxSummons=Math.Max(maxSummons,boss.LivingSummons);
                maximumBossFog=Mathf.Max(maximumBossFog,loop.fog.DistanceTravelled);
                foreach(var owned in Object.FindObjectsOfType<HiveTyrantSummonOwner>())
                    if(owned.Owner==boss)Check(owned.GetComponent<PooledEnemy>().Type!=EnemyType.Support&&owned.GetComponent<PooledEnemy>().Type!=EnemyType.Exploder&&owned.GetComponent<RegionEnemyLease>()?.Ticket==null,"Boss summons keep four-type whitelist and no region ownership",false);
                if(boss.Phase2Entered&&!p2){p2=true;Capture("Integrated_BossPhase2");}
                yield return null;
            }
            int shots=pilot.Shots;pilot.Battle=false;Object.Destroy(pilot);yield return Seconds(.4f);
            Check(p2&&boss.PhaseTransitionCount==1&&shots>0&&maxSummons>0,"Real weapon fire defeats both Boss phases with native summons");
            Check(maximumBossFog<=fog+.1f,"Region fog does not advance during the Boss encounter");
            Check(loop.HasMilestone("BossDefeated")&&loop.world.arenaBridge.encounterFinished&&!loop.world.progression.gates.First(g=>g.gateId=="G7").locked,"Boss death records regional milestone and opens onward extraction gate");
            loop.world.arenaBridge.NotifyBossDefeated();Check(loop.HasMilestone("BossDefeated"),"Duplicate victory callback is idempotent");
            results.Add("MEASURE integrated Boss shots="+shots+" summons="+boss.TotalSummoned+" peak="+maxSummons+" phases="+boss.PhaseTransitionCount);
            accelerate=oldAcceleration;
        }
    }
}
