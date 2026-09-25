using System.Collections.Generic;
using UnityEngine;


public sealed class PersistentStash
{
    // =========================================================
    // Size
    // =========================================================

    public const int Width = 10;

    public const int Height = 10;


    // =========================================================
    // Runtime Grid
    // =========================================================

    private readonly InventoryGrid grid;


    // =========================================================
    // Read Only
    // =========================================================

    public InventoryGrid Grid =>
        grid;


    public int TotalItemCount
    {
        get
        {
            int total = 0;

            IReadOnlyList<ItemStack> items =
                grid.Items;

            for (int i = 0;
                 i < items.Count;
                 i++)
            {
                ItemStack stack =
                    items[i];

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


    public int TotalValue
    {
        get
        {
            int total = 0;

            IReadOnlyList<ItemStack> items =
                grid.Items;

            for (int i = 0;
                 i < items.Count;
                 i++)
            {
                ItemStack stack =
                    items[i];

                if (stack == null ||
                    stack.IsEmpty ||
                    stack.Item == null)
                {
                    continue;
                }

                total +=
                    stack.Item.BaseValue
                    *
                    stack.Quantity;
            }

            return total;
        }
    }


    public int OccupiedCellCount =>
        grid.GetOccupiedCellCount();


    public bool IsFull =>
        OccupiedCellCount >=
        Width * Height;


    // =========================================================
    // Construction
    // =========================================================

    public PersistentStash()
    {
        grid =
            new InventoryGrid(
                Width,
                Height
            );
    }


    // =========================================================
    // Store
    // =========================================================

    public bool TryStore(
        ItemData item,
        int quantity,
        out int storedQuantity,
        out int remainingQuantity
    )
    {
        storedQuantity = 0;

        remainingQuantity =
            Mathf.Max(
                0,
                quantity
            );

        if (item == null ||
            quantity <= 0)
        {
            return false;
        }


        return grid.TryAddItem(
            item,
            quantity,
            out storedQuantity,
            out remainingQuantity
        );
    }


    // =========================================================
    // Remove
    // =========================================================

    public bool TryRemoveItem(
        ItemData item,
        int quantity,
        out int removedQuantity
    )
    {
        removedQuantity = 0;

        if (item == null ||
            quantity <= 0)
        {
            return false;
        }


        return grid.TryRemoveItem(
            item,
            quantity,
            out removedQuantity
        );
    }


    // =========================================================
    // Discard Whole Stack
    // =========================================================

    public bool TryDiscardStack(
        ItemStack stack
    )
    {
        if (stack == null ||
            stack.IsEmpty)
        {
            return false;
        }


        return grid.DetachStack(
            stack
        );
    }


    // =========================================================
    // Move
    // =========================================================

    public bool TryMoveStack(
        ItemStack stack,
        int x,
        int y
    )
    {
        if (stack == null ||
            stack.IsEmpty)
        {
            return false;
        }


        return grid.TryMoveStack(
            stack,
            x,
            y
        );
    }


    // =========================================================
    // Load
    // =========================================================

    public void LoadFromSave(
        IReadOnlyList<SavedGridItem> savedItems,
        ItemCatalog itemCatalog,
        out int loadedCount,
        out int skippedCount
    )
    {
        loadedCount = 0;

        skippedCount = 0;


        grid.Clear();


        if (savedItems == null ||
            itemCatalog == null)
        {
            return;
        }


        for (int i = 0;
             i < savedItems.Count;
             i++)
        {
            SavedGridItem saved =
                savedItems[i];


            if (saved == null)
            {
                skippedCount++;
                continue;
            }


            if (string.IsNullOrWhiteSpace(
                    saved.itemId
                ))
            {
                skippedCount++;

                Debug.LogWarning(
                    "[PersistentStash] "
                    + "Skipped saved item with empty ItemId."
                );

                continue;
            }


            if (saved.quantity <= 0)
            {
                skippedCount++;

                Debug.LogWarning(
                    "[PersistentStash] "
                    + "Skipped saved item with invalid quantity. "
                    + "ItemId: "
                    + saved.itemId
                );

                continue;
            }


            if (!itemCatalog.TryGetItem(
                    saved.itemId,
                    out ItemData item
                ))
            {
                skippedCount++;

                Debug.LogWarning(
                    "[PersistentStash] "
                    + "Saved ItemId could not be resolved: "
                    + saved.itemId
                );

                continue;
            }


            if (saved.quantity >
                item.MaxStackSize)
            {
                skippedCount++;

                Debug.LogWarning(
                    "[PersistentStash] "
                    + "Saved quantity exceeds MaxStackSize. "
                    + "ItemId: "
                    + saved.itemId
                    + ", Quantity: "
                    + saved.quantity
                    + ", Max: "
                    + item.MaxStackSize
                );

                continue;
            }


            ItemStack stack =
                new ItemStack(
                    item,
                    saved.quantity
                );


            bool placed =
                grid.TryPlaceStack(
                    stack,
                    saved.gridX,
                    saved.gridY
                );


            if (!placed)
            {
                skippedCount++;

                Debug.LogWarning(
                    "[PersistentStash] "
                    + "Could not restore saved grid position. "
                    + "ItemId: "
                    + saved.itemId
                    + ", Position: "
                    + saved.gridX
                    + ", "
                    + saved.gridY
                );

                continue;
            }


            loadedCount++;
        }
    }


    // =========================================================
    // Save
    // =========================================================

    public List<SavedGridItem>
        BuildSaveItems()
    {
        List<SavedGridItem> result =
            new List<SavedGridItem>();


        IReadOnlyList<ItemStack> items =
            grid.Items;


        for (int i = 0;
             i < items.Count;
             i++)
        {
            ItemStack stack =
                items[i];


            if (stack == null ||
                stack.IsEmpty ||
                stack.Item == null)
            {
                continue;
            }


            string itemId =
                stack.Item.ItemId;


            if (string.IsNullOrWhiteSpace(
                    itemId
                ))
            {
                Debug.LogError(
                    "[PersistentStash] "
                    + "Cannot save runtime stack because "
                    + "ItemData has no valid ItemId: "
                    + stack.Item.name
                );

                continue;
            }


            if (!stack.IsPlaced)
            {
                Debug.LogError(
                    "[PersistentStash] "
                    + "Cannot save an unplaced runtime stack: "
                    + itemId
                );

                continue;
            }


            result.Add(
                new SavedGridItem(
                    itemId,
                    stack.Quantity,
                    stack.GridX,
                    stack.GridY
                )
            );
        }


        return result;
    }


    // =========================================================
    // Clear
    // =========================================================

    public void Clear()
    {
        grid.Clear();
    }
}