using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Actual Editor Play Mode: native pools, physics, A*, weapons and ability clocks.
[InitializeOnLoad]
public static partial class CoreEnemyArtVerification
{
    const string Key="CoreEnemies.ArtVerification", Folder="Docs/Enemies/CoreEnemies/PolishV2/Core";
    static readonly Stack<IEnumerator> jobs=new Stack<IEnumerator>();
    static readonly List<string> report=new List<string>(), errors=new List<string>();
    static double started;
    static bool done;
    static CoreEnemyArtLab lab;
    static PlayerHealth player;
    static CoreEnemyArtVerification()
    {
        EditorApplication.playModeStateChanged+=Changed; EditorApplication.update+=Tick;
        Application.logMessageReceived+=(message,trace,type)=> { if(SessionState.GetBool(Key,false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert)) errors.Add(message+"\n"+trace); };
    }
    [MenuItem("AlienPurge/Core Enemies/4 Run Play Mode Verification")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        EnemyPolishSetup.Configure();
        string installedScene=File.ReadAllText(CoreEnemyArtSetup.ScenePath);
        EnemyPolishSetup.Configure();
        if(File.ReadAllText(CoreEnemyArtSetup.ScenePath)!=installedScene)
            throw new InvalidOperationException("Repeated setup changed the installed test-scene identities");
        Directory.CreateDirectory(Folder);
        Object.FindObjectOfType<CoreEnemyArtLab>().spawnOnStart=false;
        SessionState.SetBool(Key,true); SessionState.SetInt(Key+".Exit",1); EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            jobs.Clear(); report.Clear(); errors.Clear(); done=false; started=EditorApplication.timeSinceStartup;
            report.Add("Unity "+Application.unityVersion+" / real Editor Play Mode / "+SystemInfo.graphicsDeviceType);
            jobs.Push(Suite());
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false); EditorSceneManager.OpenScene(CoreEnemyArtSetup.ScenePath);
            CoreEnemyTestProfileIsolation.Restore();
            if(Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key+".Exit",1));
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || done || jobs.Count==0) return;
        try
        {
            if(errors.Count>0) throw new InvalidOperationException(errors[0]);
            if(EditorApplication.timeSinceStartup-started>480) throw new TimeoutException("Eight minute verification timeout");
            var job=jobs.Peek();
            if(!job.MoveNext()) { jobs.Pop(); if(jobs.Count==0) Finish(true); }
            else if(job.Current is IEnumerator child) jobs.Push(child);
        }
        catch(Exception e) { report.Add("FAIL: "+e); Finish(false); }
    }
    static void Finish(bool pass)
    {
        done=true; jobs.Clear(); if(errors.Count>0) { pass=false; report.AddRange(errors); }
        Time.timeScale=1; Time.captureDeltaTime=0; report.Add(pass?"RESULT: PASS":"RESULT: FAIL"); File.WriteAllLines(Folder+"/PlayMode_Result.txt",report);
        SessionState.SetInt(Key+".Exit",pass?0:1); EditorApplication.isPlaying=false;
    }
    static void Check(bool value,string label)
    {
        report.Add((value?"PASS: ":"FAIL: ")+label); if(!value) throw new InvalidOperationException(label);
    }
    static IEnumerator Wait(float seconds)
    {
        if(Time.captureDeltaTime>0 && Time.timeScale>0)
        { float until=Time.time+seconds*Time.timeScale; while(Time.time<until) yield return null; }
        else { float until=Time.realtimeSinceStartup+seconds; while(Time.realtimeSinceStartup<until) yield return null; }
    }
    static IEnumerator Until(Func<bool> condition,float seconds,string label)
    {
        float until=Time.realtimeSinceStartup+seconds;
        while(!condition() && Time.realtimeSinceStartup<until) yield return null;
        Check(condition(),label);
    }
    static void Place(Component value,Vector2 point)
    {
        value.transform.position=point; if(value.TryGetComponent<Rigidbody2D>(out var rb)) { rb.position=point; rb.velocity=Vector2.zero; }
        Physics2D.SyncTransforms();
    }
    static Behaviour Ability(PooledEnemy enemy) => enemy.Type==EnemyType.Fast ? (Behaviour)enemy.GetComponent<FastChargeAttack>() : enemy.Type==EnemyType.Heavy ? (Behaviour)enemy.GetComponent<HeavyShockwaveAttack>() : enemy.GetComponent<RangedEnemyAttack>();
    static PooledEnemy Spawn(EnemyType type,Vector2 point,bool move=false,bool attack=false)
    {
        var enemy=lab.Spawn(type,point); Check(enemy!=null && !enemy.IsReturned,"Native pool spawn "+type);
        if(!move) enemy.GetComponent<EnemyMovement>().enabled=false;
        // Tests deliberately disable skills, including on actors killed while disabled.
        // Restore the requested fixture state even when the pool returns that actor.
        if(Ability(enemy)!=null) Ability(enemy).enabled=attack;
        return enemy;
    }
    static void Recycle()
    {
        lab.RecycleAll();
        foreach(var projectile in Object.FindObjectsOfType<EnemyProjectile>()) projectile.ReturnToPool();
        foreach(var orb in Object.FindObjectsOfType<ExperienceOrb>()) orb.ReturnToPool();
        player.RestoreHealth(player.MaxHealth); player.CancelTemporaryInvulnerability();
    }
    static IEnumerator Suite()
    {
        yield return Wait(.8f); lab=Object.FindObjectOfType<CoreEnemyArtLab>(); player=Object.FindObjectOfType<PlayerHealth>();
        player.GetComponent<PlayerShooting>().enabled=false; player.GetComponent<PlayerMovement>().enabled=false;
        Recycle(); Place(player,new Vector2(14,-8));
        Capture("Lab_Initial",Vector2.zero,10.5f); // Warm render resources before skill timing captures.
        Check(Object.FindObjectsOfType<EnemyPool>().Length==1 && Object.FindObjectsOfType<GameManager>().Length==1 && Object.FindObjectsOfType<CoreEnemyArtLab>().Length==1,"Repeated configure leaves one native manager / pool / lab");
        Check(!Object.FindObjectsOfType<EnemyAbilityTestLab>().Any() && !Object.FindObjectsOfType<HiveTyrantCombat>().Any(),"Independent lab excludes other test controllers and Boss encounter");
        foreach(var type in new[]{EnemyType.Normal,EnemyType.Fast,EnemyType.Heavy,EnemyType.Ranged})
        {
            Debug.Log("Core verification: directions / reuse / navigation / weapons for "+type);
            yield return DirectionAndFrames(type);
            yield return Reuse(type);
            yield return NavigationAndWeapon(type);
        }
        foreach(var type in new[]{EnemyType.Fast,EnemyType.Heavy,EnemyType.Ranged})
        {
            Debug.Log("Core verification: native skill and lethal interruption for "+type);
            yield return NativeAttack(type);
            yield return KillDuringWindup(type);
        }
        yield return PolishCases();
        yield return Compatibility();
        yield return PreviewAndRestart();
        Check(errors.Count==0,"No Unity errors, exceptions or assertions");
    }
    static IEnumerator DirectionAndFrames(EnemyType type)
    {
        Recycle(); Place(player,new Vector2(14,-8)); var enemy=Spawn(type,new Vector2(-8,3));
        var driver=enemy.GetComponent<CoreEnemyAnimation>(); var body=enemy.GetComponent<SpriteRenderer>(); var animator=enemy.GetComponent<Animator>();
        string name=CoreEnemyArtSetup.Enemies[(int)type];
        Vector3 scale=enemy.transform.localScale; float radius=enemy.GetComponent<CircleCollider2D>().radius;
        enemy.GetComponent<EnemyHealth>().TakeDamage(1);
        Check(body.color.r>1 && body.color.g>1 && body.color.b>1,name+" native hit visibly brightens the authored sprite");
        yield return Wait(.15f); Check(body.color==Color.white,name+" native hit flash restores tint");
        for(int d=0;d<8;d++)
        {
            float angle=(d-2)*45*Mathf.Deg2Rad;
            float until=Time.time+.23f;
            while(Time.time<until) { Place(enemy,(Vector2)enemy.transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Time.deltaTime*2); yield return null; }
            Check(animator.GetInteger("Direction")==d && driver.CurrentDirection==d && driver.CurrentPose==CoreEnemyAnimation.Pose.Move,name+" native movement selects "+CoreEnemyArtSetup.Directions[d]);
        }
        yield return Wait(.2f); Check(driver.CurrentPose==CoreEnemyAnimation.Pose.Idle,name+" stopped movement returns to idle and retains facing");
        for(int d=0;d<8;d++)
        {
            driver.Preview(CoreEnemyAnimation.Pose.Move,d); var frames=new HashSet<Sprite>(); float end=Time.time+1.2f;
            while(Time.time<end) { frames.Add(body.sprite); yield return null; }
            Check(frames.Count==8 && frames.All(s=>AssetDatabase.GetAssetPath(s).Contains("/"+name+"/Authored/Move/"+CoreEnemyArtSetup.Directions[d]+"/")),name+" "+CoreEnemyArtSetup.Directions[d]+" plays eight own walk frames");
            var poses=type==EnemyType.Normal ? new[]{CoreEnemyAnimation.Pose.Idle,CoreEnemyAnimation.Pose.Death} : new[]{CoreEnemyAnimation.Pose.Idle,CoreEnemyAnimation.Pose.Windup,CoreEnemyAnimation.Pose.Attack,CoreEnemyAnimation.Pose.Recover,CoreEnemyAnimation.Pose.Death};
            foreach(var pose in poses)
            {
                driver.Preview(pose,d); yield return Wait(.02f);
                Check(body.sprite!=null && body.sprite.name.StartsWith(name+"_") && body.sprite.name.Contains("_"+CoreEnemyArtSetup.Directions[d]+"_"),name+" "+pose+" "+CoreEnemyArtSetup.Directions[d]+" has own directional frames");
            }
        }
        Check(enemy.transform.localScale==scale && enemy.transform.rotation==Quaternion.identity && enemy.GetComponent<CircleCollider2D>().radius==radius,name+" all animations preserve root scale / rotation / collider");
    }
    static IEnumerator Reuse(EnemyType type)
    {
        float previousCapture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
        Recycle(); Place(player,new Vector2(14,-8));
        while(EnemyPool.Instance.GetAvailableCount(type)>1) Spawn(type,new Vector2(13,8));
        var enemy=Spawn(type,new Vector2(-8,3)); var driver=enemy.GetComponent<CoreEnemyAnimation>(); var body=enemy.GetComponent<SpriteRenderer>();
        for(int cycle=0;cycle<3;cycle++)
        {
            if(Ability(enemy)!=null) Ability(enemy).enabled=true;
            int kills=GameManager.Instance.KillCount, orbs=ExperienceOrbPool.Instance.ActiveCount;
            enemy.GetComponent<EnemyHealth>().TakeDamage(9999); enemy.GetComponent<EnemyHealth>().TakeDamage(9999);
            Check(driver.CurrentPose==CoreEnemyAnimation.Pose.Death && GameManager.Instance.KillCount==kills+1 && ExperienceOrbPool.Instance.ActiveCount==orbs+1,type+" repeated lethal hit gives one kill and XP orb, cycle "+cycle);
            yield return Wait(.44f);
            Check(body.color.a<1 && !enemy.IsReturned && !driver.ImpactVisible,type+" collapse fades before native pool return, cycle "+cycle);
            if(cycle==0)
            {
                float elapsed=driver.DeathElapsed,alpha=body.color.a; Time.timeScale=0; yield return Wait(.2f);
                Check(Mathf.Approximately(driver.DeathElapsed,elapsed) && Mathf.Approximately(body.color.a,alpha) && !enemy.IsReturned,type+" pause freezes collapse and return together"); Time.timeScale=1;
                enemy.ReturnToPool(); // Explicit early recycle exercises stale delayed callback cleanup.
            }
            else yield return Until(()=>enemy.IsReturned,1,type+" native death routine returns the object");
            var next=Spawn(type,new Vector2(-8,3));
            Check(next==enemy && !driver.IsPreview && driver.CurrentPose==CoreEnemyAnimation.Pose.Idle && driver.CurrentDirection==1 && body.color==Color.white && enemy.GetComponent<Animator>().speed==1,type+" same instance resets pose / direction / alpha / speed, cycle "+cycle);
            // Spawn helper freezes abilities for inspection; enable and observe the real lifecycle.
            if(Ability(enemy)!=null) Ability(enemy).enabled=true;
            yield return Wait(.65f);
            Check(!enemy.IsReturned && !enemy.GetComponent<EnemyHealth>().IsDead && GameManager.Instance.KillCount==kills+1 && !driver.ImpactVisible,type+" previous life cannot damage/recycle/reward next life, cycle "+cycle);
        }
        Time.captureDeltaTime=previousCapture;
    }
    static IEnumerator NavigationAndWeapon(EnemyType type)
    {
        Recycle(); Place(player,new Vector2(0,-6)); var enemy=Spawn(type,new Vector2(-7,5),true);
        Vector2 begin=enemy.transform.position; bool path=false; float end=Time.time+3;
        while(Time.time<end) { path|=enemy.GetComponent<EnemyPathNavigator>().HasValidPath; yield return null; }
        Check(path && Vector2.Distance(begin,enemy.transform.position)>.5f,type+" obtains a native A* path and physically moves around cover");
        Check(Physics2D.OverlapCircle(enemy.transform.position,.2f,1<<7)==null,type+" collision guard keeps body outside environment");
        Recycle(); enemy=Spawn(type,new Vector2(0,-2)); int hp=enemy.GetComponent<EnemyHealth>().CurrentHealth;
        var weapon=player.GetComponent<WeaponManager>(); weapon.AimAt(enemy.transform.position);
        Check(weapon.TryFire(),type+" native weapon fires a pooled bullet"); yield return Wait(.7f);
        Check(enemy.GetComponent<EnemyHealth>().CurrentHealth<hp,type+" native bullet physics damages the existing collider");
        enemy.GetComponent<EnemyHealth>().TakeDamage(9999); yield return Wait(.65f);
        var xp=player.GetComponent<PlayerExperience>(); int before=xp.CurrentExperience,level=xp.CurrentLevel;
        Place(player,enemy.transform.position); yield return Wait(.5f);
        Check(xp.CurrentExperience>before || xp.CurrentLevel>level,type+" actual XP pickup collects death reward");
    }
    static void Capture(string name,Vector2 center,float size=4)
    {
        var camera=Camera.main; var position=camera.transform.position; float zoom=camera.orthographicSize;
        camera.transform.position=new Vector3(center.x,center.y,position.z); camera.orthographicSize=size;
        var target=new RenderTexture(1280,720,24); var before=camera.targetTexture; var active=RenderTexture.active;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=before; RenderTexture.active=active; Object.Destroy(image); Object.Destroy(target);
        camera.transform.position=position; camera.orthographicSize=zoom;
    }
}
