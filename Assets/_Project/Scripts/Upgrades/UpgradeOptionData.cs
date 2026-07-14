using System;
using UnityEngine;

/// <summary>
/// 当前游戏支持的基础升级类型。
/// 后续 UpgradeManager 会根据这个类型判断应该强化哪一项属性。
/// </summary>
public enum UpgradeType
{
    MoveSpeedIncrease,
    FireCooldownDecrease,
    BulletDamageIncrease,
    MaxHealthIncrease,
    HealthRestore
}

/// <summary>
/// 一个完整升级选项所包含的数据。
/// Serializable 让这个普通 C# 类可以显示在 Unity Inspector 中。
/// </summary>
[Serializable]
public class UpgradeOptionData
{
    [Tooltip("这个升级选项对应的强化类型")]
    [SerializeField] private UpgradeType upgradeType;

    [Tooltip("显示在升级按钮上的升级名称")]
    [SerializeField] private string upgradeName = "新升级";

    [Tooltip("显示在升级按钮上的效果说明")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "升级效果说明";

    [Tooltip("本次升级所增加或减少的数值")]
    [Min(0f)]
    [SerializeField] private float value = 1f;

    public UpgradeType Type => upgradeType;
    public string UpgradeName => upgradeName;
    public string Description => description;
    public float Value => value;

    /// <summary>
    /// 返回适合直接显示在按钮上的文本。
    /// 第一行为升级名称，第二行为效果说明。
    /// </summary>
    public string GetDisplayText()
    {
        return upgradeName + "\n" + description;
    }
}