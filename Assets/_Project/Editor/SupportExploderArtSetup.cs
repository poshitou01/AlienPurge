using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The existing asset hierarchy and Sprite-only Animator pipeline are retained.
public static class SupportExploderArtSetup
{
    public const string Art = "Assets/_Project/Art/Enemies/Animated/";
    static readonly string[] Directions = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    struct Spec
    {
        public string role, source;
        public int[] frames;
        public float seconds, fps;
        public bool loop;
        public Spec(string role, string source, int[] frames, float seconds, float fps, bool loop)
        { this.role = role; this.source = source; this.frames = frames; this.seconds = seconds; this.fps = fps; this.loop = loop; }
    }
    static Spec[] Specs(string enemy)
    {
        int[] four = { 1, 2, 3, 4 }, eight = { 1, 2, 3, 4, 5, 6, 7, 8 };
        var common = new[] {
            new Spec("Idle", "Idle", four, 1, 4, true),
            new Spec("Move", "Move", eight, 8f/12, 12, true),
            new Spec("Death", "Death", four, SupportExploderAnimation.DeathSeconds, 8, false)
        };
        return common.Concat(enemy == "Support" ? new[] {
            new Spec("SupportStart", "SupportStart", four, 1f/3, 12, false),
            new Spec("SupportLoop", "SupportStart", new[] { 4, 3, 4, 3, 2, 3 }, 1, 6, true),
            new Spec("SupportMove", "SupportMove", eight, 8f/12, 12, true),
            new Spec("SupportEnd", "SupportStart", new[] { 4, 3, 2, 1 }, 1f/3, 12, false)
        } : new[] {
            new Spec("Warning", "Warning", four, 1.5f, 12, false),
            new Spec("Burst", "Burst", four, SupportExploderAnimation.BurstSeconds, 8, false)
        }).ToArray();
    }
    public static Sprite Frame(string enemy, string state, string direction, int frame)
    {
        string path = Art + enemy + "/" + state + "/" + direction + "/" + enemy + "_" + state + "_" + direction + "_" + frame.ToString("00") + ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Missing authored sprite: " + path);
        return sprite;
    }
    public static AnimatorController MakeController(string enemy)
    {
        string folder = "Assets/_Project/Animations/" + enemy, path = folder + "/" + enemy + "_Animator.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (!controller.parameters.Any(p => p.name == "Direction")) controller.AddParameter("Direction", AnimatorControllerParameterType.Int);
        var machine = controller.layers[0].stateMachine;
        foreach (var spec in Specs(enemy)) foreach (string direction in Directions)
        {
            string name = enemy + "_" + spec.role + "_" + direction;
            string clipPath = folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.name = name; clip.frameRate = spec.fps;
            var keys = new ObjectReferenceKeyframe[spec.frames.Length + 1];
            for (int i = 0; i < spec.frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i * spec.seconds / spec.frames.Length, value = Frame(enemy, spec.source, direction, spec.frames[i]) };
            keys[keys.Length - 1] = new ObjectReferenceKeyframe { time = spec.seconds, value = keys[spec.loop ? 0 : keys.Length - 2].value };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = spec.loop; settings.stopTime = spec.seconds;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            string stateName = spec.role + "_" + direction;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName) ?? machine.AddState(stateName);
            state.motion = clip; state.writeDefaultValues = false;
            if (stateName == "Idle_DR") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller); return controller;
    }
    public static void PrepareImports()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string enemy in new[] { "Support", "Exploder" })
        foreach (string path in Directory.GetFiles(Art + enemy, "*.png", SearchOption.AllDirectories))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            if (importer.textureType == TextureImporterType.Sprite && importer.spritePixelsPerUnit == 256 && importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize == 512 && !importer.mipmapEnabled) continue;
            ApplyImport(importer); importer.SaveAndReimport();
        }
    }
    public static void ApplyImport(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256; importer.spritePivot = new Vector2(.5f, .5f);
        importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.sRGBTexture = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 512;
        importer.npotScale = TextureImporterNPOTScale.None; importer.isReadable = false;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteExtrude = 1;
        importer.SetTextureSettings(settings);
    }
    public static void Validate(GameObject prefab)
    {
        string enemy = prefab.GetComponent<PooledEnemy>().Type == EnemyType.Support ? "Support" : "Exploder";
        Require(!prefab.GetComponent<EnemyAbilityPresentation>().TemporaryArt, "Legacy placeholder overlay disabled");
        Require(prefab.GetComponent<SupportExploderAnimation>() != null, "State-driven authored Animator");
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController;
        Require(controller.animationClips.Length == (enemy == "Support" ? 56 : 40), "Complete directional state clips");
        foreach (var clip in controller.animationClips)
        {
            Require(!clip.name.Contains("TEMP") && AnimationUtility.GetAnimationEvents(clip).Length == 0, "Authored clips without gameplay Animation Events");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Require(bindings.Length == 1 && bindings[0].path == "" && bindings[0].type == typeof(SpriteRenderer), "Sprite-only curves retain collider/root transform");
            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]))
            {
                var sprite = key.value as Sprite;
                Require(sprite != null && AssetDatabase.GetAssetPath(sprite).StartsWith(Art + enemy + "/", StringComparison.Ordinal), "Own enemy sprite, never borrowed Swarmer");
                Require(sprite.pixelsPerUnit == 256 && sprite.rect.size == new Vector2(512, 512) && sprite.pivot == new Vector2(256, 256), "512 canvas / PPU 256 / center pivot");
            }
        }
        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0, "No missing scripts");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

public sealed class SupportExploderArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.StartsWith(SupportExploderArtSetup.Art + "Support/", StringComparison.Ordinal) || assetPath.StartsWith(SupportExploderArtSetup.Art + "Exploder/", StringComparison.Ordinal))
            SupportExploderArtSetup.ApplyImport((TextureImporter)assetImporter);
    }
}
