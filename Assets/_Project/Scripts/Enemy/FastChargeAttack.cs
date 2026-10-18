
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyContactDamage))]
[RequireComponent(typeof(PooledEnemy))]
[RequireComponent(typeof(EnemyDefinition))]
public class FastChargeAttack : MonoBehaviour
{
    private enum ChargeState
    {
        Idle,
        Preparing,
        Charging,
        Recovering,
        Cooldown
    }

    // =====================================================
    // Trigger Settings
    // =====================================================

    [Header("Trigger Settings")]
    [Min(0f)]
    [SerializeField]
    private float triggerDistance = 6f;

    // =====================================================
    // Charge Timing
    // =====================================================

    [Header("Charge Timing")]
    [Min(0f)]
    [SerializeField]
    private float windupDuration = 0.65f;

    [Min(0f)]
    [SerializeField]
    private float chargeDuration = 0.45f;

    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 0.65f;

    [Min(0f)]
    [SerializeField]
    private float attackCooldown = 4f;

    [Min(0f)]
    [SerializeField]
    private float interruptedCooldown = 1f;

    // =====================================================
    // Charge Movement
    // =====================================================

    [Header("Charge Movement")]
    [Min(0f)]
    [SerializeField]
    private float chargeSpeed = 11f;

    // =====================================================
    // Phase40: Charge Environment Collision Guard
    // =====================================================

    [Header("Phase40 - Charge Environment Collision Guard")]

    [Tooltip(
        "Check solid environment obstacles before each charge step."
    )]
    [SerializeField]
    private bool preventChargeThroughWalls = false;

    [Tooltip(
        "Physics layers containing solid walls, " +
        "containers and buildings."
    )]
    [SerializeField]
    private LayerMask chargeObstacleLayers = 1 << 7;

    [Tooltip(
        "Small extra distance used to stop before a wall."
    )]
    [Min(0f)]
    [SerializeField]
    private float chargeWallSkin = 0.025f;

    private readonly RaycastHit2D[] chargeWallHits =
        new RaycastHit2D[16];

    // =====================================================
    // Temporary Warning
    // =====================================================

    [Header("Temporary Warning")]

    [Tooltip("FastChargeWarning child Transform")]
    [SerializeField]
    private Transform warningVisual;

    [Tooltip("FastChargeWarning child SpriteRenderer")]
    [SerializeField]
    private SpriteRenderer warningRenderer;

    [Min(0.01f)]
    [SerializeField]
    private float warningWidth = 0.18f;

    [SerializeField]
    private Color warningStartColor =
        new Color(1f, 0.45f, 0.1f, 0.3f);

    [SerializeField]
    private Color warningEndColor =
        new Color(1f, 0.1f, 0.05f, 0.85f);

    // =====================================================
    // Runtime State
    // =====================================================

    [Header("Runtime State")]
    [SerializeField]
    private ChargeState currentState =
        ChargeState.Idle;

    [SerializeField]
    private float stateTimeRemaining;

    [SerializeField]
    private Vector2 lockedDirection;

    [SerializeField]
    private bool hasHitPlayerThisCharge;

    // =====================================================
    // References
    // =====================================================

    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;
    private EnemyHealth enemyHealth;
    private EnemyContactDamage enemyContactDamage;
    private PooledEnemy pooledEnemy;
    private EnemyDefinition enemyDefinition;

    private Transform playerTarget;

    // Presentation observes the native state; gameplay and serialized settings are unchanged.
    // Uses the native collider cast and wall mask, matching the actual charge corridor.
    public float VisualChargeWidth => GetComponent<CircleCollider2D>().radius * 2 * Mathf.Abs(transform.lossyScale.x);
    public float VisualChargeDistance
    {
        get
        {
            float length = chargeSpeed * chargeDuration;
            if (!preventChargeThroughWalls || rb == null || chargeObstacleLayers.value == 0) return length;
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(chargeObstacleLayers);
            int count = rb.Cast(lockedDirection, filter, chargeWallHits, length + chargeWallSkin);
            for (int i=0;i<count;i++) if (chargeWallHits[i].collider != null)
                length = Mathf.Min(length, Mathf.Max(0,chargeWallHits[i].distance-chargeWallSkin));
            return length;
        }
    }
    public EnemyAttackVisualSample VisualSample =>
        !isActiveAndEnabled ? default :
        currentState == ChargeState.Preparing ? new EnemyAttackVisualSample(EnemyAttackVisualPhase.Windup, stateTimeRemaining, windupDuration, lockedDirection) :
        currentState == ChargeState.Charging ? new EnemyAttackVisualSample(EnemyAttackVisualPhase.Active, stateTimeRemaining, chargeDuration, lockedDirection) :
        currentState == ChargeState.Recovering ? new EnemyAttackVisualSample(EnemyAttackVisualPhase.Recover, stateTimeRemaining, recoveryDuration, lockedDirection) : default;


    // =====================================================
    // Unity Lifecycle
    // =====================================================

    private void Awake()
    {
        CacheComponents();
        CacheWarningReferences();
        ValidateSettings();
        ResetRuntimeState();
    }

    private void OnEnable()
    {
        CacheComponents();
        CacheWarningReferences();
        ResetRuntimeState();
    }

    private void Update()
    {
        if (ShouldCancelAttack())
        {
            CancelToIdle();
            return;
        }

        if (IsGameplayFrozen())
        {
            return;
        }

        if (enemyMovement.IsKnockedBack)
        {
            if (IsAttackInProgress())
            {
                InterruptAttack();
            }

            return;
        }

        switch (currentState)
        {
            case ChargeState.Idle:
                TryBeginPreparing();
                break;

            case ChargeState.Preparing:
                UpdatePreparing();
                break;

            case ChargeState.Charging:
                // Charging movement and timer
                // are handled in FixedUpdate.
                break;

            case ChargeState.Recovering:
                UpdateRecovering();
                break;

            case ChargeState.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    // =====================================================
    // Charge Physics Movement
    // =====================================================

    private void FixedUpdate()
    {
        if (currentState != ChargeState.Charging)
        {
            return;
        }

        if (ShouldCancelAttack())
        {
            CancelToIdle();
            return;
        }

        if (IsGameplayFrozen())
        {
            return;
        }

        if (enemyMovement.IsKnockedBack)
        {
            InterruptAttack();
            return;
        }

        float movementDistance =
            chargeSpeed * Time.fixedDeltaTime;

        // =================================================
        // Phase40:
        // Check the charge collider against Environment
        // BEFORE using Rigidbody2D.MovePosition.
        // =================================================

        if (WouldChargeHitEnvironment(movementDistance))
        {
            // Stop before the obstacle.
            // Do not teleport through or into it.
            EnterRecovering();
            return;
        }

        // =================================================
        // Original Charge Movement
        // =================================================

        Vector2 nextPosition =
            rb.position
            + lockedDirection
            * movementDistance;

        rb.MovePosition(nextPosition);

        stateTimeRemaining -=
            Time.fixedDeltaTime;

        if (stateTimeRemaining <= 0f)
        {
            EnterRecovering();
        }
    }

    // =====================================================
    // Phase40: Charge Obstacle Detection
    // =====================================================

    /// <summary>
    /// Checks whether the enemy's attached Collider2D
    /// would collide with a solid environment obstacle
    /// during the next physics charge step.
    ///
    /// Uses Rigidbody2D.Cast so detection takes the
    /// actual collider geometry into account.
    ///
    /// Trigger colliders are excluded.
    /// </summary>
    private bool WouldChargeHitEnvironment(
        float movementDistance)
    {
        if (!preventChargeThroughWalls)
        {
            return false;
        }

        if (rb == null)
        {
            return false;
        }

        if (chargeObstacleLayers.value == 0)
        {
            return false;
        }

        if (movementDistance <= 0.00001f)
        {
            return false;
        }

        if (lockedDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        ContactFilter2D collisionFilter =
            new ContactFilter2D();

        collisionFilter.SetLayerMask(
            chargeObstacleLayers
        );

        // Search only for solid obstacles.
        // Loot interaction triggers must not stop charging.
        collisionFilter.useTriggers = false;

        float castDistance =
            movementDistance + chargeWallSkin;

        int hitCount = rb.Cast(
            lockedDirection.normalized,
            collisionFilter,
            chargeWallHits,
            castDistance
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = chargeWallHits[i];

            if (hit.collider == null)
            {
                continue;
            }

            // A solid environment collider lies within
            // the next movement step plus safety margin.
            return true;
        }

        return false;
    }

    // =====================================================
    // Start Charge Preparation
    // =====================================================

    private void TryBeginPreparing()
    {
        if (enemyDefinition == null
            || !enemyDefinition.HasBeenInitialized)
        {
            return;
        }

        if (!TryFindPlayer())
        {
            return;
        }

        Vector2 directionToPlayer =
            (Vector2)playerTarget.position
            - rb.position;

        float distanceToPlayer =
            directionToPlayer.magnitude;

        if (distanceToPlayer > triggerDistance)
        {
            return;
        }

        if (directionToPlayer.sqrMagnitude
            <= 0.0001f)
        {
            directionToPlayer = Vector2.right;
        }

        lockedDirection =
            directionToPlayer.normalized;

        currentState =
            ChargeState.Preparing;

        stateTimeRemaining =
            windupDuration;

        hasHitPlayerThisCharge = false;

        enemyMovement.SetMovementLocked(true);

        DisableContactDamageForAttack();

        ShowWarning();

        if (windupDuration <= 0f)
        {
            BeginCharging();
        }
    }

    private void UpdatePreparing()
    {
        UpdateWarningVisual();

        stateTimeRemaining -= Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            BeginCharging();
        }
    }

    // =====================================================
    // Charging
    // =====================================================

    private void BeginCharging()
    {
        HideWarning();

        currentState =
            ChargeState.Charging;

        stateTimeRemaining =
            chargeDuration;

        hasHitPlayerThisCharge = false;

        if (chargeDuration <= 0f
            || chargeSpeed <= 0f)
        {
            EnterRecovering();
        }
    }

    // =====================================================
    // Recovering
    // =====================================================

    private void EnterRecovering()
    {
        currentState =
            ChargeState.Recovering;

        stateTimeRemaining =
            recoveryDuration;

        StopRigidbodyMotion();

        if (recoveryDuration <= 0f)
        {
            EnterCooldown();
        }
    }

    private void UpdateRecovering()
    {
        stateTimeRemaining -= Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            EnterCooldown();
        }
    }

    // =====================================================
    // Cooldown
    // =====================================================

    private void EnterCooldown()
    {
        currentState =
            ChargeState.Cooldown;

        stateTimeRemaining =
            attackCooldown;

        lockedDirection =
            Vector2.zero;

        hasHitPlayerThisCharge = false;

        enemyMovement.SetMovementLocked(false);

        RestoreContactDamage();

        if (attackCooldown <= 0f)
        {
            FinishCooldown();
        }
    }

    private void UpdateCooldown()
    {
        stateTimeRemaining -= Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            FinishCooldown();
        }
    }

    private void FinishCooldown()
    {
        currentState =
            ChargeState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        hasHitPlayerThisCharge = false;
    }

    // =====================================================
    // Interrupt
    // =====================================================

    private void InterruptAttack()
    {
        HideWarning();
        StopRigidbodyMotion();

        enemyMovement.SetMovementLocked(false);

        RestoreContactDamage();

        currentState =
            ChargeState.Cooldown;

        stateTimeRemaining =
            interruptedCooldown;

        lockedDirection =
            Vector2.zero;

        hasHitPlayerThisCharge = false;

        if (interruptedCooldown <= 0f)
        {
            FinishCooldown();
        }
    }

    private bool IsAttackInProgress()
    {
        return currentState
            == ChargeState.Preparing
            || currentState
            == ChargeState.Charging
            || currentState
            == ChargeState.Recovering;
    }

    // =====================================================
    // Attack Validation
    // =====================================================

    private bool ShouldCancelAttack()
    {
        if (!isActiveAndEnabled)
        {
            return true;
        }

        if (GameManager.Instance == null
            || !GameManager.Instance.IsPlaying)
        {
            return true;
        }

        if (enemyHealth == null
            || enemyHealth.IsDead)
        {
            return true;
        }

        if (pooledEnemy == null
            || pooledEnemy.IsReturned)
        {
            return true;
        }

        return false;
    }

    private bool IsGameplayFrozen()
    {
        if (PauseMenuController.IsPaused)
        {
            return true;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return true;
        }

        return Time.timeScale <= 0f;
    }

    // =====================================================
    // Player Target
    // =====================================================

    private bool TryFindPlayer()
    {
        if (playerTarget != null)
        {
            PlayerHealth cachedPlayerHealth =
                playerTarget.GetComponent<PlayerHealth>();

            if (cachedPlayerHealth != null
                && !cachedPlayerHealth.IsDead)
            {
                return true;
            }

            playerTarget = null;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject == null)
        {
            return false;
        }

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        if (playerHealth == null
            || playerHealth.IsDead)
        {
            return false;
        }

        playerTarget =
            playerObject.transform;

        return true;
    }

    // =====================================================
    // Charge Warning
    // =====================================================

    private void ShowWarning()
    {
        if (warningVisual == null)
        {
            return;
        }

        warningVisual.gameObject.SetActive(true);

        UpdateWarningVisual();
    }

    private void UpdateWarningVisual()
    {
        if (warningVisual == null)
        {
            return;
        }

        float warningLength =
            Mathf.Max(
                0.1f,
                chargeSpeed * chargeDuration
            );

        float progress = 1f;

        if (windupDuration > 0f)
        {
            progress =
                1f
                - Mathf.Clamp01(
                    stateTimeRemaining
                    / windupDuration
                );
        }

        float currentWidth =
            warningWidth
            * Mathf.Lerp(
                0.8f,
                1.15f,
                progress
            );

        float directionAngle =
            Mathf.Atan2(
                lockedDirection.y,
                lockedDirection.x
            )
            * Mathf.Rad2Deg;

        warningVisual.position =
            transform.position
            + (Vector3)(
                lockedDirection
                * warningLength
                * 0.5f
            );

        warningVisual.rotation =
            Quaternion.Euler(
                0f,
                0f,
                directionAngle
            );

        Vector3 parentWorldScale =
            transform.lossyScale;

        float safeParentScaleX =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(parentWorldScale.x)
            );

        float safeParentScaleY =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(parentWorldScale.y)
            );

        warningVisual.localScale =
            new Vector3(
                warningLength
                / safeParentScaleX,
                currentWidth
                / safeParentScaleY,
                1f
            );

        if (warningRenderer != null)
        {
            warningRenderer.color =
                Color.Lerp(
                    warningStartColor,
                    warningEndColor,
                    progress
                );
        }
    }

    private void HideWarning()
    {
        if (warningVisual != null)
        {
            warningVisual.gameObject.SetActive(
                false
            );
        }
    }

    // =====================================================
    // Contact Damage Control
    // =====================================================

    private void DisableContactDamageForAttack()
    {
        if (enemyContactDamage != null)
        {
            enemyContactDamage.enabled = false;
        }
    }

    private void RestoreContactDamage()
    {
        if (enemyContactDamage == null)
        {
            return;
        }

        if (enemyHealth != null
            && enemyHealth.IsDead)
        {
            return;
        }

        if (pooledEnemy != null
            && pooledEnemy.IsReturned)
        {
            return;
        }

        enemyContactDamage.enabled = true;
        enemyContactDamage.ResetDamageCooldown();
    }

    // =====================================================
    // Charge Player Damage
    // =====================================================

    private void TryDamagePlayer(
        GameObject targetObject)
    {
        if (currentState != ChargeState.Charging)
        {
            return;
        }

        if (hasHitPlayerThisCharge)
        {
            return;
        }

        if (targetObject == null
            || !targetObject.CompareTag("Player"))
        {
            return;
        }

        PlayerHealth playerHealth =
            targetObject.GetComponent<PlayerHealth>();

        if (playerHealth == null
            || playerHealth.IsDead)
        {
            return;
        }

        int chargeDamage = 1;

        if (enemyContactDamage != null)
        {
            chargeDamage =
                Mathf.Max(
                    1,
                    enemyContactDamage.Damage
                );
        }

        hasHitPlayerThisCharge = true;

        playerHealth.TakeDamage(
            chargeDamage
        );

        EnterRecovering();
    }

    // =====================================================
    // Physics Contact Callbacks
    // =====================================================

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        TryDamagePlayer(
            collision.gameObject
        );
    }

    private void OnCollisionStay2D(
        Collision2D collision)
    {
        TryDamagePlayer(
            collision.gameObject
        );
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        TryDamagePlayer(
            other.gameObject
        );
    }

    private void OnTriggerStay2D(
        Collider2D other)
    {
        TryDamagePlayer(
            other.gameObject
        );
    }

    // =====================================================
    // Component References
    // =====================================================

    private void CacheComponents()
    {
        if (rb == null)
        {
            rb =
                GetComponent<Rigidbody2D>();
        }

        if (enemyMovement == null)
        {
            enemyMovement =
                GetComponent<EnemyMovement>();
        }

        if (enemyHealth == null)
        {
            enemyHealth =
                GetComponent<EnemyHealth>();
        }

        if (enemyContactDamage == null)
        {
            enemyContactDamage =
                GetComponent<EnemyContactDamage>();
        }

        if (pooledEnemy == null)
        {
            pooledEnemy =
                GetComponent<PooledEnemy>();
        }

        if (enemyDefinition == null)
        {
            enemyDefinition =
                GetComponent<EnemyDefinition>();
        }
    }

    private void CacheWarningReferences()
    {
        if (warningVisual == null)
        {
            Transform foundWarning =
                transform.Find(
                    "FastChargeWarning"
                );

            if (foundWarning != null)
            {
                warningVisual =
                    foundWarning;
            }
        }

        if (warningRenderer == null
            && warningVisual != null)
        {
            warningRenderer =
                warningVisual
                    .GetComponent<SpriteRenderer>();
        }
    }

    private void StopRigidbodyMotion()
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    // =====================================================
    // Cancel
    // =====================================================

    private void CancelToIdle()
    {
        HideWarning();
        StopRigidbodyMotion();

        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(
                false
            );
        }

        RestoreContactDamage();

        currentState =
            ChargeState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        hasHitPlayerThisCharge = false;
        playerTarget = null;
    }

    // =====================================================
    // Pool Lifecycle Reset
    // =====================================================

    private void ResetRuntimeState()
    {
        HideWarning();
        StopRigidbodyMotion();

        currentState =
            ChargeState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        hasHitPlayerThisCharge = false;
        playerTarget = null;

        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(
                false
            );
        }

        RestoreContactDamage();
    }

    // =====================================================
    // Validation
    // =====================================================

    private void ValidateSettings()
    {
        triggerDistance =
            Mathf.Max(0f, triggerDistance);

        windupDuration =
            Mathf.Max(0f, windupDuration);

        chargeDuration =
            Mathf.Max(0f, chargeDuration);

        recoveryDuration =
            Mathf.Max(0f, recoveryDuration);

        attackCooldown =
            Mathf.Max(0f, attackCooldown);

        interruptedCooldown =
            Mathf.Max(0f, interruptedCooldown);

        chargeSpeed =
            Mathf.Max(0f, chargeSpeed);

        warningWidth =
            Mathf.Max(0.01f, warningWidth);

        // Phase40
        chargeWallSkin =
            Mathf.Max(0f, chargeWallSkin);
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }
}
