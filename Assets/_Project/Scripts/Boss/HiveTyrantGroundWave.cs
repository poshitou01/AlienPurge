using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HiveTyrantGroundWave : MonoBehaviour
{
    private HiveTyrantCombat boss;
    private HiveTyrantEffects effects;
    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();
    private PlayerHealth[] players;
    private PlayerAbilityState[] abilities;
    private Collider2D[][] bodies;
    private Vector2[] previousPlayers;
    public HiveTyrantWavePlan Plan { get; private set; }
    public bool IsTravelling { get; private set; }
    public bool IsWarning { get; private set; }
    public float Travelled { get; private set; }
    public int HitCount => hitPlayers.Count;
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
        hitPlayers.Clear(); IsWarning = false; IsTravelling = true;
        effects.DrawWave(Plan, 0, false);
    }
    private void FixedUpdate() { if (IsTravelling) Advance(Time.fixedDeltaTime); }
    // Relative swept rectangles cover the full step, including a player crossing a moving front.
    public void Advance(float dt)
    {
        if (!IsTravelling || Plan == null || boss.Health.IsDead) return;
        float from = Travelled;
        Travelled = Mathf.Min(Plan.distance + Plan.thickness, Travelled + Mathf.Max(0, dt) * Plan.speed);
        Plan.ClipWalls(boss.Config.environmentLayers);
        for (int p = 0; p < players.Length; p++)
        {
            PlayerHealth player = players[p];
            if (player == null) continue;
            Vector2 oldOffset = previousPlayers[p] - (Vector2)player.transform.position;
            if (!player.IsDead && !hitPlayers.Contains(player) && (abilities[p] == null || !abilities[p].IsAirborne))
            {
                foreach (Collider2D body in bodies[p])
                {
                    if (body == null || !body.enabled || body.isTrigger) continue;
                    HiveTyrantGeometry.Project(body, Plan.right, out float xl, out float xh);
                    HiveTyrantGeometry.Project(body, Plan.forward, out float zl, out float zh);
                    Vector2 center = new Vector2((xl + xh) * .5f - Vector2.Dot(Plan.origin, Plan.right), (zl + zh) * .5f - Vector2.Dot(Plan.origin, Plan.forward));
                    Vector2 extents = new Vector2((xh - xl) * .5f, (zh - zl) * .5f);
                    Vector2 oldCenter = center + new Vector2(Vector2.Dot(oldOffset, Plan.right), Vector2.Dot(oldOffset, Plan.forward));
                    for (int s = 0; s < 3; s++)
                    {
                        if (from > Plan.lengths[s] || Plan.lengths[s] <= 0) continue;
                        float stop = Mathf.Min(Travelled, Plan.lengths[s]);
                        float fraction = Travelled > from ? (stop - from) / (Travelled - from) : 1;
                        Vector2 endCenter = Vector2.Lerp(oldCenter, center, fraction);
                        Vector2 half = extents + new Vector2(Plan.width, Plan.thickness) * .5f;
                        Vector2 boxCenter = new Vector2(Plan.SegmentCenter(s), 0);
                        if (!HiveTyrantGeometry.SegmentBox(oldCenter - Vector2.up * from, endCenter - Vector2.up * stop, boxCenter - half, boxCenter + half, out float time)) continue;
                        Vector2 at = Vector2.Lerp(oldCenter, endCenter, time);
                        Vector2 point = Plan.Point(at.x, at.y);
                        if (Physics2D.Linecast(Plan.Point(at.x, 0), point, boss.Config.environmentLayers).collider != null) continue;
                        if (hitPlayers.Add(player)) player.TakeDamage(boss.Config.slamDamage);
                        break;
                    }
                    if (hitPlayers.Contains(player)) break;
                }
            }
            previousPlayers[p] = player.transform.position;
        }
        effects.DrawWave(Plan, Travelled, false);
        if (Travelled > Plan.MaximumLength + Plan.thickness * .5f) { IsTravelling = false; effects.HideWave(); }
    }
    public void Cancel()
    {
        IsTravelling = false; IsWarning = false; Plan = null; Travelled = 0; hitPlayers.Clear();
        if (effects != null) effects.HideWave();
    }
    private void OnDisable() { Cancel(); }
}
