using System;
using System.Collections.Generic;


[Serializable]
public sealed class SaveGameData
{
    // =========================================================
    // Version
    // =========================================================

    public int saveVersion = 1;


    // =========================================================
    // Economy
    // =========================================================

    public int credits = 0;


    // =========================================================
    // Persistent Stash
    // =========================================================

    public List<SavedGridItem>
        stashItems =
            new List<SavedGridItem>();


    // =========================================================
    // Pending Recovery
    // =========================================================

    public List<SavedPendingRecoveryItem>
        pendingRecoveryItems =
            new List<
                SavedPendingRecoveryItem
            >();


    // =========================================================
    // Construction
    // =========================================================

    public SaveGameData()
    {
    }


    public SaveGameData(
        int version
    )
    {
        saveVersion =
            version;
    }


    // =========================================================
    // Validation
    // =========================================================

    public void EnsureCollections()
    {
        if (stashItems == null)
        {
            stashItems =
                new List<SavedGridItem>();
        }


        if (pendingRecoveryItems == null)
        {
            pendingRecoveryItems =
                new List<
                    SavedPendingRecoveryItem
                >();
        }
    }
}


[Serializable]
public sealed class SavedGridItem
{
    public string itemId;

    public int quantity;

    public int gridX;

    public int gridY;


    public SavedGridItem()
    {
    }


    public SavedGridItem(
        string itemId,
        int quantity,
        int gridX,
        int gridY
    )
    {
        this.itemId =
            itemId;

        this.quantity =
            quantity;

        this.gridX =
            gridX;

        this.gridY =
            gridY;
    }
}


[Serializable]
public sealed class SavedPendingRecoveryItem
{
    public string itemId;

    public int quantity;


    public SavedPendingRecoveryItem()
    {
    }


    public SavedPendingRecoveryItem(
        string itemId,
        int quantity
    )
    {
        this.itemId =
            itemId;

        this.quantity =
            quantity;
    }
}