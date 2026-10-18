using UnityEngine;

// Ground geometry uses the same locked direction, collision width and wave
// clock as the attack. It never applies damage or scales the collision root.
[DefaultExecutionOrder(210), DisallowMultipleComponent]
public sealed class EnemyGroundTelegraph : MonoBehaviour
{
    [SerializeField] Material materialTemplate;
    FastChargeAttack charge;
    HeavyShockwaveAttack shock;
    RangedEnemyAttack spit;
    EnemyHealth health;
    CoreEnemyAnimation animationDriver;
    EnemyAbilityPresentation.Surface ground;
    Material material;
    GameObject ownedGround;
    public bool Visible => ground!=null && ground.Visible;
    public float DisplayedRadius { get; private set; }
    public float DisplayedLength { get; private set; }
    public Vector2 DisplayedDirection { get; private set; }
    void Awake()
    {
        charge=GetComponent<FastChargeAttack>(); shock=GetComponent<HeavyShockwaveAttack>(); spit=GetComponent<RangedEnemyAttack>();
        health=GetComponent<EnemyHealth>(); animationDriver=GetComponent<CoreEnemyAnimation>();
        if(materialTemplate==null) return;
        material=new Material(materialTemplate); material.mainTexture=Texture2D.whiteTexture;
        ownedGround=new GameObject("Enemy locked attack telegraph (runtime)");
        ground=new EnemyAbilityPresentation.Surface(ownedGround.transform,"Attack geometry",material,"Pickups");
    }
    void LateUpdate()
    {
        if(ground==null) return;
        ground.Clear(); DisplayedRadius=DisplayedLength=0; DisplayedDirection=Vector2.zero;
        if(health.IsDead || animationDriver.IsPreview) { ground.Apply(); return; }
        var sample=animationDriver.AttackSample;
        if(shock!=null)
        {
            Vector2 center=shock.UsesExpandingWave ? shock.WaveCenter : (Vector2)transform.position;
            float radius=shock.VisualShockwaveRadius;
            if(sample.Phase==EnemyAttackVisualPhase.Windup)
            {
                DisplayedRadius=radius;
                ground.Ellipse(center,Vector2.one*radius,new Color(1,.52f,.08f,.08f+.08f*sample.Progress));
                ground.Ring(center,radius-.05f,.1f,new Color(1,.65f,.16f,.9f));
                ground.Ring(center,Mathf.Lerp(radius,.45f,sample.Progress),.075f,new Color(1,.88f,.45f,.85f));
            }
            else if(sample.Phase==EnemyAttackVisualPhase.Active && shock.UsesExpandingWave)
            {
                DisplayedRadius=shock.WaveRadius;
                ground.Ring(center,Mathf.Max(.05f,shock.WaveRadius),shock.WaveWidth,new Color(1,.69f,.22f,.92f));
                ground.Ring(center,radius-.025f,.05f,new Color(1,.7f,.3f,.18f));
            }
        }
        else if(sample.Phase==EnemyAttackVisualPhase.Windup)
        {
            Vector2 direction=sample.Direction.normalized, origin=transform.position;
            DisplayedDirection=direction;
            if(charge!=null)
            {
                float length=charge.VisualChargeDistance;
                float width=charge.VisualChargeWidth;
                DisplayedLength=length;
                Vector2 end=origin+direction*length, side=Vector2.Perpendicular(direction)*width*.5f;
                var edge=new Color(1,.53f,.13f,.95f);
                ground.Line(origin,end,width,new Color(1,.34f,.06f,.14f+.12f*sample.Progress));
                ground.Ellipse(end,Vector2.one*width*.5f,new Color(1,.34f,.06f,.14f+.12f*sample.Progress));
                ground.Ring(end,width*.5f,.04f,edge);
                ground.Line(origin+side,end+side,.045f,edge); ground.Line(origin-side,end-side,.045f,edge);
                for(float d=.8f;d<length;d+=1.15f)
                {
                    Vector2 tip=origin+direction*d;
                    ground.Line(tip-direction*.3f+side*.75f,tip,.06f,edge);
                    ground.Line(tip-direction*.3f-side*.75f,tip,.06f,edge);
                }
                ground.Line(origin,Vector2.Lerp(origin,end,sample.Progress),.07f,new Color(1,.91f,.55f));
            }
            else if(spit!=null)
            {
                float length=spit.VisualAimLength; DisplayedLength=length;
                Vector2 end=origin+direction*length;
                ground.Line(origin,end,.14f,new Color(.65f,.95f,.15f,.17f));
                ground.Line(origin,end,.035f,new Color(.85f,1,.25f,.85f));
                ground.Ring(origin,.55f,.06f,new Color(.85f,1,.35f,.95f),sample.Progress);
                ground.Ring(end,.22f,.045f,new Color(.85f,1,.35f,.85f));
                Vector2 side=Vector2.Perpendicular(direction)*.18f;
                ground.Line(end-direction*.35f+side,end,.05f,new Color(.9f,1,.45f));
                ground.Line(end-direction*.35f-side,end,.05f,new Color(.9f,1,.45f));
            }
        }
        ground.Apply();
    }
    void OnDisable() { ground?.Hide(); DisplayedRadius=DisplayedLength=0; DisplayedDirection=Vector2.zero; }
    void OnDestroy() { ground?.Dispose(); if(ownedGround!=null) Destroy(ownedGround); if(material!=null) Destroy(material); }
}
