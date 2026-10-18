using UnityEngine;

// Selects authored Sprite clips in the existing Animator. Gameplay never depends on
// clip completion or Animation Events. EnemyAnimationController still owns Direction.
[DefaultExecutionOrder(150), DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(EnemyHealth), typeof(EnemyAnimationController))]
public sealed class SupportExploderAnimation : MonoBehaviour
{
    public enum Pose { Idle, Move, SupportStart, SupportLoop, SupportMove, SupportEnd, Warning, Death, Burst }
    public const float DeathSeconds = .55f, BurstSeconds = .75f;
    static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    static readonly int DirectionHash = Animator.StringToHash("Direction");
    static readonly int[,] Hashes = MakeHashes();
    Animator animator;
    SpriteRenderer body;
    EnemyHealth health;
    SupportEnemyAbility support;
    ExploderEnemyAbility exploder;
    Vector3 previousPosition;
    float movingGrace, activeElapsed, closingRemaining, deathElapsed;
    bool wasSupporting, dying, bursting, preview;
    Pose pose = Pose.Idle;
    int direction = 1;
    public Pose CurrentPose => pose;
    public int CurrentDirection => direction;
    public float DeathElapsed => deathElapsed;
    public bool IsPreview => preview;
    static int[,] MakeHashes()
    {
        var hashes = new int[9, 8];
        for (int p = 0; p < 9; p++) for (int d = 0; d < 8; d++)
            hashes[p, d] = Animator.StringToHash("Base Layer." + (Pose)p + "_" + Directions[d]);
        return hashes;
    }
    void Awake()
    {
        animator = GetComponent<Animator>(); body = GetComponent<SpriteRenderer>(); health = GetComponent<EnemyHealth>();
        support = GetComponent<SupportEnemyAbility>(); exploder = GetComponent<ExploderEnemyAbility>();
    }
    void OnEnable()
    {
        previousPosition = transform.position; movingGrace = activeElapsed = closingRemaining = deathElapsed = 0;
        wasSupporting = dying = bursting = preview = false; direction = 1; pose = Pose.Idle;
        animator.speed = 1; animator.Rebind(); animator.SetInteger(DirectionHash, 1);
        animator.Play(Hashes[(int)Pose.Idle, 1], 0, 0); animator.Update(0);
        var color = body.color; color.a = 1; body.color = color; body.flipX = body.flipY = false;
    }
    public void BeginDeath(bool detonation)
    {
        if (dying) return;
        dying = true; bursting = detonation; deathElapsed = 0; wasSupporting = false; closingRemaining = 0;
        animator.speed = 1;
        Select(detonation ? Pose.Burst : Pose.Death, direction, 0);
    }
    void LateUpdate()
    {
        if (Time.deltaTime <= 0) { previousPosition = transform.position; return; }
        if (preview) { previousPosition = transform.position; return; }
        if (health.IsDead)
        {
            if (!dying) BeginDeath(exploder != null && exploder.State == ExploderState.Detonated);
            deathElapsed += Time.deltaTime;
            float end = bursting ? BurstSeconds : DeathSeconds;
            var color = body.color; color.a = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(end - .15f, end, deathElapsed)); body.color = color;
            return;
        }
        int facing = Mathf.Clamp(animator.GetInteger(DirectionHash), 0, 7);
        var delta = transform.position - previousPosition; previousPosition = transform.position;
        movingGrace = delta.sqrMagnitude > .000004f ? .09f : Mathf.Max(0, movingGrace - Time.deltaTime);
        bool moving = movingGrace > 0;
        if (exploder != null && exploder.State == ExploderState.Warning)
        {
            // Sample the warning directly from the ability's scaled-time countdown.
            // This stays synchronized under pause/slow motion and cannot cause damage.
            Select(Pose.Warning, facing, exploder.WarningProgress * .999f);
            animator.speed = 0;
            return;
        }
        animator.speed = 1;
        if (support != null)
        {
            bool active = support.TargetCount > 0;
            if (active)
            {
                activeElapsed = wasSupporting ? activeElapsed + Time.deltaTime : 0;
                closingRemaining = 0;
                Select(moving ? Pose.SupportMove : activeElapsed < 1f / 3 ? Pose.SupportStart : Pose.SupportLoop, facing);
            }
            else
            {
                if (wasSupporting) closingRemaining = 1f / 3;
                Select(moving ? Pose.Move : closingRemaining > 0 ? Pose.SupportEnd : Pose.Idle, facing);
                closingRemaining = Mathf.Max(0, closingRemaining - Time.deltaTime);
                activeElapsed = 0;
            }
            wasSupporting = active;
        }
        else Select(moving ? Pose.Move : Pose.Idle, facing);
    }
    static bool Cyclic(Pose p) => p == Pose.Move || p == Pose.SupportMove || p == Pose.Idle || p == Pose.SupportLoop;
    void Select(Pose next, int facing, float sample = -1)
    {
        if (next == pose && facing == direction && sample < 0) return;
        float phase = sample >= 0 ? sample : Cyclic(next) && (next == pose || ((next == Pose.Move || next == Pose.SupportMove) && (pose == Pose.Move || pose == Pose.SupportMove)))
            ? Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1) : 0;
        pose = next; direction = facing;
        animator.Play(Hashes[(int)pose, direction], 0, phase); animator.Update(0);
    }
    // Used only by the isolated lab. Preview never mutates health, abilities or rewards.
    public void Preview(Pose value, int facing)
    {
        preview = true; animator.speed = 1; Select(value, Mathf.Clamp(facing, 0, 7), 0);
    }
    void OnDisable()
    {
        preview = dying = bursting = wasSupporting = false; movingGrace = activeElapsed = closingRemaining = deathElapsed = 0;
        if (animator != null) animator.speed = 1;
        if (body != null) { var color = body.color; color.a = 1; body.color = color; }
    }
}
