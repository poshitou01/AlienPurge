/// <summary>
/// 当前游戏支持的敌人类型。
/// 不同敌人类型共用相同的行为脚本，
/// 仅通过 EnemyData 提供不同的属性倍率和视觉参数。
/// </summary>
public enum EnemyType
{
    Normal = 0,
    Fast = 1,
    Heavy = 2
}