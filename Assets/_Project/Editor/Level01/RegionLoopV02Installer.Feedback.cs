using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using AlienPurge.Level01ConnectedV01;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Installer
    {
        static void PrepareFeedbackDefinitions(RegionLoopController loop)
        {
            var definitions = new List<RegionDefinition>();
            for (int i = 0; i < Ids.Length; i++)
            {
                string path = DataFolder + "/" + Ids[i] + ".asset";
                var d = loop.definitions.FirstOrDefault(x => x != null && x.regionId == Ids[i] && AssetDatabase.GetAssetPath(x).StartsWith(DataFolder + "/"));
                if (d == null) d = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
                bool created = d == null;
                if (created)
                {
                    d = Object.Instantiate(loop.definitions[Mathf.Min(i, loop.definitions.Length - 1)]);
                    d.name = Ids[i]; AssetDatabase.CreateAsset(d, path);
                }
                if (created || loop.feedbackRevision < 1)
                {
                    float start = d.v02 != null ? d.v02.fogStartDistance : 0, end = d.v02 != null ? d.v02.fogEndDistance : 0;
                    ConfigureFeedbackDefinition(d, i);
                    if (i < 3) { d.v02.fogStartDistance = start; d.v02.fogEndDistance = end; }
                    EditorUtility.SetDirty(d); AssetDatabase.SaveAssetIfDirty(d);
                }
                definitions.Add(d);
            }
            loop.definitions = definitions.ToArray();
        }
        static void ConfigureFeedbackDefinition(RegionDefinition d, int i)
        {
            d.regionId = Ids[i]; d.missionId = Missions[i]; d.encounterId = Ids[i] + "-garrison";
            d.exitGateId = i == 5 ? "G7" : "G" + (i + 1); d.prerequisite = i == 0 ? "" : Missions[i - 1];
            d.enemies = new[] {
                new RegionEnemyProfile { archetypeId="Swarmer", type=EnemyType.Normal, health=4+Mathf.Min(i,3), experience=i==0?1:2, speed=2.15f+.12f*Mathf.Min(i,3) },
                new RegionEnemyProfile { archetypeId="Rusher", type=EnemyType.Fast, health=6+Mathf.Min(i,3), experience=3, speed=3.4f, special=true, simultaneousLimit=3 },
                new RegionEnemyProfile { archetypeId="Tank", type=EnemyType.Heavy, health=18+Mathf.Min(i,4), experience=5, speed=1.45f, damage=2, clearance=1.1f, special=true, simultaneousLimit=1 }
            }.Take(i == 0 ? 1 : i == 1 || i == 5 ? 2 : 3).ToArray();
            int count = new[] {12,16,18,18,20,10}[i], keys = new[] {4,4,5,5,6,4}[i];
            var garrison = new List<RegionSpawnGroup> { G(EnemyType.Normal, keys, true) };
            int fast = i == 0 ? 0 : 2, heavy = i >= 2 && i < 5 ? 1 : 0;
            garrison.Add(G(EnemyType.Normal, count - keys - fast - heavy));
            if (fast > 0) garrison.Add(G(EnemyType.Fast, fast));
            if (heavy > 0) garrison.Add(G(EnemyType.Heavy, heavy));
            d.residents = garrison.ToArray(); d.aliveLimit = count + 10; d.specialLimit = 4;
            d.patrolBudget = 0; d.spawnInterval = .32f; d.warningSeconds = .35f;
            d.encounterExperience = 2 + i; d.missionExperience = new[] {2,6,8,10,12,0}[i];
            d.moduleSlot = i == 1 || i == 2 ? i : 0;
            d.minimumPlayerDistance = 9; d.perceptionRadius = 16; d.leashRadius = 90;
            d.v02 = new RegionV02Settings {
                extractionRegion=i==5, ambientTarget=new[]{12,16,18,18,20,12}[i], missionCapacityReserve=8,
                environmentInterval= i==0?5:4, searchSeconds=8, preloadDistance=42, pressureLowWatermark=3,
                explorationSeconds=new[]{115,140,165,150,150,180}[i], fogWarningSeconds=25,
                fogAdvanceSeconds=70+10*Mathf.Min(i,3), oneTimeExitBuffer=15,
                stormAfterEntry=28+4*Mathf.Min(i,3), stormWarningMinimum=12, stormActiveSeconds=20, safeRadius=4.5f
            };
            d.v02.environmentSquads = i == 0 ? new[] { W("advance",G(EnemyType.Normal,4)), W("flank",G(EnemyType.Normal,5)) } :
                i == 1 || i == 5 ? new[] { W("advance",G(EnemyType.Normal,4)), W("rush",G(EnemyType.Normal,3),G(EnemyType.Fast,1)), W("flank",G(EnemyType.Normal,5)) } :
                new[] { W("advance",G(EnemyType.Normal,4),G(EnemyType.Fast,1)), W("armor",G(EnemyType.Normal,3),G(EnemyType.Heavy,1)), W("flank",G(EnemyType.Normal,5)) };
            var ordinary = new RegionOptionalDefinition { id="beacon-defense",experience=12+4*i,defendSeconds=24+3*Mathf.Min(i,2),deadlineSeconds=65+5*Mathf.Min(i,2),acceptSeconds=110,
                waves=new[]{W("defense-1",G(EnemyType.Normal,4)),W("defense-2",G(EnemyType.Normal,3),i==0?G(EnemyType.Normal,1):G(EnemyType.Fast,1))} };
            ordinary.waves[1].delay=10;
            var high = new RegionOptionalDefinition { id="high-risk-defense", highRisk=true, triggersStorm=true, radius=7, acceptSeconds=125, defendSeconds=32, deadlineSeconds=75,
                claimAndExitSeconds=35, experience=26, maxAttempts=2,
                waves=new[]{W("risk-1",G(EnemyType.Normal,4),G(EnemyType.Fast,1,true)),W("risk-2",G(EnemyType.Normal,4),G(EnemyType.Fast,1,true))} };
            high.waves[1].delay=12;
            d.v02.optionalTasks=i==5?Array.Empty<RegionOptionalDefinition>():i==1?new[]{ordinary,high}:new[]{ordinary};
        }
        static void PrepareFrontierTerminals(Scene scene, RegionLoopController loop)
        {
            var terminals = loop.terminals.ToList();
            var legacy = All<L01Terminal>(scene);
            var template = legacy.FirstOrDefault(t => t.completesFlag == "Research");
            // Instantiate the existing terminal art before converting its original component.
            L01Terminal extractionTemplate = null;
            if (loop.Terminal("Extraction") == null && template != null)
                extractionTemplate = Object.Instantiate(template.gameObject, loop.transform).GetComponent<L01Terminal>();
            for (int i=3;i<Ids.Length;i++)
            {
                if (loop.Terminal(Ids[i]) != null) continue;
                var old = i==5 ? extractionTemplate : legacy.FirstOrDefault(t=>t.completesFlag==Missions[i]);
                if (old == null) throw new InvalidOperationException("Existing terminal missing for " + Ids[i]);
                var obj = old.gameObject; var label = old.status;
                loop.dormantBehaviours = loop.dormantBehaviours.Where(b=>b!=old).ToArray();
                Object.DestroyImmediate(old);
                var terminal = obj.AddComponent<RegionTerminal>();
                terminal.controller=loop; terminal.stableId="device/"+Ids[i]; terminal.regionId=Ids[i]; terminal.status=label;
                obj.SetActive(true); obj.name="Device_"+Ids[i];
                if (i==5) obj.transform.position=loop.world.World(loop.world.Region("Extraction").center.V);
                terminals.Add(terminal); loop.terminals=terminals.ToArray();
                terminal.transform.position=FindSpace(loop,loop.definitions[i],terminal.transform.position,.8f,Array.Empty<Vector3>());
                if(label!=null) { label.gameObject.SetActive(true); loop.dormantObjects=loop.dormantObjects.Where(o=>o!=label.gameObject).ToArray(); }
            }
        }
        static void ClearExtractionIngress(RegionLoopController loop)
        {
            if(loop.feedbackRevision<3)
            {
                var choke=loop.world.World(new Vector2(35,-56.48438f));
                var cases=Physics2D.OverlapCircleAll(choke,1).Where(c=>c.name=="Case_Footprint")
                    .Select(c=>c.GetComponentInParent<LootContainer>()).Where(c=>c!=null).Distinct().ToArray();
                var definition=loop.definitions.First(d=>d.regionId=="Extraction");
                foreach(var cache in cases)
                    cache.transform.position=FindSpace(loop,definition,loop.world.World(new Vector2(39,-61)),1.2f,
                        loop.anchors.Where(a=>a.residentSlot&&a.regionId=="Extraction").Select(a=>a.transform.position));
            }
            // The new terminal footprint and moved scene-instance cache must be included before placement validation.
            Physics2D.SyncTransforms();loop.world.navigation.astar.Scan();
        }
        static void PrepareTerminalTasks(Scene scene, RegionLoopController loop)
        {
            foreach (var site in loop.optionalSites)
            {
                var host = loop.Terminal(site.definition.regionId);
                site.transform.SetParent(host.transform); site.transform.localPosition=Vector3.zero;
                site.name="TaskLogic_"+site.taskDefinitionId;
                if(site.stormAnchor==null)
                    site.stormAnchor=NamedPoint(loop.transform,"TaskStorm_"+site.definition.regionId+"_"+site.taskDefinitionId,FindSafeDisk(loop,site.definition,site.transform.position));
                if(site.Config.triggersStorm&&Vector2.Distance(site.StormPosition,site.transform.position)>site.Config.radius+site.definition.v02.safeRadius-2)
                    throw new InvalidOperationException("High-risk safety disk has no usable defense overlap: "+site.definition.regionId+" distance="+Vector2.Distance(site.StormPosition,site.transform.position));
                foreach(var collider in site.GetComponentsInChildren<Collider2D>(true)) collider.enabled=false;
                foreach(var renderer in site.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
                if(site.rewardContainer!=null && loop.feedbackRevision<1)
                    site.rewardContainer.transform.position=FindSpace(loop,site.definition,host.transform.position+Vector3.right*3,.8f,Array.Empty<Vector3>());
            }
        }
        static void PrepareStormMask(RegionLoopController loop)
        {
            var mask=loop.GetComponentInChildren<RegionStormMask>(true);
            if(mask==null) { mask=new GameObject("StormAreaMask").AddComponent<RegionStormMask>(); mask.transform.SetParent(loop.transform,false); }
            mask.controller=loop;
            string path=DataFolder+"/MAT_StormAreaMask.asset";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("AlienPurge/Level01/RegionStormMask")); AssetDatabase.CreateAsset(material,path); }
            mask.material=material;
            var renderer=mask.GetComponent<MeshRenderer>(); renderer.enabled=false; renderer.sharedMaterial=material;
        }
        static void PrepareChineseFonts(Scene scene)
        {
            string folder=DataFolder+"/Fonts";
            var fonts=AssetDatabase.FindAssets("t:TMP_FontAsset",new[]{folder}).Select(g=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).Where(f=>f!=null).ToArray();
            var chinese=fonts.FirstOrDefault(f=>f.faceInfo.familyName.Contains("YaHei"));
            if(chinese==null)throw new InvalidOperationException("Private Chinese font missing");
            UnityEngine.TextCore.LowLevel.FontEngine.InitializeFontEngine();
            Set(chinese,"m_SourceFontFile_EditorRef",AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Fonts/MSYH.TTC"));
            chinese.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            var faceResult=UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(chinese.sourceFontFile,chinese.faceInfo.pointSize);
            if(faceResult!=UnityEngine.TextCore.LowLevel.FontEngineError.Success)
                throw new InvalidOperationException("Chinese source font load failed: "+faceResult+" source="+(chinese.sourceFontFile==null?"null":chinese.sourceFontFile.name));
            string characters=string.Concat(Directory.GetFiles("Assets/_Project/Scripts/Level01/RegionLoop","*.cs").Select(File.ReadAllText))+
                File.ReadAllText("Assets/_Project/Editor/Level01/RegionLoopV02Installer.Polish.cs");
            characters=new string(characters.Where(c=>c>127).Distinct().ToArray());
            // TMP 3.0.7 returns false when the entire requested set is already present.
            string pending=new string(characters.Where(c=>!chinese.HasCharacter(c)).ToArray());
            if(pending.Length>0)chinese.TryAddCharacters(pending,out _);
            string missing=new string(characters.Where(c=>!chinese.HasCharacter(c)).ToArray());
            if(missing.Length>0)throw new InvalidOperationException("Chinese glyphs unavailable: "+missing);
            EditorUtility.SetDirty(chinese); AssetDatabase.SaveAssetIfDirty(chinese);
            foreach(var font in fonts.Where(f=>f!=chinese))
            {
                if(font.fallbackFontAssetTable==null)font.fallbackFontAssetTable=new List<TMP_FontAsset>();
                if(!font.fallbackFontAssetTable.Contains(chinese)) { font.fallbackFontAssetTable.Add(chinese); EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); }
            }
            // Keep the original font family/size/layout; Chinese glyphs resolve through private fallback assets.
        }
        static void ExtendFrontierFog(Transform root, RegionLoopController loop)
        {
            var points=new[]{
                new Vector4(-100.75781f,12.1f,42,0),new Vector4(-100.75781f,68,42,0),
                new Vector4(-77,44.8125f,0,2.5f),new Vector4(-57,44.8125f,0,2.5f),
                new Vector4(-57,66.78125f,2.5f,0),new Vector4(-44,66.78125f,0,2.5f),
                new Vector4(-44,67,0,28),new Vector4(40,67,0,28),new Vector4(16.53906f,47,3.5f,0),
                new Vector4(16.53906f,33,3.5f,0),new Vector4(48,33,0,3.5f),new Vector4(48,26,3.5f,0)
            };
            for(int i=0;i<points.Length;i++)
            {
                string name="Section_"+(15+i).ToString("D2");
                if(root.Find(name)!=null && (loop.feedbackRevision>=2 || i<8))continue;
                var v=points[i];var p=NamedPoint(root,name,loop.world.World(new Vector2(v.x,v.y)));
                p.position=loop.world.World(new Vector2(v.x,v.y));
                var section=p.GetComponent<ToxicFogCrossSection>()??p.gameObject.AddComponent<ToxicFogCrossSection>();
                var left=NamedPoint(p,"FogLeft",p.position-new Vector3(v.z,v.w));var right=NamedPoint(p,"FogRight",p.position+new Vector3(v.z,v.w));
                left.position=p.position-new Vector3(v.z,v.w);right.position=p.position+new Vector3(v.z,v.w);
                if(i==0||i==1) { left.position=loop.world.World(new Vector2(-152.125f,v.y));right.position=loop.world.World(new Vector2(-68.125f,v.y)); }
                Set(section,"leftBoundary",left);Set(section,"rightBoundary",right);
            }
            foreach(int start in new[]{18,19,23,24,25})
            {
                string name="Section_"+start+"A";if(root.Find(name)!=null&&loop.feedbackRevision>=2)continue;
                var a=root.Find("Section_"+start);var b=root.Find("Section_"+(start+1));
                var p=NamedPoint(root,name,Vector3.Lerp(a.position,b.position,.01f));p.position=Vector3.Lerp(a.position,b.position,.01f);
                var section=p.GetComponent<ToxicFogCrossSection>()??p.gameObject.AddComponent<ToxicFogCrossSection>();
                var target=b.GetComponent<ToxicFogCrossSection>();
                var left=NamedPoint(p,"FogLeft",p.position+(Vector3)(target.LeftPosition-target.Center));left.position=p.position+(Vector3)(target.LeftPosition-target.Center);
                var right=NamedPoint(p,"FogRight",p.position+(Vector3)(target.RightPosition-target.Center));right.position=p.position+(Vector3)(target.RightPosition-target.Center);
                Set(section,"leftBoundary",left);Set(section,"rightBoundary",right);
            }
            if(loop.feedbackRevision>=2)return;
            float distance=0;Vector3 previous=Vector3.zero;
            foreach(var section in root.GetComponentsInChildren<ToxicFogCrossSection>(true).OrderBy(s=>s.name,StringComparer.Ordinal))
            {
                if(section.name!="Section_00")distance+=Vector3.Distance(previous,section.transform.position);
                previous=section.transform.position;
                int stage=section.name=="Section_03"?0:section.name=="Section_10"?1:section.name=="Section_14"?2:section.name=="Section_20"?3:section.name=="Section_26"?4:-1;
                if(stage<0)continue;
                loop.definitions[stage].v02.fogEndDistance=distance;
                loop.definitions[stage+1].v02.fogStartDistance=distance;
                if(stage==4)loop.definitions[5].v02.fogEndDistance=distance;
            }
            foreach(var d in loop.definitions) { EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d); }
        }
    }
}
