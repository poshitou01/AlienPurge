/// <summary>
/// 当前游戏支持的敌人类型。
/// 不同敌人类型共用相同的基础组件，
/// 并通过 EnemyData 与独立攻击脚本产生差异。
/// </summary>
public enum EnemyType
{
    Normal = 0,
    Fast = 1,
    Heavy = 2,
    Ranged = 3
}