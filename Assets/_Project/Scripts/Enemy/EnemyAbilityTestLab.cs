using UnityEngine;
using UnityEngine.SceneManagement;

// Only installed in SupportExploder_Test. No global spawner or level registration.
public sealed class EnemyAbilityTestLab : MonoBehaviour
{
    public bool spawnOnStart = true;
    public bool previewOnStart;
    private PlayerHealth player;
    private string lastAction = "Opening encounter";
    private GUIStyle title, text;
    private bool previewMode;
    private int previewDirection = 1, previewState;
    private float resumeScale = 1;
    private readonly PooledEnemy[] previewActors = new PooledEnemy[2];
    private static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    private void Start()
    {
        player = FindObjectOfType<PlayerHealth>();
        if (previewOnStart) EnterPreview();
        else if (spawnOnStart) SpawnEncounter();
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
        foreach (var enemy in FindObjectsOfType<PooledEnemy>()) if (!enemy.IsReturned)
        {
            enemy.ReturnToPool();
            // Preview disabled these on pooled instances; restore while inactive so
            // the next real spawn runs the normal OnEnable lifecycle.
            if (enemy.TryGetComponent<SupportEnemyAbility>(out var support)) support.enabled = true;
            if (enemy.TryGetComponent<ExploderEnemyAbility>(out var exploder)) exploder.enabled = true;
        }
        previewMode = false; previewActors[0] = previewActors[1] = null;
        if (Camera.main != null) Camera.main.orthographicSize = 10.5f;
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
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (Time.timeScale > 0) { resumeScale = Time.timeScale; Time.timeScale = 0; }
            else Time.timeScale = resumeScale;
        }
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        if (Input.GetKeyDown(KeyCode.F5))
        {
            if (previewMode) { RecycleAll(); SpawnEncounter(); }
            else EnterPreview();
        }
        if (previewMode)
        {
            if (Input.GetKeyDown(KeyCode.F6)) { previewDirection = (previewDirection + 1) % 8; ApplyPreview(); }
            if (Input.GetKeyDown(KeyCode.F7)) { previewState = (previewState + 1) % 7; ApplyPreview(); }
            if (Input.GetKeyDown(KeyCode.F10) && Camera.main != null) Camera.main.orthographicSize = Camera.main.orthographicSize > 6 ? 3.5f : 10.5f;
            if (Input.GetKeyDown(KeyCode.F3)) RecycleAll();
            return;
        }
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
    public void EnterPreview()
    {
        RecycleAll(); previewMode = true; previewDirection = 1; previewState = 0;
        foreach (var orb in FindObjectsOfType<ExperienceOrb>()) orb.ReturnToPool();
        previewActors[0] = Spawn(EnemyType.Support, new Vector2(-1.3f, 0));
        previewActors[1] = Spawn(EnemyType.Exploder, new Vector2(1.3f, 0));
        foreach (var actor in previewActors)
        {
            if (actor == null) continue;
            actor.GetComponent<EnemyMovement>().enabled = false;
            if (actor.TryGetComponent<SupportEnemyAbility>(out var support)) support.enabled = false;
            if (actor.TryGetComponent<ExploderEnemyAbility>(out var exploder)) exploder.enabled = false;
            foreach (var collider in actor.GetComponents<Collider2D>()) collider.enabled = false;
        }
        ApplyPreview();
    }
    private void ApplyPreview()
    {
        SupportExploderAnimation.Pose[] support = { SupportExploderAnimation.Pose.Idle, SupportExploderAnimation.Pose.Move,
            SupportExploderAnimation.Pose.SupportStart, SupportExploderAnimation.Pose.SupportLoop, SupportExploderAnimation.Pose.SupportMove,
            SupportExploderAnimation.Pose.SupportEnd, SupportExploderAnimation.Pose.Death };
        SupportExploderAnimation.Pose[] exploder = { SupportExploderAnimation.Pose.Idle, SupportExploderAnimation.Pose.Move,
            SupportExploderAnimation.Pose.Warning, SupportExploderAnimation.Pose.Warning, SupportExploderAnimation.Pose.Burst,
            SupportExploderAnimation.Pose.Death, SupportExploderAnimation.Pose.Death };
        if (previewActors[0] != null) previewActors[0].GetComponent<SupportExploderAnimation>().Preview(support[previewState], previewDirection);
        if (previewActors[1] != null) previewActors[1].GetComponent<SupportExploderAnimation>().Preview(exploder[previewState], previewDirection);
        lastAction = "BODY PREVIEW " + Directions[previewDirection] + " | " + support[previewState] + " / " + exploder[previewState];
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
        GUI.Box(new Rect(14, 12, 740, 200), GUIContent.none);
        GUI.Label(new Rect(28, 22, 660, 30), "ALIENPURGE  /  SUPPORT + EXPLODER LAB", title);
        GUI.Label(new Rect(28, 55, 710, 25), "AUTHORED ART / 8 directions / state-driven sprites / native enemy pool", text);
        GUI.Label(new Rect(28, 80, 660, 25), "WASD move  |  LMB shoot  |  Shift Dash  |  Space Jet Jump", text);
        GUI.Label(new Rect(28, 103, 660, 25), "F1 encounter   F2 Exploder   F3 recycle   F4 kill specialists   F9 Restart", text);
        GUI.Label(new Rect(28, 128, 710, 25), "F5 body preview / combat   F6 direction   F7 pose   F8 pause   F10 preview zoom", text);
        if (player != null && GameManager.Instance != null)
        GUI.Label(new Rect(28, 153, 710, 25), "HP " + player.CurrentHealth + "/" + player.MaxHealth + "   Kills " + GameManager.Instance.KillCount + "   " + lastAction, text);
        GUI.Label(new Rect(28, 177, 710, 25), previewMode ? "Visual inspection only; F5 returns to the real abilities and collision tests." : "Warning lasts 1.5s. Kill the Exploder or leave the circle before it expires.", text);
        if (player != null && player.IsDead) GUI.Label(new Rect(Screen.width * .5f - 140, Screen.height * .5f, 400, 50), "DEFEATED  /  F9 to restart", title);
    }
}
