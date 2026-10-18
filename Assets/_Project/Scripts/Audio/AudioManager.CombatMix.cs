using UnityEngine;

public partial class AudioManager
{
    AudioSource weaponSource;
    float emphasisStarted = -100, emphasisHold, emphasisRecovery, nextEmphasis;
    float weaponFloor = 1, musicFloor = 1;
    public float CombatWeaponGain { get; private set; } = 1;
    public float CombatMusicGain { get; private set; } = 1;

    AudioSource WeaponSource
    {
        get
        {
            if (CombatImpactFeedback.Instance == null || !CombatImpactFeedback.Instance.isActiveAndEnabled) return sfxSource;
            if (weaponSource == null)
            {
                weaponSource = new GameObject("PlayerWeaponAudio").AddComponent<AudioSource>();
                weaponSource.transform.SetParent(transform, false);
                weaponSource.playOnAwake = false; weaponSource.spatialBlend = 0; weaponSource.priority = 140;
                if (sfxSource != null) weaponSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            }
            ApplyCombatMix(); return weaponSource;
        }
    }
    public void EmphasizeCombatKill(bool heavy)
    {
        if (Time.unscaledTime < nextEmphasis || masterVolume <= 0 || sfxVolume <= 0 || CombatImpactFeedback.Instance == null ||
            !CombatImpactFeedback.Instance.CanPresent || CombatImpactFeedback.Instance.feedbackVolume <= 0) return;
        emphasisStarted = Time.unscaledTime; emphasisHold = heavy ? .07f : .04f; emphasisRecovery = heavy ? .19f : .14f;
        nextEmphasis = emphasisStarted + (heavy ? .34f : .24f);
        weaponFloor = heavy ? .28f : .4f; musicFloor = heavy ? .72f : .84f;
        ApplyCombatMix();
    }
    void LateUpdate()
    {
        if (weaponSource == null && emphasisStarted < 0) return;
        var feedback = CombatImpactFeedback.Instance;
        if (feedback == null || !feedback.CanPresent) { ClearCombatEmphasis(); return; }
        ApplyCombatMix();
    }
    void ApplyCombatMix()
    {
        if (masterVolume <= 0 || sfxVolume <= 0) { emphasisStarted = -100; weaponFloor = musicFloor = 1; }
        float recovery = emphasisStarted < 0 ? 1 : Mathf.Clamp01((Time.unscaledTime - emphasisStarted - emphasisHold) / Mathf.Max(.001f, emphasisRecovery));
        recovery = recovery * recovery * (3 - 2 * recovery);
        CombatWeaponGain = Mathf.Lerp(weaponFloor, 1, recovery); CombatMusicGain = Mathf.Lerp(musicFloor, 1, recovery);
        if (weaponSource != null) weaponSource.volume = masterVolume * sfxVolume * CombatWeaponGain;
        if (musicSource != null) musicSource.volume = masterVolume * musicVolume * CombatMusicGain;
        // Enemy attacks, player injury, abilities and UI keep their existing buses and full audibility.
    }
    public void ClearCombatEmphasis()
    {
        emphasisStarted = -100; nextEmphasis = 0; weaponFloor = musicFloor = 1;
        if (weaponSource != null) weaponSource.Stop();
        ApplyCombatMix();
    }
}
