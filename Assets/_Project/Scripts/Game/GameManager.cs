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
        60f;

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
    // Result Info
    // =========================================================

    [Header("Result Info Text")]

    [SerializeField]
    private TextMeshProUGUI
        gameOverInfoText;

    [SerializeField]
    private TextMeshProUGUI
        victoryInfoText;


    // =========================================================
    // Player
    // =========================================================

    [Header("Player Reference")]

    [SerializeField]
    private PlayerExperience
        playerExperience;


    // =========================================================
    // Game Data
    // =========================================================

    [Header("Game Data")]

    [SerializeField]
    private int killCount;


    private float survivalTime;


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
            targetSurvivalTime -
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


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(
                gameObject
            );

            return;
        }


        Instance = this;


        // 每次进入测试场景时恢复游戏时间。
        Time.timeScale = 1f;
    }


    private void Start()
    {
        currentState =
            GameState.Playing;

        survivalTime = 0f;

        killCount = 0;


        if (playerExperience == null)
        {
            playerExperience =
                FindFirstObjectByType<
                    PlayerExperience
                >();
        }


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
        // 不推进生存时间。
        if (UpgradeManager.IsChoosingUpgrade ||
            WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            return;
        }


        survivalTime +=
            Time.deltaTime;


        RefreshTimeUI();


        if (survivalTime >=
            targetSurvivalTime)
        {
            EnterVictory();
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
    // Game Over
    // =========================================================

    private void EnterGameOver()
    {
        currentState =
            GameState.GameOver;


        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                true
            );
        }


        if (victoryPanel != null)
        {
            victoryPanel.SetActive(
                false
            );
        }


        UpdateResultInfo(
            gameOverInfoText,
            false
        );


        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayGameOver();
        }


        Time.timeScale = 0f;


        Debug.Log(
            "Game Over"
        );
    }


    // =========================================================
    // Victory
    // =========================================================

    private void EnterVictory()
    {
        if (currentState !=
            GameState.Playing)
        {
            return;
        }


        currentState =
            GameState.Victory;


        if (victoryPanel != null)
        {
            victoryPanel.SetActive(
                true
            );
        }


        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                false
            );
        }


        UpdateResultInfo(
            victoryInfoText,
            true
        );


        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .PlayVictory();
        }


        Time.timeScale = 0f;


        Debug.Log(
            "Victory"
        );
    }


    // =========================================================
    // Result Panels
    // =========================================================

    private void HideResultPanels()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(
                false
            );
        }


        if (victoryPanel != null)
        {
            victoryPanel.SetActive(
                false
            );
        }
    }


    // =========================================================
    // HUD
    // =========================================================

    private void RefreshTimeUI()
    {
        if (HUDManager.Instance == null)
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
        if (HUDManager.Instance == null)
        {
            return;
        }


        HUDManager.Instance
            .UpdateKillCountUI(
                killCount
            );
    }


    // =========================================================
    // Result Info
    // =========================================================

    private void UpdateResultInfo(
        TextMeshProUGUI resultInfoText,
        bool victory
    )
    {
        if (resultInfoText == null)
        {
            return;
        }


        int currentLevel = 1;


        if (playerExperience != null)
        {
            currentLevel =
                playerExperience
                    .CurrentLevel;
        }


        int carriedItemCount = 0;

        int lootValue = 0;


        if (RunInventory.Instance != null)
        {
            carriedItemCount =
                RunInventory.Instance
                    .TotalItemCount;

            lootValue =
                RunInventory.Instance
                    .TotalLootValue;
        }


        string lootResultTitle =
            victory
                ? "RUN LOOT"
                : "LOOT LOST";


        resultInfoText.text =
            "Survival Time: "
            + FormatTime(
                survivalTime
            )
            + "\n"
            + "Level: "
            + currentLevel
            + "\n"
            + "Kill Count: "
            + killCount
            + "\n\n"
            + lootResultTitle
            + "\n"
            + "Items Carried: "
            + carriedItemCount
            + "\n"
            + "Loot Value: "
            + lootValue;
    }


    // =========================================================
    // Restart
    // =========================================================

    public void RestartGame()
    {
        Time.timeScale = 1f;


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
                time
            );


        int minutes =
            totalSeconds / 60;

        int seconds =
            totalSeconds % 60;


        return
            minutes.ToString("00")
            + ":"
            + seconds.ToString("00");
    }
}