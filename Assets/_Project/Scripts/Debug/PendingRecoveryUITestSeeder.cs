
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PendingRecoveryUITestSeeder : MonoBehaviour
{
    [ContextMenu("Debug/Add Pending UI Test Items")]
    private void AddTestItems()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[Pending UI Test] Enter Play Mode first."
            );
            return;
        }

        PersistentProfile profile =
            PersistentProfile.Instance;

        if (profile == null ||
            !profile.IsInitialized ||
            profile.ItemCatalog == null)
        {
            Debug.LogError(
                "[Pending UI Test] Profile is not ready."
            );
            return;
        }

        // Common
        AddItem(profile, "scrap_alloy", 5);
        AddItem(profile, "alien_tissue", 3);

        // Rare
        AddItem(profile, "energy_crystal", 2);
        AddItem(profile, "advanced_circuit", 1);

        // Epic
        AddItem(profile, "prototype_capacitor", 1);

        // Legendary
        AddItem(profile, "alien_relic", 1);

        Debug.Log(
            "[Pending UI Test] Items added.\n" +
            "Pending Count: " +
            profile.PendingRecoveryItemCount +
            "\nPending Value: " +
            profile.PendingRecoveryTotalValue
        );

        // Deliberately no Save() here.
        // SaveManager can still save on exit,
        // so the original save must be backed up.
#else
        Debug.LogWarning(
            "Pending UI test is Editor-only."
        );
#endif
    }

    private void AddItem(
        PersistentProfile profile,
        string itemId,
        int quantity
    )
    {
        if (!profile.ItemCatalog.TryGetItem(
                itemId,
                out ItemData item))
        {
            Debug.LogWarning(
                "[Pending UI Test] Missing Item: " + itemId
            );
            return;
        }

        profile.AddPendingRecovery(
            item,
            quantity
        );
    }
}
