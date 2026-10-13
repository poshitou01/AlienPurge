
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class EnemyAnimationController : MonoBehaviour
{
    // Animator 中的 Int 参数名称：Direction
    private static readonly int DirectionHash =
        Animator.StringToHash("Direction");

    [Header("Movement Source")]
    [Tooltip("敌人实际移动的根对象")]
    [SerializeField]
    private Transform movementRoot;

    [Header("Direction Settings")]
    [Range(0, 7)]
    [SerializeField]
    private int defaultDirection = 1;

    [Min(0f)]
    [Tooltip("单帧最小移动距离，用于过滤位置抖动")]
    [SerializeField]
    private float minimumMovementDelta = 0.002f;

    private Animator animator;
    private Vector3 previousPosition;
    private int currentDirection;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (movementRoot == null)
        {
            movementRoot = transform;
        }

        previousPosition = movementRoot.position;

        currentDirection =
            Mathf.Clamp(defaultDirection, 0, 7);

        animator.SetInteger(
            DirectionHash,
            currentDirection
        );
    }

    private void LateUpdate()
    {
        if (movementRoot == null)
        {
            return;
        }

        Vector3 currentPosition =
            movementRoot.position;

        Vector2 movementDelta =
            currentPosition - previousPosition;

        previousPosition = currentPosition;

        // 停止移动时保持最后朝向
        if (movementDelta.sqrMagnitude <
            minimumMovementDelta * minimumMovementDelta)
        {
            return;
        }

        int newDirection =
            CalculateEightDirection(movementDelta);

        if (newDirection == currentDirection)
        {
            return;
        }

        currentDirection = newDirection;

        animator.SetInteger(
            DirectionHash,
            currentDirection
        );
    }

    private int CalculateEightDirection(
        Vector2 movement)
    {
        float angle = Mathf.Atan2(
            movement.y,
            movement.x
        ) * Mathf.Rad2Deg;

        int sector =
            Mathf.RoundToInt(angle / 45f);

        // 方向编号：
        // 0 = D
        // 1 = DR
        // 2 = R
        // 3 = UR
        // 4 = U
        // 5 = UL
        // 6 = L
        // 7 = DL

        return (sector + 10) % 8;
    }

    private void OnValidate()
    {
        defaultDirection =
            Mathf.Clamp(defaultDirection, 0, 7);

        minimumMovementDelta =
            Mathf.Max(0f, minimumMovementDelta);
    }
}
