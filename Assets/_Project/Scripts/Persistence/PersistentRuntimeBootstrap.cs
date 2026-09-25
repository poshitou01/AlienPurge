using UnityEngine;


public static class PersistentRuntimeBootstrap
{
    // =========================================================
    // Runtime Bootstrap
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad
    )]
    private static void EnsurePersistentRuntime()
    {
        PersistentProfile profile =
            PersistentProfile.Instance;


        SaveManager saveManager =
            SaveManager.Instance;


        // =====================================================
        // Already Available
        // =====================================================

        if (profile != null &&
            saveManager != null)
        {
            saveManager
                .InitializeIfNeeded();

            return;
        }


        // =====================================================
        // Resolve Existing Runtime Root
        // =====================================================

        GameObject root =
            null;


        if (profile != null)
        {
            root =
                profile.gameObject;
        }
        else if (saveManager != null)
        {
            root =
                saveManager.gameObject;
        }


        // =====================================================
        // Create Runtime Root
        // =====================================================

        if (root == null)
        {
            root =
                new GameObject(
                    "[Persistent Runtime]"
                );


            Object.DontDestroyOnLoad(
                root
            );
        }


        // =====================================================
        // Persistent Profile
        // =====================================================

        if (profile == null)
        {
            profile =
                root.AddComponent<
                    PersistentProfile
                >();
        }


        // =====================================================
        // Save Manager
        // =====================================================

        if (saveManager == null)
        {
            saveManager =
                root.AddComponent<
                    SaveManager
                >();
        }


        // =====================================================
        // Initialize
        // =====================================================

        saveManager
            .InitializeIfNeeded();
    }
}