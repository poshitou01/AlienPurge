using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run V02 Ability and Menu Checks")]
        public static void RunAbilities()=>Start(false,false,true,false,true);
        static void WriteField(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).SetValue(target,value);
        static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
        static PooledEnemy AbilityEnemy(EnemyType type,Vector3 position,int hp)
        {
            var enemy=loop.pool.GetEnemy(type,position,Quaternion.identity);
            Check(enemy!=null,"Native pool provides "+type+" fixture",false);
            enemy.GetComponent<EnemyHealth>().InitializeHealth(hp);
            foreach(var behaviour in enemy.GetComponents<MonoBehaviour>())
                if(behaviour is HeavyShockwaveAttack||behaviour is FastChargeAttack||behaviour.GetType().Name=="RangedEnemyAttack")behaviour.enabled=false;
            enemy.GetComponent<EnemyMovement>().SetMovementLocked(true);
            enemy.GetComponent<EnemyPathNavigator>().enabled=false;
            enemy.GetComponent<EnemyContactDamage>().enabled=false;
            Physics2D.SyncTransforms();return enemy;
        }
        static Vector3 AbilityClearSpace(Vector3 near)
        {
            for(int y=-8;y<=8;y+=2)for(int x=-10;x<=10;x+=2)
            {
                Vector3 point=near+new Vector3(x,y,0);
                if(Physics2D.OverlapBoxAll(point+Vector3.right*.3f,new Vector2(8,8),0,1<<7).All(c=>c.isTrigger))return point;
            }
            throw new InvalidOperationException("No unobstructed ability fixture area near entry terminal.");
        }
        static IEnumerator Abilities()
        {
            yield return null;accelerate=invulnerable=false;Time.timeScale=1;
            loop=Object.FindObjectOfType<RegionLoopController>();
            yield return Until(()=>loop.Regions[0].initialized,"Ability fixture initialized");
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"),"Skill tests use isolated save profile");
            loop.regionModeEnabled=false;
            foreach(var region in loop.Regions)foreach(var ticket in region.enemies.ToArray())if(ticket.lease!=null)ticket.lease.Release();
            var player=loop.world.player.gameObject;var hp=player.GetComponent<PlayerHealth>();hp.CancelTemporaryInvulnerability();hp.IncreaseMaxHealth(100);
            var feel=player.GetComponent<PlayerAbilityFeel>();var dash=player.GetComponent<PlayerDash>();var jump=player.GetComponent<PlayerJetJump>();var pulse=player.GetComponent<PlayerPulseSkill>();
            var state=player.GetComponent<PlayerAbilityState>();var body=player.GetComponent<Rigidbody2D>();
            player.GetComponent<PlayerMovement>().enabled=false;player.GetComponent<PlayerShooting>().enabled=false;
            var origin=AbilityClearSpace(loop.Terminal("QuarantineCommons").transform.position);Move(origin,true);yield return Seconds(.5f);
            results.Add("MEASURE clear fixture origin="+origin);
            var visual=player.transform.Find("VisualRoot");var baseVisual=visual.localPosition;
            Check(EditorBuildSettings.scenes[0].enabled&&EditorBuildSettings.scenes[0].path==RegionLoopV02Installer.DeskMenu,"DeskV1 is the enabled build entry menu");
            Check(Read<string>(GameManager.Instance,"mainMenuSceneName")=="MainMenu_DeskV1"&&Read<string>(Object.FindObjectOfType<PauseMenuController>(),"mainMenuSceneName")=="MainMenu_DeskV1","Result and pause actions target DeskV1");

            // The production request path sets airborne and presents lift-off synchronously.
            Check(feel.RequestAbility(PlayerAbilityMode.Airborne)&&jump.IsActive&&state.IsAirborne&&visual.localPosition.y>baseVisual.y,"Space request becomes airborne and visibly lifts in the same frame");
            int before=hp.CurrentHealth;
            var normal=AbilityEnemy(EnemyType.Normal,origin+Vector3.right*1.4f,100);
            Call(normal.GetComponent<EnemyContactDamage>(),"TryDamagePlayer",player);
            var heavy=AbilityEnemy(EnemyType.Heavy,origin+Vector3.up*1.4f,100);
            Call(heavy.GetComponent<HeavyShockwaveAttack>(),"ApplyShockwaveDamage");
            var fast=AbilityEnemy(EnemyType.Fast,origin+Vector3.left*1.4f,100);var charge=fast.GetComponent<FastChargeAttack>();
            var chargeField=charge.GetType().GetField("currentState",BindingFlags.NonPublic|BindingFlags.Instance);
            chargeField.SetValue(charge,Enum.Parse(chargeField.FieldType,"Charging"));Call(charge,"TryDamagePlayer",player);
            Check(hp.CurrentHealth==before,"Immediate jump evades native contact, fast charge and heavy ground shockwave");
            var projectile=EnemyProjectilePool.Instance.GetProjectile(origin+Vector3.down*2,Quaternion.identity);projectile.Initialize(Vector2.up,1,1,10);
            Call(projectile,"OnTriggerEnter2D",player.GetComponent<Collider2D>());
            Check(hp.CurrentHealth==before-1&&projectile.IsReturned,"Airborne player still takes ranged projectile damage through native health/pool path");
            hp.TakeDamage(1);Check(hp.CurrentHealth==before-2,"Airborne is ground evasion, not global hazard invulnerability");
            yield return Seconds(.2f);Capture("Abilities_Jump");yield return null;
            yield return Until(()=>!jump.IsActive,"Jump lands",3);
            yield return Seconds(.04f);
            Check(!state.IsAirborne&&Vector3.Distance(visual.localPosition,baseVisual)<.001f,"Landing restores sprite position and grounded state");
            Check(!Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(),normal.GetComponent<Collider2D>()),"Landing restores enemy body collision pairs");
            before=hp.CurrentHealth;normal.GetComponent<EnemyContactDamage>().ResetDamageCooldown();Call(normal.GetComponent<EnemyContactDamage>(),"TryDamagePlayer",player);
            Check(hp.CurrentHealth<before,"Ground contact can damage again after landing");
            normal.ReturnToPool();heavy.ReturnToPool();fast.ReturnToPool();
            yield return Seconds(.4f);

            // Deterministic distance at two physics timesteps, including wall contact.
            float oldStep=Time.fixedDeltaTime;
            foreach(float step in new[]{.02f,1f/30})
            {
                Time.fixedDeltaTime=step;Move(origin,true);yield return Until(()=>dash.IsReady,"Dash cooldown",3);
                Vector2 start=body.position;Check(dash.TryActivateBuffered(Vector2.right),"Dash starts at physics step "+step,false);
                before=hp.CurrentHealth;hp.TakeDamage(1);Check(hp.CurrentHealth==before,"Dash protects from damage through the active action at step "+step);
                yield return Seconds(.06f);Check(feel.VisibleAfterimages>0,"Dash emits reusable fading sprite afterimages",step==.02f);Capture("Abilities_Dash");
                yield return Until(()=>!dash.IsActive,"Dash finishes",3);yield return Seconds(step*2);
                float distance=Vector2.Distance(start,body.position);results.Add("MEASURE dash step="+step+" distance="+distance);
                Check(distance>3&&distance<3.6f,"Dash covers ~3.36 units independent of tested physics timestep");
                before=hp.CurrentHealth;hp.TakeDamage(1);Check(hp.CurrentHealth==before-1,"Dash protection ends with the action",step==.02f);
                Check(player.GetComponent<PlayerMovement>().CanMove&&!state.IsUsingAbility,"Dash restores normal controls",step==.02f);
            }
            Time.fixedDeltaTime=oldStep;
            var wall=new GameObject("AbilityTest_ThinWall");wall.layer=7;wall.transform.position=origin+Vector3.right*1.8f;
            var box=wall.AddComponent<BoxCollider2D>();box.size=new Vector2(.08f,5);Physics2D.SyncTransforms();
            Move(origin,true);yield return Until(()=>dash.IsReady,"Dash ready at wall",3);dash.TryActivateBuffered(Vector2.right);
            yield return Until(()=>!dash.IsActive,"Wall stops dash",3);yield return Seconds(.08f);
            Check(body.position.x<wall.transform.position.x-.08f,"Fast dash sweep cannot cross a thin environment wall");
            Object.Destroy(wall);yield return null;

            // A jump can immediately take over an in-flight dash without waiting for its duration.
            Move(origin,true);yield return Until(()=>dash.IsReady&&jump.IsReady,"Chain ready",3);dash.TryActivateBuffered(Vector2.right);
            feel.RequestAbility(PlayerAbilityMode.Airborne);
            Check(jump.IsActive&&!dash.IsActive&&state.IsAirborne&&!feel.IsDashProtected,"Space interrupts dash immediately with clean protection ownership");
            yield return Until(()=>!jump.IsActive,"Chain landing",3);
            yield return Until(()=>jump.CooldownRemaining<.12f,"Near cooldown boundary",3);
            feel.RequestAbility(PlayerAbilityMode.Airborne);Check(feel.HasBufferedInput&&!jump.IsActive,"Slightly early Space is buffered across the cooldown boundary");
            yield return Until(()=>jump.IsActive,"Buffered Space activates",1);yield return Until(()=>!jump.IsActive,"Buffered jump lands",3);
            feel.RequestAbility(PlayerAbilityMode.Airborne);yield return Seconds(.23f);
            Check(!feel.HasBufferedInput&&!jump.IsActive,"Expired input does not create a delayed phantom jump");
            var pause=Object.FindObjectOfType<PauseMenuController>();pause.PauseGame();
            Check(!feel.RequestAbility(PlayerAbilityMode.Airborne),"Paused game rejects ability requests");yield return null;pause.ResumeGame();
            yield return Seconds(.2f);Check(!jump.IsActive&&!feel.HasBufferedInput,"Resuming does not replay paused input");

            // Q applies native damage once per enemy even with several colliders, uses line of sight,
            // removes native pooled projectiles and keeps late-game relevance.
            Move(origin,true);yield return Until(()=>pulse.IsReady&&!state.IsUsingAbility,"Pulse ready",3);
            WriteField(player.GetComponent<PlayerExperience>(),"currentLevel",15);
            var target=AbilityEnemy(EnemyType.Normal,origin+Vector3.right*2,100);
            var extra=target.gameObject.AddComponent<CircleCollider2D>();extra.isTrigger=true;extra.radius=.2f;
            var far=AbilityEnemy(EnemyType.Normal,origin+Vector3.right*6,100);
            var occluded=AbilityEnemy(EnemyType.Normal,origin+Vector3.up*3,100);
            wall=new GameObject("AbilityTest_PulseCover");wall.layer=7;wall.transform.position=origin+Vector3.up*1.8f;
            box=wall.AddComponent<BoxCollider2D>();box.size=new Vector2(3,.08f);
            projectile=EnemyProjectilePool.Instance.GetProjectile(origin+Vector3.down*2,Quaternion.identity);projectile.Initialize(Vector2.left,.01f,1,10);
            var farProjectile=EnemyProjectilePool.Instance.GetProjectile(origin+Vector3.down*6,Quaternion.identity);farProjectile.Initialize(Vector2.left,.01f,1,10);
            Physics2D.SyncTransforms();int damage=feel.CurrentPulseDamage;
            Vector3 targetStart=target.transform.position;
            Check(feel.RequestAbility(PlayerAbilityMode.Casting)&&pulse.IsActive,"Q applies on accepted request with short action lock");
            Check(target.GetComponent<EnemyHealth>().CurrentHealth==100-damage&&feel.LastPulseHits==1,"Pulse deduplicates multiple enemy colliders and applies scaled native damage once");
            Check(far.GetComponent<EnemyHealth>().CurrentHealth==100&&occluded.GetComponent<EnemyHealth>().CurrentHealth==100,"Pulse respects range and solid cover");
            Check(projectile.IsReturned&&!farProjectile.IsReturned&&feel.LastPulseCleared==1,"Pulse clears only nearby visible enemy projectiles through their pool");
            Check(damage>=9&&damage<=28,"Level-15 pulse damage remains useful and bounded");
            WriteField(player.GetComponent<PlayerExperience>(),"currentLevel",200);
            Check(feel.CurrentPulseDamage==28,"Extreme level cannot exceed the pulse damage cap");
            WriteField(player.GetComponent<PlayerExperience>(),"currentLevel",15);
            yield return Seconds(.08f);Capture("Abilities_Pulse");yield return null;
            yield return Seconds(.28f);Check(Vector2.Distance(target.transform.position,targetStart)>1,"Pulse physically pushes the enemy away");
            Check(!pulse.IsActive&&!state.IsUsingAbility,"Q releases its control lock promptly while feedback fades");
            Object.Destroy(extra);target.ReturnToPool();far.ReturnToPool();occluded.ReturnToPool();farProjectile.ReturnToPool();Object.Destroy(wall);yield return null;
            WriteField(player.GetComponent<PlayerExperience>(),"currentLevel",1);

            yield return Until(()=>pulse.IsReady&&jump.IsReady,"Q cancel ready",8);feel.RequestAbility(PlayerAbilityMode.Casting);feel.RequestAbility(PlayerAbilityMode.Airborne);
            Check(!jump.IsActive&&feel.HasBufferedInput,"Space during Q is buffered during its brief commitment");
            yield return Until(()=>jump.IsActive,"Space cancels Q recovery",1);
            Check(!pulse.IsActive&&state.IsAirborne,"Q-to-jump transition leaves one active ability state");
            feel.enabled=false;
            Check(!jump.IsActive&&!dash.IsActive&&!pulse.IsActive&&!feel.HasBufferedInput&&!state.IsUsingAbility&&Vector3.Distance(visual.localPosition,baseVisual)<.001f,"Disabling ability controller cleans visuals, states and pending input");
            feel.enabled=true;
            before=hp.CurrentHealth;hp.TakeDamage(1);Check(hp.CurrentHealth==before-1,"No leaked invulnerability after interruption");

            yield return Until(()=>jump.IsReady,"Death cleanup fixture",3);feel.RequestAbility(PlayerAbilityMode.Airborne);
            hp.TakeDamage(hp.CurrentHealth);yield return Until(()=>GameManager.Instance.IsGameOver,"Native defeat",3);yield return null;yield return null;
            Check(!jump.IsActive&&!state.IsUsingAbility&&!feel.HasBufferedInput,"Dying while airborne clears action and queued input");
            Check(GameManager.Instance.CurrentRunResultSnapshot!=null&&!GameManager.Instance.IsVictory,"Defeat captures a native loss snapshot without a success settlement");
            GameManager.Instance.ReturnToBase();yield return Until(()=>SceneManager.GetActiveScene().name=="MainMenu_DeskV1","Actual result return loads DeskV1",15);
            Check(Object.FindObjectOfType<MainMenuManager>()!=null,"Native result return reaches the correct DeskV1 menu");
            Object.FindObjectOfType<MainMenuManager>().StartGame();
            yield return Until(()=>SceneManager.GetActiveScene().name=="Level01_FullWorld_GameplayV02","DeskV1 StartGame loads V02",20);yield return null;
            loop=Object.FindObjectOfType<RegionLoopController>();
            Check(loop!=null&&loop.world.player.GetComponent<PlayerAbilityFeel>()!=null,"DeskV1 StartGame returns to the upgraded V02 scene");
            Check(!loop.world.player.GetComponent<PlayerAbilityState>().IsUsingAbility&&Time.timeScale==1,"Menu round-trip starts with clean controls and time scale");
            results.Add("LIMITS: automation uses the same input-request path as keyboard, native physics/damage/pools/menu callbacks and synthetic combat fixtures; subjective dash feel/audio mix still benefit from human playtesting.");
        }
    }
}
