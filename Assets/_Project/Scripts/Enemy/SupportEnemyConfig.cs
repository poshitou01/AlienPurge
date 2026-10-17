using UnityEngine;

[CreateAssetMenu(fileName = "Support_Ability", menuName = "AlienPurge/Enemies/Support Ability")]
public sealed class SupportEnemyConfig : ScriptableObject
{
    [Min(.5f)] public float supportRadius = 4.5f;
    [Range(1, 6)] public int maximumTargets = 3;
    [Tooltip("Integer damage system: +1 is the smallest visible increase. Multiple sources use the strongest bonus only.")]
    [Range(1, 3)] public int bonusDamage = 1;
    [Min(.1f)] public float refreshInterval = .2f;
    [Min(1f)] public float allySearchRadius = 10f;
    [Min(1f)] public float keepAwayFromPlayer = 5f;
    [Min(.5f)] public float behindAllyDistance = 2f;
    [Tooltip("Current enemies use Default, not a dedicated Enemy physics layer.")]
    public LayerMask allyLayers = 1;
}
