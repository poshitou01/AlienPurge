using UnityEngine;

[DisallowMultipleComponent]
public class EnemyContactDamage : MonoBehaviour
{
    [Header("Contact Damage Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float damageInterval = 1f;

    private float nextDamageTime;

    public int Damage => damage;
    public float DamageInterval => damageInterval;

    private void Awake()
    {
        ValidateDamageSettings();
        ResetDamageCooldown();
    }

    private void OnCollisionEnter2D(
        Collision2D collision
    )
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnCollisionStay2D(
        Collision2D collision
    )
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        TryDamagePlayer(other.gameObject);
    }

    private void OnTriggerStay2D(
        Collider2D other
    )
    {
        TryDamagePlayer(other.gameObject);
    }

    /// <summary>
    /// 在敌人生成后初始化本次敌人的接触伤害。
    ///
    /// 传入的应该是全局难度属性与敌人类型倍率
    /// 计算完成后的最终整数伤害。
    /// </summary>
    public void InitializeDamage(int newDamage)
    {
        if (newDamage <= 0)
        {
            Debug.LogWarning(
                gameObject.name
                + " 收到了无效接触伤害："
                + newDamage
                + "，已自动修正为 1。",
                this
            );

            newDamage = 1;
        }

        damage = newDamage;

        // 初始化新敌人属性时同步清除旧冷却状态。
        ResetDamageCooldown();

        Debug.Log(
            gameObject.name
            + " 接触伤害初始化完成："
            + damage,
            this
        );
    }

    /// <summary>
    /// 清除接触伤害冷却，使敌人可以从当前时间开始正常判定伤害。
    /// </summary>
    public void ResetDamageCooldown()
    {
        nextDamageTime = 0f;
    }

    private void TryDamagePlayer(GameObject target)
    {
        if (Time.time < nextDamageTime)
        {
            return;
        }

        if (!target.CompareTag("Player"))
        {
            return;
        }

        PlayerHealth playerHealth =
            target.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogWarning(
                "Enemy touched Player, but "
                + "PlayerHealth was not found.",
                this
            );

            return;
        }

        if (playerHealth.IsDead)
        {
            return;
        }

        playerHealth.TakeDamage(damage);

        nextDamageTime =
            Time.time + damageInterval;

        Debug.Log(
            gameObject.name
            + " 对 Player 造成了 "
            + damage
            + " 点接触伤害。",
            this
        );
    }

    private void ValidateDamageSettings()
    {
        damage = Mathf.Max(1, damage);

        damageInterval =
            Mathf.Max(0.01f, damageInterval);
    }

    private void OnValidate()
    {
        ValidateDamageSettings();
    }

    [ContextMenu("Test Initialize Damage To 3")]
    private void TestInitializeDamageTo3()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试"
                + "敌人接触伤害初始化。",
                this
            );

            return;
        }

        InitializeDamage(3);
    }

    [ContextMenu("Test Initialize Damage To Invalid Value")]
    private void TestInitializeDamageToInvalidValue()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试"
                + "敌人接触伤害初始化。",
                this
            );

            return;
        }

        InitializeDamage(0);
    }
}