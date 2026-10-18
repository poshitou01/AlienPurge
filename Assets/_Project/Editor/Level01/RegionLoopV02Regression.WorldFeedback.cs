using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run Integrated World Feedback Checks")]
        public static void RunIntegratedWorldFeedback()=>StartIntegrated(false,false,true);
        static IEnumerator IntegratedWorldFeedbackChecks()
        {
            yield return null;loop=Object.FindObjectOfType<RegionLoopController>();specialistChecks=false;
            yield return Until(()=>loop.State("CargoYard").initialized,"Cargo resident preparation");
            ValidateEnemyMix();
            yield return CompleteMain(0);
            var cargo=loop.State("CargoYard");Move(loop.Terminal("CargoYard").transform.position);
            yield return Until(()=>loop.CurrentRegion=="CargoYard"&&Enumerable.Range(0,6).All(k=>cargo.enemies.Any(t=>(int)t.type==k&&t.lease!=null)),"All six enemies appear from actual Cargo garrison",25);
            foreach(var type in Enumerable.Range(0,6).Select(k=>(EnemyType)k))
            {
                var actor=cargo.enemies.First(t=>t.type==type&&t.lease!=null).lease.gameObject;
                var animator=actor.GetComponentInChildren<Animator>();
                Check(animator!=null&&animator.runtimeAnimatorController!=null&&actor.GetComponentInChildren<SpriteRenderer>().sprite!=null,
                    "Actual Cargo resident has authored animation and sprite: "+type);
                Check(type>=EnemyType.Support?actor.GetComponent<SupportExploderAnimation>()!=null:actor.GetComponent<CoreEnemyAnimation>()!=null,"Production presentation component: "+type);
            }
            var ranged=cargo.enemies.First(t=>t.type==EnemyType.Ranged&&t.lease!=null);var spitter=ranged.lease.GetComponent<PooledEnemy>();
            loop.regionModeEnabled=false;
            foreach(var region in loop.Regions)foreach(var ticket in region.enemies.ToArray())if(ticket!=ranged&&ticket.lease!=null)ticket.lease.Release();
            accelerate=false;Time.timeScale=1;
            var origin=AbilityClearSpace(loop.Terminal("CargoYard").transform.position);
            Move(origin,true);PlaceEnemy(spitter,origin+Vector3.right*5);
            var attack=spitter.GetComponent<RangedEnemyAttack>();
            yield return Until(()=>attack.VisualSample.Phase==EnemyAttackVisualPhase.Windup,"Resident Spitter performs native windup",10);
            yield return null;
            Check(spitter.GetComponent<CoreEnemyAnimation>().CurrentPose==CoreEnemyAnimation.Pose.Windup,"Spitter shows its formal attack windup");
            yield return Until(()=>EnemyProjectilePool.Instance.ActiveCount>0,"Resident Spitter fires a real pooled projectile",5);
            Check(Object.FindObjectsOfType<EnemyProjectile>().Any(p=>!p.IsReturned&&p.Speed>0),"Spitter projectile is active and moving");
            int guards=loop.RemainingAccessGuards(cargo),kills=GameManager.Instance.KillCount,xp=TotalXP(),amount=ranged.lease.Health.ExperienceAmount;
            spitter.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);yield return Seconds(.7f);yield return Collect();
            Check(GameManager.Instance.KillCount==kills+1&&TotalXP()==xp+amount,"Spitter awards exactly one native death and XP pickup");
            Check(loop.RemainingAccessGuards(cargo)==guards,"Ordinary Spitter does not count as an access guard");
            yield return Fresh();accelerate=false;Time.timeScale=1;
            loop.regionModeEnabled=false;
            foreach(var state in loop.Regions)foreach(var t in state.enemies.ToArray())if(t.lease!=null)t.lease.Release();
            var movement=loop.world.player.GetComponent<PlayerMovement>();movement.UseExternalInput=true;
            foreach(var test in new[]{new Vector4(-102.19297f,-1.75f,0,1),new Vector4(-102.19297f,5.75f,0,1),new Vector4(-153.05f,14.51065f,1,0),new Vector4(-53.85f,53.96615f,1,0)})
            {
                Vector2 center=new Vector2(test.x,test.y),direction=new Vector2(test.z,test.w);
                Move(loop.world.World(center-direction*2));movement.SetMovementInput(direction);yield return Seconds(.85f);
                movement.SetMovementInput(Vector2.zero);
                Check(Vector2.Dot(loop.world.Local(loop.world.PlayerFeet)-center,direction)<0,"Actual player motor cannot pass through road wall at "+center);
            }
            var gate=loop.world.progression.gates.Single(g=>g.gateId=="G7");gate.SetLocked(true,true);
            Move(loop.world.World(new Vector2(16,-32)));
            yield return WalkRoad(new Vector2(16,-40));
            Check(loop.world.Local(loop.world.PlayerFeet).y< -39,"Deleted ordinary wall no longer blocks extraction side-path entrance");
            movement.SetMovementInput(Vector2.down);yield return Seconds(2);movement.SetMovementInput(Vector2.zero);
            Check(loop.world.Local(loop.world.PlayerFeet).y> -46,"G7 still blocks premature extraction entry");
            gate.SetLocked(false,true);yield return WalkRoad(new Vector2(16,-54));yield return WalkRoad(new Vector2(16,-56.4f));yield return WalkRoad(new Vector2(23,-56.4f));
            Check(loop.world.Local(loop.world.PlayerFeet).x>22.8f,"Actual motor traverses open G7 and the repaired extraction corner");
            var pause=Object.FindObjectOfType<PauseMenuController>();pause.LoadMainMenu();loop=null;yield return null;
            yield return Until(()=>SceneManager.GetActiveScene().name=="MainMenu_IntegratedV01","Integrated main menu");
            yield return SettingsChecks();
            ValidateTestProtection();
            results.Add("LIMITS: native movement/physics, regional garrison and attacks, and UI pointer events ran in Play Mode. Deterministic movement input and invulnerable isolated profile; no human balance claim. Normal ambient scheduling and Boss route are covered by the separate full integration regression.");
        }
        static IEnumerator WalkRoad(Vector2 target)
        {
            var movement=loop.world.player.GetComponent<PlayerMovement>();double end=EditorApplication.timeSinceStartup+8;
            while(Vector2.Distance(loop.world.Local(loop.world.PlayerFeet),target)>.12f)
            {
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("Native walk blocked: "+loop.world.Local(loop.world.PlayerFeet)+" -> "+target);
                movement.SetMovementInput((target-loop.world.Local(loop.world.PlayerFeet)).normalized);yield return null;
            }
            movement.SetMovementInput(Vector2.zero);yield return Seconds(.08f);
        }
        static void ValidateEnemyMix()
        {
            Check(loop.definitions[0].enemies.Length==1,"Entry keeps its introductory Swarmer mix");
            var picker=typeof(RegionLoopController).GetMethod("SelectEnvironmentWave",BindingFlags.Instance|BindingFlags.NonPublic);
            var random=UnityEngine.Random.state;float previous=0;
            try
            {
                UnityEngine.Random.InitState(181026);
                foreach(var state in loop.Regions.Skip(1))
                {
                    var d=state.config;
                    Check(d.enemies.Select(p=>(int)p.type).OrderBy(k=>k).SequenceEqual(Enumerable.Range(0,6)),"All six profiles configured: "+d.regionId);
                    Check(Enumerable.Range(0,6).All(k=>d.residents.Any(g=>(int)g.type==k&&g.count>0)),"All six have actual resident slots: "+d.regionId);
                    Check(d.residents.Where(g=>g.keyGuard).All(g=>g.type==EnemyType.Normal),"Existing access-guard ownership preserved: "+d.regionId);
                    float fraction=d.v02.environmentSquads.Sum(w=>w.weight*w.groups.Where(g=>g.type!=EnemyType.Normal).Sum(g=>g.count)/w.groups.Sum(g=>g.count))/d.v02.environmentSquads.Sum(w=>w.weight);
                    Check(fraction>previous&&fraction<.3f,"Specialist share increases while ordinary enemies remain the majority: "+d.regionId+" = "+fraction.ToString("P1"));previous=fraction;
                    var counts=d.v02.environmentSquads.ToDictionary(w=>w,w=>0);
                    for(int n=0;n<6000;n++)counts[(RegionWave)picker.Invoke(loop,new object[]{state})]++;
                    foreach(var wave in d.v02.environmentSquads)Check(Math.Abs(counts[wave]/6000f-wave.weight/d.v02.environmentSquads.Sum(w=>w.weight))<.025f,"Native weighted sampler honors "+d.regionId+" / "+wave.id,false);
                    Check(counts.Values.All(n=>n>0),"Every configured squad can be selected by the real weighted scheduler: "+d.regionId);
                }
            }
            finally{UnityEngine.Random.state=random;}
        }
        static IEnumerator SettingsChecks()
        {
            var scene=SceneManager.GetActiveScene();var panel=HiveTyrantCombatSetup.SceneComponents<AudioSettingsPanel>(scene).Single();
            var button=HiveTyrantCombatSetup.SceneComponents<Button>(scene).Single(b=>b.name=="SettingsButton");button.onClick.Invoke();yield return null;
            Check(panel.gameObject.activeInHierarchy,"Native main-menu Settings button opens the styled panel");
            var sliders=panel.GetComponentsInChildren<Slider>();Check(sliders.Length==4,"All four native volume controls retained");
            string[] names={"Master","Music","SFX","UI"};float[] values={.25f,.5f,.75f,.9f};
            for(int i=0;i<names.Length;i++)
            {
                var slider=sliders.Single(s=>s.name.StartsWith(names[i]));var area=(RectTransform)slider.handleRect.parent;
                Vector3 point=area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin,area.rect.xMax,values[i]),0));
                var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,point),button=PointerEventData.InputButton.Left};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                Check(hits.Any(h=>h.gameObject==slider.gameObject||h.gameObject.transform.IsChildOf(slider.transform)),"Slider has an unobstructed pointer target: "+names[i]);
                ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(slider.gameObject,data,ExecuteEvents.pointerUpHandler);
                yield return null;
                float actual=i==0?AudioManager.Instance.MasterVolume:i==1?AudioManager.Instance.MusicVolume:i==2?AudioManager.Instance.SfxVolume:AudioManager.Instance.UiVolume;
                Check(Mathf.Abs(actual-values[i])<.015f,"Real slider pointer input updates native mixer: "+names[i]);
                var value=slider.transform.parent.GetComponentsInChildren<TMP_Text>().Single(t=>t.name.EndsWith("Text"));
                Check(value.text.Contains(Mathf.RoundToInt(actual*100).ToString()),"Percentage label follows mixer: "+names[i]);
            }
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1280,1024)})CaptureSettings(panel,size.x,size.y);
            var back=panel.GetComponentsInChildren<Button>().Single(b=>b.name=="BackButton");back.onClick.Invoke();yield return null;
            Check(!panel.gameObject.activeSelf&&button.gameObject.activeInHierarchy,"Back saves and restores the main menu");
            button.onClick.Invoke();yield return null;
            Check(Mathf.Abs(sliders.Single(s=>s.name.StartsWith("Music")).value-.5f)<.015f,"Reopening Settings retains the selected volume");
            panel.GetComponentsInChildren<Button>().Single(b=>b.name=="ResetDefaultsButton").onClick.Invoke();yield return null;
            Check(AudioManager.Instance.MasterVolume==1&&Mathf.Abs(AudioManager.Instance.MusicVolume-.32f)<.001f&&AudioManager.Instance.SfxVolume==1&&AudioManager.Instance.UiVolume==1,"Reset Defaults retains native 100/32/100/100 values");
            back.onClick.Invoke();yield return null;
        }
        static void CaptureSettings(AudioSettingsPanel panel,int width,int height)
        {
            var canvas=panel.GetComponentInParent<Canvas>();var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var go=new GameObject("Settings resolution check camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=height/2f;camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<LayerMask.NameToLayer("UI");
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(canvas.GetComponent<CanvasScaler>(),null);Canvas.ForceUpdateCanvases();
            foreach(var text in panel.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
            var corners=new Vector3[4];((RectTransform)panel.transform.Find("SettingsFrame")).GetWorldCorners(corners);
            Check(corners.Select(p=>camera.WorldToViewportPoint(p)).All(p=>p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1),"Settings frame fits "+width+"x"+height);
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
            File.WriteAllBytes(IntegratedFeedbackAudit.Folder+"/Settings_"+width+"x"+height+".png",tex.EncodeToPNG());RenderTexture.active=previous;
            canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;camera.targetTexture=null;
            Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(canvas.GetComponent<CanvasScaler>(),null);Canvas.ForceUpdateCanvases();
        }
    }
}
