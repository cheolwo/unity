using System;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    public static class 통합SimulationWorldBuilder
    {
        private const string ShellScenePath = 통합WorldScenePolicy.CanonicalScenePath;

        [MenuItem("Ssalddel/통합 월드/전체 재생성")]
        public static void BuildAll()
        {
            SimulationWorldShellBuilder.BuildWorldShell();
            대한민국법정동WorldBuilder.Build();
            SimulationWorldShellBuilder.Build통합월드ModeNavigation();
            통합WorldScenePolicy.ApplyCanonicalBuildSettings();
            ValidateOpenScene();
            Debug.Log("UNIFIED-WORLD-1: Farm·1인칭·전술·Hub UI 통합 World 재생성을 완료했습니다.");
        }

        [MenuItem("Ssalddel/통합 월드/기존 Scene 통합 배선과 Build Settings 적용")]
        public static void IntegrateExistingScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ShellScenePath)
                EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            대한민국법정동WorldBuilder.Build대관령L2창고일인칭상호작용();
            SimulationWorldShellBuilder.Build통합월드ModeNavigation();
            통합WorldScenePolicy.ApplyCanonicalBuildSettings();
            ValidateOpenScene();
            Debug.Log("UNIFIED-WORLD-1: 기존 통합 Scene을 단일 플레이 진입점으로 정리했습니다.");
        }

        [MenuItem("Ssalddel/통합 월드/검증")]
        public static void ValidateOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ShellScenePath)
                throw new InvalidOperationException("UnifiedWorldSceneNotOpen");
            var player = UnityEngine.Object.FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldPlayerMissing");
            if (!player.ValidateWiring() || !player.PresentationOnly)
                throw new InvalidOperationException("UnifiedWorldPlayerInvalid");
            대한민국법정동WorldBuilder
                .Validate대관령L2창고일인칭상호작용OpenScene();
            var inbound = UnityEngine.Object.FindFirstObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldInboundUiMissing");
            inbound.ValidateWiring();
            var mode = UnityEngine.Object.FindFirstObjectByType<통합월드ModePresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldModePresenterMissing");
            mode.ValidateWiring();
            var officialWorld = GameObject.Find(
                "SimulationWorldShell/WorldMapRoot/OfficialRegionProjectionRoot")
                ?? throw new InvalidOperationException("UnifiedWorldOfficialProjectionMissing");
            var completionArea = officialWorld.transform.Find(
                "SpatialPipeline_EPSG5186_TileAreaSet/L4_L7_Synty경관_PresentationOnly/"
                + "CompletionArea_대관령면Farm_1km_L2_2x2");
            if (completionArea == null)
                throw new InvalidOperationException("UnifiedWorldFarmCompletionAreaMissing");
            var bar = GameObject.Find(
                "SimulationWorldShell/PersistentUI/UnifiedWorldModeCanvas/UnifiedWorldModeBar")
                ?? throw new InvalidOperationException("UnifiedWorldModeBarMissing");
            var modeCanvas = bar.GetComponentInParent<Canvas>();
            if (modeCanvas == null || modeCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                throw new InvalidOperationException("UnifiedWorldModeCanvasMustBeOverlay");
            if (bar.GetComponentsInChildren<Button>(true).Length != 4)
                throw new InvalidOperationException("UnifiedWorldModeButtonCountInvalid");

            var enabledScenes = EditorBuildSettings.scenes.Where(value => value.enabled).ToArray();
            if (enabledScenes.Length != 1 || enabledScenes[0].path != ShellScenePath)
                throw new InvalidOperationException("UnifiedWorldBuildEntryInvalid");
            통합WorldScenePolicy.ValidateCanonicalPolicy();
            Debug.Log("UNIFIED-WORLD-1 validation passed");
        }
    }
}
