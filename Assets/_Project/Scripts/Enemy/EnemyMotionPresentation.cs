using UnityEngine;

// Opt-in on the six ordinary enemy prefabs. Physics, navigation and hitboxes
// remain in their native owners; only facing and a distance-based gait are read.
[DefaultExecutionOrder(100), DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class EnemyMotionPresentation : MonoBehaviour
{
    [Min(.1f)] public float strideDistance = 1.2f;
    [Range(0,20)] public float turnHysteresis = 10;
    [Min(0)] public float minimumTurnInterval = .1f;
    [Min(.01f)] public float headingSmoothing = .065f;
    [Min(0)] public float stopGrace = .14f;
    Animator animator;
    Vector2 previous, velocity;
    float lastTurn, movingRemaining, gait;
    int direction = 1;
    public bool IsMoving => movingRemaining > 0;
    public int Direction => direction;
    public float Gait => gait;
    public int DirectionChanges { get; private set; }
    static readonly int DirectionHash = Animator.StringToHash("Direction");
    void Awake() { animator=GetComponent<Animator>(); }
    void OnEnable()
    {
        previous=transform.position; velocity=Vector2.zero; gait=movingRemaining=0;
        direction=1; lastTurn=-10; DirectionChanges=0;
        animator.SetInteger(DirectionHash,direction);
    }
    void LateUpdate()
    {
        Vector2 position=transform.position, delta=position-previous; previous=position;
        float dt=Time.deltaTime;
        if(dt<=0) return;
        if(delta.sqrMagnitude>2.25f) { velocity=Vector2.zero; movingRemaining=0; return; }
        float distance=delta.magnitude;
        if(distance>dt*.06f)
        {
            movingRemaining=stopGrace;
            gait=Mathf.Repeat(gait+distance/Mathf.Max(.1f,strideDistance),1);
        }
        else movingRemaining=Mathf.Max(0,movingRemaining-dt);
        velocity=Vector2.Lerp(velocity,delta/dt,1-Mathf.Exp(-dt/headingSmoothing));
        if(velocity.sqrMagnitude>.0064f)
        {
            float angle=Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg;
            int candidate=(Mathf.RoundToInt(angle/45f)+10)%8;
            float change=Mathf.Abs(Mathf.DeltaAngle((direction-2)*45,angle));
            if(candidate!=direction && change>22.5f+turnHysteresis &&
               (Time.time-lastTurn>=minimumTurnInterval || change>100))
            { direction=candidate; lastTurn=Time.time; DirectionChanges++; }
        }
        animator.SetInteger(DirectionHash,direction);
    }
    void OnDisable() { velocity=Vector2.zero; movingRemaining=gait=0; }
}
