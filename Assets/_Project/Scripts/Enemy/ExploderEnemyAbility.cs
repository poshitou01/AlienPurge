using System.Collections.Generic;
using UnityEngine;

public enum ExploderState { Safe, Chase, Warning, Detonated, Cancelled }

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovement), typeof(EnemyHealth), typeof(PooledEnemy))]
public sealed class ExploderEnemyAbility : MonoBehaviour, IEnemyDeathPresentation
{
    [SerializeField] private ExploderEnemyConfig config;
    private EnemyMovement movement;
    private EnemyHealth health;
    private EnemyContactDamage damage;
    private PooledEnemy pooled;
    private PlayerHealth player;
    private Animator animator;
    private float age, warningElapsed;
    private bool sawSafeDistance;
    private float lockedRadius, lockedDuration;
    private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
    private readonly HashSet<PlayerHealth> victims = new HashSet<PlayerHealth>();
    public ExploderEnemyConfig Config => config;
    public ExploderState State { get; private set; }
    public Vector2 BlastCenter { get; private set; }
    public float BlastRadius => State == ExploderState.Warning || State == ExploderState.Detonated ? lockedRadius : config != null ? config.explosionRadius : 0;
    public float WarningProgress => State == ExploderState.Warning ? Mathf.Clamp01(warningElapsed / lockedDuration) : 0;
    public int ExplosionCount { get; private set; }
    public int DamageApplications { get; private set; }
    public float DeathPresentationDuration => .3f;
    private void Awake()
    {
        movement = GetComponent<EnemyMovement>(); health = GetComponent<EnemyHealth>();
        damage = GetComponent<EnemyContactDamage>(); pooled = GetComponent<PooledEnemy>(); animator = GetComponentInChildren<Animator>();
    }
    private void OnEnable()
    {
        age = warningElapsed = 0; sawSafeDistance = false; ExplosionCount = DamageApplications = 0;
        State = ExploderState.Safe; victims.Clear(); overlaps.Clear(); BlastCenter = transform.position;
        damage.enabled = false; if (animator != null) animator.speed = 1;
        var found = GameObject.FindGameObjectWithTag("Player"); player = found != null ? found.GetComponent<PlayerHealth>() : null;
    }
    private void Update()
    {
        if (config == null || health.IsDead || pooled.IsReturned) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || player == null || player.IsDead)
        { Cancel(); return; }
        if (State == ExploderState.Cancelled || State == ExploderState.Detonated) return;
        age += Time.deltaTime;
        if (State == ExploderState.Warning)
        {
            warningElapsed += Time.deltaTime;
            if (warningElapsed >= lockedDuration) Detonate();
            return;
        }
        float distance = Vector2.Distance(transform.position, player.transform.position);
        float safeDistance = Mathf.Max(config.safeArmingDistance, config.explosionRadius + 1f);
        if (distance >= safeDistance) sawSafeDistance = true;
        if (!sawSafeDistance || age < config.spawnGraceDuration) return;
        State = ExploderState.Chase;
        if (distance <= config.triggerDistance) BeginWarning();
    }
    private void BeginWarning()
    {
        State = ExploderState.Warning; warningElapsed = 0;
        BlastCenter = transform.position; lockedRadius = config.explosionRadius; lockedDuration = Mathf.Max(.8f, config.warningDuration);
        // Disable the native motor: its movement lock deliberately allows knockback. A locked ground blast must not drift.
        movement.enabled = false;
        var rb = GetComponent<Rigidbody2D>(); rb.velocity = Vector2.zero; rb.angularVelocity = 0;
        if (animator != null) animator.speed = 0;
    }
    private void Detonate()
    {
        if (State != ExploderState.Warning || health.IsDead || pooled.IsReturned || !isActiveAndEnabled) return;
        State = ExploderState.Detonated; ExplosionCount++; // Commit before callbacks, including lethal player damage.
        var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(config.victimLayers);
        Physics2D.OverlapCircle(BlastCenter, lockedRadius, filter, overlaps);
        if (config.victims == ExploderDamageFaction.PlayersOnly)
        foreach (var hit in overlaps)
        {
            var target = hit.GetComponentInParent<PlayerHealth>();
            if (target == null || !target.CompareTag("Player") || target.IsDead || !victims.Add(target)) continue;
            var ability = target.GetComponent<PlayerAbilityState>();
            if (config.groundOnly && ability != null && ability.IsAirborne) continue;
            int before = target.CurrentHealth;
            target.TakeDamage(Mathf.Max(1, Mathf.CeilToInt(damage.Damage * config.damageMultiplier)));
            if (target.CurrentHealth < before) DamageApplications++;
        }
        // Native death path owns kill count, XP and exactly one return. Self-destruction also grants the normal reward.
        health.TakeDamage(health.CurrentHealth);
    }
    public void BeginDeathPresentation()
    {
        if (State != ExploderState.Detonated) Cancel();
    }
    private void Cancel()
    {
        if (State == ExploderState.Detonated) return;
        State = ExploderState.Cancelled; warningElapsed = 0;
        if (movement != null) movement.enabled = false;
    }
    private void OnDisable()
    {
        State = ExploderState.Cancelled; age = warningElapsed = 0; sawSafeDistance = false;
        victims.Clear(); overlaps.Clear(); if (animator != null) animator.speed = 1;
    }
}
