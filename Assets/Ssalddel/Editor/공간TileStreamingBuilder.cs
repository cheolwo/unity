using System;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    public static class 공간TileStreamingBuilder
    {
        public const string RootName = "L11_동적공간TileStreaming_PresentationOnly";

        [MenuItem("Ssalddel/WORLD-STREAM-VISIBILITY-1/SimulationWorldShell에 시야 기반 월드 연결")]
        public static void IntegrateIntoSimulationWorldShell()
        {
            OpenShell();
            var player = UnityEngine.Object.FindFirstObjectByType<플레이어경관Controller>(
                             FindObjectsInactive.Include)
                         ?? throw new InvalidOperationException("LegalWorldFarmPlayerMissing");
            var parent = player.transform.parent
                         ?? throw new InvalidOperationException("SpatialPipelineRootMissing");
            var previous = parent.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            var visualRoot = new GameObject("DynamicTileBoundaryPool").transform;
            visualRoot.SetParent(root.transform, false);
            var objectVisualRoot = new GameObject("DynamicVisibleObjectPool").transform;
            objectVisualRoot.SetParent(root.transform, false);

            var canvasRoot = new GameObject(
                "DynamicTileStreamingStatusCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(root.transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 410;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var panel = new GameObject(
                "동적타일상태Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasRoot.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(22f, -120f);
            panelRect.sizeDelta = new Vector2(560f, 136f);
            panel.GetComponent<Image>().color = new Color(.025f, .045f, .055f, .9f);

            var accent = new GameObject(
                "자료대기Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, .5f);
            accentRect.sizeDelta = new Vector2(7f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
            accent.GetComponent<Image>().color = new Color(.18f, .92f, .82f, 1f);

            var labelObject = new GameObject(
                "동적타일상태Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(panel.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(22f, 10f);
            labelRect.offsetMax = new Vector2(-12f, -10f);
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.lineSpacing = 1.08f;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(.9f, .97f, .96f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = "동적 공간 타일 준비 중\n실제 DEM·배치 마스크 자료 대기 · 경계 표현만 사용합니다.";

            var controller = root.AddComponent<공간TileStreamingController>();
            controller.Configure(
                player.transform,
                visualRoot,
                label,
                player.transform.position,
                24f,
                .16f);

            var gate = root.AddComponent<공간안전이동Gate>();
            gate.Configure(controller, ~0, true);
            player.ConfigureMovementGate(gate);

            var catalog = AssetDatabase.LoadAssetAtPath<법정동경관VisualCatalog>(
                              대한민국법정동WorldBuilder.CatalogPath)
                          ?? throw new InvalidOperationException("LegalDongScenicCatalogMissing");
            var objectController = root.AddComponent<공간시야ObjectStreamingController>();
            objectController.Configure(
                player.transform,
                player,
                controller,
                catalog,
                objectVisualRoot);

            var diagnosticPanel = CreateDiagnosticPanel(canvasRoot.transform, out var treeText);
            var diagnosticButton = CreateDiagnosticButton(canvasRoot.transform, out var buttonText);
            var diagnostic = root.AddComponent<공간StreamingTreeDiagnosticPresenter>();
            diagnostic.Configure(
                controller, objectController, gate,
                diagnosticPanel, treeText, buttonText);
            UnityEventTools.AddPersistentListener(
                diagnosticButton.onClick, diagnostic.Toggle);

            root.AddComponent<공간TileStreamingCompositionRoot>()
                .Configure(controller, objectController, null, false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                "WORLD-STREAM-VISIBILITY-1: 3x3 활성·5x5 준비 타일, 안전 이동 경계, "
                + "시야 기반 건물 프록시→Synty 상세 승격, 런타임 진단 트리를 연결했습니다.");
        }

        private static GameObject CreateDiagnosticPanel(Transform canvas, out Text treeText)
        {
            var panel = new GameObject(
                "시야Streaming진단TreePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -88f);
            rect.sizeDelta = new Vector2(660f, 590f);
            panel.GetComponent<Image>().color = new Color(.018f, .03f, .043f, .92f);

            var header = CreateText(panel.transform, "진단TreeHeader", 26, FontStyle.Bold);
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(.5f, 1f);
            headerRect.offsetMin = new Vector2(26f, -66f);
            headerRect.offsetMax = new Vector2(-26f, -16f);
            header.text = "WORLD STREAM · 수평 창과 수직 생성 절차";
            header.color = new Color(.3f, .96f, .84f, 1f);

            treeText = CreateText(panel.transform, "시야Streaming진단TreeText", 20, FontStyle.Normal);
            var treeRect = treeText.rectTransform;
            treeRect.anchorMin = Vector2.zero;
            treeRect.anchorMax = Vector2.one;
            treeRect.offsetMin = new Vector2(28f, 24f);
            treeRect.offsetMax = new Vector2(-22f, -76f);
            treeText.alignment = TextAnchor.UpperLeft;
            treeText.lineSpacing = 1.16f;
            treeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            treeText.verticalOverflow = VerticalWrapMode.Truncate;
            treeText.color = new Color(.89f, .94f, .97f, 1f);
            return panel;
        }

        private static Button CreateDiagnosticButton(Transform canvas, out Text buttonText)
        {
            var root = new GameObject(
                "시야Streaming진단TreeToggle", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-22f, -22f);
            rect.sizeDelta = new Vector2(250f, 48f);
            root.GetComponent<Image>().color = new Color(.08f, .32f, .34f, .96f);

            buttonText = CreateText(root.transform, "ToggleText", 18, FontStyle.Bold);
            buttonText.rectTransform.anchorMin = Vector2.zero;
            buttonText.rectTransform.anchorMax = Vector2.one;
            buttonText.rectTransform.offsetMin = Vector2.zero;
            buttonText.rectTransform.offsetMax = Vector2.zero;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.text = "진단 트리 닫기  F8";
            return root.GetComponent<Button>();
        }

        private static Text CreateText(
            Transform parent, string name, int fontSize, FontStyle style)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            return text;
        }

        private static void OpenShell()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == SimulationWorldShellBuilder.ScenePath) return;
            EditorSceneManager.OpenScene(
                SimulationWorldShellBuilder.ScenePath, OpenSceneMode.Single);
        }
    }
}
