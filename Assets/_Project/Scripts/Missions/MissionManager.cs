using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MissionManager : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [Tooltip("玩家逻辑根对象。")]
    [SerializeField]
    private Transform player;

    [Tooltip("用于发放 Mission EXP 奖励。")]
    [SerializeField]
    private PlayerExperience playerExperience;

    [Tooltip("包含所有 MissionSpawnPoint 的父节点。")]
    [SerializeField]
    private Transform spawnPointRoot;

    [Tooltip("运行时任务目标统一挂到这里。")]
    [SerializeField]
    private Transform runtimeObjectiveRoot;


    // =========================================================
    // Environment Risk References
    // =========================================================

    [Header("Environment Risk References")]

    [Tooltip("用于判断当前是否处于 Active Storm。")]
    [SerializeField]
    private PlanetStormController planetStormController;

    [Tooltip("用于判断 Mission Objective 是否位于 Safe Zone 内。")]
    [SerializeField]
    private SafeZoneController safeZoneController;


    // =========================================================
    // Mission Schedule
    // =========================================================

    [Header("Mission Schedule")]

    [SerializeField]
    private MissionScheduleEntry[] missionSchedule;


    // =========================================================
    // Spawn Rules
    // =========================================================

    [Header("Spawn Rules")]

    [Tooltip("任务点距离 Player 的优先最小距离。")]
    [Min(0f)]
    [SerializeField]
    private float minSpawnDistance = 8f;

    [Tooltip("任务点距离 Player 的优先最大距离。")]
    [Min(0f)]
    [SerializeField]
    private float maxSpawnDistance = 22f;


    // =========================================================
    // Objective Prefabs
    // =========================================================

    [Header("Objective Prefabs")]

    [Tooltip("正式 Beacon Mission Prefab。")]
    [SerializeField]
    private GameObject beaconObjectivePrefab;

    [Tooltip("正式 Alien Core Mission Prefab。")]
    [SerializeField]
    private GameObject alienCoreObjectivePrefab;


    // =========================================================
    // Mission Rewards
    // =========================================================

    [Header("Mission Rewards")]

    [Tooltip("Beacon Mission 普通完成奖励。")]
    [Min(0)]
    [SerializeField]
    private int beaconBaseRewardExp = 10;

    [Tooltip("Alien Core Mission 普通完成奖励。")]
    [Min(0)]
    [SerializeField]
    private int alienCoreBaseRewardExp = 15;


    // =========================================================
    // Storm Risk Reward
    // =========================================================

    [Header("Storm Risk Reward")]

    [Tooltip(
        "当 Mission Objective 位于 Active Storm 的 Safe Zone 外时，"
        + "Mission EXP 奖励倍率。"
    )]
    [Min(1f)]
    [SerializeField]
    private float stormRiskRewardMultiplier = 1.5f;


    // =========================================================
    // Runtime State
    // =========================================================

    [Header("Runtime State")]

    [SerializeField]
    private MissionState currentMissionState =
        MissionState.None;

    [SerializeField]
    private MissionType currentMissionType =
        MissionType.BeaconActivation;

    [SerializeField]
    private Vector2 currentObjectivePosition;

    [SerializeField]
    private float missionStartSurvivalTime;

    [SerializeField]
    private float missionEndSurvivalTime;

    [SerializeField]
    private float missionTimeRemaining;

    [Range(0f, 1f)]
    [SerializeField]
    private float missionProgress01;

    [SerializeField]
    private int nextMissionIndex;

    [SerializeField]
    private int completedMissionCount;

    [SerializeField]
    private int failedMissionCount;

    [SerializeField]
    private string lastSpawnPointName = "None";

    [SerializeField]
    private bool lastSelectionUsedFallback;

    [SerializeField]
    private int lastGrantedExpReward;

    [SerializeField]
    private bool lastRewardUsedStormRiskBonus;


    // =========================================================
    // Cached Runtime Data
    // =========================================================

    private MissionSpawnPoint[] spawnPoints;

    private readonly List<MissionSpawnPoint>
        validSpawnPoints =
            new List<MissionSpawnPoint>(16);

    private MissionSpawnPoint lastSpawnPoint;

    private GameObject runtimeObjective;


    // =========================================================
    // Public Read Only State
    // =========================================================

    /// <summary>
    /// 当前是否存在正在执行的正式 Mission。
    /// </summary>
    public bool HasActiveMission =>
        currentMissionState
        == MissionState.Active;


    /// <summary>
    /// 当前 Mission 类型。
    /// </summary>
    public MissionType CurrentMissionType =>
        currentMissionType;


    /// <summary>
    /// 当前 Mission 生命周期状态。
    /// </summary>
    public MissionState CurrentMissionState =>
        currentMissionState;


    /// <summary>
    /// 当前 Mission Objective 的世界坐标。
    ///
    /// Mission HUD / Direction Arrow / Storm Risk
    /// 都可以读取这个位置。
    /// </summary>
    public Vector2 CurrentObjectivePosition =>
        currentObjectivePosition;


    /// <summary>
    /// 当前 Mission 剩余时间。
    /// </summary>
    public float MissionTimeRemaining =>
        missionTimeRemaining;


    /// <summary>
    /// 当前 Mission 进度，范围 0 ~ 1。
    ///
    /// Beacon：
    /// 持续从 0 -> 1。
    ///
    /// Alien Core：
    /// 回收前为 0，完成后变为 1。
    /// </summary>
    public float MissionProgress01 =>
        missionProgress01;


    /// <summary>
    /// 本局已经完成的 Mission 数量。
    /// </summary>
    public int CompletedMissionCount =>
        completedMissionCount;


    /// <summary>
    /// 本局因为超时失败的 Mission 数量。
    /// </summary>
    public int FailedMissionCount =>
        failedMissionCount;


    /// <summary>
    /// 当前 Active Mission 如果现在完成，
    /// 是否可以获得 Storm Risk Bonus。
    ///
    /// 这是动态结果：
    /// Storm 状态变化后会实时变化。
    /// </summary>
    public bool CurrentMissionHasStormRiskBonus =>
        HasActiveMission
        && EvaluateStormRiskBonus();


    /// <summary>
    /// 当前 Mission 如果现在完成，
    /// 实际会获得多少 EXP。
    ///
    /// MissionHUD 只读取这个结果，
    /// 不自己重复计算奖励。
    /// </summary>
    public int CurrentPotentialRewardExp =>
        HasActiveMission
            ? CalculateCurrentPotentialReward()
            : 0;


    /// <summary>
    /// HUD 用于显示 x1.5 等风险倍率。
    /// </summary>
    public float StormRiskRewardMultiplier =>
        stormRiskRewardMultiplier;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        ResetRuntimeState();

        ResolveReferences();

        CacheSpawnPoints();

        ValidateSchedule();
    }


    private void OnValidate()
    {
        minSpawnDistance =
            Mathf.Max(
                0f,
                minSpawnDistance
            );


        maxSpawnDistance =
            Mathf.Max(
                minSpawnDistance,
                maxSpawnDistance
            );


        beaconBaseRewardExp =
            Mathf.Max(
                0,
                beaconBaseRewardExp
            );


        alienCoreBaseRewardExp =
            Mathf.Max(
                0,
                alienCoreBaseRewardExp
            );


        stormRiskRewardMultiplier =
            Mathf.Max(
                1f,
                stormRiskRewardMultiplier
            );
    }


    private void Update()
    {
        if (GameManager.Instance == null)
        {
            return;
        }


        // -----------------------------------------------------
        // GameOver / Victory
        //
        // 整局游戏已经结束时：
        //
        // 当前 Mission 不记为 Failed，
        // 而是直接 Cancel。
        //
        // 因为：
        //
        // Failed
        // = Mission 自己超时。
        //
        // Cancel
        // = 整局 Gameplay 已结束。
        // -----------------------------------------------------

        if (!GameManager.Instance.IsPlaying)
        {
            if (HasActiveMission)
            {
                CancelCurrentMission(
                    "Game ended."
                );
            }

            return;
        }


        // -----------------------------------------------------
        // Pause / Upgrade / Module Selection / Overclock
        //
        // 当前项目都会通过：
        //
        // Time.timeScale = 0
        //
        // 冻结正式 Gameplay。
        //
        // MissionManager 不需要分别依赖这些 UI Manager。
        // -----------------------------------------------------

        if (Time.timeScale <= 0f)
        {
            return;
        }


        float survivalTime =
            GameManager.Instance
                .SurvivalTime;


        // -----------------------------------------------------
        // 当前有 Active Mission：
        //
        // 优先更新 Mission 生命周期。
        // -----------------------------------------------------

        if (HasActiveMission)
        {
            UpdateActiveMission(
                survivalTime
            );


            // 如果更新以后仍然 Active，
            // 本帧不能继续生成新任务。
            if (HasActiveMission)
            {
                return;
            }
        }


        // -----------------------------------------------------
        // 当前没有 Active Mission，
        // 检查是否有新的 Schedule 到时。
        // -----------------------------------------------------

        TryStartScheduledMission(
            survivalTime
        );
    }


    // =========================================================
    // Public Mission Query
    // =========================================================

    /// <summary>
    /// 判断当前是否正在执行指定 Mission Type。
    ///
    /// Beacon / Alien Core Objective
    /// 都通过这个接口确认自己是否仍然有效。
    /// </summary>
    public bool IsMissionActive(
        MissionType missionType
    )
    {
        return HasActiveMission
            && currentMissionType
                == missionType;
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        // -----------------------------------------------------
        // Player
        // -----------------------------------------------------

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );


            if (playerObject != null)
            {
                player =
                    playerObject.transform;
            }
        }


        // -----------------------------------------------------
        // Player Experience
        // -----------------------------------------------------

        if (playerExperience == null
            && player != null)
        {
            playerExperience =
                player.GetComponent<
                    PlayerExperience
                >();
        }


        if (playerExperience == null)
        {
            playerExperience =
                FindFirstObjectByType<
                    PlayerExperience
                >();
        }


        // -----------------------------------------------------
        // Planet Storm
        // -----------------------------------------------------

        if (planetStormController == null)
        {
            planetStormController =
                PlanetStormController
                    .Instance;
        }


        // -----------------------------------------------------
        // Safe Zone
        // -----------------------------------------------------

        if (safeZoneController == null)
        {
            safeZoneController =
                FindFirstObjectByType<
                    SafeZoneController
                >();
        }
    }


    // =========================================================
    // Spawn Point Cache
    // =========================================================

    private void CacheSpawnPoints()
    {
        if (spawnPointRoot == null)
        {
            spawnPoints =
                new MissionSpawnPoint[0];


            Debug.LogWarning(
                "MissionManager: "
                + "Spawn Point Root 未设置。",
                this
            );


            return;
        }


        spawnPoints =
            spawnPointRoot
                .GetComponentsInChildren<
                    MissionSpawnPoint
                >(true);


        Debug.Log(
            "MissionManager Cached Spawn Points: "
            + spawnPoints.Length,
            this
        );
    }


    // =========================================================
    // Schedule
    // =========================================================

    private void TryStartScheduledMission(
        float survivalTime
    )
    {
        if (missionSchedule == null
            || nextMissionIndex
                >= missionSchedule.Length)
        {
            return;
        }


        MissionScheduleEntry entry =
            missionSchedule[
                nextMissionIndex
            ];


        if (entry == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "Mission Schedule Entry "
                + nextMissionIndex
                + " 为空，已跳过。",
                this
            );


            // 防止下一帧无限重复处理
            // 同一条坏数据。
            nextMissionIndex++;


            return;
        }


        if (survivalTime
            < entry.StartTime)
        {
            return;
        }


        int triggeredScheduleIndex =
            nextMissionIndex;


        // -----------------------------------------------------
        // 非常重要：
        //
        // 先推进 Schedule Index。
        //
        // 即使下面 Prefab 配置错误，
        // 也不能让同一条任务每帧重复触发。
        // -----------------------------------------------------

        nextMissionIndex++;


        StartMissionFromSchedule(
            triggeredScheduleIndex,
            entry,
            survivalTime
        );
    }


    private void StartMissionFromSchedule(
        int scheduleIndex,
        MissionScheduleEntry entry,
        float survivalTime
    )
    {
        ResolveReferences();


        if (player == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "找不到 Player，"
                + "无法开始 Mission。",
                this
            );


            return;
        }


        MissionSpawnPoint selectedPoint =
            SelectSpawnPoint();


        if (selectedPoint == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "没有可用 MissionSpawnPoint，"
                + "本次 Mission 已跳过。",
                this
            );


            return;
        }


        MissionType resolvedType =
            ResolveMissionType(
                entry
            );


        // -----------------------------------------------------
        // 建立 Runtime Mission State
        // -----------------------------------------------------

        currentMissionType =
            resolvedType;


        currentMissionState =
            MissionState.Active;


        currentObjectivePosition =
            selectedPoint.Position;


        missionStartSurvivalTime =
            survivalTime;


        missionEndSurvivalTime =
            survivalTime
            + entry.Duration;


        missionTimeRemaining =
            entry.Duration;


        missionProgress01 =
            0f;


        lastGrantedExpReward =
            0;


        lastRewardUsedStormRiskBonus =
            false;


        // -----------------------------------------------------
        // 创建真正的 Mission Objective。
        // -----------------------------------------------------

        bool objectiveCreated =
            CreateRuntimeObjective(
                selectedPoint
            );


        if (!objectiveCreated)
        {
            // -------------------------------------------------
            // 如果 Prefab / Component 配置错误：
            //
            // 不能留下一个没有 Objective 的 Active Mission。
            // -------------------------------------------------

            currentMissionState =
                MissionState.None;


            missionTimeRemaining =
                0f;


            missionProgress01 =
                0f;


            return;
        }


        lastSpawnPoint =
            selectedPoint;


        lastSpawnPointName =
            selectedPoint.name;


        float distance =
            Vector2.Distance(
                player.position,
                selectedPoint.Position
            );


        Debug.Log(
            "===== Mission Started ====="
            + "\nSchedule Index: "
            + scheduleIndex
            + "\nType: "
            + currentMissionType
            + "\nTriggered At: "
            + survivalTime.ToString("F2")
            + "s"
            + "\nDuration: "
            + entry.Duration.ToString("F2")
            + "s"
            + "\nSpawn Point: "
            + selectedPoint.name
            + "\nPlayer Distance: "
            + distance.ToString("F2")
            + "\nFallback: "
            + lastSelectionUsedFallback,
            this
        );
    }


    private MissionType ResolveMissionType(
        MissionScheduleEntry entry
    )
    {
        if (!entry.RandomizeType)
        {
            return entry.Type;
        }


        // -----------------------------------------------------
        // 当前 Phase34 正式 Mission 只有两个：
        //
        // Beacon
        // Alien Core
        //
        // 第三次 Mission 使用简单 50 / 50 即可。
        // -----------------------------------------------------

        if (Random.value < 0.5f)
        {
            return MissionType
                .BeaconActivation;
        }


        return MissionType
            .AlienCoreRecovery;
    }


    // =========================================================
    // Mission Runtime
    // =========================================================

    private void UpdateActiveMission(
        float survivalTime
    )
    {
        // -----------------------------------------------------
        // Mission 不建立第二套独立总计时器。
        //
        // 统一使用：
        //
        // Mission End Survival Time
        // -
        // GameManager SurvivalTime
        //
        // 因此 Pause / Upgrade 等情况下
        // 会自然保持同步。
        // -----------------------------------------------------

        missionTimeRemaining =
            Mathf.Max(
                0f,
                missionEndSurvivalTime
                - survivalTime
            );


        if (missionTimeRemaining > 0f)
        {
            return;
        }


        FailCurrentMission();
    }


    /// <summary>
    /// Objective 向 MissionManager 上报进度。
    ///
    /// MissionHUD 之后只读取 MissionManager，
    /// 不直接读取具体 Objective。
    /// </summary>
    public void SetMissionProgress01(
        float progress01
    )
    {
        if (!HasActiveMission)
        {
            return;
        }


        missionProgress01 =
            Mathf.Clamp01(
                progress01
            );
    }


    // =========================================================
    // Mission Complete
    // =========================================================

    /// <summary>
    /// 正式完成当前 Mission。
    ///
    /// 这里同时是 Reward Transaction Boundary。
    ///
    /// EXP / Completed Count / Storm Bonus
    /// 都只能结算一次。
    /// </summary>
    public void CompleteCurrentMission()
    {
        // -----------------------------------------------------
        // Complete Guard
        // -----------------------------------------------------

        if (!HasActiveMission)
        {
            return;
        }


        // -----------------------------------------------------
        // 必须在 Mission 还是 Active 的时候
        // 判断 Storm Risk。
        //
        // 因为 EvaluateStormRiskBonus()
        // 本身会检查 HasActiveMission。
        // -----------------------------------------------------

        bool usedStormRiskBonus =
            EvaluateStormRiskBonus();


        int expReward =
            CalculateCurrentPotentialReward();


        // -----------------------------------------------------
        // 在调用外部系统之前，
        // 先正式关闭 Mission。
        // -----------------------------------------------------

        currentMissionState =
            MissionState.Completed;


        missionProgress01 =
            1f;


        missionTimeRemaining =
            0f;


        completedMissionCount++;


        lastRewardUsedStormRiskBonus =
            usedStormRiskBonus;


        lastGrantedExpReward =
            expReward;


        // -----------------------------------------------------
        // Objective 生命周期由 MissionManager 拥有。
        // -----------------------------------------------------

        CleanupRuntimeObjective();


        ResolveReferences();


        // -----------------------------------------------------
        // EXP Reward
        // -----------------------------------------------------

        if (expReward > 0
            && playerExperience != null)
        {
            playerExperience
                .AddExperience(
                    expReward
                );
        }


        Debug.Log(
            "===== Mission Completed ====="
            + "\nType: "
            + currentMissionType
            + "\nStorm Risk Bonus: "
            + lastRewardUsedStormRiskBonus
            + "\nEXP Reward: "
            + expReward
            + "\nCompleted Count: "
            + completedMissionCount,
            this
        );
    }


    // =========================================================
    // Mission Fail
    // =========================================================

    /// <summary>
    /// Mission 因为正式超时而失败。
    /// </summary>
    public void FailCurrentMission()
    {
        if (!HasActiveMission)
        {
            return;
        }


        currentMissionState =
            MissionState.Failed;


        missionTimeRemaining =
            0f;


        failedMissionCount++;


        CleanupRuntimeObjective();


        Debug.Log(
            "===== Mission Failed ====="
            + "\nType: "
            + currentMissionType
            + "\nFailed Count: "
            + failedMissionCount,
            this
        );
    }


    // =========================================================
    // Mission Cancel
    // =========================================================

    /// <summary>
    /// 整局 Gameplay 已经结束时取消当前 Mission。
    ///
    /// Cancel 不增加 Failed Mission Count。
    /// </summary>
    private void CancelCurrentMission(
        string reason
    )
    {
        if (!HasActiveMission)
        {
            CleanupRuntimeObjective();

            return;
        }


        currentMissionState =
            MissionState.None;


        missionTimeRemaining =
            0f;


        missionProgress01 =
            0f;


        CleanupRuntimeObjective();


        Debug.Log(
            "Mission cancelled."
            + "\nReason: "
            + reason,
            this
        );
    }


    // =========================================================
    // Base Rewards
    // =========================================================

    private int CalculateCurrentBaseReward()
    {
        switch (currentMissionType)
        {
            case MissionType.BeaconActivation:

                return beaconBaseRewardExp;


            case MissionType.AlienCoreRecovery:

                return alienCoreBaseRewardExp;


            default:

                return 0;
        }
    }


    // =========================================================
    // Storm Risk Reward
    // =========================================================

    /// <summary>
    /// 判断当前 Mission 如果现在完成，
    /// 是否应该获得 Storm Risk Bonus。
    ///
    /// 条件：
    ///
    /// 1. Mission Active
    /// 2. Storm Phase == Active
    /// 3. Safe Zone 当前有效
    /// 4. Mission Objective 位于 Safe Zone 外
    /// </summary>
    private bool EvaluateStormRiskBonus()
    {
        if (!HasActiveMission)
        {
            return false;
        }


        if (planetStormController == null
            || safeZoneController == null)
        {
            return false;
        }


        // -----------------------------------------------------
        // Warning 不算。
        // Recovery 不算。
        //
        // 只有真正 Active Storm 才有风险奖励。
        // -----------------------------------------------------

        if (!planetStormController
            .IsStormActive)
        {
            return false;
        }


        if (!safeZoneController
            .HasActiveZone)
        {
            return false;
        }


        // -----------------------------------------------------
        // 非常重要：
        //
        // 判断的是 Objective Position。
        //
        // 不是 Player Position。
        //
        // 否则玩家可以在安全区内完成 Mission，
        // 最后一瞬间故意踏出 Safe Zone
        // 来骗取 Risk Bonus。
        // -----------------------------------------------------

        bool objectiveIsSafe =
            safeZoneController
                .IsPositionSafe(
                    currentObjectivePosition
                );


        return !objectiveIsSafe;
    }


    /// <summary>
    /// 计算当前 Mission 如果现在完成，
    /// 实际可以获得多少 EXP。
    ///
    /// HUD 和真正结算都读取同一套规则。
    /// </summary>
    private int CalculateCurrentPotentialReward()
    {
        int baseReward =
            CalculateCurrentBaseReward();


        if (!EvaluateStormRiskBonus())
        {
            return baseReward;
        }


        float multipliedReward =
            baseReward
            * stormRiskRewardMultiplier;


        return RoundPositiveReward(
            multipliedReward
        );
    }


    /// <summary>
    /// 对正数奖励执行明确的 .5 向上舍入。
    ///
    /// 例如：
    ///
    /// 22.5 -> 23
    ///
    /// 避免奖励规则依赖其他 Round 行为。
    /// </summary>
    private int RoundPositiveReward(
        float reward
    )
    {
        return Mathf.FloorToInt(
            Mathf.Max(
                0f,
                reward
            )
            + 0.5f
        );
    }


    // =========================================================
    // Objective Creation
    // =========================================================

    private bool CreateRuntimeObjective(
        MissionSpawnPoint spawnPoint
    )
    {
        // -----------------------------------------------------
        // 正常情况下，
        // 上一个 Runtime Objective 已经在：
        //
        // Complete
        // Fail
        // Cancel
        //
        // 时被删除。
        //
        // 这里再做一次防御式清理。
        // -----------------------------------------------------

        CleanupRuntimeObjective();


        switch (currentMissionType)
        {
            case MissionType.BeaconActivation:

                return CreateBeaconObjective(
                    spawnPoint
                );


            case MissionType.AlienCoreRecovery:

                return CreateAlienCoreObjective(
                    spawnPoint
                );


            default:

                Debug.LogError(
                    "MissionManager: "
                    + "未知 Mission Type："
                    + currentMissionType,
                    this
                );


                return false;
        }
    }


    // =========================================================
    // Beacon Creation
    // =========================================================

    private bool CreateBeaconObjective(
        MissionSpawnPoint spawnPoint
    )
    {
        if (beaconObjectivePrefab == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "Beacon Objective Prefab 未设置。",
                this
            );


            return false;
        }


        runtimeObjective =
            Instantiate(
                beaconObjectivePrefab,
                spawnPoint.Position,
                Quaternion.identity,
                runtimeObjectiveRoot
            );


        runtimeObjective.name =
            "Mission_Beacon_Runtime";


        BeaconMissionObjective
            beaconObjective =
                runtimeObjective
                    .GetComponent<
                        BeaconMissionObjective
                    >();


        if (beaconObjective == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "Beacon Prefab 上缺少 "
                + "BeaconMissionObjective。",
                runtimeObjective
            );


            CleanupRuntimeObjective();


            return false;
        }


        beaconObjective.Initialize(
            this,
            player
        );


        return true;
    }


    // =========================================================
    // Alien Core Creation
    // =========================================================

    private bool CreateAlienCoreObjective(
        MissionSpawnPoint spawnPoint
    )
    {
        if (alienCoreObjectivePrefab == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "Alien Core Objective Prefab 未设置。",
                this
            );


            return false;
        }


        runtimeObjective =
            Instantiate(
                alienCoreObjectivePrefab,
                spawnPoint.Position,
                Quaternion.identity,
                runtimeObjectiveRoot
            );


        runtimeObjective.name =
            "Mission_AlienCore_Runtime";


        AlienCoreMissionObjective
            alienCoreObjective =
                runtimeObjective
                    .GetComponent<
                        AlienCoreMissionObjective
                    >();


        if (alienCoreObjective == null)
        {
            Debug.LogError(
                "MissionManager: "
                + "Alien Core Prefab 上缺少 "
                + "AlienCoreMissionObjective。",
                runtimeObjective
            );


            CleanupRuntimeObjective();


            return false;
        }


        alienCoreObjective.Initialize(
            this
        );


        return true;
    }


    // =========================================================
    // Objective Cleanup
    // =========================================================

    private void CleanupRuntimeObjective()
    {
        if (runtimeObjective == null)
        {
            return;
        }


        Destroy(
            runtimeObjective
        );


        runtimeObjective =
            null;
    }


    // =========================================================
    // Spawn Selection
    // =========================================================

    private MissionSpawnPoint SelectSpawnPoint()
    {
        lastSelectionUsedFallback =
            false;


        if (player == null
            || spawnPoints == null
            || spawnPoints.Length == 0)
        {
            return null;
        }


        validSpawnPoints.Clear();


        // -----------------------------------------------------
        // 第一优先级：
        //
        // 1. SpawnPoint 有效
        // 2. 不重复上一任务点
        // 3. Player 距离在 min ~ max
        // -----------------------------------------------------

        for (int i = 0;
             i < spawnPoints.Length;
             i++)
        {
            MissionSpawnPoint point =
                spawnPoints[i];


            if (!IsSpawnPointUsable(
                    point
                ))
            {
                continue;
            }


            if (point == lastSpawnPoint)
            {
                continue;
            }


            float distance =
                GetPlayerDistance(
                    point
                );


            if (distance
                < minSpawnDistance)
            {
                continue;
            }


            if (distance
                > maxSpawnDistance)
            {
                continue;
            }


            validSpawnPoints.Add(
                point
            );
        }


        // -----------------------------------------------------
        // 存在理想候选：
        //
        // 从合法 Candidate 中随机。
        // -----------------------------------------------------

        if (validSpawnPoints.Count > 0)
        {
            int randomIndex =
                Random.Range(
                    0,
                    validSpawnPoints.Count
                );


            return validSpawnPoints[
                randomIndex
            ];
        }


        // -----------------------------------------------------
        // Fallback
        //
        // 不随机世界坐标。
        //
        // 仍然只从人工 Authoring 的
        // MissionSpawnPoint 中选择。
        // -----------------------------------------------------

        lastSelectionUsedFallback =
            true;


        MissionSpawnPoint fallbackPoint =
            FindBestFallbackPoint(
                true
            );


        if (fallbackPoint != null)
        {
            return fallbackPoint;
        }


        // -----------------------------------------------------
        // 极端情况：
        //
        // 如果场景中真的只剩一个可用点，
        // 最后才允许重复上一 Mission Point。
        // -----------------------------------------------------

        return FindBestFallbackPoint(
            false
        );
    }


    // =========================================================
    // Spawn Fallback
    // =========================================================

    private MissionSpawnPoint
        FindBestFallbackPoint(
            bool excludeLastPoint
        )
    {
        MissionSpawnPoint bestPoint =
            null;


        float bestDistanceError =
            float.PositiveInfinity;


        // -----------------------------------------------------
        // 8 ~ 22 的理想中间距离：
        //
        // (8 + 22) / 2
        // =
        // 15
        // -----------------------------------------------------

        float preferredDistance =
            (
                minSpawnDistance
                + maxSpawnDistance
            )
            * 0.5f;


        for (int i = 0;
             i < spawnPoints.Length;
             i++)
        {
            MissionSpawnPoint point =
                spawnPoints[i];


            if (!IsSpawnPointUsable(
                    point
                ))
            {
                continue;
            }


            if (excludeLastPoint
                && point == lastSpawnPoint)
            {
                continue;
            }


            float distance =
                GetPlayerDistance(
                    point
                );


            float distanceError =
                Mathf.Abs(
                    distance
                    - preferredDistance
                );


            if (distanceError
                >= bestDistanceError)
            {
                continue;
            }


            bestDistanceError =
                distanceError;


            bestPoint =
                point;
        }


        return bestPoint;
    }


    // =========================================================
    // Spawn Point Helpers
    // =========================================================

    private bool IsSpawnPointUsable(
        MissionSpawnPoint point
    )
    {
        return point != null
            && point.enabled
            && point.gameObject
                .activeInHierarchy;
    }


    private float GetPlayerDistance(
        MissionSpawnPoint point
    )
    {
        return Vector2.Distance(
            player.position,
            point.Position
        );
    }


    // =========================================================
    // Runtime Reset
    // =========================================================

    private void ResetRuntimeState()
    {
        currentMissionState =
            MissionState.None;


        currentMissionType =
            MissionType.BeaconActivation;


        currentObjectivePosition =
            Vector2.zero;


        missionStartSurvivalTime =
            0f;


        missionEndSurvivalTime =
            0f;


        missionTimeRemaining =
            0f;


        missionProgress01 =
            0f;


        nextMissionIndex =
            0;


        completedMissionCount =
            0;


        failedMissionCount =
            0;


        lastSpawnPoint =
            null;


        lastSpawnPointName =
            "None";


        lastSelectionUsedFallback =
            false;


        lastGrantedExpReward =
            0;


        lastRewardUsedStormRiskBonus =
            false;


        CleanupRuntimeObjective();
    }


    // =========================================================
    // Schedule Validation
    // =========================================================

    private void ValidateSchedule()
    {
        if (missionSchedule == null)
        {
            return;
        }


        float previousStartTime =
            -1f;


        for (int i = 0;
             i < missionSchedule.Length;
             i++)
        {
            MissionScheduleEntry entry =
                missionSchedule[i];


            if (entry == null)
            {
                Debug.LogWarning(
                    "MissionManager: "
                    + "Schedule Entry "
                    + i
                    + " 为空。",
                    this
                );


                continue;
            }


            if (entry.StartTime
                < previousStartTime)
            {
                Debug.LogWarning(
                    "MissionManager: "
                    + "Mission Schedule "
                    + "没有按照时间升序排列。"
                    + "\nEntry Index: "
                    + i,
                    this
                );
            }


            previousStartTime =
                entry.StartTime;
        }
    }


    // =========================================================
    // Debug - Spawn Point Test
    // =========================================================

    [ContextMenu(
        "Debug/Test Spawn Point Selection x40"
    )]
    private void
        DebugTestSpawnPointSelection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Mission Spawn Point 测试"
                + "只能在 Play Mode 中运行。",
                this
            );


            return;
        }


        ResolveReferences();

        CacheSpawnPoints();


        if (player == null
            || spawnPoints == null
            || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "MissionManager: "
                + "无法执行 Spawn Point 测试。",
                this
            );


            return;
        }


        MissionSpawnPoint
            originalLastSpawnPoint =
                lastSpawnPoint;


        MissionSpawnPoint previousPoint =
            lastSpawnPoint;


        int nullSelectionCount =
            0;


        int repeatedSelectionCount =
            0;


        int fallbackCount =
            0;


        int outsidePreferredRangeCount =
            0;


        for (int i = 0;
             i < 40;
             i++)
        {
            MissionSpawnPoint selectedPoint =
                SelectSpawnPoint();


            if (selectedPoint == null)
            {
                nullSelectionCount++;

                continue;
            }


            if (previousPoint != null
                && selectedPoint
                    == previousPoint)
            {
                repeatedSelectionCount++;
            }


            if (lastSelectionUsedFallback)
            {
                fallbackCount++;
            }


            float distance =
                GetPlayerDistance(
                    selectedPoint
                );


            if (distance
                    < minSpawnDistance
                || distance
                    > maxSpawnDistance)
            {
                outsidePreferredRangeCount++;
            }


            previousPoint =
                selectedPoint;


            lastSpawnPoint =
                selectedPoint;
        }


        // Debug 测试结束后恢复真实 Runtime 状态。
        lastSpawnPoint =
            originalLastSpawnPoint;


        Debug.Log(
            "===== Mission Spawn Point Test ====="
            + "\nIterations: 40"
            + "\nSpawn Points: "
            + spawnPoints.Length
            + "\nNull Selections: "
            + nullSelectionCount
            + "\nRepeated Consecutive Points: "
            + repeatedSelectionCount
            + "\nFallback Used: "
            + fallbackCount
            + "\nOutside Preferred Range: "
            + outsidePreferredRangeCount,
            this
        );
    }


    // =========================================================
    // Debug - Mission Lifecycle
    // =========================================================

    [ContextMenu(
        "Debug/Complete Current Mission"
    )]
    private void
        DebugCompleteCurrentMission()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        CompleteCurrentMission();
    }


    [ContextMenu(
        "Debug/Fail Current Mission"
    )]
    private void
        DebugFailCurrentMission()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        FailCurrentMission();
    }
}