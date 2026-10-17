/// <summary>Optional presentation lease; EnemyHealth remains the sole health/reward owner.</summary>
public interface IEnemyDeathPresentation
{
    float DeathPresentationDuration { get; }
    void BeginDeathPresentation();
}
