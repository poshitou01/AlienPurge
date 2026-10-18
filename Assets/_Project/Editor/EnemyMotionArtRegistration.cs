using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Import metadata only: preserves PNG pixels, GUIDs, world PPU and collision roots.
public static class EnemyMotionArtRegistration
{
    public const string Path = "Assets/_Project/Art/Enemies/Animated/MotionRegistration.json";
    [Serializable] public sealed class Entry { public string path; public float x,y; }
    [Serializable] public sealed class Table { public Entry[] entries; }
    static Dictionary<string,Vector2> pivots;
    static Dictionary<string,Vector2> Pivots
    {
        get
        {
            if(pivots!=null) return pivots;
            pivots=new Dictionary<string,Vector2>(StringComparer.Ordinal);
            if(File.Exists(Path)) foreach(var entry in JsonUtility.FromJson<Table>(File.ReadAllText(Path)).entries)
                pivots[entry.path]=new Vector2(entry.x,entry.y);
            return pivots;
        }
    }
    public static void Apply(TextureImporter importer)
    {
        if(!Pivots.TryGetValue(importer.assetPath,out var pivot)) return;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=pivot;
        importer.SetTextureSettings(settings);
    }
    public static bool Valid(Sprite sprite)
    {
        Vector2 pivot=Pivots.TryGetValue(AssetDatabase.GetAssetPath(sprite),out var expected) ? expected : Vector2.one*.5f;
        return Vector2.Distance(sprite.pivot,Vector2.Scale(pivot,sprite.rect.size))<.001f;
    }
    public static void Reimport()
    {
        pivots=null;
        foreach(var entry in Pivots)
        {
            var importer=AssetImporter.GetAtPath(entry.Key) as TextureImporter;
            if(importer==null) throw new InvalidOperationException("Missing sprite importer: "+entry.Key);
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            if(settings.spriteAlignment==(int)SpriteAlignment.Custom && Vector2.Distance(settings.spritePivot,entry.Value)<.000001f) continue;
            Apply(importer); importer.SaveAndReimport();
        }
    }
}
