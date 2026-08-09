using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.WorldMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    public static class WorldBootstrapSceneBuilder
    {
        [MenuItem("Ssalddel/Build World Bootstrap Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("SsalddelWorld");
            var presenter = world.AddComponent<PublicWorldMapPresenter>();
            var compositionRoot = world.AddComponent<WorldBootstrapSceneCompositionRoot>();

            var canvas = new GameObject("WorldMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);

            var statusPanel = CreatePanel(canvas.transform, "WorldMapStatusPanel", new Vector2(20, -20), new Vector2(430, 190), new Vector2(0, 1));
            var statusText = CreateText(statusPanel.transform, "StatusText", new Vector2(16, -14), new Vector2(398, 38), 22, TextAnchor.MiddleLeft);
            var metadataText = CreateText(statusPanel.transform, "MetadataText", new Vector2(16, -58), new Vector2(398, 70), 15, TextAnchor.UpperLeft);
            var retryButton = CreateButton(statusPanel.transform, "RetryButton", "다시 시도", new Vector2(16, -142));
            var refreshButton = CreateButton(statusPanel.transform, "RefreshButton", "새로고침", new Vector2(150, -142));
            var sceneView = statusPanel.AddComponent<PublicWorldMapSceneView>();
            sceneView.Configure(statusText, metadataText, retryButton, refreshButton);

            var detailRoot = CreatePanel(canvas.transform, "ObservationDetailPanel", new Vector2(-20, -20), new Vector2(420, 480), new Vector2(1, 1));
            var detailTitle = CreateText(detailRoot.transform, "DetailTitle", new Vector2(18, -18), new Vector2(384, 58), 24, TextAnchor.UpperLeft);
            var detailBody = CreateText(detailRoot.transform, "DetailBody", new Vector2(18, -82), new Vector2(384, 320), 16, TextAnchor.UpperLeft);
            var detailButton = CreateButton(detailRoot.transform, "OpenDetailButton", "상세 보기", new Vector2(18, -430));
            var closeButton = CreateButton(detailRoot.transform, "CloseDetailButton", "닫기", new Vector2(152, -430));
            var detailPanel = detailRoot.AddComponent<PublicWorldMapDetailPanel>();
            detailPanel.Configure(detailRoot, detailTitle, detailBody, detailButton, closeButton);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            const string settingsPath = "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(settingsPath);
            if (settings == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Ssalddel/Settings");
                settings = ScriptableObject.CreateInstance<UnityClientRuntimeSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            compositionRoot.Configure(settings, presenter, sceneView, detailPanel);

            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "PublicWorldMapSurface";
            surface.transform.SetParent(world.transform, false);
            surface.transform.localScale = new Vector3(1.8f, 1f, .9f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 12, -8);
            camera.transform.rotation = Quaternion.Euler(55, 0, 0);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            const string scenePath = "Assets/Ssalddel/Scenes/WorldBootstrapScene.unity";
            System.IO.Directory.CreateDirectory("Assets/Ssalddel/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = new Color(.06f, .08f, .12f, .92f);
            return panel;
        }

        private static Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(120, 36);
            item.GetComponent<Image>().color = new Color(.15f, .48f, .82f, 1f);
            var labelText = CreateText(item.transform, "Label", Vector2.zero, rect.sizeDelta, 16, TextAnchor.MiddleCenter);
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.pivot = new Vector2(.5f, .5f);
            labelText.rectTransform.anchoredPosition = Vector2.zero;
            labelText.rectTransform.sizeDelta = Vector2.zero;
            labelText.text = label;
            return item.GetComponent<Button>();
        }
    }
}
