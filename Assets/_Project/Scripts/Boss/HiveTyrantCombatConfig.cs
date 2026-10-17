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
    [Min(.1f)] public float sweepRadius = 7;
    [Range(10, 180)] public float sweepAngle = 120;
    [Min(1)] public int sweepDamage = 1;
    [Min(.2f)] public float sweepWindup = 1;
    [Min(.1f)] public float sweepRecovery = .75f;
    [Min(.2f)] public float sweepCooldown = 4.5f;
    [Min(.3f)] public float phase2SecondSweepWarning = .85f;
    [Header("Ground wave: three advancing segments, two real safe lanes")]
    [HideInInspector] public int contentVersion;
    [Tooltip("Legacy disk radius, retained only for serialized compatibility. Not used for wave damage.")]
    [HideInInspector] public float slamRadius = 7;
    [Min(1)] public int slamDamage = 2;
    [Min(.3f)] public float slamWindup = 1.55f;
    [Min(.1f)] public float slamRecovery = 1;
    [Min(.5f)] public float slamCooldown = 8;
    [Min(.1f)] public float waveSpeed = 8;
    [Min(1)] public float waveDistance = 14;
    [Min(.1f)] public float waveSegmentWidth = 3;
    [Min(.1f)] public float waveThickness = 1;
    [Min(0)] public float waveStartOffset = 3.3f;
    [Min(1)] public float waveGapBodyMultiplier = 1.8f;
    [Min(0)] public float waveGapMargin = .35f;
    [Min(.1f)] public float waveMinimumGap = 1.65f;
    [Min(.1f)] public float waveRouteReactionMargin = .25f;
    [Header("Summon: existing native enemy definitions and attacks")]
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
    [HideInInspector] public int summonHealth = 4;
    [HideInInspector] public float summonSpeed = 2;
    [HideInInspector] public int summonDamage = 5;
    public HiveTyrantSummonChoice[] summonChoices = {
        new HiveTyrantSummonChoice { type = EnemyType.Normal, weight = 45, aliveCap = 5 },
        new HiveTyrantSummonChoice { type = EnemyType.Fast, weight = 25, aliveCap = 3 },
        new HiveTyrantSummonChoice { type = EnemyType.Heavy, weight = 10, aliveCap = 1 },
        new HiveTyrantSummonChoice { type = EnemyType.Ranged, weight = 20, aliveCap = 2 }
    };
    [Header("Presentation")]
    [Range(4, 32)] public int effectFragmentCount = 16;
    [Min(.1f)] public float effectLifetime = .45f;
    public LayerMask environmentLayers = 1 << 7;
}

[System.Serializable]
public sealed class HiveTyrantSummonChoice
{
    public EnemyType type;
    [Min(0)] public float weight = 1;
    [Min(0)] public int aliveCap = 2;
    [Min(.1f)] public float healthMultiplier = 1;
    [Min(.1f)] public float speedMultiplier = 1;
    [Min(.1f)] public float damageMultiplier = 1;
}
