using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]
    private float moveSpeed = 1.5f;

    [SerializeField]
    private float stoppingDistance = 0.72f;

    [Header("Target")]
    [SerializeField]
    private Transform target;

    [Header("Runtime Movement Control")]
    [SerializeField]
    private bool isMovementLocked;

    [Header("Runtime Knockback State")]
    [SerializeField]
    private bool isKnockedBack;

    private Rigidbody2D rb;

    private Vector2 knockbackDirection;
    private float knockbackSpeed;
    private float knockbackTimeRemaining;

    public float MoveSpeed => moveSpeed;

    public bool IsMovementLocked =>
        isMovementLocked;

    public bool IsKnockedBack =>
        isKnockedBack;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

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

        Vector2 nextPosition =
            Vector2.MoveTowards(
                currentPosition,
                targetPosition,
                moveSpeed
                * Time.fixedDeltaTime
            );

        rb.MovePosition(nextPosition);
    }

    private void UpdateKnockbackMovement()
    {
        if (knockbackTimeRemaining <= 0f
            || knockbackDirection.sqrMagnitude
            <= 0.0001f)
        {
            ResetKnockbackState();
            return;
        }

        Vector2 nextPosition =
            rb.position
            + knockbackDirection
            * knockbackSpeed
            * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);

        knockbackTimeRemaining -=
            Time.fixedDeltaTime;

        if (knockbackTimeRemaining <= 0f)
        {
            ResetKnockbackState();
        }
    }

    /// <summary>
    /// 对敌人施加一段可控位移的击退。
    /// distance 表示总击退距离，
    /// duration 表示击退持续时间。
    /// </summary>
    public void ApplyKnockback(
        Vector2 direction,
        float distance,
        float duration
    )
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (direction.sqrMagnitude
            <= 0.0001f)
        {
            return;
        }

        if (distance <= 0f
            || duration <= 0f)
        {
            return;
        }

        knockbackDirection =
            direction.normalized;

        knockbackTimeRemaining =
            duration;

        knockbackSpeed =
            distance / duration;

        isKnockedBack = true;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }


    /// <summary>
    /// 设置是否禁止 EnemyMovement 执行普通追踪。
    ///
    /// 此锁定不阻止 Knockback。
    /// Fast 等攻击脚本取得移动控制时使用。
    /// </summary>
    public void SetMovementLocked(bool locked)
    {
        isMovementLocked = locked;

        if (locked && rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
    private void ResetKnockbackState()
    {
        isKnockedBack = false;

        knockbackDirection =
            Vector2.zero;

        knockbackSpeed = 0f;
        knockbackTimeRemaining = 0f;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    /// <summary>
    /// 在敌人生成后初始化本次敌人的移动速度。
    /// </summary>
    public void InitializeMoveSpeed(
        float newMoveSpeed
    )
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

    /// <summary>
    /// 每次从 EnemyPool 取出时重置移动和击退状态。
    /// </summary>
    public void PrepareForSpawn()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        enabled = true;
        target = null;

        SetMovementLocked(false);
        ResetKnockbackState();
        FindPlayer();
    }

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
            target =
                playerObject.transform;
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

    private void ValidateMovementSettings()
    {
        moveSpeed =
            Mathf.Max(0f, moveSpeed);

        stoppingDistance =
            Mathf.Max(0f, stoppingDistance);
    }

    private void OnDisable()
    {
        SetMovementLocked(false);
        ResetKnockbackState();
    }

    private void OnValidate()
    {
        ValidateMovementSettings();
    }

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