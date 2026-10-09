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

    // =====================================================
    // Public Properties
    // =====================================================

    public float MoveSpeed => moveSpeed;

    public float StoppingDistance =>
        stoppingDistance;

    public Transform NavigationTarget => target;

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

        Vector2 currentPosition =
            rb.position;

        Vector2 targetPosition =
            target.position;

        float distance =
            Vector2.Distance(
                currentPosition,
                targetPosition
            );

        if (distance <= stoppingDistance)
        {
            return;
        }

        Vector2 steeringTarget =
            targetPosition;

        // 只有安装并启用了导航组件的敌人
        // 才改为使用 A*。
        if (pathNavigator != null &&
            pathNavigator.isActiveAndEnabled)
        {
            if (!pathNavigator.TryGetNextWaypoint(
                    currentPosition,
                    out steeringTarget))
            {
                // 无有效路径时不执行直线追踪。
                return;
            }
        }

        Vector2 nextPosition =
            Vector2.MoveTowards(
                currentPosition,
                steeringTarget,
                moveSpeed * Time.fixedDeltaTime
            );

        MoveWithEnvironmentGuard(nextPosition);
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