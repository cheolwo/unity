using System;
using System.IO;
using System.Threading.Tasks;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 통합SimulationWorldValidationMenu
    {
        public const string EvidencePath =
            "Documentation/Changes/2026-08-14-simulation-world-integration/"
            + "unified-world-farm-hub-game-view.png";

        public const string FarmTacticalEvidencePath =
            "Documentation/Changes/2026-08-14-simulation-world-integration/"
            + "unified-world-farm-tactical-game-view.png";

        [MenuItem("Ssalddel/통합 월드/Play Mode 통합 화면 저장")]
        public static async void CaptureIntegratedGameView()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("UnifiedWorldCaptureRequiresPlayMode");
            foreach (var value in UnityEngine.Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                         FindObjectsInactive.Include))
                UnityEngine.Object.Destroy(value);
            foreach (var value in UnityEngine.Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                         FindObjectsInactive.Include))
                UnityEngine.Object.Destroy(value);
            await Task.Yield();

            var inbound = UnityEngine.Object.FindAnyObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldInboundUiMissing");
            await inbound.InitializeAsync(
                new 진부Hub입고UiFixtureAuthorityClient(),
                SimulationWorldShellFixture.SessionStableId);
            await inbound.RunGoldenPathAsync();
            var mode = UnityEngine.Object.FindAnyObjectByType<통합월드ModePresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldModePresenterMissing");
            mode.ShowJinbuInbound();

            var absolutePath = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);
            Debug.Log("UNIFIED-WORLD-1 Game View 저장 요청: " + absolutePath);
        }

        [MenuItem("Ssalddel/통합 월드/Play Mode 농장 전술 화면 저장")]
        public static void CaptureFarmTacticalGameView()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("UnifiedWorldCaptureRequiresPlayMode");

            var mode = UnityEngine.Object.FindAnyObjectByType<통합월드ModePresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldModePresenterMissing");
            mode.ShowFarmTactical();

            var absolutePath = Path.GetFullPath(FarmTacticalEvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);
            Debug.Log("UNIFIED-WORLD-1 농장 전술 Game View 저장 요청: " + absolutePath);
        }
    }
}
