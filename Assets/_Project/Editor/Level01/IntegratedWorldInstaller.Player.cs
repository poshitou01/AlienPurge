using UnityEditor;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.Level01.Editor
{
    public static partial class IntegratedWorldInstaller
    {
        // Restore the authored body Animator and attach the calibrated weapon to each
        // displayed frame. Neither the player prefab nor shared Animator/clip assets change.
        static void RestorePlayerLocomotion(Scene scene)
        {
            Require(scene.path==Target,"Player locomotion repair is limited to the integrated scene.");
            var movement=One<PlayerMovement>(scene);
            var driver=movement.GetComponent<PlayerAnimationDriver>();
            Require(driver!=null,"Integrated player animation driver is missing.");
            var animator=new SerializedObject(driver).FindProperty("bodyAnimator").objectReferenceValue as Animator;
            var sockets=movement.GetComponentsInChildren<WeaponSocketController>(true);
            Require(sockets.Length==1,"Expected one native player weapon socket, found "+sockets.Length);
            var weaponSocket=sockets[0];
            Require(animator!=null&&animator.runtimeAnimatorController!=null&&driver!=null&&weaponSocket!=null,
                "Cannot restore the original player animation/socket components.");
            var rig=movement.GetComponent<PlayerCombatPresentation>();
            Require(rig!=null,"Integrated player is missing its calibrated weapon presentation.");
            rig.animatorLocomotion=true;
            if(rig.walkPoses==null||rig.walkPoses.Length==0)rig.walkPoses=WalkingGrips(rig);
            var serializedDriver=new SerializedObject(driver);
            serializedDriver.FindProperty("pauseWithGameplay").boolValue=true;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            animator.enabled=true;driver.enabled=true;weaponSocket.enabled=false;
            foreach(var component in new Component[]{animator,driver,weaponSocket})
            {EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            EditorUtility.SetDirty(rig);
        }
        static void ValidatePlayerLocomotion(Scene scene)
        {
            var movement=One<PlayerMovement>(scene);
            var driver=movement.GetComponent<PlayerAnimationDriver>();
            var animator=new SerializedObject(driver).FindProperty("bodyAnimator").objectReferenceValue as Animator;
            var rig=movement.GetComponent<PlayerCombatPresentation>();
            Require(rig!=null&&rig.animatorLocomotion,"Player body must use movement-owned authored animation.");
            Require(rig.walkPoses!=null&&rig.walkPoses.Length==32&&rig.walkPoses.Select(p=>p.idle).Distinct().Count()==32&&rig.walkPoses.All(p=>p.idle!=null),"Original walk-frame weapon attachments are incomplete.");
            Require(driver.enabled&&animator!=null&&animator.enabled,
                "Native player animation must be enabled for WASD facing and authored walking frames.");
            Require(!movement.GetComponentInChildren<WeaponSocketController>(true).enabled,"Two weapon socket drivers must not run together.");
        }
        static PlayerCombatPresentation.Pose[] WalkingGrips(PlayerCombatPresentation rig)
        {
            // Palm centres measured from the existing, unchanged source PNGs (top-left normalized).
            // Each authored frame has its own socket; no static hand is left floating over a swinging arm.
            Vector2[][] primary={
                new[]{new Vector2(.565f,.669f),new Vector2(.552f,.681f),new Vector2(.375f,.706f),new Vector2(.560f,.669f)},
                new[]{new Vector2(.677f,.556f),new Vector2(.706f,.581f),new Vector2(.706f,.508f),new Vector2(.702f,.573f)},
                new[]{new Vector2(.661f,.520f),new Vector2(.661f,.520f),new Vector2(.680f,.520f),new Vector2(.661f,.520f)},
                new[]{new Vector2(.327f,.540f),new Vector2(.343f,.536f),new Vector2(.339f,.536f),new Vector2(.343f,.536f)},
                new[]{new Vector2(.480f,.665f),new Vector2(.440f,.653f),new Vector2(.427f,.661f),new Vector2(.480f,.665f)},
                new[]{new Vector2(.577f,.698f),new Vector2(.597f,.718f),new Vector2(.540f,.661f),new Vector2(.548f,.669f)},
                new[]{new Vector2(.669f,.610f),new Vector2(.600f,.633f),new Vector2(.617f,.706f),new Vector2(.597f,.645f)},
                new[]{new Vector2(.476f,.685f),new Vector2(.472f,.673f),new Vector2(.294f,.637f),new Vector2(.444f,.690f)}
            };
            Vector2[][] secondary={
                new[]{new Vector2(.690f,.609f),new Vector2(.700f,.610f),new Vector2(.702f,.610f),new Vector2(.694f,.610f)},
                null,null,null,
                new[]{new Vector2(.302f,.589f),new Vector2(.323f,.597f),new Vector2(.315f,.589f),new Vector2(.315f,.589f)},
                new[]{new Vector2(.319f,.629f),new Vector2(.343f,.625f),new Vector2(.335f,.633f),new Vector2(.339f,.641f)},
                new[]{new Vector2(.363f,.657f),new Vector2(.383f,.661f),new Vector2(.306f,.617f),new Vector2(.379f,.657f)},
                new[]{new Vector2(.698f,.637f),new Vector2(.706f,.649f),new Vector2(.730f,.661f),new Vector2(.710f,.661f)}
            };
            var result=new PlayerCombatPresentation.Pose[32];
            for(int d=0;d<8;d++)for(int f=0;f<4;f++)
            {
                var original=rig.poses[d];var grip=primary[d][f];var far=secondary[d]!=null?secondary[d][f]:grip;
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Walk/"+original.direction+"/Player_Walk_"+original.direction+"_0"+(f+1)+".png");
                Require(sprite!=null,"Missing original walking frame: "+original.direction+" "+(f+1));
                result[d*4+f]=new PlayerCombatPresentation.Pose{direction=original.direction,idle=sprite,grip=grip,
                    weaponBehindBody=original.weaponBehindBody,nearHand=new Rect(grip.x-.05f,grip.y-.045f,.10f,.09f),
                    farHand=new Rect(far.x-.04f,far.y-.04f,.08f,.08f)};
            }
            return result;
        }
    }
}
