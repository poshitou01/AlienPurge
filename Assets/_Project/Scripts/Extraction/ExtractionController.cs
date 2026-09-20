using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


[DisallowMultipleComponent]
public class ExtractionController :
    MonoBehaviour
{
    // =========================================================
    // Timeline
    // =========================================================

    [Header("Timeline")]

    [Min(0f)]
    [SerializeField]
    private float extractionAvailableTime =
        315f;


    [Min(0f)]
    [SerializeField]
    private float finalRushTime =
        330f;


    [Min(0f)]
    [SerializeField]
    private float emergencyExtractionTime =
        360f;


    // =========================================================
    // Defense
    // =========================================================

    [Header("Extraction Defense")]

    [Tooltip(
        "Early / Late Normal Extraction 的防守时间。"
    )]
    [Min(1f)]
    [SerializeField]
    private float normalDefenseDuration =
        25f;


    [Tooltip(
        "360s Emergency Extraction 的防守时间。"
    )]
    [Min(1f)]
    [SerializeField]
    private float emergencyDefenseDuration =
        30f;


    // =========================================================
    // Gameplay References
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private Transform player;


    [SerializeField]
    private EnemySpawner enemySpawner;


    [SerializeField]
    private MissionManager missionManager;


    // =========================================================
    // Extraction Spawn Points
    // =========================================================

    [Header("Extraction Spawn Points")]

    [SerializeField]
    private ExtractionSpawnPoint[] spawnPoints;


    [SerializeField]
    private bool autoFindSpawnPointsIfEmpty =
        true;


    [Min(0f)]
    [SerializeField]
    private float minSpawnDistance =
        12f;


    [Min(0f)]
    [SerializeField]
    private float maxSpawnDistance =
        25f;


    [Min(0f)]
    [SerializeField]
    private float
        minDistanceFromActiveMission =
            5f;


    // =========================================================
    // Runtime Extraction Zone
    // =========================================================

    [Header("Runtime Extraction Zone")]

    [FormerlySerializedAs(
        "extractionMarkerPrefab"
    )]
    [SerializeField]
    private GameObject extractionZonePrefab;


    [SerializeField]
    private Transform runtimeExtractionRoot;


    // =========================================================
    // High-Risk Cache
    // =========================================================

    [Header("High-Risk Cache")]

    [SerializeField]
    private GameObject highRiskCachePrefab;


    [SerializeField]
    private Transform runtimeHighRiskCacheRoot;


    [SerializeField]
    private HighRiskCacheSpawnPoint[]
        highRiskCacheSpawnPoints;


    [SerializeField]
    private bool
        autoFindHighRiskCacheSpawnPointsIfEmpty =
            true;


    [Min(0f)]
    [SerializeField]
    private float
        minHighRiskCacheDistanceFromPlayer =
            10f;


    [Min(0f)]
    [SerializeField]
    private float
        maxHighRiskCacheDistanceFromPlayer =
            28f;


    [Min(0f)]
    [SerializeField]
    private float
        minHighRiskCacheDistanceFromExtraction =
            8f;


    // =========================================================
    // Runtime Extraction Debug
    // =========================================================

    [Header("Runtime Extraction Debug")]

    [SerializeField]
    private ExtractionState currentState =
        ExtractionState.Locked;


    [SerializeField]
    private bool
        hasExtractionAvailabilityTriggered;


    [SerializeField]
    private bool hasFinalRushTriggered;


    [SerializeField]
    private bool hasEmergencyTriggered;


    [SerializeField]
    private ExtractionSpawnPoint
        currentSpawnPoint;


    [SerializeField]
    private Vector2
        currentExtractionPosition;


    [SerializeField]
    private string currentSpawnPointName =
        "None";


    [SerializeField]
    private bool lastSelectionUsedFallback;


    [SerializeField]
    private float
        extractionStartSurvivalTime =
            -1f;


    [SerializeField]
    private float defenseProgress;


    [SerializeField]
    private float currentDefenseDuration;


    [SerializeField]
    private bool isPlayerInsideZone;


    [SerializeField]
    private EnemyPressureState
        activeDefensePressure =
            EnemyPressureState.Normal;


    // =========================================================
    // Runtime High-Risk Cache Debug
    // =========================================================

    [Header("Runtime High-Risk Cache Debug")]

    [SerializeField]
    private bool hasHighRiskCacheSpawned;


    [SerializeField]
    private HighRiskCacheSpawnPoint
        currentHighRiskCacheSpawnPoint;


    [SerializeField]
    private Vector2
        currentHighRiskCachePosition;


    [SerializeField]
    private string
        currentHighRiskCacheSpawnPointName =
            "None";


    [SerializeField]
    private bool
        lastHighRiskCacheSelectionUsedFallback;


    // =========================================================
    // Runtime Objects
    // =========================================================

    private GameObject currentZoneObject;

    private ExtractionZone currentZone;

    private GameObject
        currentHighRiskCacheObject;


    // =========================================================
    // Candidate Lists
    // =========================================================

    private readonly List<
        ExtractionSpawnPoint
    > validSpawnPoints =
        new List<
            ExtractionSpawnPoint
        >();


    private readonly List<
        HighRiskCacheSpawnPoint
    > validHighRiskCacheSpawnPoints =
        new List<
            HighRiskCacheSpawnPoint
        >();


    // =========================================================
    // Read Only
    // =========================================================

    public ExtractionState CurrentState =>
        currentState;


    public bool IsExtractionAvailable =>
        currentState ==
        ExtractionState.Available;


    public bool IsDefending =>
        currentState ==
            ExtractionState.Defending
        ||
        currentState ==
            ExtractionState.Emergency;


    public bool IsEmergency =>
        currentState ==
        ExtractionState.Emergency;


    public bool HasActiveExtractionTarget =>
        currentSpawnPoint != null
        &&
        currentZone != null;


    public Vector2 CurrentExtractionPosition =>
        currentExtractionPosition;


    public ExtractionSpawnPoint
        CurrentSpawnPoint =>
            currentSpawnPoint;


    public ExtractionZone CurrentZone =>
        currentZone;


    public float DefenseDuration =>
        currentDefenseDuration;


    public float DefenseProgress =>
        defenseProgress;


    public float DefenseProgress01
    {
        get
        {
            if (currentDefenseDuration <=
                0f)
            {
                return 0f;
            }


            return Mathf.Clamp01(
                defenseProgress
                /
                currentDefenseDuration
            );
        }
    }


    public float DefenseTimeRemaining =>
        Mathf.Max(
            0f,
            currentDefenseDuration
            -
            defenseProgress
        );


    public bool IsPlayerInsideZone =>
        isPlayerInsideZone;


    public float ExtractionStartSurvivalTime =>
        extractionStartSurvivalTime;


    public bool HasFinalRushTriggered =>
        hasFinalRushTriggered;


    public bool HasEmergencyTriggered =>
        hasEmergencyTriggered;


    public bool HasHighRiskCacheSpawned =>
        hasHighRiskCacheSpawned;


    public Vector2
        CurrentHighRiskCachePosition =>
            currentHighRiskCachePosition;


    public EnemyPressureState
        ActiveDefensePressure =>
            activeDefensePressure;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        ResetRuntimeState();
    }


    private void Start()
    {
        ResolveReferences();

        CacheExtractionSpawnPoints();

        CacheHighRiskCacheSpawnPoints();


        if (enemySpawner != null)
        {
            enemySpawner
                .ResetPressureState();
        }
    }


    private void Update()
    {
        if (GameManager.Instance ==
            null)
        {
            return;
        }


        // =====================================================
        // Terminal State
        // =====================================================

        if (!GameManager.Instance
                .IsPlaying)
        {
            if (GameManager.Instance
                    .IsGameOver
                &&
                (
                    currentState ==
                        ExtractionState.Available
                    ||
                    currentState ==
                        ExtractionState.Defending
                    ||
                    currentState ==
                        ExtractionState.Emergency
                ))
            {
                currentState =
                    ExtractionState.Failed;
            }


            return;
        }


        float survivalTime =
            GameManager.Instance
                .SurvivalTime;


        // =====================================================
        // 315 - Extraction Available
        // =====================================================

        if (!hasExtractionAvailabilityTriggered
            &&
            survivalTime >=
                extractionAvailableTime)
        {
            ActivateExtractionAvailable();
        }


        // =====================================================
        // Timeline while NOT yet started
        // =====================================================
        //
        // 只有 Available 才允许：
        //
        // 330 -> Final Rush
        // 360 -> Emergency
        //
        // 一旦进入 Defending，
        // 后续时间节点不允许覆盖当前 Defense。
        // =====================================================

        if (currentState ==
            ExtractionState.Available)
        {
            // -------------------------------------------------
            // Emergency 优先检查。
            //
            // 防止极端情况下时间从 <330
            // 一次跳到 >=360，
            // 导致 360 后才生成新 Cache。
            // -------------------------------------------------

            if (survivalTime >=
                emergencyExtractionTime)
            {
                TriggerEmergencyExtraction();
            }
            else if (!hasFinalRushTriggered
                     &&
                     survivalTime >=
                        finalRushTime)
            {
                TriggerFinalRush();
            }
        }


        // =====================================================
        // Defense
        // =====================================================

        if (currentState ==
                ExtractionState.Defending
            ||
            currentState ==
                ExtractionState.Emergency)
        {
            UpdateDefense();
        }
    }


    // =========================================================
    // 315 Extraction Available
    // =========================================================

    private void ActivateExtractionAvailable()
    {
        if (hasExtractionAvailabilityTriggered)
        {
            return;
        }


        hasExtractionAvailabilityTriggered =
            true;


        ResolveReferences();

        CacheExtractionSpawnPoints();


        ExtractionSpawnPoint selectedPoint =
            SelectExtractionSpawnPoint();


        if (selectedPoint == null)
        {
            Debug.LogError(
                "ExtractionController: "
                + "没有可用 ExtractionSpawnPoint。",
                this
            );


            return;
        }


        currentSpawnPoint =
            selectedPoint;


        currentExtractionPosition =
            selectedPoint.Position;


        currentSpawnPointName =
            selectedPoint.name;


        if (!SpawnRuntimeZone())
        {
            Debug.LogError(
                "ExtractionController: "
                + "Extraction Zone 生成失败。",
                this
            );


            return;
        }


        currentState =
            ExtractionState.Available;


        currentDefenseDuration =
            normalDefenseDuration;


        if (enemySpawner != null)
        {
            enemySpawner.SetPressureState(
                EnemyPressureState
                    .OverstayLevel1
            );
        }


        Debug.Log(
            "===== EXTRACTION AVAILABLE ====="
            + "\nTime: "
            + GameManager.Instance
                .SurvivalTime
                .ToString("F2")
            + "\nSpawn Point: "
            + currentSpawnPointName
            + "\nPosition: "
            + currentExtractionPosition,
            this
        );
    }


    // =========================================================
    // 330 Final Rush
    // =========================================================

    private void TriggerFinalRush()
    {
        if (hasFinalRushTriggered)
        {
            return;
        }


        if (currentState !=
            ExtractionState.Available)
        {
            return;
        }


        float survivalTime =
            GameManager.Instance != null
                ?
                GameManager.Instance
                    .SurvivalTime
                :
                0f;


        // -----------------------------------------------------
        // 360以后禁止新生成高价值奖励。
        // -----------------------------------------------------

        if (survivalTime >=
            emergencyExtractionTime)
        {
            return;
        }


        hasFinalRushTriggered =
            true;


        if (enemySpawner != null)
        {
            enemySpawner.SetPressureState(
                EnemyPressureState
                    .OverstayLevel2
            );
        }


        SpawnHighRiskCache();


        Debug.Log(
            "===== FINAL RUSH ====="
            + "\nTime: "
            + survivalTime
                .ToString("F2")
            + "\nPressure: "
            + EnemyPressureState
                .OverstayLevel2
            + "\nHigh-Risk Cache Spawned: "
            + hasHighRiskCacheSpawned,
            this
        );
    }


    // =========================================================
    // 360 Emergency
    // =========================================================

    private void TriggerEmergencyExtraction()
    {
        if (hasEmergencyTriggered)
        {
            return;
        }


        // Emergency 只允许从：
        //
        // Available
        //
        // 进入。
        //
        // 如果已经 Defending，
        // 说明玩家360前已经正式呼叫。
        // 那么永远保持原 Defense。
        if (currentState !=
            ExtractionState.Available)
        {
            return;
        }


        hasEmergencyTriggered =
            true;


        // -----------------------------------------------------
        // 如果因为某种调试 / 帧跳跃，
        // 330事件此前没有触发，
        // 从现在起也不能再生成Cache。
        // -----------------------------------------------------

        hasFinalRushTriggered =
            true;


        extractionStartSurvivalTime =
            GameManager.Instance != null
                ?
                GameManager.Instance
                    .SurvivalTime
                :
                emergencyExtractionTime;


        defenseProgress =
            0f;


        currentDefenseDuration =
            emergencyDefenseDuration;


        activeDefensePressure =
            EnemyPressureState.Emergency;


        currentState =
            ExtractionState.Emergency;


        UpdatePlayerInsideZone();


        if (enemySpawner != null)
        {
            enemySpawner.SetPressureState(
                EnemyPressureState.Emergency
            );
        }


        Debug.Log(
            "===== EMERGENCY EXTRACTION ====="
            + "\nTime: "
            + extractionStartSurvivalTime
                .ToString("F2")
            + "\nDefense Duration: "
            + currentDefenseDuration
                .ToString("F1")
            + "s"
            + "\nPressure: "
            + EnemyPressureState.Emergency
            + "\nExisting Cache Preserved: "
            + hasHighRiskCacheSpawned,
            this
        );
    }


    // =========================================================
    // Normal Call Extraction
    // =========================================================

    public bool CanCallExtraction(
        ExtractionZone zone
    )
    {
        if (zone == null)
        {
            return false;
        }


        // Emergency 已经自动启动，
        // 所以不会再显示 CALL EXTRACTION。
        if (currentState !=
            ExtractionState.Available)
        {
            return false;
        }


        if (currentZone != zone)
        {
            return false;
        }


        if (GameManager.Instance ==
            null
            ||
            !GameManager.Instance
                .IsPlaying)
        {
            return false;
        }


        return true;
    }


    public bool TryStartExtraction(
        ExtractionZone zone
    )
    {
        if (!CanCallExtraction(
                zone
            ))
        {
            return false;
        }


        float startTime =
            GameManager.Instance
                .SurvivalTime;


        // =====================================================
        // 360 Boundary Protection
        // =====================================================
        //
        // 如果 PlayerInteractor.Update 比
        // ExtractionController.Update 更早运行，
        //
        // 玩家恰好在 360.00+ 按 E，
        // 也绝不能被算成 Late Defense。
        // =====================================================

        if (startTime >=
            emergencyExtractionTime)
        {
            TriggerEmergencyExtraction();

            return true;
        }


        // =====================================================
        // 330 Boundary Protection
        // =====================================================

        if (startTime >=
                finalRushTime
            &&
            !hasFinalRushTriggered)
        {
            TriggerFinalRush();
        }


        extractionStartSurvivalTime =
            startTime;


        defenseProgress =
            0f;


        currentDefenseDuration =
            normalDefenseDuration;


        UpdatePlayerInsideZone();


        currentState =
            ExtractionState.Defending;


        // =====================================================
        // Lock Defense Pressure
        // =====================================================

        if (startTime >=
            finalRushTime)
        {
            activeDefensePressure =
                EnemyPressureState
                    .ExtractionDefenseLate;
        }
        else
        {
            activeDefensePressure =
                EnemyPressureState
                    .ExtractionDefenseEarly;
        }


        if (enemySpawner != null)
        {
            enemySpawner.SetPressureState(
                activeDefensePressure
            );
        }


        Debug.Log(
            "===== EXTRACTION CALLED ====="
            + "\nStart Time: "
            + extractionStartSurvivalTime
                .ToString("F2")
            + "\nDefense Duration: "
            + currentDefenseDuration
                .ToString("F1")
            + "\nDefense Pressure: "
            + activeDefensePressure
            + "\nHigh-Risk Cache Exists: "
            + hasHighRiskCacheSpawned,
            this
        );


        return true;
    }


    // =========================================================
    // Defense
    // =========================================================

    private void UpdateDefense()
    {
        UpdatePlayerInsideZone();


        if (!CanAdvanceDefense())
        {
            return;
        }


        // 离开 Zone：
        //
        // Progress Pause
        //
        // 不 Reset。
        if (!isPlayerInsideZone)
        {
            return;
        }


        defenseProgress +=
            Time.deltaTime;


        defenseProgress =
            Mathf.Min(
                defenseProgress,
                currentDefenseDuration
            );


        if (defenseProgress >=
            currentDefenseDuration)
        {
            CompleteExtraction();
        }
    }


    private void UpdatePlayerInsideZone()
    {
        if (currentZone == null
            ||
            player == null)
        {
            isPlayerInsideZone =
                false;


            return;
        }


        isPlayerInsideZone =
            currentZone
                .IsPlayerInsideDefenseZone(
                    player
                );
    }


    private bool CanAdvanceDefense()
    {
        if (GameManager.Instance ==
            null
            ||
            !GameManager.Instance
                .IsPlaying)
        {
            return false;
        }


        if (Time.timeScale <=
            0f)
        {
            return false;
        }


        if (UpgradeManager
                .IsChoosingUpgrade)
        {
            return false;
        }


        if (WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // Success
    // =========================================================

    private void CompleteExtraction()
    {
        if (currentState !=
                ExtractionState.Defending
            &&
            currentState !=
                ExtractionState.Emergency)
        {
            return;
        }


        defenseProgress =
            currentDefenseDuration;


        currentState =
            ExtractionState.Succeeded;


        isPlayerInsideZone =
            true;


        Debug.Log(
            "===== EXTRACTION SUCCESS ====="
            + "\nStart: "
            + extractionStartSurvivalTime
                .ToString("F2")
            + "\nComplete: "
            + (
                GameManager.Instance != null
                    ?
                    GameManager.Instance
                        .SurvivalTime
                        .ToString("F2")
                    :
                    "--"
            )
            + "\nDefense Pressure: "
            + activeDefensePressure,
            this
        );


        if (GameManager.Instance !=
            null)
        {
            GameManager.Instance
                .CompleteExtraction();
        }
    }


    // =========================================================
    // Runtime Extraction Zone
    // =========================================================

    private bool SpawnRuntimeZone()
    {
        if (currentZoneObject != null)
        {
            Destroy(
                currentZoneObject
            );


            currentZoneObject =
                null;


            currentZone =
                null;
        }


        if (extractionZonePrefab ==
            null)
        {
            Debug.LogError(
                "ExtractionController: "
                + "Extraction Zone Prefab 未设置。",
                this
            );


            return false;
        }


        currentZoneObject =
            Instantiate(
                extractionZonePrefab,
                currentExtractionPosition,
                Quaternion.identity,
                runtimeExtractionRoot
            );


        currentZoneObject.name =
            "Extraction_Zone_Runtime";


        currentZone =
            currentZoneObject
                .GetComponent<
                    ExtractionZone
                >();


        if (currentZone == null)
        {
            Debug.LogError(
                "ExtractionController: "
                + "ExtractionZone Prefab 根对象"
                + "缺少 ExtractionZone。",
                currentZoneObject
            );


            Destroy(
                currentZoneObject
            );


            currentZoneObject =
                null;


            return false;
        }


        currentZone.Initialize(
            this
        );


        return true;
    }


    // =========================================================
    // High-Risk Cache Spawn
    // =========================================================

    private bool SpawnHighRiskCache()
    {
        if (hasHighRiskCacheSpawned)
        {
            return true;
        }


        // -----------------------------------------------------
        // 360以后绝对禁止新增 High-Risk Cache。
        // -----------------------------------------------------

        if (GameManager.Instance != null
            &&
            GameManager.Instance
                .SurvivalTime
            >=
            emergencyExtractionTime)
        {
            Debug.Log(
                "High-Risk Cache spawn skipped: "
                + "Emergency timeline already reached.",
                this
            );


            return false;
        }


        if (highRiskCachePrefab ==
            null)
        {
            Debug.LogError(
                "ExtractionController: "
                + "High-Risk Cache Prefab 未设置。",
                this
            );


            return false;
        }


        CacheHighRiskCacheSpawnPoints();


        HighRiskCacheSpawnPoint selectedPoint =
            SelectHighRiskCacheSpawnPoint();


        if (selectedPoint == null)
        {
            Debug.LogError(
                "ExtractionController: "
                + "没有可用 HighRiskCacheSpawnPoint。",
                this
            );


            return false;
        }


        currentHighRiskCacheSpawnPoint =
            selectedPoint;


        currentHighRiskCachePosition =
            selectedPoint.Position;


        currentHighRiskCacheSpawnPointName =
            selectedPoint.name;


        currentHighRiskCacheObject =
            Instantiate(
                highRiskCachePrefab,
                currentHighRiskCachePosition,
                Quaternion.identity,
                runtimeHighRiskCacheRoot
            );


        currentHighRiskCacheObject.name =
            "LootContainer_HighRiskCache_Runtime";


        LootContainer lootContainer =
            currentHighRiskCacheObject
                .GetComponent<
                    LootContainer
                >();


        if (lootContainer == null)
        {
            Debug.LogError(
                "High-Risk Cache Prefab "
                + "根对象缺少 LootContainer。",
                currentHighRiskCacheObject
            );


            Destroy(
                currentHighRiskCacheObject
            );


            currentHighRiskCacheObject =
                null;


            return false;
        }


        hasHighRiskCacheSpawned =
            true;


        Debug.Log(
            "===== HIGH-RISK CACHE SPAWNED ====="
            + "\nSpawn Point: "
            + currentHighRiskCacheSpawnPointName
            + "\nPosition: "
            + currentHighRiskCachePosition
            + "\nFallback Used: "
            + lastHighRiskCacheSelectionUsedFallback,
            this
        );


        return true;
    }


    // =========================================================
    // Extraction Spawn Selection
    // =========================================================

    private ExtractionSpawnPoint
        SelectExtractionSpawnPoint()
    {
        lastSelectionUsedFallback =
            false;


        if (player == null
            ||
            spawnPoints == null
            ||
            spawnPoints.Length == 0)
        {
            return null;
        }


        validSpawnPoints.Clear();


        for (int i = 0;
             i < spawnPoints.Length;
             i++)
        {
            ExtractionSpawnPoint point =
                spawnPoints[i];


            if (!IsExtractionSpawnPointUsable(
                    point
                ))
            {
                continue;
            }


            float playerDistance =
                Vector2.Distance(
                    player.position,
                    point.Position
                );


            if (playerDistance <
                    minSpawnDistance
                ||
                playerDistance >
                    maxSpawnDistance)
            {
                continue;
            }


            if (IsTooCloseToActiveMission(
                    point
                ))
            {
                continue;
            }


            validSpawnPoints.Add(
                point
            );
        }


        if (validSpawnPoints.Count >
            0)
        {
            return validSpawnPoints[
                Random.Range(
                    0,
                    validSpawnPoints.Count
                )
            ];
        }


        lastSelectionUsedFallback =
            true;


        ExtractionSpawnPoint fallback =
            FindBestExtractionFallback(
                true
            );


        if (fallback != null)
        {
            return fallback;
        }


        return FindBestExtractionFallback(
            false
        );
    }


    private ExtractionSpawnPoint
        FindBestExtractionFallback(
            bool avoidMission
        )
    {
        ExtractionSpawnPoint bestPoint =
            null;


        float bestError =
            float.PositiveInfinity;


        float preferredDistance =
            (
                minSpawnDistance
                +
                maxSpawnDistance
            )
            *
            0.5f;


        for (int i = 0;
             i < spawnPoints.Length;
             i++)
        {
            ExtractionSpawnPoint point =
                spawnPoints[i];


            if (!IsExtractionSpawnPointUsable(
                    point
                ))
            {
                continue;
            }


            if (avoidMission
                &&
                IsTooCloseToActiveMission(
                    point
                ))
            {
                continue;
            }


            float distance =
                Vector2.Distance(
                    player.position,
                    point.Position
                );


            float error =
                Mathf.Abs(
                    distance
                    -
                    preferredDistance
                );


            if (error >=
                bestError)
            {
                continue;
            }


            bestError =
                error;


            bestPoint =
                point;
        }


        return bestPoint;
    }


    private bool
        IsExtractionSpawnPointUsable(
            ExtractionSpawnPoint point
        )
    {
        return point != null
            &&
            point.IsUsable;
    }


    private bool
        IsTooCloseToActiveMission(
            ExtractionSpawnPoint point
        )
    {
        if (missionManager == null
            ||
            !missionManager.HasActiveMission)
        {
            return false;
        }


        return Vector2.Distance(
            point.Position,
            missionManager
                .CurrentObjectivePosition
        )
        <
        minDistanceFromActiveMission;
    }


    // =========================================================
    // High-Risk Cache Selection
    // =========================================================

    private HighRiskCacheSpawnPoint
        SelectHighRiskCacheSpawnPoint()
    {
        lastHighRiskCacheSelectionUsedFallback =
            false;


        if (player == null
            ||
            highRiskCacheSpawnPoints == null
            ||
            highRiskCacheSpawnPoints.Length ==
                0)
        {
            return null;
        }


        validHighRiskCacheSpawnPoints.Clear();


        for (int i = 0;
             i <
             highRiskCacheSpawnPoints.Length;
             i++)
        {
            HighRiskCacheSpawnPoint point =
                highRiskCacheSpawnPoints[i];


            if (!IsHighRiskCachePointUsable(
                    point
                ))
            {
                continue;
            }


            float playerDistance =
                Vector2.Distance(
                    player.position,
                    point.Position
                );


            if (playerDistance <
                    minHighRiskCacheDistanceFromPlayer
                ||
                playerDistance >
                    maxHighRiskCacheDistanceFromPlayer)
            {
                continue;
            }


            if (IsTooCloseToExtraction(
                    point
                ))
            {
                continue;
            }


            validHighRiskCacheSpawnPoints.Add(
                point
            );
        }


        if (validHighRiskCacheSpawnPoints.Count >
            0)
        {
            return
                validHighRiskCacheSpawnPoints[
                    Random.Range(
                        0,
                        validHighRiskCacheSpawnPoints
                            .Count
                    )
                ];
        }


        lastHighRiskCacheSelectionUsedFallback =
            true;


        HighRiskCacheSpawnPoint fallback =
            FindBestHighRiskCacheFallback(
                true
            );


        if (fallback != null)
        {
            return fallback;
        }


        return
            FindBestHighRiskCacheFallback(
                false
            );
    }


    private HighRiskCacheSpawnPoint
        FindBestHighRiskCacheFallback(
            bool avoidExtraction
        )
    {
        HighRiskCacheSpawnPoint bestPoint =
            null;


        float bestError =
            float.PositiveInfinity;


        float preferredDistance =
            (
                minHighRiskCacheDistanceFromPlayer
                +
                maxHighRiskCacheDistanceFromPlayer
            )
            *
            0.5f;


        for (int i = 0;
             i <
             highRiskCacheSpawnPoints.Length;
             i++)
        {
            HighRiskCacheSpawnPoint point =
                highRiskCacheSpawnPoints[i];


            if (!IsHighRiskCachePointUsable(
                    point
                ))
            {
                continue;
            }


            if (avoidExtraction
                &&
                IsTooCloseToExtraction(
                    point
                ))
            {
                continue;
            }


            float distance =
                Vector2.Distance(
                    player.position,
                    point.Position
                );


            float error =
                Mathf.Abs(
                    distance
                    -
                    preferredDistance
                );


            if (error >=
                bestError)
            {
                continue;
            }


            bestError =
                error;


            bestPoint =
                point;
        }


        return bestPoint;
    }


    private bool
        IsHighRiskCachePointUsable(
            HighRiskCacheSpawnPoint point
        )
    {
        return point != null
            &&
            point.IsUsable;
    }


    private bool IsTooCloseToExtraction(
        HighRiskCacheSpawnPoint point
    )
    {
        if (!HasActiveExtractionTarget)
        {
            return false;
        }


        return Vector2.Distance(
            point.Position,
            currentExtractionPosition
        )
        <
        minHighRiskCacheDistanceFromExtraction;
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
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


        if (enemySpawner == null)
        {
            enemySpawner =
                FindFirstObjectByType<
                    EnemySpawner
                >();
        }


        if (missionManager == null)
        {
            missionManager =
                FindFirstObjectByType<
                    MissionManager
                >();
        }
    }


    private void
        CacheExtractionSpawnPoints()
    {
        if (spawnPoints != null
            &&
            spawnPoints.Length > 0)
        {
            return;
        }


        if (!autoFindSpawnPointsIfEmpty)
        {
            return;
        }


        spawnPoints =
            FindObjectsByType<
                ExtractionSpawnPoint
            >(
                FindObjectsSortMode.None
            );
    }


    private void
        CacheHighRiskCacheSpawnPoints()
    {
        if (highRiskCacheSpawnPoints !=
                null
            &&
            highRiskCacheSpawnPoints.Length >
                0)
        {
            return;
        }


        if (!autoFindHighRiskCacheSpawnPointsIfEmpty)
        {
            return;
        }


        highRiskCacheSpawnPoints =
            FindObjectsByType<
                HighRiskCacheSpawnPoint
            >(
                FindObjectsSortMode.None
            );
    }


    // =========================================================
    // Reset
    // =========================================================

    private void ResetRuntimeState()
    {
        currentState =
            ExtractionState.Locked;


        hasExtractionAvailabilityTriggered =
            false;


        hasFinalRushTriggered =
            false;


        hasEmergencyTriggered =
            false;


        currentSpawnPoint =
            null;


        currentExtractionPosition =
            Vector2.zero;


        currentSpawnPointName =
            "None";


        lastSelectionUsedFallback =
            false;


        extractionStartSurvivalTime =
            -1f;


        defenseProgress =
            0f;


        currentDefenseDuration =
            normalDefenseDuration;


        isPlayerInsideZone =
            false;


        activeDefensePressure =
            EnemyPressureState.Normal;


        currentZoneObject =
            null;


        currentZone =
            null;


        hasHighRiskCacheSpawned =
            false;


        currentHighRiskCacheSpawnPoint =
            null;


        currentHighRiskCachePosition =
            Vector2.zero;


        currentHighRiskCacheSpawnPointName =
            "None";


        lastHighRiskCacheSelectionUsedFallback =
            false;


        currentHighRiskCacheObject =
            null;
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Force Extraction Available"
    )]
    private void
        DebugForceExtractionAvailable()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        ActivateExtractionAvailable();
    }


    [ContextMenu(
        "Debug/Force Final Rush"
    )]
    private void
        DebugForceFinalRush()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        if (!hasExtractionAvailabilityTriggered)
        {
            ActivateExtractionAvailable();
        }


        if (GameManager.Instance != null
            &&
            GameManager.Instance
                .SurvivalTime
            >=
            emergencyExtractionTime)
        {
            TriggerEmergencyExtraction();

            return;
        }


        if (currentState ==
            ExtractionState.Available)
        {
            TriggerFinalRush();
        }
    }


    [ContextMenu(
        "Debug/Force Emergency Extraction"
    )]
    private void
        DebugForceEmergencyExtraction()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        if (!hasExtractionAvailabilityTriggered)
        {
            ActivateExtractionAvailable();
        }


        if (currentState ==
            ExtractionState.Available)
        {
            TriggerEmergencyExtraction();
        }
    }


    [ContextMenu(
        "Debug/Force Start Extraction Defense"
    )]
    private void
        DebugForceStartExtractionDefense()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        if (!hasExtractionAvailabilityTriggered)
        {
            ActivateExtractionAvailable();
        }


        if (currentState ==
                ExtractionState.Available
            &&
            currentZone != null)
        {
            TryStartExtraction(
                currentZone
            );
        }
    }


    [ContextMenu(
        "Debug/Print Extraction State"
    )]
    private void
        DebugPrintExtractionState()
    {
        Debug.Log(
            "===== Extraction State ====="
            + "\nState: "
            + currentState
            + "\nFinal Rush: "
            + hasFinalRushTriggered
            + "\nEmergency: "
            + hasEmergencyTriggered
            + "\nDefense Pressure: "
            + activeDefensePressure
            + "\nDefense Progress: "
            + defenseProgress
                .ToString("F2")
            + " / "
            + currentDefenseDuration
                .ToString("F2")
            + "\nPlayer Inside: "
            + isPlayerInsideZone
            + "\nHigh-Risk Cache: "
            + hasHighRiskCacheSpawned
            + "\nCache Point: "
            + currentHighRiskCacheSpawnPointName,
            this
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        extractionAvailableTime =
            Mathf.Max(
                0f,
                extractionAvailableTime
            );


        finalRushTime =
            Mathf.Max(
                extractionAvailableTime,
                finalRushTime
            );


        emergencyExtractionTime =
            Mathf.Max(
                finalRushTime,
                emergencyExtractionTime
            );


        normalDefenseDuration =
            Mathf.Max(
                1f,
                normalDefenseDuration
            );


        emergencyDefenseDuration =
            Mathf.Max(
                1f,
                emergencyDefenseDuration
            );


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


        minDistanceFromActiveMission =
            Mathf.Max(
                0f,
                minDistanceFromActiveMission
            );


        minHighRiskCacheDistanceFromPlayer =
            Mathf.Max(
                0f,
                minHighRiskCacheDistanceFromPlayer
            );


        maxHighRiskCacheDistanceFromPlayer =
            Mathf.Max(
                minHighRiskCacheDistanceFromPlayer,
                maxHighRiskCacheDistanceFromPlayer
            );


        minHighRiskCacheDistanceFromExtraction =
            Mathf.Max(
                0f,
                minHighRiskCacheDistanceFromExtraction
            );
    }
}