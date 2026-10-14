
using UnityEngine;

/// <summary>
/// AlienPurge - Phase40 Step 3.5-D1.
/// Records Level01 region progress without changing scenes, mission scheduling,
/// rewards, player position, or the global Toxic Fog timeline.
///
/// For now, only Cargo Yard -> Early Extraction unlock is wired.
/// A later actual Early Extraction entrance trigger may call
/// TryConfirmEarlyExtractionEntry() after its playable area exists.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level01RegionProgressionController : MonoBehaviour
{
    public enum Level01Region
    {
        CargoYard,
        EarlyExtraction,
        ResearchMedical,
        InfectedResearch
    }

    [Header("Existing Level01 References")]
    [Tooltip("CY_ExitTrigger's existing CargoYardExitGuide component.")]
    [SerializeField]
    private CargoYardExitGuide cargoYardExitGuide;

    [Header("Debug Logging")]
    [SerializeField]
    private bool logProgressChanges = true;

    [Header("Runtime State - Read Only")]
    [SerializeField]
    private Level01Region currentRegion =
        Level01Region.CargoYard;

    [SerializeField]
    private bool cargoYardMissionCompleted;

    [SerializeField]
    private bool cargoYardExitReached;

    [SerializeField]
    private bool earlyExtractionUnlocked;

    [SerializeField]
    private bool awaitingEarlyExtractionEntry;

    [SerializeField]
    private bool earlyExtractionEntered;

    public Level01Region CurrentRegion => currentRegion;

    public bool CargoYardMissionCompleted =>
        cargoYardMissionCompleted;

    public bool CargoYardExitReached =>
        cargoYardExitReached;

    public bool EarlyExtractionUnlocked =>
        earlyExtractionUnlocked;

    public bool AwaitingEarlyExtractionEntry =>
        awaitingEarlyExtractionEntry;

    public bool EarlyExtractionEntered =>
        earlyExtractionEntered;

    private void Awake()
    {
        ResetRunState();
        ResolveReferences();
    }

    private void Start()
    {
        if (cargoYardExitGuide == null)
        {
            Debug.LogWarning(
                "[Level01Region] Missing CargoYardExitGuide. " +
                "Assign CY_ExitTrigger in the Inspector.",
                this);
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying ||
            cargoYardExitGuide == null)
        {
            return;
        }

        // The Cargo Yard guide already remembers Beacon completion.
        if (!cargoYardMissionCompleted &&
            cargoYardExitGuide.BeaconMissionCompleted)
        {
            cargoYardMissionCompleted = true;

            if (logProgressChanges)
            {
                Debug.Log(
                    "[Level01Region] Cargo Yard Beacon completed. " +
                    "Right exit is the next progression objective.",
                    this);
            }
        }

        if (!cargoYardExitReached &&
            cargoYardMissionCompleted &&
            cargoYardExitGuide.ReachedExitAfterMission)
        {
            cargoYardExitReached = true;
            earlyExtractionUnlocked = true;
            awaitingEarlyExtractionEntry = true;

            if (logProgressChanges)
            {
                Debug.Log(
                    "[Level01Region] Cargo Yard exit reached. " +
                    "Early Extraction UNLOCKED, but NOT entered. " +
                    "No scene transition has occurred.",
                    this);
            }
        }
    }

    /// <summary>
    /// Future integration point ONLY.
    /// Do not call until Early Extraction has a playable entrance.
    /// </summary>
    public bool TryConfirmEarlyExtractionEntry()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying ||
            currentRegion != Level01Region.CargoYard ||
            !earlyExtractionUnlocked ||
            !awaitingEarlyExtractionEntry)
        {
            return false;
        }

        currentRegion = Level01Region.EarlyExtraction;
        awaitingEarlyExtractionEntry = false;
        earlyExtractionEntered = true;

        if (logProgressChanges)
        {
            Debug.Log(
                "[Level01Region] Entered Early Extraction. " +
                "Region state updated; no scene load or fog reset.",
                this);
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (cargoYardExitGuide == null)
        {
            cargoYardExitGuide =
                FindFirstObjectByType<CargoYardExitGuide>();
        }
    }

    private void ResetRunState()
    {
        currentRegion = Level01Region.CargoYard;
        cargoYardMissionCompleted = false;
        cargoYardExitReached = false;
        earlyExtractionUnlocked = false;
        awaitingEarlyExtractionEntry = false;
        earlyExtractionEntered = false;
    }
}
