using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public class BackpackPanelController :
    MonoBehaviour
{
    // =========================================================
    // Static State
    // =========================================================

    public static BackpackPanelController
        Instance
    {
        get;
        private set;
    }


    public static bool IsOpen
    {
        get;
        private set;
    }


    // =========================================================
    // Panel
    // =========================================================

    [Header("Panel")]

    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private InventoryGridView gridView;


    // =========================================================
    // Summary
    // =========================================================

    [Header("Summary")]

    [SerializeField]
    private TMP_Text itemCountText;

    [SerializeField]
    private TMP_Text lootValueText;

    [SerializeField]
    private TMP_Text occupiedCellsText;


    // =========================================================
    // Selected Item
    // =========================================================

    [Header("Selected Item")]

    [SerializeField]
    private TMP_Text selectedNameText;

    [SerializeField]
    private TMP_Text selectedDescriptionText;

    [SerializeField]
    private TMP_Text selectedMetaText;

    [SerializeField]
    private TMP_Text selectedQuantityText;

    [SerializeField]
    private TMP_Text selectedValueText;


    // =========================================================
    // Buttons
    // =========================================================

    [Header("Buttons")]

    [SerializeField]
    private Button useButton;

    [SerializeField]
    private Button discardButton;


    // =========================================================
    // Player
    // =========================================================

    [Header("Player")]

    [SerializeField]
    private PlayerHealth playerHealth;

    [SerializeField]
    private PlayerShooting playerShooting;


    // =========================================================
    // Input
    // =========================================================

    [Header("Input")]

    [SerializeField]
    private KeyCode toggleKey =
        KeyCode.Tab;


    // =========================================================
    // Runtime
    // =========================================================

    private RunInventory runInventory;

    private ItemStack selectedStack;

    private bool initialized;


    // =========================================================
    // Backpack Pause State
    // =========================================================

    /// <summary>
    /// 打开背包之前的 Time.timeScale。
    ///
    /// 目前正常 Gameplay 下通常是 1，
    /// 但仍保存真实旧值，
    /// 避免以后加入慢动作等系统后被写死。
    /// </summary>
    private float timeScaleBeforeOpen = 1f;


    /// <summary>
    /// 当前 Time.timeScale = 0
    /// 是否是 Backpack 自己造成的。
    ///
    /// 用它区分：
    ///
    /// Backpack Pause
    /// 和
    /// PauseMenu / Upgrade / GameOver 等其他暂停。
    /// </summary>
    private bool ownsTimeScalePause;


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

        IsOpen = false;

        ownsTimeScalePause = false;

        ResolvePlayerReferences();


        if (panelRoot != null)
        {
            panelRoot.SetActive(
                false
            );
        }


        if (useButton != null)
        {
            useButton.onClick
                .AddListener(
                    UseSelectedItem
                );
        }


        if (discardButton != null)
        {
            discardButton.onClick
                .AddListener(
                    DiscardSelectedItem
                );
        }
    }


    private void Start()
    {
        TryInitialize();
    }


    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
        }


        // =====================================================
        // Game State Protection
        // =====================================================

        if (GameManager.Instance != null &&
            !GameManager.Instance.IsPlaying)
        {
            if (IsOpen)
            {
                ClosePanelForExternalPause();
            }

            return;
        }


        // =====================================================
        // Other Pause / Selection Protection
        // =====================================================
        //
        // 如果游戏已经进入：
        //
        // Pause Menu
        // Upgrade Selection
        // Weapon Module Selection
        //
        // Backpack 必须退出。
        //
        // 但这里绝对不能恢复 Time.timeScale，
        // 因为现在的暂停权已经属于其他系统。
        // =====================================================

        if (PauseMenuController.IsPaused ||
            UpgradeManager.IsChoosingUpgrade ||
            WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            if (IsOpen)
            {
                ClosePanelForExternalPause();
            }

            return;
        }


        // =====================================================
        // Backpack Toggle
        // =====================================================

        if (Input.GetKeyDown(
                toggleKey
            ))
        {
            if (IsOpen)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }


        if (IsOpen)
        {
            RefreshSelectedItemState();
        }
    }


    // =========================================================
    // Initialization
    // =========================================================

    private void TryInitialize()
    {
        if (initialized)
        {
            return;
        }


        runInventory =
            RunInventory.Instance;


        if (runInventory == null)
        {
            return;
        }


        if (gridView == null)
        {
            Debug.LogError(
                "[Backpack UI] "
                + "InventoryGridView is missing.",
                this
            );

            return;
        }


        gridView.Initialize(
            runInventory.Backpack,
            SelectItem
        );


        runInventory.Changed +=
            HandleInventoryChanged;


        RefreshSummary();

        ClearSelection();


        initialized = true;
    }


    private void ResolvePlayerReferences()
    {
        if (playerHealth == null)
        {
            playerHealth =
                FindFirstObjectByType<
                    PlayerHealth
                >();
        }


        if (playerShooting == null)
        {
            playerShooting =
                FindFirstObjectByType<
                    PlayerShooting
                >();
        }
    }


    // =========================================================
    // Open Panel
    // =========================================================

    public void OpenPanel()
    {
        if (!initialized)
        {
            TryInitialize();
        }


        if (!initialized ||
            IsOpen)
        {
            return;
        }


        // =====================================================
        // Game State Guard
        // =====================================================

        if (GameManager.Instance != null &&
            !GameManager.Instance.IsPlaying)
        {
            return;
        }


        if (PauseMenuController.IsPaused ||
            UpgradeManager.IsChoosingUpgrade ||
            WeaponModuleSelectionManager
                .IsChoosingModule)
        {
            return;
        }

        // =====================================================
        // Close Loot Search
        // =====================================================
        //
        // Backpack 是暂停式安全整理界面，
        // 不能和实时 Loot Search 同时存在。
        //
        // 如果玩家正在搜箱子时按 Tab：
        //
        // Search
        // → 安全关闭
        // → 当前未完成搜索归零
        // → 已识别结果保留
        // → Backpack 打开
        // =====================================================

        if (LootSearchPanelController.IsOpen &&
            LootSearchPanelController.Instance != null)
        {
            LootSearchPanelController
                .Instance
                .CloseActiveSearch();
        }


        // =====================================================
        // Open Backpack
        // =====================================================

        IsOpen = true;


        if (panelRoot != null)
        {
            panelRoot.SetActive(
                true
            );
        }


        // =====================================================
        // Shooting Lock
        // =====================================================

        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(
                false
            );
        }


        // =====================================================
        // Pause World
        // =====================================================
        //
        // Backpack 是“安全整理界面”。
        //
        // 与之后的 Loot Container Search 不同：
        //
        // Backpack:
        // Time.timeScale = 0
        //
        // Loot Search:
        // Time.timeScale = 1
        // =====================================================

        PauseWorldForBackpack();


        // =====================================================
        // Refresh UI
        // =====================================================

        gridView.Refresh();

        RefreshSummary();

        ClearSelection();


        Debug.Log(
            "[Backpack UI] Opened. "
            + "Gameplay paused.",
            this
        );
    }


    // =========================================================
    // Close Panel
    // =========================================================

    public void ClosePanel()
    {
        if (!IsOpen)
        {
            return;
        }


        IsOpen = false;


        if (panelRoot != null)
        {
            panelRoot.SetActive(
                false
            );
        }


        // =====================================================
        // Shooting Restore
        // =====================================================

        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(
                true
            );
        }


        // =====================================================
        // Resume World
        // =====================================================
        //
        // 只有确认这个暂停是 Backpack 自己造成的，
        // 才恢复之前的 timeScale。
        // =====================================================

        ResumeWorldFromBackpack();


        ClearSelection();


        Debug.Log(
            "[Backpack UI] Closed. "
            + "Gameplay resumed.",
            this
        );
    }


    // =========================================================
    // External Close
    // =========================================================

    /// <summary>
    /// 当 PauseMenu / Upgrade / Module Selection /
    /// GameOver / Victory 等其他系统接管暂停时，
    /// 关闭 Backpack。
    ///
    /// 非常重要：
    /// 不恢复 Time.timeScale。
    ///
    /// 因为此时世界仍然应该由其他系统保持暂停。
    /// </summary>
    private void ClosePanelForExternalPause()
    {
        if (!IsOpen)
        {
            return;
        }


        IsOpen = false;


        if (panelRoot != null)
        {
            panelRoot.SetActive(
                false
            );
        }


        if (playerShooting != null)
        {
            playerShooting.SetCanShoot(
                true
            );
        }


        // Backpack 不再拥有暂停状态，
        // 但不修改 Time.timeScale。
        ownsTimeScalePause =
            false;


        ClearSelection();


        Debug.Log(
            "[Backpack UI] Closed because "
            + "another gameplay state took control.",
            this
        );
    }


    // =========================================================
    // Time Scale
    // =========================================================

    private void PauseWorldForBackpack()
    {
        if (ownsTimeScalePause)
        {
            return;
        }


        timeScaleBeforeOpen =
            Time.timeScale;


        Time.timeScale =
            0f;


        ownsTimeScalePause =
            true;
    }


    private void ResumeWorldFromBackpack()
    {
        if (!ownsTimeScalePause)
        {
            return;
        }


        Time.timeScale =
            timeScaleBeforeOpen;


        ownsTimeScalePause =
            false;
    }


    // =========================================================
    // Selection
    // =========================================================

    private void SelectItem(
        ItemStack stack
    )
    {
        selectedStack =
            stack;


        RefreshSelectedItemState();
    }


    private void ClearSelection()
    {
        selectedStack = null;


        if (selectedNameText != null)
        {
            selectedNameText.text =
                "SELECT AN ITEM";

            selectedNameText.color =
                Color.white;
        }


        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text =
                "Click an item in the backpack "
                + "to inspect it.";
        }


        if (selectedMetaText != null)
        {
            selectedMetaText.text =
                string.Empty;
        }


        if (selectedQuantityText != null)
        {
            selectedQuantityText.text =
                string.Empty;
        }


        if (selectedValueText != null)
        {
            selectedValueText.text =
                string.Empty;
        }


        if (useButton != null)
        {
            useButton.interactable =
                false;
        }


        if (discardButton != null)
        {
            discardButton.interactable =
                false;
        }
    }


    private void RefreshSelectedItemState()
    {
        if (selectedStack == null ||
            selectedStack.IsEmpty ||
            selectedStack.Item == null)
        {
            ClearSelection();
            return;
        }


        ItemData item =
            selectedStack.Item;


        Color rarityColor =
            ItemRarityVisuals.GetColor(
                item.Rarity
            );


        if (selectedNameText != null)
        {
            selectedNameText.text =
                item.DisplayName;

            selectedNameText.color =
                rarityColor;
        }


        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text =
                item.Description;
        }


        if (selectedMetaText != null)
        {
            selectedMetaText.text =
                item.Rarity
                + "  //  "
                + item.Category
                + "\nSIZE  "
                + item.GridWidth
                + "x"
                + item.GridHeight;
        }


        if (selectedQuantityText != null)
        {
            selectedQuantityText.text =
                "QUANTITY  x"
                + selectedStack.Quantity;
        }


        if (selectedValueText != null)
        {
            int stackValue =
                item.BaseValue *
                selectedStack.Quantity;


            selectedValueText.text =
                "VALUE  "
                + stackValue;
        }


        if (discardButton != null)
        {
            discardButton.interactable =
                true;
        }


        if (useButton != null)
        {
            bool canUse =
                item is ConsumableItemData
                && playerHealth != null
                && playerHealth
                    .CanRestoreHealth;


            useButton.interactable =
                canUse;
        }
    }


    // =========================================================
    // Use
    // =========================================================

    private void UseSelectedItem()
    {
        if (selectedStack == null ||
            selectedStack.IsEmpty ||
            runInventory == null ||
            playerHealth == null)
        {
            return;
        }


        ConsumableItemData consumable =
            selectedStack.Item
                as ConsumableItemData;


        if (consumable == null)
        {
            return;
        }


        if (!playerHealth
                .CanRestoreHealth)
        {
            return;
        }


        bool complete =
            runInventory.TryRemoveItem(
                consumable,
                1,
                out int removed
            );


        if (!complete ||
            removed != 1)
        {
            return;
        }


        playerHealth.RestoreHealth(
            consumable.HealAmount
        );


        Debug.Log(
            "[Backpack UI] Used "
            + consumable.DisplayName
            + ".",
            this
        );


        ClearSelection();
    }


    // =========================================================
    // Discard
    // =========================================================

    private void DiscardSelectedItem()
    {
        if (selectedStack == null ||
            selectedStack.IsEmpty ||
            runInventory == null)
        {
            return;
        }


        string itemName =
            selectedStack.Item
                .DisplayName;


        int quantity =
            selectedStack.Quantity;


        bool success =
            runInventory.TryDiscardStack(
                selectedStack
            );


        if (!success)
        {
            return;
        }


        Debug.Log(
            "[Backpack UI] Discarded "
            + itemName
            + " x"
            + quantity
            + ".",
            this
        );


        ClearSelection();
    }


    // =========================================================
    // Inventory Events
    // =========================================================

    private void HandleInventoryChanged()
    {
        RefreshSummary();
    }


    private void RefreshSummary()
    {
        if (runInventory == null)
        {
            return;
        }


        if (itemCountText != null)
        {
            itemCountText.text =
                "ITEMS  "
                + runInventory
                    .TotalItemCount;
        }


        if (lootValueText != null)
        {
            lootValueText.text =
                "LOOT VALUE  "
                + runInventory
                    .TotalLootValue;
        }


        if (occupiedCellsText != null)
        {
            occupiedCellsText.text =
                "SPACE  "
                + runInventory
                    .OccupiedCellCount
                + "/30";
        }
    }


    // =========================================================
    // Cleanup
    // =========================================================

    private void OnDestroy()
    {
        if (runInventory != null)
        {
            runInventory.Changed -=
                HandleInventoryChanged;
        }


        if (useButton != null)
        {
            useButton.onClick
                .RemoveListener(
                    UseSelectedItem
                );
        }


        if (discardButton != null)
        {
            discardButton.onClick
                .RemoveListener(
                    DiscardSelectedItem
                );
        }


        // 如果对象在 Backpack 自己持有暂停权时
        // 被意外销毁，避免 Time.timeScale 永久停在 0。
        if (ownsTimeScalePause)
        {
            Time.timeScale =
                timeScaleBeforeOpen;

            ownsTimeScalePause =
                false;
        }


        if (Instance == this)
        {
            Instance = null;

            IsOpen = false;
        }
    }
}