using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Controller")]
    [SerializeField]
    private AudioSettingsPanel audioSettingsPanel;

    [Header("Scene")]
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        IsPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        // 设置界面打开时，Esc 只返回暂停主面板。
        if (settingsPanel != null
            && settingsPanel.activeSelf)
        {
            CloseSettings();
            return;
        }

        if (IsPaused)
        {
            ResumeGame();
            return;
        }

        PauseGame();
    }

    /// <summary>
    /// 只有游戏仍在 Playing 状态，并且没有正在进行
    /// 升级三选一时，才允许打开普通暂停菜单。
    /// </summary>
    private bool CanPause()
    {
        if (GameManager.Instance == null
            || !GameManager.Instance.IsPlaying)
        {
            return false;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        if (WeaponModuleSelectionManager.IsChoosingModule)
        {
            return false;
        }

        return true;
    }
    public void PauseGame()
    {
        if (IsPaused || !CanPause())
        {
            return;
        }

        IsPaused = true;
        Time.timeScale = 0f;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        if (!IsPaused)
        {
            return;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        IsPaused = false;
        Time.timeScale = 1f;
    }

    public void OpenSettings()
    {
        if (!IsPaused)
        {
            return;
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (audioSettingsPanel != null)
        {
            audioSettingsPanel.OpenPanel();
            return;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
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
            settingsPanel.SetActive(false);
        }

        ShowPausePanelAfterSettingsClosed();
    }

    /// <summary>
    /// 供 AudioSettingsPanel 的 On Panel Closed 事件调用。
    /// 关闭设置界面后仍保持暂停，只恢复暂停主面板。
    /// </summary>
    public void ShowPausePanelAfterSettingsClosed()
    {
        if (!IsPaused)
        {
            return;
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void RestartGame()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SaveVolumeSettings();
        }

        IsPaused = false;
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void LoadMainMenu()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SaveVolumeSettings();
        }

        IsPaused = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }

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