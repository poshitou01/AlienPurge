using System;
using UnityEngine;


public enum TraderSaleSource
{
    Stash,
    PendingRecovery
}


[Serializable]
public sealed class TraderSaleResult
{
    [SerializeField]
    private bool success;


    [SerializeField]
    private TraderSaleSource source;


    [SerializeField]
    private string itemId;


    [SerializeField]
    private string displayName;


    [SerializeField]
    private int quantity;


    [SerializeField]
    private int totalValue;


    [SerializeField]
    private string failureReason;


    public bool Success =>
        success;


    public TraderSaleSource Source =>
        source;


    public string ItemId =>
        itemId;


    public string DisplayName =>
        displayName;


    public int Quantity =>
        quantity;


    public int TotalValue =>
        totalValue;


    public string FailureReason =>
        failureReason;


    public TraderSaleResult(
        bool success,
        TraderSaleSource source,
        string itemId,
        string displayName,
        int quantity,
        int totalValue,
        string failureReason
    )
    {
        this.success =
            success;


        this.source =
            source;


        this.itemId =
            itemId
            ??
            string.Empty;


        this.displayName =
            displayName
            ??
            string.Empty;


        this.quantity =
            Mathf.Max(
                0,
                quantity
            );


        this.totalValue =
            Mathf.Max(
                0,
                totalValue
            );


        this.failureReason =
            failureReason
            ??
            string.Empty;
    }


    public static TraderSaleResult Failed(
        TraderSaleSource source,
        string reason
    )
    {
        return new TraderSaleResult(
            false,
            source,
            string.Empty,
            string.Empty,
            0,
            0,
            reason
        );
    }
}


public sealed class TraderService
{
    // =========================================================
    // Sell Stash Stack
    // =========================================================

    public bool TrySellStashStack(
        PersistentProfile profile,
        SaveManager saveManager,
        ItemStack stack,
        out TraderSaleResult result
    )
    {
        if (!CanOperate(
                profile,
                saveManager
            ))
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.Stash,
                    "Persistent systems are unavailable."
                );


            return false;
        }


        if (stack == null ||
            stack.IsEmpty ||
            stack.Item == null)
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.Stash,
                    "Selected Stash stack is invalid."
                );


            return false;
        }


        ItemData item =
            stack.Item;


        int quantity =
            stack.Quantity;


        if (!TryCalculateSaleValue(
                profile,
                item,
                quantity,
                out int totalValue,
                out string failureReason
            ))
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.Stash,
                    failureReason
                );


            return false;
        }


        // =====================================================
        // Rollback Snapshot
        // =====================================================

        SaveGameData rollbackData =
            profile.BuildSaveData(
                SaveManager
                    .CurrentSaveVersion
            );


        ItemCatalog catalog =
            profile.ItemCatalog;


        // =====================================================
        // Remove REAL Model Data
        // =====================================================

        bool removed =
            profile.Stash
                .TryDiscardStack(
                    stack
                );


        if (!removed)
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.Stash,
                    "Could not remove Stash stack."
                );


            return false;
        }


        // =====================================================
        // Credits
        // =====================================================

        profile.AddCredits(
            totalValue
        );


        // =====================================================
        // Save Commit
        // =====================================================

        bool saved =
            saveManager.Save();


        if (!saved)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.Stash,
                    "Save failed. Sale rolled back."
                );


            Debug.LogError(
                "[Trader] "
                + "Stash sale Save failed. "
                + "Transaction rolled back."
            );


            return false;
        }


        result =
            new TraderSaleResult(
                true,
                TraderSaleSource.Stash,
                item.ItemId,
                item.DisplayName,
                quantity,
                totalValue,
                string.Empty
            );


        Debug.Log(
            "===== TRADER SALE COMMITTED ====="
            + "\nSource: Stash"
            + "\nItem: "
            + item.DisplayName
            + "\nQuantity: "
            + quantity
            + "\nCredits Earned: "
            + totalValue
            + "\nCredits Total: "
            + profile.Credits
        );


        return true;
    }


    // =========================================================
    // Sell Pending Recovery
    // =========================================================

    public bool TrySellPendingItem(
        PersistentProfile profile,
        SaveManager saveManager,
        ItemData item,
        out TraderSaleResult result
    )
    {
        if (!CanOperate(
                profile,
                saveManager
            ))
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.PendingRecovery,
                    "Persistent systems are unavailable."
                );


            return false;
        }


        if (item == null)
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.PendingRecovery,
                    "Pending item is invalid."
                );


            return false;
        }


        int quantity =
            profile.GetPendingRecoveryQuantity(
                item
            );


        if (quantity <= 0)
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.PendingRecovery,
                    "No Pending Recovery quantity exists."
                );


            return false;
        }


        if (!TryCalculateSaleValue(
                profile,
                item,
                quantity,
                out int totalValue,
                out string failureReason
            ))
        {
            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.PendingRecovery,
                    failureReason
                );


            return false;
        }


        // =====================================================
        // Rollback Snapshot
        // =====================================================

        SaveGameData rollbackData =
            profile.BuildSaveData(
                SaveManager
                    .CurrentSaveVersion
            );


        ItemCatalog catalog =
            profile.ItemCatalog;


        // =====================================================
        // Remove REAL Pending Model Data
        // =====================================================

        bool removedComplete =
            profile.TryRemovePendingRecovery(
                item,
                quantity,
                out int removed
            );


        if (!removedComplete ||
            removed != quantity)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.PendingRecovery,
                    "Could not remove Pending Recovery quantity."
                );


            return false;
        }


        // =====================================================
        // Credits
        // =====================================================

        profile.AddCredits(
            totalValue
        );


        // =====================================================
        // Save
        // =====================================================

        bool saved =
            saveManager.Save();


        if (!saved)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            result =
                TraderSaleResult.Failed(
                    TraderSaleSource.PendingRecovery,
                    "Save failed. Sale rolled back."
                );


            Debug.LogError(
                "[Trader] "
                + "Pending sale Save failed. "
                + "Transaction rolled back."
            );


            return false;
        }


        result =
            new TraderSaleResult(
                true,
                TraderSaleSource.PendingRecovery,
                item.ItemId,
                item.DisplayName,
                quantity,
                totalValue,
                string.Empty
            );


        Debug.Log(
            "===== TRADER SALE COMMITTED ====="
            + "\nSource: Pending Recovery"
            + "\nItem: "
            + item.DisplayName
            + "\nQuantity: "
            + quantity
            + "\nCredits Earned: "
            + totalValue
            + "\nCredits Total: "
            + profile.Credits
        );


        return true;
    }


    // =========================================================
    // Sale Value
    // =========================================================

    private bool TryCalculateSaleValue(
        PersistentProfile profile,
        ItemData item,
        int quantity,
        out int totalValue,
        out string failureReason
    )
    {
        totalValue =
            0;


        failureReason =
            string.Empty;


        if (item == null ||
            quantity <= 0)
        {
            failureReason =
                "Invalid sale quantity.";


            return false;
        }


        if (string.IsNullOrWhiteSpace(
                item.ItemId
            ))
        {
            failureReason =
                "Item has invalid ItemId.";


            return false;
        }


        long calculatedValue =
            (long)item.BaseValue
            *
            quantity;


        if (calculatedValue < 0 ||
            calculatedValue >
            int.MaxValue)
        {
            failureReason =
                "Sale value exceeds supported range.";


            return false;
        }


        totalValue =
            (int)calculatedValue;


        if (profile.Credits >
            int.MaxValue -
            totalValue)
        {
            failureReason =
                "Credits would exceed supported range.";


            return false;
        }


        return true;
    }


    // =========================================================
    // Guard
    // =========================================================

    private bool CanOperate(
        PersistentProfile profile,
        SaveManager saveManager
    )
    {
        if (profile == null ||
            !profile.IsInitialized ||
            profile.Stash == null)
        {
            return false;
        }


        if (saveManager == null ||
            !saveManager.IsInitialized)
        {
            return false;
        }


        return true;
    }
}