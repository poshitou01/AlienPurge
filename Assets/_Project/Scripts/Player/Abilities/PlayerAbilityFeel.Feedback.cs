using UnityEngine;

public sealed partial class PlayerAbilityFeel
{
    static readonly Color DashTint=new Color(.48f,.95f,1f), PulseTint=new Color(.55f,.88f,1f);
    sealed class Ghost { public SpriteRenderer[] parts; public float until; }
    readonly Ghost[] ghosts=new Ghost[7];
    readonly Vector3[] circle=new Vector3[49];
    SpriteRenderer[] originals;
    GameObject feedbackRoot;
    Material ownedMaterial;
    AudioSource dashAudio,jumpAudio,pulseAudio;
    LineRenderer pulseOuter,pulseInner,jumpRing;
    readonly LineRenderer[] streaks=new LineRenderer[3];
    float nextGhost,pulseBegan=-10,jumpBegan=-10,dashBegan=-10,pulseRange,jumpRange;
    Vector3 pulseOrigin,jumpOrigin;
    Vector2 trailDirection;
    int ghostIndex;
    public int VisibleAfterimages
    {
        get { int count=0;foreach(var g in ghosts)if(g!=null&&g.until>Time.time)count++;return count; }
    }
    void CreateFeedback()
    {
        feedbackRoot=new GameObject("Player_AbilityFeedback");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(feedbackRoot,gameObject.scene);
        if(effectMaterial==null)
        {
            ownedMaterial=new Material(Shader.Find("Sprites/Default"));effectMaterial=ownedMaterial;
        }
        var visual=transform.Find("VisualRoot");
        originals=visual!=null?visual.GetComponentsInChildren<SpriteRenderer>(true):new SpriteRenderer[0];
        for(int i=0;i<ghosts.Length;i++)
        {
            ghosts[i]=new Ghost{parts=new SpriteRenderer[originals.Length],until=-1};
            for(int p=0;p<originals.Length;p++)
            {
                var part=new GameObject("DashEcho_"+i+"_"+p).AddComponent<SpriteRenderer>();
                part.transform.SetParent(feedbackRoot.transform);part.sharedMaterial=effectMaterial;
                part.sortingLayerName="VFX";part.sortingOrder=-5;part.enabled=false;ghosts[i].parts[p]=part;
            }
        }
        pulseOuter=NewLine("PulseFront",.09f);pulseInner=NewLine("PulseEcho",.035f);
        jumpRing=NewLine("JetGroundBurst",.045f);
        for(int i=0;i<streaks.Length;i++){streaks[i]=NewLine("DashStreak_"+i,.035f);streaks[i].positionCount=2;streaks[i].loop=false;}
        dashAudio=NewAudio("DashJet");jumpAudio=NewAudio("JumpJet");pulseAudio=NewAudio("PulseImpact");
    }
    LineRenderer NewLine(string name,float width)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(feedbackRoot.transform);
        line.sharedMaterial=effectMaterial;line.useWorldSpace=true;line.loop=true;line.positionCount=48;
        line.widthMultiplier=width;line.numCornerVertices=2;line.sortingLayerName="VFX";line.sortingOrder=2;line.enabled=false;return line;
    }
    AudioSource NewAudio(string name)
    {
        var source=new GameObject(name).AddComponent<AudioSource>();source.transform.SetParent(transform,false);
        source.playOnAwake=false;source.loop=false;source.spatialBlend=0;return source;
    }
    void Sound(AudioSource source,AudioClip clip,float pitch,float strength)
    {
        if(source==null||clip==null)return;
        var audio=AudioManager.Instance;
        source.volume=feedbackVolume*(audio!=null?audio.MasterVolume*audio.SfxVolume:1);
        source.pitch=pitch;source.Stop();source.PlayOneShot(clip,strength);
    }
    public void PlayDash(Vector2 direction)
    {
        trailDirection=direction;dashBegan=Time.time;nextGhost=Time.time;
        StampGhost();Sound(dashAudio,dashSound,Random.Range(1.08f,1.15f),1);
        if(CameraFollow.Instance!=null)CameraFollow.Instance.Shake(.065f*cameraFeedback,.085f);
    }
    public void PlayJump()
    {
        jumpOrigin=transform.position;jumpBegan=Time.time;jumpRange=.65f;
        Sound(jumpAudio,jumpSound,1.12f,.8f);DrawRing(jumpRing,jumpOrigin,.16f,.6f,DashTint);
    }
    public void PlayLanding()
    {
        jumpOrigin=transform.position;jumpBegan=Time.time;jumpRange=.48f;
        if(CameraFollow.Instance!=null)CameraFollow.Instance.Shake(.025f*cameraFeedback,.06f);
    }
    public void PlayPulse(float radius)
    {
        pulseOrigin=transform.position;pulseRange=radius;pulseBegan=Time.time;
        Sound(pulseAudio,pulseSound,.86f,1);
        DrawRing(pulseOuter,pulseOrigin,.25f,.9f,PulseTint);
        if(CameraFollow.Instance!=null)CameraFollow.Instance.Shake(.12f*cameraFeedback,.13f);
    }
    void StampGhost()
    {
        if(originals==null)return;
        var ghost=ghosts[ghostIndex++%ghosts.Length];ghost.until=Time.time+.16f;
        for(int p=0;p<originals.Length;p++)
        {
            var source=originals[p];var part=ghost.parts[p];
            part.enabled=source!=null&&source.enabled&&source.gameObject.activeInHierarchy&&source.sprite!=null;
            if(!part.enabled)continue;
            part.sprite=source.sprite;part.flipX=source.flipX;part.flipY=source.flipY;
            part.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            part.transform.localScale=source.transform.lossyScale;
            part.color=new Color(DashTint.r,DashTint.g,DashTint.b,.48f);
        }
        nextGhost=Time.time+.026f;
    }
    void UpdateFeedback()
    {
        if(dash.IsActive&&Time.time>=nextGhost)StampGhost();
        foreach(var ghost in ghosts)
        {
            if(ghost==null)continue;
            float alpha=Mathf.Clamp01((ghost.until-Time.time)/.16f)*.48f;
            foreach(var part in ghost.parts)
            {if(alpha<=0)part.enabled=false;else part.color=new Color(DashTint.r,DashTint.g,DashTint.b,alpha);}
        }
        float dashFade=Mathf.Clamp01(1-(Time.time-dashBegan)/.24f);
        Vector2 normal=new Vector2(-trailDirection.y,trailDirection.x);
        for(int i=0;i<streaks.Length;i++)
        {
            var line=streaks[i];line.enabled=dashFade>0;
            if(!line.enabled)continue;
            Vector3 end=transform.position+(Vector3)(normal*(i-1)*.13f);
            line.SetPosition(0,end-(Vector3)trailDirection*(.9f+i*.19f));line.SetPosition(1,end-(Vector3)trailDirection*.2f);
            line.startColor=new Color(DashTint.r,DashTint.g,DashTint.b,0);line.endColor=new Color(1,1,1,dashFade*.7f);
        }
        float t=(Time.time-pulseBegan)/.34f;
        pulseOuter.enabled=pulseInner.enabled=t<1&&t>=0;
        if(t<1&&t>=0)
        {
            float radius=pulseRange*(1-Mathf.Pow(1-t,3));
            DrawRing(pulseOuter,pulseOrigin,Mathf.Max(.25f,radius),(1-t)*.9f,PulseTint);
            DrawRing(pulseInner,pulseOrigin,Mathf.Max(.16f,radius*.83f),(1-t)*.5f,Color.white);
        }
        float j=(Time.time-jumpBegan)/.2f;jumpRing.enabled=j<1&&j>=0;
        if(j<1&&j>=0)DrawRing(jumpRing,jumpOrigin,Mathf.Lerp(.14f,jumpRange,j),.65f*(1-j),DashTint);
        var audio=AudioManager.Instance;
        float volume=feedbackVolume*(audio!=null?audio.MasterVolume*audio.SfxVolume:1);
        dashAudio.volume=jumpAudio.volume=pulseAudio.volume=volume;
    }
    void DrawRing(LineRenderer line,Vector3 center,float radius,float alpha,Color color)
    {
        line.enabled=true;color.a=alpha;line.startColor=line.endColor=color;
        for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;circle[i]=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;}
        // Reuse the positions buffer; the final duplicate is deliberately omitted on the loop.
        for(int i=0;i<48;i++)line.SetPosition(i,circle[i]);
    }
    void SilenceFeedback()
    {
        if(feedbackRoot==null)return;
        foreach(var ghost in ghosts)if(ghost!=null){ghost.until=-1;foreach(var part in ghost.parts)if(part!=null)part.enabled=false;}
        foreach(var line in streaks)if(line!=null)line.enabled=false;
        pulseOuter.enabled=pulseInner.enabled=jumpRing.enabled=false;
        pulseBegan=jumpBegan=dashBegan=-10;
        if(dashAudio!=null)dashAudio.Stop();if(jumpAudio!=null)jumpAudio.Stop();if(pulseAudio!=null)pulseAudio.Stop();
    }
    void DestroyFeedback()
    {
        if(feedbackRoot!=null)Destroy(feedbackRoot);
        if(ownedMaterial!=null)Destroy(ownedMaterial);
    }
}
