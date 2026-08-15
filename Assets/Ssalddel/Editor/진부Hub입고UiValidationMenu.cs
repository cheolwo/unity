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
    public static class 진부Hub입고UiValidationMenu
    {
        public const string EvidencePath =
            "Assets/Documentation/Changes/2026-08-14-figma-maui-jinbu-inbound-ui/"
            + "jinbu-inbound-ui-completed.png";

        [MenuItem("Ssalddel/Validation/JINBU-INBOUND-UI-0 Fixture 완료 화면 저장")]
        public static async void CaptureFixtureCompletedGameView()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("JinbuInboundUiCaptureRequiresPlayMode");

            foreach (var composition in UnityEngine.Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                         FindObjectsInactive.Include))
                UnityEngine.Object.Destroy(composition);
            foreach (var composition in UnityEngine.Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                         FindObjectsInactive.Include))
                UnityEngine.Object.Destroy(composition);
            await Task.Yield();

            var presenter = UnityEngine.Object.FindAnyObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("JinbuInboundUiPresenterMissing");
            presenter.ForceVisibleForTests(true);
            await presenter.InitializeAsync(
                new 진부Hub입고UiFixtureAuthorityClient(),
                SimulationWorldShellFixture.SessionStableId);
            await presenter.RunGoldenPathAsync();
            if (presenter.CurrentProjection?.StateCode != 진부Hub입고UiCodes.Completed)
                throw new InvalidOperationException("JinbuInboundUiGoldenPathIncomplete");

            var absolutePath = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);
            Debug.Log("JINBU-INBOUND-UI-0 Game View 저장 요청: " + absolutePath);
        }
    }
}
