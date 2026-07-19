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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletCollider = GetComponent<Collider2D>();

        // 保存 Bullet Prefab 原始尺寸。
        // 后续尺寸倍率都基于这个尺寸计算。
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    private void OnDisable()
    {
        StopRigidbodyMovement();
    }

    /// <summary>
    /// 设置负责管理当前 Bullet 的对象池。
    /// BulletPool 创建 Bullet 后会调用这个方法。
    /// </summary>
    public void SetPool(BulletPool pool)
    {
        ownerPool = pool;
        hasPool = ownerPool != null;
    }

    /// <summary>
    /// 只设置子弹移动方向。
    /// 保留该方法以兼容现有调用。
    /// </summary>
    public void Initialize(Vector2 direction)
    {
        ResetRuntimeState();
        SetMoveDirection(direction);
    }

    /// <summary>
    /// 设置子弹的攻击属性。
    /// 保留四参数版本以兼容现有 PlayerShooting。
    /// </summary>
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
            lifeTime
        );
    }

    /// <summary>
    /// 完整设置一颗刚被取出的 Bullet。
    /// </summary>
    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier,
        float newLifeTime)
    {
        ResetRuntimeState();

        SetMoveDirection(direction);
        SetSpeed(newSpeed);
        SetDamage(newDamage);
        SetScaleMultiplier(newScaleMultiplier);
        SetLifeTime(newLifeTime);
    }

    /// <summary>
    /// 重置 Bullet 再次使用前的运行状态。
    /// </summary>
    private void ResetRuntimeState()
    {
        elapsedLifeTime = 0f;
        isReturned = false;

        StopRigidbodyMovement();

        if (bulletCollider != null)
        {
            bulletCollider.enabled = true;
        }
    }

    private void Update()
    {
        if (isReturned)
        {
            return;
        }

        elapsedLifeTime += Time.deltaTime;

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

    /// <summary>
    /// 设置子弹移动方向，并同步设置旋转角度。
    /// </summary>
    private void SetMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        moveDirection = direction.normalized;

        float angle =
            Mathf.Atan2(
                moveDirection.y,
                moveDirection.x
            )
            * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0.01f, newSpeed);
    }

    public void SetDamage(int newDamage)
    {
        damage = Mathf.Max(1, newDamage);
    }

    public void SetLifeTime(float newLifeTime)
    {
        lifeTime = Mathf.Max(0.01f, newLifeTime);
    }

    public void SetScaleMultiplier(
        float newScaleMultiplier)
    {
        scaleMultiplier =
            Mathf.Max(0.01f, newScaleMultiplier);

        transform.localScale =
            originalScale * scaleMultiplier;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 同一颗子弹已经开始回收时，
        // 不再处理新的碰撞。
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

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }
        else
        {
            Debug.LogWarning(
                other.gameObject.name
                + " has Enemy Tag, but no EnemyHealth "
                + "component was found.",
                other
            );
        }

        SpawnHitEffect();
        ReturnToPool();
    }

    /// <summary>
    /// 将当前 Bullet 回收到 BulletPool。
    /// </summary>
    public void ReturnToPool()
    {
        // 命中与生命周期结束可能在相近时间发生。
        // 先设置标志，防止重复回收。
        if (isReturned)
        {
            return;
        }

        isReturned = true;

        StopRigidbodyMovement();

        if (bulletCollider != null)
        {
            bulletCollider.enabled = false;
        }

        if (ownerPool != null)
        {
            ownerPool.ReturnBullet(this);
        }
        else
        {
            // 保留旧版兼容处理。
            // 正常游戏流程中的 Bullet 应当全部由 BulletPool 创建。
            Debug.LogWarning(
                "Bullet: No owner BulletPool was assigned. "
                + "The Bullet will be destroyed.",
                this
            );

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 清除 Rigidbody2D 中可能残留的运动状态。
    /// </summary>
    private void StopRigidbodyMovement()
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    /// <summary>
    /// 从 HitEffectPool 获取并播放命中特效。
    /// 不再执行 Instantiate。
    /// </summary>
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
            HitEffectPool.Instance.GetHitEffect(
                transform.position,
                Quaternion.identity
            );

        if (hitEffect == null)
        {
            return;
        }

        // GetHitEffect 会负责位置、旋转和启用对象。
        // Initialize 负责重置尺寸、颜色、Alpha 和生命周期。
        hitEffect.Initialize();
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.01f, speed);
        lifeTime = Mathf.Max(0.01f, lifeTime);
        damage = Mathf.Max(1, damage);

        scaleMultiplier =
            Mathf.Max(0.01f, scaleMultiplier);
    }
}