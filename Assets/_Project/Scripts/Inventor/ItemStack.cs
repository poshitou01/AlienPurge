using System;
using UnityEngine;


[Serializable]
public class ItemStack
{
    // =========================================================
    // Runtime Data
    // =========================================================

    [SerializeField]
    private ItemData item;

    [SerializeField]
    private int quantity;

    [SerializeField]
    private int gridX = -1;

    [SerializeField]
    private int gridY = -1;


    // =========================================================
    // Construction
    // =========================================================

    public ItemStack()
    {
    }

    public ItemStack(
        ItemData item,
        int quantity
    )
    {
        Set(
            item,
            quantity
        );
    }


    // =========================================================
    // Read Only Access
    // =========================================================

    public ItemData Item => item;

    public int Quantity => quantity;

    public int GridX => gridX;

    public int GridY => gridY;

    public int Width
    {
        get
        {
            if (item == null)
            {
                return 0;
            }

            return item.GridWidth;
        }
    }

    public int Height
    {
        get
        {
            if (item == null)
            {
                return 0;
            }

            return item.GridHeight;
        }
    }

    public int MaxStackSize
    {
        get
        {
            if (item == null)
            {
                return 0;
            }

            return item.MaxStackSize;
        }
    }

    public int RemainingCapacity
    {
        get
        {
            if (IsEmpty)
            {
                return 0;
            }

            return Mathf.Max(
                0,
                MaxStackSize - quantity
            );
        }
    }

    public bool IsEmpty =>
        item == null ||
        quantity <= 0;

    public bool IsPlaced =>
        !IsEmpty &&
        gridX >= 0 &&
        gridY >= 0;


    // =========================================================
    // Stack Query
    // =========================================================

    public bool CanStackWith(ItemData targetItem)
    {
        if (targetItem == null ||
            IsEmpty)
        {
            return false;
        }

        if (item != targetItem)
        {
            return false;
        }

        return quantity <
               item.MaxStackSize;
    }


    // =========================================================
    // Quantity
    // =========================================================

    public void Set(
        ItemData newItem,
        int newQuantity
    )
    {
        if (newItem == null ||
            newQuantity <= 0)
        {
            Clear();
            return;
        }

        item = newItem;

        quantity =
            Mathf.Clamp(
                newQuantity,
                1,
                newItem.MaxStackSize
            );
    }

    public int Add(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        if (IsEmpty)
        {
            return amount;
        }

        int amountToAdd =
            Mathf.Min(
                RemainingCapacity,
                amount
            );

        quantity +=
            amountToAdd;

        return
            amount -
            amountToAdd;
    }

    public int Remove(int amount)
    {
        if (amount <= 0 ||
            IsEmpty)
        {
            return 0;
        }

        int amountToRemove =
            Mathf.Min(
                amount,
                quantity
            );

        quantity -=
            amountToRemove;

        if (quantity <= 0)
        {
            Clear();
        }

        return amountToRemove;
    }


    // =========================================================
    // Position
    // =========================================================

    public void SetPosition(
        int x,
        int y
    )
    {
        gridX = x;
        gridY = y;
    }

    public void ClearPosition()
    {
        gridX = -1;
        gridY = -1;
    }


    // =========================================================
    // Clear
    // =========================================================

    public void Clear()
    {
        item = null;
        quantity = 0;

        ClearPosition();
    }
}