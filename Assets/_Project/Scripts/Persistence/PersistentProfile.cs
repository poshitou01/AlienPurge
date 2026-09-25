using System;
using System.Collections.Generic;
using UnityEngine;


public sealed class PendingRecoveryItem
{
    private readonly ItemData item;

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


        stash =
            new PersistentStash();


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


        itemCatalog =
            catalog;


        credits =
            Mathf.Max(
                0,
                data.credits
            );


        stash =
            new PersistentStash();


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
            + "\nPending Recovery Entries: "
            + pendingRecovery.Count
        );
    }


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
            SavedPendingRecoveryItem
                saved =
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


            pendingRecovery.Add(
                new PendingRecoveryItem(
                    item,
                    saved.quantity
                )
            );
        }
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
            + "\nStash: "
            + (
                stash != null
                    ? stash.Grid.Items.Count
                    : 0
            )
            + " stacks"
            + "\nPending Recovery: "
            + pendingRecovery.Count,
            this
        );
    }
}