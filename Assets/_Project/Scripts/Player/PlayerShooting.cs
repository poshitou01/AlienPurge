using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerShooting : MonoBehaviour
{
    private const float FloatComparisonTolerance = 0.0001f;

    [Header("Shooting Settings")]
    [Tooltip("负责提供和回收 Bullet 的对象池")]
    [SerializeField] private BulletPool bulletPool;

    [Tooltip("子弹生成位置与玩家中心的距离")]
    [SerializeField] private float spawnOffset = 0.45f;

    [Tooltip("每颗子弹存在的时间")]
    [SerializeField] private float bulletLifeTime = 2f;

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
    private PlayerAbilityState abilityState;

    // 第三十二阶段新增：
    // 保存玩家当前机制型升级状态的组件引用。
    private PlayerWeaponModifiers weaponModifiers;

    private float nextFireTime;
    private bool canShoot = true;

    // 当前攻击属性只读接口
    public int CurrentBulletDamage => bulletDamage;
    public float CurrentFireCooldown => fireCooldown;
    public float CurrentBulletSpeed => bulletSpeed;
    public float CurrentBulletLifeTime => bulletLifeTime;

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

        abilityState =
            GetComponent<PlayerAbilityState>();

        weaponModifiers =
            GetComponent<PlayerWeaponModifiers>();

        if (weaponModifiers == null)
        {
            Debug.LogWarning(
                "PlayerShooting: "
                + "PlayerWeaponModifiers was not found. "
                + "Bullets will use the default "
                + "mechanic snapshot.",
                this
            );
        }
    }


    private void Update()
    {
        if (!CanProcessShootingInput())
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            TryShoot();
        }
    }


    private bool CanProcessShootingInput()
    {
        if (abilityState != null
            && (abilityState.IsDashing
                || abilityState.IsCasting))
        {
            return false;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        if (PauseMenuController.IsPaused)
        {
            return false;
        }

        if (GameManager.Instance != null
            && !GameManager.Instance.IsPlaying)
        {
            return false;
        }

        if (EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        return true;
    }


    private void TryShoot()
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        if (bulletPool == null)
        {
            Debug.LogWarning(
                "PlayerShooting: Bullet Pool has not been assigned.",
                this
            );

            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning(
                    "PlayerShooting: Main Camera not found.",
                    this
                );

                return;
            }
        }

        Vector3 mouseScreenPosition =
            Input.mousePosition;

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(
                mouseScreenPosition
            );

        mouseWorldPosition.z = 0f;

        Vector2 playerPosition =
            transform.position;

        Vector2 shootDirection =
            (Vector2)mouseWorldPosition
            - playerPosition;

        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        shootDirection.Normalize();

        bool firedAnyProjectile =
            FireProjectiles(
                playerPosition,
                shootDirection
            );

        if (!firedAnyProjectile)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayShoot();
        }

        nextFireTime =
            Time.time + fireCooldown;
    }


    private bool FireProjectiles(
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

        // 一次射击只创建一次机制快照。
        // 同一轮散射出来的所有 Bullet
        // 都使用完全相同的机制规则。
        ProjectileModifierSnapshot modifierSnapshot =
            BuildProjectileModifierSnapshot();

        bool firedAnyProjectile = false;

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

            bool projectileCreated =
                CreateProjectile(
                    spawnPosition,
                    projectileDirection,
                    modifierSnapshot
                );

            if (projectileCreated)
            {
                firedAnyProjectile = true;
            }
        }

        return firedAnyProjectile;
    }


    private bool CreateProjectile(
        Vector2 spawnPosition,
        Vector2 projectileDirection,
        ProjectileModifierSnapshot modifierSnapshot)
    {
        Bullet bullet =
            bulletPool.GetBullet(
                spawnPosition,
                Quaternion.identity
            );

        if (bullet == null)
        {
            Debug.LogWarning(
                "PlayerShooting: Bullet Pool could not "
                + "provide an available Bullet.",
                this
            );

            return false;
        }

        bullet.Initialize(
            projectileDirection,
            bulletSpeed,
            bulletDamage,
            bulletScaleMultiplier,
            bulletLifeTime,
            modifierSnapshot
        );

        return true;
    }


    /// <summary>
    /// 在当前射击发生的瞬间，
    /// 将玩家当前机制升级状态转换成独立快照。
    ///
    /// Bullet 之后只读取自己的 Snapshot，
    /// 不会在飞行途中重新读取 PlayerWeaponModifiers。
    /// </summary>
    private ProjectileModifierSnapshot
        BuildProjectileModifierSnapshot()
    {
        if (weaponModifiers == null)
        {
            return ProjectileModifierSnapshot.Default;
        }

        return new ProjectileModifierSnapshot(
            weaponModifiers.PierceCount,

            weaponModifiers.HasExplosive,
            weaponModifiers.ExplosionRadius,
            weaponModifiers.ExplosionDamageMultiplier,

            weaponModifiers.HasChainLightning,
            weaponModifiers.ChainCount,
            weaponModifiers.ChainRange,
            weaponModifiers.ChainDamageMultiplier,

            weaponModifiers.HasSplitShot,
            weaponModifiers.SplitCount,
            weaponModifiers.ChildDamageMultiplier,
            weaponModifiers.ChildSpeedMultiplier,
            weaponModifiers.ChildScaleMultiplier,
            weaponModifiers.ChildLifeTimeMultiplier,

            0
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

        float cosine =
            Mathf.Cos(angleRadians);

        float sine =
            Mathf.Sin(angleRadians);

        Vector2 rotatedDirection =
            new Vector2(
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


    public void ReduceFireCooldown(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        fireCooldown =
            Mathf.Max(
                minimumFireCooldown,
                fireCooldown - amount
            );

        Debug.Log(
            "Fire cooldown upgraded. "
            + "Current fire cooldown: "
            + fireCooldown
        );
    }


    public void AddBulletDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        bulletDamage += amount;

        Debug.Log(
            "Bullet damage upgraded. "
            + "Current bullet damage: "
            + bulletDamage
        );
    }


    public void AddBulletSpeed(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        bulletSpeed =
            Mathf.Min(
                maximumBulletSpeed,
                bulletSpeed + amount
            );

        Debug.Log(
            "Bullet speed upgraded. "
            + "Current bullet speed: "
            + bulletSpeed
        );
    }


    public void AddBulletScaleMultiplier(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        bulletScaleMultiplier =
            Mathf.Min(
                maximumBulletScaleMultiplier,
                bulletScaleMultiplier + amount
            );

        Debug.Log(
            "Bullet scale upgraded. "
            + "Current bullet scale: "
            + bulletScaleMultiplier
        );
    }


    public void AddProjectileCount(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        projectileCount =
            Mathf.Min(
                maximumProjectileCount,
                projectileCount + amount
            );

        Debug.Log(
            "Projectile count upgraded. "
            + "Current projectile count: "
            + projectileCount
        );
    }


    // =========================================================
    // Phase 32 Debug
    // =========================================================

    [ContextMenu("Debug/Print Current Projectile Snapshot")]
    private void PrintCurrentProjectileSnapshot()
    {
        ProjectileModifierSnapshot snapshot =
            BuildProjectileModifierSnapshot();

        Debug.Log(
            snapshot.GetDebugText(),
            this
        );
    }


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
            + "\nBullet Life Time: "
            + bulletLifeTime
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

        fireCooldown =
            minimumFireCooldown;

        bulletSpeed =
            maximumBulletSpeed;

        bulletScaleMultiplier =
            maximumBulletScaleMultiplier;

        projectileCount =
            maximumProjectileCount;

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
        spawnOffset =
            Mathf.Max(0f, spawnOffset);

        bulletLifeTime =
            Mathf.Max(0.01f, bulletLifeTime);

        minimumFireCooldown =
            Mathf.Max(
                0.01f,
                minimumFireCooldown
            );

        fireCooldown =
            Mathf.Max(
                minimumFireCooldown,
                fireCooldown
            );

        bulletDamage =
            Mathf.Max(1, bulletDamage);

        maximumBulletSpeed =
            Mathf.Max(
                0.01f,
                maximumBulletSpeed
            );

        bulletSpeed =
            Mathf.Clamp(
                bulletSpeed,
                0.01f,
                maximumBulletSpeed
            );

        maximumBulletScaleMultiplier =
            Mathf.Max(
                0.01f,
                maximumBulletScaleMultiplier
            );

        bulletScaleMultiplier =
            Mathf.Clamp(
                bulletScaleMultiplier,
                0.01f,
                maximumBulletScaleMultiplier
            );

        maximumProjectileCount =
            Mathf.Max(
                1,
                maximumProjectileCount
            );

        projectileCount =
            Mathf.Clamp(
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