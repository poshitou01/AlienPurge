using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;

    private Vector2 moveInput;
    private Vector2 lastNonZeroMoveDirection;

    private bool canMove = true;

    /// <summary>
    /// 当前帧读取到的归一化移动方向。
    /// 没有输入或移动被锁定时为 Vector2.zero。
    /// </summary>
    public Vector2 CurrentMoveInput =>
        moveInput;

    /// <summary>
    /// 玩家最近一次有效的非零移动方向。
    /// 冲刺时没有当前输入，可以使用这个方向。
    /// </summary>
    public Vector2 LastNonZeroMoveDirection =>
        lastNonZeroMoveDirection;

    public bool CanMove =>
        canMove;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        moveInput = Vector2.zero;
        lastNonZeroMoveDirection = Vector2.zero;
    }

    private void Update()
    {
        if (!canMove
            || UpgradeManager.IsChoosingUpgrade)
        {
            moveInput = Vector2.zero;
            return;
        }

        float moveX =
            Input.GetAxisRaw("Horizontal");

        float moveY =
            Input.GetAxisRaw("Vertical");

        Vector2 rawInput =
            new Vector2(moveX, moveY);

        if (rawInput.sqrMagnitude
            <= 0.0001f)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = rawInput.normalized;

        lastNonZeroMoveDirection =
            moveInput;
    }

    private void FixedUpdate()
    {
        if (!canMove
            || UpgradeManager.IsChoosingUpgrade)
        {
            return;
        }

        Vector2 targetPosition =
            rb.position
            + moveInput
            * moveSpeed
            * Time.fixedDeltaTime;

        rb.MovePosition(targetPosition);
    }

    /// <summary>
    /// 控制普通移动是否允许执行。
    /// 锁定移动时清除当前输入，
    /// 但保留最近一次有效移动方向。
    /// </summary>
    public void SetCanMove(bool value)
    {
        canMove = value;

        if (!canMove)
        {
            moveInput = Vector2.zero;
        }
    }

    public void AddMoveSpeed(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        moveSpeed += amount;

        Debug.Log(
            "Move speed upgraded. "
            + "Current move speed: "
            + moveSpeed
        );
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
}