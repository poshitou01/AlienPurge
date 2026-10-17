using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Only installed in the dedicated test scene. No debug cheats in Level01.</summary>
public sealed class HiveTyrantCombatDebug : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    [SerializeField] private bool showControls;
    private Canvas canvas;
    private GameObject controls, result, ownedEventSystem;
    private Text hint, status, defeatDetail;
    private Button retry;
    private PlayerHealth player;
    private bool defeated, previousCursor;
    private CursorLockMode previousLock;
    private float nextStatusUpdate;
    public bool ControlsVisible => controls != null && controls.activeSelf;
    private void Awake()
    {
        player = FindObjectOfType<PlayerHealth>(); previousCursor = Cursor.visible; previousLock = Cursor.lockState;
        canvas = HiveTyrantHudLayout.Canvas("HiveTyrant_TestHUD", 100, true);
        if (EventSystem.current == null) ownedEventSystem = new GameObject("HiveTyrant_TestEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var strip = HiveTyrantHudLayout.Root(canvas.transform, "TestHint", new Vector2(440, 30), Vector2.zero, Vector2.zero, new Vector2(24, 20));
        HiveTyrantHudLayout.Box(strip, "Background", new Rect(0, 0, 440, 30), new Color(.04f, .05f, .05f, .72f));
        hint = HiveTyrantHudLayout.Label(strip, "Hint", new Rect(12, 0, 416, 30), "TEST ARENA    AUTO AI    F8 CONTROLS", 12, HiveTyrantHudLayout.Paper);
        var panel = HiveTyrantHudLayout.Root(canvas.transform, "TestControls", new Vector2(440, 218), Vector2.zero, Vector2.zero, new Vector2(24, 62));
        controls = panel.gameObject;
        HiveTyrantHudLayout.Box(panel, "Border", new Rect(0, 0, 440, 218), HiveTyrantHudLayout.Line);
        HiveTyrantHudLayout.Box(panel, "Panel", new Rect(1, 1, 438, 216), HiveTyrantHudLayout.Ink);
        HiveTyrantHudLayout.Label(panel, "Title", new Rect(18, 12, 404, 26), "TEST CONTROLS", 16, HiveTyrantHudLayout.Paper, TextAnchor.MiddleLeft, true);
        HiveTyrantHudLayout.Label(panel, "Keys", new Rect(18, 48, 404, 100), "F1  Sweep                 F2  Ground wave\nF3  Summon              F4  Riftwing phase\nF5  Defeat boss          F6  Toggle auto AI\nF8  Hide controls        F9  Restart fight", 14, HiveTyrantHudLayout.Muted);
        status = HiveTyrantHudLayout.Label(panel, "Status", new Rect(18, 164, 404, 36), "", 12, HiveTyrantHudLayout.Brood);
        controls.SetActive(showControls);
        var overlay = HiveTyrantHudLayout.Root(canvas.transform, "HiveTyrant_TestResult", Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero);
        overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero; result = overlay.gameObject;
        var shade = result.AddComponent<Image>(); shade.color = new Color(.015f, .02f, .02f, .67f); shade.raycastTarget = true;
        var card = HiveTyrantHudLayout.Root(overlay, "RetryCard", new Vector2(500, 268), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
        HiveTyrantHudLayout.Box(card, "Border", new Rect(0, 0, 500, 268), HiveTyrantHudLayout.Line);
        HiveTyrantHudLayout.Box(card, "Panel", new Rect(1, 1, 498, 266), HiveTyrantHudLayout.Ink);
        HiveTyrantHudLayout.Box(card, "Accent", new Rect(0, 0, 500, 3), HiveTyrantHudLayout.Rift);
        HiveTyrantHudLayout.Label(card, "Eyebrow", new Rect(24, 22, 452, 22), "HIVE TYRANT  /  TEST ARENA", 12, HiveTyrantHudLayout.Rift, TextAnchor.MiddleCenter);
        HiveTyrantHudLayout.Label(card, "Title", new Rect(24, 59, 452, 38), "YOU WERE DEFEATED", 27, HiveTyrantHudLayout.Paper, TextAnchor.MiddleCenter, true);
        defeatDetail = HiveTyrantHudLayout.Label(card, "Details", new Rect(24, 108, 452, 40), "", 15, HiveTyrantHudLayout.Muted, TextAnchor.MiddleCenter);
        retry = HiveTyrantHudLayout.Button(card, "Retry", new Rect(130, 169, 240, 46), "RETRY FIGHT  [F9]", RestartScene);
        HiveTyrantHudLayout.Label(card, "Footer", new Rect(24, 229, 452, 20), "A fresh attempt. Read the warning, find your opening.", 12, HiveTyrantHudLayout.Muted, TextAnchor.MiddleCenter);
        result.SetActive(false);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9)) { RestartScene(); return; }
        if (Input.GetKeyDown(KeyCode.F8)) ToggleControls();
        bool lost = (player != null && player.IsDead) || (GameManager.Instance != null && GameManager.Instance.IsGameOver);
        if (lost && !defeated)
        {
            defeated = true; result.SetActive(true); controls.SetActive(false);
            defeatDetail.text = boss != null && boss.Health != null ? "Boss remaining: " + boss.Health.CurrentHealth + " / " + boss.Health.MaxHealth + " HP" : "Prepare for another attempt.";
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retry.gameObject);
        }
        if (lost || boss == null) return;
        if (Input.GetKeyDown(KeyCode.F1)) boss.RequestSkill(HiveTyrantSkill.Sweep, true);
        if (Input.GetKeyDown(KeyCode.F2)) boss.RequestSkill(HiveTyrantSkill.Slam, true);
        if (Input.GetKeyDown(KeyCode.F3)) boss.RequestSkill(HiveTyrantSkill.Summon, true);
        if (Input.GetKeyDown(KeyCode.F4)) TriggerPhase2();
        if (Input.GetKeyDown(KeyCode.F5)) boss.Health.TakeDamage(boss.Health.CurrentHealth);
        if (Input.GetKeyDown(KeyCode.F6)) boss.AutomaticSkills = !boss.AutomaticSkills;
        if (Time.unscaledTime >= nextStatusUpdate)
        {
            nextStatusUpdate = Time.unscaledTime + .2f;
            hint.text = "TEST ARENA    F6 AUTO: " + (boss.AutomaticSkills ? "ON" : "OFF") + "    F8 CONTROLS";
            if (controls.activeSelf) status.text = boss.State + "   /   Summons " + boss.LivingSummons + "/" + boss.Summoning.AliveCap + "\nSummon budget used: " + boss.TotalSummoned + "/" + boss.Config.summonBudget;
        }
    }
    public void ToggleControls() { showControls = !showControls; if (controls != null && !defeated) controls.SetActive(showControls); }
    public void TriggerPhase2()
    {
        if (boss == null || boss.Health.IsDead) return;
        int threshold = Mathf.FloorToInt(boss.Health.MaxHealth * boss.Config.phase2HealthRatio);
        boss.Health.TakeDamage(Mathf.Max(0, boss.Health.CurrentHealth - threshold));
    }
    public static void RestartScene()
    {
        Time.timeScale = 1;
        // Build index is unnecessary: works when the scene is opened directly in the Editor.
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
    }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject); if (ownedEventSystem != null) Destroy(ownedEventSystem);
        if (defeated) { Cursor.visible = previousCursor; Cursor.lockState = previousLock; }
    }
}
