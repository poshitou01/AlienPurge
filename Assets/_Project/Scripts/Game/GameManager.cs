using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


public enum GameState
{
    Playing,
    GameOver,
    Victory
}


public class GameManager :
    MonoBehaviour
{
    // =========================================================
    // Singleton
    // =========================================================

    public static GameManager Instance
    {
        get;
        private set;
    }


    // =========================================================
    // Game State
    // =========================================================

    [Header("Game State")]

    [SerializeField]
    private GameState currentState =
        GameState.Playing;


    // =========================================================
    // Survival Timer
    // =========================================================

    [Header("Survival Timer")]

    [SerializeField]
    private float targetSurvivalTime =
        360f;


    [SerializeField]
    private bool showRemainingTime =
        false;


    // =========================================================
    // Result Panels
    // =========================================================

    [Header("Result Panels")]

    [SerializeField]
    private GameObject gameOverPanel;


    [SerializeField]
    private GameObject victoryPanel;


    // =========================================================
    // Result Text
    // =========================================================

    [Header("Result Info Text")]

    [SerializeField]
    private TextMeshProUGUI
        gameOverInfoText;


    [SerializeField]
    private TextMeshProUGUI
        victoryInfoText;


    [Tooltip(
        "结果界面最多显示多少种不同 Loot。"
        + "Snapshot 本身仍保存完整列表。"
    )]
    [Min(1)]


    // =========================================================
    // Gameplay References
    // =========================================================

    [Header("Gameplay References")]

    [SerializeField]
    private PlayerExperience
        playerExperience;


    [SerializeField]
    private MissionManager
        missionManager;


    // =========================================================
    // Game Data
    // =========================================================

    [Header("Game Data")]

    [SerializeField]
    private int killCount;


    private float survivalTime;


    // =========================================================
    // Run Result Snapshot
    // =========================================================

    [Header("Run Result Snapshot")]

    [SerializeField]
    private RunResultSnapshot
        currentRunResultSnapshot;


    // =========================================================
    // Read Only
    // =========================================================

    public GameState CurrentState =>
        currentState;


    public float SurvivalTime =>
        survivalTime;


    public float TargetSurvivalTime =>
        targetSurvivalTime;


    public float RemainingTime =>
        Mathf.Max(
            0f,
            targetSurvivalTime
            -
            survivalTime
        );


    public int KillCount =>
        killCount;


    public bool IsPlaying =>
        currentState ==
        GameState.Playing;


    public bool IsGameOver =>
        currentState ==
        GameState.GameOver;


    public bool IsVictory =>
        currentState ==
        GameState.Victory;


    public RunResultSnapshot
        CurrentRunResultSnapshot =>
            currentRunResultSnapshot;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null
            &&
            Instance != this)
        {
            Destroy(
                gameObject
            );


            return;
        }


        Instance =
            this;


        Time.timeScale =
            1f;
    }


    private void Start()
    {
        currentState =
            GameState.Playing;


        survivalTime =
            0f;


        killCount =
            0;


        currentRunResultSnapshot =
            null;


        ResolveReferences();


        HideResultPanels();


        RefreshTimeUI();

        RefreshKillCountUI();


        Debug.Log(
            "Game started. Kill Count = 0"
        );
    }


    private void Update()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        // Upgrade / Module Selection
        // 不推进正式生存时间。
        if (UpgradeManager
                .IsChoosingUpgrade
            ||
            WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            return;
        }


        survivalTime +=
            Time.deltaTime;


        RefreshTimeUI();


        // =====================================================
        // Phase38
        // =====================================================
        //
        // 不再：
        //
        // survivalTime >= 360
        // -> EnterVictory()
        //
        // 315 / 330 / 360+
        // 全部由 ExtractionController 负责。
        //
        // Run 成功只能通过：
        //
        // CompleteExtraction()
        // =====================================================
    }


    private void OnDestroy()
    {
        if (Instance ==
            this)
        {
            Instance =
                null;
        }
    }


    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (playerExperience ==
            null)
        {
            playerExperience =
                FindFirstObjectByType<
                    PlayerExperience
                >();
        }


        if (missionManager ==
            null)
        {
            missionManager =
                FindFirstObjectByType<
                    MissionManager
                >();
        }
    }


    // =========================================================
    // Player Death
    // =========================================================

    public void OnPlayerDied()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        EnterGameOver();
    }


    // =========================================================
    // Enemy Kill
    // =========================================================

    public void RegisterEnemyKilled()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        killCount++;


        RefreshKillCountUI();


        Debug.Log(
            "Enemy killed. Current Kill Count: "
            + killCount
        );
    }


    public void AddKillCount()
    {
        RegisterEnemyKilled();
    }


    // =========================================================
    // Extraction Success
    // =========================================================

    public void CompleteExtraction()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        EnterVictory();
    }


    // =========================================================
    // Game Over
    // =========================================================

    private void EnterGameOver()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        // -----------------------------------------------------
        // Snapshot MUST happen before runtime data
        // is allowed to change.
        // -----------------------------------------------------

        CaptureRunResult(
            RunOutcome.PlayerDeath
        );


        currentState =
            GameState.GameOver;


        CloseGameplayPanelsBeforeResult();


        if (gameOverPanel !=
            null)
        {
            gameOverPanel.SetActive(
                true
            );
        }


        if (victoryPanel !=
            null)
        {
            victoryPanel.SetActive(
                false
            );
        }


        UpdateResultInfo(
            gameOverInfoText,
            currentRunResultSnapshot
        );


        if (AudioManager.Instance !=
            null)
        {
            AudioManager.Instance
                .PlayGameOver();
        }


        Time.timeScale =
            0f;


        Debug.Log(
            "===== MISSION FAILED ====="
            + "\nLoot Lost: "
            + (
                currentRunResultSnapshot != null
                    ?
                    currentRunResultSnapshot
                        .LootTotalValue
                    :
                    0
            )
        );
    }


    // =========================================================
    // Victory / Extraction Success
    // =========================================================

    private void EnterVictory()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        // -----------------------------------------------------
        // Capture first.
        // -----------------------------------------------------

        CaptureRunResult(
            RunOutcome
                .ExtractionSuccess
        );


        currentState =
            GameState.Victory;


        CloseGameplayPanelsBeforeResult();


        if (victoryPanel !=
            null)
        {
            victoryPanel.SetActive(
                true
            );
        }


        if (gameOverPanel !=
            null)
        {
            gameOverPanel.SetActive(
                false
            );
        }


        UpdateResultInfo(
            victoryInfoText,
            currentRunResultSnapshot
        );


        if (AudioManager.Instance !=
            null)
        {
            AudioManager.Instance
                .PlayVictory();
        }


        Time.timeScale =
            0f;


        Debug.Log(
            "===== EXTRACTION SUCCESS ====="
            + "\nLoot Secured: "
            + (
                currentRunResultSnapshot != null
                    ?
                    currentRunResultSnapshot
                        .LootTotalValue
                    :
                    0
            )
        );
    }


    // =========================================================
    // Snapshot
    // =========================================================

    private void CaptureRunResult(
        RunOutcome outcome
    )
    {
        // -----------------------------------------------------
        // Terminal state should only capture once.
        // -----------------------------------------------------

        if (currentRunResultSnapshot !=
            null)
        {
            Debug.LogWarning(
                "GameManager: "
                + "RunResultSnapshot already exists. "
                + "Duplicate capture ignored.",
                this
            );


            return;
        }


        ResolveReferences();


        currentRunResultSnapshot =
            RunResultSnapshot.Capture(
                outcome,
                survivalTime,
                killCount,
                playerExperience,
                missionManager,
                RunInventory.Instance
            );


        Debug.Log(
            "===== RUN RESULT SNAPSHOT CAPTURED ====="
            + "\nOutcome: "
            + currentRunResultSnapshot
                .Outcome
            + "\nSurvival Time: "
            + currentRunResultSnapshot
                .SurvivalTime
                .ToString("F2")
            + "\nLevel: "
            + currentRunResultSnapshot
                .PlayerLevel
            + "\nKills: "
            + currentRunResultSnapshot
                .KillCount
            + "\nMissions Completed: "
            + currentRunResultSnapshot
                .CompletedMissionCount
            + "\nItems: "
            + currentRunResultSnapshot
                .LootItemCount
            + "\nLoot Value: "
            + currentRunResultSnapshot
                .LootTotalValue,
            this
        );
    }


    // =========================================================
    // Gameplay UI Cleanup
    // =========================================================

    private void
        CloseGameplayPanelsBeforeResult()
    {
        // -----------------------------------------------------
        // Backpack
        // -----------------------------------------------------

        if (BackpackPanelController
                .IsOpen
            &&
            BackpackPanelController
                .Instance != null)
        {
            BackpackPanelController
                .Instance
                .ClosePanel();
        }


        // -----------------------------------------------------
        // Loot Search
        // -----------------------------------------------------

        if (LootSearchPanelController
                .IsOpen
            &&
            LootSearchPanelController
                .Instance != null)
        {
            LootSearchPanelController
                .Instance
                .CloseActiveSearch();
        }
    }


    // =========================================================
    // Result Panels
    // =========================================================

    private void HideResultPanels()
    {
        if (gameOverPanel !=
            null)
        {
            gameOverPanel.SetActive(
                false
            );
        }


        if (victoryPanel !=
            null)
        {
            victoryPanel.SetActive(
                false
            );
        }
    }


    // =========================================================
    // Result Info
    // =========================================================

    private void UpdateResultInfo(
        TextMeshProUGUI resultInfoText,
        RunResultSnapshot snapshot
    )
    {
        if (resultInfoText ==
            null)
        {
            return;
        }


        if (snapshot ==
            null)
        {
            resultInfoText.text =
                "RUN RESULT UNAVAILABLE";


            return;
        }


        StringBuilder builder =
            new StringBuilder(
                512
            );


        bool success =
            snapshot
                .IsExtractionSuccess;





        // -----------------------------------------------------
        // Run Stats
        // -----------------------------------------------------

        builder.Append(
            "Survival Time: "
        );


        builder.AppendLine(
            FormatTime(
                snapshot
                    .SurvivalTime
            )
        );


        builder.Append(
            "Level: "
        );


        builder.AppendLine(
            snapshot
                .PlayerLevel
                .ToString()
        );


        builder.Append(
            "Kill Count: "
        );


        builder.AppendLine(
            snapshot
                .KillCount
                .ToString()
        );


        builder.Append(
            "Missions Completed: "
        );


        builder.AppendLine(
            snapshot
                .CompletedMissionCount
                .ToString()
        );


        // -----------------------------------------------------
        // Loot Outcome
        // -----------------------------------------------------

        builder.AppendLine();


        builder.AppendLine(
            success
                ?
                "LOOT SECURED"
                :
                "LOOT LOST"
        );


        builder.Append(
            success
                ?
                "Items Secured: "
                :
                "Items Lost: "
        );


        builder.AppendLine(
            snapshot
                .LootItemCount
                .ToString()
        );


        builder.Append(
            success
                ?
                "Secured Value: "
                :
                "Lost Value: "
        );


        builder.AppendLine(
            snapshot
                .LootTotalValue
                .ToString()
        );




        resultInfoText.text =
            builder.ToString();
    }




    // =========================================================
    // HUD
    // =========================================================

    private void RefreshTimeUI()
    {
        if (HUDManager.Instance ==
            null)
        {
            return;
        }


        HUDManager.Instance
            .UpdateTimeUI(
                survivalTime,
                targetSurvivalTime,
                showRemainingTime
            );
    }


    private void RefreshKillCountUI()
    {
        if (HUDManager.Instance ==
            null)
        {
            return;
        }


        HUDManager.Instance
            .UpdateKillCountUI(
                killCount
            );
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Print Run Result Snapshot"
    )]
    private void
        DebugPrintRunResultSnapshot()
    {
        if (!Application.isPlaying)
        {
            return;
        }


        if (currentRunResultSnapshot ==
            null)
        {
            Debug.Log(
                "RunResultSnapshot: None",
                this
            );


            return;
        }


        StringBuilder builder =
            new StringBuilder();


        builder.AppendLine(
            "===== CURRENT RUN RESULT SNAPSHOT ====="
        );


        builder.Append(
            "Outcome: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .Outcome
                .ToString()
        );


        builder.Append(
            "Survival Time: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .SurvivalTime
                .ToString("F2")
        );


        builder.Append(
            "Kills: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .KillCount
                .ToString()
        );


        builder.Append(
            "Level: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .PlayerLevel
                .ToString()
        );


        builder.Append(
            "Missions Completed: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .CompletedMissionCount
                .ToString()
        );


        builder.Append(
            "Loot Items: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .LootItemCount
                .ToString()
        );


        builder.Append(
            "Loot Value: "
        );


        builder.AppendLine(
            currentRunResultSnapshot
                .LootTotalValue
                .ToString()
        );


        Debug.Log(
            builder.ToString(),
            this
        );
    }


    // =========================================================
    // Restart
    // =========================================================

    public void RestartGame()
    {
        Time.timeScale =
            1f;


        Scene currentScene =
            SceneManager
                .GetActiveScene();


        SceneManager.LoadScene(
            currentScene.name
        );
    }


    // =========================================================
    // Formatting
    // =========================================================

    public static string FormatTime(
        float time
    )
    {
        int totalSeconds =
            Mathf.FloorToInt(
                Mathf.Max(
                    0f,
                    time
                )
            );


        int minutes =
            totalSeconds / 60;


        int seconds =
            totalSeconds % 60;


        return
            minutes.ToString("00")
            +
            ":"
            +
            seconds.ToString("00");
    }


    // =========================================================
    // Validation
    // =========================================================

    private void OnValidate()
    {
        targetSurvivalTime =
            Mathf.Max(
                1f,
                targetSurvivalTime
            );



    }
}