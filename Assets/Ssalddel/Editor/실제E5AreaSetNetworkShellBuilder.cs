using System;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 별도 공식 Scene을 만들지 않고 canonical SimulationWorldShell에 실제 E5 Network를 더한다.
    /// 기존 월드 스트리밍 모판은 보존하고 이 Builder가 소유한 Root와 HUD만 재생성한다.
    /// </summary>
    public static class 실제E5AreaSetNetworkShellBuilder
    {
        public const string RuntimeRootName = "ActualE5NetworkRoot";
        public const string HudRootName = "ActualE5NetworkHud";
        private const string RuntimeSettingsPath =
            "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset";

        [MenuItem("Ssalddel/WORLD-E5-1/SimulationWorldShell에 실제 E5 Network 연결")]
        public static void BindToCanonicalShell()
        {
            OpenShell();
            var compositionRoot = UnityEngine.Object
                .FindFirstObjectByType<공간TileStreamingCompositionRoot>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "ActualE5RequiresWorldTileStreamingCompositionRoot");
            BindToOpenShell(compositionRoot, true);
        }

        public static void BindToOpenShell(
            공간TileStreamingCompositionRoot compositionRoot,
            bool saveScene)
        {
            if (compositionRoot == null)
                throw new ArgumentNullException(nameof(compositionRoot));
            var shell = GameObject.Find(SimulationWorldShellBuilder.RootName)
                        ?? throw new InvalidOperationException("SimulationWorldShellMissing");
            var shellRuntimeRoot = Required(shell.transform, "ShellRuntimeRoot");
            var hudCanvas = Required(shell.transform,
                "PersistentUI/SimulationWorldHud");
            RemoveOwnedChild(shellRuntimeRoot, RuntimeRootName);
            RemoveOwnedChild(hudCanvas, HudRootName);

            var runtimeRoot = new GameObject(RuntimeRootName).transform;
            runtimeRoot.SetParent(shellRuntimeRoot, false);
            var areaSetRoot = new GameObject("AreaSets_4").transform;
            areaSetRoot.SetParent(runtimeRoot, false);
            var routeGraphRoot = new GameObject("NetworkRouteGraphs_3").transform;
            routeGraphRoot.SetParent(runtimeRoot, false);

            var grammar = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                              공간문법CompositionCatalogBuilder.CatalogPath)
                          ?? throw new InvalidOperationException(
                              "ActualE5LandscapeGrammarCatalogMissing");
            grammar.Validate();
            var binding = AssetDatabase.LoadAssetAtPath<공간문법SyntyBindingCatalog>(
                공간문법SyntyBindingCatalogBuilder.CatalogPath);
            var hud = BuildHud(hudCanvas);
            var controller = runtimeRoot.gameObject
                .AddComponent<실제E5AreaSetNetworkController>();
            controller.Configure(
                areaSetRoot, routeGraphRoot, grammar, binding, hud, 24f);
            var shellPresenter = shell.GetComponentInChildren<SimulationWorldShellPresenter>(true)
                                 ?? throw new InvalidOperationException(
                                     "SimulationWorldShellPresenterMissing");
            controller.BindShell(shellPresenter);
            hud.ShowUnavailable("Simulation 서버의 실제 E5 공간 결속을 조회합니다");

            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                               RuntimeSettingsPath)
                           ?? throw new InvalidOperationException(
                               "UnityClientRuntimeSettingsMissing");
            compositionRoot.BindActualE5Network(controller, settings);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                if (!EditorSceneManager.SaveScene(
                        scene, SimulationWorldShellBuilder.ScenePath))
                    throw new InvalidOperationException("ActualE5ShellSceneSaveFailed");
                AssetDatabase.SaveAssets();
                ValidateOpenScene();
            }
            Selection.activeGameObject = runtimeRoot.gameObject;
            Debug.Log(
                "WORLD-E5-1: canonical SimulationWorldShell에 Nature 상시 + "
                + "Farm·Hub·Town 선택 적재, 3개 Network 경로 Graph와 "
                + "WI 30/5/6·지역 인과 HUD를 연결했습니다.");
        }

        [MenuItem("Ssalddel/WORLD-E5-1/Validate Open Scene")]
        public static void ValidateOpenScene()
        {
            var shell = GameObject.Find(SimulationWorldShellBuilder.RootName)
                        ?? throw new InvalidOperationException("SimulationWorldShellMissing");
            var runtimeRoot = Required(shell.transform,
                "ShellRuntimeRoot/" + RuntimeRootName);
            var controller = runtimeRoot.GetComponent<실제E5AreaSetNetworkController>()
                             ?? throw new InvalidOperationException(
                                 "ActualE5NetworkControllerMissing");
            if (runtimeRoot.Find("AreaSets_4") == null
                || runtimeRoot.Find("NetworkRouteGraphs_3") == null
                || controller.Hud == null)
                throw new InvalidOperationException("ActualE5NetworkWiringMissing");
            var compositionRoot = UnityEngine.Object
                .FindFirstObjectByType<공간TileStreamingCompositionRoot>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "ActualE5WorldTileStreamingCompositionRootMissing");
            if (!compositionRoot.실제E5Network연결됨)
                throw new InvalidOperationException("ActualE5NetworkNotBound");
            Required(shell.transform,
                "PersistentUI/SimulationWorldHud/" + HudRootName);
            Debug.Log("WORLD-E5-1 validation passed");
        }

        private static 실제E5AreaSetNetworkHudPresenter BuildHud(Transform canvas)
        {
            var panel = new GameObject(
                HudRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(22f, 22f);
            rect.sizeDelta = new Vector2(760f, 174f);
            panel.GetComponent<Image>().color = new Color(.025f, .045f, .065f, .92f);

            var network = CreateLine(panel.transform, "E5NetworkStatus", 0,
                20, FontStyle.Bold, new Color(.35f, .95f, .85f, 1f));
            var activeArea = CreateLine(panel.transform, "E5ActiveArea", 1,
                18, FontStyle.Normal, Color.white);
            var readiness = CreateLine(panel.transform, "E5InteractionReadiness", 2,
                18, FontStyle.Normal, new Color(.88f, .94f, .98f, 1f));
            var causality = CreateLine(panel.transform, "E5RegionalCausality", 3,
                18, FontStyle.Bold, new Color(1f, .84f, .42f, 1f));
            var presenter = panel.AddComponent<실제E5AreaSetNetworkHudPresenter>();
            presenter.Configure(network, activeArea, readiness, causality);
            return presenter;
        }

        private static Text CreateLine(
            Transform parent,
            string name,
            int index,
            int fontSize,
            FontStyle style,
            Color color)
        {
            var root = new GameObject(
                name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f - index * 39f);
            rect.sizeDelta = new Vector2(-36f, 34f);
            var text = root.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Transform Required(Transform parent, string path) =>
            parent.Find(path)
            ?? throw new InvalidOperationException("RequiredShellPathMissing:" + path);

        private static void RemoveOwnedChild(Transform parent, string name)
        {
            var previous = parent.Find(name);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
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
