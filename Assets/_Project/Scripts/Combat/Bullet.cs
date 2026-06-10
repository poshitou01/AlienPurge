using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 2f;

    [Header("Damage Settings")]
    [SerializeField] private int damage = 1;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;

    private Rigidbody2D rb;
    private Vector2 moveDirection = Vector2.right;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        Destroy(gameObject, lifeTime);
    }

    public void Initialize(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        moveDirection = direction.normalized;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void SetDamage(int newDamage)
    {
        damage = newDamage;
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition = rb.position + moveDirection * speed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy"))
        {
            return;
        }

        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }
        else
        {
            Debug.LogWarning(other.gameObject.name + " has Enemy Tag, but no EnemyHealth component was found.");
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

        Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
    }
}