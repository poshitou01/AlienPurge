using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    // =========================================================
    // Enemy Spawn Configuration
    // =========================================================

    [Header("Enemy Spawn Configuration")]

    [Tooltip(
        "所有可参与自动刷怪的敌人配置。"
        + "敌人是否可用将由解锁时间、权重和 Prefab 共同决定"
    )]
    [SerializeField]
    private List<EnemySpawnEntry> enemySpawnEntries =
        new List<EnemySpawnEntry>();


    // =========================================================
    // Legacy Spawner Settings
    // =========================================================

    [Header("Spawner Settings")]

    [Tooltip("游戏刚开始时的刷怪间隔")]
    [SerializeField]
    private float spawnInterval = 2f;


    [Tooltip("游戏刚开始时允许存在的最大敌人数")]
    [SerializeField]
    private int maxEnemies = 5;


    [Header("Spawn Interval Difficulty")]

    [Tooltip("刷怪间隔允许降低到的最小值")]
    [SerializeField]
    private float minSpawnInterval = 0.7f;


    // =========================================================
    // 360s Base Spawn Difficulty
    // =========================================================

    [Header("360s Spawn Pressure Curve")]

    [Tooltip("第一阶段结束时间：第一次 Weapon Module 出现")]
    [SerializeField]
    private float spawnStage1Time = 45f;


    [Tooltip("第二阶段结束时间：第二次 Weapon Module 出现")]
    [SerializeField]
    private float spawnStage2Time = 150f;


    [Tooltip("第三阶段结束时间：Build 基本成型")]
    [SerializeField]
    private float spawnStage3Time = 240f;


    [Tooltip("第四阶段结束时间：Final Rush 开始")]
    [SerializeField]
    private float spawnStage4Time = 330f;


    [Tooltip("正式一局结束时间")]
    [SerializeField]
    private float spawnFinalTime = 360f;


    // =========================================================
    // 360s Spawn Interval
    // =========================================================

    [Header("360s Spawn Interval Targets")]

    [SerializeField]
    private float spawnIntervalAtStart = 2.20f;


    [SerializeField]
    private float spawnIntervalAtStage1 = 1.80f;


    [SerializeField]
    private float spawnIntervalAtStage2 = 1.20f;


    [SerializeField]
    private float spawnIntervalAtStage3 = 0.75f;


    [SerializeField]
    private float spawnIntervalAtStage4 = 0.45f;


    [SerializeField]
    private float spawnIntervalAtFinal = 0.35f;


    // =========================================================
    // 360s Max Enemy
    // =========================================================

    [Header("360s Enemy Count Targets")]

    [SerializeField]
    private int maxEnemiesAtStart = 5;


    [SerializeField]
    private int maxEnemiesAtStage1 = 8;


    [SerializeField]
    private int maxEnemiesAtStage2 = 18;


    [SerializeField]
    private int maxEnemiesAtStage3 = 30;


    [SerializeField]
    private int maxEnemiesAtStage4 = 45;


    [SerializeField]
    private int maxEnemiesAtFinal = 55;


    // =========================================================
    // Phase38 Enemy Pressure
    // =========================================================

    [Header("Phase38 Enemy Pressure")]

    [Tooltip(
        "Normal：不增加额外撤离压力。"
    )]
    [SerializeField]
    private EnemyPressureProfile normalPressureProfile =
        new EnemyPressureProfile(
            1.00f,
            0
        );


    [Tooltip(
        "315 秒 Extraction Available 后，"
        + "玩家仍选择继续停留时使用。"
    )]
    [SerializeField]
    private EnemyPressureProfile overstayLevel1Profile =
        new EnemyPressureProfile(
            0.85f,
            2
        );


    [Tooltip(
        "315~330 之间开始 Extraction Defense 时使用。"
    )]
    [SerializeField]
    private EnemyPressureProfile extractionDefenseEarlyProfile =
        new EnemyPressureProfile(
            0.70f,
            3
        );


    [Tooltip(
        "330 秒后仍未开始撤离时使用。"
    )]
    [SerializeField]
    private EnemyPressureProfile overstayLevel2Profile =
        new EnemyPressureProfile(
            0.70f,
            4
        );


    [Tooltip(
        "330~360 之间开始 Extraction Defense 时使用。"
    )]
    [SerializeField]
    private EnemyPressureProfile extractionDefenseLateProfile =
        new EnemyPressureProfile(
            0.62f,
            5
        );


    [Tooltip(
        "360 秒后仍未开始撤离时使用。"
    )]
    [SerializeField]
    private EnemyPressureProfile emergencyPressureProfile =
        new EnemyPressureProfile(
            0.55f,
            6
        );


    // =========================================================
    // Phase38 Pressure Safety
    // =========================================================

    [Header("Phase38 Pressure Safety")]

    [Tooltip(
        "所有 Pressure 计算完成后，"
        + "最终 Spawn Interval 不得低于这个值。"
        + "这是安全下限，不是额外难度倍率。"
    )]
    [Min(0.05f)]
    [SerializeField]
    private float minimumEffectiveSpawnInterval =
        0.25f;


    [Tooltip(
        "所有 Pressure 计算完成后，"
        + "允许存在的敌人数硬上限。"
        + "这是工程保护，不是额外难度系统。"
    )]
    [Min(1)]
    [SerializeField]
    private int maximumEffectiveEnemies =
        64;


    // =========================================================
    // Legacy Difficulty
    // =========================================================

    [Tooltip("每生存 1 秒，刷怪间隔减少多少秒")]
    [SerializeField]
    private float spawnIntervalDecreasePerSecond = 0.02f;


    [Header("Enemy Count Difficulty")]

    [Tooltip("场上敌人数量允许提高到的最终上限")]
    [SerializeField]
    private int maxEnemiesLimit = 12;


    [Tooltip("每隔多少秒提高一次敌人数量上限")]
    [SerializeField]
    private float maxEnemiesIncreaseInterval = 10f;


    [Tooltip("每次提高多少个敌人数量上限")]
    [SerializeField]
    private int maxEnemiesIncreaseAmount = 1;


    // =========================================================
    // Enemy Health Difficulty
    // =========================================================

    [Header("Enemy Health Difficulty")]

    [Tooltip("游戏开始时普通敌人的全局基础生命值")]
    [SerializeField]
    private int enemyInitialMaxHealth = 3;


    [Tooltip("每隔多少秒提高一次全局基础生命值")]
    [SerializeField]
    private float enemyHealthIncreaseInterval = 20f;


    [Tooltip("每次提高多少点全局基础生命值")]
    [SerializeField]
    private int enemyHealthIncreaseAmount = 1;


    [Tooltip("全局基础生命值允许成长到的最终上限")]
    [SerializeField]
    private int enemyMaxHealthLimit = 6;


    // =========================================================
    // Enemy Move Speed Difficulty
    // =========================================================

    [Header("Enemy Move Speed Difficulty")]

    [Tooltip("游戏开始时普通敌人的全局基础移动速度")]
    [SerializeField]
    private float enemyInitialMoveSpeed = 1.5f;


    [Tooltip("每隔多少秒提高一次全局基础移动速度")]
    [SerializeField]
    private float enemyMoveSpeedIncreaseInterval = 20f;


    [Tooltip("每次提高多少全局基础移动速度")]
    [SerializeField]
    private float enemyMoveSpeedIncreaseAmount = 0.25f;


    [Tooltip("全局基础移动速度允许成长到的最终上限")]
    [SerializeField]
    private float enemyMoveSpeedLimit = 2.25f;


    // =========================================================
    // Enemy Contact Damage Difficulty
    // =========================================================

    [Header("Enemy Contact Damage Difficulty")]

    [Tooltip("游戏开始时普通敌人的全局基础接触伤害")]
    [SerializeField]
    private int enemyInitialContactDamage = 1;


    [Tooltip("每隔多少秒提高一次全局基础接触伤害")]
    [SerializeField]
    private float enemyContactDamageIncreaseInterval = 30f;


    [Tooltip("每次提高多少点全局基础接触伤害")]
    [SerializeField]
    private int enemyContactDamageIncreaseAmount = 1;


    [Tooltip("全局基础接触伤害允许成长到的最终上限")]
    [SerializeField]
    private int enemyContactDamageLimit = 3;


    // =========================================================
    // Spawn Distance
    // =========================================================

    [Header("Spawn Distance")]

    [SerializeField]
    private float minSpawnDistance = 5f;


    [SerializeField]
    private float maxSpawnDistance = 8f;


    // =========================================================
    // Map Spawn Bounds
    // =========================================================

    [Header("Map Spawn Bounds")]

    [Tooltip("是否把敌人的生成位置限制在正式地图范围内")]
    [SerializeField]
    private bool limitSpawnToMapBounds = true;


    [Tooltip("正式地图的左下角世界坐标")]
    [SerializeField]
    private Vector2 spawnMapMin =
        new Vector2(
            -25f,
            -25f
        );


    [Tooltip("正式地图的右上角世界坐标")]
    [SerializeField]
    private Vector2 spawnMapMax =
        new Vector2(
            25f,
            25f
        );


    [Tooltip("生成点与地图边界之间保留的安全距离")]
    [Min(0f)]
    [SerializeField]
    private float spawnBoundsPadding = 1f;


    [Tooltip("寻找地图内部有效生成位置的最大尝试次数")]
    [Min(1)]
    [SerializeField]
    private int maxSpawnPositionAttempts = 24;


    // =========================================================
    // Target Settings
    // =========================================================

    [Header("Target Settings")]

    [SerializeField]
    private string playerTag = "Player";


    [SerializeField]
    private string enemyTag = "Enemy";


    // =========================================================
    // Debug Settings
    // =========================================================

    [Header("Debug Settings")]

    [Tooltip("是否允许正常的计时自动刷怪")]
    [SerializeField]
    private bool enableAutomaticSpawning = true;


    [Tooltip("进入游戏后是否立即生成一个普通敌人")]
    [SerializeField]
    private bool spawnOnStart = true;


    // =========================================================
    // Runtime Spawn Debug
    // =========================================================

    [Header("Runtime Spawn Debug")]

    [Tooltip("当前最终实际使用的刷怪间隔")]
    [SerializeField]
    private float currentSpawnInterval;


    [Tooltip("当前最终实际允许存在的最大敌人数")]
    [SerializeField]
    private int currentMaxEnemies;


    [Tooltip("最近一次检测到的场上敌人数")]
    [SerializeField]
    private int currentEnemyCount;


    [Tooltip("当前通过全部检查的有效刷怪候选数量")]
    [SerializeField]
    private int currentSpawnCandidateCount;


    [Tooltip(
        "当前同时满足解锁时间、"
        + "Prefab 和权重检查的敌人类型"
    )]
    [SerializeField]
    private string currentUnlockedEnemyTypes =
        "None";


    [Tooltip("当前所有有效刷怪候选的权重总和")]
    [SerializeField]
    private float currentSpawnWeightTotal;


    [Tooltip("最近一次加权随机选择是否成功")]
    [SerializeField]
    private bool lastSpawnSelectionSucceeded;


    [Tooltip("最近一次加权随机选中的敌人类型")]
    [SerializeField]
    private EnemyType lastSelectedEnemyType =
        EnemyType.Normal;


    // =========================================================
    // Runtime Pressure Debug
    // =========================================================

    [Header("Runtime Pressure Debug")]

    [Tooltip("Phase38 当前唯一有效的 Pressure State")]
    [SerializeField]
    private EnemyPressureState currentPressureState =
        EnemyPressureState.Normal;


    [Tooltip("尚未应用 Pressure 的 Base Spawn Interval")]
    [SerializeField]
    private float currentBaseSpawnInterval;


    [Tooltip("尚未应用 Pressure 的 Base Max Enemy")]
    [SerializeField]
    private int currentBaseMaxEnemies;


    [Tooltip("当前唯一 Pressure Profile 的 Spawn Interval 倍率")]
    [SerializeField]
    private float currentPressureMultiplier =
        1f;


    [Tooltip("当前唯一 Pressure Profile 的 Max Enemy Bonus")]
    [SerializeField]
    private int currentPressureMaxEnemyBonus;


    // =========================================================
    // Runtime Enemy Attribute Debug
    // =========================================================

    [Header("Runtime Enemy Attribute Debug")]

    [Tooltip("当前时间点的全局基础最大生命值")]
    [SerializeField]
    private int currentEnemyMaxHealth;


    [Tooltip("当前时间点的全局基础移动速度")]
    [SerializeField]
    private float currentEnemyMoveSpeed;


    [Tooltip("当前时间点的全局基础接触伤害")]
    [SerializeField]
    private int currentEnemyContactDamage;


    // =========================================================
    // Runtime
    // =========================================================

    private readonly List<EnemySpawnEntry>
        currentSpawnCandidates =
            new List<EnemySpawnEntry>();


    private Transform player;

    private float spawnTimer;


    // 避免配置全部无效时，
    // 每个刷怪间隔都重复输出相同警告。
    private bool
        hasWarnedAboutMissingSpawnCandidate;


    // =========================================================
    // Public Read Only
    // =========================================================

    public int CurrentEnemyMaxHealth =>
        currentEnemyMaxHealth;


    public float CurrentEnemyMoveSpeed =>
        currentEnemyMoveSpeed;


    public int CurrentEnemyContactDamage =>
        currentEnemyContactDamage;


    public EnemyPressureState CurrentPressureState =>
        currentPressureState;


    public float CurrentBaseSpawnInterval =>
        currentBaseSpawnInterval;


    public int CurrentBaseMaxEnemies =>
        currentBaseMaxEnemies;


    public float CurrentEffectiveSpawnInterval =>
        currentSpawnInterval;


    public int CurrentEffectiveMaxEnemies =>
        currentMaxEnemies;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        currentPressureState =
            EnemyPressureState.Normal;


        currentPressureMultiplier =
            1f;


        currentPressureMaxEnemyBonus =
            0;
    }


    private void Start()
    {
        FindPlayer();

        UpdateDifficulty();


        spawnTimer = 0f;


        if (enableAutomaticSpawning
            &&
            spawnOnStart
            &&
            CanSpawnEnemies())
        {
            TrySpawnEnemy();
        }
    }


    private void Update()
    {
        if (!enableAutomaticSpawning)
        {
            return;
        }


        if (!CanSpawnEnemies())
        {
            return;
        }


        if (player == null)
        {
            FindPlayer();


            if (player == null)
            {
                return;
            }
        }


        UpdateDifficulty();


        spawnTimer +=
            Time.deltaTime;


        if (spawnTimer >=
            currentSpawnInterval)
        {
            spawnTimer = 0f;

            TrySpawnEnemy();
        }
    }


    // =========================================================
    // Spawn Gate
    // =========================================================

    private bool CanSpawnEnemies()
    {
        if (GameManager.Instance == null)
        {
            return false;
        }


        if (GameManager.Instance.CurrentState
            != GameState.Playing)
        {
            return false;
        }


        if (UpgradeManager.IsChoosingUpgrade
            ||
            WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // Difficulty
    // =========================================================

    private void UpdateDifficulty()
    {
        float survivalTime = 0f;


        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance
                    .SurvivalTime;
        }


        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        UpdateSpawnDifficulty(
            survivalTime
        );


        UpdateEnemyAttributeDifficulty(
            survivalTime
        );


        RefreshSpawnCandidates(
            survivalTime
        );
    }


    // =========================================================
    // Phase38 Spawn Difficulty
    // =========================================================

    private void UpdateSpawnDifficulty(
        float survivalTime
    )
    {
        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        // -----------------------------------------------------
        // 1. Base Difficulty
        //
        // 这是原 Phase37 / 360s 时间曲线。
        // Pressure 永远不能直接修改这些 Base 值。
        // -----------------------------------------------------

        currentBaseSpawnInterval =
            Calculate360SpawnInterval(
                survivalTime
            );


        currentBaseMaxEnemies =
            Calculate360MaxEnemies(
                survivalTime
            );


        // -----------------------------------------------------
        // 2. Resolve ONE Pressure Profile
        // -----------------------------------------------------

        EnemyPressureProfile profile =
            ResolvePressureProfile(
                currentPressureState
            );


        currentPressureMultiplier =
            profile
                .SpawnIntervalMultiplier;


        currentPressureMaxEnemyBonus =
            profile
                .MaxEnemyBonus;


        // -----------------------------------------------------
        // 3. Effective Spawn Interval
        // -----------------------------------------------------

        float calculatedSpawnInterval =
            currentBaseSpawnInterval
            *
            currentPressureMultiplier;


        currentSpawnInterval =
            Mathf.Max(
                minimumEffectiveSpawnInterval,
                calculatedSpawnInterval
            );


        // -----------------------------------------------------
        // 4. Effective Max Enemy
        // -----------------------------------------------------

        int calculatedMaxEnemies =
            currentBaseMaxEnemies
            +
            currentPressureMaxEnemyBonus;


        currentMaxEnemies =
            Mathf.Clamp(
                calculatedMaxEnemies,
                1,
                maximumEffectiveEnemies
            );
    }


    // =========================================================
    // Phase38 Pressure Resolver
    // =========================================================

    private EnemyPressureProfile
        ResolvePressureProfile(
            EnemyPressureState pressureState
        )
    {
        switch (pressureState)
        {
            case EnemyPressureState
                    .OverstayLevel1:

                return GetConfiguredPressureProfile(
                    overstayLevel1Profile,
                    0.85f,
                    2
                );


            case EnemyPressureState
                    .ExtractionDefenseEarly:

                return GetConfiguredPressureProfile(
                    extractionDefenseEarlyProfile,
                    0.70f,
                    3
                );


            case EnemyPressureState
                    .OverstayLevel2:

                return GetConfiguredPressureProfile(
                    overstayLevel2Profile,
                    0.70f,
                    4
                );


            case EnemyPressureState
                    .ExtractionDefenseLate:

                return GetConfiguredPressureProfile(
                    extractionDefenseLateProfile,
                    0.62f,
                    5
                );


            case EnemyPressureState
                    .Emergency:

                return GetConfiguredPressureProfile(
                    emergencyPressureProfile,
                    0.55f,
                    6
                );


            case EnemyPressureState.Normal:

            default:

                return GetConfiguredPressureProfile(
                    normalPressureProfile,
                    1.00f,
                    0
                );
        }
    }


    private EnemyPressureProfile
        GetConfiguredPressureProfile(
            EnemyPressureProfile configuredProfile,
            float fallbackMultiplier,
            int fallbackEnemyBonus
        )
    {
        if (configuredProfile.IsConfigured)
        {
            return configuredProfile;
        }


        return new EnemyPressureProfile(
            fallbackMultiplier,
            fallbackEnemyBonus
        );
    }


    // =========================================================
    // Phase38 Pressure State API
    // =========================================================

    public void SetPressureState(
        EnemyPressureState newState
    )
    {
        if (currentPressureState ==
            newState)
        {
            return;
        }


        EnemyPressureState previousState =
            currentPressureState;


        currentPressureState =
            newState;


        float survivalTime = 0f;


        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance
                    .SurvivalTime;
        }


        UpdateSpawnDifficulty(
            survivalTime
        );


        Debug.Log(
            "[Enemy Pressure] "
            + previousState
            + " -> "
            + currentPressureState
            + "\nBase Interval: "
            + currentBaseSpawnInterval
                .ToString("F3")
            + "\nEffective Interval: "
            + currentSpawnInterval
                .ToString("F3")
            + "\nBase Max Enemy: "
            + currentBaseMaxEnemies
            + "\nEffective Max Enemy: "
            + currentMaxEnemies,
            this
        );
    }


    public void ResetPressureState()
    {
        if (currentPressureState ==
            EnemyPressureState.Normal)
        {
            currentPressureState =
                EnemyPressureState.Normal;


            float survivalTime = 0f;


            if (GameManager.Instance != null)
            {
                survivalTime =
                    GameManager.Instance
                        .SurvivalTime;
            }


            UpdateSpawnDifficulty(
                survivalTime
            );


            return;
        }


        SetPressureState(
            EnemyPressureState.Normal
        );
    }


    // =========================================================
    // 360s Base Spawn Interval Curve
    // =========================================================

    private float Calculate360SpawnInterval(
        float survivalTime
    )
    {
        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        if (survivalTime <=
            spawnStage1Time)
        {
            float t =
                Mathf.InverseLerp(
                    0f,
                    spawnStage1Time,
                    survivalTime
                );


            return Mathf.Lerp(
                spawnIntervalAtStart,
                spawnIntervalAtStage1,
                t
            );
        }


        if (survivalTime <=
            spawnStage2Time)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage1Time,
                    spawnStage2Time,
                    survivalTime
                );


            return Mathf.Lerp(
                spawnIntervalAtStage1,
                spawnIntervalAtStage2,
                t
            );
        }


        if (survivalTime <=
            spawnStage3Time)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage2Time,
                    spawnStage3Time,
                    survivalTime
                );


            return Mathf.Lerp(
                spawnIntervalAtStage2,
                spawnIntervalAtStage3,
                t
            );
        }


        if (survivalTime <=
            spawnStage4Time)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage3Time,
                    spawnStage4Time,
                    survivalTime
                );


            return Mathf.Lerp(
                spawnIntervalAtStage3,
                spawnIntervalAtStage4,
                t
            );
        }


        if (survivalTime <=
            spawnFinalTime)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage4Time,
                    spawnFinalTime,
                    survivalTime
                );


            return Mathf.Lerp(
                spawnIntervalAtStage4,
                spawnIntervalAtFinal,
                t
            );
        }


        return spawnIntervalAtFinal;
    }


    // =========================================================
    // 360s Base Max Enemy Curve
    // =========================================================

    private int Calculate360MaxEnemies(
        float survivalTime
    )
    {
        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        float calculatedMaxEnemies;


        if (survivalTime <=
            spawnStage1Time)
        {
            float t =
                Mathf.InverseLerp(
                    0f,
                    spawnStage1Time,
                    survivalTime
                );


            calculatedMaxEnemies =
                Mathf.Lerp(
                    maxEnemiesAtStart,
                    maxEnemiesAtStage1,
                    t
                );
        }
        else if (survivalTime <=
            spawnStage2Time)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage1Time,
                    spawnStage2Time,
                    survivalTime
                );


            calculatedMaxEnemies =
                Mathf.Lerp(
                    maxEnemiesAtStage1,
                    maxEnemiesAtStage2,
                    t
                );
        }
        else if (survivalTime <=
            spawnStage3Time)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage2Time,
                    spawnStage3Time,
                    survivalTime
                );


            calculatedMaxEnemies =
                Mathf.Lerp(
                    maxEnemiesAtStage2,
                    maxEnemiesAtStage3,
                    t
                );
        }
        else if (survivalTime <=
            spawnStage4Time)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage3Time,
                    spawnStage4Time,
                    survivalTime
                );


            calculatedMaxEnemies =
                Mathf.Lerp(
                    maxEnemiesAtStage3,
                    maxEnemiesAtStage4,
                    t
                );
        }
        else if (survivalTime <=
            spawnFinalTime)
        {
            float t =
                Mathf.InverseLerp(
                    spawnStage4Time,
                    spawnFinalTime,
                    survivalTime
                );


            calculatedMaxEnemies =
                Mathf.Lerp(
                    maxEnemiesAtStage4,
                    maxEnemiesAtFinal,
                    t
                );
        }
        else
        {
            calculatedMaxEnemies =
                maxEnemiesAtFinal;
        }


        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                calculatedMaxEnemies
            )
        );
    }


    // =========================================================
    // Enemy Attribute Difficulty
    // =========================================================

    private void UpdateEnemyAttributeDifficulty(
        float survivalTime
    )
    {
        currentEnemyMaxHealth =
            CalculateEnemyMaxHealth(
                survivalTime
            );


        currentEnemyMoveSpeed =
            CalculateEnemyMoveSpeed(
                survivalTime
            );


        currentEnemyContactDamage =
            CalculateEnemyContactDamage(
                survivalTime
            );
    }


    private int CalculateEnemyMaxHealth(
        float survivalTime
    )
    {
        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        int increaseCount =
            Mathf.FloorToInt(
                survivalTime
                /
                enemyHealthIncreaseInterval
            );


        int calculatedMaxHealth =
            enemyInitialMaxHealth
            +
            increaseCount
            *
            enemyHealthIncreaseAmount;


        return Mathf.Min(
            calculatedMaxHealth,
            enemyMaxHealthLimit
        );
    }


    private float CalculateEnemyMoveSpeed(
        float survivalTime
    )
    {
        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        int increaseCount =
            Mathf.FloorToInt(
                survivalTime
                /
                enemyMoveSpeedIncreaseInterval
            );


        float calculatedMoveSpeed =
            enemyInitialMoveSpeed
            +
            increaseCount
            *
            enemyMoveSpeedIncreaseAmount;


        return Mathf.Min(
            calculatedMoveSpeed,
            enemyMoveSpeedLimit
        );
    }


    private int CalculateEnemyContactDamage(
        float survivalTime
    )
    {
        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        int increaseCount =
            Mathf.FloorToInt(
                survivalTime
                /
                enemyContactDamageIncreaseInterval
            );


        int calculatedDamage =
            enemyInitialContactDamage
            +
            increaseCount
            *
            enemyContactDamageIncreaseAmount;


        return Mathf.Min(
            calculatedDamage,
            enemyContactDamageLimit
        );
    }


    // =========================================================
    // Spawn Candidates
    // =========================================================

    private void RefreshSpawnCandidates(
        float survivalTime
    )
    {
        currentSpawnCandidates.Clear();


        currentSpawnCandidateCount =
            0;


        currentUnlockedEnemyTypes =
            "None";


        currentSpawnWeightTotal =
            0f;


        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        if (enemySpawnEntries == null)
        {
            return;
        }


        for (int i = 0;
             i < enemySpawnEntries.Count;
             i++)
        {
            EnemySpawnEntry entry =
                enemySpawnEntries[i];


            if (!IsSpawnEntryValid(
                    entry,
                    survivalTime
                ))
            {
                continue;
            }


            currentSpawnCandidates.Add(
                entry
            );


            currentSpawnWeightTotal +=
                entry.SpawnWeight;
        }


        currentSpawnCandidateCount =
            currentSpawnCandidates.Count;


        if (currentSpawnCandidates.Count > 0)
        {
            currentUnlockedEnemyTypes =
                string.Empty;


            for (int i = 0;
                 i < currentSpawnCandidates.Count;
                 i++)
            {
                if (i > 0)
                {
                    currentUnlockedEnemyTypes +=
                        ", ";
                }


                currentUnlockedEnemyTypes +=
                    currentSpawnCandidates[i]
                        .Type
                        .ToString();
            }
        }


        if (float.IsNaN(
                currentSpawnWeightTotal
            )
            ||
            float.IsInfinity(
                currentSpawnWeightTotal
            )
            ||
            currentSpawnWeightTotal < 0f)
        {
            currentSpawnCandidates.Clear();


            currentSpawnCandidateCount =
                0;


            currentUnlockedEnemyTypes =
                "None";


            currentSpawnWeightTotal =
                0f;
        }
    }


    private bool IsSpawnEntryValid(
        EnemySpawnEntry entry,
        float survivalTime
    )
    {
        if (entry == null)
        {
            return false;
        }


        if (entry.Prefab == null)
        {
            return false;
        }


        float spawnWeight =
            entry.SpawnWeight;


        if (float.IsNaN(
                spawnWeight
            )
            ||
            float.IsInfinity(
                spawnWeight
            )
            ||
            spawnWeight <= 0f)
        {
            return false;
        }


        float unlockTime =
            entry.UnlockTime;


        if (float.IsNaN(
                unlockTime
            )
            ||
            float.IsInfinity(
                unlockTime
            ))
        {
            return false;
        }


        float safeUnlockTime =
            Mathf.Max(
                0f,
                unlockTime
            );


        if (survivalTime <
            safeUnlockTime)
        {
            return false;
        }


        return true;
    }


    private EnemySpawnEntry
        SelectWeightedSpawnEntry(
            float survivalTime
        )
    {
        lastSpawnSelectionSucceeded =
            false;


        RefreshSpawnCandidates(
            survivalTime
        );


        if (currentSpawnCandidates.Count ==
            0)
        {
            return null;
        }


        if (currentSpawnWeightTotal <= 0f
            ||
            float.IsNaN(
                currentSpawnWeightTotal
            )
            ||
            float.IsInfinity(
                currentSpawnWeightTotal
            ))
        {
            return null;
        }


        float randomWeight =
            Random.Range(
                0f,
                currentSpawnWeightTotal
            );


        float accumulatedWeight =
            0f;


        for (int i = 0;
             i < currentSpawnCandidates.Count;
             i++)
        {
            EnemySpawnEntry entry =
                currentSpawnCandidates[i];


            accumulatedWeight +=
                entry.SpawnWeight;


            if (randomWeight <
                accumulatedWeight)
            {
                RecordSelectedSpawnEntry(
                    entry
                );


                return entry;
            }
        }


        // 浮点误差安全回退。
        EnemySpawnEntry fallbackCandidate =
            currentSpawnCandidates[
                currentSpawnCandidates.Count
                - 1
            ];


        RecordSelectedSpawnEntry(
            fallbackCandidate
        );


        return fallbackCandidate;
    }


    private void RecordSelectedSpawnEntry(
        EnemySpawnEntry selectedEntry
    )
    {
        if (selectedEntry == null)
        {
            lastSpawnSelectionSucceeded =
                false;


            return;
        }


        lastSelectedEnemyType =
            selectedEntry.Type;


        lastSpawnSelectionSucceeded =
            true;
    }


    // =========================================================
    // Player
    // =========================================================

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                playerTag
            );


        if (playerObject != null)
        {
            player =
                playerObject.transform;
        }
        else
        {
            Debug.LogWarning(
                "EnemySpawner: 没有找到 Tag 为 "
                + playerTag
                + " 的对象。",
                this
            );
        }
    }


    // =========================================================
    // Normal Automatic Spawn
    // =========================================================

    private void TrySpawnEnemy()
    {
        float survivalTime =
            0f;


        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance
                    .SurvivalTime;
        }


        survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        EnemySpawnEntry selectedEntry =
            SelectWeightedSpawnEntry(
                survivalTime
            );


        if (selectedEntry == null)
        {
            if (!hasWarnedAboutMissingSpawnCandidate)
            {
                Debug.LogError(
                    "EnemySpawner: 当前没有有效的"
                    + "敌人生成候选，"
                    + "本次对象池生成已安全取消。",
                    this
                );


                hasWarnedAboutMissingSpawnCandidate =
                    true;
            }


            return;
        }


        hasWarnedAboutMissingSpawnCandidate =
            false;


        SpawnEnemyFromPool(
            selectedEntry.Type,
            true,
            selectedEntry.Type
                .ToString()
        );
    }


    // =========================================================
    // Enemy Pool Spawn
    // =========================================================

    private bool SpawnEnemyFromPool(
        EnemyType enemyType,
        bool respectEnemyLimit,
        string enemyLabel
    )
    {
        if (EnemyPool.Instance == null)
        {
            Debug.LogError(
                "EnemySpawner: 场景中没有 EnemyPool，"
                + "无法生成 "
                + enemyLabel
                + " Enemy。",
                this
            );


            return false;
        }


        if (!EnemyPool.Instance.IsInitialized)
        {
            Debug.LogError(
                "EnemySpawner: EnemyPool "
                + "尚未完成有效初始化，"
                + "无法生成 "
                + enemyLabel
                + " Enemy。",
                this
            );


            return false;
        }


        if (player == null)
        {
            FindPlayer();


            if (player == null)
            {
                Debug.LogWarning(
                    "EnemySpawner: Player 为空，"
                    + "无法生成 "
                    + enemyLabel
                    + " Enemy。",
                    this
                );


                return false;
            }
        }


        RefreshCurrentEnemyCount();


        if (respectEnemyLimit
            &&
            currentEnemyCount >=
            currentMaxEnemies)
        {
            return false;
        }


        Vector3 spawnPosition =
            GetRandomSpawnPositionAroundPlayer();


        PooledEnemy pooledEnemy =
            EnemyPool.Instance.GetEnemy(
                enemyType,
                spawnPosition,
                Quaternion.identity
            );


        if (pooledEnemy == null)
        {
            Debug.LogWarning(
                "EnemySpawner: 未能从 EnemyPool "
                + "取得 "
                + enemyLabel
                + " Enemy。",
                this
            );


            RefreshCurrentEnemyCount();

            return false;
        }


        GameObject spawnedEnemy =
            pooledEnemy.gameObject;


        if (!InitializeEnemyAttributes(
                spawnedEnemy
            ))
        {
            Debug.LogError(
                spawnedEnemy.name
                + " 属性初始化失败，"
                + "将立即返回对象池。",
                spawnedEnemy
            );


            pooledEnemy.ReturnToPool();


            RefreshCurrentEnemyCount();

            return false;
        }


        RefreshCurrentEnemyCount();


        return true;
    }


    // =========================================================
    // Enemy Attribute Initialization
    // =========================================================

    private bool InitializeEnemyAttributes(
        GameObject spawnedEnemy
    )
    {
        if (spawnedEnemy == null)
        {
            Debug.LogError(
                "EnemySpawner: 生成的敌人为空，"
                + "无法初始化属性。",
                this
            );


            return false;
        }


        EnemyDefinition enemyDefinition =
            spawnedEnemy.GetComponent<
                EnemyDefinition
            >();


        if (enemyDefinition == null)
        {
            Debug.LogError(
                spawnedEnemy.name
                + " 没有 EnemyDefinition，"
                + "无法应用敌人类型数据。",
                spawnedEnemy
            );


            return false;
        }


        enemyDefinition
            .InitializeFromGlobalDifficulty(
                currentEnemyMaxHealth,
                currentEnemyMoveSpeed,
                currentEnemyContactDamage
            );


        if (!enemyDefinition.HasBeenInitialized)
        {
            Debug.LogError(
                spawnedEnemy.name
                + " 的 EnemyDefinition "
                + "初始化失败。",
                spawnedEnemy
            );


            return false;
        }


        return true;
    }


    // =========================================================
    // Enemy Count
    // =========================================================

    private void RefreshCurrentEnemyCount()
    {
        if (EnemyPool.Instance != null
            &&
            EnemyPool.Instance.IsInitialized)
        {
            currentEnemyCount =
                EnemyPool.Instance
                    .GetTotalActiveCount();


            return;
        }


        // EnemyPool 缺失时保留旧 Tag 统计，
        // 仅作为安全调试回退。
        currentEnemyCount =
            GameObject.FindGameObjectsWithTag(
                enemyTag
            ).Length;
    }


    // =========================================================
    // Spawn Position
    // =========================================================

    private Vector3
        GetRandomSpawnPositionAroundPlayer()
    {
        if (!limitSpawnToMapBounds)
        {
            return
                CreateRandomSpawnPositionAroundPlayer();
        }


        float safePadding =
            Mathf.Max(
                0f,
                spawnBoundsPadding
            );


        Vector2 safeMin =
            spawnMapMin
            +
            Vector2.one
            *
            safePadding;


        Vector2 safeMax =
            spawnMapMax
            -
            Vector2.one
            *
            safePadding;


        if (safeMin.x > safeMax.x
            ||
            safeMin.y > safeMax.y)
        {
            Debug.LogWarning(
                "EnemySpawner: 地图生成范围无效，"
                + "将暂时使用未限制的随机生成位置。",
                this
            );


            return
                CreateRandomSpawnPositionAroundPlayer();
        }


        int attemptCount =
            Mathf.Max(
                1,
                maxSpawnPositionAttempts
            );


        for (int i = 0;
             i < attemptCount;
             i++)
        {
            Vector3 candidatePosition =
                CreateRandomSpawnPositionAroundPlayer();


            if (IsSpawnPositionInsideBounds(
                    candidatePosition,
                    safeMin,
                    safeMax
                ))
            {
                return candidatePosition;
            }
        }


        // -----------------------------------------------------
        // Fallback：
        // 如果多次随机仍没有进入地图范围，
        // 朝地图中心寻找一个合法位置。
        // -----------------------------------------------------

        Vector2 mapCenter =
            (
                safeMin
                +
                safeMax
            )
            *
            0.5f;


        Vector2 directionToCenter =
            mapCenter
            -
            (Vector2)player.position;


        if (directionToCenter.sqrMagnitude
            <= Mathf.Epsilon)
        {
            directionToCenter =
                Vector2.right;
        }


        float fallbackDistance =
            Random.Range(
                minSpawnDistance,
                maxSpawnDistance
            );


        Vector2 fallbackPosition =
            (Vector2)player.position
            +
            directionToCenter.normalized
            *
            fallbackDistance;


        fallbackPosition.x =
            Mathf.Clamp(
                fallbackPosition.x,
                safeMin.x,
                safeMax.x
            );


        fallbackPosition.y =
            Mathf.Clamp(
                fallbackPosition.y,
                safeMin.y,
                safeMax.y
            );


        return new Vector3(
            fallbackPosition.x,
            fallbackPosition.y,
            0f
        );
    }


    private Vector3
        CreateRandomSpawnPositionAroundPlayer()
    {
        float randomAngle =
            Random.Range(
                0f,
                360f
            );


        float randomDistance =
            Random.Range(
                minSpawnDistance,
                maxSpawnDistance
            );


        Vector2 direction =
            new Vector2(
                Mathf.Cos(
                    randomAngle
                    *
                    Mathf.Deg2Rad
                ),
                Mathf.Sin(
                    randomAngle
                    *
                    Mathf.Deg2Rad
                )
            );


        Vector3 spawnPosition =
            player.position
            +
            (Vector3)(
                direction
                *
                randomDistance
            );


        spawnPosition.z =
            0f;


        return spawnPosition;
    }


    private bool IsSpawnPositionInsideBounds(
        Vector3 spawnPosition,
        Vector2 safeMin,
        Vector2 safeMax
    )
    {
        return
            spawnPosition.x >=
            safeMin.x
            &&
            spawnPosition.x <=
            safeMax.x
            &&
            spawnPosition.y >=
            safeMin.y
            &&
            spawnPosition.y <=
            safeMax.y;
    }


    // =========================================================
    // Spawn Testing
    // =========================================================

    private void SpawnEnemyForTesting(
        EnemyType enemyType,
        string enemyLabel
    )
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试生成 "
                + enemyLabel
                + " Enemy。",
                this
            );


            return;
        }


        if (!CanSpawnEnemies())
        {
            Debug.LogWarning(
                "当前游戏状态不允许生成敌人。",
                this
            );


            return;
        }


        UpdateDifficulty();


        SpawnEnemyFromPool(
            enemyType,
            false,
            enemyLabel
        );
    }


    [ContextMenu(
        "Test Spawn Normal Enemy"
    )]
    private void TestSpawnNormalEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Normal,
            "Normal"
        );
    }


    [ContextMenu(
        "Test Spawn Fast Enemy"
    )]
    private void TestSpawnFastEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Fast,
            "Fast"
        );
    }


    [ContextMenu(
        "Test Spawn Heavy Enemy"
    )]
    private void TestSpawnHeavyEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Heavy,
            "Heavy"
        );
    }


    [ContextMenu(
        "Test Spawn Ranged Enemy"
    )]
    private void TestSpawnRangedEnemy()
    {
        SpawnEnemyForTesting(
            EnemyType.Ranged,
            "Ranged"
        );
    }


    // =========================================================
    // Phase38 Pressure Debug
    // =========================================================

    [ContextMenu(
        "Debug/Pressure/Print Current State"
    )]
    private void
        DebugPrintCurrentPressureState()
    {
        float survivalTime =
            0f;


        if (GameManager.Instance != null)
        {
            survivalTime =
                GameManager.Instance
                    .SurvivalTime;
        }


        UpdateSpawnDifficulty(
            survivalTime
        );


        Debug.Log(
            "===== Enemy Pressure ====="
            + "\nState: "
            + currentPressureState
            + "\nSurvival Time: "
            + survivalTime
                .ToString("F2")
            + "\nBase Spawn Interval: "
            + currentBaseSpawnInterval
                .ToString("F3")
            + "\nPressure Multiplier: "
            + currentPressureMultiplier
                .ToString("F2")
            + "\nEffective Spawn Interval: "
            + currentSpawnInterval
                .ToString("F3")
            + "\nBase Max Enemy: "
            + currentBaseMaxEnemies
            + "\nPressure Max Bonus: +"
            + currentPressureMaxEnemyBonus
            + "\nEffective Max Enemy: "
            + currentMaxEnemies,
            this
        );
    }


    [ContextMenu(
        "Debug/Pressure/Set Normal"
    )]
    private void DebugPressureNormal()
    {
        SetPressureState(
            EnemyPressureState.Normal
        );
    }


    [ContextMenu(
        "Debug/Pressure/Set Overstay Level 1"
    )]
    private void DebugPressureOverstay1()
    {
        SetPressureState(
            EnemyPressureState
                .OverstayLevel1
        );
    }


    [ContextMenu(
        "Debug/Pressure/Set Early Defense"
    )]
    private void DebugPressureEarlyDefense()
    {
        SetPressureState(
            EnemyPressureState
                .ExtractionDefenseEarly
        );
    }


    [ContextMenu(
        "Debug/Pressure/Set Overstay Level 2"
    )]
    private void DebugPressureOverstay2()
    {
        SetPressureState(
            EnemyPressureState
                .OverstayLevel2
        );
    }


    [ContextMenu(
        "Debug/Pressure/Set Late Defense"
    )]
    private void DebugPressureLateDefense()
    {
        SetPressureState(
            EnemyPressureState
                .ExtractionDefenseLate
        );
    }


    [ContextMenu(
        "Debug/Pressure/Set Emergency"
    )]
    private void DebugPressureEmergency()
    {
        SetPressureState(
            EnemyPressureState
                .Emergency
        );
    }


    [ContextMenu(
        "Debug/Pressure/Preview 315 Seconds"
    )]
    private void
        DebugPreviewPressureAt315Seconds()
    {
        LogPressurePreviewAtTime(
            315f
        );
    }


    [ContextMenu(
        "Debug/Pressure/Preview 330 Seconds"
    )]
    private void
        DebugPreviewPressureAt330Seconds()
    {
        LogPressurePreviewAtTime(
            330f
        );
    }


    [ContextMenu(
        "Debug/Pressure/Preview 360 Seconds"
    )]
    private void
        DebugPreviewPressureAt360Seconds()
    {
        LogPressurePreviewAtTime(
            360f
        );
    }


    private void LogPressurePreviewAtTime(
        float testTime
    )
    {
        testTime =
            Mathf.Max(
                0f,
                testTime
            );


        float baseInterval =
            Calculate360SpawnInterval(
                testTime
            );


        int baseMaxEnemies =
            Calculate360MaxEnemies(
                testTime
            );


        EnemyPressureProfile profile =
            ResolvePressureProfile(
                currentPressureState
            );


        float effectiveInterval =
            Mathf.Max(
                minimumEffectiveSpawnInterval,
                baseInterval
                *
                profile
                    .SpawnIntervalMultiplier
            );


        int effectiveMaxEnemies =
            Mathf.Clamp(
                baseMaxEnemies
                +
                profile.MaxEnemyBonus,
                1,
                maximumEffectiveEnemies
            );


        Debug.Log(
            "===== Pressure Preview ====="
            + "\nTime: "
            + testTime
                .ToString("F0")
            + "s"
            + "\nState: "
            + currentPressureState
            + "\nBase Interval: "
            + baseInterval
                .ToString("F3")
            + "\nPressure Multiplier: "
            + profile
                .SpawnIntervalMultiplier
                .ToString("F2")
            + "\nEffective Interval: "
            + effectiveInterval
                .ToString("F3")
            + "\nBase Max Enemy: "
            + baseMaxEnemies
            + "\nPressure Bonus: +"
            + profile.MaxEnemyBonus
            + "\nEffective Max Enemy: "
            + effectiveMaxEnemies,
            this
        );
    }


    // =========================================================
    // Legacy Difficulty Debug
    // =========================================================

    private void LogDifficultyAtTime(
        float testSurvivalTime
    )
    {
        testSurvivalTime =
            Mathf.Max(
                0f,
                testSurvivalTime
            );


        float testSpawnInterval =
            spawnInterval
            -
            testSurvivalTime
            *
            spawnIntervalDecreasePerSecond;


        testSpawnInterval =
            Mathf.Max(
                minSpawnInterval,
                testSpawnInterval
            );


        int enemyCountIncreaseCount =
            Mathf.FloorToInt(
                testSurvivalTime
                /
                maxEnemiesIncreaseInterval
            );


        int testMaxEnemies =
            maxEnemies
            +
            enemyCountIncreaseCount
            *
            maxEnemiesIncreaseAmount;


        testMaxEnemies =
            Mathf.Min(
                testMaxEnemies,
                maxEnemiesLimit
            );


        int testEnemyMaxHealth =
            CalculateEnemyMaxHealth(
                testSurvivalTime
            );


        float testEnemyMoveSpeed =
            CalculateEnemyMoveSpeed(
                testSurvivalTime
            );


        int testEnemyContactDamage =
            CalculateEnemyContactDamage(
                testSurvivalTime
            );


        Debug.Log(
            "===== Legacy Difficulty At "
            + testSurvivalTime
                .ToString("F0")
            + " Seconds ====="
            + "\nSpawn Interval: "
            + testSpawnInterval
                .ToString("F2")
            + "\nMax Enemies: "
            + testMaxEnemies
            + "\nGlobal Enemy Max Health: "
            + testEnemyMaxHealth
            + "\nGlobal Enemy Move Speed: "
            + testEnemyMoveSpeed
                .ToString("F2")
            + "\nGlobal Enemy Contact Damage: "
            + testEnemyContactDamage
            + "\nNOTE: Spawn Interval / Max Enemy "
            + "这里是 Legacy Debug Formula，"
            + "不是当前 Runtime 360s Curve。",
            this
        );
    }


    [ContextMenu(
        "Debug Difficulty At 0 Seconds"
    )]
    private void DebugDifficultyAt0Seconds()
    {
        LogDifficultyAtTime(
            0f
        );
    }


    [ContextMenu(
        "Debug Difficulty At 30 Seconds"
    )]
    private void DebugDifficultyAt30Seconds()
    {
        LogDifficultyAtTime(
            30f
        );
    }


    [ContextMenu(
        "Debug Difficulty At 60 Seconds"
    )]
    private void DebugDifficultyAt60Seconds()
    {
        LogDifficultyAtTime(
            60f
        );
    }


    // =========================================================
    // Enemy Type Debug
    // =========================================================

    private GameObject FindConfiguredEnemyPrefab(
        EnemyType enemyType
    )
    {
        if (enemySpawnEntries == null)
        {
            return null;
        }


        for (int i = 0;
             i < enemySpawnEntries.Count;
             i++)
        {
            EnemySpawnEntry entry =
                enemySpawnEntries[i];


            if (entry == null)
            {
                continue;
            }


            if (entry.Type !=
                enemyType)
            {
                continue;
            }


            if (entry.Prefab == null)
            {
                continue;
            }


            return entry.Prefab;
        }


        return null;
    }


    private void LogEnemyTypeStatsAtTime(
        float testSurvivalTime
    )
    {
        testSurvivalTime =
            Mathf.Max(
                0f,
                testSurvivalTime
            );


        int globalMaxHealth =
            CalculateEnemyMaxHealth(
                testSurvivalTime
            );


        float globalMoveSpeed =
            CalculateEnemyMoveSpeed(
                testSurvivalTime
            );


        int globalContactDamage =
            CalculateEnemyContactDamage(
                testSurvivalTime
            );


        Debug.Log(
            "===== Enemy Type Stats At "
            + testSurvivalTime
                .ToString("F0")
            + " Seconds ====="
            + "\nGlobal HP="
            + globalMaxHealth
            + ", Global Speed="
            + globalMoveSpeed
                .ToString("F2")
            + ", Global Damage="
            + globalContactDamage,
            this
        );


        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Normal
            ),
            "Normal",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );


        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Fast
            ),
            "Fast",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );


        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Heavy
            ),
            "Heavy",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );


        LogSingleEnemyTypeStats(
            FindConfiguredEnemyPrefab(
                EnemyType.Ranged
            ),
            "Ranged",
            globalMaxHealth,
            globalMoveSpeed,
            globalContactDamage
        );
    }


    private void LogSingleEnemyTypeStats(
        GameObject enemyPrefab,
        string enemyLabel,
        int globalMaxHealth,
        float globalMoveSpeed,
        int globalContactDamage
    )
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning(
                enemyLabel
                + " Enemy Prefab 没有绑定，"
                + "无法预览属性。",
                this
            );


            return;
        }


        EnemyDefinition enemyDefinition =
            enemyPrefab.GetComponent<
                EnemyDefinition
            >();


        if (enemyDefinition == null)
        {
            Debug.LogWarning(
                enemyPrefab.name
                + " 没有 EnemyDefinition，"
                + "无法预览属性。",
                enemyPrefab
            );


            return;
        }


        EnemyData enemyData =
            enemyDefinition.Data;


        if (enemyData == null)
        {
            Debug.LogWarning(
                enemyPrefab.name
                + " 没有绑定 EnemyData，"
                + "无法预览属性。",
                enemyPrefab
            );


            return;
        }


        int finalMaxHealth =
            RoundEnemyAttributeToPositiveInt(
                globalMaxHealth
                *
                enemyData.HealthMultiplier
            );


        float finalMoveSpeed =
            Mathf.Max(
                0.01f,
                globalMoveSpeed
                *
                enemyData.MoveSpeedMultiplier
            );


        int finalContactDamage =
            RoundEnemyAttributeToPositiveInt(
                globalContactDamage
                *
                enemyData.DamageMultiplier
            );


        int finalExperienceAmount =
            Mathf.Max(
                1,
                enemyData
                    .ExperienceAmount
            );


        float finalVisualScale =
            Mathf.Max(
                0.1f,
                enemyData
                    .VisualScale
            );


        Debug.Log(
            enemyLabel
            + " Enemy"
            + ": HP="
            + finalMaxHealth
            + ", Speed="
            + finalMoveSpeed
                .ToString("F3")
            + ", Damage="
            + finalContactDamage
            + ", EXP="
            + finalExperienceAmount
            + ", Scale="
            + finalVisualScale
                .ToString("F2"),
            enemyPrefab
        );
    }


    private int
        RoundEnemyAttributeToPositiveInt(
            float value
        )
    {
        int roundedValue =
            Mathf.FloorToInt(
                value
                +
                0.5f
            );


        return Mathf.Max(
            1,
            roundedValue
        );
    }


    [ContextMenu(
        "Debug Enemy Types At 0 Seconds"
    )]
    private void DebugEnemyTypesAt0Seconds()
    {
        LogEnemyTypeStatsAtTime(
            0f
        );
    }


    [ContextMenu(
        "Debug Enemy Types At 30 Seconds"
    )]
    private void DebugEnemyTypesAt30Seconds()
    {
        LogEnemyTypeStatsAtTime(
            30f
        );
    }


    [ContextMenu(
        "Debug Enemy Types At 60 Seconds"
    )]
    private void DebugEnemyTypesAt60Seconds()
    {
        LogEnemyTypeStatsAtTime(
            60f
        );
    }


    // =========================================================
    // Spawn Candidate Debug
    // =========================================================

    private void LogSpawnCandidatesAtTime(
        float testSurvivalTime
    )
    {
        testSurvivalTime =
            Mathf.Max(
                0f,
                testSurvivalTime
            );


        RefreshSpawnCandidates(
            testSurvivalTime
        );


        Debug.Log(
            "===== Spawn Candidates At "
            + testSurvivalTime
                .ToString("F0")
            + " Seconds ====="
            + "\nValid Candidate Count: "
            + currentSpawnCandidates.Count
            + "\nUnlocked Enemy Types: "
            + currentUnlockedEnemyTypes
            + "\nTotal Spawn Weight: "
            + currentSpawnWeightTotal
                .ToString("F1"),
            this
        );


        if (currentSpawnCandidates.Count ==
            0)
        {
            Debug.LogWarning(
                "当前没有有效的敌人生成候选。",
                this
            );


            return;
        }


        for (int i = 0;
             i < currentSpawnCandidates.Count;
             i++)
        {
            EnemySpawnEntry entry =
                currentSpawnCandidates[i];


            Debug.Log(
                "Candidate "
                + i
                + ": Type="
                + entry.Type
                + ", Prefab="
                + entry.Prefab.name
                + ", Unlock Time="
                + entry.UnlockTime
                    .ToString("F1")
                + ", Spawn Weight="
                + entry.SpawnWeight
                    .ToString("F1"),
                entry.Prefab
            );
        }
    }


    private void TestWeightedSelectionAtTime(
        float testSurvivalTime
    )
    {
        EnemySpawnEntry selectedEntry =
            SelectWeightedSpawnEntry(
                testSurvivalTime
            );


        if (selectedEntry == null)
        {
            Debug.LogWarning(
                "===== Weighted Selection At "
                + testSurvivalTime
                    .ToString("F0")
                + " Seconds ====="
                + "\n没有有效候选，"
                + "本次随机选择已安全取消。",
                this
            );


            return;
        }


        Debug.Log(
            "===== Weighted Selection At "
            + testSurvivalTime
                .ToString("F0")
            + " Seconds ====="
            + "\nTotal Spawn Weight: "
            + currentSpawnWeightTotal
                .ToString("F1")
            + "\nSelection Succeeded: "
            + lastSpawnSelectionSucceeded
            + "\nSelected Type: "
            + selectedEntry.Type
            + "\nSelected Prefab: "
            + selectedEntry.Prefab.name
            + "\nSelected Weight: "
            + selectedEntry.SpawnWeight
                .ToString("F1"),
            selectedEntry.Prefab
        );
    }


    [ContextMenu(
        "Test Weighted Selection At 0 Seconds"
    )]
    private void TestWeightedSelectionAt0Seconds()
    {
        TestWeightedSelectionAtTime(
            0f
        );
    }


    [ContextMenu(
        "Test Weighted Selection At 15 Seconds"
    )]
    private void TestWeightedSelectionAt15Seconds()
    {
        TestWeightedSelectionAtTime(
            15f
        );
    }


    [ContextMenu(
        "Test Weighted Selection At 30 Seconds"
    )]
    private void TestWeightedSelectionAt30Seconds()
    {
        TestWeightedSelectionAtTime(
            30f
        );
    }


    [ContextMenu(
        "Test Weighted Selection At 45 Seconds"
    )]
    private void TestWeightedSelectionAt45Seconds()
    {
        TestWeightedSelectionAtTime(
            45f
        );
    }


    [ContextMenu(
        "Test Weighted Selection At 60 Seconds"
    )]
    private void TestWeightedSelectionAt60Seconds()
    {
        TestWeightedSelectionAtTime(
            60f
        );
    }


    [ContextMenu(
        "Debug Spawn Candidates At 0 Seconds"
    )]
    private void DebugSpawnCandidatesAt0Seconds()
    {
        LogSpawnCandidatesAtTime(
            0f
        );
    }


    [ContextMenu(
        "Debug Spawn Candidates At 15 Seconds"
    )]
    private void DebugSpawnCandidatesAt15Seconds()
    {
        LogSpawnCandidatesAtTime(
            15f
        );
    }


    [ContextMenu(
        "Debug Spawn Candidates At 30 Seconds"
    )]
    private void DebugSpawnCandidatesAt30Seconds()
    {
        LogSpawnCandidatesAtTime(
            30f
        );
    }


    [ContextMenu(
        "Debug Spawn Candidates At 45 Seconds"
    )]
    private void DebugSpawnCandidatesAt45Seconds()
    {
        LogSpawnCandidatesAtTime(
            45f
        );
    }


    [ContextMenu(
        "Debug Spawn Candidates At 60 Seconds"
    )]
    private void DebugSpawnCandidatesAt60Seconds()
    {
        LogSpawnCandidatesAtTime(
            60f
        );
    }


    // =========================================================
    // Weighted Batch Debug
    // =========================================================

    private void RunWeightedSelectionBatchTest(
        float testSurvivalTime,
        int sampleCount
    )
    {
        testSurvivalTime =
            Mathf.Max(
                0f,
                testSurvivalTime
            );


        sampleCount =
            Mathf.Max(
                1,
                sampleCount
            );


        RefreshSpawnCandidates(
            testSurvivalTime
        );


        if (currentSpawnCandidates.Count ==
            0
            ||
            currentSpawnWeightTotal <= 0f)
        {
            Debug.LogWarning(
                "===== Weighted Batch Test At "
                + testSurvivalTime
                    .ToString("F0")
                + " Seconds ====="
                + "\n没有有效候选，"
                + "批量随机测试已安全取消。",
                this
            );


            return;
        }


        Dictionary<EnemyType, int>
            selectionCounts =
                new Dictionary<
                    EnemyType,
                    int
                >();


        for (int i = 0;
             i < currentSpawnCandidates.Count;
             i++)
        {
            EnemyType candidateType =
                currentSpawnCandidates[i]
                    .Type;


            if (!selectionCounts
                    .ContainsKey(
                        candidateType
                    ))
            {
                selectionCounts.Add(
                    candidateType,
                    0
                );
            }
        }


        int successfulSelectionCount =
            0;


        int failedSelectionCount =
            0;


        for (int i = 0;
             i < sampleCount;
             i++)
        {
            EnemySpawnEntry selectedEntry =
                SelectWeightedSpawnEntry(
                    testSurvivalTime
                );


            if (selectedEntry == null)
            {
                failedSelectionCount++;

                continue;
            }


            EnemyType selectedType =
                selectedEntry.Type;


            if (!selectionCounts
                    .ContainsKey(
                        selectedType
                    ))
            {
                selectionCounts.Add(
                    selectedType,
                    0
                );
            }


            selectionCounts[
                selectedType
            ]++;


            successfulSelectionCount++;
        }


        string resultMessage =
            "===== Weighted Batch Test At "
            + testSurvivalTime
                .ToString("F0")
            + " Seconds ====="
            + "\nRequested Samples: "
            + sampleCount
            + "\nSuccessful Selections: "
            + successfulSelectionCount
            + "\nFailed Selections: "
            + failedSelectionCount
            + "\nTotal Spawn Weight: "
            + currentSpawnWeightTotal
                .ToString("F1");


        foreach (
            KeyValuePair<EnemyType, int>
                result
            in
            selectionCounts
        )
        {
            float percentage =
                0f;


            if (successfulSelectionCount >
                0)
            {
                percentage =
                    result.Value
                    *
                    100f
                    /
                    successfulSelectionCount;
            }


            resultMessage +=
                "\n"
                + result.Key
                + ": "
                + result.Value
                + " ("
                + percentage
                    .ToString("F2")
                + "%)";
        }


        Debug.Log(
            resultMessage,
            this
        );
    }


    [ContextMenu(
        "Batch Test 1000 Selections At 0 Seconds"
    )]
    private void BatchTestAt0Seconds()
    {
        RunWeightedSelectionBatchTest(
            0f,
            1000
        );
    }


    [ContextMenu(
        "Batch Test 1000 Selections At 15 Seconds"
    )]
    private void BatchTestAt15Seconds()
    {
        RunWeightedSelectionBatchTest(
            15f,
            1000
        );
    }


    [ContextMenu(
        "Batch Test 1000 Selections At 30 Seconds"
    )]
    private void BatchTestAt30Seconds()
    {
        RunWeightedSelectionBatchTest(
            30f,
            1000
        );
    }


    [ContextMenu(
        "Batch Test 1000 Selections At 45 Seconds"
    )]
    private void BatchTestAt45Seconds()
    {
        RunWeightedSelectionBatchTest(
            45f,
            1000
        );
    }


    [ContextMenu(
        "Batch Test 1000 Selections At 60 Seconds"
    )]
    private void BatchTestAt60Seconds()
    {
        RunWeightedSelectionBatchTest(
            60f,
            1000
        );
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        spawnInterval =
            Mathf.Max(
                0.01f,
                spawnInterval
            );


        minSpawnInterval =
            Mathf.Clamp(
                minSpawnInterval,
                0.01f,
                spawnInterval
            );


        spawnIntervalDecreasePerSecond =
            Mathf.Max(
                0f,
                spawnIntervalDecreasePerSecond
            );


        maxEnemies =
            Mathf.Max(
                1,
                maxEnemies
            );


        maxEnemiesLimit =
            Mathf.Max(
                maxEnemies,
                maxEnemiesLimit
            );


        maxEnemiesIncreaseInterval =
            Mathf.Max(
                0.1f,
                maxEnemiesIncreaseInterval
            );


        maxEnemiesIncreaseAmount =
            Mathf.Max(
                1,
                maxEnemiesIncreaseAmount
            );


        enemyInitialMaxHealth =
            Mathf.Max(
                1,
                enemyInitialMaxHealth
            );


        enemyHealthIncreaseInterval =
            Mathf.Max(
                0.1f,
                enemyHealthIncreaseInterval
            );


        enemyHealthIncreaseAmount =
            Mathf.Max(
                1,
                enemyHealthIncreaseAmount
            );


        enemyMaxHealthLimit =
            Mathf.Max(
                enemyInitialMaxHealth,
                enemyMaxHealthLimit
            );


        enemyInitialMoveSpeed =
            Mathf.Max(
                0.01f,
                enemyInitialMoveSpeed
            );


        enemyMoveSpeedIncreaseInterval =
            Mathf.Max(
                0.1f,
                enemyMoveSpeedIncreaseInterval
            );


        enemyMoveSpeedIncreaseAmount =
            Mathf.Max(
                0f,
                enemyMoveSpeedIncreaseAmount
            );


        enemyMoveSpeedLimit =
            Mathf.Max(
                enemyInitialMoveSpeed,
                enemyMoveSpeedLimit
            );


        enemyInitialContactDamage =
            Mathf.Max(
                1,
                enemyInitialContactDamage
            );


        enemyContactDamageIncreaseInterval =
            Mathf.Max(
                0.1f,
                enemyContactDamageIncreaseInterval
            );


        enemyContactDamageIncreaseAmount =
            Mathf.Max(
                1,
                enemyContactDamageIncreaseAmount
            );


        enemyContactDamageLimit =
            Mathf.Max(
                enemyInitialContactDamage,
                enemyContactDamageLimit
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


        spawnBoundsPadding =
            Mathf.Max(
                0f,
                spawnBoundsPadding
            );


        maxSpawnPositionAttempts =
            Mathf.Max(
                1,
                maxSpawnPositionAttempts
            );


        // -----------------------------------------------------
        // Phase38 Pressure Safety
        // -----------------------------------------------------

        minimumEffectiveSpawnInterval =
            Mathf.Clamp(
                minimumEffectiveSpawnInterval,
                0.05f,
                Mathf.Max(
                    0.05f,
                    spawnIntervalAtFinal
                )
            );


        maximumEffectiveEnemies =
            Mathf.Max(
                maxEnemiesAtFinal,
                maximumEffectiveEnemies
            );
    }


    // =========================================================
    // Gizmos
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Transform center =
            player;


        if (center == null)
        {
            GameObject playerObject =
                null;


            try
            {
                playerObject =
                    GameObject
                        .FindGameObjectWithTag(
                            playerTag
                        );
            }
            catch (UnityException)
            {
                // Editor 中 Player Tag 尚不存在时安全忽略。
            }


            if (playerObject != null)
            {
                center =
                    playerObject.transform;
            }
            else
            {
                center =
                    transform;
            }
        }


        Gizmos.color =
            Color.yellow;


        Gizmos.DrawWireSphere(
            center.position,
            minSpawnDistance
        );


        Gizmos.color =
            Color.red;


        Gizmos.DrawWireSphere(
            center.position,
            maxSpawnDistance
        );
    }
}