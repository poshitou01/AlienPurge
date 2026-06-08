using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed=12f;
    [SerializeField] private float lifeTime = 2f;//子弹存在时间
    //只负责子弹生成出来之后应该往那边飞而不是负责生成子弹和检测鼠标的点击
    private Rigidbody2D rb;
    private Vector2 moveDirection = Vector2.right;//子弹移动的默认值为向右

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()//生命周期函数
    {
        Destroy(gameObject,lifeTime);
    }

    public void Initialize(Vector2 direction)//让外部脚本告诉子弹应该往那个方向飞   即方向就是鼠标点击的方向
    {
        if (direction.sqrMagnitude <= 0.0001f)//向量长度的平方，是不是与角色重叠
        {
            direction = Vector2.right;
        }

        moveDirection = direction.normalized;//保留方向但是长度变为一

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;//mathf.atan2用来计算向量的角度，*后面的即把角度转换成弧度
        transform.rotation = Quaternion.Euler(0f, 0f, angle);//z轴旋转
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition = rb.position + moveDirection * speed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }
}