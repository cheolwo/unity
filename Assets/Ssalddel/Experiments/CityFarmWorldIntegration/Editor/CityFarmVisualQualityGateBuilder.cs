using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts.Cargo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class CityFarmVisualQualityGateBuilder
    {
        public const string SourceScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmCargoJourney.unity";
        public const string ScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmVisualQualityGate.unity";
        public const string IntegrationRootName = "WORLD-5 Visual Quality Gate";
        public const float SelectedZoneDistance = 26f;

        private const string WorldFocusAnchorId =
            "camera-focus:world.city-farm-supply-chain";

        [MenuItem("Ssalddel/WORLD-5/Build Visual Quality Gate")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new InvalidOperationException("WORLD5SourceSceneMissing");

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            var world = Require("WorldBootstrap");
            var previous = world.transform.Find(IntegrationRootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(IntegrationRootName);
            root.transform.SetParent(world.transform, false);

            ConfigureCamera();
            SuppressUnreadableWorldText();
            EmphasizeCargoMarkers();
            BuildHud(root);

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("WORLD5SceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("WORLD-5 visual quality gate created: " + ScenePath);
        }

        [MenuItem("Ssalddel/WORLD-5/Validate Visual Quality Gate")]
        public static void ValidateOpenScene()
        {
            CityFarmSyntyWorldBuilder.ValidateOpenScene();
            CityFarmBusinessViewIntegrationBuilder.ValidateOpenScene();
            CityFarmCargoJourneyBuilder.ValidateOpenScene();

            var quality = UnityEngine.Object.FindFirstObjectByType<WorldVisualQualityGateView>();
            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>();
            if (quality == null || !quality.ValidateApplied() || quality.StageCount != 4)
                throw new InvalidOperationException("WORLD5HudInvalid");
            if (rig == null || Math.Abs(rig.ConfiguredZoneDistance - SelectedZoneDistance) > .01f)
                throw new InvalidOperationException("WORLD5CameraCompositionInvalid");
            if (UnityEngine.Object.FindObjectsByType<TextMesh>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(value => value.gameObject.activeInHierarchy))
                throw new InvalidOperationException("WORLD5UnreadableWorldTextVisible");

            var instances = UnityEngine.Object.FindObjectsByType<WorldVisualInstanceView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (instances.Length < 80 || instances.Any(value => !value.ValidateWiring()
                || PrefabUtility.GetCorrespondingObjectFromSource(value.PrefabInstanceRoot) == null))
                throw new InvalidOperationException("WORLD5PrefabReferenceInvalid");
            var renderers = instances.SelectMany(value =>
                value.GetComponentsInChildren<Renderer>(true)).ToArray();
            if (renderers.Length == 0 || renderers.Any(value =>
                value.sharedMaterials.Any(material => material == null
                    || material.shader == null
                    || material.shader.name == "Hidden/InternalErrorShader")))
                throw new InvalidOperationException("WORLD5ShaderReferenceInvalid");

            var objects = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (objects.Any(value =>
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(value.gameObject) > 0))
                throw new InvalidOperationException("WORLD5MissingScriptReference");
        }

        private static void ConfigureCamera()
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new InvalidOperationException("WORLD5CameraRigMissing");
            rig.ConfigureComposition(
                50f,
                96f,
                SelectedZoneDistance,
                20f,
                35f,
                30f,
                28f);
            rig.Focus(WorldFocusAnchorId);
            rig.ApplyNowForTests();
            var occlusion = rig.GetComponent<DioramaForegroundOcclusionController>();
            if (occlusion != null) occlusion.ApplyNow();
            EditorUtility.SetDirty(rig);
        }

        private static void SuppressUnreadableWorldText()
        {
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                text.gameObject.SetActive(false);
            }
        }

        private static void EmphasizeCargoMarkers()
        {
            foreach (var anchor in OrderedAnchors())
            {
                var marker = anchor.transform.Find("CargoJourneyStateMarker")
                    ?? throw new InvalidOperationException("WORLD5CargoMarkerMissing:" + anchor.ZoneCode);
                marker.localPosition = new Vector3(0f, .88f, 0f);
                marker.localScale = new Vector3(1.30f, .12f, .48f);
            }
        }

        private static void BuildHud(GameObject root)
        {
            var shared = Require("WorldBootstrap/SharedPresentationCanvasAnchor").transform;
            var existing = shared.Find("WorldQualityPresentationCanvas");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var canvasObject = new GameObject("WorldQualityPresentationCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(shared, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;

            var header = Panel(canvasObject.transform, "JourneyHeader",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f),
                new Vector2(720f, 116f), new Vector2(0f, 1f), new Color(.06f, .08f, .11f, .91f));
            var title = Label(header, "Title", string.Empty, 30, TextAnchor.UpperLeft,
                new Vector2(18f, -12f), new Vector2(-36f, -16f), FontStyle.Bold);
            var status = Label(header, "Status", string.Empty, 19, TextAnchor.LowerLeft,
                new Vector2(18f, 12f), new Vector2(-36f, -58f), FontStyle.Normal);

            var boundaryPanel = Panel(canvasObject.transform, "AuthorityBoundary",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f),
                new Vector2(550f, 48f), new Vector2(1f, 1f), new Color(.06f, .08f, .11f, .90f));
            var boundary = Label(boundaryPanel, "Boundary", string.Empty, 14,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(-24f, -12f), FontStyle.Bold);

            var stageBar = Panel(canvasObject.transform, "CargoStages",
                new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 18f),
                new Vector2(1120f, 88f), new Vector2(.5f, 0f), new Color(.06f, .08f, .11f, .90f));
            var anchors = OrderedAnchors();
            var stages = new List<WorldQualityStageBinding>(4);
            for (var index = 0; index < anchors.Length; index++)
            {
                var x = -375f + index * 250f;
                var stage = Panel(stageBar, "Stage_" + anchors[index].ZoneCode,
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, 0f),
                    new Vector2(214f, 58f), new Vector2(.5f, .5f), new Color(.2f, .3f, .4f, .94f));
                var label = Label(stage, "Label", string.Empty, 17, TextAnchor.MiddleCenter,
                    Vector2.zero, new Vector2(-18f, -10f), FontStyle.Bold);
                stages.Add(new WorldQualityStageBinding
                {
                    ZoneCode = anchors[index].ZoneCode,
                    Background = stage.GetComponent<Image>(),
                    Label = label,
                });
                if (index < anchors.Length - 1)
                    FixedLabel(stageBar, "Arrow_" + index, "→", 24, TextAnchor.MiddleCenter,
                        new Vector2(x + 125f, 0f), new Vector2(36f, 46f), FontStyle.Bold);
            }

            var view = root.AddComponent<WorldVisualQualityGateView>();
            view.Configure(
                canvas,
                title,
                status,
                boundary,
                UnityEngine.Object.FindFirstObjectByType<CargoJourneyView>(),
                anchors,
                stages.ToArray());
            view.Apply();
        }

        private static CargoJourneyAnchorView[] OrderedAnchors()
        {
            var values = UnityEngine.Object.FindObjectsByType<CargoJourneyAnchorView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return new[]
            {
                values.Single(value => value.ZoneCode == CargoJourneyZoneCodes.FarmYard),
                values.Single(value => value.ZoneCode == CargoJourneyZoneCodes.TransportCorridor),
                values.Single(value => value.ZoneCode == CargoJourneyZoneCodes.UrbanLogistics),
                values.Single(value => value.ZoneCode == CargoJourneyZoneCodes.UrbanMarket),
            };
        }

        private static RectTransform Panel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot,
            Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            value.transform.SetParent(parent, false);
            var rect = value.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = value.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text Label(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            FontStyle style)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            target.transform.SetParent(parent, false);
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var text = target.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Text FixedLabel(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size,
            FontStyle style)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            target.transform.SetParent(parent, false);
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var text = target.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject Require(string path)
            => GameObject.Find(path)
               ?? throw new InvalidOperationException("WORLD5ObjectMissing:" + path);
    }
}
