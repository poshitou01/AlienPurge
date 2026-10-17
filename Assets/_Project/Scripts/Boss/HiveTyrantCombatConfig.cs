using UnityEngine;

[CreateAssetMenu(menuName = "AlienPurge/Boss/Hive Tyrant Combat")]
public sealed class HiveTyrantCombatConfig : ScriptableObject
{
    [Header("Encounter endurance (captured once on entry)")]
    [Min(1)] public int maxHealth = 1200;
    public bool scaleHealthWithEntryLoadout = true;
    [Range(0, 1)] public float entryPowerExponent = .7f;
    [Min(1)] public float maximumHealthScale = 18;
    [Min(1)] public float maximumSummonHealthScale = 3;
    [Range(.01f, .99f)] public float phase2HealthRatio = .5f;
    [Min(.1f)] public float phaseTransitionTime = 3;
    [Min(.1f)] public float deathTime = 1.6f;
    [Min(1)] public float engageDistance = 18;
    [Min(.1f)] public float globalRecovery = .55f;
    [Min(.1f)] public float phase2GlobalRecovery = .35f;
    [Header("Autonomous decisions")]
    [Min(.5f)] public float openingDelay = 2.2f;
    [Min(.1f)] public float decisionInterval = .25f;
    [Min(0)] public float firstSummonDelay = 5;
    [Header("Sweep: original sector followed by a travelling, jumpable crescent")]
    [Min(.1f)] public float sweepRadius = 7;
    [Range(10, 180)] public float sweepAngle = 120;
    [Min(1)] public int sweepDamage = 2;
    [Min(.2f)] public float sweepWindup = 1.15f;
    [Min(.1f)] public float sweepRecovery = .75f;
    [Min(.2f)] public float sweepCooldown = 4.5f;
    [Min(.3f)] public float phase2SecondSweepWarning = 1.1f;
    [Min(1)] public float sweepTravelDistance = 14;
    [Min(1)] public float sweepTravelSpeed = 8.5f;
    [Min(.1f)] public float sweepThickness = .55f;
    [Tooltip("Release-to-release spacing exceeds the native 2.5 second jump cooldown.")]
    [Min(2.6f)] public float phase2SweepSpacing = 2.9f;
    [Header("Spore barrage: each ground mark locks once, then detonates")]
    [Range(1, 6)] public int sporeCount = 3;
    [Range(1, 6)] public int phase2SporeCount = 4;
    [Min(.5f)] public float sporeWarning = 1.3f;
    [Min(.3f)] public float sporeInterval = .75f;
    [Min(.5f)] public float sporeRadius = 1.7f;
    [Min(.1f)] public float sporeResidueTime = 2.2f;
    [Min(1)] public int sporeDamage = 2;
    [Min(1)] public float sporeCooldown = 11;
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
    [Min(0)] public int summonWaveSize = 7;
    [Min(0)] public int summonAliveCap = 12;
    [Min(0)] public int phase2SummonWaveSize = 10;
    [Min(0)] public int phase2SummonAliveCap = 16;
    [Min(0)] public int summonBudget = 64;
    [Min(.5f)] public float summonInterval = 12;
    [Min(.5f)] public float phase2SummonInterval = 10;
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
        new HiveTyrantSummonChoice { type = EnemyType.Normal, weight = 40, aliveCap = 10 },
        new HiveTyrantSummonChoice { type = EnemyType.Fast, weight = 25, aliveCap = 6 },
        new HiveTyrantSummonChoice { type = EnemyType.Heavy, weight = 15, aliveCap = 2 },
        new HiveTyrantSummonChoice { type = EnemyType.Ranged, weight = 20, aliveCap = 4 }
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
