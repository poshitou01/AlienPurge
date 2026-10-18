using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Installer
    {
        public const string DeskMenu="Assets/_Project/Scenes/MainMenu_DeskV1.unity";
        const string OldDeskRoute="  gameSceneName: Day01_TestGround";
        const string NewDeskRoute="  gameSceneName: Level01_FullWorld_GameplayV02";
        static void ConfigureAbilities(Scene scene)
        {
            Set(One<GameManager>(scene),"mainMenuSceneName","MainMenu_DeskV1");
            Set(One<PauseMenuController>(scene),"mainMenuSceneName","MainMenu_DeskV1");
            var dash=One<PlayerDash>(scene);var jump=One<PlayerJetJump>(scene);var pulse=One<PlayerPulseSkill>(scene);
            var feel=dash.GetComponent<PlayerAbilityFeel>()??dash.gameObject.AddComponent<PlayerAbilityFeel>();
            if(feel.installationRevision>=2)return;
            Set(dash,"dashSpeed",21f);Set(dash,"dashDuration",.16f);Set(dash,"dashCooldown",.85f);
            // The opt-in protection covers the actual action, without cancelling another system's timed invulnerability.
            Set(dash,"grantInvulnerability",false);Set(dash,"stretchAmount",.2f);Set(dash,"compressionAmount",.1f);
            Set(jump,"jumpDuration",.58f);Set(jump,"jumpHeight",.85f);Set(jump,"jumpCooldown",1.35f);
            Set(jump,"playLandingCameraShake",false);Set(jump,"shadowScaleMultiplier",.65f);Set(jump,"shadowAlphaMultiplier",.5f);
            Set(pulse,"pulseRadius",3.8f);Set(pulse,"pulseCooldown",5.5f);Set(pulse,"castDuration",.12f);
            Set(pulse,"pulseScaleMultiplier",1.08f);Set(pulse,"pulseVisual",null);
            feel.dashSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Player/SFX_PlayerDash.wav");
            feel.jumpSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Player/SFX_PlayerJetJump.wav");
            feel.pulseSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Player/SFX_PlayerPulse.wav");
            // Explicit scene reference retains the shader in player builds.
            string materialPath=DataFolder+"/PlayerAbilityFeedback.mat";
            feel.effectMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(feel.effectMaterial==null)
            {
                feel.effectMaterial=new Material(Shader.Find("Sprites/Default")){name="PlayerAbilityFeedback"};
                AssetDatabase.CreateAsset(feel.effectMaterial,materialPath);
            }
            var body=dash.GetComponent<Rigidbody2D>();body.interpolation=RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            feel.installationRevision=2;EditorUtility.SetDirty(feel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }
        static void ConfigureDeskRoute()
        {
            // User explicitly selected this menu. Change only its existing StartGame destination;
            // retain every scene object, art reference and button event byte for byte.
            var encoding=Encoding.GetEncoding(28591);var bytes=File.ReadAllBytes(DeskMenu);
            string text=encoding.GetString(bytes);
            if(text.Contains(OldDeskRoute))
            {
                if(text.Split(new[]{OldDeskRoute},StringSplitOptions.None).Length!=2)throw new InvalidOperationException("Ambiguous Desk menu route.");
                File.WriteAllBytes(DeskMenu,encoding.GetBytes(text.Replace(OldDeskRoute,NewDeskRoute)));
                AssetDatabase.ImportAsset(DeskMenu);
            }
            else if(!text.Contains(NewDeskRoute))throw new InvalidOperationException("Desk menu StartGame route not recognized.");
            var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=DeskMenu).ToList();
            scenes.Insert(0,new EditorBuildSettingsScene(DeskMenu,true));EditorBuildSettings.scenes=scenes.ToArray();
        }
        static bool ProtectedMatch(Entry entry)
        {
            if(!File.Exists(entry.path))return false;
            if(RegionLoopInstaller.HashGitBlob(entry.path)==entry.blob)return true;
            if(entry.path!=DeskMenu)return false;
            // Exact, documented exception: normalize ONLY the authorized menu destination.
            // All other original scene bytes must still match the original immutable baseline.
            var encoding=Encoding.GetEncoding(28591);
            string text=encoding.GetString(File.ReadAllBytes(entry.path)).Replace(NewDeskRoute,OldDeskRoute).Replace("\r\n","\n");
            byte[] bytes=encoding.GetBytes(text),header=Encoding.ASCII.GetBytes("blob "+bytes.Length+"\0");
            using(var hash=SHA1.Create())
            {
                hash.TransformBlock(header,0,header.Length,header,0);hash.TransformFinalBlock(bytes,0,bytes.Length);
                return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant()==entry.blob;
            }
        }
        static void ValidateDeskRoute()
        {
            var scenes=EditorBuildSettings.scenes;
            if(scenes.Length==0||scenes[0].path!=DeskMenu||!scenes[0].enabled||!File.ReadAllText(DeskMenu).Contains(NewDeskRoute))
                throw new InvalidOperationException("DeskV1 must be the build entry and its StartGame must target V02.");
        }
        static void ValidateAbilities(Scene scene,List<string> errors)
        {
            var feel=All<PlayerAbilityFeel>(scene);
            if(feel.Length!=1||feel[0].installationRevision<1||feel[0].dashSound==null||feel[0].jumpSound==null||feel[0].pulseSound==null||feel[0].effectMaterial==null)
                errors.Add("Responsive abilities require one configured scene-local controller with existing audio/material references.");
            foreach(var component in new UnityEngine.Object[]{One<GameManager>(scene),One<PauseMenuController>(scene)})
                if(new SerializedObject(component).FindProperty("mainMenuSceneName").stringValue!="MainMenu_DeskV1")errors.Add("Incorrect return menu: "+component.name);
        }
    }
}
