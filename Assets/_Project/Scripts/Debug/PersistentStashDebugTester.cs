using UnityEngine;


[DisallowMultipleComponent]
public sealed class PersistentStashDebugTester :
    MonoBehaviour
{
    // =========================================================
    // Layout Test
    // =========================================================

    [ContextMenu(
        "Debug/Add Grid Layout Test Set"
    )]
    private void AddGridLayoutTestSet()
    {
        PersistentProfile profile =
            PersistentProfile.Instance;


        if (!CanUseProfile(
                profile
            ))
        {
            return;
        }


        DebugStore(
            profile,
            "scrap_alloy",
            25
        );


        DebugStore(
            profile,
            "energy_crystal",
            3
        );


        DebugStore(
            profile,
            "advanced_circuit",
            2
        );


        DebugStore(
            profile,
            "alien_relic",
            1
        );


        Save();


        PrintSummary(
            profile
        );
    }


    // =========================================================
    // Fill
    // =========================================================

    [ContextMenu(
        "Debug/Fill Stash With 1x1 Items"
    )]
    private void FillStash()
    {
        PersistentProfile profile =
            PersistentProfile.Instance;


        if (!CanUseProfile(
                profile
            ))
        {
            return;
        }


        if (!profile.ItemCatalog.TryGetItem(
                "reinforced_fiber",
                out ItemData item
            ))
        {
            Debug.LogError(
                "[Stash Debug] "
                + "reinforced_fiber not found.",
                this
            );

            return;
        }


        int safety =
            PersistentStash.Width
            *
            PersistentStash.Height
            *
            2;


        int totalAdded =
            0;


        for (int i = 0;
             i < safety;
             i++)
        {
            profile.Stash.TryStore(
                item,
                1,
                out int added,
                out int remaining
            );


            totalAdded +=
                added;


            if (added <= 0 ||
                remaining > 0)
            {
                break;
            }
        }


        Save();


        Debug.Log(
            "[Stash Debug] Fill complete."
            + "\nAdded: "
            + totalAdded
            + "\nOccupied: "
            + profile.Stash.OccupiedCellCount
            + "/100",
            this
        );
    }


    // =========================================================
    // Clear
    // =========================================================

    [ContextMenu(
        "Debug/Clear Stash"
    )]
    private void ClearStash()
    {
        PersistentProfile profile =
            PersistentProfile.Instance;


        if (!CanUseProfile(
                profile
            ))
        {
            return;
        }


        profile.Stash.Clear();


        Save();


        Debug.Log(
            "[Stash Debug] Stash cleared.",
            this
        );
    }


    // =========================================================
    // Print
    // =========================================================

    [ContextMenu(
        "Debug/Print Stash Summary"
    )]
    private void DebugPrintSummary()
    {
        PersistentProfile profile =
            PersistentProfile.Instance;


        if (!CanUseProfile(
                profile
            ))
        {
            return;
        }


        PrintSummary(
            profile
        );
    }


    // =========================================================
    // Store Helper
    // =========================================================

    private void DebugStore(
        PersistentProfile profile,
        string itemId,
        int quantity
    )
    {
        if (!profile.ItemCatalog.TryGetItem(
                itemId,
                out ItemData item
            ))
        {
            Debug.LogError(
                "[Stash Debug] "
                + "Item not found: "
                + itemId,
                this
            );

            return;
        }


        bool complete =
            profile.Stash.TryStore(
                item,
                quantity,
                out int stored,
                out int remaining
            );


        Debug.Log(
            "[Stash Debug] "
            + itemId
            + " requested="
            + quantity
            + ", stored="
            + stored
            + ", remaining="
            + remaining
            + ", complete="
            + complete,
            this
        );
    }


    // =========================================================
    // Guard
    // =========================================================

    private bool CanUseProfile(
        PersistentProfile profile
    )
    {
        if (profile == null ||
            !profile.IsInitialized ||
            profile.Stash == null ||
            profile.ItemCatalog == null)
        {
            Debug.LogError(
                "[Stash Debug] "
                + "Persistent Profile is not ready.",
                this
            );

            return false;
        }


        return true;
    }


    // =========================================================
    // Save
    // =========================================================

    private void Save()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }
    }


    // =========================================================
    // Summary
    // =========================================================

    private void PrintSummary(
        PersistentProfile profile
    )
    {
        Debug.Log(
            "===== PERSISTENT STASH ====="
            + "\nStacks: "
            + profile.Stash.Grid.Items.Count
            + "\nItems: "
            + profile.Stash.TotalItemCount
            + "\nValue: "
            + profile.Stash.TotalValue
            + "\nOccupied Cells: "
            + profile.Stash.OccupiedCellCount
            + "/100",
            this
        );
    }
}