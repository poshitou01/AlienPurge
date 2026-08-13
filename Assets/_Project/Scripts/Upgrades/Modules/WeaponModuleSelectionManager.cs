using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WeaponModuleSelectionManager : MonoBehaviour
{
    public static WeaponModuleSelectionManager Instance
    {
        get;
        private set;
    }

    public static bool IsChoosingModule
    {
        get;
        private set;
    }

    private enum ModuleSelectionMode
    {
        None,
        Unlock,
        Overclock
    }

    private const int ModuleChoiceCount = 3;


    // =========================================================
    // Rules
    // =========================================================

    [Header("Module Rules")]

    [Tooltip("一局最多允许拥有多少种不同核心模块")]
    [SerializeField]
    private int maxModuleSlots = 2;


    // =========================================================
    // Timed Selection
    // =========================================================

    [Header("Timed Module Selection")]

    [Tooltip("第一次核心模块选择出现的生存时间")]
    [SerializeField]
    private float firstModuleSelectionTime = 45f;

    [Tooltip("第二次核心模块选择出现的生存时间")]
    [SerializeField]
    private float secondModuleSelectionTime = 150f;


    // =========================================================
    // UI
    // =========================================================

    [Header("Module Selection UI")]

    [SerializeField]
    private GameObject moduleSelectionPanel;

    [Tooltip("依次放入三个核心模块按钮")]
    [SerializeField]
    private Button[] moduleButtons =
        new Button[ModuleChoiceCount];


    // =========================================================
    // Player
    // =========================================================

    [Header("Player Reference")]

    [SerializeField]
    private PlayerWeaponModifiers playerWeaponModifiers;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Overclock Settings")]

    [Tooltip("核心模块超频强化出现的生存时间")]
    [SerializeField]
    private float overclockSelectionTime = 240f;


    [Header("Overclock Runtime Debug")]

    [SerializeField]
    private bool overclockSelectionTriggered;

    [SerializeField]
    private ModuleSelectionMode currentSelectionMode =
        ModuleSelectionMode.None;

    [Header("Runtime Debug")]

    [SerializeField]
    private int unlockedModuleCount;

    [SerializeField]
    private bool canUnlockNewModule;

    [SerializeField]
    private bool firstSelectionTriggered;

    [SerializeField]
    private bool secondSelectionTriggered;

    [SerializeField]
    private int currentSelectionIndex;

    [SerializeField]
    private float lastTriggeredSurvivalTime;

    [SerializeField]
    private string displayedModule1 = "None";

    [SerializeField]
    private string displayedModule2 = "None";

    [SerializeField]
    private string displayedModule3 = "None";


    private readonly UpgradeType[] displayedModules =
        new UpgradeType[ModuleChoiceCount];

    private readonly bool[] displayedModuleValid =
        new bool[ModuleChoiceCount];


    public int MaxModuleSlots =>
        maxModuleSlots;

    public int UnlockedModuleCount =>
        GetUnlockedModuleCount();

    public bool CanUnlockNewModule =>
        GetUnlockedModuleCount()
        < maxModuleSlots;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        IsChoosingModule = false;

        firstSelectionTriggered = false;
        secondSelectionTriggered = false;

        overclockSelectionTriggered = false;

        currentSelectionMode =
            ModuleSelectionMode.None;

        currentSelectionIndex = 0;
        lastTriggeredSurvivalTime = 0f;

        ClearDisplayedModules();

        if (moduleSelectionPanel != null)
        {
            moduleSelectionPanel.SetActive(false);
        }
    }


    private void Start()
    {
        FindPlayerWeaponModifiers();

        SetupButtons();

        RefreshDebugState();
    }


    private void Update()
    {
        RefreshDebugState();

        CheckTimedModuleSelections();
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;

            IsChoosingModule = false;
        }
    }


    // =========================================================
    // Timed Selection
    // =========================================================

    private void CheckTimedModuleSelections()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        if (GameManager.Instance.CurrentState
            != GameState.Playing)
        {
            return;
        }

        if (IsChoosingModule)
        {
            return;
        }

        if (UpgradeManager.IsChoosingUpgrade)
        {
            return;
        }

        float survivalTime =
            GameManager.Instance.SurvivalTime;


        // ==========================================
        // 240s Overclock
        // ==========================================

        if (!overclockSelectionTriggered
            && survivalTime
            >= overclockSelectionTime)
        {
            BeginOverclockSelection(
                survivalTime
            );

            return;
        }


        // ==========================================
        // 150s 第二核心模块
        // ==========================================

        if (!secondSelectionTriggered
            && survivalTime
            >= secondModuleSelectionTime)
        {
            BeginTimedModuleSelection(
                2,
                survivalTime
            );

            return;
        }


        // ==========================================
        // 45s 第一核心模块
        // ==========================================

        if (!firstSelectionTriggered
            && survivalTime
            >= firstModuleSelectionTime)
        {
            BeginTimedModuleSelection(
                1,
                survivalTime
            );
        }
    }


    private void BeginTimedModuleSelection(
        int selectionIndex,
        float survivalTime)
    {
        if (IsChoosingModule)
        {
            return;
        }

        if (!CanUnlockNewModule)
        {
            return;
        }

        if (selectionIndex == 1)
        {
            if (firstSelectionTriggered)
            {
                return;
            }

            firstSelectionTriggered = true;
        }
        else if (selectionIndex == 2)
        {
            if (secondSelectionTriggered)
            {
                return;
            }

            secondSelectionTriggered = true;
        }
        else
        {
            Debug.LogWarning(
                "WeaponModuleSelectionManager: "
                + "无效的模块选择节点编号："
                + selectionIndex,
                this
            );

            return;
        }


        if (!SelectModuleOptions())
        {
            Debug.LogWarning(
                "WeaponModuleSelectionManager: "
                + "没有可供选择的新核心模块。",
                this
            );


            return;
        }
        currentSelectionMode =
    ModuleSelectionMode.Unlock;


        currentSelectionIndex =
            selectionIndex;

        lastTriggeredSurvivalTime =
            survivalTime;

        IsChoosingModule = true;

        RefreshModuleButtons();

        if (moduleSelectionPanel != null)
        {
            moduleSelectionPanel.SetActive(true);
        }

        Time.timeScale = 0f;


        Debug.Log(
            "===== Weapon Module Selection =====\n"
            + "Selection Index: "
            + currentSelectionIndex
            + "\nTriggered At: "
            + survivalTime.ToString("F2")
            + "s\n"
            + "Option 1: "
            + displayedModule1
            + "\nOption 2: "
            + displayedModule2
            + "\nOption 3: "
            + displayedModule3,
            this
        );
    }

    private void BeginOverclockSelection(
    float survivalTime)
    {
        if (overclockSelectionTriggered)
        {
            return;
        }

        overclockSelectionTriggered = true;


        if (!SelectOverclockOptions())
        {
            Debug.Log(
                "240s Overclock 已到达，"
                + "但当前所有核心模块都已经满级，"
                + "本次节点自动跳过。",
                this
            );

            return;
        }


        currentSelectionMode =
            ModuleSelectionMode.Overclock;

        currentSelectionIndex = 3;

        lastTriggeredSurvivalTime =
            survivalTime;

        IsChoosingModule = true;


        RefreshModuleButtons();


        if (moduleSelectionPanel != null)
        {
            moduleSelectionPanel
                .SetActive(true);
        }


        Time.timeScale = 0f;


        Debug.Log(
            "===== Weapon Module Overclock =====\n"
            + "Triggered At: "
            + survivalTime.ToString("F2")
            + "s\n"
            + "Option 1: "
            + displayedModule1
            + "\nOption 2: "
            + displayedModule2
            + "\nOption 3: "
            + displayedModule3,
            this
        );
    }


    private bool SelectOverclockOptions()
    {
        ClearDisplayedModules();


        List<UpgradeType> candidates =
            new List<UpgradeType>();


        TryAddOverclockCandidate(
            candidates,
            UpgradeType.Piercing
        );

        TryAddOverclockCandidate(
            candidates,
            UpgradeType.Explosive
        );

        TryAddOverclockCandidate(
            candidates,
            UpgradeType.ChainLightning
        );

        TryAddOverclockCandidate(
            candidates,
            UpgradeType.SplitShot
        );


        if (candidates.Count == 0)
        {
            return false;
        }


        int selectedCount =
            Mathf.Min(
                ModuleChoiceCount,
                candidates.Count
            );


        for (int i = 0;
             i < selectedCount;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    candidates.Count
                );


            UpgradeType temporary =
                candidates[i];

            candidates[i] =
                candidates[randomIndex];

            candidates[randomIndex] =
                temporary;


            displayedModules[i] =
                candidates[i];

            displayedModuleValid[i] =
                true;
        }


        RefreshDisplayedModuleDebug();

        return true;
    }


    private void TryAddOverclockCandidate(
        List<UpgradeType> candidates,
        UpgradeType upgradeType)
    {
        if (!CanOverclockModule(
                upgradeType))
        {
            return;
        }

        candidates.Add(
            upgradeType
        );
    }

    private bool CanOverclockModule(
    UpgradeType upgradeType)
    {
        EnsurePlayerReference();


        if (playerWeaponModifiers == null)
        {
            return false;
        }


        // 没获得过的模块绝对不能 Overclock。
        if (!IsModuleUnlocked(
                upgradeType))
        {
            return false;
        }


        switch (upgradeType)
        {
            case UpgradeType.Piercing:

                return playerWeaponModifiers
                    .CanUpgradePiercing;


            case UpgradeType.Explosive:

                return playerWeaponModifiers
                    .CanUpgradeExplosive;


            case UpgradeType.ChainLightning:

                return playerWeaponModifiers
                    .CanUpgradeChainLightning;


            case UpgradeType.SplitShot:

                return playerWeaponModifiers
                    .CanUpgradeSplitShot;


            default:
                return false;
        }
    }
    // =========================================================
    // Candidate Selection
    // =========================================================

    private bool SelectModuleOptions()
    {
        ClearDisplayedModules();

        List<UpgradeType> candidates =
            BuildModuleCandidates();

        if (candidates.Count == 0)
        {
            return false;
        }

        int selectedCount =
            Mathf.Min(
                ModuleChoiceCount,
                candidates.Count
            );


        // Partial Fisher-Yates Shuffle
        for (int i = 0;
             i < selectedCount;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    candidates.Count
                );

            UpgradeType temporary =
                candidates[i];

            candidates[i] =
                candidates[randomIndex];

            candidates[randomIndex] =
                temporary;


            displayedModules[i] =
                candidates[i];

            displayedModuleValid[i] =
                true;
        }

        RefreshDisplayedModuleDebug();

        return true;
    }


    private List<UpgradeType>
        BuildModuleCandidates()
    {
        List<UpgradeType> candidates =
            new List<UpgradeType>();


        TryAddModuleCandidate(
            candidates,
            UpgradeType.Piercing
        );

        TryAddModuleCandidate(
            candidates,
            UpgradeType.Explosive
        );

        TryAddModuleCandidate(
            candidates,
            UpgradeType.ChainLightning
        );

        TryAddModuleCandidate(
            candidates,
            UpgradeType.SplitShot
        );


        return candidates;
    }


    private void TryAddModuleCandidate(
        List<UpgradeType> candidates,
        UpgradeType upgradeType)
    {
        if (!CanUnlockModule(upgradeType))
        {
            return;
        }

        candidates.Add(upgradeType);
    }


    // =========================================================
    // UI
    // =========================================================

    private void SetupButtons()
    {
        if (moduleButtons == null ||
            moduleButtons.Length
            != ModuleChoiceCount)
        {
            Debug.LogWarning(
                "Weapon Module Buttons "
                + "数组必须正好包含三个按钮。",
                this
            );

            return;
        }


        for (int i = 0;
             i < moduleButtons.Length;
             i++)
        {
            Button button =
                moduleButtons[i];

            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;

            button.onClick.RemoveAllListeners();

            button.onClick.AddListener(
                () =>
                    ChooseModule(
                        capturedIndex
                    )
            );
        }
    }


    private void RefreshModuleButtons()
    {
        if (moduleButtons == null)
        {
            return;
        }

        for (int i = 0;
             i < ModuleChoiceCount;
             i++)
        {
            Button button =
                moduleButtons[i];

            if (button == null)
            {
                continue;
            }


            if (!displayedModuleValid[i])
            {
                button.gameObject
                    .SetActive(false);

                button.interactable =
                    false;

                continue;
            }


            button.gameObject
                .SetActive(true);

            button.interactable =
                true;


            SetButtonText(
                button,
                GetModuleDisplayText(
                    displayedModules[i]
                )
            );
        }
    }


    private void SetButtonText(
        Button button,
        string displayText)
    {
        TMP_Text tmpText =
            button.GetComponentInChildren<
                TMP_Text
            >(true);

        if (tmpText != null)
        {
            tmpText.text =
                displayText;

            return;
        }


        Text legacyText =
            button.GetComponentInChildren<
                Text
            >(true);

        if (legacyText != null)
        {
            legacyText.text =
                displayText;

            return;
        }


        Debug.LogWarning(
            button.name
            + " 下没有找到文字组件。",
            button
        );
    }


    private string GetModuleDisplayText(
    UpgradeType upgradeType)
    {
        if (currentSelectionMode
            == ModuleSelectionMode.Overclock)
        {
            return GetOverclockDisplayText(
                upgradeType
            );
        }


        switch (upgradeType)
        {
            case UpgradeType.Piercing:
                return
                    "穿透弹头\n"
                    + "子弹可额外穿透 1 名敌人";


            case UpgradeType.Explosive:
                return
                    "爆裂弹头\n"
                    + "命中后产生范围爆炸";


            case UpgradeType.ChainLightning:
                return
                    "电弧核心\n"
                    + "命中后连锁附近敌人";


            case UpgradeType.SplitShot:
                return
                    "裂变模块\n"
                    + "命中后产生 2 枚裂变弹";


            default:
                return
                    "Unknown Module";
        }
    }


    private string GetOverclockDisplayText(
    UpgradeType upgradeType)
    {
        int currentLevel =
            GetModuleLevel(
                upgradeType
            );


        int nextLevel =
            Mathf.Min(
                3,
                currentLevel + 1
            );


        string moduleName =
            GetModuleName(
                upgradeType
            );


        return
            moduleName
            + "\nLv"
            + currentLevel
            + " → Lv"
            + nextLevel;
    }

    private int GetModuleLevel(
    UpgradeType upgradeType)
    {
        EnsurePlayerReference();


        if (playerWeaponModifiers == null)
        {
            return 0;
        }


        switch (upgradeType)
        {
            case UpgradeType.Piercing:
                return playerWeaponModifiers
                    .PiercingLevel;


            case UpgradeType.Explosive:
                return playerWeaponModifiers
                    .ExplosiveLevel;


            case UpgradeType.ChainLightning:
                return playerWeaponModifiers
                    .ChainLightningLevel;


            case UpgradeType.SplitShot:
                return playerWeaponModifiers
                    .SplitShotLevel;


            default:
                return 0;
        }
    }


    private string GetModuleName(
        UpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case UpgradeType.Piercing:
                return "穿透弹头";

            case UpgradeType.Explosive:
                return "爆裂弹头";

            case UpgradeType.ChainLightning:
                return "电弧核心";

            case UpgradeType.SplitShot:
                return "裂变模块";

            default:
                return "Unknown Module";
        }
    }
    // =========================================================
    // Choose Module
    // =========================================================

    private void ChooseModule(
        int buttonIndex)
    {
        if (!IsChoosingModule)
        {
            return;
        }

        if (buttonIndex < 0 ||
            buttonIndex
            >= ModuleChoiceCount)
        {
            return;
        }

        if (!displayedModuleValid[
                buttonIndex])
        {
            return;
        }


        UpgradeType selectedModule =
            displayedModules[
                buttonIndex
            ];


        bool success = false;


        if (currentSelectionMode
            == ModuleSelectionMode.Unlock)
        {
            success =
                UnlockModule(
                    selectedModule
                );
        }
        else if (currentSelectionMode
                 == ModuleSelectionMode.Overclock)
        {
            success =
                UpgradeOwnedModule(
                    selectedModule
                );
        }


        if (!success)
        {
            Debug.LogWarning(
                "Weapon Module 选择失败："
                + selectedModule,
                this
            );

            return;
        }

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance
                .NotifyGuaranteedModuleProgression();
        }

        Debug.Log(
            "玩家完成 Weapon Module 选择："
            + selectedModule
            + "\nMode: "
            + currentSelectionMode,
            this
        );


        CloseModuleSelection();
    }


    private bool UpgradeOwnedModule(
    UpgradeType upgradeType)
    {
        EnsurePlayerReference();


        if (playerWeaponModifiers == null)
        {
            return false;
        }


        if (!CanOverclockModule(
                upgradeType))
        {
            return false;
        }


        switch (upgradeType)
        {
            case UpgradeType.Piercing:

                playerWeaponModifiers
                    .ApplyPiercingUpgrade();

                break;


            case UpgradeType.Explosive:

                playerWeaponModifiers
                    .ApplyExplosiveUpgrade();

                break;


            case UpgradeType.ChainLightning:

                playerWeaponModifiers
                    .ApplyChainLightningUpgrade();

                break;


            case UpgradeType.SplitShot:

                playerWeaponModifiers
                    .ApplySplitShotUpgrade();

                break;


            default:
                return false;
        }


        RefreshDebugState();


        Debug.Log(
            "Weapon Module Overclock 完成："
            + upgradeType
            + "\nNew Level: "
            + GetModuleLevel(
                upgradeType
            ),
            this
        );


        return true;
    }

    private void CloseModuleSelection()
    {
        if (moduleSelectionPanel != null)
        {
            moduleSelectionPanel
                .SetActive(false);
        }


        currentSelectionIndex = 0;

        currentSelectionMode =
            ModuleSelectionMode.None;

        IsChoosingModule = false;

        ClearDisplayedModules();


        if (GameManager.Instance != null
            && GameManager.Instance
                .CurrentState
            == GameState.Playing
            && !UpgradeManager
                .IsChoosingUpgrade)
        {
            Time.timeScale = 1f;
        }
    }


    // =========================================================
    // Player Reference
    // =========================================================

    private void FindPlayerWeaponModifiers()
    {
        if (playerWeaponModifiers != null)
        {
            return;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (player == null)
        {
            Debug.LogWarning(
                "WeaponModuleSelectionManager: "
                + "找不到 Tag 为 Player 的对象。",
                this
            );

            return;
        }


        playerWeaponModifiers =
            player.GetComponent<
                PlayerWeaponModifiers
            >();


        if (playerWeaponModifiers == null)
        {
            Debug.LogWarning(
                "WeaponModuleSelectionManager: "
                + "Player 上没有 "
                + "PlayerWeaponModifiers。",
                this
            );
        }
    }


    private void EnsurePlayerReference()
    {
        if (playerWeaponModifiers == null)
        {
            FindPlayerWeaponModifiers();
        }
    }


    // =========================================================
    // Module State
    // =========================================================

    public bool IsModuleType(
        UpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case UpgradeType.Piercing:
            case UpgradeType.Explosive:
            case UpgradeType.ChainLightning:
            case UpgradeType.SplitShot:
                return true;

            default:
                return false;
        }
    }


    public bool IsModuleUnlocked(
        UpgradeType upgradeType)
    {
        EnsurePlayerReference();

        if (playerWeaponModifiers == null)
        {
            return false;
        }


        switch (upgradeType)
        {
            case UpgradeType.Piercing:
                return
                    playerWeaponModifiers
                        .PiercingLevel
                    > 0;


            case UpgradeType.Explosive:
                return
                    playerWeaponModifiers
                        .ExplosiveLevel
                    > 0;


            case UpgradeType.ChainLightning:
                return
                    playerWeaponModifiers
                        .ChainLightningLevel
                    > 0;


            case UpgradeType.SplitShot:
                return
                    playerWeaponModifiers
                        .SplitShotLevel
                    > 0;


            default:
                return false;
        }
    }


    public bool CanUnlockModule(
        UpgradeType upgradeType)
    {
        if (!IsModuleType(
                upgradeType))
        {
            return false;
        }

        if (IsModuleUnlocked(
                upgradeType))
        {
            return false;
        }

        if (GetUnlockedModuleCount()
            >= maxModuleSlots)
        {
            return false;
        }

        return true;
    }


    public bool CanOfferAsNormalUpgrade(
        UpgradeType upgradeType)
    {
        if (!IsModuleType(
                upgradeType))
        {
            return false;
        }

        return IsModuleUnlocked(
            upgradeType
        );
    }


    public int GetUnlockedModuleCount()
    {
        EnsurePlayerReference();

        if (playerWeaponModifiers == null)
        {
            return 0;
        }


        int count = 0;


        if (playerWeaponModifiers
            .PiercingLevel > 0)
        {
            count++;
        }

        if (playerWeaponModifiers
            .ExplosiveLevel > 0)
        {
            count++;
        }

        if (playerWeaponModifiers
            .ChainLightningLevel > 0)
        {
            count++;
        }

        if (playerWeaponModifiers
            .SplitShotLevel > 0)
        {
            count++;
        }


        return count;
    }


    // =========================================================
    // Unlock
    // =========================================================

    public bool UnlockModule(
        UpgradeType upgradeType)
    {
        EnsurePlayerReference();

        if (playerWeaponModifiers == null)
        {
            return false;
        }

        if (!CanUnlockModule(
                upgradeType))
        {
            Debug.LogWarning(
                "Weapon Module 无法解锁："
                + upgradeType,
                this
            );

            return false;
        }


        switch (upgradeType)
        {
            case UpgradeType.Piercing:

                playerWeaponModifiers
                    .ApplyPiercingUpgrade();

                break;


            case UpgradeType.Explosive:

                playerWeaponModifiers
                    .ApplyExplosiveUpgrade();

                break;


            case UpgradeType.ChainLightning:

                playerWeaponModifiers
                    .ApplyChainLightningUpgrade();

                break;


            case UpgradeType.SplitShot:

                playerWeaponModifiers
                    .ApplySplitShotUpgrade();

                break;


            default:
                return false;
        }


        RefreshDebugState();


        Debug.Log(
            "Weapon Module 已解锁："
            + upgradeType
            + "\n当前模块槽："
            + GetUnlockedModuleCount()
            + " / "
            + maxModuleSlots,
            this
        );


        return true;
    }


    // =========================================================
    // Helpers
    // =========================================================

    private void RefreshDebugState()
    {
        unlockedModuleCount =
            GetUnlockedModuleCount();

        canUnlockNewModule =
            unlockedModuleCount
            < maxModuleSlots;
    }


    private void ClearDisplayedModules()
    {
        for (int i = 0;
             i < ModuleChoiceCount;
             i++)
        {
            displayedModules[i] =
                UpgradeType.Piercing;

            displayedModuleValid[i] =
                false;
        }


        RefreshDisplayedModuleDebug();
    }


    private void RefreshDisplayedModuleDebug()
    {
        displayedModule1 =
            displayedModuleValid[0]
            ? displayedModules[0]
                .ToString()
            : "None";

        displayedModule2 =
            displayedModuleValid[1]
            ? displayedModules[1]
                .ToString()
            : "None";

        displayedModule3 =
            displayedModuleValid[2]
            ? displayedModules[2]
                .ToString()
            : "None";
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Unlock Piercing")]
    private void DebugUnlockPiercing()
    {
        UnlockModule(
            UpgradeType.Piercing
        );
    }


    [ContextMenu(
        "Debug/Unlock Explosive")]
    private void DebugUnlockExplosive()
    {
        UnlockModule(
            UpgradeType.Explosive
        );
    }


    [ContextMenu(
        "Debug/Unlock Chain Lightning")]
    private void DebugUnlockChain()
    {
        UnlockModule(
            UpgradeType.ChainLightning
        );
    }


    [ContextMenu(
        "Debug/Unlock Split Shot")]
    private void DebugUnlockSplit()
    {
        UnlockModule(
            UpgradeType.SplitShot
        );
    }


    [ContextMenu(
        "Debug/Print Module State")]
    private void DebugPrintModuleState()
    {
        Debug.Log(
            "===== Weapon Module State =====\n"
            + "Unlocked Count: "
            + GetUnlockedModuleCount()
            + " / "
            + maxModuleSlots
            + "\nPiercing: "
            + IsModuleUnlocked(
                UpgradeType.Piercing)
            + "\nExplosive: "
            + IsModuleUnlocked(
                UpgradeType.Explosive)
            + "\nChain Lightning: "
            + IsModuleUnlocked(
                UpgradeType.ChainLightning)
            + "\nSplit Shot: "
            + IsModuleUnlocked(
                UpgradeType.SplitShot),
            this
        );
    }


    private void OnValidate()
    {
        maxModuleSlots =
            Mathf.Clamp(
                maxModuleSlots,
                1,
                4
            );

        firstModuleSelectionTime =
            Mathf.Max(
                0f,
                firstModuleSelectionTime
            );

        secondModuleSelectionTime =
            Mathf.Max(
                firstModuleSelectionTime,
                secondModuleSelectionTime
            );

        overclockSelectionTime =
    Mathf.Max(
        secondModuleSelectionTime,
        overclockSelectionTime
    );
    }
}