#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Explicit verification fixtures only. Never installed in the player prefab or live level.
public static class HiveTyrantTestLoadouts
{
    public static void Apply(PlayerHealth player, int tier)
    {
        if (tier <= 0) return;
        var weapon = player.GetComponent<WeaponManager>();
        var modules = player.GetComponent<PlayerWeaponModifiers>();
        weapon.AddBulletDamage(tier == 1 ? 2 : 4);
        weapon.AddProjectileCount(tier == 1 ? 2 : 4);
        weapon.ReduceFireCooldown(tier == 1 ? .03f : .06f);
        player.IncreaseMaxHealth(tier == 1 ? 3 : 5);
        // Level01 offers +0.8 movement, +0.03 cooldown, +1 damage/count/health.
        player.GetComponent<PlayerMovement>().AddMoveSpeed(tier == 1 ? .8f : 1.6f);
        if (tier == 1) { modules.ApplyPiercingUpgrade(); modules.ApplyExplosiveUpgrade(); }
        else
        {
            // Exactly two core modules, matching the formal run's slot limit.
            for (int i = 0; i < 3; i++)
                if (tier == 2) { modules.ApplyExplosiveUpgrade(); modules.ApplySplitShotUpgrade(); }
                else { modules.ApplyPiercingUpgrade(); modules.ApplyChainLightningUpgrade(); }
            var evolution = player.GetComponent<WeaponEvolutionController>();
            if (evolution == null || !evolution.TryApplyEligibleEvolution())
                throw new System.InvalidOperationException("The requested legal test evolution is unavailable on this player.");
        }
    }
    public static string Describe(PlayerHealth player)
    {
        var weapon = player.GetComponent<WeaponManager>().CurrentWeapon;
        var modules = player.GetComponent<PlayerWeaponModifiers>(); var evolution = player.GetComponent<WeaponEvolutionController>();
        return "HP=" + player.CurrentHealth + "/" + player.MaxHealth + ", speed=" + player.GetComponent<PlayerMovement>().MoveSpeed +
            ", bulletDamage=" + weapon.CurrentDamage + ", projectiles=" + weapon.CurrentProjectileCount + ", fireCooldown=" + weapon.EffectiveFireCooldown +
            ", modules(P/E/C/S)=" + modules.PiercingLevel + "/" + modules.ExplosiveLevel + "/" + modules.ChainLightningLevel + "/" + modules.SplitShotLevel +
            ", evolution=" + (evolution != null ? evolution.CurrentEvolution.ToString() : "None");
    }
}
#endif
