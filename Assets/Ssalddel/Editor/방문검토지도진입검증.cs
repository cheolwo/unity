using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// r54 전용 graphics Editor의 격리된 canonical Scene 표현 검증. Scene 저장·위치 승인·외부 조회 없음.
    /// -executeMethod Ssalddel.Unity.Editor.방문검토지도진입검증.Run -visitReviewMapEntryCapture
    /// -visitReviewOutput &lt;새 출력 폴더&gt;; SSALDDEL_R53_REPO_ROOT/SSALDDEL_R53_RECORD_PATH 필요.
    /// -batchmode, -nographics, -quit 없이 통합 담당이 단독 Editor 실행을 지정한 뒤 사용한다.
    /// </summary>
    [InitializeOnLoad]
    public static class 방문검토지도진입검증
    {
        private const string Marker = "-visitReviewMapEntryCapture", Prefix = "Ssalddel.VisitMapEntryR54.";
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private static 방문순서재생검토진입 entry;
        private static 방문순서재생검토Presenter presenter;
        private static 행정동디오라마검토View view;
        private static 방문검토지도진입검증Hud hud;
        private static IDisposable lease;
        private static double phaseClock, pausedSeconds;
        private static Vector3 firstPosition;
        private static JArray checkpoints = new JArray(), captures = new JArray();

        static 방문검토지도진입검증()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            UnityEngine.Application.logMessageReceived -= ObserveLog;
            UnityEngine.Application.logMessageReceived += ObserveLog;
        }

        public static void Run()
        {
            if (!Environment.GetCommandLineArgs().Contains(Marker)) return;
            SessionState.SetInt(Prefix + "Pid", System.Diagnostics.Process.GetCurrentProcess().Id);
            try
            {
                Require(!UnityEngine.Application.isBatchMode && !Has("-nographics") && !Has("-quit"), "GraphicsProcessRequired");
                Require(!EditorApplication.isPlayingOrWillChangePlaymode && !Owned(), "EditModeStartRequired");
                for (var i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "DirtySceneAtStart");
                var output = Path.GetFullPath(Argument("-visitReviewOutput"));
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "FreshOutputRequired");
                Require(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSALDDEL_R53_REPO_ROOT"))
                    && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSALDDEL_R53_RECORD_PATH")), "LocalInputsRequired");
                Directory.CreateDirectory(output);
                SessionState.SetString(Prefix + "Output", output);
                SessionState.SetString(Prefix + "SceneHash", Hash(SceneAbsolutePath));
                SessionState.SetString(Prefix + "StartedUtc", DateTime.UtcNow.ToString("O"));
                SessionState.SetString(Prefix + "Deadline", DateTime.UtcNow.AddMinutes(3).ToString("O"));
                SessionState.SetString(Prefix + "FailureCode", "");
                SessionState.SetString(Prefix + "Checkpoints", "[]"); SessionState.SetString(Prefix + "Captures", "[]");
                SessionState.SetBool(Prefix + "Passed", false);
                SessionState.SetBool(Prefix + "SyntheticValidated", false);
                SessionState.SetInt(Prefix + "ActualCount", -1);
                SessionState.SetInt(Prefix + "ActualDiagnosticCount", -1);
                SessionState.SetString(Prefix + "RecordHash", "");
                SessionState.SetString(Prefix + "ProjectionHash", "");
                SessionState.SetString(Prefix + "SyntheticFingerprint", "");
                foreach (var code in new[] { "Error", "Exception", "Assert", "Warning" }) SessionState.SetInt(Prefix + code, 0);
                SessionState.SetInt(Prefix + "ConsoleStart", ConsoleErrors());
                SessionState.SetBool(Prefix + "Active", true);
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Require(scene.path == ScenePath && !scene.isDirty, "CanonicalSceneRequired");
                // 전용 프로세스의 비저장 준비. Awake/Start가 실제 서버를 호출하기 전에 기존 루트를 격리한다.
                var roots = scene.GetRootGameObjects().Where(value => value.activeSelf).ToArray();
                foreach (var root in roots) root.SetActive(false);
                SessionState.SetInt(Prefix + "IsolatedRoots", roots.Length);
                SessionState.SetInt(Prefix + "Phase", 1);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Tick()
        {
            if (!Owned()) return;
            try
            {
                var phase = SessionState.GetInt(Prefix + "Phase", 0);
                if (phase == 90) { Finish(); return; }
                Require(DateTime.UtcNow < DateTime.Parse(SessionState.GetString(Prefix + "Deadline", ""),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), "Timeout");
                if (phase == 1) { if (EditorApplication.isPlaying) InitializePlay(); return; }
                Require(EditorApplication.isPlaying, "UnexpectedPlayStop");
                switch (phase)
                {
                    case 2:
                        if (Elapsed < .5) return;
                        Capture("01-actual-record-rejected.png", 3); break;
                    case 3:
                        if (!CaptureReady()) return;
                        var space = 방문순서재생검토진입.LoadFrozenFocus(RepositoryRoot, view.Snapshot);
                        var sample = Synthetic(space);
                        Require(presenter.TryLoad(sample, Clock), "SyntheticLoadFailed");
                        firstPosition = presenter.MarkerTransform.localPosition;
                        Require(presenter.Play(Clock), "PlayFailed");
                        Checkpoint("synthetic-three-visits-playing"); Phase(4); break;
                    case 4:
                        if (Elapsed < 3) return;
                        Require(presenter.CurrentFrame.IsMoving && presenter.MarkerCount == 1
                            && Vector3.Distance(firstPosition, presenter.MarkerTransform.localPosition) > 1, "RealClockMovementMissing");
                        Checkpoint("synthetic-moved-on-real-clock");
                        Require(presenter.Pause(Clock), "PauseFailed");
                        pausedSeconds = presenter.CurrentFrame.PresentationSeconds;
                        hud.Status = "합성 방문 1 → 2 이동 뒤 일시정지 · 표시 1개";
                        Checkpoint("synthetic-paused"); Capture("02-synthetic-paused.png", 5); break;
                    case 5:
                        if (!CaptureReady() || Elapsed < .5) return;
                        Require(presenter.CurrentFrame.State == 방문재생상태.Paused
                            && presenter.CurrentFrame.PresentationSeconds == pausedSeconds, "PauseDidNotHold");
                        Checkpoint("synthetic-pause-held");
                        Require(presenter.SeekVisit("synthetic-r54-visit:3", Clock), "SeekFailed");
                        Require(presenter.CurrentFrame.Sequence == 3 && presenter.CurrentFrame.State == 방문재생상태.Paused
                            && Vector3.Distance(firstPosition, presenter.MarkerTransform.localPosition) < .001f, "SeekPositionMismatch");
                        hud.Status = "합성 방문 3 선택 · 시작 표본으로 귀환 · 일시정지";
                        Checkpoint("synthetic-seek-third-visit"); Phase(6); break;
                    case 6:
                        if (Elapsed < .25) return;
                        Capture("03-synthetic-seek.png", 7); break;
                    case 7:
                        if (!CaptureReady()) return;
                        presenter.Clear(); Phase(8); break;
                    case 8:
                        if (Elapsed < .25) return;
                        Require(presenter.MarkerCount == 0 && presenter.FocusRoot == null
                            && presenter.CurrentFrame.State == 방문재생상태.Empty && view.ModelRoot != null, "ClearFailed");
                        hud.Status = "합성 재생 정리 완료 · 표시 0개 · 원래 동결 지도 유지";
                        Checkpoint("presenter-cleared-map-preserved"); Capture("04-cleared.png", 9); break;
                    case 9:
                        if (!CaptureReady()) return;
                        entry.ClearReview(); Phase(10); break;
                    case 10:
                        if (Elapsed < .25) return;
                        Require(view == null && presenter == null && entry.PreparedMap == null && entry.Presenter == null,
                            "OwnedHostClearFailed");
                        Checkpoint("entry-owned-hosts-released");
                        SessionState.SetBool(Prefix + "Passed", true); Stop(); break;
                    default: throw new InvalidOperationException("VisitCaptureUnknownPhase");
                }
            }
            catch (Exception error) { Fail(error); }
        }

        private static void InitializePlay()
        {
            Require(SceneManager.GetActiveScene().path == ScenePath, "CanonicalSceneRequired");
            Require(사가정GameView검증CaptureCoordinator.TryAcquire("VisitMapEntryR54", out lease), "CaptureAlreadyOwned");
            entry = ScriptableObject.CreateInstance<방문순서재생검토진입>();
            entry.hideFlags = HideFlags.DontSave;
            Require(entry.TryPrepareFrozenMap(RepositoryRoot), "FrozenMapPreparationFailed");
            view = entry.PreparedMap; presenter = entry.Presenter;
            Require(view != null && presenter != null && presenter.FocusRoot != null, "PreparedObjectsMissing");
            view.CompactEvidenceHud = true;
            var snapshot = view.Snapshot;
            Require(snapshot.TileCount == 12 && snapshot.Buildings.Count == 2700 && snapshot.Roads.Count == 291
                && snapshot.PrivateObservationOverlay != null && snapshot.PrivateWalkNetworkOverlay != null
                && snapshot.PrivateRoadDirectionOverlay != null && snapshot.PrivateBiotopeOverlay != null
                && snapshot.PrivateLaneMarkingOverlay != null && snapshot.PrivateHorizontalContourTraceOverlay != null,
                "FrozenMapOrOverlayMismatch");
            SessionState.SetString(Prefix + "ProjectionHash", snapshot.ProjectionHashSha256);
            var path = Environment.GetEnvironmentVariable("SSALDDEL_R53_RECORD_PATH");
            var record = new 방문기록JsonDecoder().Decode(File.ReadAllText(path));
            SessionState.SetInt(Prefix + "ActualCount", record.Visits.Length);
            Require(record.Visits.Length == 7, "ActualRecordCountChanged");
            var actual = 방문순서재생검토진입.PrepareRecordFile(path, string.Empty, false);
            Require(!actual.CanReplay && actual.Visits.Count == 0 && actual.Diagnostics.Count > 0, "ActualRecordUnexpectedlyReady");
            Require(!presenter.TryLoad(actual, Clock) && presenter.MarkerCount == 0
                && presenter.DiagnosticCode == "VisitPlaybackPreparationRequired", "ActualRecordWasDisplayed");
            SessionState.SetString(Prefix + "RecordHash", Hash(path));
            SessionState.SetInt(Prefix + "ActualDiagnosticCount", actual.Diagnostics.Count);
            hud = view.gameObject.AddComponent<방문검토지도진입검증Hud>();
            hud.hideFlags = HideFlags.DontSave; hud.Status = "실제 기록 7건 재생 보류 · 위치 결속 미확정 · 표시 0개";
            SetGameViewSize();
            checkpoints = new JArray(); captures = new JArray();
            Checkpoint("actual-seven-records-rejected-zero-markers"); Phase(2);
        }

        private static 방문재생준비Result Synthetic(방문재생공간검토 space)
        {
            // 기존 도로의 중간 두 점도 합성 표시 위치로만 쓴다. 실제 방문·주행·출입구를 추정하지 않는다.
            foreach (var road in space.SelectedRoads)
            {
                var ax = space.Snapshot.SourceCenterX + .65 * road.From.X + .35 * road.To.X;
                var az = space.Snapshot.SourceCenterZ + .65 * road.From.Z + .35 * road.To.Z;
                var bx = space.Snapshot.SourceCenterX + .35 * road.From.X + .65 * road.To.X;
                var bz = space.Snapshot.SourceCenterZ + .35 * road.From.Z + .65 * road.To.Z;
                if (Math.Abs(ax - bx) + Math.Abs(az - bz) < 20) continue;
                var visits = Enumerable.Range(1, 3).Select(i => new RegionalPickupVisit
                {
                    VisitId = "synthetic-r54-visit:" + i, StoreId = "synthetic-r54-point:" + (i == 2 ? "B" : "A"),
                    Sequence = i, Name = "검토용 합성 표본", Menu = "합성 순서 검증", Address = "합성 지점 " + (i == 2 ? "B" : "A"),
                    Latitude = 1, Longitude = 1, AddressStatus = "Verified", CoordinateStatus = "Verified",
                    AreaBindingStatus = "Verified", AdministrativeAreaStableId = space.Snapshot.AdministrativeAreaStableId,
                    AreaBindingEvidence = "SyntheticTestOnly_NotLocationEvidence", EventKind = "RecordedVisit", PickupCompletionConfirmed = false
                }).ToArray();
                var record = new RegionalPickupRecord { SchemaVersion = "delivery-visit-record.v2", RecordId = "synthetic-r54",
                    LocalReviewOnly = true, IsOperationalState = false, Visits = visits };
                var bindings = visits.Select(v => new 방문위치결속(record.RecordId, "synthetic-r54-revision-1", v.VisitId,
                    v.StoreId, v.Address, v.Latitude, v.Longitude, v.AdministrativeAreaStableId, space.CoordinateFrameKey,
                    v.Sequence == 2 ? bx : ax, v.Sequence == 2 ? bz : az, "synthetic-coordinate-r1",
                    "synthetic-historical-boundary-test-r1", "ExplicitSyntheticVerificationOnly")).ToArray();
                var result = 방문재생준비.Prepare(record, "synthetic-r54-revision-1", bindings);
                if (space.ValidatePrepared(result) != string.Empty) continue;
                SessionState.SetString(Prefix + "SyntheticFingerprint", result.InputFingerprint);
                SessionState.SetBool(Prefix + "SyntheticValidated", true);
                hud.Status = "합성 표본 A → B → A · 실제 기록과 무관한 재생 검증";
                return result;
            }
            throw new InvalidOperationException("VisitCaptureSyntheticPointsOutsideHistoricalFocus");
        }

        private static void Checkpoint(string code)
        {
            checkpoints.Add(new JObject { ["code"] = code, ["frame"] = Time.frameCount,
                ["markerCount"] = presenter != null ? presenter.MarkerCount : 0,
                ["state"] = presenter != null ? presenter.CurrentFrame.State.ToString() : "Released",
                ["sequence"] = presenter != null ? presenter.CurrentFrame.Sequence : 0,
                ["presentationSeconds"] = presenter != null ? presenter.CurrentFrame.PresentationSeconds : 0 });
            SessionState.SetString(Prefix + "Checkpoints", checkpoints.ToString(Formatting.None));
        }

        private static void Capture(string fileName, int next)
        {
            Require(view != null && view.ObservationCamera != null && view.ObservationCamera.enabled
                && view.ObservationCamera.gameObject.activeInHierarchy && view.ObservationCamera.targetTexture == null,
                "CaptureCameraInactive");
            var activeCameras = Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid()
                && c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null).ToArray();
            Require(activeCameras.Length == 1 && activeCameras[0] == view.ObservationCamera, "UnexpectedActiveCamera");
            var path = Path.Combine(Output, fileName); Require(!File.Exists(path), "CaptureExists");
            SessionState.SetString(Prefix + "PendingCapture", path);
            SessionState.SetInt(Prefix + "CaptureFrame", Time.frameCount);
            ScreenCapture.CaptureScreenshot(path); Phase(next);
        }

        private static bool CaptureReady()
        {
            var path = SessionState.GetString(Prefix + "PendingCapture", "");
            if (path.Length == 0) return true;
            if (Time.frameCount <= SessionState.GetInt(Prefix + "CaptureFrame", 0) + 1 || !File.Exists(path)) return false;
            var bytes = File.ReadAllBytes(path); if (bytes.Length < 32) return false;
            Require(bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71
                && BigEndian(bytes, 16) >= 640 && BigEndian(bytes, 20) >= 360, "InvalidCapturePng");
            captures.Add(new JObject { ["fileName"] = Path.GetFileName(path), ["sha256"] = Hash(path),
                ["width"] = BigEndian(bytes, 16), ["height"] = BigEndian(bytes, 20), ["actualScreenCapture"] = true });
            SessionState.SetString(Prefix + "Captures", captures.ToString(Formatting.None));
            SessionState.SetString(Prefix + "PendingCapture", ""); return true;
        }

        private static void Stop()
        {
            if (entry != null) { entry.ClearReview(); UnityEngine.Object.DestroyImmediate(entry); }
            lease?.Dispose(); lease = null;
            SessionState.SetInt(Prefix + "Phase", 90);
            SessionState.SetString(Prefix + "FinishedUtc", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = false;
        }

        private static void Finish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Require(Hash(SceneAbsolutePath) == SessionState.GetString(Prefix + "SceneHash", ""), "SavedSceneHashChanged");
            // 전용 프로세스가 바꾼 비저장 격리 상태만 폐기한다. 원래 Scene 파일을 수정하거나 저장하지 않는다.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(!scene.isDirty, "SceneDirtyAfterReload"); RestoreGameViewSize();
            var errors = SessionState.GetInt(Prefix + "Error", 0) + SessionState.GetInt(Prefix + "Exception", 0)
                + SessionState.GetInt(Prefix + "Assert", 0);
            var consoleEnd = ConsoleErrors();
            var recorded = JArray.Parse(SessionState.GetString(Prefix + "Checkpoints", "[]"));
            bool Checked(string code) => recorded.Any(value => (string)value["code"] == code);
            var actualChecked = Checked("actual-seven-records-rejected-zero-markers");
            var syntheticChecked = SessionState.GetBool(Prefix + "SyntheticValidated", false);
            var scenarioPassed = SessionState.GetBool(Prefix + "Passed", false);
            var profilePassed = scenarioPassed && errors == 0 && consoleEnd == 0;
            if (scenarioPassed && !profilePassed) SessionState.SetString(Prefix + "FailureCode",
                consoleEnd < 0 ? "VisitCaptureConsoleCountUnavailable" : "VisitCaptureConsoleErrorsObserved");
            var manifest = new JObject
            {
                ["schemaVersion"] = "visit-review-map-entry-capture.r54.v1",
                ["profilePassed"] = profilePassed, ["scenarioChecksPassed"] = scenarioPassed,
                ["failureCode"] = SessionState.GetString(Prefix + "FailureCode", ""),
                ["scope"] = "IsolatedCanonicalScenePresentationOnly", ["sourceMode"] = "FrozenPrivateMapAndExplicitSyntheticVisits",
                ["sceneSaved"] = false, ["sceneHashBefore"] = SessionState.GetString(Prefix + "SceneHash", ""),
                ["sceneHashAfter"] = Hash(SceneAbsolutePath), ["sceneCleanAfterReload"] = !scene.isDirty,
                ["isolationRootCount"] = SessionState.GetInt(Prefix + "IsolatedRoots", 0),
                ["npcOrOperationalControllerContinuityVerified"] = false,
                ["actualRecordCount"] = SessionState.GetInt(Prefix + "ActualCount", -1) >= 0
                    ? (JToken)SessionState.GetInt(Prefix + "ActualCount", -1) : JValue.CreateNull(),
                ["actualRejectionVerified"] = actualChecked,
                ["actualRecordReadyCount"] = actualChecked ? (JToken)0 : JValue.CreateNull(),
                ["actualRecordMarkerCount"] = actualChecked ? (JToken)0 : JValue.CreateNull(),
                ["actualRejectionCode"] = actualChecked ? (JToken)"VisitPlaybackPreparationRequired" : JValue.CreateNull(),
                ["actualPreparationDiagnosticCount"] = actualChecked
                    ? (JToken)SessionState.GetInt(Prefix + "ActualDiagnosticCount", -1) : JValue.CreateNull(),
                ["actualRecordSha256"] = SessionState.GetString(Prefix + "RecordHash", ""),
                ["projectionHashSha256"] = SessionState.GetString(Prefix + "ProjectionHash", ""),
                ["syntheticVisitCount"] = syntheticChecked ? (JToken)3 : JValue.CreateNull(),
                ["syntheticDistinctPointCount"] = syntheticChecked ? (JToken)2 : JValue.CreateNull(),
                ["syntheticInputFingerprint"] = SessionState.GetString(Prefix + "SyntheticFingerprint", ""),
                ["syntheticPointsValidatedInsideFocusAndHistoricalBoundary"] = syntheticChecked,
                ["realClockMovementVerified"] = Checked("synthetic-moved-on-real-clock"),
                ["pauseHeldVerified"] = Checked("synthetic-pause-held"),
                ["seekThirdVisitVerified"] = Checked("synthetic-seek-third-visit"),
                ["clearPreservedMapVerified"] = Checked("presenter-cleared-map-preserved"),
                ["entryOwnedHostsReleasedVerified"] = Checked("entry-owned-hosts-released"),
                ["usesActualHttp"] = false, ["databaseWrites"] = false, ["operationalEffectsAllowed"] = false,
                ["actualDeliveryPlaybackClaimed"] = false, ["evidenceStageClaimed"] = null,
                ["consoleErrorsAtStart"] = SessionState.GetInt(Prefix + "ConsoleStart", -1),
                ["consoleErrorsAtEnd"] = consoleEnd, ["runErrorExceptionAssertCount"] = errors,
                ["runWarningCount"] = SessionState.GetInt(Prefix + "Warning", 0), ["consoleRawMessagesStored"] = false,
                ["consoleCleanDuringProfile"] = errors == 0, ["gameViewSettingsRestored"] = !SessionState.GetBool(Prefix + "SizeChanged", false),
                ["recoveredOwnedGameViewSizeCount"] = SessionState.GetInt(Prefix + "RecoveredOwnedSizes", 0),
                ["startedUtc"] = SessionState.GetString(Prefix + "StartedUtc", ""),
                ["finishedUtc"] = SessionState.GetString(Prefix + "FinishedUtc", ""),
                ["checkpoints"] = recorded,
                ["captures"] = JArray.Parse(SessionState.GetString(Prefix + "Captures", "[]")),
                ["newDioramaRuleCandidate"] = false
            };
            File.WriteAllText(Path.Combine(Output, "capture-manifest.json"), manifest.ToString(Formatting.Indented));
            SessionState.SetBool(Prefix + "Active", false);
            EditorApplication.Exit(profilePassed ? 0 : 2);
        }

        private static void SetGameViewSize()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Require(type != null, "GameViewUnavailable");
            var window = EditorWindow.GetWindow(type); var selected = type.GetProperty("selectedSizeIndex", Flags);
            SessionState.SetInt(Prefix + "OriginalSize", (int)selected.GetValue(window));
            var group = SizeGroup(); var groupType = group.GetType();
            var count = (int)groupType.GetMethod("GetTotalCount", Flags).Invoke(group, null);
            // 첫 실행의 Unity 6 전체 index 계약 확인 실패로 남을 수 있는 이 실행기 전용 항목만 복구한다.
            var recovered = 0;
            for (var index = count - 1; index >= (int)groupType.GetMethod("GetBuiltinCount", Flags).Invoke(group, null); index--)
            {
                var previous = groupType.GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { index });
                var label = previous.GetType().GetProperty("baseText", Flags)?.GetValue(previous) as string;
                if (label != "r54 private visit review") continue;
                groupType.GetMethod("RemoveCustomSize", Flags).Invoke(group, new object[] { index }); recovered++;
            }
            SessionState.SetInt(Prefix + "RecoveredOwnedSizes", recovered);
            count = (int)groupType.GetMethod("GetTotalCount", Flags).Invoke(group, null);
            var sizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
            var enumType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, Flags, null,
                new[] { Enum.Parse(enumType, "FixedResolution"), (object)1280, 720, "r54 private visit review" }, CultureInfo.InvariantCulture);
            groupType.GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
            SessionState.SetInt(Prefix + "AddedSize", count);
            SessionState.SetBool(Prefix + "SizeChanged", true);
            selected.SetValue(window, count); window.Show(); window.Focus();
        }
        private static object SizeGroup()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(type)
                .GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            return type.GetMethod("GetGroup", Flags).Invoke(instance, new[] { type.GetProperty("currentGroupType", Flags).GetValue(instance) });
        }
        private static void RestoreGameViewSize()
        {
            if (!SessionState.GetBool(Prefix + "SizeChanged", false)) return;
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            type.GetProperty("selectedSizeIndex", Flags).SetValue(EditorWindow.GetWindow(type), SessionState.GetInt(Prefix + "OriginalSize", 0));
            var group = SizeGroup(); group.GetType().GetMethod("RemoveCustomSize", Flags)
                .Invoke(group, new object[] { SessionState.GetInt(Prefix + "AddedSize", -1) });
            Require((int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null)
                == SessionState.GetInt(Prefix + "AddedSize", -1), "GameViewSizeRestorationFailed");
            SessionState.SetBool(Prefix + "SizeChanged", false);
        }

        private static void Fail(Exception error)
        {
            SessionState.SetBool(Prefix + "Passed", false);
            // 오류 원문·stack·실제 기록 식별자를 파일이나 Console에 복사하지 않는다.
            var code = error is InvalidOperationException && error.Message.StartsWith("VisitCapture", StringComparison.Ordinal)
                && error.Message.All(c => char.IsLetterOrDigit(c)) ? error.Message : "VisitCaptureExecutionFailed";
            SessionState.SetString(Prefix + "FailureCode", code);
            if (SessionState.GetInt(Prefix + "Phase", 0) == 90)
            {
                SessionState.SetBool(Prefix + "Active", false);
                if (Directory.Exists(Output)) File.WriteAllText(Path.Combine(Output, "capture-failure.json"),
                    new JObject { ["profilePassed"] = false, ["failureCode"] = code }.ToString());
                EditorApplication.Exit(2); return;
            }
            if (Owned()) Stop(); else EditorApplication.Exit(2);
        }
        private static void ObserveLog(string condition, string stack, LogType type)
        {
            if (!Owned()) return;
            var code = type == LogType.Error ? "Error" : type == LogType.Exception ? "Exception"
                : type == LogType.Assert ? "Assert" : type == LogType.Warning ? "Warning" : null;
            if (code != null) SessionState.SetInt(Prefix + code, SessionState.GetInt(Prefix + code, 0) + 1);
        }
        private static int ConsoleErrors()
        {
            try { var args = new object[] { 0, 0, 0 }; typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries")
                .GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args); return (int)args[0]; }
            catch { return -1; }
        }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static string RepositoryRoot => Environment.GetEnvironmentVariable("SSALDDEL_R53_REPO_ROOT");
        private static string Output => SessionState.GetString(Prefix + "Output", "");
        private static string SceneAbsolutePath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ScenePath));
        private static double Clock => 방문순서재생검토Presenter.ClockSeconds;
        private static double Elapsed => Clock - phaseClock;
        private static void Phase(int value) { phaseClock = Clock; SessionState.SetInt(Prefix + "Phase", value); }
        private static bool Has(string value) => Environment.GetCommandLineArgs().Contains(value);
        private static bool Owned() => Has(Marker) && SessionState.GetBool(Prefix + "Active", false)
            && SessionState.GetInt(Prefix + "Pid", -1) == System.Diagnostics.Process.GetCurrentProcess().Id;
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name);
            Require(index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal), "OutputRequired");
            return args[index + 1];
        }
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static int BigEndian(byte[] bytes, int i) => bytes[i] << 24 | bytes[i + 1] << 16 | bytes[i + 2] << 8 | bytes[i + 3];
        private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException("VisitCapture" + code); }
    }

    public sealed class 방문검토지도진입검증Hud : MonoBehaviour
    {
        public string Status { get; set; } = string.Empty;
        private void OnGUI()
        {
            GUI.depth = -100;
            var width = Mathf.Min(850, Screen.width - 24);
            GUI.Box(new Rect(12, Screen.height - 86, width, 74), GUIContent.none);
            GUI.Label(new Rect(24, Screen.height - 79, width - 24, 65),
                "검토용 합성 순서 · 실제 배달 내역 아님\n" + Status + "\n비공개 표현 검증 · 실제 주행·픽업·업무 완료 아님");
        }
    }
}
