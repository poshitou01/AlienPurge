using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [Min(0.01f)]
    [SerializeField]
    private float speed = 7f;

    [Min(0.01f)]
    [SerializeField]
    private float lifeTime = 3f;

    [Min(1)]
    [SerializeField]
    private int damage = 1;

    [Min(0.01f)]
    [SerializeField]
    private float scaleMultiplier = 1f;

    [Header("Runtime Debug")]
    [SerializeField]
    private float elapsedLifeTime;

    [SerializeField]
    private bool isReturned = true;

    [SerializeField]
    private bool hasPool;

    [SerializeField]
    private Vector2 moveDirection =
        Vector2.right;

    private Rigidbody2D rb;
    private Collider2D projectileCollider;

    private EnemyProjectilePool ownerPool;

    private Vector3 originalScale;

    public bool IsReturned =>
        isReturned;

    public bool HasPool =>
        hasPool;

    public float Speed =>
        speed;

    public int Damage =>
        damage;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        projectileCollider =
            GetComponent<Collider2D>();

        originalScale =
            transform.localScale;

        if (projectileCollider == null)
        {
            Debug.LogError(
                gameObject.name
                + " 缺少 Collider2D，"
                + "敌方投射物无法检测玩家。",
                this
            );
        }
    }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    private void Update()
    {
        if (isReturned)
        {
            return;
        }

        if (GameManager.Instance != null
            && !GameManager.Instance.IsPlaying)
        {
            ReturnToPool();
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

    public void SetPool(
        EnemyProjectilePool pool
    )
    {
        ownerPool = pool;
        hasPool = ownerPool != null;
    }

    /// <summary>
    /// 初始化一颗刚从对象池取出的敌方投射物。
    /// </summary>
    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newLifeTime,
        float newScaleMultiplier = 1f
    )
    {
        ResetRuntimeState();

        SetMoveDirection(direction);

        speed =
            Mathf.Max(0.01f, newSpeed);

        damage =
            Mathf.Max(1, newDamage);

        lifeTime =
            Mathf.Max(0.01f, newLifeTime);

        scaleMultiplier =
            Mathf.Max(
                0.01f,
                newScaleMultiplier
            );

        transform.localScale =
            originalScale
            * scaleMultiplier;
    }

    private void SetMoveDirection(
        Vector2 direction
    )
    {
        if (direction.sqrMagnitude
            <= 0.0001f)
        {
            direction = Vector2.right;
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

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        if (isReturned || other == null)
        {
            return;
        }

        PlayerHealth playerHealth =
            other.GetComponentInParent<
                PlayerHealth
            >();

        if (playerHealth == null)
        {
            // 敌人、地图边界、经验球等对象
            // 均不处理，投射物继续飞行。
            return;
        }

        if (!playerHealth.CompareTag("Player"))
        {
            return;
        }

        if (!playerHealth.IsDead)
        {
            // PlayerHealth 会自行处理 Dash 无敌等状态。
            // Ranged 投射物不检查 IsAirborne，
            // 所以 Jet Jump 不能免疫远程攻击。
            playerHealth.TakeDamage(damage);
        }

        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (isReturned)
        {
            return;
        }

        isReturned = true;

        StopRigidbodyMovement();

        if (projectileCollider != null)
        {
            projectileCollider.enabled =
                false;
        }

        if (ownerPool != null)
        {
            ownerPool.ReturnProjectile(
                this
            );

            return;
        }

        Debug.LogWarning(
            gameObject.name
            + " 没有 EnemyProjectilePool，"
            + "将使用 Destroy 兼容处理。",
            this
        );

        Destroy(gameObject);
    }

    private void ResetRuntimeState()
    {
        elapsedLifeTime = 0f;
        isReturned = false;

        StopRigidbodyMovement();

        if (projectileCollider != null)
        {
            projectileCollider.enabled =
                true;
        }
    }

    private void StopRigidbodyMovement()
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void OnDisable()
    {
        StopRigidbodyMovement();
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.01f, speed);
        lifeTime = Mathf.Max(0.01f, lifeTime);
        damage = Mathf.Max(1, damage);

        scaleMultiplier =
            Mathf.Max(
                0.01f,
                scaleMultiplier
            );
    }
}