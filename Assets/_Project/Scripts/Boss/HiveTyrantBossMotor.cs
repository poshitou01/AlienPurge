using UnityEngine;

// Locomotion owns actions 0-5. Combat explicitly leases the animator.
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
public class HiveTyrantBossMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private Transform target;
    [SerializeField] private Collider2D solidBody;
    [Header("Auto Find")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.7f;
    [SerializeField] private float stoppingDistance = 4.2f;
    [SerializeField] private float movementThreshold = .02f;
    [SerializeField] private LayerMask blockingLayers = 1 << 7;
    [Header("Animator Parameters")]
    [SerializeField] private string directionParam = "Direction";
    [SerializeField] private string actionParam = "Action";
    [Tooltip("Action-major, eight directions each. Filled by the existing-controller setup.")]
    [SerializeField] private string[] stateNames = new string[80];
    [Tooltip("The articulated presentation owns combat poses; Animator holds only the hidden base sprite.")]
    [SerializeField] private bool temporaryCombatPoses = true;
    [Header("Runtime")]
    [SerializeField] private bool movementLocked;
    [SerializeField] private bool dead;
    private bool actionOwned, isPhase2;
    private int currentDirection = 1, currentAction = -1;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];
    private HiveTyrantCombat combat;

    public Transform Target => target;
    public bool IsDead => dead;
    public bool IsMovementLocked => movementLocked;
    public bool IsPhase2 => isPhase2;
    public bool HasActionOwner => actionOwned;
    public Animator BodyAnimator => bodyAnimator;
    public Vector2 CombatOrigin => solidBody != null ? (Vector2)solidBody.bounds.center : (Vector2)transform.position;
    public float StoppingDistance => stoppingDistance;

    private void Reset() { Cache(); }
    private void Awake() { Cache(); SetLocomotionAction(false); }
    private void Cache()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (solidBody == null) solidBody = GetComponent<Collider2D>();
        if (bodyAnimator == null) bodyAnimator = GetComponentInChildren<Animator>(true);
        combat = GetComponent<HiveTyrantCombat>();
    }
    private void Update()
    {
        if (!dead && autoFindPlayer && target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null) target = player.transform;
        }
    }
    private void FixedUpdate()
    {
        if (dead || actionOwned) { StopMotion(); return; }
        if (movementLocked || target == null || (combat != null && !combat.TargetInArena) || (GameManager.Instance != null && !GameManager.Instance.IsPlaying))
        { StopMotion(); SetLocomotionAction(false); return; }
        Vector2 delta = (Vector2)target.position - CombatOrigin;
        Face(delta);
        float distance = delta.magnitude;
        if (distance <= stoppingDistance) { StopMotion(); SetLocomotionAction(false); return; }
        Vector2 direction = delta.normalized;
        float step = Mathf.Min(moveSpeed * Time.fixedDeltaTime, distance - stoppingDistance);
        Vector2 move = ConstrainMotion(direction * step);
        if (move.sqrMagnitude < step * step * .05f)
        {
            Vector2 left = ConstrainMotion((Vector2)(Quaternion.Euler(0, 0, 55) * direction) * step);
            Vector2 right = ConstrainMotion((Vector2)(Quaternion.Euler(0, 0, -55) * direction) * step);
            move = left.sqrMagnitude >= right.sqrMagnitude ? left : right;
        }
        if (move.sqrMagnitude > .000001f) Face(move);
        rb.MovePosition(rb.position + move);
        SetLocomotionAction(move.sqrMagnitude > movementThreshold * movementThreshold * Time.fixedDeltaTime * Time.fixedDeltaTime);
    }
    private Vector2 ConstrainMotion(Vector2 move)
    {
        if (combat != null && !combat.InsideArena(CombatOrigin + move)) return Vector2.zero;
        if (solidBody == null || move.sqrMagnitude < .000001f) return move;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(blockingLayers); filter.useTriggers = false;
        float length = move.magnitude;
        Vector2 direction = move / length;
        int count = solidBody.Cast(direction, filter, castHits, length + .03f, true);
        for (int i = 0; i < count; i++)
        {
            if (castHits[i].collider == null || castHits[i].collider.transform.IsChildOf(transform)) continue;
            if (Vector2.Dot(direction, castHits[i].normal) < -.001f)
                length = Mathf.Min(length, Mathf.Max(0, castHits[i].distance - .03f));
        }
        return direction * length;
    }
    private void StopMotion()
    {
        if (rb == null) return;
        rb.velocity = Vector2.zero; rb.angularVelocity = 0;
        // A cast/death may begin after FixedUpdate queued movement for this step.
        // Cancel that pending target as well as the current velocity.
        rb.MovePosition(rb.position);
    }
    public void Face(Vector2 direction)
    {
        if (dead || direction.sqrMagnitude < .0001f) return;
        int next = (Mathf.RoundToInt((Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f) / 45f) + 8) % 8;
        if (next == currentDirection) return;
        float gait = 0;
        if (bodyAnimator != null && (currentAction == 1 || currentAction == 5))
            gait = Mathf.Repeat(bodyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
        currentDirection = next;
        ApplyAnimator(currentAction, gait);
    }
    private void SetLocomotionAction(bool moving)
    {
        if (dead || actionOwned) return;
        SetAction(isPhase2 ? (moving ? 5 : 4) : (moving ? 1 : 0));
    }
    private void SetAction(int action)
    {
        if (currentAction == action) return;
        currentAction = action;
        ApplyAnimator(action, 0);
    }
    private void ApplyAnimator(int action, float normalizedTime)
    {
        if (bodyAnimator == null || bodyAnimator.runtimeAnimatorController == null || action < 0) return;
        bodyAnimator.SetInteger(directionParam, currentDirection);
        int visualAction = temporaryCombatPoses && action >= 6 ? (isPhase2 ? 4 : 0) : action;
        bodyAnimator.SetInteger(actionParam, visualAction);
        int index = visualAction * 8 + currentDirection;
        if (index < stateNames.Length && !string.IsNullOrEmpty(stateNames[index]))
        {
            int hash = Animator.StringToHash("Base Layer." + stateNames[index]);
            if (bodyAnimator.HasState(0, hash)) bodyAnimator.Play(hash, 0, normalizedTime);
        }
    }
    public void BeginAction(int action, Vector2 facing)
    {
        if (dead) return;
        Face(facing); actionOwned = true; StopMotion();
        SetAction(action);
        if (bodyAnimator != null) bodyAnimator.speed = temporaryCombatPoses && action >= 6 ? 0 : 1;
    }
    public void EndAction()
    {
        if (dead) return;
        actionOwned = false;
        if (bodyAnimator != null) bodyAnimator.speed = 1;
        SetLocomotionAction(false);
    }
    public void EnterPhase2()
    {
        if (dead || isPhase2) return;
        isPhase2 = true;
        if (!actionOwned) SetLocomotionAction(false);
    }
    public void SetTarget(Transform value) { target = value; }
    public void SetMovementLocked(bool value)
    {
        movementLocked = value;
        if (value) { StopMotion(); if (!actionOwned && !dead) SetLocomotionAction(false); }
    }
    public void SetDead(bool value)
    {
        dead = value; StopMotion();
        if (bodyAnimator != null) bodyAnimator.speed = 1;
        if (value) { actionOwned = true; SetAction(3); }
        else { actionOwned = false; SetLocomotionAction(false); }
    }
    public void ResetEncounter()
    {
        isPhase2 = false; dead = false; actionOwned = false; movementLocked = false; currentAction = -1;
        if (bodyAnimator != null) bodyAnimator.speed = 1;
        StopMotion(); SetLocomotionAction(false);
    }
    public void ForceIdle() { if (!dead && !actionOwned) SetLocomotionAction(false); }
    public void ForceMove() { if (!dead && !actionOwned) SetLocomotionAction(true); }
    public void ForceHit() { if (!dead && !actionOwned) SetAction(2); }
    public void ForcePhase2Idle() { EnterPhase2(); ForceIdle(); }
    public int GetCurrentDirection() => currentDirection;
    private void OnDisable() { StopMotion(); }
    private void OnValidate()
    { moveSpeed = Mathf.Max(0, moveSpeed); stoppingDistance = Mathf.Max(.1f, stoppingDistance); movementThreshold = Mathf.Max(0, movementThreshold); }
}

