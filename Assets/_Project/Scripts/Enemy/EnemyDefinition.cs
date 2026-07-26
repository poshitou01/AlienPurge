using UnityEngine;

/// <summary>
/// 敌人的统一数据入口。
///
/// 每个敌人 Prefab 通过此组件绑定一个 EnemyData。
/// EnemySpawner 只需要向此组件传入当前全局难度属性，
/// 本组件负责计算类型倍率，并把最终数值分发给现有行为组件。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyContactDamage))]
public class EnemyDefinition : MonoBehaviour
{
    [Header("Enemy Data")]
    [Tooltip("该敌人 Prefab 使用的敌人类型数据")]
    [SerializeField] private EnemyData enemyData;

    [Header("Runtime Debug")]
    [Tooltip("本次生成后是否已经完成属性初始化")]
    [SerializeField] private bool hasBeenInitialized;

    [Tooltip("应用类型倍率后的最终最大生命值")]
    [SerializeField] private int finalMaxHealth;

    [Tooltip("应用类型倍率后的最终移动速度")]
    [SerializeField] private float finalMoveSpeed;

    [Tooltip("应用类型倍率后的最终接触伤害")]
    [SerializeField] private int finalContactDamage;

    [Tooltip("该敌人死亡时提供的最终经验值")]
    [SerializeField] private int finalExperienceAmount;

    [Tooltip("该敌人的最终视觉尺寸倍率")]
    [SerializeField] private float finalVisualScale = 1f;

    private EnemyHealth enemyHealth;
    private EnemyMovement enemyMovement;
    private EnemyContactDamage enemyContactDamage;

    private Vector3 originalLocalScale = Vector3.one;

    public EnemyData Data => enemyData;

    public bool HasBeenInitialized =>
        hasBeenInitialized;

    public int FinalMaxHealth =>
        finalMaxHealth;

    public float FinalMoveSpeed =>
        finalMoveSpeed;

    public int FinalContactDamage =>
        finalContactDamage;

    public int FinalExperienceAmount =>
        finalExperienceAmount;

    public float FinalVisualScale =>
        finalVisualScale;

    private void Awake()
    {
        CacheComponents();

        // 记录 Prefab 原始尺寸。
        // 以后所有视觉倍率都基于这个原始尺寸计算，
        // 避免多次初始化时不断累乘缩放。
        originalLocalScale = transform.localScale;

        hasBeenInitialized = false;
    }

    /// <summary>
    /// 根据当前全局难度属性和 EnemyData 类型倍率，
    /// 初始化本次新生成的敌人。
    ///
    /// 已经完成初始化的旧敌人不会因为全局难度变化
    /// 而自动改变属性。
    /// </summary>
    public void InitializeFromGlobalDifficulty(
        int globalMaxHealth,
        float globalMoveSpeed,
        int globalContactDamage
    )
    {
        CacheComponents();

        if (enemyData == null)
        {
            Debug.LogError(
                gameObject.name
                + " 的 EnemyDefinition 没有绑定 EnemyData，"
                + "无法初始化敌人类型属性。",
                this
            );

            hasBeenInitialized = false;
            return;
        }

        // 全局基础属性也进行最低值保护。
        globalMaxHealth =
            Mathf.Max(1, globalMaxHealth);

        globalMoveSpeed =
            Mathf.Max(0.01f, globalMoveSpeed);

        globalContactDamage =
            Mathf.Max(1, globalContactDamage);

        finalMaxHealth =
            RoundToPositiveInt(
                globalMaxHealth
                * enemyData.HealthMultiplier
            );

        finalMoveSpeed =
            Mathf.Max(
                0.01f,
                globalMoveSpeed
                * enemyData.MoveSpeedMultiplier
            );

        finalContactDamage =
            RoundToPositiveInt(
                globalContactDamage
                * enemyData.DamageMultiplier
            );

        finalExperienceAmount =
            Mathf.Max(
                1,
                enemyData.ExperienceAmount
            );

        finalVisualScale =
            Mathf.Max(
                0.1f,
                enemyData.VisualScale
            );

        ApplyCalculatedAttributes();

        hasBeenInitialized = true;

        Debug.Log(
            gameObject.name
            + " 敌人类型初始化完成："
            + " Type="
            + enemyData.Type
            + ", HP="
            + finalMaxHealth
            + ", Speed="
            + finalMoveSpeed.ToString("F2")
            + ", Damage="
            + finalContactDamage
            + ", EXP="
            + finalExperienceAmount
            + ", Scale="
            + finalVisualScale.ToString("F2"),
            this
        );
    }

    /// <summary>
    /// 将计算完成的最终属性分发给现有组件。
    /// </summary>
    private void ApplyCalculatedAttributes()
    {
        enemyHealth.InitializeHealth(
            finalMaxHealth
        );

        enemyHealth.InitializeExperienceAmount(
            finalExperienceAmount
        );

        enemyMovement.InitializeMoveSpeed(
            finalMoveSpeed
        );

        enemyContactDamage.InitializeDamage(
            finalContactDamage
        );

        // 根对象统一缩放后，
        // Sprite 和 Collider 会同步改变世界尺寸。
        transform.localScale =
            originalLocalScale
            * finalVisualScale;

        enemyHealth.InitializeVisualColor(
            enemyData.VisualColor
        );
    }

    /// <summary>
    /// 将浮点属性转换为正整数。
    ///
    /// 使用四舍五入而不是直接截断：
    /// 2.1 转换为 2；
    /// 1.5 转换为 2；
    /// 4.5 转换为 5。
    ///
    /// 最终结果至少为 1。
    /// </summary>
    private int RoundToPositiveInt(float value)
    {
        int roundedValue =
            Mathf.FloorToInt(value + 0.5f);

        return Mathf.Max(1, roundedValue);
    }

    private void CacheComponents()
    {
        if (enemyHealth == null)
        {
            enemyHealth =
                GetComponent<EnemyHealth>();
        }

        if (enemyMovement == null)
        {
            enemyMovement =
                GetComponent<EnemyMovement>();
        }

        if (enemyContactDamage == null)
        {
            enemyContactDamage =
                GetComponent<EnemyContactDamage>();
        }
    }

    private void OnValidate()
    {
        finalMaxHealth =
            Mathf.Max(0, finalMaxHealth);

        finalMoveSpeed =
            Mathf.Max(0f, finalMoveSpeed);

        finalContactDamage =
            Mathf.Max(0, finalContactDamage);

        finalExperienceAmount =
            Mathf.Max(0, finalExperienceAmount);

        finalVisualScale =
            Mathf.Max(0.1f, finalVisualScale);
    }

    [ContextMenu(
        "Test Apply Global Stats (HP 3, Speed 1.5, Damage 1)"
    )]
    private void TestApplyGlobalStats()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请进入 Play 模式后再测试敌人类型初始化。",
                this
            );

            return;
        }

        InitializeFromGlobalDifficulty(
            3,
            1.5f,
            1
        );
    }
}