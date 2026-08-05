using UnityEngine;

/// <summary>
/// 临时开发测试区域。
/// 玩家处于区域内且位于地面时周期性受到伤害；
/// 处于 Airborne 状态时忽略地面伤害。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class GroundDamageTestZone : MonoBehaviour
{
    [Header("Ground Damage Test")]
    [Min(1)]
    [SerializeField]
    private int damage = 1;

    [Min(0.05f)]
    [SerializeField]
    private float damageInterval = 0.75f;

    private Collider2D triggerCollider;

    private PlayerHealth playerInside;
    private PlayerAbilityState abilityStateInside;
    private Collider2D playerColliderInside;

    private float nextDamageTime;

    private void Awake()
    {
        triggerCollider =
            GetComponent<Collider2D>();

        if (triggerCollider == null)
        {
            Debug.LogError(
                "GroundDamageTestZone 缺少 Collider2D。",
                this
            );

            enabled = false;
            return;
        }

        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                "GroundDamageTestZone 的 Collider2D "
                + "不是 Trigger，已自动设为 Trigger。",
                this
            );

            triggerCollider.isTrigger = true;
        }

        ClearTrackedPlayer();
    }

    private void Update()
    {
        if (playerInside == null)
        {
            return;
        }

        if (playerInside.IsDead)
        {
            ClearTrackedPlayer();
            return;
        }

        if (Time.time < nextDamageTime)
        {
            return;
        }

        // 跃迁只免疫地面类型伤害。
        if (abilityStateInside != null
            && abilityStateInside.IsAirborne)
        {
            return;
        }

        playerInside.TakeDamage(damage);

        nextDamageTime =
            Time.time + damageInterval;
    }

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        PlayerHealth detectedPlayer =
            other.GetComponentInParent<
                PlayerHealth
            >();

        if (detectedPlayer == null)
        {
            return;
        }

        playerInside = detectedPlayer;

        abilityStateInside =
            detectedPlayer.GetComponent<
                PlayerAbilityState
            >();

        playerColliderInside = other;

        // 进入区域后允许立即进行第一次伤害判定。
        nextDamageTime = 0f;
    }

    private void OnTriggerExit2D(
        Collider2D other
    )
    {
        if (other != playerColliderInside)
        {
            return;
        }

        ClearTrackedPlayer();
    }

    private void ClearTrackedPlayer()
    {
        playerInside = null;
        abilityStateInside = null;
        playerColliderInside = null;
        nextDamageTime = 0f;
    }

    private void OnDisable()
    {
        ClearTrackedPlayer();
    }

    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);

        damageInterval =
            Mathf.Max(0.05f, damageInterval);
    }
}