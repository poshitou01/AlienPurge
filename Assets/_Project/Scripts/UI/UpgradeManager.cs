using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public static bool IsChoosingUpgrade { get; private set; }

    private const int UpgradeChoiceCount = 3;

    [Header("UI")]
    [SerializeField] private GameObject upgradePanel;

    [Tooltip("依次放入三个升级按钮")]
    [SerializeField]
    private Button[] upgradeButtons =
        new Button[UpgradeChoiceCount];

    [Header("Upgrade Pool")]
    [Tooltip("游戏中当前可出现的全部升级选项")]
    [SerializeField]
    private List<UpgradeOptionData> upgradePool =
        new List<UpgradeOptionData>();

    private readonly UpgradeOptionData[] displayedOptions =
        new UpgradeOptionData[UpgradeChoiceCount];

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(false);
        }

        IsChoosingUpgrade = false;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        FindPlayerComponents();
        SetupButtons();
    }

    private void FindPlayerComponents()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning(
                "UpgradeManager 找不到 Tag 为 Player 的对象。"
                + "请确认 Player 的 Tag 是 Player。"
            );

            return;
        }

        playerMovement =
            player.GetComponent<PlayerMovement>();

        playerShooting =
            player.GetComponent<PlayerShooting>();

        playerHealth =
            player.GetComponent<PlayerHealth>();

        if (playerMovement == null)
        {
            Debug.LogWarning(
                "UpgradeManager 找不到 PlayerMovement。"
            );
        }

        if (playerShooting == null)
        {
            Debug.LogWarning(
                "UpgradeManager 找不到 PlayerShooting。"
            );
        }

        if (playerHealth == null)
        {
            Debug.LogWarning(
                "UpgradeManager 找不到 PlayerHealth。"
            );
        }
    }

    private void SetupButtons()
    {
        if (upgradeButtons == null ||
            upgradeButtons.Length != UpgradeChoiceCount)
        {
            Debug.LogWarning(
                "Upgrade Buttons 数组必须正好包含三个按钮。"
            );

            return;
        }

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            Button button = upgradeButtons[i];

            if (button == null)
            {
                Debug.LogWarning(
                    $"Upgrade Button {i + 1} 没有赋值。"
                );

                continue;
            }

            int capturedIndex = i;

            button.onClick.RemoveAllListeners();

            button.onClick.AddListener(
                () => ChooseUpgrade(capturedIndex)
            );
        }
    }

    public void ShowUpgradePanel()
    {
        if (upgradePanel == null)
        {
            Debug.LogWarning(
                "UpgradePanel 没有赋值。"
            );

            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState !=
            GameState.Playing)
        {
            return;
        }

        if (IsChoosingUpgrade)
        {
            return;
        }

        if (!ValidateUpgradeSettings())
        {
            return;
        }

        if (playerMovement == null ||
            playerShooting == null ||
            playerHealth == null)
        {
            FindPlayerComponents();
        }

        if (!SelectRandomUpgradeOptions())
        {
            return;
        }

        RefreshUpgradeButtons();

        IsChoosingUpgrade = true;
        upgradePanel.SetActive(true);
        Time.timeScale = 0f;

        Debug.Log(
            "玩家升级，游戏暂停，显示随机升级选项。"
        );
    }

    private bool ValidateUpgradeSettings()
    {
        if (upgradeButtons == null ||
            upgradeButtons.Length != UpgradeChoiceCount)
        {
            Debug.LogWarning(
                "Upgrade Buttons 数组必须正好包含三个按钮。"
            );

            return false;
        }

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            if (upgradeButtons[i] == null)
            {
                Debug.LogWarning(
                    $"Upgrade Button {i + 1} 没有赋值。"
                );

                return false;
            }
        }

        if (upgradePool == null ||
     upgradePool.Count == 0)
        {
            Debug.LogWarning(
                "Upgrade Pool 为空，无法生成升级选项。",
                this
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// 从当前有效候选中随机选择最多三个不同类型的升级。
    /// 候选不足三个时，只使用实际存在的候选。
    /// 候选为零时安全返回，不打开升级面板。
    /// </summary>
    private bool SelectRandomUpgradeOptions()
    {
        // 防止上一次显示的数据残留。
        ClearDisplayedOptions();

        List<UpgradeOptionData> candidates =
            BuildUniqueUpgradeCandidates();

        if (candidates.Count == 0)
        {
            Debug.LogWarning(
                "当前没有任何可以继续生效的升级选项，"
                + "本次不会打开升级面板。",
                this
            );

            return false;
        }

        int selectedCount = Mathf.Min(
            UpgradeChoiceCount,
            candidates.Count
        );

        // 使用部分 Fisher-Yates 洗牌。
        // 实际有几个候选，就随机选择几个。
        for (int i = 0; i < selectedCount; i++)
        {
            int randomIndex =
                Random.Range(i, candidates.Count);

            UpgradeOptionData temporaryOption =
                candidates[i];

            candidates[i] =
                candidates[randomIndex];

            candidates[randomIndex] =
                temporaryOption;

            displayedOptions[i] =
                candidates[i];
        }

        string selectedUpgradeNames = "";

        for (int i = 0; i < selectedCount; i++)
        {
            if (i > 0)
            {
                selectedUpgradeNames += "、";
            }

            selectedUpgradeNames +=
                displayedOptions[i].UpgradeName;
        }

        Debug.Log(
            $"本次可用候选数量：{candidates.Count}，"
            + $"实际显示数量：{selectedCount}。\n"
            + $"本次随机升级：{selectedUpgradeNames}",
            this
        );

        return true;
    }

    /// <summary>
    /// 根据玩家当前状态建立有效升级候选列表。
    /// 空选项和当前无法生效的升级会被忽略。
    /// 相同 UpgradeType 只保留一个，
    /// 防止同一次升级出现相同强化类型。
    /// </summary>
    private List<UpgradeOptionData>
        BuildUniqueUpgradeCandidates()
    {
        List<UpgradeOptionData> candidates =
            new List<UpgradeOptionData>();

        HashSet<UpgradeType> addedTypes =
            new HashSet<UpgradeType>();

        foreach (UpgradeOptionData option in upgradePool)
        {
            // 忽略空选项。
            if (option == null)
            {
                continue;
            }

            // 忽略当前已经无法产生实际收益的升级。
            if (!IsUpgradeAvailable(option))
            {
                continue;
            }

            // 同一次升级中，相同类型只加入一次。
            if (!addedTypes.Add(option.Type))
            {
                continue;
            }

            candidates.Add(option);
        }

        return candidates;
    }
    /// <summary>
    /// 判断一个升级选项在玩家当前状态下是否仍然能够生效。
    /// 此方法目前只负责判断，暂时不直接修改随机抽取流程。
    /// </summary>
    private bool IsUpgradeAvailable(
        UpgradeOptionData option)
    {
        if (option == null)
        {
            return false;
        }

        switch (option.Type)
        {
            // 当前没有设置移动速度上限，可以重复获得。
            case UpgradeType.MoveSpeedIncrease:
                return playerMovement != null;

            // 射击冷却达到最低值后不可继续强化。
            case UpgradeType.FireCooldownDecrease:
                return playerShooting != null
                    && playerShooting.CanReduceFireCooldown;

            // 当前没有设置子弹伤害上限，可以重复获得。
            case UpgradeType.BulletDamageIncrease:
                return playerShooting != null;

            // 当前没有设置最大生命值上限，可以重复获得。
            case UpgradeType.MaxHealthIncrease:
                return playerHealth != null
                    && !playerHealth.IsDead;

            // 只有玩家存活且未满血时，生命恢复才有效。
            case UpgradeType.HealthRestore:
                return playerHealth != null
                    && playerHealth.CanRestoreHealth;

            // 子弹速度达到上限后不可继续强化。
            case UpgradeType.BulletSpeedIncrease:
                return playerShooting != null
                    && playerShooting.CanIncreaseBulletSpeed;

            // 子弹尺寸达到上限后不可继续强化。
            case UpgradeType.BulletScaleIncrease:
                return playerShooting != null
                    && playerShooting.CanIncreaseBulletScale;

            // 弹丸数量达到上限后不可继续强化。
            case UpgradeType.ProjectileCountIncrease:
                return playerShooting != null
                    && playerShooting.CanIncreaseProjectileCount;

            default:
                Debug.LogWarning(
                    $"无法判断升级类型是否有效：{option.Type}",
                    this
                );

                return false;
        }
    }
    /// <summary>
    /// 根据实际选中的升级数量刷新按钮。
    /// 没有对应升级数据的按钮会被隐藏。
    /// 后续候选重新增加时，按钮会自动重新显示。
    /// </summary>
    private void RefreshUpgradeButtons()
    {
        for (int i = 0;
             i < UpgradeChoiceCount;
             i++)
        {
            Button button =
                upgradeButtons[i];

            if (button == null)
            {
                continue;
            }

            UpgradeOptionData option =
                displayedOptions[i];

            if (option == null)
            {
                button.interactable = false;
                button.gameObject.SetActive(false);
                continue;
            }

            // 上一次可能因为候选不足隐藏过按钮，
            // 所以有数据时必须重新启用。
            button.gameObject.SetActive(true);
            button.interactable = true;

            SetButtonText(
                button,
                option.GetDisplayText()
            );
        }
    }

    private void SetButtonText(
        Button button,
        string displayText)
    {
        TMP_Text tmpText =
            button.GetComponentInChildren<TMP_Text>(true);

        if (tmpText != null)
        {
            tmpText.text = displayText;
            return;
        }

        Text legacyText =
            button.GetComponentInChildren<Text>(true);

        if (legacyText != null)
        {
            legacyText.text = displayText;
            return;
        }

        Debug.LogWarning(
            $"{button.name} 下没有找到文字组件。"
        );
    }

    /// <summary>
    /// 处理玩家点击升级按钮。
    /// 在真正应用升级前，再次检查该升级是否仍然有效。
    /// </summary>
    private void ChooseUpgrade(int buttonIndex)
    {
        if (!IsChoosingUpgrade)
        {
            return;
        }

        if (buttonIndex < 0 ||
            buttonIndex >= displayedOptions.Length)
        {
            Debug.LogWarning(
                "升级按钮编号超出有效范围。",
                this
            );

            return;
        }

        UpgradeOptionData selectedOption =
            displayedOptions[buttonIndex];

        if (selectedOption == null)
        {
            Debug.LogWarning(
                "当前按钮没有有效的升级数据。",
                this
            );

            return;
        }

        // 二次检查：
        // 防止面板打开后，玩家状态发生变化，
        // 导致原本有效的升级在点击时已经无法产生收益。
        if (!IsUpgradeAvailable(selectedOption))
        {
            Debug.LogWarning(
                $"升级选项“{selectedOption.UpgradeName}”"
                + "在点击时已经失效，"
                + "正在重新生成可用升级。",
                this
            );

            // 尝试使用玩家当前状态重新生成候选。
            if (SelectRandomUpgradeOptions())
            {
                RefreshUpgradeButtons();

                Debug.Log(
                    "升级选项已经根据当前玩家状态刷新。",
                    this
                );

                return;
            }

            // 如果已经没有任何有效候选，
            // 则安全关闭升级面板，避免玩家一直停留在暂停状态。
            Debug.LogWarning(
                "当前已经没有可以生效的升级选项，"
                + "升级面板将被关闭。",
                this
            );

            CloseUpgradePanel();
            return;
        }

        ApplyUpgrade(selectedOption);

        Debug.Log(
            $"选择升级：{selectedOption.UpgradeName}",
            this
        );

        CloseUpgradePanel();
    }

    private void ApplyUpgrade(
        UpgradeOptionData option)
    {
        switch (option.Type)
        {
            case UpgradeType.MoveSpeedIncrease:
                ApplyMoveSpeedUpgrade(option.Value);
                break;

            case UpgradeType.FireCooldownDecrease:
                ApplyFireCooldownUpgrade(option.Value);
                break;

            case UpgradeType.BulletDamageIncrease:
                ApplyBulletDamageUpgrade(option.Value);
                break;

            case UpgradeType.MaxHealthIncrease:
                ApplyMaxHealthUpgrade(option.Value);
                break;

            case UpgradeType.HealthRestore:
                ApplyHealthRestoreUpgrade(option.Value);
                break;

            case UpgradeType.BulletSpeedIncrease:
                ApplyBulletSpeedUpgrade(option.Value);
                break;

            case UpgradeType.BulletScaleIncrease:
                ApplyBulletScaleUpgrade(option.Value);
                break;

            case UpgradeType.ProjectileCountIncrease:
                ApplyProjectileCountUpgrade(option.Value);
                break;

            default:
                Debug.LogWarning(
                    $"未处理的升级类型：{option.Type}"
                );
                break;
        }
    }

    private void ApplyMoveSpeedUpgrade(float amount)
    {
        if (playerMovement == null)
        {
            Debug.LogWarning(
                "无法应用移动速度强化："
                + "PlayerMovement 为空。"
            );

            return;
        }

        playerMovement.AddMoveSpeed(amount);
    }

    private void ApplyFireCooldownUpgrade(float amount)
    {
        if (playerShooting == null)
        {
            Debug.LogWarning(
                "无法应用射击冷却强化："
                + "PlayerShooting 为空。"
            );

            return;
        }

        playerShooting.ReduceFireCooldown(amount);
    }

    private void ApplyBulletDamageUpgrade(float amount)
    {
        if (playerShooting == null)
        {
            Debug.LogWarning(
                "无法应用子弹伤害强化："
                + "PlayerShooting 为空。"
            );

            return;
        }

        int integerAmount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(amount)
            );

        playerShooting.AddBulletDamage(
            integerAmount
        );
    }

    private void ApplyBulletSpeedUpgrade(float amount)
    {
        if (playerShooting == null)
        {
            Debug.LogWarning(
                "无法应用子弹速度强化："
                + "PlayerShooting 为空。"
            );

            return;
        }

        playerShooting.AddBulletSpeed(amount);
    }

    private void ApplyBulletScaleUpgrade(float amount)
    {
        if (playerShooting == null)
        {
            Debug.LogWarning(
                "无法应用子弹尺寸强化："
                + "PlayerShooting 为空。"
            );

            return;
        }

        playerShooting.AddBulletScaleMultiplier(
            amount
        );
    }

    private void ApplyProjectileCountUpgrade(float amount)
    {
        if (playerShooting == null)
        {
            Debug.LogWarning(
                "无法应用额外弹丸强化："
                + "PlayerShooting 为空。"
            );

            return;
        }

        int integerAmount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(amount)
            );

        playerShooting.AddProjectileCount(
            integerAmount
        );
    }

    private void ApplyMaxHealthUpgrade(float amount)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning(
                "无法应用最大生命值强化："
                + "PlayerHealth 为空。"
            );

            return;
        }

        int integerAmount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(amount)
            );

        playerHealth.IncreaseMaxHealth(
            integerAmount
        );
    }

    private void ApplyHealthRestoreUpgrade(float amount)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning(
                "无法应用生命恢复强化："
                + "PlayerHealth 为空。"
            );

            return;
        }

        int integerAmount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(amount)
            );

        playerHealth.RestoreHealth(
            integerAmount
        );
    }

    private void CloseUpgradePanel()
    {
        if (upgradePanel != null)
        {
            upgradePanel.SetActive(false);
        }

        ClearDisplayedOptions();

        IsChoosingUpgrade = false;

        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentState ==
            GameState.Playing)
        {
            Time.timeScale = 1f;
        }

        Debug.Log(
            "升级选择完成，游戏恢复。"
        );
    }

    private void ClearDisplayedOptions()
    {
        for (int i = 0;
             i < displayedOptions.Length;
             i++)
        {
            displayedOptions[i] = null;
        }
    }
    /// <summary>
    /// 在 Console 中输出升级池内所有选项当前是否有效。
    /// 只用于第二十阶段调试。
    /// </summary>
    [ContextMenu("Debug/Print Upgrade Availability")]
    private void PrintUpgradeAvailability()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "请先进入 Play Mode，"
                + "再检查升级有效性。",
                this
            );

            return;
        }

        if (playerMovement == null ||
            playerShooting == null ||
            playerHealth == null)
        {
            FindPlayerComponents();
        }

        if (upgradePool == null ||
            upgradePool.Count == 0)
        {
            Debug.LogWarning(
                "Upgrade Pool 为空，"
                + "没有可以检查的升级选项。",
                this
            );

            return;
        }

        string report =
            "===== Upgrade Availability =====\n";

        for (int i = 0; i < upgradePool.Count; i++)
        {
            UpgradeOptionData option = upgradePool[i];

            if (option == null)
            {
                report +=
                    $"[{i}] Null Option: False\n";

                continue;
            }

            report +=
                $"[{i}] "
                + $"{option.UpgradeName} "
                + $"({option.Type}): "
                + $"{IsUpgradeAvailable(option)}\n";
        }

        Debug.Log(report, this);
    }
    [ContextMenu("Test Show Upgrade Panel")]
    private void TestShowUpgradePanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "升级面板测试只能在 Play 模式中执行。"
            );

            return;
        }

        ShowUpgradePanel();
    }
}