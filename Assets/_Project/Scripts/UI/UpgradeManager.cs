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
            upgradePool.Count < UpgradeChoiceCount)
        {
            Debug.LogWarning(
                "升级池至少需要三个升级选项。"
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// 从升级池中随机选择三个不同类型的升级。
    /// 每次打开面板都会重新建立候选列表，
    /// 所以后续升级仍然可以再次抽到相同强化。
    /// </summary>
    private bool SelectRandomUpgradeOptions()
    {
        List<UpgradeOptionData> candidates =
            BuildUniqueUpgradeCandidates();

        if (candidates.Count < UpgradeChoiceCount)
        {
            Debug.LogWarning(
                "升级池中至少需要三个不同类型的有效升级。"
            );

            return false;
        }

        // 使用部分 Fisher-Yates 洗牌。
        // 只需要随机确定前三项，不必打乱整个列表。
        for (int i = 0; i < UpgradeChoiceCount; i++)
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

        Debug.Log(
            "本次随机升级："
            + displayedOptions[0].UpgradeName
            + "、"
            + displayedOptions[1].UpgradeName
            + "、"
            + displayedOptions[2].UpgradeName
        );

        return true;
    }

    /// <summary>
    /// 建立有效候选列表。
    /// 空选项会被忽略。
    /// 相同 UpgradeType 只保留一个，
    /// 防止同一次三选一出现相同强化类型。
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
            if (option == null)
            {
                continue;
            }

            if (addedTypes.Add(option.Type))
            {
                candidates.Add(option);
            }
        }

        return candidates;
    }

    private void RefreshUpgradeButtons()
    {
        for (int i = 0;
             i < UpgradeChoiceCount;
             i++)
        {
            Button button =
                upgradeButtons[i];

            UpgradeOptionData option =
                displayedOptions[i];

            if (button == null)
            {
                continue;
            }

            if (option == null)
            {
                button.interactable = false;

                SetButtonText(
                    button,
                    "无效升级选项"
                );

                continue;
            }

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
                "升级按钮编号超出有效范围。"
            );

            return;
        }

        UpgradeOptionData selectedOption =
            displayedOptions[buttonIndex];

        if (selectedOption == null)
        {
            Debug.LogWarning(
                "当前按钮没有有效的升级数据。"
            );

            return;
        }

        ApplyUpgrade(selectedOption);

        Debug.Log(
            $"选择升级：{selectedOption.UpgradeName}"
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