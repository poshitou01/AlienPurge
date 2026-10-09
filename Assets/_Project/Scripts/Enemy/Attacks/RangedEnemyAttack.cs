
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyContactDamage))]
[RequireComponent(typeof(PooledEnemy))]
[RequireComponent(typeof(EnemyDefinition))]
public class RangedEnemyAttack : MonoBehaviour
{
    private enum RangedAttackState
    {
        Idle,
        Preparing,
        Recovering,
        Cooldown
    }

    // =========================================================
    // Original: Trigger Settings
    // =========================================================

    [Header("Trigger Settings")]
    [Min(0f)]
    [SerializeField]
    private float triggerDistance = 7f;

    // =========================================================
    // Phase40: Line of Sight
    // =========================================================

    [Header("Phase40 - Line of Sight")]

    [Tooltip(
        "Require an unobstructed line of sight " +
        "before aiming and before firing."
    )]
    [SerializeField]
    private bool requireClearLineOfSight = false;

    [Tooltip(
        "Solid world obstacles that block ranged attacks. " +
        "Select Environment."
    )]
    [SerializeField]
    private LayerMask lineOfSightBlockingLayers = 1 << 7;

    [Tooltip(
        "Draw green rays for clear sight and red rays " +
        "for blocked sight in the Scene view."
    )]
    [SerializeField]
    private bool drawLineOfSightDebug = false;

    private readonly RaycastHit2D[] lineOfSightHits =
        new RaycastHit2D[16];

    // =========================================================
    // Original: Attack Timing
    // =========================================================

    [Header("Attack Timing")]
    [Min(0f)]
    [SerializeField]
    private float windupDuration = 0.85f;

    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 0.4f;

    [Min(0f)]
    [SerializeField]
    private float attackCooldown = 2.6f;

    [Min(0f)]
    [SerializeField]
    private float interruptedCooldown = 1.1f;

    // =========================================================
    // Original: Projectile Settings
    // =========================================================

    [Header("Projectile Settings")]
    [Min(0f)]
    [SerializeField]
    private float projectileSpawnOffset = 0.5f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileSpeed = 7f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileLifeTime = 3f;

    [Min(0.01f)]
    [SerializeField]
    private float projectileScaleMultiplier = 1f;

    // =========================================================
    // Original: Temporary Aim Warning
    // =========================================================

    [Header("Temporary Aim Warning")]

    [Tooltip("RangedAimWarning child LineRenderer")]
    [SerializeField]
    private LineRenderer warningLine;

    [Min(0.1f)]
    [SerializeField]
    private float warningLength = 7f;

    [Min(0.001f)]
    [SerializeField]
    private float warningStartWidth = 0.035f;

    [Min(0.001f)]
    [SerializeField]
    private float warningEndWidth = 0.11f;

    [SerializeField]
    private Color warningStartColor =
        new Color(
            0.75f,
            0.2f,
            1f,
            0.18f
        );

    [SerializeField]
    private Color warningEndColor =
        new Color(
            1f,
            0.05f,
            0.35f,
            0.82f
        );

    // =========================================================
    // Original: Runtime State
    // =========================================================

    [Header("Runtime State")]
    [SerializeField]
    private RangedAttackState currentState =
        RangedAttackState.Idle;

    [SerializeField]
    private float stateTimeRemaining;

    [SerializeField]
    private Vector2 lockedDirection;

    // =========================================================
    // References
    // =========================================================

    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;
    private EnemyHealth enemyHealth;
    private EnemyContactDamage enemyContactDamage;
    private PooledEnemy pooledEnemy;
    private EnemyDefinition enemyDefinition;

    private Transform playerTarget;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        CacheComponents();
        CacheWarningReference();
        ValidateSettings();
        ResetRuntimeState();
    }

    private void OnEnable()
    {
        CacheComponents();
        CacheWarningReference();
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
            if (currentState == RangedAttackState.Preparing
                || currentState == RangedAttackState.Recovering)
            {
                InterruptAttack();
            }

            return;
        }

        switch (currentState)
        {
            case RangedAttackState.Idle:
                TryBeginPreparing();
                break;

            case RangedAttackState.Preparing:
                UpdatePreparing();
                break;

            case RangedAttackState.Recovering:
                UpdateRecovering();
                break;

            case RangedAttackState.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    // =========================================================
    // Original: Begin Attack
    // Phase40: Check LOS before locking movement
    // =========================================================

    private void TryBeginPreparing()
    {
        if (enemyDefinition == null
            || !enemyDefinition.HasBeenInitialized)
        {
            return;
        }

        if (EnemyProjectilePool.Instance == null)
        {
            return;
        }

        if (!TryFindPlayer())
        {
            return;
        }

        Vector2 directionToPlayer =
            (Vector2)playerTarget.position - rb.position;

        if (directionToPlayer.magnitude > triggerDistance)
        {
            return;
        }

        // Phase40:
        // If a solid Environment collider blocks the player,
        // stay Idle so EnemyMovement continues A* chasing.
        if (!HasClearLineOfSightToPlayer())
        {
            return;
        }

        if (directionToPlayer.sqrMagnitude <= 0.0001f)
        {
            directionToPlayer = Vector2.right;
        }

        lockedDirection = directionToPlayer.normalized;

        currentState = RangedAttackState.Preparing;
        stateTimeRemaining = windupDuration;

        enemyMovement.SetMovementLocked(true);

        StopRigidbodyMotion();
        ShowWarning();

        if (windupDuration <= 0f)
        {
            FireProjectile();
        }
    }

    // =========================================================
    // Original: Windup
    // Phase40: Cancel if player moves behind cover
    // =========================================================

    private void UpdatePreparing()
    {
        if (requireClearLineOfSight)
        {
            if (!TryFindPlayer()
                || !HasClearLineOfSightToPlayer())
            {
                CancelBlockedPreparation();
                return;
            }
        }

        UpdateWarningVisual();

        stateTimeRemaining -= Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            FireProjectile();
        }
    }

    // =========================================================
    // Original: Projectile Fire
    // Phase40: Final LOS and locked-ray validation
    // =========================================================

    private void FireProjectile()
    {
        if (requireClearLineOfSight)
        {
            if (!TryFindPlayer()
                || !HasClearLineOfSightToPlayer()
                || !HasClearLockedShotPath())
            {
                CancelBlockedPreparation();
                return;
            }
        }

        HideWarning();

        Vector3 spawnPosition =
            transform.position
            + (Vector3)(
                lockedDirection * projectileSpawnOffset
            );

        EnemyProjectile projectile =
            EnemyProjectilePool.Instance.GetProjectile(
                spawnPosition,
                Quaternion.identity
            );

        if (projectile != null)
        {
            int projectileDamage = 1;

            if (enemyContactDamage != null)
            {
                projectileDamage = Mathf.Max(
                    1,
                    enemyContactDamage.Damage
                );
            }

            projectile.Initialize(
                lockedDirection,
                projectileSpeed,
                projectileDamage,
                projectileLifeTime,
                projectileScaleMultiplier
            );
        }

        EnterRecovering();
    }

    // =========================================================
    // Phase40: Line of Sight Detection
    // =========================================================

    /// <summary>
    /// Returns true when no solid Environment obstacle
    /// exists between this enemy and the current player.
    /// </summary>
    private bool HasClearLineOfSightToPlayer()
    {
        if (!requireClearLineOfSight)
        {
            return true;
        }

        if (rb == null || playerTarget == null)
        {
            return false;
        }

        Vector2 origin = rb.position;

        Vector2 toPlayer =
            (Vector2)playerTarget.position - origin;

        float distance = toPlayer.magnitude;

        if (distance <= 0.0001f)
        {
            return true;
        }

        return !IsEnvironmentBlockingRay(
            origin,
            toPlayer,
            distance
        );
    }

    /// <summary>
    /// Checks the original locked firing direction.
    /// Prevents a projectile from being spawned beyond
    /// nearby cover even if the player moved during windup.
    /// </summary>
    private bool HasClearLockedShotPath()
    {
        if (!requireClearLineOfSight)
        {
            return true;
        }

        if (rb == null || playerTarget == null)
        {
            return false;
        }

        Vector2 toPlayer =
            (Vector2)playerTarget.position - rb.position;

        float forwardDistance =
            Vector2.Dot(toPlayer, lockedDirection);

        if (forwardDistance <= 0.0001f)
        {
            return false;
        }

        float checkDistance = Mathf.Max(
            projectileSpawnOffset,
            forwardDistance
        );

        return !IsEnvironmentBlockingRay(
            rb.position,
            lockedDirection,
            checkDistance
        );
    }

    /// <summary>
    /// Uses a non-allocating Physics2D.Raycast query
    /// with a ContactFilter2D.
    ///
    /// Only the configured blocking layers count.
    /// Trigger colliders and this enemy's own collider
    /// are ignored.
    /// </summary>
    private bool IsEnvironmentBlockingRay(
        Vector2 origin,
        Vector2 direction,
        float distance)
    {
        // Fail closed for a missing LayerMask.
        // This prevents accidentally shooting through
        // every wall due to an empty Inspector mask.
        if (lineOfSightBlockingLayers.value == 0)
        {
            return true;
        }

        if (distance <= 0f
            || direction.sqrMagnitude <= 0.00000001f)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();

        filter.SetLayerMask(lineOfSightBlockingLayers);

        // Loot/search Trigger colliders should not
        // block the enemy's line of sight.
        filter.useTriggers = false;

        Vector2 rayDirection = direction.normalized;

        int hitCount = Physics2D.Raycast(
            origin,
            rayDirection,
            filter,
            lineOfSightHits,
            distance
        );

        bool blocked = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider =
                lineOfSightHits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            if (hitCollider.isTrigger)
            {
                continue;
            }

            // Ignore this enemy's own rigidbody colliders.
            if (hitCollider.attachedRigidbody == rb)
            {
                continue;
            }

            blocked = true;
            break;
        }

        if (drawLineOfSightDebug)
        {
            Debug.DrawLine(
                origin,
                origin + rayDirection * distance,
                blocked ? Color.red : Color.green,
                0.12f
            );
        }

        return blocked;
    }

    // =========================================================
    // Phase40: Cancel Attack When Sight Is Blocked
    // =========================================================

    private void CancelBlockedPreparation()
    {
        HideWarning();
        StopRigidbodyMotion();

        currentState = RangedAttackState.Idle;
        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;

        // Important:
        // Restore normal A* movement immediately.
        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(false);
        }
    }

    // =========================================================
    // Original: Recovering
    // =========================================================

    private void EnterRecovering()
    {
        currentState = RangedAttackState.Recovering;
        stateTimeRemaining = recoveryDuration;

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

    // =========================================================
    // Original: Cooldown
    // =========================================================

    private void EnterCooldown()
    {
        currentState = RangedAttackState.Cooldown;
        stateTimeRemaining = attackCooldown;

        lockedDirection = Vector2.zero;

        enemyMovement.SetMovementLocked(false);

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
        currentState = RangedAttackState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
    }

    // =========================================================
    // Original: Attack Interrupted By Knockback
    // =========================================================

    private void InterruptAttack()
    {
        HideWarning();
        StopRigidbodyMotion();

        enemyMovement.SetMovementLocked(false);

        currentState = RangedAttackState.Cooldown;
        stateTimeRemaining = interruptedCooldown;

        lockedDirection = Vector2.zero;

        if (interruptedCooldown <= 0f)
        {
            FinishCooldown();
        }
    }

    // =========================================================
    // Original: Attack State Validation
    // =========================================================

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

        if (enemyHealth == null || enemyHealth.IsDead)
        {
            return true;
        }

        if (pooledEnemy == null || pooledEnemy.IsReturned)
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

    // =========================================================
    // Original: Find Player
    // =========================================================

    private bool TryFindPlayer()
    {
        if (playerTarget != null)
        {
            PlayerHealth cachedHealth =
                playerTarget.GetComponent<PlayerHealth>();

            if (cachedHealth != null && !cachedHealth.IsDead)
            {
                return true;
            }

            playerTarget = null;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            return false;
        }

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        if (playerHealth == null || playerHealth.IsDead)
        {
            return false;
        }

        playerTarget = playerObject.transform;

        return true;
    }

    // =========================================================
    // Original: Warning Visual
    // =========================================================

    private void ShowWarning()
    {
        if (warningLine == null)
        {
            return;
        }

        warningLine.gameObject.SetActive(true);

        UpdateWarningVisual();
    }

    private void UpdateWarningVisual()
    {
        if (warningLine == null)
        {
            return;
        }

        float progress = 1f;

        if (windupDuration > 0f)
        {
            progress =
                1f
                - Mathf.Clamp01(
                    stateTimeRemaining / windupDuration
                );
        }

        Vector3 lineStart = transform.position;

        Vector3 lineEnd =
            lineStart
            + (Vector3)(
                lockedDirection * warningLength
            );

        warningLine.positionCount = 2;
        warningLine.useWorldSpace = true;

        warningLine.SetPosition(0, lineStart);
        warningLine.SetPosition(1, lineEnd);

        float currentWidth = Mathf.Lerp(
            warningStartWidth,
            warningEndWidth,
            progress
        );

        warningLine.startWidth = currentWidth;
        warningLine.endWidth = currentWidth;

        Color currentColor = Color.Lerp(
            warningStartColor,
            warningEndColor,
            progress
        );

        warningLine.startColor = currentColor;
        warningLine.endColor = currentColor;
    }

    private void HideWarning()
    {
        if (warningLine != null)
        {
            warningLine.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // Original: Cache Components
    // =========================================================

    private void CacheComponents()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (enemyMovement == null)
        {
            enemyMovement = GetComponent<EnemyMovement>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (enemyContactDamage == null)
        {
            enemyContactDamage =
                GetComponent<EnemyContactDamage>();
        }

        if (pooledEnemy == null)
        {
            pooledEnemy = GetComponent<PooledEnemy>();
        }

        if (enemyDefinition == null)
        {
            enemyDefinition =
                GetComponent<EnemyDefinition>();
        }
    }

    private void CacheWarningReference()
    {
        if (warningLine != null)
        {
            return;
        }

        Transform warningTransform =
            transform.Find("RangedAimWarning");

        if (warningTransform != null)
        {
            warningLine =
                warningTransform.GetComponent<LineRenderer>();
        }
    }

    // =========================================================
    // Original: Stop Rigidbody
    // =========================================================

    private void StopRigidbodyMotion()
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    // =========================================================
    // Original: Cancel To Idle
    // =========================================================

    private void CancelToIdle()
    {
        HideWarning();
        StopRigidbodyMotion();

        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(false);
        }

        currentState = RangedAttackState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        playerTarget = null;
    }

    // =========================================================
    // Original: Object Pool Runtime Reset
    // =========================================================

    private void ResetRuntimeState()
    {
        HideWarning();
        StopRigidbodyMotion();

        currentState = RangedAttackState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        playerTarget = null;

        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(false);
        }
    }

    // =========================================================
    // Original: Inspector Validation
    // =========================================================

    private void ValidateSettings()
    {
        triggerDistance =
            Mathf.Max(0f, triggerDistance);

        windupDuration =
            Mathf.Max(0f, windupDuration);

        recoveryDuration =
            Mathf.Max(0f, recoveryDuration);

        attackCooldown =
            Mathf.Max(0f, attackCooldown);

        interruptedCooldown =
            Mathf.Max(0f, interruptedCooldown);

        projectileSpawnOffset =
            Mathf.Max(0f, projectileSpawnOffset);

        projectileSpeed =
            Mathf.Max(0.01f, projectileSpeed);

        projectileLifeTime =
            Mathf.Max(0.01f, projectileLifeTime);

        projectileScaleMultiplier =
            Mathf.Max(0.01f, projectileScaleMultiplier);

        warningLength =
            Mathf.Max(0.1f, warningLength);

        warningStartWidth =
            Mathf.Max(0.001f, warningStartWidth);

        warningEndWidth =
            Mathf.Max(0.001f, warningEndWidth);
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    // =========================================================
    // Original: Attack Range Gizmo
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                0.7f,
                0.2f,
                1f,
                0.8f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            triggerDistance
        );
    }
}
