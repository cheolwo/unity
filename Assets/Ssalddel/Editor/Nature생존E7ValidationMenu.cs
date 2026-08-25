using System.IO;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// Nature 생활거점 Goal의 집중 PlayMode 시험과 실제 Game View 증거를 한 메뉴에 둔다.
    /// </summary>
    public static class Nature생존E7ValidationMenu
    {
        private const string TargetTestName =
            "Ssalddel.Unity.Tests.PlayMode.Nature생존E7폐루프PlayModeTests."
            + "SimulationWorldShell은_실제입력으로_Nature생활거점저장복원을완주한다";
        private const string EvidenceDirectory =
            "Assets/Documentation/Changes/2026-08-26-nature-e7-entry";
        private const string EvidencePath =
            EvidenceDirectory + "/nature-e7-axe-acquired-game-view.png";
        private static double captureImportDeadline;

        [MenuItem("Ssalddel/검증/NATURE-E7 집중 Play Mode 검증")]
        public static void RunFocusedTest()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCallbacks());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { TargetTestName },
            }));
        }

        [MenuItem("Ssalddel/검증/NATURE-E7 현재 Game View 저장 %#e")]
        public static void CaptureCurrentGameView()
        {
            if (!global::UnityEngine.Application.isPlaying)
                throw new System.InvalidOperationException(
                    "NatureE7GameViewRequiresPlayMode");
            var controller = Object.FindAnyObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            if (controller == null || player == null
                || !controller.ValidateWiring()
                || !controller.State.HasAxe
                || !Nature생존Controller.CanUseNatureInput(
                    player.CurrentActivityCode))
                throw new System.InvalidOperationException(
                    "NatureE7GameViewStateInvalid");

            Directory.CreateDirectory(EvidenceDirectory);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            captureImportDeadline = EditorApplication.timeSinceStartup + 5d;
            ScreenCapture.CaptureScreenshot(EvidencePath, 1);
            EditorApplication.delayCall += ImportCapturedEvidence;
            Debug.Log("NATURE-E7 Game View 저장 요청: " + EvidencePath);
        }

        private static void ImportCapturedEvidence()
        {
            if (!File.Exists(EvidencePath))
            {
                if (EditorApplication.timeSinceStartup < captureImportDeadline)
                    EditorApplication.delayCall += ImportCapturedEvidence;
                else
                    Debug.LogWarning(
                        "NATURE-E7 Game View 파일을 제한 시간 안에 찾지 못했습니다: "
                        + EvidencePath);
                return;
            }
            AssetDatabase.ImportAsset(EvidencePath,
                ImportAssetOptions.ForceUpdate);
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
                => Debug.Log(
                    $"NATURE-E7 집중 검증 시작: {testsToRun.TestCaseCount}개");

            public void RunFinished(ITestResultAdaptor result)
                => Debug.Log(
                    "NATURE-E7 집중 검증 완료: "
                    + $"통과 {result.PassCount}, 실패 {result.FailCount}, "
                    + $"건너뜀 {result.SkipCount}");

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                    Debug.LogError(
                        $"NATURE-E7 실패: {result.FullName}\n{result.Message}");
            }
        }
    }
}
