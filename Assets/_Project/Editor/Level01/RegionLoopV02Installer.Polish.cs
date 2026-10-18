using System;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Installer
    {
        const string Skin="Assets/_Project/Art/UI/Backpack/Sprites/";
        static readonly Color Gold=new Color(.86f,.76f,.49f), Ink=new Color(.9f,.91f,.85f), Muted=new Color(.64f,.7f,.65f), Cyan=new Color(.46f,.89f,.87f);
        static TMP_FontAsset uiFont;
        static Sprite SkinSprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Skin+name+".png");
        static RectTransform UIRect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var child=parent.Find(name);
            var r=child!=null?(RectTransform)child:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);r.gameObject.SetActive(true);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
            Place(r,position,size);return r;
        }
        static void Place(RectTransform r,Vector2 position,Vector2 size)
        {r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;}
        static Image Plate(Transform parent,string name,string sprite,Vector2 position,Vector2 size)
        {
            var r=UIRect(parent,name,position,size);var image=r.GetComponent<Image>()??r.gameObject.AddComponent<Image>();
            image.sprite=SkinSprite(sprite);image.type=Image.Type.Sliced;image.color=Color.white;image.raycastTarget=false;return image;
        }
        static TMP_Text Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float fontSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {
            var r=UIRect(parent,name,position,size);var label=r.GetComponent<TMP_Text>()??r.gameObject.AddComponent<TextMeshProUGUI>();
            Style(label,parent,position,size,fontSize,color,align);label.text=text;return label;
        }
        static void Style(TMP_Text label,Transform parent,Vector2 position,Vector2 size,float fontSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {
            label.transform.SetParent(parent,false);label.gameObject.SetActive(true);label.enabled=true;
            Place(label.rectTransform,position,size);label.rectTransform.localScale=Vector3.one;
            label.font=uiFont;label.fontSize=fontSize;label.enableAutoSizing=false;label.alignment=align;
            label.color=color;label.fontStyle=FontStyles.Normal;label.margin=Vector4.zero;label.enableWordWrapping=true;
            label.overflowMode=TextOverflowModes.Ellipsis;label.raycastTarget=false;
        }
        static RegionHudGlyph Glyph(Transform parent,string name,RegionHudGlyph.Shape shape,Vector2 position,Vector2 size)
        {
            var r=UIRect(parent,name,position,size);var glyph=r.GetComponent<RegionHudGlyph>()??r.gameObject.AddComponent<RegionHudGlyph>();
            glyph.shape=shape;glyph.color=Gold;glyph.raycastTarget=false;return glyph;
        }
        static void ConfigurePolish(Scene scene,RegionLoopController loop)
        {
            if(loop.polishRevision>=3)return;
            loop.combatGrowth=true;
            for(int i=3;i<loop.definitions.Length;i++)
            {
                var d=loop.definitions[i];d.aliveLimit=Mathf.Max(d.aliveLimit,d.v02.ambientTarget+14);d.specialLimit=5;
                var heavy=d.Profile(EnemyType.Heavy);if(heavy!=null)heavy.simultaneousLimit=2;
                if(!d.v02.environmentSquads.Any(w=>w.id=="pursuit"))
                    d.v02.environmentSquads=d.v02.environmentSquads.Concat(new[]{W("pursuit",G(EnemyType.Normal,3),G(EnemyType.Fast,2))}).ToArray();
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
            }
            uiFont=Reference<TMP_Text>(One<MissionHUD>(scene),"titleText").font;
            PolishMission(scene);PolishAbilities(scene);PolishResults(scene);
            loop.polishRevision=3;
        }
        static void PolishMission(Scene scene)
        {
            var hud=One<MissionHUD>(scene);var group=Reference<CanvasGroup>(hud,"missionPanelGroup");var root=(RectTransform)group.transform;
            root.SetParent(root.GetComponentInParent<Canvas>().rootCanvas.transform,false);
            root.anchorMin=root.anchorMax=root.pivot=Vector2.one;root.anchoredPosition=new Vector2(-28,-24);root.sizeDelta=new Vector2(440,274);root.localScale=Vector3.one;
            var image=root.GetComponent<Image>()??root.gameObject.AddComponent<Image>();image.sprite=SkinSprite("Panel_Main_9Slice");image.type=Image.Type.Sliced;image.color=Color.white;image.raycastTarget=false;
            // Keep native MissionHUD and its field references. Layout changes are explicitly requested in this revision.
            string[] fields={"titleText","objectiveText","timerText","distanceText","rewardText","riskText","progressText"};
            var labels=fields.Select(f=>Reference<TMP_Text>(hud,f)).ToArray();
            foreach(var label in labels)label.transform.SetParent(root,false);
            var progress=Reference<GameObject>(hud,"progressRoot");progress.transform.SetParent(root,false);
            foreach(Transform child in root)
                if(!labels.Any(t=>t.transform==child)&&child!=progress.transform)child.gameObject.SetActive(false);
            Plate(root,"BriefingHeader","Panel_Header_9Slice",new Vector2(0,107),new Vector2(424,48));
            Style(labels[0],root,new Vector2(-8,107),new Vector2(384,40),22,Gold);
            Style(labels[1],root,new Vector2(-24,51),new Vector2(350,62),23,Ink);
            Style(labels[2],root,new Vector2(-99,-2),new Vector2(204,28),18,Gold);
            Style(labels[3],root,new Vector2(115,-2),new Vector2(160,28),18,Muted,TextAlignmentOptions.MidlineRight);
            Style(labels[4],root,new Vector2(0,-66),new Vector2(394,28),17,Cyan);
            Style(labels[5],root,new Vector2(0,-103),new Vector2(394,48),17,Gold);
            Style(labels[6],root,new Vector2(178,-33),new Vector2(48,24),15,Muted,TextAlignmentOptions.MidlineRight);
            var slider=Reference<Slider>(hud,"progressSlider");
            if(slider.transform!=progress.transform)slider.transform.SetParent(progress.transform,false);
            Place((RectTransform)progress.transform,new Vector2(-27,-33),new Vector2(340,7));
            if(slider.transform!=progress.transform)Place((RectTransform)slider.transform,Vector2.zero,new Vector2(340,7));slider.interactable=false;
            if(slider.handleRect!=null)slider.handleRect.gameObject.SetActive(false);
            foreach(var img in slider.GetComponentsInChildren<Image>(true)){img.raycastTarget=false;img.color=img.transform==slider.fillRect?Gold:new Color(.15f,.22f,.2f);}
            var arrow=One<MissionDirectionArrow>(scene);var arrowRect=Reference<RectTransform>(arrow,"arrowRectTransform");
            arrowRect.SetParent(root,false);Place(arrowRect,new Vector2(184,51),new Vector2(34,34));
            var old=arrowRect.GetComponent<Image>();if(old!=null)Object.DestroyImmediate(old);
            var glyph=arrowRect.GetComponent<RegionHudGlyph>()??arrowRect.gameObject.AddComponent<RegionHudGlyph>();glyph.shape=RegionHudGlyph.Shape.Arrow;glyph.color=Gold;glyph.raycastTarget=false;
            Set(arrow,"spriteAngleOffset",0f);Set(hud,"normalColor",Ink);Set(hud,"riskColor",Gold);
            // Sibling order keeps text above textured plates.
            root.Find("BriefingHeader").SetAsFirstSibling();
            foreach(var t in labels)t.transform.SetAsLastSibling();
        }
        static void PolishAbilities(Scene scene)
        {
            var hud=One<PlayerAbilityHUD>(scene);var root=(RectTransform)hud.transform;
            root.SetParent(root.GetComponentInParent<Canvas>().rootCanvas.transform,false);
            foreach(Transform child in root)child.gameObject.SetActive(false);
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,0);root.anchoredPosition=new Vector2(0,22);root.sizeDelta=new Vector2(552,104);root.localScale=Vector3.one;
            var backing=root.GetComponent<Image>();if(backing!=null)backing.enabled=false;
            var names=new[]{"dash","jump","pulse"};var titles=new[]{"冲刺","跳跃","脉冲"};var keys=new[]{"SHIFT","SPACE","Q"};
            for(int i=0;i<3;i++)
            {
                var slot=Plate(root,"TacticalSlot_"+names[i],"Panel_Detail_9Slice",new Vector2((i-1)*184,0),new Vector2(176,98));
                var icon=Plate(slot.transform,"IconPlate","Panel_IconPreview_9Slice",new Vector2(-48,5),new Vector2(52,52));
                Glyph(icon.transform,"Glyph",(RegionHudGlyph.Shape)(i+1),Vector2.zero,new Vector2(34,34));
                var overlay=Plate(icon.transform,"Cooldown","Slot_Normal",Vector2.zero,new Vector2(50,50));
                overlay.type=Image.Type.Filled;overlay.fillMethod=Image.FillMethod.Radial360;overlay.fillOrigin=2;overlay.color=new Color(.02f,.05f,.04f,.85f);overlay.fillAmount=0;
                Label(slot.transform,"Name",titles[i],new Vector2(26,23),new Vector2(72,28),20,Ink);
                var text=Label(slot.transform,"CooldownText","就绪",new Vector2(26,-4),new Vector2(72,25),17,Cyan);
                var key=Label(slot.transform,"Key",keys[i],new Vector2(0,-32),new Vector2(140,20),13,Gold,TextAlignmentOptions.Center);
                Set(hud,names[i]+"Icon",icon);Set(hud,names[i]+"CooldownOverlay",overlay);Set(hud,names[i]+"CooldownText",text);Set(hud,names[i]+"KeyText",key);
            }
            Set(hud,"readyText","就绪");Set(hud,"activeText","释放中");
            Set(hud,"readyIconColor",Color.white);Set(hud,"coolingIconColor",new Color(.45f,.5f,.48f));Set(hud,"activeIconColor",Cyan);
        }
        static void PolishResults(Scene scene)
        {
            var gm=One<GameManager>(scene);
            foreach(string field in new[]{"gameOverPanel","victoryPanel"})
            {
                var panel=Reference<GameObject>(gm,field);var root=(RectTransform)panel.transform;
                var buttons=panel.GetComponentsInChildren<Button>(true);
                Func<Button,string,bool> calls=(b,method)=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)==method);
                var restart=buttons.Single(b=>calls(b,"RestartGame"));var back=buttons.Single(b=>calls(b,"ReturnToBase"));
                foreach(Transform child in root)child.gameObject.SetActive(false);
                root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.pivot=new Vector2(.5f,.5f);root.offsetMin=root.offsetMax=Vector2.zero;root.localScale=Vector3.one;
                var dim=root.GetComponent<Image>()??root.gameObject.AddComponent<Image>();dim.sprite=null;dim.color=new Color(.015f,.025f,.02f,.91f);dim.raycastTarget=true;
                var frame=Plate(root,"DebriefFrame","Panel_Main_9Slice",Vector2.zero,new Vector2(1020,680));
                Plate(frame.transform,"Header","Panel_Header_9Slice",new Vector2(0,264),new Vector2(988,116));
                var view=panel.GetComponent<RegionResultPresentation>()??panel.AddComponent<RegionResultPresentation>();
                view.heading=Label(frame.transform,"Heading","行动报告",new Vector2(0,278),new Vector2(900,54),40,Gold,TextAlignmentOptions.Center);
                view.subtitle=Label(frame.transform,"Subtitle","",new Vector2(0,234),new Vector2(900,30),19,Muted,TextAlignmentOptions.Center);
                var statNames=new[]{"行动时间","消灭敌人","最终等级"};var values=new TMP_Text[3];
                for(int i=0;i<3;i++)
                {
                    var card=Plate(frame.transform,"Stat_"+i,"Panel_Detail_9Slice",new Vector2((i-1)*312,139),new Vector2(300,104));
                    Label(card.transform,"Label",statNames[i],new Vector2(0,26),new Vector2(260,26),18,Muted,TextAlignmentOptions.Center);
                    values[i]=Label(card.transform,"Value","--",new Vector2(0,-15),new Vector2(260,50),34,Ink,TextAlignmentOptions.Center);
                }
                view.timeValue=values[0];view.killValue=values[1];view.levelValue=values[2];
                var cargo=Plate(frame.transform,"CargoSummary","Panel_Detail_9Slice",new Vector2(0,-60),new Vector2(928,264));
                view.lootTitle=Label(cargo.transform,"Title","物资",new Vector2(-222,102),new Vector2(400,30),22,Gold);
                view.lootSummary=Label(cargo.transform,"Loot","",new Vector2(-222,-18),new Vector2(400,202),19,Ink,TextAlignmentOptions.TopLeft);
                view.settlement=Label(cargo.transform,"Settlement","",new Vector2(236,-16),new Vector2(398,194),21,Ink,TextAlignmentOptions.TopLeft);
                StyleButton(restart,frame.transform,new Vector2(-238,-264),"再次行动",field=="gameOverPanel");
                StyleButton(back,frame.transform,new Vector2(238,-264),"返回基地",field=="victoryPanel");
                var retryRoot=UIRect(frame.transform,"RetrySettlement",new Vector2(236,-150),new Vector2(358,52));
                var retry=retryRoot.GetComponent<Button>()??retryRoot.gameObject.AddComponent<Button>();StyleButton(retry,frame.transform,new Vector2(236,-150),"重试保存",true,new Vector2(358,52));
                if(retry.onClick.GetPersistentEventCount()==0)UnityEventTools.AddPersistentListener(retry.onClick,gm.RetryRunSettlement);
                retry.gameObject.SetActive(false);view.restart=restart;view.returnToBase=back;view.retrySave=retry;
                panel.SetActive(false);
            }
        }
        static void StyleButton(Button button,Transform parent,Vector2 position,string caption,bool primary,Vector2? size=null)
        {
            button.transform.SetParent(parent,false);button.gameObject.SetActive(true);Place((RectTransform)button.transform,position,size??new Vector2(444,66));
            button.transform.localScale=Vector3.one;
            foreach(Transform child in button.transform)child.gameObject.SetActive(false);
            var image=button.GetComponent<Image>()??button.gameObject.AddComponent<Image>();
            string stem=primary?"Button_Use_":"Button_Discard_";
            image.sprite=SkinSprite(stem+"Normal");image.type=Image.Type.Sliced;image.color=Color.white;image.raycastTarget=true;
            button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=SkinSprite(stem+"Hover"),pressedSprite=SkinSprite(stem+"Pressed"),selectedSprite=SkinSprite(stem+"Hover"),disabledSprite=SkinSprite(stem+"Disabled")};
            Label(button.transform,"TacticalCaption",caption,Vector2.zero,(size??new Vector2(444,66))-new Vector2(30,12),23,Ink,TextAlignmentOptions.Center);
        }
        static void ValidatePolish(Scene scene,System.Collections.Generic.List<string> errors)
        {
            var loop=One<RegionLoopController>(scene);if(loop.polishRevision<1)return;
            if(!loop.combatGrowth)errors.Add("Combat growth disabled");
            var panel=Reference<CanvasGroup>(One<MissionHUD>(scene),"missionPanelGroup").transform;
            if(panel.parent!=panel.GetComponentInParent<Canvas>().rootCanvas.transform)errors.Add("Mission panel must anchor to screen canvas, not the small health HUD root");
            var arrow=Reference<RectTransform>(One<MissionDirectionArrow>(scene),"arrowRectTransform");
            if(arrow.GetComponent<RegionHudGlyph>()==null||arrow.GetComponent<Image>()!=null&&arrow.GetComponent<Image>().enabled)errors.Add("Direction arrow still uses a projectile image");
            foreach(var view in All<RegionResultPresentation>(scene))
                if(view.restart==null||view.returnToBase==null||view.retrySave==null||view.lootSummary==null)errors.Add("Result presentation references missing");
            if(All<RegionResultPresentation>(scene).Length!=2)errors.Add("Victory/defeat presentation pair missing");
            foreach(var hud in All<PlayerAbilityHUD>(scene))foreach(var name in new[]{"dash","jump","pulse"})
                if(Reference<Image>(hud,name+"Icon").GetComponentInChildren<RegionHudGlyph>(true)==null)errors.Add("Ability glyph missing: "+name);
        }
    }
}
