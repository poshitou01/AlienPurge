using TMPro;
using UnityEngine;
using UnityEngine.UI;


[DisallowMultipleComponent]
public sealed class StashPanelController :
    MonoBehaviour
{
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
    private TMP_Text creditsText;


    [SerializeField]
    private TMP_Text itemCountText;


    [SerializeField]
    private TMP_Text stashValueText;


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
    private TMP_Text selectedBaseValueText;


    [SerializeField]
    private TMP_Text selectedStackValueText;


    // =========================================================
    // Buttons
    // =========================================================

    [Header("Buttons")]

    [SerializeField]
    private Button closeButton;


    [SerializeField]
    private Button discardButton;


    [Tooltip(
        "Phase39 Step6才接Trader。"
    )]
    [SerializeField]
    private Button sellButton;


    // =========================================================
    // Runtime
    // =========================================================

    private PersistentProfile profile;

    private PersistentStash stash;

    private ItemStack selectedStack;

    private bool initialized;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(
                false
            );
        }


        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                ClosePanel
            );
        }


        if (discardButton != null)
        {
            discardButton.onClick.AddListener(
                DiscardSelectedStack
            );
        }


        if (sellButton != null)
        {
            // Step6才正式接Trader。
            sellButton.interactable =
                false;
        }
    }


    private void Start()
    {
        TryInitialize();
    }


    // =========================================================
    // Initialize
    // =========================================================

    private void TryInitialize()
    {
        if (initialized)
        {
            return;
        }


        profile =
            PersistentProfile.Instance;


        if (profile == null ||
            !profile.IsInitialized ||
            profile.Stash == null)
        {
            return;
        }


        if (gridView == null)
        {
            Debug.LogError(
                "[Stash UI] "
                + "InventoryGridView is missing.",
                this
            );

            return;
        }


        stash =
            profile.Stash;


        gridView.Initialize(
            stash.Grid,
            SelectItem
        );


        gridView.StackMoved +=
            HandleStackMoved;


        stash.Grid.Changed +=
            HandleGridChanged;


        profile.Changed +=
            HandleProfileChanged;


        initialized =
            true;


        RefreshAll();


        Debug.Log(
            "[Stash UI] Initialized 10x10 stash.",
            this
        );
    }


    // =========================================================
    // Open
    // =========================================================

    public void OpenPanel()
    {
        if (!initialized)
        {
            TryInitialize();
        }


        if (!initialized)
        {
            Debug.LogWarning(
                "[Stash UI] "
                + "Persistent Profile is not ready.",
                this
            );

            return;
        }


        if (panelRoot != null)
        {
            panelRoot.SetActive(
                true
            );
        }


        RefreshAll();

        ClearSelection();
    }


    // =========================================================
    // Close
    // =========================================================

    public void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(
                false
            );
        }


        ClearSelection();
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


        RefreshSelectedItem();
    }


    private void ClearSelection()
    {
        selectedStack =
            null;


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
                "Select an item in your stash "
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


        if (selectedBaseValueText != null)
        {
            selectedBaseValueText.text =
                string.Empty;
        }


        if (selectedStackValueText != null)
        {
            selectedStackValueText.text =
                string.Empty;
        }


        if (discardButton != null)
        {
            discardButton.interactable =
                false;
        }


        if (sellButton != null)
        {
            sellButton.interactable =
                false;
        }
    }


    private void RefreshSelectedItem()
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


        if (selectedBaseValueText != null)
        {
            selectedBaseValueText.text =
                "BASE VALUE  "
                + item.BaseValue;
        }


        if (selectedStackValueText != null)
        {
            int stackValue =
                item.BaseValue
                *
                selectedStack.Quantity;


            selectedStackValueText.text =
                "STACK VALUE  "
                + stackValue;
        }


        if (discardButton != null)
        {
            discardButton.interactable =
                true;
        }


        // SELL在第六大步开启。
        if (sellButton != null)
        {
            sellButton.interactable =
                false;
        }
    }


    // =========================================================
    // Discard
    // =========================================================

    private void DiscardSelectedStack()
    {
        if (stash == null ||
            selectedStack == null ||
            selectedStack.IsEmpty)
        {
            return;
        }


        string itemName =
            selectedStack.Item != null
                ? selectedStack.Item.DisplayName
                : "Unknown Item";


        int quantity =
            selectedStack.Quantity;


        bool success =
            stash.TryDiscardStack(
                selectedStack
            );


        if (!success)
        {
            return;
        }


        ClearSelection();


        bool saved =
            SaveManager.Instance != null
            &&
            SaveManager.Instance.Save();


        Debug.Log(
            "[Stash UI] Discarded "
            + itemName
            + " x"
            + quantity
            + ". Save: "
            + saved,
            this
        );
    }


    // =========================================================
    // Drag Save
    // =========================================================

    private void HandleStackMoved(
        ItemStack stack
    )
    {
        if (stack == null ||
            stack.IsEmpty)
        {
            return;
        }


        bool saved =
            SaveManager.Instance != null
            &&
            SaveManager.Instance.Save();


        Debug.Log(
            "[Stash UI] "
            + "Stack moved to "
            + stack.GridX
            + ", "
            + stack.GridY
            + ". Save: "
            + saved,
            this
        );
    }


    // =========================================================
    // Events
    // =========================================================

    private void HandleGridChanged()
    {
        RefreshSummary();

        RefreshSelectedItem();
    }


    private void HandleProfileChanged()
    {
        RefreshSummary();
    }


    // =========================================================
    // Refresh
    // =========================================================

    private void RefreshAll()
    {
        if (!initialized)
        {
            return;
        }


        gridView.Refresh();

        RefreshSummary();

        RefreshSelectedItem();
    }


    private void RefreshSummary()
    {
        if (profile == null ||
            stash == null)
        {
            return;
        }


        if (creditsText != null)
        {
            creditsText.text =
                "CREDITS  "
                + profile.Credits;
        }


        if (itemCountText != null)
        {
            itemCountText.text =
                "ITEMS  "
                + stash.TotalItemCount;
        }


        if (stashValueText != null)
        {
            stashValueText.text =
                "STASH VALUE  "
                + stash.TotalValue;
        }


        if (occupiedCellsText != null)
        {
            occupiedCellsText.text =
                "SPACE  "
                + stash.OccupiedCellCount
                + "/"
                + (
                    PersistentStash.Width
                    *
                    PersistentStash.Height
                );
        }
    }


    // =========================================================
    // Cleanup
    // =========================================================

    private void OnDestroy()
    {
        if (gridView != null)
        {
            gridView.StackMoved -=
                HandleStackMoved;
        }


        if (stash != null &&
            stash.Grid != null)
        {
            stash.Grid.Changed -=
                HandleGridChanged;
        }


        if (profile != null)
        {
            profile.Changed -=
                HandleProfileChanged;
        }


        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                ClosePanel
            );
        }


        if (discardButton != null)
        {
            discardButton.onClick.RemoveListener(
                DiscardSelectedStack
            );
        }
    }
}