using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAbilityState))]
public class PlayerJetJump : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private KeyCode jumpKey = KeyCode.Space;

    [Header("Jump Settings")]
    [Min(0.01f)]
    [SerializeField]
    private float jumpDuration = 0.75f;

    [Min(0f)]
    [SerializeField]
    private float jumpHeight = 0.5f;

    [Min(0f)]
    [SerializeField]
    private float jumpCooldown = 2.5f;

    [Header("Visual References")]
    [Tooltip("只负责玩家身体视觉升降，不包含物理组件")]
    [SerializeField]
    private Transform visualRoot;

    [Tooltip("保持在地面上的阴影对象")]
    [SerializeField]
    private Transform shadowTransform;

    [Tooltip("用于控制阴影透明度")]
    [SerializeField]
    private SpriteRenderer shadowRenderer;

    [Header("Shadow At Jump Peak")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float shadowScaleMultiplier = 0.55f;

    [Range(0f, 1f)]
    [SerializeField]
    private float shadowAlphaMultiplier = 0.35f;

    [Header("Landing Feedback")]
    [SerializeField]
    private bool playLandingCameraShake = true;

    private PlayerHealth playerHealth;
    private PlayerAbilityState abilityState;

    private Vector3 originalVisualLocalPosition;
    private Vector3 originalShadowLocalScale;
    private Color originalShadowColor;

    private float jumpElapsed;
    private float cooldownRemaining;

    private bool isJumping;

    public bool IsActive => isJumping;

    public bool IsReady =>
        !isJumping
        && cooldownRemaining <= 0f;

    public float CooldownRemaining =>
        Mathf.Max(0f, cooldownRemaining);

    public float CooldownDuration =>
        jumpCooldown;

    /// <summary>
    /// 冷却开始时为 1，冷却完成时为 0。
    /// 后续由 PlayerAbilityHUD 读取。
    /// </summary>
    public float CooldownNormalized
    {
        get
        {
            if (jumpCooldown <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                cooldownRemaining
                / jumpCooldown
            );
        }
    }

    private void Awake()
    {
        playerHealth =
            GetComponent<PlayerHealth>();

        abilityState =
            GetComponent<PlayerAbilityState>();

        if (visualRoot == null)
        {
            visualRoot =
                transform.Find("VisualRoot");
        }

        if (shadowTransform == null)
        {
            shadowTransform =
                transform.Find("Shadow");
        }

        if (shadowRenderer == null
            && shadowTransform != null)
        {
            shadowRenderer =
                shadowTransform.GetComponent<
                    SpriteRenderer
                >();
        }

        if (visualRoot != null)
        {
            originalVisualLocalPosition =
                visualRoot.localPosition;
        }

        if (shadowTransform != null)
        {
            originalShadowLocalScale =
                shadowTransform.localScale;
        }

        if (shadowRenderer != null)
        {
            originalShadowColor =
                shadowRenderer.color;
        }

        jumpElapsed = 0f;
        cooldownRemaining = 0f;
        isJumping = false;

        RestoreVisualState();
    }

    private void Update()
    {
        UpdateCooldown();

        if (isJumping)
        {
            if (ShouldInterruptJump())
            {
                EndJump(true);
                return;
            }

            UpdateJumpVisual();
            return;
        }

        if (!Input.GetKeyDown(jumpKey))
        {
            return;
        }

        TryStartJump();
    }

    private void UpdateCooldown()
    {
        if (cooldownRemaining <= 0f)
        {
            cooldownRemaining = 0f;
            return;
        }

        cooldownRemaining -= Time.deltaTime;

        if (cooldownRemaining < 0f)
        {
            cooldownRemaining = 0f;
        }
    }

    private void TryStartJump()
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
                PlayerAbilityMode.Airborne
            ))
        {
            return;
        }

        StartJump();
    }

    private void StartJump()
    {
        isJumping = true;
        jumpElapsed = 0f;

        cooldownRemaining =
            jumpCooldown;

        RestoreVisualState();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayPlayerJetJump();
        }
    }

    private void UpdateJumpVisual()
    {
        if (jumpDuration <= 0f)
        {
            EndJump(false);
            return;
        }

        jumpElapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(
            jumpElapsed / jumpDuration
        );

        // 简单抛物线：
        // 0 时高度为 0；
        // 0.5 时达到最高点；
        // 1 时重新回到 0。
        float airborneFactor =
            4f
            * progress
            * (1f - progress);

        UpdateVisualRoot(airborneFactor);
        UpdateShadow(airborneFactor);

        if (progress >= 1f)
        {
            EndJump(false);
        }
    }

    private void UpdateVisualRoot(
        float airborneFactor
    )
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 localPosition =
            originalVisualLocalPosition;

        localPosition.y +=
            jumpHeight * airborneFactor;

        visualRoot.localPosition =
            localPosition;
    }

    private void UpdateShadow(
        float airborneFactor
    )
    {
        if (shadowTransform != null)
        {
            Vector3 peakScale =
                originalShadowLocalScale
                * shadowScaleMultiplier;

            shadowTransform.localScale =
                Vector3.Lerp(
                    originalShadowLocalScale,
                    peakScale,
                    airborneFactor
                );
        }

        if (shadowRenderer != null)
        {
            Color shadowColor =
                originalShadowColor;

            float peakAlpha =
                originalShadowColor.a
                * shadowAlphaMultiplier;

            shadowColor.a =
                Mathf.Lerp(
                    originalShadowColor.a,
                    peakAlpha,
                    airborneFactor
                );

            shadowRenderer.color =
                shadowColor;
        }
    }

    private bool ShouldInterruptJump()
    {
        if (abilityState == null)
        {
            return true;
        }

        if (!abilityState.IsAirborne)
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

    private void EndJump(bool interrupted)
    {
        bool wasJumping = isJumping;

        isJumping = false;
        jumpElapsed = 0f;

        RestoreVisualState();

        if (wasJumping
            && abilityState != null)
        {
            abilityState.ExitAbility(
                PlayerAbilityMode.Airborne
            );
        }

        if (!interrupted
            && wasJumping
            && playLandingCameraShake
            && (playerHealth == null
                || !playerHealth.IsDead)
            && CameraFollow.Instance != null)
        {
            CameraFollow.Instance
                .PlayLightShake();
        }
    }

    private void RestoreVisualState()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition =
                originalVisualLocalPosition;
        }

        if (shadowTransform != null)
        {
            shadowTransform.localScale =
                originalShadowLocalScale;
        }

        if (shadowRenderer != null)
        {
            shadowRenderer.color =
                originalShadowColor;
        }
    }

    private void OnDisable()
    {
        EndJump(true);
    }

    private void OnValidate()
    {
        jumpDuration =
            Mathf.Max(0.01f, jumpDuration);

        jumpHeight =
            Mathf.Max(0f, jumpHeight);

        jumpCooldown =
            Mathf.Max(0f, jumpCooldown);

        shadowScaleMultiplier =
            Mathf.Clamp(
                shadowScaleMultiplier,
                0.1f,
                1f
            );

        shadowAlphaMultiplier =
            Mathf.Clamp01(
                shadowAlphaMultiplier
            );
    }
}