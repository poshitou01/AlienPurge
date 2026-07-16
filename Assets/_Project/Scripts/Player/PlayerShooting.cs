using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    private const float FloatComparisonTolerance = 0.0001f;

    [Header("Shooting Settings")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("子弹生成位置与玩家中心的距离")]
    [SerializeField] private float spawnOffset = 0.45f;

    [Tooltip("当前两次射击之间的冷却时间")]
    [SerializeField] private float fireCooldown = 0.15f;

    [Header("Current Bullet Attack Settings")]
    [Tooltip("当前每颗子弹造成的伤害")]
    [SerializeField] private int bulletDamage = 1;

    [Tooltip("当前子弹移动速度")]
    [SerializeField] private float bulletSpeed = 12f;

    [Tooltip("当前子弹相对于 Prefab 原始尺寸的倍率")]
    [SerializeField] private float bulletScaleMultiplier = 1f;

    [Tooltip("当前每次射击生成的弹丸数量")]
    [SerializeField] private int projectileCount = 1;

    [Tooltip("相邻两颗弹丸之间的角度")]
    [SerializeField] private float projectileSpreadAngle = 10f;

    [Header("Upgrade Limits")]
    [Tooltip("射击冷却时间的最低值")]
    [SerializeField] private float minimumFireCooldown = 0.05f;

    [Tooltip("子弹速度的最终上限")]
    [SerializeField] private float maximumBulletSpeed = 24f;

    [Tooltip("子弹尺寸倍率的最终上限")]
    [SerializeField] private float maximumBulletScaleMultiplier = 2f;

    [Tooltip("每次射击弹丸数量的最终上限")]
    [SerializeField] private int maximumProjectileCount = 5;

    private Camera mainCamera;
    private float nextFireTime;
    private bool canShoot = true;

    // 当前攻击属性只读接口
    public int CurrentBulletDamage => bulletDamage;
    public float CurrentFireCooldown => fireCooldown;
    public float CurrentBulletSpeed => bulletSpeed;

    public float CurrentBulletScaleMultiplier =>
        bulletScaleMultiplier;

    public int CurrentProjectileCount => projectileCount;

    // 攻击属性限制只读接口
    public float MinimumFireCooldown =>
        minimumFireCooldown;

    public float MaximumBulletSpeed =>
        maximumBulletSpeed;

    public float MaximumBulletScaleMultiplier =>
        maximumBulletScaleMultiplier;

    public int MaximumProjectileCount =>
        maximumProjectileCount;

    // 升级有效性判断接口
    public bool CanReduceFireCooldown =>
        fireCooldown >
        minimumFireCooldown + FloatComparisonTolerance;

    public bool CanIncreaseBulletSpeed =>
        bulletSpeed <
        maximumBulletSpeed - FloatComparisonTolerance;

    public bool CanIncreaseBulletScale =>
        bulletScaleMultiplier <
        maximumBulletScaleMultiplier
        - FloatComparisonTolerance;

    public bool CanIncreaseProjectileCount =>
        projectileCount < maximumProjectileCount;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!canShoot || UpgradeManager.IsChoosingUpgrade)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            TryShoot();
        }
    }

    private void TryShoot()
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        if (bulletPrefab == null)
        {
            Debug.LogWarning(
                "PlayerShooting: Bullet Prefab has not been assigned."
            );

            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning(
                    "PlayerShooting: Main Camera not found."
                );

                return;
            }
        }

        Vector3 mouseScreenPosition = Input.mousePosition;

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        mouseWorldPosition.z = 0f;

        Vector2 playerPosition = transform.position;

        Vector2 shootDirection =
            (Vector2)mouseWorldPosition - playerPosition;

        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        shootDirection.Normalize();

        FireProjectiles(
            playerPosition,
            shootDirection
        );

        nextFireTime = Time.time + fireCooldown;
    }

    /// <summary>
    /// 根据当前弹丸数量，以瞄准方向为中心生成对称散射。
    /// </summary>
    private void FireProjectiles(
        Vector2 playerPosition,
        Vector2 aimDirection)
    {
        float startAngle =
            -projectileSpreadAngle
            * (projectileCount - 1)
            * 0.5f;

        Vector2 spawnPosition =
            playerPosition
            + aimDirection * spawnOffset;

        for (int i = 0; i < projectileCount; i++)
        {
            float angleOffset =
                startAngle
                + projectileSpreadAngle * i;

            Vector2 projectileDirection =
                RotateDirection(
                    aimDirection,
                    angleOffset
                );

            CreateProjectile(
                spawnPosition,
                projectileDirection
            );
        }
    }

    /// <summary>
    /// 创建一颗子弹，并传入玩家当前的全部攻击属性。
    /// </summary>
    private void CreateProjectile(
        Vector2 spawnPosition,
        Vector2 projectileDirection)
    {
        GameObject bulletObject = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.identity
        );

        Bullet bullet =
            bulletObject.GetComponent<Bullet>();

        if (bullet == null)
        {
            Debug.LogWarning(
                "PlayerShooting: The spawned Bullet Prefab "
                + "does not contain a Bullet component."
            );

            Destroy(bulletObject);
            return;
        }

        bullet.Initialize(
            projectileDirection,
            bulletSpeed,
            bulletDamage,
            bulletScaleMultiplier
        );
    }

    /// <summary>
    /// 将一个二维方向旋转指定角度。
    /// 正角度为逆时针，负角度为顺时针。
    /// </summary>
    private Vector2 RotateDirection(
        Vector2 direction,
        float angleDegrees)
    {
        float angleRadians =
            angleDegrees * Mathf.Deg2Rad;

        float cosine = Mathf.Cos(angleRadians);
        float sine = Mathf.Sin(angleRadians);

        Vector2 rotatedDirection = new Vector2(
            direction.x * cosine
                - direction.y * sine,
            direction.x * sine
                + direction.y * cosine
        );

        return rotatedDirection.normalized;
    }

    public void SetCanShoot(bool value)
    {
        canShoot = value;
    }

    /// <summary>
    /// 减少射击冷却时间，但不会低于最低限制。
    /// </summary>
    public void ReduceFireCooldown(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        fireCooldown = Mathf.Max(
            minimumFireCooldown,
            fireCooldown - amount
        );

        Debug.Log(
            "Fire cooldown upgraded. Current fire cooldown: "
            + fireCooldown
        );
    }

    /// <summary>
    /// 增加子弹伤害。
    /// </summary>
    public void AddBulletDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        bulletDamage += amount;

        Debug.Log(
            "Bullet damage upgraded. Current bullet damage: "
            + bulletDamage
        );
    }

    /// <summary>
    /// 增加子弹速度，但不会超过最终上限。
    /// </summary>
    public void AddBulletSpeed(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        bulletSpeed = Mathf.Min(
            maximumBulletSpeed,
            bulletSpeed + amount
        );

        Debug.Log(
            "Bullet speed upgraded. Current bullet speed: "
            + bulletSpeed
        );
    }

    /// <summary>
    /// 增加子弹尺寸倍率，但不会超过最终上限。
    /// </summary>
    public void AddBulletScaleMultiplier(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        bulletScaleMultiplier = Mathf.Min(
            maximumBulletScaleMultiplier,
            bulletScaleMultiplier + amount
        );

        Debug.Log(
            "Bullet scale upgraded. Current bullet scale: "
            + bulletScaleMultiplier
        );
    }

    /// <summary>
    /// 增加每次射击的弹丸数量，但不会超过最终上限。
    /// </summary>
    public void AddProjectileCount(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        projectileCount = Mathf.Min(
            maximumProjectileCount,
            projectileCount + amount
        );

        Debug.Log(
            "Projectile count upgraded. Current projectile count: "
            + projectileCount
        );
    }

    /// <summary>
    /// 在 Console 中输出当前全部攻击属性和升级有效性。
    /// </summary>
    [ContextMenu("Debug/Print Current Attack Attributes")]
    private void PrintCurrentAttackAttributes()
    {
        Debug.Log(
            "===== Current Attack Attributes =====\n"
            + "Bullet Damage: "
            + bulletDamage
            + "\nFire Cooldown: "
            + fireCooldown
            + " / Minimum: "
            + minimumFireCooldown
            + "\nCan Reduce Fire Cooldown: "
            + CanReduceFireCooldown
            + "\nBullet Speed: "
            + bulletSpeed
            + " / Maximum: "
            + maximumBulletSpeed
            + "\nCan Increase Bullet Speed: "
            + CanIncreaseBulletSpeed
            + "\nBullet Scale Multiplier: "
            + bulletScaleMultiplier
            + " / Maximum: "
            + maximumBulletScaleMultiplier
            + "\nCan Increase Bullet Scale: "
            + CanIncreaseBulletScale
            + "\nProjectile Count: "
            + projectileCount
            + " / Maximum: "
            + maximumProjectileCount
            + "\nCan Increase Projectile Count: "
            + CanIncreaseProjectileCount,
            this
        );
    }

    /// <summary>
    /// 仅在运行模式下，将所有有限制的攻击属性设为上限，
    /// 用于快速测试满级升级过滤。
    /// </summary>
    [ContextMenu("Debug/Set Attack Attributes To Limits")]
    private void SetAttackAttributesToLimits()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请先进入 Play Mode，"
                + "再使用 Set Attack Attributes To Limits。",
                this
            );

            return;
        }

        fireCooldown = minimumFireCooldown;
        bulletSpeed = maximumBulletSpeed;

        bulletScaleMultiplier =
            maximumBulletScaleMultiplier;

        projectileCount = maximumProjectileCount;

        Debug.Log(
            "PlayerShooting: "
            + "All limited attack attributes "
            + "have been set to their limits.",
            this
        );

        PrintCurrentAttackAttributes();
    }

    private void OnValidate()
    {
        spawnOffset = Mathf.Max(0f, spawnOffset);

        minimumFireCooldown =
            Mathf.Max(0.01f, minimumFireCooldown);

        fireCooldown = Mathf.Max(
            minimumFireCooldown,
            fireCooldown
        );

        bulletDamage = Mathf.Max(1, bulletDamage);

        maximumBulletSpeed =
            Mathf.Max(0.01f, maximumBulletSpeed);

        bulletSpeed = Mathf.Clamp(
            bulletSpeed,
            0.01f,
            maximumBulletSpeed
        );

        maximumBulletScaleMultiplier =
            Mathf.Max(
                0.01f,
                maximumBulletScaleMultiplier
            );

        bulletScaleMultiplier = Mathf.Clamp(
            bulletScaleMultiplier,
            0.01f,
            maximumBulletScaleMultiplier
        );

        maximumProjectileCount =
            Mathf.Max(1, maximumProjectileCount);

        projectileCount = Mathf.Clamp(
            projectileCount,
            1,
            maximumProjectileCount
        );

        projectileSpreadAngle =
            Mathf.Clamp(
                projectileSpreadAngle,
                0f,
                90f
            );
    }
}