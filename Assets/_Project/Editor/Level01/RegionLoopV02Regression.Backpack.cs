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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        const string BackpackReports = IntegratedWorldInstaller.Reports + "/Backpack";

        [MenuItem("AlienPurge/Gameplay/Level01/Run Integrated Backpack UI Checks")]
        public static void RunIntegratedBackpack()
        {
            Directory.CreateDirectory(BackpackReports);
            StartIntegrated(false, backpack: true);
        }

        static IEnumerator IntegratedBackpackChecks()
        {
            accelerate = choose = false;
            Time.timeScale = 1;
            yield return null;
            loop = Object.FindObjectOfType<RegionLoopController>();
            yield return Until(() => loop.State("ColonyEntry").initialized && RunInventory.Instance != null,
                "Integrated inventory and first region initialize");
            var backpack = BackpackPanelController.Instance;
            var canvas = backpack.GetComponentInParent<Canvas>();
            var panel = Read<GameObject>(backpack, "panelRoot");
            var status = canvas.transform.Find("CombatStatus");
            var mission = canvas.transform.Find("MissionPanel");
            yield return Until(() => mission.GetComponent<CanvasGroup>().alpha > .99f,
                "Native regional mission HUD is visible");
            var inventory = RunInventory.Instance;
            var scrap = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Data/Items/Item_ScrapAlloy.asset");
            Check(inventory.TryAddItem(scrap, 3, out int added, out int remaining) && added == 3 && remaining == 0,
                "Native inventory accepts the item fixture");
            backpack.OpenPanel();
            yield return null;
            Check(BackpackPanelController.IsOpen && panel.activeInHierarchy && Time.timeScale == 0,
                "Native backpack opens and pauses gameplay");

            // Reconstruct the previous sibling order only in Play Mode for a visual comparison.
            int correctedIndex = backpack.transform.GetSiblingIndex();
            backpack.transform.SetSiblingIndex(5);
            CaptureBackpack(canvas, "Before", 1920, 1080, false);
            backpack.transform.SetSiblingIndex(correctedIndex);
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 1024) })
                CaptureBackpack(canvas, "After", size.x, size.y, true);

            Canvas.ForceUpdateCanvases();
            var grid = Read<InventoryGridView>(backpack, "gridView");
            var item = grid.GetComponentsInChildren<InventoryItemView>().Single();
            var stack = inventory.Backpack.Items.Single();
            var rect = (RectTransform)item.transform;
            var click = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(click, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<InventoryItemView>() == item,
                "The visible inventory item wins the native UI raycast");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, click, ExecuteEvents.pointerClickHandler);
            Check(Read<ItemStack>(backpack, "selectedStack") == stack,
                "Native pointer click selects the item and updates backpack details");
            click.delta = new Vector2(grid.CellStep * (4 - stack.GridX), -grid.CellStep * (3 - stack.GridY)) * canvas.scaleFactor;
            ExecuteEvents.Execute(item.gameObject, click, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(item.gameObject, click, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(item.gameObject, click, ExecuteEvents.endDragHandler);
            yield return null;
            Check(stack.GridX == 4 && stack.GridY == 3 && stack.Quantity == 3,
                "Native drag moves the stack to an empty cell without losing items");
            item = grid.GetComponentsInChildren<InventoryItemView>().Single();
            Check(item.GetComponent<CanvasGroup>().blocksRaycasts && item.GetComponent<CanvasGroup>().alpha == 1,
                "Drag restores item opacity and pointer interaction");

            for (int i = 0; i < 3; i++)
            {
                backpack.ClosePanel();
                yield return null;
                Check(!BackpackPanelController.IsOpen && !panel.activeInHierarchy && Time.timeScale == 1 &&
                    status.gameObject.activeInHierarchy && mission.GetComponent<CanvasGroup>().alpha > .99f,
                    "Closing backpack restores gameplay and both corner HUDs, cycle " + (i + 1));
                backpack.OpenPanel();
                yield return null;
                Check(BackpackPanelController.IsOpen && Time.timeScale == 0, "Backpack reopens, cycle " + (i + 1));
            }
            var pause = Object.FindObjectOfType<PauseMenuController>();
            pause.PauseGame();
            yield return null;
            Check(PauseMenuController.IsPaused && !BackpackPanelController.IsOpen && !panel.activeInHierarchy && Time.timeScale == 0,
                "Native Pause closes the backpack and retains pause ownership");
            pause.ResumeGame();
            yield return null;
            Check(!PauseMenuController.IsPaused && Time.timeScale == 1, "Resume restores normal gameplay");
            CaptureBackpack(canvas, "Closed", 1920, 1080, false);
            ValidateTestProtection();
            results.Add("LIMITS: native Play Mode UI, raycasting, pointer handlers, pause and rendered cameras; deterministic item fixture. Before image reconstructs only the previous HUD sibling order in memory. No scene layout or save data was changed by the fixture.");
        }

        static void CaptureBackpack(Canvas canvas, string label, int width, int height, bool validate)
        {
            var captureObject = new GameObject("Backpack UI capture camera");
            var camera = captureObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height / 2f;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            var layers = canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            var previous = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var scaler = canvas.GetComponent<CanvasScaler>();
            var scale = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                camera.targetTexture = rt;
                foreach (var transform in layers.Keys) transform.gameObject.layer = LayerMask.NameToLayer("UI");
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                scale.Invoke(scaler, null);
                Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                camera.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(BackpackReports + "/" + label + "_" + width + "x" + height + ".png", tex.EncodeToPNG());
                if (validate)
                {
                    var background = canvas.transform.Find("BackPackUIController/BackPackPanelRoot/BackdropOpaque").GetComponent<Image>();
                    foreach (string corner in new[] { "CombatStatus", "MissionPanel" })
                    {
                        var graphics = canvas.transform.Find(corner).GetComponentsInChildren<Graphic>().Where(g => g.depth >= 0).ToArray();
                        Check(background.depth >= 0 && graphics.Length > 0 && graphics.All(g => g.depth < background.depth),
                            corner + " renders behind the opaque backpack at " + width + "x" + height);
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldDistance;
                foreach (var layer in layers) layer.Key.gameObject.layer = layer.Value;
                camera.targetTexture = null;
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(captureObject);
                scale.Invoke(scaler, null);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
