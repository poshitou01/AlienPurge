
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class HiveTyrantBossMotor : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private Transform target;

    // =========================================================
    // Auto Find
    // =========================================================

    [Header("Auto Find")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";

    // =========================================================
    // Movement
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.7f;
    [SerializeField] private float stoppingDistance = 2.2f;
    [SerializeField] private float movementThreshold = 0.02f;

    // =========================================================
    // Animator Parameters
    // =========================================================

    [Header("Animator Parameters")]
    [SerializeField] private string directionParam = "Direction";
    [SerializeField] private string actionParam = "Action";

    // =========================================================
    // Debug
    // =========================================================

    [Header("Debug")]
    [SerializeField] private bool movementLocked = false;
    [SerializeField] private bool dead = false;

    // =========================================================
    // Animator Action Mapping
    // =========================================================

    private const int ACTION_IDLE = 0;
    private const int ACTION_MOVE = 1;
    private const int ACTION_HIT = 2;
    private const int ACTION_DEATH = 3;
    private const int ACTION_IDLE_PHASE2 = 4;
    private const int ACTION_MOVE_PHASE2 = 5;

    // =========================================================
    // Runtime State
    // =========================================================

    private int currentDirection = 1; // DR

    private bool isPhase2 = false;
    private bool isMoving = false;

    // =========================================================
    // Public Properties
    // =========================================================

    public Transform Target => target;
    public bool IsDead => dead;
    public bool IsMovementLocked => movementLocked;
    public bool IsPhase2 => isPhase2;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        FindBodyAnimator();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        FindBodyAnimator();

        // 默认从普通阶段开始。
        isPhase2 = false;
        isMoving = false;
        currentDirection = 1;

        SetDirection(currentDirection);
        SetLocomotionAction(false);
    }

    private void Start()
    {
        if (autoFindPlayer && target == null)
            TryFindPlayer();
    }

    private void Update()
    {
        if (autoFindPlayer && target == null)
            TryFindPlayer();
    }

    private void FixedUpdate()
    {
        // 死亡具有最高优先级。
        if (dead)
        {
            isMoving = false;
            SetAction(ACTION_DEATH);
            return;
        }

        // 移动锁定时保持当前阶段的 Idle。
        if (movementLocked || target == null)
        {
            SetLocomotionAction(false);
            return;
        }

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;

        Vector2 toTarget = targetPos - currentPos;
        float distance = toTarget.magnitude;

        if (distance <= stoppingDistance)
        {
            SetLocomotionAction(false);
            return;
        }

        Vector2 moveDir = toTarget.normalized;

        if (moveDir.sqrMagnitude <=
            movementThreshold * movementThreshold)
        {
            SetLocomotionAction(false);
            return;
        }

        // 保持原来的八方向立即转向机制。
        currentDirection = GetDirectionIndex(moveDir);

        SetDirection(currentDirection);

        Vector2 nextPos =
            currentPos +
            moveDir * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPos);

        // 这里会根据 Phase1 / Phase2 自动选择 Move。
        SetLocomotionAction(true);
    }

    // =========================================================
    // Persistent Phase2 State
    // =========================================================

    /// <summary>
    /// 进入狂暴阶段。
    /// 一旦进入 Phase2，本次战斗不会自动恢复 Phase1。
    /// </summary>
    public void EnterPhase2()
    {
        if (dead)
            return;

        if (isPhase2)
            return;

        isPhase2 = true;

        // 立即刷新当前动画，不必等待下一次移动。
        SetLocomotionAction(isMoving);

        Debug.Log(
            "[HiveTyrant] Entered Phase2.",
            this
        );
    }

    // =========================================================
    // Locomotion Action
    // =========================================================

    private void SetLocomotionAction(bool moving)
    {
        isMoving = moving;

        int action;

        if (isPhase2)
        {
            action = moving
                ? ACTION_MOVE_PHASE2
                : ACTION_IDLE_PHASE2;
        }
        else
        {
            action = moving
                ? ACTION_MOVE
                : ACTION_IDLE;
        }

        SetAction(action);
    }

    // =========================================================
    // Animator
    // =========================================================

    private void SetDirection(int dir)
    {
        currentDirection = dir;

        if (bodyAnimator != null)
        {
            bodyAnimator.SetInteger(
                directionParam,
                currentDirection
            );
        }
    }

    private void SetAction(int action)
    {
        if (bodyAnimator != null)
        {
            bodyAnimator.SetInteger(
                actionParam,
                action
            );
        }
    }

    // =========================================================
    // Direction Mapping
    // =========================================================

    private int GetDirectionIndex(Vector2 dir)
    {
        float angle =
            Mathf.Atan2(dir.y, dir.x) *
            Mathf.Rad2Deg;

        // 0 = D
        // 1 = DR
        // 2 = R
        // 3 = UR
        // 4 = U
        // 5 = UL
        // 6 = L
        // 7 = DL

        if (angle >= -112.5f && angle < -67.5f)
            return 0;

        if (angle >= -67.5f && angle < -22.5f)
            return 1;

        if (angle >= -22.5f && angle < 22.5f)
            return 2;

        if (angle >= 22.5f && angle < 67.5f)
            return 3;

        if (angle >= 67.5f && angle < 112.5f)
            return 4;

        if (angle >= 112.5f && angle < 157.5f)
            return 5;

        if (angle >= 157.5f || angle < -157.5f)
            return 6;

        return 7;
    }

    // =========================================================
    // References
    // =========================================================

    private void FindBodyAnimator()
    {
        if (bodyAnimator != null)
            return;

        Transform body =
            transform.Find("VisualRoot/Body");

        if (body == null)
            body = transform.Find("BossVisualRoot/Body");

        if (body == null)
            body = transform.Find("Body");

        if (body != null)
            bodyAnimator = body.GetComponent<Animator>();
    }

    private void TryFindPlayer()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag(playerTag);

        if (player != null)
            target = player.transform;
    }

    // =========================================================
    // Public Control API
    // =========================================================

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked && !dead)
            SetLocomotionAction(false);
    }

    public void SetDead(bool isDead)
    {
        dead = isDead;

        if (dead)
        {
            isMoving = false;
            SetAction(ACTION_DEATH);
        }
        else
        {
            SetLocomotionAction(false);
        }
    }

    public void ForceIdle()
    {
        if (!dead)
            SetLocomotionAction(false);
    }

    public void ForceMove()
    {
        if (!dead)
            SetLocomotionAction(true);
    }

    public void ForceHit()
    {
        if (!dead)
            SetAction(ACTION_HIT);
    }

    public void ForcePhase2Idle()
    {
        if (dead)
            return;

        EnterPhase2();
        SetLocomotionAction(false);
    }

    public int GetCurrentDirection()
    {
        return currentDirection;
    }

    // =========================================================
    // Inspector Testing
    // =========================================================

    [ContextMenu("Test - Enter Phase2 (Play Mode)")]
    private void TestEnterPhase2()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[HiveTyrant] Enter Play Mode before testing Phase2.",
                this
            );
            return;
        }

        EnterPhase2();
    }

    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        movementThreshold = Mathf.Max(0f, movementThreshold);
    }
}
