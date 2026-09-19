using System;
using UnityEngine;


[DisallowMultipleComponent]
public class RunInventory : MonoBehaviour
{
    // =========================================================
    // Singleton
    // =========================================================

    public static RunInventory Instance
    {
        get;
        private set;
    }


    // =========================================================
    // Backpack Settings
    // =========================================================

    public const int BackpackWidth = 6;
    public const int BackpackHeight = 5;


    // =========================================================
    // Runtime Data
    // =========================================================

    private InventoryGrid backpack;


    // =========================================================
    // Events
    // =========================================================

    public event Action Changed;


    // =========================================================
    // Read Only Access
    // =========================================================

    public InventoryGrid Backpack =>
        backpack;

    public int TotalItemCount
    {
        get
        {
            if (backpack == null)
            {
                return 0;
            }

            int total = 0;

            for (int i = 0;
                 i < backpack.Items.Count;
                 i++)
            {
                ItemStack stack =
                    backpack.Items[i];

                if (stack == null ||
                    stack.IsEmpty)
                {
                    continue;
                }

                total +=
                    stack.Quantity;
            }

            return total;
        }
    }

    public int TotalLootValue
    {
        get
        {
            if (backpack == null)
            {
                return 0;
            }

            int total = 0;

            for (int i = 0;
                 i < backpack.Items.Count;
                 i++)
            {
                ItemStack stack =
                    backpack.Items[i];

                if (stack == null ||
                    stack.IsEmpty ||
                    stack.Item == null)
                {
                    continue;
                }

                total +=
                    stack.Item.BaseValue *
                    stack.Quantity;
            }

            return total;
        }
    }

    public int OccupiedCellCount
    {
        get
        {
            if (backpack == null)
            {
                return 0;
            }

            return
                backpack.GetOccupiedCellCount();
        }
    }


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

        backpack =
            new InventoryGrid(
                BackpackWidth,
                BackpackHeight
            );

        backpack.Changed +=
            HandleBackpackChanged;

        Debug.Log(
            "[RunInventory] Initialized "
            + BackpackWidth
            + "x"
            + BackpackHeight
            + " backpack."
        );
    }

    private void OnDestroy()
    {
        if (backpack != null)
        {
            backpack.Changed -=
                HandleBackpackChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // Add Item
    // =========================================================

    public bool TryAddItem(
        ItemData item,
        int quantity,
        out int addedQuantity,
        out int remainingQuantity
    )
    {
        addedQuantity = 0;

        remainingQuantity =
            Mathf.Max(
                0,
                quantity
            );

        if (backpack == null ||
            item == null ||
            quantity <= 0)
        {
            return false;
        }

        return
            backpack.TryAddItem(
                item,
                quantity,
                out addedQuantity,
                out remainingQuantity
            );
    }


    // =========================================================
    // Remove Item
    // =========================================================

    public bool TryRemoveItem(
        ItemData item,
        int quantity,
        out int removedQuantity
    )
    {
        removedQuantity = 0;

        if (backpack == null ||
            item == null ||
            quantity <= 0)
        {
            return false;
        }

        return
            backpack.TryRemoveItem(
                item,
                quantity,
                out removedQuantity
            );
    }


    // =========================================================
    // Discard Stack
    // =========================================================

    public bool TryDiscardStack(
        ItemStack stack
    )
    {
        if (backpack == null ||
            stack == null)
        {
            return false;
        }

        return
            backpack.DetachStack(
                stack
            );
    }


    // =========================================================
    // Clear Run
    // =========================================================

    public void ClearRunInventory()
    {
        if (backpack == null)
        {
            return;
        }

        backpack.Clear();

        Debug.Log(
            "[RunInventory] Backpack cleared."
        );
    }


    // =========================================================
    // Event Forwarding
    // =========================================================

    private void HandleBackpackChanged()
    {
        Changed?.Invoke();
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu("Debug/Print Inventory Summary")]
    private void PrintInventorySummary()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[RunInventory] Enter Play Mode first.",
                this
            );

            return;
        }

        Debug.Log(
            "===== Run Inventory =====\n"
            + "Grid: "
            + BackpackWidth
            + "x"
            + BackpackHeight
            + "\nOccupied Cells: "
            + OccupiedCellCount
            + "/"
            + (BackpackWidth * BackpackHeight)
            + "\nItem Count: "
            + TotalItemCount
            + "\nLoot Value: "
            + TotalLootValue,
            this
        );
    }
}