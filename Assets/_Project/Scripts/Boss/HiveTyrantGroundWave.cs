using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HiveTyrantGroundWave : MonoBehaviour
{
    private HiveTyrantCombat boss;
    private HiveTyrantEffects effects;
    private readonly HashSet<PlayerHealth>[] hits = { new HashSet<PlayerHealth>(), new HashSet<PlayerHealth>(), new HashSet<PlayerHealth>() };
    private float elapsed;
    private PlayerHealth[] players;
    private PlayerAbilityState[] abilities;
    private Collider2D[][] bodies;
    private Vector2[] previousPlayers;
    public HiveTyrantWavePlan Plan { get; private set; }
    public bool IsTravelling { get; private set; }
    public bool IsWarning { get; private set; }
    public float Travelled { get; private set; }
    public int HitCount { get { int count = 0; foreach (var hit in hits) count += hit.Count; return count; } }
    public int LaunchedFronts => Plan == null || IsWarning ? 0 : Mathf.Min(Plan.volleyCount, 1 + Mathf.FloorToInt(elapsed / Plan.volleyInterval));
    public float FrontDistance(int index) => Plan == null ? 0 : (elapsed - index * Plan.volleyInterval) * Plan.speed;
    private void Awake() { boss = GetComponent<HiveTyrantCombat>(); effects = GetComponent<HiveTyrantEffects>(); }
    public void Prepare(HiveTyrantWavePlan plan)
    {
        Cancel(); Plan = plan; IsWarning = true; Travelled = 0;
        effects.DrawWave(plan, 0, true);
    }
    public void Launch()
    {
        if (Plan == null) return;
        if (players == null)
        {
            players = FindObjectsOfType<PlayerHealth>(); bodies = new Collider2D[players.Length][]; previousPlayers = new Vector2[players.Length]; abilities = new PlayerAbilityState[players.Length];
            for (int i = 0; i < players.Length; i++) { bodies[i] = players[i].GetComponentsInChildren<Collider2D>(); abilities[i] = players[i].GetComponent<PlayerAbilityState>(); }
        }
        for (int i = 0; i < players.Length; i++) if (players[i] != null) previousPlayers[i] = players[i].transform.position;
        foreach (var hit in hits) hit.Clear();
        elapsed = Travelled = 0; IsWarning = false; IsTravelling = true;
        effects.DrawWave(Plan, 0, false);
    }
    private void FixedUpdate() { if (IsTravelling) Advance(Time.fixedDeltaTime); }
    private void Update()
    {
        if ((IsWarning || IsTravelling) && (boss.Health.IsDead || !boss.TargetInArena ||
            (GameManager.Instance != null && !GameManager.Instance.IsPlaying))) Cancel();
    }
    // Relative swept rectangles cover the full step, including a player crossing a moving front.
    public void Advance(float dt)
    {
        if (!IsTravelling || Plan == null || boss.Health.IsDead) return;
        if (dt <= 0) return;
        float previousTime = elapsed;
        elapsed += dt; Travelled = elapsed * Plan.speed;
        Plan.ClipWalls(boss.Config.environmentLayers);
        for (int p = 0; p < players.Length; p++)
        {
            PlayerHealth player = players[p];
            if (player == null) continue;
            Vector2 oldOffset = previousPlayers[p] - (Vector2)player.transform.position;
            if (!player.IsDead && (abilities[p] == null || !abilities[p].IsAirborne))
            {
                for (int front = 0; front < Plan.volleyCount; front++)
                {
                    float begins = front * Plan.volleyInterval;
                    if (elapsed < begins || hits[front].Contains(player)) continue;
                    float from = Mathf.Max(0, previousTime - begins) * Plan.speed, to = (elapsed - begins) * Plan.speed;
                    // A front born partway through a long step must not hit an earlier player position.
                    float birthFraction = Mathf.Clamp01((begins - previousTime) / dt);
                    if (Touches(p, oldOffset, from, to, birthFraction))
                    { hits[front].Add(player); player.TakeDamage(boss.Config.slamDamage); }
                    if (player.IsDead) break;
                }
            }
            previousPlayers[p] = player.transform.position;
        }
        effects.DrawWave(Plan, Travelled, false);
        if (FrontDistance(Plan.volleyCount - 1) > Plan.MaximumLength + Plan.thickness * .5f) { IsTravelling = false; effects.HideWave(); }
    }
    private bool Touches(int player, Vector2 oldOffset, float from, float to, float birthFraction)
    {
        foreach (Collider2D body in bodies[player])
        {
            if (body == null || !body.enabled || body.isTrigger) continue;
            HiveTyrantGeometry.Project(body, Plan.right, out float xl, out float xh);
            HiveTyrantGeometry.Project(body, Plan.forward, out float zl, out float zh);
            Vector2 center = new Vector2((xl + xh) * .5f - Vector2.Dot(Plan.origin, Plan.right), (zl + zh) * .5f - Vector2.Dot(Plan.origin, Plan.forward));
            Vector2 extents = new Vector2((xh - xl) * .5f, (zh - zl) * .5f);
            Vector2 beforeBirth = center + new Vector2(Vector2.Dot(oldOffset, Plan.right), Vector2.Dot(oldOffset, Plan.forward));
            Vector2 oldCenter = Vector2.Lerp(beforeBirth, center, birthFraction);
            for (int s = 0; s < Plan.SegmentCount; s++)
            {
                if (from > Plan.lengths[s] || Plan.lengths[s] <= 0) continue;
                float stop = Mathf.Min(to, Plan.lengths[s]);
                float fraction = to > from ? (stop - from) / (to - from) : 1;
                Vector2 endCenter = Vector2.Lerp(oldCenter, center, fraction);
                Vector2 half = extents + new Vector2(Plan.width, Plan.thickness) * .5f;
                Vector2 boxCenter = new Vector2(Plan.SegmentCenter(s), 0);
                if (!HiveTyrantGeometry.SegmentBox(oldCenter - Vector2.up * from, endCenter - Vector2.up * stop, boxCenter - half, boxCenter + half, out float time)) continue;
                Vector2 at = Vector2.Lerp(oldCenter, endCenter, time);
                if (Physics2D.Linecast(Plan.Point(at.x, 0), Plan.Point(at.x, at.y), boss.Config.environmentLayers).collider == null) return true;
            }
        }
        return false;
    }
    public void Cancel()
    {
        IsTravelling = false; IsWarning = false; Plan = null; elapsed = Travelled = 0;
        foreach (var hit in hits) hit.Clear();
        if (effects != null) effects.HideWave();
    }
    private void OnDisable() { Cancel(); }
}
