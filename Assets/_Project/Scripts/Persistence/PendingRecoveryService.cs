using System.Collections.Generic;
using UnityEngine;


public sealed class PendingRecoveryService
{
    // =========================================================
    // Move One Entry
    // =========================================================

    public bool TryMoveToStash(
        PersistentProfile profile,
        SaveManager saveManager,
        ItemData item,
        int requestedQuantity,
        out int movedQuantity,
        out int remainingPendingQuantity
    )
    {
        movedQuantity =
            0;


        remainingPendingQuantity =
            0;


        if (!CanOperate(
                profile,
                saveManager
            ) ||
            item == null)
        {
            return false;
        }


        int available =
            profile.GetPendingRecoveryQuantity(
                item
            );


        remainingPendingQuantity =
            available;


        if (available <= 0)
        {
            return false;
        }


        int quantityToMove =
            requestedQuantity <= 0
                ? available
                : Mathf.Min(
                    requestedQuantity,
                    available
                );


        SaveGameData rollbackData =
            profile.BuildSaveData(
                SaveManager
                    .CurrentSaveVersion
            );


        ItemCatalog catalog =
            profile.ItemCatalog;


        profile.Stash.TryStore(
            item,
            quantityToMove,
            out int stored,
            out int notStored
        );


        if (stored <= 0)
        {
            remainingPendingQuantity =
                available;


            Debug.Log(
                "[PendingRecovery] "
                + "No stash space available for "
                + item.DisplayName
                + "."
            );


            return true;
        }


        profile.TryRemovePendingRecovery(
            item,
            stored,
            out int removed
        );


        if (removed != stored)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            Debug.LogError(
                "[PendingRecovery] "
                + "Transfer invariant failed."
            );


            return false;
        }


        bool saved =
            saveManager.Save();


        if (!saved)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            Debug.LogError(
                "[PendingRecovery] "
                + "Save failed. "
                + "Transfer rolled back."
            );


            return false;
        }


        movedQuantity =
            stored;


        remainingPendingQuantity =
            profile.GetPendingRecoveryQuantity(
                item
            );


        Debug.Log(
            "[PendingRecovery] "
            + "Moved "
            + item.DisplayName
            + " x"
            + movedQuantity
            + " to Stash."
            + "\nRemaining Pending: "
            + remainingPendingQuantity
        );


        return true;
    }


    // =========================================================
    // Move All
    // =========================================================

    public bool TryMoveAllToStash(
        PersistentProfile profile,
        SaveManager saveManager,
        out int movedQuantity,
        out int remainingPendingQuantity
    )
    {
        movedQuantity =
            0;


        remainingPendingQuantity =
            0;


        if (!CanOperate(
                profile,
                saveManager
            ))
        {
            return false;
        }


        if (profile.PendingRecoveryItemCount <= 0)
        {
            return true;
        }


        List<TransferLine> lines =
            new List<TransferLine>();


        IReadOnlyList<PendingRecoveryItem>
            pending =
                profile.PendingRecovery;


        for (int i = 0;
             i < pending.Count;
             i++)
        {
            PendingRecoveryItem entry =
                pending[i];


            if (entry == null ||
                entry.Item == null ||
                entry.Quantity <= 0)
            {
                continue;
            }


            lines.Add(
                new TransferLine(
                    entry.Item,
                    entry.Quantity
                )
            );
        }


        SaveGameData rollbackData =
            profile.BuildSaveData(
                SaveManager
                    .CurrentSaveVersion
            );


        ItemCatalog catalog =
            profile.ItemCatalog;


        for (int i = 0;
             i < lines.Count;
             i++)
        {
            TransferLine line =
                lines[i];


            profile.Stash.TryStore(
                line.Item,
                line.Quantity,
                out int stored,
                out int notStored
            );


            if (stored <= 0)
            {
                continue;
            }


            profile.TryRemovePendingRecovery(
                line.Item,
                stored,
                out int removed
            );


            if (removed != stored)
            {
                profile.LoadFromSaveData(
                    rollbackData,
                    catalog
                );


                Debug.LogError(
                    "[PendingRecovery] "
                    + "Move All invariant failed."
                );


                return false;
            }


            movedQuantity +=
                stored;
        }


        remainingPendingQuantity =
            profile.PendingRecoveryItemCount;


        if (movedQuantity <= 0)
        {
            Debug.Log(
                "[PendingRecovery] "
                + "Move All found no available Stash space."
            );


            return true;
        }


        bool saved =
            saveManager.Save();


        if (!saved)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            movedQuantity =
                0;


            remainingPendingQuantity =
                profile.PendingRecoveryItemCount;


            Debug.LogError(
                "[PendingRecovery] "
                + "Save failed. "
                + "Move All rolled back."
            );


            return false;
        }


        Debug.Log(
            "[PendingRecovery] "
            + "Move All complete."
            + "\nMoved: "
            + movedQuantity
            + "\nRemaining Pending: "
            + remainingPendingQuantity
        );


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
            profile.Stash == null ||
            profile.ItemCatalog == null)
        {
            Debug.LogError(
                "[PendingRecovery] "
                + "PersistentProfile is unavailable."
            );


            return false;
        }


        if (saveManager == null ||
            !saveManager.IsInitialized)
        {
            Debug.LogError(
                "[PendingRecovery] "
                + "SaveManager is unavailable."
            );


            return false;
        }


        return true;
    }


    // =========================================================
    // Transfer Line
    // =========================================================

    private readonly struct TransferLine
    {
        public ItemData Item
        {
            get;
        }


        public int Quantity
        {
            get;
        }


        public TransferLine(
            ItemData item,
            int quantity
        )
        {
            Item =
                item;


            Quantity =
                quantity;
        }
    }
}