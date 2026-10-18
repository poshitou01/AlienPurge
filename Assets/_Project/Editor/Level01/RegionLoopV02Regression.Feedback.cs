using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run V02 Playtest Feedback Checks")]
        public static void RunFeedback()=>Start(false,false,true);
        static void KillLive(RegionRunState state, Func<RegionEnemyTicket,bool> predicate)
        {
            foreach(var t in state.enemies.ToArray())
                if(t.lease!=null&&!t.dead&&predicate(t))t.lease.Health.TakeDamage(int.MaxValue);
        }
        static IEnumerator CompleteMain(int index)
        {
            var state=loop.Regions[index];Move(loop.Terminal(state.config.regionId).transform.position);
            yield return Until(()=>loop.CurrentRegion==state.config.regionId&&state.initialized,"Enter "+state.config.regionId);
            yield return Clear(state,RegionEnemyRole.Resident);
            Move(loop.Terminal(state.config.regionId).transform.position);
            loop.Terminal(state.config.regionId).Interact(loop.world.player.GetComponent<PlayerInteractor>());
            yield return Until(()=>loop.HasMilestone(state.config.missionId),"Main "+state.config.missionId);
            yield return Until(()=>!loop.rewards.HasPending,"Main reward queue");
            if(!state.config.v02.extractionRegion)Check(!loop.world.progression.gates.First(g=>g.gateId==state.config.exitGateId).locked,"Main opens "+state.config.exitGateId+" exactly once");
            Check(!loop.TryActivate(state.config.regionId),"Repeated main activation cannot pay again: "+state.config.regionId);
        }
        static IEnumerator Feedback()
        {
            yield return null;loop=Object.FindObjectOfType<RegionLoopController>();
            pair=new[]{UpgradeType.Piercing,UpgradeType.ChainLightning};
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"),"Isolated profile selected before initialization");
            Check(loop.Regions.Count==6,"Five exploration regions and a playable extraction phase installed");
            yield return Until(()=>loop.Regions[0].initialized&&loop.State("CargoYard").enemies.Any(t=>t.lease!=null),"Adjacent garrison prepared before entry");
            var entry=loop.Regions[0];var cargo=loop.State("CargoYard");
            Check(cargo.firstEntered<0&&cargo.enemies.Count(t=>t.lease!=null)>=12,"Cargo already populated before player entry; no premature fog clock");
            Check(loop.terminals.All(t=>t.deviceRange==null||!t.deviceRange.enabled)&&loop.optionalSites.All(s=>s.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled)),"Blue pads, floating markers and permanent terminal circles removed");
            Move(loop.Terminal("ColonyEntry").transform.position);
            yield return Seconds(.5f);
            var sentinel=entry.enemies.First(t=>t.lease!=null);Vector3 oldPosition=sentinel.lease.transform.position;
            accelerate=false;Time.timeScale=1;yield return Seconds(2);
            Check(Vector2.Distance(oldPosition,sentinel.lease.transform.position)>.25f,"Entering region actually activates native enemy movement");
            accelerate=true;
            int xp=TotalXP(),kills=GameManager.Instance.KillCount,amount=sentinel.lease.Health.ExperienceAmount;
            sentinel.lease.Health.TakeDamage(int.MaxValue);yield return Seconds(.3f);yield return Collect();
            Check(TotalXP()==xp+amount&&GameManager.Instance.KillCount==kills+1,"Real pooled death awards one physical XP pickup and one kill");
            yield return Clear(entry,RegionEnemyRole.Resident);Move(loop.Terminal("ColonyEntry").transform.position);
            yield return Until(()=>entry.enemies.Any(t=>t.role==RegionEnemyRole.Patrol&&t.lease!=null),"First ambient spawn");
            var survivor=entry.enemies.First(t=>t.role==RegionEnemyRole.Patrol&&t.lease!=null);
            int wave=entry.environmentWave;float until=Time.time+19;int freshSpawns=0;var seen=new HashSet<string>(entry.enemies.Where(t=>t.lease!=null).Select(t=>t.id));
            while(Time.time<until)
            {
                foreach(var t in entry.enemies.Where(t=>t.role==RegionEnemyRole.Patrol&&t.lease!=null).ToArray())
                {
                    if(seen.Add(t.id)) { freshSpawns++;var view=Camera.main.WorldToViewportPoint(t.lease.transform.position);Check(view.x<0||view.x>1||view.y<0||view.y>1,"Reinforcement enters outside view",false); }
                    if(t!=survivor)t.lease.Health.TakeDamage(int.MaxValue);
                }
                Check(loop.anchors.All(a=>a.warning==null||!a.warning.enabled),"No yellow spawn circles",false);
                yield return null;
            }
            Check(!survivor.dead&&entry.environmentWave>wave&&freshSpawns>=4,"A surviving patrol no longer blocks later reinforcements; new arrivals="+freshSpawns);
            Check(entry.enemies.Count<entry.config.residents.Sum(g=>g.count)+20,"Ambient ticket queue remains bounded");
            yield return CompleteMain(0);yield return CompleteMain(1);
            loop.storm.EndRegionStorm();
            var high=loop.optionalSites.Single(s=>s.Config.highRisk);
            Move(high.transform.position);yield return Seconds(.4f);
            Check(loop.TerminalTask("CargoYard")==high,"High-risk challenge offered through the existing cargo terminal");
            loop.Terminal("CargoYard").Interact(loop.world.player.GetComponent<PlayerInteractor>());
            Check(high.Active&&loop.storm.CurrentPhase==StormPhase.Warning,"Accepting high-risk defense triggers a real storm warning");
            Check(Vector2.Distance(loop.safeZone.CurrentCenter,high.transform.position)<high.Config.radius+loop.safeZone.Radius-2,"Storm safety has a usable overlap with the chosen defense terminal");
            yield return Until(()=>cargo.enemies.Any(t=>t.role==RegionEnemyRole.Mission&&t.lease!=null),"Task guards enter");
            var contest=cargo.enemies.First(t=>t.role==RegionEnemyRole.Mission&&t.lease!=null);
            contest.lease.transform.position=high.transform.position+Vector3.right;Physics2D.SyncTransforms();
            yield return Seconds(.3f);float beforeProgress=high.beacon.RegionDefendedSeconds;yield return Seconds(1.2f);
            Check(high.Contested&&high.beacon.RegionDefendedSeconds<=beforeProgress+.05f,"Enemy occupation interrupts defense progress");
            contest.lease.Health.TakeDamage(int.MaxValue);
            Move(high.transform.position+Vector3.right*8);yield return Seconds(.4f);beforeProgress=high.beacon.RegionDefendedSeconds;float deadlineBefore=loop.missionView.RegionTaskRemaining;
            yield return Seconds(1.2f);Check(high.beacon.RegionDefendedSeconds<=beforeProgress+.05f&&loop.missionView.RegionTaskRemaining<deadlineBefore,"Leaving pauses hold progress but not the deadline");
            Move(high.transform.position);
            int stormArrivals=0;seen=new HashSet<string>(cargo.enemies.Where(t=>t.lease!=null).Select(t=>t.id));bool captured=false;
            double end=EditorApplication.timeSinceStartup+100;
            while(high.Active)
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("High-risk combined defense");
                foreach(var t in cargo.enemies.Where(t=>t.lease!=null&&!t.dead).ToArray())
                {
                    bool newArrival=seen.Add(t.id);
                    if(t.role==RegionEnemyRole.Patrol&&newArrival&&loop.storm.CurrentPhase==StormPhase.Active)stormArrivals++;
                    if(t.role==RegionEnemyRole.Patrol||Vector2.Distance(t.lease.transform.position,high.transform.position)<5)t.lease.Health.TakeDamage(int.MaxValue);
                }
                if(!captured&&loop.storm.CurrentPhase==StormPhase.Active)
                {
                    yield return Seconds(.2f);Capture("Feedback_StormDefense");yield return null;yield return null;captured=true;
                    Check(loop.GetComponentInChildren<RegionStormMask>().CurrentAlpha>.15f,"Storm spatial overlay active with native safety hole");
                }
                yield return null;
            }
            Check(high.Succeeded&&high.rewardContainer.gameObject.activeSelf,"Combined storm defense completes and unlocks rare cache");
            Check(stormArrivals>0,"Ambient enemies keep spawning DURING active storm and optional defense; arrivals="+stormArrivals);
            Check(!loop.CanAcceptOptional(high)&&loop.rewards.WasApplied("optional/"+high.TaskId),"Task reward applied once");
            yield return Collect();
            yield return CompleteMain(2);yield return VerifyShot(WeaponEvolutionType.ThunderPiercer);
            Move(loop.Terminal("QuarantineCommons").transform.position);
            yield return Until(()=>loop.CurrentRegion=="QuarantineCommons"&&loop.State("QuarantineCommons").enemies.Any(t=>t.lease!=null),"G3 leads into a populated follow-up region");
            accelerate=false;Time.timeScale=1;yield return Seconds(5);Capture("Feedback_Quarantine");yield return null;yield return null;accelerate=true;
            Check(loop.missionView.RegionTitle.Contains("隔离生活区")&&loop.missionView.RegionObjective.Contains("守卫")&&loop.missionView.CurrentMissionState==MissionState.Active,"After G3, Chinese HUD gives an active isolation objective");
            var hud=Object.FindObjectOfType<MissionHUD>();
            foreach(var text in hud.GetComponentsInChildren<TMP_Text>(true))
                if(text.gameObject.activeInHierarchy)Check(!text.text.Contains("DISTANCE")&&!text.text.Contains("REGION ROUTE COMPLETE"),"Mission panel localization",false);
            yield return CompleteMain(3);yield return CompleteMain(4);
            Check(loop.missionView.RegionObjective.Contains("撤离区"),"Research completion points forward to extraction");
            yield return CompleteMain(5);
            Check(loop.extraction.IsDefending&&loop.extraction.CurrentZone!=null&&loop.extraction.HasActiveExtractionTarget,"Existing native extraction defense starts from terminal interaction");
            Move(loop.extraction.CurrentExtractionPosition+Vector2.right*8);yield return Seconds(.5f);float held=loop.extraction.DefenseProgress;
            yield return Seconds(2);Check(loop.extraction.DefenseProgress<=held+.05f,"Leaving extraction pauses native progress");
            Move(loop.extraction.CurrentExtractionPosition);
            float spawnDeadline=Time.time+8;
            while(!loop.State("Extraction").enemies.Any(t=>t.lease!=null)&&Time.time<spawnDeadline)yield return null;
            yield return Seconds(2);Capture("Feedback_Extraction");yield return null;yield return null;
            var ev=loop.State("Extraction");
            results.Add("MEASURE extraction t="+Time.time+" current="+loop.CurrentRegion+" wave="+ev.environmentWave+" due="+ev.environmentDue+" quiet="+ev.quietUntil+" retired="+ev.retired+" global="+loop.pool.ActiveCount+" camera="+Camera.main.transform.position+" size="+Camera.main.orthographicSize+" player="+loop.world.PlayerFeet+" tickets="+string.Join(",",ev.enemies.Where(t=>t.role==RegionEnemyRole.Patrol).Select(t=>t.type+":"+t.failedSpawns+":"+(t.lease!=null))));
            foreach(var point in new[]{new Vector2(27,-56.48438f),new Vector2(23,-56.48438f),new Vector2(16,-50),new Vector2(16,-36)})
            {
                var position=loop.world.World(point);var args=new object[]{ev,ev.config.Profile(EnemyType.Normal),position,null};
                var valid=loop.GetType().GetMethod("ValidV02Ingress",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(loop,args);
                var clear=loop.GetType().GetMethod("IsIngressClearV02",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(loop,new object[]{position});
                results.Add("MEASURE ingress "+point+" region="+loop.world.ResolveRegion(point)+" valid="+valid+" clear="+clear+" reason="+args[3]);
            }
            Check(loop.State("Extraction").enemies.Any(t=>t.lease!=null),"Extraction remains defended by active enemies");
            yield return Until(()=>loop.extraction.CurrentState==ExtractionState.Succeeded,"Successful native extraction",70);
            Check(!GameManager.Instance.IsPlaying,"Extraction reaches actual run result, not a dead-end label");
            yield return Fresh();
            yield return FeedbackHazardsAndFailure();
            results.Add("LIMITS: teleported travel, accelerated simulation and deterministic damage inputs; real native movement, XP pickups, reward buttons, projectiles, storm/fog damage and extraction run. This verifies behavior, not human difficulty balance.");
        }
        static IEnumerator FeedbackHazardsAndFailure()
        {
            yield return CompleteMain(0);var entry=loop.Regions[0];
            loop.storm.EndRegionStorm();var ordinary=loop.optionalSites.Single(s=>s.definition.regionId=="ColonyEntry");
            Move(ordinary.transform.position);ordinary.Interact();Check(ordinary.Active,"Normal terminal defense starts");
            Move(ordinary.transform.position+Vector3.right*9);
            yield return Until(()=>!ordinary.Active,"Optional deadline failure",100);
            Check(!ordinary.Succeeded&&ordinary.LastResult=="Deadline expired"&&!loop.rewards.WasApplied("optional/"+ordinary.TaskId),"Ignoring optional combat fails without rewards; main remains open");
            loop.storm.EndRegionStorm();Move(loop.Terminal("CargoYard").transform.position);
            yield return Until(()=>loop.State("CargoYard").firstEntered>=0,"Cargo entered for hazard checks");
            var cargo=loop.State("CargoYard");var center=loop.stormCandidates.First(p=>loop.world.ResolveRegion(loop.world.Local(p.position))=="CargoYard").position;
            KillLive(cargo,t=>true);Move(center);loop.storm.EndRegionStorm();
            Check(loop.storm.TryBeginRegionStorm(center,cargo.config.v02.safeRadius,2,12,3),"Native hazard fixture publishes warning");
            yield return Until(()=>loop.storm.CurrentPhase==StormPhase.Active,"Active hazard");
            invulnerable=false;var hp=loop.world.player.GetComponent<PlayerHealth>();hp.CancelTemporaryInvulnerability();int health=hp.CurrentHealth;
            var damage=Object.FindObjectOfType<StormDamageController>();int damageTicks=Read<int>(damage,"attemptedDamageTickCount");
            yield return Seconds(2.5f);Check(hp.CurrentHealth==health,"Safety hole corresponds to native storm protection");
            Move(center+Vector3.right*(cargo.config.v02.safeRadius+2));yield return Seconds(2.5f);
            Check(hp.CurrentHealth<health&&Read<int>(damage,"attemptedDamageTickCount")>damageTicks&&Read<bool>(damage,"lastDamageTickApplied"),"Exposed player takes native storm damage");invulnerable=true;
            loop.storm.EndRegionStorm();yield return Seconds(.3f);Check(loop.GetComponentInChildren<RegionStormMask>().CurrentAlpha==0,"Storm mask clears after event");
            float first=cargo.firstEntered;Move(loop.Terminal("ColonyEntry").transform.position);yield return Seconds(.5f);Move(center);yield return Seconds(.5f);
            Check(cargo.firstEntered==first,"Re-entering cannot reset fog deadline");
            foreach(var state in loop.Regions.Take(5))state.firstEntered=Time.time-state.config.v02.explorationSeconds-state.config.v02.fogWarningSeconds-state.fogBuffer-1;
            float previous=loop.fog.DistanceTravelled;yield return Seconds(1);
            Check(loop.fog.DistanceTravelled>previous&&loop.fog.DistanceTravelled-previous<5,"Fog advances continuously rather than jumping on elapsed time");
            loop.fog.AdvanceRegionFront(loop.definitions[4].v02.fogEndDistance,10000,1);yield return Seconds(.5f);
            VerifyRoomFog(loop.definitions.Take(5));
            foreach(var point in new[]{new Vector2(16.53906f,36),new Vector2(32,33),new Vector2(48,29)})
                Check(loop.fogArea.IsPositionInsideFog(loop.world.World(point)),"Research exit corridor reclaimed: "+point);
            Check(loop.Regions.Take(5).All(s=>s.retired)&&loop.State("Extraction").retired==false,"Reclaimed combat regions retire; forward extraction stays usable");
            Move(loop.Terminal("ColonyEntry").transform.position);invulnerable=false;hp.CancelTemporaryInvulnerability();health=hp.CurrentHealth;
            yield return Seconds(2.5f);Check(hp.CurrentHealth<health,"Native fog damage remains active");
            hp.CancelTemporaryInvulnerability();hp.TakeDamage(int.MaxValue);yield return null;yield return null;
            Check(loop.ShuttingDown&&loop.rewards.PendingCount==0&&!loop.missionView.RegionTaskActive,"Death releases enemies and task/reward ownership");
            invulnerable=true;yield return Fresh();
            Check(!loop.extraction.RegionExtractionStarted&&loop.Regions.All(s=>!s.retired),"Restart resets extraction and reclaimed-region state");
            RegionLoopV02Installer.ValidateProtected();Check(true,"Protected original assets remain byte-identical");
        }
    }
}
