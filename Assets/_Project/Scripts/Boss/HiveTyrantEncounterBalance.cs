using UnityEngine;

// Entry-only, sublinear scaling. Never observes damage dealt, health, aim or dodge success.
// Builds remain stronger: doubling weapon power increases endurance by only 2^0.7.
public static class HiveTyrantEncounterBalance
{
    public static float OffensivePower(Transform target)
    {
        if (target == null) return 1;
        var manager = target.GetComponentInParent<WeaponManager>();
        var weapon = manager != null ? manager.CurrentWeapon : null;
        if (weapon == null || weapon.Data == null) return 1;
        const float startingDps = 1f / .15f; // Project's starting plasma rifle.
        float dps = Mathf.Max(1, weapon.CurrentDamage) * Mathf.Max(1, weapon.CurrentProjectileCount)
            / Mathf.Max(.05f, weapon.EffectiveFireCooldown);
        var modules = target.GetComponentInParent<PlayerWeaponModifiers>();
        // A small allowance for secondary damage, not a full reversal of module benefits.
        if (modules != null) dps *= 1 + .15f * modules.ExplosiveLevel + .04f * modules.ChainLightningLevel + .05f * modules.SplitShotLevel;
        return Mathf.Clamp(dps / startingDps, 1, 200);
    }
    public static int Health(HiveTyrantCombatConfig config, float power)
    {
        float scale = config.scaleHealthWithEntryLoadout
            ? Mathf.Clamp(Mathf.Pow(Mathf.Max(1, power), config.entryPowerExponent), 1, config.maximumHealthScale) : 1;
        return Mathf.Max(1, Mathf.RoundToInt(config.maxHealth * scale));
    }
}
