using System;
using System.Collections.Generic;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Installer
    {
        static void ConfigureImpact(Scene scene)
        {
            CombatImpactAudioAuthoring.EnsureClips();
            var loop = One<RegionLoopController>(scene);
            var impact = loop.GetComponent<CombatImpactFeedback>();
            if (impact == null) impact = loop.gameObject.AddComponent<CombatImpactFeedback>();
            if (impact.installationRevision >= 3) return;
            impact.worldCamera = All<Camera>(scene).First(c => c.CompareTag("MainCamera"));
            impact.effectMaterial = AssetDatabase.LoadAssetAtPath<Material>(DataFolder + "/PlayerAbilityFeedback.mat");
            string materialPath = DataFolder + "/CombatImpactFlash.mat";
            impact.flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (impact.flashMaterial == null)
            {
                var shader = Shader.Find("AlienPurge/Combat/ImpactSilhouette");
                if (shader == null) throw new InvalidOperationException("Impact silhouette shader missing");
                impact.flashMaterial = new Material(shader); AssetDatabase.CreateAsset(impact.flashMaterial, materialPath);
            }
            var clips = CombatImpactAudioAuthoring.Names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(CombatImpactAudioAuthoring.Folder + "/SFX_" + n + ".wav")).ToArray();
            impact.hitSound = clips[0]; impact.strongSound = clips[1]; impact.killSound = clips[2]; impact.heavyKillSound = clips[3];
            if (impact.installationRevision < 3)
            {
                if (Mathf.Approximately(impact.feedbackVolume, .7f)) impact.feedbackVolume = .82f;
                if (Mathf.Approximately(impact.cameraFeedback, .65f)) impact.cameraFeedback = .75f;
            }
            var status = One<PlayerStatusPanel>(scene);
            uiFont = status.GetComponentInChildren<TMP_Text>().font;
            var plate = Plate(status.transform.parent, "CombatConfirmationPanel", "Panel_Detail_9Slice", Vector2.zero, new Vector2(320, 42));
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = plate.rectTransform.pivot = new Vector2(.5f, 0);
            plate.rectTransform.anchoredPosition = new Vector2(0, 144);
            var oldLabel = status.transform.parent.Find("CombatConfirmation");
            if (oldLabel != null) oldLabel.SetParent(plate.transform, false);
            StatusIcon(plate.transform, "KillIcon", "Kills", new Vector2(-130, 0), 28);
            var label = Label(plate.transform, "CombatConfirmation", "", new Vector2(20, 0), new Vector2(248, 32), 21, Gold);
            impact.confirmation = label;
            impact.confirmationGroup = plate.GetComponent<CanvasGroup>();
            if (impact.confirmationGroup == null) impact.confirmationGroup = plate.gameObject.AddComponent<CanvasGroup>();
            impact.confirmationGroup.alpha = 0; impact.confirmationGroup.interactable = impact.confirmationGroup.blocksRaycasts = false;
            var markerRoot = UIRect(status.transform.parent, "CombatHitMarker", Vector2.zero, new Vector2(64, 64));
            impact.hitMarker = markerRoot.GetComponent<CombatHitMarker>();
            if (impact.hitMarker == null) impact.hitMarker = markerRoot.gameObject.AddComponent<CombatHitMarker>();
            impact.hitMarker.raycastTarget = false; impact.hitMarker.Clear();
            impact.installationRevision = 3; EditorUtility.SetDirty(impact);
        }
        static void ValidateImpact(Scene scene, List<string> errors)
        {
            var list = All<CombatImpactFeedback>(scene);
            if (list.Length != 1) { errors.Add("Expected one scene combat impact service"); return; }
            var impact = list[0];
            if (impact.worldCamera == null || impact.effectMaterial == null || impact.flashMaterial == null || impact.confirmation == null || impact.confirmationGroup == null || impact.hitMarker == null)
                errors.Add("Impact presentation references missing");
            if (impact.hitSound == null || impact.strongSound == null || impact.killSound == null || impact.heavyKillSound == null)
                errors.Add("Impact sound palette missing");
            if (impact.flashMaterial != null && ShaderUtil.ShaderHasError(impact.flashMaterial.shader)) errors.Add("Impact silhouette shader failed compilation");
            if (impact.confirmation != null && impact.confirmation.raycastTarget) errors.Add("Combat confirmation must not intercept aiming");
            if (impact.hitMarker != null && impact.hitMarker.raycastTarget) errors.Add("Hit marker must not intercept aiming");
        }
    }
}
