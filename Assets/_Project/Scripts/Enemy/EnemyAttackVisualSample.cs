using UnityEngine;

// Read-only view of existing attack state. Presentation never writes a timer,
// emits a projectile, applies damage, or drives a gameplay transition.
public enum EnemyAttackVisualPhase { None, Windup, Active, Recover }

public readonly struct EnemyAttackVisualSample
{
    public readonly EnemyAttackVisualPhase Phase;
    public readonly float Progress, Duration;
    public readonly Vector2 Direction;
    public EnemyAttackVisualSample(EnemyAttackVisualPhase phase, float remaining, float duration, Vector2 direction)
    {
        Phase = phase; Duration = duration; Direction = direction;
        Progress = duration > 0 ? 1 - Mathf.Clamp01(remaining / duration) : 1;
    }
}
