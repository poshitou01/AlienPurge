using UnityEngine;

// One immutable orientation and lane layout per cast. Lengths can only shrink when a wall closes.
public sealed class HiveTyrantWavePlan
{
    public Vector2 origin, forward, right;
    public float width, gap, thickness, speed, distance;
    public readonly float[] lengths = new float[3];
    public readonly bool[] reachableGaps = new bool[2];
    public float SegmentCenter(int segment) => (segment - 1) * (width + gap);
    public float GapCenter(int index) => (index == 0 ? -1 : 1) * (width + gap) * .5f;
    public Vector2 Point(float across, float along) => origin + right * across + forward * along;
    public float MaximumLength => Mathf.Max(lengths[0], lengths[1], lengths[2]);

    public static bool TryCreate(HiveTyrantCombat boss, out HiveTyrantWavePlan plan)
    {
        plan = null;
        var player = boss.Motor.Target.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead) return false;
        var c = boss.Config;
        Vector2 forward = ((Vector2)player.transform.position - boss.Motor.CombatOrigin).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector2.down;
        var p = new HiveTyrantWavePlan {
            forward = forward, right = new Vector2(forward.y, -forward.x),
            origin = boss.Motor.CombatOrigin + forward * c.waveStartOffset,
            width = c.waveSegmentWidth, thickness = c.waveThickness, speed = c.waveSpeed, distance = c.waveDistance
        };
        p.gap = Mathf.Max(c.waveMinimumGap, HiveTyrantGeometry.PlayerWidth(player, p.right) * c.waveGapBodyMultiplier + c.waveGapMargin);
        for (int i = 0; i < 3; i++)
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
        float arrival = c.slamWindup + Mathf.Max(0, playerZ - p.thickness * .5f - radius) / p.speed;
        for (int i = 0; i < 2; i++)
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
        }
        if (!p.reachableGaps[0] && !p.reachableGaps[1]) return false;
        plan = p; return true;
    }
    public void ClipWalls(LayerMask mask)
    {
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90;
        for (int i = 0; i < 3; i++)
        {
            RaycastHit2D hit = Physics2D.BoxCast(Point(SegmentCenter(i), 0), new Vector2(width, .04f), angle, forward, lengths[i], mask);
            if (hit.collider != null) lengths[i] = Mathf.Min(lengths[i], Mathf.Max(0, hit.distance - .04f));
        }
    }
}
