using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float stoppingDistance = 1.0f;

    [Header("Target")]
    [SerializeField] private Transform target;

    private Rigidbody2D rb;

    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        ValidateMovementSettings();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FixedUpdate()
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
            currentPosition,
            targetPosition
        );

        if (distance <= stoppingDistance)
        {
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            moveSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);
    }

    /// <summary>
    /// 在敌人生成后初始化本次敌人的移动速度。
    /// </summary>
    public void InitializeMoveSpeed(float newMoveSpeed)
    {
        if (newMoveSpeed < 0f)
        {
            Debug.LogWarning(
                $"{gameObject.name} 收到了无效移动速度：{newMoveSpeed}，已自动修正为 0。"
            );

            newMoveSpeed = 0f;
        }

        moveSpeed = newMoveSpeed;

        Debug.Log(
            $"{gameObject.name} 移动速度初始化完成：{moveSpeed:F2}"
        );
    }

    private void FindPlayer()
    {
        if (target != null)
        {
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            target = playerObject.transform;
        }
        else
        {
            Debug.LogWarning(
                $"{gameObject.name} 没有找到 Tag 为 Player 的对象。"
            );
        }
    }

    private void ValidateMovementSettings()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
    }

    private void OnValidate()
    {
        ValidateMovementSettings();
    }

    [ContextMenu("Test Initialize Move Speed To 3")]
    private void TestInitializeMoveSpeedTo3()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试敌人移动速度初始化。"
            );

            return;
        }

        InitializeMoveSpeed(3f);
    }
}