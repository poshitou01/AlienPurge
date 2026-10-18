using UnityEngine;

/// <summary>
/// Phase40 - Enemy Movement
///
/// 保留：
/// - Rigidbody2D 移动
/// - 普通追踪
/// - Movement Lock
/// - Knockback
/// - EnemyPool 初始化
///
/// 新增：
/// - 可选 A* 路径点追踪
/// - Environment Collider 阻挡检测
/// - 可选同类敌人避让（默认关闭）
///
/// 没有 EnemyPathNavigator 时，
/// 使用原有的直接追踪逻辑。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    // =====================================================
    // Movement Settings
    // =====================================================

    [Header("Movement Settings")]
    [SerializeField]
    private float moveSpeed = 1.5f;

    [SerializeField]
    private float stoppingDistance = 0.72f;

    // =====================================================
    // Target
    // =====================================================

    [Header("Target")]
    [SerializeField]
    private Transform target;

    // =====================================================
    // Environment Collision Guard
    // =====================================================

    [Header("Environment Collision Guard")]

    [SerializeField]
    private bool preventEnvironmentPenetration = false;

    [SerializeField]
    private LayerMask environmentBlockingLayers = 1 << 7;

    [Min(0f)]
    [SerializeField]
    private float collisionSkin = 0.025f;

    // =====================================================
    // Optional Crowd Separation
    // =====================================================

    [Header("Crowd Separation (Optional)")]

    [Tooltip("仅在需要分离的敌人（如 Swarmer）上勾选；其他敌人保持原样")]
    [SerializeField]
    private bool enableSeparation = false;

    [Tooltip("在这个半径内检测同类敌人，单位为 Unity 世界单位")]
    [Min(0.1f)]
    [SerializeField]
    private float separationRadius = 1.0f;

    [Tooltip("邻近敌人排斥方向相对于追踪方向的权重")]
    [Min(0f)]
    [SerializeField]
    private float separationStrength = 1.5f;

    [Tooltip("参与检测的物理 Layer。仓库中 Enemy_Normal 使用 Default Layer。")]
    [SerializeField]
    private LayerMask separationLayers = 1 << 0;

    [Tooltip("只避让与自己相同 EnemyType 的敌人")]
    [SerializeField]
    private bool sameEnemyTypeOnly = true;

    // =====================================================
    // Movement Lock
    // =====================================================

    [Header("Runtime Movement Control")]
    [SerializeField]
    private bool isMovementLocked;

    // =====================================================
    // Knockback
    // =====================================================

    [Header("Runtime Knockback State")]
    [SerializeField]
    private bool isKnockedBack;

    private Vector2 knockbackDirection;
    private float knockbackSpeed;
    private float knockbackTimeRemaining;

    // =====================================================
    // References
    // =====================================================

    private Rigidbody2D rb;

    private EnemyPathNavigator pathNavigator;

    private ContactFilter2D environmentCastFilter;

    private readonly RaycastHit2D[] castHits =
        new RaycastHit2D[8];

    // 每个敌人只创建一次查询缓存，避免每次 FixedUpdate 分配数组。
    private readonly Collider2D[] separationHits =
        new Collider2D[32];

    private PooledEnemy pooledEnemy;

    // =====================================================
    // Public Properties
    // =====================================================

    public float MoveSpeed => moveSpeed;

    public float StoppingDistance =>
        stoppingDistance;

    public Transform NavigationTarget => target;

    // Specialist steering still uses the existing A* and Rigidbody movement pipeline.
    public void SetNavigationTarget(Transform navigationTarget)
    {
        if (target == navigationTarget) return;
        target = navigationTarget;
        if (pathNavigator != null) pathNavigator.ResetNavigation();
    }

    public bool IsMovementLocked =>
        isMovementLocked;

    public bool IsKnockedBack =>
        isKnockedBack;

    // =====================================================
    // Unity Lifecycle
    // =====================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        pathNavigator =
            GetComponent<EnemyPathNavigator>();

        pooledEnemy = GetComponent<PooledEnemy>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        environmentCastFilter =
            new ContactFilter2D();

        environmentCastFilter.SetLayerMask(
            environmentBlockingLayers
        );

        environmentCastFilter.useTriggers = false;

        ValidateMovementSettings();

        SetMovementLocked(false);
        ResetKnockbackState();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FixedUpdate()
    {
        // 击退优先于普通移动和移动锁定。
        if (isKnockedBack)
        {
            UpdateKnockbackMovement();
            return;
        }

        if (isMovementLocked)
        {
            return;
        }

        UpdateChaseMovement();
    }

    // =====================================================
    // Chase
    // =====================================================

    private void UpdateChaseMovement()
    {
        if (target == null)
        {
            FindPlayer();

            if (target == null)
            {
                return;
            }
        }

        Vector2 currentPosition = rb.position;
        Vector2 targetPosition = target.position;

        float distance = Vector2.Distance(
            currentPosition, targetPosition);

        // 已抵达原有攻击/停靠距离：停止追踪，但允许挤在
        // 同一点的同类敌人缓慢互相避让。
        if (distance <= stoppingDistance)
        {
            if (enableSeparation)
            {
                MoveWithOptionalSeparation(
                    currentPosition, Vector2.zero);
            }

            return;
        }

        Vector2 steeringTarget = targetPosition;

        // 原 Phase40 A* 路径点追踪保持不变。
        // 导航启用但找不到路径时，不改为穿过障碍直追玩家。
        if (pathNavigator != null &&
            pathNavigator.isActiveAndEnabled)
        {
            if (!pathNavigator.TryGetNextWaypoint(
                    currentPosition,
                    out steeringTarget))
            {
                return;
            }
        }

        // 保留原本 MoveTowards 的步长，避免抵达临近
        // waypoint 时发生不必要的越界移动。
        Vector2 normalNextPosition =
            Vector2.MoveTowards(
                currentPosition,
                steeringTarget,
                moveSpeed * Time.fixedDeltaTime);

        if (!enableSeparation)
        {
            // 与原 Phase40 完全相同的追踪和环境碰撞流程。
            MoveWithEnvironmentGuard(normalNextPosition);
            return;
        }

        MoveWithOptionalSeparation(
            currentPosition,
            normalNextPosition - currentPosition);
    }

    // =====================================================
    // Optional Separation: steer, never move Rigidbody twice
    // =====================================================

    private void MoveWithOptionalSeparation(
        Vector2 currentPosition,
        Vector2 originalStep)
    {
        // 查不到同类邻居时，直接执行原来的移动结果。
        Vector2 away = CalculateSeparation(currentPosition);

        if (away.sqrMagnitude <= 0.000001f)
        {
            if (originalStep.sqrMagnitude > 0.00000001f)
            {
                MoveWithEnvironmentGuard(
                    currentPosition + originalStep);
            }

            return;
        }

        Vector2 chaseDirection =
            originalStep.sqrMagnitude > 0.00000001f
                ? originalStep.normalized
                : Vector2.zero;

        Vector2 adjustedDirection =
            chaseDirection + away * separationStrength;

        // 单次移动上限仍为原始 moveSpeed，不额外加速。
        Vector2 adjustedStep =
            Vector2.ClampMagnitude(adjustedDirection, 1f)
            * moveSpeed * Time.fixedDeltaTime;

        if (adjustedStep.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        // 关键：避让位移仍走现有环境 Cast 阻挡路径，
        // 不能绕过 Phase40 的墙壁碰撞安全逻辑。
        MoveWithEnvironmentGuard(
            currentPosition + adjustedStep);
    }

    private Vector2 CalculateSeparation(
        Vector2 currentPosition)
    {
        if (separationRadius <= 0f ||
            separationStrength <= 0f ||
            separationLayers.value == 0)
        {
            return Vector2.zero;
        }

        // Unity 2022.3 支持的 NonAlloc 查询，避免每帧 new。
        int count = Physics2D.OverlapCircleNonAlloc(
            currentPosition,
            separationRadius,
            separationHits,
            separationLayers);

        Vector2 combinedAway = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = separationHits[i];

            if (candidate == null || candidate.isTrigger)
            {
                continue;
            }

            EnemyMovement other =
                candidate.GetComponentInParent<EnemyMovement>();

            if (other == null || other == this ||
                !other.isActiveAndEnabled)
            {
                continue;
            }

            if (sameEnemyTypeOnly)
            {
                if (pooledEnemy == null ||
                    other.pooledEnemy == null ||
                    pooledEnemy.Type != other.pooledEnemy.Type)
                {
                    continue;
                }
            }

            EnemyHealth otherHealth =
                other.GetComponent<EnemyHealth>();

            if (otherHealth != null && otherHealth.IsDead)
            {
                continue;
            }

            Vector2 away =
                currentPosition - (Vector2)other.transform.position;

            float neighborDistance = away.magnitude;

            if (neighborDistance >= separationRadius)
            {
                continue;
            }

            if (neighborDistance < 0.001f)
            {
                // 完全重叠时按实例 ID 产生相反的解散方向。
                away = GetInstanceID() < other.GetInstanceID()
                    ? Vector2.left : Vector2.right;
            }
            else
            {
                away /= neighborDistance;
            }

            float weight =
                1f - neighborDistance / separationRadius;

            combinedAway += away * weight;
        }

        return Vector2.ClampMagnitude(combinedAway, 1f);
    }

    // =====================================================
    // Knockback Movement
    // =====================================================

    private void UpdateKnockbackMovement()
    {
        if (knockbackTimeRemaining <= 0f ||
            knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            ResetKnockbackState();
            return;
        }

        Vector2 nextPosition =
            rb.position +
            knockbackDirection *
            knockbackSpeed *
            Time.fixedDeltaTime;

        MoveWithEnvironmentGuard(nextPosition);

        knockbackTimeRemaining -=
            Time.fixedDeltaTime;

        if (knockbackTimeRemaining <= 0f)
        {
            ResetKnockbackState();
        }
    }

    // =====================================================
    // Environment Collision
    // =====================================================

    /// <summary>
    /// 移动前检测 Environment 实体障碍。
    ///
    /// 特别针对 Kinematic Rigidbody2D。
    ///
    /// 不负责自动滑墙，绕行由 A* 路径负责。
    /// </summary>
    private void MoveWithEnvironmentGuard(
        Vector2 wantedPosition)
    {
        Vector2 delta =
            wantedPosition - rb.position;

        float distance = delta.magnitude;

        if (distance <= 0.00001f)
        {
            return;
        }

        if (!preventEnvironmentPenetration ||
            environmentBlockingLayers.value == 0)
        {
            rb.MovePosition(wantedPosition);
            return;
        }

        Vector2 direction =
            delta / distance;

        int hitCount = rb.Cast(
            direction,
            environmentCastFilter,
            castHits,
            distance + collisionSkin
        );

        float allowedDistance = distance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];

            if (hit.collider == null)
            {
                continue;
            }

            float safeDistance =
                Mathf.Max(
                    0f,
                    hit.distance - collisionSkin
                );

            allowedDistance =
                Mathf.Min(
                    allowedDistance,
                    safeDistance
                );
        }

        if (allowedDistance > 0.00001f)
        {
            rb.MovePosition(
                rb.position +
                direction * allowedDistance
            );
        }
    }

    // =====================================================
    // Apply Knockback
    // =====================================================

    /// <summary>
    /// direction：击退方向
    /// distance：总击退距离
    /// duration：持续时间
    /// </summary>
    public void ApplyKnockback(
        Vector2 direction,
        float distance,
        float duration)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (distance <= 0f || duration <= 0f)
        {
            return;
        }

        knockbackDirection =
            direction.normalized;

        knockbackTimeRemaining = duration;

        knockbackSpeed =
            distance / duration;

        isKnockedBack = true;

        // 击退后应重新寻路，
        // 不继续依赖击退前的路径。
        if (pathNavigator != null)
        {
            pathNavigator.ResetNavigation();
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // =====================================================
    // Movement Lock
    // =====================================================

    /// <summary>
    /// 阻止普通追踪，不阻止击退。
    ///
    /// Fast Charge、Heavy Shockwave、
    /// Ranged Attack 继续使用此方法。
    /// </summary>
    public void SetMovementLocked(bool locked)
    {
        bool wasLocked = isMovementLocked;

        isMovementLocked = locked;

        // 特殊攻击接管移动时清理旧路径。
        if (locked &&
            !wasLocked &&
            pathNavigator != null)
        {
            pathNavigator.ResetNavigation();
        }

        if (locked && rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // =====================================================
    // Knockback Reset
    // =====================================================

    private void ResetKnockbackState()
    {
        isKnockedBack = false;

        knockbackDirection = Vector2.zero;

        knockbackSpeed = 0f;

        knockbackTimeRemaining = 0f;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // =====================================================
    // Difficulty
    // =====================================================

    /// <summary>
    /// 保留原有难度成长速度初始化接口。
    /// </summary>
    public void InitializeMoveSpeed(
        float newMoveSpeed)
    {
        if (newMoveSpeed < 0f)
        {
            Debug.LogWarning(
                $"{gameObject.name} 收到了无效移动速度："
                + $"{newMoveSpeed}，已自动修正为 0。",
                this
            );

            newMoveSpeed = 0f;
        }

        moveSpeed = newMoveSpeed;

        Debug.Log(
            $"{gameObject.name} 移动速度初始化完成："
            + $"{moveSpeed:F2}",
            this
        );
    }

    // =====================================================
    // EnemyPool
    // =====================================================

    /// <summary>
    /// 每次从 EnemyPool 取出时重置状态。
    /// </summary>
    public void PrepareForSpawn()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (pathNavigator == null)
        {
            pathNavigator =
                GetComponent<EnemyPathNavigator>();
        }

        if (pooledEnemy == null)
        {
            pooledEnemy = GetComponent<PooledEnemy>();
        }

        enabled = true;

        target = null;

        SetMovementLocked(false);

        ResetKnockbackState();

        FindPlayer();

        if (pathNavigator != null)
        {
            pathNavigator.ResetNavigation();
        }
    }

    // =====================================================
    // Find Player
    // =====================================================

    private void FindPlayer()
    {
        if (target != null)
        {
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject != null)
        {
            target = playerObject.transform;
        }
        else
        {
            Debug.LogWarning(
                $"{gameObject.name} 没有找到 "
                + "Tag 为 Player 的对象。",
                this
            );
        }
    }

    // =====================================================
    // Validation
    // =====================================================

    private void ValidateMovementSettings()
    {
        moveSpeed =
            Mathf.Max(0f, moveSpeed);

        stoppingDistance =
            Mathf.Max(0f, stoppingDistance);

        collisionSkin =
            Mathf.Max(0f, collisionSkin);

        separationRadius =
            Mathf.Max(0.1f, separationRadius);

        separationStrength =
            Mathf.Max(0f, separationStrength);
    }

    private void OnDisable()
    {
        SetMovementLocked(false);

        ResetKnockbackState();

        if (pathNavigator != null)
        {
            pathNavigator.ResetNavigation();
        }
    }

    private void OnValidate()
    {
        ValidateMovementSettings();
    }

    // =====================================================
    // Scene Debug (selected object only)
    // =====================================================

    private void OnDrawGizmosSelected()
    {
        if (!enableSeparation)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position, separationRadius);
    }

    // =====================================================
    // Existing Debug Function
    // =====================================================

    [ContextMenu(
        "Test Initialize Move Speed To 3"
    )]
    private void TestInitializeMoveSpeedTo3()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试"
                + "敌人移动速度初始化。",
                this
            );

            return;
        }

        InitializeMoveSpeed(3f);
    }
}
