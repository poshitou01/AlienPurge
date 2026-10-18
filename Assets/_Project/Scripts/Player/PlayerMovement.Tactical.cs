using UnityEngine;

public partial class PlayerMovement
{
    [Header("Tactical locomotion (scene opt-in)")]
    [SerializeField] bool tacticalLocomotion;
    [SerializeField, Min(.01f)] float accelerationTime = .075f;
    [SerializeField, Min(.01f)] float brakingTime = .045f;
    [SerializeField, Range(0, 1)] float initialSpeedFraction = .45f;
    float travelSpeed;
    Vector2 travelDirection, previousPhysicsPosition, externalInput;
    bool positionSampled;
    PlayerHealth movementHealth;

    /// <summary>External input uses the same gameplay gates and motor as the keyboard.</summary>
    public bool UseExternalInput { get; set; }
    public Vector2 MeasuredVelocity { get; private set; }
    public Vector2 MotorVelocity => travelDirection * travelSpeed;
    public bool TacticalLocomotion => tacticalLocomotion;
    public void SetMovementInput(Vector2 value) { externalInput = Vector2.ClampMagnitude(value, 1); }

    bool MovementAllowed => canMove && Time.timeScale > 0 && !PauseMenuController.IsPaused
        && !UpgradeManager.IsChoosingUpgrade && !WeaponModuleSelectionManager.IsChoosingModule
        && (GameManager.Instance == null || GameManager.Instance.IsPlaying)
        && (movementHealth == null || !movementHealth.IsDead);

    void ReadTacticalInput()
    {
        if (movementHealth == null) movementHealth = GetComponent<PlayerHealth>();
        if (!MovementAllowed) { ResetMotor(); externalInput = Vector2.zero; return; }
        moveInput = UseExternalInput ? externalInput : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1);
        if (moveInput.sqrMagnitude > .0001f) lastNonZeroMoveDirection = moveInput.normalized;
    }
    void StepTacticalMovement()
    {
        if (!MovementAllowed) { ResetMotor(); return; }
        float dt = Time.fixedDeltaTime;
        // Sample the previous solved physics step, not the requested movement into a wall.
        Vector2 displacement = rb.position - previousPhysicsPosition;
        MeasuredVelocity = positionSampled && displacement.magnitude < Mathf.Max(1, moveSpeed * dt * 2)
            ? displacement / dt : Vector2.zero;
        previousPhysicsPosition = rb.position; positionSampled = true;
        float target = moveSpeed * moveInput.magnitude;
        if (target > .001f)
        {
            // Turn immediately, including a full reversal. The short ramp changes speed, never steering.
            travelDirection = moveInput.normalized;
            if (travelSpeed < .001f) travelSpeed = target * initialSpeedFraction;
            travelSpeed = Mathf.MoveTowards(travelSpeed, target, moveSpeed * dt / accelerationTime);
        }
        else travelSpeed = Mathf.MoveTowards(travelSpeed, 0, moveSpeed * dt / brakingTime);
        rb.MovePosition(rb.position + travelDirection * (travelSpeed * dt));
    }
    void ResetMotor()
    {
        travelSpeed = 0; travelDirection = moveInput = MeasuredVelocity = Vector2.zero; positionSampled = false;
    }
    void OnDisable() { if (tacticalLocomotion) { ResetMotor(); externalInput = Vector2.zero; } }
}
