using UnityEngine;

/// <summary>
/// 单个敌人类型的数据配置。
///
/// EnemyData 不直接保存最终生命值、最终速度和最终伤害，
/// 而是保存敌人类型倍率。
///
/// 最终属性计算规则：
/// 当前全局难度属性 × EnemyData 中的类型倍率。
/// </summary>
[CreateAssetMenu(
    fileName = "Enemy_Data",
    menuName = "AlienPurge/Enemy Data"
)]
public class EnemyData : ScriptableObject
{
    [Header("Enemy Identity")]
    [Tooltip("敌人的类型")]
    [SerializeField]
    private EnemyType enemyType =
        EnemyType.Normal;

    [Tooltip("用于 Inspector、调试输出或未来 UI 显示的名称")]
    [SerializeField]
    private string displayName =
        "Normal Enemy";

    [Header("Attribute Multipliers")]
    [Tooltip("生命值倍率。最终生命值 = 当前全局生命值 × 此倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float healthMultiplier =
        1f;

    [Tooltip("移动速度倍率。最终速度 = 当前全局速度 × 此倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float moveSpeedMultiplier =
        1f;

    [Tooltip("接触伤害倍率。最终伤害 = 当前全局伤害 × 此倍率")]
    [Min(0.01f)]
    [SerializeField]
    private float damageMultiplier =
        1f;

    [Header("Experience Drop")]
    [Tooltip("该类型敌人死亡后掉落的经验值")]
    [Min(1)]
    [SerializeField]
    private int experienceAmount =
        1;

    [Header("Visual Settings")]
    [Tooltip("敌人的整体视觉尺寸倍率。根对象缩放后 Collider 也会同步缩放")]
    [Min(0.1f)]
    [SerializeField]
    private float visualScale =
        1f;

    [Tooltip("敌人的临时显示颜色")]
    [SerializeField]
    private Color visualColor =
        Color.white;

    public EnemyType Type => enemyType;
    public string DisplayName => displayName;

    public float HealthMultiplier =>
        healthMultiplier;

    public float MoveSpeedMultiplier =>
        moveSpeedMultiplier;

    public float DamageMultiplier =>
        damageMultiplier;

    public int ExperienceAmount =>
        experienceAmount;

    public float VisualScale =>
        visualScale;

    public Color VisualColor =>
        visualColor;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = enemyType.ToString();
        }
        else
        {
            displayName = displayName.Trim();
        }

        healthMultiplier =
            Mathf.Max(0.01f, healthMultiplier);

        moveSpeedMultiplier =
            Mathf.Max(0.01f, moveSpeedMultiplier);

        damageMultiplier =
            Mathf.Max(0.01f, damageMultiplier);

        experienceAmount =
            Mathf.Max(1, experienceAmount);

        visualScale =
            Mathf.Max(0.1f, visualScale);
    }
}