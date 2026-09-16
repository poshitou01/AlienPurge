using UnityEngine;


[DisallowMultipleComponent]
public class WeaponController : MonoBehaviour
{
    private const float FloatComparisonTolerance =
        0.0001f;


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

    [SerializeField]
    private Transform weaponHolder;

    [SerializeField]
    private Transform muzzlePoint;

    [SerializeField]
    private GameObject muzzleFlash;


    // =========================================================
    // Shot Feedback
    // =========================================================

    [Header("Shot Feedback")]

    [Min(0.01f)]
    [SerializeField]
    private float muzzleFlashDuration =
        0.05f;

    [Min(0f)]
    [SerializeField]
    private float recoilDistance =
        0.04f;

    [Min(0.01f)]
    [SerializeField]
    private float recoilRecoveryDuration =
        0.08f;


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

    [Min(1)]
    [SerializeField]
    private int weaponLevel = 1;


    // =========================================================
    // Runtime Dependencies
    // =========================================================

    private BulletPool bulletPool;

    private PlayerWeaponModifiers weaponModifiers;

    private WeaponEvolutionController
        evolutionController;


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
    // Runtime Aim
    // =========================================================

    private Vector2 aimDirection =
        Vector2.right;

    private bool isAimingLeft;

    private bool isOperational = true;


    // =========================================================
    // Runtime Visual
    // =========================================================

    private Vector3 baseLocalPosition;

    private Vector3 baseLocalScale;

    private Vector3 baseMuzzlePointLocalPosition;

    private bool recoilActive;

    private float recoilElapsed;

    private float muzzleFlashHideTime;


    private WeaponEvolutionType
        appliedVisualEvolution =
            WeaponEvolutionType.None;

    private WeaponEvolutionData
        appliedVisualEvolutionData;


    // =========================================================
    // Public Access
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


    public float EffectiveFireCooldown =>
        GetEffectiveFireCooldown();

    public float EffectiveProjectileSpeed =>
        GetEffectiveProjectileSpeed();

    public float EffectiveProjectileScale =>
        GetEffectiveProjectileScale();


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


        ResolveEvolutionController();


        if (muzzlePoint != null)
        {
            baseMuzzlePointLocalPosition =
                muzzlePoint.localPosition;
        }


        InitializeRuntimeStats();

        ResetShotFeedback();

        RefreshEvolutionVisual(
            true
        );
    }


    private void Update()
    {
        UpdateMuzzleFlash();

        UpdateRecoil();

        RefreshEvolutionVisual(
            false
        );
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


    public void SetRuntimeDependencies(
        BulletPool newBulletPool,
        PlayerWeaponModifiers newWeaponModifiers)
    {
        bulletPool =
            newBulletPool;

        weaponModifiers =
            newWeaponModifiers;


        ResolveEvolutionController();

        RefreshEvolutionVisual(
            true
        );


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
                + "PlayerWeaponModifiers dependency is null.",
                this
            );
        }
    }


    // =========================================================
    // Evolution
    // =========================================================

    private void ResolveEvolutionController()
    {
        if (evolutionController != null)
        {
            return;
        }


        evolutionController =
            GetComponentInParent<
                WeaponEvolutionController>();
    }


    private WeaponEvolutionData
        GetCurrentEvolutionData()
    {
        ResolveEvolutionController();


        if (evolutionController == null)
        {
            return null;
        }


        if (evolutionController.CurrentEvolution
            == WeaponEvolutionType.None)
        {
            return null;
        }


        return
            evolutionController
                .CurrentEvolutionData;
    }


    private WeaponEvolutionData
        GetThunderPiercerEvolutionData()
    {
        WeaponEvolutionData data =
            GetCurrentEvolutionData();


        if (data == null
            || data.EvolutionType
            != WeaponEvolutionType.ThunderPiercer)
        {
            return null;
        }


        return data;
    }


    private WeaponEvolutionData
        GetClusterBurstEvolutionData()
    {
        WeaponEvolutionData data =
            GetCurrentEvolutionData();


        if (data == null
            || data.EvolutionType
            != WeaponEvolutionType.ClusterBurst)
        {
            return null;
        }


        return data;
    }


    private void RefreshEvolutionVisual(
        bool force)
    {
        WeaponEvolutionData data =
            GetCurrentEvolutionData();


        WeaponEvolutionType type =
            data != null
                ? data.EvolutionType
                : WeaponEvolutionType.None;


        if (!force
            && type == appliedVisualEvolution
            && data == appliedVisualEvolutionData)
        {
            return;
        }


        appliedVisualEvolution =
            type;

        appliedVisualEvolutionData =
            data;


        if (upgradeVisualController != null)
        {
            if (data != null)
            {
                upgradeVisualController
                    .ApplyEvolutionVisual(
                        data
                    );
            }
            else
            {
                upgradeVisualController
                    .ResetEvolutionVisual();
            }
        }


        if (muzzlePoint != null)
        {
            Vector3 targetPosition =
                baseMuzzlePointLocalPosition;


            if (data != null)
            {
                Vector2 offset =
                    data.MuzzlePointLocalOffset;


                targetPosition.x +=
                    offset.x;

                targetPosition.y +=
                    offset.y;
            }


            muzzlePoint.localPosition =
                targetPosition;
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


        scale.y =
            Mathf.Abs(
                baseLocalScale.y
            )
            * (isAimingLeft
                ? -1f
                : 1f);


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


        if (weaponData == null
            || bulletPool == null
            || muzzlePoint == null)
        {
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
            + GetEffectiveFireCooldown();


        PlayShotFeedback();


        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayShoot();
        }


        return true;
    }


    private bool FireProjectiles()
    {
        float startAngle =
            -currentProjectileSpreadAngle
            * (currentProjectileCount - 1)
            * 0.5f;


        Vector2 spawnPosition =
            muzzlePoint.position;


        ProjectileModifierSnapshot
            modifierSnapshot =
                BuildProjectileModifierSnapshot();


        WeaponEvolutionData
            projectileEvolutionData =
                GetCurrentEvolutionData();


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
                    modifierSnapshot,
                    projectileEvolutionData
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
        ProjectileModifierSnapshot modifierSnapshot,
        WeaponEvolutionData evolutionData)
    {
        Bullet bullet =
            bulletPool.GetBullet(
                spawnPosition,
                Quaternion.identity
            );


        if (bullet == null)
        {
            return false;
        }


        bullet.Initialize(
            projectileDirection,
            GetEffectiveProjectileSpeed(),
            currentDamage,
            GetEffectiveProjectileScale(),
            currentProjectileLifeTime,
            modifierSnapshot,
            evolutionData
        );


        return true;
    }


    // =========================================================
    // Effective Runtime Stats
    // =========================================================

    private float GetEffectiveProjectileSpeed()
    {
        float value =
            currentProjectileSpeed;


        WeaponEvolutionData thunderData =
            GetThunderPiercerEvolutionData();


        if (thunderData != null)
        {
            value *=
                thunderData
                    .ProjectileSpeedMultiplier;
        }


        return Mathf.Max(
            0.01f,
            value
        );
    }


    private float GetEffectiveProjectileScale()
    {
        float value =
            currentProjectileScale;


        WeaponEvolutionData clusterData =
            GetClusterBurstEvolutionData();


        if (clusterData != null)
        {
            value *=
                clusterData
                    .ProjectileScaleMultiplier;
        }


        return Mathf.Max(
            0.01f,
            value
        );
    }


    private float GetEffectiveFireCooldown()
    {
        float value =
            currentFireCooldown;


        WeaponEvolutionData clusterData =
            GetClusterBurstEvolutionData();


        if (clusterData != null)
        {
            value *=
                clusterData
                    .FireCooldownMultiplier;
        }


        return Mathf.Max(
            0.01f,
            value
        );
    }


    // =========================================================
    // Snapshot
    // =========================================================

    private ProjectileModifierSnapshot
        BuildProjectileModifierSnapshot()
    {
        if (weaponModifiers == null)
        {
            return
                ProjectileModifierSnapshot.Default;
        }


        int finalPierceCount =
            weaponModifiers.PierceCount;


        bool finalExplosive =
            weaponModifiers.HasExplosive;

        float finalExplosionRadius =
            weaponModifiers.ExplosionRadius;

        float finalExplosionDamageMultiplier =
            weaponModifiers
                .ExplosionDamageMultiplier;


        bool finalChainLightning =
            weaponModifiers.HasChainLightning;

        int finalChainCount =
            weaponModifiers.ChainCount;

        float finalChainRange =
            weaponModifiers.ChainRange;

        float finalChainDamageMultiplier =
            weaponModifiers
                .ChainDamageMultiplier;

        int finalMaxChainTriggerCount =
            1;


        bool finalSplitShot =
            weaponModifiers.HasSplitShot;

        int finalSplitCount =
            weaponModifiers.SplitCount;

        float finalChildDamageMultiplier =
            weaponModifiers.ChildDamageMultiplier;

        float finalChildSpeedMultiplier =
            weaponModifiers.ChildSpeedMultiplier;

        float finalChildScaleMultiplier =
            weaponModifiers.ChildScaleMultiplier;

        float finalChildLifeTimeMultiplier =
            weaponModifiers.ChildLifeTimeMultiplier;


        float finalChildExplosionRadiusMultiplier =
            0.75f;

        float finalChildExplosionDamageMultiplier =
            0.75f;


        WeaponEvolutionData thunderData =
            GetThunderPiercerEvolutionData();


        if (thunderData != null)
        {
            finalPierceCount +=
                thunderData.ExtraPierceCount;


            finalChainRange *=
                thunderData.ChainRangeMultiplier;


            finalMaxChainTriggerCount =
                Mathf.Max(
                    1,
                    thunderData
                        .MaxChainTriggersPerBullet
                );
        }


        WeaponEvolutionData clusterData =
            GetClusterBurstEvolutionData();


        if (clusterData != null)
        {
            finalExplosionRadius *=
                clusterData
                    .ExplosionRadiusMultiplier;


            finalExplosionDamageMultiplier *=
                clusterData
                    .ExplosionDamageMultiplier;


            finalSplitCount +=
                clusterData
                    .SplitCountBonus;


            finalChildExplosionRadiusMultiplier =
                clusterData
                    .ChildExplosionRadiusMultiplier;


            finalChildExplosionDamageMultiplier =
                clusterData
                    .ChildExplosionDamageMultiplier;
        }


        return
            new ProjectileModifierSnapshot(
                finalPierceCount,

                finalExplosive,
                finalExplosionRadius,
                finalExplosionDamageMultiplier,

                finalChainLightning,
                finalChainCount,
                finalChainRange,
                finalChainDamageMultiplier,
                finalMaxChainTriggerCount,

                finalSplitShot,
                finalSplitCount,
                finalChildDamageMultiplier,
                finalChildSpeedMultiplier,
                finalChildScaleMultiplier,
                finalChildLifeTimeMultiplier,

                finalChildExplosionRadiusMultiplier,
                finalChildExplosionDamageMultiplier,

                0
            );
    }


    // =========================================================
    // Direction
    // =========================================================

    private Vector2 RotateDirection(
        Vector2 direction,
        float angleDegrees)
    {
        float radians =
            angleDegrees
            * Mathf.Deg2Rad;


        float cosine =
            Mathf.Cos(radians);

        float sine =
            Mathf.Sin(radians);


        Vector2 result =
            new Vector2(
                direction.x * cosine
                - direction.y * sine,

                direction.x * sine
                + direction.y * cosine
            );


        return result.normalized;
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


        Vector3 recoilStart =
            baseLocalPosition
            + Vector3.left
            * recoilDistance;


        transform.localPosition =
            Vector3.Lerp(
                recoilStart,
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
        }
    }


    // =========================================================
    // Weapon Growth
    // =========================================================

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
    // Runtime Upgrades
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
    }


    // =========================================================
    // Operational
    // =========================================================

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

    [ContextMenu(
        "Debug/Increase Weapon Level")]
    private void DebugIncreaseWeaponLevel()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        IncreaseWeaponLevel();

        PrintRuntimeWeaponStats();
    }


    [ContextMenu(
        "Debug/Print Runtime Weapon Stats")]
    private void PrintRuntimeWeaponStats()
    {
        WeaponEvolutionData data =
            GetCurrentEvolutionData();


        Debug.Log(
            "===== Runtime Weapon Stats =====\n"
            + "Weapon: "
            + (weaponData != null
                ? weaponData.DisplayName
                : "None")

            + "\nWeapon Level: "
            + weaponLevel

            + "\nEvolution: "
            + (data != null
                ? data.EvolutionType.ToString()
                : "None")

            + "\nDamage: "
            + currentDamage

            + "\nBase Fire Cooldown: "
            + currentFireCooldown

            + "\nEffective Fire Cooldown: "
            + GetEffectiveFireCooldown()

            + "\nBase Projectile Speed: "
            + currentProjectileSpeed

            + "\nEffective Projectile Speed: "
            + GetEffectiveProjectileSpeed()

            + "\nBase Projectile Scale: "
            + currentProjectileScale

            + "\nEffective Projectile Scale: "
            + GetEffectiveProjectileScale()

            + "\nProjectile Count: "
            + currentProjectileCount,
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


        if (weaponHolder == null
            && transform.parent != null)
        {
            weaponHolder =
                transform.parent;
        }
    }
}