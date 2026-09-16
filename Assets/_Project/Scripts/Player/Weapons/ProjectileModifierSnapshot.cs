using System;
using UnityEngine;


[Serializable]
public struct ProjectileModifierSnapshot
{
    // =========================================================
    // Piercing
    // =========================================================

    [Header("Piercing")]
    [SerializeField]
    private int pierceCount;


    // =========================================================
    // Explosion
    // =========================================================

    [Header("Explosion")]
    [SerializeField]
    private bool explosive;

    [SerializeField]
    private float explosionRadius;

    [SerializeField]
    private float explosionDamageMultiplier;


    // =========================================================
    // Chain Lightning
    // =========================================================

    [Header("Chain Lightning")]
    [SerializeField]
    private bool chainLightning;

    [SerializeField]
    private int chainCount;

    [SerializeField]
    private float chainRange;

    [SerializeField]
    private float chainDamageMultiplier;

    [Tooltip(
        "当前 Projectile 在整个生命周期中，"
        + "最多可以由 Direct Hit 启动多少次完整 Chain Sequence。"
    )]
    [SerializeField]
    private int maxChainTriggerCount;


    // =========================================================
    // Split Shot
    // =========================================================

    [Header("Split Shot")]
    [SerializeField]
    private bool splitShot;

    [SerializeField]
    private int splitCount;

    [SerializeField]
    private float childDamageMultiplier;

    [SerializeField]
    private float childSpeedMultiplier;

    [SerializeField]
    private float childScaleMultiplier;

    [SerializeField]
    private float childLifeTimeMultiplier;


    // =========================================================
    // Child Explosion Runtime Parameters
    // =========================================================

    [Header("Child Explosion")]

    [Tooltip(
        "Split Child继承Parent Explosion时使用的半径倍率。"
        + "普通Explosion + Split为0.75，"
        + "Cluster Burst为0.85。"
    )]
    [SerializeField]
    private float childExplosionRadiusMultiplier;

    [Tooltip(
        "Split Child继承Parent Explosion时使用的伤害倍率。"
        + "普通Explosion + Split为0.75，"
        + "Cluster Burst为0.85。"
    )]
    [SerializeField]
    private float childExplosionDamageMultiplier;


    // =========================================================
    // Recursion Control
    // =========================================================

    [Header("Recursion Control")]
    [SerializeField]
    private int generation;


    // =========================================================
    // Read-Only Properties
    // =========================================================

    public int PierceCount =>
        pierceCount;


    public bool Explosive =>
        explosive;

    public float ExplosionRadius =>
        explosionRadius;

    public float ExplosionDamageMultiplier =>
        explosionDamageMultiplier;


    public bool ChainLightning =>
        chainLightning;

    /// <summary>
    /// 一条完整 Chain Sequence
    /// 最多允许进行多少次跳跃。
    /// </summary>
    public int ChainCount =>
        chainCount;

    public float ChainRange =>
        chainRange;

    public float ChainDamageMultiplier =>
        chainDamageMultiplier;

    /// <summary>
    /// 当前 Bullet 整个生命周期中，
    /// 最多允许启动多少条完整 Chain Sequence。
    ///
    /// 普通 Chain = 1
    /// Thunder Piercer = 2
    /// </summary>
    public int MaxChainTriggerCount =>
        maxChainTriggerCount;


    public bool SplitShot =>
        splitShot;

    public int SplitCount =>
        splitCount;

    public float ChildDamageMultiplier =>
        childDamageMultiplier;

    public float ChildSpeedMultiplier =>
        childSpeedMultiplier;

    public float ChildScaleMultiplier =>
        childScaleMultiplier;

    public float ChildLifeTimeMultiplier =>
        childLifeTimeMultiplier;


    public float ChildExplosionRadiusMultiplier =>
        childExplosionRadiusMultiplier;

    public float ChildExplosionDamageMultiplier =>
        childExplosionDamageMultiplier;


    public int Generation =>
        generation;


    // =========================================================
    // Constructor
    // =========================================================

    public ProjectileModifierSnapshot(
        int pierceCount,
        bool explosive,
        float explosionRadius,
        float explosionDamageMultiplier,
        bool chainLightning,
        int chainCount,
        float chainRange,
        float chainDamageMultiplier,
        int maxChainTriggerCount,
        bool splitShot,
        int splitCount,
        float childDamageMultiplier,
        float childSpeedMultiplier,
        float childScaleMultiplier,
        float childLifeTimeMultiplier,
        float childExplosionRadiusMultiplier,
        float childExplosionDamageMultiplier,
        int generation)
    {
        this.pierceCount =
            Mathf.Max(
                0,
                pierceCount
            );


        this.explosive =
            explosive;

        this.explosionRadius =
            Mathf.Max(
                0f,
                explosionRadius
            );

        this.explosionDamageMultiplier =
            Mathf.Max(
                0f,
                explosionDamageMultiplier
            );


        this.chainLightning =
            chainLightning;

        this.chainCount =
            Mathf.Max(
                0,
                chainCount
            );

        this.chainRange =
            Mathf.Max(
                0f,
                chainRange
            );

        this.chainDamageMultiplier =
            Mathf.Max(
                0f,
                chainDamageMultiplier
            );

        this.maxChainTriggerCount =
            Mathf.Max(
                1,
                maxChainTriggerCount
            );


        this.splitShot =
            splitShot;

        this.splitCount =
            Mathf.Max(
                0,
                splitCount
            );

        this.childDamageMultiplier =
            Mathf.Max(
                0f,
                childDamageMultiplier
            );

        this.childSpeedMultiplier =
            Mathf.Max(
                0f,
                childSpeedMultiplier
            );

        this.childScaleMultiplier =
            Mathf.Max(
                0f,
                childScaleMultiplier
            );

        this.childLifeTimeMultiplier =
            Mathf.Clamp01(
                childLifeTimeMultiplier
            );


        this.childExplosionRadiusMultiplier =
            Mathf.Max(
                0.01f,
                childExplosionRadiusMultiplier
            );

        this.childExplosionDamageMultiplier =
            Mathf.Max(
                0.01f,
                childExplosionDamageMultiplier
            );


        this.generation =
            Mathf.Max(
                0,
                generation
            );
    }


    // =========================================================
    // Default Snapshot
    // =========================================================

    /// <summary>
    /// 完全没有任何机制升级的默认快照。
    /// 用于旧调用兼容和对象池状态重置。
    /// </summary>
    public static ProjectileModifierSnapshot Default =>
        new ProjectileModifierSnapshot(
            // Piercing
            0,

            // Explosion
            false,
            0f,
            0f,

            // Chain Lightning
            false,
            0,
            0f,
            0f,
            1,

            // Split Shot
            false,
            0,
            0f,
            0f,
            0f,
            0f,

            // Normal Child Explosion Synergy
            0.75f,
            0.75f,

            // Generation
            0
        );


    // =========================================================
    // Debug
    // =========================================================

    public string GetDebugText()
    {
        return
            "===== Projectile Modifier Snapshot =====\n"
            + "Generation: "
            + generation

            + "\n\nPiercing"
            + "\nPierce Count: "
            + pierceCount

            + "\n\nExplosion"
            + "\nEnabled: "
            + explosive
            + "\nRadius: "
            + explosionRadius
            + "\nDamage Multiplier: "
            + explosionDamageMultiplier

            + "\n\nChain Lightning"
            + "\nEnabled: "
            + chainLightning
            + "\nChain Count: "
            + chainCount
            + "\nRange: "
            + chainRange
            + "\nDamage Multiplier: "
            + chainDamageMultiplier
            + "\nMax Chain Trigger Count: "
            + maxChainTriggerCount

            + "\n\nSplit Shot"
            + "\nEnabled: "
            + splitShot
            + "\nSplit Count: "
            + splitCount
            + "\nChild Damage Multiplier: "
            + childDamageMultiplier
            + "\nChild Speed Multiplier: "
            + childSpeedMultiplier
            + "\nChild Scale Multiplier: "
            + childScaleMultiplier
            + "\nChild Life Time Multiplier: "
            + childLifeTimeMultiplier

            + "\n\nChild Explosion"
            + "\nRadius Multiplier: "
            + childExplosionRadiusMultiplier
            + "\nDamage Multiplier: "
            + childExplosionDamageMultiplier;
    }
}