using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 2f; // 子弹存在时间

    // 只负责子弹生成出来之后应该往哪边飞，不负责生成子弹和检测鼠标点击
    private Rigidbody2D rb;
    private Vector2 moveDirection = Vector2.right; // 子弹移动的默认值为向右

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable() // 子弹启用时开始计时销毁
    {
        Destroy(gameObject, lifeTime);
    }

    public void Initialize(Vector2 direction) // 让外部脚本告诉子弹应该往哪个方向飞
    {
        if (direction.sqrMagnitude <= 0.0001f) // 防止方向几乎为 0，比如鼠标位置和角色位置重叠
        {
            direction = Vector2.right;
        }

        moveDirection = direction.normalized; // 保留方向，但是长度变为 1

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle); // 绕 Z 轴旋转，让子弹朝向移动方向
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition = rb.position + moveDirection * speed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }
}