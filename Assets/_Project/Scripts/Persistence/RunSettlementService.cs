using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public sealed class RunSettlementResult
{
    [SerializeField]
    private bool committed;


    [SerializeField]
    private int runItemCount;


    [SerializeField]
    private int storedItemCount;


    [SerializeField]
    private int pendingItemCount;


    [SerializeField]
    private int storedValue;


    [SerializeField]
    private int pendingValue;


    [SerializeField]
    private string failureReason;


    public bool Committed =>
        committed;


    public int RunItemCount =>
        runItemCount;


    public int StoredItemCount =>
        storedItemCount;


    public int PendingItemCount =>
        pendingItemCount;


    public int StoredValue =>
        storedValue;


    public int PendingValue =>
        pendingValue;


    public string FailureReason =>
        failureReason;


    public RunSettlementResult(
        bool committed,
        int runItemCount,
        int storedItemCount,
        int pendingItemCount,
        int storedValue,
        int pendingValue,
        string failureReason
    )
    {
        this.committed =
            committed;


        this.runItemCount =
            Mathf.Max(
                0,
                runItemCount
            );


        this.storedItemCount =
            Mathf.Max(
                0,
                storedItemCount
            );


        this.pendingItemCount =
            Mathf.Max(
                0,
                pendingItemCount
            );


        this.storedValue =
            Mathf.Max(
                0,
                storedValue
            );


        this.pendingValue =
            Mathf.Max(
                0,
                pendingValue
            );


        this.failureReason =
            failureReason
            ??
            string.Empty;
    }


    public static RunSettlementResult Failed(
        string reason,
        int runItemCount = 0
    )
    {
        return new RunSettlementResult(
            false,
            runItemCount,
            0,
            0,
            0,
            0,
            reason
        );
    }
}


public sealed class RunSettlementService
{
    // =========================================================
    // Runtime State
    // =========================================================

    private bool hasCommitted;


    private RunSettlementResult
        lastResult;


    // =========================================================
    // Read Only
    // =========================================================

    public bool HasCommitted =>
        hasCommitted;


    public RunSettlementResult LastResult =>
        lastResult;


    // =========================================================
    // Commit
    // =========================================================

    public bool TryCommit(
        RunInventory runInventory,
        PersistentProfile profile,
        SaveManager saveManager,
        out RunSettlementResult result
    )
    {
        // =====================================================
        // Idempotency
        // =====================================================

        if (hasCommitted)
        {
            result =
                lastResult;


            Debug.LogWarning(
                "[RunSettlement] "
                + "Duplicate settlement request ignored."
            );


            return true;
        }


        // =====================================================
        // Dependencies
        // =====================================================

        if (runInventory == null ||
            runInventory.Backpack == null)
        {
            result =
                RunSettlementResult.Failed(
                    "RunInventory is unavailable."
                );


            lastResult =
                result;


            return false;
        }


        if (profile == null ||
            !profile.IsInitialized ||
            profile.Stash == null ||
            profile.ItemCatalog == null)
        {
            result =
                RunSettlementResult.Failed(
                    "PersistentProfile is unavailable.",
                    runInventory.TotalItemCount
                );


            lastResult =
                result;


            return false;
        }


        if (saveManager == null ||
            !saveManager.IsInitialized)
        {
            result =
                RunSettlementResult.Failed(
                    "SaveManager is unavailable.",
                    runInventory.TotalItemCount
                );


            lastResult =
                result;


            return false;
        }


        // =====================================================
        // Preflight
        // =====================================================
        //
        // 先复制Run中的ItemData + Quantity。
        //
        // Settlement过程中不直接修改RunInventory。
        // =====================================================

        List<SettlementLine> lines =
            new List<SettlementLine>();


        IReadOnlyList<ItemStack> stacks =
            runInventory
                .Backpack
                .Items;


        int runItemCount =
            0;


        for (int i = 0;
             i < stacks.Count;
             i++)
        {
            ItemStack stack =
                stacks[i];


            if (stack == null ||
                stack.IsEmpty)
            {
                continue;
            }


            ItemData item =
                stack.Item;


            if (item == null)
            {
                result =
                    RunSettlementResult.Failed(
                        "Run contains a stack with no ItemData.",
                        runInventory.TotalItemCount
                    );


                lastResult =
                    result;


                return false;
            }


            if (string.IsNullOrWhiteSpace(
                    item.ItemId
                ))
            {
                result =
                    RunSettlementResult.Failed(
                        "Run contains an item with invalid ItemId: "
                        + item.name,
                        runInventory.TotalItemCount
                    );


                lastResult =
                    result;


                return false;
            }


            if (!profile.ItemCatalog.TryGetItem(
                    item.ItemId,
                    out ItemData resolvedItem
                ) ||
                resolvedItem == null)
            {
                result =
                    RunSettlementResult.Failed(
                        "ItemCatalog cannot resolve ItemId: "
                        + item.ItemId,
                        runInventory.TotalItemCount
                    );


                lastResult =
                    result;


                return false;
            }


            int quantity =
                stack.Quantity;


            if (quantity <= 0)
            {
                continue;
            }


            lines.Add(
                new SettlementLine(
                    resolvedItem,
                    quantity
                )
            );


            runItemCount +=
                quantity;
        }


        // =====================================================
        // Runtime Rollback Snapshot
        // =====================================================
        //
        // 如果Save失败：
        //
        // Profile恢复到Settlement之前。
        // RunInventory保持不动。
        // =====================================================

        SaveGameData rollbackData =
            profile.BuildSaveData(
                SaveManager
                    .CurrentSaveVersion
            );


        ItemCatalog catalog =
            profile.ItemCatalog;


        int storedItemCount =
            0;


        int pendingItemCount =
            0;


        int storedValue =
            0;


        int pendingValue =
            0;


        // =====================================================
        // Apply
        // =====================================================

        for (int i = 0;
             i < lines.Count;
             i++)
        {
            SettlementLine line =
                lines[i];


            ItemData item =
                line.Item;


            int quantity =
                line.Quantity;


            profile.Stash.TryStore(
                item,
                quantity,
                out int stored,
                out int remaining
            );


            storedItemCount +=
                stored;


            storedValue +=
                stored
                *
                item.BaseValue;


            if (remaining > 0)
            {
                profile.AddPendingRecovery(
                    item,
                    remaining
                );


                pendingItemCount +=
                    remaining;


                pendingValue +=
                    remaining
                    *
                    item.BaseValue;
            }
        }


        // =====================================================
        // Invariant
        // =====================================================

        if (storedItemCount +
            pendingItemCount !=
            runItemCount)
        {
            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            result =
                RunSettlementResult.Failed(
                    "Settlement quantity invariant failed.",
                    runItemCount
                );


            lastResult =
                result;


            Debug.LogError(
                "[RunSettlement] "
                + "Quantity invariant failed."
                + "\nRun: "
                + runItemCount
                + "\nStored: "
                + storedItemCount
                + "\nPending: "
                + pendingItemCount
            );


            return false;
        }


        // =====================================================
        // Disk Commit
        // =====================================================

        bool saved =
            saveManager.Save();


        if (!saved)
        {
            // -------------------------------------------------
            // Runtime rollback.
            //
            // RunInventory仍然保留完整Loot。
            // -------------------------------------------------

            profile.LoadFromSaveData(
                rollbackData,
                catalog
            );


            result =
                RunSettlementResult.Failed(
                    "Save failed. Persistent profile rolled back.",
                    runItemCount
                );


            lastResult =
                result;


            Debug.LogError(
                "[RunSettlement] "
                + "Settlement Save failed. "
                + "Run loot was NOT cleared."
            );


            return false;
        }


        // =====================================================
        // Commit Complete
        // =====================================================
        //
        // 只有磁盘保存成功后，
        // 才允许清空RunInventory。
        // =====================================================

        runInventory
            .ClearRunInventory();


        hasCommitted =
            true;


        result =
            new RunSettlementResult(
                true,
                runItemCount,
                storedItemCount,
                pendingItemCount,
                storedValue,
                pendingValue,
                string.Empty
            );


        lastResult =
            result;


        Debug.Log(
            "===== RUN SETTLEMENT COMMITTED ====="
            + "\nRun Items: "
            + runItemCount
            + "\nStored In Stash: "
            + storedItemCount
            + "\nPending Recovery: "
            + pendingItemCount
            + "\nStored Value: "
            + storedValue
            + "\nPending Value: "
            + pendingValue
        );


        return true;
    }


    // =========================================================
    // Settlement Line
    // =========================================================

    private readonly struct SettlementLine
    {
        public ItemData Item
        {
            get;
        }


        public int Quantity
        {
            get;
        }


        public SettlementLine(
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