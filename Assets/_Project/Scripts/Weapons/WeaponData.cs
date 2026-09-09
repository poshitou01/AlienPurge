using UnityEngine;


[CreateAssetMenu(
    fileName = "WeaponData",
    menuName = "AlienPurge/Weapon Data"
)]
public class WeaponData : ScriptableObject
{
    // =========================================================
    // Identity
    // =========================================================

    [Header("Identity")]

    [SerializeField]
    private string weaponId = "plasma_rifle";

    [SerializeField]
    private string displayName = "Plasma Rifle";

    [SerializeField]
    private Sprite weaponIcon;

    [SerializeField]
    private GameObject weaponPrefab;


    // =========================================================
    // Base Attack Settings
    // =========================================================

    [Header("Base Attack Settings")]

    [Min(1)]
    [SerializeField]
    private int baseDamage = 1;

    [Min(0.01f)]
    [SerializeField]
    private float baseFireCooldown = 0.15f;

    [Min(0.01f)]
    [SerializeField]
    private float baseProjectileSpeed = 12f;

    [Min(0.01f)]
    [SerializeField]
    private float baseProjectileLifeTime = 2f;

    [Min(0.01f)]
    [SerializeField]
    private float baseProjectileScale = 1f;

    [Min(1)]
    [SerializeField]
    private int baseProjectileCount = 1;

    [Range(0f, 90f)]
    [SerializeField]
    private float baseProjectileSpreadAngle = 10f;


    // =========================================================
    // Upgrade Limits
    // =========================================================

    [Header("Upgrade Limits")]

    [Min(0.01f)]
    [SerializeField]
    private float minimumFireCooldown = 0.05f;

    [Min(0.01f)]
    [SerializeField]
    private float maximumProjectileSpeed = 24f;

    [Min(0.01f)]
    [SerializeField]
    private float maximumProjectileScale = 2f;

    [Min(1)]
    [SerializeField]
    private int maximumProjectileCount = 5;


    // =========================================================
    // Read Only Access
    // =========================================================

    public string WeaponId => weaponId;

    public string DisplayName => displayName;

    public Sprite WeaponIcon => weaponIcon;

    public GameObject WeaponPrefab => weaponPrefab;


    public int BaseDamage => baseDamage;

    public float BaseFireCooldown =>
        baseFireCooldown;

    public float BaseProjectileSpeed =>
        baseProjectileSpeed;

    public float BaseProjectileLifeTime =>
        baseProjectileLifeTime;

    public float BaseProjectileScale =>
        baseProjectileScale;

    public int BaseProjectileCount =>
        baseProjectileCount;

    public float BaseProjectileSpreadAngle =>
        baseProjectileSpreadAngle;


    public float MinimumFireCooldown =>
        minimumFireCooldown;

    public float MaximumProjectileSpeed =>
        maximumProjectileSpeed;

    public float MaximumProjectileScale =>
        maximumProjectileScale;

    public int MaximumProjectileCount =>
        maximumProjectileCount;


    private void OnValidate()
    {
        baseDamage =
            Mathf.Max(1, baseDamage);

        minimumFireCooldown =
            Mathf.Max(
                0.01f,
                minimumFireCooldown
            );

        baseFireCooldown =
            Mathf.Max(
                minimumFireCooldown,
                baseFireCooldown
            );

        maximumProjectileSpeed =
            Mathf.Max(
                0.01f,
                maximumProjectileSpeed
            );

        baseProjectileSpeed =
            Mathf.Clamp(
                baseProjectileSpeed,
                0.01f,
                maximumProjectileSpeed
            );

        baseProjectileLifeTime =
            Mathf.Max(
                0.01f,
                baseProjectileLifeTime
            );

        maximumProjectileScale =
            Mathf.Max(
                0.01f,
                maximumProjectileScale
            );

        baseProjectileScale =
            Mathf.Clamp(
                baseProjectileScale,
                0.01f,
                maximumProjectileScale
            );

        maximumProjectileCount =
            Mathf.Max(
                1,
                maximumProjectileCount
            );

        baseProjectileCount =
            Mathf.Clamp(
                baseProjectileCount,
                1,
                maximumProjectileCount
            );

        baseProjectileSpreadAngle =
            Mathf.Clamp(
                baseProjectileSpreadAngle,
                0f,
                90f
            );
    }
}