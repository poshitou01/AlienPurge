using UnityEngine;

// The original components remain compatible without the scene's PlayerAbilityFeel opt-in.
public partial class PlayerDash
{
    PlayerAbilityFeel abilityFeel;
    public KeyCode InputKey => dashKey;
    public Vector2 RequestedDirection => GetRequestedDashDirection();
    public bool TryActivateBuffered(Vector2 direction)
    {
        if(!isActiveAndEnabled||!IsReady||direction.sqrMagnitude<.0001f||!abilityState.TryEnterAbility(PlayerAbilityMode.Dashing))return false;
        StartDash(direction);return true;
    }
    public void CancelForTransition() { EndDash(true); }
    void ResponsiveDashStep()
    {
        float dt=Mathf.Min(Time.fixedDeltaTime,dashTimeRemaining);
        float elapsed=dashDuration-dashTimeRemaining;
        // Integrate the linear 1.35 -> .65 speed curve exactly; total distance stays speed * duration.
        float multiplier=1.35f-.7f*(elapsed+dt*.5f)/dashDuration;
        float wanted=dashSpeed*dt*multiplier;
        float distance=abilityFeel.DashDistance(dashDirection,wanted);
        rb.MovePosition(rb.position+dashDirection*distance);
        dashTimeRemaining-=dt;
        if(distance<wanted-.001f||dashTimeRemaining<=.0001f)EndDash(false);
    }
}
public partial class PlayerJetJump
{
    PlayerAbilityFeel abilityFeel;
    public KeyCode InputKey => jumpKey;
    public bool TryActivateBuffered()
    {
        if(!isActiveAndEnabled||!IsReady)return false;
        TryStartJump();return isJumping;
    }
    public void CancelForTransition() { EndJump(true); }
    void ResponsiveLiftOff()
    {
        // Present the takeoff in the accepted input frame, with no anticipation lockout.
        jumpElapsed=Mathf.Min(.012f,jumpDuration*.05f);
        float t=jumpElapsed/jumpDuration;
        UpdateVisualRoot(4*t*(1-t));UpdateShadow(4*t*(1-t));
        abilityFeel.PlayJump();
    }
}
public partial class PlayerPulseSkill
{
    PlayerAbilityFeel abilityFeel;
    public KeyCode InputKey => pulseKey;
    public float CastElapsed => castElapsed;
    public bool TryActivateBuffered()
    {
        if(!isActiveAndEnabled||!IsReady)return false;
        TryStartPulse();return isCasting;
    }
    public void CancelForTransition() { EndPulse(); }
}
