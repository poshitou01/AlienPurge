using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated inspection fixture. The normal encounter uses EnemyPool and native
// attacks. Preview is explicitly noncombat and restores its input/camera state.
public sealed class CoreEnemyArtLab : MonoBehaviour
{
    public bool spawnOnStart=true, previewOnStart;
    static readonly string[] Names={"Swarmer","Rusher","Tank","Spitter"};
    static readonly string[] Directions={"D","DR","R","UR","U","UL","L","DL"};
    readonly PooledEnemy[] actors=new PooledEnemy[4];
    Behaviour[] playerInputs;
    bool[] inputEnabled;
    Behaviour follow;
    bool followEnabled, preview;
    PlayerHealth player;
    Vector3 cameraPosition, playerPosition;
    float cameraSize, resumeScale=1;
    int direction=1, pose, duelType;
    string status="Four native enemies";
    GUIStyle title,text;
    public bool IsPreview => preview;
    void Start()
    {
        player=FindObjectOfType<PlayerHealth>();
        if(previewOnStart) EnterPreview(); else if(spawnOnStart) SpawnEncounter();
    }
    public PooledEnemy Spawn(EnemyType type,Vector2 position)
    {
        var actor=EnemyPool.Instance.GetEnemy(type,position,Quaternion.identity);
        if(actor!=null) actor.GetComponent<EnemyDefinition>().InitializeFromGlobalDifficulty(4,1.8f,1);
        return actor;
    }
    public void SpawnEncounter()
    {
        RecycleAll();
        Spawn(EnemyType.Normal,new Vector2(-7,4)); Spawn(EnemyType.Fast,new Vector2(1,6));
        Spawn(EnemyType.Heavy,new Vector2(9,4)); Spawn(EnemyType.Ranged,new Vector2(12,-3));
        status="Native four-enemy encounter";
    }
    public void RecycleAll()
    {
        foreach(var enemy in FindObjectsOfType<PooledEnemy>()) if(!enemy.IsReturned)
        {
            enemy.ReturnToPool(); RestoreAttacks(enemy);
        }
        for(int i=0;i<actors.Length;i++) actors[i]=null;
        if(preview)
        {
            if(player!=null) Place(player,playerPosition);
            for(int i=0;playerInputs!=null && i<playerInputs.Length;i++) if(playerInputs[i]!=null) playerInputs[i].enabled=inputEnabled[i];
            if(Camera.main!=null) { Camera.main.transform.position=cameraPosition; Camera.main.orthographicSize=cameraSize; }
            if(follow!=null) follow.enabled=followEnabled;
        }
        preview=false; status="Recycled without kills or rewards";
    }
    public static void RestoreAttacks(PooledEnemy enemy)
    {
        if(enemy.TryGetComponent<FastChargeAttack>(out var charge)) charge.enabled=true;
        if(enemy.TryGetComponent<HeavyShockwaveAttack>(out var shock)) shock.enabled=true;
        if(enemy.TryGetComponent<RangedEnemyAttack>(out var spit)) spit.enabled=true;
    }
    public void EnterPreview()
    {
        RecycleAll(); preview=true; direction=1; pose=0;
        foreach(var orb in FindObjectsOfType<ExperienceOrb>()) orb.ReturnToPool();
        if(player==null) player=FindObjectOfType<PlayerHealth>();
        playerPosition=player.transform.position;
        playerInputs=new Behaviour[]{player.GetComponent<PlayerMovement>(),player.GetComponent<PlayerShooting>(),player.GetComponent<PlayerDash>(),player.GetComponent<PlayerJetJump>()};
        inputEnabled=new bool[playerInputs.Length];
        for(int i=0;i<playerInputs.Length;i++) if(playerInputs[i]!=null) { inputEnabled[i]=playerInputs[i].enabled; playerInputs[i].enabled=false; }
        Place(player,new Vector2(14,8));
        var camera=Camera.main; cameraPosition=camera.transform.position; cameraSize=camera.orthographicSize;
        follow=camera.GetComponent<CameraFollow>(); followEnabled=follow!=null && follow.enabled; if(follow!=null) follow.enabled=false;
        camera.transform.position=new Vector3(0,-4,cameraPosition.z); camera.orthographicSize=4;
        for(int i=0;i<4;i++)
        {
            var actor=actors[i]=Spawn((EnemyType)i,new Vector2(-3.6f+i*2.4f,-4));
            actor.GetComponent<EnemyMovement>().enabled=false;
            if(actor.TryGetComponent<FastChargeAttack>(out var charge)) charge.enabled=false;
            if(actor.TryGetComponent<HeavyShockwaveAttack>(out var shock)) shock.enabled=false;
            if(actor.TryGetComponent<RangedEnemyAttack>(out var spit)) spit.enabled=false;
            foreach(var collider in actor.GetComponents<Collider2D>()) collider.enabled=false;
        }
        ApplyPreview();
    }
    public void SetPreview(int facing,CoreEnemyAnimation.Pose state)
    {
        direction=Mathf.Clamp(facing,0,7); pose=(int)state; ApplyPreview();
    }
    void ApplyPreview()
    {
        for(int i=0;i<4;i++) if(actors[i]!=null)
        {
            var chosen=(CoreEnemyAnimation.Pose)pose;
            if(i==0 && chosen!=CoreEnemyAnimation.Pose.Idle && chosen!=CoreEnemyAnimation.Pose.Move && chosen!=CoreEnemyAnimation.Pose.Death) chosen=CoreEnemyAnimation.Pose.Idle;
            actors[i].GetComponent<CoreEnemyAnimation>().Preview(chosen,direction);
        }
        status="PREVIEW / "+Directions[direction]+" / "+(CoreEnemyAnimation.Pose)pose+" (Swarmer: contact attack only)";
    }
    void Duel()
    {
        RecycleAll(); Place(player,new Vector2(0,-6));
        Spawn((EnemyType)duelType,new Vector2(0,0)); status="Duel / "+Names[duelType]; duelType=(duelType+1)%4;
    }
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F9)) Restart();
        if(Input.GetKeyDown(KeyCode.F8)) { if(Time.timeScale>0) { resumeScale=Time.timeScale; Time.timeScale=0; } else Time.timeScale=resumeScale; }
        if(Input.GetKeyDown(KeyCode.F11)) { resumeScale=resumeScale==1 ? .5f : 1; if(Time.timeScale>0) Time.timeScale=resumeScale; }
        if(GameManager.Instance==null || !GameManager.Instance.IsPlaying) return;
        if(Input.GetKeyDown(KeyCode.F5)) { if(preview) SpawnEncounter(); else EnterPreview(); }
        if(Input.GetKeyDown(KeyCode.F3)) RecycleAll();
        if(preview)
        {
            if(Input.GetKeyDown(KeyCode.F6)) { direction=(direction+1)%8; ApplyPreview(); }
            if(Input.GetKeyDown(KeyCode.F7)) { pose=(pose+1)%6; ApplyPreview(); }
            if(Input.GetKeyDown(KeyCode.F10)) Camera.main.orthographicSize=Camera.main.orthographicSize>6 ? 4 : 10.5f;
            return;
        }
        if(Input.GetKeyDown(KeyCode.F1)) SpawnEncounter();
        if(Input.GetKeyDown(KeyCode.F2)) Duel();
        if(Input.GetKeyDown(KeyCode.F4)) foreach(var enemy in FindObjectsOfType<PooledEnemy>()) if(!enemy.IsReturned) enemy.GetComponent<EnemyHealth>().TakeDamage(9999);
    }
    static void Place(Component value,Vector3 position)
    {
        value.transform.position=position;
        if(value.TryGetComponent<Rigidbody2D>(out var rb)) { rb.position=position; rb.velocity=Vector2.zero; }
        Physics2D.SyncTransforms();
    }
    public static void Restart()
    {
        Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
    }
    void OnGUI()
    {
        if(title==null) { title=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold}; text=new GUIStyle(GUI.skin.label){fontSize=14}; }
        GUI.Box(new Rect(12,12,790,170),GUIContent.none);
        GUI.Label(new Rect(26,20,760,30),"ALIENPURGE / SWARMER + RUSHER + TANK + SPITTER",title);
        GUI.Label(new Rect(26,52,760,24),"WASD move | LMB shoot | Shift Dash | Space Jet Jump",text);
        GUI.Label(new Rect(26,77,760,24),"F1 encounter | F2 next duel | F3 recycle | F4 kill | F9 Restart",text);
        GUI.Label(new Rect(26,102,760,24),"F5 preview/combat | F6 direction | F7 pose | F8 pause | F10 zoom | F11 half speed",text);
        GUI.Label(new Rect(26,128,760,26),status,text);
        if(player!=null && player.IsDead) GUI.Label(new Rect(Screen.width*.5f-140,Screen.height*.5f,400,50),"DEFEATED / F9 Restart",title);
    }
}
