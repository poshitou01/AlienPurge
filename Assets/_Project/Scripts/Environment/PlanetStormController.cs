using UnityEngine;

public enum StormPhase
{
    Calm,
    Warning,
    Active,
    Recovery
}

[DisallowMultipleComponent]
public class PlanetStormController : MonoBehaviour
{
    public static PlanetStormController Instance
    {
        get;
        private set;
    }


    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip("负责生成和判断安全区域。")]
    [SerializeField]
    private SafeZoneController safeZoneController;


    // =========================================================
    // Storm Schedule
    // =========================================================

    [Header("Storm Schedule")]

    [Tooltip(
        "每轮风暴 Warning 开始时对应的正式生存时间。"
    )]
    [SerializeField]
    private float[] stormStartTimes =
    {
        90f,
        195f,
        285f
    };


    // =========================================================
    // Phase Durations
    // =========================================================

    [Header("Phase Durations")]

    [Tooltip("安全区域出现后，正式风暴开始前的警告时间。")]
    [Min(0f)]
    [SerializeField]
    private float warningDuration = 8f;

    [Tooltip("风暴正式生效的持续时间。")]
    [Min(0f)]
    [SerializeField]
    private float activeDuration = 18f;

    [Tooltip("风暴结束后的短暂恢复阶段。")]
    [Min(0f)]
    [SerializeField]
    private float recoveryDuration = 2f;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Debug")]

    [SerializeField]
    private StormPhase currentPhase =
        StormPhase.Calm;

    [Tooltip("当前正在进行或最近一次开始的风暴编号。0 表示尚未开始。")]
    [SerializeField]
    private int currentStormNumber;

    [Tooltip("下一轮等待触发的数组索引。")]
    [SerializeField]
    private int nextStormIndex;

    [SerializeField]
    private float phaseTimeRemaining;

    [SerializeField]
    private float lastStormTriggeredAt;


    // =========================================================
    // Public Read Only State
    // =========================================================

    public StormPhase CurrentPhase =>
        currentPhase;

    public float PhaseTimeRemaining =>
        phaseTimeRemaining;

    public int CurrentStormNumber =>
        currentStormNumber;

    public bool IsWarning =>
        currentPhase == StormPhase.Warning;

    public bool IsStormActive =>
        currentPhase == StormPhase.Active;

    public bool IsRecovering =>
        currentPhase == StormPhase.Recovery;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (Instance != null
            && Instance != this)
        {
            Debug.LogWarning(
                "场景中存在多个 PlanetStormController，"
                + "新的实例将被禁用。",
                this
            );

            enabled = false;
            return;
        }

        Instance = this;

        ResolveReferences();

        ResetRuntimeState();
    }


    private void Update()
    {
        if (!CanUpdateStorm())
        {
            return;
        }

        switch (currentPhase)
        {
            case StormPhase.Calm:
                TryStartScheduledStorm();
                break;

            case StormPhase.Warning:
            case StormPhase.Active:
            case StormPhase.Recovery:
                UpdateCurrentPhase();
                break;
        }
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    private void OnValidate()
    {
        warningDuration =
            Mathf.Max(
                0f,
                warningDuration
            );

        activeDuration =
            Mathf.Max(
                0f,
                activeDuration
            );

        recoveryDuration =
            Mathf.Max(
                0f,
                recoveryDuration
            );
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (safeZoneController != null)
        {
            return;
        }

        safeZoneController =
            FindFirstObjectByType<
                SafeZoneController
            >();
    }


    // =========================================================
    // Main State Machine
    // =========================================================

    private bool CanUpdateStorm()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }

        if (!GameManager.Instance.IsPlaying)
        {
            return false;
        }

        // 当前项目中的 Pause、Upgrade、
        // Weapon Module 都会把 timeScale 设为 0。
        //
        // 因此这里统一冻结 Storm 系统，
        // 防止暂停期间切换阶段。
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        return true;
    }


    private void TryStartScheduledStorm()
    {
        if (stormStartTimes == null
            || stormStartTimes.Length == 0)
        {
            return;
        }

        if (nextStormIndex < 0
            || nextStormIndex
                >= stormStartTimes.Length)
        {
            return;
        }


        float survivalTime =
            GameManager.Instance.SurvivalTime;


        float scheduledTime =
            Mathf.Max(
                0f,
                stormStartTimes[
                    nextStormIndex
                ]
            );


        if (survivalTime < scheduledTime)
        {
            return;
        }


        BeginWarning(
            nextStormIndex,
            survivalTime
        );
    }


    private void UpdateCurrentPhase()
    {
        phaseTimeRemaining -=
            Time.deltaTime;


        if (phaseTimeRemaining > 0f)
        {
            return;
        }


        phaseTimeRemaining = 0f;


        switch (currentPhase)
        {
            case StormPhase.Warning:
                BeginActive();
                break;

            case StormPhase.Active:
                BeginRecovery();
                break;

            case StormPhase.Recovery:
                FinishStorm();
                break;
        }
    }


    // =========================================================
    // Phase Transitions
    // =========================================================

    private void BeginWarning(
        int stormIndex,
        float survivalTime
    )
    {
        ResolveReferences();


        if (safeZoneController == null)
        {
            SkipStormBecauseSafeZoneFailed(
                stormIndex,
                "找不到 SafeZoneController。"
            );

            return;
        }


        // 先占用这一轮计划索引。
        //
        // 即使后续生成失败，也不会每一帧
        // 无限尝试同一轮 Storm。
        nextStormIndex =
            stormIndex + 1;


        bool generated =
            safeZoneController
                .GenerateSafeZone();


        if (!generated)
        {
            SkipStormBecauseSafeZoneFailed(
                stormIndex,
                "Safe Zone 生成失败。"
            );

            return;
        }


        currentStormNumber =
            stormIndex + 1;

        currentPhase =
            StormPhase.Warning;

        phaseTimeRemaining =
            warningDuration;

        lastStormTriggeredAt =
            survivalTime;

        currentStormNumber =
    stormIndex + 1;

        currentPhase =
            StormPhase.Warning;

        phaseTimeRemaining =
            warningDuration;

        lastStormTriggeredAt =
            survivalTime;

        Debug.Log(
            "===== Planet Storm Warning ====="
            + "\nStorm: #"
            + currentStormNumber
            + "\nTriggered At: "
            + survivalTime.ToString("F2")
            + "s"
            + "\nWarning Duration: "
            + warningDuration.ToString("F2")
            + "s"
            + "\nSafe Zone Center: "
            + safeZoneController.CurrentCenter
            + "\nSafe Zone Radius: "
            + safeZoneController.Radius
                .ToString("F2"),
            this
        );


        // 支持 Warning Duration = 0 的测试配置。
        if (phaseTimeRemaining <= 0f)
        {
            BeginActive();
        }
    }


    private void BeginActive()
    {
        currentPhase =
            StormPhase.Active;

        phaseTimeRemaining =
            activeDuration;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayStormStart();
        }


        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance
                .PlayMediumShake();
        }

        Debug.Log(
            "===== Planet Storm Active ====="
            + "\nStorm: #"
            + currentStormNumber
            + "\nActive Duration: "
            + activeDuration.ToString("F2")
            + "s",
            this
        );


        if (phaseTimeRemaining <= 0f)
        {
            BeginRecovery();
        }
    }


    private void BeginRecovery()
    {
        currentPhase =
            StormPhase.Recovery;

        phaseTimeRemaining =
            recoveryDuration;


        Debug.Log(
            "===== Planet Storm Recovery ====="
            + "\nStorm: #"
            + currentStormNumber
            + "\nRecovery Duration: "
            + recoveryDuration.ToString("F2")
            + "s",
            this
        );


        if (phaseTimeRemaining <= 0f)
        {
            FinishStorm();
        }
    }


    private void FinishStorm()
    {
        if (safeZoneController != null)
        {
            safeZoneController
                .ClearSafeZone();
        }


        currentPhase =
            StormPhase.Calm;

        phaseTimeRemaining = 0f;


        Debug.Log(
            "===== Planet Storm Finished ====="
            + "\nStorm: #"
            + currentStormNumber
            + "\nNext Storm Index: "
            + nextStormIndex,
            this
        );
    }


    private void SkipStormBecauseSafeZoneFailed(
        int stormIndex,
        string reason
    )
    {
        // 确保失败的 Storm 不会每一帧重复触发。
        nextStormIndex =
            Mathf.Max(
                nextStormIndex,
                stormIndex + 1
            );


        currentStormNumber =
            stormIndex + 1;

        currentPhase =
            StormPhase.Calm;

        phaseTimeRemaining = 0f;


        if (safeZoneController != null)
        {
            safeZoneController
                .ClearSafeZone();
        }


        Debug.LogError(
            "PlanetStormController: "
            + "Storm #"
            + currentStormNumber
            + " 已安全跳过。"
            + "\n原因："
            + reason,
            this
        );
    }


    // =========================================================
    // Runtime Reset
    // =========================================================

    private void ResetRuntimeState()
    {
        currentPhase =
            StormPhase.Calm;

        currentStormNumber = 0;
        nextStormIndex = 0;

        phaseTimeRemaining = 0f;
        lastStormTriggeredAt = 0f;


        if (safeZoneController != null)
        {
            safeZoneController
                .ClearSafeZone();
        }
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu("Test Start Next Storm")]
    private void TestStartNextStorm()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PlanetStormController: "
                + "请进入 Play Mode 后测试。",
                this
            );

            return;
        }


        if (currentPhase != StormPhase.Calm)
        {
            Debug.LogWarning(
                "PlanetStormController: "
                + "当前 Storm 尚未结束。",
                this
            );

            return;
        }


        if (stormStartTimes == null
            || nextStormIndex
                >= stormStartTimes.Length)
        {
            Debug.LogWarning(
                "PlanetStormController: "
                + "已经没有下一轮 Storm。",
                this
            );

            return;
        }


        float survivalTime = 0f;

        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance
                    .SurvivalTime;
        }


        BeginWarning(
            nextStormIndex,
            survivalTime
        );
    }


    [ContextMenu("Test Advance Current Phase")]
    private void TestAdvanceCurrentPhase()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PlanetStormController: "
                + "请进入 Play Mode 后测试。",
                this
            );

            return;
        }


        switch (currentPhase)
        {
            case StormPhase.Warning:
                BeginActive();
                break;

            case StormPhase.Active:
                BeginRecovery();
                break;

            case StormPhase.Recovery:
                FinishStorm();
                break;

            case StormPhase.Calm:
                Debug.LogWarning(
                    "PlanetStormController: "
                    + "当前处于 Calm，"
                    + "请先执行 Test Start Next Storm。",
                    this
                );
                break;
        }
    }


    [ContextMenu("Test Reset Storm System")]
    private void TestResetStormSystem()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "PlanetStormController: "
                + "请进入 Play Mode 后测试。",
                this
            );

            return;
        }


        ResetRuntimeState();


        Debug.Log(
            "Planet Storm Runtime State 已重置。",
            this
        );
    }
}