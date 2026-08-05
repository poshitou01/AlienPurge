using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MasterVolumeKey =
    "AlienPurge.Audio.Master";

    private const string MusicVolumeKey =
        "AlienPurge.Audio.Music";

    private const string SfxVolumeKey =
        "AlienPurge.Audio.SFX";

    private const string UiVolumeKey =
        "AlienPurge.Audio.UI";

    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.32f;
    private const float DefaultSfxVolume = 1f;
    private const float DefaultUiVolume = 1f;

    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
    public float UiVolume => uiVolume;

    [Header("Audio Sources")]
    [Tooltip("负责播放和循环背景音乐")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("负责播放战斗、玩家、拾取和胜负音效")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("负责播放按钮等 UI 音效")]
    [SerializeField] private AudioSource uiSource;

    [Header("Scene Names")]
    [SerializeField]
    private string mainMenuSceneName =
        "MainMenu";

    [SerializeField]
    private string gameplaySceneName =
        "Day01_TestGround";

    [Header("Background Music")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip gameplayMusic;

    [Header("Combat SFX")]
    [SerializeField] private AudioClip playerShootClip;
    [SerializeField] private AudioClip enemyHitClip;

    [Header("Enemy Death SFX")]
    [SerializeField]
    private AudioClip enemyDeathNormalClip;

    [SerializeField]
    private AudioClip enemyDeathFastClip;

    [SerializeField]
    private AudioClip enemyDeathHeavyClip;

    [Header("Player SFX")]
    [SerializeField] private AudioClip playerHurtClip;
    [SerializeField] private AudioClip playerDeathClip;
    [SerializeField] private AudioClip playerDashClip;
    [SerializeField] private AudioClip playerJetJumpClip;
    [SerializeField] private AudioClip playerPulseClip;

    [Header("Pickup And Progression SFX")]
    [SerializeField]
    private AudioClip experiencePickupClip;

    [SerializeField] private AudioClip levelUpClip;

    [Header("Game State SFX")]
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] private AudioClip victoryClip;

    [Header("UI SFX")]
    [SerializeField] private AudioClip buttonClickClip;

    [Header("Bus Volumes")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.32f;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float uiVolume = 1f;

    [Header("Individual SFX Volumes")]
    [Range(0f, 1f)]
    [SerializeField] private float playerShootVolume = 0.22f;

    [Range(0f, 1f)]
    [SerializeField] private float enemyHitVolume = 0.14f;

    [Range(0f, 1f)]
    [SerializeField]
    private float enemyDeathNormalVolume = 0.28f;

    [Range(0f, 1f)]
    [SerializeField]
    private float enemyDeathFastVolume = 0.24f;

    [Range(0f, 1f)]
    [SerializeField]
    private float enemyDeathHeavyVolume = 0.34f;

    [Range(0f, 1f)]
    [SerializeField] private float playerHurtVolume = 0.42f;

    [Range(0f, 1f)]
    [SerializeField] private float playerDeathVolume = 0.55f;

    [Range(0f, 1f)]
    [SerializeField] private float playerDashVolume = 0.38f;

    [Range(0f, 1f)]
    [SerializeField]
    private float playerJetJumpVolume = 0.42f;

    [Range(0f, 1f)]
    [SerializeField]
    private float playerPulseVolume = 0.5f;

    [Range(0f, 1f)]
    [SerializeField]
    private float experiencePickupVolume = 0.18f;

    [Range(0f, 1f)]
    [SerializeField] private float levelUpVolume = 0.52f;

    [Range(0f, 1f)]
    [SerializeField] private float gameOverVolume = 0.58f;

    [Range(0f, 1f)]
    [SerializeField] private float victoryVolume = 0.58f;

    [Range(0f, 1f)]
    [SerializeField] private float buttonClickVolume = 0.22f;

    [Header("High Frequency SFX Throttling")]
    [Tooltip("两次射击音效之间允许的最短真实时间")]
    [Min(0f)]
    [SerializeField]
    private float minimumShootInterval =
        0.035f;

    [Tooltip("两次命中音效之间允许的最短真实时间")]
    [Min(0f)]
    [SerializeField]
    private float minimumEnemyHitInterval =
        0.035f;

    [Tooltip("两次经验拾取音效之间允许的最短真实时间")]
    [Min(0f)]
    [SerializeField]
    private float minimumExperiencePickupInterval =
        0.04f;

    [Tooltip("两次按钮点击音效之间允许的最短真实时间")]
    [Min(0f)]
    [SerializeField]
    private float minimumButtonClickInterval =
        0.03f;

    private float lastShootTime =
        float.NegativeInfinity;

    private float lastEnemyHitTime =
        float.NegativeInfinity;

    private float lastExperiencePickupTime =
        float.NegativeInfinity;

    private float lastButtonClickTime =
        float.NegativeInfinity;

    private bool hasPlayedGameOver;
    private bool hasPlayedVictory;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        ConfigureAudioSources();
        LoadVolumeSettings();
        ApplyVolumeSettings();
        ResetRuntimeAudioState();
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }

    private void Start()
    {
        if (Instance != this)
        {
            return;
        }

        HandleSceneLoaded(
            SceneManager.GetActiveScene(),
            LoadSceneMode.Single
        );
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    /// <summary>
    /// 统一设置三个 AudioSource 的基本运行属性。
    /// 具体引用仍需要在 Inspector 中绑定。
    /// </summary>
    private void ConfigureAudioSources()
    {
        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        if (uiSource != null)
        {
            uiSource.playOnAwake = false;
            uiSource.loop = false;
            uiSource.spatialBlend = 0f;
        }
    }

    /// <summary>
    /// 场景加载后重置一次性反馈状态，
    /// 并根据场景名称切换背景音乐。
    /// </summary>
    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode loadSceneMode)
    {
        ResetRuntimeAudioState();

        // 切换或重载场景时，
        // 清除上一场景仍在播放的战斗类一次性音效。
        if (sfxSource != null)
        {
            sfxSource.Stop();
        }

        if (scene.name == mainMenuSceneName)
        {
            PlayMusic(mainMenuMusic);
            return;
        }

        if (scene.name == gameplaySceneName)
        {
            PlayMusic(gameplayMusic);
        }
    }

    /// <summary>
    /// 重置高频音效计时和胜负音效的一次性标记。
    /// Restart 或重新进入场景后可以再次正常播放。
    /// </summary>
    private void ResetRuntimeAudioState()
    {
        lastShootTime = float.NegativeInfinity;
        lastEnemyHitTime = float.NegativeInfinity;

        lastExperiencePickupTime =
            float.NegativeInfinity;

        lastButtonClickTime =
            float.NegativeInfinity;

        hasPlayedGameOver = false;
        hasPlayedVictory = false;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null)
        {
            return;
        }

        // 相同音乐已经播放时不从头重新开始，
        // 避免 Restart 或重复 sceneLoaded 导致音乐跳变。
        if (musicSource.clip == clip
            && musicSource.isPlaying)
        {
            return;
        }

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PlayShoot()
    {
        TryPlayThrottled(
            sfxSource,
            playerShootClip,
            playerShootVolume,
            minimumShootInterval,
            ref lastShootTime
        );
    }

    public void PlayEnemyHit()
    {
        TryPlayThrottled(
            sfxSource,
            enemyHitClip,
            enemyHitVolume,
            minimumEnemyHitInterval,
            ref lastEnemyHitTime
        );
    }

    public void PlayEnemyDeath(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Fast:
                PlayOneShot(
                    sfxSource,
                    enemyDeathFastClip,
                    enemyDeathFastVolume
                );
                break;

            case EnemyType.Heavy:
                PlayOneShot(
                    sfxSource,
                    enemyDeathHeavyClip,
                    enemyDeathHeavyVolume
                );
                break;

            case EnemyType.Normal:
            default:
                PlayOneShot(
                    sfxSource,
                    enemyDeathNormalClip,
                    enemyDeathNormalVolume
                );
                break;
        }
    }

    public void PlayPlayerHurt()
    {
        PlayOneShot(
            sfxSource,
            playerHurtClip,
            playerHurtVolume
        );
    }

    public void PlayPlayerDeath()
    {
        PlayOneShot(
            sfxSource,
            playerDeathClip,
            playerDeathVolume
        );
    }

    public void PlayPlayerDash()
    {
        PlayOneShot(
            sfxSource,
            playerDashClip,
            playerDashVolume
        );
    }

    public void PlayPlayerJetJump()
    {
        PlayOneShot(
            sfxSource,
            playerJetJumpClip,
            playerJetJumpVolume
        );
    }

    public void PlayPlayerPulse()
    {
        PlayOneShot(
            sfxSource,
            playerPulseClip,
            playerPulseVolume
        );
    }

    public void PlayExperiencePickup()
    {
        TryPlayThrottled(
            sfxSource,
            experiencePickupClip,
            experiencePickupVolume,
            minimumExperiencePickupInterval,
            ref lastExperiencePickupTime
        );
    }

    public void PlayLevelUp()
    {
        PlayOneShot(
            sfxSource,
            levelUpClip,
            levelUpVolume
        );
    }

    public void PlayGameOver()
    {
        if (hasPlayedGameOver)
        {
            return;
        }

        hasPlayedGameOver = true;

        PlayOneShot(
            sfxSource,
            gameOverClip,
            gameOverVolume
        );
    }

    public void PlayVictory()
    {
        if (hasPlayedVictory)
        {
            return;
        }

        hasPlayedVictory = true;

        PlayOneShot(
            sfxSource,
            victoryClip,
            victoryVolume
        );
    }

    public void PlayButtonClick()
    {
        TryPlayThrottled(
            uiSource,
            buttonClickClip,
            buttonClickVolume,
            minimumButtonClickInterval,
            ref lastButtonClickTime
        );
    }

    /// <summary>
    /// 播放普通一次性音效。
    /// 空 AudioSource 或空 AudioClip 会被安全忽略。
    /// </summary>
    private void PlayOneShot(
        AudioSource source,
        AudioClip clip,
        float volumeScale)
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.PlayOneShot(
            clip,
            Mathf.Clamp01(volumeScale)
        );
    }

    /// <summary>
    /// 使用 Time.unscaledTime 对高频音效执行节流。
    /// 即使 Time.timeScale 为 0，也不会出现计时冻结。
    /// </summary>
    private void TryPlayThrottled(
        AudioSource source,
        AudioClip clip,
        float volumeScale,
        float minimumInterval,
        ref float lastPlayTime)
    {
        if (source == null || clip == null)
        {
            return;
        }

        float currentTime = Time.unscaledTime;

        if (currentTime - lastPlayTime
            < minimumInterval)
        {
            return;
        }

        lastPlayTime = currentTime;

        source.PlayOneShot(
            clip,
            Mathf.Clamp01(volumeScale)
        );
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings();
    }

    public void SetUiVolume(float value)
    {
        uiVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings();
    }

    /// <summary>
    /// 从 PlayerPrefs 读取四类音量。
    /// 没有保存值时使用第二十八阶段确定的默认音量。
    /// </summary>
    private void LoadVolumeSettings()
    {
        masterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MasterVolumeKey,
                DefaultMasterVolume
            )
        );

        musicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MusicVolumeKey,
                DefaultMusicVolume
            )
        );

        sfxVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                SfxVolumeKey,
                DefaultSfxVolume
            )
        );

        uiVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                UiVolumeKey,
                DefaultUiVolume
            )
        );
    }

    /// <summary>
    /// 保存当前四类音量。
    /// 由设置面板在关闭或确认重置时调用。
    /// </summary>
    public void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(
            MasterVolumeKey,
            masterVolume
        );

        PlayerPrefs.SetFloat(
            MusicVolumeKey,
            musicVolume
        );

        PlayerPrefs.SetFloat(
            SfxVolumeKey,
            sfxVolume
        );

        PlayerPrefs.SetFloat(
            UiVolumeKey,
            uiVolume
        );

        PlayerPrefs.Save();
    }

    /// <summary>
    /// 只重置 AlienPurge 的四项音量设置。
    /// 不删除其他 PlayerPrefs 数据。
    /// </summary>
    public void ResetVolumeSettings()
    {
        masterVolume = DefaultMasterVolume;
        musicVolume = DefaultMusicVolume;
        sfxVolume = DefaultSfxVolume;
        uiVolume = DefaultUiVolume;

        ApplyVolumeSettings();
        SaveVolumeSettings();
    }

    private void ApplyVolumeSettings()
    {
        if (musicSource != null)
        {
            musicSource.volume =
                masterVolume * musicVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume =
                masterVolume * sfxVolume;
        }

        if (uiSource != null)
        {
            uiSource.volume =
                masterVolume * uiVolume;
        }
    }

    private void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        uiVolume = Mathf.Clamp01(uiVolume);

        playerShootVolume =
            Mathf.Clamp01(playerShootVolume);

        enemyHitVolume =
            Mathf.Clamp01(enemyHitVolume);

        enemyDeathNormalVolume =
            Mathf.Clamp01(enemyDeathNormalVolume);

        enemyDeathFastVolume =
            Mathf.Clamp01(enemyDeathFastVolume);

        enemyDeathHeavyVolume =
            Mathf.Clamp01(enemyDeathHeavyVolume);

        playerHurtVolume =
            Mathf.Clamp01(playerHurtVolume);

        playerDeathVolume =
            Mathf.Clamp01(playerDeathVolume);

        playerDashVolume =
            Mathf.Clamp01(playerDashVolume);

        playerJetJumpVolume =
            Mathf.Clamp01(playerJetJumpVolume );

        playerPulseVolume =
            Mathf.Clamp01(playerPulseVolume );

        experiencePickupVolume =
            Mathf.Clamp01(experiencePickupVolume);

        levelUpVolume =
            Mathf.Clamp01(levelUpVolume);

        gameOverVolume =
            Mathf.Clamp01(gameOverVolume);

        victoryVolume =
            Mathf.Clamp01(victoryVolume);

        buttonClickVolume =
            Mathf.Clamp01(buttonClickVolume);

        minimumShootInterval =
            Mathf.Max(0f, minimumShootInterval);

        minimumEnemyHitInterval =
            Mathf.Max(0f, minimumEnemyHitInterval);

        minimumExperiencePickupInterval =
            Mathf.Max(
                0f,
                minimumExperiencePickupInterval
            );

        minimumButtonClickInterval =
            Mathf.Max(
                0f,
                minimumButtonClickInterval
            );

        ApplyVolumeSettings();
    }
}