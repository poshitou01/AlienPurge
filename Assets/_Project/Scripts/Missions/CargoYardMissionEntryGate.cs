
using UnityEngine;

/// <summary>
/// AlienPurge - Phase40 Step 3.5-C.
/// Level01-only entry trigger for the existing single Cargo Yard mission.
///
/// While armed, MissionManager's Update is disabled. When the Player
/// enters this area during gameplay, the original MissionManager is
/// enabled again and processes its existing Mission Schedule normally.
///
/// This is a temporary single-mission integration gate, NOT a final
/// multi-area Mission scheduler. It does not change rewards, objectives,
/// Storm, Toxic Fog, exit guidance or other scenes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CargoYardMissionEntryGate : MonoBehaviour
{
    [Header("Existing Gameplay References")]
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private Transform playerRoot;

    [Header("Entry Rules")]
    [Tooltip("Only unlock MissionManager when gameplay is running.")]
    [SerializeField] private bool requireGameplayRunning = true;

    [Tooltip("Write one Console message when the gate unlocks.")]
    [SerializeField] private bool logActivation = true;

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool gateArmed;
    [SerializeField] private bool playerInsideTrigger;
    [SerializeField] private bool gateTriggered;
    [SerializeField] private bool missionManagerEnabledByGate;

    public bool GateTriggered => gateTriggered;
    public bool PlayerInsideTrigger => playerInsideTrigger;

    private BoxCollider2D triggerCollider;

    private void Reset()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size = new Vector2(5f, 3f);
        }
    }

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                "[CargoYardMissionGate] BoxCollider2D should be a Trigger; correcting it.",
                this);
            triggerCollider.isTrigger = true;
        }

        if (missionManager == null)
            missionManager = FindFirstObjectByType<MissionManager>();

        if (playerRoot == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                playerRoot = playerObject.transform;
        }

        gateArmed = false;
        playerInsideTrigger = false;
        gateTriggered = false;
        missionManagerEnabledByGate = false;

        if (missionManager == null || playerRoot == null)
        {
            Debug.LogError(
                "[CargoYardMissionGate] Missing MissionManager or Player Root. " +
                "Assign both references in the Level01 Inspector.", this);
            return;
        }

        // Disable ONLY this component, never the whole MissionSystem object.
        // MissionManager.Awake still initializes on the active GameObject.
        missionManager.enabled = false;
        gateArmed = true;
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null || playerRoot == null)
            return false;

        Transform t = other.transform;
        return t == playerRoot || t.IsChildOf(playerRoot);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryUnlockMission(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Also works when Player spawned within the trigger or entered
        // before gameplay switched into its Playing state.
        TryUnlockMission(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayerCollider(other))
            playerInsideTrigger = false;
    }

    private void TryUnlockMission(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        playerInsideTrigger = true;

        if (!gateArmed || gateTriggered || missionManager == null)
            return;

        if (requireGameplayRunning &&
            (GameManager.Instance == null || !GameManager.Instance.IsPlaying))
            return;

        gateTriggered = true;
        gateArmed = false;
        missionManager.enabled = true;
        missionManagerEnabledByGate = missionManager.enabled;

        if (logActivation)
        {
            Debug.Log(
                "[CargoYardMissionGate] Player entered Cargo Yard mission area. " +
                "MissionManager enabled; existing Mission Schedule will run.",
                this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            return;

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.offset, box.size);
        Gizmos.matrix = originalMatrix;
    }
}
