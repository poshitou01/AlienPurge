using System;
using System.Collections.Generic;
using UnityEngine;


public class InventoryGrid
{
    // =========================================================
    // Grid Data
    // =========================================================

    private readonly int width;

    private readonly int height;

    private readonly ItemStack[,] occupancy;

    private readonly List<ItemStack> items =
        new List<ItemStack>();


    // =========================================================
    // Events
    // =========================================================

    public event Action Changed;


    // =========================================================
    // Read Only Access
    // =========================================================

    public int Width => width;

    public int Height => height;

    public int CellCount =>
        width * height;

    public IReadOnlyList<ItemStack> Items =>
        items;


    // =========================================================
    // Construction
    // =========================================================

    public InventoryGrid(
        int width,
        int height
    )
    {
        this.width =
            Mathf.Max(
                1,
                width
            );

        this.height =
            Mathf.Max(
                1,
                height
            );

        occupancy =
            new ItemStack[
                this.width,
                this.height
            ];
    }


    // =========================================================
    // Cell Query
    // =========================================================

    public bool IsValidPosition(
        int x,
        int y
    )
    {
        return
            x >= 0 &&
            x < width &&
            y >= 0 &&
            y < height;
    }


    public ItemStack GetOccupant(
        int x,
        int y
    )
    {
        if (!IsValidPosition(
                x,
                y
            ))
        {
            return null;
        }

        return
            occupancy[
                x,
                y
            ];
    }


    public bool IsCellOccupied(
        int x,
        int y
    )
    {
        return
            GetOccupant(
                x,
                y
            ) != null;
    }


    // =========================================================
    // Placement Query
    // =========================================================

    public bool CanPlace(
        ItemData item,
        int x,
        int y
    )
    {
        return
            CanPlace(
                item,
                x,
                y,
                null
            );
    }


    private bool CanPlace(
        ItemData item,
        int x,
        int y,
        ItemStack ignoredStack
    )
    {
        if (item == null)
        {
            return false;
        }

        if (x < 0 ||
            y < 0)
        {
            return false;
        }

        if (x + item.GridWidth > width ||
            y + item.GridHeight > height)
        {
            return false;
        }

        for (int checkY = y;
             checkY <
             y + item.GridHeight;
             checkY++)
        {
            for (int checkX = x;
                 checkX <
                 x + item.GridWidth;
                 checkX++)
            {
                ItemStack occupant =
                    occupancy[
                        checkX,
                        checkY
                    ];

                if (occupant != null &&
                    occupant != ignoredStack)
                {
                    return false;
                }
            }
        }

        return true;
    }


    // =========================================================
    // Move Query
    // =========================================================

    public bool CanMoveStack(
        ItemStack stack,
        int newX,
        int newY
    )
    {
        if (stack == null ||
            stack.IsEmpty ||
            !items.Contains(stack))
        {
            return false;
        }

        return
            CanPlace(
                stack.Item,
                newX,
                newY,
                stack
            );
    }


    // =========================================================
    // First Fit
    // =========================================================

    public bool FindFirstAvailablePosition(
        ItemData item,
        out int resultX,
        out int resultY
    )
    {
        resultX = -1;
        resultY = -1;

        if (item == null)
        {
            return false;
        }

        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                if (!CanPlace(
                        item,
                        x,
                        y
                    ))
                {
                    continue;
                }

                resultX = x;
                resultY = y;

                return true;
            }
        }

        return false;
    }


    // =========================================================
    // Place Existing Stack
    // =========================================================

    public bool TryPlaceStack(
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

        if (items.Contains(stack))
        {
            return false;
        }

        if (!CanPlace(
                stack.Item,
                x,
                y
            ))
        {
            return false;
        }

        PlaceStackInternal(
            stack,
            x,
            y
        );

        Changed?.Invoke();

        return true;
    }


    // =========================================================
    // Move Existing Stack
    // =========================================================

    public bool TryMoveStack(
        ItemStack stack,
        int newX,
        int newY
    )
    {
        if (!CanMoveStack(
                stack,
                newX,
                newY
            ))
        {
            return false;
        }

        ClearCells(
            stack
        );

        stack.SetPosition(
            newX,
            newY
        );

        FillCells(
            stack
        );

        Changed?.Invoke();

        return true;
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

        if (item == null ||
            quantity <= 0)
        {
            return false;
        }


        // -----------------------------------------------------
        // Pass 1:
        // Fill existing stacks first
        // -----------------------------------------------------

        for (int i = 0;
             i < items.Count;
             i++)
        {
            if (remainingQuantity <= 0)
            {
                break;
            }

            ItemStack stack =
                items[i];

            if (!stack.CanStackWith(
                    item
                ))
            {
                continue;
            }

            int before =
                remainingQuantity;

            remainingQuantity =
                stack.Add(
                    remainingQuantity
                );

            addedQuantity +=
                before -
                remainingQuantity;
        }


        // -----------------------------------------------------
        // Pass 2:
        // Create new stacks using First Fit
        // -----------------------------------------------------

        while (remainingQuantity > 0)
        {
            if (!FindFirstAvailablePosition(
                    item,
                    out int x,
                    out int y
                ))
            {
                break;
            }

            int amountToPlace =
                Mathf.Min(
                    item.MaxStackSize,
                    remainingQuantity
                );

            ItemStack newStack =
                new ItemStack(
                    item,
                    amountToPlace
                );

            PlaceStackInternal(
                newStack,
                x,
                y
            );

            addedQuantity +=
                amountToPlace;

            remainingQuantity -=
                amountToPlace;
        }


        if (addedQuantity > 0)
        {
            Changed?.Invoke();
        }

        return
            remainingQuantity == 0;
    }


    // =========================================================
    // Remove Quantity
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

        int remainingToRemove =
            quantity;

        for (int i =
                 items.Count - 1;
             i >= 0;
             i--)
        {
            if (remainingToRemove <= 0)
            {
                break;
            }

            ItemStack stack =
                items[i];

            if (stack.Item != item)
            {
                continue;
            }

            int quantityBefore =
                stack.Quantity;

            int removed =
                stack.Remove(
                    remainingToRemove
                );

            removedQuantity +=
                removed;

            remainingToRemove -=
                removed;

            if (removed >=
                quantityBefore)
            {
                ClearCells(
                    stack
                );

                items.RemoveAt(
                    i
                );
            }
        }

        if (removedQuantity > 0)
        {
            Changed?.Invoke();
        }

        return
            removedQuantity ==
            quantity;
    }


    // =========================================================
    // Detach Stack
    // =========================================================

    public bool DetachStack(
        ItemStack stack
    )
    {
        if (stack == null ||
            !items.Contains(stack))
        {
            return false;
        }

        ClearCells(
            stack
        );

        items.Remove(
            stack
        );

        stack.ClearPosition();

        Changed?.Invoke();

        return true;
    }


    // =========================================================
    // Count
    // =========================================================

    public int GetItemCount(
        ItemData item
    )
    {
        if (item == null)
        {
            return 0;
        }

        int total = 0;

        for (int i = 0;
             i < items.Count;
             i++)
        {
            ItemStack stack =
                items[i];

            if (stack.Item == item)
            {
                total +=
                    stack.Quantity;
            }
        }

        return total;
    }


    // =========================================================
    // Occupied Cells
    // =========================================================

    public int GetOccupiedCellCount()
    {
        int total = 0;

        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                if (occupancy[
                        x,
                        y
                    ] != null)
                {
                    total++;
                }
            }
        }

        return total;
    }


    // =========================================================
    // Clear
    // =========================================================

    public void Clear()
    {
        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                occupancy[
                    x,
                    y
                ] = null;
            }
        }

        for (int i = 0;
             i < items.Count;
             i++)
        {
            items[i].Clear();
        }

        items.Clear();

        Changed?.Invoke();
    }


    // =========================================================
    // Internal Placement
    // =========================================================

    private void PlaceStackInternal(
        ItemStack stack,
        int x,
        int y
    )
    {
        stack.SetPosition(
            x,
            y
        );

        items.Add(
            stack
        );

        FillCells(
            stack
        );
    }


    private void FillCells(
        ItemStack stack
    )
    {
        if (stack == null ||
            stack.IsEmpty)
        {
            return;
        }

        for (int y = stack.GridY;
             y <
             stack.GridY +
             stack.Height;
             y++)
        {
            for (int x =
                     stack.GridX;
                 x <
                 stack.GridX +
                 stack.Width;
                 x++)
            {
                occupancy[
                    x,
                    y
                ] = stack;
            }
        }
    }


    private void ClearCells(
        ItemStack stack
    )
    {
        if (stack == null)
        {
            return;
        }

        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                if (occupancy[
                        x,
                        y
                    ] == stack)
                {
                    occupancy[
                        x,
                        y
                    ] = null;
                }
            }
        }
    }
}