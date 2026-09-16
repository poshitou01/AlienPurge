using UnityEngine;


[CreateAssetMenu(
    fileName = "WeaponEvolutionData",
    menuName = "AlienPurge/Weapon Evolution Data"
)]
public class WeaponEvolutionData : ScriptableObject
{
    // =========================================================
    // Identity
    // =========================================================

    [Header("Identity")]

    [SerializeField]
    private string evolutionId =
        "weapon_evolution";

    [SerializeField]
    private WeaponEvolutionType evolutionType =
        WeaponEvolutionType.None;

    [SerializeField]
    private string displayName =
        "Weapon Evolution";

    [TextArea(2, 5)]
    [SerializeField]
    private string description =
        "Signature weapon evolution.";


    // =========================================================
    // Requirements
    // =========================================================

    [Header("Required Core Modules")]

    [SerializeField]
    private UpgradeType requiredModuleA =
        UpgradeType.Piercing;

    [SerializeField]
    private UpgradeType requiredModuleB =
        UpgradeType.ChainLightning;


    // =========================================================
    // Weapon Visual Definition
    // =========================================================

    [Header("Weapon Visual Definition")]

    [SerializeField]
    private Sprite weaponSprite;

    [Tooltip(
        "相对于Starter WeaponVisual位置的额外偏移。"
    )]
    [SerializeField]
    private Vector2 weaponVisualLocalOffset =
        Vector2.zero;

    [Min(0.01f)]
    [Tooltip(
        "相对于Starter WeaponVisual尺寸的倍率。"
    )]
    [SerializeField]
    private float weaponVisualScaleMultiplier =
        1f;


    // =========================================================
    // Muzzle Definition
    // =========================================================

    [Header("Muzzle Definition")]

    [SerializeField]
    private Sprite muzzleFlashSprite;

    [Tooltip(
        "相对于Starter MuzzlePoint位置的额外偏移。"
    )]
    [SerializeField]
    private Vector2 muzzlePointLocalOffset =
        Vector2.zero;

    [Tooltip(
        "相对于Starter MuzzleFlash自身位置的额外偏移。"
    )]
    [SerializeField]
    private Vector2 muzzleFlashLocalOffset =
        Vector2.zero;

    [Min(0.01f)]
    [SerializeField]
    private float muzzleFlashScaleMultiplier =
        1f;


    // =========================================================
    // Projectile Visual Definition
    // =========================================================

    [Header("Projectile Visual Definition")]

    [SerializeField]
    private Sprite projectileSprite;


    // =========================================================
    // Projectile Trail Definition
    // =========================================================

    [Header("Projectile Trail Definition")]

    [SerializeField]
    private bool useProjectileTrail = false;

    [Min(0.01f)]
    [SerializeField]
    private float projectileTrailTime =
        0.10f;

    [Min(0f)]
    [SerializeField]
    private float projectileTrailStartWidth =
        0.08f;

    [Min(0f)]
    [SerializeField]
    private float projectileTrailEndWidth =
        0f;

    [SerializeField]
    private Color projectileTrailStartColor =
        Color.white;

    [SerializeField]
    private Color projectileTrailEndColor =
        new Color(
            1f,
            1f,
            1f,
            0f
        );


    // =========================================================
    // Audio Definition
    // =========================================================

    [Header("Future Audio Definition")]

    [SerializeField]
    private AudioClip evolutionSfx;

    [SerializeField]
    private AudioClip shootSfx;


    // =========================================================
    // Thunder Piercer
    // =========================================================

    [Header("Thunder Piercer Signature")]

    [Min(0)]
    [SerializeField]
    private int extraPierceCount = 0;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeedMultiplier = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float chainRangeMultiplier = 1f;

    [Min(1)]
    [SerializeField]
    private int maxChainTriggersPerBullet = 1;


    // =========================================================
    // Cluster Burst
    // =========================================================

    [Header("Cluster Burst Signature")]

    [Min(0.01f)]
    [SerializeField]
    private float explosionRadiusMultiplier = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float explosionDamageMultiplier = 1f;

    [Min(0)]
    [SerializeField]
    private int splitCountBonus = 0;

    [Min(0.01f)]
    [SerializeField]
    private float childExplosionRadiusMultiplier =
        0.75f;

    [Min(0.01f)]
    [SerializeField]
    private float childExplosionDamageMultiplier =
        0.75f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileScaleMultiplier =
        1f;

    [Min(0.01f)]
    [SerializeField]
    private float fireCooldownMultiplier =
        1f;


    // =========================================================
    // Read Only Access
    // =========================================================

    public string EvolutionId =>
        evolutionId;

    public WeaponEvolutionType EvolutionType =>
        evolutionType;

    public string DisplayName =>
        displayName;

    public string Description =>
        description;


    public UpgradeType RequiredModuleA =>
        requiredModuleA;

    public UpgradeType RequiredModuleB =>
        requiredModuleB;


    public Sprite WeaponSprite =>
        weaponSprite;

    public Vector2 WeaponVisualLocalOffset =>
        weaponVisualLocalOffset;

    public float WeaponVisualScaleMultiplier =>
        weaponVisualScaleMultiplier;


    public Sprite MuzzleFlashSprite =>
        muzzleFlashSprite;

    public Vector2 MuzzlePointLocalOffset =>
        muzzlePointLocalOffset;

    public Vector2 MuzzleFlashLocalOffset =>
        muzzleFlashLocalOffset;

    public float MuzzleFlashScaleMultiplier =>
        muzzleFlashScaleMultiplier;


    public Sprite ProjectileSprite =>
        projectileSprite;


    public bool UseProjectileTrail =>
        useProjectileTrail;

    public float ProjectileTrailTime =>
        projectileTrailTime;

    public float ProjectileTrailStartWidth =>
        projectileTrailStartWidth;

    public float ProjectileTrailEndWidth =>
        projectileTrailEndWidth;

    public Color ProjectileTrailStartColor =>
        projectileTrailStartColor;

    public Color ProjectileTrailEndColor =>
        projectileTrailEndColor;


    public AudioClip EvolutionSfx =>
        evolutionSfx;

    public AudioClip ShootSfx =>
        shootSfx;


    public int ExtraPierceCount =>
        extraPierceCount;

    public float ProjectileSpeedMultiplier =>
        projectileSpeedMultiplier;

    public float ChainRangeMultiplier =>
        chainRangeMultiplier;

    public int MaxChainTriggersPerBullet =>
        maxChainTriggersPerBullet;


    public float ExplosionRadiusMultiplier =>
        explosionRadiusMultiplier;

    public float ExplosionDamageMultiplier =>
        explosionDamageMultiplier;

    public int SplitCountBonus =>
        splitCountBonus;

    public float ChildExplosionRadiusMultiplier =>
        childExplosionRadiusMultiplier;

    public float ChildExplosionDamageMultiplier =>
        childExplosionDamageMultiplier;

    public float ProjectileScaleMultiplier =>
        projectileScaleMultiplier;

    public float FireCooldownMultiplier =>
        fireCooldownMultiplier;


    // =========================================================
    // Validation
    // =========================================================

    public bool HasValidModulePair =>
        IsCoreModuleType(requiredModuleA)
        && IsCoreModuleType(requiredModuleB)
        && requiredModuleA != requiredModuleB;


    private bool IsCoreModuleType(
        UpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case UpgradeType.Piercing:
            case UpgradeType.Explosive:
            case UpgradeType.ChainLightning:
            case UpgradeType.SplitShot:
                return true;

            default:
                return false;
        }
    }


    private void OnValidate()
    {
        weaponVisualScaleMultiplier =
            Mathf.Max(
                0.01f,
                weaponVisualScaleMultiplier
            );

        muzzleFlashScaleMultiplier =
            Mathf.Max(
                0.01f,
                muzzleFlashScaleMultiplier
            );

        projectileTrailTime =
            Mathf.Max(
                0.01f,
                projectileTrailTime
            );

        projectileTrailStartWidth =
            Mathf.Max(
                0f,
                projectileTrailStartWidth
            );

        projectileTrailEndWidth =
            Mathf.Max(
                0f,
                projectileTrailEndWidth
            );


        extraPierceCount =
            Mathf.Max(
                0,
                extraPierceCount
            );

        projectileSpeedMultiplier =
            Mathf.Max(
                0.01f,
                projectileSpeedMultiplier
            );

        chainRangeMultiplier =
            Mathf.Max(
                0.01f,
                chainRangeMultiplier
            );

        maxChainTriggersPerBullet =
            Mathf.Max(
                1,
                maxChainTriggersPerBullet
            );


        explosionRadiusMultiplier =
            Mathf.Max(
                0.01f,
                explosionRadiusMultiplier
            );

        explosionDamageMultiplier =
            Mathf.Max(
                0.01f,
                explosionDamageMultiplier
            );

        splitCountBonus =
            Mathf.Max(
                0,
                splitCountBonus
            );

        childExplosionRadiusMultiplier =
            Mathf.Max(
                0.01f,
                childExplosionRadiusMultiplier
            );

        childExplosionDamageMultiplier =
            Mathf.Max(
                0.01f,
                childExplosionDamageMultiplier
            );

        projectileScaleMultiplier =
            Mathf.Max(
                0.01f,
                projectileScaleMultiplier
            );

        fireCooldownMultiplier =
            Mathf.Max(
                0.01f,
                fireCooldownMultiplier
            );
    }
}