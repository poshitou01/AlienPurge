using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        static void ResearchFixture()
        {
            // Mission completion itself is exercised by the full integrated route. This
            // fixture reaches the lifecycle boundary without repeating the entire route.
            var flags=Read<HashSet<string>>(loop,"milestones");
            foreach(var region in loop.definitions.Where(d=>!d.v02.extractionRegion))flags.Add(region.missionId);
            typeof(RegionLoopController).GetMethod("RefreshDoors",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loop,null);
            results.Add("FIXTURE: prior Research milestones set in memory for focused encounter interruption checks.");
        }
        static IEnumerator IntegratedLifecycle()
        {
            yield return null;loop=Object.FindObjectOfType<RegionLoopController>();
            yield return Until(()=>loop.Regions.Count==6&&loop.Regions[0].initialized,"Initial region");
            var boss=loop.world.boss.GetComponent<HiveTyrantCombat>();boss.AutomaticSkills=false;
            Move(loop.world.World(new Vector2(0,-10)));yield return Seconds(.3f);
            Check(!loop.world.arenaBridge.encounterStarted&&boss.Motor.IsMovementLocked,"Arena entry cannot start Boss without Research progression");
            ResearchFixture();
            var hp=loop.world.player.GetComponent<PlayerHealth>();invulnerable=false;hp.CancelTemporaryInvulnerability();hp.TakeDamage(int.MaxValue);
            yield return null;yield return null;yield return null;
            Check(hp.IsDead&&!loop.world.arenaBridge.encounterStarted&&boss.PendingSpawnCount==0,"Player death at unlocked Arena entry cannot start a late encounter");
            invulnerable=true;yield return Fresh();
            ResearchFixture();boss=loop.world.boss.GetComponent<HiveTyrantCombat>();boss.AutomaticSkills=false;
            Move(loop.world.World(new Vector2(0,-10)));yield return Until(()=>loop.world.arenaBridge.encounterStarted,"Arena startup");
            yield return Seconds(.3f);
            Check(boss.RequestSkill(HiveTyrantSkill.Summon,true),"Accepted Boss starts native summon windup for interruption test");
            yield return Until(()=>boss.PendingSpawnCount>0,"Pending native summon");
            var oldBoss=boss;string oldRun=loop.RunId;
            yield return Fresh();
            boss=loop.world.boss.GetComponent<HiveTyrantCombat>();
            Check(oldBoss==null&&loop.RunId!=oldRun&&!loop.world.arenaBridge.encounterStarted&&!loop.world.arenaBridge.encounterFinished,
                "Restart during Boss windup destroys the old encounter and resets bridge state");
            Check(boss.PendingSpawnCount==0&&boss.LivingSummons==0&&boss.PhaseTransitionCount==0&&!loop.HasMilestone("BossDefeated")&&loop.world.progression.gates.Single(g=>g.gateId=="G7").locked,
                "Restart clears pending summons, phase, victory milestone and extraction access");
            Check(Object.FindObjectsOfType<RegionEnemyLease>().Where(l=>l.Ticket!=null).All(l=>l.RunId==loop.RunId),
                "Restarted region enemies have only the new run ownership");
            ResearchFixture();boss.AutomaticSkills=false;Move(loop.world.World(new Vector2(0,-10)));
            yield return Until(()=>loop.world.arenaBridge.encounterStarted,"Second actual Arena entry");yield return Seconds(.3f);
            Check(boss.RequestSkill(HiveTyrantSkill.Summon,true),"Fresh encounter can start native summon once");
            yield return Until(()=>boss.PendingSpawnCount>0,"Pending before player death");
            hp=loop.world.player.GetComponent<PlayerHealth>();invulnerable=false;hp.CancelTemporaryInvulnerability();hp.TakeDamage(int.MaxValue);
            yield return Until(()=>boss.PendingSpawnCount==0&&loop.ShuttingDown,"Death cancels native windup",15);
            Check(!loop.HasMilestone("BossDefeated")&&!loop.world.arenaBridge.encounterFinished&&loop.Regions.All(s=>s.enemies.All(t=>t.lease==null)),
                "Death during Boss windup clears regional ownership without awarding victory");
            invulnerable=true;yield return Fresh();
            Check(!loop.world.arenaBridge.encounterStarted&&loop.pool.GetActiveCount(EnemyType.Support)==0&&loop.pool.GetActiveCount(EnemyType.Exploder)<=1,
                "Death Restart returns to the introductory population without specialist or encounter leaks");
            ValidateTestProtection();
            results.Add("LIMITS: native death/restart and actual Boss summon coroutine; focused progression fixtures, not a human playthrough.");
        }
    }
}
