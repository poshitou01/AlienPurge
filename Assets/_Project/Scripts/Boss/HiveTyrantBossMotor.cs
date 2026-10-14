
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class HiveTyrantBossMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private Transform target;

    [Header("Auto Find")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.7f;
    [SerializeField] private float stoppingDistance = 2.2f;
    [SerializeField] private float movementThreshold = 0.02f;

    [Header("Smooth Turning")]
    [Tooltip("转向速度，单位：度/秒")]
    [SerializeField] private float turnSpeed = 360f;

    [Tooltip("方向切换缓冲角度，减少临界点抖动")]
    [Range(0f, 15f)]
    [SerializeField] private float directionHysteresis = 7f;

    [Tooltip("目标转向角度过大时，先原地转向")]
    [SerializeField] private bool turnInPlace = true;

    [Range(20f, 180f)]
    [SerializeField] private float turnInPlaceAngle = 60f;

    [Header("Animator Parameters")]
    [SerializeField] private string directionParam = "Direction";
    [SerializeField] private string actionParam = "Action";

    [Header("Debug")]
    [SerializeField] private bool movementLocked;
    [SerializeField] private bool dead;

    [SerializeField] private int currentDirection = 1;
    [SerializeField] private float currentFacingAngle = -45f;

    private const int ACTION_IDLE = 0;
    private const int ACTION_MOVE = 1;
    private const int ACTION_HIT = 2;
    private const int ACTION_DEATH = 3;
    private const int ACTION_IDLE_PHASE2 = 4;

    public Transform Target => target;
    public bool IsDead => dead;
    public bool IsMovementLocked => movementLocked;

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

        // 保持与原有默认 DR 方向一致。
        currentDirection = 1;
        currentFacingAngle = -45f;

        SetDirection(currentDirection);
        SetAction(ACTION_IDLE);
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
        if (dead)
        {
            SetAction(ACTION_DEATH);
            return;
        }

        if (movementLocked || target == null)
        {
            SetAction(ACTION_IDLE);
            return;
        }

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;

        float distance = toTarget.magnitude;

        if (distance <= movementThreshold)
        {
            SetAction(ACTION_IDLE);
            return;
        }

        Vector2 desiredDirection = toTarget.normalized;

        // 即使停止追击，也允许缓慢朝向 Player。
        float remainingTurnAngle =
            UpdateSmoothFacing(desiredDirection);

        if (distance <= stoppingDistance)
        {
            SetAction(ACTION_IDLE);
            return;
        }

        // 玩家突然绕到背后时，先转身再追击。
        if (turnInPlace &&
            remainingTurnAngle > turnInPlaceAngle)
        {
            SetAction(ACTION_IDLE);
            return;
        }

        Vector2 nextPos =
            currentPos +
            desiredDirection * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(nextPos);

        SetAction(ACTION_MOVE);
    }

    private float UpdateSmoothFacing(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude <= 0.0001f)
            return 0f;

        float desiredAngle =
            Mathf.Atan2(
                desiredDirection.y,
                desiredDirection.x
            ) * Mathf.Rad2Deg;

        // 不是立即设置 desiredAngle，
        // 而是按固定转向速度逐步接近。
        currentFacingAngle =
            Mathf.MoveTowardsAngle(
                currentFacingAngle,
                desiredAngle,
                turnSpeed * Time.fixedDeltaTime
            );

        Vector2 facingVector = new Vector2(
            Mathf.Cos(currentFacingAngle * Mathf.Deg2Rad),
            Mathf.Sin(currentFacingAngle * Mathf.Deg2Rad)
        );

        int candidateDirection =
            GetDirectionIndex(facingVector);

        // 防止在两个方向的分界线上反复切换。
        if (candidateDirection != currentDirection)
        {
            float currentCenterAngle =
                (currentDirection - 2) * 45f;

            float deviation =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        currentCenterAngle,
                        currentFacingAngle
                    )
                );

            if (deviation >= 22.5f + directionHysteresis)
            {
                SetDirection(candidateDirection);
            }
        }

        return Mathf.Abs(
            Mathf.DeltaAngle(
                currentFacingAngle,
                desiredAngle
            )
        );
    }

    private int GetDirectionIndex(Vector2 dir)
    {
        float angle =
            Mathf.Atan2(dir.y, dir.x) *
            Mathf.Rad2Deg;

        // 0 D, 1 DR, 2 R, 3 UR
        // 4 U, 5 UL, 6 L, 7 DL

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

    private void SetDirection(int direction)
    {
        currentDirection = direction;

        if (bodyAnimator != null)
        {
            bodyAnimator.SetInteger(
                directionParam,
                direction
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

    // 供后续 BossController 调用。

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked && !dead)
            SetAction(ACTION_IDLE);
    }

    public void SetDead(bool isDead)
    {
        dead = isDead;

        if (dead)
            SetAction(ACTION_DEATH);
    }

    public void ForceIdle()
    {
        if (!dead)
            SetAction(ACTION_IDLE);
    }

    public void ForceMove()
    {
        if (!dead)
            SetAction(ACTION_MOVE);
    }

    public void ForceHit()
    {
        if (!dead)
            SetAction(ACTION_HIT);
    }

    public void ForcePhase2Idle()
    {
        if (!dead)
            SetAction(ACTION_IDLE_PHASE2);
    }

    public int GetCurrentDirection()
    {
        return currentDirection;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        movementThreshold = Mathf.Max(0f, movementThreshold);
        turnSpeed = Mathf.Max(1f, turnSpeed);
    }
}
