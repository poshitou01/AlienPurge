using UnityEngine;

public static class HiveTyrantGeometry
{
    // Project the actual collider, not the sprite canvas. Conservative bounds only for unknown shapes.
    public static void Project(Collider2D c, Vector2 axis, out float low, out float high)
    {
        Vector2 center = c.transform.TransformPoint(c.offset);
        float extent;
        if (c is CircleCollider2D circle)
        {
            Vector2 x = c.transform.TransformVector(Vector3.right * circle.radius);
            Vector2 y = c.transform.TransformVector(Vector3.up * circle.radius);
            extent = Mathf.Sqrt(Mathf.Pow(Vector2.Dot(x, axis), 2) + Mathf.Pow(Vector2.Dot(y, axis), 2));
        }
        else if (c is BoxCollider2D box)
            extent = Mathf.Abs(Vector2.Dot(c.transform.TransformVector(Vector3.right * box.size.x * .5f), axis)) +
                     Mathf.Abs(Vector2.Dot(c.transform.TransformVector(Vector3.up * box.size.y * .5f), axis));
        else
        { center = c.bounds.center; extent = Mathf.Abs(axis.x) * c.bounds.extents.x + Mathf.Abs(axis.y) * c.bounds.extents.y; }
        float middle = Vector2.Dot(center, axis); low = middle - extent; high = middle + extent;
    }
    public static float PlayerWidth(PlayerHealth player, Vector2 axis)
    {
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        foreach (Collider2D c in player.GetComponentsInChildren<Collider2D>())
        {
            if (!c.enabled || c.isTrigger) continue;
            Project(c, axis, out float l, out float h); low = Mathf.Min(low, l); high = Mathf.Max(high, h);
        }
        return float.IsInfinity(low) ? .72f : high - low;
    }
    public static bool SegmentBox(Vector2 from, Vector2 to, Vector2 min, Vector2 max, out float time)
    {
        float enter = 0, leave = 1;
        Vector2 delta = to - from;
        for (int axis = 0; axis < 2; axis++)
        {
            if (Mathf.Abs(delta[axis]) < .000001f)
            { if (from[axis] < min[axis] || from[axis] > max[axis]) { time = 0; return false; } }
            else
            {
                float a = (min[axis] - from[axis]) / delta[axis], b = (max[axis] - from[axis]) / delta[axis];
                if (a > b) { float swap = a; a = b; b = swap; }
                enter = Mathf.Max(enter, a); leave = Mathf.Min(leave, b);
                if (enter > leave) { time = 0; return false; }
            }
        }
        time = enter; return leave >= 0 && enter <= 1;
    }
}
