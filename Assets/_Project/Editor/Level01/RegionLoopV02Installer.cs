using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using AlienPurge.Level01ConnectedV01;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static class RegionLoopV02Installer
    {
        public const string Source = RegionLoopInstaller.Target;
        public const string Target = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld_GameplayV02.unity";
        public const string DataFolder = "Assets/_Project/Data/Level01/RegionLoopV02";
        public const string Reports = "Docs/Level01/V02";
        public const string Owner = "AlienPurge.Level01.RegionLoop.V02";
        static readonly string[] Ids = { "ColonyEntry", "CargoYard", "PowerMaintenance" };
        static readonly Vector2[] Exits = { new Vector2(-213.1875f,-18), new Vector2(-171,38.35938f), new Vector2(-100.75781f,-17) };
        static T[] All<T>(Scene scene) where T : Component => RegionLoopInstaller.All<T>(scene);
        static T One<T>(Scene scene) where T : Component
        {
            var found = All<T>(scene);
            if (found.Length != 1) throw new InvalidOperationException(typeof(T).Name + " expected once, found " + found.Length);
            return found[0];
        }
        internal static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(target.name + " missing " + field);
            if (value is bool b) p.boolValue = b;
            else if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
            else if (value is string s) p.stringValue = s;
            else p.objectReferenceValue = value as Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetArray(Object target, string field, IEnumerable<Object> values)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(target.name + " missing " + field);
            var items = values.ToArray(); p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static T Reference<T>(Object target, string field) where T : Object => new SerializedObject(target).FindProperty(field).objectReferenceValue as T;

        [MenuItem("AlienPurge/Gameplay/Level01/Install Region Loop V02")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save pending scene edits first: " + SceneManager.GetSceneAt(i).path);
            ValidateProtected();
            if (!File.Exists(Reports + "/NativeHudStyle.txt"))
            {
                var sourceScene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
                File.WriteAllText(Reports + "/NativeHudStyle.txt", HudStyle(sourceScene));
            }
            bool created = !File.Exists(Target);
            if (created && !AssetDatabase.CopyAsset(Source, Target)) throw new IOException("Could not copy V01.");
            var scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
            var loop = One<RegionLoopController>(scene);
            if (!created && !string.IsNullOrEmpty(loop.v02InstallationId) && loop.v02InstallationId != Owner)
                throw new InvalidOperationException("Unknown V02 scene ownership. Existing scene retained.");
            if (!created && string.IsNullOrEmpty(loop.v02InstallationId) && loop.installationId != RegionLoopController.OwnerId)
                throw new InvalidOperationException("Target does not contain the expected V01 foundation.");
            bool firstInstall = string.IsNullOrEmpty(loop.v02InstallationId);
            loop.v02InstallationId = Owner; loop.v02Enabled = true; loop.regionModeEnabled = true;
            loop.installedVersion = 2;
            loop.gameObject.name = "Level01_RegionLoop_V02";
            if (firstInstall) loop.globalAliveLimit = 28;
            Directory.CreateDirectory(DataFolder); Directory.CreateDirectory(Reports); AssetDatabase.Refresh();
            for (int i = 0; i < 3; i++)
            {
                string path = DataFolder + "/" + Ids[i] + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
                if (definition == null)
                {
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(loop.definitions[i]), path)) throw new IOException(path);
                    definition = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
                    ConfigureDefinition(definition, i); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                }
                // An already isolated, hand-assigned definition is authoritative.
                if (AssetDatabase.GetAssetPath(loop.definitions[i]).StartsWith(DataFolder + "/")) definition = loop.definitions[i];
                loop.definitions[i] = definition;
            }
            loop.storm = One<PlanetStormController>(scene); loop.fog = One<ToxicFogRouteController>(scene);
            loop.fogArea = One<ToxicFogAreaController>(scene); loop.extraction = One<ExtractionController>(scene);
            loop.storm.regionDriven = loop.fog.regionDriven = loop.extraction.regionDriven = true;
            loop.missionView.regionDriven = true; loop.rewards.modules.regionDriven = true;
            loop.rewards.modules.eventDrivenEvolution = true;
            foreach (var reveal in All<WeaponEvolutionRevealUI>(scene)) reveal.deferRegionFeedback = true;
            foreach (var spawner in All<EnemySpawner>(scene)) { spawner.regionDriven = true; Set(spawner, "enableAutomaticSpawning", false); }
            RestoreNativePresentation(scene, loop);
            loop.world.navigation.ConfigureGrid(); Physics2D.SyncTransforms(); loop.world.navigation.astar.Scan();
            var anchors = new List<RegionAnchor>(loop.anchors);
            var candidates = new List<Transform>(); var exits = new List<Transform>(); var sites = new List<RegionTaskSite>();
            var loot = CreateLoot();
            for (int i = 0; i < 3; i++)
            {
                var d = loop.definitions[i]; var region = loop.world.Region(d.regionId);
                var terminal = loop.Terminal(d.regionId);
                terminal.SetInteractionPrompt("OPEN " + d.exitGateId);
                var exit = loop.exitApproaches != null && loop.exitApproaches.Length > i && loop.exitApproaches[i] != null ? loop.exitApproaches[i] :
                    NamedPoint(loop.transform, "ExitApproach_" + d.regionId, loop.world.World(Exits[i])); exits.Add(exit);
                int count = d.residents.Sum(g => g.count);
                for (int n = 0; n < count; n++)
                {
                    string id = "resident/" + d.regionId + "/" + n;
                    var anchor = All<RegionAnchor>(scene).SingleOrDefault(a => a.stableId == id);
                    if (anchor == null)
                    {
                        float angle = n * Mathf.PI * 2 / count;
                        var desired = loop.world.World(region.center.V) + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (n % 2 == 0 ? 13 : 18);
                        var pos = FindSpace(loop, d, desired, 1.15f, anchors.Where(a => a.residentSlot).Select(a => a.transform.position));
                        anchor = new GameObject("Garrison_" + d.regionId + "_" + n).AddComponent<RegionAnchor>();
                        anchor.transform.SetParent(loop.transform); anchor.transform.position = pos;
                        anchor.stableId = id; anchor.regionId = d.regionId; anchor.residentSlot = true;
                    }
                    if (!anchors.Contains(anchor)) anchors.Add(anchor);
                }
                for (int n = 0; n < d.v02.optionalTasks.Length; n++)
                {
                    var task = d.v02.optionalTasks[n]; string id = "optional/" + d.regionId + "/" + task.id;
                    var site = All<RegionTaskSite>(scene).SingleOrDefault(s => s.stableId == id);
                    if (site == null)
                    {
                        var prefab = Reference<GameObject>(loop.missionView, "beaconObjectivePrefab");
                        if (prefab == null) throw new InvalidOperationException("Native Beacon prefab missing.");
                        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, loop.transform);
                        obj.name = (task.highRisk ? "HighRisk_" : "OptionalDefense_") + d.regionId;
                        var desired = loop.world.World(region.center.V) + new Vector3(n == 0 ? -7 : 10, n == 0 ? 8 : -8);
                        obj.transform.position = FindSpace(loop, d, desired, .8f, new[] { terminal.transform.position });
                        site = obj.AddComponent<RegionTaskSite>(); site.stableId = id;
                        site.beacon = obj.GetComponent<BeaconMissionObjective>();
                    }
                    site.controller = loop; site.definition = d; site.taskDefinitionId = task.id;
                    site.beacon.regionSite = site; site.beacon.enabled = true;
                    if (task.highRisk && site.rewardContainer == null)
                    {
                        var prefab = Reference<GameObject>(loop.extraction, "highRiskCachePrefab");
                        if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Loot/LootContainer_HighRiskCase.prefab");
                        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, loop.transform);
                        obj.name = "Region_HighRiskCache";
                        obj.transform.position = FindSpace(loop, d, site.transform.position + Vector3.right * 3, .8f, Array.Empty<Vector3>());
                        site.rewardContainer = obj.GetComponent<LootContainer>();
                        Set(site.rewardContainer, "lootTable", loot); Set(site.rewardContainer, "gridWidth", 6); Set(site.rewardContainer, "gridHeight", 5);
                        obj.SetActive(false);
                    }
                    sites.Add(site);
                }
                for (int n = 0; n < 2; n++)
                {
                    string name = "StormCandidate_" + d.regionId + "_" + n;
                    var existing = loop.transform.Find(name);
                    if (existing != null) candidates.Add(existing);
                    else candidates.Add(NamedPoint(loop.transform, name, FindSafeDisk(loop, d, n == 0 ? terminal.transform.position : loop.world.World(region.center.V) + Vector3.up * 12)));
                }
            }
            foreach (var extra in loop.stormCandidates ?? Array.Empty<Transform>()) if (extra != null && !candidates.Contains(extra)) candidates.Add(extra);
            loop.anchors = anchors.ToArray(); loop.optionalSites = sites.ToArray(); loop.stormCandidates = candidates.ToArray(); loop.exitApproaches = exits.ToArray();
            ConfigureFog(scene, loop);
            ConfigureWorldSignals(scene, loop);
            IsolateExperience(scene);
            RegionLoopInstaller.CloneSceneFonts(scene, DataFolder + "/Fonts");
            EditorSceneManager.MarkSceneDirty(scene);
            ValidateScene(scene);
            EditorSceneManager.SaveScene(scene, Target);
            if (!EditorBuildSettings.scenes.Any(s => s.path == Target))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(Target, true) }).ToArray();
            ValidateProtected();
            File.WriteAllText(Reports + "/Installation.txt", "PASS\n" + Path.GetFullPath(Target) + "\nCreated=" + created + "\nGarrisons=" + anchors.Count(a => a.residentSlot) + "\nOptional sites=" + sites.Count + "\nNative MissionHUD/Storm/Fog restored. Reinstallation retains transforms and isolated tuning.\n");
            Debug.Log("[RegionV02] Installed and validated " + Target);
        }

        static RegionSpawnGroup G(EnemyType type, int count, bool key = false) => new RegionSpawnGroup { type = type, count = count, keyGuard = key };
        static string HudStyle(Scene scene)
        {
            var panel = Reference<CanvasGroup>(One<MissionHUD>(scene), "missionPanelGroup").transform;
            var rows = new List<string>();
            foreach (var rect in panel.GetComponentsInChildren<RectTransform>(true))
            {
                string path = rect.name; for (var p = rect.parent; p != null && p != panel.parent; p = p.parent) path = p.name + "/" + path;
                rows.Add(path + " sibling=" + rect.GetSiblingIndex() + " anchor=" + rect.anchorMin.ToString("R") + ":" + rect.anchorMax.ToString("R") +
                    " pos=" + rect.anchoredPosition.ToString("R") + " size=" + rect.sizeDelta.ToString("R") + " pivot=" + rect.pivot.ToString("R") +
                    " scale=" + rect.localScale.ToString("R") + " rotation=" + rect.localRotation.ToString("R"));
                var label = rect.GetComponent<TMP_Text>();
                if (label != null) rows.Add("text-style " + label.fontSize + " " + label.color + " " + label.fontStyle + " " + label.alignment + " " + label.margin +
                    " auto=" + label.enableAutoSizing + " font=" + label.font.faceInfo.familyName + "/" + label.font.faceInfo.styleName);
                var image = rect.GetComponent<Image>();
                if (image != null) rows.Add("image-style " + image.color + " " + image.type + " sprite=" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.sprite)));
                var slider = rect.GetComponent<Slider>();
                if (slider != null) rows.Add("slider-style " + slider.minValue + " " + slider.maxValue + " " + slider.direction);
            }
            return string.Join("\n", rows);
        }
        static RegionWave W(string id, params RegionSpawnGroup[] groups) => new RegionWave { id = id, groups = groups };
        static void ConfigureDefinition(RegionDefinition d, int i)
        {
            d.enemies = new[] {
                new RegionEnemyProfile { archetypeId="Swarmer",type=EnemyType.Normal,health=i==0?3:4,experience=i==0?1:2,speed=2+.15f*i },
                new RegionEnemyProfile { archetypeId="Rusher",type=EnemyType.Fast,health=4+i,experience=i==2?3:2,speed=3.3f,special=true,simultaneousLimit=2 },
                new RegionEnemyProfile { archetypeId="Tank",type=EnemyType.Heavy,health=16,experience=4,speed=1.35f,damage=2,clearance=1.1f,special=true,simultaneousLimit=1 }
            }.Take(i+1).ToArray();
            d.residents = i == 0 ? new[] { G(EnemyType.Normal,2,true),G(EnemyType.Normal,6) } : i == 1 ?
                new[] { G(EnemyType.Normal,2,true),G(EnemyType.Normal,4),G(EnemyType.Fast,2) } :
                new[] { G(EnemyType.Normal,2,true),G(EnemyType.Normal,3),G(EnemyType.Fast,2),G(EnemyType.Heavy,1) };
            d.aliveLimit=16; d.specialLimit=3; d.patrolBudget=0; d.encounterExperience=2+i; d.missionExperience=new[]{2,6,8}[i];
            d.moduleSlot=i; d.minimumPlayerDistance=5; d.perceptionRadius=12; d.leashRadius=24;
            d.v02 = new RegionV02Settings { explorationSeconds=120+30*i, fogAdvanceSeconds=70+10*i };
            d.v02.environmentSquads = i == 0 ? new[] {W("small",G(EnemyType.Normal,3)),W("wide",G(EnemyType.Normal,4)),W("press",G(EnemyType.Normal,3))} : i==1 ?
                new[] {W("swarm",G(EnemyType.Normal,4)),W("rush",G(EnemyType.Normal,3),G(EnemyType.Fast,1)),W("flank",G(EnemyType.Normal,2),G(EnemyType.Fast,2))} :
                new[] {W("mixed",G(EnemyType.Normal,3),G(EnemyType.Fast,1)),W("armor",G(EnemyType.Normal,3),G(EnemyType.Heavy,1)),W("rush",G(EnemyType.Normal,2),G(EnemyType.Fast,2))};
            var ordinary = new RegionOptionalDefinition { id="beacon-defense",experience=10+4*i,defendSeconds=18+3*i,deadlineSeconds=50+5*i,
                waves=new[]{W("defense-1",G(EnemyType.Normal,3)),W("defense-2", i==0?G(EnemyType.Normal,2):G(EnemyType.Fast,1))} };
            ordinary.waves[1].delay=10;
            var high = new RegionOptionalDefinition { id="high-risk-defense",highRisk=true,acceptSeconds=110,defendSeconds=24,deadlineSeconds=65,claimAndExitSeconds=40,experience=18,maxAttempts=2,
                waves=new[]{W("risk-1",G(EnemyType.Normal,3),G(EnemyType.Fast,1,true)),W("risk-2",G(EnemyType.Normal,3),G(EnemyType.Fast,1,true))} };
            high.waves[1].delay=12;
            d.v02.optionalTasks=i==1?new[]{ordinary,high}:new[]{ordinary};
        }

        static void RestoreNativePresentation(Scene scene, RegionLoopController loop)
        {
            var dormant = new HashSet<Behaviour>(loop.dormantBehaviours);
            var objects = new HashSet<GameObject>(loop.dormantObjects);
            foreach (var item in All<MonoBehaviour>(scene))
            {
                bool restore = item is MissionHUD || item is MissionDirectionArrow || item is StormHUD || item is StormOverlayController ||
                    item is StormDamageController || item is ToxicFogDamageController || item is ToxicFogFeedbackHUD || item is SafeZoneDirectionIndicator ||
                    item is SafeZoneEdgeIndicator || item is SafeZoneVisual || item is ToxicFogGroundVisual || item is ToxicFogFrontVisual || item is ToxicFogMistEmitter;
                if (!restore) continue;
                dormant.Remove(item); item.enabled=true; objects.Remove(item.gameObject); item.gameObject.SetActive(true);
                var iterator = new SerializedObject(item).GetIterator();
                while(iterator.NextVisible(true))
                {
                    if(iterator.propertyType!=SerializedPropertyType.ObjectReference) continue;
                    if(iterator.objectReferenceValue is Graphic graphic) { objects.Remove(graphic.gameObject); graphic.gameObject.SetActive(true); }
                    else if(iterator.objectReferenceValue is RectTransform rect && iterator.name.ToLowerInvariant().Contains("arrow")) { objects.Remove(rect.gameObject); rect.gameObject.SetActive(true); }
                }
            }
            var oldHud=loop.hud.GetComponentInParent<Canvas>(true); oldHud.gameObject.SetActive(false); objects.Add(oldHud.gameObject);
            loop.dormantBehaviours=dormant.ToArray(); loop.dormantObjects=objects.ToArray();
        }
        static Transform NamedPoint(Transform parent,string name,Vector3 position)
        {
            var existing=parent.Find(name); if(existing!=null)return existing;
            var point=new GameObject(name).transform; point.SetParent(parent);point.position=position;return point;
        }
        static Vector3 FindSpace(RegionLoopController loop,RegionDefinition d,Vector3 desired,float radius,IEnumerable<Vector3> separated)
        {
            for(int ring=0;ring<20;ring++)for(int n=0;n<(ring==0?1:24);n++)
            {
                float a=n*Mathf.PI*2/24;var p=desired+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*ring*.8f;
                if(separated.Any(s=>Vector2.Distance(s,p)<4))continue;
                if(RegionSpawnSpace.Valid(loop.world,d.regionId,p,loop.Terminal(d.regionId).transform.position,radius,true,out _))return p;
            }
            throw new InvalidOperationException("No valid placement: "+d.regionId+" near "+desired);
        }
        internal static bool DiskValid(RegionLoopController loop,RegionDefinition d,Vector3 center)
        {
            if (!RegionSpawnSpace.Valid(loop.world,d.regionId,center,center,d.v02.safeRadius+.35f,true,out _)) return false;
            for(int ring=0;ring<=3;ring++)for(int n=0;n<(ring==0?1:24);n++)
            {
                float a=n*Mathf.PI*2/24;var p=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*d.v02.safeRadius*ring/3;
                if(!RegionSpawnSpace.Valid(loop.world,d.regionId,p,center,.35f,true,out _))return false;
            }
            return true;
        }
        static Vector3 FindSafeDisk(RegionLoopController loop,RegionDefinition d,Vector3 desired)
        {
            for(int ring=0;ring<22;ring++)for(int n=0;n<(ring==0?1:16);n++)
            {float a=n*Mathf.PI*2/16;var p=desired+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*ring;if(DiskValid(loop,d,p))return p;}
            throw new InvalidOperationException("No full safe disk in "+d.regionId);
        }
        static void ConfigureWorldSignals(Scene scene, RegionLoopController loop)
        {
            int vfx=SortingLayer.NameToID("VFX");
            foreach(var beacon in loop.optionalSites.Select(s=>s.beacon))
            {
                var feedback=beacon.GetComponent<BeaconVisualFeedback>();Set(beacon,"visualFeedback",feedback);
                var body=Reference<SpriteRenderer>(feedback,"bodyRenderer");
                if(body.sprite==null)
                {
                    body.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Environment/Props/Ground_Device.png");
                    if(body.sprite==null)throw new InvalidOperationException("Existing ground device art missing");
                    body.transform.localScale=Vector3.one*(1.8f/Mathf.Max(body.sprite.bounds.size.x,body.sprite.bounds.size.y));
                    var depth=body.GetComponent<L01DepthSort>()??body.gameObject.AddComponent<L01DepthSort>();
                    depth.target=body;depth.anchor=beacon.transform;depth.sortingLayer=SortingLayer.NameToID("World");depth.Apply();
                }
                var marker=Reference<GameObject>(beacon,"interactionMarker").GetComponent<SpriteRenderer>();
                if(marker!=null&&marker.sprite==null)
                {
                    marker.sprite=Reference<RectTransform>(One<MissionDirectionArrow>(scene),"arrowRectTransform").GetComponent<Image>().sprite;
                    marker.transform.localScale=Vector3.one*(.7f/Mathf.Max(marker.sprite.bounds.size.x,marker.sprite.bounds.size.y));
                    marker.sortingLayerID=vfx;marker.sortingOrder=15;
                }
                var line=Reference<LineRenderer>(beacon,"activationRangeVisual");
                if(line.sortingLayerID==0){line.sortingLayerID=vfx;line.sortingOrder=4;}
            }
            // Default sorts below this map's opaque Ground layer. Rebind scene instances only.
            foreach(var line in loop.anchors.Where(a=>a.warning!=null).Select(a=>a.warning)
                .Concat(loop.terminals.Where(t=>t.deviceRange!=null).Select(t=>t.deviceRange))
                .Concat(All<SafeZoneVisual>(scene).Select(s=>s.GetComponent<LineRenderer>())))
                if(line.sortingLayerID==0){line.sortingLayerID=vfx;line.sortingOrder=5;}
            foreach(var terminal in loop.terminals)
                if(terminal.status!=null&&terminal.status.GetComponent<Renderer>().sortingLayerID==0)
                    terminal.status.GetComponent<Renderer>().sortingLayerID=vfx;
            foreach(var ground in All<ToxicFogGroundVisual>(scene))
            {
                if(NeedsFogMaterial(Reference<Material>(ground,"fogMaterial"),"Ground"))Set(ground,"fogMaterial",FogMaterial("Ground",false));
                if(new SerializedObject(ground).FindProperty("sortingLayerName").stringValue=="Default")
                {Set(ground,"sortingLayerName","Ground");Set(ground,"sortingOrder",30000);}
            }
            foreach(var front in All<ToxicFogFrontVisual>(scene))
            {
                if(NeedsFogMaterial(Reference<Material>(front,"frontMaterial"),"Front"))Set(front,"frontMaterial",FogMaterial("Front",false));
                if(new SerializedObject(front).FindProperty("sortingLayerName").stringValue=="Default")
                {Set(front,"sortingLayerName","VFX");Set(front,"sortingOrder",1);}
            }
            foreach(var mist in All<ToxicFogMistEmitter>(scene))
                foreach(var renderer in mist.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if(NeedsFogMaterial(renderer.sharedMaterial,"Mist"))renderer.sharedMaterial=FogMaterial("Mist",true);
                    if(renderer.sortingLayerID==0)renderer.sortingLayerID=vfx;
                }
        }
        static bool NeedsFogMaterial(Material material,string kind) => material==null||AssetDatabase.GetAssetPath(material)=="Assets/_Project/Material/Environment/MAT_ToxicFog_"+kind+".mat";
        static Material FogMaterial(string kind,bool particles)
        {
            string path=DataFolder+"/MAT_ToxicFog_"+kind+"_V02.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Material/Environment/MAT_ToxicFog_"+kind+".mat");
            // Preserve the existing colors/soft-mist artwork. Use passes supported by this map's
            // 2D renderer, with vertex tint for particles and the existing mesh atlas pass for strips.
            material=new Material(source){name="MAT_ToxicFog_"+kind+"_V02"};
            material.shader=Shader.Find(particles?"Universal Render Pipeline/2D/Sprite-Unlit-Default":"AlienPurge/Level01ConnectedV01/Atlas");
            if(material.shader==null)throw new InvalidOperationException("Existing 2D fog-compatible shader missing");
            material.shaderKeywords=Array.Empty<string>();material.renderQueue=3000;
            material.SetColor("_Color",source.GetColor("_BaseColor"));
            if(particles)material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Effects/ToxicFog/ToxicFog_SoftMist_128.png"));
            AssetDatabase.CreateAsset(material,path);return material;
        }
        static LootTableData CreateLoot()
        {
            string path=DataFolder+"/LootTable_RegionHighRisk.asset";
            var table=AssetDatabase.LoadAssetAtPath<LootTableData>(path);if(table!=null)return table;
            if(!AssetDatabase.CopyAsset("Assets/_Project/Data/Loot/LootTable_HighRiskCase.asset",path))throw new IOException(path);
            table=AssetDatabase.LoadAssetAtPath<LootTableData>(path);
            var so=new SerializedObject(table);so.FindProperty("minRolls").intValue=4;so.FindProperty("maxRolls").intValue=5;
            var entries=so.FindProperty("entries"); ItemData guaranteed=null;
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);var item=entry.FindPropertyRelative("item").objectReferenceValue as ItemData;
                if(item==null)continue;
                float weight=item.Rarity==ItemRarity.Common?1:item.Rarity==ItemRarity.Uncommon?3:item.Rarity==ItemRarity.Rare?25:item.Rarity==ItemRarity.Epic?18:5;
                entry.FindPropertyRelative("weight").floatValue=weight;
                if(item.Rarity==ItemRarity.Rare && (guaranteed==null||item.GridArea<guaranteed.GridArea))guaranteed=item;
            }
            if(guaranteed==null)throw new InvalidOperationException("High-risk source has no rare reward.");
            var guarantee=so.FindProperty("guaranteedEntries");guarantee.arraySize=1;var g=guarantee.GetArrayElementAtIndex(0);
            g.FindPropertyRelative("item").objectReferenceValue=guaranteed;g.FindPropertyRelative("weight").floatValue=1;
            g.FindPropertyRelative("minQuantity").intValue=1;g.FindPropertyRelative("maxQuantity").intValue=1;
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(table);return table;
        }
        static void IsolateExperience(Scene scene)
        {
            var pool=One<ExperienceOrbPool>(scene);string path=DataFolder+"/ExperienceOrb_V02.prefab";
            if(!File.Exists(path))
            {
                if(!AssetDatabase.CopyAsset("Assets/_Project/Prefabs/Pickups/ExperienceOrb.prefab",path))throw new IOException(path);
                var contents=PrefabUtility.LoadPrefabContents(path);
                try { Set(contents.GetComponent<ExperienceOrbMagnet>(),"attractionRadius",3.25f);PrefabUtility.SaveAsPrefabAsset(contents,path); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            Set(pool,"experienceOrbPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ExperienceOrb>());
        }

        static void ConfigureFog(Scene scene,RegionLoopController loop)
        {
            var root=loop.transform.Find("Fog_Route_V02");bool create=root==null;
            if(create)
            {
                root=new GameObject("Fog_Route_V02").transform;root.SetParent(loop.transform);
                // Cross sections follow rooms and bent connecting roads, with broad coverage only inside each room.
                var points=new[]{
                    new Vector4(-213.1875f,-46,27,0),new Vector4(-213.1875f,-10,27,0),new Vector4(-213.1875f,-9,3,0),new Vector4(-213.1875f,12,3,0),
                    new Vector4(-213.1875f,12.1f,33,0),new Vector4(-213.1875f,68,33,0),new Vector4(-171,38.35938f,0,3.3f),new Vector4(-157,38.35938f,0,3.3f),new Vector4(-157,-2,3.3f,0),new Vector4(-147.57812f,-2,0,3.3f),new Vector4(-147.57812f,-8,3.3f,0),
                    new Vector4(-123,-8,39,0),new Vector4(-123,-60,39,0),new Vector4(-100.75781f,-17,3.75f,0),new Vector4(-100.75781f,12,3.75f,0)};
                // Cargo coverage is asymmetric around its incoming road center, matching the existing room bounds.
                float distance=0;Vector3 previous=Vector3.zero;
                for(int i=0;i<points.Length;i++)
                {
                    var v=points[i];var p=NamedPoint(root,"Section_"+i.ToString("D2"),loop.world.World(new Vector2(v.x,v.y)));
                    if(i>0)distance+=Vector3.Distance(previous,p.position);previous=p.position;
                    var section=p.gameObject.AddComponent<ToxicFogCrossSection>();
                    var left=NamedPoint(p,"FogLeft",p.position-new Vector3(v.z,v.w));var right=NamedPoint(p,"FogRight",p.position+new Vector3(v.z,v.w));
                    if(i==4||i==5){left.position=loop.world.World(new Vector2(-246,v.y));right.position=loop.world.World(new Vector2(-162,v.y));}
                    Set(section,"leftBoundary",left);Set(section,"rightBoundary",right);
                    if(i==3){loop.definitions[0].v02.fogStartDistance=0;loop.definitions[0].v02.fogEndDistance=distance;loop.definitions[1].v02.fogStartDistance=distance;}
                    if(i==10){loop.definitions[1].v02.fogEndDistance=distance;loop.definitions[2].v02.fogStartDistance=distance;}
                    if(i==points.Length-1)loop.definitions[2].v02.fogEndDistance=distance;
                }
                foreach(var d in loop.definitions){EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);}
            }
            // Preserve the full road width at right-angle turns. A single rotating strip would leave
            // safe wedges at the road edges. Existing authored centers/boundaries are retained.
            foreach (string start in new[] { "07", "08", "09" })
            {
                string name = "Section_" + start + "A";
                if (root.Find(name) != null) continue;
                var a = root.Find("Section_" + start);
                var b = root.Find("Section_" + (int.Parse(start) + 1).ToString("D2"));
                var point = NamedPoint(root, name, Vector3.Lerp(a.position, b.position, .01f));
                var section = point.gameObject.AddComponent<ToxicFogCrossSection>();
                var target = b.GetComponent<ToxicFogCrossSection>();
                Set(section, "leftBoundary", NamedPoint(point, "FogLeft", point.position + (Vector3)(target.LeftPosition - target.Center)));
                Set(section, "rightBoundary", NamedPoint(point, "FogRight", point.position + (Vector3)(target.RightPosition - target.Center)));
            }
            var sections=root.GetComponentsInChildren<ToxicFogCrossSection>(true).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            SetArray(loop.fog,"routePoints",sections.Select(s=>(Object)s.transform));SetArray(loop.fogArea,"crossSections",sections);Set(loop.fogArea,"routeController",loop.fog);
            foreach(var item in All<MonoBehaviour>(scene).Where(m=>m is ToxicFogGroundVisual||m is ToxicFogFrontVisual||m is ToxicFogMistEmitter||m is ToxicFogFeedbackHUD))
            {
                SetArray(item,"crossSections",sections);Set(item,"fogRouteRoot",root);Set(item,"fogArea",loop.fogArea);
                var so=new SerializedObject(item);if(so.FindProperty("fogRoute")!=null)Set(item,"fogRoute",loop.fog);
            }
        }

        [MenuItem("AlienPurge/Gameplay/Level01/Validate Region Loop V02")]
        public static void Validate()
        {
            var scene=SceneManager.GetSceneByPath(Target);if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(Target);
            var loop=One<RegionLoopController>(scene);loop.world.navigation.ConfigureGrid();Physics2D.SyncTransforms();loop.world.navigation.astar.Scan();
            ValidateScene(scene);ValidateProtected();
            File.WriteAllText(Reports+"/Validation.txt","PASS: unique services, V02 asset isolation, native HUD, scheduler flags, mission/gate IDs, reserved population capacity, reachable garrisons and storm disks, fog geometry, reward IDs, protected assets. Editor validation only.\n");
            Debug.Log("[RegionV02] Editor validation PASS");
        }
        internal static void ValidateScene(Scene scene)
        {
            var c=One<RegionLoopController>(scene);One<GameManager>(scene);One<MissionManager>(scene);One<EnemyPool>(scene);One<UpgradeManager>(scene);One<WeaponModuleSelectionManager>(scene);
            var errors=new List<string>();
            if (File.ReadAllText(Reports + "/NativeHudStyle.txt") != HudStyle(scene)) errors.Add("Native MissionHUD appearance/layout differs from V01 source");
            foreach(var t in All<Transform>(scene))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add("Missing script "+t.name);
            if(!c.v02Enabled||c.v02InstallationId!=Owner||c.world.progression.regionLoop!=c)errors.Add("V02 authority missing");
            if(!c.storm.regionDriven||!c.fog.regionDriven||!c.extraction.regionDriven||!c.missionView.regionDriven||!c.rewards.modules.eventDrivenEvolution||!c.rewards.modules.regionDriven||All<EnemySpawner>(scene).Any(s=>!s.regionDriven))errors.Add("Double/legacy scheduling");
            if(!One<MissionHUD>(scene).enabled||!One<MissionDirectionArrow>(scene).enabled||c.hud.GetComponentInParent<Canvas>(true).gameObject.activeSelf)errors.Add("Native HUD not restored / duplicate HUD");
            if(!One<StormDamageController>(scene).isActiveAndEnabled||!One<ToxicFogDamageController>(scene).isActiveAndEnabled)errors.Add("Native damage disabled");
            foreach(var site in c.optionalSites)
                if(Reference<SpriteRenderer>(site.beacon.GetComponent<BeaconVisualFeedback>(),"bodyRenderer").sprite==null||
                    Reference<LineRenderer>(site.beacon,"activationRangeVisual").sortingLayerID==0)errors.Add("Invisible native Beacon / range: "+site.name);
            if(One<SafeZoneVisual>(scene).GetComponent<LineRenderer>().sortingLayerID==0||
                new SerializedObject(One<ToxicFogGroundVisual>(scene)).FindProperty("sortingLayerName").stringValue=="Default")errors.Add("Environment visuals sort below Ground");
            var mistMaterial=One<ToxicFogMistEmitter>(scene).GetComponent<ParticleSystemRenderer>().sharedMaterial;
            if(mistMaterial==null||mistMaterial.GetTexture("_MainTex")==null||!AssetDatabase.GetAssetPath(mistMaterial).StartsWith(DataFolder+"/"))errors.Add("Fog mist missing independent material/soft texture");
            var unique=new HashSet<string>();
            for(int i=0;i<c.definitions.Length;i++)
            {
                var d=c.definitions[i];if(d==null||d.v02==null){errors.Add("Missing definition");continue;}
                if(!AssetDatabase.GetAssetPath(d).StartsWith(DataFolder+"/"))errors.Add("Shared definition "+d.name);
                if(d.regionId!=Ids[i]||d.missionId!=new[]{"Arrival","Beacon","Power"}[i]||d.exitGateId!="G"+(i+1)||d.moduleSlot!=i)errors.Add("Gate/reward mapping "+d.name);
                if(c.world.progression.gates.Count(g=>g.gateId==d.exitGateId&&g.requirement==d.missionId)!=1)errors.Add("Gate authority "+d.name);
                if(d.aliveLimit-d.v02.missionCapacityReserve<d.residents.Sum(g=>g.count)||c.globalAliveLimit<=d.v02.missionCapacityReserve||d.v02.searchSeconds<8||d.v02.searchSeconds>15||d.v02.environmentSquads.Length<2)errors.Add("Invalid pressure/capacity "+d.name);
                if(d.v02.fogEndDistance<=d.v02.fogStartDistance||i>0&&Mathf.Abs(d.v02.fogStartDistance-c.definitions[i-1].v02.fogEndDistance)>.1f)errors.Add("Fog stage discontinuity "+d.name);
                if(!c.stormCandidates.Any(p=>p!=null&&c.world.ResolveRegion(c.world.Local(p.position))==d.regionId&&DiskValid(c,d,p.position)))errors.Add("No valid full storm disk "+d.name);
                foreach(var a in c.anchors.Where(a=>a.regionId==d.regionId))
                {
                    if(!unique.Add(a.stableId))errors.Add("Duplicate anchor "+a.stableId);
                    if(!RegionSpawnSpace.Valid(c.world,d.regionId,a.transform.position,c.Terminal(d.regionId).transform.position,d.enemies.Max(p=>p.clearance),true,out var why))errors.Add(a.stableId+": "+why);
                }
                foreach(var task in d.v02.optionalTasks)
                {
                    if(task.defendSeconds>=task.deadlineSeconds||task.deadlineSeconds+task.claimAndExitSeconds>d.v02.explorationSeconds+d.v02.fogWarningSeconds)errors.Add("Impossible task timing "+task.id);
                    if(!unique.Add(d.regionId+"/"+task.id))errors.Add("Duplicate task ID");
                }
            }
            foreach(var site in c.optionalSites)if(site==null||site.beacon==null||site.beacon.regionSite!=site||site.Config==null||site.Config.highRisk&&site.rewardContainer==null)errors.Add("Task references");
            if(!AssetDatabase.GetAssetPath(Reference<ExperienceOrb>(One<ExperienceOrbPool>(scene),"experienceOrbPrefab")).StartsWith(DataFolder+"/"))errors.Add("Shared XP prefab");
            foreach(var site in c.optionalSites.Where(s=>s.Config.highRisk))
            {
                var table=Reference<LootTableData>(site.rewardContainer,"lootTable");if(table==null||!AssetDatabase.GetAssetPath(table).StartsWith(DataFolder+"/")||table.GuaranteedEntries.Count==0)errors.Add("High-risk loot isolation/guarantee");
            }
            var route = new SerializedObject(c.fog).FindProperty("routePoints");
            var sections = new SerializedObject(c.fogArea).FindProperty("crossSections");
            if (route.arraySize != sections.arraySize || route.arraySize < 12) errors.Add("Fog route/coverage arrays differ");
            float distance = 0;
            for (int i = 0; i < route.arraySize; i++)
            {
                var point = route.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                var section = sections.GetArrayElementAtIndex(i).objectReferenceValue as ToxicFogCrossSection;
                if (point == null || section == null || !section.IsValid || section.transform != point) { errors.Add("Fog route missing/order mismatch " + i); continue; }
                if (i == 0) continue;
                var previous = route.GetArrayElementAtIndex(i - 1).objectReferenceValue as Transform;
                float length = Vector3.Distance(previous.position, point.position); distance += length;
                if (length < .001f) errors.Add("Repeated fog route center " + i);
            }
            if (Mathf.Abs(distance - c.definitions.Last().v02.fogEndDistance) > .1f) errors.Add("Fog stage cap differs from authored route length");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
        }
        [Serializable] class Baseline { public string baseline; public Entry[] files; }
        [Serializable] class Entry { public string path,blob; }
        public static void ValidateProtected()
        {
            var data=JsonUtility.FromJson<Baseline>(File.ReadAllText(Reports+"/ProtectedBaseline.json"));
            var errors=data.files.Where(f=>!File.Exists(f.path)||RegionLoopInstaller.HashGitBlob(f.path)!=f.blob).Select(f=>f.path).ToArray();
            if(errors.Length>0)throw new InvalidOperationException("Protected V01/Boss/shared assets changed:\n"+string.Join("\n",errors));
        }
        public static void BatchInstallAndValidate(){Install();Validate();}
    }
}
