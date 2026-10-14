using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UpgradeCardView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Card Visual")]
    [SerializeField] private Image cardVisual;

    [Header("Dynamic Text")]
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private TMP_Text categoryTMP;
    [SerializeField] private TMP_Text rarityTMP;
    [SerializeField] private TMP_Text descriptionTMP;
    [SerializeField] private TMP_Text valueTMP;

    private const string ResourceFolder = "LevelUpCards/";

    private Sprite normalSprite;
    private Sprite hoverSprite;
    private Sprite selectedSprite;

    private Button cardButton;

    private bool hasOption;
    private bool isHovered;
    private bool isFocused;

    private void Awake()
    {
        cardButton = GetComponent<Button>();

        if (cardVisual == null)
        {
            Transform child = transform.Find("CardVisual");
            if (child != null)
            {
                cardVisual = child.GetComponent<Image>();
            }
        }

        if (titleTMP == null) titleTMP = FindText("TitleTMP");
        if (categoryTMP == null) categoryTMP = FindText("CategoryTMP");
        if (rarityTMP == null) rarityTMP = FindText("RarityTMP");
        if (descriptionTMP == null) descriptionTMP = FindText("DescriptionTMP");
        if (valueTMP == null) valueTMP = FindText("ValueTMP");

        if (cardVisual != null)
        {
            cardVisual.raycastTarget = false;
            cardVisual.preserveAspect = true;
        }
        else
        {
            Debug.LogError($"{name}: 找不到 CardVisual Image。", this);
        }
    }

    private TMP_Text FindText(string objectName)
    {
        Transform child = transform.Find(objectName);

        if (child == null)
        {
            Debug.LogWarning($"{name}: 找不到 {objectName}。", this);
            return null;
        }

        return child.GetComponent<TMP_Text>();
    }

    // =========================================================
    // 普通升级卡
    // =========================================================

    public void SetOption(UpgradeOptionData option)
    {
        hasOption = false;
        isHovered = false;
        isFocused = false;

        if (option == null)
        {
            if (cardVisual != null)
                cardVisual.enabled = false;

            ClearText();
            return;
        }

        SetupCardSprites(option.Type);
        ApplyBasicTexts(
            GetTitle(option.Type),
            GetCategory(option.Type),
            IsLegendary(option.Type) ? "传奇" : "普通",
            option.Description ?? string.Empty,
            GetValueText(option),
            IsLegendary(option.Type)
        );
    }

    // =========================================================
    // 核心武器模块卡
    // =========================================================

    public void SetModuleCard(UpgradeType moduleType, bool isOverclock)
    {
        hasOption = false;
        isHovered = false;
        isFocused = false;

        SetupCardSprites(moduleType);

        string description = GetModuleDescription(moduleType);
        string value = GetModuleLevelText(moduleType);

        if (!isOverclock)
        {
            // 初次解锁也直接显示等级推进
            value = GetModuleLevelText(moduleType);
        }

        ApplyBasicTexts(
            GetModuleTitle(moduleType),
            "核心机制",
            "传奇",
            description,
            value,
            true
        );
    }

    private void SetupCardSprites(UpgradeType type)
    {
        if (cardVisual == null)
            return;

        string assetName = GetAssetName(type);

        if (string.IsNullOrEmpty(assetName))
        {
            cardVisual.enabled = false;
            Debug.LogWarning($"没有配置卡牌素材：{type}", this);
            return;
        }

        normalSprite = LoadSprite(assetName, "Normal");
        hoverSprite = LoadSprite(assetName, "Hover");
        selectedSprite = LoadSprite(assetName, "Selected");

        hasOption = normalSprite != null;
        cardVisual.enabled = hasOption;

        isFocused =
            EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject;

        RefreshVisual();
    }

    private void ApplyBasicTexts(
        string title,
        string category,
        string rarity,
        string description,
        string value,
        bool legendary)
    {
        if (titleTMP != null)
            titleTMP.text = title;

        if (categoryTMP != null)
            categoryTMP.text = category;

        if (rarityTMP != null)
        {
            rarityTMP.text = rarity;
            rarityTMP.color = legendary
                ? new Color32(212, 171, 88, 255)
                : new Color32(191, 201, 197, 255);
        }

        if (descriptionTMP != null)
            descriptionTMP.text = description;

        if (valueTMP != null)
            valueTMP.text = value;
    }

    private void ClearText()
    {
        if (titleTMP != null) titleTMP.text = "";
        if (categoryTMP != null) categoryTMP.text = "";
        if (rarityTMP != null) rarityTMP.text = "";
        if (descriptionTMP != null) descriptionTMP.text = "";
        if (valueTMP != null) valueTMP.text = "";
    }

    private Sprite LoadSprite(string assetName, string state)
    {
        string path = ResourceFolder + assetName + "_" + state;
        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
        {
            Debug.LogWarning($"升级卡素材加载失败：{path}", this);
        }

        return sprite;
    }

    private static string GetAssetName(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MoveSpeedIncrease:
                return "Common_MoveSpeed";
            case UpgradeType.FireCooldownDecrease:
                return "Common_FireRate";
            case UpgradeType.BulletDamageIncrease:
                return "Common_DamageBoost";
            case UpgradeType.MaxHealthIncrease:
                return "Common_MaxHealth";
            case UpgradeType.HealthRestore:
                return "Common_Heal";
            case UpgradeType.BulletSpeedIncrease:
                return "Common_BulletSpeed";
            case UpgradeType.BulletScaleIncrease:
                return "Common_BulletSize";
            case UpgradeType.ProjectileCountIncrease:
                return "Common_MultiShot";
            case UpgradeType.Piercing:
                return "Legendary_Pierce";
            case UpgradeType.Explosive:
                return "Legendary_Explosion";
            case UpgradeType.ChainLightning:
                return "Legendary_Chain";
            case UpgradeType.SplitShot:
                return "Legendary_Split";
            default:
                return null;
        }
    }

    private static string GetTitle(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MoveSpeedIncrease: return "移动速度";
            case UpgradeType.FireCooldownDecrease: return "射速提升";
            case UpgradeType.BulletDamageIncrease: return "伤害提升";
            case UpgradeType.MaxHealthIncrease: return "生命提升";
            case UpgradeType.HealthRestore: return "生命恢复";
            case UpgradeType.BulletSpeedIncrease: return "子弹速度";
            case UpgradeType.BulletScaleIncrease: return "子弹尺寸";
            case UpgradeType.ProjectileCountIncrease: return "额外弹丸";
            case UpgradeType.Piercing: return "穿透";
            case UpgradeType.Explosive: return "爆炸";
            case UpgradeType.ChainLightning: return "连锁闪电";
            case UpgradeType.SplitShot: return "分裂弹";
            default: return type.ToString();
        }
    }

    private static string GetCategory(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.MoveSpeedIncrease:
                return "机动强化";

            case UpgradeType.MaxHealthIncrease:
            case UpgradeType.HealthRestore:
                return "生存强化";

            case UpgradeType.FireCooldownDecrease:
            case UpgradeType.BulletDamageIncrease:
                return "武器强化";

            default:
                return "弹道强化";
        }
    }

    private static bool IsLegendary(UpgradeType type)
    {
        return type == UpgradeType.Piercing
            || type == UpgradeType.Explosive
            || type == UpgradeType.ChainLightning
            || type == UpgradeType.SplitShot;
    }

    private static string GetValueText(UpgradeOptionData option)
    {
        UpgradeType type = option.Type;
        float amount = option.Value;

        int wholeAmount = Mathf.Max(1, Mathf.RoundToInt(amount));
        string formatted = amount.ToString("0.##");

        const string accent = "#7DE9D4";

        switch (type)
        {
            case UpgradeType.BulletDamageIncrease:
                return $"<color={accent}>+{wholeAmount}</color> 子弹伤害";

            case UpgradeType.FireCooldownDecrease:
                return $"<color={accent}>-{formatted}</color> 秒射击间隔";

            case UpgradeType.MoveSpeedIncrease:
                return $"<color={accent}>+{formatted}</color> 移动速度";

            case UpgradeType.MaxHealthIncrease:
                return $"<color={accent}>+{wholeAmount}</color> 最大生命";

            case UpgradeType.HealthRestore:
                return $"<color={accent}>+{wholeAmount}</color> 恢复生命";

            case UpgradeType.BulletSpeedIncrease:
                return $"<color={accent}>+{formatted}</color> 子弹速度";

            case UpgradeType.BulletScaleIncrease:
                return $"<color={accent}>+{formatted}</color> 子弹尺寸系数";

            case UpgradeType.ProjectileCountIncrease:
                return $"<color={accent}>+{wholeAmount}</color> 弹丸数量";

            case UpgradeType.Piercing:
            case UpgradeType.Explosive:
            case UpgradeType.ChainLightning:
            case UpgradeType.SplitShot:
                return GetModuleLevelText(type);

            default:
                return "";
        }
    }

    private static string GetModuleTitle(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Piercing: return "穿透";
            case UpgradeType.Explosive: return "爆炸";
            case UpgradeType.ChainLightning: return "连锁闪电";
            case UpgradeType.SplitShot: return "分裂弹";
            default: return type.ToString();
        }
    }

    private static string GetModuleDescription(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Piercing:
                return "弹体穿透敌人，继续沿原方向飞行。";

            case UpgradeType.Explosive:
                return "命中目标时产生爆炸，伤害附近敌人。";

            case UpgradeType.ChainLightning:
                return "命中时释放电弧，在敌人之间跳跃。";

            case UpgradeType.SplitShot:
                return "命中后分裂为多发子弹继续攻击。";

            default:
                return "核心机制强化";
        }
    }

    private static string GetModuleLevelText(UpgradeType type)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return "<color=#D4AB58>LV 0 → LV 1</color>";

        PlayerWeaponModifiers modifiers =
            player.GetComponent<PlayerWeaponModifiers>();

        if (modifiers == null)
            return "<color=#D4AB58>LV 0 → LV 1</color>";

        int currentLevel;

        switch (type)
        {
            case UpgradeType.Piercing:
                currentLevel = modifiers.PiercingLevel;
                break;
            case UpgradeType.Explosive:
                currentLevel = modifiers.ExplosiveLevel;
                break;
            case UpgradeType.ChainLightning:
                currentLevel = modifiers.ChainLightningLevel;
                break;
            case UpgradeType.SplitShot:
                currentLevel = modifiers.SplitShotLevel;
                break;
            default:
                currentLevel = 0;
                break;
        }

        return $"<color=#D4AB58>LV {currentLevel} → LV {currentLevel + 1}</color>";
    }

    private void RefreshVisual()
    {
        if (!hasOption || cardVisual == null)
            return;

        Sprite target = normalSprite;

        if (isFocused && selectedSprite != null)
        {
            target = selectedSprite;
        }
        else if (isHovered && hoverSprite != null)
        {
            target = hoverSprite;
        }

        cardVisual.sprite = target;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasOption || (cardButton != null && !cardButton.interactable))
            return;

        isHovered = true;
        RefreshVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshVisual();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isFocused = true;
        RefreshVisual();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isFocused = false;
        RefreshVisual();
    }

    private void OnDisable()
    {
        isHovered = false;
        isFocused = false;
    }
}