using System;
using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    [Serializable]
    public sealed class RegionCombatStats
    {
        public int health, damage, level;
        public float speed;
    }

    public sealed partial class RegionLoopController
    {
        [Header("V02 combat / interface refinement")]
        public int polishRevision;
        public bool combatGrowth;
        [Min(1)] public int growthStartsAfterLevel = 6, growthLevelCap = 30;
        [Min(0)] public float healthPerRegion = .24f, healthPerLevel = .14f, healthLevelCurve = .0035f;
        [Range(1,10)] public float maxHealthMultiplier = 7;

        public RegionCombatStats PreviewCombatStats(RegionRunState state, RegionEnemyProfile profile, int level, bool guard)
        {
            int stage = Mathf.Max(0, states.IndexOf(state));
            float excess = Mathf.Max(0, Mathf.Min(level, growthLevelCap) - growthStartsAfterLevel);
            bool grow = v02Enabled && combatGrowth;
            float multiplier = grow ? Mathf.Min(maxHealthMultiplier, 1 + stage * healthPerRegion + excess * healthPerLevel + excess * excess * healthLevelCurve) : 1;
            if (grow && guard && stage > 0) multiplier = Mathf.Min(maxHealthMultiplier, multiplier * 1.25f);
            int bonusDamage = grow && stage >= 3 && level >= (profile.special ? 18 : 12) ? 1 : 0;
            return new RegionCombatStats {
                level = level, health = Mathf.Max(1, Mathf.CeilToInt(profile.health * multiplier)),
                damage = profile.damage + bonusDamage,
                speed = profile.speed * (grow ? 1 + Mathf.Min(.22f, stage * .02f + excess * .007f) : 1)
            };
        }
        public RegionCombatStats CombatStats(RegionRunState state, RegionEnemyTicket ticket)
        {
            if (ticket.combatStats == null)
                ticket.combatStats = PreviewCombatStats(state, state.config.Profile(ticket.type), rewards.experience.CurrentLevel, ticket.keyGuard);
            return ticket.combatStats;
        }
        int GrowthPopulationBonus(RegionRunState state)
        {
            if (!combatGrowth) return 0;
            return Mathf.Min(6, Mathf.Max(0, rewards.experience.CurrentLevel - 8) / 3 + Mathf.Max(0, states.IndexOf(state) - 2));
        }
    }
}
