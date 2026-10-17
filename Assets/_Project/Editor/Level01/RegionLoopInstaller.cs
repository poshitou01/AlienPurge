using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    public static class RegionLoopInstaller
    {
        public const string Source = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld.unity";
        public const string Target = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld_GameplayV01.unity";
        public const string DataFolder = "Assets/_Project/Data/Level01/RegionLoop";
        public const string ReportFolder = "Docs/Level01";
        static readonly string[] regionIds = { "ColonyEntry", "CargoYard", "PowerMaintenance" };
        static readonly string[] missionIds = { "Arrival", "Beacon", "Power" };
        static readonly string[] gateIds = { "G1", "G2", "G3" };
        static string phase;

        [MenuItem("AlienPurge/Gameplay/Level01/Install Region Loop V01")]
        public static void Install()
        {
            bool createdScene = false;
            try
            {
                phase = "preflight";
                if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before installing.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your current scene edits first: " + SceneManager.GetSceneAt(i).path);
                if (!File.Exists(Source)) throw new FileNotFoundException("Complete map source is missing", Source);
                ValidateProtected();
                if (!File.Exists(Target))
                {
                    phase = "copy full-world scene";
                    if (!AssetDatabase.CopyAsset(Source, Target)) throw new IOException("Could not copy source scene.");
                    createdScene = true;
                }
                var scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Single);
                var existing = All<RegionLoopController>(scene);
                if (existing.Length > 1) throw new InvalidOperationException("Duplicate region controllers in target scene.");
                if (!createdScene && (existing.Length == 0 || existing[0].installationId != RegionLoopController.OwnerId))
                    throw new InvalidOperationException("Target scene has unknown ownership. It was NOT overwritten: " + Target);
                var world = One<L01World>(scene);
                if (world.Data == null || new[] { "ColonyEntry", "CargoYard", "PowerMaintenance", "QuarantineCommons", "InfectedResearch", "Extraction" }.Any(id => world.Region(id) == null) || world.existingArena == null)
                    throw new InvalidOperationException("Source lacks the complete Level01 regions or imported Hive Tyrant Arena.");
                var controller = existing.Length == 1 ? existing[0] : new GameObject("Level01_RegionLoop_V01").AddComponent<RegionLoopController>();
                controller.installedVersion = 0;
                controller.world = world;
                controller.sourceSceneGuid = AssetDatabase.AssetPathToGUID(Source);
                if (string.IsNullOrEmpty(controller.sourceSceneHash)) controller.sourceSceneHash = HashGitBlob(Source);
                controller.regionModeEnabled = true;
                phase = "connect existing services and suppress legacy scheduling";
                ConfigureMode(scene, controller);
                phase = "create or retain editable regional definitions";
                Directory.CreateDirectory(DataFolder); AssetDatabase.Refresh();
                var definitions = new List<RegionDefinition>();
                for (int i = 0; i < regionIds.Length; i++)
                {
                    var configured = controller.definitions?.FirstOrDefault(d => d != null && d.regionId == regionIds[i]);
                    string path = DataFolder + "/" + regionIds[i] + ".asset";
                    var definition = configured != null ? configured : AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
                    if (definition == null)
                    {
                        if (File.Exists(path)) throw new InvalidOperationException("Conflicting asset: " + path);
                        definition = MakeDefinition(i); AssetDatabase.CreateAsset(definition, path);
                    }
                    foreach (var profile in definition.enemies)
                        if (string.IsNullOrEmpty(profile.archetypeId))
                        {
                            profile.archetypeId = profile.type == EnemyType.Normal ? "Swarmer" : profile.type == EnemyType.Fast ? "Rusher" : profile.type == EnemyType.Heavy ? "Tank" : "Spitter";
                            EditorUtility.SetDirty(definition);
                        }
                    definitions.Add(definition);
                }
                controller.definitions = definitions.ToArray();
                phase = "scan copied map and author traversable device / ingress anchors";
                world.navigation.ConfigureGrid(); Physics2D.SyncTransforms(); world.navigation.astar.Scan();
                var terminalList = new List<RegionTerminal>();
                var anchorList = new List<RegionAnchor>();
                var font = All<TMP_Text>(scene).Select(t => t.font).FirstOrDefault(f => f != null) ?? TMP_Settings.defaultFontAsset;
                var material = AssetDatabase.LoadAssetAtPath<Material>(DataFolder + "/RegionSignals.mat");
                if (material == null)
                {
                    material = new Material(Shader.Find("Sprites/Default")) { name = "RegionSignals" };
                    AssetDatabase.CreateAsset(material, DataFolder + "/RegionSignals.mat");
                }
                foreach (var definition in definitions)
                {
                    var region = world.Region(definition.regionId);
                    var matches = All<RegionTerminal>(scene).Where(t => t.stableId == "device/" + definition.regionId).ToArray();
                    if (matches.Length > 1) throw new InvalidOperationException("Duplicate device id: " + definition.regionId);
                    var terminal = matches.FirstOrDefault();
                    if (terminal == null)
                    {
                        var old = All<L01Terminal>(scene).FirstOrDefault(t => t.completesFlag == definition.missionId && t.requiredFlag != "BeaconFailed");
                        Vector3 desired = old != null ? old.transform.position : world.World(region.center.V);
                        var position = FindSpace(world, definition.regionId, desired, desired, false, 1);
                        terminal = CreateTerminal(controller, definition, position, font, material);
                    }
                    terminal.controller = controller; terminalList.Add(terminal);
                    for (int i = 0; i < region.missionPoints.Length; i++)
                    {
                        string id = "ingress/" + definition.regionId + "/" + i;
                        var points = All<RegionAnchor>(scene).Where(a => a.stableId == id).ToArray();
                        if (points.Length > 1) throw new InvalidOperationException("Duplicate anchor id: " + id);
                        var anchor = points.FirstOrDefault();
                        if (anchor == null)
                        {
                            var position = FindSpace(world, definition.regionId, world.World(region.missionPoints[i].V), terminal.transform.position, true,
                                definition.enemies.Max(p => p.clearance));
                            anchor = new GameObject("Ingress_" + definition.regionId + "_" + i).AddComponent<RegionAnchor>();
                            anchor.transform.SetParent(controller.transform); anchor.transform.position = position;
                            anchor.stableId = id; anchor.regionId = definition.regionId;
                            anchor.warning = Circle(anchor.transform, "ArrivalWarning", 1, material, new Color(1,.6f,.15f));
                            anchor.warning.enabled = false;
                        }
                        anchorList.Add(anchor);
                    }
                }
                // Preserve additional authored anchors with stable identifiers.
                foreach (var anchor in All<RegionAnchor>(scene)) if (!anchorList.Contains(anchor)) anchorList.Add(anchor);
                controller.terminals = terminalList.ToArray(); controller.anchors = anchorList.ToArray();
                if (controller.hud == null) controller.hud = CreateHud(controller.transform, font);
                phase = "isolate dynamic fonts used by copied UI";
                CloneSceneFonts(scene);
                phase = "validate installation";
                controller.installedVersion = 1;
                EditorSceneManager.MarkSceneDirty(scene);
                ValidateScene(scene, true);
                phase = "save target and append restart/build entry";
                EditorSceneManager.SaveScene(scene, Target);
                foreach (var definition in definitions) AssetDatabase.SaveAssetIfDirty(definition);
                AssetDatabase.SaveAssetIfDirty(material);
                bool appended = !EditorBuildSettings.scenes.Any(s => s.path == Target);
                if (appended) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(Target, true) }).ToArray();
                ValidateProtected();
                Directory.CreateDirectory(ReportFolder);
                File.WriteAllText(ReportFolder + "/RegionLoop_InstallReport.txt", "SUCCESS\nProject: " + Path.GetFullPath(".") +
                    "\nSource: " + Source + "\nTarget: " + Target + "\nScene created: " + createdScene +
                    "\nBuild entry appended: " + appended + "\nDefinitions: " + definitions.Count + "\nDevices: " + terminalList.Count +
                    "\nIngress anchors: " + anchorList.Count + "\nExisting positions, wave counts and tuning retained.\n" +
                    string.Join("\n", terminalList.Select(t => t.regionId + " device world position: " + t.transform.position)), Encoding.UTF8);
                Debug.Log("[RegionLoop] Installed and validated " + Target + " (3 regions, " + anchorList.Count + " ingress anchors). Open this worktree project to play.");
            }
            catch (Exception exception)
            {
                // Never save an incomplete installation over a user's previous target.
                if (createdScene)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(Target);
                }
                Directory.CreateDirectory(ReportFolder);
                File.WriteAllText(ReportFolder + "/RegionLoop_InstallFailure.txt", "FAILED at " + phase + "\n" + exception);
                Debug.LogError("[RegionLoop] Installation FAILED at " + phase + ": " + exception);
                throw;
            }
        }

        static void ConfigureMode(Scene scene, RegionLoopController controller)
        {
            controller.pool = One<EnemyPool>(scene); controller.missionView = One<MissionManager>(scene);
            controller.safeZone = One<SafeZoneController>(scene);
            var queue = controller.GetComponent<RegionRewardQueue>();
            if (queue == null) queue = controller.gameObject.AddComponent<RegionRewardQueue>();
            controller.rewards = queue;
            queue.upgrades = One<UpgradeManager>(scene); queue.modules = ResolveModuleManager(scene);
            queue.experience = controller.world.player.GetComponent<PlayerExperience>();
            queue.upgrades.regionRewards = queue; queue.modules.regionRewards = queue; queue.modules.regionDriven = true;
            controller.world.regionDriven = true; controller.world.progression.regionLoop = controller;
            controller.world.progression.previewAllDoorsOpen = false;
            controller.world.navigation.astar.logPathResults = Pathfinding.PathLog.OnlyErrors;
            foreach (var spawner in All<EnemySpawner>(scene)) { spawner.regionDriven = true; SetBool(spawner, "enableAutomaticSpawning", false); }
            foreach (var mission in All<MissionManager>(scene)) { mission.regionDriven = true; mission.enabled = true; }
            foreach (var storm in All<PlanetStormController>(scene)) storm.regionDriven = true;
            foreach (var fog in All<ToxicFogRouteController>(scene)) fog.regionDriven = true;
            foreach (var extraction in All<ExtractionController>(scene)) extraction.regionDriven = true;
            SetBool(One<GameManager>(scene), "showRemainingTime", false);
            controller.safeZone.ClearSafeZone();
            var dormant = new HashSet<Behaviour>(controller.dormantBehaviours ?? Array.Empty<Behaviour>());
            var objects = new HashSet<GameObject>(controller.dormantObjects ?? Array.Empty<GameObject>());
            foreach (var item in All<MonoBehaviour>(scene))
            {
                if (item == null) continue;
                if (item is L01Terminal || item is BeaconMissionObjective || item is AlienCoreMissionObjective || item is StormDamageController || item is ToxicFogDamageController ||
                    item is MissionHUD || item is MissionDirectionArrow || item is StormHUD || item is StormOverlayController ||
                    item is ExtractionHUD || item is ToxicFogFeedbackHUD || item is SafeZoneDirectionIndicator || item is SafeZoneEdgeIndicator)
                { dormant.Add(item); item.enabled = false; HideReferencedGraphics(item, objects); }
                if (item is CargoYardMissionEntryGate || item is EarlyExtractionEntryTrigger || item is SafeZoneVisual ||
                    item is ToxicFogGroundVisual || item is ToxicFogFrontVisual || item is ToxicFogMistEmitter)
                { objects.Add(item.gameObject); item.gameObject.SetActive(false); }
                // Legacy exit-guide scripts are presentation only, but their old timed advice is misleading here.
                if (item is CargoYardExitGuide) { dormant.Add(item); item.enabled = false; HideReferencedGraphics(item, objects); }
            }
            controller.dormantBehaviours = dormant.Where(x => x != null).ToArray();
            controller.dormantObjects = objects.Where(x => x != null).ToArray();
        }
        static WeaponModuleSelectionManager ResolveModuleManager(Scene scene)
        {
            var managers = All<WeaponModuleSelectionManager>(scene);
            if (managers.Length == 1) return managers[0];
            var active = managers.Where(m => m.gameObject.activeInHierarchy && m.enabled).ToArray();
            if (active.Length != 1) throw new InvalidOperationException("Conflicting active module managers: " + string.Join(", ", managers.Select(m => m.name)));
            var keeper = active[0]; var keeperData = new SerializedObject(keeper);
            foreach (var duplicate in managers.Where(m => m != keeper))
            {
                var data = new SerializedObject(duplicate);
                bool same = !duplicate.gameObject.activeInHierarchy;
                foreach (string field in new[] { "moduleSelectionPanel", "playerWeaponModifiers", "weaponEvolutionController", "weaponManager" })
                    same &= data.FindProperty(field).objectReferenceValue == keeperData.FindProperty(field).objectReferenceValue;
                var buttons = data.FindProperty("moduleButtons"); var expected = keeperData.FindProperty("moduleButtons");
                same &= buttons.arraySize == expected.arraySize;
                for (int i = 0; same && i < buttons.arraySize; i++) same &= buttons.GetArrayElementAtIndex(i).objectReferenceValue == expected.GetArrayElementAtIndex(i).objectReferenceValue;
                foreach (string field in new[] { "firstModuleSelectionTime", "secondModuleSelectionTime", "powerSpikeSelectionTime" })
                    same &= data.FindProperty(field).floatValue == keeperData.FindProperty(field).floatValue;
                if (!same) throw new InvalidOperationException("Module manager conflict needs review: " + duplicate.name);
                Debug.Log("[RegionLoop] Removing identical inactive shadow module component from copied scene: " + duplicate.transform.parent.name + "/" + duplicate.name);
                Object.DestroyImmediate(duplicate); // Only the target scene component; retain its GameObject and all UI.
            }
            return keeper;
        }
        static void HideReferencedGraphics(MonoBehaviour owner, HashSet<GameObject> objects)
        {
            // Editor serialization only: no runtime reflection or per-frame scene searches.
            var iterator = new SerializedObject(owner).GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var reference = iterator.objectReferenceValue;
                if (reference is CanvasGroup group) { group.alpha = 0; group.blocksRaycasts = false; }
                else if (reference is Graphic graphic) { objects.Add(graphic.gameObject); graphic.gameObject.SetActive(false); }
                else if (reference is RectTransform rect && iterator.name.ToLowerInvariant().Contains("arrow"))
                { objects.Add(rect.gameObject); rect.gameObject.SetActive(false); }
            }
        }
        static RegionDefinition MakeDefinition(int index)
        {
            var definition = ScriptableObject.CreateInstance<RegionDefinition>();
            definition.regionId = regionIds[index]; definition.missionId = missionIds[index];
            definition.encounterId = regionIds[index] + "/garrison"; definition.exitGateId = gateIds[index];
            definition.prerequisite = index == 0 ? "" : missionIds[index - 1];
            var profiles = new List<RegionEnemyProfile> { new RegionEnemyProfile { type = EnemyType.Normal, health = 3 + index, speed = 1.9f + index * .15f, experience = 2 } };
            if (index >= 1) profiles.Add(new RegionEnemyProfile { type = EnemyType.Fast, health = 3 + index, speed = 3.3f, experience = 3, special = true });
            if (index >= 2) profiles.Add(new RegionEnemyProfile { type = EnemyType.Heavy, health = 20, damage = 2, speed = 1.35f, experience = 7, clearance = 1.1f, special = true });
            definition.enemies = profiles.ToArray();
            foreach (var profile in profiles) profile.archetypeId = profile.type == EnemyType.Normal ? "Swarmer" : profile.type == EnemyType.Fast ? "Rusher" : "Tank";
            definition.residents = index == 0 ? new[] { Group(EnemyType.Normal, 5) } :
                index == 1 ? new[] { Group(EnemyType.Normal, 5), Group(EnemyType.Fast, 2) } :
                new[] { Group(EnemyType.Normal, 5), Group(EnemyType.Fast, 2), Group(EnemyType.Heavy, 1) };
            definition.waves = index == 0 ? Array.Empty<RegionWave>() : new[] {
                new RegionWave { id = "wave-1", delay = 0, groups = new[] { Group(EnemyType.Normal, 3), Group(EnemyType.Fast, 1) } },
                new RegionWave { id = "wave-2", delay = 12, groups = index == 1 ?
                    new[] { Group(EnemyType.Normal, 2), Group(EnemyType.Fast, 2) } :
                    new[] { Group(EnemyType.Normal, 3), Group(EnemyType.Heavy, 1, true) } }
            };
            definition.moduleSlot = index; definition.deviceSeconds = index == 0 ? 0 : index == 1 ? 22 : 28;
            definition.encounterExperience = 4 + index * 3; definition.missionExperience = 10 + index * 8;
            definition.patrolBudget = index == 0 ? 2 : 3; definition.specialLimit = index == 2 ? 4 : 3;
            return definition;
        }
        static RegionSpawnGroup Group(EnemyType type, int count, bool key = false) => new RegionSpawnGroup { type = type, count = count, keyGuard = key };

        static RegionTerminal CreateTerminal(RegionLoopController controller, RegionDefinition definition, Vector3 position, TMP_FontAsset font, Material material)
        {
            var terminal = new GameObject("Device_" + definition.regionId).AddComponent<RegionTerminal>();
            terminal.transform.SetParent(controller.transform); terminal.transform.position = position;
            terminal.stableId = "device/" + definition.regionId; terminal.regionId = definition.regionId;
            terminal.gameObject.layer = LayerMask.NameToLayer("Interactable");
            if (terminal.gameObject.layer < 0) throw new InvalidOperationException("Interactable physics layer missing.");
            var collider = terminal.gameObject.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = .65f;
            var so = new SerializedObject(terminal); so.FindProperty("interactionPrompt").stringValue = "START / RETRY " + definition.missionId.ToUpperInvariant(); so.ApplyModifiedPropertiesWithoutUndo();
            // Reuse the project's device art on a scene instance; never Apply to the shared prefab.
            var mission = new SerializedObject(controller.missionView);
            var prefab = mission.FindProperty("beaconObjectivePrefab").objectReferenceValue as GameObject;
            if (prefab != null)
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, terminal.transform);
                art.name = "ExistingBeaconDeviceVisual"; art.transform.localPosition = Vector3.zero;
                foreach (var behaviour in art.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var hit in art.GetComponentsInChildren<Collider2D>(true)) hit.enabled = false;
                foreach (var line in art.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
            }
            terminal.deviceRange = Circle(terminal.transform, "DeviceDefenseRange", definition.deviceRadius, material, new Color(.1f,.9f,1,.5f));
            var label = new GameObject("DeviceLabel").AddComponent<TextMeshPro>(); label.transform.SetParent(terminal.transform, false);
            label.transform.localPosition = new Vector3(0,2,0); label.font = font; label.fontSize = 3; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(9,3); label.text = definition.missionId.ToUpperInvariant() + "\n[E] START";
            label.GetComponent<MeshRenderer>().sortingOrder = 500; terminal.status = label;
            return terminal;
        }
        static LineRenderer Circle(Transform parent, string name, float radius, Material material, Color color)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.useWorldSpace = false; line.loop = true; line.positionCount = 48; line.widthMultiplier = .08f;
            line.sharedMaterial = material; line.startColor = line.endColor = color; line.sortingOrder = 400;
            for (int i = 0; i < line.positionCount; i++) { float angle = i * Mathf.PI * 2 / line.positionCount; line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius); }
            return line;
        }
        static void CloneSceneFonts(Scene scene)
        {
            string folder = DataFolder + "/Fonts";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var copies = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
            Func<TMP_FontAsset, TMP_FontAsset> clone = null;
            clone = source =>
            {
                if (source == null) return null;
                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (sourcePath.StartsWith(folder + "/", StringComparison.Ordinal)) return source;
                if (copies.TryGetValue(source, out var found)) return found;
                string path = folder + "/" + AssetDatabase.AssetPathToGUID(sourcePath) + ".asset";
                var copy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (copy == null)
                {
                    if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, path)) throw new IOException("Cannot isolate font " + source.name);
                    copy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                }
                copies.Add(source, copy);
                copy.fallbackFontAssetTable = (source.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Select(clone).ToList();
                EditorUtility.SetDirty(copy); AssetDatabase.SaveAssetIfDirty(copy);
                return copy;
            };
            foreach (var label in All<TMP_Text>(scene))
            {
                if (label.font == null) continue;
                var original = label.font; var material = label.fontSharedMaterial; var copy = clone(original);
                if (copy == original) continue;
                label.font = copy;
                if (material == null || material == original.material) label.fontSharedMaterial = copy.material;
                else
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long localId);
                    string path = folder + "/Material_" + guid + "_" + localId + ".mat";
                    var copiedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copiedMaterial == null)
                    {
                        copiedMaterial = new Material(material); copiedMaterial.mainTexture = copy.atlasTexture;
                        AssetDatabase.CreateAsset(copiedMaterial, path);
                    }
                    label.fontSharedMaterial = copiedMaterial;
                }
            }
        }
        static TMP_Text CreateHud(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("RegionLoopHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 35;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            var panel = new GameObject("RegionObjective", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(go.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1,1); rect.pivot = new Vector2(1,1);
            rect.anchoredPosition = new Vector2(-24,-200); rect.sizeDelta = new Vector2(510,250);
            panel.GetComponent<Image>().color = new Color(.015f,.035f,.055f,.85f); panel.GetComponent<Image>().raycastTarget = false;
            var text = new GameObject("ObjectiveText").AddComponent<TextMeshProUGUI>(); text.transform.SetParent(panel.transform, false);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16,12); text.rectTransform.offsetMax = new Vector2(-16,-12);
            text.font = font; text.fontSize = 22; text.color = new Color(.7f,.95f,1); text.raycastTarget = false;
            text.text = "REGION LOOP V01\nColonyEntry\nClear entry guards / activate access terminal\nG1: requires Arrival";
            return text;
        }
        static Vector3 FindSpace(L01World world, string region, Vector3 desired, Vector3 destination, bool path, float radius)
        {
            for (int ring = 0; ring < 14; ring++)
                for (int step = 0; step < (ring == 0 ? 1 : 24); step++)
                {
                    float angle = step * Mathf.PI * 2 / 24;
                    var point = desired + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * .8f;
                    if (RegionSpawnSpace.Valid(world, region, point, path ? destination : point, radius, true, out _)) return point;
                }
            throw new InvalidOperationException("No traversable point near " + region + " " + desired + ". Check scene collision and A*.");
        }
        internal static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static T One<T>(Scene scene) where T : Component
        {
            var items = All<T>(scene); if (items.Length != 1) throw new InvalidOperationException(typeof(T).Name + " count must be 1, found " + items.Length);
            return items[0];
        }
        static void SetBool(Object target, string name, bool value)
        {
            var so = new SerializedObject(target); var field = so.FindProperty(name);
            if (field == null) throw new InvalidOperationException(target.name + ": missing serialized field " + name);
            field.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("AlienPurge/Gameplay/Level01/Validate Region Loop V01")]
        public static void Validate()
        {
            var scene = SceneManager.GetSceneByPath(Target);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(Target, OpenSceneMode.Additive);
            ValidateScene(scene, true); ValidateProtected();
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/RegionLoop_Validation.txt", "PASS: required references, unique managers/IDs, G1-G3 mappings, finite waves, pool types, traversable anchors, scheduler opt-in, dormant hazards, protected asset hashes.\nThis is Editor validation, not a Play Mode result.\n");
            Debug.Log("[RegionLoop] Validation PASS. Editor checks only; see Docs/Level01/RegionLoop_Validation.txt");
        }
        public static void ValidateScene(Scene scene, bool scan)
        {
            var errors = new List<string>();
            foreach (var transform in All<Transform>(scene))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) errors.Add("Missing script: " + transform.name);
            var controller = One<RegionLoopController>(scene); var queue = One<RegionRewardQueue>(scene);
            One<GameManager>(scene); One<EnemyPool>(scene); One<MissionManager>(scene); One<UpgradeManager>(scene); One<WeaponModuleSelectionManager>(scene);
            if (controller.world == null || controller.pool == null || controller.hud == null || controller.rewards != queue ||
                queue.upgrades == null || queue.modules == null || queue.experience == null || controller.world.player == null ||
                controller.world.player.GetComponent<PlayerInteractor>() == null) errors.Add("Missing runtime service/player/HUD reference.");
            if (AssetDatabase.AssetPathToGUID(Source) == AssetDatabase.AssetPathToGUID(Target)) errors.Add("Source and target scene GUIDs collide.");
            if (controller.sourceSceneHash != HashGitBlob(Source)) errors.Add("Source scene changed since installation.");
            if (!controller.regionModeEnabled || !controller.world.regionDriven || controller.world.progression.regionLoop != controller) errors.Add("Region mode / authority not connected.");
            if (scan) { controller.world.navigation.ConfigureGrid(); Physics2D.SyncTransforms(); controller.world.navigation.astar.Scan(); }
            var ids = new HashSet<string>();
            if (controller.definitions == null || controller.definitions.Length != 3) errors.Add("Exactly three region definitions are required.");
            else for (int i = 0; i < 3; i++)
            {
                var definition = controller.definitions[i];
                if (definition == null) { errors.Add("Missing region definition " + i); continue; }
                if (definition.regionId != regionIds[i] || definition.missionId != missionIds[i] || definition.exitGateId != gateIds[i] || definition.prerequisite != (i == 0 ? "" : missionIds[i-1])) errors.Add("Region/task/gate mapping mismatch: " + definition.name);
                if (!ids.Add("region/" + definition.regionId) || !ids.Add("mission/" + definition.missionId) || !ids.Add("encounter/" + definition.encounterId)) errors.Add("Duplicate region/reward ID: " + definition.name);
                var gates = controller.world.progression.gates.Where(g => g != null && g.gateId == definition.exitGateId).ToArray();
                if (gates.Length != 1 || gates[0].requirement != definition.missionId) errors.Add("Gate mapping conflict: " + definition.exitGateId);
                var device = controller.terminals?.SingleOrDefault(t => t != null && t.regionId == definition.regionId);
                if (device == null) { errors.Add("Missing device " + definition.regionId); continue; }
                if (!RegionSpawnSpace.Valid(controller.world, definition.regionId, device.transform.position, device.transform.position, .6f, true, out var why)) errors.Add("Device " + device.name + ": " + why);
                if (definition.enemies.Length == 0 || definition.residents.Length == 0 || definition.aliveLimit < 1 || definition.spawnRetryLimit < 1 || definition.spawnInterval <= 0 || definition.warningSeconds <= 0 || definition.patrolBudget < 0) errors.Add("Invalid finite encounter settings: " + definition.name);
                var types = new HashSet<EnemyType>();
                foreach (var profile in definition.enemies)
                    if (!types.Add(profile.type) || profile.health < 1 || profile.speed <= 0 || profile.experience < 1 || !controller.pool.TryGetConfiguredPrefab(profile.type, out _)) errors.Add("Invalid/unavailable profile: " + definition.name + "/" + profile.type);
                var waveIds = new HashSet<string>();
                foreach (var wave in definition.waves)
                {
                    if (string.IsNullOrEmpty(wave.id) || !waveIds.Add(wave.id) || wave.delay < 0 || wave.groups.Length == 0 || wave.delay >= definition.missionDeadline) errors.Add("Invalid wave " + definition.name);
                }
                foreach (var group in definition.residents.Concat(definition.waves.SelectMany(w => w.groups)))
                    if (group.count < 1 || definition.Profile(group.type) == null || (definition.Profile(group.type).special && definition.specialLimit < 1)) errors.Add("Invalid spawn group " + definition.name);
                if (i > 0 && (definition.waves.Length == 0 || definition.deviceSeconds <= 0 || definition.deviceIntegrity <= 0 || definition.deviceSeconds >= definition.missionDeadline)) errors.Add("Device defense/waves invalid: " + definition.name);
                if (i == 2 && !definition.waves.SelectMany(w => w.groups).Any(g => g.keyGuard && g.type == EnemyType.Heavy)) errors.Add("Power key guard missing.");
                var anchors = controller.anchors.Where(a => a != null && a.regionId == definition.regionId).ToArray();
                if (anchors.Length < 3) errors.Add("Too few ingress anchors: " + definition.regionId);
                foreach (var anchor in anchors)
                {
                    if (!ids.Add(anchor.stableId)) errors.Add("Duplicate anchor: " + anchor.stableId);
                    if (anchor.warning == null) errors.Add("Missing ingress warning: " + anchor.stableId);
                    if (!RegionSpawnSpace.Valid(controller.world, definition.regionId, anchor.transform.position, device.transform.position, definition.enemies.Max(p => p.clearance), true, out why)) errors.Add(anchor.stableId + ": " + why);
                }
            }
            if (All<EnemySpawner>(scene).Any(s => !s.regionDriven) || All<MissionManager>(scene).Any(s => !s.regionDriven) ||
                All<PlanetStormController>(scene).Any(s => !s.regionDriven) || All<ToxicFogRouteController>(scene).Any(s => !s.regionDriven) ||
                All<ExtractionController>(scene).Any(s => !s.regionDriven) || !queue.modules.regionDriven || queue.upgrades.regionRewards != queue || queue.modules.regionRewards != queue) errors.Add("Legacy automatic scheduling / reward queue remains disconnected.");
            if (All<L01Terminal>(scene).Any(t => t.enabled) || All<StormDamageController>(scene).Any(d => d.isActiveAndEnabled) ||
                All<ToxicFogDamageController>(scene).Any(d => d.isActiveAndEnabled) || controller.safeZone.HasActiveZone) errors.Add("Legacy terminal or environment hazard remains active.");
            if (All<CargoYardMissionEntryGate>(scene).Any(g => g.gameObject.activeInHierarchy) || All<EarlyExtractionEntryTrigger>(scene).Any(g => g.gameObject.activeInHierarchy)) errors.Add("Legacy trigger remains active.");
            if (errors.Count > 0) throw new InvalidOperationException("Region Loop validation failed:\n" + string.Join("\n", errors));
        }
        [Serializable] class ProtectedFiles { public string baseline; public ProtectedFile[] files; }
        [Serializable] class ProtectedFile { public string path, blob; }
        public static void ValidateProtected()
        {
            string path = ReportFolder + "/RegionLoop_ProtectedBaseline.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Protected asset baseline missing", path);
            var baseline = JsonUtility.FromJson<ProtectedFiles>(File.ReadAllText(path));
            var errors = new List<string>();
            foreach (var file in baseline.files)
                if (!File.Exists(file.path) || HashGitBlob(file.path) != file.blob) errors.Add(file.path);
            if (errors.Count > 0) throw new InvalidOperationException("Protected assets differ from committed baseline " + baseline.baseline + ":\n" + string.Join("\n", errors));
        }
        public static string HashGitBlob(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            // Match Git's text normalization on Windows (core.autocrlf=true in this project).
            // Binary Unity assets are hashed byte-for-byte.
            if (!bytes.Contains((byte)0))
            {
                using (var normalized = new MemoryStream(bytes.Length))
                {
                    for (int i = 0; i < bytes.Length; i++)
                        if (bytes[i] != 13 || i + 1 >= bytes.Length || bytes[i + 1] != 10) normalized.WriteByte(bytes[i]);
                    bytes = normalized.ToArray();
                }
            }
            using (var hash = SHA1.Create())
            {
                byte[] header = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
                hash.TransformBlock(header, 0, header.Length, header, 0);
                hash.TransformFinalBlock(bytes, 0, bytes.Length);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        public static void BatchInstallAndValidate() { Install(); Validate(); }
    }
}
