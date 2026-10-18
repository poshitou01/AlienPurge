using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyContactDamage))]
[RequireComponent(typeof(PooledEnemy))]
[RequireComponent(typeof(EnemyDefinition))]
public class HeavyShockwaveAttack : MonoBehaviour
{
    private enum ShockwaveState
    {
        Idle,
        Preparing,
        Attacking,
        Recovering,
        Cooldown
    }

    [Header("Trigger Settings")]
    [Min(0f)]
    [SerializeField]
    private float triggerDistance = 3.5f;

    [Header("Shockwave Settings")]
    [Min(0f)]
    [SerializeField]
    private float shockwaveRadius = 2.6f;

    [Header("Expanding wave (opt-in for authored Tank)")]
    [SerializeField] private bool expandingShockwave;
    [Min(.1f), SerializeField] private float waveDuration = .6f;
    [Min(.05f), SerializeField] private float waveWidth = .3f;
    private readonly List<Collider2D> waveHits = new List<Collider2D>(16);
    private readonly HashSet<PlayerHealth> waveVictims = new HashSet<PlayerHealth>();
    private Vector2 waveCenter, lockedFacing;
    private bool restoreContact;
    public bool UsesExpandingWave => expandingShockwave;
    public Vector2 WaveCenter => waveCenter;
    public float WaveWidth => waveWidth;
    public float WaveRadius => Mathf.Lerp(0, Mathf.Max(0,shockwaveRadius-waveWidth*.5f),
        Mathf.Clamp01(1-stateTimeRemaining/Mathf.Max(.1f,waveDuration)));

    [Header("Attack Timing")]
    [Min(0f)]
    [SerializeField]
    private float windupDuration = 1.1f;

    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 0.8f;

    [Min(0f)]
    [SerializeField]
    private float attackCooldown = 5.5f;

    [Min(0f)]
    [SerializeField]
    private float interruptedCooldown = 1.25f;

    [Header("Temporary Warning")]
    [Tooltip("HeavyShockwaveWarning 子对象")]
    [SerializeField]
    private Transform warningVisual;

    [Tooltip("HeavyShockwaveWarning 的 SpriteRenderer")]
    [SerializeField]
    private SpriteRenderer warningRenderer;

    [SerializeField]
    private Color warningStartColor =
        new Color(
            1f,
            0.65f,
            0.05f,
            0.22f
        );

    [SerializeField]
    private Color warningEndColor =
        new Color(
            1f,
            0.12f,
            0.02f,
            0.72f
        );

    [Header("Runtime State")]
    [SerializeField]
    private ShockwaveState currentState =
        ShockwaveState.Idle;

    [SerializeField]
    private float stateTimeRemaining;

    [SerializeField]
    private bool hasReleasedCurrentShockwave;

    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;
    private EnemyHealth enemyHealth;
    private EnemyContactDamage enemyContactDamage;
    private PooledEnemy pooledEnemy;
    private EnemyDefinition enemyDefinition;

    private Transform playerTarget;

    // Read-only native clock and facing for visual presentation.
    public EnemyAttackVisualSample VisualSample =>
        !isActiveAndEnabled ? default :
        currentState == ShockwaveState.Preparing ? new EnemyAttackVisualSample(EnemyAttackVisualPhase.Windup, stateTimeRemaining, windupDuration, lockedFacing) :
        currentState == ShockwaveState.Attacking ? new EnemyAttackVisualSample(EnemyAttackVisualPhase.Active, stateTimeRemaining, waveDuration, lockedFacing) :
        currentState == ShockwaveState.Recovering ? new EnemyAttackVisualSample(EnemyAttackVisualPhase.Recover, stateTimeRemaining, recoveryDuration, lockedFacing) : default;
    public float VisualShockwaveRadius => shockwaveRadius;


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

        if (enemyMovement.IsKnockedBack && currentState != ShockwaveState.Attacking)
        {
            if (currentState
                == ShockwaveState.Preparing)
            {
                InterruptPreparing();
            }

            return;
        }

        switch (currentState)
        {
            case ShockwaveState.Idle:
                TryBeginPreparing();
                break;

            case ShockwaveState.Preparing:
                UpdatePreparing();
                break;

            case ShockwaveState.Attacking:
                if (expandingShockwave) UpdateExpandingWave(); else ReleaseShockwave();
                break;

            case ShockwaveState.Recovering:
                UpdateRecovering();
                break;

            case ShockwaveState.Cooldown:
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

        if (!TryFindPlayer())
        {
            return;
        }

        float distanceToPlayer =
            Vector2.Distance(
                rb.position,
                playerTarget.position
            );

        if (distanceToPlayer > triggerDistance)
        {
            return;
        }

        currentState =
            ShockwaveState.Preparing;

        stateTimeRemaining =
            windupDuration;

        hasReleasedCurrentShockwave = false;
        if (expandingShockwave)
        {
            waveCenter=rb.position; lockedFacing=((Vector2)playerTarget.position-rb.position).normalized;
            waveVictims.Clear(); waveHits.Clear();
            restoreContact=enemyContactDamage.enabled; enemyContactDamage.enabled=false;
        }

        enemyMovement.SetMovementLocked(true);

        StopRigidbodyMotion();
        ShowWarning();

        if (windupDuration <= 0f)
        {
            EnterAttacking();
        }
    }

    private void UpdatePreparing()
    {
        UpdateWarningVisual();

        stateTimeRemaining -= Time.deltaTime;

        if (stateTimeRemaining <= 0f)
        {
            EnterAttacking();
        }
    }

    private void EnterAttacking()
    {
        HideWarning();

        currentState =
            ShockwaveState.Attacking;

        stateTimeRemaining = expandingShockwave ? waveDuration : 0f;
        if (!expandingShockwave) ReleaseShockwave();
    }

    private void ReleaseShockwave()
    {
        if (hasReleasedCurrentShockwave)
        {
            EnterRecovering();
            return;
        }

        hasReleasedCurrentShockwave = true;

        ApplyShockwaveDamage();

        EnterRecovering();
    }

    private void UpdateExpandingWave()
    {
        float previousRadius=WaveRadius;
        stateTimeRemaining=Mathf.Max(0,stateTimeRemaining-Time.deltaTime);
        float outer=Mathf.Min(shockwaveRadius,WaveRadius+waveWidth*.5f);
        float inner=Mathf.Max(0,previousRadius-waveWidth*.5f);
        var filter=new ContactFilter2D { useTriggers=false };
        Physics2D.OverlapCircle(waveCenter,outer,filter,waveHits);
        foreach(var hit in waveHits)
        {
            var target=hit.GetComponentInParent<PlayerHealth>();
            if(target==null || !target.CompareTag("Player") || target.IsDead || waveVictims.Contains(target)) continue;
            // Swept annulus: a long frame cannot skip the front; a player behind it stays safe.
            Vector2 far=hit.bounds.center;
            far+=new Vector2(far.x>=waveCenter.x ? hit.bounds.extents.x : -hit.bounds.extents.x,
                             far.y>=waveCenter.y ? hit.bounds.extents.y : -hit.bounds.extents.y);
            float farDistance=Vector2.Distance(waveCenter,far);
            if(hit is CircleCollider2D circle)
                farDistance=Vector2.Distance(waveCenter,circle.transform.TransformPoint(circle.offset))+
                    circle.radius*Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x),Mathf.Abs(circle.transform.lossyScale.y));
            if(farDistance<inner) continue;
            waveVictims.Add(target);
            var ability=target.GetComponent<PlayerAbilityState>();
            if(ability!=null && ability.IsAirborne) continue;
            target.TakeDamage(Mathf.Max(1,enemyContactDamage.Damage));
        }
        if(stateTimeRemaining<=0) EnterRecovering();
    }
    private void RestoreWaveContact()
    {
        if(restoreContact && enemyContactDamage!=null && enemyHealth!=null && !enemyHealth.IsDead) enemyContactDamage.enabled=true;
        restoreContact=false;
    }

    private void ApplyShockwaveDamage()
    {
        Collider2D[] hitColliders =
            Physics2D.OverlapCircleAll(
                rb.position,
                shockwaveRadius
            );

        for (int i = 0;
            i < hitColliders.Length;
            i++)
        {
            Collider2D hitCollider =
                hitColliders[i];

            if (hitCollider == null)
            {
                continue;
            }

            PlayerHealth playerHealth =
                hitCollider.GetComponentInParent<
                    PlayerHealth
                >();

            if (playerHealth == null
                || playerHealth.IsDead)
            {
                continue;
            }

            PlayerAbilityState abilityState =
                playerHealth.GetComponent<
                    PlayerAbilityState
                >();

            if (abilityState != null
                && abilityState.IsAirborne)
            {
                return;
            }

            int shockwaveDamage = 1;

            if (enemyContactDamage != null)
            {
                shockwaveDamage =
                    Mathf.Max(
                        1,
                        enemyContactDamage.Damage
                    );
            }

            playerHealth.TakeDamage(
                shockwaveDamage
            );

            // 当前游戏只有一个 Player。
            // 找到并处理后立即结束，避免多个 Collider
            // 导致一次冲击波重复伤害。
            return;
        }
    }

    private void EnterRecovering()
    {
        currentState =
            ShockwaveState.Recovering;

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

    private void EnterCooldown()
    {
        RestoreWaveContact();
        currentState =
            ShockwaveState.Cooldown;

        stateTimeRemaining =
            attackCooldown;

        hasReleasedCurrentShockwave = false;

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
        currentState =
            ShockwaveState.Idle;

        stateTimeRemaining = 0f;
        hasReleasedCurrentShockwave = false;
    }

    private void InterruptPreparing()
    {
        RestoreWaveContact();
        HideWarning();
        StopRigidbodyMotion();

        enemyMovement.SetMovementLocked(false);

        currentState =
            ShockwaveState.Cooldown;

        stateTimeRemaining =
            interruptedCooldown;

        hasReleasedCurrentShockwave = false;

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
        if (warningVisual == null)
        {
            return;
        }

        warningVisual.gameObject.SetActive(
            true
        );

        UpdateWarningVisual();
    }

    private void UpdateWarningVisual()
    {
        if (warningVisual == null)
        {
            return;
        }

        warningVisual.position =
            transform.position;

        warningVisual.rotation =
            Quaternion.identity;

        float diameter =
            shockwaveRadius * 2f;

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
                diameter / safeParentScaleX,
                diameter / safeParentScaleY,
                1f
            );

        if (warningRenderer == null)
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

        warningRenderer.color =
            Color.Lerp(
                warningStartColor,
                warningEndColor,
                progress
            );
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

    private void CacheWarningReferences()
    {
        if (warningVisual == null)
        {
            Transform foundWarning =
                transform.Find(
                    "HeavyShockwaveWarning"
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
                warningVisual.GetComponent<
                    SpriteRenderer
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
        RestoreWaveContact(); waveHits.Clear(); waveVictims.Clear(); lockedFacing=Vector2.zero;
        HideWarning();
        StopRigidbodyMotion();

        if (enemyMovement != null)
        {
            enemyMovement.SetMovementLocked(
                false
            );
        }

        currentState =
            ShockwaveState.Idle;

        stateTimeRemaining = 0f;
        hasReleasedCurrentShockwave = false;
        playerTarget = null;
    }

    private void ResetRuntimeState()
    {
        RestoreWaveContact(); waveHits.Clear(); waveVictims.Clear(); lockedFacing=Vector2.zero;
        HideWarning();
        StopRigidbodyMotion();

        currentState =
            ShockwaveState.Idle;

        stateTimeRemaining = 0f;
        hasReleasedCurrentShockwave = false;
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
        waveDuration=Mathf.Max(.1f,waveDuration); waveWidth=Mathf.Clamp(waveWidth,.05f,Mathf.Max(.05f,shockwaveRadius));
        triggerDistance =
            Mathf.Max(0f, triggerDistance);

        shockwaveRadius =
            Mathf.Max(0f, shockwaveRadius);

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
                1f,
                0.25f,
                0.05f,
                0.8f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            shockwaveRadius
        );
    }
}
