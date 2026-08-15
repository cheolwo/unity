using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Ssalddel.Unity.Presentation.World;

namespace Ssalddel.Unity.Editor
{
    public static class 플레이어보행AnimationValidationMenu
    {
        private const string TargetTestName =
            "Ssalddel.Unity.Tests.PlayMode.플레이어경관PlayModeTests."
            + "Farm플레이어는_1인칭직접이동과_3인칭선택이동을_표현전용으로수행한다";
        private const string EvidenceDirectory =
            "Assets/Documentation/Changes/2026-08-13-synty-player-locomotion";
        private const string EvidencePath =
            EvidenceDirectory + "/synty-farm-player-locomotion-game-view.png";
        private static double _captureImportDeadline;

        [MenuItem("Ssalddel/Validation/PLAYER-LOCOMOTION-1 집중 Play Mode 검증")]
        public static void RunFocusedTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCallbacks());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { TargetTestName },
            }));
        }

        [MenuItem("Ssalddel/Validation/PLAYER-LOCOMOTION-1 현재 Game View 저장")]
        public static void CaptureCurrentGameView()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning(
                    "PLAYER-LOCOMOTION-1: Play Mode에서 현재 Game View 저장 메뉴를 실행해야 합니다.");
                return;
            }

            Directory.CreateDirectory(EvidenceDirectory);
            var controller = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var animationAdapter = controller == null
                ? null : controller.GetComponent<공용AnimationAdapter>();
            if (controller == null || animationAdapter == null
                || !animationAdapter.UsesFullBodyProceduralPose)
                throw new System.InvalidOperationException(
                    "PlayerLocomotionCaptureWiringInvalid");
            controller.EnterThirdPersonMode();
            controller.SetThirdPersonSelection(true);
            controller.SetThirdPersonDestination(
                controller.transform.position + new Vector3(3.2f, 0f, 2.4f));
            controller.TickThirdPersonMovement(.18f);
            animationAdapter.TickPresentation(.14f);
            foreach (var canvas in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
                canvas.gameObject.SetActive(false);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            _captureImportDeadline = EditorApplication.timeSinceStartup + 5d;
            ScreenCapture.CaptureScreenshot(EvidencePath, 1);
            EditorApplication.delayCall += ImportCapturedEvidence;
            Debug.Log("PLAYER-LOCOMOTION-1 Game View 저장 요청: " + EvidencePath);
        }

        private static void ImportCapturedEvidence()
        {
            if (!File.Exists(EvidencePath))
            {
                if (EditorApplication.timeSinceStartup < _captureImportDeadline)
                    EditorApplication.delayCall += ImportCapturedEvidence;
                else
                    Debug.LogWarning(
                        "PLAYER-LOCOMOTION-1 Game View 파일을 제한 시간 안에 찾지 못했습니다: "
                        + EvidencePath);
                return;
            }

            AssetDatabase.ImportAsset(EvidencePath, ImportAssetOptions.ForceUpdate);
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
                => Debug.Log(
                    $"PLAYER-LOCOMOTION-1 집중 검증 시작: {testsToRun.TestCaseCount}개");

            public void RunFinished(ITestResultAdaptor result)
                => Debug.Log(
                    "PLAYER-LOCOMOTION-1 집중 검증 완료: "
                    + $"통과 {result.PassCount}, 실패 {result.FailCount}, "
                    + $"건너뜀 {result.SkipCount}");

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                    Debug.LogError(
                        $"PLAYER-LOCOMOTION-1 실패: {result.FullName}\n{result.Message}");
            }
        }
    }
}
