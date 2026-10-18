using UnityEngine;
using TMPro;

public enum PlayerImpactSource { Projectile, Explosion, Chain, Pulse }
public enum CombatImpactTier { Hit, Strong, Kill, HeavyKill }

// Scene opt-in presentation. Damage, death, drops and kill credit remain owned by EnemyHealth.
[DefaultExecutionOrder(9000), DisallowMultipleComponent]
public sealed partial class CombatImpactFeedback : MonoBehaviour
{
    public static CombatImpactFeedback Instance { get; private set; }
    static EnemyHealth resolvingPlayerDamage;
    public Camera worldCamera;
    public Material effectMaterial, flashMaterial;
    public TMP_Text confirmation;
    public CanvasGroup confirmationGroup;
    public CombatHitMarker hitMarker;
    public AudioClip hitSound, strongSound, killSound, heavyKillSound;
    [Range(0, 1)] public float feedbackVolume = .82f;
    [Range(0, 1)] public float cameraFeedback = .75f;
    [Range(.02f, .2f)] public float minimumHitInterval = .075f;
    [Range(.05f, .3f)] public float minimumKillInterval = .12f;
    [Min(.5f)] public float chainWindow = 2.2f;
    public bool showHealthBars = true;
    public int installationRevision;

    public int PresentedHits { get; private set; }
    public int PresentedKills { get; private set; }
    public int PlayedSounds { get; private set; }
    public int ConsecutiveKills { get; private set; }
    public CombatImpactTier LastTier { get; private set; }
    public bool CanPresent => isActiveAndEnabled && Time.timeScale > 0 &&
        !PauseMenuController.IsPaused && !UpgradeManager.IsChoosingUpgrade &&
        !WeaponModuleSelectionManager.IsChoosingModule &&
        (GameManager.Instance == null || GameManager.Instance.IsPlaying);

    float lastKill = -100, captionUntil, nextShake;
    bool wasSuspended;
    readonly System.Collections.Generic.HashSet<EnemyImpactReaction> reactions = new System.Collections.Generic.HashSet<EnemyImpactReaction>();

    public static bool OwnsDeath(EnemyHealth enemy) => resolvingPlayerDamage == enemy;

    // Only actual player attack paths use this gateway. Health restoration, debug kills and
    // region lease rehydration deliberately keep using TakeDamage without combat confirmation.
    public static bool ApplyDamage(EnemyHealth enemy, int damage, Vector2 point, Vector2 direction, PlayerImpactSource source)
    {
        if (enemy == null || enemy.IsDead || damage <= 0) return false;
        var host = Instance;
        bool managed = host != null && host.isActiveAndEnabled && enemy.gameObject.scene == host.gameObject.scene;
        int before = enemy.CurrentHealth;
        var previous = resolvingPlayerDamage;
        try
        {
            if (managed) resolvingPlayerDamage = enemy;
            enemy.TakeDamage(damage);
        }
        finally { resolvingPlayerDamage = previous; }
        if (managed && enemy != null && enemy.CurrentHealth < before && host.CanPresent)
            host.Present(enemy, before - enemy.CurrentHealth, point, direction, source);
        return managed;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (worldCamera == null) worldCamera = Camera.main;
        CreateVisuals(); CreateDeathEchoes(); CreateAudio();
        if (confirmation != null) confirmation.text = "";
        if (confirmationGroup != null) confirmationGroup.alpha = 0;
    }

    void Present(EnemyHealth enemy, int appliedDamage, Vector2 point, Vector2 direction, PlayerImpactSource source)
    {
        if (worldCamera == null) return;
        Vector3 viewport = worldCamera.WorldToViewportPoint(enemy.transform.position);
        if (viewport.z <= 0 || viewport.x < -.02f || viewport.x > 1.02f || viewport.y < -.02f || viewport.y > 1.02f) return;
        var pooled = enemy.GetComponent<PooledEnemy>();
        bool boss = enemy.GetComponent<HiveTyrantCombat>() != null;
        bool heavy = boss || pooled != null && pooled.Type == EnemyType.Heavy;
        bool killed = enemy.IsDead;
        CombatImpactTier tier = killed ? (heavy ? CombatImpactTier.HeavyKill : CombatImpactTier.Kill) :
            (source == PlayerImpactSource.Pulse || appliedDamage >= Mathf.Max(2, Mathf.CeilToInt(enemy.MaxHealth * .2f)) ? CombatImpactTier.Strong : CombatImpactTier.Hit);
        LastTier = tier; PresentedHits++;
        direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.up;
        if (killed)
        {
            PresentedKills++;
            ConsecutiveKills = Time.time - lastKill <= chainWindow ? ConsecutiveKills + 1 : 1;
            lastKill = Time.time;
            if (confirmation != null)
            {
                confirmation.text = heavy ? (boss ? "首领击杀" : "重型击杀") : ConsecutiveKills >= 3 ? "连续击杀" : "击杀确认";
                confirmation.text += "  × " + ConsecutiveKills;
                confirmation.color = new Color(.95f, .82f, .53f, 1);
                if (confirmationGroup != null) confirmationGroup.alpha = 1;
                captionUntil = Time.time + 1.15f;
            }
        }
        if (!boss)
        {
            var reaction = enemy.GetComponent<EnemyImpactReaction>();
            if (reaction == null) reaction = enemy.gameObject.AddComponent<EnemyImpactReaction>();
            reactions.Add(reaction);
            reaction.Play(this, direction, tier, heavy);
            // Body bounds give area attacks a precise visible contact point, rather than the player's feet.
            if (source != PlayerImpactSource.Projectile) point = reaction.ImpactCenter;
        }
        EmitBurst(point, direction, tier, source);
        if (hitMarker != null) hitMarker.Show(worldCamera, point, tier);
        QueueSound(tier, Mathf.Clamp((viewport.x - .5f) * .65f, -.32f, .32f));
        if (tier >= CombatImpactTier.Kill && Time.time >= nextShake && CameraFollow.Instance != null)
        {
            nextShake = Time.time + .13f;
            CameraFollow.Instance.Shake((tier == CombatImpactTier.HeavyKill ? .16f : .075f) * cameraFeedback,
                tier == CombatImpactTier.HeavyKill ? .14f : .085f);
        }
    }

    void LateUpdate()
    {
        if (!CanPresent)
        {
            if (!wasSuspended) ClearPresentation();
            wasSuspended = true;
            return;
        }
        wasSuspended = false;
        UpdateVisuals(); UpdateDeathEchoes(); UpdateAudio();
        if (Time.time - lastKill > chainWindow) ConsecutiveKills = 0;
        if (confirmation != null)
        {
            var color = confirmation.color;
            color.a = Mathf.Clamp01((captionUntil - Time.time) / .35f);
            confirmation.color = color;
            if (confirmationGroup != null)
            {
                confirmationGroup.alpha = color.a > 0 ? 1 : 0;
                confirmationGroup.transform.localScale = Vector3.one * (1 + .09f * Mathf.Clamp01(1 - (Time.time - lastKill) / .13f));
            }
        }
    }

    void ClearPresentation()
    {
        ClearVisuals(); ClearDeathEchoes(); ClearAudio();
        if (hitMarker != null) hitMarker.Clear();
        foreach (var reaction in reactions) if (reaction != null) reaction.Clear();
        ConsecutiveKills = 0; lastKill = -100; captionUntil = 0;
        if (confirmation != null) confirmation.text = "";
        if (confirmationGroup != null) confirmationGroup.alpha = 0;
    }
    void OnDisable() { ClearPresentation(); }
    void OnDestroy()
    {
        if (Instance == this) { Instance = null; resolvingPlayerDamage = null; }
        DestroyVisuals();
    }
}
