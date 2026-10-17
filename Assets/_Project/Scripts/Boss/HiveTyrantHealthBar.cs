using UnityEngine;
using UnityEngine.UI;

// The existing component/references remain the entry point. EnemyHealth is authoritative.
public sealed class HiveTyrantHealthBar : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    [SerializeField, Tooltip("Original frame retained for optional ornamental styling.")] private Texture2D frame;
    [SerializeField] private Texture2D portrait;
    [SerializeField] private bool useOrnamentalFrame;
    private Canvas canvas;
    private RectTransform panel, fill, trail;
    private Image fillImage, accent;
    private Text healthText, phaseText, subtitle;
    private int lastHealth = -1, lastMaximum = -1;
    private bool lastPhase;
    private float trailRatio = 1, damageTime;
    private const float BarWidth = 548;
    public RectTransform Panel => panel;
    private void Awake()
    {
        if (boss == null) boss = GetComponent<HiveTyrantCombat>();
        canvas = HiveTyrantHudLayout.Canvas("HiveTyrant_BossHUD", 60);
        panel = HiveTyrantHudLayout.Root(canvas.transform, "BossStatus", new Vector2(680, 92), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24));
        HiveTyrantHudLayout.Box(panel, "Border", new Rect(0, 0, 680, 92), HiveTyrantHudLayout.Line);
        HiveTyrantHudLayout.Box(panel, "Panel", new Rect(1, 1, 678, 90), HiveTyrantHudLayout.Ink);
        accent = HiveTyrantHudLayout.Box(panel, "Accent", new Rect(0, 0, 3, 92), HiveTyrantHudLayout.Brood);
        HiveTyrantHudLayout.Box(panel, "PortraitBorder", new Rect(16, 12, 68, 68), HiveTyrantHudLayout.Line);
        HiveTyrantHudLayout.Box(panel, "PortraitWell", new Rect(17, 13, 66, 66), new Color(.07f, .08f, .07f));
        if (portrait != null)
        {
            float ratio = portrait.width / (float)portrait.height;
            Vector2 size = ratio >= 1 ? new Vector2(64, 64 / ratio) : new Vector2(64 * ratio, 64);
            var image = HiveTyrantHudLayout.Rect(panel, "Portrait", new Rect(50 - size.x * .5f, 46 - size.y * .5f, size.x, size.y)).gameObject.AddComponent<RawImage>();
            image.texture = portrait; image.raycastTarget = false;
        }
        HiveTyrantHudLayout.Label(panel, "BossName", new Rect(104, 12, 360, 28), "HIVE TYRANT", 23, HiveTyrantHudLayout.Paper, TextAnchor.MiddleLeft, true);
        subtitle = HiveTyrantHudLayout.Label(panel, "Subtitle", new Rect(104, 39, 350, 18), "BROOD SOVEREIGN", 11, HiveTyrantHudLayout.Muted);
        phaseText = HiveTyrantHudLayout.Label(panel, "Phase", new Rect(540, 14, 112, 22), "PHASE 1", 12, HiveTyrantHudLayout.Brood, TextAnchor.MiddleRight, true);
        healthText = HiveTyrantHudLayout.Label(panel, "Health", new Rect(472, 39, 180, 18), "", 12, HiveTyrantHudLayout.Paper, TextAnchor.MiddleRight);
        HiveTyrantHudLayout.Box(panel, "HealthTrack", new Rect(104, 63, BarWidth, 12), new Color(.16f, .18f, .15f));
        trail = HiveTyrantHudLayout.Box(panel, "DamageTrail", new Rect(104, 63, BarWidth, 12), new Color(.76f, .69f, .49f)).rectTransform;
        fillImage = HiveTyrantHudLayout.Box(panel, "HealthFill", new Rect(104, 63, BarWidth, 12), HiveTyrantHudLayout.Brood); fill = fillImage.rectTransform;
        for (int i = 1; i < 4; i++) HiveTyrantHudLayout.Box(panel, "Quarter_" + i, new Rect(104 + BarWidth * i / 4, 63, 1, 12), new Color(.05f, .06f, .04f, .65f));
        if (useOrnamentalFrame && frame != null)
        {
            float height = BarWidth * frame.height / frame.width;
            var decoration = HiveTyrantHudLayout.Rect(panel, "OriginalOrnament", new Rect(104, 69 - height * .5f, BarWidth, height)).gameObject.AddComponent<RawImage>();
            decoration.texture = frame; decoration.raycastTarget = false;
        }
    }
    private void LateUpdate()
    {
        bool visible = boss != null && boss.Health != null && boss.State != HiveTyrantState.Dormant && boss.State != HiveTyrantState.Disabled;
        if (canvas == null) return;
        if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        if (!visible) return;
        int hp = boss.Health.CurrentHealth, max = boss.Health.MaxHealth;
        float ratio = Mathf.Clamp01(hp / Mathf.Max(1f, max));
        if (hp != lastHealth || max != lastMaximum)
        {
            if (lastHealth >= 0 && hp < lastHealth) damageTime = Time.unscaledTime;
            else trailRatio = ratio;
            fill.sizeDelta = new Vector2(BarWidth * ratio, 12);
            healthText.text = hp + " / " + max; lastHealth = hp; lastMaximum = max;
        }
        if (Time.unscaledTime > damageTime + .25f) trailRatio = Mathf.MoveTowards(trailRatio, ratio, Time.unscaledDeltaTime * .55f);
        trail.sizeDelta = new Vector2(BarWidth * trailRatio, 12);
        bool phase = boss.Phase2Entered;
        if (lastPhase != phase)
        {
            lastPhase = phase; Color tint = phase ? HiveTyrantHudLayout.Rift : HiveTyrantHudLayout.Brood;
            fillImage.color = accent.color = phaseText.color = tint;
            phaseText.text = phase ? "PHASE 2" : "PHASE 1"; subtitle.text = phase ? "RIFTWING" : "BROOD SOVEREIGN";
        }
    }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
}
