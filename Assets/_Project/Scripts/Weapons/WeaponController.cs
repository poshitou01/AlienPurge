using UnityEngine;


[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    private const float FloatComparisonTolerance = 0.0001f;


    // =========================================================
    // Weapon Definition
    // =========================================================

    [Header("Weapon Definition")]

    [SerializeField]
    private WeaponData weaponData;


    // =========================================================
    // Visual References
    // =========================================================

    [Header("Visual References")]

    [Tooltip("负责围绕玩家旋转的武器瞄准 Pivot。")]
    [SerializeField]
    private Transform weaponHolder;

    [Tooltip("真实枪口位置。")]
    [SerializeField]
    private Transform muzzlePoint;

    [Tooltip("枪口瞬时闪光对象。")]
    [SerializeField]
    private GameObject muzzleFlash;


    // =========================================================
    // Shot Feedback
    // =========================================================

    [Header("Shot Feedback")]

    [Tooltip("枪口闪光单次显示时间。")]
    [Min(0.01f)]
    [SerializeField]
    private float muzzleFlashDuration = 0.05f;

    [Tooltip("每次射击时武器沿局部 X 轴向后移动的距离。")]
    [Min(0f)]
    [SerializeField]
    private float recoilDistance = 0.04f;

    [Tooltip("武器从后坐位置恢复到正常位置所需时间。")]
    [Min(0.01f)]
    [SerializeField]
    private float recoilRecoveryDuration = 0.08f;


    // =========================================================
    // Visual Upgrade
    // =========================================================

    [Header("Visual Upgrade")]

    [SerializeField]
    private WeaponUpgradeVisualController
        upgradeVisualController;

    // =========================================================
    // Weapon Growth
    // =========================================================

    [Header("Weapon Growth")]

    [Tooltip("当前这一局中武器的成长等级。")]
    [Min(1)]
    [SerializeField]
    private int weaponLevel = 1;

    [Tooltip("每提升一级，枪口闪光尺寸额外增加多少比例。")]
    [Min(0f)]
    [SerializeField]
    private float muzzleFlashScalePerLevel = 0.15f;


    // =========================================================
    // Runtime Dependencies
    // =========================================================

    private BulletPool bulletPool;

    private PlayerWeaponModifiers weaponModifiers;


    // =========================================================
    // Runtime Weapon Stats
    // =========================================================

    private int currentDamage;

    private float currentFireCooldown;

    private float currentProjectileSpeed;

    private float currentProjectileLifeTime;

    private float currentProjectileScale;

    private int currentProjectileCount;

    private float currentProjectileSpreadAngle;

    private float nextFireTime;


    // =========================================================
    // Runtime Aim State
    // =========================================================

    private Vector2 aimDirection =
        Vector2.right;

    private bool isAimingLeft;

    private bool isOperational = true;


    // =========================================================
    // Runtime Visual State
    // =========================================================

    private Vector3 baseLocalPosition;

    private Vector3 baseLocalScale;

    private Vector3 muzzleFlashBaseLocalScale =
        Vector3.one;

    private bool recoilActive;

    private float recoilElapsed;

    private float muzzleFlashHideTime;


    // =========================================================
    // Public Read Only Access
    // =========================================================

    public WeaponData Data =>
        weaponData;

    public Transform MuzzlePoint =>
        muzzlePoint;

    public Vector2 AimDirection =>
        aimDirection;

    public bool IsAimingLeft =>
        isAimingLeft;

    public int WeaponLevel =>
        weaponLevel;


    public int CurrentDamage =>
        currentDamage;

    public float CurrentFireCooldown =>
        currentFireCooldown;

    public float CurrentProjectileSpeed =>
        currentProjectileSpeed;

    public float CurrentProjectileLifeTime =>
        currentProjectileLifeTime;

    public float CurrentProjectileScale =>
        currentProjectileScale;

    public int CurrentProjectileCount =>
        currentProjectileCount;

    public float CurrentProjectileSpreadAngle =>
        currentProjectileSpreadAngle;


    /// <summary>
    /// 为以后音效、震动等系统预留的武器强度倍率。
    /// 当前暂时只作为只读接口使用。
    /// </summary>
    public float ShotPowerMultiplier =>
        1f
        + (weaponLevel - 1)
        * 0.1f;


    // =========================================================
    // Upgrade Availability
    // =========================================================

    public bool CanReduceFireCooldown =>
        weaponData != null
        && currentFireCooldown
        > weaponData.MinimumFireCooldown
        + FloatComparisonTolerance;

    public bool CanIncreaseProjectileSpeed =>
        weaponData != null
        && currentProjectileSpeed
        < weaponData.MaximumProjectileSpeed
        - FloatComparisonTolerance;

    public bool CanIncreaseProjectileScale =>
        weaponData != null
        && currentProjectileScale
        < weaponData.MaximumProjectileScale
        - FloatComparisonTolerance;

    public bool CanIncreaseProjectileCount =>
        weaponData != null
        && currentProjectileCount
        < weaponData.MaximumProjectileCount;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        baseLocalPosition =
            transform.localPosition;

        baseLocalScale =
            transform.localScale;

        if (weaponHolder == null)
        {
            weaponHolder =
                transform.parent;
        }

        if (upgradeVisualController == null)
        {
            upgradeVisualController =
                GetComponent<
                WeaponUpgradeVisualController>();
        }

        if (muzzleFlash != null)
        {
            muzzleFlashBaseLocalScale =
                muzzleFlash.transform.localScale;
        }

        InitializeRuntimeStats();

        ResetShotFeedback();
    }


    private void Update()
    {
        UpdateMuzzleFlash();

        UpdateRecoil();
    }


    private void OnDisable()
    {
        ResetShotFeedback();
    }


    // =========================================================
    // Initialization
    // =========================================================

    private void InitializeRuntimeStats()
    {
        if (weaponData == null)
        {
            Debug.LogError(
                "WeaponController: "
                + "WeaponData has not been assigned.",
                this
            );

            return;
        }

        currentDamage =
            weaponData.BaseDamage;

        currentFireCooldown =
            weaponData.BaseFireCooldown;

        currentProjectileSpeed =
            weaponData.BaseProjectileSpeed;

        currentProjectileLifeTime =
            weaponData.BaseProjectileLifeTime;

        currentProjectileScale =
            weaponData.BaseProjectileScale;

        currentProjectileCount =
            weaponData.BaseProjectileCount;

        currentProjectileSpreadAngle =
            weaponData.BaseProjectileSpreadAngle;

        nextFireTime =
            0f;

        // 每一局开始时，
        // 当前武器从 Lv1 开始。
        weaponLevel =
            1;

        if (upgradeVisualController != null)
        {
            upgradeVisualController
                .ApplyWeaponLevel(
                    weaponLevel
                );
        }
    }


    /// <summary>
    /// WeaponManager 在运行时向当前武器注入
    /// BulletPool 和玩家机制升级状态。
    /// </summary>
    public void SetRuntimeDependencies(
        BulletPool newBulletPool,
        PlayerWeaponModifiers newWeaponModifiers)
    {
        bulletPool =
            newBulletPool;

        weaponModifiers =
            newWeaponModifiers;

        if (bulletPool == null)
        {
            Debug.LogWarning(
                "WeaponController: "
                + "BulletPool dependency is null.",
                this
            );
        }

        if (weaponModifiers == null)
        {
            Debug.LogWarning(
                "WeaponController: "
                + "PlayerWeaponModifiers dependency is null. "
                + "Projectiles will use the default "
                + "modifier snapshot.",
                this
            );
        }
    }


    // =========================================================
    // Aim
    // =========================================================

    public void AimAt(
        Vector2 worldPosition)
    {
        if (!isOperational
            || weaponHolder == null)
        {
            return;
        }

        Vector2 direction =
            worldPosition
            - (Vector2)weaponHolder.position;

        if (direction.sqrMagnitude
            <= 0.0001f)
        {
            return;
        }

        aimDirection =
            direction.normalized;

        float angle =
            Mathf.Atan2(
                aimDirection.y,
                aimDirection.x
            )
            * Mathf.Rad2Deg;

        weaponHolder.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );

        UpdateWeaponFlip();
    }


    private void UpdateWeaponFlip()
    {
        // 接近纯竖直方向时保持上一帧状态，
        // 避免鼠标经过正上方或正下方时
        // 左右翻转状态频繁抖动。
        if (aimDirection.x > 0.001f)
        {
            SetAimingLeft(false);
        }
        else if (aimDirection.x < -0.001f)
        {
            SetAimingLeft(true);
        }
    }


    private void SetAimingLeft(
        bool value)
    {
        if (isAimingLeft == value)
        {
            return;
        }

        isAimingLeft =
            value;

        Vector3 scale =
            baseLocalScale;

        // 武器本身默认沿 +X 朝右。
        // WeaponHolder 已经负责旋转方向。
        // 左侧瞄准时只翻转局部 Y，
        // 防止武器上下倒置。
        scale.y =
            Mathf.Abs(
                baseLocalScale.y
            )
            * (isAimingLeft ? -1f : 1f);

        transform.localScale =
            scale;
    }


    // =========================================================
    // Fire
    // =========================================================

    public bool TryFire()
    {
        if (!isOperational)
        {
            return false;
        }

        if (Time.time < nextFireTime)
        {
            return false;
        }

        if (weaponData == null)
        {
            return false;
        }

        if (bulletPool == null)
        {
            Debug.LogWarning(
                "WeaponController: "
                + "Cannot fire because BulletPool "
                + "has not been assigned.",
                this
            );

            return false;
        }

        if (muzzlePoint == null)
        {
            Debug.LogWarning(
                "WeaponController: "
                + "Cannot fire because MuzzlePoint "
                + "has not been assigned.",
                this
            );

            return false;
        }

        bool firedAnyProjectile =
            FireProjectiles();

        if (!firedAnyProjectile)
        {
            return false;
        }

        nextFireTime =
            Time.time
            + currentFireCooldown;

        PlayShotFeedback();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayShoot();
        }

        return true;
    }


    private bool FireProjectiles()
    {
        float startAngle =
            -currentProjectileSpreadAngle
            * (currentProjectileCount - 1)
            * 0.5f;

        // Phase35：
        // 不再使用 Player 中心 + spawnOffset。
        // 所有玩家子弹真正从枪口生成。
        Vector2 spawnPosition =
            muzzlePoint.position;

        // 每轮射击只生成一份机制快照。
        ProjectileModifierSnapshot
            modifierSnapshot =
                BuildProjectileModifierSnapshot();

        bool firedAnyProjectile =
            false;

        for (int i = 0;
             i < currentProjectileCount;
             i++)
        {
            float angleOffset =
                startAngle
                + currentProjectileSpreadAngle
                * i;

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
                firedAnyProjectile =
                    true;
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
                "WeaponController: "
                + "BulletPool could not provide "
                + "an available Bullet.",
                this
            );

            return false;
        }

        bullet.Initialize(
            projectileDirection,
            currentProjectileSpeed,
            currentDamage,
            currentProjectileScale,
            currentProjectileLifeTime,
            modifierSnapshot
        );

        return true;
    }


    // =========================================================
    // Projectile Modifier Snapshot
    // =========================================================

    /// <summary>
    /// 在射击瞬间读取玩家当前机制型升级，
    /// 并转换为独立 ProjectileModifierSnapshot。
    ///
    /// Bullet 发射之后只读取自己的 Snapshot，
    /// 不会继续读取 PlayerWeaponModifiers。
    /// </summary>
    private ProjectileModifierSnapshot
        BuildProjectileModifierSnapshot()
    {
        if (weaponModifiers == null)
        {
            return
                ProjectileModifierSnapshot.Default;
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


    // =========================================================
    // Direction Utility
    // =========================================================

    private Vector2 RotateDirection(
        Vector2 direction,
        float angleDegrees)
    {
        float angleRadians =
            angleDegrees
            * Mathf.Deg2Rad;

        float cosine =
            Mathf.Cos(
                angleRadians
            );

        float sine =
            Mathf.Sin(
                angleRadians
            );

        Vector2 rotatedDirection =
            new Vector2(
                direction.x * cosine
                - direction.y * sine,

                direction.x * sine
                + direction.y * cosine
            );

        return
            rotatedDirection.normalized;
    }


    // =========================================================
    // Shot Feedback
    // =========================================================

    private void PlayShotFeedback()
    {
        PlayMuzzleFlash();

        PlayRecoil();
    }


    private void PlayMuzzleFlash()
    {
        if (muzzleFlash == null)
        {
            return;
        }

        float levelScaleMultiplier =
            1f
            + (weaponLevel - 1)
            * muzzleFlashScalePerLevel;

        // 保留你在 Inspector 中已经调好的
        // MuzzleFlash 原始比例，
        // 而不是强制改成 Vector3.one。
        muzzleFlash.transform.localScale =
            muzzleFlashBaseLocalScale
            * levelScaleMultiplier;

        muzzleFlash.SetActive(
            true
        );

        muzzleFlashHideTime =
            Time.unscaledTime
            + muzzleFlashDuration;
    }


    private void PlayRecoil()
    {
        if (recoilDistance <= 0f)
        {
            return;
        }

        recoilActive =
            true;

        recoilElapsed =
            0f;

        // CurrentWeapon 沿自己的局部 -X 后坐。
        // WeaponHolder 负责旋转，因此无论枪朝哪个方向，
        // 后坐永远都是枪口方向的反方向。
        transform.localPosition =
            baseLocalPosition
            + Vector3.left
            * recoilDistance;
    }


    private void UpdateMuzzleFlash()
    {
        if (muzzleFlash == null
            || !muzzleFlash.activeSelf)
        {
            return;
        }

        if (Time.unscaledTime
            < muzzleFlashHideTime)
        {
            return;
        }

        muzzleFlash.SetActive(
            false
        );
    }


    private void UpdateRecoil()
    {
        if (!recoilActive)
        {
            return;
        }

        recoilElapsed +=
            Time.unscaledDeltaTime;

        float t =
            Mathf.Clamp01(
                recoilElapsed
                / recoilRecoveryDuration
            );

        Vector3 recoilStartPosition =
            baseLocalPosition
            + Vector3.left
            * recoilDistance;

        transform.localPosition =
            Vector3.Lerp(
                recoilStartPosition,
                baseLocalPosition,
                t
            );

        if (t >= 1f)
        {
            recoilActive =
                false;

            transform.localPosition =
                baseLocalPosition;
        }
    }


    private void ResetShotFeedback()
    {
        recoilActive =
            false;

        recoilElapsed =
            0f;

        transform.localPosition =
            baseLocalPosition;

        transform.localScale =
            baseLocalScale;

        isAimingLeft =
            false;

        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(
                false
            );

            muzzleFlash.transform.localScale =
                muzzleFlashBaseLocalScale;
        }
    }


    // =========================================================
    // Weapon Growth
    // =========================================================

    /// <summary>
    /// 当前武器成长等级提高一级。
    ///
    /// 注意：
    /// Level 本身目前主要用于视觉反馈和后续系统。
    /// 实际 Damage / Cooldown 等数值
    /// 仍由对应升级接口独立修改。
    /// </summary>
    public void IncreaseWeaponLevel()
    {
        weaponLevel++;


        if (upgradeVisualController != null)
        {
            upgradeVisualController
                .ApplyWeaponLevel(
                    weaponLevel
                );
        }


        Debug.Log(
            "Weapon upgraded to Lv"
            + weaponLevel,
            this
        );
    }


    // =========================================================
    // Runtime Weapon Upgrades
    // =========================================================

    public void ReduceFireCooldown(
        float amount)
    {
        if (weaponData == null
            || amount <= 0f)
        {
            return;
        }

        currentFireCooldown =
            Mathf.Max(
                weaponData.MinimumFireCooldown,
                currentFireCooldown - amount
            );

        Debug.Log(
            "WeaponController: "
            + "Fire cooldown upgraded. "
            + "Current: "
            + currentFireCooldown,
            this
        );
    }


    public void AddDamage(
        int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentDamage +=
            amount;

        Debug.Log(
            "WeaponController: "
            + "Damage upgraded. "
            + "Current: "
            + currentDamage,
            this
        );
    }


    public void AddProjectileSpeed(
        float amount)
    {
        if (weaponData == null
            || amount <= 0f)
        {
            return;
        }

        currentProjectileSpeed =
            Mathf.Min(
                weaponData.MaximumProjectileSpeed,
                currentProjectileSpeed + amount
            );

        Debug.Log(
            "WeaponController: "
            + "Projectile speed upgraded. "
            + "Current: "
            + currentProjectileSpeed,
            this
        );
    }


    public void AddProjectileScale(
        float amount)
    {
        if (weaponData == null
            || amount <= 0f)
        {
            return;
        }

        currentProjectileScale =
            Mathf.Min(
                weaponData.MaximumProjectileScale,
                currentProjectileScale + amount
            );

        Debug.Log(
            "WeaponController: "
            + "Projectile scale upgraded. "
            + "Current: "
            + currentProjectileScale,
            this
        );
    }


    public void AddProjectileCount(
        int amount)
    {
        if (weaponData == null
            || amount <= 0)
        {
            return;
        }

        currentProjectileCount =
            Mathf.Min(
                weaponData.MaximumProjectileCount,
                currentProjectileCount + amount
            );

        Debug.Log(
            "WeaponController: "
            + "Projectile count upgraded. "
            + "Current: "
            + currentProjectileCount,
            this
        );
    }


    // =========================================================
    // Operational State
    // =========================================================

    /// <summary>
    /// 控制当前武器是否允许继续瞄准和射击。
    /// 主要用于死亡、卸下武器等状态。
    /// </summary>
    public void SetOperational(
        bool value)
    {
        isOperational =
            value;

        if (!isOperational)
        {
            ResetShotFeedback();
        }
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu("Debug/Increase Weapon Level")]
    private void DebugIncreaseWeaponLevel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "WeaponController: "
                + "Please enter Play Mode before "
                + "increasing weapon level.",
                this
            );

            return;
        }

        IncreaseWeaponLevel();

        PrintRuntimeWeaponStats();
    }


    [ContextMenu("Debug/Print Runtime Weapon Stats")]
    private void PrintRuntimeWeaponStats()
    {
        Debug.Log(
            "===== Runtime Weapon Stats =====\n"
            + "Weapon: "
            + (weaponData != null
                ? weaponData.DisplayName
                : "None")
            + "\nWeapon Level: "
            + weaponLevel
            + "\nDamage: "
            + currentDamage
            + "\nFire Cooldown: "
            + currentFireCooldown
            + "\nProjectile Speed: "
            + currentProjectileSpeed
            + "\nProjectile Life Time: "
            + currentProjectileLifeTime
            + "\nProjectile Scale: "
            + currentProjectileScale
            + "\nProjectile Count: "
            + currentProjectileCount
            + "\nSpread Angle: "
            + currentProjectileSpreadAngle
            + "\nShot Power Multiplier: "
            + ShotPowerMultiplier,
            this
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        muzzleFlashDuration =
            Mathf.Max(
                0.01f,
                muzzleFlashDuration
            );

        recoilDistance =
            Mathf.Max(
                0f,
                recoilDistance
            );

        recoilRecoveryDuration =
            Mathf.Max(
                0.01f,
                recoilRecoveryDuration
            );

        weaponLevel =
            Mathf.Max(
                1,
                weaponLevel
            );

        muzzleFlashScalePerLevel =
            Mathf.Max(
                0f,
                muzzleFlashScalePerLevel
            );

        if (weaponHolder == null
            && transform.parent != null)
        {
            weaponHolder =
                transform.parent;
        }
    }
}