using UnityEngine;


[DisallowMultipleComponent]
public class RunInventoryDebugTester : MonoBehaviour
{
    // =========================================================
    // Test Loot
    // =========================================================

    [Header("Test Loot")]

    [SerializeField]
    private ItemData scrapAlloy;

    [SerializeField]
    private ConsumableItemData fieldBandage;

    [SerializeField]
    private ConsumableItemData medInjector;

    [SerializeField]
    private ItemData energyCrystal;

    [SerializeField]
    private ItemData advancedCircuit;

    [SerializeField]
    private ItemData alienRelic;


    // =========================================================
    // Player
    // =========================================================

    [Header("Player")]

    [SerializeField]
    private PlayerHealth playerHealth;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth =
                FindFirstObjectByType<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            AddDemoLoot();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            DamagePlayerForTest();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            UseBandageForTest();
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            PrintSummary();
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            ClearBackpack();
        }
    }


    // =========================================================
    // Add Demo Loot
    // =========================================================

    [ContextMenu("Test/Add Demo Loot")]
    public void AddDemoLoot()
    {
        RunInventory inventory =
            RunInventory.Instance;

        if (inventory == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "RunInventory.Instance was not found.",
                this
            );

            return;
        }

        bool startedEmpty =
            inventory.TotalItemCount == 0;

        AddItem(
            scrapAlloy,
            27
        );

        AddItem(
            fieldBandage,
            2
        );

        AddItem(
            medInjector,
            1
        );

        AddItem(
            energyCrystal,
            2
        );

        AddItem(
            advancedCircuit,
            1
        );

        AddItem(
            alienRelic,
            1
        );

        PrintSummary();

        if (startedEmpty)
        {
            Check(
                inventory.TotalItemCount == 34,
                "Demo loot total item count = 34."
            );

            Check(
                inventory.TotalLootValue == 3380,
                "Demo loot total value = 3380."
            );

            Check(
                inventory.OccupiedCellCount == 12,
                "Demo loot occupies exactly 12 cells."
            );
        }
    }


    private void AddItem(
        ItemData item,
        int quantity
    )
    {
        if (item == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "A test ItemData reference is missing.",
                this
            );

            return;
        }

        RunInventory inventory =
            RunInventory.Instance;

        bool complete =
            inventory.TryAddItem(
                item,
                quantity,
                out int added,
                out int remaining
            );

        Debug.Log(
            "[RunInventory Test] Add "
            + item.DisplayName
            + " x"
            + quantity
            + " | Complete = "
            + complete
            + " | Added = "
            + added
            + " | Remaining = "
            + remaining,
            this
        );
    }


    // =========================================================
    // Health Test
    // =========================================================

    [ContextMenu("Test/Take 3 Damage")]
    public void DamagePlayerForTest()
    {
        if (playerHealth == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "PlayerHealth was not found.",
                this
            );

            return;
        }

        playerHealth.TakeDamage(3);

        Debug.Log(
            "[RunInventory Test] "
            + "Player HP after test damage: "
            + playerHealth.CurrentHealth
            + "/"
            + playerHealth.MaxHealth,
            this
        );
    }


    // =========================================================
    // Consumable Test
    // =========================================================

    [ContextMenu("Test/Use One Bandage")]
    public void UseBandageForTest()
    {
        if (fieldBandage == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "Field Bandage is missing.",
                this
            );

            return;
        }

        if (playerHealth == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "PlayerHealth was not found.",
                this
            );

            return;
        }

        RunInventory inventory =
            RunInventory.Instance;

        if (inventory == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "RunInventory was not found.",
                this
            );

            return;
        }

        if (!playerHealth.CanRestoreHealth)
        {
            Debug.LogWarning(
                "[RunInventory Test] "
                + "Player cannot restore health right now.",
                this
            );

            return;
        }

        int itemCountBefore =
            inventory.TotalItemCount;

        int lootValueBefore =
            inventory.TotalLootValue;

        int healthBefore =
            playerHealth.CurrentHealth;

        bool removedComplete =
            inventory.TryRemoveItem(
                fieldBandage,
                1,
                out int removed
            );

        if (!removedComplete ||
            removed != 1)
        {
            Debug.LogWarning(
                "[RunInventory Test] "
                + "No Field Bandage available.",
                this
            );

            return;
        }

        playerHealth.RestoreHealth(
            fieldBandage.HealAmount
        );

        int expectedHealth =
            Mathf.Min(
                playerHealth.MaxHealth,
                healthBefore +
                fieldBandage.HealAmount
            );

        Check(
            playerHealth.CurrentHealth ==
            expectedHealth,
            "Bandage restores the correct amount of health."
        );

        Check(
            inventory.TotalItemCount ==
            itemCountBefore - 1,
            "Using Bandage removes exactly one item."
        );

        Check(
            inventory.TotalLootValue ==
            lootValueBefore -
            fieldBandage.BaseValue,
            "Using Bandage removes its value from Run Loot Value."
        );

        PrintSummary();
    }


    // =========================================================
    // Clear Test
    // =========================================================

    [ContextMenu("Test/Clear Backpack")]
    public void ClearBackpack()
    {
        if (RunInventory.Instance == null)
        {
            return;
        }

        RunInventory.Instance
            .ClearRunInventory();

        Check(
            RunInventory.Instance.TotalItemCount == 0,
            "Clear leaves Item Count = 0."
        );

        Check(
            RunInventory.Instance.TotalLootValue == 0,
            "Clear leaves Loot Value = 0."
        );

        Check(
            RunInventory.Instance.OccupiedCellCount == 0,
            "Clear leaves Occupied Cells = 0."
        );
    }


    // =========================================================
    // Summary
    // =========================================================

    [ContextMenu("Test/Print Summary")]
    public void PrintSummary()
    {
        RunInventory inventory =
            RunInventory.Instance;

        if (inventory == null)
        {
            Debug.LogError(
                "[RunInventory Test] "
                + "RunInventory was not found.",
                this
            );

            return;
        }

        Debug.Log(
            "===== RUN INVENTORY SUMMARY =====\n"
            + "Items: "
            + inventory.TotalItemCount
            + "\nLoot Value: "
            + inventory.TotalLootValue
            + "\nOccupied Cells: "
            + inventory.OccupiedCellCount
            + "/30",
            this
        );
    }


    // =========================================================
    // Check
    // =========================================================

    private void Check(
        bool condition,
        string message
    )
    {
        if (condition)
        {
            Debug.Log(
                "[PASS] "
                + message,
                this
            );
        }
        else
        {
            Debug.LogError(
                "[FAIL] "
                + message,
                this
            );
        }
    }
}