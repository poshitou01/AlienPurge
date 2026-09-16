using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField]
    private float speed = 12f;

    [SerializeField]
    private float lifeTime = 2f;


    [Header("Damage Settings")]
    [SerializeField]
    private int damage = 1;


    [Header("Scale Settings")]
    [SerializeField]
    private float scaleMultiplier = 1f;


    [Header("Modifier Snapshot Debug")]
    [SerializeField]
    private ProjectileModifierSnapshot
        modifierSnapshot;


    [Header("Piercing Runtime Debug")]
    [SerializeField]
    private int remainingPierceCount;

    [SerializeField]
    private int directHitEnemyCount;


    [Header("Explosion Runtime Debug")]
    [SerializeField]
    private int lastExplosionTargetCount;

    [SerializeField]
    private int lastExplosionDamage;


    [Header("Chain Lightning Runtime Debug")]
    [SerializeField]
    private int chainTriggerCount;

    [SerializeField]
    private int lastChainHitCount;

    [SerializeField]
    private int lastChainDamage;


    [Header("Split Shot Runtime Debug")]
    [SerializeField]
    private int lastSplitSpawnCount;

    [SerializeField]
    private int totalSplitSpawnCount;


    [Header("Runtime Debug")]
    [SerializeField]
    private float elapsedLifeTime;

    [SerializeField]
    private bool isReturned;

    [SerializeField]
    private bool hasPool;


    [Header("Visual Runtime Debug")]
    [SerializeField]
    private WeaponEvolutionData
        currentEvolutionVisualData;


    private Rigidbody2D rb;

    private Collider2D bulletCollider;

    private SpriteRenderer projectileRenderer;

    private TrailRenderer trailRenderer;


    private BulletPool ownerPool;


    private Vector2 moveDirection =
        Vector2.right;

    private Vector3 originalScale;

    private Sprite starterProjectileSprite;


    private readonly HashSet<EnemyHealth>
        hitEnemies =
            new HashSet<EnemyHealth>();

    private readonly HashSet<EnemyHealth>
        explosionHitEnemies =
            new HashSet<EnemyHealth>();

    private readonly HashSet<EnemyHealth>
        chainVisitedEnemies =
            new HashSet<EnemyHealth>();


    public ProjectileModifierSnapshot
        ModifierSnapshot =>
            modifierSnapshot;

    public int RemainingPierceCount =>
        remainingPierceCount;

    public int DirectHitEnemyCount =>
        directHitEnemyCount;

    public bool HasTriggeredChain =>
        chainTriggerCount > 0;

    public int ChainTriggerCount =>
        chainTriggerCount;

    public int LastChainHitCount =>
        lastChainHitCount;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody2D>();

        bulletCollider =
            GetComponent<Collider2D>();

        projectileRenderer =
            GetComponent<SpriteRenderer>();

        trailRenderer =
            GetComponent<TrailRenderer>();


        originalScale =
            transform.localScale;


        if (projectileRenderer != null)
        {
            starterProjectileSprite =
                projectileRenderer.sprite;
        }


        ResetProjectileVisual();
    }


    private void OnEnable()
    {
        ResetRuntimeState();
    }


    private void OnDisable()
    {
        StopRigidbodyMovement();

        ClearTrail();
    }


    // =========================================================
    // Pool
    // =========================================================

    public void SetPool(
        BulletPool pool)
    {
        ownerPool =
            pool;

        hasPool =
            ownerPool != null;
    }


    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(
        Vector2 direction)
    {
        ResetRuntimeState();

        SetMoveDirection(
            direction
        );
    }


    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier)
    {
        Initialize(
            direction,
            newSpeed,
            newDamage,
            newScaleMultiplier,
            lifeTime,
            ProjectileModifierSnapshot.Default,
            null
        );
    }


    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier,
        float newLifeTime)
    {
        Initialize(
            direction,
            newSpeed,
            newDamage,
            newScaleMultiplier,
            newLifeTime,
            ProjectileModifierSnapshot.Default,
            null
        );
    }


    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier,
        float newLifeTime,
        ProjectileModifierSnapshot
            newModifierSnapshot)
    {
        Initialize(
            direction,
            newSpeed,
            newDamage,
            newScaleMultiplier,
            newLifeTime,
            newModifierSnapshot,
            null
        );
    }


    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier,
        float newLifeTime,
        ProjectileModifierSnapshot newModifierSnapshot,
        WeaponEvolutionData newEvolutionVisualData)
    {
        ResetRuntimeState();


        SetMoveDirection(
            direction
        );

        SetSpeed(
            newSpeed
        );

        SetDamage(
            newDamage
        );

        SetScaleMultiplier(
            newScaleMultiplier
        );

        SetLifeTime(
            newLifeTime
        );


        modifierSnapshot =
            newModifierSnapshot;


        currentEvolutionVisualData =
            newEvolutionVisualData;


        ApplyProjectileVisual(
            currentEvolutionVisualData
        );


        remainingPierceCount =
            Mathf.Max(
                0,
                modifierSnapshot.PierceCount
            );
    }


    private void ResetRuntimeState()
    {
        elapsedLifeTime = 0f;

        isReturned = false;


        modifierSnapshot =
            ProjectileModifierSnapshot.Default;


        remainingPierceCount = 0;

        directHitEnemyCount = 0;


        lastExplosionTargetCount = 0;

        lastExplosionDamage = 0;


        chainTriggerCount = 0;

        lastChainHitCount = 0;

        lastChainDamage = 0;


        lastSplitSpawnCount = 0;

        totalSplitSpawnCount = 0;


        currentEvolutionVisualData =
            null;


        hitEnemies.Clear();

        explosionHitEnemies.Clear();

        chainVisitedEnemies.Clear();


        ResetProjectileVisual();

        StopRigidbodyMovement();


        if (bulletCollider != null)
        {
            bulletCollider.enabled =
                true;
        }
    }


    // =========================================================
    // Projectile Visual
    // =========================================================

    private void ApplyProjectileVisual(
        WeaponEvolutionData evolutionData)
    {
        if (projectileRenderer != null)
        {
            if (evolutionData != null
                && evolutionData
                        .ProjectileSprite != null)
            {
                projectileRenderer.sprite =
                    evolutionData
                        .ProjectileSprite;
            }
            else
            {
                projectileRenderer.sprite =
                    starterProjectileSprite;
            }
        }


        if (trailRenderer == null)
        {
            return;
        }


        ClearTrail();


        bool useTrail =
            evolutionData != null
            && evolutionData
                .UseProjectileTrail;


        trailRenderer.enabled =
            useTrail;

        trailRenderer.emitting =
            useTrail;


        if (!useTrail)
        {
            return;
        }


        trailRenderer.time =
            evolutionData
                .ProjectileTrailTime;

        trailRenderer.startWidth =
            evolutionData
                .ProjectileTrailStartWidth;

        trailRenderer.endWidth =
            evolutionData
                .ProjectileTrailEndWidth;

        trailRenderer.startColor =
            evolutionData
                .ProjectileTrailStartColor;

        trailRenderer.endColor =
            evolutionData
                .ProjectileTrailEndColor;


        trailRenderer.Clear();
    }


    private void ResetProjectileVisual()
    {
        if (projectileRenderer != null)
        {
            projectileRenderer.sprite =
                starterProjectileSprite;
        }


        if (trailRenderer != null)
        {
            trailRenderer.emitting =
                false;

            trailRenderer.Clear();

            trailRenderer.enabled =
                false;
        }
    }


    private void ClearTrail()
    {
        if (trailRenderer == null)
        {
            return;
        }


        trailRenderer.emitting =
            false;

        trailRenderer.Clear();
    }


    // =========================================================
    // Ignore Direct Hit
    // =========================================================

    public void IgnoreEnemyForDirectHit(
        EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return;
        }


        hitEnemies.Add(
            enemyHealth
        );
    }


    // =========================================================
    // Movement
    // =========================================================

    private void Update()
    {
        if (isReturned)
        {
            return;
        }


        elapsedLifeTime +=
            Time.deltaTime;


        if (elapsedLifeTime >= lifeTime)
        {
            ReturnToPool();
        }
    }


    private void FixedUpdate()
    {
        if (isReturned
            || rb == null)
        {
            return;
        }


        Vector2 nextPosition =
            rb.position
            + moveDirection
            * speed
            * Time.fixedDeltaTime;


        rb.MovePosition(
            nextPosition
        );
    }


    private void SetMoveDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude
            <= 0.0001f)
        {
            direction =
                Vector2.right;
        }


        moveDirection =
            direction.normalized;


        float angle =
            Mathf.Atan2(
                moveDirection.y,
                moveDirection.x
            )
            * Mathf.Rad2Deg;


        transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );
    }


    public void SetSpeed(
        float newSpeed)
    {
        speed =
            Mathf.Max(
                0.01f,
                newSpeed
            );
    }


    public void SetDamage(
        int newDamage)
    {
        damage =
            Mathf.Max(
                1,
                newDamage
            );
    }


    public void SetLifeTime(
        float newLifeTime)
    {
        lifeTime =
            Mathf.Max(
                0.01f,
                newLifeTime
            );
    }


    public void SetScaleMultiplier(
        float newScaleMultiplier)
    {
        scaleMultiplier =
            Mathf.Max(
                0.01f,
                newScaleMultiplier
            );


        transform.localScale =
            originalScale
            * scaleMultiplier;
    }


    // =========================================================
    // Direct Hit
    // =========================================================

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (isReturned)
        {
            return;
        }


        if (!other.CompareTag("Enemy"))
        {
            return;
        }


        EnemyHealth enemyHealth =
            other.GetComponentInParent<
                EnemyHealth>();


        if (enemyHealth == null)
        {
            ReturnToPool();

            return;
        }


        if (enemyHealth.IsDead)
        {
            return;
        }


        if (!hitEnemies.Add(
                enemyHealth))
        {
            return;
        }


        directHitEnemyCount =
            hitEnemies.Count;


        enemyHealth.TakeDamage(
            damage
        );


        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayEnemyHit();
        }


        SpawnHitEffect();


        TriggerExplosion();

        TriggerChainLightning(
            enemyHealth
        );

        TriggerSplitShot(
            enemyHealth
        );


        if (remainingPierceCount > 0)
        {
            remainingPierceCount--;

            return;
        }


        ReturnToPool();
    }


    // =========================================================
    // Explosion
    // =========================================================

    private void TriggerExplosion()
    {
        if (!modifierSnapshot.Explosive)
        {
            return;
        }


        float radius =
            modifierSnapshot
                .ExplosionRadius;

        float damageMultiplier =
            modifierSnapshot
                .ExplosionDamageMultiplier;


        if (radius <= 0f
            || damageMultiplier <= 0f)
        {
            return;
        }


        int explosionDamage =
            CalculateSecondaryDamage(
                damageMultiplier
            );


        lastExplosionDamage =
            explosionDamage;


        explosionHitEnemies.Clear();


        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                radius
            );


        foreach (Collider2D targetCollider
                 in colliders)
        {
            if (targetCollider == null)
            {
                continue;
            }


            EnemyHealth target =
                targetCollider
                    .GetComponentInParent<
                        EnemyHealth>();


            if (target == null
                || target.IsDead)
            {
                continue;
            }


            if (!explosionHitEnemies.Add(
                    target))
            {
                continue;
            }


            target.TakeDamage(
                explosionDamage
            );
        }


        lastExplosionTargetCount =
            explosionHitEnemies.Count;


        SpawnExplosionEffect(
            transform.position,
            radius
        );
    }


    // =========================================================
    // Chain
    // =========================================================

    private void TriggerChainLightning(
        EnemyHealth directHitTarget)
    {
        if (!modifierSnapshot
                .ChainLightning)
        {
            return;
        }


        int maxTriggerCount =
            Mathf.Max(
                1,
                modifierSnapshot
                    .MaxChainTriggerCount
            );


        if (chainTriggerCount
            >= maxTriggerCount)
        {
            return;
        }


        int maximumChainCount =
            modifierSnapshot
                .ChainCount;

        float chainRange =
            modifierSnapshot
                .ChainRange;

        float damageMultiplier =
            modifierSnapshot
                .ChainDamageMultiplier;


        if (maximumChainCount <= 0
            || chainRange <= 0f
            || damageMultiplier <= 0f)
        {
            return;
        }


        chainTriggerCount++;


        lastChainHitCount = 0;

        lastChainDamage =
            CalculateSecondaryDamage(
                damageMultiplier
            );


        chainVisitedEnemies.Clear();


        if (directHitTarget != null)
        {
            chainVisitedEnemies.Add(
                directHitTarget
            );
        }


        Vector2 currentOrigin =
            directHitTarget != null
                ? (Vector2)
                    directHitTarget
                        .transform.position
                : (Vector2)
                    transform.position;


        for (int jumpIndex = 0;
             jumpIndex < maximumChainCount;
             jumpIndex++)
        {
            EnemyHealth nextTarget =
                FindNearestChainTarget(
                    currentOrigin,
                    chainRange
                );


            if (nextTarget == null)
            {
                break;
            }


            Vector2 nextPosition =
                nextTarget
                    .transform.position;


            chainVisitedEnemies.Add(
                nextTarget
            );


            nextTarget.TakeDamage(
                lastChainDamage
            );


            SpawnChainLightningEffect(
                currentOrigin,
                nextPosition
            );


            lastChainHitCount++;

            currentOrigin =
                nextPosition;
        }
    }


    private EnemyHealth
        FindNearestChainTarget(
            Vector2 origin,
            float chainRange)
    {
        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                origin,
                chainRange
            );


        EnemyHealth nearestTarget =
            null;

        float nearestDistance =
            float.MaxValue;

        float maxDistance =
            chainRange
            * chainRange;


        foreach (Collider2D targetCollider
                 in colliders)
        {
            if (targetCollider == null)
            {
                continue;
            }


            EnemyHealth candidate =
                targetCollider
                    .GetComponentInParent<
                        EnemyHealth>();


            if (candidate == null
                || candidate.IsDead
                || chainVisitedEnemies
                    .Contains(candidate))
            {
                continue;
            }


            Vector2 candidatePosition =
                candidate
                    .transform.position;


            float sqrDistance =
                (candidatePosition - origin)
                    .sqrMagnitude;


            if (sqrDistance > maxDistance
                || sqrDistance >= nearestDistance)
            {
                continue;
            }


            nearestDistance =
                sqrDistance;

            nearestTarget =
                candidate;
        }


        return nearestTarget;
    }


    // =========================================================
    // Split
    // =========================================================

    private void TriggerSplitShot(
        EnemyHealth directHitTarget)
    {
        lastSplitSpawnCount =
            0;


        if (!modifierSnapshot.SplitShot)
        {
            return;
        }


        if (modifierSnapshot.Generation
            >= 1)
        {
            return;
        }


        int splitCount =
            modifierSnapshot.SplitCount;


        if (splitCount <= 0
            || ownerPool == null)
        {
            return;
        }


        int childDamage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    damage
                    * modifierSnapshot
                        .ChildDamageMultiplier
                )
            );


        float childSpeed =
            Mathf.Max(
                0.01f,
                speed
                * modifierSnapshot
                    .ChildSpeedMultiplier
            );


        float childScale =
            Mathf.Max(
                0.01f,
                scaleMultiplier
                * modifierSnapshot
                    .ChildScaleMultiplier
            );


        float childLifeTime =
            Mathf.Max(
                0.01f,
                lifeTime
                * modifierSnapshot
                    .ChildLifeTimeMultiplier
            );


        ProjectileModifierSnapshot
            childSnapshot =
                CreateChildModifierSnapshot();


        for (int i = 0;
             i < splitCount;
             i++)
        {
            float angleOffset =
                GetSplitAngleOffset(
                    splitCount,
                    i
                );


            Vector2 childDirection =
                RotateDirection(
                    moveDirection,
                    angleOffset
                );


            Bullet childBullet =
                ownerPool.GetBullet(
                    transform.position,
                    Quaternion.identity
                );


            if (childBullet == null)
            {
                continue;
            }


            childBullet.Initialize(
                childDirection,
                childSpeed,
                childDamage,
                childScale,
                childLifeTime,
                childSnapshot,
                currentEvolutionVisualData
            );


            childBullet.IgnoreEnemyForDirectHit(
                directHitTarget
            );


            lastSplitSpawnCount++;

            totalSplitSpawnCount++;
        }
    }


    private ProjectileModifierSnapshot
        CreateChildModifierSnapshot()
    {
        int childPierceCount =
            Mathf.Max(
                0,
                modifierSnapshot.PierceCount
                - 1
            );


        float childExplosionRadius =
            modifierSnapshot
                .ExplosionRadius
            * modifierSnapshot
                .ChildExplosionRadiusMultiplier;


        float childExplosionDamage =
            modifierSnapshot
                .ExplosionDamageMultiplier
            * modifierSnapshot
                .ChildExplosionDamageMultiplier;


        return
            new ProjectileModifierSnapshot(
                childPierceCount,

                modifierSnapshot.Explosive,
                childExplosionRadius,
                childExplosionDamage,

                modifierSnapshot.ChainLightning,
                modifierSnapshot.ChainCount,
                modifierSnapshot.ChainRange,
                modifierSnapshot
                    .ChainDamageMultiplier,
                modifierSnapshot
                    .MaxChainTriggerCount,

                false,
                0,
                0f,
                0f,
                0f,
                0f,

                modifierSnapshot
                    .ChildExplosionRadiusMultiplier,
                modifierSnapshot
                    .ChildExplosionDamageMultiplier,

                modifierSnapshot.Generation
                + 1
            );
    }


    private float GetSplitAngleOffset(
        int splitCount,
        int childIndex)
    {
        switch (splitCount)
        {
            case 2:
                return childIndex == 0
                    ? -15f
                    : 15f;

            case 3:
                return
                    (childIndex - 1)
                    * 20f;

            case 4:
                return
                    -30f
                    + childIndex
                    * 20f;

            case 5:
                return
                    -40f
                    + childIndex
                    * 20f;

            default:
                return 0f;
        }
    }


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


        return
            result.sqrMagnitude
            <= 0.0001f
                ? Vector2.right
                : result.normalized;
    }


    // =========================================================
    // Shared Damage
    // =========================================================

    private int CalculateSecondaryDamage(
        float multiplier)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                damage
                * multiplier
            )
        );
    }


    // =========================================================
    // Return
    // =========================================================

    public void ReturnToPool()
    {
        if (isReturned)
        {
            return;
        }


        isReturned =
            true;


        ClearTrail();

        StopRigidbodyMovement();


        if (bulletCollider != null)
        {
            bulletCollider.enabled =
                false;
        }


        if (ownerPool != null)
        {
            ownerPool.ReturnBullet(
                this
            );
        }
        else
        {
            Destroy(
                gameObject
            );
        }
    }


    private void StopRigidbodyMovement()
    {
        if (rb == null)
        {
            return;
        }


        rb.velocity =
            Vector2.zero;

        rb.angularVelocity =
            0f;
    }


    // =========================================================
    // Effects
    // =========================================================

    private void SpawnChainLightningEffect(
        Vector2 startPosition,
        Vector2 endPosition)
    {
        if (ChainLightningEffectPool.Instance
            == null)
        {
            return;
        }


        ChainLightningEffectPool.Instance
            .GetEffect(
                startPosition,
                endPosition
            );
    }


    private void SpawnExplosionEffect(
        Vector3 position,
        float radius)
    {
        if (ExplosionEffectPool.Instance
            == null)
        {
            return;
        }


        ExplosionEffectPool.Instance
            .GetEffect(
                position,
                radius
            );
    }


    private void SpawnHitEffect()
    {
        if (HitEffectPool.Instance == null)
        {
            return;
        }


        HitEffect effect =
            HitEffectPool.Instance
                .GetHitEffect(
                    transform.position,
                    Quaternion.identity
                );


        if (effect != null)
        {
            effect.Initialize();
        }
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Print Modifier Snapshot")]
    private void PrintModifierSnapshot()
    {
        Debug.Log(
            modifierSnapshot
                .GetDebugText(),
            this
        );
    }


    [ContextMenu(
        "Debug/Print Projectile Visual State")]
    private void PrintProjectileVisualState()
    {
        Debug.Log(
            "===== Projectile Visual State =====\n"
            + "Evolution: "
            + (currentEvolutionVisualData != null
                ? currentEvolutionVisualData
                    .EvolutionType.ToString()
                : "None")

            + "\nProjectile Sprite: "
            + (projectileRenderer != null
               && projectileRenderer.sprite != null
                ? projectileRenderer.sprite.name
                : "None")

            + "\nTrail Enabled: "
            + (trailRenderer != null
               && trailRenderer.enabled),
            this
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        speed =
            Mathf.Max(
                0.01f,
                speed
            );

        lifeTime =
            Mathf.Max(
                0.01f,
                lifeTime
            );

        damage =
            Mathf.Max(
                1,
                damage
            );

        scaleMultiplier =
            Mathf.Max(
                0.01f,
                scaleMultiplier
            );
    }
}