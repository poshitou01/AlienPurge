using UnityEngine;

// Four voices shared by the integrated special effects, with independent rate limits for both mechanics.
[DisallowMultipleComponent]
public sealed class UpgradeVfxAudio : MonoBehaviour
{
    static UpgradeVfxAudio instance;
    readonly AudioSource[] voices = new AudioSource[4];
    readonly float[] gain = new float[4];
    float nextExplosion, nextLightning;
    int explosionVoice, lightningVoice;
    public int PlayedCount { get; private set; }
    public static bool RunEnded => GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsVictory);
    public static bool GameplayVisible => Time.timeScale > 0 && !PauseMenuController.IsPaused &&
        !UpgradeManager.IsChoosingUpgrade && !WeaponModuleSelectionManager.IsChoosingModule &&
        (GameManager.Instance == null || GameManager.Instance.IsPlaying);

    void Awake()
    {
        if (instance != null && instance != this) { enabled = false; return; }
        instance = this;
        for (int i = 0; i < voices.Length; i++)
        {
            var voice = new GameObject(i < 2 ? "ExplosionVoice" : "LightningVoice").AddComponent<AudioSource>();
            voice.transform.SetParent(transform, false); voice.playOnAwake = false;
            voice.spatialBlend = 0; voice.priority = 160; voices[i] = voice;
        }
    }
    public static void Request(UpgradeVfxProfile profile, bool lightning, Vector3 position)
    {
        var host = instance;
        if (host == null || !host.isActiveAndEnabled || !GameplayVisible) return;
        float next = lightning ? host.nextLightning : host.nextExplosion;
        if (Time.time < next) return;
        Camera camera = CombatImpactFeedback.Instance != null ? CombatImpactFeedback.Instance.worldCamera : Camera.main;
        Vector3 view = camera != null ? camera.WorldToViewportPoint(position) : new Vector3(.5f, .5f, 1);
        if (view.z <= 0 || view.x < -.05f || view.x > 1.05f || view.y < -.05f || view.y > 1.05f) return;
        AudioClip clip = lightning ? profile.lightningSound : profile.explosionSound;
        if (clip == null) return;
        int index = lightning ? 2 + host.lightningVoice++ % 2 : host.explosionVoice++ % 2;
        if (lightning) host.nextLightning = Time.time + .09f; else host.nextExplosion = Time.time + .10f;
        var voice = host.voices[index]; voice.Stop(); voice.clip = clip;
        host.gain[index] = lightning ? profile.lightningVolume : profile.explosionVolume;
        voice.volume = host.gain[index] * BusGain;
        voice.panStereo = Mathf.Clamp((view.x - .5f) * .6f, -.3f, .3f);
        voice.pitch = 1 + (host.PlayedCount % 3 - 1) * .025f;
        voice.Play(); host.PlayedCount++;
    }
    static float BusGain => AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1;
    void LateUpdate()
    {
        foreach (var voice in voices) if (voice != null && !GameplayVisible) voice.Stop();
        for (int i = 0; i < voices.Length; i++) if (voices[i] != null) voices[i].volume = gain[i] * BusGain;
    }
    void OnDisable() { foreach (var voice in voices) if (voice != null) voice.Stop(); }
    void OnDestroy() { if (instance == this) instance = null; }
}
