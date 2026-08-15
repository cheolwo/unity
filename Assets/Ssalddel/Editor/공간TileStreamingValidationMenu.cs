using System;
using System.IO;
using System.Threading.Tasks;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 공간TileStreamingValidationMenu
    {
        public const string EvidencePath =
            "Documentation/Changes/2026-08-14-researched-l2-stream-window/"
            + "l2-researched-window-game-view.png";

        [MenuItem("Ssalddel/WORLD-STREAM-VISIBILITY-1/Play Mode 1인칭 검증 화면 준비")]
        public static async void PrepareFirstPersonEvidence()
        {
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("WorldTileStreamingCaptureRequiresPlayMode");
            var mode = UnityEngine.Object.FindAnyObjectByType<통합월드ModePresenter>(
                           FindObjectsInactive.Include)
                       ?? throw new InvalidOperationException("UnifiedWorldModePresenterMissing");
            var player = UnityEngine.Object.FindAnyObjectByType<플레이어경관Controller>(
                             FindObjectsInactive.Include)
                         ?? throw new InvalidOperationException("LegalWorldFarmPlayerMissing");
            var streaming = UnityEngine.Object.FindAnyObjectByType<공간TileStreamingController>(
                                FindObjectsInactive.Include)
                            ?? throw new InvalidOperationException("WorldTileStreamingControllerMissing");
            var visibility = UnityEngine.Object.FindAnyObjectByType<공간시야ObjectStreamingController>(
                                 FindObjectsInactive.Include)
                             ?? throw new InvalidOperationException("WorldVisibilityObjectControllerMissing");
            var diagnostic = UnityEngine.Object.FindAnyObjectByType<공간StreamingTreeDiagnosticPresenter>(
                                 FindObjectsInactive.Include)
                             ?? throw new InvalidOperationException("WorldStreamingDiagnosticMissing");
            mode.ShowFarmFirstPerson();

            var startedAt = Time.realtimeSinceStartup;
            while (!streaming.IsInitialized || streaming.PreparedTileCount != 81
                   || !visibility.IsInitialized || visibility.LoadedObjectCount != 5)
            {
                if (Time.realtimeSinceStartup - startedAt > 5f)
                    throw new InvalidOperationException("WorldTileStreamingEvidenceTimeout");
                await Task.Yield();
            }

            startedAt = Time.realtimeSinceStartup;
            while (visibility.DetailActiveCount == 0)
            {
                if (Time.realtimeSinceStartup - startedAt > 5f)
                    throw new InvalidOperationException("WorldVisibilityDetailEvidenceTimeout");
                await Task.Yield();
            }
            diagnostic.RefreshNow();

            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("WORLD-STREAM-VISIBILITY-1 실제 Play Mode 1인칭 Game View 저장 요청: " + absolute);
        }
    }
}
