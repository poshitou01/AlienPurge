using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAbilityState))]
public class PlayerPulseSkill : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private KeyCode pulseKey = KeyCode.Q;

    [Header("Pulse Settings")]
    [Min(0.1f)]
    [SerializeField]
    private float pulseRadius = 3f;

    [Min(1)]
    [SerializeField]
    private int pulseDamage = 2;

    [Min(0f)]
    [SerializeField]
    private float knockbackDistance = 1.25f;

    [Min(0.01f)]
    [SerializeField]
    private float knockbackDuration = 0.18f;

    [Min(0.01f)]
    [SerializeField]
    private float castDuration = 0.18f;

    [Min(0f)]
    [SerializeField]
    private float pulseCooldown = 5f;

    [Header("Detection")]
    [Tooltip(
        "第一版可以设置为 Everything，"
        + "代码仍会继续筛选 EnemyHealth"
    )]
    [SerializeField]
    private LayerMask enemyLayerMask = ~0;

    [Header("Pulse Ring")]
    [SerializeField]
    private SpriteRenderer pulseVisual;

    [Range(0f, 1f)]
    [SerializeField]
    private float pulseStartAlpha = 0.45f;

    [SerializeField]
    private Color pulseColor =
        new Color(
            0.25f,
            0.9f,
            1f,
            1f
        );

    [Header("Player Visual Feedback")]
    [Tooltip(
        "释放脉冲时短暂缩放的视觉根节点，"
        + "不能绑定 Player 根对象"
    )]
    [SerializeField]
    private Transform visualRoot;

    [Range(1f, 1.3f)]
    [SerializeField]
    private float pulseScaleMultiplier = 1.1f;

    private PlayerHealth playerHealth;
    private PlayerShooting playerShooting;
    private PlayerAbilityState abilityState;

    private readonly HashSet<EnemyHealth>
        processedEnemies =
            new HashSet<EnemyHealth>();

    private Vector3 originalVisualScale;

    private float castElapsed;
    private float cooldownRemaining;

    private bool isCasting;

    public bool IsActive =>
        isCasting;

    public bool IsReady =>
        !isCasting
        && cooldownRemaining <= 0f;

    public float CooldownRemaining =>
        Mathf.Max(0f, cooldownRemaining);

    public float CooldownDuration =>
        pulseCooldown;

    public float CooldownNormalized
    {
        get
        {
            if (pulseCooldown <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                cooldownRemaining
                / pulseCooldown
            );
        }
    }

    private void Awake()
    {
        playerHealth =
            GetComponent<PlayerHealth>();

        playerShooting =
            GetComponent<PlayerShooting>();

        abilityState =
            GetComponent<PlayerAbilityState>();

        if (visualRoot == null)
        {
            visualRoot =
                transform.Find("VisualRoot");
        }

        if (visualRoot != null)
        {
            originalVisualScale =
                visualRoot.localScale;
        }
        else
        {
            originalVisualScale =
                Vector3.one;

            Debug.LogWarning(
                "PlayerPulseSkill 没有找到 "
                + "Player/VisualRoot，"
                + "玩家释放缩放反馈不会显示。",
                this
            );
        }

        castElapsed = 0f;
        cooldownRemaining = 0f;
        isCasting = false;

        HidePulseVisual();
        RestorePlayerVisual();
    }

    private void Update()
    {
        UpdateCooldown();

        if (isCasting)
        {
            if (ShouldInterruptPulse())
            {
                EndPulse();
                return;
            }

            UpdateCastingFeedback();
            return;
        }

        if (!Input.GetKeyDown(pulseKey))
        {
            return;
        }

        TryStartPulse();
    }

    private void UpdateCooldown()
    {
        if (cooldownRemaining <= 0f)
        {
            cooldownRemaining = 0f;
            return;
        }

        cooldownRemaining -=
            Time.deltaTime;

        if (cooldownRemaining < 0f)
        {
            cooldownRemaining = 0f;
        }
    }

    private void TryStartPulse()
    {
        if (cooldownRemaining > 0f)
        {
            return;
        }

        if (abilityState == null)
        {
            return;
        }

        if (!abilityState.TryEnterAbility(
                PlayerAbilityMode.Casting
            ))
        {
            return;
        }

        StartPulse();
    }

    private void StartPulse()
    {
        isCasting = true;
        castElapsed = 0f;

        cooldownRemaining =
            pulseCooldown;

        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(false);
        }

        ShowPulseVisual();
        ApplyPlayerVisualPunch();
        ApplyPulseEffect();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayPlayerPulse();
        }

        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance
                .PlayHeavyShake();
        }
    }

    private void ApplyPulseEffect()
    {
        processedEnemies.Clear();

        Collider2D[] detectedColliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                pulseRadius,
                enemyLayerMask
            );

        for (int i = 0;
             i < detectedColliders.Length;
             i++)
        {
            Collider2D detectedCollider =
                detectedColliders[i];

            if (detectedCollider == null)
            {
                continue;
            }

            EnemyHealth enemyHealth =
                detectedCollider
                    .GetComponentInParent<
                        EnemyHealth
                    >();

            if (enemyHealth == null
                || enemyHealth.IsDead)
            {
                continue;
            }

            // 一个敌人可能具有多个 Collider。
            // 同一次技能只允许处理一次。
            if (!processedEnemies.Add(
                    enemyHealth
                ))
            {
                continue;
            }

            Vector3 enemyPosition =
                enemyHealth.transform.position;

            // 在伤害可能导致敌人死亡回池前，
            // 先记录并播放命中特效。
            SpawnHitEffect(enemyPosition);

            Vector2 knockbackDirection =
                (Vector2)enemyPosition
                - (Vector2)transform.position;

            if (knockbackDirection.sqrMagnitude
                <= 0.0001f)
            {
                knockbackDirection =
                    Random.insideUnitCircle;

                if (knockbackDirection.sqrMagnitude
                    <= 0.0001f)
                {
                    knockbackDirection =
                        Vector2.right;
                }
            }

            EnemyMovement enemyMovement =
                enemyHealth.GetComponent<
                    EnemyMovement
                >();

            if (enemyMovement != null)
            {
                enemyMovement.ApplyKnockback(
                    knockbackDirection,
                    knockbackDistance,
                    knockbackDuration
                );
            }

            enemyHealth.TakeDamage(
                pulseDamage
            );
        }

        Debug.Log(
            "Pulse Skill hit "
            + processedEnemies.Count
            + " enemies.",
            this
        );
    }

    private void SpawnHitEffect(
        Vector3 spawnPosition
    )
    {
        if (HitEffectPool.Instance == null)
        {
            return;
        }

        HitEffect hitEffect =
            HitEffectPool.Instance.GetHitEffect(
                spawnPosition,
                Quaternion.identity
            );

        if (hitEffect == null)
        {
            return;
        }

        hitEffect.Initialize();
    }

    private void ShowPulseVisual()
    {
        if (pulseVisual == null)
        {
            return;
        }

        pulseVisual.gameObject.SetActive(true);

        pulseVisual.transform.localScale =
            Vector3.zero;

        Color color = pulseColor;
        color.a = pulseStartAlpha;

        pulseVisual.color = color;
    }

    private void UpdateCastingFeedback()
    {
        castElapsed += Time.deltaTime;

        float progress =
            Mathf.Clamp01(
                castElapsed / castDuration
            );

        UpdatePulseScale(progress);
        UpdatePulseAlpha(progress);
        UpdatePlayerVisual(progress);

        if (progress >= 1f)
        {
            EndPulse();
        }
    }

    private void UpdatePulseScale(
        float progress
    )
    {
        if (pulseVisual == null)
        {
            return;
        }

        float spriteDiameter = 1f;

        if (pulseVisual.sprite != null)
        {
            spriteDiameter =
                pulseVisual.sprite
                    .bounds.size.x;
        }

        if (spriteDiameter <= 0.0001f)
        {
            spriteDiameter = 1f;
        }

        float targetScale =
            pulseRadius
            * 2f
            / spriteDiameter;

        // 释放初段稍快、末段减速，
        // 比完全线性扩张更有冲击感。
        float easedProgress =
            1f
            - Mathf.Pow(
                1f - progress,
                2f
            );

        float currentScale =
            Mathf.Lerp(
                0f,
                targetScale,
                easedProgress
            );

        pulseVisual.transform.localScale =
            new Vector3(
                currentScale,
                currentScale,
                1f
            );
    }

    private void UpdatePulseAlpha(
        float progress
    )
    {
        if (pulseVisual == null)
        {
            return;
        }

        Color color =
            pulseVisual.color;

        color.a =
            Mathf.Lerp(
                pulseStartAlpha,
                0f,
                progress
            );

        pulseVisual.color = color;
    }

    private void ApplyPlayerVisualPunch()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localScale =
            originalVisualScale
            * pulseScaleMultiplier;
    }

    private void UpdatePlayerVisual(
        float progress
    )
    {
        if (visualRoot == null)
        {
            return;
        }

        if (playerHealth != null
            && playerHealth.IsDead)
        {
            return;
        }

        visualRoot.localScale =
            Vector3.Lerp(
                originalVisualScale
                    * pulseScaleMultiplier,
                originalVisualScale,
                progress
            );
    }

    private bool ShouldInterruptPulse()
    {
        if (abilityState == null)
        {
            return true;
        }

        if (!abilityState.IsCasting)
        {
            return true;
        }

        if (!abilityState
            .IsGameplayAbilityAllowed())
        {
            return true;
        }

        return false;
    }

    private void EndPulse()
    {
        bool wasCasting =
            isCasting;

        isCasting = false;
        castElapsed = 0f;

        HidePulseVisual();
        RestorePlayerVisual();

        if (wasCasting
            && abilityState != null)
        {
            abilityState.ExitAbility(
                PlayerAbilityMode.Casting
            );
        }

        RestoreShooting();
    }

    private void RestorePlayerVisual()
    {
        if (visualRoot == null)
        {
            return;
        }

        // PlayerHealth 死亡时已经设置了死亡缩放。
        // 这里不能将死亡缩放重置为普通尺寸。
        if (playerHealth != null
            && playerHealth.IsDead)
        {
            return;
        }

        visualRoot.localScale =
            originalVisualScale;
    }

    private void RestoreShooting()
    {
        if (playerHealth != null
            && playerHealth.IsDead)
        {
            return;
        }

        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(true);
        }
    }

    private void HidePulseVisual()
    {
        if (pulseVisual == null)
        {
            return;
        }

        pulseVisual.transform.localScale =
            Vector3.zero;

        pulseVisual.gameObject.SetActive(
            false
        );
    }

    private void OnDisable()
    {
        EndPulse();
    }

    private void OnValidate()
    {
        pulseRadius =
            Mathf.Max(0.1f, pulseRadius);

        pulseDamage =
            Mathf.Max(1, pulseDamage);

        knockbackDistance =
            Mathf.Max(
                0f,
                knockbackDistance
            );

        knockbackDuration =
            Mathf.Max(
                0.01f,
                knockbackDuration
            );

        castDuration =
            Mathf.Max(
                0.01f,
                castDuration
            );

        pulseCooldown =
            Mathf.Max(
                0f,
                pulseCooldown
            );

        pulseStartAlpha =
            Mathf.Clamp01(
                pulseStartAlpha
            );

        pulseScaleMultiplier =
            Mathf.Clamp(
                pulseScaleMultiplier,
                1f,
                1.3f
            );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            pulseRadius
        );
    }
}