using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene opt-in input arbitration and combat tuning. Native abilities still own cooldowns and HUD state.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-200)]
[RequireComponent(typeof(PlayerDash), typeof(PlayerJetJump), typeof(PlayerPulseSkill))]
public sealed partial class PlayerAbilityFeel : MonoBehaviour
{
    [HideInInspector] public int installationRevision;
    [Header("Responsive input")]
    [Range(.05f, .3f)] public float inputBuffer = .18f;
    [Range(0, .15f)] public float pulseCancelAfter = .06f;
    [Header("Pulse combat")]
    public LayerMask environmentMask = 1 << 7;
    [Min(1)] public int pulseBaseDamage = 3;
    [Range(0, 1)] public float pulseDamagePerLevel = .45f;
    [Min(1)] public int pulseDamageLimit = 28;
    [Min(0)] public float pulsePush = 2.3f;
    [Min(.01f)] public float pulsePushDuration = .28f;
    [Header("Feedback (existing project audio)")]
    public AudioClip dashSound, jumpSound, pulseSound;
    [Range(0,1)] public float feedbackVolume = .55f;
    [Range(0,1)] public float cameraFeedback = .65f;
    public Material effectMaterial;

    PlayerDash dash;
    PlayerJetJump jump;
    PlayerPulseSkill pulse;
    PlayerAbilityState state;
    PlayerHealth health;
    PlayerExperience experience;
    WeaponManager weapons;
    Rigidbody2D body;
    Collider2D[] playerColliders;
    PlayerAbilityMode pending;
    float expires;
    Vector2 bufferedDirection;
    readonly Collider2D[] overlaps = new Collider2D[256];
    readonly RaycastHit2D[] walls = new RaycastHit2D[32];
    readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    readonly HashSet<EnemyProjectile> clearedProjectiles = new HashSet<EnemyProjectile>();
    readonly List<CollisionPair> ignored = new List<CollisionPair>();
    struct CollisionPair { public Collider2D player, enemy; }

    public bool HasBufferedInput => pending != PlayerAbilityMode.None;
    public bool IsDashProtected => isActiveAndEnabled && dash != null && dash.IsActive;
    public bool EvadesGroundAttacks => isActiveAndEnabled && jump != null && jump.IsActive && state.IsAirborne;
    public int LastPulseHits { get; private set; }
    public int LastPulseCleared { get; private set; }
    public int CurrentPulseDamage => Mathf.Clamp(pulseBaseDamage
        + Mathf.FloorToInt(Mathf.Max(0, (experience != null ? experience.CurrentLevel : 1) - 1) * pulseDamagePerLevel)
        + Mathf.FloorToInt((weapons != null && weapons.CurrentWeapon != null ? weapons.CurrentWeapon.CurrentDamage : 1) * .5f), 1, pulseDamageLimit);

    void Awake()
    {
        dash=GetComponent<PlayerDash>();jump=GetComponent<PlayerJetJump>();pulse=GetComponent<PlayerPulseSkill>();
        state=GetComponent<PlayerAbilityState>();health=GetComponent<PlayerHealth>();
        experience=GetComponent<PlayerExperience>();weapons=GetComponent<WeaponManager>();body=GetComponent<Rigidbody2D>();
        playerColliders=GetComponentsInChildren<Collider2D>();
        CreateFeedback();
    }
    bool Allowed => state != null && state.IsGameplayAbilityAllowed() && Time.timeScale > 0
        && !WeaponModuleSelectionManager.IsChoosingModule;
    void Update()
    {
        if(!Allowed)
        {
            pending=PlayerAbilityMode.None;RestoreContacts();SilenceFeedback();return;
        }
        // One deterministic intent: jump takes priority when keys are pressed in the same frame.
        if(Input.GetKeyDown(jump.InputKey))RequestAbility(PlayerAbilityMode.Airborne);
        else if(Input.GetKeyDown(dash.InputKey))RequestAbility(PlayerAbilityMode.Dashing);
        else if(Input.GetKeyDown(pulse.InputKey))RequestAbility(PlayerAbilityMode.Casting);
        TryConsumeInput();
        UpdateFeedback();
        if(!dash.IsActive&&!jump.IsActive)RestoreContacts();
    }
    void LateUpdate() { if(Allowed)TryConsumeInput(); }
    void FixedUpdate()
    {
        if(Allowed&&(dash.IsActive||jump.IsActive))IgnoreNearbyEnemyBodies();
        else RestoreContacts();
    }
    /// <summary>Shared by keyboard and automated gameplay checks. A request never resets a cooldown.</summary>
    public bool RequestAbility(PlayerAbilityMode mode)
    {
        if(!isActiveAndEnabled||!Allowed||mode==PlayerAbilityMode.None)return false;
        pending=mode;expires=Time.time+inputBuffer;
        if(mode==PlayerAbilityMode.Dashing)bufferedDirection=dash.RequestedDirection;
        TryConsumeInput();return true;
    }
    void TryConsumeInput()
    {
        if(pending==PlayerAbilityMode.None)return;
        if(!Allowed||Time.time>expires){pending=PlayerAbilityMode.None;return;}
        bool ready=pending==PlayerAbilityMode.Airborne?jump.IsReady:pending==PlayerAbilityMode.Dashing?dash.IsReady:pulse.IsReady;
        if(!ready)return;
        // An emergency jump can leave a dash immediately; a pulse has only 60 ms of commitment.
        if(pending==PlayerAbilityMode.Airborne&&dash.IsActive)dash.CancelForTransition();
        if(pending!=PlayerAbilityMode.Casting&&pulse.IsActive&&pulse.CastElapsed>=pulseCancelAfter)pulse.CancelForTransition();
        if(state.IsUsingAbility)return;
        bool started=pending==PlayerAbilityMode.Airborne?jump.TryActivateBuffered():
            pending==PlayerAbilityMode.Dashing?dash.TryActivateBuffered(bufferedDirection):pulse.TryActivateBuffered();
        if(started){pending=PlayerAbilityMode.None;if(dash.IsActive||jump.IsActive)IgnoreNearbyEnemyBodies();}
    }
    public float DashDistance(Vector2 direction,float requested)
    {
        // Sweep the actual rigidbody against map colliders; no teleporting through thin walls.
        var filter=new ContactFilter2D {useLayerMask=true,layerMask=environmentMask,useTriggers=false};
        int count=body.Cast(direction,filter,walls,requested+.025f);
        float allowed=requested;
        for(int i=0;i<count;i++)
        {
            // A surface behind us at distance zero must not prevent escaping along/out of it.
            if(Vector2.Dot(walls[i].normal,direction)>=-.01f)continue;
            allowed=Mathf.Min(allowed,Mathf.Max(0,walls[i].distance-.025f));
        }
        return allowed;
    }
    void IgnoreNearbyEnemyBodies()
    {
        int count=Physics2D.OverlapCircleNonAlloc(body.position,3.5f,overlaps);
        Collider2D[] contacts=count==overlaps.Length?Physics2D.OverlapCircleAll(body.position,3.5f):overlaps;
        if(contacts!=overlaps)count=contacts.Length;
        for(int i=0;i<count;i++)
        {
            var other=contacts[i];if(other==null||other.isTrigger||other.GetComponentInParent<EnemyHealth>()==null)continue;
            foreach(var own in playerColliders)
            {
                if(own==null||own.isTrigger||!own.enabled||Physics2D.GetIgnoreCollision(own,other))continue;
                Physics2D.IgnoreCollision(own,other,true);ignored.Add(new CollisionPair{player=own,enemy=other});
            }
        }
    }
    void RestoreContacts()
    {
        foreach(var pair in ignored)if(pair.player!=null&&pair.enemy!=null)Physics2D.IgnoreCollision(pair.player,pair.enemy,false);
        ignored.Clear();
    }
    bool VisibleFromPulse(Vector2 target)
    {
        Vector2 delta=target-body.position;
        var filter=new ContactFilter2D {useLayerMask=true,layerMask=environmentMask,useTriggers=false};
        return Physics2D.Raycast(body.position,delta.normalized,filter,walls,delta.magnitude)==0;
    }
    public void ApplyPulse(float radius)
    {
        LastPulseHits=LastPulseCleared=0;hitEnemies.Clear();clearedProjectiles.Clear();
        int count=Physics2D.OverlapCircleNonAlloc(body.position,radius,overlaps);
        // A dense arena can exceed the reusable buffer; never silently miss eligible targets.
        Collider2D[] targets=count==overlaps.Length?Physics2D.OverlapCircleAll(body.position,radius):overlaps;
        if(targets!=overlaps)count=targets.Length;
        int damage=CurrentPulseDamage;
        for(int i=0;i<count;i++)
        {
            var collider=targets[i];if(collider==null)continue;
            var projectile=collider.GetComponentInParent<EnemyProjectile>();
            if(projectile!=null&&!projectile.IsReturned&&clearedProjectiles.Add(projectile)&&VisibleFromPulse(projectile.transform.position))
            {projectile.ReturnToPool();LastPulseCleared++;continue;}
            var enemy=collider.GetComponentInParent<EnemyHealth>();
            if(enemy==null||enemy.IsDead||!hitEnemies.Add(enemy)||!VisibleFromPulse(enemy.transform.position))continue;
            Vector2 delta=(Vector2)enemy.transform.position-body.position;
            var movement=enemy.GetComponent<EnemyMovement>();
            // Heavy enemies retain weight; boss attack logic and phase transitions remain native.
            float weight=enemy.GetComponent<HeavyShockwaveAttack>()!=null?.45f:1;
            if(movement!=null&&enemy.GetComponent<HiveTyrantCombat>()==null)
                movement.ApplyKnockback(delta.sqrMagnitude>.001f?delta.normalized:Vector2.up,pulsePush*weight,pulsePushDuration);
            enemy.TakeDamage(damage);LastPulseHits++;
        }
    }
    public static bool AvoidsGroundHit(PlayerHealth target)
    {
        var feel=target!=null?target.GetComponent<PlayerAbilityFeel>():null;
        return feel!=null&&feel.EvadesGroundAttacks;
    }
    void OnDisable()
    {
        pending=PlayerAbilityMode.None;
        if(dash!=null&&dash.IsActive)dash.CancelForTransition();
        if(jump!=null&&jump.IsActive)jump.CancelForTransition();
        if(pulse!=null&&pulse.IsActive)pulse.CancelForTransition();
        RestoreContacts();SilenceFeedback();
    }
    void OnDestroy() { DestroyFeedback(); }
}
