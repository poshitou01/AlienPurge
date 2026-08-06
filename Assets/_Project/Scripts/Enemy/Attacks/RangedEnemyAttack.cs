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

    [Header("Trigger Settings")]
    [Min(0f)]
    [SerializeField]
    private float triggerDistance = 7f;

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

    [Header("Temporary Aim Warning")]
    [Tooltip("RangedAimWarning 子对象上的 LineRenderer")]
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

    [Header("Runtime State")]
    [SerializeField]
    private RangedAttackState currentState =
        RangedAttackState.Idle;

    [SerializeField]
    private float stateTimeRemaining;

    [SerializeField]
    private Vector2 lockedDirection;

    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;
    private EnemyHealth enemyHealth;
    private EnemyContactDamage enemyContactDamage;
    private PooledEnemy pooledEnemy;
    private EnemyDefinition enemyDefinition;

    private Transform playerTarget;

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
            if (currentState
                == RangedAttackState.Preparing
                || currentState
                == RangedAttackState.Recovering)
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
            (Vector2)playerTarget.position
            - rb.position;

        if (directionToPlayer.magnitude
            > triggerDistance)
        {
            return;
        }

        if (directionToPlayer.sqrMagnitude
            <= 0.0001f)
        {
            directionToPlayer =
                Vector2.right;
        }

        lockedDirection =
            directionToPlayer.normalized;

        currentState =
            RangedAttackState.Preparing;

        stateTimeRemaining =
            windupDuration;

        enemyMovement.SetMovementLocked(
            true
        );

        StopRigidbodyMotion();
        ShowWarning();

        if (windupDuration <= 0f)
        {
            FireProjectile();
        }
    }

    private void UpdatePreparing()
    {
        UpdateWarningVisual();

        stateTimeRemaining -=
            Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            FireProjectile();
        }
    }

    private void FireProjectile()
    {
        HideWarning();

        Vector3 spawnPosition =
            transform.position
            + (Vector3)(
                lockedDirection
                * projectileSpawnOffset
            );

        EnemyProjectile projectile =
            EnemyProjectilePool.Instance
                .GetProjectile(
                    spawnPosition,
                    Quaternion.identity
                );

        if (projectile != null)
        {
            int projectileDamage = 1;

            if (enemyContactDamage != null)
            {
                projectileDamage =
                    Mathf.Max(
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

    private void EnterRecovering()
    {
        currentState =
            RangedAttackState.Recovering;

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
        stateTimeRemaining -=
            Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            EnterCooldown();
        }
    }

    private void EnterCooldown()
    {
        currentState =
            RangedAttackState.Cooldown;

        stateTimeRemaining =
            attackCooldown;

        lockedDirection =
            Vector2.zero;

        enemyMovement.SetMovementLocked(
            false
        );

        if (attackCooldown <= 0f)
        {
            FinishCooldown();
        }
    }

    private void UpdateCooldown()
    {
        stateTimeRemaining -=
            Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            FinishCooldown();
        }
    }

    private void FinishCooldown()
    {
        currentState =
            RangedAttackState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
    }

    private void InterruptAttack()
    {
        HideWarning();
        StopRigidbodyMotion();

        enemyMovement.SetMovementLocked(
            false
        );

        currentState =
            RangedAttackState.Cooldown;

        stateTimeRemaining =
            interruptedCooldown;

        lockedDirection =
            Vector2.zero;

        if (interruptedCooldown <= 0f)
        {
            FinishCooldown();
        }
    }

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

    private bool TryFindPlayer()
    {
        if (playerTarget != null)
        {
            PlayerHealth cachedHealth =
                playerTarget.GetComponent<
                    PlayerHealth
                >();

            if (cachedHealth != null
                && !cachedHealth.IsDead)
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
            playerObject.GetComponent<
                PlayerHealth
            >();

        if (playerHealth == null
            || playerHealth.IsDead)
        {
            return false;
        }

        playerTarget =
            playerObject.transform;

        return true;
    }

    private void ShowWarning()
    {
        if (warningLine == null)
        {
            return;
        }

        warningLine.gameObject.SetActive(
            true
        );

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
                    stateTimeRemaining
                    / windupDuration
                );
        }

        Vector3 lineStart =
            transform.position;

        Vector3 lineEnd =
            lineStart
            + (Vector3)(
                lockedDirection
                * warningLength
            );

        warningLine.positionCount = 2;
        warningLine.useWorldSpace = true;

        warningLine.SetPosition(
            0,
            lineStart
        );

        warningLine.SetPosition(
            1,
            lineEnd
        );

        float currentWidth =
            Mathf.Lerp(
                warningStartWidth,
                warningEndWidth,
                progress
            );

        warningLine.startWidth =
            currentWidth;

        warningLine.endWidth =
            currentWidth;

        Color currentColor =
            Color.Lerp(
                warningStartColor,
                warningEndColor,
                progress
            );

        warningLine.startColor =
            currentColor;

        warningLine.endColor =
            currentColor;
    }

    private void HideWarning()
    {
        if (warningLine != null)
        {
            warningLine.gameObject.SetActive(
                false
            );
        }
    }

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
                GetComponent<
                    EnemyContactDamage
                >();
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

    private void CacheWarningReference()
    {
        if (warningLine != null)
        {
            return;
        }

        Transform warningTransform =
            transform.Find(
                "RangedAimWarning"
            );

        if (warningTransform != null)
        {
            warningLine =
                warningTransform.GetComponent<
                    LineRenderer
                >();
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

        currentState =
            RangedAttackState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        playerTarget = null;
    }

    private void ResetRuntimeState()
    {
        HideWarning();
        StopRigidbodyMotion();

        currentState =
            RangedAttackState.Idle;

        stateTimeRemaining = 0f;
        lockedDirection = Vector2.zero;
        playerTarget = null;

        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(
                false
            );
        }
    }

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
            Mathf.Max(
                0f,
                interruptedCooldown
            );

        projectileSpawnOffset =
            Mathf.Max(
                0f,
                projectileSpawnOffset
            );

        projectileSpeed =
            Mathf.Max(
                0.01f,
                projectileSpeed
            );

        projectileLifeTime =
            Mathf.Max(
                0.01f,
                projectileLifeTime
            );

        projectileScaleMultiplier =
            Mathf.Max(
                0.01f,
                projectileScaleMultiplier
            );

        warningLength =
            Mathf.Max(
                0.1f,
                warningLength
            );

        warningStartWidth =
            Mathf.Max(
                0.001f,
                warningStartWidth
            );

        warningEndWidth =
            Mathf.Max(
                0.001f,
                warningEndWidth
            );
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

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