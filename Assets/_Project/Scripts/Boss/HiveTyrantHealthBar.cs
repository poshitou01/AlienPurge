using UnityEngine;

// Minimal boss HUD using existing artwork and the original EnemyHealth values.
public sealed class HiveTyrantHealthBar : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    [SerializeField] private Texture2D frame;
    [SerializeField] private Texture2D portrait;
    private void Awake() { if (boss == null) boss = GetComponent<HiveTyrantCombat>(); }
    private void OnGUI()
    {
        if (boss == null || boss.State == HiveTyrantState.Dormant || boss.State == HiveTyrantState.Disabled) return;
        float width = Mathf.Min(560, Screen.width - 40);
        Rect area = new Rect((Screen.width - width) / 2, 18, width, 66);
        GUI.Box(area, "");
        Color saved = GUI.color;
        GUI.color = boss.Phase2Entered ? new Color(1, .55f, .12f) : new Color(.65f, .8f, .25f);
        GUI.DrawTexture(new Rect(area.x + 56, area.y + 31, (width - 70) * boss.Health.CurrentHealth / Mathf.Max(1f, boss.Health.MaxHealth), 16), Texture2D.whiteTexture);
        GUI.color = saved;
        if (portrait != null) GUI.DrawTexture(new Rect(area.x + 4, area.y + 4, 50, 54), portrait, ScaleMode.ScaleToFit);
        if (frame != null) GUI.DrawTexture(new Rect(area.x + 54, area.y + 24, width - 62, 30), frame, ScaleMode.StretchToFill);
        GUI.Label(new Rect(area.x + 60, area.y + 5, width - 70, 25), "HIVE TYRANT  |  PHASE " + (boss.Phase2Entered ? "2" : "1") + "  |  " + boss.Health.CurrentHealth + "/" + boss.Health.MaxHealth);
    }
}
