using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AudioSettingsPanel : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider uiVolumeSlider;

    [Header("Percentage Text")]
    [SerializeField] private TMP_Text masterVolumeText;
    [SerializeField] private TMP_Text musicVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;
    [SerializeField] private TMP_Text uiVolumeText;

    [Header("Panel")]
    [Tooltip("不指定时默认使用当前挂载脚本的对象")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("关闭设置面板后需要执行的额外操作")]
    [SerializeField] private UnityEvent onPanelClosed;

    private bool listenersRegistered;

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }
    }

    private void Start()
    {
        RefreshFromAudioManager();
    }

    private void OnEnable()
    {
        RegisterSliderListeners();
        RefreshFromAudioManager();
    }

    private void OnDisable()
    {
        SaveCurrentSettings();
        UnregisterSliderListeners();
    }

    private void OnDestroy()
    {
        UnregisterSliderListeners();
    }

    /// <summary>
    /// 显示设置面板。
    /// OnEnable 会自动读取当前音量并刷新界面。
    /// </summary>
    public void OpenPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    /// <summary>
    /// 保存当前音量并关闭设置面板。
    /// </summary>
    public void ClosePanel()
    {
        SaveCurrentSettings();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        onPanelClosed?.Invoke();
    }

    /// <summary>
    /// 恢复第二十八阶段确定的默认音量，
    /// 然后立即刷新四个 Slider 和百分比文字。
    /// </summary>
    public void ResetDefaults()
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        audioManager.ResetVolumeSettings();
        RefreshFromAudioManager();
    }

    /// <summary>
    /// 从 AudioManager 读取当前音量。
    /// 使用 SetValueWithoutNotify，避免初始化时触发音量回调。
    /// </summary>
    public void RefreshFromAudioManager()
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        SetSliderValueWithoutNotify(
            masterVolumeSlider,
            audioManager.MasterVolume
        );

        SetSliderValueWithoutNotify(
            musicVolumeSlider,
            audioManager.MusicVolume
        );

        SetSliderValueWithoutNotify(
            sfxVolumeSlider,
            audioManager.SfxVolume
        );

        SetSliderValueWithoutNotify(
            uiVolumeSlider,
            audioManager.UiVolume
        );

        UpdatePercentageText(
            masterVolumeText,
            audioManager.MasterVolume
        );

        UpdatePercentageText(
            musicVolumeText,
            audioManager.MusicVolume
        );

        UpdatePercentageText(
            sfxVolumeText,
            audioManager.SfxVolume
        );

        UpdatePercentageText(
            uiVolumeText,
            audioManager.UiVolume
        );
    }

    private void RegisterSliderListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(
                HandleMasterVolumeChanged
            );
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(
                HandleMusicVolumeChanged
            );
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(
                HandleSfxVolumeChanged
            );
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.onValueChanged.AddListener(
                HandleUiVolumeChanged
            );
        }

        listenersRegistered = true;
    }

    private void UnregisterSliderListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(
                HandleMasterVolumeChanged
            );
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(
                HandleMusicVolumeChanged
            );
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(
                HandleSfxVolumeChanged
            );
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.onValueChanged.RemoveListener(
                HandleUiVolumeChanged
            );
        }

        listenersRegistered = false;
    }

    private void HandleMasterVolumeChanged(float value)
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        audioManager.SetMasterVolume(value);

        UpdatePercentageText(
            masterVolumeText,
            audioManager.MasterVolume
        );
    }

    private void HandleMusicVolumeChanged(float value)
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        audioManager.SetMusicVolume(value);

        UpdatePercentageText(
            musicVolumeText,
            audioManager.MusicVolume
        );
    }

    private void HandleSfxVolumeChanged(float value)
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        audioManager.SetSfxVolume(value);

        UpdatePercentageText(
            sfxVolumeText,
            audioManager.SfxVolume
        );
    }

    private void HandleUiVolumeChanged(float value)
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        audioManager.SetUiVolume(value);

        UpdatePercentageText(
            uiVolumeText,
            audioManager.UiVolume
        );
    }

    private void SaveCurrentSettings()
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            return;
        }

        audioManager.SaveVolumeSettings();
    }

    private static void SetSliderValueWithoutNotify(
        Slider slider,
        float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(
            Mathf.Clamp01(value)
        );
    }

    private static void UpdatePercentageText(
        TMP_Text percentageText,
        float value)
    {
        if (percentageText == null)
        {
            return;
        }

        int percentage = Mathf.RoundToInt(
            Mathf.Clamp01(value) * 100f
        );

        percentageText.text = $"{percentage}%";
    }
}