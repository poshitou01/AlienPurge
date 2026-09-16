using UnityEngine;


[DisallowMultipleComponent]
public class WeaponEvolutionController : MonoBehaviour
{
    // =========================================================
    // Evolution Definitions
    // =========================================================

    [Header("Evolution Definitions")]

    [Tooltip(
        "当前版本支持的 Signature Evolutions。"
        + "正常顺序建议：Thunder Piercer、Cluster Burst。"
    )]
    [SerializeField]
    private WeaponEvolutionData[] evolutionDefinitions;


    // =========================================================
    // Runtime Debug
    // =========================================================

    [Header("Runtime Evolution State")]

    [SerializeField]
    private WeaponEvolutionType currentEvolution =
        WeaponEvolutionType.None;

    [SerializeField]
    private WeaponEvolutionData currentEvolutionData;


    // =========================================================
    // Dependencies
    // =========================================================

    private PlayerWeaponModifiers weaponModifiers;


    // =========================================================
    // Public Read Only Access
    // =========================================================

    public WeaponEvolutionType CurrentEvolution =>
        currentEvolution;

    public WeaponEvolutionData CurrentEvolutionData =>
        currentEvolutionData;

    public bool HasEvolution =>
        currentEvolution
        != WeaponEvolutionType.None;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        weaponModifiers =
            GetComponent<PlayerWeaponModifiers>();

        if (weaponModifiers == null)
        {
            Debug.LogError(
                "WeaponEvolutionController: "
                + "PlayerWeaponModifiers was not found "
                + "on the Player.",
                this
            );
        }

        ResetRuntimeState();
    }


    private void Start()
    {
        ValidateDefinitions();
    }


    // =========================================================
    // Eligibility
    // =========================================================

    public bool CanEvolve()
    {
        return TryGetEligibleEvolution(
            out _
        );
    }


    public bool TryGetEligibleEvolution(
        out WeaponEvolutionData evolutionData)
    {
        evolutionData = null;

        if (HasEvolution)
        {
            return false;
        }

        EnsureDependencies();

        if (weaponModifiers == null)
        {
            return false;
        }


        // Phase36 的正式规则：
        // 当前正常 Run 一局最多持有两个不同 Core Module。
        //
        // 这里要求“恰好两个已解锁 Module”，
        // 可以避免 Debug/未来状态下四个模块同时存在时
        // 同时满足多个 Evolution Pair。
        if (GetOwnedCoreModuleCount() != 2)
        {
            return false;
        }


        if (evolutionDefinitions == null)
        {
            return false;
        }

        for (int i = 0;
             i < evolutionDefinitions.Length;
             i++)
        {
            WeaponEvolutionData data =
                evolutionDefinitions[i];

            if (!IsDefinitionUsable(data))
            {
                continue;
            }

            if (!HasRequiredPair(data))
            {
                continue;
            }

            evolutionData =
                data;

            return true;
        }

        return false;
    }


    private bool HasRequiredPair(
        WeaponEvolutionData data)
    {
        if (data == null)
        {
            return false;
        }

        return
            IsModuleUnlocked(
                data.RequiredModuleA
            )
            &&
            IsModuleUnlocked(
                data.RequiredModuleB
            );
    }


    private bool IsModuleUnlocked(
        UpgradeType upgradeType)
    {
        if (weaponModifiers == null)
        {
            return false;
        }

        switch (upgradeType)
        {
            case UpgradeType.Piercing:

                return
                    weaponModifiers.PiercingLevel > 0;


            case UpgradeType.Explosive:

                return
                    weaponModifiers.ExplosiveLevel > 0;


            case UpgradeType.ChainLightning:

                return
                    weaponModifiers
                        .ChainLightningLevel > 0;


            case UpgradeType.SplitShot:

                return
                    weaponModifiers.SplitShotLevel > 0;


            default:

                return false;
        }
    }


    private int GetOwnedCoreModuleCount()
    {
        if (weaponModifiers == null)
        {
            return 0;
        }

        int count = 0;

        if (weaponModifiers.PiercingLevel > 0)
        {
            count++;
        }

        if (weaponModifiers.ExplosiveLevel > 0)
        {
            count++;
        }

        if (weaponModifiers.ChainLightningLevel > 0)
        {
            count++;
        }

        if (weaponModifiers.SplitShotLevel > 0)
        {
            count++;
        }

        return count;
    }


    // =========================================================
    // Apply Evolution
    // =========================================================

    public bool TryApplyEligibleEvolution()
    {
        if (!TryGetEligibleEvolution(
                out WeaponEvolutionData data))
        {
            return false;
        }

        return ApplyEvolutionInternal(
            data
        );
    }


    private bool ApplyEvolutionInternal(
        WeaponEvolutionData data)
    {
        if (!IsDefinitionUsable(data))
        {
            return false;
        }

        currentEvolution =
            data.EvolutionType;

        currentEvolutionData =
            data;


        Debug.Log(
            "===== Weapon Evolution Applied =====\n"
            + "Type: "
            + currentEvolution
            + "\nDisplay Name: "
            + data.DisplayName
            + "\nRequired Pair: "
            + data.RequiredModuleA
            + " + "
            + data.RequiredModuleB,
            this
        );

        return true;
    }


    // =========================================================
    // Reset
    // =========================================================

    public void ResetForNewRun()
    {
        ResetRuntimeState();

        Debug.Log(
            "WeaponEvolutionController: "
            + "Evolution state reset to None.",
            this
        );
    }


    private void ResetRuntimeState()
    {
        currentEvolution =
            WeaponEvolutionType.None;

        currentEvolutionData =
            null;
    }


    // =========================================================
    // Definition Validation
    // =========================================================

    private bool IsDefinitionUsable(
        WeaponEvolutionData data)
    {
        return
            data != null
            && data.EvolutionType
                != WeaponEvolutionType.None
            && data.HasValidModulePair;
    }


    private void ValidateDefinitions()
    {
        if (evolutionDefinitions == null
            || evolutionDefinitions.Length == 0)
        {
            Debug.LogWarning(
                "WeaponEvolutionController: "
                + "No WeaponEvolutionData definitions "
                + "have been assigned.",
                this
            );

            return;
        }

        for (int i = 0;
             i < evolutionDefinitions.Length;
             i++)
        {
            WeaponEvolutionData data =
                evolutionDefinitions[i];

            if (data == null)
            {
                Debug.LogWarning(
                    "WeaponEvolutionController: "
                    + "Evolution definition index "
                    + i
                    + " is null.",
                    this
                );

                continue;
            }

            if (!data.HasValidModulePair)
            {
                Debug.LogWarning(
                    "WeaponEvolutionController: "
                    + data.name
                    + " has an invalid required "
                    + "module pair.",
                    data
                );
            }

            if (data.EvolutionType
                == WeaponEvolutionType.None)
            {
                Debug.LogWarning(
                    "WeaponEvolutionController: "
                    + data.name
                    + " uses EvolutionType.None.",
                    data
                );
            }
        }
    }


    // =========================================================
    // Dependency
    // =========================================================

    private void EnsureDependencies()
    {
        if (weaponModifiers == null)
        {
            weaponModifiers =
                GetComponent<PlayerWeaponModifiers>();
        }
    }


    // =========================================================
    // Debug
    // =========================================================

    [ContextMenu(
        "Debug/Force Thunder Evolution")]
    private void DebugForceThunderEvolution()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ForceEvolution(
            WeaponEvolutionType.ThunderPiercer
        );
    }


    [ContextMenu(
        "Debug/Force Cluster Evolution")]
    private void DebugForceClusterEvolution()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ForceEvolution(
            WeaponEvolutionType.ClusterBurst
        );
    }


    [ContextMenu(
        "Debug/Reset Evolution")]
    private void DebugResetEvolution()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        ResetForNewRun();

        PrintEvolutionState();
    }


    [ContextMenu(
        "Debug/Print Evolution State")]
    private void DebugPrintEvolutionState()
    {
        if (!CanUseRuntimeDebug())
        {
            return;
        }

        PrintEvolutionState();
    }


    private void ForceEvolution(
        WeaponEvolutionType evolutionType)
    {
        WeaponEvolutionData data =
            FindEvolutionDefinition(
                evolutionType
            );

        if (data == null)
        {
            Debug.LogWarning(
                "WeaponEvolutionController: "
                + "No definition was found for "
                + evolutionType
                + ".",
                this
            );

            return;
        }

        // Debug Force 的目的就是可以独立测试。
        // 因此这里允许绕过 Module Pair。
        ResetRuntimeState();

        ApplyEvolutionInternal(
            data
        );
    }


    private WeaponEvolutionData
        FindEvolutionDefinition(
            WeaponEvolutionType evolutionType)
    {
        if (evolutionDefinitions == null)
        {
            return null;
        }

        for (int i = 0;
             i < evolutionDefinitions.Length;
             i++)
        {
            WeaponEvolutionData data =
                evolutionDefinitions[i];

            if (data == null)
            {
                continue;
            }

            if (data.EvolutionType
                == evolutionType)
            {
                return data;
            }
        }

        return null;
    }


    private void PrintEvolutionState()
    {
        Debug.Log(
            "===== Weapon Evolution State =====\n"
            + "Current Type: "
            + currentEvolution
            + "\nHas Evolution: "
            + HasEvolution
            + "\nCurrent Data: "
            + (currentEvolutionData != null
                ? currentEvolutionData.name
                : "None")
            + "\nOwned Module Count: "
            + GetOwnedCoreModuleCount()
            + "\nEligible Now: "
            + CanEvolve(),
            this
        );
    }


    private bool CanUseRuntimeDebug()
    {
        if (Application.isPlaying)
        {
            return true;
        }

        Debug.LogWarning(
            "WeaponEvolutionController: "
            + "Please enter Play Mode before "
            + "using runtime debug commands.",
            this
        );

        return false;
    }
}