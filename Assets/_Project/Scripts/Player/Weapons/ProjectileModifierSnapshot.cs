using System;
using UnityEngine;

[Serializable]
public struct ProjectileModifierSnapshot
{
    // =========================================================
    // Piercing
    // =========================================================

    [Header("Piercing")]
    [SerializeField] private int pierceCount;


    // =========================================================
    // Explosion
    // =========================================================

    [Header("Explosion")]
    [SerializeField] private bool explosive;

    [SerializeField] private float explosionRadius;

    [SerializeField] private float explosionDamageMultiplier;


    // =========================================================
    // Chain Lightning
    // =========================================================

    [Header("Chain Lightning")]
    [SerializeField] private bool chainLightning;

    [SerializeField] private int chainCount;

    [SerializeField] private float chainRange;

    [SerializeField] private float chainDamageMultiplier;


    // =========================================================
    // Split Shot
    // =========================================================

    [Header("Split Shot")]
    [SerializeField] private bool splitShot;

    [SerializeField] private int splitCount;

    [SerializeField] private float childDamageMultiplier;

    [SerializeField] private float childSpeedMultiplier;

    [SerializeField] private float childScaleMultiplier;

    [SerializeField] private float childLifeTimeMultiplier;


    // =========================================================
    // Recursion Control
    // =========================================================

    [Header("Recursion Control")]
    [SerializeField] private int generation;


    // =========================================================
    // Read-Only Properties
    // =========================================================

    public int PierceCount => pierceCount;

    public bool Explosive => explosive;

    public float ExplosionRadius =>
        explosionRadius;

    public float ExplosionDamageMultiplier =>
        explosionDamageMultiplier;

    public bool ChainLightning =>
        chainLightning;

    public int ChainCount =>
        chainCount;

    public float ChainRange =>
        chainRange;

    public float ChainDamageMultiplier =>
        chainDamageMultiplier;

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
        bool splitShot,
        int splitCount,
        float childDamageMultiplier,
        float childSpeedMultiplier,
        float childScaleMultiplier,
        float childLifeTimeMultiplier,
        int generation)
    {
        this.pierceCount =
            Mathf.Max(0, pierceCount);

        this.explosive =
            explosive;

        this.explosionRadius =
            Mathf.Max(0f, explosionRadius);

        this.explosionDamageMultiplier =
            Mathf.Max(
                0f,
                explosionDamageMultiplier
            );

        this.chainLightning =
            chainLightning;

        this.chainCount =
            Mathf.Max(0, chainCount);

        this.chainRange =
            Mathf.Max(0f, chainRange);

        this.chainDamageMultiplier =
            Mathf.Max(
                0f,
                chainDamageMultiplier
            );

        this.splitShot =
            splitShot;

        this.splitCount =
            Mathf.Max(0, splitCount);

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

        this.generation =
            Mathf.Max(0, generation);
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
            0,
            false,
            0f,
            0f,
            false,
            0,
            0f,
            0f,
            false,
            0,
            0f,
            0f,
            0f,
            0f,
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
            + "\n"
            + "\nPiercing"
            + "\nPierce Count: "
            + pierceCount
            + "\n"
            + "\nExplosion"
            + "\nEnabled: "
            + explosive
            + "\nRadius: "
            + explosionRadius
            + "\nDamage Multiplier: "
            + explosionDamageMultiplier
            + "\n"
            + "\nChain Lightning"
            + "\nEnabled: "
            + chainLightning
            + "\nChain Count: "
            + chainCount
            + "\nRange: "
            + chainRange
            + "\nDamage Multiplier: "
            + chainDamageMultiplier
            + "\n"
            + "\nSplit Shot"
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
            + childLifeTimeMultiplier;
    }
}