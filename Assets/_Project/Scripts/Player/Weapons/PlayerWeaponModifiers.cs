using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeaponModifiers : MonoBehaviour
{
    [Header("Mechanic Upgrade Levels")]
    [Tooltip("穿透升级当前等级，0 表示尚未获得")]
    [SerializeField] private int piercingLevel = 0;

    [Tooltip("爆裂升级当前等级，0 表示尚未获得")]
    [SerializeField] private int explosiveLevel = 0;

    [Tooltip("连锁电弧升级当前等级，0 表示尚未获得")]
    [SerializeField] private int chainLightningLevel = 0;

    [Tooltip("分裂弹升级当前等级，0 表示尚未获得")]
    [SerializeField] private int splitShotLevel = 0;


    // =========================================================
    // Maximum Levels
    // =========================================================

    private const int MaxPiercingLevel = 3;
    private const int MaxExplosiveLevel = 3;
    private const int MaxChainLightningLevel = 3;
    private const int MaxSplitShotLevel = 3;


    // =========================================================
    // Explosion Tuning
    // =========================================================

    [Header("Explosion Tuning")]
    [Tooltip("爆裂 Lv1 的爆炸半径")]
    [SerializeField] private float explosionRadiusLevel1 = 1.4f;

    [Tooltip("爆裂 Lv2 的爆炸半径")]
    [SerializeField] private float explosionRadiusLevel2 = 1.7f;

    [Tooltip("爆裂 Lv3 的爆炸半径")]
    [SerializeField] private float explosionRadiusLevel3 = 2.0f;

    [Tooltip("爆裂 Lv1 的伤害倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float explosionDamageMultiplierLevel1 = 0.40f;

    [Tooltip("爆裂 Lv2 的伤害倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float explosionDamageMultiplierLevel2 = 0.55f;

    [Tooltip("爆裂 Lv3 的伤害倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float explosionDamageMultiplierLevel3 = 0.70f;


    // =========================================================
    // Chain Lightning Tuning
    // =========================================================

    [Header("Chain Lightning Tuning")]
    [Tooltip("连锁电弧寻找下一个目标的最大距离")]
    [SerializeField] private float chainRange = 3.5f;

    [Tooltip("连锁电弧相对于当前 Bullet Damage 的伤害倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float chainDamageMultiplier = 0.60f;


    // =========================================================
    // Split Shot Tuning
    // =========================================================

    [Header("Split Shot Tuning")]
    [Tooltip("分裂子弹相对于母弹的伤害倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float childDamageMultiplier = 0.50f;

    [Tooltip("分裂子弹相对于母弹的速度倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float childSpeedMultiplier = 0.85f;

    [Tooltip("分裂子弹相对于母弹的尺寸倍率")]
    [Range(0f, 2f)]
    [SerializeField] private float childScaleMultiplier = 0.70f;

    [Tooltip("分裂子弹相对于母弹的生命周期倍率")]
    [Range(0f, 1f)]
    [SerializeField] private float childLifeTimeMultiplier = 0.65f;


    // =========================================================
    // Current Level Read-Only Properties
    // =========================================================

    public int PiercingLevel => piercingLevel;

    public int ExplosiveLevel => explosiveLevel;

    public int ChainLightningLevel =>
        chainLightningLevel;

    public int SplitShotLevel =>
        splitShotLevel;


    // =========================================================
    // Upgrade Availability
    // =========================================================

    public bool CanUpgradePiercing =>
        piercingLevel < MaxPiercingLevel;

    public bool CanUpgradeExplosive =>
        explosiveLevel < MaxExplosiveLevel;

    public bool CanUpgradeChainLightning =>
        chainLightningLevel < MaxChainLightningLevel;

    public bool CanUpgradeSplitShot =>
        splitShotLevel < MaxSplitShotLevel;


    // =========================================================
    // Mechanic Active State
    // =========================================================

    public bool HasPiercing =>
        piercingLevel > 0;

    public bool HasExplosive =>
        explosiveLevel > 0;

    public bool HasChainLightning =>
        chainLightningLevel > 0;

    public bool HasSplitShot =>
        splitShotLevel > 0;


    // =========================================================
    // Runtime Mechanic Parameters
    // =========================================================

    /// <summary>
    /// 当前 Bullet 可以额外穿透的敌人数量。
    ///
    /// Lv0 = 0
    /// Lv1 = 1
    /// Lv2 = 2
    /// Lv3 = 3
    /// </summary>
    public int PierceCount =>
        piercingLevel;


    /// <summary>
    /// 根据当前爆裂等级返回爆炸半径。
    /// 未获得爆裂时返回 0。
    /// </summary>
    public float ExplosionRadius
    {
        get
        {
            switch (explosiveLevel)
            {
                case 1:
                    return explosionRadiusLevel1;

                case 2:
                    return explosionRadiusLevel2;

                case 3:
                    return explosionRadiusLevel3;

                default:
                    return 0f;
            }
        }
    }


    /// <summary>
    /// 根据当前爆裂等级返回爆炸伤害倍率。
    /// 未获得爆裂时返回 0。
    /// </summary>
    public float ExplosionDamageMultiplier
    {
        get
        {
            switch (explosiveLevel)
            {
                case 1:
                    return explosionDamageMultiplierLevel1;

                case 2:
                    return explosionDamageMultiplierLevel2;

                case 3:
                    return explosionDamageMultiplierLevel3;

                default:
                    return 0f;
            }
        }
    }


    /// <summary>
    /// 当前允许进行的连锁跳跃次数。
    ///
    /// Lv0 = 0
    /// Lv1 = 1
    /// Lv2 = 2
    /// Lv3 = 3
    /// </summary>
    public int ChainCount =>
        chainLightningLevel;


    public float ChainRange =>
        HasChainLightning
            ? chainRange
            : 0f;


    public float ChainDamageMultiplier =>
        HasChainLightning
            ? chainDamageMultiplier
            : 0f;


    /// <summary>
    /// 当前直接命中后生成的分裂子弹数量。
    ///
    /// Lv0 = 0
    /// Lv1 = 2
    /// Lv2 = 3
    /// Lv3 = 4
    /// </summary>
    public int SplitCount
    {
        get
        {
            switch (splitShotLevel)
            {
                case 1:
                    return 2;

                case 2:
                    return 3;

                case 3:
                    return 4;

                default:
                    return 0;
            }
        }
    }


    public float ChildDamageMultiplier =>
        HasSplitShot
            ? childDamageMultiplier
            : 0f;


    public float ChildSpeedMultiplier =>
        HasSplitShot
            ? childSpeedMultiplier
            : 0f;


    public float ChildScaleMultiplier =>
        HasSplitShot
            ? childScaleMultiplier
            : 0f;


    public float ChildLifeTimeMultiplier =>
        HasSplitShot
            ? childLifeTimeMultiplier
            : 0f;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        // 每一局游戏开始时，
        // 机制型升级都从 Lv0 开始。
        ResetMechanicUpgradesInternal();
    }


    private void OnValidate()
    {
        ClampUpgradeLevels();
        ValidateTuningValues();
    }


    // =========================================================
    // Apply Upgrades
    // =========================================================

    /// <summary>
    /// 穿透升级提高一级。
    /// 最大 Lv3。
    /// </summary>
    public void ApplyPiercingUpgrade()
    {
        if (!CanUpgradePiercing)
        {
            Debug.Log(
                "PlayerWeaponModifiers: "
                + "Piercing has already reached max level.",
                this
            );

            return;
        }

        piercingLevel = Mathf.Min(
            MaxPiercingLevel,
            piercingLevel + 1
        );

        Debug.Log(
            "Piercing upgraded to Lv"
            + piercingLevel
            + ". Pierce Count: "
            + PierceCount,
            this
        );
    }


    /// <summary>
    /// 爆裂升级提高一级。
    /// 最大 Lv3。
    /// </summary>
    public void ApplyExplosiveUpgrade()
    {
        if (!CanUpgradeExplosive)
        {
            Debug.Log(
                "PlayerWeaponModifiers: "
                + "Explosive has already reached max level.",
                this
            );

            return;
        }

        explosiveLevel = Mathf.Min(
            MaxExplosiveLevel,
            explosiveLevel + 1
        );

        Debug.Log(
            "Explosive upgraded to Lv"
            + explosiveLevel
            + ". Radius: "
            + ExplosionRadius
            + ", Damage Multiplier: "
            + ExplosionDamageMultiplier,
            this
        );
    }


    /// <summary>
    /// 连锁电弧升级提高一级。
    /// 最大 Lv3。
    /// </summary>
    public void ApplyChainLightningUpgrade()
    {
        if (!CanUpgradeChainLightning)
        {
            Debug.Log(
                "PlayerWeaponModifiers: "
                + "Chain Lightning has already reached max level.",
                this
            );

            return;
        }

        chainLightningLevel = Mathf.Min(
            MaxChainLightningLevel,
            chainLightningLevel + 1
        );

        Debug.Log(
            "Chain Lightning upgraded to Lv"
            + chainLightningLevel
            + ". Chain Count: "
            + ChainCount,
            this
        );
    }


    /// <summary>
    /// 分裂弹升级提高一级。
    /// 最大 Lv3。
    /// </summary>
    public void ApplySplitShotUpgrade()
    {
        if (!CanUpgradeSplitShot)
        {
            Debug.Log(
                "PlayerWeaponModifiers: "
                + "Split Shot has already reached max level.",
                this
            );

            return;
        }

        splitShotLevel = Mathf.Min(
            MaxSplitShotLevel,
            splitShotLevel + 1
        );

        Debug.Log(
            "Split Shot upgraded to Lv"
            + splitShotLevel
            + ". Split Count: "
            + SplitCount,
            this
        );
    }


    // =========================================================
    // Reset
    // =========================================================

    /// <summary>
    /// 将所有机制型升级恢复到 Lv0。
    /// </summary>
    public void ResetMechanicUpgrades()
    {
        ResetMechanicUpgradesInternal();

        Debug.Log(
            "PlayerWeaponModifiers: "
            + "All mechanic upgrades have been reset.",
            this
        );
    }


    private void ResetMechanicUpgradesInternal()
    {
        piercingLevel = 0;
        explosiveLevel = 0;
        chainLightningLevel = 0;
        splitShotLevel = 0;
    }


    // =========================================================
    // Validation
    // =========================================================

    private void ClampUpgradeLevels()
    {
        piercingLevel = Mathf.Clamp(
            piercingLevel,
            0,
            MaxPiercingLevel
        );

        explosiveLevel = Mathf.Clamp(
            explosiveLevel,
            0,
            MaxExplosiveLevel
        );

        chainLightningLevel = Mathf.Clamp(
            chainLightningLevel,
            0,
            MaxChainLightningLevel
        );

        splitShotLevel = Mathf.Clamp(
            splitShotLevel,
            0,
            MaxSplitShotLevel
        );
    }


    private void ValidateTuningValues()
    {
        explosionRadiusLevel1 =
            Mathf.Max(0f, explosionRadiusLevel1);

        explosionRadiusLevel2 =
            Mathf.Max(0f, explosionRadiusLevel2);

        explosionRadiusLevel3 =
            Mathf.Max(0f, explosionRadiusLevel3);

        explosionDamageMultiplierLevel1 =
            Mathf.Max(
                0f,
                explosionDamageMultiplierLevel1
            );

        explosionDamageMultiplierLevel2 =
            Mathf.Max(
                0f,
                explosionDamageMultiplierLevel2
            );

        explosionDamageMultiplierLevel3 =
            Mathf.Max(
                0f,
                explosionDamageMultiplierLevel3
            );

        chainRange =
            Mathf.Max(0f, chainRange);

        chainDamageMultiplier =
            Mathf.Max(
                0f,
                chainDamageMultiplier
            );

        childDamageMultiplier =
            Mathf.Max(
                0f,
                childDamageMultiplier
            );

        childSpeedMultiplier =
            Mathf.Max(
                0f,
                childSpeedMultiplier
            );

        childScaleMultiplier =
            Mathf.Max(
                0f,
                childScaleMultiplier
            );

        childLifeTimeMultiplier =
            Mathf.Clamp01(
                childLifeTimeMultiplier
            );
    }


    // =========================================================
    // Debug Context Menu
    // =========================================================

    [ContextMenu("Debug/Add Piercing Level")]
    private void DebugAddPiercingLevel()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ApplyPiercingUpgrade();
    }


    [ContextMenu("Debug/Add Explosive Level")]
    private void DebugAddExplosiveLevel()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ApplyExplosiveUpgrade();
    }


    [ContextMenu("Debug/Add Chain Level")]
    private void DebugAddChainLevel()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ApplyChainLightningUpgrade();
    }


    [ContextMenu("Debug/Add Split Level")]
    private void DebugAddSplitLevel()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ApplySplitShotUpgrade();
    }


    [ContextMenu("Debug/Reset Mechanic Upgrades")]
    private void DebugResetMechanicUpgrades()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ResetMechanicUpgrades();
        PrintWeaponModifierStatus();
    }


    [ContextMenu("Debug/Print Weapon Modifier Status")]
    private void DebugPrintWeaponModifierStatus()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        PrintWeaponModifierStatus();
    }


    private bool CanUseRuntimeDebug()
    {
        if (Application.isPlaying)
        {
            return true;
        }

        Debug.LogWarning(
            "PlayerWeaponModifiers: "
            + "Please enter Play Mode before "
            + "using runtime debug commands.",
            this
        );

        return false;
    }


    private void PrintWeaponModifierStatus()
    {
        Debug.Log(
            "===== Weapon Modifier Status =====\n"
            + "\n"
            + "Piercing\n"
            + "Level: "
            + piercingLevel
            + " / "
            + MaxPiercingLevel
            + "\nCan Upgrade: "
            + CanUpgradePiercing
            + "\nPierce Count: "
            + PierceCount
            + "\n"
            + "\n"
            + "Explosive\n"
            + "Level: "
            + explosiveLevel
            + " / "
            + MaxExplosiveLevel
            + "\nCan Upgrade: "
            + CanUpgradeExplosive
            + "\nExplosion Radius: "
            + ExplosionRadius
            + "\nExplosion Damage Multiplier: "
            + ExplosionDamageMultiplier
            + "\n"
            + "\n"
            + "Chain Lightning\n"
            + "Level: "
            + chainLightningLevel
            + " / "
            + MaxChainLightningLevel
            + "\nCan Upgrade: "
            + CanUpgradeChainLightning
            + "\nChain Count: "
            + ChainCount
            + "\nChain Range: "
            + ChainRange
            + "\nChain Damage Multiplier: "
            + ChainDamageMultiplier
            + "\n"
            + "\n"
            + "Split Shot\n"
            + "Level: "
            + splitShotLevel
            + " / "
            + MaxSplitShotLevel
            + "\nCan Upgrade: "
            + CanUpgradeSplitShot
            + "\nSplit Count: "
            + SplitCount
            + "\nChild Damage Multiplier: "
            + ChildDamageMultiplier
            + "\nChild Speed Multiplier: "
            + ChildSpeedMultiplier
            + "\nChild Scale Multiplier: "
            + ChildScaleMultiplier
            + "\nChild Life Time Multiplier: "
            + ChildLifeTimeMultiplier,
            this
        );
    }

    [ContextMenu("Debug/Set All Modules To Lv3")]
    private void DebugSetAllModulesToLevel3()
    {
        while (CanUpgradePiercing)
        {
            ApplyPiercingUpgrade();
        }

        while (CanUpgradeExplosive)
        {
            ApplyExplosiveUpgrade();
        }

        while (CanUpgradeChainLightning)
        {
            ApplyChainLightningUpgrade();
        }

        while (CanUpgradeSplitShot)
        {
            ApplySplitShotUpgrade();
        }

        Debug.Log(
            "===== DEBUG: All Weapon Modules Set To Lv3 =====\n" +
            "Piercing Lv" + PiercingLevel + "\n" +
            "Explosion Lv" + ExplosiveLevel + "\n" +
            "Chain Lv" + ChainLightningLevel + "\n" +
            "Split Lv" + SplitShotLevel,
            this
        );
    }
}