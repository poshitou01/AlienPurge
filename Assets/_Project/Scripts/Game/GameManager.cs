using UnityEngine;

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

    private float survivalTime;

    public GameState CurrentState => currentState;
    public float SurvivalTime => survivalTime;
    public float TargetSurvivalTime => targetSurvivalTime;
    public float RemainingTime => Mathf.Max(0f, targetSurvivalTime - survivalTime);

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

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

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

        Time.timeScale = 0f;

        Debug.Log("Game Over");
    }

    private void EnterVictory()
    {
        currentState = GameState.Victory;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        Time.timeScale = 0f;

        Debug.Log("Victory");
    }

    private void RefreshTimeUI()
    {
        if (HUDManager.Instance == null)
        {
            return;
        }

        HUDManager.Instance.UpdateTimeUI(survivalTime, targetSurvivalTime, showRemainingTime);
    }
}