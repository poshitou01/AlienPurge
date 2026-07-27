using System;
using UnityEngine;

/// <summary>
/// EnemyPool 中单个敌人类型的对象池配置。
///
/// 该类只保存池配置，不负责敌人生成、回收、
/// 属性计算或刷怪权重选择。
/// </summary>
[Serializable]
public class EnemyPoolEntry
{
    [Header("Enemy Identity")]

    [Tooltip("该对象池配置对应的敌人类型")]
    [SerializeField]
    private EnemyType enemyType =
        EnemyType.Normal;

    [Tooltip(
        "该类型使用的敌人 Prefab。"
        + "后续需要在根对象上挂载 PooledEnemy"
    )]
    [SerializeField]
    private GameObject enemyPrefab;

    [Header("Pool Capacity")]

    [Tooltip("游戏开始时预生成的对象数量")]
    [Min(0)]
    [SerializeField]
    private int initialPoolSize = 1;

    [Tooltip("对象池不足时是否允许自动扩容")]
    [SerializeField]
    private bool allowExpansion = true;

    [Tooltip("每次自动扩容创建的对象数量")]
    [Min(1)]
    [SerializeField]
    private int expansionAmount = 1;

    public EnemyType Type =>
        enemyType;

    public GameObject Prefab =>
        enemyPrefab;

    public int InitialPoolSize =>
        initialPoolSize;

    public bool AllowExpansion =>
        allowExpansion;

    public int ExpansionAmount =>
        expansionAmount;

    /// <summary>
    /// 检查单条对象池配置是否有效。
    ///
    /// EnemyType 是否重复由 EnemyPool 统一检查。
    /// </summary>
    public bool IsValid(
        out string validationMessage
    )
    {
        if (enemyPrefab == null)
        {
            validationMessage =
                enemyType
                + " 没有绑定 Enemy Prefab。";

            return false;
        }

        if (initialPoolSize < 0)
        {
            validationMessage =
                enemyType
                + " 的 Initial Pool Size 不能小于 0。";

            return false;
        }

        if (allowExpansion
            && expansionAmount <= 0)
        {
            validationMessage =
                enemyType
                + " 允许扩容时，"
                + "Expansion Amount 必须大于 0。";

            return false;
        }

        validationMessage = string.Empty;
        return true;
    }
}