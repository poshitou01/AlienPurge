using UnityEngine;

// Presentation only. Radius, damage, target count and evolution remain owned by the projectile.
[CreateAssetMenu(menuName = "AlienPurge/Combat/Special upgrade presentation")]
public sealed class UpgradeVfxProfile : ScriptableObject
{
    public Material material;
    public Sprite[] explosionFrames;
    [Range(.18f, .6f)] public float explosionDuration = .38f;
    [Range(.1f, .35f)] public float lightningDuration = .20f;
    [Range(0, .035f)] public float hopDelay = .018f;
    public Color explosionColor = new Color(1f, .4f, .075f);
    public Color lightningColor = new Color(.2f, .65f, 1f);
    public AudioClip explosionSound, lightningSound;
    [Range(0, 1)] public float explosionVolume = .32f, lightningVolume = .24f;
}
