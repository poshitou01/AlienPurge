using System;
using System.Collections.Generic;
using UnityEngine;


public enum RunOutcome
{
    ExtractionSuccess,
    PlayerDeath
}


[Serializable]
public sealed class RunLootSummaryEntry
{
    // =========================================================
    // Snapshot Data
    // =========================================================

    [SerializeField]
    private string itemId;


    [SerializeField]
    private string displayName;


    [SerializeField]
    private ItemRarity rarity;


    [SerializeField]
    private int quantity;


    [SerializeField]
    private int totalValue;


    // =========================================================
    // Read Only
    // =========================================================

    public string ItemId =>
        itemId;


    public string DisplayName =>
        displayName;


    public ItemRarity Rarity =>
        rarity;


    public int Quantity =>
        quantity;


    public int TotalValue =>
        totalValue;


    // =========================================================
    // Construction
    // =========================================================

    public RunLootSummaryEntry(
        string itemId,
        string displayName,
        ItemRarity rarity,
        int quantity,
        int totalValue
    )
    {
        this.itemId =
            itemId ?? string.Empty;


        this.displayName =
            displayName ?? string.Empty;


        this.rarity =
            rarity;


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
    }
}


[Serializable]
public sealed class RunResultSnapshot
{
    // =========================================================
    // Outcome
    // =========================================================

    [SerializeField]
    private RunOutcome outcome;


    // =========================================================
    // Run Stats
    // =========================================================

    [SerializeField]
    private float survivalTime;


    [SerializeField]
    private int killCount;


    [SerializeField]
    private int playerLevel;


    [SerializeField]
    private int completedMissionCount;


    [SerializeField]
    private int failedMissionCount;


    // =========================================================
    // Loot
    // =========================================================

    [SerializeField]
    private int lootItemCount;


    [SerializeField]
    private int lootTotalValue;


    [SerializeField]
    private List<RunLootSummaryEntry>
        lootSummary =
            new List<RunLootSummaryEntry>();


    // =========================================================
    // Read Only
    // =========================================================

    public RunOutcome Outcome =>
        outcome;


    public bool IsExtractionSuccess =>
        outcome ==
        RunOutcome.ExtractionSuccess;


    public bool IsPlayerDeath =>
        outcome ==
        RunOutcome.PlayerDeath;


    public bool LootSecured =>
        IsExtractionSuccess;


    public float SurvivalTime =>
        survivalTime;


    public int KillCount =>
        killCount;


    public int PlayerLevel =>
        playerLevel;


    public int CompletedMissionCount =>
        completedMissionCount;


    public int FailedMissionCount =>
        failedMissionCount;


    public int LootItemCount =>
        lootItemCount;


    public int LootTotalValue =>
        lootTotalValue;


    public IReadOnlyList<RunLootSummaryEntry>
        LootSummary =>
            lootSummary;


    // =========================================================
    // Construction
    // =========================================================

    private RunResultSnapshot(
        RunOutcome outcome,
        float survivalTime,
        int killCount,
        int playerLevel,
        int completedMissionCount,
        int failedMissionCount,
        int lootItemCount,
        int lootTotalValue,
        List<RunLootSummaryEntry>
            lootSummary
    )
    {
        this.outcome =
            outcome;


        this.survivalTime =
            Mathf.Max(
                0f,
                survivalTime
            );


        this.killCount =
            Mathf.Max(
                0,
                killCount
            );


        this.playerLevel =
            Mathf.Max(
                1,
                playerLevel
            );


        this.completedMissionCount =
            Mathf.Max(
                0,
                completedMissionCount
            );


        this.failedMissionCount =
            Mathf.Max(
                0,
                failedMissionCount
            );


        this.lootItemCount =
            Mathf.Max(
                0,
                lootItemCount
            );


        this.lootTotalValue =
            Mathf.Max(
                0,
                lootTotalValue
            );


        this.lootSummary =
            lootSummary
            ??
            new List<RunLootSummaryEntry>();
    }


    // =========================================================
    // Capture
    // =========================================================

    public static RunResultSnapshot Capture(
        RunOutcome outcome,
        float survivalTime,
        int killCount,
        PlayerExperience playerExperience,
        MissionManager missionManager,
        RunInventory runInventory
    )
    {
        int level =
            playerExperience != null
                ?
                playerExperience.CurrentLevel
                :
                1;


        int completedMissions =
            missionManager != null
                ?
                missionManager
                    .CompletedMissionCount
                :
                0;


        int failedMissions =
            missionManager != null
                ?
                missionManager
                    .FailedMissionCount
                :
                0;


        int totalItemCount =
            0;


        int totalLootValue =
            0;


        List<RunLootSummaryEntry>
            summary =
                BuildLootSummary(
                    runInventory,
                    out totalItemCount,
                    out totalLootValue
                );


        return new RunResultSnapshot(
            outcome,
            survivalTime,
            killCount,
            level,
            completedMissions,
            failedMissions,
            totalItemCount,
            totalLootValue,
            summary
        );
    }


    // =========================================================
    // Loot Snapshot
    // =========================================================

    private static List<RunLootSummaryEntry>
        BuildLootSummary(
            RunInventory runInventory,
            out int totalItemCount,
            out int totalLootValue
        )
    {
        totalItemCount =
            0;


        totalLootValue =
            0;


        List<RunLootSummaryEntry>
            result =
                new List<
                    RunLootSummaryEntry
                >();


        if (runInventory == null
            ||
            runInventory.Backpack == null)
        {
            return result;
        }


        IReadOnlyList<ItemStack> stacks =
            runInventory
                .Backpack
                .Items;


        Dictionary<ItemData, int>
            quantities =
                new Dictionary<
                    ItemData,
                    int
                >();


        // -----------------------------------------------------
        // Copy runtime backpack quantities.
        //
        // Snapshot 不保存 ItemStack 引用。
        // -----------------------------------------------------

        for (int i = 0;
             i < stacks.Count;
             i++)
        {
            ItemStack stack =
                stacks[i];


            if (stack == null
                ||
                stack.IsEmpty
                ||
                stack.Item == null)
            {
                continue;
            }


            ItemData item =
                stack.Item;


            int quantity =
                Mathf.Max(
                    0,
                    stack.Quantity
                );


            if (quantity <= 0)
            {
                continue;
            }


            totalItemCount +=
                quantity;


            totalLootValue +=
                item.BaseValue
                *
                quantity;


            if (quantities.ContainsKey(
                    item
                ))
            {
                quantities[item] +=
                    quantity;
            }
            else
            {
                quantities.Add(
                    item,
                    quantity
                );
            }
        }


        // -----------------------------------------------------
        // Convert to immutable snapshot entries.
        //
        // 从这里开始不再保留 ItemStack。
        // -----------------------------------------------------

        foreach (
            KeyValuePair<ItemData, int>
                pair
            in quantities
        )
        {
            ItemData item =
                pair.Key;


            int quantity =
                pair.Value;


            if (item == null
                ||
                quantity <= 0)
            {
                continue;
            }


            string resolvedItemId =
                string.IsNullOrWhiteSpace(
                    item.ItemId
                )
                    ?
                    item.name
                    :
                    item.ItemId;


            string resolvedDisplayName =
                string.IsNullOrWhiteSpace(
                    item.DisplayName
                )
                    ?
                    item.name
                    :
                    item.DisplayName;


            int entryTotalValue =
                Mathf.Max(
                    0,
                    item.BaseValue
                    *
                    quantity
                );


            result.Add(
                new RunLootSummaryEntry(
                    resolvedItemId,
                    resolvedDisplayName,
                    item.Rarity,
                    quantity,
                    entryTotalValue
                )
            );
        }


        // -----------------------------------------------------
        // Result UI:
        //
        // Legendary -> Epic -> Rare -> ...
        //
        // 同稀有度下价值高的优先。
        // -----------------------------------------------------

        result.Sort(
            CompareLootEntries
        );


        return result;
    }


    private static int CompareLootEntries(
        RunLootSummaryEntry first,
        RunLootSummaryEntry second
    )
    {
        if (ReferenceEquals(
                first,
                second
            ))
        {
            return 0;
        }


        if (first == null)
        {
            return 1;
        }


        if (second == null)
        {
            return -1;
        }


        int rarityComparison =
            second.Rarity
                .CompareTo(
                    first.Rarity
                );


        if (rarityComparison != 0)
        {
            return rarityComparison;
        }


        int valueComparison =
            second.TotalValue
                .CompareTo(
                    first.TotalValue
                );


        if (valueComparison != 0)
        {
            return valueComparison;
        }


        return string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.Ordinal
        );
    }
}