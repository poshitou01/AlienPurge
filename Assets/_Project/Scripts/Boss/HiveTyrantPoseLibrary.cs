using UnityEngine;

[CreateAssetMenu(menuName = "AlienPurge/Boss/Hive Tyrant Pose Library")]
public sealed class HiveTyrantPoseLibrary : ScriptableObject
{
    public HiveTyrantDirectionalRig[] directions = new HiveTyrantDirectionalRig[16];
    [HideInInspector] public int authoringVersion;
}

[System.Serializable]
public sealed class HiveTyrantDirectionalRig
{
    [HideInInspector] public byte[] skinCells;
    public Sprite source;
    [Tooltip("Authoring coordinates: pixels from top-left of the original 1024 canvas.")]
    public Vector2 scytheJoint, scytheTip, fistJoint, fistTip, chestPivot;
    public Vector2[] scytheRegion, fistRegion;
    public float swingSign = 1;
    [Range(.1f, 1)] public float jointAmplitude = 1;
    public float torsoAnchorY = 780;
    public Vector2 wingHinge = new Vector2(512, 460);
}
