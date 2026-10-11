
using UnityEngine;

/// <summary>
/// Phase40 - Toxic Fog Damage.
/// Applies periodic damage while the Player root is inside the
/// covered Toxic Fog area. Does not change the area, route or Storm.
///
/// Damage is always applied through PlayerHealth.TakeDamage(),
/// preserving invulnerability, HUD, effects and death handling.
/// </summary>
[DisallowMultipleComponent]
public class ToxicFogDamageController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("Toxic Fog References")]
    [SerializeField] private ToxicFogAreaController toxicFogArea;
    [SerializeField] private Transform player;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Existing Storm References (Optional)")]
    [SerializeField] private PlanetStormController planetStormController;
    [SerializeField] private SafeZoneController safeZoneController;
    [SerializeField] private StormDamageController stormDamageController;

    // =========================================================
    // Damage Settings
    // =========================================================

    [Header("Toxic Fog Damage Settings")]
    [Min(1)]
    [SerializeField] private int fogDamagePerTick = 1;

    [Min(0.1f)]
    [SerializeField] private float damageInterval = 2f;

    [Tooltip("If an enabled StormDamageController is actively damaging " +
             "the Player outside the Storm Safe Zone, Storm takes priority. " +
             "Inside the Storm Safe Zone, Toxic Fog still deals damage.")]
    [SerializeField] private bool giveActiveStormDamagePriority = true;

    [Header("Debug")]
    [SerializeField] private bool logDamageTicks = true;

    // =========================================================
    // Runtime Debug - Do Not Edit During Play
    // =========================================================

    [Header("Runtime Debug (Read Only)")]
    [SerializeField] private bool playerInsideFog;
    [SerializeField] private bool stormHasDamagePriority;
    [SerializeField] private float damageTimer;
    [SerializeField] private int attemptedDamageTickCount;
    [SerializeField] private bool lastDamageTickApplied;

    public bool PlayerInsideFog => playerInsideFog;
    public bool StormHasDamagePriority => stormHasDamagePriority;
    public float DamageTimer => damageTimer;
    public int AttemptedDamageTickCount => attemptedDamageTickCount;
    public bool LastDamageTickApplied => lastDamageTickApplied;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Reset()
    {
        toxicFogArea = GetComponent<ToxicFogAreaController>();
    }

    private void Awake()
    {
        ResolveReferences();
        ResetDamageState();
    }

    private void Start()
    {
        ResolveReferences();

        if (toxicFogArea == null || player == null || playerHealth == null)
        {
            Debug.LogWarning(
                "[ToxicFogDamage] Missing Area, Player root, or " +
                "PlayerHealth reference. Assign them in the Inspector.",
                this);
        }
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;

        if (manager == null || !manager.IsPlaying ||
            playerHealth == null || playerHealth.IsDead)
        {
            ResetDamageCycle();
            playerInsideFog = false;
            stormHasDamagePriority = false;
            return;
        }

        // IMPORTANT: a paused/selection frame must FREEZE the current
        // accumulated exposure, not reset it or apply damage.
        if (Time.timeScale <= 0f ||
            UpgradeManager.IsChoosingUpgrade ||
            WeaponModuleSelectionManager.IsChoosingModule)
        {
            return;
        }

        if (toxicFogArea == null || player == null ||
            !toxicFogArea.GeometryIsValid)
        {
            ResetDamageCycle();
            playerInsideFog = false;
            stormHasDamagePriority = false;
            return;
        }

        // Use the logical Player root, NOT the jumping VisualRoot.
        playerInsideFog =
            toxicFogArea.IsPositionInsideFog(player.position);

        if (!playerInsideFog)
        {
            // Leaving the fog clears partial exposure time.
            ResetDamageCycle();
            stormHasDamagePriority = false;
            return;
        }

        stormHasDamagePriority = ShouldYieldDamageToStorm();

        if (stormHasDamagePriority)
        {
            // Prevent overlapping environment damage ticks.
            // Storm controls damage until its dangerous state ends.
            ResetDamageCycle();
            return;
        }

        damageTimer += Time.deltaTime;

        if (damageTimer < damageInterval)
        {
            return;
        }

        // Only one damage attempt per frame, even after a long frame.
        damageTimer = Mathf.Max(0f, damageTimer - damageInterval);
        ApplyFogDamageTick();
    }

    private void OnDisable()
    {
        ResetDamageState();
    }

    private void OnValidate()
    {
        fogDamagePerTick = Mathf.Max(1, fogDamagePerTick);
        damageInterval = Mathf.Max(0.1f, damageInterval);
    }

    // =========================================================
    // References
    // =========================================================

    private void ResolveReferences()
    {
        if (toxicFogArea == null)
        {
            toxicFogArea = GetComponent<ToxicFogAreaController>();
        }

        if (player == null && playerHealth != null)
        {
            player = playerHealth.transform;
        }

        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (planetStormController == null)
        {
            planetStormController =
                FindFirstObjectByType<PlanetStormController>();
        }

        if (safeZoneController == null)
        {
            safeZoneController =
                FindFirstObjectByType<SafeZoneController>();
        }

        if (stormDamageController == null)
        {
            stormDamageController =
                FindFirstObjectByType<StormDamageController>();
        }
    }

    // =========================================================
    // Storm Damage Priority
    // =========================================================

    private bool ShouldYieldDamageToStorm()
    {
        if (!giveActiveStormDamagePriority ||
            planetStormController == null ||
            safeZoneController == null ||
            stormDamageController == null ||
            !stormDamageController.isActiveAndEnabled ||
            !planetStormController.IsStormActive ||
            !safeZoneController.HasActiveZone)
        {
            return false;
        }

        // A Storm Safe Zone is NOT protection from Toxic Fog.
        // Only yield to Storm when the Player is actually outside it.
        return !safeZoneController.IsPositionSafe(player.position);
    }

    // =========================================================
    // Damage
    // =========================================================

    private void ApplyFogDamageTick()
    {
        if (playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        attemptedDamageTickCount++;

        int healthBefore = playerHealth.CurrentHealth;

        // Existing PlayerHealth handles invulnerability, feedback,
        // death, HUD updates, audio and camera shake.
        playerHealth.TakeDamage(fogDamagePerTick);

        int healthAfter = playerHealth.CurrentHealth;
        lastDamageTickApplied = healthAfter < healthBefore;

        if (logDamageTicks)
        {
            Debug.Log(
                "[ToxicFogDamage] Tick #" + attemptedDamageTickCount +
                " | Requested: " + fogDamagePerTick +
                " | Applied: " + lastDamageTickApplied +
                " | HP: " + healthBefore + " -> " + healthAfter,
                this);
        }
    }

    // =========================================================
    // Runtime State
    // =========================================================

    private void ResetDamageCycle()
    {
        damageTimer = 0f;
        lastDamageTickApplied = false;
    }

    private void ResetDamageState()
    {
        ResetDamageCycle();
        playerInsideFog = false;
        stormHasDamagePriority = false;
        attemptedDamageTickCount = 0;
    }

    [ContextMenu("Debug/Print Toxic Fog Damage State")]
    private void PrintDamageState()
    {
        Debug.Log(
            "===== Toxic Fog Damage =====" +
            "\nPlayer Inside Fog: " + playerInsideFog +
            "\nStorm Has Priority: " + stormHasDamagePriority +
            "\nDamage Timer: " + damageTimer.ToString("F2") +
            "\nDamage Interval: " + damageInterval.ToString("F2") +
            "\nAttempted Ticks: " + attemptedDamageTickCount +
            "\nLast Tick Applied: " + lastDamageTickApplied +
            "\nCurrent HP: " +
            (playerHealth != null
                ? playerHealth.CurrentHealth.ToString()
                : "NO PLAYER HEALTH"),
            this);
    }
}
