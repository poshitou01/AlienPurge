using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        const string WalkEvidence="Docs/Integration/PlayerLocomotionFrames";
        [MenuItem("AlienPurge/Gameplay/Level01/Run Integrated Player Locomotion Checks")]
        public static void RunIntegratedPlayerLocomotion()=>StartIntegrated(false,true);

        static IEnumerator IntegratedPlayerLocomotion()
        {
            yield return null;accelerate=false;invulnerable=true;Time.timeScale=1;
            loop=Object.FindObjectOfType<AlienPurge.Level01.RegionLoop.RegionLoopController>();
            yield return Until(()=>loop.Regions[0].initialized,"Initial player region");
            // A focused, unsaved fixture keeps garrison attacks out of the close-up recording.
            // Production region scheduling is unchanged; the full route was covered separately.
            loop.regionModeEnabled=false;
            foreach(var region in loop.Regions)foreach(var ticket in region.enemies.ToArray())if(ticket.lease!=null)ticket.lease.Release();
            Directory.CreateDirectory(WalkEvidence);
            var player=loop.world.player;
            var movement=player.GetComponent<PlayerMovement>();var rb=player.GetComponent<Rigidbody2D>();
            var driver=player.GetComponent<PlayerAnimationDriver>();var weapons=player.GetComponent<WeaponManager>();
            var visual=player.transform.Find("VisualRoot/LocomotionPose");
            var animator=Read<Animator>(driver,"bodyAnimator");var body=animator.GetComponent<SpriteRenderer>();
            var socket=player.GetComponentInChildren<WeaponSocketController>(true);
            var rig=player.GetComponent<PlayerCombatPresentation>();
            Check(rig!=null&&rig.animatorLocomotion&&animator.enabled&&driver.enabled&&!socket.enabled,
                "Integrated scene restores native locomotion Animator and WASD facing with one calibrated weapon socket driver");
            Check(body.sharedMaterial.shader.name!="AlienPurge/Player/HeldStride","Body uses original sprite rendering without static-leg deformation");
            Check(movement.TacticalLocomotion,"Existing movement acceleration, collision motor and speed remain enabled");
            player.GetComponent<PlayerShooting>().enabled=false;movement.UseExternalInput=true;
            Vector3 origin=AbilityClearSpace(loop.Terminal("QuarantineCommons").transform.position);
            Move(origin,true);yield return Seconds(.25f);
            string[] dirs={"E","NE","N","NW","W","SW","S","SE"};
            int record=0;
            for(int i=0;i<dirs.Length;i++)
            {
                Vector2 direction=new Vector2(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4));
                var frames=Enumerable.Range(1,4).Select(f=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Walk/"+dirs[i]+"/Player_Walk_"+dirs[i]+"_0"+f+".png")).ToArray();
                Check(frames.All(f=>f!=null)&&frames.Distinct().Count()==4,"Original four distinct authored frames resolve for "+dirs[i]);
                movement.SetMovementInput(Vector2.zero);Move(origin,true);yield return Seconds(.12f);
                var before=rb.position;
                movement.SetMovementInput(direction);
                var seen=new HashSet<Sprite>();float end=Time.time+.9f,nextCapture=Time.time+.15f;
                int captures=0;
                while(Time.time<end)
                {
                    // Deliberately aim across movement; mouse input must not seize body direction.
                    Vector2 aim=new Vector2(-direction.y,direction.x);
                    weapons.AimAt((Vector2)player.transform.position+aim*12);
                    if(frames.Contains(body.sprite))seen.Add(body.sprite);
                    if(Time.time>=nextCapture&&captures<6)
                    {
                        CaptureLocomotionFrame(body,record++);captures++;nextCapture+=.125f;
                    }
                    yield return null;
                }
                Check(Vector2.Dot(rb.position-before,direction)>2,"Native motor visibly travels in keyboard direction "+dirs[i]);
                Check(Vector2.Dot(driver.LastFacingDirection,direction)>.99f,"WASD sets body direction independently of mouse aim: "+dirs[i]);
                Check(seen.Count==4,"Actual renderer cycles all four original walk frames at native clip timing: "+dirs[i]);
                Check(Vector2.Dot(weapons.CurrentWeapon.AimDirection,new Vector2(-direction.y,direction.x))>.98f,
                    "Weapon retains independent mouse aim while walking "+dirs[i]);
                Check(Vector3.Distance(rig.HandAnchor,rig.WeaponGripPosition)<.018f,"Weapon grip follows the displayed walk frame's hand: "+dirs[i]);
                movement.SetMovementInput(Vector2.zero);yield return Seconds(.22f);
                var idle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Player/Idle/Player_Idle_"+dirs[i]+".png");
                Check(body.sprite==idle&&Vector2.Dot(driver.LastFacingDirection,direction)>.99f,
                    "Releasing movement returns to the original idle pose and preserves last facing: "+dirs[i]);
            }
            Move(origin,true);movement.SetMovementInput(Vector2.right);yield return Seconds(.2f);
            var cursorFrames=new HashSet<Sprite>();float cursorEnd=Time.time+.6f;
            while(Time.time<cursorEnd)
            {
                weapons.AimAt(body.transform.position);cursorFrames.Add(body.sprite);
                Check(Vector3.Distance(rig.HandAnchor,rig.WeaponGripPosition)<.018f,"Cursor crossing keeps the animated hand attached",false);
                yield return null;
            }
            Check(cursorFrames.Count>=4&&driver.LastFacingDirection.x>.99f,"Cursor over the body neither freezes walking frames nor takes over WASD facing");
            Move(origin,true);movement.SetMovementInput(Vector2.one);yield return Seconds(.16f);
            Check(Mathf.Abs(movement.MotorVelocity.magnitude-movement.MoveSpeed)<.02f,"Diagonal travel does not increase movement speed");
            var pause=Object.FindObjectOfType<PauseMenuController>();pause.PauseGame();yield return null;
            var stopped=rb.position;var frozen=body.sprite;
            for(int i=0;i<8;i++)yield return null;
            Check(body.sprite==frozen&&Vector2.Distance(rb.position,stopped)<.005f,"Pause freezes both the authored animation and position");
            pause.ResumeGame();movement.SetMovementInput(Vector2.right);yield return Seconds(.2f);
            Check(animator.GetBool("IsMoving")&&movement.MotorVelocity.x>1,"Resume restores walking and animation");
            movement.SetMovementInput(Vector2.zero);yield return Seconds(.15f);Move(origin,true);
            var wall=new GameObject("PlayerLocomotion_ThinWallFixture");wall.layer=LayerMask.NameToLayer("Environment");
            wall.transform.position=origin+Vector3.right*1.25f;wall.AddComponent<BoxCollider2D>().size=new Vector2(.08f,5);Physics2D.SyncTransforms();
            movement.SetMovementInput(Vector2.one);yield return Seconds(.55f);
            Check(rb.position.x<origin.x+1.25f&&rb.position.y>origin.y+.7f,"Restored animation preserves thin-wall collision and wall sliding");
            movement.SetMovementInput(Vector2.zero);Object.Destroy(wall);Move(origin,true);yield return Seconds(.2f);
            var target=AbilityEnemy(EnemyType.Normal,player.transform.position+Vector3.right*3,100);
            foreach(var bullet in Object.FindObjectsOfType<Bullet>())bullet.ReturnToPool();
            weapons.AimAt(target.GetComponentInChildren<Collider2D>().bounds.center);yield return Seconds(.25f);
            Check(weapons.TryFire(),"Restored socket fires through the native weapon and projectile pool");
            yield return Until(()=>target.GetComponent<EnemyHealth>().CurrentHealth<100,"Real projectile hit",5);
            Check(target.GetComponent<EnemyHealth>().CurrentHealth<100,"Actual aimed projectile still hits and damages its target");target.ReturnToPool();
            var evolution=player.GetComponent<WeaponEvolutionController>();
            foreach(var definition in Read<WeaponEvolutionData[]>(evolution,"evolutionDefinitions"))
            {
                WriteField(evolution,"currentEvolution",definition.EvolutionType);WriteField(evolution,"currentEvolutionData",definition);
                movement.SetMovementInput(Vector2.left);yield return Seconds(.2f);
                weapons.AimAt(player.transform.position+Vector3.right*10);yield return Seconds(.3f);
                Check(body.sprite.name.StartsWith("Player_Walk_W_")&&weapons.TryFire(),"Walking and native firing remain available for "+definition.EvolutionType);
            }
            WriteField(evolution,"currentEvolution",WeaponEvolutionType.None);WriteField(evolution,"currentEvolutionData",null);
            movement.SetMovementInput(Vector2.zero);Move(origin,true);yield return Seconds(.2f);
            var feel=player.GetComponent<PlayerAbilityFeel>();var dash=player.GetComponent<PlayerDash>();var jump=player.GetComponent<PlayerJetJump>();
            movement.SetMovementInput(Vector2.right);yield return Seconds(.15f);
            Check(feel.RequestAbility(PlayerAbilityMode.Dashing)&&dash.IsActive,"Dash starts from restored walking presentation");
            yield return Until(()=>!dash.IsActive,"Dash completes",5);movement.SetMovementInput(Vector2.right);yield return Seconds(.2f);
            Check(animator.GetBool("IsMoving")&&movement.MotorVelocity.x>1,"Walking animation resumes after Dash releases movement");
            Check(feel.RequestAbility(PlayerAbilityMode.Airborne)&&jump.IsActive,"Jump remains available while moving");
            yield return Seconds(.15f);
            Check(visual.parent.localPosition.y>0,"Body and weapon remain attached during the existing jump lift");
            movement.SetMovementInput(Vector2.zero);yield return Until(()=>!jump.IsActive,"Jump lands",5);
            var health=player.GetComponent<PlayerHealth>();invulnerable=false;health.CancelTemporaryInvulnerability();health.TakeDamage(int.MaxValue);
            yield return null;yield return null;
            Check(health.IsDead&&movement.MotorVelocity==Vector2.zero,"Death stops the locomotion motor");
            invulnerable=true;yield return Fresh();
            Check(loop.world.player.GetComponent<PlayerCombatPresentation>().animatorLocomotion&&loop.world.player.GetComponent<PlayerAnimationDriver>().enabled,
                "Restart loads the fixed integrated scene rather than reinstating mouse-owned body animation");
            ValidateTestProtection();
            results.Add("ASSETS: eight directions, four distinct source sprites per direction, original clips at 8 fps; no generated or duplicated walk frames.");
            results.Add("LIMITS: deterministic vectors enter the same motor path as WASD; InputManager bindings inspected separately. No OS keyboard injection or subjective human feel claim. Close-up frames are real camera renders.");
        }
        static void CaptureLocomotionFrame(SpriteRenderer body,int frame)
        {
            var camera=Camera.main;var oldTarget=camera.targetTexture;var oldPosition=camera.transform.position;float oldZoom=camera.orthographicSize;
            var render=RenderTexture.GetTemporary(384,384,24);var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=render;camera.orthographicSize=1.35f;
                camera.transform.position=new Vector3(body.bounds.center.x,body.bounds.center.y,oldPosition.z);camera.Render();RenderTexture.active=render;
                var capture=new Texture2D(384,384,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,384,384),0,0);capture.Apply();
                File.WriteAllBytes(WalkEvidence+"/"+frame.ToString("D3")+".png",capture.EncodeToPNG());Object.Destroy(capture);
            }
            finally
            {camera.targetTexture=oldTarget;camera.transform.position=oldPosition;camera.orthographicSize=oldZoom;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(render);}
        }
    }
}
