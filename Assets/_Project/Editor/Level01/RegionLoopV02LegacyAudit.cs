using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.Level01.Editor
{
    [InitializeOnLoad]
    public static class RegionLoopV02LegacyAudit
    {
        const string Key="RegionV02.LegacyAudit";
        const string Report=RegionLoopV02Installer.Reports+"/LegacyPlayMode.txt";
        static bool running;
        static double end;
        static RegionLoopV02LegacyAudit()
        {
            EditorApplication.playModeStateChanged+=Mode;
            EditorApplication.update+=Tick;
        }
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first.");
            RegionLoopV02Installer.ValidateProtected();
            SessionState.SetString(Key+".product",PlayerSettings.productName);
            PlayerSettings.productName="AlienPurge_RegionLoopV02_LegacyAudit";
            SessionState.SetInt(Key+".scene",0);SessionState.SetBool(Key,true);SessionState.SetBool(Key+".failed",false);
            File.WriteAllText(Report,"RUNNING: independent profile, old scene files are never saved.\n");
            Prepare();
        }
        static void Prepare()
        {
            var scene=EditorSceneManager.OpenScene(SessionState.GetInt(Key+".scene",0)==0?RegionLoopInstaller.Target:RegionLoopInstaller.Source);
            // Test-only in-memory references prevent dynamic TMP glyph writes into protected fonts.
            RegionLoopInstaller.CloneSceneFonts(scene,RegionLoopV02Installer.DataFolder+"/LegacyAuditFonts");
            EditorApplication.isPlaying=true;
        }
        static void Mode(PlayModeStateChange mode)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(mode==PlayModeStateChange.EnteredPlayMode){running=true;end=EditorApplication.timeSinceStartup+100;Application.runInBackground=true;}
            if(mode!=PlayModeStateChange.EnteredEditMode)return;
            bool failed=SessionState.GetBool(Key+".failed",false);
            if(!failed&&SessionState.GetInt(Key+".scene",0)==0)
            {SessionState.SetInt(Key+".scene",1);EditorApplication.delayCall+=Prepare;return;}
            PlayerSettings.productName=SessionState.GetString(Key+".product","AlienPurge");SessionState.SetBool(Key,false);
            // Discard only the audit's known, unsaved font reference substitutions.
            EditorSceneManager.OpenScene(RegionLoopV02Installer.Target);
            try{RegionLoopV02Installer.ValidateProtected();File.AppendAllText(Report,"PASS: all 806 protected assets/metas retain their V01 baseline hashes.\n");}
            catch(Exception e){failed=true;File.AppendAllText(Report,"FAIL: "+e+"\n");}
            File.WriteAllText(Report,File.ReadAllText(Report).Replace("RUNNING:",(failed?"FAIL:":"PASS:")));
            if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);
        }
        static void Tick()
        {
            if(!running||!Application.isPlaying)return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.isPaused)EditorApplication.isPaused=false;
                if(EditorApplication.timeSinceStartup>end)throw new TimeoutException("Legacy audit");
                if(GameManager.Instance==null)return;
                if(!Application.persistentDataPath.Contains("RegionLoopV02_LegacyAudit"))throw new InvalidOperationException("Profile not isolated");
                var health=UnityEngine.Object.FindObjectOfType<PlayerHealth>();if(health!=null)health.BeginTemporaryInvulnerability(1000);
                var loop=UnityEngine.Object.FindObjectOfType<RegionLoopController>();
                if(SessionState.GetInt(Key+".scene",0)==0)
                {
                    Time.timeScale=30;if(GameManager.Instance.SurvivalTime<380)return;
                    if(loop==null||loop.v02Enabled||loop.State("ColonyEntry").patrolSpent!=loop.definitions[0].patrolBudget||
                        loop.rewards.modules.eventDrivenEvolution||loop.rewards.modules.UnlockedModuleCount!=0||
                        UnityEngine.Object.FindObjectOfType<PlanetStormController>().CurrentPhase!=StormPhase.Calm||
                        UnityEngine.Object.FindObjectOfType<ToxicFogRouteController>().HasStarted)
                        throw new InvalidOperationException("V01 mode changed");
                    File.AppendAllText(Report,"PASS: V01 at 380 seconds retains finite patrol budget, dormant storm/fog and event-only module rewards.\n");
                }
                else
                {
                    if(loop!=null||UnityEngine.Object.FindObjectOfType<EnemySpawner>().regionDriven||WeaponModuleSelectionManager.Instance.regionDriven||WeaponModuleSelectionManager.Instance.eventDrivenEvolution)
                        throw new InvalidOperationException("V02 opt-in leaked into original scene");
                    if(!WeaponModuleSelectionManager.IsChoosingModule){Time.timeScale=30;return;}
                    if(GameManager.Instance.SurvivalTime<44||GameManager.Instance.SurvivalTime>55)throw new InvalidOperationException("Original timed reward not at 45 seconds");
                    File.AppendAllText(Report,"PASS: original FullWorld retains native 45-second first-module choice with V02 flags off. Boss scene/assets protected; Boss combat was not modified or simulated.\n");
                }
                running=false;Time.timeScale=1;EditorApplication.isPlaying=false;
            }
            catch(Exception e)
            {running=false;SessionState.SetBool(Key+".failed",true);File.AppendAllText(Report,"FAIL: "+e+"\n");Time.timeScale=1;EditorApplication.isPlaying=false;}
        }
    }
}
