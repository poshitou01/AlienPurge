using System;
using System.Collections;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run V02 Player and Status Checks")]
        public static void RunPlayer()=>Start(false,false,true,false,false,true);

        static IEnumerator PlayerPresentation()
        {
            yield return null;accelerate=invulnerable=false;Time.timeScale=1;
            loop=Object.FindObjectOfType<RegionLoopController>();
            yield return Until(()=>loop.Regions[0].initialized,"Player fixture initialized");
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"),"Player checks use an isolated profile");
            loop.regionModeEnabled=false;
            foreach(var region in loop.Regions)foreach(var t in region.enemies.ToArray())if(t.lease!=null)t.lease.Release();
            var p=loop.world.player.gameObject;var movement=p.GetComponent<PlayerMovement>();
            var rig=p.GetComponent<PlayerCombatPresentation>();var weapons=p.GetComponent<WeaponManager>();var hp=p.GetComponent<PlayerHealth>();
            hp.CancelTemporaryInvulnerability();
            var rb=p.GetComponent<Rigidbody2D>();movement.UseExternalInput=true;
            p.GetComponent<PlayerShooting>().enabled=false;
            var origin=AbilityClearSpace(loop.Terminal("QuarantineCommons").transform.position);
            Move(origin,true);yield return Seconds(.3f);
            Check(movement.TacticalLocomotion&&rig.enabled&&!rig.bodyRenderer.GetComponent<Animator>().enabled,"V02 installs a single aim-facing presentation driver");
            Check(rig.poses.Length==8&&rig.poses.All(pose=>pose.idle!=null)&&rig.heldStrideMaterial!=null,"All eight held poses and the continuous stride material are assigned");
            Check(Vector2.Distance(rig.AimTarget,p.transform.position)>1&&Vector3.Distance(rig.HandAnchor,rig.bodyRenderer.transform.position)<1,
                "A held stance is initialized before the first mouse aim");
            movement.SetMovementInput(Vector2.right);yield return Seconds(.055f);
            Check(movement.MotorVelocity.x>=movement.MoveSpeed*.7f&&rb.position.x>origin.x,"Movement responds in the first physics steps");
            yield return Seconds(.14f);
            Check(Mathf.Abs(movement.MotorVelocity.magnitude-movement.MoveSpeed)<.01f,"Short acceleration reaches the original upgradeable top speed");
            weapons.AimAt(p.transform.position+Vector3.up*10);yield return null;
            Check(rig.FacingSector==2&&movement.MeasuredVelocity.x>1,"Strafing keeps the body facing the aim direction");
            movement.SetMovementInput(Vector2.left);yield return Seconds(.04f);
            Check(movement.MotorVelocity.x<-.9f*movement.MoveSpeed,"Reversing direction does not wait for inertia to turn");
            movement.SetMovementInput(Vector2.one);yield return Seconds(.12f);
            Check(Mathf.Abs(movement.MotorVelocity.magnitude-movement.MoveSpeed)<.01f,"Diagonal movement cannot exceed straight-line top speed");
            movement.SetMovementInput(Vector2.zero);yield return null;
            Vector2 brakingStart=rb.position;yield return Seconds(.11f);
            Check(movement.MotorVelocity==Vector2.zero&&(rb.position-brakingStart).magnitude<.19f,"Releasing movement stops within a short bounded distance");
            Move(origin,true);yield return Seconds(.1f);
            movement.SetMovementInput(Vector2.right);yield return Seconds(.15f);
            movement.SetCanMove(false);var locked=rb.position;yield return Seconds(.12f);
            Check(Vector2.Distance(rb.position,locked)<.04f&&movement.MotorVelocity==Vector2.zero,"Ability movement locks clear the motor without coasting");
            movement.SetCanMove(true);movement.SetMovementInput(Vector2.zero);yield return Seconds(.1f);
            movement.SetMovementInput(Vector2.right);yield return Seconds(.1f);
            Time.timeScale=0;yield return null;yield return null;
            Check(movement.CurrentMoveInput==Vector2.zero&&movement.MotorVelocity==Vector2.zero,"Pause clears pending movement and motor state");
            Time.timeScale=1;movement.SetMovementInput(Vector2.zero);yield return Seconds(.1f);
            Move(origin,true);yield return Seconds(.1f);
            var wall=new GameObject("PlayerCheck_Wall");wall.layer=7;wall.transform.position=origin+new Vector3(1.25f,0,0);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.08f,5);Physics2D.SyncTransforms();
            movement.SetMovementInput(new Vector2(1,1));yield return Seconds(.55f);
            Check(rb.position.x<origin.x+1.25f&&rb.position.y>origin.y+.7f,"Walking respects thin Environment walls and slides along them");
            movement.SetMovementInput(Vector2.zero);yield return Seconds(.1f);Object.Destroy(wall);Move(origin,true);yield return Seconds(.2f);
            var hud=Object.FindObjectOfType<HUDManager>();
            Check(Read<bool>(hud,"tacticalLabels")&&Read<TMP_Text>(hud,"hpText").text.StartsWith("HP")&&Read<TMP_Text>(hud,"expText").text.StartsWith("EXP"),
                "Status retains English HP/EXP labels in the V02 icon layout");
            hp.TakeDamage(1);yield return null;
            Check(Read<Slider>(hud,"hpSlider").value==hp.CurrentHealth&&Read<TMP_Text>(hud,"hpText").text.Contains(hp.CurrentHealth+" / "+hp.MaxHealth),"The restyled health bar and text follow real damage");
            hud.UpdateLevelUI(99);hud.UpdateExperienceUI(999,1000);hud.UpdateTimeUI(6000,0,false);hud.UpdateKillCountUI(999999);
            var panel=Object.FindObjectOfType<PlayerStatusPanel>();
            foreach(string icon in new[]{"HealthIcon","ExperienceIcon","TimeIcon","KillsIcon"})
                Check(panel.transform.Find(icon).GetComponent<Image>().sprite!=null&&panel.transform.Find(icon).gameObject.activeInHierarchy,
                    "Original status icon is visible: "+icon,false);
            foreach(var text in panel.GetComponentsInChildren<TMP_Text>())
            {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Status text accommodates high values: "+text.name,false);}
            Check(panel.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Status presentation cannot block aiming or clicking");
            Check(panel.GetComponent<Image>().sprite.name=="Panel_Main_9Slice","Status uses the same inventory panel art as the mission and ability HUD");
            hp.RestoreHealth(1);hud.UpdateLevelUI(1);hud.UpdateExperienceUI(0,5);hud.UpdateKillCountUI(0);
            weapons.AimAt(p.transform.position+Vector3.right*10);yield return Seconds(.15f);
            Capture("Player_StatusHUD");yield return null;yield return null;
            hp.CancelTemporaryInvulnerability();hp.TakeDamage(4);yield return Seconds(.2f);
            Check(panel.healthFill.color==panel.low&&Read<TMP_Text>(hud,"hpText").text.Contains("1 / 5"),
                "Low-health color remains legible and follows actual HP in the icon layout");
            Capture("Player_StatusHUD_Low");yield return null;yield return null;
            hp.RestoreHealth(5);hp.CancelTemporaryInvulnerability();

            var camera=Camera.main;float oldZoom=camera.orthographicSize;camera.orthographicSize=1.8f;
            for(int i=0;i<8;i++)
            {
                Vector3 direction=new Vector3(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4),0);
                weapons.AimAt(p.transform.position+direction*10);yield return null;yield return null;
                Check(rig.FacingSector==i,"Aim chooses the correct body direction "+rig.poses[i].direction);
                Check(Vector3.Distance(rig.HandAnchor,rig.WeaponGripPosition)<.018f,"Neutral weapon grip stays at the palm "+rig.poses[i].direction);
                Check((rig.weaponSorting.sortingOrder<0)==rig.poses[i].weaponBehindBody,"Weapon occlusion follows the body pose "+rig.poses[i].direction,false);

                yield return Seconds(.15f);Capture("Player_Pose_"+rig.poses[i].direction);yield return null;yield return null;
            }
            camera.orthographicSize=oldZoom;yield return Seconds(.1f);

            Move(origin,true);weapons.AimAt(p.transform.position+Vector3.right*10);yield return Seconds(.1f);
            var heldBody=rig.bodyRenderer.sprite;var heldHand=rig.handOcclusion.sprite;
            var legPhases=new System.Collections.Generic.HashSet<int>();
            movement.SetMovementInput(Vector2.right);float gaitEnd=Time.time+.5f;
            while(Time.time<gaitEnd)
            {
                legPhases.Add(Mathf.FloorToInt(rig.StridePhase*8));
                Check(rig.bodyRenderer.sprite==heldBody&&rig.handOcclusion.sprite==heldHand,"Walking keeps the held upper body and hands stable",false);
                yield return null;
            }
            Check(legPhases.Count>=3,"Physical walking drives a continuous leg gait while the upper body holds the gun");
            camera.orthographicSize=1.8f;CameraFollow.Instance.enabled=false;
            camera.transform.position=p.transform.position+Vector3.back*10;Capture("Player_Walking_E");yield return null;yield return null;
            movement.SetMovementInput(Vector2.zero);Move(origin,true);yield return Seconds(.15f);
            weapons.AimAt(p.transform.position+Vector3.down*10);movement.SetMovementInput(Vector2.down);
            yield return Seconds(.3f);camera.transform.position=p.transform.position+Vector3.back*10;Capture("Player_Walking_S");yield return null;yield return null;
            movement.SetMovementInput(Vector2.zero);camera.orthographicSize=oldZoom;CameraFollow.Instance.enabled=true;Move(origin,true);yield return Seconds(.15f);


            weapons.AimAt(p.transform.position+new Vector3(Mathf.Cos(22*Mathf.Deg2Rad),Mathf.Sin(22*Mathf.Deg2Rad))*10);yield return null;
            int stable=rig.FacingSector;
            for(int i=0;i<6;i++)weapons.AimAt(p.transform.position+new Vector3(Mathf.Cos((22+i%2)*Mathf.Deg2Rad),Mathf.Sin((22+i%2)*Mathf.Deg2Rad))*10);
            Check(rig.FacingSector==stable,"Direction hysteresis prevents flicker at an eight-way boundary");
            weapons.AimAt(p.transform.position);Check(rig.FacingSector==stable,"Cursor inside the body does not snap the stance");
            // Native projectile collisions, damage and evolution still originate at the installed muzzle.
            foreach(var b in Object.FindObjectsOfType<Bullet>())b.ReturnToPool();
            var target=AbilityEnemy(EnemyType.Normal,p.transform.position+Vector3.right*3,100);
            weapons.AimAt(target.GetComponentInChildren<Collider2D>().bounds.center);yield return Seconds(.3f);
            Check(weapons.TryFire(),"Held weapon fires through the native pool");
            Check(Vector3.Distance(weapons.CurrentWeapon.MuzzlePoint.position,rig.HandAnchor)>.5f,"The muzzle remains ahead of the hand after rescaling");
            Vector3 recoil=weapons.CurrentWeapon.transform.localPosition;Time.timeScale=0;
            yield return null;yield return null;yield return null;
            Check(Vector3.Distance(recoil,weapons.CurrentWeapon.transform.localPosition)<.0001f,"Pause freezes the held gun's native recoil");
            Time.timeScale=1;
            yield return Seconds(.3f);
            Check(target.GetComponent<EnemyHealth>().CurrentHealth<100,"A native bullet from the attached gun hits and damages its target");
            target.ReturnToPool();
            var evolution=p.GetComponent<WeaponEvolutionController>();
            var data=Read<WeaponEvolutionData[]>(evolution,"evolutionDefinitions");
            foreach(var definition in data)
            {
                WriteField(evolution,"currentEvolution",definition.EvolutionType);WriteField(evolution,"currentEvolutionData",definition);
                yield return null;yield return null;
                weapons.AimAt(p.transform.position+Vector3.left*8);yield return null;
                Check(Vector3.Distance(rig.HandAnchor,rig.WeaponGripPosition)<.018f,"Evolution keeps its own calibrated grip: "+definition.EvolutionType);
            }
            WriteField(evolution,"currentEvolution",WeaponEvolutionType.None);WriteField(evolution,"currentEvolutionData",null);yield return null;
            var feel=p.GetComponent<PlayerAbilityFeel>();var dash=p.GetComponent<PlayerDash>();var jump=p.GetComponent<PlayerJetJump>();
            Move(origin,true);movement.SetMovementInput(Vector2.right);yield return Seconds(.12f);
            Check(feel.RequestAbility(PlayerAbilityMode.Dashing)&&dash.IsActive,"Dash starts directly from the new walking motor");
            yield return Until(()=>!dash.IsActive,"Dash completes",3);
            movement.SetMovementInput(Vector2.right);yield return Seconds(.12f);
            Check(movement.MotorVelocity.x>4,"Walking resumes after Dash releases its movement lock");
            Check(feel.RequestAbility(PlayerAbilityMode.Airborne)&&jump.IsActive,"Jump still responds immediately while strafing");
            var jumpStart=rb.position;yield return Seconds(.18f);
            Check(rb.position.x>jumpStart.x+.3f&&rig.stanceRoot.parent.localPosition.y>0,"Airborne movement and the entire held weapon lift together");
            movement.SetMovementInput(Vector2.zero);yield return Until(()=>!jump.IsActive,"Jump lands",3);
            var lockedAt=rb.position;movement.enabled=false;yield return Seconds(.08f);
            Check(movement.MotorVelocity==Vector2.zero&&Vector2.Distance(rb.position,lockedAt)<.04f,"Disabling movement clears drift");
            movement.enabled=true;movement.SetMovementInput(Vector2.zero);
            hp.TakeDamage(999);yield return null;yield return null;
            Check(hp.IsDead&&movement.MotorVelocity==Vector2.zero,"Death clears movement while native defeat UI remains available");
            results.Add("LIMITS: deterministic native Play Mode inputs, collision/aim/HUD assertions and eight close-up renders; not a substitute for subjective mouse-and-keyboard feel testing.");
        }
    }
}
