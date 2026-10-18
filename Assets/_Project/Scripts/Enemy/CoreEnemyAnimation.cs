using UnityEngine;

// Sprite-only presentation layered on the project's Animator and stable motion
// facing (legacy fallback retained). Attack timers, hits and rewards stay in their owners.
[DefaultExecutionOrder(160), DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(EnemyAnimationController), typeof(EnemyHealth))]
public sealed class CoreEnemyAnimation : MonoBehaviour, IEnemyDeathPresentation
{
    public enum Pose { Idle, Move, Windup, Attack, Recover, Death }
    public const float CollapseSeconds = .55f;
    static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    static readonly int DirectionHash = Animator.StringToHash("Direction");
    static readonly int[,] Hashes = BuildHashes();
    [SerializeField] Material impactMaterial;
    Animator animator;
    EnemyMotionPresentation motion;
    SpriteRenderer body;
    EnemyHealth health;
    FastChargeAttack charge;
    HeavyShockwaveAttack shock;
    RangedEnemyAttack spit;
    Behaviour attack;
    LineRenderer impact;
    readonly Vector3[] circle = new Vector3[64];
    Vector3 previousPosition;
    float movingGrace, deathElapsed;
    bool dying, preview, restoreAttack;
    Pose pose;
    int direction = 1;
    public Pose CurrentPose => pose;
    public int CurrentDirection => direction;
    public bool IsPreview => preview;
    public float DeathElapsed => deathElapsed;
    public float DeathPresentationDuration => CollapseSeconds;
    public bool ImpactVisible => impact != null && impact.enabled;
    public float DisplayedImpactRadius { get; private set; }
    public EnemyAttackVisualSample AttackSample => charge != null ? charge.VisualSample : shock != null ? shock.VisualSample : spit != null ? spit.VisualSample : default;

    static int[,] BuildHashes()
    {
        var hashes = new int[6,8];
        for (int p=0; p<6; p++) for (int d=0; d<8; d++) hashes[p,d] = Animator.StringToHash("Base Layer." + (Pose)p + "_" + Directions[d]);
        return hashes;
    }
    void Awake()
    {
        motion=GetComponent<EnemyMotionPresentation>(); animator=GetComponent<Animator>(); body=GetComponent<SpriteRenderer>(); health=GetComponent<EnemyHealth>();
        charge=GetComponent<FastChargeAttack>(); shock=GetComponent<HeavyShockwaveAttack>(); spit=GetComponent<RangedEnemyAttack>();
        attack = charge != null ? (Behaviour)charge : shock != null ? (Behaviour)shock : spit;
        if (shock != null && impactMaterial != null)
        {
            var child=new GameObject("Tank impact boundary (visual only)"); child.transform.SetParent(transform,false);
            impact=child.AddComponent<LineRenderer>(); impact.sharedMaterial=impactMaterial;
            impact.useWorldSpace=true; impact.loop=true; impact.widthMultiplier=.06f;
            impact.sortingLayerName="Pickups"; impact.sortingOrder=5; impact.enabled=false;
        }
    }
    void OnEnable()
    {
        if (restoreAttack && attack != null) attack.enabled=true;
        restoreAttack=false; dying=preview=false; movingGrace=deathElapsed=0;
        previousPosition=transform.position; pose=Pose.Idle; direction=1;
        animator.speed=1; animator.Rebind(); animator.SetInteger(DirectionHash,1);
        animator.Play(Hashes[(int)Pose.Idle,1],0,0); animator.Update(0);
        var color=body.color; color.a=1; body.color=color; body.flipX=body.flipY=false;
        HideImpact();
    }
    public void BeginDeathPresentation()
    {
        if (dying) return;
        dying=true; deathElapsed=0; preview=false; animator.speed=1;
        // Native OnDisable clears its warning immediately; restore on the next life.
        if (attack != null && attack.enabled) { restoreAttack=true; attack.enabled=false; }
        HideImpact(); Select(Pose.Death,direction,0);
    }
    void LateUpdate()
    {
        if (Time.deltaTime<=0) { previousPosition=transform.position; return; }
        if (preview) { previousPosition=transform.position; HideImpact(); return; }
        if (health.IsDead)
        {
            if (!dying) BeginDeathPresentation();
            deathElapsed+=Time.deltaTime;
            var color=body.color; color.a=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(CollapseSeconds-.15f,CollapseSeconds,deathElapsed)); body.color=color;
            return;
        }
        var delta=transform.position-previousPosition; previousPosition=transform.position;
        movingGrace=delta.sqrMagnitude>.000004f ? .09f : Mathf.Max(0,movingGrace-Time.deltaTime);
        bool moving=motion!=null ? motion.IsMoving : movingGrace>0;
        int facing=motion!=null ? motion.Direction : movingGrace>0 ? Mathf.Clamp(animator.GetInteger(DirectionHash),0,7) : direction;
        var sample=AttackSample;
        if (sample.Direction.sqrMagnitude>.0001f) facing=(Mathf.RoundToInt(Mathf.Atan2(sample.Direction.y,sample.Direction.x)*Mathf.Rad2Deg/45f)+10)%8;
        HideImpact();
        if (sample.Phase==EnemyAttackVisualPhase.None)
        {
            animator.speed=moving && motion!=null ? 0 : 1;
            Select(moving ? Pose.Move : Pose.Idle,facing,moving && motion!=null ? motion.Gait : -1); return;
        }
        animator.speed=0;
        if (sample.Phase==EnemyAttackVisualPhase.Windup) Select(Pose.Windup,facing,sample.Progress*.999f);
        else if (sample.Phase==EnemyAttackVisualPhase.Active) Select(Pose.Attack,facing,sample.Progress*.999f);
        else if (charge!=null || (shock!=null && shock.UsesExpandingWave)) Select(Pose.Recover,facing,sample.Progress*.999f);
        else
        {
            // Tank and Spitter apply their native attack once on entering recovery.
            // First show impact/recoil, then settle, all sampled from that same clock.
            float strikeFraction=.5f;
            if (sample.Progress<strikeFraction)
            {
                Select(Pose.Attack,facing,sample.Progress/strikeFraction*.999f);
                if (shock!=null) ShowImpact(1-sample.Progress/strikeFraction);
            }
            else Select(Pose.Recover,facing,(sample.Progress-strikeFraction)/(1-strikeFraction)*.999f);
        }
    }
    void Select(Pose next,int facing,float sample=-1)
    {
        if (next==pose && facing==direction && sample<0) return;
        if (next==Pose.Move && next==pose && facing==direction && sample>=0 &&
            Mathf.FloorToInt(sample*8)==Mathf.FloorToInt(Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1)*8)) return;
        float phase=sample>=0 ? sample : next==pose && (next==Pose.Idle || next==Pose.Move) ? Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1) : 0;
        pose=next; direction=facing; animator.Play(Hashes[(int)pose,direction],0,phase); animator.Update(0);
    }
    void ShowImpact(float alpha)
    {
        if (impact==null) return;
        DisplayedImpactRadius=shock.VisualShockwaveRadius;
        for (int i=0; i<circle.Length; i++)
        {
            float angle=i*Mathf.PI*2/circle.Length;
            circle[i]=transform.position+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*DisplayedImpactRadius;
        }
        impact.positionCount=circle.Length; impact.SetPositions(circle);
        impact.startColor=impact.endColor=new Color(1,.74f,.25f,alpha*.8f); impact.enabled=true;
    }
    void HideImpact() { DisplayedImpactRadius=0; if (impact!=null) { impact.enabled=false; impact.positionCount=0; } }
    public void Preview(Pose value,int facing)
    {
        preview=true; animator.speed=1; Select(value,Mathf.Clamp(facing,0,7),0); HideImpact();
    }
    void OnDisable()
    {
        preview=dying=false; movingGrace=deathElapsed=0; HideImpact();
        if (animator!=null) animator.speed=1;
        if (body!=null) { var color=body.color; color.a=1; body.color=color; }
    }
}
