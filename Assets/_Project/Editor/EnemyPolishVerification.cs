using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class CoreEnemyArtVerification
{
    static IEnumerator PolishCases()
    {
        float previousCapture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
        for(int t=0;t<6;t++)
        {
            Recycle(); Place(player,new Vector2(14,8));
            var actor=Spawn((EnemyType)t,new Vector2(-8,-5));
            if(actor.TryGetComponent<SupportEnemyAbility>(out var support)) support.enabled=false;
            if(actor.TryGetComponent<ExploderEnemyAbility>(out var exploder)) exploder.enabled=false;
            var motion=actor.GetComponent<EnemyMotionPresentation>(); var body=actor.GetComponent<SpriteRenderer>();
            Check(motion!=null && !actor.GetComponent<EnemyAnimationController>().enabled && actor.GetComponent<Rigidbody2D>().interpolation==RigidbodyInterpolation2D.Interpolate,
                actor.Type+" opts into a single stable facing driver and interpolated native physics");
            yield return Walk(actor,Vector2.right,.3f);
            int turns=motion.DirectionChanges;
            var sprites=new HashSet<Sprite>();
            for(int frame=0;frame<90;frame++)
            {
                // Around the R/UR sector boundary: a raw round(angle/45) changes every frame.
                float angle=(frame%2==0 ? 21 : 24)*Mathf.Deg2Rad;
                Place(actor,(Vector2)actor.transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Time.deltaTime*2);
                sprites.Add(body.sprite); yield return null;
            }
            Check(motion.DirectionChanges==turns,actor.Type+" 90 alternating boundary samples cause zero facing chatter");
            Check(sprites.Count==8,actor.Type+" real distance-driven walk visits all eight gait frames");
            float before=motion.Gait; int direction=motion.Direction;
            yield return Walk(actor,Vector2.up,.3f);
            Check(motion.Direction==4 && direction!=motion.Direction,actor.Type+" sustained turn responds within 0.3s");
            before=motion.Gait; Time.timeScale=0; yield return Wait(.1f);
            Check(motion.Gait==before,actor.Type+" pause freezes the distance gait"); Time.timeScale=1;
            yield return Wait(.2f); Check(!motion.IsMoving,actor.Type+" stop grace expires without repeated move/idle switches");
            actor.ReturnToPool();
            Check(motion.Gait==0,actor.Type+" recycle clears gait phase");
            if(support!=null) support.enabled=true; if(exploder!=null) exploder.enabled=true;
        }
        // Real moving/turning Tank capture, with a camera that follows its world motion.
        Recycle(); var tank=Spawn(EnemyType.Heavy,new Vector2(-8,-5));
        Directory.CreateDirectory(Folder+"/Tank_Motion_V2");
        for(int frame=0;frame<96;frame++)
        {
            float angle=(-45+frame*.6f)*Mathf.Deg2Rad;
            Place(tank,(Vector2)tank.transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Time.deltaTime*1.5f);
            yield return null;
            if(frame%4==0) Capture("Tank_Motion_V2/"+(frame/4).ToString("000"),tank.transform.position,2.5f);
        }
        foreach(var type in new[]{EnemyType.Fast,EnemyType.Ranged})
        {
            Recycle(); Place(player,new Vector2(2,-3)); var actor=Spawn(type,new Vector2(0,-3),false,true);
            var driver=actor.GetComponent<CoreEnemyAnimation>(); var telegraph=actor.GetComponent<EnemyGroundTelegraph>();
            yield return Until(()=>driver.AttackSample.Phase==EnemyAttackVisualPhase.Windup,1,type+" telegraph starts with native windup");
            yield return Wait(.05f); var locked=driver.AttackSample.Direction;
            Check(telegraph.Visible && telegraph.DisplayedLength>2 && Vector2.Dot(telegraph.DisplayedDirection,locked)>.99f,type+" ground cue shows actual locked path");
            Place(player,new Vector2(-2,-3)); yield return Wait(.15f);
            Check(Vector2.Dot(driver.AttackSample.Direction,locked)>.99f && Vector2.Dot(telegraph.DisplayedDirection,locked)>.99f,type+" warning never tracks a player changing sides");
            Capture(type+"_LockedTelegraph",actor.transform.position,4.5f);
            actor.ReturnToPool(); Check(!telegraph.Visible,type+" recycling immediately hides the cue");
        }
        yield return TankWaveCases();
        Recycle(); Place(player,new Vector2(14,-8));
        var source=Spawn(EnemyType.Support,new Vector2(-8,5)); var aura=source.GetComponent<SupportEnemyAbility>();
        var allies=new List<PooledEnemy>();
        for(int i=0;i<10;i++) allies.Add(Spawn(EnemyType.Normal,new Vector2(-6+i,5)));
        yield return Wait(.3f);
        Check(aura.TargetCount==8 && allies.Count(a=>a.GetComponent<EnemyContactDamage>().Damage==2)==8,"Large Support aura still caps ten nearby candidates at eight");
        for(int i=1;i<allies.Count;i++) allies[i].ReturnToPool();
        Place(allies[0],new Vector2(5,5)); yield return Wait(.3f);
        Check(allies[0].GetComponent<EnemyContactDamage>().Damage==2,"Support reaches an ally 13 world units away");
        Place(allies[0],new Vector2(7,5));
        Check(allies[0].GetComponent<EnemyContactDamage>().Damage==1,"Support revokes beyond 14 units immediately");
        Recycle(); Time.captureDeltaTime=previousCapture;
    }
    static IEnumerator Walk(PooledEnemy actor,Vector2 direction,float seconds)
    {
        float end=Time.time+seconds;
        while(Time.time<end) { Place(actor,(Vector2)actor.transform.position+direction*Time.deltaTime*2); yield return null; }
    }
    static IEnumerator TankWaveCases()
    {
        Recycle(); Place(player,new Vector2(2.8f,-3));
        var tank=Spawn(EnemyType.Heavy,new Vector2(0,-3),false,true); var wave=tank.GetComponent<HeavyShockwaveAttack>();
        int hp=player.CurrentHealth,damage=tank.GetComponent<EnemyContactDamage>().Damage;
        var extra=player.gameObject.AddComponent<CircleCollider2D>(); extra.radius=.15f;
        yield return Until(()=>wave.VisualSample.Phase==EnemyAttackVisualPhase.Active,2,"Tank releases a travelling wave after windup");
        Check(player.CurrentHealth==hp && wave.WaveRadius<1,"Tank release is not an instant full-circle hit");
        yield return Until(()=>player.CurrentHealth<hp,1,"Tank wave damages when the front reaches the player collider");
        yield return Wait(.8f);
        Check(player.CurrentHealth==hp-damage,"Tank wave with duplicate colliders damages only once");
        Object.Destroy(extra);
        Recycle(); Place(player,new Vector2(2.8f,-3)); tank=Spawn(EnemyType.Heavy,new Vector2(0,-3),false,true); wave=tank.GetComponent<HeavyShockwaveAttack>();
        hp=player.CurrentHealth;
        yield return Until(()=>wave.VisualSample.Phase==EnemyAttackVisualPhase.Windup,1,"Tank dodge test enters windup");
        Place(player,new Vector2(7,-3)); yield return Wait(2.4f);
        Check(player.CurrentHealth==hp,"Leaving Tank's marked circle during windup avoids damage");
        Recycle(); Place(player,new Vector2(2.8f,-3)); tank=Spawn(EnemyType.Heavy,new Vector2(0,-3),false,true); wave=tank.GetComponent<HeavyShockwaveAttack>();
        hp=player.CurrentHealth;
        yield return Until(()=>wave.VisualSample.Phase==EnemyAttackVisualPhase.Active && wave.WaveRadius>1.6f,2,"Tank front has travelled beyond the center");
        Place(player,new Vector2(.9f,-3)); yield return Wait(.6f);
        Check(player.CurrentHealth==hp,"Ground behind a passed Tank front is safe during active/recovery");
        tank.ReturnToPool(); Check(!tank.GetComponent<EnemyGroundTelegraph>().Visible,"Tank recycle clears an expanding wave");
    }
}
