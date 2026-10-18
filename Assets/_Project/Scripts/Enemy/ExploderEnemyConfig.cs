using UnityEngine;

public enum ExploderDamageFaction { PlayersOnly = 0 }

[CreateAssetMenu(fileName = "Exploder_Ability", menuName = "AlienPurge/Enemies/Exploder Ability")]
public sealed class ExploderEnemyConfig : ScriptableObject
{
    [Min(.5f)] public float triggerDistance = 2.4f;
    [Min(.5f)] public float explosionRadius = 3f;
    [Min(.8f)] public float warningDuration = 1.5f;
    [Min(.5f)] public float spawnGraceDuration = 1.5f;
    [Tooltip("Must observe the player at this safe distance at least once after spawn before arming.")]
    [Min(3.5f)] public float safeArmingDistance = 4.5f;
    [Min(1f)] public float damageMultiplier = 2f;
    public ExploderDamageFaction victims = ExploderDamageFaction.PlayersOnly;
    [Tooltip("Player currently uses Default. Layer filtering is followed by PlayerHealth + Player tag checks.")]
    public LayerMask victimLayers = 1;
    [Tooltip("Native Jet Jump avoids this ground blast; Dash retains native PlayerHealth invulnerability.")]
    public bool groundOnly = true;
    // V1 is a uniform ground circle, including behind cover. There is no hidden LOS damage mask.
    private void OnValidate()
    {
        explosionRadius = Mathf.Max(.5f, explosionRadius);
        triggerDistance = Mathf.Clamp(triggerDistance, .5f, explosionRadius);
        safeArmingDistance = Mathf.Max(safeArmingDistance, explosionRadius + 1f);
        warningDuration = Mathf.Max(.8f, warningDuration);
        spawnGraceDuration = Mathf.Max(.5f, spawnGraceDuration);
    }
}
