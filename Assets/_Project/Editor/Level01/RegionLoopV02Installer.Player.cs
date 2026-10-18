using System;
using System.Collections.Generic;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Installer
    {
        static void ConfigurePlayer(Scene scene)
        {
            // HUD revisions are independent of the player's tuned movement/weapon rig.
            var existingStatus=All<PlayerStatusPanel>(scene).FirstOrDefault();
            if(existingStatus==null||existingStatus.installationRevision<3)PolishPlayerStatus(scene);
            var movement=One<PlayerMovement>(scene);
            var rig=movement.GetComponent<PlayerCombatPresentation>()??movement.gameObject.AddComponent<PlayerCombatPresentation>();
            if(rig.installationRevision>=5)return;
            Set(movement,"tacticalLocomotion",true);
            Set(movement,"accelerationTime",.075f);Set(movement,"brakingTime",.045f);Set(movement,"initialSpeedFraction",.45f);
            var visual=movement.transform.Find("VisualRoot");
            var stance=visual.Find("LocomotionPose");
            if(stance==null){stance=new GameObject("LocomotionPose").transform;stance.SetParent(visual,false);}
            var body=visual.Find("BodyVisual")??stance.Find("BodyVisual");
            var socket=visual.Find("WeaponSocket")??stance.Find("WeaponSocket");
            body.SetParent(stance,false);socket.SetParent(stance,false);
            rig.stanceRoot=stance;rig.bodyRenderer=body.GetComponent<SpriteRenderer>();rig.weaponSocket=socket;
            rig.weaponHolder=socket.Find("WeaponHolder");rig.weaponSorting=rig.weaponHolder.GetComponent<SortingGroup>();
            rig.weaponHolder.localScale=Vector3.one*.72f;
            // These drivers belong to the older movement-facing setup. V02 owns presentation without changing their shared assets.
            body.GetComponent<Animator>().enabled=false;
            movement.GetComponent<PlayerAnimationDriver>().enabled=false;
            socket.GetComponent<WeaponSocketController>().enabled=false;
            var hands=stance.Find("GripOcclusion");
            if(hands==null){hands=new GameObject("GripOcclusion",typeof(SpriteRenderer)).transform;hands.SetParent(stance,false);}
            rig.handOcclusion=hands.GetComponent<SpriteRenderer>();rig.handOcclusion.sortingLayerID=rig.bodyRenderer.sortingLayerID;
            rig.handOcclusion.sortingOrder=4;rig.handOcclusion.sharedMaterial=rig.bodyRenderer.sharedMaterial;
            hands.localScale=body.localScale;
            var support=stance.Find("SupportGripOcclusion");
            if(support==null){support=new GameObject("SupportGripOcclusion",typeof(SpriteRenderer)).transform;support.SetParent(stance,false);}
            rig.supportHandOcclusion=support.GetComponent<SpriteRenderer>();rig.supportHandOcclusion.sortingLayerID=rig.bodyRenderer.sortingLayerID;
            rig.supportHandOcclusion.sortingOrder=4;rig.supportHandOcclusion.sharedMaterial=rig.bodyRenderer.sharedMaterial;support.localScale=body.localScale;
            string[] dirs={"E","NE","N","NW","W","SW","S","SE"};
            Vector2[] grips={new Vector2(.598f,.68f),new Vector2(.695f,.59f),new Vector2(.64f,.54f),new Vector2(.33f,.55f),
                new Vector2(.408f,.666f),new Vector2(.54f,.694f),new Vector2(.612f,.701f),new Vector2(.50f,.686f)};
            Rect[] near={new Rect(.545f,.635f,.11f,.095f),new Rect(),new Rect(),new Rect(),new Rect(.351f,.621f,.115f,.102f),
                new Rect(.478f,.65f,.114f,.09f),new Rect(.564f,.661f,.1f,.085f),new Rect(.45f,.639f,.103f,.1f)};
            Rect[] far={new Rect(.657f,.584f,.076f,.08f),new Rect(),new Rect(),new Rect(),new Rect(.264f,.558f,.081f,.08f),
                new Rect(.287f,.602f,.089f,.085f),new Rect(.334f,.665f,.095f,.085f),new Rect(.669f,.628f,.071f,.073f)};
            rig.poses=new PlayerCombatPresentation.Pose[8];
            for(int i=0;i<8;i++)
            {
                var pose=new PlayerCombatPresentation.Pose{direction=dirs[i],grip=grips[i],nearHand=near[i],farHand=far[i],weaponBehindBody=i>=1&&i<=3};
                pose.idle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Player/Idle/Player_Idle_"+dirs[i]+".png");
                if(pose.idle==null)throw new InvalidOperationException("Missing player direction art: "+dirs[i]);
                rig.poses[i]=pose;
            }
            string[] art={"PlasmaRifle/Weapon_PlasmaRifle","Thunder/Weapon_ThunderPiercer","Cluster/Weapon_ClusterBurst"};
            Vector2[] weaponGrips={new Vector2(.366f,.61f),new Vector2(.30f,.59f),new Vector2(.33f,.63f)};
            rig.weaponGrips=new PlayerCombatPresentation.WeaponGrip[3];
            for(int i=0;i<3;i++)rig.weaponGrips[i]=new PlayerCombatPresentation.WeaponGrip{sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Weapons/"+art[i]+".png"),normalizedGrip=weaponGrips[i]};
            rig.bodyRenderer.sprite=rig.poses[6].idle;
            string strideMaterialPath=DataFolder+"/PlayerHeldStride.mat";
            rig.heldStrideMaterial=AssetDatabase.LoadAssetAtPath<Material>(strideMaterialPath);
            if(rig.heldStrideMaterial==null)
            {
                rig.heldStrideMaterial=new Material(Shader.Find("AlienPurge/Player/HeldStride")){name="PlayerHeldStride"};
                AssetDatabase.CreateAsset(rig.heldStrideMaterial,strideMaterialPath);
            }
            var weapon=One<WeaponController>(scene);Set(weapon,"recoilDistance",.055f);Set(weapon,"recoilRecoveryDuration",.095f);Set(weapon,"useScaledFeedbackTime",true);
            Set(One<PlayerHealth>(scene),"bodySpriteRenderer",rig.bodyRenderer);Set(One<PlayerHealth>(scene),"visualRoot",visual);
            rig.installationRevision=5;EditorUtility.SetDirty(rig);
        }
        static void PolishPlayerStatus(Scene scene)
        {
            var hud=One<HUDManager>(scene);uiFont=Reference<TMP_Text>(One<MissionHUD>(scene),"titleText").font;
            var canvas=hud.GetComponentInParent<Canvas>().rootCanvas;
            var frame=Plate(canvas.transform,"CombatStatus","Panel_Main_9Slice",Vector2.zero,new Vector2(400,180));
            var root=frame.rectTransform;root.anchorMin=root.anchorMax=root.pivot=new Vector2(0,1);root.anchoredPosition=new Vector2(28,-24);
            var hp=Reference<TMP_Text>(hud,"hpText");var xp=Reference<TMP_Text>(hud,"expText");var level=Reference<TMP_Text>(hud,"levelText");
            var timer=Reference<TMP_Text>(hud,"timeText");var kills=Reference<TMP_Text>(hud,"killCountText");
            var hpSlider=Reference<Slider>(hud,"hpSlider");var xpSlider=Reference<Slider>(hud,"expSlider");
            var oldHP=hp.transform.parent.gameObject;var oldTime=timer.transform.parent.gameObject;var oldKills=kills.transform.parent.gameObject;
            foreach(string name in new[]{"Header","Title"}){var old=root.Find(name);if(old!=null)old.gameObject.SetActive(false);}
            StatusIcon(root,"HealthIcon","HP",new Vector2(-165,51),44);
            StatusIcon(root,"ExperienceIcon","EXP",new Vector2(-165,-12),40);
            var levelPlate=Plate(root,"LevelBadge","Panel_Detail_9Slice",new Vector2(134,57),new Vector2(98,36));
            Style(level,levelPlate.transform,Vector2.zero,new Vector2(82,30),18,Gold,TextAlignmentOptions.Center);
            Style(hp,root,new Vector2(-27,56),new Vector2(220,40),25,Ink);
            var fill=StatusBar(hpSlider,root,new Vector2(22,30),new Vector2(318,13),new Color(.80f,.35f,.28f));
            Style(xp,root,new Vector2(22,-6),new Vector2(318,28),19,Ink);
            StatusBar(xpSlider,root,new Vector2(22,-29),new Vector2(318,7),new Color(.44f,.76f,.71f));
            Plate(root,"Footer","Panel_Detail_9Slice",new Vector2(0,-64),new Vector2(374,34));
            // The original clock has more transparent padding than the other three icons.
            StatusIcon(root,"TimeIcon","Time",new Vector2(-162,-64),42);
            StatusIcon(root,"KillsIcon","Kills",new Vector2(75,-64),26);
            Style(timer,root,new Vector2(-63,-64),new Vector2(154,27),19,Ink);
            Style(kills,root,new Vector2(130,-64),new Vector2(76,30),19,Ink,TextAlignmentOptions.MidlineRight);
            foreach(var old in new[]{oldHP,oldTime,oldKills})if(old!=frame.gameObject)old.SetActive(false);
            Set(hud,"tacticalLabels",true);
            hud.UpdateHealthUI(5,5);hud.UpdateExperienceUI(0,5);hud.UpdateLevelUI(1);hud.UpdateTimeUI(0,0,false);hud.UpdateKillCountUI(0);
            var status=frame.GetComponent<PlayerStatusPanel>();if(status==null)status=frame.gameObject.AddComponent<PlayerStatusPanel>();
            status.health=hpSlider;status.healthFill=fill;status.healthLabel=hp;
            status.normal=new Color(.80f,.35f,.28f);status.installationRevision=3;EditorUtility.SetDirty(status);
            foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
        }
        static void StatusIcon(Transform parent,string name,string asset,Vector2 position,float size)
        {
            var icon=Plate(parent,name,"Panel_IconPreview_9Slice",position,new Vector2(size,size));
            icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Icons/UI_Icon_"+asset+".png");
            if(icon.sprite==null)throw new InvalidOperationException("Missing original status icon: "+asset);
            icon.type=Image.Type.Simple;icon.preserveAspect=true;icon.color=new Color(.94f,.94f,.87f);
        }
        static Image StatusBar(Slider slider,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            slider.transform.SetParent(parent,false);Place((RectTransform)slider.transform,position,size);slider.transform.localScale=Vector3.one;
            foreach(Transform child in slider.transform)child.gameObject.SetActive(false);
            var back=Plate(slider.transform,"TacticalTrack","Panel_Detail_9Slice",Vector2.zero,size);
            back.sprite=null;back.color=new Color(.07f,.1f,.08f);
            var fill=Plate(slider.transform,"TacticalFill","Panel_Detail_9Slice",Vector2.zero,size);fill.sprite=null;fill.color=color;
            fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
            slider.fillRect=fill.rectTransform;slider.handleRect=null;slider.targetGraphic=null;slider.interactable=false;
            slider.navigation=new Navigation{mode=Navigation.Mode.None};return fill;
        }
        static void ValidatePlayer(Scene scene,List<string> errors)
        {
            var rig=One<PlayerCombatPresentation>(scene);var movement=One<PlayerMovement>(scene);
            if(!movement.TacticalLocomotion||rig.installationRevision<1||rig.poses==null||rig.poses.Length!=8||
                rig.poses.Any(p=>p.idle==null))errors.Add("Player direction/locomotion configuration missing.");
            if(rig.bodyRenderer==null||rig.handOcclusion==null||rig.supportHandOcclusion==null||rig.weaponHolder==null||rig.weaponSorting==null||
                rig.bodyRenderer.GetComponent<Animator>().enabled||movement.GetComponent<PlayerAnimationDriver>().enabled||
                rig.weaponSocket.GetComponent<WeaponSocketController>().enabled)errors.Add("Competing or incomplete player presentation drivers.");
            if(rig.weaponGrips==null||rig.weaponGrips.Length!=3||rig.weaponGrips.Any(p=>p.sprite==null))errors.Add("Weapon grip profiles missing.");
            if(rig.heldStrideMaterial==null||ShaderUtil.ShaderHasError(rig.heldStrideMaterial.shader))errors.Add("Held upper body / walking leg shader missing or invalid.");
            var panel=One<PlayerStatusPanel>(scene);var hud=One<HUDManager>(scene);
            if(panel.health!=Reference<Slider>(hud,"hpSlider")||panel.healthLabel!=Reference<TMP_Text>(hud,"hpText")||
                !new SerializedObject(hud).FindProperty("tacticalLabels").boolValue||panel.GetComponentsInChildren<Graphic>(true).Any(g=>g.raycastTarget))
                errors.Add("Status panel must use native HUD values and never intercept input.");
        }
    }
}
