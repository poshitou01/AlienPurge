using System;
using System.IO;
using System.Text;
using UnityEngine;


[DisallowMultipleComponent]
public sealed class SaveManager :
    MonoBehaviour
{
    // =========================================================
    // Singleton
    // =========================================================

    public static SaveManager Instance
    {
        get;
        private set;
    }


    // =========================================================
    // Save Version
    // =========================================================

    public const int CurrentSaveVersion =
        1;


    // =========================================================
    // File
    // =========================================================

    private const string SaveFileName =
        "AlienPurgeSave.json";


    private const string TempFileName =
        "AlienPurgeSave.tmp";


    private const string BackupFileName =
        "AlienPurgeSave.backup.json";


    // =========================================================
    // Runtime
    // =========================================================

    private PersistentProfile profile;

    private ItemCatalog itemCatalog;


    private bool initialized;

    private bool catalogValid;


    // =========================================================
    // Read Only
    // =========================================================

    public bool IsInitialized =>
        initialized;


    public string SavePath =>
        Path.Combine(
            Application.persistentDataPath,
            SaveFileName
        );


    public string TempPath =>
        Path.Combine(
            Application.persistentDataPath,
            TempFileName
        );


    public string BackupPath =>
        Path.Combine(
            Application.persistentDataPath,
            BackupFileName
        );


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


    private void OnApplicationQuit()
    {
        if (!initialized ||
            !catalogValid)
        {
            return;
        }


        Save();
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
    // Initialization
    // =========================================================

    public void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }


        profile =
            PersistentProfile.Instance;


        if (profile == null)
        {
            profile =
                GetComponent<
                    PersistentProfile
                >();
        }


        if (profile == null)
        {
            Debug.LogError(
                "[SaveManager] "
                + "PersistentProfile is missing.",
                this
            );


            return;
        }


        itemCatalog =
            Resources.Load<ItemCatalog>(
                ItemCatalog.ResourcesPath
            );


        if (itemCatalog == null)
        {
            Debug.LogError(
                "[SaveManager] "
                + "ItemCatalog could not be loaded from Resources path: "
                + ItemCatalog.ResourcesPath,
                this
            );


            profile.CreateNewProfile(
                null
            );


            initialized =
                true;


            catalogValid =
                false;


            return;
        }


        catalogValid =
            itemCatalog.ValidateCatalog();


        if (!catalogValid)
        {
            Debug.LogError(
                "[SaveManager] "
                + "ItemCatalog validation failed. "
                + "Persistence Save is disabled "
                + "until Item IDs are fixed.",
                itemCatalog
            );


            profile.CreateNewProfile(
                itemCatalog
            );


            initialized =
                true;


            return;
        }


        LoadOrCreateProfile();


        initialized =
            true;


        Debug.Log(
            "[SaveManager] Initialized."
            + "\nSave Version: "
            + CurrentSaveVersion
            + "\nSave Path: "
            + SavePath,
            this
        );
    }


    // =========================================================
    // Load / Create
    // =========================================================

    public void LoadOrCreateProfile()
    {
        if (profile == null ||
            itemCatalog == null ||
            !catalogValid)
        {
            return;
        }


        // -----------------------------------------------------
        // Primary Save
        // -----------------------------------------------------

        if (File.Exists(
                SavePath
            ))
        {
            if (TryReadSaveFile(
                    SavePath,
                    out SaveGameData data
                ))
            {
                profile.LoadFromSaveData(
                    data,
                    itemCatalog
                );


                Debug.Log(
                    "[SaveManager] "
                    + "Loaded primary save.",
                    this
                );


                return;
            }


            QuarantineBrokenPrimarySave();
        }


        // -----------------------------------------------------
        // Backup Recovery
        // -----------------------------------------------------

        if (File.Exists(
                BackupPath
            ))
        {
            if (TryReadSaveFile(
                    BackupPath,
                    out SaveGameData backupData
                ))
            {
                profile.LoadFromSaveData(
                    backupData,
                    itemCatalog
                );


                Debug.LogWarning(
                    "[SaveManager] "
                    + "Primary save was unavailable or invalid. "
                    + "Recovered profile from backup.",
                    this
                );


                Save();


                return;
            }
        }


        // -----------------------------------------------------
        // New Profile
        // -----------------------------------------------------

        CreateNewProfile();
    }


    public void CreateNewProfile()
    {
        if (profile == null)
        {
            return;
        }


        profile.CreateNewProfile(
            itemCatalog
        );


        Debug.Log(
            "[SaveManager] "
            + "No valid save found. "
            + "Created new profile.",
            this
        );


        Save();
    }


    // =========================================================
    // Save
    // =========================================================

    public bool Save()
    {
        if (!catalogValid)
        {
            Debug.LogError(
                "[SaveManager] "
                + "Save aborted because ItemCatalog is invalid.",
                this
            );


            return false;
        }


        if (profile == null ||
            !profile.IsInitialized)
        {
            Debug.LogError(
                "[SaveManager] "
                + "Save aborted because "
                + "PersistentProfile is not initialized.",
                this
            );


            return false;
        }


        try
        {
            SaveGameData data =
                profile.BuildSaveData(
                    CurrentSaveVersion
                );


            string json =
                JsonUtility.ToJson(
                    data,
                    true
                );


            Directory.CreateDirectory(
                Application.persistentDataPath
            );


            // -------------------------------------------------
            // Stage 1:
            // Write complete new save to temp.
            // -------------------------------------------------

            File.WriteAllText(
                TempPath,
                json,
                new UTF8Encoding(
                    false
                )
            );


            // -------------------------------------------------
            // Stage 2:
            // Preserve previous valid save.
            // -------------------------------------------------

            if (File.Exists(
                    SavePath
                ))
            {
                File.Copy(
                    SavePath,
                    BackupPath,
                    true
                );
            }


            // -------------------------------------------------
            // Stage 3:
            // Promote temp to primary.
            // -------------------------------------------------

            if (File.Exists(
                    SavePath
                ))
            {
                File.Delete(
                    SavePath
                );
            }


            File.Move(
                TempPath,
                SavePath
            );


            Debug.Log(
                "[SaveManager] Save complete."
                + "\nPath: "
                + SavePath,
                this
            );


            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SaveManager] Save failed."
                + "\n"
                + exception,
                this
            );


            TryDeleteTempFile();


            return false;
        }
    }


    // =========================================================
    // Read
    // =========================================================

    private bool TryReadSaveFile(
        string path,
        out SaveGameData data
    )
    {
        data =
            null;


        try
        {
            string json =
                File.ReadAllText(
                    path,
                    Encoding.UTF8
                );


            if (string.IsNullOrWhiteSpace(
                    json
                ))
            {
                throw new InvalidDataException(
                    "Save file is empty."
                );
            }


            SaveGameData loaded =
                JsonUtility.FromJson<
                    SaveGameData
                >(
                    json
                );


            if (loaded == null)
            {
                throw new InvalidDataException(
                    "JsonUtility returned null."
                );
            }


            loaded.EnsureCollections();


            // -------------------------------------------------
            // Version
            // -------------------------------------------------

            if (loaded.saveVersion !=
                CurrentSaveVersion)
            {
                throw new InvalidDataException(
                    "Unsupported save version. "
                    + "Found: "
                    + loaded.saveVersion
                    + ", Expected: "
                    + CurrentSaveVersion
                );
            }


            data =
                loaded;


            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SaveManager] "
                + "Failed to load save file:"
                + "\n"
                + path
                + "\n"
                + exception,
                this
            );


            return false;
        }
    }


    // =========================================================
    // Broken Save
    // =========================================================

    private void QuarantineBrokenPrimarySave()
    {
        if (!File.Exists(
                SavePath
            ))
        {
            return;
        }


        try
        {
            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss"
                );


            string brokenPath =
                Path.Combine(
                    Application
                        .persistentDataPath,
                    "AlienPurgeSave.corrupt_"
                    + timestamp
                    + ".json"
                );


            File.Move(
                SavePath,
                brokenPath
            );


            Debug.LogWarning(
                "[SaveManager] "
                + "Broken save moved to:"
                + "\n"
                + brokenPath,
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[SaveManager] "
                + "Could not quarantine broken save."
                + "\n"
                + exception,
                this
            );
        }
    }


    // =========================================================
    // Utility
    // =========================================================

    private void TryDeleteTempFile()
    {
        try
        {
            if (File.Exists(
                    TempPath
                ))
            {
                File.Delete(
                    TempPath
                );
            }
        }
        catch
        {
            // Debug cleanup only.
        }
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Force Save"
    )]
    private void DebugForceSave()
    {
        Save();
    }


    [ContextMenu(
        "Debug/Force Load"
    )]
    private void DebugForceLoad()
    {
        if (!initialized)
        {
            InitializeIfNeeded();

            return;
        }


        LoadOrCreateProfile();
    }


    [ContextMenu(
        "Debug/Print Save Path"
    )]
    private void DebugPrintSavePath()
    {
        Debug.Log(
            "[SaveManager] Save Path:"
            + "\n"
            + SavePath,
            this
        );
    }


    [ContextMenu(
        "Debug/Delete Save File"
    )]
    private void DebugDeleteSaveFile()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        try
        {
            if (File.Exists(
                    SavePath
                ))
            {
                File.Delete(
                    SavePath
                );
            }


            if (File.Exists(
                    BackupPath
                ))
            {
                File.Delete(
                    BackupPath
                );
            }


            if (File.Exists(
                    TempPath
                ))
            {
                File.Delete(
                    TempPath
                );
            }


            Debug.Log(
                "[SaveManager] "
                + "Debug save files deleted.",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[SaveManager] "
                + "Delete save failed."
                + "\n"
                + exception,
                this
            );
        }

#else

        Debug.LogWarning(
            "[SaveManager] "
            + "Delete Save is only available "
            + "in Editor or Development Build.",
            this
        );

#endif
    }
}