using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace AlienPurge.Level01.Editor
{
    // A one-time, scene-local migration. Labels prevent later installs from resetting hand tuning.
    public static partial class IntegratedWorldFeedback
    {
        const string Label="integrated-playtest-feedback-v1";
        const string Art="Assets/_Project/Art/UI/Backpack/Sprites/";
        static readonly Color Text=new Color(.85f,.84f,.76f),Gold=new Color(.70f,.71f,.48f);
        static T[] All<T>(Scene s) where T:Component=>HiveTyrantCombatSetup.SceneComponents<T>(s);
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static bool Applied(string path)=>AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path)).Contains(Label);
        static void Mark(string path)
        {
            var asset=AssetDatabase.LoadMainAssetAtPath(path);
            AssetDatabase.SetLabels(asset,AssetDatabase.GetLabels(asset).Concat(new[]{Label}).Distinct().ToArray());
        }
        [MenuItem("AlienPurge/Gameplay/Level01/Apply Integrated Playtest Feedback V1")]
        public static void Apply()
        {
            Require(!Application.isPlaying,"Exit Play Mode before applying feedback.");
            Require(Path.GetFullPath(Application.dataPath).Replace('\\','/').Equals("D:/AlienPurge_IntegratedV01/Assets",StringComparison.OrdinalIgnoreCase),"Only the fourth integration project may be edited.");
            for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Save scene edits first.");
            IntegratedWorldInstaller.CheckProtectedScenes();
            Directory.CreateDirectory(IntegratedFeedbackAudit.Folder);
            var lines=new List<string>();
            var scene=EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
            if(!Applied(scene.path))
            {
                var loop=All<RegionLoopController>(scene).Single();
                TuneRegions(loop,lines);
                RepairRoads(loop,lines);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Mark(scene.path);
            }
            else lines.Add("World migration already applied; editable tuning and collision preserved.");
            scene=EditorSceneManager.OpenScene(IntegratedWorldInstaller.Menu);
            if(!Applied(scene.path))
            {
                StyleSettings(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Mark(scene.path);
                lines.Add("Main menu Settings now uses existing backpack/trader art. Native controls and events retained.");
            }
            else lines.Add("Settings migration already applied; authored UI preserved.");
            IntegratedWorldInstaller.CheckProtectedScenes();
            EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
            IntegratedWorldInstaller.Validate();
            File.AppendAllLines(IntegratedFeedbackAudit.Folder+"/Installation.txt",lines);
        }
        static void TuneRegions(RegionLoopController loop,List<string> lines)
        {
            string[] ids={"CargoYard","PowerMaintenance","QuarantineCommons","InfectedResearch","Extraction"};
            int[][] weights={new[]{50,20,6,12,5,7},new[]{40,20,8,14,8,10},new[]{30,20,10,16,10,14},new[]{24,18,12,18,12,16},new[]{20,18,12,20,12,18}};
            int[][] residents={new[]{1,1,1,1,1},new[]{1,1,2,1,1},new[]{1,1,2,1,2},new[]{1,1,3,2,2},new[]{1,1,1,1,1}};
            int[][] caps={new[]{3,1,1,1,1},new[]{3,2,2,1,2},new[]{3,2,3,2,3},new[]{4,2,4,2,3},new[]{4,2,4,2,3}};
            var names=new[]{"Swarmer","Rusher","Brute","Spitter","Support","Exploder"};
            for(int stage=0;stage<ids.Length;stage++)
            {
                var d=loop.definitions.Single(x=>x.regionId==ids[stage]);
                Require(AssetDatabase.GetAssetPath(d).StartsWith(IntegratedWorldInstaller.Data+"/"),"Shared region asset refused: "+d.name);
                var normal=d.Profile(EnemyType.Normal);var profiles=d.enemies.ToList();
                for(int kind=1;kind<=5;kind++)
                {
                    var type=(EnemyType)kind;var profile=profiles.SingleOrDefault(p=>p.type==type);
                    if(profile==null)
                    {
                        var data=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/Data/Enemies/Enemy_"+type+"_Data.asset");
                        Require(data!=null,"Enemy data missing: "+type);
                        profile=new RegionEnemyProfile{type=type,archetypeId=names[kind],health=Mathf.CeilToInt(normal.health*data.HealthMultiplier),
                            damage=Mathf.CeilToInt(normal.damage*data.DamageMultiplier),experience=data.ExperienceAmount,
                            speed=normal.speed*data.MoveSpeedMultiplier,clearance=kind==2?.9f:.8f,special=true};
                        profiles.Add(profile);
                    }
                    profile.simultaneousLimit=caps[stage][kind-1];
                    if(type==EnemyType.Support)profile.globalSimultaneousLimit=4;
                    if(type==EnemyType.Exploder)profile.globalSimultaneousLimit=6;
                }
                d.enemies=profiles.OrderBy(p=>(int)p.type).ToArray();
                d.reservedEnemyIds=d.reservedEnemyIds.Where(id=>!names.Contains(id)).ToArray();
                // Replace ordinary garrison slots, retaining total population and every mission-owned guard.
                int total=d.residents.Sum(g=>g.count);var guards=d.residents.Where(g=>g.keyGuard).ToList();
                int normalCount=total-guards.Sum(g=>g.count)-residents[stage].Sum();
                Require(normalCount>=1,"Not enough ordinary resident slots in "+d.regionId);
                guards.Add(new RegionSpawnGroup{type=EnemyType.Normal,count=normalCount});
                for(int kind=1;kind<=5;kind++)guards.Add(new RegionSpawnGroup{type=(EnemyType)kind,count=residents[stage][kind-1]});
                d.residents=guards.ToArray();
                d.specialLimit=new[]{5,6,7,9,9}[stage];
                d.v02.environmentSquads=Enumerable.Range(0,6).Select(kind=>
                {
                    int count=stage>=2&&(kind==3||kind==5)?2:1;
                    return new RegionWave{id="integrated-"+names[kind].ToLowerInvariant(),weight=weights[stage][kind],
                        groups=kind==0?new[]{new RegionSpawnGroup{type=EnemyType.Normal,count=5}}:
                            new[]{new RegionSpawnGroup{type=(EnemyType)kind,count=count},new RegionSpawnGroup{type=EnemyType.Normal,count=5-count}}};
                }).ToArray();
                d.v02.weightedEnvironmentSquads=true;
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
                lines.Add(d.regionId+": weights [Swarmer,Rusher,Brute,Spitter,Support,Exploder]="+string.Join("/",weights[stage])+"; type caps="+string.Join("/",caps[stage])+"; special cap="+d.specialLimit+"; residents="+total+"; access guards="+d.residents.Where(g=>g.keyGuard).Sum(g=>g.count));
            }
            var so=new SerializedObject(loop.pool);var entries=so.FindProperty("poolEntries");
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);int type=entry.FindPropertyRelative("enemyType").intValue;
                if(type!=4&&type!=5)continue;
                entry.FindPropertyRelative("initialPoolSize").intValue=type==4?4:6;
                entry.FindPropertyRelative("allowExpansion").boolValue=false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            lines.Add("ColonyEntry unchanged. Total regional/global population limits, tasks, ability data and Boss summons unchanged. Support pool=4, Exploder pool=6; expansion off.");
        }
        static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png");
        static RectTransform Rect(Transform t,Vector2 position,Vector2 size)
        {
            var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;r.localScale=Vector3.one;return r;
        }
        static Image Panel(Transform parent,string name,string sprite,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            Rect(go.transform,position,size);var img=go.GetComponent<Image>();img.sprite=Sprite(sprite);img.type=Image.Type.Sliced;img.raycastTarget=false;return img;
        }
        static void StyleText(TMP_Text t,string text,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
        {t.text=text;t.fontSize=size;t.enableAutoSizing=false;t.color=color;t.alignment=alignment;t.raycastTarget=false;t.enableWordWrapping=false;}
        static TMP_Text NewText(Transform parent,string name,string text,Vector2 pos,Vector2 size,float fontSize,TMP_FontAsset font)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            Rect(go.transform,pos,size);var t=go.GetComponent<TMP_Text>();t.font=font;StyleText(t,text,fontSize,Text);return t;
        }
        static void StyleSettings(Scene scene)
        {
            var settings=All<AudioSettingsPanel>(scene).Single();var root=(RectTransform)settings.transform;
            var children=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i)).ToArray();
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.localScale=Vector3.one;
            var shade=root.GetComponent<Image>();shade.sprite=null;shade.type=Image.Type.Simple;shade.color=new Color(.015f,.022f,.018f,.78f);shade.raycastTarget=true;
            var frame=Panel(root,"SettingsFrame","Panel_Main_9Slice",Vector2.zero,new Vector2(760,700)).rectTransform;frame.GetComponent<Image>().raycastTarget=true;
            foreach(var child in children)child.SetParent(frame,false);
            var font=All<TMP_Text>(scene).First(t=>t.name=="PendingItemCountText").font;
            var header=Panel(frame,"SettingsHeader","Panel_Header_9Slice",new Vector2(0,260),new Vector2(688,100));header.transform.SetAsFirstSibling();
            var title=frame.Find("SettingsTitle").GetComponent<TMP_Text>();title.font=font;
            Rect(title.transform,new Vector2(0,268),new Vector2(624,44));StyleText(title,"SETTINGS",32,Text);
            var subtitle=NewText(frame,"SettingsSubtitle","AUDIO CONTROL",new Vector2(6,236),new Vector2(612,24),16,font);subtitle.color=Gold;
            string[] rowNames={"MasterVolumeRow","MusicVolumeRow","SFXVolumeRow","UIVolumeRow"};
            string[] labels={"MASTER","MUSIC","SFX","UI"};
            for(int i=0;i<rowNames.Length;i++)
            {
                var row=frame.Find(rowNames[i]);Rect(row,new Vector2(0,146-88*i),new Vector2(688,76));
                var bg=row.GetComponent<Image>()??row.gameObject.AddComponent<Image>();bg.sprite=Sprite("Panel_Detail_9Slice");bg.type=Image.Type.Sliced;bg.raycastTarget=false;bg.color=Color.white;
                var label=row.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name.EndsWith("Label"));
                label.font=font;Rect(label.transform,new Vector2(-239,0),new Vector2(154,40));StyleText(label,labels[i],23,Text);
                var value=row.GetComponentsInChildren<TMP_Text>(true).Single(t=>t!=label);
                value.font=font;Rect(value.transform,new Vector2(278,0),new Vector2(76,40));StyleText(value,value.text,22,Gold,TextAlignmentOptions.MidlineRight);
                var slider=row.GetComponentInChildren<Slider>(true);Rect(slider.transform,new Vector2(45,0),new Vector2(324,56));
                var hit=slider.GetComponent<Image>()??slider.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
                var track=slider.transform.Find("Background").GetComponent<Image>();Rect(track.transform,Vector2.zero,new Vector2(324,18));track.sprite=Sprite("Panel_GridArea_9Slice");track.type=Image.Type.Sliced;track.color=Color.white;track.raycastTarget=false;
                var fillArea=(RectTransform)slider.fillRect.parent;Rect(fillArea,Vector2.zero,new Vector2(304,6));
                var fill=slider.fillRect.GetComponent<Image>();fill.sprite=null;fill.type=Image.Type.Simple;fill.color=Gold;fill.raycastTarget=false;
                slider.fillRect.offsetMin=slider.fillRect.offsetMax=Vector2.zero;
                var handleArea=(RectTransform)slider.handleRect.parent;Rect(handleArea,Vector2.zero,new Vector2(304,28));
                var handle=slider.handleRect.GetComponent<Image>();handle.sprite=null;handle.type=Image.Type.Simple;handle.color=new Color(.86f,.86f,.66f);
                slider.handleRect.sizeDelta=new Vector2(22,0);slider.handleRect.anchoredPosition=Vector2.zero;
                slider.transition=Selectable.Transition.ColorTint;var colors=slider.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.97f,.72f);colors.pressedColor=Gold;colors.selectedColor=colors.highlightedColor;slider.colors=colors;
                slider.SetValueWithoutNotify(slider.value);
            }
            var divider=frame.Find("BottomDivider").GetComponent<Image>();Rect(divider.transform,new Vector2(0,-186),new Vector2(688,3));divider.sprite=Sprite("Divider_Horizontal");divider.color=Color.white;divider.raycastTarget=false;
            StyleButton(frame.Find("ResetDefaultsButton"),new Vector2(-176,-250),"RESET DEFAULTS",font);
            StyleButton(frame.Find("BackButton"),new Vector2(176,-250),"BACK",font);
            var hint=NewText(frame,"SettingsSaveHint","CHANGES ARE SAVED AUTOMATICALLY",new Vector2(0,-308),new Vector2(624,24),15,font);hint.alignment=TextAlignmentOptions.Center;hint.color=new Color(.59f,.61f,.54f);
            root.gameObject.SetActive(false);
        }
        static void StyleButton(Transform t,Vector2 pos,string text,TMP_FontAsset font)
        {
            Rect(t,pos,new Vector2(336,62));var button=t.GetComponent<Button>();var img=t.GetComponent<Image>();img.sprite=Sprite("Button_Use_Normal");img.type=Image.Type.Sliced;img.color=Color.white;
            button.transition=Selectable.Transition.SpriteSwap;button.spriteState=new SpriteState{highlightedSprite=Sprite("Button_Use_Hover"),selectedSprite=Sprite("Button_Use_Hover"),pressedSprite=Sprite("Button_Use_Pressed"),disabledSprite=Sprite("Button_Use_Disabled")};
            var label=t.GetComponentInChildren<TMP_Text>(true);label.font=font;Rect(label.transform,Vector2.zero,new Vector2(300,44));StyleText(label,text,22,Text,TextAlignmentOptions.Center);
        }
        public static void VerifyAndRunRegression()
        {
            var paths=Directory.GetFiles(IntegratedWorldInstaller.Data,"*.asset",SearchOption.AllDirectories)
                .Concat(new[]{IntegratedWorldInstaller.Target,IntegratedWorldInstaller.Menu}).ToArray();
            var before=paths.ToDictionary(p=>p,File.ReadAllBytes);
            Apply();
            foreach(var p in paths)Require(before[p].SequenceEqual(File.ReadAllBytes(p)),"Repeated feedback migration changed authored content: "+p);
            File.WriteAllText(IntegratedFeedbackAudit.Folder+"/RepeatApply.txt","PASS: repeated feedback application preserves both scene files, all regional tuning and cropped mesh bytes.\n");
            IntegratedWorldFinalVerification.Verify();
            IntegratedWorldRouteVerification.Run();
            IntegratedRoadProbe.Run();
            RegionLoopV02Regression.RunIntegrated();
        }
    }
}
