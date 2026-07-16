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

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;

    private Rigidbody2D rb;
    private Vector2 moveDirection = Vector2.right;
    private Vector3 originalScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // 保存 Bullet Prefab 原本的尺寸。
        // 后续所有尺寸倍率都在这个基础上进行计算。
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 只设置子弹的移动方向。
    /// 保留这个方法是为了兼容当前 PlayerShooting 的调用方式。
    /// </summary>
    public void Initialize(Vector2 direction)
    {
        SetMoveDirection(direction);
    }

    /// <summary>
    /// 一次性设置新生成子弹的全部攻击属性。
    /// 后续 PlayerShooting 会调用这个重载方法。
    /// </summary>
    public void Initialize(
        Vector2 direction,
        float newSpeed,
        int newDamage,
        float newScaleMultiplier)
    {
        SetMoveDirection(direction);
        SetSpeed(newSpeed);
        SetDamage(newDamage);
        SetScaleMultiplier(newScaleMultiplier);
    }

    private void SetMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        moveDirection = direction.normalized;

        float angle =
            Mathf.Atan2(moveDirection.y, moveDirection.x)
            * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 设置当前子弹的移动速度。
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0.01f, newSpeed);
    }

    /// <summary>
    /// 设置当前子弹的伤害。
    /// </summary>
    public void SetDamage(int newDamage)
    {
        damage = Mathf.Max(1, newDamage);
    }

    /// <summary>
    /// 设置当前子弹相对于 Prefab 原始尺寸的倍率。
    /// 例如 1.2 表示宽度和高度都变为原来的 1.2 倍。
    /// </summary>
    public void SetScaleMultiplier(float newScaleMultiplier)
    {
        scaleMultiplier =
            Mathf.Max(0.01f, newScaleMultiplier);

        transform.localScale =
            originalScale * scaleMultiplier;
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition =
            rb.position
            + moveDirection
            * speed
            * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
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
                + "component was found."
            );
        }

        SpawnHitEffect();

        Destroy(gameObject);
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab == null)
        {
            return;
        }

        Instantiate(
            hitEffectPrefab,
            transform.position,
            Quaternion.identity
        );
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