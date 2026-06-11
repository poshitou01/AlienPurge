using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    GameOver,
    Victory
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Playing;

    [Header("Survival Timer")]
    [SerializeField] private float targetSurvivalTime = 60f;
    [SerializeField] private bool showRemainingTime = false;

    [Header("Result Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Result Info Text")]
    [SerializeField] private TextMeshProUGUI gameOverInfoText;
    [SerializeField] private TextMeshProUGUI victoryInfoText;

    [Header("Player Reference")]
    [SerializeField] private PlayerExperience playerExperience;

    private float survivalTime;

    public GameState CurrentState => currentState;
    public float SurvivalTime => survivalTime;
    public float TargetSurvivalTime => targetSurvivalTime;
    public float RemainingTime => Mathf.Max(0f, targetSurvivalTime - survivalTime);

    public bool IsPlaying => currentState == GameState.Playing;
    public bool IsGameOver => currentState == GameState.GameOver;
    public bool IsVictory => currentState == GameState.Victory;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 每次进入测试场景时，确保游戏时间恢复正常
        Time.timeScale = 1f;
    }

    private void Start()
    {
        currentState = GameState.Playing;
        survivalTime = 0f;

        if (playerExperience == null)
        {
            playerExperience = FindFirstObjectByType<PlayerExperience>();
        }

        HideResultPanels();

        RefreshTimeUI();
    }

    private void Update()
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

        // 如果正在升级三选一，不计算生存时间
        if (UpgradeManager.IsChoosingUpgrade)
        {
            return;
        }

        survivalTime += Time.deltaTime;

        RefreshTimeUI();

        if (survivalTime >= targetSurvivalTime)
        {
            EnterVictory();
        }
    }

    public void OnPlayerDied()
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

        EnterGameOver();
    }

    private void EnterGameOver()
    {
        currentState = GameState.GameOver;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        UpdateResultInfo(gameOverInfoText);

        Time.timeScale = 0f;

        Debug.Log("Game Over");
    }

    private void EnterVictory()
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

        currentState = GameState.Victory;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        UpdateResultInfo(victoryInfoText);

        Time.timeScale = 0f;

        Debug.Log("Victory");
    }

    private void HideResultPanels()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }

    private void RefreshTimeUI()
    {
        if (HUDManager.Instance == null)
        {
            return;
        }

        HUDManager.Instance.UpdateTimeUI(survivalTime, targetSurvivalTime, showRemainingTime);
    }

    private void UpdateResultInfo(TextMeshProUGUI resultInfoText)
    {
        if (resultInfoText == null)
        {
            return;
        }

        int currentLevel = 1;

        if (playerExperience != null)
        {
            currentLevel = playerExperience.CurrentLevel;
        }

        resultInfoText.text =
            "Survival Time: " + FormatTime(survivalTime) + "\n" +
            "Level: " + currentLevel;
    }

    public void RestartGame()
    {
        // 重新加载场景前，必须恢复时间
        Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public static string FormatTime(float time)
    {
        int totalSeconds = Mathf.FloorToInt(time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }
}