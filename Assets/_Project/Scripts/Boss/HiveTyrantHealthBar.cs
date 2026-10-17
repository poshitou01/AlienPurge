using UnityEngine;
using UnityEngine.UI;

// Original artwork and the same EnemyHealth source; owned UI never rewrites other HUDs.
public sealed class HiveTyrantHealthBar : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    [SerializeField] private Texture2D frame;
    [SerializeField] private Texture2D portrait;
    [SerializeField] private bool showPortrait;
    [SerializeField] private bool useOrnamentalFrame = true;
    private Canvas canvas;
    private RectTransform panel, fill, trail, announcement;
    private CanvasGroup announcementGroup;
    private Image fillImage;
    private Text healthText, phaseText, subtitle, attackHint;
    private Sprite frameSprite;
    private int lastHealth = -1, lastMaximum = -1, observedTransitions;
    private bool lastPhase;
    private float trailRatio = 1, damageTime, announcementStarted = -10;
    private const float BarWidth = 610, BarHeight = 18;
    private static readonly Color Ember = new Color(1, .42f, .08f);
    private static readonly Color Gold = new Color(1, .79f, .45f);
    public RectTransform Panel => panel;
    public bool PhaseAnnouncementVisible => announcementGroup != null && announcementGroup.alpha > .01f;

    private void Awake()
    {
        if (boss == null) boss = GetComponent<HiveTyrantCombat>();
        canvas = HiveTyrantHudLayout.Canvas("HiveTyrant_BossHUD", 60);
        panel = HiveTyrantHudLayout.Root(canvas.transform, "BossStatus", new Vector2(760, 126), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -12));
        var name = HiveTyrantHudLayout.Label(panel, "BossName", new Rect(150, 10, 460, 26), "HIVE TYRANT", 22, new Color(1, .91f, .75f), TextAnchor.MiddleCenter, true);
        AddShadow(name);
        subtitle = HiveTyrantHudLayout.Label(panel, "Subtitle", new Rect(150, 37, 460, 17), "B R O O D   S O V E R E I G N", 10, Gold, TextAnchor.MiddleCenter);
        AddShadow(subtitle);
        if (showPortrait && portrait != null)
        {
            var icon = HiveTyrantHudLayout.Rect(panel, "Portrait", new Rect(77, 18, 36, 42)).gameObject.AddComponent<RawImage>();
            icon.texture = portrait; icon.raycastTarget = false;
        }
        HiveTyrantHudLayout.Box(panel, "HealthTrack", new Rect(75, 70, BarWidth, BarHeight), new Color(.07f, .025f, .018f, .9f));
        trail = HiveTyrantHudLayout.Box(panel, "DamageTrail", new Rect(75, 70, BarWidth, BarHeight), new Color(1, .77f, .39f, .75f)).rectTransform;
        fillImage = HiveTyrantHudLayout.Box(panel, "HealthFill", new Rect(75, 70, BarWidth, BarHeight), Ember);
        fill = fillImage.rectTransform;
        StretchBand(fill, "UpperGlow", .58f, 1, new Color(1, .85f, .42f, .25f));
        StretchBand(fill, "LowerShade", 0, .24f, new Color(.26f, .04f, 0, .3f));
        if (useOrnamentalFrame && frame != null)
        {
            // Stretch only the long rails. Preserve the original claw caps' proportions.
            frameSprite = Sprite.Create(frame, new Rect(0, 0, frame.width, frame.height), Vector2.one * .5f,
                frame.height / 96f * 100f, 0, SpriteMeshType.FullRect, new Vector4(frame.width * .22f, 0, frame.width * .22f, 0));
            frameSprite.name = "HiveTyrant_OriginalFrame_Sliced";
            var ornament = HiveTyrantHudLayout.Box(panel, "OriginalOrnament", new Rect(18, 28, 724, 96), Color.white);
            ornament.sprite = frameSprite; ornament.type = Image.Type.Sliced; ornament.fillCenter = true;
        }
        phaseText = HiveTyrantHudLayout.Label(panel, "Phase", new Rect(76, 101, 220, 18), "PHASE I", 11, Gold, TextAnchor.MiddleLeft, true);
        healthText = HiveTyrantHudLayout.Label(panel, "Health", new Rect(505, 101, 180, 18), "", 12, new Color(1, .9f, .73f), TextAnchor.MiddleRight);
        AddShadow(phaseText); AddShadow(healthText);
        attackHint = HiveTyrantHudLayout.Label(panel, "AttackHint", new Rect(60, 124, 640, 22), "", 12, Gold, TextAnchor.MiddleCenter, true);
        AddShadow(attackHint);
        BuildAnnouncement();
    }
    private static void AddShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(0, -1);
    }
    private static void StretchBand(RectTransform parent, string name, float bottom, float top, Color tint)
    {
        var band = HiveTyrantHudLayout.Box(parent, name, new Rect(), tint).rectTransform;
        band.anchorMin = new Vector2(0, bottom); band.anchorMax = new Vector2(1, top); band.offsetMin = band.offsetMax = Vector2.zero;
    }
    private void BuildAnnouncement()
    {
        announcement = HiveTyrantHudLayout.Root(canvas.transform, "PhaseAnnouncement", new Vector2(600, 100), new Vector2(.5f, .76f), Vector2.one * .5f, Vector2.zero);
        announcementGroup = announcement.gameObject.AddComponent<CanvasGroup>();
        announcementGroup.blocksRaycasts = false; announcementGroup.interactable = false; announcementGroup.alpha = 0;
        var veil = HiveTyrantHudLayout.Rect(announcement, "Veil", new Rect(0, -6, 600, 106)).gameObject.AddComponent<HiveTyrantPhaseVeil>();
        veil.color = new Color(.04f, .025f, .045f, .8f); veil.raycastTarget = false;
        var phase = HiveTyrantHudLayout.Label(announcement, "Phase", new Rect(0, 5, 600, 20), "P H A S E   I I", 13, new Color(.86f, .73f, 1), TextAnchor.MiddleCenter, true);
        var title = HiveTyrantHudLayout.Label(announcement, "Title", new Rect(0, 26, 600, 38), "RIFTWING AWAKENS", 30, new Color(1, .86f, .6f), TextAnchor.MiddleCenter, true);
        AddShadow(phase); AddShadow(title);
        HiveTyrantHudLayout.Box(announcement, "Rule", new Rect(110, 66, 380, 1), new Color(.9f, .62f, .3f, .7f));
        HiveTyrantHudLayout.Label(announcement, "Detail", new Rect(0, 70, 600, 19), "THE BROOD ANSWERS", 11, HiveTyrantHudLayout.Paper, TextAnchor.MiddleCenter);
    }
    private void LateUpdate()
    {
        bool visible = boss != null && boss.Health != null && boss.State != HiveTyrantState.Dormant && boss.State != HiveTyrantState.Disabled;
        if (canvas == null) return;
        if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        if (!visible) { announcementGroup.alpha = 0; return; }
        int hp = boss.Health.CurrentHealth, max = boss.Health.MaxHealth;
        float ratio = Mathf.Clamp01(hp / Mathf.Max(1f, max));
        if (hp != lastHealth || max != lastMaximum)
        {
            if (lastHealth >= 0 && hp < lastHealth) damageTime = Time.unscaledTime;
            else trailRatio = ratio;
            fill.sizeDelta = new Vector2(BarWidth * ratio, BarHeight);
            healthText.text = hp + " / " + max; lastHealth = hp; lastMaximum = max;
        }
        if (Time.unscaledTime > damageTime + .25f || trailRatio > ratio + .08f)
            trailRatio = Mathf.MoveTowards(trailRatio, ratio, Time.unscaledDeltaTime * .55f);
        trail.sizeDelta = new Vector2(BarWidth * trailRatio, BarHeight);
        bool phase2 = boss.Phase2Entered;
        bool sweep = boss.State == HiveTyrantState.SweepWindup || boss.Pressure.HasCrescent;
        bool spores = boss.State == HiveTyrantState.SporeWindup || boss.Pressure.HasSpores;
        attackHint.text = sweep ? (phase2 ? "TWIN RIFT  /  WATCH FOR THE SECOND SWEEP" : "RIFT CLEAVE  /  SPACE TO JUMP OR FLANK") :
            spores ? (phase2 ? "SPORE CROSSFIRE  /  ESCAPE THE PAIRED MARKS" : "SPORE BARRAGE  /  KEEP MOVING") :
            boss.Wave.IsWarning || boss.Wave.IsTravelling ? (phase2 ? "FAULTLINE CASCADE  /  HOLD THE GAP THROUGH EACH WAVE" : "FAULTLINE  /  FIND THE OPEN LANE") :
            phase2 && boss.State == HiveTyrantState.SummonWindup ? "BROOD RALLY  /  BREAK THE RANGED LINE" : "";
        attackHint.color = spores ? new Color(.94f, .73f, 1) : Gold;
        if (lastPhase != phase2)
        {
            lastPhase = phase2; fillImage.color = phase2 ? new Color(1, .34f, .055f) : Ember;
            phaseText.color = phase2 ? new Color(.9f, .76f, 1) : Gold;
            phaseText.text = phase2 ? "PHASE II  /  RIFTWING" : "PHASE I";
            subtitle.text = phase2 ? "R I F T W I N G" : "B R O O D   S O V E R E I G N";
        }
        if (boss.PhaseTransitionCount != observedTransitions)
        {
            observedTransitions = boss.PhaseTransitionCount;
            announcementStarted = observedTransitions > 0 ? Time.time : -10;
        }
        float elapsed = Time.time - announcementStarted, duration = boss.Config.phaseTransitionTime;
        float alpha = Mathf.SmoothStep(0, 1, elapsed / .28f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - .7f, duration, elapsed)));
        announcementGroup.alpha = boss.Health.IsDead || boss.State != HiveTyrantState.PhaseTransition ? 0 : alpha;
        announcement.anchoredPosition = new Vector2(0, Mathf.Lerp(8, 0, Mathf.Clamp01(elapsed / .4f)));
    }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); if (frameSprite != null) Destroy(frameSprite); }
}

// A soft cinematic backing built from UI vertices; no new texture or fullscreen flash.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HiveTyrantPhaseVeil : MaskableGraphic
{
    private static readonly float[] Stops = { 0, .18f, .82f, 1 };
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear(); Rect r = rectTransform.rect;
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
        {
            Color tint = color; if (x == 0 || x == 3 || y == 0 || y == 3) tint.a = 0;
            helper.AddVert(new Vector3(Mathf.Lerp(r.xMin, r.xMax, Stops[x]), Mathf.Lerp(r.yMin, r.yMax, Stops[y]), 0), tint, Vector2.zero);
        }
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
        {
            int i = y * 4 + x; helper.AddTriangle(i, i + 4, i + 1); helper.AddTriangle(i + 1, i + 4, i + 5);
        }
    }
}
