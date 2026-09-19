using UnityEngine;
using UnityEngine.SceneManagement;


[DisallowMultipleComponent]
public class PauseMenuController :
    MonoBehaviour
{
    [Header("Panels")]
    [SerializeField]
    private GameObject pausePanel;

    [SerializeField]
    private GameObject settingsPanel;


    [Header("Settings Controller")]
    [SerializeField]
    private AudioSettingsPanel audioSettingsPanel;


    [Header("Scene")]
    [SerializeField]
    private string mainMenuSceneName =
        "MainMenu";


    public static bool IsPaused
    {
        get;
        private set;
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        IsPaused = false;


        if (pausePanel != null)
        {
            pausePanel.SetActive(
                false
            );
        }


        if (settingsPanel != null)
        {
            settingsPanel.SetActive(
                false
            );
        }
    }


    private void Update()
    {
        if (!Input.GetKeyDown(
                KeyCode.Escape
            ))
        {
            return;
        }


        // =====================================================
        // Settings
        // =====================================================

        if (settingsPanel != null &&
            settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }


        // =====================================================
        // Existing Pause
        // =====================================================

        if (IsPaused)
        {
            ResumeGame();
            return;
        }


        // =====================================================
        // Backpack Priority
        // =====================================================
        //
        // 第一次 Esc：
        // Backpack → Close
        //
        // 不在同一帧继续进入 Pause。
        // =====================================================

        if (BackpackPanelController.IsOpen)
        {
            if (BackpackPanelController
                    .Instance != null)
            {
                BackpackPanelController
                    .Instance
                    .ClosePanel();
            }

            return;
        }


        // =====================================================
        // Loot Search Priority
        // =====================================================
        //
        // 第一次 Esc：
        // Search → Close
        //
        // 第二次 Esc：
        // Normal Gameplay → Pause
        // =====================================================

        if (LootSearchPanelController.IsOpen)
        {
            if (LootSearchPanelController
                    .Instance != null)
            {
                LootSearchPanelController
                    .Instance
                    .CloseActiveSearch();
            }

            return;
        }


        // =====================================================
        // Normal Pause
        // =====================================================

        PauseGame();
    }


    // =========================================================
    // Permission
    // =========================================================

    private bool CanPause()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
        {
            return false;
        }


        if (UpgradeManager.IsChoosingUpgrade)
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
    // Pause
    // =========================================================

    public void PauseGame()
    {
        if (IsPaused ||
            !CanPause())
        {
            return;
        }


        // 如果其他代码直接调用 PauseGame()，
        // 也保证 Loot Search 不会留在背后。

        if (LootSearchPanelController.IsOpen &&
            LootSearchPanelController.Instance != null)
        {
            LootSearchPanelController
                .Instance
                .CloseActiveSearch();
        }


        // 如果其他代码直接调用 PauseGame()，
        // Backpack 也不能继续保持打开。

        if (BackpackPanelController.IsOpen &&
            BackpackPanelController.Instance != null)
        {
            BackpackPanelController
                .Instance
                .ClosePanel();
        }


        IsPaused = true;

        Time.timeScale = 0f;


        if (settingsPanel != null)
        {
            settingsPanel.SetActive(
                false
            );
        }


        if (pausePanel != null)
        {
            pausePanel.SetActive(
                true
            );
        }
    }


    // =========================================================
    // Resume
    // =========================================================

    public void ResumeGame()
    {
        if (!IsPaused)
        {
            return;
        }


        if (settingsPanel != null)
        {
            settingsPanel.SetActive(
                false
            );
        }


        if (pausePanel != null)
        {
            pausePanel.SetActive(
                false
            );
        }


        IsPaused = false;

        Time.timeScale = 1f;
    }


    // =========================================================
    // Settings
    // =========================================================

    public void OpenSettings()
    {
        if (!IsPaused)
        {
            return;
        }


        if (pausePanel != null)
        {
            pausePanel.SetActive(
                false
            );
        }


        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.OpenPanel();
            return;
        }


        if (settingsPanel != null)
        {
            settingsPanel.SetActive(
                true
            );
        }
    }


    public void CloseSettings()
    {
        if (!IsPaused)
        {
            return;
        }


        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.ClosePanel();
            return;
        }


        if (settingsPanel != null)
        {
            settingsPanel.SetActive(
                false
            );
        }


        ShowPausePanelAfterSettingsClosed();
    }


    public void ShowPausePanelAfterSettingsClosed()
    {
        if (!IsPaused)
        {
            return;
        }


        if (pausePanel != null)
        {
            pausePanel.SetActive(
                true
            );
        }
    }


    // =========================================================
    // Restart
    // =========================================================

    public void RestartGame()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .SaveVolumeSettings();
        }


        IsPaused = false;

        Time.timeScale = 1f;


        if (GameManager.Instance != null)
        {
            GameManager.Instance
                .RestartGame();

            return;
        }


        Scene currentScene =
            SceneManager.GetActiveScene();


        SceneManager.LoadScene(
            currentScene.name
        );
    }


    // =========================================================
    // Main Menu
    // =========================================================

    public void LoadMainMenu()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance
                .SaveVolumeSettings();
        }


        IsPaused = false;

        Time.timeScale = 1f;


        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }


    // =========================================================
    // Cleanup
    // =========================================================

    private void OnDestroy()
    {
        if (!IsPaused)
        {
            return;
        }


        IsPaused = false;

        Time.timeScale = 1f;
    }
}