using UnityEngine;

// One immutable orientation and lane layout per cast. Lengths can only shrink when a wall closes.
public sealed class HiveTyrantWavePlan
{
    public Vector2 origin, forward, right;
    public const int MaximumSegments = 5, MaximumVolleys = 3;
    public float width, gap, thickness, speed, distance, warning;
    public float volleyInterval = .65f;
    public int volleyCount = 1;
    public bool enraged;
    public readonly float[] lengths;
    public readonly bool[] reachableGaps;
    public int SegmentCount => lengths.Length;
    public int GapCount => reachableGaps.Length;
    public HiveTyrantWavePlan(int segments = 3)
    {
        lengths = new float[Mathf.Clamp(segments, 2, MaximumSegments)];
        reachableGaps = new bool[lengths.Length - 1];
    }
    public float SegmentCenter(int segment) => (segment - (SegmentCount - 1) * .5f) * (width + gap);
    public float GapCenter(int index) => (SegmentCenter(index) + SegmentCenter(index + 1)) * .5f;
    public Vector2 Point(float across, float along) => origin + right * across + forward * along;
    public float MaximumLength { get { float longest = 0; foreach (float length in lengths) longest = Mathf.Max(longest, length); return longest; } }

    public static bool TryCreate(HiveTyrantCombat boss, out HiveTyrantWavePlan plan)
    {
        plan = null;
        var player = boss.Motor.Target.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead) return false;
        var c = boss.Config;
        Vector2 forward = ((Vector2)player.transform.position - boss.Motor.CombatOrigin).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector2.down;
        bool phase2 = boss.Phase2Entered;
        var p = new HiveTyrantWavePlan(phase2 ? 5 : 3) {
            forward = forward, right = new Vector2(forward.y, -forward.x),
            origin = boss.Motor.CombatOrigin + forward * c.waveStartOffset,
            width = c.waveSegmentWidth, thickness = c.waveThickness, speed = phase2 ? c.phase2WaveSpeed : c.waveSpeed, distance = c.waveDistance,
            warning = phase2 ? c.phase2SlamWindup : c.slamWindup, enraged = phase2,
            volleyCount = phase2 ? Mathf.Clamp(c.phase2WaveVolleyCount, 2, MaximumVolleys) : 1,
            volleyInterval = Mathf.Max(.4f, c.phase2WaveVolleyInterval)
        };
        p.gap = Mathf.Max(c.waveMinimumGap, HiveTyrantGeometry.PlayerWidth(player, p.right) * c.waveGapBodyMultiplier + c.waveGapMargin);
        for (int i = 0; i < p.SegmentCount; i++)
        {
            p.lengths[i] = p.distance;
            for (float z = 0; z <= p.distance; z += .25f)
            {
                if (!boss.InsideArena(p.Point(p.SegmentCenter(i) - p.width * .5f, z)) ||
                    !boss.InsideArena(p.Point(p.SegmentCenter(i) + p.width * .5f, z)))
                { p.lengths[i] = Mathf.Max(0, z - .25f); break; }
            }
        }
        p.ClipWalls(c.environmentLayers);
        if (p.MaximumLength < 1) return false;
        // Validate a complete collider-sized walk to a gap within the advertised reaction time.
        float radius = Mathf.Max(HiveTyrantGeometry.PlayerWidth(player, Vector2.right), HiveTyrantGeometry.PlayerWidth(player, Vector2.up)) * .5f + .06f;
        float playerZ = Vector2.Dot((Vector2)player.transform.position - p.origin, forward);
        float arrival = p.warning + Mathf.Max(0, playerZ - p.thickness * .5f - radius) / p.speed;
        bool anyReachable = false;
        for (int i = 0; i < p.GapCount; i++)
        {
            Vector2 escape = p.Point(p.GapCenter(i), Mathf.Clamp(playerZ, 0, p.MaximumLength));
            Vector2 walk = escape - (Vector2)player.transform.position;
            float walkingSpeed = player.GetComponent<PlayerMovement>().MoveSpeed;
            bool clear = boss.InsideArena(escape) && walk.magnitude / Mathf.Max(.1f, walkingSpeed) + c.waveRouteReactionMargin <= arrival;
            if (clear && walk.sqrMagnitude > .001f)
                clear = Physics2D.CircleCast(player.transform.position, radius, walk.normalized, walk.magnitude, c.environmentLayers).collider == null;
            // The safe lane itself must not be a wall pocket or terminate immediately against the player.
            for (float z = 0; clear && z <= Mathf.Min(p.MaximumLength, Mathf.Max(playerZ + 1, 2)); z += .5f)
            {
                Vector2 point = p.Point(p.GapCenter(i), z);
                clear = boss.InsideArena(point) && Physics2D.OverlapCircle(point, radius, c.environmentLayers) == null;
            }
            p.reachableGaps[i] = clear;
            anyReachable |= clear;
        }
        if (!anyReachable) return false;
        plan = p; return true;
    }
    public void ClipWalls(LayerMask mask)
    {
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90;
        for (int i = 0; i < SegmentCount; i++)
        {
            RaycastHit2D hit = Physics2D.BoxCast(Point(SegmentCenter(i), 0), new Vector2(width, .04f), angle, forward, lengths[i], mask);
            if (hit.collider != null) lengths[i] = Mathf.Min(lengths[i], Mathf.Max(0, hit.distance - .04f));
        }
    }
}
