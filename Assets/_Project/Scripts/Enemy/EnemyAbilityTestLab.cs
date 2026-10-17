using UnityEngine;
using UnityEngine.SceneManagement;

// Only installed in SupportExploder_Test. No global spawner or level registration.
public sealed class EnemyAbilityTestLab : MonoBehaviour
{
    public bool spawnOnStart = true;
    private PlayerHealth player;
    private string lastAction = "Opening encounter";
    private GUIStyle title, text;
    private void Start()
    {
        player = FindObjectOfType<PlayerHealth>();
        if (spawnOnStart) SpawnEncounter();
    }
    public PooledEnemy Spawn(EnemyType type, Vector2 position)
    {
        var enemy = EnemyPool.Instance.GetEnemy(type, position, Quaternion.identity);
        if (enemy != null) enemy.GetComponent<EnemyDefinition>().InitializeFromGlobalDifficulty(4, 1.8f, 1);
        return enemy;
    }
    public void SpawnEncounter()
    {
        if (EnemyPool.Instance.GetActiveCount(EnemyType.Support) < 2) Spawn(EnemyType.Support, new Vector2(-7, 4));
        if (EnemyPool.Instance.GetActiveCount(EnemyType.Normal) < 6)
        {
            Spawn(EnemyType.Normal, new Vector2(-5, 3)); Spawn(EnemyType.Normal, new Vector2(-7, 1)); Spawn(EnemyType.Normal, new Vector2(-9, 3));
        }
        SpawnSafeExploder();
    }
    public void RecycleAll()
    {
        foreach (var enemy in FindObjectsOfType<PooledEnemy>()) if (!enemy.IsReturned) enemy.ReturnToPool();
        lastAction = "All enemies recycled (no explosion / rewards)";
    }
    public void SpawnSafeExploder()
    {
        if (player == null || EnemyPool.Instance.GetActiveCount(EnemyType.Exploder) >= 3) return;
        Vector2 spot = (Vector2)player.transform.position + ((player.transform.position.x < 0) ? Vector2.right : Vector2.left) * 8;
        spot.x = Mathf.Clamp(spot.x, -14, 14); spot.y = Mathf.Clamp(spot.y + 4, -7, 7);
        if (Vector2.Distance(spot, player.transform.position) < 5 || Physics2D.OverlapCircle(spot, .6f, 1 << 7) != null) spot = new Vector2(12, 6);
        if (Vector2.Distance(spot, player.transform.position) < 5) return;
        Spawn(EnemyType.Exploder, spot); lastAction = "Exploder spawned with safe separation";
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9)) Restart();
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        if (Input.GetKeyDown(KeyCode.F1)) SpawnEncounter();
        if (Input.GetKeyDown(KeyCode.F2)) SpawnSafeExploder();
        if (Input.GetKeyDown(KeyCode.F3)) RecycleAll();
        if (Input.GetKeyDown(KeyCode.F4))
        {
            foreach (var enemy in FindObjectsOfType<PooledEnemy>())
                if (!enemy.IsReturned && (enemy.Type == EnemyType.Support || enemy.Type == EnemyType.Exploder)) enemy.GetComponent<EnemyHealth>().TakeDamage(9999);
            lastAction = "Lethal damage to specialists (cancels active warning)";
        }
    }
    public static void Restart()
    {
        Time.timeScale = 1;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
    }
    private void OnGUI()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        }
        GUI.Box(new Rect(14, 12, 680, 154), GUIContent.none);
        GUI.Label(new Rect(28, 22, 660, 30), "ALIENPURGE  /  SUPPORT + EXPLODER LAB", title);
        GUI.Label(new Rect(28, 55, 660, 25), "TEMP ART: shared Swarmer gait + procedural organs. New enemy art is pending.", text);
        GUI.Label(new Rect(28, 80, 660, 25), "WASD move  |  LMB shoot  |  Shift Dash  |  Space Jet Jump", text);
        GUI.Label(new Rect(28, 103, 660, 25), "F1 encounter   F2 Exploder   F3 recycle   F4 kill specialists   F9 Restart", text);
        if (player != null && GameManager.Instance != null)
        GUI.Label(new Rect(28, 128, 660, 25), "HP " + player.CurrentHealth + "/" + player.MaxHealth + "   Kills " + GameManager.Instance.KillCount + "   " + lastAction, text);
        if (player != null && player.IsDead) GUI.Label(new Rect(Screen.width * .5f - 140, Screen.height * .5f, 400, 50), "DEFEATED  /  F9 to restart", title);
    }
}
