using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run V02 Combat and UI Checks")]
        public static void RunPolish()=>Start(false,false,true,true);
        static IEnumerator CheckResultScreen(bool success)
        {
            yield return null;yield return null;
            var view=Object.FindObjectOfType<RegionResultPresentation>();
            Check(view!=null&&view.heading.text==(success?"撤离成功":"行动失败"),"Native result opens localized "+(success?"victory":"defeat")+" panel");
            Check(view.killValue.text==GameManager.Instance.KillCount.ToString()&&view.levelValue.text==GameManager.Instance.CurrentRunResultSnapshot.PlayerLevel.ToString(),"Result stats come from actual immutable run snapshot");
            Check(view.restart.interactable&&view.returnToBase.interactable&&!view.retrySave.gameObject.activeSelf,"Result actions retain native restart/base callbacks and settlement availability");
            Check(view.restart.transition==Selectable.Transition.SpriteSwap&&view.restart.spriteState.pressedSprite!=null,"Existing inventory button skins include hover/pressed/disabled states");
            foreach(var text in view.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Result text fits: "+text.name,false);}
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            var buttonRect=(RectTransform)view.returnToBase.transform;
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,buttonRect.TransformPoint(buttonRect.rect.center));
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==view.returnToBase,"Result button receives pointer raycasts above the backdrop");
            Capture(success?"Polish_Victory":"Polish_Defeat");yield return null;yield return null;
        }
        static IEnumerator Polish()
        {
            yield return Feedback();
            var late=loop.State("QuarantineCommons");var profile=late.config.Profile(EnemyType.Normal);
            var l15=loop.PreviewCombatStats(late,profile,15,false);var l25=loop.PreviewCombatStats(late,profile,25,false);
            var cap=loop.PreviewCombatStats(late,profile,200,false);var l30=loop.PreviewCombatStats(late,profile,30,false);
            Check(l15.health>=20&&l25.health>l15.health&&cap.health==l30.health,"Late health grows through level 15/25 and stops at configured level cap");
            Check(cap.speed<=profile.speed*1.221f&&cap.damage<=profile.damage+1,"Movement and contact damage growth remain bounded");
            results.Add("MEASURE Quarantine Normal HP: base="+profile.health+" L15="+l15.health+" L25="+l25.health+" cap="+cap.health);
            typeof(PlayerExperience).GetField("currentLevel",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(loop.rewards.experience,15);
            var preload=loop.State("CargoYard").enemies.First(t=>t.lease!=null&&!t.keyGuard&&t.type==EnemyType.Normal);
            int preloadHealth=preload.lease.Health.MaxHealth;
            Move(loop.Terminal("CargoYard").transform.position);
            yield return Until(()=>loop.State("CargoYard").firstEntered>=0,"High-level first entry into preloaded garrison");
            Check(preload.lease.Health.MaxHealth>preloadHealth&&preload.combatStats.level==15,"Unengaged preloaded garrison locks strength to first-entry level");
            var center=loop.stormCandidates.First(p=>loop.world.ResolveRegion(loop.world.Local(p.position))=="QuarantineCommons").position;
            Move(center,true);yield return Until(()=>loop.CurrentRegion=="QuarantineCommons"&&late.enemies.Count(t=>t.lease!=null)>10,"Late-level actual garrison");
            var survivor=late.enemies.First(t=>t.lease!=null&&!t.keyGuard&&t.type==EnemyType.Normal);
            Check(survivor.lease.Health.MaxHealth==l15.health,"Actual pooled enemies receive level-scaled attributes");
            survivor.lease.Health.TakeDamage(1);int remaining=survivor.lease.Health.CurrentHealth;
            survivor.lease.PoolMember.ReturnToPool();
            typeof(PlayerExperience).GetField("currentLevel",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(loop.rewards.experience,25);
            typeof(PlayerExperience).GetField("experienceToNextLevel",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(loop.rewards.experience,125);
            typeof(PlayerExperience).GetMethod("RefreshAllExperienceUI",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(loop.rewards.experience,null);
            yield return Until(()=>survivor.lease!=null,"Scaled survivor pool reuse");
            Check(survivor.lease.Health.CurrentHealth==remaining&&survivor.lease.Health.MaxHealth==l15.health,"Pool reuse after a level increase neither heals nor rescales existing enemies");
            late.environmentDue=Time.time;
            yield return Until(()=>late.enemies.Any(t=>t.role==RegionEnemyRole.Patrol&&t.lease!=null&&t.combatStats.level==25),"New late reinforcements use new growth snapshot");
            Check(late.enemies.Any(t=>t.role==RegionEnemyRole.Patrol&&t.lease!=null&&t.lease.Health.MaxHealth>=l25.health),"New reinforcements remain threatening after further leveling");
            var hud=Object.FindObjectOfType<PlayerAbilityHUD>();var pulse=loop.world.player.GetComponent<PlayerPulseSkill>();
            accelerate=false;Time.timeScale=1;
            typeof(PlayerPulseSkill).GetMethod("TryStartPulse",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pulse,null);
            yield return Seconds(.7f);
            Check(!pulse.IsReady&&Read<Image>(hud,"pulseCooldownOverlay").fillAmount>0&&Read<TMP_Text>(hud,"pulseCooldownText").text!="就绪","Styled ability slot reflects real native cooldown");
            var missionHud=Object.FindObjectOfType<MissionHUD>();
            foreach(var field in new[]{"titleText","objectiveText","timerText","distanceText","rewardText","riskText","progressText"})
            {var label=Read<TMP_Text>(missionHud,field);label.ForceMeshUpdate();Check(!label.isTextOverflowing,"Mission text fits: "+label.name,false);}
            var corners=new Vector3[4];Read<CanvasGroup>(missionHud,"missionPanelGroup").GetComponent<RectTransform>().GetWorldCorners(corners);
            Check(corners.All(p=>p.x>Screen.width*.5f&&p.x<=Screen.width&&p.y>=0&&p.y<=Screen.height),"Mission panel stays fully inside upper-right screen quadrant");
            Capture("Polish_CombatHUD");yield return null;yield return null;
            // Isolate the fixture target; shooting, collision and death still use native components.
            loop.regionModeEnabled=false;
            foreach(var ticket in late.enemies.Where(t=>t.lease!=null&&t!=survivor).ToArray())ticket.lease.Release();
            survivor.lease.GetComponent<EnemyMovement>().SetMovementLocked(true);survivor.lease.GetComponent<EnemyPathNavigator>().enabled=false;
            survivor.lease.transform.position=center+Vector3.right*3;
            survivor.lease.Health.InitializeHealth(l15.health);Move(center,true);Physics2D.SyncTransforms();
            var weapon=loop.world.player.GetComponent<WeaponManager>();weapon.AddBulletDamage(6);
            foreach(var b in Object.FindObjectsOfType<Bullet>())b.ReturnToPool();
            yield return Seconds(.4f);
            weapon.AimAt(survivor.lease.GetComponentInChildren<Collider2D>().bounds.center);
            Check(weapon.TryFire(),"Upgraded native weapon fires against late scaled enemy");yield return Seconds(.3f);
            Check(!survivor.dead&&survivor.lease.Health.CurrentHealth<l15.health,"Late normal enemy survives one upgraded direct hit but takes real damage");
            float end=Time.time+8;int kills=GameManager.Instance.KillCount;
            while(!survivor.dead&&Time.time<end)
            {
                weapon.AimAt(survivor.lease.GetComponentInChildren<Collider2D>().bounds.center);weapon.TryFire();yield return null;
            }
            Check(survivor.dead&&GameManager.Instance.KillCount>kills,"Scaled enemy remains killable through normal upgraded shooting");
            var arrow=Object.FindObjectOfType<MissionDirectionArrow>();var rect=Read<RectTransform>(arrow,"arrowRectTransform");
            loop.missionView.PresentRegionObjective("方向验证","", "","","","",loop.world.player.transform.position+Vector3.right*10,0,0);yield return null;yield return null;
            Check(rect.gameObject.activeSelf&&Mathf.Abs(Mathf.DeltaAngle(rect.eulerAngles.z,0))<1&&rect.GetComponent<RegionHudGlyph>()!=null,"True arrow points right without projectile sprite");
            loop.missionView.PresentRegionObjective("方向验证","", "","","","",loop.world.player.transform.position+Vector3.up*10,0,0);yield return null;yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(rect.eulerAngles.z,90))<1,"True arrow rotates up toward target");loop.regionModeEnabled=true;
            results.Add("LIMITS: level 15/25 combat fixtures set level and add native weapon damage directly, separately from the full real-XP/choice regression; these test scaling and shooting, not a claim of human-balanced difficulty.");
        }
    }
}
