using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAbilityState))]
public class PlayerDash : MonoBehaviour
{
    private const float DirectionTolerance =
        0.0001f;

    [Header("Input")]
    [SerializeField]
    private KeyCode dashKey =
        KeyCode.LeftShift;

    [Header("Dash Settings")]
    [Min(0.01f)]
    [SerializeField]
    private float dashSpeed = 16f;

    [Min(0.01f)]
    [SerializeField]
    private float dashDuration = 0.16f;

    [Min(0f)]
    [SerializeField]
    private float dashCooldown = 1.2f;

    [Header("Optional Invulnerability")]
    [SerializeField]
    private bool grantInvulnerability = false;

    [Min(0f)]
    [SerializeField]
    private float invulnerabilityDuration = 0.12f;

    [Header("Visual Feedback")]
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private TrailRenderer dashTrail;

    [Range(0f, 0.5f)]
    [SerializeField]
    private float stretchAmount = 0.12f;

    [Range(0f, 0.5f)]
    [SerializeField]
    private float compressionAmount = 0.06f;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private PlayerHealth playerHealth;
    private PlayerAbilityState abilityState;

    private Camera mainCamera;

    private Vector2 dashDirection;
    private Vector3 originalVisualScale;

    private float dashTimeRemaining;
    private float cooldownRemaining;

    private bool isDashing;

    public bool IsActive =>
        isDashing;

    public bool IsReady =>
        !isDashing
        && cooldownRemaining <= 0f;

    public float CooldownRemaining =>
        Mathf.Max(0f, cooldownRemaining);

    public float CooldownDuration =>
        dashCooldown;

    public float CooldownNormalized
    {
        get
        {
            if (dashCooldown <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                cooldownRemaining
                / dashCooldown
            );
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        playerMovement =
            GetComponent<PlayerMovement>();

        playerShooting =
            GetComponent<PlayerShooting>();

        playerHealth =
            GetComponent<PlayerHealth>();

        abilityState =
            GetComponent<PlayerAbilityState>();

        mainCamera = Camera.main;

        if (visualRoot == null)
        {
            visualRoot =
                transform.Find("VisualRoot");
        }

        if (dashTrail == null
            && visualRoot != null)
        {
            dashTrail =
                visualRoot.GetComponent<
                    TrailRenderer
                >();
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
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.Clear();
        }

        dashDirection = Vector2.zero;
        dashTimeRemaining = 0f;
        cooldownRemaining = 0f;
        isDashing = false;
    }

    private void Update()
    {
        UpdateCooldown();

        if (isDashing)
        {
            CheckDashInterruption();
            return;
        }

        if (!Input.GetKeyDown(dashKey))
        {
            return;
        }

        TryStartDash();
    }

    private void FixedUpdate()
    {
        if (!isDashing)
        {
            return;
        }

        if (abilityState == null
            || !abilityState.IsDashing)
        {
            EndDash(true);
            return;
        }

        Vector2 targetPosition =
            rb.position
            + dashDirection
            * dashSpeed
            * Time.fixedDeltaTime;

        rb.MovePosition(targetPosition);

        dashTimeRemaining -=
            Time.fixedDeltaTime;

        if (dashTimeRemaining <= 0f)
        {
            EndDash(false);
        }
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

    private void TryStartDash()
    {
        if (cooldownRemaining > 0f)
        {
            return;
        }

        if (abilityState == null)
        {
            return;
        }

        Vector2 requestedDirection =
            GetRequestedDashDirection();

        if (requestedDirection.sqrMagnitude
            <= DirectionTolerance)
        {
            return;
        }

        if (!abilityState.TryEnterAbility(
                PlayerAbilityMode.Dashing
            ))
        {
            return;
        }

        StartDash(requestedDirection);
    }

    private Vector2 GetRequestedDashDirection()
    {
        Vector2 currentInput =
            new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

        if (currentInput.sqrMagnitude
            > DirectionTolerance)
        {
            return currentInput.normalized;
        }

        if (playerMovement != null
            && playerMovement
                .LastNonZeroMoveDirection
                .sqrMagnitude
                > DirectionTolerance)
        {
            return playerMovement
                .LastNonZeroMoveDirection
                .normalized;
        }

        return GetMouseDirection();
    }

    private Vector2 GetMouseDirection()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return Vector2.zero;
        }

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(
                Input.mousePosition
            );

        mouseWorldPosition.z = 0f;

        Vector2 direction =
            (Vector2)mouseWorldPosition
            - rb.position;

        if (direction.sqrMagnitude
            <= DirectionTolerance)
        {
            return Vector2.zero;
        }

        return direction.normalized;
    }

    private void StartDash(
        Vector2 requestedDirection
    )
    {
        dashDirection =
            requestedDirection.normalized;

        dashTimeRemaining =
            dashDuration;

        cooldownRemaining =
            dashCooldown;

        isDashing = true;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(false);
        }

        if (grantInvulnerability
            && playerHealth != null)
        {
            playerHealth
                .BeginTemporaryInvulnerability(
                    invulnerabilityDuration
                );
        }

        PlayDashFeedback();
    }

    private void PlayDashFeedback()
    {
        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
        }

        ApplyDashStretch();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayPlayerDash();
        }

        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance
                .PlayLightShake();
        }
    }

    private void ApplyDashStretch()
    {
        if (visualRoot == null)
        {
            return;
        }

        float horizontalInfluence =
            Mathf.Abs(dashDirection.x);

        float verticalInfluence =
            Mathf.Abs(dashDirection.y);

        float scaleX =
            1f
            + horizontalInfluence
            * stretchAmount
            - verticalInfluence
            * compressionAmount;

        float scaleY =
            1f
            + verticalInfluence
            * stretchAmount
            - horizontalInfluence
            * compressionAmount;

        Vector3 dashScale =
            new Vector3(
                scaleX,
                scaleY,
                1f
            );

        visualRoot.localScale =
            Vector3.Scale(
                originalVisualScale,
                dashScale
            );
    }

    private void CheckDashInterruption()
    {
        if (abilityState == null)
        {
            EndDash(true);
            return;
        }

        if (!abilityState.IsDashing)
        {
            EndDash(true);
            return;
        }

        if (!abilityState
            .IsGameplayAbilityAllowed())
        {
            EndDash(true);
        }
    }

    private void EndDash(
        bool clearTrailImmediately
    )
    {
        bool wasDashing = isDashing;

        isDashing = false;
        dashTimeRemaining = 0f;
        dashDirection = Vector2.zero;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (wasDashing
            && abilityState != null)
        {
            abilityState.ExitAbility(
                PlayerAbilityMode.Dashing
            );
        }

        if (playerHealth != null)
        {
            playerHealth
                .CancelTemporaryInvulnerability();
        }

        StopDashFeedback(
            clearTrailImmediately
        );

        if (wasDashing)
        {
            RestoreNormalControls();
        }
    }

    private void StopDashFeedback(
        bool clearTrailImmediately
    )
    {
        if (dashTrail != null)
        {
            dashTrail.emitting = false;

            if (clearTrailImmediately)
            {
                dashTrail.Clear();
            }
        }

        if (visualRoot != null
            && (playerHealth == null
                || !playerHealth.IsDead))
        {
            visualRoot.localScale =
                originalVisualScale;
        }
    }

    private void RestoreNormalControls()
    {
        if (playerHealth != null
            && playerHealth.IsDead)
        {
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }

        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(true);
        }
    }

    private void OnDisable()
    {
        EndDash(true);
    }

    private void OnValidate()
    {
        dashSpeed =
            Mathf.Max(0.01f, dashSpeed);

        dashDuration =
            Mathf.Max(0.01f, dashDuration);

        dashCooldown =
            Mathf.Max(0f, dashCooldown);

        invulnerabilityDuration =
            Mathf.Max(
                0f,
                invulnerabilityDuration
            );

        stretchAmount =
            Mathf.Clamp(
                stretchAmount,
                0f,
                0.5f
            );

        compressionAmount =
            Mathf.Clamp(
                compressionAmount,
                0f,
                0.5f
            );
    }
}