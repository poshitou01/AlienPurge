using UnityEngine;

[DisallowMultipleComponent]
public class StormDamageController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip("负责提供当前 Storm Phase。")]
    [SerializeField]
    private PlanetStormController
        planetStormController;

    [Tooltip("负责 Safe Zone 的位置和安全判断。")]
    [SerializeField]
    private SafeZoneController
        safeZoneController;

    [Tooltip(
        "玩家逻辑根对象。"
        + "Storm 判断不使用 VisualRoot。"
    )]
    [SerializeField]
    private Transform player;

    [Tooltip("复用现有玩家生命系统。")]
    [SerializeField]
    private PlayerHealth playerHealth;


    // =========================================================
    // Damage Settings
    // =========================================================

    [Header("Storm Damage Settings")]

    [Tooltip("每次 Storm Tick 造成的伤害。")]
    [Min(1)]
    [SerializeField]
    private int stormDamage = 1;

    [Tooltip(
        "玩家持续处于 Safe Zone 外时，"
        + "两次 Storm Damage Tick 之间的间隔。"
    )]
    [Min(0.1f)]
    [SerializeField]
    private float damageInterval = 2f;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug")]

    [Tooltip("当前玩家是否处于 Safe Zone 内。")]
    [SerializeField]
    private bool playerCurrentlySafe = true;

    [Tooltip(
        "当前玩家在 Active Storm 中"
        + "连续处于危险区域的累计时间。"
    )]
    [SerializeField]
    private float damageTimer;

    [Tooltip("本局已经尝试执行的 Storm Damage Tick 数量。")]
    [SerializeField]
    private int attemptedDamageTickCount;

    [Tooltip(
        "最近一次 Storm Tick 是否真正扣除了生命值。"
        + "如果 PlayerHealth 正处于无敌状态，"
        + "这里会保持 False。"
    )]
    [SerializeField]
    private bool lastDamageTickApplied;


    // =========================================================
    // Public Read Only State
    // =========================================================

    public bool PlayerCurrentlySafe =>
        playerCurrentlySafe;

    public float DamageTimer =>
        damageTimer;

    public float TimeUntilNextDamage
    {
        get
        {
            if (playerCurrentlySafe
                || planetStormController == null
                || !planetStormController
                    .IsStormActive)
            {
                return damageInterval;
            }

            return Mathf.Max(
                0f,
                damageInterval
                - damageTimer
            );
        }
    }


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResolveReferences();

        ResetRuntimeState();
    }


    private void Update()
    {
        ResolveReferences();

        if (!CanEvaluatePlayer())
        {
            ResetDamageCycle();
            return;
        }


        // -----------------------------------------------------
        // Safe Zone 不存在时，
        // 当前世界环境视为安全。
        // -----------------------------------------------------

        if (!safeZoneController.HasActiveZone)
        {
            playerCurrentlySafe = true;

            ResetDamageCycle();

            return;
        }


        // -----------------------------------------------------
        // 只检查 Player Root 的世界位置。
        //
        // Jet Jump 只移动 VisualRoot，
        // 因此不会改变这里的安全判断。
        // -----------------------------------------------------

        playerCurrentlySafe =
            safeZoneController
                .IsPositionSafe(
                    player.position
                );


        // -----------------------------------------------------
        // Warning / Recovery 阶段：
        //
        // 可以知道玩家是否在圈内，
        // 但绝对不造成 Storm Damage。
        // -----------------------------------------------------

        if (!planetStormController
                .IsStormActive)
        {
            ResetDamageCycle();
            return;
        }


        // -----------------------------------------------------
        // Active 阶段但玩家在 Safe Zone 内：
        //
        // 不扣血，同时清除危险区累计时间。
        // -----------------------------------------------------

        if (playerCurrentlySafe)
        {
            ResetDamageCycle();
            return;
        }


        // -----------------------------------------------------
        // Active + Outside Safe Zone
        //
        // 开始累计暴露时间。
        //
        // 使用 Time.deltaTime，
        // 所以 Pause / Upgrade / Module Selection
        // 将自然冻结伤害计时。
        // -----------------------------------------------------

        damageTimer +=
            Time.deltaTime;


        if (damageTimer
            < damageInterval)
        {
            return;
        }


        // 不直接归零，
        // 尽量保留这一帧超过间隔的少量时间误差。
        //
        // 同时每帧最多执行一次伤害，
        // 避免卡顿后突然补算多次伤害。
        damageTimer =
            Mathf.Max(
                0f,
                damageTimer
                - damageInterval
            );


        ApplyStormDamageTick();
    }


    private void OnValidate()
    {
        stormDamage =
            Mathf.Max(
                1,
                stormDamage
            );

        damageInterval =
            Mathf.Max(
                0.1f,
                damageInterval
            );
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (planetStormController == null)
        {
            planetStormController =
                PlanetStormController
                    .Instance;
        }


        if (safeZoneController == null)
        {
            safeZoneController =
                FindFirstObjectByType<
                    SafeZoneController
                >();
        }


        if (player == null)
        {
            GameObject playerObject =
                GameObject
                    .FindGameObjectWithTag(
                        "Player"
                    );

            if (playerObject != null)
            {
                player =
                    playerObject.transform;
            }
        }


        if (playerHealth == null
            && player != null)
        {
            playerHealth =
                player.GetComponent<
                    PlayerHealth
                >();
        }
    }


    // =========================================================
    // Validation
    // =========================================================

    private bool CanEvaluatePlayer()
    {
        if (GameManager.Instance == null
            || !GameManager.Instance
                .IsPlaying)
        {
            return false;
        }


        if (planetStormController == null
            || safeZoneController == null
            || player == null
            || playerHealth == null)
        {
            return false;
        }


        if (playerHealth.IsDead)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // Damage
    // =========================================================

    private void ApplyStormDamageTick()
    {
        if (playerHealth == null
            || playerHealth.IsDead)
        {
            return;
        }


        attemptedDamageTickCount++;


        int healthBeforeDamage =
            playerHealth.CurrentHealth;


        // 重要：
        // 不直接修改 currentHealth。
        //
        // 必须走现有 PlayerHealth，
        // 这样临时无敌、受伤反馈、死亡、
        // HUD、Audio、Camera Shake 都继续复用。
        playerHealth.TakeDamage(
            stormDamage
        );


        int healthAfterDamage =
            playerHealth.CurrentHealth;


        lastDamageTickApplied =
            healthAfterDamage
            < healthBeforeDamage;


        Debug.Log(
            "===== Storm Damage Tick ====="
            + "\nAttempt: "
            + attemptedDamageTickCount
            + "\nDamage Requested: "
            + stormDamage
            + "\nApplied: "
            + lastDamageTickApplied
            + "\nHP Before: "
            + healthBeforeDamage
            + "\nHP After: "
            + healthAfterDamage
            + "\nPlayer Position: "
            + (Vector2)player.position
            + "\nSafe Zone Center: "
            + safeZoneController
                .CurrentCenter,
            this
        );
    }


    // =========================================================
    // Runtime State
    // =========================================================

    private void ResetDamageCycle()
    {
        damageTimer = 0f;
    }


    private void ResetRuntimeState()
    {
        playerCurrentlySafe = true;

        damageTimer = 0f;

        attemptedDamageTickCount = 0;

        lastDamageTickApplied = false;
    }


    private void OnDisable()
    {
        ResetDamageCycle();
    }
}