using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class CoreEnemyArtVerification
{
    static IEnumerator NativeAttack(EnemyType type)
    {
        Recycle(); Place(player,new Vector2(2,-3));
        var enemy=Spawn(type,new Vector2(0,-3),false,true); var visual=enemy.GetComponent<CoreEnemyAnimation>();
        var body=enemy.GetComponent<SpriteRenderer>(); Vector3 scale=enemy.transform.localScale;
        float radius=enemy.GetComponent<CircleCollider2D>().radius;
        yield return Until(()=>visual.AttackSample.Phase==EnemyAttackVisualPhase.Windup,2,type+" native proximity starts windup");
        yield return Wait(.03f);
        Check(visual.CurrentPose==CoreEnemyAnimation.Pose.Windup && body.sprite.name.Contains("_Windup_"),type+" real windup selects authored body action");
        float progress=visual.AttackSample.Progress; Sprite sprite=body.sprite;
        Time.timeScale=0; yield return Wait(.18f);
        Check(Mathf.Approximately(progress,visual.AttackSample.Progress) && body.sprite==sprite,type+" pause freezes real skill clock and sampled sprite");
        Time.timeScale=.5f; float gameTime=Time.time, duration=visual.AttackSample.Duration;
        yield return Wait(.2f);
        Check(Mathf.Abs((visual.AttackSample.Progress-progress)*duration-(Time.time-gameTime))<.055f,type+" half speed samples the native scaled skill clock");
        Time.timeScale=1;
        int hp=player.CurrentHealth, kills=GameManager.Instance.KillCount;
        int damage=enemy.GetComponent<EnemyContactDamage>().Damage;
        var poses=new HashSet<CoreEnemyAnimation.Pose>(); var projectiles=new HashSet<int>();
        var timing=new List<string>{"frame,scaled_time,pose,phase,progress,player_hp"};
        bool ring=false,movedAfterHit=false; float finish=Time.time+2, next=Time.time; int capture=0;
        string name=CoreEnemyArtSetup.Enemies[(int)type];
        Directory.CreateDirectory(Folder+"/"+name+"_Live");
        foreach(var oldCapture in Directory.GetFiles(Folder+"/"+name+"_Live","*.png")) File.Delete(oldCapture);
        // Offline Camera.Render/PNG encoding can stall the Editor past the
        // Spitter's 0.2s settle phase. Use Unity's capture clock for this recording
        // only; native behaviour and physics still advance at their normal rates.
        float previousCaptureDelta=Time.captureDeltaTime;
        Time.captureDeltaTime=1f/60;
        while(Time.time<finish)
        {
            poses.Add(visual.CurrentPose);
            if(!movedAfterHit && player.CurrentHealth<hp) { movedAfterHit=true; Place(player,new Vector2(14,-8)); }
            foreach(var projectile in Object.FindObjectsOfType<EnemyProjectile>()) projectiles.Add(projectile.GetInstanceID());
            if(visual.ImpactVisible && !ring)
            {
                ring=true; Check(Mathf.Approximately(visual.DisplayedImpactRadius,enemy.GetComponent<HeavyShockwaveAttack>().VisualShockwaveRadius),"Tank impact boundary matches native damage radius");
            }
            if(Time.time>=next)
            {
                Capture(name+"_Live/"+capture.ToString("000"),new Vector2(1,-3),4);
                timing.Add(capture+","+Time.time.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+visual.CurrentPose+","+visual.AttackSample.Phase+","+visual.AttackSample.Progress.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+player.CurrentHealth);
                capture++; next=Time.time+.08f;
            }
            yield return null;
        }
        Time.captureDeltaTime=previousCaptureDelta;
        File.WriteAllLines(Folder+"/"+name+"_Live/timing.csv",timing);
        Check(poses.Contains(CoreEnemyAnimation.Pose.Attack) && poses.Contains(CoreEnemyAnimation.Pose.Recover),type+" native release passes through attack and recovery art");
        Check(player.CurrentHealth==hp-damage,type+" native ability damages player exactly once; no animation event damage");
        Check(GameManager.Instance.KillCount==kills,type+" ability presentation grants no spurious kill reward");
        Check(enemy.transform.localScale==scale && enemy.transform.rotation==Quaternion.identity && enemy.GetComponent<CircleCollider2D>().radius==radius,type+" skill art never changes collision root");
        if(type==EnemyType.Heavy) Check(ring && !visual.ImpactVisible,"Tank shows true-radius impact briefly and clears it");
        if(type==EnemyType.Ranged) Check(projectiles.Count==1,"Spitter releases one native pooled projectile");
        // Recycle in a live action has no implicit attack or reward.
        int after=player.CurrentHealth; enemy.ReturnToPool(); yield return Wait(.3f);
        Check(!visual.ImpactVisible && GameManager.Instance.KillCount==kills && player.CurrentHealth==after,type+" recycle cleans ability presentation without extra damage/reward");
    }
    static IEnumerator KillDuringWindup(EnemyType type)
    {
        Recycle(); Place(player,new Vector2(2,-3)); var enemy=Spawn(type,new Vector2(0,-3),false,true);
        var visual=enemy.GetComponent<CoreEnemyAnimation>();
        yield return Until(()=>visual.AttackSample.Phase==EnemyAttackVisualPhase.Windup,2,type+" interrupt test enters native windup");
        Place(player,new Vector2(14,-8)); int hp=player.CurrentHealth,kills=GameManager.Instance.KillCount,orbs=ExperienceOrbPool.Instance.ActiveCount;
        enemy.GetComponent<EnemyHealth>().TakeDamage(9999);
        Check(visual.CurrentPose==CoreEnemyAnimation.Pose.Death && !Ability(enemy).enabled && !visual.ImpactVisible,type+" death interrupts body/warning before native release");
        Check(enemy.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("Warning")).All(r=>!r.gameObject.activeInHierarchy),type+" native warning hidden immediately on death");
        yield return Wait(1.6f);
        Check(enemy.IsReturned && player.CurrentHealth==hp && EnemyProjectilePool.Instance.ActiveCount==0 && GameManager.Instance.KillCount==kills+1 && ExperienceOrbPool.Instance.ActiveCount==orbs+1,type+" killed windup never fires late or duplicates XP/kill");
    }
    static IEnumerator Compatibility()
    {
        foreach(var type in new[]{EnemyType.Normal,EnemyType.Fast,EnemyType.Heavy,EnemyType.Ranged})
        {
            Recycle(); Place(player,new Vector2(14,-8));
            var source=lab.Spawn(EnemyType.Support,new Vector2(-8,4)); source.GetComponent<EnemyMovement>().enabled=false;
            var target=Spawn(type,new Vector2(-7,4)); int baseDamage=target.GetComponent<EnemyDefinition>().FinalContactDamage;
            yield return Wait(.3f);
            Check(target.GetComponent<EnemyContactDamage>().Damage==baseDamage+1,type+" authored prefab still receives native Support buff");
            source.ReturnToPool();
            Check(target.GetComponent<EnemyContactDamage>().Damage==baseDamage,type+" Support recycle revokes buff without altering shared data");
        }
        Recycle(); Place(player,new Vector2(14,8));
        var ecology=new List<PooledEnemy>();
        for(int i=0;i<6;i++)
        {
            var actor=lab.Spawn((EnemyType)i,new Vector2(-5+i*2,-4)); ecology.Add(actor);
            actor.GetComponent<EnemyMovement>().enabled=false;
            if(Ability(actor)!=null) Ability(actor).enabled=false;
            foreach(var collider in actor.GetComponents<Collider2D>()) collider.enabled=false;
            if(actor.TryGetComponent<CoreEnemyAnimation>(out var core)) core.Preview(CoreEnemyAnimation.Pose.Idle,1);
            if(actor.TryGetComponent<SupportEnemyAbility>(out var support)) support.enabled=false;
            if(actor.TryGetComponent<ExploderEnemyAbility>(out var exploder)) exploder.enabled=false;
            if(actor.TryGetComponent<SupportExploderAnimation>(out var specialist)) specialist.Preview(SupportExploderAnimation.Pose.Idle,1);
        }
        yield return Wait(.1f); Capture("SixEnemy_StyleComparison",new Vector2(0,-4),4);
        foreach(var actor in ecology)
        {
            actor.ReturnToPool(); CoreEnemyArtLab.RestoreAttacks(actor);
            if(actor.TryGetComponent<SupportEnemyAbility>(out var support)) support.enabled=true;
            if(actor.TryGetComponent<ExploderEnemyAbility>(out var exploder)) exploder.enabled=true;
        }
        Recycle(); Place(player,new Vector2(0,-6));
        var boss=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HiveTyrantCombatSetup.BossPath)).GetComponent<HiveTyrantCombat>();
        Place(boss,new Vector2(0,3)); boss.AutomaticSkills=false; boss.Motor.SetTarget(player.transform); boss.Motor.enabled=false;
        yield return Wait(.1f); boss.CaptureEntryLoadout(); boss.Summoning.Prepare();
        Check(EnemyPool.Instance.TryGetConfiguredPrefab(EnemyType.Support,out _) && EnemyPool.Instance.TryGetConfiguredPrefab(EnemyType.Exploder,out _),"Compatibility test includes all six registered enemy types");
        Check(boss.PendingSpawnCount>0,"Boss prepares a real native summon plan");
        for(int i=0;i<boss.PendingSpawnCount;i++) Check((int)boss.Summoning.Pending(i).choice.type<=3,"Boss plan retains original four-type whitelist");
        boss.Summoning.Release();
        Check(boss.LivingSummons>0 && EnemyPool.Instance.GetActiveCount(EnemyType.Support)==0 && EnemyPool.Instance.GetActiveCount(EnemyType.Exploder)==0,"Boss releases only original four roles with updated art");
        Check(Object.FindObjectsOfType<PooledEnemy>().Where(e=>(int)e.Type<=3).All(e=>e.GetComponent<CoreEnemyAnimation>()!=null),"Boss native summons resolve the updated existing prefabs");
        boss.Summoning.Cleanup(); Object.Destroy(boss.gameObject); yield return Wait(.2f);
    }
    static IEnumerator PreviewAndRestart()
    {
        Recycle(); lab.EnterPreview(); yield return Wait(.15f);
        Check(Object.FindObjectsOfType<CoreEnemyAnimation>().Length==4 && lab.IsPreview,"Preview spawns all four through the real enemy pool");
        Check(Object.FindObjectsOfType<CoreEnemyAnimation>().All(a=>a.IsPreview && !a.ImpactVisible),"Preview is explicitly visual with no active impact");
        foreach(var direction in Enumerable.Range(0,8))
        {
            lab.SetPreview(direction,CoreEnemyAnimation.Pose.Idle); yield return Wait(.05f);
            Capture("Unity_Direction_"+CoreEnemyArtSetup.Directions[direction],new Vector2(0,-4),3.8f);
        }
        lab.SetPreview(1,CoreEnemyAnimation.Pose.Move); yield return Wait(.05f);
        Directory.CreateDirectory(Folder+"/Four_Move_Live");
        for(int i=0;i<12;i++) { Capture("Four_Move_Live/"+i.ToString("000"),new Vector2(0,-4),3.8f); yield return Wait(1f/12); }
        lab.SpawnEncounter(); yield return Wait(.15f);
        Check(!lab.IsPreview && Object.FindObjectsOfType<CoreEnemyAnimation>().All(a=>!a.IsPreview),"Leaving preview restores native combat on reused actors");
        Check(Object.FindObjectsOfType<FastChargeAttack>().All(a=>a.enabled) && Object.FindObjectsOfType<HeavyShockwaveAttack>().All(a=>a.enabled) && Object.FindObjectsOfType<RangedEnemyAttack>().All(a=>a.enabled),"Preview exit restores all three native attacks");
        CoreEnemyArtLab.Restart(); yield return Wait(1);
        lab=Object.FindObjectOfType<CoreEnemyArtLab>(); player=Object.FindObjectOfType<PlayerHealth>();
        Check(player.CurrentHealth==player.MaxHealth && GameManager.Instance.KillCount==0,"Restart clears health/kill state");
        Check(Object.FindObjectsOfType<EnemyPool>().Length==1 && Object.FindObjectsOfType<CoreEnemyArtLab>().Length==1 && Object.FindObjectsOfType<CoreEnemyAnimation>().Length==4,"Restart loads one lab / one pool / four native enemies");
        Check(Object.FindObjectsOfType<CoreEnemyAnimation>().All(a=>!a.IsPreview && a.GetComponent<SpriteRenderer>().color.a==1),"Restart has no old preview / death fade");
        Capture("TestScene_Overview",Vector2.zero,10.5f);
    }
}
