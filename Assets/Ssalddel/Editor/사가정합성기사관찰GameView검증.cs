using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Warehouse;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// canonical SimulationWorldShell을 저장하지 않고 실제 synthetic-delivery.r1
    /// Aggregate 사본을 사가정 합성 기사 관찰 계층에 적용해 Game View 증거를 만든다.
    /// 화면 선택과 표시 토글은 Simulation Command 또는 운영 쓰기를 호출하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class SagajeongSyntheticCourierObservationCliCapture
    {
        public const string ExecuteMethod =
            "Ssalddel.Unity.Editor.SagajeongSyntheticCourierObservationCliCapture.Run";
        public const string CommandLineMarker =
            "-sagajeongSyntheticCourierObservationCliCapture";
        public const string OutputArgument =
            "-sagajeongSyntheticCourierObservationOutput";
        public const string SourceProjectPathArgument =
            "-sagajeongSyntheticCourierObservationSourceProjectPath";
        public const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        private static readonly Guid SessionId = Guid.Parse(
            "3f905be8-4980-48e7-a2d1-36e1412fba12");
        private static readonly Vector3 DioramaCoordinateOrigin =
            new(550f, 0f, 8f);
        private const float DioramaCoordinateScale = 1f;
        private const float SelectedOrthographicSize = 34f;
        private const string SessionPrefix =
            "Ssalddel.SagajeongSyntheticCourierObservationCliCapture.";
        private const string ActiveKey = SessionPrefix + "Active";
        private const string PhaseKey = SessionPrefix + "Phase";
        private const string FrameKey = SessionPrefix + "Frame";
        private const string PreparedKey = SessionPrefix + "Prepared";
        private const string OutputKey = SessionPrefix + "Output";
        private const string RunStartedKey = SessionPrefix + "RunStarted";
        private const string SceneHashKey = SessionPrefix + "SceneHash";
        private const string SourceProjectPathKey =
            SessionPrefix + "SourceProjectPath";
        private const string DeadlineKey = SessionPrefix + "Deadline";
        private const int FailureExitCode = 2;

        private static 경영SimulationSessionAggregate aggregate;
        private static 경영SimulationSessionSnapshot authorityState;
        private static 사가정합성기사관찰Snapshot initialProjection;
        private static 사가정합성기사관찰Snapshot latestProjection;
        private static 사가정합성Actor관찰Layer observationLayer;
        private static Vector3 actorWorldPosition;
        private static string initialAuthorityHash = string.Empty;
        private static string initialAuthorityHashAfterSelection = string.Empty;
        private static string latestAuthorityHash = string.Empty;
        private static string latestAuthorityHashAfterPresentation = string.Empty;
        private static bool programmaticScreenSelectionVerified;
        private static bool hiddenHigherRevisionAccepted;
        private static bool reenabledLatestVisible;
        private static bool initialProjectionFingerprintUnchanged;
        private static bool latestProjectionFingerprintUnchanged;
        private static double actorPositionDeltaXWhileHidden;
        private static double actorPositionDeltaZWhileHidden;
        private static double actorMovementDistanceWhileHidden;
        private static bool actorPositionChangedWhileHidden;

        private enum Phase
        {
            None,
            AwaitingPlayMode,
            CaptureOverview,
            CaptureSelectedClose,
            ValidateAndStop,
            StoppingSuccess,
            StoppingFailure,
        }

        static SagajeongSyntheticCourierObservationCliCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!HasArgument(CommandLineMarker))
            {
                Debug.LogError(
                    "[SagajeongSyntheticCourierObservationCliCapture] 전용 명령줄 표식이 없습니다.");
                return;
            }

            try
            {
                if (UnityEngine.Application.isBatchMode || HasArgument("-nographics"))
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationCaptureRequiresGraphicsProcess");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationCaptureRequiresEditModeStart");
                if (EditorSceneManager.GetActiveScene().isDirty)
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationDirtySceneAtStart");

                var sourceProjectArgument = ReadArgument(
                    SourceProjectPathArgument);
                if (string.IsNullOrWhiteSpace(sourceProjectArgument))
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationSourceProjectPathRequired");
                var sourceProjectPath = NormalizeDirectoryPath(
                    sourceProjectArgument);
                var validationProjectPath = NormalizeDirectoryPath(Path.Combine(
                    UnityEngine.Application.dataPath, ".."));
                if (!Directory.Exists(sourceProjectPath))
                    throw new DirectoryNotFoundException(
                        "SagajeongSyntheticCourierObservationSourceProjectPathMissing: "
                        + sourceProjectPath);
                if (string.Equals(sourceProjectPath, validationProjectPath,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationSourceAndValidationProjectMustDiffer");

                var output = ReadArgument(OutputArgument);
                if (string.IsNullOrWhiteSpace(output))
                    output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                        "..", "artifacts", "local", "validation",
                        "sagajeong-synthetic-courier-observation-r20", "gameview"));
                Directory.CreateDirectory(output);
                var evidenceNames = new[]
                {
                    "01-overview.png",
                    "02-selected-card-close.png",
                    "capture-manifest.json",
                };
                if (evidenceNames.Any(name => File.Exists(Path.Combine(output, name))))
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationOutputMustBeFresh");

                var scenePath = CanonicalSceneFullPath();
                if (!File.Exists(scenePath))
                    throw new FileNotFoundException(
                        "SagajeongSyntheticCourierObservationCanonicalSceneMissing",
                        scenePath);

                ResetRuntimeFields();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(PhaseKey, (int)Phase.AwaitingPlayMode);
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetBool(PreparedKey, false);
                SessionState.SetString(OutputKey, output);
                SessionState.SetString(RunStartedKey,
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                SessionState.SetString(SceneHashKey, Sha256File(scenePath));
                SessionState.SetString(SourceProjectPathKey, sourceProjectPath);
                SessionState.SetString(DeadlineKey,
                    DateTime.UtcNow.AddMinutes(12).Ticks.ToString(
                        CultureInfo.InvariantCulture));

                var scene = EditorSceneManager.OpenScene(CanonicalScenePath,
                    OpenSceneMode.Single);
                if (!scene.IsValid() || scene.isDirty)
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationCanonicalSceneOpenFailed");
                Debug.Log(
                    "[SagajeongSyntheticCourierObservationCliCapture] 시작 · Scene 저장 없음 · "
                    + "실제 합성 Simulation Aggregate 읽기 사본 · 운영 쓰기 없음");
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Fail("StartFailed", exception);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                if (DateTime.UtcNow.Ticks > long.Parse(
                        SessionState.GetString(DeadlineKey, "0"),
                        CultureInfo.InvariantCulture))
                    throw new TimeoutException(
                        "SagajeongSyntheticCourierObservationCaptureTimedOut");

                switch ((Phase)SessionState.GetInt(PhaseKey, 0))
                {
                    case Phase.AwaitingPlayMode:
                        AwaitPlayMode();
                        break;
                    case Phase.CaptureOverview:
                        CaptureOverview();
                        break;
                    case Phase.CaptureSelectedClose:
                        CaptureSelectedClose();
                        break;
                    case Phase.ValidateAndStop:
                        ValidateAndStop();
                        break;
                    case Phase.StoppingSuccess:
                        ExitWhenStopped(0);
                        break;
                    case Phase.StoppingFailure:
                        ExitWhenStopped(FailureExitCode);
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail("TickFailed", exception);
            }
        }

        private static void AwaitPlayMode()
        {
            if (!EditorApplication.isPlaying) return;
            var view = FindView();
            if (view == null) return;
            view.Initialize();
            if (view.MapStatus != "Ready" || view.ObservationCamera == null) return;
            if (!WaitFrames(24)) return;

            view.GameOverlayVisible = false;
            view.Show(true);
            view.ShowWhole();
            aggregate = new 경영SimulationSessionAggregate(
                가상배달관찰표본.Create(SessionId));
            authorityState = AdvanceToDriveHome(aggregate);
            initialAuthorityHash = AuthorityHash(authorityState);
            initialProjection = new 사가정합성기사관찰SnapshotFactory()
                .생성(authorityState);
            var initialFingerprint = initialProjection.ProjectionFingerprintSha256;

            observationLayer = view.gameObject.AddComponent<사가정합성Actor관찰Layer>();
            observationLayer.Configure(view.ObservationCamera,
                DioramaCoordinateOrigin, DioramaCoordinateScale);
            var initialApply = observationLayer.Apply(initialProjection);
            if (!initialApply.Accepted || !initialApply.Changed
                || observationLayer.AppliedRevision
                != initialProjection.SourceSessionRevision)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationInitialApplyFailed");

            actorWorldPosition = view.transform.TransformPoint(
                DioramaCoordinateOrigin + new Vector3(
                    (float)initialProjection.ActorX * DioramaCoordinateScale,
                    2.2f,
                    (float)initialProjection.ActorZ * DioramaCoordinateScale));
            var screen = view.ObservationCamera.WorldToScreenPoint(actorWorldPosition);
            programmaticScreenSelectionVerified = screen.z > 0f
                && observationLayer.TrySelectActorAtScreen(
                    new Vector2(screen.x, screen.y))
                && observationLayer.SelectedActorStableId
                == 사가정합성기사관찰Policy.ActorStableId;
            if (!programmaticScreenSelectionVerified)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationScreenSelectionFailed");
            initialAuthorityHashAfterSelection = AuthorityHash(aggregate.Snapshot());
            initialProjectionFingerprintUnchanged = string.Equals(
                initialFingerprint,
                사가정합성기사관찰Fingerprint.Compute(initialProjection),
                StringComparison.Ordinal);
            if (!string.Equals(initialAuthorityHash,
                    initialAuthorityHashAfterSelection, StringComparison.Ordinal)
                || !initialProjectionFingerprintUnchanged)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationSelectionChangedAuthority");

            observationLayer.CloseCard();
            observationLayer.SetDisplayVisible(false);
            authorityState = aggregate.Advance(new 경영SimulationTick진행Request
            {
                CommandId = "sagajeong-synthetic-courier-observation:hidden-next",
                ExpectedRevision = authorityState.Revision,
                TickCount = 1
            });
            latestAuthorityHash = AuthorityHash(authorityState);
            latestProjection = new 사가정합성기사관찰SnapshotFactory()
                .생성(authorityState, initialProjection.SourceSessionRevision + 1);
            actorPositionDeltaXWhileHidden = latestProjection.ActorX
                                             - initialProjection.ActorX;
            actorPositionDeltaZWhileHidden = latestProjection.ActorZ
                                             - initialProjection.ActorZ;
            actorMovementDistanceWhileHidden = Math.Sqrt(
                actorPositionDeltaXWhileHidden * actorPositionDeltaXWhileHidden
                + actorPositionDeltaZWhileHidden * actorPositionDeltaZWhileHidden);
            actorPositionChangedWhileHidden =
                actorMovementDistanceWhileHidden > 0d;
            if (latestProjection.CurrentTick != initialProjection.CurrentTick + 1
                || !actorPositionChangedWhileHidden)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationAggregateDidNotMoveActorWhileHidden");
            var latestFingerprint = latestProjection.ProjectionFingerprintSha256;
            var hiddenApply = observationLayer.Apply(latestProjection);
            hiddenHigherRevisionAccepted = hiddenApply.Accepted && hiddenApply.Changed
                && !observationLayer.EffectiveVisible
                && observationLayer.AppliedRevision
                == latestProjection.SourceSessionRevision;
            observationLayer.SetDisplayVisible(true);
            reenabledLatestVisible = observationLayer.EffectiveVisible
                && observationLayer.HasAcceptedObservation
                && observationLayer.AppliedRevision
                == latestProjection.SourceSessionRevision
                && string.Equals(observationLayer.AppliedFingerprint,
                    latestFingerprint, StringComparison.Ordinal);
            latestAuthorityHashAfterPresentation = AuthorityHash(aggregate.Snapshot());
            latestProjectionFingerprintUnchanged = string.Equals(
                latestFingerprint,
                사가정합성기사관찰Fingerprint.Compute(latestProjection),
                StringComparison.Ordinal);
            if (!hiddenHigherRevisionAccepted || !reenabledLatestVisible
                || !string.Equals(latestAuthorityHash,
                    latestAuthorityHashAfterPresentation, StringComparison.Ordinal)
                || !latestProjectionFingerprintUnchanged)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationHiddenRefreshFailed");

            actorWorldPosition = view.transform.TransformPoint(
                DioramaCoordinateOrigin + new Vector3(
                    (float)latestProjection.ActorX * DioramaCoordinateScale,
                    2.2f,
                    (float)latestProjection.ActorZ * DioramaCoordinateScale));
            SetPhase(Phase.CaptureOverview);
        }

        private static void CaptureOverview()
        {
            if (!SessionState.GetBool(PreparedKey, false))
            {
                var view = RequireView();
                view.GameOverlayVisible = false;
                view.Show(true);
                view.ShowWhole();
                observationLayer.CloseCard();
                BeginPreparedWait();
                return;
            }
            if (!WaitFrames(18)) return;
            Capture("01-overview.png");
            SetPhase(Phase.CaptureSelectedClose);
        }

        private static void CaptureSelectedClose()
        {
            if (!WaitForCapture("01-overview.png")) return;
            if (!SessionState.GetBool(PreparedKey, false))
            {
                var view = RequireView();
                if (!observationLayer.SelectCurrentActor()
                    || observationLayer.SelectedActorStableId
                    != 사가정합성기사관찰Policy.ActorStableId)
                    throw new InvalidOperationException(
                        "SagajeongSyntheticCourierObservationCardSelectionFailed");
                view.FocusPoint(actorWorldPosition);
                view.ObservationCamera.orthographicSize = SelectedOrthographicSize;
                BeginPreparedWait();
                return;
            }
            if (!WaitFrames(24)) return;
            Capture("02-selected-card-close.png");
            SetPhase(Phase.ValidateAndStop);
        }

        private static void ValidateAndStop()
        {
            if (!WaitForCapture("02-selected-card-close.png")) return;
            var view = RequireView();
            if (observationLayer == null || latestProjection == null)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationRuntimeMissing");
            var actorLayerColliderCount = observationLayer
                .GetComponentsInChildren<Collider>(true).Length;
            if (actorLayerColliderCount != 0)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationColliderDetected");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationSceneBecameDirty");
            if (!observationLayer.IsSelected
                || observationLayer.SelectedActorStableId
                != 사가정합성기사관찰Policy.ActorStableId)
                throw new InvalidOperationException(
                    "SagajeongSyntheticCourierObservationSelectedCardMissing");

            var report = new CaptureManifest
            {
                schemaVersion =
                    "sagajeong-synthetic-courier-observation-gameview-evidence.v1",
                capturedAtUtc = DateTime.UtcNow.ToString("O",
                    CultureInfo.InvariantCulture),
                canonicalScenePath = CanonicalScenePath,
                sourceProjectPath = SessionState.GetString(
                    SourceProjectPathKey, string.Empty),
                validationProjectPath = NormalizeDirectoryPath(Path.Combine(
                    UnityEngine.Application.dataPath, "..")),
                sceneSha256Before = SessionState.GetString(SceneHashKey,
                    string.Empty),
                scenarioStableId = latestProjection.ScenarioStableId,
                scenarioDataRevision = latestProjection.ScenarioDataRevision,
                sessionStableId = latestProjection.SessionStableId,
                actorStableId = latestProjection.ActorStableId,
                actorDisplayAlias = latestProjection.DisplayAlias,
                currentStageCode = latestProjection.CurrentStageCode,
                currentActionCode = latestProjection.CurrentActionCode,
                initialSourceRevision = initialProjection.SourceSessionRevision,
                latestSourceRevision = latestProjection.SourceSessionRevision,
                initialTick = initialProjection.CurrentTick,
                latestTick = latestProjection.CurrentTick,
                initialActorX = initialProjection.ActorX,
                initialActorZ = initialProjection.ActorZ,
                latestActorX = latestProjection.ActorX,
                latestActorZ = latestProjection.ActorZ,
                actorPositionDeltaXWhileHidden =
                    actorPositionDeltaXWhileHidden,
                actorPositionDeltaZWhileHidden =
                    actorPositionDeltaZWhileHidden,
                actorMovementDistanceWhileHidden =
                    actorMovementDistanceWhileHidden,
                actorPositionChangedWhileHidden =
                    actorPositionChangedWhileHidden,
                initialProjectionFingerprintSha256 =
                    initialProjection.ProjectionFingerprintSha256,
                latestProjectionFingerprintSha256 =
                    latestProjection.ProjectionFingerprintSha256,
                initialAuthoritySnapshotSha256 = initialAuthorityHash,
                initialAuthoritySnapshotAfterSelectionSha256 =
                    initialAuthorityHashAfterSelection,
                latestAuthoritySnapshotSha256 = latestAuthorityHash,
                latestAuthoritySnapshotAfterPresentationSha256 =
                    latestAuthorityHashAfterPresentation,
                actorLayerColliderCount = actorLayerColliderCount,
                runtimeVisualCount = observationLayer.RuntimeVisualCount,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                actualSimulationAggregateUsed = true,
                programmaticScreenSelectionVerified =
                    programmaticScreenSelectionVerified,
                selectedReadOnlyCardVisible = observationLayer.IsSelected,
                hiddenHigherRevisionAccepted = hiddenHigherRevisionAccepted,
                reenabledLatestVisible = reenabledLatestVisible,
                initialAuthorityUnchangedBySelection = string.Equals(
                    initialAuthorityHash, initialAuthorityHashAfterSelection,
                    StringComparison.Ordinal),
                latestAuthorityUnchangedByPresentation = string.Equals(
                    latestAuthorityHash, latestAuthorityHashAfterPresentation,
                    StringComparison.Ordinal),
                initialProjectionFingerprintUnchanged =
                    initialProjectionFingerprintUnchanged,
                latestProjectionFingerprintUnchanged =
                    latestProjectionFingerprintUnchanged,
                observationPresentationOnly = latestProjection
                    .ObservationPresentationOnly,
                actualPerson = latestProjection.ActualPerson,
                operational = latestProjection.Operational,
                settlementWriteAllowed = latestProjection
                    .SettlementWriteAllowed,
                syntheticCompensationStatus = latestProjection
                    .SyntheticCompensation.StatusCode,
                syntheticCompensationAmountAvailable = latestProjection
                    .SyntheticCompensation.AmountMinor.HasValue,
                sceneDirtyDuringPlay = false,
                screenshotEvidenceOnly = true,
                provesServerConnection = false,
                provesOperationalDelivery = false,
                provesRoadTraversalOrTraffic = false,
                publicReleaseEvidence = false,
                isolatedValidationCopy = !string.Equals(
                    SessionState.GetString(SourceProjectPathKey, string.Empty),
                    NormalizeDirectoryPath(Path.Combine(
                        UnityEngine.Application.dataPath, "..")),
                    StringComparison.OrdinalIgnoreCase),
                coverageBoundary =
                    "Play Mode Game View 표현 검증이며 실제 서버 연결·운영 배달·도로 주행·실제 기사 실적·정산 증거가 아님"
            };
            File.WriteAllText(Path.Combine(OutputDirectory(),
                    "capture-manifest.json"),
                JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log(
                "[SagajeongSyntheticCourierObservationCliCapture] Game View 검증 완료 · "
                + $"selection={programmaticScreenSelectionVerified} · "
                + $"hiddenRefresh={hiddenHigherRevisionAccepted} · "
                + $"collider={actorLayerColliderCount}");
            SetPhase(Phase.StoppingSuccess);
            EditorApplication.isPlaying = false;
        }

        private static void ExitWhenStopped(int code)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scenePath = CanonicalSceneFullPath();
            var before = SessionState.GetString(SceneHashKey, string.Empty);
            var after = Sha256File(scenePath);
            var sceneDirtyAfterPlay = EditorSceneManager.GetActiveScene().isDirty;
            var sceneHashUnchanged = string.Equals(before, after,
                StringComparison.Ordinal);
            if (code == 0 && (!sceneHashUnchanged || sceneDirtyAfterPlay))
            {
                Debug.LogError(
                    "[SagajeongSyntheticCourierObservationCliCapture] 종료 뒤 Scene 불변 검사 실패");
                code = FailureExitCode;
            }

            var manifestPath = Path.Combine(OutputDirectory(),
                "capture-manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonUtility.FromJson<CaptureManifest>(
                    File.ReadAllText(manifestPath));
                manifest.sceneSha256After = after;
                manifest.sceneHashUnchanged = sceneHashUnchanged;
                manifest.sceneDirtyAfterPlay = sceneDirtyAfterPlay;
                File.WriteAllText(manifestPath,
                    JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            }

            SessionState.SetBool(ActiveKey, false);
            Debug.Log(
                "[SagajeongSyntheticCourierObservationCliCapture] 종료 · exit="
                + code + " · sceneHashUnchanged=" + sceneHashUnchanged
                + " · sceneDirtyAfterPlay=" + sceneDirtyAfterPlay);
            EditorApplication.Exit(code);
        }

        private static 경영SimulationSessionSnapshot AdvanceToDriveHome(
            경영SimulationSessionAggregate value)
        {
            var state = value.Snapshot();
            for (var tick = 0; tick < 160; tick++)
            {
                if (string.Equals(state.SyntheticCourier?.Stage, "DriveHome",
                        StringComparison.Ordinal))
                    return state;
                state = value.Advance(new 경영SimulationTick진행Request
                {
                    CommandId = "sagajeong-synthetic-courier-observation:tick:"
                                + tick,
                    ExpectedRevision = state.Revision,
                    TickCount = 1
                });
            }
            throw new InvalidOperationException(
                "SagajeongSyntheticCourierObservationDriveHomeNotReached");
        }

        private static string AuthorityHash(경영SimulationSessionSnapshot value)
            => Sha256Text(JsonConvert.SerializeObject(value, Formatting.None));

        private static void Fail(string code, Exception exception)
        {
            Debug.LogError(
                "[SagajeongSyntheticCourierObservationCliCapture] " + code
                + " · " + exception);
            var output = OutputDirectory();
            if (!string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output, "failure.txt"),
                        code + Environment.NewLine + exception,
                        new UTF8Encoding(false));
                }
                catch
                {
                    // 원본 예외를 보존하기 위해 실패 기록 오류는 무시한다.
                }
            }
            if (!SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.Exit(FailureExitCode);
                return;
            }
            SessionState.SetInt(PhaseKey, (int)Phase.StoppingFailure);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = false;
        }

        private static bool WaitFrames(int required)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            var frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            return frame >= required;
        }

        private static bool WaitForCapture(string fileName)
        {
            var path = Path.Combine(OutputDirectory(), fileName);
            if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;
            var started = long.Parse(
                SessionState.GetString(RunStartedKey, "0"),
                CultureInfo.InvariantCulture);
            return File.GetLastWriteTimeUtc(path).Ticks >= started;
        }

        private static void BeginPreparedWait()
        {
            SessionState.SetBool(PreparedKey, true);
            SessionState.SetInt(FrameKey, 0);
        }

        private static void SetPhase(Phase value)
        {
            SessionState.SetInt(PhaseKey, (int)value);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetBool(PreparedKey, false);
        }

        private static void Capture(string fileName)
        {
            var path = Path.Combine(OutputDirectory(), fileName);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log(
                "[SagajeongSyntheticCourierObservationCliCapture] 캡처 요청 · "
                + path);
        }

        private static 사가정운영디오라마View FindView()
            => Resources.FindObjectsOfTypeAll<사가정운영디오라마View>()
                .FirstOrDefault(value => value != null
                                         && value.gameObject.scene.IsValid());

        private static 사가정운영디오라마View RequireView()
            => FindView() ?? throw new InvalidOperationException(
                "SagajeongSyntheticCourierObservationViewMissing");

        private static string OutputDirectory()
            => SessionState.GetString(OutputKey, string.Empty);

        private static string CanonicalSceneFullPath()
            => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "..", CanonicalScenePath));

        private static string NormalizeDirectoryPath(string value)
            => Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        private static bool HasArgument(string value)
            => Environment.GetCommandLineArgs().Any(argument => string.Equals(
                argument, value, StringComparison.OrdinalIgnoreCase));

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], name,
                        StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }

        private static string Sha256File(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return Hex(algorithm.ComputeHash(stream));
        }

        private static string Sha256Text(string value)
        {
            using var algorithm = SHA256.Create();
            return Hex(algorithm.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static string Hex(byte[] value)
            => string.Concat(value.Select(item => item.ToString("X2",
                CultureInfo.InvariantCulture)));

        private static void ResetRuntimeFields()
        {
            aggregate = null;
            authorityState = null;
            initialProjection = null;
            latestProjection = null;
            observationLayer = null;
            actorWorldPosition = default;
            initialAuthorityHash = string.Empty;
            initialAuthorityHashAfterSelection = string.Empty;
            latestAuthorityHash = string.Empty;
            latestAuthorityHashAfterPresentation = string.Empty;
            programmaticScreenSelectionVerified = false;
            hiddenHigherRevisionAccepted = false;
            reenabledLatestVisible = false;
            initialProjectionFingerprintUnchanged = false;
            latestProjectionFingerprintUnchanged = false;
            actorPositionDeltaXWhileHidden = 0d;
            actorPositionDeltaZWhileHidden = 0d;
            actorMovementDistanceWhileHidden = 0d;
            actorPositionChangedWhileHidden = false;
        }

        [Serializable]
        private sealed class CaptureManifest
        {
            public string schemaVersion;
            public string capturedAtUtc;
            public string canonicalScenePath;
            public string sourceProjectPath;
            public string validationProjectPath;
            public string sceneSha256Before;
            public string sceneSha256After;
            public string scenarioStableId;
            public string scenarioDataRevision;
            public string sessionStableId;
            public string actorStableId;
            public string actorDisplayAlias;
            public string currentStageCode;
            public string currentActionCode;
            public long initialSourceRevision;
            public long latestSourceRevision;
            public int initialTick;
            public int latestTick;
            public double initialActorX;
            public double initialActorZ;
            public double latestActorX;
            public double latestActorZ;
            public double actorPositionDeltaXWhileHidden;
            public double actorPositionDeltaZWhileHidden;
            public double actorMovementDistanceWhileHidden;
            public bool actorPositionChangedWhileHidden;
            public string initialProjectionFingerprintSha256;
            public string latestProjectionFingerprintSha256;
            public string initialAuthoritySnapshotSha256;
            public string initialAuthoritySnapshotAfterSelectionSha256;
            public string latestAuthoritySnapshotSha256;
            public string latestAuthoritySnapshotAfterPresentationSha256;
            public int actorLayerColliderCount;
            public int runtimeVisualCount;
            public int screenWidth;
            public int screenHeight;
            public bool actualSimulationAggregateUsed;
            public bool programmaticScreenSelectionVerified;
            public bool selectedReadOnlyCardVisible;
            public bool hiddenHigherRevisionAccepted;
            public bool reenabledLatestVisible;
            public bool initialAuthorityUnchangedBySelection;
            public bool latestAuthorityUnchangedByPresentation;
            public bool initialProjectionFingerprintUnchanged;
            public bool latestProjectionFingerprintUnchanged;
            public bool observationPresentationOnly;
            public bool actualPerson;
            public bool operational;
            public bool settlementWriteAllowed;
            public string syntheticCompensationStatus;
            public bool syntheticCompensationAmountAvailable;
            public bool sceneDirtyDuringPlay;
            public bool screenshotEvidenceOnly;
            public bool provesServerConnection;
            public bool provesOperationalDelivery;
            public bool provesRoadTraversalOrTraffic;
            public bool publicReleaseEvidence;
            public bool isolatedValidationCopy;
            public string coverageBoundary;
            public bool sceneHashUnchanged;
            public bool sceneDirtyAfterPlay;
        }
    }
}
