using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public sealed class PendingRecoveryItem
{
    [SerializeField]
    private ItemData item;


    [SerializeField]
    private int quantity;


    public ItemData Item =>
        item;


    public int Quantity =>
        quantity;


    public PendingRecoveryItem(
        ItemData item,
        int quantity
    )
    {
        this.item =
            item;


        this.quantity =
            Mathf.Max(
                0,
                quantity
            );
    }


    public void Add(
        int amount
    )
    {
        if (amount <= 0)
        {
            return;
        }


        quantity +=
            amount;
    }


    public int Remove(
        int amount
    )
    {
        if (amount <= 0 ||
            quantity <= 0)
        {
            return 0;
        }


        int removed =
            Mathf.Min(
                amount,
                quantity
            );


        quantity -=
            removed;


        return removed;
    }
}


[DisallowMultipleComponent]
public sealed class PersistentProfile :
    MonoBehaviour
{
    // =========================================================
    // Singleton
    // =========================================================

    public static PersistentProfile Instance
    {
        get;
        private set;
    }


    // =========================================================
    // Runtime Profile
    // =========================================================

    private ItemCatalog itemCatalog;


    private PersistentStash stash;


    private readonly List<
        PendingRecoveryItem
    > pendingRecovery =
        new List<
            PendingRecoveryItem
        >();


    private int credits;


    private bool initialized;


    // =========================================================
    // Events
    // =========================================================

    public event Action Changed;


    // =========================================================
    // Read Only
    // =========================================================

    public bool IsInitialized =>
        initialized;


    public ItemCatalog ItemCatalog =>
        itemCatalog;


    public PersistentStash Stash =>
        stash;


    public int Credits =>
        credits;


    public IReadOnlyList<
        PendingRecoveryItem
    > PendingRecovery =>
        pendingRecovery;


    public int PendingRecoveryItemCount
    {
        get
        {
            int total =
                0;


            for (int i = 0;
                 i < pendingRecovery.Count;
                 i++)
            {
                PendingRecoveryItem entry =
                    pendingRecovery[i];


                if (entry == null)
                {
                    continue;
                }


                total +=
                    Mathf.Max(
                        0,
                        entry.Quantity
                    );
            }


            return total;
        }
    }


    public int PendingRecoveryTotalValue
    {
        get
        {
            int total =
                0;


            for (int i = 0;
                 i < pendingRecovery.Count;
                 i++)
            {
                PendingRecoveryItem entry =
                    pendingRecovery[i];


                if (entry == null ||
                    entry.Item == null)
                {
                    continue;
                }


                total +=
                    entry.Item.BaseValue
                    *
                    entry.Quantity;
            }


            return total;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(
                gameObject
            );

            return;
        }


        Instance =
            this;


        DontDestroyOnLoad(
            gameObject
        );
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }
    }


    // =========================================================
    // New Profile
    // =========================================================

    public void CreateNewProfile(
        ItemCatalog catalog
    )
    {
        itemCatalog =
            catalog;


        credits =
            0;


        EnsureStash();


        stash.Clear();


        pendingRecovery.Clear();


        initialized =
            true;


        Changed?.Invoke();


        Debug.Log(
            "[PersistentProfile] "
            + "Created new runtime profile."
        );
    }


    // =========================================================
    // Load
    // =========================================================

    public void LoadFromSaveData(
        SaveGameData data,
        ItemCatalog catalog
    )
    {
        if (data == null)
        {
            CreateNewProfile(
                catalog
            );

            return;
        }


        data.EnsureCollections();


        itemCatalog =
            catalog;


        credits =
            Mathf.Max(
                0,
                data.credits
            );


        EnsureStash();


        stash.LoadFromSave(
            data.stashItems,
            itemCatalog,
            out int loadedStashItems,
            out int skippedStashItems
        );


        LoadPendingRecovery(
            data.pendingRecoveryItems
        );


        initialized =
            true;


        Changed?.Invoke();


        Debug.Log(
            "[PersistentProfile] "
            + "Profile loaded."
            + "\nCredits: "
            + credits
            + "\nStash Stacks Loaded: "
            + loadedStashItems
            + "\nStash Stacks Skipped: "
            + skippedStashItems
            + "\nPending Recovery Items: "
            + PendingRecoveryItemCount
        );
    }


    private void EnsureStash()
    {
        if (stash != null)
        {
            return;
        }


        stash =
            new PersistentStash();
    }


    // =========================================================
    // Pending Recovery Load
    // =========================================================

    private void LoadPendingRecovery(
        IReadOnlyList<
            SavedPendingRecoveryItem
        > savedItems
    )
    {
        pendingRecovery.Clear();


        if (savedItems == null ||
            itemCatalog == null)
        {
            return;
        }


        for (int i = 0;
             i < savedItems.Count;
             i++)
        {
            SavedPendingRecoveryItem saved =
                savedItems[i];


            if (saved == null ||
                saved.quantity <= 0 ||
                string.IsNullOrWhiteSpace(
                    saved.itemId
                ))
            {
                continue;
            }


            if (!itemCatalog.TryGetItem(
                    saved.itemId,
                    out ItemData item
                ))
            {
                Debug.LogWarning(
                    "[PersistentProfile] "
                    + "Pending Recovery ItemId "
                    + "could not be resolved: "
                    + saved.itemId
                );


                continue;
            }


            AddPendingRecoveryInternal(
                item,
                saved.quantity,
                false
            );
        }
    }


    // =========================================================
    // Pending Recovery Query
    // =========================================================

    public int GetPendingRecoveryQuantity(
        ItemData item
    )
    {
        if (item == null)
        {
            return 0;
        }


        for (int i = 0;
             i < pendingRecovery.Count;
             i++)
        {
            PendingRecoveryItem entry =
                pendingRecovery[i];


            if (entry == null ||
                entry.Item == null)
            {
                continue;
            }


            if (IsSameItem(
                    entry.Item,
                    item
                ))
            {
                return entry.Quantity;
            }
        }


        return 0;
    }


    // =========================================================
    // Pending Recovery Add
    // =========================================================

    public void AddPendingRecovery(
        ItemData item,
        int quantity
    )
    {
        AddPendingRecoveryInternal(
            item,
            quantity,
            true
        );
    }


    private void AddPendingRecoveryInternal(
        ItemData item,
        int quantity,
        bool notify
    )
    {
        if (item == null ||
            quantity <= 0)
        {
            return;
        }


        for (int i = 0;
             i < pendingRecovery.Count;
             i++)
        {
            PendingRecoveryItem existing =
                pendingRecovery[i];


            if (existing == null ||
                existing.Item == null)
            {
                continue;
            }


            if (!IsSameItem(
                    existing.Item,
                    item
                ))
            {
                continue;
            }


            existing.Add(
                quantity
            );


            if (notify)
            {
                Changed?.Invoke();
            }


            return;
        }


        pendingRecovery.Add(
            new PendingRecoveryItem(
                item,
                quantity
            )
        );


        if (notify)
        {
            Changed?.Invoke();
        }
    }


    // =========================================================
    // Pending Recovery Remove
    // =========================================================

    public bool TryRemovePendingRecovery(
        ItemData item,
        int quantity,
        out int removedQuantity
    )
    {
        removedQuantity =
            0;


        if (item == null ||
            quantity <= 0)
        {
            return false;
        }


        for (int i = 0;
             i < pendingRecovery.Count;
             i++)
        {
            PendingRecoveryItem entry =
                pendingRecovery[i];


            if (entry == null ||
                entry.Item == null)
            {
                continue;
            }


            if (!IsSameItem(
                    entry.Item,
                    item
                ))
            {
                continue;
            }


            removedQuantity =
                entry.Remove(
                    quantity
                );


            if (entry.Quantity <= 0)
            {
                pendingRecovery.RemoveAt(
                    i
                );
            }


            if (removedQuantity > 0)
            {
                Changed?.Invoke();
            }


            return removedQuantity ==
                   quantity;
        }


        return false;
    }


    public void ClearPendingRecovery()
    {
        if (pendingRecovery.Count == 0)
        {
            return;
        }


        pendingRecovery.Clear();


        Changed?.Invoke();
    }


    // =========================================================
    // Item Identity
    // =========================================================

    private bool IsSameItem(
        ItemData first,
        ItemData second
    )
    {
        if (first == null ||
            second == null)
        {
            return false;
        }


        if (first == second)
        {
            return true;
        }


        if (string.IsNullOrWhiteSpace(
                first.ItemId
            ) ||
            string.IsNullOrWhiteSpace(
                second.ItemId
            ))
        {
            return false;
        }


        return string.Equals(
            first.ItemId,
            second.ItemId,
            StringComparison.Ordinal
        );
    }


    // =========================================================
    // Build Save
    // =========================================================

    public SaveGameData BuildSaveData(
        int saveVersion
    )
    {
        SaveGameData data =
            new SaveGameData(
                saveVersion
            );


        data.credits =
            Mathf.Max(
                0,
                credits
            );


        if (stash != null)
        {
            data.stashItems =
                stash.BuildSaveItems();
        }


        for (int i = 0;
             i < pendingRecovery.Count;
             i++)
        {
            PendingRecoveryItem pending =
                pendingRecovery[i];


            if (pending == null ||
                pending.Item == null ||
                pending.Quantity <= 0)
            {
                continue;
            }


            string itemId =
                pending.Item.ItemId;


            if (string.IsNullOrWhiteSpace(
                    itemId
                ))
            {
                Debug.LogError(
                    "[PersistentProfile] "
                    + "Pending Recovery item "
                    + "has invalid ItemId: "
                    + pending.Item.name
                );


                continue;
            }


            data.pendingRecoveryItems.Add(
                new SavedPendingRecoveryItem(
                    itemId,
                    pending.Quantity
                )
            );
        }


        return data;
    }


    // =========================================================
    // Credits
    // =========================================================

    public void AddCredits(
        int amount
    )
    {
        if (amount <= 0)
        {
            return;
        }


        credits +=
            amount;


        Changed?.Invoke();
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Add 100 Test Credits"
    )]
    private void DebugAddTestCredits()
    {
        if (!initialized)
        {
            Debug.LogWarning(
                "[PersistentProfile] "
                + "Profile is not initialized.",
                this
            );

            return;
        }


        AddCredits(
            100
        );


        Debug.Log(
            "[PersistentProfile] "
            + "Added 100 test credits. "
            + "Current Credits: "
            + credits,
            this
        );
    }


    [ContextMenu(
        "Debug/Print Profile"
    )]
    private void DebugPrintProfile()
    {
        Debug.Log(
            "===== PERSISTENT PROFILE ====="
            + "\nInitialized: "
            + initialized
            + "\nCredits: "
            + credits
            + "\nStash Stacks: "
            + (
                stash != null
                    ? stash.Grid.Items.Count
                    : 0
            )
            + "\nStash Items: "
            + (
                stash != null
                    ? stash.TotalItemCount
                    : 0
            )
            + "\nStash Value: "
            + (
                stash != null
                    ? stash.TotalValue
                    : 0
            )
            + "\nPending Recovery Items: "
            + PendingRecoveryItemCount
            + "\nPending Recovery Value: "
            + PendingRecoveryTotalValue,
            this
        );
    }
}