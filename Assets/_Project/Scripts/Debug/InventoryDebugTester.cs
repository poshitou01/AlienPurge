using UnityEngine;


public class InventoryDebugTester : MonoBehaviour
{
    // =========================================================
    // Test Items
    // =========================================================

    [Header("Test Items")]

    [SerializeField]
    private ItemData oneByOneItem;

    [SerializeField]
    private ItemData oneByTwoItem;

    [SerializeField]
    private ItemData twoByOneItem;

    [SerializeField]
    private ItemData twoByTwoItem;


    private InventoryGrid inventory;


    // =========================================================
    // Unity
    // =========================================================

    private void Start()
    {
        RunAllTests();
    }


    // =========================================================
    // Main Test
    // =========================================================

    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[InventoryGrid Test] Enter Play Mode first."
            );

            return;
        }

        if (oneByOneItem == null ||
            oneByTwoItem == null ||
            twoByOneItem == null ||
            twoByTwoItem == null)
        {
            Debug.LogError(
                "[InventoryGrid Test] " +
                "One or more test items are missing."
            );

            return;
        }

        inventory =
            new InventoryGrid(
                6,
                5
            );

        Debug.Log(
            "============================================"
        );

        Debug.Log(
            "[InventoryGrid Test] START"
        );


        TestGridSize();

        TestOneByOneStacking();

        TestOneByTwoPlacement();

        TestTwoByOnePlacement();

        TestTwoByTwoPlacement();

        TestOverlap();

        TestOutOfBounds();

        TestRemove();

        TestPartialTransfer();

        TestNoSpace();


        Debug.Log(
            "[InventoryGrid Test] END"
        );

        Debug.Log(
            "============================================"
        );
    }


    // =========================================================
    // Tests
    // =========================================================

    private void TestGridSize()
    {
        Check(
            inventory.Width == 6 &&
            inventory.Height == 5 &&
            inventory.CellCount == 30,
            "6x5 grid contains 30 cells."
        );
    }


    private void TestOneByOneStacking()
    {
        bool complete =
            inventory.TryAddItem(
                oneByOneItem,
                27,
                out int added,
                out int remaining
            );

        Check(
            complete &&
            added == 27 &&
            remaining == 0 &&
            inventory.GetItemCount(oneByOneItem) == 27,
            "1x1 stack item: 27 items added correctly."
        );
    }


    private void TestOneByTwoPlacement()
    {
        bool complete =
            inventory.TryAddItem(
                oneByTwoItem,
                1,
                out int added,
                out int remaining
            );

        Check(
            complete &&
            added == 1 &&
            remaining == 0,
            "1x2 item auto placement succeeds."
        );

        ItemStack stack =
            FindFirstStack(
                oneByTwoItem
            );

        bool cellsCorrect =
            stack != null &&
            inventory.GetOccupant(
                stack.GridX,
                stack.GridY
            ) == stack &&
            inventory.GetOccupant(
                stack.GridX,
                stack.GridY + 1
            ) == stack;

        Check(
            cellsCorrect,
            "1x2 item occupies exactly two vertical cells."
        );
    }


    private void TestTwoByOnePlacement()
    {
        bool complete =
            inventory.TryAddItem(
                twoByOneItem,
                1,
                out int added,
                out int remaining
            );

        Check(
            complete &&
            added == 1 &&
            remaining == 0,
            "2x1 item auto placement succeeds."
        );

        ItemStack stack =
            FindFirstStack(
                twoByOneItem
            );

        bool cellsCorrect =
            stack != null &&
            inventory.GetOccupant(
                stack.GridX,
                stack.GridY
            ) == stack &&
            inventory.GetOccupant(
                stack.GridX + 1,
                stack.GridY
            ) == stack;

        Check(
            cellsCorrect,
            "2x1 item occupies exactly two horizontal cells."
        );
    }


    private void TestTwoByTwoPlacement()
    {
        bool complete =
            inventory.TryAddItem(
                twoByTwoItem,
                1,
                out int added,
                out int remaining
            );

        Check(
            complete &&
            added == 1 &&
            remaining == 0,
            "2x2 item auto placement succeeds."
        );

        ItemStack stack =
            FindFirstStack(
                twoByTwoItem
            );

        bool cellsCorrect =
            stack != null &&
            inventory.GetOccupant(
                stack.GridX,
                stack.GridY
            ) == stack &&
            inventory.GetOccupant(
                stack.GridX + 1,
                stack.GridY
            ) == stack &&
            inventory.GetOccupant(
                stack.GridX,
                stack.GridY + 1
            ) == stack &&
            inventory.GetOccupant(
                stack.GridX + 1,
                stack.GridY + 1
            ) == stack;

        Check(
            cellsCorrect,
            "2x2 item occupies four cells referencing one stack."
        );
    }


    private void TestOverlap()
    {
        ItemStack existing =
            FindFirstStack(
                twoByTwoItem
            );

        if (existing == null)
        {
            Check(
                false,
                "Overlap test could not find 2x2 stack."
            );

            return;
        }

        ItemStack testStack =
            new ItemStack(
                twoByTwoItem,
                1
            );

        bool placed =
            inventory.TryPlaceStack(
                testStack,
                existing.GridX,
                existing.GridY
            );

        Check(
            !placed,
            "Overlapping placement is rejected."
        );
    }


    private void TestOutOfBounds()
    {
        ItemStack testStack =
            new ItemStack(
                twoByTwoItem,
                1
            );

        bool placed =
            inventory.TryPlaceStack(
                testStack,
                5,
                4
            );

        Check(
            !placed,
            "2x2 item cannot extend outside the 6x5 grid."
        );
    }


    private void TestRemove()
    {
        bool complete =
            inventory.TryRemoveItem(
                oneByOneItem,
                3,
                out int removed
            );

        Check(
            complete &&
            removed == 3 &&
            inventory.GetItemCount(oneByOneItem) == 24,
            "Removing 3 items leaves exactly 24."
        );
    }


    private void TestPartialTransfer()
    {
        InventoryGrid smallGrid =
            new InventoryGrid(
                1,
                1
            );

        smallGrid.TryAddItem(
            oneByOneItem,
            18,
            out _,
            out _
        );

        smallGrid.TryAddItem(
            oneByOneItem,
            5,
            out int added,
            out int remaining
        );

        Check(
            added == 2 &&
            remaining == 3 &&
            smallGrid.GetItemCount(oneByOneItem) == 20,
            "Partial transfer preserves overflow: +2 added, 3 remain."
        );
    }


    private void TestNoSpace()
    {
        InventoryGrid smallGrid =
            new InventoryGrid(
                2,
                2
            );

        smallGrid.TryAddItem(
            twoByTwoItem,
            1,
            out _,
            out _
        );

        bool complete =
            smallGrid.TryAddItem(
                oneByOneItem,
                1,
                out int added,
                out int remaining
            );

        Check(
            !complete &&
            added == 0 &&
            remaining == 1,
            "No-space condition returns the original item safely."
        );
    }


    // =========================================================
    // Helpers
    // =========================================================

    private ItemStack FindFirstStack(
        ItemData item
    )
    {
        for (int i = 0;
             i < inventory.Items.Count;
             i++)
        {
            ItemStack stack =
                inventory.Items[i];

            if (stack.Item == item)
            {
                return stack;
            }
        }

        return null;
    }

    private void Check(
        bool condition,
        string message
    )
    {
        if (condition)
        {
            Debug.Log(
                "[PASS] " +
                message
            );
        }
        else
        {
            Debug.LogError(
                "[FAIL] " +
                message
            );
        }
    }
}