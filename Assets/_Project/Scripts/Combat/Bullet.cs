using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 2f;

    [Header("Damage Settings")]
    [SerializeField] private int damage = 1;

    [Header("Scale Settings")]
    [Tooltip("当前子弹使用的尺寸倍率，仅用于运行时调试")]
    [SerializeField] private float scaleMultiplier = 1f;

    [Header("Modifier Snapshot Debug")]
    [Tooltip("当前 Bullet 出生时保存的机制快照")]
    [SerializeField]
    private ProjectileModifierSnapshot modifierSnapshot;

    [Header("Piercing Runtime Debug")]
    [Tooltip("当前还剩多少次额外穿透机会")]
    [SerializeField] private int remainingPierceCount;

    [Tooltip("当前生命周期中已经直接命中的不同敌人数量")]
    [SerializeField] private int directHitEnemyCount;

    [Header("Explosion Runtime Debug")]
    [Tooltip("最近一次爆炸实际伤害到的不同敌人数量")]
    [SerializeField] private int lastExplosionTargetCount;

    [Tooltip("最近一次爆炸造成的单目标 Secondary Damage")]
    [SerializeField] private int lastExplosionDamage;

    [Header("Chain Lightning Runtime Debug")]
    [Tooltip("当前 Bullet 是否已经启动过一次完整连锁")]
    [SerializeField] private bool hasTriggeredChain;

    [Header("Split Shot Runtime Debug")]
    [Tooltip("最近一次 Direct Hit 实际生成的 Split Child 数量")]
    [SerializeField] private int lastSplitSpawnCount;

    [Tooltip("当前 Bullet 生命周期累计生成的 Split Child 数量")]
    [SerializeField] private int totalSplitSpawnCount;

    [Tooltip("最近一次 Chain 实际完成的跳跃次数")]
    [SerializeField] private int lastChainHitCount;

    [Tooltip("最近一次 Chain 每个目标受到的 Secondary Damage")]
    [SerializeField] private int lastChainDamage;

    [Header("Runtime Debug")]
    [Tooltip("当前生命周期已经经过的时间")]
    [SerializeField] private float elapsedLifeTime;

    [Tooltip("当前 Bullet 是否已经被回收")]
    [SerializeField] private bool isReturned;

    [Tooltip("当前 Bullet 是否由 BulletPool 管理")]
    [SerializeField] private bool hasPool;

    private Rigidbody2D rb;
    private Collider2D bulletCollider;

    private BulletPool ownerPool;

    private Vector2 moveDirection = Vector2.right;

    private Vector3 originalScale;

    // =========================================================
    // Runtime Collections
    // =========================================================

    // 当前 Bullet 已经直接命中过的 Enemy。
    // 防止 Piercing 对同一个 Enemy 重复 Direct Hit。
    private readonly HashSet<EnemyHealth> hitEnemies =
        new HashSet<EnemyHealth>();

    // 每次 Explosion 查询时使用。
    // 防止同一个 Enemy 因多个 Collider
    // 在一次爆炸中受到多次伤害。
    private readonly HashSet<EnemyHealth> explosionHitEnemies =
        new HashSet<EnemyHealth>();

    // 当前这一次完整 Chain 已经访问过的 Enemy。
    //
    // Direct Hit Target 也会首先加入这里，
    // 防止 Chain 又跳回最初目标。
    private readonly HashSet<EnemyHealth> chainVisitedEnemies =
        new HashSet<EnemyHealth>();


    // =========================================================
    // Read-Only Runtime Data
    // =========================================================

    public ProjectileModifierSnapshot ModifierSnapshot =>
        modifierSnapshot;

    public int RemainingPierceCount =>
        remainingPierceCount;

    public int DirectHitEnemyCount =>
        directHitEnemyCount;

    public bool HasTriggeredChain =>
        hasTriggeredChain;

    public int LastChainHitCount =>
        lastChainHitCount;

    /// <summary>
    /// 将一个 Enemy 标记为这颗 Bullet 已经处理过的 Direct Hit Target。
    ///
    /// 主要用于 Split Child：
    /// Child 出生在 Parent 的命中位置时，
    /// 不应该立即再次伤害刚刚被 Parent 命中的 Enemy。
    /// </summary>
    public void IgnoreEnemyForDirectHit(
        EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return;
        }

        hitEnemies.Add(enemyHealth);
    }

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        bulletCollider =
            GetComponent<Collider2D>();

        originalScale =
            transform.localScale;
    }


    private void OnEnable()
    {
        ResetRuntimeState();
    }


    private void OnDisable()
    {
        StopRigidbodyMovement();
    }


    // =========================================================
    // Pool
    // =========================================================

    public void SetPool(BulletPool pool)
    {
        ownerPool = pool;

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

        SetMoveDirection(direction);
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
            ProjectileModifierSnapshot.Default
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
            ProjectileModifierSnapshot.Default
        );
    }


    /// <summary>
    /// 第三十二阶段完整初始化入口。
    /// Bullet 在出生时保存玩家当时的机制 Snapshot。
    /// </summary>
    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier,
        float newLifeTime,
        ProjectileModifierSnapshot newModifierSnapshot)
    {
        ResetRuntimeState();

        SetMoveDirection(direction);
        SetSpeed(newSpeed);
        SetDamage(newDamage);
        SetScaleMultiplier(newScaleMultiplier);
        SetLifeTime(newLifeTime);

        modifierSnapshot =
            newModifierSnapshot;

        remainingPierceCount =
            Mathf.Max(
                0,
                modifierSnapshot.PierceCount
            );
    }


    /// <summary>
    /// 对象池中的 Bullet 每次重新使用时，
    /// 清空上一轮留下的全部 Runtime State。
    /// </summary>
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

        hasTriggeredChain = false;
        lastChainHitCount = 0;
        lastChainDamage = 0;

        lastSplitSpawnCount = 0;
        totalSplitSpawnCount = 0;

        hitEnemies.Clear();
        explosionHitEnemies.Clear();
        chainVisitedEnemies.Clear();

        StopRigidbodyMovement();

        if (bulletCollider != null)
        {
            bulletCollider.enabled = true;
        }
    }


    // =========================================================
    // Lifetime / Movement
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
        if (isReturned || rb == null)
        {
            return;
        }

        Vector2 nextPosition =
            rb.position
            + moveDirection
            * speed
            * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);
    }


    private void SetMoveDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
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


    public void SetSpeed(float newSpeed)
    {
        speed =
            Mathf.Max(
                0.01f,
                newSpeed
            );
    }


    public void SetDamage(int newDamage)
    {
        damage =
            Mathf.Max(
                1,
                newDamage
            );
    }


    public void SetLifeTime(float newLifeTime)
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
            other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
        {
            Debug.LogWarning(
                other.gameObject.name
                + " has Enemy Tag, but no EnemyHealth "
                + "component was found.",
                other
            );

            ReturnToPool();
            return;
        }

        if (enemyHealth.IsDead)
        {
            return;
        }

        // 同一颗 Bullet 对同一个 Enemy
        // 最多产生一次 Direct Hit。
        if (!hitEnemies.Add(enemyHealth))
        {
            return;
        }

        directHitEnemyCount =
            hitEnemies.Count;


        // =====================================================
        // Direct Hit Damage
        // =====================================================

        enemyHealth.TakeDamage(damage);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEnemyHit();
        }

        SpawnHitEffect();


        // =====================================================
        // Explosion - Secondary Damage
        // =====================================================

        TriggerExplosion();

        TriggerChainLightning(
            enemyHealth
        );

        // Parent Bullet 在 Direct Hit 后产生 Split Child。
        // Generation >= 1 的 Child 不允许继续裂变。
        TriggerSplitShot(
           enemyHealth
       );

        // =====================================================
        // Piercing
        // =====================================================

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

    /// <summary>
    /// 在当前 Direct Hit 位置产生一次范围 Secondary Damage。
    ///
    /// Explosion 只调用 EnemyHealth.TakeDamage，
    /// 不会递归触发其他 Projectile Mechanic。
    /// </summary>
    private void TriggerExplosion()
    {
        if (!modifierSnapshot.Explosive)
        {
            return;
        }

        float explosionRadius =
            modifierSnapshot.ExplosionRadius;

        float explosionDamageMultiplier =
            modifierSnapshot.ExplosionDamageMultiplier;

        if (explosionRadius <= 0f ||
            explosionDamageMultiplier <= 0f)
        {
            return;
        }

        int explosionDamage =
            CalculateSecondaryDamage(
                explosionDamageMultiplier
            );

        lastExplosionDamage =
            explosionDamage;

        explosionHitEnemies.Clear();

        Collider2D[] overlappingColliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                explosionRadius
            );

        for (int i = 0;
             i < overlappingColliders.Length;
             i++)
        {
            Collider2D targetCollider =
                overlappingColliders[i];

            if (targetCollider == null)
            {
                continue;
            }

            EnemyHealth targetEnemyHealth =
                targetCollider
                    .GetComponentInParent<EnemyHealth>();

            if (targetEnemyHealth == null)
            {
                continue;
            }

            if (targetEnemyHealth.IsDead)
            {
                continue;
            }

            if (!explosionHitEnemies.Add(
                    targetEnemyHealth))
            {
                continue;
            }

            // Secondary Damage：
            // 不重新进入 Bullet Direct Hit 流程。
            targetEnemyHealth.TakeDamage(
                explosionDamage
            );
        }

        lastExplosionTargetCount =
            explosionHitEnemies.Count;

        SpawnExplosionEffect(
    transform.position,
    explosionRadius
);
    }


    // =========================================================
    // Chain Lightning
    // =========================================================

    /// <summary>
    /// 从本次 Direct Hit Target 开始，
    /// 根据 Snapshot 完成一整条 Chain。
    ///
    /// 同一颗 Bullet 的整个生命周期
    /// 最多调用成功一次完整 Chain。
    /// </summary>
    private void TriggerChainLightning(
        EnemyHealth directHitTarget)
    {
        if (!modifierSnapshot.ChainLightning)
        {
            return;
        }

        // Piercing + Chain 的关键保护：
        //
        // 第一处 Direct Hit 已经触发过 Chain 后，
        // 后续穿透命中不允许再次启动新的 Chain。
        if (hasTriggeredChain)
        {
            return;
        }

        int maximumChainCount =
            modifierSnapshot.ChainCount;

        float chainRange =
            modifierSnapshot.ChainRange;

        float chainDamageMultiplier =
            modifierSnapshot.ChainDamageMultiplier;

        if (maximumChainCount <= 0 ||
            chainRange <= 0f ||
            chainDamageMultiplier <= 0f)
        {
            return;
        }

        // 一旦第一次 Direct Hit 尝试启动 Chain，
        // 就认为本 Bullet 已经使用了 Chain。
        //
        // 即使附近暂时没有可跳跃目标，
        // 后续 Piercing Direct Hit 也不会再启动第二次 Chain。
        hasTriggeredChain = true;

        lastChainHitCount = 0;

        lastChainDamage =
            CalculateSecondaryDamage(
                chainDamageMultiplier
            );

        chainVisitedEnemies.Clear();

        // 最初的 Direct Hit Target
        // 视为已经访问过。
        //
        // 这样 Chain 不会：
        //
        // A → B → A
        if (directHitTarget != null)
        {
            chainVisitedEnemies.Add(
                directHitTarget
            );
        }

        Vector2 currentChainOrigin =
            directHitTarget != null
                ? (Vector2)directHitTarget.transform.position
                : (Vector2)transform.position;


        // =====================================================
        // Sequential Chain Jumps
        // =====================================================

        for (int jumpIndex = 0;
             jumpIndex < maximumChainCount;
             jumpIndex++)
        {
            EnemyHealth nextTarget =
                FindNearestChainTarget(
                    currentChainOrigin,
                    chainRange
                );

            // 当前节点附近已经没有合法目标，
            // 连锁提前结束。
            if (nextTarget == null)
            {
                break;
            }

            // 在伤害前保存位置。
            //
            // 因为伤害可能直接杀死目标，
            // 而 Enemy 随后可能进入死亡 / 回池流程。
            Vector2 nextTargetPosition =
                nextTarget.transform.position;

            chainVisitedEnemies.Add(
                nextTarget
            );

            // =================================================
            // Secondary Damage
            // =================================================
            //
            // Chain 伤害只进入 EnemyHealth.TakeDamage。
            //
            // 因此不会：
            // Chain → Chain
            // Chain → Explosion
            // Chain → Split
            // Chain → Pierce
            nextTarget.TakeDamage(
                lastChainDamage
            );

            // 显示当前这一跳的电弧。
            // 这里只负责视觉反馈，不参与伤害计算。
            SpawnChainLightningEffect(
                currentChainOrigin,
                nextTargetPosition
            );

            lastChainHitCount++;

            currentChainOrigin =
                nextTargetPosition;
        }
    }


    /// <summary>
    /// 搜索 origin 周围 chainRange 内
    /// 距离最近的合法 Enemy。
    ///
    /// 合法条件：
    /// 1. 有 EnemyHealth
    /// 2. 仍然存活
    /// 3. 本次 Chain 尚未访问
    /// 4. Enemy 中心距离没有超过 Chain Range
    /// </summary>
    private EnemyHealth FindNearestChainTarget(
        Vector2 origin,
        float chainRange)
    {
        Collider2D[] overlappingColliders =
            Physics2D.OverlapCircleAll(
                origin,
                chainRange
            );

        EnemyHealth nearestTarget =
            null;

        float nearestSqrDistance =
            float.MaxValue;

        float maximumSqrDistance =
            chainRange * chainRange;

        for (int i = 0;
             i < overlappingColliders.Length;
             i++)
        {
            Collider2D targetCollider =
                overlappingColliders[i];

            if (targetCollider == null)
            {
                continue;
            }

            EnemyHealth candidate =
                targetCollider
                    .GetComponentInParent<EnemyHealth>();

            if (candidate == null)
            {
                continue;
            }

            if (candidate.IsDead)
            {
                continue;
            }

            if (chainVisitedEnemies.Contains(
                    candidate))
            {
                continue;
            }

            Vector2 candidatePosition =
                candidate.transform.position;

            float sqrDistance =
                (candidatePosition - origin)
                    .sqrMagnitude;

            // OverlapCircleAll 检查的是 Collider。
            //
            // 这里再用 Enemy Transform 中心做一次距离限制，
            // 让 Chain Range 更符合我们设计中的数值含义。
            if (sqrDistance >
                maximumSqrDistance)
            {
                continue;
            }

            if (sqrDistance >=
                nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance =
                sqrDistance;

            nearestTarget =
                candidate;
        }

        return nearestTarget;
    }

    // =========================================================
    // Split Shot
    // =========================================================

    /// <summary>
    /// 当前 Parent Bullet 在 Direct Hit 后产生 Child Bullets。
    ///
    /// Split 只允许 Generation 0 的 Projectile 触发。
    ///
    /// Child：
    /// Damage   × ChildDamageMultiplier
    /// Speed    × ChildSpeedMultiplier
    /// Scale    × ChildScaleMultiplier
    /// LifeTime × ChildLifeTimeMultiplier
    ///
    /// Child 不再拥有 Split，
    /// 但会保留 Explosion 与 Chain Lightning。
    /// </summary>
    private void TriggerSplitShot(
        EnemyHealth directHitTarget)
    {
        lastSplitSpawnCount = 0;

        // 没有 Split 机制。
        if (!modifierSnapshot.SplitShot)
        {
            return;
        }

        // Generation >= 1 代表当前已经是 Child。
        // 禁止 Child 再产生下一代。
        if (modifierSnapshot.Generation >= 1)
        {
            return;
        }

        int splitCount =
            modifierSnapshot.SplitCount;

        if (splitCount <= 0)
        {
            return;
        }

        // Split Child 必须继续使用当前 Bullet 所属的
        // BulletPool，不能 Instantiate。
        if (ownerPool == null)
        {
            Debug.LogWarning(
                "Bullet: Split Shot cannot spawn children "
                + "because ownerPool is null.",
                this
            );

            return;
        }


        // =====================================================
        // Child Stats
        // =====================================================

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


        // =====================================================
        // Child Modifier Snapshot
        // =====================================================

        ProjectileModifierSnapshot
            childModifierSnapshot =
                CreateChildModifierSnapshot();


        // =====================================================
        // Spawn Children
        // =====================================================

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

            // 从 Parent 当前命中位置生成。
            Vector3 spawnPosition =
                transform.position;

            Bullet childBullet =
                ownerPool.GetBullet(
                    spawnPosition,
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
                childModifierSnapshot
            );

            // Split Child 出生时通常仍位于
            // Parent 刚刚命中的 Enemy Collider 内。
            //
            // 因此提前将该 Enemy 加入 Child 的 Direct Hit 去重集合，
            // 防止 Child 出生后立刻再次伤害同一个 Enemy。
            childBullet.IgnoreEnemyForDirectHit(
                directHitTarget
            );

            lastSplitSpawnCount++;
            totalSplitSpawnCount++;
        }
    }


    /// <summary>
    /// 为 Split Child 创建新的 Projectile Snapshot。
    ///
    /// Child：
    ///
    /// Pierce = Parent Pierce - 1
    /// Explosion = 保留
    /// Chain = 保留
    /// Split = 禁用
    /// Generation = Parent Generation + 1
    /// </summary>
    private ProjectileModifierSnapshot
        CreateChildModifierSnapshot()
    {
        int childPierceCount =
            Mathf.Max(
                0,
                modifierSnapshot.PierceCount - 1
            );


        // =====================================================
        // Explosion + Split Synergy
        // =====================================================
        //
        // Split Child 会继承 Explosion，
        // 但为了防止多枚 Child 带来的 AoE 伤害过度膨胀：
        //
        // Radius × 0.75
        // Damage Multiplier × 0.75
        //
        // 如果 Parent 本身没有 Explosion，
        // 这些值本来就是 0，因此不会产生额外效果。

        float childExplosionRadius =
            modifierSnapshot.ExplosionRadius
            * 0.75f;

        float childExplosionDamageMultiplier =
            modifierSnapshot.ExplosionDamageMultiplier
            * 0.75f;


        return new ProjectileModifierSnapshot(
            // Piercing
            childPierceCount,

            // Explosion
            modifierSnapshot.Explosive,
            childExplosionRadius,
            childExplosionDamageMultiplier,

            // Chain Lightning
            modifierSnapshot.ChainLightning,
            modifierSnapshot.ChainCount,
            modifierSnapshot.ChainRange,
            modifierSnapshot.ChainDamageMultiplier,

            // Child 不再拥有 Split。
            false,
            0,
            0f,
            0f,
            0f,
            0f,

            // Generation
            modifierSnapshot.Generation + 1
        );
    }


    /// <summary>
    /// 根据 Split 数量和 Child Index，
    /// 返回相对于 Parent 飞行方向的角度偏移。
    /// </summary>
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
                switch (childIndex)
                {
                    case 0:
                        return -20f;

                    case 1:
                        return 0f;

                    default:
                        return 20f;
                }

            case 4:
                switch (childIndex)
                {
                    case 0:
                        return -30f;

                    case 1:
                        return -10f;

                    case 2:
                        return 10f;

                    default:
                        return 30f;
                }

            default:
                return 0f;
        }
    }


    /// <summary>
    /// 将一个二维方向旋转指定角度。
    /// </summary>
    private Vector2 RotateDirection(
        Vector2 direction,
        float angleDegrees)
    {
        float angleRadians =
            angleDegrees
            * Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(angleRadians);

        float sin =
            Mathf.Sin(angleRadians);

        Vector2 rotatedDirection =
            new Vector2(
                direction.x * cos
                - direction.y * sin,

                direction.x * sin
                + direction.y * cos
            );

        if (rotatedDirection.sqrMagnitude
            <= 0.0001f)
        {
            return Vector2.right;
        }

        return rotatedDirection.normalized;
    }

    // =========================================================
    // Shared Secondary Damage Calculation
    // =========================================================

    /// <summary>
    /// Explosion 与 Chain 共用的 Secondary Damage 计算。
    ///
    /// 有效 Secondary Damage 最低为 1。
    /// </summary>
    private int CalculateSecondaryDamage(
        float damageMultiplier)
    {
        int calculatedDamage =
            Mathf.RoundToInt(
                damage
                * damageMultiplier
            );

        return Mathf.Max(
            1,
            calculatedDamage
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

        isReturned = true;

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
            Debug.LogWarning(
                "Bullet: No owner BulletPool was assigned. "
                + "The Bullet will be destroyed.",
                this
            );

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

    private void SpawnChainLightningEffect(
    Vector2 startPosition,
    Vector2 endPosition)
    {
        if (ChainLightningEffectPool.Instance == null)
        {
            return;
        }

        ChainLightningEffectPool.Instance.GetEffect(
            startPosition,
            endPosition
        );
    }

    private void SpawnExplosionEffect(
    Vector3 position,
    float radius)
    {
        if (ExplosionEffectPool.Instance == null)
        {
            return;
        }

        ExplosionEffectPool.Instance
            .GetEffect(
                position,
                radius
            );
    }

    // =========================================================
    // Hit Effect
    // =========================================================

    private void SpawnHitEffect()
    {
        if (HitEffectPool.Instance == null)
        {
            Debug.LogWarning(
                "Bullet: HitEffectPool was not found "
                + "in the current scene.",
                this
            );

            return;
        }

        HitEffect hitEffect =
            HitEffectPool.Instance
                .GetHitEffect(
                    transform.position,
                    Quaternion.identity
                );

        if (hitEffect == null)
        {
            return;
        }

        hitEffect.Initialize();
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Print Modifier Snapshot")]
    private void PrintModifierSnapshot()
    {
        Debug.Log(
            modifierSnapshot.GetDebugText(),
            this
        );
    }


    [ContextMenu(
        "Debug/Print Piercing Runtime State")]
    private void PrintPiercingRuntimeState()
    {
        Debug.Log(
            "===== Bullet Piercing Runtime State =====\n"
            + "Snapshot Pierce Count: "
            + modifierSnapshot.PierceCount
            + "\nRemaining Pierce Count: "
            + remainingPierceCount
            + "\nDirect Hit Enemy Count: "
            + directHitEnemyCount
            + "\nRecorded Enemy Count: "
            + hitEnemies.Count
            + "\nIs Returned: "
            + isReturned,
            this
        );
    }


    [ContextMenu(
        "Debug/Print Explosion Runtime State")]
    private void PrintExplosionRuntimeState()
    {
        Debug.Log(
            "===== Bullet Explosion Runtime State =====\n"
            + "Explosive Enabled: "
            + modifierSnapshot.Explosive
            + "\nExplosion Radius: "
            + modifierSnapshot.ExplosionRadius
            + "\nExplosion Damage Multiplier: "
            + modifierSnapshot.ExplosionDamageMultiplier
            + "\nLast Explosion Damage: "
            + lastExplosionDamage
            + "\nLast Explosion Target Count: "
            + lastExplosionTargetCount,
            this
        );
    }


    [ContextMenu(
        "Debug/Print Chain Runtime State")]
    private void PrintChainRuntimeState()
    {
        Debug.Log(
            "===== Bullet Chain Runtime State =====\n"
            + "Chain Enabled: "
            + modifierSnapshot.ChainLightning
            + "\nSnapshot Chain Count: "
            + modifierSnapshot.ChainCount
            + "\nChain Range: "
            + modifierSnapshot.ChainRange
            + "\nChain Damage Multiplier: "
            + modifierSnapshot.ChainDamageMultiplier
            + "\nHas Triggered Chain: "
            + hasTriggeredChain
            + "\nLast Chain Damage: "
            + lastChainDamage
            + "\nLast Chain Hit Count: "
            + lastChainHitCount
            + "\nVisited Enemy Count: "
            + chainVisitedEnemies.Count,
            this
        );
    }

    [ContextMenu(
    "Debug/Print Split Runtime State")]
    private void PrintSplitRuntimeState()
    {
        Debug.Log(
            "===== Bullet Split Runtime State =====\n"
            + "Split Enabled: "
            + modifierSnapshot.SplitShot
            + "\nSnapshot Split Count: "
            + modifierSnapshot.SplitCount
            + "\nGeneration: "
            + modifierSnapshot.Generation
            + "\nChild Damage Multiplier: "
            + modifierSnapshot.ChildDamageMultiplier
            + "\nChild Speed Multiplier: "
            + modifierSnapshot.ChildSpeedMultiplier
            + "\nChild Scale Multiplier: "
            + modifierSnapshot.ChildScaleMultiplier
            + "\nChild Life Time Multiplier: "
            + modifierSnapshot.ChildLifeTimeMultiplier
            + "\nLast Split Spawn Count: "
            + lastSplitSpawnCount
            + "\nTotal Split Spawn Count: "
            + totalSplitSpawnCount,
            this
        );
    }


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