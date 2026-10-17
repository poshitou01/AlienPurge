using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Only installed in the dedicated test scene. No debug cheats in Level01.</summary>
public sealed class HiveTyrantCombatDebug : MonoBehaviour
{
    [SerializeField] private HiveTyrantCombat boss;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9)) RestartScene();
        if (boss == null) return;
        if (Input.GetKeyDown(KeyCode.F1)) boss.RequestSkill(HiveTyrantSkill.Sweep, true);
        if (Input.GetKeyDown(KeyCode.F2)) boss.RequestSkill(HiveTyrantSkill.Slam, true);
        if (Input.GetKeyDown(KeyCode.F3)) boss.RequestSkill(HiveTyrantSkill.Summon, true);
        if (Input.GetKeyDown(KeyCode.F4)) TriggerPhase2();
        if (Input.GetKeyDown(KeyCode.F5)) boss.Health.TakeDamage(boss.Health.CurrentHealth);
        if (Input.GetKeyDown(KeyCode.F6)) boss.AutomaticSkills = !boss.AutomaticSkills;
    }
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
    private void OnGUI()
    {
        GUI.Box(new Rect(12, 92, 620, 100), "Hive Tyrant combat test - directional joint animation / native enemy AI");
        GUI.Label(new Rect(24, 116, 600, 24), "F1 Sweep | F2 Ground wave | F3 Summon | F4 Phase2 | F5 Kill | F6 Auto AI | F9 Restart");
        if (boss != null)
            GUI.Label(new Rect(24, 144, 600, 32), boss.State + " | Auto: " + boss.AutomaticSkills + " | Summons: " + boss.LivingSummons + "/" + boss.Config.summonAliveCap + " | Budget used: " + boss.TotalSummoned);
        if (GUI.Button(new Rect(12, 200, 130, 28), "Restart (F9)")) RestartScene();
    }
}
