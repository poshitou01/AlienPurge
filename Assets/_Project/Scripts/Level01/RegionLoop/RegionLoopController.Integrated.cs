using System.Linq;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    public sealed partial class RegionLoopController
    {
        [Header("Integrated world: explicit opt-in; original scenes keep their existing flow")]
        public bool integratedWorld;
        bool bossHandoffActive;

        bool IntegratedNeedsBoss(RegionRunState state) => integratedWorld && state.config.missionId == "Research" && !HasMilestone("BossDefeated");

        bool HasTypeCapacity(RegionRunState state, RegionEnemyProfile profile)
        {
            // Pool counts include death presentation. A dying specialist still occupies its global slot.
            if (profile.globalSimultaneousLimit > 0 && pool.GetActiveCount(profile.type) >= profile.globalSimultaneousLimit) return false;
            return profile.simultaneousLimit <= 0 || state.enemies.Count(t => t.type == profile.type && t.lease != null && !t.dead) < profile.simultaneousLimit;
        }

        RegionWave SelectEnvironmentWave(RegionRunState state)
        {
            var settings = state.config.v02;
            if (!settings.weightedEnvironmentSquads) return settings.environmentSquads[state.environmentWave % settings.environmentSquads.Length];
            float total = settings.environmentSquads.Sum(w => Mathf.Max(0, w.weight));
            float pick = Random.value * total;
            foreach (var wave in settings.environmentSquads)
            {
                pick -= Mathf.Max(0, wave.weight);
                if (pick < 0) return wave;
            }
            return settings.environmentSquads[0];
        }

        bool TickIntegratedBoss()
        {
            if (!integratedWorld || world.arenaBridge == null) return false;
            var bridge = world.arenaBridge;
            if (!bridge.encounterStarted) return false;
            if (!bossHandoffActive && !bridge.encounterFinished)
            {
                bossHandoffActive = true;
                missionView.CancelRegionTask("Entering Hive Tyrant encounter");
                storm.EndRegionStorm();
                CancelWarning(); runningRegions.Clear();
                foreach (var state in states)
                    foreach (var ticket in state.enemies.ToArray())
                        if (ticket.lease != null) ticket.lease.Release();
                fog.SetRegionNotice(null);
            }
            if (bridge.encounterFinished)
            {
                var health = world.boss != null ? world.boss.GetComponent<EnemyHealth>() : null;
                if (health != null && health.IsDead && milestones.Add("BossDefeated"))
                {
                    // Native Boss death already paid its XP/kill reward. Only route progress belongs here.
                    world.progression.onBossDefeated.Invoke();
                    RefreshDoors();
                }
                bossHandoffActive = false;
                return false;
            }
            CurrentRegion = "HiveTyrant";
            // Keep core managers alive; yield only the region scheduler while the accepted Boss owns combat.
            PresentV02();
            return true;
        }
    }
}
