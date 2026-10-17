using UnityEngine;

[CreateAssetMenu(menuName = "AlienPurge/Boss/Hive Tyrant Combat")]
public sealed class HiveTyrantCombatConfig : ScriptableObject
{
    [Header("Test starting values, not final balance")]
    [Min(1)] public int maxHealth = 600;
    [Range(.01f, .99f)] public float phase2HealthRatio = .5f;
    [Min(.1f)] public float phaseTransitionTime = 1.8f;
    [Min(.1f)] public float deathTime = 1.6f;
    [Min(1)] public float engageDistance = 18;
    [Min(.1f)] public float globalRecovery = 1.1f;
    [Header("Sweep: filled sector, direction locked for the entire warning")]
    [Min(.1f)] public float sweepRadius = 6;
    [Range(10, 180)] public float sweepAngle = 110;
    [Min(1)] public int sweepDamage = 12;
    [Min(.2f)] public float sweepWindup = 1;
    [Min(.1f)] public float sweepRecovery = .75f;
    [Min(.2f)] public float sweepCooldown = 4.5f;
    [Min(.3f)] public float phase2SecondSweepWarning = .85f;
    [Header("Slam: filled disk, one instantaneous hit")]
    [Min(.1f)] public float slamRadius = 7;
    [Min(1)] public int slamDamage = 18;
    [Min(.3f)] public float slamWindup = 1.55f;
    [Min(.1f)] public float slamRecovery = 1;
    [Min(.5f)] public float slamCooldown = 8;
    [Header("Summon: existing Normal/Swarmer pool")]
    [Min(0)] public int summonWaveSize = 3;
    [Min(0)] public int summonAliveCap = 5;
    [Min(0)] public int summonBudget = 12;
    [Min(.5f)] public float summonInterval = 14;
    [Min(.5f)] public float summonWarning = 1.4f;
    [Min(.1f)] public float summonRecovery = .8f;
    [Min(1)] public float summonMinRadius = 5;
    [Min(1)] public float summonMaxRadius = 10;
    [Min(.1f)] public float summonClearance = .6f;
    [Min(1)] public float summonPlayerClearance = 3;
    [Min(1)] public int summonAttempts = 32;
    [Min(1)] public int summonHealth = 4;
    [Min(.1f)] public float summonSpeed = 2;
    [Min(1)] public int summonDamage = 5;
    public LayerMask environmentLayers = 1 << 7;
}
