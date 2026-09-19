using System;
using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class LootContainer :
    InteractableBase
{
    // =========================================================
    // Runtime Generated Loot
    // =========================================================

    private class GeneratedLootRecord
    {
        public ItemData Item;

        public int Quantity;

        public int FirstRollOrder;


        public GeneratedLootRecord(
            ItemData item,
            int quantity,
            int firstRollOrder
        )
        {
            Item =
                item;

            Quantity =
                quantity;

            FirstRollOrder =
                firstRollOrder;
        }
    }


    // =========================================================
    // Identity
    // =========================================================

    [Header("Container")]

    [SerializeField]
    private string displayName =
        "FIELD SUPPLY CRATE";


    // =========================================================
    // Loot
    // =========================================================

    [Header("Loot")]

    [SerializeField]
    private LootTableData lootTable;


    // =========================================================
    // Grid
    // =========================================================

    [Header("Container Grid")]

    [Min(1)]
    [SerializeField]
    private int gridWidth = 4;

    [Min(1)]
    [SerializeField]
    private int gridHeight = 3;


    // =========================================================
    // Search
    // =========================================================

    [Header("Search Settings")]

    [Min(0f)]
    [SerializeField]
    private float firstOpenDuration =
        0.45f;

    [Min(0.1f)]
    [SerializeField]
    private float cancelDistance =
        2.4f;


    [Header("Search Time By Rarity")]

    [Min(0.01f)]
    [SerializeField]
    private float commonSearchTime =
        0.60f;

    [Min(0.01f)]
    [SerializeField]
    private float uncommonSearchTime =
        0.90f;

    [Min(0.01f)]
    [SerializeField]
    private float rareSearchTime =
        1.40f;

    [Min(0.01f)]
    [SerializeField]
    private float epicSearchTime =
        2.10f;

    [Min(0.01f)]
    [SerializeField]
    private float legendarySearchTime =
        3.00f;


    // =========================================================
    // Runtime
    // =========================================================

    private InventoryGrid containerGrid;


    private readonly List<
        LootContainerSearchEntry
    > searchEntries =
        new List<
            LootContainerSearchEntry
        >();


    private LootContainerState state =
        LootContainerState.Closed;


    private PlayerInteractor
        activeInteractor;


    private LootContainerSearchEntry
        currentSearchEntry;


    private bool lootGenerated;

    private bool hasBeenOpened;


    private float openingElapsed;

    private float itemSearchElapsed;

    private float currentItemSearchDuration;


    // =========================================================
    // Events
    // =========================================================

    public event Action<LootContainer>
        SearchStateChanged;

    public event Action<LootContainer>
        LootChanged;


    // =========================================================
    // Read Only Access
    // =========================================================

    public string DisplayName =>
        displayName;


    public int GridWidth =>
        gridWidth;


    public int GridHeight =>
        gridHeight;


    public InventoryGrid ContainerGrid =>
        containerGrid;


    public IReadOnlyList<
        LootContainerSearchEntry
    > SearchEntries =>
        searchEntries;


    public LootContainerState State =>
        state;


    public LootContainerSearchEntry
        CurrentSearchEntry =>
            currentSearchEntry;


    public float CurrentSearchProgress01
    {
        get
        {
            if (state ==
                LootContainerState.Opening)
            {
                if (firstOpenDuration <=
                    0f)
                {
                    return 1f;
                }


                return
                    Mathf.Clamp01(
                        openingElapsed /
                        firstOpenDuration
                    );
            }


            if (state ==
                    LootContainerState.Searching
                &&
                currentSearchEntry != null)
            {
                if (currentItemSearchDuration <=
                    0f)
                {
                    return 1f;
                }


                return
                    Mathf.Clamp01(
                        itemSearchElapsed /
                        currentItemSearchDuration
                    );
            }


            if (state ==
                LootContainerState.SearchComplete)
            {
                return 1f;
            }


            return 0f;
        }
    }


    public bool IsBeingViewed =>
        activeInteractor != null;


    // =========================================================
    // Unity
    // =========================================================

    private void Update()
    {
        if (activeInteractor == null)
        {
            return;
        }


        if (!CanContinueSearch())
        {
            CancelAndCloseSearch();

            return;
        }


        switch (state)
        {
            case LootContainerState.Opening:

                UpdateOpening();

                break;


            case LootContainerState.Searching:

                UpdateItemSearch();

                break;
        }
    }


    private void OnDisable()
    {
        if (activeInteractor != null)
        {
            CancelAndCloseSearch();
        }
    }


    private void OnValidate()
    {
        gridWidth =
            Mathf.Max(
                1,
                gridWidth
            );

        gridHeight =
            Mathf.Max(
                1,
                gridHeight
            );


        firstOpenDuration =
            Mathf.Max(
                0f,
                firstOpenDuration
            );

        cancelDistance =
            Mathf.Max(
                0.1f,
                cancelDistance
            );


        commonSearchTime =
            Mathf.Max(
                0.01f,
                commonSearchTime
            );

        uncommonSearchTime =
            Mathf.Max(
                0.01f,
                uncommonSearchTime
            );

        rareSearchTime =
            Mathf.Max(
                0.01f,
                rareSearchTime
            );

        epicSearchTime =
            Mathf.Max(
                0.01f,
                epicSearchTime
            );

        legendarySearchTime =
            Mathf.Max(
                0.01f,
                legendarySearchTime
            );
    }


    // =========================================================
    // Interaction
    // =========================================================

    public override bool CanInteract(
        PlayerInteractor interactor
    )
    {
        return
            base.CanInteract(
                interactor
            );
    }


    public override void Interact(
        PlayerInteractor interactor
    )
    {
        if (interactor == null)
        {
            return;
        }


        if (activeInteractor ==
            interactor)
        {
            CancelAndCloseSearch();

            return;
        }


        if (activeInteractor != null)
        {
            return;
        }


        OpenSearch(
            interactor
        );
    }


    // =========================================================
    // Open
    // =========================================================

    private void OpenSearch(
        PlayerInteractor interactor
    )
    {
        EnsureLootGenerated();


        activeInteractor =
            interactor;


        openingElapsed = 0f;

        itemSearchElapsed = 0f;

        currentItemSearchDuration = 0f;

        currentSearchEntry = null;


        if (!hasBeenOpened)
        {
            hasBeenOpened = true;


            SetState(
                LootContainerState.Opening
            );
        }
        else
        {
            ResumeSearchAfterOpen();
        }


        if (LootSearchPanelController
                .Instance != null)
        {
            LootSearchPanelController
                .Instance
                .OpenContainer(
                    this
                );
        }
    }


    private void ResumeSearchAfterOpen()
    {
        if (HasUnknownItems())
        {
            SetState(
                LootContainerState.Searching
            );


            BeginNextSearchItem();
        }
        else
        {
            SetState(
                LootContainerState.SearchComplete
            );
        }
    }


    // =========================================================
    // Opening
    // =========================================================

    private void UpdateOpening()
    {
        openingElapsed +=
            Time.deltaTime;


        if (openingElapsed <
            firstOpenDuration)
        {
            return;
        }


        openingElapsed =
            firstOpenDuration;


        SetState(
            LootContainerState.Searching
        );


        BeginNextSearchItem();
    }


    // =========================================================
    // Search
    // =========================================================

    private void BeginNextSearchItem()
    {
        currentSearchEntry =
            FindNextUnknownEntry();


        itemSearchElapsed = 0f;


        if (currentSearchEntry == null)
        {
            currentItemSearchDuration = 0f;


            SetState(
                LootContainerState.SearchComplete
            );


            return;
        }


        currentSearchEntry
            .BeginSearching();


        currentItemSearchDuration =
            GetSearchDuration(
                currentSearchEntry
                    .Stack
                    .Item
                    .Rarity
            );


        NotifySearchStateChanged();
    }


    private void UpdateItemSearch()
    {
        if (currentSearchEntry == null)
        {
            BeginNextSearchItem();

            return;
        }


        itemSearchElapsed +=
            Time.deltaTime;


        if (itemSearchElapsed <
            currentItemSearchDuration)
        {
            return;
        }


        itemSearchElapsed =
            currentItemSearchDuration;


        currentSearchEntry
            .Identify();


        currentSearchEntry = null;


        NotifySearchStateChanged();


        BeginNextSearchItem();
    }


    private LootContainerSearchEntry
        FindNextUnknownEntry()
    {
        for (int i = 0;
             i < searchEntries.Count;
             i++)
        {
            LootContainerSearchEntry
                entry =
                    searchEntries[i];


            if (entry == null ||
                entry.IsTaken)
            {
                continue;
            }


            if (entry.State ==
                LootSearchState.Unknown)
            {
                return entry;
            }
        }


        return null;
    }


    private bool HasUnknownItems()
    {
        return
            FindNextUnknownEntry() !=
            null;
    }


    // =========================================================
    // Search Duration
    // =========================================================

    private float GetSearchDuration(
        ItemRarity rarity
    )
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:

                return
                    uncommonSearchTime;


            case ItemRarity.Rare:

                return
                    rareSearchTime;


            case ItemRarity.Epic:

                return
                    epicSearchTime;


            case ItemRarity.Legendary:

                return
                    legendarySearchTime;


            default:

                return
                    commonSearchTime;
        }
    }


    // =========================================================
    // Generate Loot
    // =========================================================

    private void EnsureLootGenerated()
    {
        if (lootGenerated)
        {
            return;
        }


        lootGenerated = true;


        containerGrid =
            new InventoryGrid(
                gridWidth,
                gridHeight
            );


        searchEntries.Clear();


        if (lootTable == null)
        {
            Debug.LogWarning(
                "[Loot Container] "
                + name
                + " has no LootTable assigned.",
                this
            );


            LootChanged?.Invoke(
                this
            );


            return;
        }


        List<GeneratedLootRecord>
            generatedLoot =
                GenerateLootRecords();


        PlaceGeneratedLoot(
            generatedLoot
        );


        BuildSearchEntries();


        LootChanged?.Invoke(
            this
        );
    }


    // =========================================================
    // Roll Loot
    // =========================================================

    private List<GeneratedLootRecord>
        GenerateLootRecords()
    {
        List<GeneratedLootRecord>
            generated =
                new List<
                    GeneratedLootRecord
                >();


        Dictionary<
            ItemData,
            GeneratedLootRecord
        > lookup =
            new Dictionary<
                ItemData,
                GeneratedLootRecord
            >();


        int rollCount =
            lootTable
                .GetRandomRollCount();


        for (int rollIndex = 0;
             rollIndex < rollCount;
             rollIndex++)
        {
            LootTableEntry entry =
                RollEligibleEntry(
                    lookup
                );


            if (entry == null)
            {
                break;
            }


            ItemData item =
                entry.Item;


            int rolledQuantity =
                entry.RollQuantity();


            if (!lookup.TryGetValue(
                    item,
                    out GeneratedLootRecord
                        record
                ))
            {
                int quantity =
                    Mathf.Min(
                        item.MaxStackSize,
                        rolledQuantity
                    );


                if (quantity <= 0)
                {
                    continue;
                }


                record =
                    new GeneratedLootRecord(
                        item,
                        quantity,
                        rollIndex
                    );


                lookup.Add(
                    item,
                    record
                );


                generated.Add(
                    record
                );
            }
            else
            {
                int freeStackSpace =
                    Mathf.Max(
                        0,
                        item.MaxStackSize -
                        record.Quantity
                    );


                int amountToAdd =
                    Mathf.Min(
                        freeStackSpace,
                        rolledQuantity
                    );


                record.Quantity +=
                    amountToAdd;
            }
        }


        return generated;
    }


    // =========================================================
    // Weighted Roll
    // =========================================================

    private LootTableEntry
        RollEligibleEntry(
            Dictionary<
                ItemData,
                GeneratedLootRecord
            > generatedLookup
        )
    {
        IReadOnlyList<
            LootTableEntry
        > entries =
            lootTable.Entries;


        float totalWeight = 0f;


        for (int i = 0;
             i < entries.Count;
             i++)
        {
            LootTableEntry entry =
                entries[i];


            if (!IsEntryEligible(
                    entry,
                    generatedLookup
                ))
            {
                continue;
            }


            totalWeight +=
                entry.Weight;
        }


        if (totalWeight <= 0f)
        {
            return null;
        }


        float randomValue =
            UnityEngine.Random.Range(
                0f,
                totalWeight
            );


        float cumulativeWeight = 0f;


        for (int i = 0;
             i < entries.Count;
             i++)
        {
            LootTableEntry entry =
                entries[i];


            if (!IsEntryEligible(
                    entry,
                    generatedLookup
                ))
            {
                continue;
            }


            cumulativeWeight +=
                entry.Weight;


            if (randomValue <=
                cumulativeWeight)
            {
                return entry;
            }
        }


        return null;
    }


    // =========================================================
    // Eligibility
    // =========================================================

    private bool IsEntryEligible(
        LootTableEntry entry,
        Dictionary<
            ItemData,
            GeneratedLootRecord
        > generatedLookup
    )
    {
        if (entry == null ||
            entry.Item == null ||
            entry.Weight <= 0f)
        {
            return false;
        }


        ItemData item =
            entry.Item;


        if (!CanEverFitInContainer(
                item
            ))
        {
            return false;
        }


        if (generatedLookup.TryGetValue(
                item,
                out GeneratedLootRecord record
            ))
        {
            if (record.Quantity >=
                item.MaxStackSize)
            {
                return false;
            }
        }


        return true;
    }


    private bool CanEverFitInContainer(
        ItemData item
    )
    {
        if (item == null)
        {
            return false;
        }


        return
            item.GridWidth <=
                gridWidth
            &&
            item.GridHeight <=
                gridHeight;
    }


    // =========================================================
    // Place Generated Loot
    // =========================================================

    private void PlaceGeneratedLoot(
        List<GeneratedLootRecord>
            generatedLoot
    )
    {
        if (generatedLoot == null ||
            generatedLoot.Count <= 0)
        {
            return;
        }


        // 大物品优先摆放，减少 First Fit
        // 导致的碎片化。
        generatedLoot.Sort(
            (
                first,
                second
            ) =>
            {
                int firstArea =
                    first.Item.GridArea;

                int secondArea =
                    second.Item.GridArea;


                int areaComparison =
                    secondArea.CompareTo(
                        firstArea
                    );


                if (areaComparison != 0)
                {
                    return areaComparison;
                }


                return
                    first.FirstRollOrder
                        .CompareTo(
                            second.FirstRollOrder
                        );
            }
        );


        for (int i = 0;
             i < generatedLoot.Count;
             i++)
        {
            GeneratedLootRecord record =
                generatedLoot[i];


            if (record == null ||
                record.Item == null ||
                record.Quantity <= 0)
            {
                continue;
            }


            if (!containerGrid
                    .FindFirstAvailablePosition(
                        record.Item,
                        out int x,
                        out int y
                    ))
            {
                Debug.Log(
                    "[Loot Container] "
                    + name
                    + " could not fit "
                    + record.Item.DisplayName
                    + " in its grid.",
                    this
                );


                continue;
            }


            ItemStack stack =
                new ItemStack(
                    record.Item,
                    record.Quantity
                );


            containerGrid.TryPlaceStack(
                stack,
                x,
                y
            );
        }
    }


    // =========================================================
    // Build Search Entries
    // =========================================================

    private void BuildSearchEntries()
    {
        searchEntries.Clear();


        if (containerGrid == null)
        {
            return;
        }


        for (int i = 0;
             i <
             containerGrid.Items.Count;
             i++)
        {
            ItemStack stack =
                containerGrid.Items[i];


            if (stack == null ||
                stack.IsEmpty)
            {
                continue;
            }


            searchEntries.Add(
                new LootContainerSearchEntry(
                    stack
                )
            );
        }


        SortSearchEntriesByGridPosition();
    }


    // =========================================================
    // Search Order
    // =========================================================

    private void SortSearchEntriesByGridPosition()
    {
        searchEntries.Sort(
            (
                first,
                second
            ) =>
            {
                if (first == null &&
                    second == null)
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


                ItemStack firstStack =
                    first.Stack;

                ItemStack secondStack =
                    second.Stack;


                if (firstStack == null &&
                    secondStack == null)
                {
                    return 0;
                }


                if (firstStack == null)
                {
                    return 1;
                }


                if (secondStack == null)
                {
                    return -1;
                }


                int yComparison =
                    firstStack.GridY
                        .CompareTo(
                            secondStack.GridY
                        );


                if (yComparison != 0)
                {
                    return yComparison;
                }


                return
                    firstStack.GridX
                        .CompareTo(
                            secondStack.GridX
                        );
            }
        );
    }


    // =========================================================
    // Take Item
    // =========================================================

    public bool TryTakeEntry(
        LootContainerSearchEntry entry
    )
    {
        if (entry == null ||
            entry.State !=
                LootSearchState.Identified ||
            entry.Stack == null ||
            entry.Stack.IsEmpty)
        {
            return false;
        }


        RunInventory runInventory =
            RunInventory.Instance;


        if (runInventory == null)
        {
            return false;
        }


        ItemData item =
            entry.Stack.Item;


        int quantityBefore =
            entry.Stack.Quantity;


        runInventory.TryAddItem(
            item,
            quantityBefore,
            out int addedQuantity,
            out int remainingQuantity
        );


        if (addedQuantity <= 0)
        {
            if (LootSearchPanelController
                    .Instance != null)
            {
                LootSearchPanelController
                    .Instance
                    .ShowTemporaryMessage(
                        "BACKPACK FULL"
                    );
            }


            return false;
        }


        if (remainingQuantity <= 0)
        {
            if (containerGrid != null)
            {
                containerGrid.DetachStack(
                    entry.Stack
                );
            }


            entry.MarkTaken();
        }
        else
        {
            entry.Stack.Remove(
                addedQuantity
            );
        }


        LootChanged?.Invoke(
            this
        );


        Debug.Log(
            "[Loot Container] Took "
            + item.DisplayName
            + " x"
            + addedQuantity
            + ". Remaining in container: "
            + remainingQuantity,
            this
        );


        return true;
    }


    // =========================================================
    // Cancel / Close
    // =========================================================

    private bool CanContinueSearch()
    {
        if (activeInteractor == null)
        {
            return false;
        }


        if (GameManager.Instance != null &&
            !GameManager.Instance.IsPlaying)
        {
            return false;
        }


        Vector2 offset =
            activeInteractor
                .transform
                .position
            -
            transform.position;


        return
            offset.sqrMagnitude
            <=
            cancelDistance *
            cancelDistance;
    }

    // =========================================================
    // External Close
    // =========================================================

    /// <summary>
    /// 当 Backpack / Pause / Upgrade / Module /
    /// GameOver / Victory 等外部状态接管游戏时，
    /// 安全结束当前容器搜索。
    ///
    /// 已经 Identified 的物品保持不变；
    /// 当前正在搜索的物品恢复为 Unknown，
    /// 本次未完成搜索进度作废。
    /// </summary>
    public void CancelSearchFromExternalState()
    {
        if (activeInteractor == null)
        {
            return;
        }

        CancelAndCloseSearch();
    }

    private void CancelAndCloseSearch()
    {
        if (currentSearchEntry != null &&
            currentSearchEntry.State ==
                LootSearchState.Searching)
        {
            currentSearchEntry
                .CancelSearching();
        }


        currentSearchEntry = null;

        itemSearchElapsed = 0f;

        currentItemSearchDuration = 0f;

        openingElapsed = 0f;


        SetState(
            LootContainerState.Closed
        );


        activeInteractor = null;


        if (LootSearchPanelController
                .Instance != null)
        {
            LootSearchPanelController
                .Instance
                .CloseContainer(
                    this
                );
        }
    }


    // =========================================================
    // State
    // =========================================================

    private void SetState(
        LootContainerState newState
    )
    {
        if (state == newState)
        {
            return;
        }


        state =
            newState;


        NotifySearchStateChanged();
    }


    private void NotifySearchStateChanged()
    {
        SearchStateChanged?.Invoke(
            this
        );
    }
}