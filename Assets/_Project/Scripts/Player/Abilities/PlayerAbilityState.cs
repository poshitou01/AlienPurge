using UnityEngine;

public enum PlayerAbilityMode
{
    None,
    Dashing,
    Airborne,
    Casting
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerAbilityState : MonoBehaviour
{
    [Header("Runtime Ability State")]
    [SerializeField]
    private PlayerAbilityMode currentMode =
        PlayerAbilityMode.None;

    private PlayerHealth playerHealth;

    /// <summary>
    /// 当前正在使用的特殊能力状态。
    /// 外部脚本只能读取，不能直接修改。
    /// </summary>
    public PlayerAbilityMode CurrentMode =>
        currentMode;

    /// <summary>
    /// 当前是否正在使用任意特殊能力。
    /// </summary>
    public bool IsUsingAbility =>
        currentMode != PlayerAbilityMode.None;

    public bool IsDashing =>
        currentMode == PlayerAbilityMode.Dashing;

    public bool IsAirborne =>
        currentMode == PlayerAbilityMode.Airborne;

    public bool IsCasting =>
        currentMode == PlayerAbilityMode.Casting;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        currentMode = PlayerAbilityMode.None;
    }

    private void Update()
    {
        if (playerHealth != null
            && playerHealth.IsDead
            && currentMode != PlayerAbilityMode.None)
        {
            ForceReset();
        }
    }

    /// <summary>
    /// 判断当前游戏环境是否允许玩家使用特殊能力。
    /// 这里只检查公共游戏状态，不检查具体能力的冷却。
    /// </summary>
    public bool IsGameplayAbilityAllowed()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (GameManager.Instance == null)
        {
            return false;
        }

        if (!GameManager.Instance.IsPlaying)
        {
            return false;
        }

        if (PauseMenuController.IsPaused)
        {
            return false;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return false;
        }

        if (playerHealth == null)
        {
            return false;
        }

        if (playerHealth.IsDead)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断当前是否可以开始一个新的特殊能力。
    /// </summary>
    public bool CanAcceptAbilityInput()
    {
        return IsGameplayAbilityAllowed()
            && currentMode == PlayerAbilityMode.None;
    }

    /// <summary>
    /// 尝试进入指定能力状态。
    /// 成功进入时返回 true，否则返回 false。
    /// </summary>
    public bool TryEnterAbility(
        PlayerAbilityMode requestedMode
    )
    {
        if (requestedMode == PlayerAbilityMode.None)
        {
            return false;
        }

        if (!CanAcceptAbilityInput())
        {
            return false;
        }

        currentMode = requestedMode;

        return true;
    }

    /// <summary>
    /// 只有当前状态与 expectedMode 相同时，
    /// 才允许退出对应能力。
    /// </summary>
    public bool ExitAbility(
        PlayerAbilityMode expectedMode
    )
    {
        if (expectedMode == PlayerAbilityMode.None)
        {
            return false;
        }

        if (currentMode != expectedMode)
        {
            return false;
        }

        currentMode = PlayerAbilityMode.None;

        return true;
    }

    /// <summary>
    /// 强制清除当前能力状态。
    /// 用于玩家死亡、组件禁用或异常中断。
    /// </summary>
    public void ForceReset()
    {
        currentMode = PlayerAbilityMode.None;
    }

    private void OnDisable()
    {
        ForceReset();
    }
}