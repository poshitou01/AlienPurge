using System;
using UnityEngine;

/// <summary>
/// EnemySpawner 中单个敌人类型的生成配置。
///
/// 该类只保存生成所需的数据，
/// 不负责生成敌人或计算全局难度。
/// </summary>
[Serializable]
public class EnemySpawnEntry
{
    [Header("Enemy Identity")]
    [Tooltip("该生成配置对应的敌人类型")]
    [SerializeField]
    private EnemyType enemyType =
        EnemyType.Normal;

    [Tooltip("实际生成的敌人 Prefab")]
    [SerializeField]
    private GameObject enemyPrefab;

    [Header("Unlock Settings")]
    [Tooltip("生存时间达到多少秒后允许生成该敌人")]
    [Min(0f)]
    [SerializeField]
    private float unlockTime;

    [Header("Weight Settings")]
    [Tooltip(
        "该敌人的随机生成权重。"
        + "权重小于或等于 0 时不进入候选"
    )]
    [SerializeField]
    private float spawnWeight = 1f;

    public EnemyType Type =>
        enemyType;

    public GameObject Prefab =>
        enemyPrefab;

    public float UnlockTime =>
        unlockTime;

    public float SpawnWeight =>
        spawnWeight;
}