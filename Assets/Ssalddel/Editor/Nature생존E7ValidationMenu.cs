using System.IO;
using System.Linq;
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
        private const string SensoryTestName =
            "Ssalddel.Unity.Tests.PlayMode.Nature감각표현PlayModeTests."
            + "권위상태사본이_도끼벌목AnimationAudioFx로만투영된다";
        private const string InputBoundaryTestName =
            "Ssalddel.Unity.Tests.EditMode.통합SimulationWorldTests."
            + "일인칭커서는_UI버튼입력으로_다시잠기지않는다";
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
                testNames = new[] { TargetTestName, SensoryTestName },
            }));
        }

        [MenuItem("Ssalddel/검증/NATURE-E7 입력 경계 EditMode 검증")]
        public static void RunInputBoundaryTest()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCallbacks());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { InputBoundaryTestName },
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

        [MenuItem("Ssalddel/검증/NATURE-E7 진입 목표 진단 %#d")]
        public static void LogCurrentEntryTarget()
        {
            if (!global::UnityEngine.Application.isPlaying)
                throw new System.InvalidOperationException(
                    "NatureE7EntryDiagnosticRequiresPlayMode");
            var controller = Object.FindAnyObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            if (controller == null || player == null || !controller.ValidateWiring())
                throw new System.InvalidOperationException(
                    "NatureE7EntryDiagnosticWiringInvalid");

            var snapshot = controller.State;
            var markers = Object.FindObjectsByType<Nature생존상호작용표식>(
                FindObjectsInactive.Exclude);
            var target = !snapshot.HasAxe
                ? markers.FirstOrDefault(value =>
                    value.Kind == Nature생존상호작용Kind.Axe)
                : markers.Where(value =>
                        value.Kind == Nature생존상호작용Kind.HarvestTree)
                    .OrderBy(value => Vector3.Distance(
                        player.transform.position, value.transform.position))
                    .ThenBy(value => value.StableId,
                        System.StringComparer.Ordinal)
                    .FirstOrDefault();
            target ??= markers.FirstOrDefault(value =>
                value.Kind == Nature생존상호작용Kind.Cabin);
            if (target == null)
                throw new System.InvalidOperationException(
                    "NatureE7EntryDiagnosticTargetMissing");

            var collider = target.GetComponentInChildren<Collider>();
            var aimPoint = collider == null
                ? target.transform.position : collider.bounds.center;
            var camera = player.FirstPersonCamera;
            var direction = aimPoint - camera.transform.position;
            Debug.Log("NATURE-E7 진입 목표 진단: "
                + $"kind={target.Kind};stableId={target.StableId};"
                + $"player={player.transform.position};target={aimPoint};"
                + $"distance={Vector3.Distance(player.transform.position, aimPoint):F3};"
                + $"angle={Vector3.Angle(camera.transform.forward, direction):F3};"
                + $"screen={camera.WorldToScreenPoint(aimPoint)};"
                + $"activity={player.CurrentActivityCode};"
                + $"cursor={Cursor.lockState}/{Cursor.visible}");
        }

        [MenuItem("Ssalddel/검증/NATURE-E7 음향 경계 진단")]
        public static void LogCurrentAudioBoundary()
        {
            if (!global::UnityEngine.Application.isPlaying)
                throw new System.InvalidOperationException(
                    "NatureE7AudioDiagnosticRequiresPlayMode");
            var controller = Object.FindAnyObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include);
            var presenter = controller == null
                ? null : controller.GetComponent<Nature감각표현Presenter>();
            if (presenter == null || !presenter.IsConfigured)
                throw new System.InvalidOperationException(
                    "NatureE7AudioDiagnosticWiringInvalid");

            var configuration = AudioSettings.GetConfiguration();
            var sources = presenter.GetComponentsInChildren<AudioSource>(true)
                .OrderBy(value => value.gameObject.name,
                    System.StringComparer.Ordinal)
                .Select(value => value.gameObject.name
                    + ":clip=" + (value.clip == null
                        ? "Pending" : value.clip.name)
                    + ":playing=" + value.isPlaying
                    + ":loop=" + value.loop
                    + ":spatial=" + value.spatialBlend.ToString("F1"));
            Debug.Log("NATURE-E7 음향 경계 진단: "
                + $"sampleRate={configuration.sampleRate};"
                + $"speakerMode={configuration.speakerMode};"
                + $"dspBuffer={configuration.dspBufferSize};"
                + $"listenerVolume={AudioListener.volume:F2};"
                + $"listenerPaused={AudioListener.pause};"
                + $"proceduralActionCues={presenter.ProceduralActionCueCount};"
                + $"optionalAreaAudio={presenter.OptionalAreaAudioAvailable};"
                + $"ambientCue={presenter.AreaAmbientCueCode};"
                + $"musicCue={presenter.AreaMusicCueCode};"
                + $"advisory={string.Join(",", presenter.Receipt.AdvisoryCodes)};"
                + "sources=[" + string.Join("|", sources) + "]");
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
