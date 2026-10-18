using UnityEngine;

public sealed partial class CombatImpactFeedback
{
    readonly AudioSource[] voices = new AudioSource[6];
    readonly float[] gains = new float[6];
    bool soundPending;
    CombatImpactTier pendingTier;
    float pendingPan, pendingSince, nextHitSound, nextKillSound, emphasisUntil;
    int hitVoice, killVoice;
    readonly System.Random soundRandom = new System.Random(1701);
    public CombatImpactTier LastPlayedTier { get; private set; }
    public int ActiveVoiceCount
    {
        get { int n = 0; foreach (var voice in voices) if (voice != null && voice.isPlaying) n++; return n; }
    }

    void CreateAudio()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            var audio = new GameObject(i < 2 ? "ImpactVoice_" + i : "KillVoice_" + i).AddComponent<AudioSource>();
            audio.transform.SetParent(transform, false); audio.playOnAwake = false; audio.loop = false;
            audio.spatialBlend = 0; audio.priority = i < 2 ? 150 : 90; voices[i] = audio;
        }
    }
    void QueueSound(CombatImpactTier tier, float pan)
    {
        // Coalesce a shotgun/explosion/chain frame. A later ordinary hit cannot hide a kill.
        if (!soundPending || tier >= pendingTier)
        { pendingTier = tier; pendingPan = pan; }
        if (!soundPending) pendingSince = Time.time;
        soundPending = true;
    }
    float BusGain => feedbackVolume * (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1);
    void UpdateAudio()
    {
        for (int i = 0; i < voices.Length; i++)
            if (voices[i] != null) voices[i].volume = BusGain * gains[i] * (i < 2 && Time.time < emphasisUntil ? .28f : 1);
        if (!soundPending) return;
        if (Time.time - pendingSince > .2f) { soundPending = false; return; }
        bool kill = pendingTier >= CombatImpactTier.Kill;
        if (Time.time < (kill ? nextKillSound : nextHitSound)) return;
        AudioClip clip = pendingTier == CombatImpactTier.HeavyKill ? heavyKillSound :
            pendingTier == CombatImpactTier.Kill ? killSound : pendingTier == CombatImpactTier.Strong ? strongSound : hitSound;
        int index = kill ? 2 + killVoice++ % 4 : hitVoice++ % 2;
        var voice = voices[index];
        gains[index] = pendingTier == CombatImpactTier.HeavyKill ? .9f : kill ? .68f : pendingTier == CombatImpactTier.Strong ? .48f : .36f;
        if (kill)
        {
            nextKillSound = Time.time + minimumKillInterval;
            emphasisUntil = Time.time + .17f;
            for (int i = 0; i < 2; i++) voices[i].volume = BusGain * gains[i] * .28f;
        }
        else nextHitSound = Time.time + minimumHitInterval;
        if (clip != null && voice != null)
        {
            voice.Stop(); voice.clip = clip;
            voice.panStereo = pendingPan;
            voice.pitch = 1 + (float)(soundRandom.NextDouble() * .06 - .03) +
                (kill ? Mathf.Min(6, Mathf.Max(0, ConsecutiveKills - 1)) * .015f : 0);
            voice.volume = BusGain * gains[index] * (!kill && Time.time < emphasisUntil ? .28f : 1);
            voice.Play(); PlayedSounds++; LastPlayedTier = pendingTier;
        }
        soundPending = false;
    }
    void ClearAudio()
    {
        soundPending = false; nextHitSound = nextKillSound = emphasisUntil = 0;
        foreach (var voice in voices) if (voice != null) voice.Stop();
    }
}
