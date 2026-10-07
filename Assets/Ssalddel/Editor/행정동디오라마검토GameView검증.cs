using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 30개 행정동 역사 경계 후보를 canonical Scene에서 한 개씩 순차 조립하고
    /// 실제 Play Mode Game View를 남긴다. Scene·Prefab·서버 권위 상태는 변경하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class AdministrativeDongDioramaReviewCliCapture
    {
        public const string ExecuteMethod =
            "Ssalddel.Unity.Editor.AdministrativeDongDioramaReviewCliCapture.Run";
        public const string CommandLineMarker = "-administrativeDongDioramaReviewCapture";
        public const string IndexArgument = "-administrativeDongDioramaReviewIndex";
        public const string IndexHashArgument = "-administrativeDongDioramaReviewIndexSha256";
        public const string OutputArgument = "-administrativeDongDioramaReviewOutput";
        public const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        private const string BaseIndexSchema =
            "administrative-dong-diorama-unity-review-index.v1";
        private const string ObservationIndexSchema =
            "administrative-dong-diorama-unity-review-index.v2";
        private const string WalkNetworkIndexSchema =
            "administrative-dong-diorama-unity-review-index.v3";
        private const string RoadDirectionIndexSchema =
            "administrative-dong-diorama-unity-review-index.v4";
        private const string BiotopeIndexSchema = "administrative-dong-diorama-unity-review-index.v5";
        private const string LaneMarkingIndexSchema =
            "administrative-dong-diorama-unity-review-index.v6";
        private const string HorizontalContourTraceIndexSchema =
            "administrative-dong-diorama-unity-review-index.v7";
        private const string BaseBundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v1";
        private const string ObservationBundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v2";
        private const string WalkNetworkBundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v3";
        private const string RoadDirectionBundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v4";
        private const string BiotopeBundleSchema = "administrative-dong-diorama-unity-review-bundle.v5";
        private const string LaneMarkingBundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v6";
        private const string HorizontalContourTraceBundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v7";
        private const string BaseCompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v1";
        private const string ObservationCompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v2";
        private const string WalkNetworkCompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v3";
        private const string RoadDirectionCompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v4";
        private const string BiotopeCompletionSchema = "administrative-dong-diorama-unity-review-completion.v5";
        private const string LaneMarkingCompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v6";
        private const string HorizontalContourTraceCompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v7";
        private const string BaseExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r2";
        private const string ObservationExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r3";
        private const string WalkNetworkExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r4";
        private const string RoadDirectionExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r5";
        private const string BiotopeExporterRevision = "administrative-dong-diorama-unity-review-exporter.r6";
        private const string LaneMarkingExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r7";
        private const string HorizontalContourTraceExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r8";
        private const string ExpectedHorizontalContourTraceGenerationHash =
            "13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753";
        private const string ExpectedHorizontalContourTraceIndexSha256 =
            "C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9";
        private const string ExpectedHorizontalContourTraceExporterSha256 =
            "168721AABE97B3B2B6C64985500ABDAC5E1B779E8DB104C0C302C13EAC031E2B";
        private const string ExpectedHorizontalContourTraceAuditSha256 =
            "D8F2EBFFF77418BAA05396D0EF13E8222B1FBD4A0126A366E90159C7F303B65D";
        private const string ExpectedHorizontalContourTraceSourceGenerationHash =
            "E5FC9551CEE5BB2C1DA8696939C882BE3141F0736AF9E36B945F9184C152CDFC";
        private const string ExpectedHorizontalContourTraceSourceManifestSha256 =
            "BAC3398814FEE00EF41FBFC1B974E47BA8E28902A0857708A2D5F093282C9A4C";
        private const string ExpectedHorizontalContourTraceSourceAuditSha256 =
            "82B8F49766FD3FE3CBE11D4472691EE566D17ED59834E917856769265066E6F3";
        private const int ExpectedHorizontalContourTraceCandidateCount = 860;
        private const int ExpectedHorizontalContourTraceFragmentCount = 184_747;
        private const int ExpectedHorizontalContourTracePathVertexCount = 369_494;
        private const int ExpectedHorizontalContourTraceSourceSegmentCount = 184_748;
        private const int ExpectedHorizontalContourTraceRenderedSegmentCount = 184_747;
        private const int ExpectedHorizontalContourTraceSkippedSegmentCount = 1;
        private const int ExpectedHorizontalContourTraceDuplicateSegmentCount = 0;
        private const string ExpectedLaneMarkingGenerationHash =
            "587601D4E9952149DA299D84496242CAD3CC761F6053D2B36A1A73C5C6B0E3D6";
        private const string ExpectedLaneMarkingIndexSha256 =
            "4DDE2DD68B6246D6DB046FCAB81FA153D111288D887D4C4729B006DFEB6B7AAF";
        private const string ExpectedLaneMarkingExporterSha256 =
            "09123CAD89BF1F27DC63C1390725AA661224334E51283F084934EE5F53B3288F";
        private const int ExpectedLaneMarkingCandidateCount = 23_360;
        private const int ExpectedLaneMarkingFragmentCount = 60_586;
        private const int ExpectedLaneMarkingPathVertexCount = 121_172;
        private const int ExpectedLaneMarkingSourceSegmentCount = 60_646;
        private const int ExpectedLaneMarkingRenderedSegmentCount = 60_586;
        private const int ExpectedLaneMarkingSkippedSegmentCount = 2;
        private const int ExpectedLaneMarkingDuplicateSegmentCount = 58;
        private const int ExpectedBiotopeCandidateCount = 3181;
        private const int ExpectedBiotopePolygonPartCount = 3408;
        private const int ExpectedBiotopeInteriorRingCount = 108;
        private const int ExpectedBiotopeRingVertexCount = 105731;
        private const int ExpectedBiotopeSourceSegmentCount = 102215;
        private const int ExpectedBiotopeSkippedSegmentCount = 128;
        private const int ExpectedBiotopeRenderedSegmentCount = 102087;
        private const string ExpectedBiotopeGenerationHash = "5207BA41C67B6BA93182CFA2485F1CD89A0AF2964EC97E55D8D4A988E77C646E";
        private const string ExpectedBiotopeIndexSha256 = "951D8E361873ACFFE1EB162845F3E59F9428A6DF1F71FF69813F9947483CD7E6";
        private const string ExpectedBiotopeExporterSha256 = "6D376489BFD03E0A5D32F561C54F905A341BD0D9492C76A2D132967CEB5F71E9";
        private const int ExpectedCrosswalkPointCandidateCount = 1533;
        private const int ExpectedIntersectionPointCandidateCount = 554;
        private const int ExpectedRoadDirectionPointCandidateCount = 8415;
        private const string ExpectedRoadDirectionGenerationHash =
            "3A7475C181E6BE11D46F256B40150119B95C3473477F529FDA7802B51053CD33";
        private const string ExpectedRoadDirectionIndexSha256 =
            "6170476FEEB84A4584E4C2D4EE5B3EA9B6CF651BEF12FF39E21780C4F020E887";
        private const string TargetArea = "region:kr:hjd:1126057500";
        private const string Prefix = "Ssalddel.AdministrativeDongDioramaReviewCapture.";
        private const string ActiveKey = Prefix + "Active";
        private const string PhaseKey = Prefix + "Phase";
        private const string FrameKey = Prefix + "Frame";
        private const string IndexKey = Prefix + "Index";
        private const string IndexHashKey = Prefix + "IndexHash";
        private const string OutputKey = Prefix + "Output";
        private const string SceneHashKey = Prefix + "SceneHash";
        private const string StartedKey = Prefix + "Started";
        private const string DeadlineKey = Prefix + "Deadline";
        private const string ModuleIndexKey = Prefix + "ModuleIndex";
        private const string ErrorCountKey = Prefix + "ErrorCount";
        private const string FirstErrorKey = Prefix + "FirstError";
        private const int FailureExitCode = 2;

        private static readonly string[] RequiredMissingLayers =
        {
            "CurrentAdministrativeBoundary", "RoadAddress", "ParcelIdentifierExport",
            "ParcelGeometry", "BuildingEntrance", "RoadWidth", "AlleyNetwork",
            "SidewalkNetwork", "LaneDirection", "IntersectionControl", "TrafficSignal",
            "PublicBusinessSpatialBinding", "LivingPopulationBinding", "NpcMovementBinding",
            "OperationalLifecycleBinding", "HRoleAreaSetBinding"
        };

        private static ReviewIndex index;
        private static ReviewCompletion completion;
        private static ReviewEntry activeEntry;
        private static ReviewBundle activeBundle;
        private static readonly List<ModuleEvidence> evidence = new();
        private static string pendingImagePath;
        private static string targetOverviewPath;

        private enum Phase
        {
            None,
            AwaitingPlayMode,
            LoadModule,
            CaptureModule,
            AwaitModuleCapture,
            PrepareTargetDetail,
            CaptureTargetDetail,
            AwaitTargetDetail,
            Finalize,
            StoppingSuccess,
            StoppingFailure
        }

        static AdministrativeDongDioramaReviewCliCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            UnityEngine.Application.logMessageReceived -= ObserveLog;
            UnityEngine.Application.logMessageReceived += ObserveLog;
        }

        public static void Run()
        {
            if (!HasArgument(CommandLineMarker))
            {
                Debug.LogError("[AdministrativeDongDioramaReviewCliCapture] 전용 표식이 없습니다.");
            }
            try
            {
                if (UnityEngine.Application.isBatchMode || HasArgument("-nographics"))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewRequiresGraphicsProcess");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewRequiresEditModeStart");
                if (EditorSceneManager.GetActiveScene().isDirty)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewDirtySceneAtStart");

                var indexPath = Path.GetFullPath(RequiredArgument(IndexArgument));
                var expectedIndexHash = RequiredArgument(IndexHashArgument).ToUpperInvariant();
                if (!File.Exists(indexPath) || !IsSha256(expectedIndexHash)
                    || !string.Equals(Sha256(indexPath), expectedIndexHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewIndexHashMismatch");
                var output = Path.GetFullPath(RequiredArgument(OutputArgument));
                Directory.CreateDirectory(output);
                if (Directory.EnumerateFileSystemEntries(output).Any())
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewOutputMustBeFresh");
                Directory.CreateDirectory(Path.Combine(output, "modules"));

                var scenePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..",
                    CanonicalScenePath));
                if (!File.Exists(scenePath))
                    throw new FileNotFoundException(
                        "AdministrativeDongDioramaReviewCanonicalSceneMissing", scenePath);

                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(PhaseKey, (int)Phase.AwaitingPlayMode);
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetString(IndexKey, indexPath);
                SessionState.SetString(IndexHashKey, expectedIndexHash);
                SessionState.SetString(OutputKey, output);
                SessionState.SetString(SceneHashKey, Sha256(scenePath));
                SessionState.SetString(StartedKey,
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                SessionState.SetString(DeadlineKey,
                    DateTime.UtcNow.AddMinutes(20).Ticks.ToString(CultureInfo.InvariantCulture));
                SessionState.SetInt(ModuleIndexKey, 0);
                SessionState.SetInt(ErrorCountKey, 0);
                SessionState.SetString(FirstErrorKey, string.Empty);
                evidence.Clear();
                targetOverviewPath = string.Empty;

                var scene = EditorSceneManager.OpenScene(CanonicalScenePath, OpenSceneMode.Single);
                if (!scene.IsValid() || scene.isDirty)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewCanonicalSceneOpenFailed");
                Debug.Log("[AdministrativeDongDioramaReviewCliCapture] 시작 · 30개 기초 공간 후보 · Scene 저장 없음");
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
                var deadline = long.Parse(SessionState.GetString(DeadlineKey, "0"),
                    CultureInfo.InvariantCulture);
                if (DateTime.UtcNow.Ticks > deadline)
                    throw new TimeoutException(
                        "AdministrativeDongDioramaReviewCaptureTimedOut");
                switch ((Phase)SessionState.GetInt(PhaseKey, 0))
                {
                    case Phase.AwaitingPlayMode: AwaitPlayMode(); break;
                    case Phase.LoadModule: LoadModule(); break;
                    case Phase.CaptureModule: CaptureModule(); break;
                    case Phase.AwaitModuleCapture: AwaitModuleCapture(); break;
                    case Phase.PrepareTargetDetail: PrepareTargetDetail(); break;
                    case Phase.CaptureTargetDetail: CaptureTargetDetail(); break;
                    case Phase.AwaitTargetDetail: AwaitTargetDetail(); break;
                    case Phase.Finalize: FinalizeEvidence(); break;
                    case Phase.StoppingSuccess: ExitWhenStopped(0); break;
                    case Phase.StoppingFailure: ExitWhenStopped(FailureExitCode); break;
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
            index = LoadAndValidateIndex();
            evidence.Clear();
            var root = new GameObject("행정동 디오라마 로컬 검토")
            {
                hideFlags = HideFlags.DontSave
            };
            root.transform.position = new Vector3(25000f, 0f, 25000f);
            root.AddComponent<행정동디오라마검토View>();
            SuppressOtherPresentationSurfaces(root);
            SetPhase(Phase.LoadModule);
        }

        private static void LoadModule()
        {
            if (!WaitFrames(3)) return;
            var moduleIndex = SessionState.GetInt(ModuleIndexKey, 0);
            if (moduleIndex < 0 || moduleIndex >= index.Bundles.Length)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewModuleIndexInvalid");
            activeEntry = index.Bundles[moduleIndex];
            activeBundle = LoadAndValidateBundle(index, activeEntry);
            var interpreter = new AdministrativeDongDioramaInterpreter();
            var applied = interpreter.Apply(activeBundle.Manifest, activeBundle.Tiles,
                activeBundle.DisplayOverlays);
            if (!applied.Accepted)
                throw new InvalidOperationException(applied.ErrorCode);

            var view = RequireView();
            if (!TryApply(view, applied, activeBundle, out var diagnostic))
                throw new InvalidOperationException(diagnostic);
            ValidateAppliedView(view, activeEntry);
            view.CompactEvidenceHud = true;
            view.Show(true);
            view.ShowWhole();
            SetPhase(Phase.CaptureModule);
        }

        private static void CaptureModule()
        {
            if (!WaitFrames(10)) return;
            var number = SessionState.GetInt(ModuleIndexKey, 0) + 1;
            var code = AreaCode(activeEntry.AdministrativeAreaStableId);
            pendingImagePath = Path.Combine(OutputDirectory(), "modules",
                $"{number:D2}-{code}-overview.png");
            ScreenCapture.CaptureScreenshot(pendingImagePath);
            if (string.Equals(activeEntry.AdministrativeAreaStableId, TargetArea,
                    StringComparison.Ordinal))
                targetOverviewPath = pendingImagePath;
            SetPhase(Phase.AwaitModuleCapture);
        }

        private static void AwaitModuleCapture()
        {
            if (!WaitForCapture(pendingImagePath)) return;
            var view = RequireView();
            evidence.Add(new ModuleEvidence
            {
                administrativeAreaStableId = activeEntry.AdministrativeAreaStableId,
                displayName = activeEntry.DisplayName,
                projectionHashSha256 = activeEntry.ProjectionHashSha256,
                bundleSha256 = activeEntry.Sha256,
                imageRelativePath = RelativeOutputPath(pendingImagePath),
                imageSha256 = Sha256(pendingImagePath),
                buildingCount = view.BuildReport.BuildingCount,
                roadSegmentCount = view.BuildReport.RoadSegmentCount,
                boundarySegmentCount = view.BuildReport.BoundarySegmentCount,
                tileCount = view.Snapshot.TileCount,
                rendererCount = view.GetComponentsInChildren<Renderer>(true).Length,
                meshFilterCount = view.GetComponentsInChildren<MeshFilter>(true).Length,
                colliderCount = view.GetComponentsInChildren<Collider>(true).Length,
                vertexCount = view.BuildReport.TotalVertexCount,
                indexCount = view.BuildReport.TotalIndexCount,
                maximumBatchVertexCount = view.BuildReport.MaximumBatchVertexCount,
                privateObservationOverlayCaptureReady =
                    activeEntry.PrivateObservationOverlayCaptureReady,
                privateObservationOverlaySetHashSha256 =
                    view.BuildReport.PrivateObservationOverlaySetHashSha256,
                crosswalkPointCandidateCount = view.BuildReport.CrosswalkPointCount,
                intersectionPointCandidateCount = view.BuildReport.IntersectionPointCount,
                privateObservationOverlayVertexCount =
                    view.BuildReport.PrivateObservationOverlayVertexCount,
                privateWalkNetworkOverlayCaptureReady =
                    activeEntry.PrivateWalkNetworkOverlayCaptureReady,
                privateWalkNetworkOverlaySetHashSha256 =
                    view.BuildReport.PrivateWalkNetworkOverlaySetHashSha256,
                walkNetworkNodePointCandidateCount =
                    view.BuildReport.WalkNetworkNodePointCount,
                walkNetworkLinkFragmentCandidateCount =
                    view.BuildReport.WalkNetworkLinkFragmentCount,
                walkNetworkLinkPathVertexCount =
                    view.BuildReport.WalkNetworkLinkPathVertexCount,
                walkNetworkOutputPathVertexCount =
                    view.BuildReport.WalkNetworkOutputPathVertexCount,
                privateWalkNetworkOverlayVertexCount =
                    view.BuildReport.PrivateWalkNetworkOverlayVertexCount,
                privateRoadDirectionOverlayCaptureReady =
                    activeEntry.PrivateRoadDirectionOverlayCaptureReady,
                privateRoadDirectionOverlaySetHashSha256 =
                    view.BuildReport.PrivateRoadDirectionOverlaySetHashSha256,
                roadDirectionPointCandidateCount =
                    view.BuildReport.RoadDirectionPointCandidateCount,
                roadDirectionUnresolvedCount =
                    view.BuildReport.RoadDirectionUnresolvedCount,
                roadDirectionRenderedArrowCount =
                    view.BuildReport.RoadDirectionRenderedArrowCount,
                privateRoadDirectionOverlayVertexCount =
                    view.BuildReport.PrivateRoadDirectionOverlayVertexCount,
                privateBiotopeOverlayCaptureReady = activeEntry.PrivateBiotopeOverlayCaptureReady,
                privateBiotopeOverlaySetHashSha256 = view.BuildReport.PrivateBiotopeOverlaySetHashSha256,
                biotopeCandidateCount = view.BuildReport.BiotopeCandidateCount,
                biotopePolygonPartCount = view.BuildReport.BiotopePolygonPartCount,
                biotopeInteriorRingCount = view.BuildReport.BiotopeInteriorRingCount,
                biotopeRingVertexCount = view.BuildReport.BiotopeRingVertexCount,
                biotopeSourceSegmentCount = view.BuildReport.BiotopeSourceSegmentCount,
                biotopeRenderedSegmentCount = view.BuildReport.BiotopeRenderedSegmentCount,
                biotopeSkippedAtOrBelowOneMillimeterSegmentCount = view.BuildReport.BiotopeSkippedAtOrBelowOneMillimeterSegmentCount,
                privateBiotopeOverlayVertexCount = view.BuildReport.PrivateBiotopeOverlayVertexCount,
                privateBiotopeOverlayIndexCount = view.BuildReport.PrivateBiotopeOverlayIndexCount,
                privateLaneMarkingOverlayCaptureReady =
                    activeEntry.PrivateLaneMarkingOverlayCaptureReady,
                privateLaneMarkingOverlaySetHashSha256 =
                    view.BuildReport.PrivateLaneMarkingOverlaySetHashSha256,
                laneMarkingCandidateCount = view.BuildReport.LaneMarkingCandidateCount,
                laneMarkingFragmentCount = view.BuildReport.LaneMarkingFragmentCount,
                laneMarkingPathVertexCount = view.BuildReport.LaneMarkingPathVertexCount,
                laneMarkingSourceSegmentCount = activeEntry.LaneMarkingSourceSegmentCount,
                laneMarkingRenderedSegmentCount = view.BuildReport.LaneMarkingRenderedSegmentCount,
                laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount =
                    activeEntry.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount,
                laneMarkingDuplicatePhysicalSegmentInstanceCount =
                    activeEntry.LaneMarkingDuplicatePhysicalSegmentInstanceCount,
                privateLaneMarkingOverlayVertexCount =
                    view.BuildReport.PrivateLaneMarkingOverlayVertexCount,
                privateLaneMarkingOverlayIndexCount =
                    view.BuildReport.PrivateLaneMarkingOverlayIndexCount,
                privateHorizontalContourTraceOverlayCaptureReady =
                    activeEntry.PrivateHorizontalContourTraceOverlayCaptureReady,
                privateHorizontalContourTraceOverlaySetHashSha256 =
                    view.BuildReport.PrivateHorizontalContourTraceOverlaySetHashSha256,
                horizontalContourTraceCandidateCount =
                    view.BuildReport.HorizontalContourTraceCandidateCount,
                horizontalContourTraceFragmentCount =
                    view.BuildReport.HorizontalContourTraceFragmentCount,
                horizontalContourTracePathVertexCount =
                    view.BuildReport.HorizontalContourTracePathVertexCount,
                horizontalContourTraceSourceSegmentCount =
                    activeEntry.HorizontalContourTraceSourceSegmentCount,
                horizontalContourTraceRenderedSegmentCount =
                    view.BuildReport.HorizontalContourTraceRenderedSegmentCount,
                horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount =
                    activeEntry.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount,
                horizontalContourTraceDuplicatePhysicalSegmentInstanceCount =
                    activeEntry.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount,
                privateHorizontalContourTraceOverlayVertexCount =
                    view.BuildReport.PrivateHorizontalContourTraceOverlayVertexCount,
                privateHorizontalContourTraceOverlayIndexCount =
                    view.BuildReport.PrivateHorizontalContourTraceOverlayIndexCount,
                readinessCode = activeBundle.DesiredReadinessCode
            });

            view.Clear();
            var next = SessionState.GetInt(ModuleIndexKey, 0) + 1;
            SessionState.SetInt(ModuleIndexKey, next);
            if (next < index.Bundles.Length)
            {
                SetPhase(Phase.LoadModule);
                return;
            }
            SetPhase(Phase.PrepareTargetDetail);
        }

        private static void PrepareTargetDetail()
        {
            if (!WaitFrames(4)) return;
            activeEntry = index.Bundles.Single(value => string.Equals(
                value.AdministrativeAreaStableId, TargetArea, StringComparison.Ordinal));
            activeBundle = LoadAndValidateBundle(index, activeEntry);
            var interpreter = new AdministrativeDongDioramaInterpreter();
            var applied = interpreter.Apply(activeBundle.Manifest, activeBundle.Tiles,
                activeBundle.DisplayOverlays);
            if (!applied.Accepted) throw new InvalidOperationException(applied.ErrorCode);
            var view = RequireView();
            if (!TryApply(view, applied, activeBundle, out var diagnostic))
                throw new InvalidOperationException(diagnostic);
            ValidateAppliedView(view, activeEntry);
            view.CompactEvidenceHud = false;
            var selected = view.Snapshot.Buildings
                .Where(value => value.FootprintArea >= 50d)
                .OrderBy(value => (value.CentroidX * value.CentroidX
                                   + value.CentroidZ * value.CentroidZ)
                                  / Math.Sqrt(value.FootprintArea))
                .ThenBy(value => value.BuildingStableId, StringComparer.Ordinal)
                .FirstOrDefault() ?? view.Snapshot.Buildings.First();
            if (!view.SelectBuilding(selected.BuildingStableId, true))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewDetailSelectionFailed");
            SetPhase(Phase.CaptureTargetDetail);
        }

        private static void CaptureTargetDetail()
        {
            if (!WaitFrames(14)) return;
            pendingImagePath = Path.Combine(OutputDirectory(),
                "31-1126057500-myeonmok-3-8-detail.png");
            ScreenCapture.CaptureScreenshot(pendingImagePath);
            SetPhase(Phase.AwaitTargetDetail);
        }

        private static void AwaitTargetDetail()
        {
            if (!WaitForCapture(pendingImagePath)) return;
            if (string.IsNullOrWhiteSpace(targetOverviewPath)
                || !File.Exists(targetOverviewPath))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewTargetOverviewMissing");
            SetPhase(Phase.Finalize);
        }

        private static void FinalizeEvidence()
        {
            if (evidence.Count != index.BundleCount
                || evidence.Select(value => value.administrativeAreaStableId)
                    .Distinct(StringComparer.Ordinal).Count() != index.BundleCount
                || evidence.Any(value => value.colliderCount != 0
                                         || value.meshFilterCount != value.rendererCount
                                         || value.maximumBatchVertexCount
                                         > 행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch
                                         || value.rendererCount
                                         > 행정동디오라마검토MeshBuilder.MaximumRendererCount))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEvidenceSetInvalid");
            if (index.PrivateObservationOverlayCaptureReady
                && (evidence.Sum(value => value.crosswalkPointCandidateCount)
                    != index.CrosswalkPointCandidateCount
                    || evidence.Sum(value => value.intersectionPointCandidateCount)
                    != index.IntersectionPointCandidateCount
                    || evidence.Any(value => !value.privateObservationOverlayCaptureReady
                                             || value.privateObservationOverlayVertexCount <= 0
                                             || !IsSha256(value
                                                 .privateObservationOverlaySetHashSha256))))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewOverlayEvidenceSetInvalid");
            if (index.PrivateWalkNetworkOverlayCaptureReady
                && (evidence.Sum(value => value.walkNetworkNodePointCandidateCount)
                    != index.WalkNetworkNodePointCandidateCount
                    || evidence.Sum(value => value.walkNetworkLinkFragmentCandidateCount)
                    != index.WalkNetworkLinkFragmentCandidateCount
                    || evidence.Sum(value => value.walkNetworkLinkPathVertexCount)
                    != index.WalkNetworkLinkPathVertexCount
                    || evidence.Sum(value => value.walkNetworkOutputPathVertexCount)
                    != index.WalkNetworkOutputPathVertexCount
                    || evidence.Any(value => !value.privateWalkNetworkOverlayCaptureReady
                                             || value.privateWalkNetworkOverlayVertexCount <= 0
                                             || !IsSha256(value
                                                 .privateWalkNetworkOverlaySetHashSha256))))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewWalkOverlayEvidenceSetInvalid");
            if (index.PrivateRoadDirectionOverlayCaptureReady
                && (evidence.Sum(value => value.roadDirectionPointCandidateCount)
                    != index.RoadDirectionPointCandidateCount
                    || evidence.Sum(value => value.roadDirectionUnresolvedCount)
                    != index.RoadDirectionPointCandidateCount
                    || evidence.Sum(value => value.roadDirectionRenderedArrowCount) != 0
                    || evidence.Sum(value => value.privateRoadDirectionOverlayVertexCount)
                    != index.RoadDirectionPointCandidateCount * 4
                    || evidence.Any(value => !value.privateRoadDirectionOverlayCaptureReady
                        || value.roadDirectionUnresolvedCount
                           != value.roadDirectionPointCandidateCount
                        || value.roadDirectionRenderedArrowCount != 0
                        || value.privateRoadDirectionOverlayVertexCount
                           != value.roadDirectionPointCandidateCount * 4
                        || !IsSha256(value.privateRoadDirectionOverlaySetHashSha256))))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewRoadDirectionEvidenceSetInvalid");
            if (index.PrivateBiotopeOverlayCaptureReady
                && (evidence.Sum(value => value.biotopeCandidateCount) != ExpectedBiotopeCandidateCount
                    || evidence.Sum(value => value.biotopePolygonPartCount) != ExpectedBiotopePolygonPartCount
                    || evidence.Sum(value => value.biotopeInteriorRingCount) != ExpectedBiotopeInteriorRingCount
                    || evidence.Sum(value => value.biotopeRingVertexCount) != ExpectedBiotopeRingVertexCount
                    || evidence.Sum(value => value.biotopeSourceSegmentCount) != ExpectedBiotopeSourceSegmentCount
                    || evidence.Sum(value => value.biotopeRenderedSegmentCount) != ExpectedBiotopeRenderedSegmentCount
                    || evidence.Sum(value => value.biotopeSkippedAtOrBelowOneMillimeterSegmentCount) != ExpectedBiotopeSkippedSegmentCount
                    || evidence.Sum(value => value.privateBiotopeOverlayVertexCount) != ExpectedBiotopeRenderedSegmentCount * 4
                    || evidence.Sum(value => value.privateBiotopeOverlayIndexCount) != ExpectedBiotopeRenderedSegmentCount * 6
                    || evidence.Any(value => !value.privateBiotopeOverlayCaptureReady
                        || value.biotopeRenderedSegmentCount + value.biotopeSkippedAtOrBelowOneMillimeterSegmentCount != value.biotopeSourceSegmentCount
                        || value.privateBiotopeOverlayVertexCount != value.biotopeRenderedSegmentCount * 4
                        || value.privateBiotopeOverlayIndexCount != value.biotopeRenderedSegmentCount * 6)))
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeEvidenceSetInvalid");
            if (index.PrivateLaneMarkingOverlayCaptureReady
                && (evidence.Sum(value => value.laneMarkingCandidateCount)
                    != index.LaneMarkingCandidateCount
                    || evidence.Sum(value => value.laneMarkingFragmentCount)
                    != index.LaneMarkingFragmentCount
                    || evidence.Sum(value => value.laneMarkingPathVertexCount)
                    != index.LaneMarkingPathVertexCount
                    || evidence.Sum(value => value.laneMarkingSourceSegmentCount)
                    != index.LaneMarkingSourceSegmentCount
                    || evidence.Sum(value => value.laneMarkingRenderedSegmentCount)
                    != index.LaneMarkingRenderedSegmentCount
                    || evidence.Sum(value =>
                           value.laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount)
                    != index.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                    || evidence.Sum(value =>
                           value.laneMarkingDuplicatePhysicalSegmentInstanceCount)
                    != index.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                    || evidence.Sum(value => value.privateLaneMarkingOverlayVertexCount)
                    != index.LaneMarkingRenderedSegmentCount * 4
                    || evidence.Sum(value => value.privateLaneMarkingOverlayIndexCount)
                    != index.LaneMarkingRenderedSegmentCount * 6
                    || evidence.Any(value => !value.privateLaneMarkingOverlayCaptureReady
                        || !IsSha256(value.privateLaneMarkingOverlaySetHashSha256)
                        || value.laneMarkingRenderedSegmentCount
                           + value.laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                           + value.laneMarkingDuplicatePhysicalSegmentInstanceCount
                           != value.laneMarkingSourceSegmentCount
                        || value.privateLaneMarkingOverlayVertexCount
                           != value.laneMarkingRenderedSegmentCount * 4
                        || value.privateLaneMarkingOverlayIndexCount
                           != value.laneMarkingRenderedSegmentCount * 6)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingEvidenceSetInvalid");
            if (index.PrivateHorizontalContourTraceOverlayCaptureReady
                && (evidence.Sum(value => value.horizontalContourTraceCandidateCount)
                    != index.HorizontalContourTraceCandidateCount
                    || evidence.Sum(value => value.horizontalContourTraceFragmentCount)
                    != index.HorizontalContourTraceFragmentCount
                    || evidence.Sum(value => value.horizontalContourTracePathVertexCount)
                    != index.HorizontalContourTracePathVertexCount
                    || evidence.Sum(value => value.horizontalContourTraceSourceSegmentCount)
                    != index.HorizontalContourTraceSourceSegmentCount
                    || evidence.Sum(value => value.horizontalContourTraceRenderedSegmentCount)
                    != index.HorizontalContourTraceRenderedSegmentCount
                    || evidence.Sum(value =>
                           value.horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount)
                    != index.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                    || evidence.Sum(value =>
                           value.horizontalContourTraceDuplicatePhysicalSegmentInstanceCount)
                    != index.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                    || evidence.Sum(value => value.privateHorizontalContourTraceOverlayVertexCount)
                    != index.HorizontalContourTraceRenderedSegmentCount * 4
                    || evidence.Sum(value => value.privateHorizontalContourTraceOverlayIndexCount)
                    != index.HorizontalContourTraceRenderedSegmentCount * 6
                    || evidence.Any(value =>
                        !value.privateHorizontalContourTraceOverlayCaptureReady
                        || !IsSha256(value.privateHorizontalContourTraceOverlaySetHashSha256)
                        || value.horizontalContourTraceRenderedSegmentCount
                           + value.horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                           + value.horizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                           != value.horizontalContourTraceSourceSegmentCount
                        || value.horizontalContourTracePathVertexCount
                           != value.horizontalContourTraceFragmentCount * 2
                        || value.horizontalContourTraceFragmentCount
                           != value.horizontalContourTraceRenderedSegmentCount
                        || value.privateHorizontalContourTraceOverlayVertexCount
                           != value.horizontalContourTraceRenderedSegmentCount * 4
                        || value.privateHorizontalContourTraceOverlayIndexCount
                           != value.horizontalContourTraceRenderedSegmentCount * 6)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewHorizontalContourTraceEvidenceSetInvalid");
            var view = RequireView();
            var manifest = new CaptureManifest
            {
                schemaVersion = index.PrivateHorizontalContourTraceOverlayCaptureReady
                    ? "administrative-dong-diorama-gameview-evidence.v8"
                    : index.PrivateLaneMarkingOverlayCaptureReady
                    ? "administrative-dong-diorama-gameview-evidence.v7"
                    : index.PrivateBiotopeOverlayCaptureReady
                    ? "administrative-dong-diorama-gameview-evidence.v6"
                    : index.PrivateRoadDirectionOverlayCaptureReady
                    ? "administrative-dong-diorama-gameview-evidence.v4"
                    : index.PrivateWalkNetworkOverlayCaptureReady
                    ? "administrative-dong-diorama-gameview-evidence.v3"
                    : index.PrivateObservationOverlayCaptureReady
                        ? "administrative-dong-diorama-gameview-evidence.v2"
                        : "administrative-dong-diorama-gameview-evidence.v1",
                capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = UnityEngine.Application.unityVersion,
                canonicalScenePath = CanonicalScenePath,
                sceneSha256Before = SessionState.GetString(SceneHashKey, string.Empty),
                sourceIndexPath = SessionState.GetString(IndexKey, string.Empty),
                sourceIndexSha256 = SessionState.GetString(IndexHashKey, string.Empty),
                sourceIndexContentHashSha256 = index.ContentHashSha256,
                scopeStableId = index.ScopeStableId,
                revision = index.Revision,
                candidateBatchId = index.CandidateBatchId,
                bundleSetHashSha256 = index.BundleSetHashSha256,
                projectionSetHashSha256 = index.ProjectionSetHashSha256,
                exporterSemanticRevision = index.ExporterSemanticRevision,
                exporterSourceHashSha256 = index.ExporterSourceHashSha256,
                generationHashSha256 = index.GenerationHashSha256,
                completionMarkerRelativePath = "complete.json",
                completionMarkerSha256 = Sha256(Path.Combine(
                    Path.GetDirectoryName(SessionState.GetString(IndexKey, string.Empty)),
                    "complete.json")),
                completionMarkerContentHashSha256 = completion.ContentHashSha256,
                administrativeAreaCount = index.AdministrativeAreaCount,
                buildingCount = index.BuildingCount,
                roadSegmentCount = index.RoadSegmentCount,
                tileCount = index.TileCount,
                unresolvedBuildingCount = index.UnresolvedBuildingCount,
                privateObservationOverlayCaptureReady =
                    index.PrivateObservationOverlayCaptureReady,
                privateObservationOverlaySetHashSha256 =
                    index.PrivateObservationOverlaySetHashSha256,
                crosswalkPointCandidateCount = index.CrosswalkPointCandidateCount,
                intersectionPointCandidateCount = index.IntersectionPointCandidateCount,
                privateWalkNetworkOverlayCaptureReady =
                    index.PrivateWalkNetworkOverlayCaptureReady,
                privateWalkNetworkOverlaySetHashSha256 =
                    index.PrivateWalkNetworkOverlaySetHashSha256,
                walkNetworkNodePointCandidateCount =
                    index.WalkNetworkNodePointCandidateCount,
                walkNetworkLinkFragmentCandidateCount =
                    index.WalkNetworkLinkFragmentCandidateCount,
                walkNetworkLinkPathVertexCount = index.WalkNetworkLinkPathVertexCount,
                walkNetworkOutputPathVertexCount =
                    index.WalkNetworkOutputPathVertexCount,
                privateRoadDirectionOverlayCaptureReady =
                    index.PrivateRoadDirectionOverlayCaptureReady,
                privateRoadDirectionOverlaySetHashSha256 =
                    index.PrivateRoadDirectionOverlaySetHashSha256,
                roadDirectionPointCandidateCount =
                    index.RoadDirectionPointCandidateCount,
                roadDirectionUnresolvedCount =
                    evidence.Sum(value => value.roadDirectionUnresolvedCount),
                roadDirectionRenderedArrowCount =
                    evidence.Sum(value => value.roadDirectionRenderedArrowCount),
                privateRoadDirectionOverlayVertexCount =
                    evidence.Sum(value => value.privateRoadDirectionOverlayVertexCount),
                roadDirectionAuditSha256 = index.RoadDirectionAuditSha256,
                directionSourceGenerationHashSha256 =
                    index.DirectionSourceGenerationHashSha256,
                privateBiotopeOverlayCaptureReady = index.PrivateBiotopeOverlayCaptureReady,
                privateBiotopeOverlaySetHashSha256 = index.PrivateBiotopeOverlaySetHashSha256,
                biotopeCandidateCount = evidence.Sum(value => value.biotopeCandidateCount),
                biotopePolygonPartCount = evidence.Sum(value => value.biotopePolygonPartCount),
                biotopeInteriorRingCount = evidence.Sum(value => value.biotopeInteriorRingCount),
                biotopeRingVertexCount = evidence.Sum(value => value.biotopeRingVertexCount),
                biotopeSourceSegmentCount = evidence.Sum(value => value.biotopeSourceSegmentCount),
                biotopeRenderedSegmentCount = evidence.Sum(value => value.biotopeRenderedSegmentCount),
                biotopeSkippedAtOrBelowOneMillimeterSegmentCount = evidence.Sum(value => value.biotopeSkippedAtOrBelowOneMillimeterSegmentCount),
                privateBiotopeOverlayVertexCount = evidence.Sum(value => value.privateBiotopeOverlayVertexCount),
                privateBiotopeOverlayIndexCount = evidence.Sum(value => value.privateBiotopeOverlayIndexCount),
                actualMeshVertexCount = evidence.Sum(value => value.vertexCount),
                actualMeshIndexCount = evidence.Sum(value => value.indexCount),
                actualRendererCount = evidence.Sum(value => value.rendererCount),
                actualMeshFilterCount = evidence.Sum(value => value.meshFilterCount),
                actualColliderCount = evidence.Sum(value => value.colliderCount),
                maximumModuleVertexCount = evidence.Max(value => value.vertexCount),
                maximumBatchVertexCount = evidence.Max(value => value.maximumBatchVertexCount),
                maximumAllowedBatchVertexCount =
                    행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch,
                biotopeAuditSha256 = index.BiotopeAuditSha256,
                biotopeSourceManifestSha256 = index.BiotopeSourceManifestSha256,
                privateLaneMarkingOverlayCaptureReady =
                    index.PrivateLaneMarkingOverlayCaptureReady,
                privateLaneMarkingOverlaySetHashSha256 =
                    index.PrivateLaneMarkingOverlaySetHashSha256,
                laneMarkingCandidateCount = evidence.Sum(value =>
                    value.laneMarkingCandidateCount),
                laneMarkingFragmentCount = evidence.Sum(value =>
                    value.laneMarkingFragmentCount),
                laneMarkingPathVertexCount = evidence.Sum(value =>
                    value.laneMarkingPathVertexCount),
                laneMarkingSourceSegmentCount = evidence.Sum(value =>
                    value.laneMarkingSourceSegmentCount),
                laneMarkingRenderedSegmentCount = evidence.Sum(value =>
                    value.laneMarkingRenderedSegmentCount),
                laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount = evidence.Sum(value =>
                    value.laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount),
                laneMarkingDuplicatePhysicalSegmentInstanceCount = evidence.Sum(value =>
                    value.laneMarkingDuplicatePhysicalSegmentInstanceCount),
                privateLaneMarkingOverlayVertexCount = evidence.Sum(value =>
                    value.privateLaneMarkingOverlayVertexCount),
                privateLaneMarkingOverlayIndexCount = evidence.Sum(value =>
                    value.privateLaneMarkingOverlayIndexCount),
                laneMarkingAuditSha256 = index.LaneMarkingAuditSha256,
                laneMarkingSourceManifestSha256 = index.LaneMarkingSourceManifestSha256,
                laneMarkingSourceGenerationHashSha256 =
                    index.LaneMarkingSourceGenerationHashSha256,
                laneMarkingSourceAuditSha256 = index.LaneMarkingSourceAuditSha256,
                privateHorizontalContourTraceOverlayCaptureReady =
                    index.PrivateHorizontalContourTraceOverlayCaptureReady,
                privateHorizontalContourTraceOverlaySetHashSha256 =
                    index.PrivateHorizontalContourTraceOverlaySetHashSha256,
                horizontalContourTraceCandidateCount = evidence.Sum(value =>
                    value.horizontalContourTraceCandidateCount),
                horizontalContourTraceFragmentCount = evidence.Sum(value =>
                    value.horizontalContourTraceFragmentCount),
                horizontalContourTracePathVertexCount = evidence.Sum(value =>
                    value.horizontalContourTracePathVertexCount),
                horizontalContourTraceSourceSegmentCount = evidence.Sum(value =>
                    value.horizontalContourTraceSourceSegmentCount),
                horizontalContourTraceRenderedSegmentCount = evidence.Sum(value =>
                    value.horizontalContourTraceRenderedSegmentCount),
                horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount = evidence.Sum(value =>
                    value.horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount),
                horizontalContourTraceDuplicatePhysicalSegmentInstanceCount = evidence.Sum(value =>
                    value.horizontalContourTraceDuplicatePhysicalSegmentInstanceCount),
                privateHorizontalContourTraceOverlayVertexCount = evidence.Sum(value =>
                    value.privateHorizontalContourTraceOverlayVertexCount),
                privateHorizontalContourTraceOverlayIndexCount = evidence.Sum(value =>
                    value.privateHorizontalContourTraceOverlayIndexCount),
                horizontalContourTraceAuditSha256 = index.HorizontalContourTraceAuditSha256,
                horizontalContourTraceSourceGenerationHashSha256 =
                    index.HorizontalContourTraceSourceGenerationHashSha256,
                horizontalContourTraceSourceManifestSha256 =
                    index.HorizontalContourTraceSourceManifestSha256,
                horizontalContourTraceSourceAuditSha256 =
                    index.HorizontalContourTraceSourceAuditSha256,
                moduleOverviewCaptureCount = evidence.Count,
                targetAdministrativeAreaStableId = TargetArea,
                targetOverviewRelativePath = RelativeOutputPath(targetOverviewPath),
                targetOverviewSha256 = Sha256(targetOverviewPath),
                targetDetailRelativePath = RelativeOutputPath(pendingImagePath),
                targetDetailSha256 = Sha256(pendingImagePath),
                targetSelectedBuildingStableId = view.SelectedBuilding.BuildingStableId,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                overviewHudMode = "CompactSourceAndCount",
                detailHudMode = "FullEvidenceAndSelection",
                consoleErrorCountDuringRun = SessionState.GetInt(ErrorCountKey, 0),
                firstConsoleErrorDuringRun = SessionState.GetString(FirstErrorKey, string.Empty),
                actualPlayMode = true,
                actualGameViewCapture = true,
                geometryOnlyCandidateEvidence =
                    !index.PrivateObservationOverlayCaptureReady,
                privateObservationOverlayCandidateEvidence =
                    index.PrivateObservationOverlayCaptureReady,
                privateWalkNetworkOverlayCandidateEvidence =
                    index.PrivateWalkNetworkOverlayCaptureReady,
                privateRoadDirectionOverlayCandidateEvidence =
                    index.PrivateRoadDirectionOverlayCaptureReady,
                privateLaneMarkingOverlayCandidateEvidence =
                    index.PrivateLaneMarkingOverlayCaptureReady,
                privateHorizontalContourTraceOverlayCandidateEvidence =
                    index.PrivateHorizontalContourTraceOverlayCaptureReady,
                laneMeaningAuthorized = false,
                laneDirectionAuthorized = false,
                laneTrafficAuthorized = false,
                laneStopLineAuthorized = false,
                laneSignalAuthorized = false,
                flatHorizontalTraceOnly =
                    index.PrivateHorizontalContourTraceOverlayCaptureReady,
                sourceHorizontalGeometryObserved =
                    index.PrivateHorizontalContourTraceOverlayCaptureReady,
                sourceAttributeDatabaseRead = false,
                sourceElevationValuesRead = false,
                sourceElevationValuesSerialized = false,
                currentBoundaryEstablished = false,
                currentTerrainEstablished = false,
                contourMeaningAuthorized = false,
                elevationMeaningAuthorized = false,
                heightUnitVerified = false,
                verticalDatumVerified = false,
                terrainSurfaceAuthorized = false,
                terrainMeshAuthorized = false,
                directionUnresolvedOnly = index.PrivateRoadDirectionOverlayCaptureReady,
                angleSemanticVerified = false,
                directionResolved = false,
                renderedArrowAuthorized = false,
                sourceLengthUnitVerified = false,
                renderedDirectionArrowCount = evidence.Sum(value =>
                    value.roadDirectionRenderedArrowCount),
                historicalBoundaryCandidate = true,
                currentPointerUsed = false,
                sceneSaved = false,
                observationPresentationOnly = true,
                distributionApproved = false,
                traversalReady = false,
                gameplayReady = false,
                addressVerified = false,
                parcelGeometryVerified = false,
                entranceVerified = false,
                laneSignalSidewalkVerified = false,
                npcOrOperationalLifecycleVerified = false,
                lifeDioramaParityVerified = false,
                missingLayerCodes = RequiredMissingLayers,
                modules = evidence.ToArray()
            };
            File.WriteAllText(Path.Combine(OutputDirectory(), "capture-manifest.json"),
                JsonConvert.SerializeObject(manifest, Formatting.Indented));
            Debug.Log("[AdministrativeDongDioramaReviewCliCapture] 30개 Game View 기초 공간 캡처 완료");
            SetPhase(Phase.StoppingSuccess);
            EditorApplication.isPlaying = false;
        }

        private static bool TryApply(행정동디오라마검토View view,
            AdministrativeDongDioramaApplyResult applied, ReviewBundle bundle,
            out string diagnostic)
        {
            if (bundle.PrivateHorizontalContourTraceOverlayCaptureReady)
                return view.TryApply(applied, bundle.PrivateObservationOverlays,
                    bundle.PrivateWalkNetworkOverlays, bundle.PrivateRoadDirectionOverlays,
                    bundle.PrivateBiotopeOverlays, bundle.PrivateLaneMarkingOverlays,
                    bundle.PrivateHorizontalContourTraceOverlays,
                    true, out diagnostic);
            if (bundle.PrivateLaneMarkingOverlayCaptureReady)
                return view.TryApply(applied, bundle.PrivateObservationOverlays,
                    bundle.PrivateWalkNetworkOverlays, bundle.PrivateRoadDirectionOverlays,
                    bundle.PrivateBiotopeOverlays, bundle.PrivateLaneMarkingOverlays,
                    true, out diagnostic);
            if (bundle.PrivateBiotopeOverlayCaptureReady)
                return view.TryApply(applied, bundle.PrivateObservationOverlays,
                    bundle.PrivateWalkNetworkOverlays, bundle.PrivateRoadDirectionOverlays,
                    bundle.PrivateBiotopeOverlays, true, out diagnostic);
            if (bundle.PrivateRoadDirectionOverlayCaptureReady)
                return view.TryApply(applied, bundle.PrivateObservationOverlays,
                    bundle.PrivateWalkNetworkOverlays,
                    bundle.PrivateRoadDirectionOverlays, true, out diagnostic);
            if (bundle.PrivateWalkNetworkOverlayCaptureReady)
                return view.TryApply(applied, bundle.PrivateObservationOverlays,
                    bundle.PrivateWalkNetworkOverlays, true, out diagnostic);
            if (bundle.PrivateObservationOverlayCaptureReady)
                return view.TryApply(applied, bundle.PrivateObservationOverlays, true,
                    out diagnostic);
            return view.TryApply(applied, true, out diagnostic);
        }

        private static void ValidateAppliedView(행정동디오라마검토View view,
            ReviewEntry entry)
        {
            var report = view.BuildReport;
            var overlay = view.Snapshot?.PrivateObservationOverlay;
            var walkOverlay = view.Snapshot?.PrivateWalkNetworkOverlay;
            var directionOverlay = view.Snapshot?.PrivateRoadDirectionOverlay;
            var rendererLimit = 행정동디오라마검토MeshBuilder.MaximumRendererCount;
            if (view.Snapshot == null || report == null
                || view.Snapshot.Buildings.Count != entry.BuildingCount
                || view.Snapshot.Roads.Count != entry.RoadSegmentCount
                || view.Snapshot.TileCount != entry.TileCount
                || view.GetComponentsInChildren<Collider>(true).Length != 0
                || report.RendererBatchCount > rendererLimit
                || view.GetComponentsInChildren<Renderer>(true).Length > rendererLimit
                || view.GetComponentsInChildren<MeshFilter>(true).Length
                   != view.GetComponentsInChildren<Renderer>(true).Length)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewPresentationCountMismatch");

            if (entry.PrivateObservationOverlayCaptureReady)
            {
                if (overlay == null
                    || !string.Equals(overlay.OverlaySetHashSha256,
                        entry.PrivateObservationOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || overlay.CrosswalkPoints.Count != entry.CrosswalkPointCandidateCount
                    || overlay.IntersectionPoints.Count
                    != entry.IntersectionPointCandidateCount
                    || !string.Equals(report.PrivateObservationOverlaySetHashSha256,
                        entry.PrivateObservationOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || report.CrosswalkPointCount != entry.CrosswalkPointCandidateCount
                    || report.IntersectionPointCount
                    != entry.IntersectionPointCandidateCount
                    || report.PrivateObservationOverlayVertexCount <= 0)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewOverlayPresentationCountMismatch");
            }
            else if (overlay != null
                || !string.IsNullOrEmpty(report.PrivateObservationOverlaySetHashSha256)
                || report.CrosswalkPointCount != 0 || report.IntersectionPointCount != 0
                || report.PrivateObservationOverlayVertexCount != 0)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnexpectedOverlayPresentation");

            if (entry.PrivateWalkNetworkOverlayCaptureReady)
            {
                if (walkOverlay == null
                    || !string.Equals(walkOverlay.OverlaySetHashSha256,
                        entry.PrivateWalkNetworkOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || walkOverlay.NodePoints.Count
                    != entry.WalkNetworkNodePointCandidateCount
                    || walkOverlay.LinkFragments.Count
                    != entry.WalkNetworkLinkFragmentCandidateCount
                    || walkOverlay.LinkPathVertexCount
                    != entry.WalkNetworkLinkPathVertexCount
                    || walkOverlay.NodePoints.Count + walkOverlay.LinkPathVertexCount
                    != entry.WalkNetworkOutputPathVertexCount
                    || !string.Equals(report.PrivateWalkNetworkOverlaySetHashSha256,
                        entry.PrivateWalkNetworkOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || report.WalkNetworkNodePointCount
                    != entry.WalkNetworkNodePointCandidateCount
                    || report.WalkNetworkLinkFragmentCount
                    != entry.WalkNetworkLinkFragmentCandidateCount
                    || report.WalkNetworkLinkPathVertexCount
                    != entry.WalkNetworkLinkPathVertexCount
                    || report.WalkNetworkOutputPathVertexCount
                    != entry.WalkNetworkOutputPathVertexCount
                    || report.PrivateWalkNetworkOverlayVertexCount <= 0)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewWalkOverlayPresentationCountMismatch");
                if (!entry.PrivateRoadDirectionOverlayCaptureReady
                    && !entry.PrivateBiotopeOverlayCaptureReady
                    && !entry.PrivateLaneMarkingOverlayCaptureReady
                    && !entry.PrivateHorizontalContourTraceOverlayCaptureReady) return;
            }

            if (entry.PrivateRoadDirectionOverlayCaptureReady)
            {
                if (directionOverlay == null
                    || directionOverlay.CandidatePoints.Count
                    != entry.RoadDirectionPointCandidateCount
                    || report.RoadDirectionPointCandidateCount
                    != entry.RoadDirectionPointCandidateCount
                    || report.RoadDirectionUnresolvedCount
                    != entry.RoadDirectionPointCandidateCount
                    || report.RoadDirectionRenderedArrowCount != 0
                    || report.PrivateRoadDirectionOverlayVertexCount
                    != entry.RoadDirectionPointCandidateCount * 4
                    || view.ModelRoot.GetComponentsInChildren<Transform>(true).Length
                       != report.RendererBatchCount + 1
                    || !string.Equals(report.PrivateRoadDirectionOverlaySetHashSha256,
                        entry.PrivateRoadDirectionOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewRoadDirectionPresentationMismatch");
            }
            else if (directionOverlay != null || report.RoadDirectionPointCandidateCount != 0
                     || report.RoadDirectionUnresolvedCount != 0
                     || report.RoadDirectionRenderedArrowCount != 0
                     || report.PrivateRoadDirectionOverlayVertexCount != 0)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnexpectedRoadDirectionPresentation");

            var biotopeOverlay = view.Snapshot?.PrivateBiotopeOverlay;
            if (entry.PrivateBiotopeOverlayCaptureReady)
            {
                if (biotopeOverlay == null || biotopeOverlay.Candidates.Count != entry.BiotopeCandidateCount
                    || biotopeOverlay.PolygonPartCount != entry.BiotopePolygonPartCount
                    || biotopeOverlay.InteriorRingCount != entry.BiotopeInteriorRingCount
                    || biotopeOverlay.RingVertexCount != entry.BiotopeRingVertexCount
                    || report.BiotopeCandidateCount != entry.BiotopeCandidateCount
                    || report.BiotopePolygonPartCount != entry.BiotopePolygonPartCount
                    || report.BiotopeInteriorRingCount != entry.BiotopeInteriorRingCount
                    || report.BiotopeRingVertexCount != entry.BiotopeRingVertexCount
                    || report.BiotopeSourceSegmentCount != entry.BiotopeSourceSegmentCount
                    || report.BiotopeSkippedAtOrBelowOneMillimeterSegmentCount != entry.BiotopeSkippedSegmentCount
                    || report.BiotopeRenderedSegmentCount != entry.BiotopeRenderedSegmentCount
                    || report.PrivateBiotopeOverlayVertexCount != entry.BiotopeRenderedSegmentCount * 4
                    || report.PrivateBiotopeOverlayIndexCount != entry.BiotopeRenderedSegmentCount * 6
                    || !string.Equals(report.PrivateBiotopeOverlaySetHashSha256, entry.PrivateBiotopeOverlaySetHashSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopePresentationMismatch");
            }

            var laneOverlay = view.Snapshot?.PrivateLaneMarkingOverlay;
            if (entry.PrivateLaneMarkingOverlayCaptureReady)
            {
                if (laneOverlay == null
                    || laneOverlay.Candidates.Count != entry.LaneMarkingCandidateCount
                    || laneOverlay.FragmentCount != entry.LaneMarkingFragmentCount
                    || laneOverlay.PathVertexCount != entry.LaneMarkingPathVertexCount
                    || laneOverlay.SegmentCount != entry.LaneMarkingRenderedSegmentCount
                    || report.LaneMarkingCandidateCount != entry.LaneMarkingCandidateCount
                    || report.LaneMarkingFragmentCount != entry.LaneMarkingFragmentCount
                    || report.LaneMarkingPathVertexCount != entry.LaneMarkingPathVertexCount
                    || report.LaneMarkingInputSegmentCount
                       != entry.LaneMarkingRenderedSegmentCount
                    || report.LaneMarkingRenderedSegmentCount
                       != entry.LaneMarkingRenderedSegmentCount
                    || report.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount != 0
                    || report.PrivateLaneMarkingOverlayVertexCount
                       != entry.LaneMarkingRenderedSegmentCount * 4
                    || report.PrivateLaneMarkingOverlayIndexCount
                       != entry.LaneMarkingRenderedSegmentCount * 6
                    || !string.Equals(report.PrivateLaneMarkingOverlaySetHashSha256,
                        entry.PrivateLaneMarkingOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || view.ModelRoot.GetComponentsInChildren<Transform>(true).Length
                       != report.RendererBatchCount + 1)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewLaneMarkingPresentationMismatch");
            }
            else if (laneOverlay != null || report.LaneMarkingCandidateCount != 0
                     || report.LaneMarkingFragmentCount != 0
                     || report.LaneMarkingPathVertexCount != 0
                     || report.LaneMarkingInputSegmentCount != 0
                     || report.LaneMarkingRenderedSegmentCount != 0
                     || report.PrivateLaneMarkingOverlayVertexCount != 0
                     || report.PrivateLaneMarkingOverlayIndexCount != 0
                     || !string.IsNullOrEmpty(report.PrivateLaneMarkingOverlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnexpectedLaneMarkingPresentation");

            var contourOverlay = view.Snapshot?.PrivateHorizontalContourTraceOverlay;
            if (entry.PrivateHorizontalContourTraceOverlayCaptureReady)
            {
                if (contourOverlay == null
                    || contourOverlay.Candidates.Count
                       != entry.HorizontalContourTraceCandidateCount
                    || contourOverlay.FragmentCount
                       != entry.HorizontalContourTraceFragmentCount
                    || contourOverlay.PathVertexCount
                       != entry.HorizontalContourTracePathVertexCount
                    || contourOverlay.SegmentCount
                       != entry.HorizontalContourTraceRenderedSegmentCount
                    || report.HorizontalContourTraceCandidateCount
                       != entry.HorizontalContourTraceCandidateCount
                    || report.HorizontalContourTraceFragmentCount
                       != entry.HorizontalContourTraceFragmentCount
                    || report.HorizontalContourTracePathVertexCount
                       != entry.HorizontalContourTracePathVertexCount
                    || report.HorizontalContourTraceInputSegmentCount
                       != entry.HorizontalContourTraceRenderedSegmentCount
                    || report.HorizontalContourTraceRenderedSegmentCount
                       != entry.HorizontalContourTraceRenderedSegmentCount
                    || report.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount != 0
                    || report.PrivateHorizontalContourTraceOverlayVertexCount
                       != entry.HorizontalContourTraceRenderedSegmentCount * 4
                    || report.PrivateHorizontalContourTraceOverlayIndexCount
                       != entry.HorizontalContourTraceRenderedSegmentCount * 6
                    || !string.Equals(report.PrivateHorizontalContourTraceOverlaySetHashSha256,
                        entry.PrivateHorizontalContourTraceOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || view.ModelRoot.GetComponentsInChildren<Transform>(true).Length
                       != report.RendererBatchCount + 1)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewHorizontalContourTracePresentationMismatch");
            }
            else if (contourOverlay != null
                     || report.HorizontalContourTraceCandidateCount != 0
                     || report.HorizontalContourTraceFragmentCount != 0
                     || report.HorizontalContourTracePathVertexCount != 0
                     || report.HorizontalContourTraceInputSegmentCount != 0
                     || report.HorizontalContourTraceRenderedSegmentCount != 0
                     || report.PrivateHorizontalContourTraceOverlayVertexCount != 0
                     || report.PrivateHorizontalContourTraceOverlayIndexCount != 0
                     || !string.IsNullOrEmpty(
                         report.PrivateHorizontalContourTraceOverlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnexpectedHorizontalContourTracePresentation");

            if (!entry.PrivateWalkNetworkOverlayCaptureReady && (walkOverlay != null
                || !string.IsNullOrEmpty(report.PrivateWalkNetworkOverlaySetHashSha256)
                || report.WalkNetworkNodePointCount != 0
                || report.WalkNetworkLinkFragmentCount != 0
                || report.WalkNetworkLinkPathVertexCount != 0
                || report.WalkNetworkOutputPathVertexCount != 0
                || report.PrivateWalkNetworkOverlayVertexCount != 0))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnexpectedWalkOverlayPresentation");
        }

        private static ReviewIndex LoadAndValidateIndex()
        {
            var path = SessionState.GetString(IndexKey, string.Empty);
            if (!string.Equals(Sha256(path), SessionState.GetString(IndexHashKey, string.Empty),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewIndexChangedAfterStart");
            var json = File.ReadAllText(path);
            var raw = ParseJsonObject(json,
                "AdministrativeDongDioramaReviewIndexInvalid");
            var value = raw.ToObject<ReviewIndex>()
                        ?? throw new InvalidOperationException(
                            "AdministrativeDongDioramaReviewIndexInvalid");
            var horizontalContourTraceReview =
                IsHorizontalContourTraceReview(value.SchemaVersion);
            var laneMarkingReview = IsLaneMarkingReview(value.SchemaVersion)
                                    || horizontalContourTraceReview;
            var biotopeReview = IsBiotopeReview(value.SchemaVersion) || laneMarkingReview;
            var roadDirectionReview = IsRoadDirectionReview(value.SchemaVersion) || biotopeReview;
            var walkNetworkReview = IsWalkNetworkReview(value.SchemaVersion) || biotopeReview
                                    || roadDirectionReview;
            var observationReview = walkNetworkReview || biotopeReview
                                    || IsObservationReview(value.SchemaVersion);
            var expectedSchema = horizontalContourTraceReview
                ? HorizontalContourTraceIndexSchema
                : laneMarkingReview ? LaneMarkingIndexSchema
                : biotopeReview ? BiotopeIndexSchema : roadDirectionReview ? RoadDirectionIndexSchema
                : walkNetworkReview ? WalkNetworkIndexSchema
                : observationReview ? ObservationIndexSchema : BaseIndexSchema;
            var expectedExporter = horizontalContourTraceReview
                ? HorizontalContourTraceExporterRevision
                : laneMarkingReview ? LaneMarkingExporterRevision
                : biotopeReview ? BiotopeExporterRevision : roadDirectionReview ? RoadDirectionExporterRevision
                : walkNetworkReview ? WalkNetworkExporterRevision
                : observationReview ? ObservationExporterRevision : BaseExporterRevision;
            if (horizontalContourTraceReview && (!string.Equals(Sha256(path),
                    ExpectedHorizontalContourTraceIndexSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.GenerationHashSha256,
                    ExpectedHorizontalContourTraceGenerationHash,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.ExporterSourceHashSha256,
                    ExpectedHorizontalContourTraceExporterSha256,
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewHorizontalContourTraceGenerationMismatch");
            if (IsLaneMarkingReview(value.SchemaVersion) && (!string.Equals(Sha256(path),
                    ExpectedLaneMarkingIndexSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.GenerationHashSha256,
                    ExpectedLaneMarkingGenerationHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.ExporterSourceHashSha256,
                    ExpectedLaneMarkingExporterSha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingGenerationMismatch");
            if (biotopeReview && !laneMarkingReview
                && (!string.Equals(Sha256(path), ExpectedBiotopeIndexSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.GenerationHashSha256, ExpectedBiotopeGenerationHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.ExporterSourceHashSha256, ExpectedBiotopeExporterSha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeGenerationMismatch");
            if (!biotopeReview && roadDirectionReview
                && (!string.Equals(Sha256(path), ExpectedRoadDirectionIndexSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(value.GenerationHashSha256,
                        ExpectedRoadDirectionGenerationHash, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewRoadDirectionGenerationMismatch");
            RequireEnvelope(value.SchemaVersion, expectedSchema, value.DesiredReadinessCode,
                value.CandidateEnvelopeIsPublicationAuthority,
                value.CandidateEnvelopeReadinessGovernsPromotion,
                value.EmbeddedManifestReadinessIsInformational, value.PublishBlocked,
                value.CurrentPointerUsed, value.CurrentPointerUpdated,
                value.ObservationPresentationOnly, value.DistributionApproved,
                value.TraversalReady, value.GameplayReady, value.BaseGeometryCaptureReady,
                value.LifeDioramaCaptureReady, value.PersonalDataIncluded,
                value.MissingLayerCodes);
            if (observationReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewIndexContentHashInvalid");
                RequireEnvelopeBooleanProperties(raw);
            }
            if (!string.Equals(value.ExporterSemanticRevision, expectedExporter,
                    StringComparison.Ordinal)
                || !IsSha256(value.ExporterSourceHashSha256)
                || !IsSha256(value.GenerationHashSha256)
                || !string.Equals(value.GenerationHashSha256, value.BundleSetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)),
                    value.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                || value.AdministrativeAreaCount != 30 || value.BundleCount != 30
                || value.Bundles == null || value.Bundles.Length != 30
                || value.Bundles.Select(item => item.AdministrativeAreaStableId)
                    .Distinct(StringComparer.Ordinal).Count() != 30
                || value.Bundles.Any(item => item == null
                                             || !IsSha256(item.Sha256)
                                             || item.ByteLength <= 0
                                             || !item.BaseGeometryCaptureReady
                                             || item.LifeDioramaCaptureReady
                                             || !SetEquals(item.MissingLayerCodes,
                                                  RequiredMissingLayers))
                || value.Bundles.Sum(item => item.BuildingCount) != value.BuildingCount
                || value.Bundles.Sum(item => item.RoadSegmentCount) != value.RoadSegmentCount
                || value.Bundles.Sum(item => item.TileCount) != value.TileCount)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewIndexCountInvalid");

            if (observationReview)
            {
                var orderedOverlayHashes = value.Bundles
                    .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                    .Select(item => item.PrivateObservationOverlaySetHashSha256)
                    .ToArray();
                if (!value.PrivateObservationOverlayCaptureReady
                    || value.CrosswalkPointCandidateCount
                    != ExpectedCrosswalkPointCandidateCount
                    || value.IntersectionPointCandidateCount
                    != ExpectedIntersectionPointCandidateCount
                    || !IsSha256(value.PrivateObservationOverlaySetHashSha256)
                    || value.Bundles.Any(item =>
                        !item.PrivateObservationOverlayCaptureReady
                        || !IsSha256(item.ContentHashSha256)
                        || !IsSha256(item.PrivateObservationOverlaySetHashSha256)
                        || item.CrosswalkPointCandidateCount < 0
                        || item.IntersectionPointCandidateCount < 0)
                    || value.Bundles.Sum(item => item.CrosswalkPointCandidateCount)
                    != value.CrosswalkPointCandidateCount
                    || value.Bundles.Sum(item => item.IntersectionPointCandidateCount)
                    != value.IntersectionPointCandidateCount
                    || !string.Equals(Sha256Text(JsonConvert.SerializeObject(
                            orderedOverlayHashes, Formatting.None)),
                        value.PrivateObservationOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewOverlayIndexInvalid");
            }
            else if (value.PrivateObservationOverlayCaptureReady
                     || value.CrosswalkPointCandidateCount != 0
                     || value.IntersectionPointCandidateCount != 0
                     || !string.IsNullOrEmpty(value.PrivateObservationOverlaySetHashSha256)
                     || value.Bundles.Any(item => item.PrivateObservationOverlayCaptureReady
                                                  || item.CrosswalkPointCandidateCount != 0
                                                  || item.IntersectionPointCandidateCount != 0
                                                  || !string.IsNullOrEmpty(item
                                                      .PrivateObservationOverlaySetHashSha256)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewBaseIndexOverlayRejected");

            if (walkNetworkReview)
            {
                var orderedWalkHashes = value.Bundles
                    .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                    .Select(item => item.PrivateWalkNetworkOverlaySetHashSha256)
                    .ToArray();
                if (!value.PrivateWalkNetworkOverlayCaptureReady
                    || !IsSha256(value.PrivateWalkNetworkOverlaySetHashSha256)
                    || value.WalkNetworkNodePointCandidateCount <= 0
                    || value.WalkNetworkLinkFragmentCandidateCount <= 0
                    || value.WalkNetworkLinkPathVertexCount
                    < value.WalkNetworkLinkFragmentCandidateCount * 2
                    || value.WalkNetworkOutputPathVertexCount
                    != value.WalkNetworkNodePointCandidateCount
                       + value.WalkNetworkLinkPathVertexCount
                    || value.Bundles.Any(item =>
                        !item.PrivateWalkNetworkOverlayCaptureReady
                        || !IsSha256(item.PrivateWalkNetworkOverlaySetHashSha256)
                        || item.WalkNetworkNodePointCandidateCount < 0
                        || item.WalkNetworkLinkFragmentCandidateCount < 0
                        || item.WalkNetworkLinkPathVertexCount
                        < item.WalkNetworkLinkFragmentCandidateCount * 2
                        || item.WalkNetworkOutputPathVertexCount
                        != item.WalkNetworkNodePointCandidateCount
                           + item.WalkNetworkLinkPathVertexCount)
                    || value.Bundles.Sum(item => item.WalkNetworkNodePointCandidateCount)
                    != value.WalkNetworkNodePointCandidateCount
                    || value.Bundles.Sum(item => item.WalkNetworkLinkFragmentCandidateCount)
                    != value.WalkNetworkLinkFragmentCandidateCount
                    || value.Bundles.Sum(item => item.WalkNetworkLinkPathVertexCount)
                    != value.WalkNetworkLinkPathVertexCount
                    || value.Bundles.Sum(item => item.WalkNetworkOutputPathVertexCount)
                    != value.WalkNetworkOutputPathVertexCount
                    || !string.Equals(Sha256Text(JsonConvert.SerializeObject(
                            orderedWalkHashes, Formatting.None)),
                        value.PrivateWalkNetworkOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewWalkOverlayIndexInvalid");
            }
            else if (value.PrivateWalkNetworkOverlayCaptureReady
                     || value.WalkNetworkNodePointCandidateCount != 0
                     || value.WalkNetworkLinkFragmentCandidateCount != 0
                     || value.WalkNetworkLinkPathVertexCount != 0
                     || value.WalkNetworkOutputPathVertexCount != 0
                     || !string.IsNullOrEmpty(value.PrivateWalkNetworkOverlaySetHashSha256)
                     || value.Bundles.Any(item => item.PrivateWalkNetworkOverlayCaptureReady
                         || item.WalkNetworkNodePointCandidateCount != 0
                         || item.WalkNetworkLinkFragmentCandidateCount != 0
                         || item.WalkNetworkLinkPathVertexCount != 0
                         || item.WalkNetworkOutputPathVertexCount != 0
                         || !string.IsNullOrEmpty(item
                             .PrivateWalkNetworkOverlaySetHashSha256)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierIndexWalkOverlayRejected");

            if (roadDirectionReview)
            {
                var orderedDirectionHashes = value.Bundles
                    .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                    .Select(item => item.PrivateRoadDirectionOverlaySetHashSha256)
                    .ToArray();
                if (!value.PrivateRoadDirectionOverlayCaptureReady
                    || value.RoadDirectionPointCandidateCount
                       != ExpectedRoadDirectionPointCandidateCount
                    || !IsSha256(value.PrivateRoadDirectionOverlaySetHashSha256)
                    || !IsSha256(value.RoadDirectionAuditSha256)
                    || !IsSha256(value.DirectionSourceGenerationHashSha256)
                    || !IsRoadDirectionAuditPathAllowed(value.SchemaVersion,
                        value.RoadDirectionAuditRelativePath)
                    || value.Bundles.Any(item =>
                        !item.PrivateRoadDirectionOverlayCaptureReady
                        || !IsSha256(item.PrivateRoadDirectionOverlaySetHashSha256)
                        || item.RoadDirectionPointCandidateCount <= 0)
                    || value.Bundles.Sum(item => item.RoadDirectionPointCandidateCount)
                       != value.RoadDirectionPointCandidateCount
                    || !string.Equals(Sha256Text(JsonConvert.SerializeObject(
                            orderedDirectionHashes, Formatting.None)),
                        value.PrivateRoadDirectionOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewRoadDirectionIndexInvalid");
                ValidateRoadDirectionAudit(path, value, biotopeReview);
            }
            else if (value.PrivateRoadDirectionOverlayCaptureReady
                     || value.RoadDirectionPointCandidateCount != 0
                     || !string.IsNullOrEmpty(value.PrivateRoadDirectionOverlaySetHashSha256)
                     || value.Bundles.Any(item => item.PrivateRoadDirectionOverlayCaptureReady
                         || item.RoadDirectionPointCandidateCount != 0
                         || !string.IsNullOrEmpty(
                             item.PrivateRoadDirectionOverlaySetHashSha256)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierIndexRoadDirectionRejected");

            if (biotopeReview)
            {
                var hashes = value.Bundles.OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                    .Select(item => item.PrivateBiotopeOverlaySetHashSha256).ToArray();
                if (!value.PrivateBiotopeOverlayCaptureReady
                    || value.BiotopeCandidateCount != ExpectedBiotopeCandidateCount
                    || value.BiotopePolygonPartCount != ExpectedBiotopePolygonPartCount
                    || value.BiotopeInteriorRingCount != ExpectedBiotopeInteriorRingCount
                    || value.BiotopeRingVertexCount != ExpectedBiotopeRingVertexCount
                    || value.BiotopeSourceSegmentCount != ExpectedBiotopeSourceSegmentCount
                    || value.BiotopeSkippedSegmentCount != ExpectedBiotopeSkippedSegmentCount
                    || value.BiotopeRenderedSegmentCount != ExpectedBiotopeRenderedSegmentCount
                    || !IsSha256(value.PrivateBiotopeOverlaySetHashSha256)
                    || !IsSha256(value.BiotopeAuditSha256) || !IsSha256(value.BiotopeSourceManifestSha256)
                    || !string.Equals(value.BiotopeAuditRelativePath,
                        laneMarkingReview ? "biotope-audit.json" : "audit.json",
                        StringComparison.Ordinal)
                    || value.Bundles.Any(item => !item.PrivateBiotopeOverlayCaptureReady
                        || !IsSha256(item.PrivateBiotopeOverlaySetHashSha256))
                    || value.Bundles.Sum(item => item.BiotopeCandidateCount) != value.BiotopeCandidateCount
                    || value.Bundles.Sum(item => item.BiotopePolygonPartCount) != value.BiotopePolygonPartCount
                    || value.Bundles.Sum(item => item.BiotopeInteriorRingCount) != value.BiotopeInteriorRingCount
                    || value.Bundles.Sum(item => item.BiotopeRingVertexCount) != value.BiotopeRingVertexCount
                    || value.Bundles.Sum(item => item.BiotopeSourceSegmentCount) != value.BiotopeSourceSegmentCount
                    || value.Bundles.Sum(item => item.BiotopeSkippedSegmentCount) != value.BiotopeSkippedSegmentCount
                    || value.Bundles.Sum(item => item.BiotopeRenderedSegmentCount) != value.BiotopeRenderedSegmentCount
                    || !string.Equals(Sha256Text(JsonConvert.SerializeObject(hashes, Formatting.None)),
                        value.PrivateBiotopeOverlaySetHashSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeIndexInvalid");
                ValidateBiotopeAudit(path, value);
            }
            else if (value.PrivateBiotopeOverlayCaptureReady || value.BiotopeCandidateCount != 0
                || value.Bundles.Any(item => item.PrivateBiotopeOverlayCaptureReady || item.BiotopeCandidateCount != 0))
                throw new InvalidOperationException("AdministrativeDongDioramaReviewEarlierIndexBiotopeRejected");

            if (laneMarkingReview)
            {
                var hashes = value.Bundles
                    .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                    .Select(item => item.PrivateLaneMarkingOverlaySetHashSha256).ToArray();
                if (!value.PrivateLaneMarkingOverlayCaptureReady
                    || !IsSha256(value.PrivateLaneMarkingOverlaySetHashSha256)
                    || value.LaneMarkingCandidateCount
                       != ExpectedLaneMarkingCandidateCount
                    || value.LaneMarkingFragmentCount
                       != ExpectedLaneMarkingFragmentCount
                    || value.LaneMarkingPathVertexCount
                       != ExpectedLaneMarkingPathVertexCount
                    || value.LaneMarkingSourceSegmentCount
                       != ExpectedLaneMarkingSourceSegmentCount
                    || value.LaneMarkingRenderedSegmentCount
                       != ExpectedLaneMarkingRenderedSegmentCount
                    || value.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                       != ExpectedLaneMarkingSkippedSegmentCount
                    || value.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                       != ExpectedLaneMarkingDuplicateSegmentCount
                    || value.LaneMarkingSourceSegmentCount
                       != value.LaneMarkingRenderedSegmentCount
                          + value.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                          + value.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                    || value.LaneMarkingRenderedSegmentCount <= 0
                    || !string.Equals(value.LaneMarkingAuditRelativePath,
                        "lane-marking-audit.json", StringComparison.Ordinal)
                    || !IsSha256(value.LaneMarkingAuditSha256)
                    || !IsSha256(value.LaneMarkingSourceManifestSha256)
                    || !IsSha256(value.LaneMarkingSourceGenerationHashSha256)
                    || !IsSha256(value.LaneMarkingSourceAuditSha256)
                    || value.Bundles.Any(item =>
                        !item.PrivateLaneMarkingOverlayCaptureReady
                        || !IsSha256(item.PrivateLaneMarkingOverlaySetHashSha256)
                        || item.LaneMarkingCandidateCount < 0
                        || item.LaneMarkingFragmentCount < 0
                        || item.LaneMarkingPathVertexCount
                           != item.LaneMarkingFragmentCount * 2
                        || item.LaneMarkingRenderedSegmentCount
                           != item.LaneMarkingFragmentCount
                        || item.LaneMarkingSourceSegmentCount
                           != item.LaneMarkingRenderedSegmentCount
                              + item.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                              + item.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                        || item.LaneMarkingRenderedSegmentCount < 0)
                    || value.Bundles.Sum(item => item.LaneMarkingCandidateCount)
                       != value.LaneMarkingCandidateCount
                    || value.Bundles.Sum(item => item.LaneMarkingFragmentCount)
                       != value.LaneMarkingFragmentCount
                    || value.Bundles.Sum(item => item.LaneMarkingPathVertexCount)
                       != value.LaneMarkingPathVertexCount
                    || value.Bundles.Sum(item => item.LaneMarkingSourceSegmentCount)
                       != value.LaneMarkingSourceSegmentCount
                    || value.Bundles.Sum(item => item.LaneMarkingRenderedSegmentCount)
                       != value.LaneMarkingRenderedSegmentCount
                    || value.Bundles.Sum(item =>
                           item.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount)
                       != value.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                    || value.Bundles.Sum(item =>
                           item.LaneMarkingDuplicatePhysicalSegmentInstanceCount)
                       != value.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                    || !string.Equals(Sha256Text(JsonConvert.SerializeObject(
                            hashes, Formatting.None)),
                        value.PrivateLaneMarkingOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewLaneMarkingIndexInvalid");
                ValidateLaneMarkingAudit(path, value);
            }
            else if (value.PrivateLaneMarkingOverlayCaptureReady
                     || value.LaneMarkingCandidateCount != 0
                     || value.LaneMarkingFragmentCount != 0
                     || value.LaneMarkingPathVertexCount != 0
                     || value.LaneMarkingSourceSegmentCount != 0
                     || value.LaneMarkingRenderedSegmentCount != 0
                     || value.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount != 0
                     || value.LaneMarkingDuplicatePhysicalSegmentInstanceCount != 0
                     || !string.IsNullOrEmpty(value.PrivateLaneMarkingOverlaySetHashSha256)
                     || value.Bundles.Any(item =>
                         item.PrivateLaneMarkingOverlayCaptureReady
                         || item.LaneMarkingCandidateCount != 0
                         || item.LaneMarkingFragmentCount != 0
                         || item.LaneMarkingPathVertexCount != 0
                         || item.LaneMarkingSourceSegmentCount != 0
                         || item.LaneMarkingRenderedSegmentCount != 0
                         || item.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount != 0
                         || item.LaneMarkingDuplicatePhysicalSegmentInstanceCount != 0))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierIndexLaneMarkingRejected");

            if (horizontalContourTraceReview)
            {
                RequireBooleanProperty(raw,
                    "privateHorizontalContourTraceOverlayCaptureReady", true);
                RequireBooleanProperty(raw, "sourceHorizontalGeometryObserved", true);
                foreach (var name in new[]
                         {
                             "sourceAttributeDatabaseRead", "sourceElevationValuesRead",
                             "sourceElevationValuesSerialized", "currentBoundaryEstablished",
                             "currentTerrainEstablished", "contourMeaningAuthorized",
                             "elevationMeaningAuthorized", "heightUnitVerified",
                             "verticalDatumVerified", "terrainSurfaceAuthorized",
                             "terrainMeshAuthorized"
                         })
                    RequireBooleanProperty(raw, name, false);
                var hashes = value.Bundles
                    .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                    .Select(item => item.PrivateHorizontalContourTraceOverlaySetHashSha256)
                    .ToArray();
                if (!value.PrivateHorizontalContourTraceOverlayCaptureReady
                    || !IsSha256(value.PrivateHorizontalContourTraceOverlaySetHashSha256)
                    || value.HorizontalContourTraceCandidateCount
                       != ExpectedHorizontalContourTraceCandidateCount
                    || value.HorizontalContourTraceFragmentCount
                       != ExpectedHorizontalContourTraceFragmentCount
                    || value.HorizontalContourTracePathVertexCount
                       != ExpectedHorizontalContourTracePathVertexCount
                    || value.HorizontalContourTraceSourceSegmentCount
                       != ExpectedHorizontalContourTraceSourceSegmentCount
                    || value.HorizontalContourTraceRenderedSegmentCount
                       != ExpectedHorizontalContourTraceRenderedSegmentCount
                    || value.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                       != ExpectedHorizontalContourTraceSkippedSegmentCount
                    || value.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                       != ExpectedHorizontalContourTraceDuplicateSegmentCount
                    || value.HorizontalContourTracePathVertexCount
                       != value.HorizontalContourTraceFragmentCount * 2
                    || value.HorizontalContourTraceRenderedSegmentCount
                       != value.HorizontalContourTraceFragmentCount
                    || value.HorizontalContourTraceSourceSegmentCount
                       != value.HorizontalContourTraceRenderedSegmentCount
                          + value.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                          + value.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                    || !string.Equals(value.HorizontalContourTraceAuditRelativePath,
                        "horizontal-contour-trace-audit.json", StringComparison.Ordinal)
                    || !string.Equals(value.HorizontalContourTraceAuditSha256,
                        ExpectedHorizontalContourTraceAuditSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(value.HorizontalContourTraceSourceGenerationHashSha256,
                        ExpectedHorizontalContourTraceSourceGenerationHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(value.HorizontalContourTraceSourceManifestSha256,
                        ExpectedHorizontalContourTraceSourceManifestSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(value.HorizontalContourTraceSourceAuditSha256,
                        ExpectedHorizontalContourTraceSourceAuditSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !value.SourceHorizontalGeometryObserved
                    || value.SourceAttributeDatabaseRead
                    || value.SourceElevationValuesRead
                    || value.SourceElevationValuesSerialized
                    || value.CurrentBoundaryEstablished
                    || value.CurrentTerrainEstablished
                    || value.ContourMeaningAuthorized
                    || value.ElevationMeaningAuthorized
                    || value.HeightUnitVerified
                    || value.VerticalDatumVerified
                    || value.TerrainSurfaceAuthorized
                    || value.TerrainMeshAuthorized
                    || value.Bundles.Any(item =>
                        !item.PrivateHorizontalContourTraceOverlayCaptureReady
                        || !IsSha256(item.PrivateHorizontalContourTraceOverlaySetHashSha256)
                        || item.HorizontalContourTraceCandidateCount <= 0
                        || item.HorizontalContourTraceFragmentCount <= 0
                        || item.HorizontalContourTracePathVertexCount
                           != item.HorizontalContourTraceFragmentCount * 2
                        || item.HorizontalContourTraceRenderedSegmentCount
                           != item.HorizontalContourTraceFragmentCount
                        || item.HorizontalContourTraceSourceSegmentCount
                           != item.HorizontalContourTraceRenderedSegmentCount
                              + item.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                              + item.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount)
                    || value.Bundles.Sum(item => item.HorizontalContourTraceCandidateCount)
                       != value.HorizontalContourTraceCandidateCount
                    || value.Bundles.Sum(item => item.HorizontalContourTraceFragmentCount)
                       != value.HorizontalContourTraceFragmentCount
                    || value.Bundles.Sum(item => item.HorizontalContourTracePathVertexCount)
                       != value.HorizontalContourTracePathVertexCount
                    || value.Bundles.Sum(item => item.HorizontalContourTraceSourceSegmentCount)
                       != value.HorizontalContourTraceSourceSegmentCount
                    || value.Bundles.Sum(item => item.HorizontalContourTraceRenderedSegmentCount)
                       != value.HorizontalContourTraceRenderedSegmentCount
                    || value.Bundles.Sum(item =>
                           item.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount)
                       != value.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                    || value.Bundles.Sum(item =>
                           item.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount)
                       != value.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                    || !string.Equals(Sha256Text(JsonConvert.SerializeObject(
                            hashes, Formatting.None)),
                        value.PrivateHorizontalContourTraceOverlaySetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewHorizontalContourTraceIndexInvalid");
                ValidateHorizontalContourTraceAudit(path, value);
            }
            else if (value.PrivateHorizontalContourTraceOverlayCaptureReady
                     || value.HorizontalContourTraceCandidateCount != 0
                     || value.HorizontalContourTraceFragmentCount != 0
                     || value.HorizontalContourTracePathVertexCount != 0
                     || value.HorizontalContourTraceSourceSegmentCount != 0
                     || value.HorizontalContourTraceRenderedSegmentCount != 0
                     || value.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount != 0
                     || value.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount != 0
                     || !string.IsNullOrEmpty(
                         value.PrivateHorizontalContourTraceOverlaySetHashSha256)
                     || value.Bundles.Any(item =>
                         item.PrivateHorizontalContourTraceOverlayCaptureReady
                         || item.HorizontalContourTraceCandidateCount != 0
                         || item.HorizontalContourTraceFragmentCount != 0
                         || item.HorizontalContourTracePathVertexCount != 0
                         || item.HorizontalContourTraceSourceSegmentCount != 0
                         || item.HorizontalContourTraceRenderedSegmentCount != 0))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierIndexHorizontalContourTraceRejected");

            completion = ValidateCompletionMarker(path, value, observationReview,
                walkNetworkReview, roadDirectionReview, biotopeReview, laneMarkingReview,
                horizontalContourTraceReview);
            value.Bundles = value.Bundles
                .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                .ToArray();
            return value;
        }

        private static void ValidateRoadDirectionAudit(string indexPath, ReviewIndex owner,
            bool biotopeReview)
        {
            var inputRoot = Path.GetDirectoryName(indexPath);
            var auditPath = ResolveInputFile(inputRoot,
                owner.RoadDirectionAuditRelativePath,
                "AdministrativeDongDioramaReviewRoadDirectionAuditPathInvalid");
            if (!AuditFileHashMatches(owner.RoadDirectionAuditSha256, Sha256(auditPath)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewRoadDirectionAuditHashMismatch");
            var raw = ParseJsonObject(File.ReadAllText(auditPath),
                "AdministrativeDongDioramaReviewRoadDirectionAuditInvalid");
            RequireContentHash(raw, "contentHashSha256",
                "AdministrativeDongDioramaReviewRoadDirectionAuditContentHashInvalid");
            var counts = raw["counts"] as JObject;
            var perArea = raw["perAdministrativeArea"] as JObject;
            if (!string.Equals(raw.Value<string>("schemaVersion"),
                    "administrative-dong-diorama-unity-review-audit.v4",
                    StringComparison.Ordinal)
                || !string.Equals(raw.Value<string>("generationHashSha256"),
                    biotopeReview ? ExpectedRoadDirectionGenerationHash
                        : owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>("directionGenerationHashSha256"),
                    owner.DirectionSourceGenerationHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || counts == null || perArea == null
                || counts.Value<int>("administrativeAreas") != owner.AdministrativeAreaCount
                || counts.Value<int>("roadDirectionPointCandidates")
                   != ExpectedRoadDirectionPointCandidateCount
                || counts.Value<int>("forbiddenDirectionOverlayFields") != 0
                || perArea.Count != owner.Bundles.Length
                || owner.Bundles.Any(entry =>
                    perArea.Value<int?>(entry.AdministrativeAreaStableId)
                    != entry.RoadDirectionPointCandidateCount))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewRoadDirectionAuditCountMismatch");
            foreach (var name in new[]
                     {
                         "privateReviewVisualizationAllowed", "reviewVisualizationOnly",
                         "historicalBoundaryBootstrapOnly"
                     })
                RequireBooleanProperty(raw, name, true);
            foreach (var name in new[]
                     {
                         "angleSemanticVerified", "directionResolved",
                         "renderedArrowAuthorized", "sourceLengthUnitVerified",
                         "publicDisplayAllowed", "runtimeAuthorized", "traversalReady",
                         "gameplayReady", "currentPointerUsed", "currentPointerUpdated",
                         "sourceCandidateUnityApplyAllowed"
                     })
                RequireBooleanProperty(raw, name, false);
        }

        public static bool IsRoadDirectionAuditPathAllowed(string schemaVersion,
            string relativePath)
            => string.Equals(schemaVersion, RoadDirectionIndexSchema, StringComparison.Ordinal)
                ? string.Equals(relativePath, "audit.json", StringComparison.Ordinal)
                : (string.Equals(schemaVersion, BiotopeIndexSchema, StringComparison.Ordinal)
                   || string.Equals(schemaVersion, LaneMarkingIndexSchema,
                       StringComparison.Ordinal)
                   || string.Equals(schemaVersion, HorizontalContourTraceIndexSchema,
                       StringComparison.Ordinal))
                  && string.Equals(relativePath, "road-direction-audit.json",
                      StringComparison.Ordinal);

        public static bool AuditFileHashMatches(string expectedSha256, string actualSha256)
            => IsSha256(expectedSha256) && IsSha256(actualSha256)
               && string.Equals(expectedSha256, actualSha256, StringComparison.OrdinalIgnoreCase);

        public static bool AuditDescriptorPathsMatch(IEnumerable<string> actualPaths,
            IEnumerable<string> expectedPaths)
        {
            if (actualPaths == null || expectedPaths == null) return false;
            var actual=actualPaths.ToArray(); var expected=expectedPaths.ToArray();
            return actual.All(value=>!string.IsNullOrWhiteSpace(value))
                   && expected.All(value=>!string.IsNullOrWhiteSpace(value))
                   && actual.Distinct(StringComparer.Ordinal).Count()==actual.Length
                   && expected.Distinct(StringComparer.Ordinal).Count()==expected.Length
                   && actual.Length==expected.Length
                   && new HashSet<string>(actual,StringComparer.Ordinal).SetEquals(expected);
        }

        private static ReviewCompletion ValidateCompletionMarker(string indexPath,
            ReviewIndex owner, bool observationReview, bool walkNetworkReview,
            bool roadDirectionReview, bool biotopeReview, bool laneMarkingReview,
            bool horizontalContourTraceReview)
        {
            var markerPath = Path.Combine(Path.GetDirectoryName(indexPath), "complete.json");
            if (!File.Exists(markerPath))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewCompletionMarkerMissing");
            var json = File.ReadAllText(markerPath);
            var raw = ParseJsonObject(json,
                "AdministrativeDongDioramaReviewCompletionMarkerInvalid");
            var marker = raw.ToObject<ReviewCompletion>()
                         ?? throw new InvalidOperationException(
                             "AdministrativeDongDioramaReviewCompletionMarkerInvalid");
            if (horizontalContourTraceReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewCompletionContentHashInvalid");
                RequireBooleanProperty(raw,
                    "privateHorizontalContourTraceOverlayCaptureReady", true);
                RequireBooleanProperty(raw, "sourceHorizontalGeometryObserved", true);
                foreach (var name in new[]
                         {
                             "sourceAttributeDatabaseRead", "sourceElevationValuesRead",
                             "sourceElevationValuesSerialized", "currentBoundaryEstablished",
                             "currentTerrainEstablished", "contourMeaningAuthorized",
                             "elevationMeaningAuthorized", "heightUnitVerified",
                             "verticalDatumVerified", "terrainSurfaceAuthorized",
                             "terrainMeshAuthorized", "currentPointerCreated",
                             "currentPointerUsed", "currentPointerUpdated",
                             "distributionApproved", "publicDisplayAllowed",
                             "runtimeAuthorized", "traversalReady", "gameplayReady",
                             "sourceCandidateUnityApplyAllowed"
                         })
                    RequireBooleanProperty(raw, name, false);
                if (!string.Equals(marker.SchemaVersion,
                        HorizontalContourTraceCompletionSchema, StringComparison.Ordinal)
                    || !string.Equals(marker.Revision, owner.Revision,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.IndexRelativePath, "index.json",
                        StringComparison.Ordinal)
                    || !string.Equals(marker.IndexSha256, Sha256(indexPath),
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.GenerationHashSha256,
                        owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BundleSetHashSha256,
                        owner.BundleSetHashSha256, StringComparison.OrdinalIgnoreCase)
                    || marker.BundleCount != owner.BundleCount
                    || !string.Equals(marker.ExporterSemanticRevision,
                        owner.ExporterSemanticRevision, StringComparison.Ordinal)
                    || !string.Equals(marker.ExporterSourceHashSha256,
                        owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.PublishBlocked || marker.CurrentPointerCreated
                    || marker.CurrentPointerUsed || marker.CurrentPointerUpdated
                    || marker.DistributionApproved || marker.PublicDisplayAllowed
                    || marker.RuntimeAuthorized || marker.TraversalReady
                    || marker.GameplayReady || marker.SourceCandidateUnityApplyAllowed
                    || !marker.PrivateHorizontalContourTraceOverlayCaptureReady
                    || marker.HorizontalContourTraceCandidateCount
                       != owner.HorizontalContourTraceCandidateCount
                    || marker.HorizontalContourTraceFragmentCount
                       != owner.HorizontalContourTraceFragmentCount
                    || marker.HorizontalContourTracePathVertexCount
                       != owner.HorizontalContourTracePathVertexCount
                    || marker.HorizontalContourTraceSourceSegmentCount
                       != owner.HorizontalContourTraceSourceSegmentCount
                    || marker.HorizontalContourTraceRenderedSegmentCount
                       != owner.HorizontalContourTraceRenderedSegmentCount
                    || marker.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                       != owner.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                    || marker.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                       != owner.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                    || !string.Equals(marker.HorizontalContourTraceAuditSha256,
                        owner.HorizontalContourTraceAuditSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.HorizontalContourTraceSourceGenerationHashSha256,
                        owner.HorizontalContourTraceSourceGenerationHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.HorizontalContourTraceSourceManifestSha256,
                        owner.HorizontalContourTraceSourceManifestSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.HorizontalContourTraceSourceAuditSha256,
                        owner.HorizontalContourTraceSourceAuditSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.LaneMarkingAuditSha256,
                        owner.LaneMarkingAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BiotopeAuditSha256,
                        owner.BiotopeAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.RoadDirectionAuditSha256,
                        owner.RoadDirectionAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.SourceHorizontalGeometryObserved
                    || marker.SourceAttributeDatabaseRead
                    || marker.SourceElevationValuesRead
                    || marker.SourceElevationValuesSerialized
                    || marker.CurrentBoundaryEstablished
                    || marker.CurrentTerrainEstablished
                    || marker.ContourMeaningAuthorized
                    || marker.ElevationMeaningAuthorized
                    || marker.HeightUnitVerified
                    || marker.VerticalDatumVerified
                    || marker.TerrainSurfaceAuthorized
                    || marker.TerrainMeshAuthorized)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewHorizontalContourTraceCompletionMismatch");
                ValidateCompletionFiles(indexPath, owner, marker.Files,
                    new AuditFileExpectation(owner.BiotopeAuditRelativePath,
                        marker.BiotopeAuditSha256),
                    new AuditFileExpectation(owner.RoadDirectionAuditRelativePath,
                        marker.RoadDirectionAuditSha256),
                    new AuditFileExpectation(owner.LaneMarkingAuditRelativePath,
                        marker.LaneMarkingAuditSha256),
                    new AuditFileExpectation(owner.HorizontalContourTraceAuditRelativePath,
                        marker.HorizontalContourTraceAuditSha256));
                return marker;
            }
            if (laneMarkingReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewCompletionContentHashInvalid");
                RequireBooleanProperty(raw, "privateLaneMarkingOverlayCaptureReady", true);
                foreach (var name in new[]
                         {
                             "currentPointerCreated", "currentPointerUsed",
                             "currentPointerUpdated", "distributionApproved",
                             "publicDisplayAllowed", "runtimeAuthorized", "traversalReady",
                             "gameplayReady", "sourceCandidateUnityApplyAllowed"
                         })
                    RequireBooleanProperty(raw, name, false);
                if (marker.SchemaVersion != LaneMarkingCompletionSchema
                    || marker.Revision != owner.Revision
                    || marker.IndexRelativePath != "index.json"
                    || !string.Equals(marker.IndexSha256, Sha256(indexPath),
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.GenerationHashSha256,
                        owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BundleSetHashSha256,
                        owner.BundleSetHashSha256, StringComparison.OrdinalIgnoreCase)
                    || marker.BundleCount != owner.BundleCount
                    || marker.ExporterSemanticRevision != owner.ExporterSemanticRevision
                    || !string.Equals(marker.ExporterSourceHashSha256,
                        owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.PublishBlocked || marker.CurrentPointerCreated
                    || marker.CurrentPointerUsed || marker.CurrentPointerUpdated
                    || marker.DistributionApproved || marker.PublicDisplayAllowed
                    || marker.RuntimeAuthorized || marker.TraversalReady
                    || marker.GameplayReady || marker.SourceCandidateUnityApplyAllowed
                    || !marker.PrivateLaneMarkingOverlayCaptureReady
                    || marker.LaneMarkingCandidateCount != owner.LaneMarkingCandidateCount
                    || marker.LaneMarkingFragmentCount != owner.LaneMarkingFragmentCount
                    || marker.LaneMarkingPathVertexCount != owner.LaneMarkingPathVertexCount
                    || marker.LaneMarkingSourceSegmentCount
                       != owner.LaneMarkingSourceSegmentCount
                    || marker.LaneMarkingRenderedSegmentCount
                       != owner.LaneMarkingRenderedSegmentCount
                    || marker.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                       != owner.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                    || marker.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                       != owner.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                    || !string.Equals(marker.LaneMarkingAuditSha256,
                        owner.LaneMarkingAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.LaneMarkingSourceManifestSha256,
                        owner.LaneMarkingSourceManifestSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.LaneMarkingSourceGenerationHashSha256,
                        owner.LaneMarkingSourceGenerationHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.LaneMarkingSourceAuditSha256,
                        owner.LaneMarkingSourceAuditSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BiotopeAuditSha256,
                        owner.BiotopeAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.RoadDirectionAuditSha256,
                        owner.RoadDirectionAuditSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewLaneMarkingCompletionMismatch");
                ValidateCompletionFiles(indexPath, owner, marker.Files,
                    new AuditFileExpectation(owner.BiotopeAuditRelativePath,
                        marker.BiotopeAuditSha256),
                    new AuditFileExpectation(owner.RoadDirectionAuditRelativePath,
                        marker.RoadDirectionAuditSha256),
                    new AuditFileExpectation(owner.LaneMarkingAuditRelativePath,
                        marker.LaneMarkingAuditSha256));
                return marker;
            }
            if (biotopeReview)
            {
                RequireContentHash(raw, "contentHashSha256", "AdministrativeDongDioramaReviewCompletionContentHashInvalid");
                RequireBooleanProperty(raw, "privateBiotopeOverlayCaptureReady", true);
                RequireBooleanProperty(raw, "publicDisplayAllowed", false);
                RequireBooleanProperty(raw, "runtimeAuthorized", false);
                RequireBooleanProperty(raw, "traversalReady", false);
                RequireBooleanProperty(raw, "gameplayReady", false);
                RequireBooleanProperty(raw, "sourceCandidateUnityApplyAllowed", false);
                if (marker.SchemaVersion != BiotopeCompletionSchema
                    || marker.Revision != owner.Revision
                    || marker.IndexRelativePath != "index.json"
                    || !string.Equals(marker.IndexSha256, Sha256(indexPath), StringComparison.OrdinalIgnoreCase)
                    || marker.GenerationHashSha256 != owner.GenerationHashSha256
                    || marker.BundleSetHashSha256 != owner.BundleSetHashSha256
                    || marker.BundleCount != owner.BundleCount
                    || marker.ExporterSemanticRevision != owner.ExporterSemanticRevision
                    || !string.Equals(marker.ExporterSourceHashSha256, owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.PublishBlocked || marker.CurrentPointerCreated || marker.CurrentPointerUsed || marker.CurrentPointerUpdated
                    || marker.DistributionApproved || marker.PublicDisplayAllowed || marker.RuntimeAuthorized
                    || marker.TraversalReady || marker.GameplayReady || marker.SourceCandidateUnityApplyAllowed
                    || marker.BiotopeCandidateCount != owner.BiotopeCandidateCount
                    || marker.BiotopePolygonPartCount != owner.BiotopePolygonPartCount
                    || marker.BiotopeInteriorRingCount != owner.BiotopeInteriorRingCount
                    || marker.BiotopeRingVertexCount != owner.BiotopeRingVertexCount
                    || marker.BiotopeSourceSegmentCount != owner.BiotopeSourceSegmentCount
                    || marker.BiotopeSkippedSegmentCount != owner.BiotopeSkippedSegmentCount
                    || marker.BiotopeRenderedSegmentCount != owner.BiotopeRenderedSegmentCount
                    || marker.BiotopeAuditSha256 != owner.BiotopeAuditSha256
                    || marker.BiotopeSourceManifestSha256 != owner.BiotopeSourceManifestSha256)
                    throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeCompletionMismatch");
                if (!string.Equals(marker.RoadDirectionAuditSha256, owner.RoadDirectionAuditSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeDirectionAuditHashMismatch");
                ValidateCompletionFiles(indexPath, owner, marker.Files,
                    new AuditFileExpectation("audit.json", marker.BiotopeAuditSha256),
                    new AuditFileExpectation(owner.RoadDirectionAuditRelativePath,
                        marker.RoadDirectionAuditSha256));
                return marker;
            }
            if (roadDirectionReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewCompletionContentHashInvalid");
                RequireBooleanProperty(raw, "privateRoadDirectionOverlayCaptureReady", true);
                RequireBooleanProperty(raw, "currentPointerCreated", false);
                RequireBooleanProperty(raw, "currentPointerUsed", false);
                RequireBooleanProperty(raw, "currentPointerUpdated", false);
                RequireBooleanProperty(raw, "distributionApproved", false);
                RequireBooleanProperty(raw, "publicDisplayAllowed", false);
                RequireBooleanProperty(raw, "runtimeAuthorized", false);
                RequireBooleanProperty(raw, "traversalReady", false);
                RequireBooleanProperty(raw, "gameplayReady", false);
                RequireBooleanProperty(raw, "sourceCandidateUnityApplyAllowed", false);
                RequireBooleanProperty(raw, "angleSemanticVerified", false);
                RequireBooleanProperty(raw, "directionResolved", false);
                RequireBooleanProperty(raw, "renderedArrowAuthorized", false);
                RequireBooleanProperty(raw, "sourceLengthUnitVerified", false);
                if (!string.Equals(marker.SchemaVersion, RoadDirectionCompletionSchema,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.Revision, owner.Revision,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.GenerationHashSha256,
                        owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BundleSetHashSha256,
                        owner.BundleSetHashSha256, StringComparison.OrdinalIgnoreCase)
                    || marker.BundleCount != owner.BundleCount
                    || !string.Equals(marker.ExporterSemanticRevision,
                        owner.ExporterSemanticRevision, StringComparison.Ordinal)
                    || !string.Equals(marker.ExporterSourceHashSha256,
                        owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.PrivateRoadDirectionOverlayCaptureReady
                    || marker.RoadDirectionPointCandidateCount
                       != owner.RoadDirectionPointCandidateCount
                    || marker.RoadDirectionPointCandidateCount
                       != ExpectedRoadDirectionPointCandidateCount
                    || !string.Equals(marker.RoadDirectionAuditSha256,
                        owner.RoadDirectionAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.DirectionSourceGenerationHashSha256,
                        owner.DirectionSourceGenerationHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.WalkNetworkAuditSha256,
                        owner.WalkNetworkAuditSha256, StringComparison.OrdinalIgnoreCase)
                    || marker.WalkNetworkNodePointCandidateCount
                       != owner.WalkNetworkNodePointCandidateCount
                    || marker.WalkNetworkLinkFragmentCandidateCount
                       != owner.WalkNetworkLinkFragmentCandidateCount
                    || marker.WalkNetworkLinkPathVertexCount
                       != owner.WalkNetworkLinkPathVertexCount
                    || marker.WalkNetworkOutputPathVertexCount
                       != owner.WalkNetworkOutputPathVertexCount)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewDirectionCompletionMismatch");
                ValidateCompletionFiles(indexPath, owner, marker.Files,
                    new AuditFileExpectation(owner.RoadDirectionAuditRelativePath,
                        marker.RoadDirectionAuditSha256));
                return marker;
            }
            if (walkNetworkReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewCompletionContentHashInvalid");
                RequireBooleanProperty(raw, "privateWalkNetworkOverlayCaptureReady", true);
                RequireBooleanProperty(raw, "currentPointerCreated", false);
                RequireBooleanProperty(raw, "currentPointerUsed", false);
                RequireBooleanProperty(raw, "currentPointerUpdated", false);
                RequireBooleanProperty(raw, "distributionApproved", false);
                RequireBooleanProperty(raw, "publicDisplayAllowed", false);
                RequireBooleanProperty(raw, "runtimeAuthorized", false);
                RequireBooleanProperty(raw, "traversalReady", false);
                RequireBooleanProperty(raw, "gameplayReady", false);
                RequireBooleanProperty(raw, "sourceCandidateUnityApplyAllowed", false);
                if (!string.Equals(marker.SchemaVersion, WalkNetworkCompletionSchema,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.Revision, owner.Revision,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.GenerationHashSha256,
                        owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BundleSetHashSha256,
                        owner.BundleSetHashSha256, StringComparison.OrdinalIgnoreCase)
                    || marker.BundleCount != owner.BundleCount
                    || !string.Equals(marker.ExporterSemanticRevision,
                        owner.ExporterSemanticRevision, StringComparison.Ordinal)
                    || !string.Equals(marker.ExporterSourceHashSha256,
                        owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.PrivateWalkNetworkOverlayCaptureReady
                    || marker.WalkNetworkNodePointCandidateCount
                    != owner.WalkNetworkNodePointCandidateCount
                    || marker.WalkNetworkLinkFragmentCandidateCount
                    != owner.WalkNetworkLinkFragmentCandidateCount
                    || marker.WalkNetworkLinkPathVertexCount
                    != owner.WalkNetworkLinkPathVertexCount
                    || marker.WalkNetworkOutputPathVertexCount
                    != owner.WalkNetworkOutputPathVertexCount
                    || !IsSha256(marker.WalkNetworkAuditSha256))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewCompletionMarkerMismatch");
                ValidateCompletionFiles(indexPath, owner, marker.Files,
                    new AuditFileExpectation("audit.json", marker.WalkNetworkAuditSha256));
                return marker;
            }

            if (observationReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewCompletionContentHashInvalid");
                RequireBooleanProperty(raw, "privateObservationOverlayCaptureReady", true);
                RequireBooleanProperty(raw, "currentPointerCreated", false);
                RequireBooleanProperty(raw, "currentPointerUsed", false);
                RequireBooleanProperty(raw, "currentPointerUpdated", false);
                RequireBooleanProperty(raw, "distributionApproved", false);
                RequireBooleanProperty(raw, "runtimeAuthorized", false);
                RequireBooleanProperty(raw, "traversalReady", false);
                RequireBooleanProperty(raw, "gameplayReady", false);
                if (!string.Equals(marker.SchemaVersion, ObservationCompletionSchema,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.Revision, owner.Revision,
                        StringComparison.Ordinal)
                    || !string.Equals(marker.GenerationHashSha256,
                        owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(marker.BundleSetHashSha256,
                        owner.BundleSetHashSha256, StringComparison.OrdinalIgnoreCase)
                    || marker.BundleCount != owner.BundleCount
                    || !string.Equals(marker.ExporterSemanticRevision,
                        owner.ExporterSemanticRevision, StringComparison.Ordinal)
                    || !string.Equals(marker.ExporterSourceHashSha256,
                        owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                    || !marker.PrivateObservationOverlayCaptureReady
                    || marker.CrosswalkPointCandidateCount
                    != owner.CrosswalkPointCandidateCount
                    || marker.IntersectionPointCandidateCount
                    != owner.IntersectionPointCandidateCount)
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewCompletionMarkerMismatch");
                ValidateCompletionFiles(indexPath, owner, marker.Files);
                return marker;
            }

            RequireEnvelope(marker.SchemaVersion, BaseCompletionSchema,
                marker.DesiredReadinessCode, marker.CandidateEnvelopeIsPublicationAuthority,
                marker.CandidateEnvelopeReadinessGovernsPromotion,
                marker.EmbeddedManifestReadinessIsInformational, marker.PublishBlocked,
                marker.CurrentPointerUsed, marker.CurrentPointerUpdated,
                marker.ObservationPresentationOnly, marker.DistributionApproved,
                marker.TraversalReady, marker.GameplayReady,
                marker.BaseGeometryCaptureReady, marker.LifeDioramaCaptureReady,
                marker.PersonalDataIncluded, marker.MissingLayerCodes);
            if (!string.Equals(marker.ScopeStableId, owner.ScopeStableId,
                    StringComparison.Ordinal)
                || !string.Equals(marker.Revision, owner.Revision, StringComparison.Ordinal)
                || !string.Equals(marker.IndexRelativePath, "index.json",
                    StringComparison.Ordinal)
                || !string.Equals(marker.IndexSha256, Sha256(indexPath),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(marker.GenerationHashSha256,
                    owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(marker.BundleSetHashSha256,
                    owner.BundleSetHashSha256, StringComparison.OrdinalIgnoreCase)
                || marker.BundleCount != owner.BundleCount
                || !string.Equals(marker.ExporterSemanticRevision,
                    owner.ExporterSemanticRevision, StringComparison.Ordinal)
                || !string.Equals(marker.ExporterSourceHashSha256,
                    owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewCompletionMarkerMismatch");
            return marker;
        }

        private static ReviewBundle LoadAndValidateBundle(ReviewIndex owner, ReviewEntry entry)
        {
            var inputRoot = Path.GetDirectoryName(SessionState.GetString(IndexKey, string.Empty));
            var fullPath = ResolveInputFile(inputRoot, entry.RelativePath,
                "AdministrativeDongDioramaReviewBundlePathInvalid");
            if (new FileInfo(fullPath).Length != entry.ByteLength
                || !string.Equals(Sha256(fullPath), entry.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewBundleHashInvalid");
            var json = File.ReadAllText(fullPath);
            var raw = ParseJsonObject(json,
                "AdministrativeDongDioramaReviewBundleInvalid");
            var value = raw.ToObject<ReviewBundle>()
                        ?? throw new InvalidOperationException(
                            "AdministrativeDongDioramaReviewBundleInvalid");
            var walkNetworkReview = owner.PrivateWalkNetworkOverlayCaptureReady;
            var observationReview = owner.PrivateObservationOverlayCaptureReady;
            var roadDirectionReview = owner.PrivateRoadDirectionOverlayCaptureReady;
            var biotopeReview = owner.PrivateBiotopeOverlayCaptureReady;
            var laneMarkingReview = owner.PrivateLaneMarkingOverlayCaptureReady;
            var horizontalContourTraceReview =
                owner.PrivateHorizontalContourTraceOverlayCaptureReady;
            RequireEnvelope(value.SchemaVersion,
                horizontalContourTraceReview ? HorizontalContourTraceBundleSchema
                : laneMarkingReview ? LaneMarkingBundleSchema
                : biotopeReview ? BiotopeBundleSchema : roadDirectionReview ? RoadDirectionBundleSchema
                : walkNetworkReview ? WalkNetworkBundleSchema
                : observationReview ? ObservationBundleSchema : BaseBundleSchema,
                value.DesiredReadinessCode,
                value.CandidateEnvelopeIsPublicationAuthority,
                value.CandidateEnvelopeReadinessGovernsPromotion,
                value.EmbeddedManifestReadinessIsInformational, value.PublishBlocked,
                value.CurrentPointerUsed, value.CurrentPointerUpdated,
                value.ObservationPresentationOnly, value.DistributionApproved,
                value.TraversalReady, value.GameplayReady, value.BaseGeometryCaptureReady,
                value.LifeDioramaCaptureReady, value.PersonalDataIncluded,
                value.MissingLayerCodes);
            if (observationReview)
            {
                RequireContentHash(raw, "contentHashSha256",
                    "AdministrativeDongDioramaReviewBundleContentHashInvalid");
                RequireEnvelopeBooleanProperties(raw);
                RequireBooleanProperty(raw, "privateObservationOverlayCaptureReady", true);
                if (walkNetworkReview)
                    RequireBooleanProperty(raw,
                        "privateWalkNetworkOverlayCaptureReady", true);
                if (roadDirectionReview)
                    RequireBooleanProperty(raw,
                        "privateRoadDirectionOverlayCaptureReady", true);
                if (laneMarkingReview)
                    RequireBooleanProperty(raw,
                        "privateLaneMarkingOverlayCaptureReady", true);
                if (horizontalContourTraceReview)
                {
                    RequireBooleanProperty(raw,
                        "privateHorizontalContourTraceOverlayCaptureReady", true);
                    RequireBooleanProperty(raw, "sourceHorizontalGeometryObserved", true);
                    foreach (var name in new[]
                             {
                                 "sourceAttributeDatabaseRead", "sourceElevationValuesRead",
                                 "sourceElevationValuesSerialized", "currentBoundaryEstablished",
                                 "currentTerrainEstablished", "contourMeaningAuthorized",
                                 "elevationMeaningAuthorized", "heightUnitVerified",
                                 "verticalDatumVerified", "terrainSurfaceAuthorized",
                                 "terrainMeshAuthorized"
                             })
                        RequireBooleanProperty(raw, name, false);
                }
            }
            if (!string.Equals(value.ScopeStableId, owner.ScopeStableId, StringComparison.Ordinal)
                || !string.Equals(value.Revision, owner.Revision, StringComparison.Ordinal)
                || !string.Equals(value.CandidateBatchId, owner.CandidateBatchId,
                    StringComparison.Ordinal)
                || !string.Equals(value.ProjectionSetHashSha256,
                    owner.ProjectionSetHashSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.ExporterSemanticRevision,
                    owner.ExporterSemanticRevision, StringComparison.Ordinal)
                || !string.Equals(value.ExporterSourceHashSha256,
                    owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.AdministrativeAreaStableId,
                    entry.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(value.ProjectionHashSha256,
                    entry.ProjectionHashSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(value.EmbeddedManifestReadinessCode,
                    AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                    StringComparison.Ordinal)
                || value.BuildingCount != entry.BuildingCount
                || value.RoadSegmentCount != entry.RoadSegmentCount
                || value.TileCount != entry.TileCount
                || value.PublicBusinessCount != 0
                || value.Manifest == null || value.Tiles == null
                || value.DisplayOverlays == null
                || value.DisplayOverlays.Items == null
                || value.DisplayOverlays.Items.Length != 0)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewBundleEnvelopeMismatch");

            if (observationReview)
                ValidatePrivateObservationOverlay(raw, value, entry);
            else if (value.PrivateObservationOverlayCaptureReady
                     || value.PrivateObservationOverlays != null
                     || value.CrosswalkPointCandidateCount != 0
                     || value.IntersectionPointCandidateCount != 0)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewBaseBundleOverlayRejected");
            if (walkNetworkReview)
                ValidatePrivateWalkNetworkOverlay(raw, value, entry);
            else if (value.PrivateWalkNetworkOverlayCaptureReady
                     || value.PrivateWalkNetworkOverlays != null
                     || value.WalkNetworkNodePointCandidateCount != 0
                     || value.WalkNetworkLinkFragmentCandidateCount != 0
                     || value.WalkNetworkLinkPathVertexCount != 0
                     || value.WalkNetworkOutputPathVertexCount != 0
                     || !string.IsNullOrEmpty(value.PrivateWalkNetworkOverlaySetHashSha256))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewEarlierBundleWalkOverlayRejected");
            if (roadDirectionReview)
                ValidatePrivateRoadDirectionOverlay(raw, value, entry);
            else if (value.PrivateRoadDirectionOverlayCaptureReady
                     || value.PrivateRoadDirectionOverlays != null
                     || value.RoadDirectionPointCandidateCount != 0
                     || !string.IsNullOrEmpty(value.PrivateRoadDirectionOverlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierBundleRoadDirectionRejected");
            if (biotopeReview)
                ValidatePrivateBiotopeOverlay(raw, value, entry);
            else if (value.PrivateBiotopeOverlayCaptureReady || value.PrivateBiotopeOverlays != null)
                throw new InvalidOperationException("AdministrativeDongDioramaReviewEarlierBundleBiotopeRejected");
            if (laneMarkingReview)
                ValidatePrivateLaneMarkingOverlay(raw, value, entry);
            else if (value.PrivateLaneMarkingOverlayCaptureReady
                     || value.PrivateLaneMarkingOverlays != null
                     || value.LaneMarkingCandidateCount != 0
                     || value.LaneMarkingFragmentCount != 0
                     || value.LaneMarkingPathVertexCount != 0
                     || value.LaneMarkingSourceSegmentCount != 0
                     || value.LaneMarkingRenderedSegmentCount != 0
                     || value.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount != 0
                     || value.LaneMarkingDuplicatePhysicalSegmentInstanceCount != 0
                     || !string.IsNullOrEmpty(value.PrivateLaneMarkingOverlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierBundleLaneMarkingRejected");
            if (horizontalContourTraceReview)
                ValidatePrivateHorizontalContourTraceOverlay(raw, value, entry);
            else if (value.PrivateHorizontalContourTraceOverlayCaptureReady
                     || value.PrivateHorizontalContourTraceOverlays != null
                     || value.HorizontalContourTraceCandidateCount != 0
                     || value.HorizontalContourTraceFragmentCount != 0
                     || value.HorizontalContourTracePathVertexCount != 0
                     || value.HorizontalContourTraceSourceSegmentCount != 0
                     || value.HorizontalContourTraceRenderedSegmentCount != 0
                     || value.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount != 0
                     || value.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount != 0
                     || !string.IsNullOrEmpty(
                         value.PrivateHorizontalContourTraceOverlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEarlierBundleHorizontalContourTraceRejected");
            return value;
        }

        private static void ValidatePrivateBiotopeOverlay(JObject raw, ReviewBundle bundle, ReviewEntry entry)
        {
            var overlay=bundle.PrivateBiotopeOverlays;
            var rawOverlay=raw["privateBiotopeOverlays"] as JObject;
            if (!bundle.PrivateBiotopeOverlayCaptureReady || overlay==null || rawOverlay==null
                || !string.Equals(bundle.PrivateBiotopeOverlaySetHashSha256,entry.PrivateBiotopeOverlaySetHashSha256,StringComparison.OrdinalIgnoreCase)
                || !string.Equals(overlay.administrativeAreaStableId,entry.AdministrativeAreaStableId,StringComparison.Ordinal)
                || !string.Equals(overlay.overlaySetHashSha256,entry.PrivateBiotopeOverlaySetHashSha256,StringComparison.OrdinalIgnoreCase)
                || overlay.candidatePolygons==null || overlay.candidatePolygons.Length!=entry.BiotopeCandidateCount
                || bundle.BiotopeCandidateCount!=entry.BiotopeCandidateCount
                || bundle.BiotopePolygonPartCount!=entry.BiotopePolygonPartCount
                || bundle.BiotopeInteriorRingCount!=entry.BiotopeInteriorRingCount
                || bundle.BiotopeRingVertexCount!=entry.BiotopeRingVertexCount
                || bundle.BiotopeSourceSegmentCount!=entry.BiotopeSourceSegmentCount
                || bundle.BiotopeSkippedSegmentCount!=entry.BiotopeSkippedSegmentCount
                || bundle.BiotopeRenderedSegmentCount!=entry.BiotopeRenderedSegmentCount)
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeOverlayMismatch");
            RequireContentHash(rawOverlay,"overlaySetHashSha256","AdministrativeDongDioramaReviewBiotopeOverlayContentHashInvalid");
            RequireBooleanProperty(rawOverlay,"privateReviewVisualizationAllowed",true);
            RequireBooleanProperty(rawOverlay,"biotopeOutlineAuthorized",true);
            foreach(var flag in new[]{"allSourceSegmentsRendered","filledPolygonAuthorized","colliderAuthorized","legalEffectAuthorized","speciesObservationAuthorized","safetyAuthorized","passageAuthorized","publicDisplayAllowed","runtimeAuthorized","traversalReady","gameplayReady","currentPointerUsed","currentPointerUpdated","databaseWriteAllowed","mongoWriteAllowed","sourceCandidateUnityApplyAllowed"})
                RequireBooleanProperty(rawOverlay,flag,false);
            if(rawOverlay["coordinateUnit"]?.Value<string>()!="millimeter"
               || rawOverlay["coordinateFrame"]?["method"]?.Value<string>()!="WGS84-ECEF-ENU-at-zero-altitude"
               || rawOverlay["coordinateFrame"]?["metersPerUnit"]?.Value<double>()!=1d)
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeCoordinateFrameInvalid");
        }

        private static void ValidatePrivateLaneMarkingOverlay(JObject raw,
            ReviewBundle bundle, ReviewEntry entry)
        {
            var overlay = bundle.PrivateLaneMarkingOverlays;
            var rawOverlay = raw["privateLaneMarkingOverlays"] as JObject;
            if (!bundle.PrivateLaneMarkingOverlayCaptureReady || overlay == null
                || rawOverlay == null
                || !string.Equals(bundle.PrivateLaneMarkingOverlaySetHashSha256,
                    entry.PrivateLaneMarkingOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(overlay.administrativeAreaStableId,
                    entry.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(overlay.overlaySetHashSha256,
                    entry.PrivateLaneMarkingOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || overlay.candidateMarkings == null
                || overlay.candidateMarkings.Length != entry.LaneMarkingCandidateCount
                || bundle.LaneMarkingCandidateCount != entry.LaneMarkingCandidateCount
                || bundle.LaneMarkingFragmentCount != entry.LaneMarkingFragmentCount
                || bundle.LaneMarkingPathVertexCount != entry.LaneMarkingPathVertexCount
                || bundle.LaneMarkingSourceSegmentCount
                   != entry.LaneMarkingSourceSegmentCount
                || bundle.LaneMarkingRenderedSegmentCount
                   != entry.LaneMarkingRenderedSegmentCount
                || bundle.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                   != entry.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                || bundle.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                   != entry.LaneMarkingDuplicatePhysicalSegmentInstanceCount)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingOverlayMismatch");
            RequireContentHash(rawOverlay, "overlaySetHashSha256",
                "AdministrativeDongDioramaReviewLaneMarkingOverlayContentHashInvalid");
            foreach (var name in new[]
                     {
                         "historicalBoundaryBootstrapOnly", "privateReviewOnly",
                         "privateReviewVisualizationAllowed", "reviewVisualizationOnly",
                         "laneOutlineAuthorized"
                     })
                RequireBooleanProperty(rawOverlay, name, true);
            foreach (var name in new[]
                     {
                         "allSourceSegmentsRendered", "laneMeaningAuthorized",
                         "directionAuthorized", "trafficAuthorized", "stopLineAuthorized",
                         "signalAuthorized", "colliderAuthorized", "publicDisplayAllowed",
                         "runtimeAuthorized", "traversalReady", "gameplayReady",
                         "currentPointerUsed", "currentPointerUpdated", "databaseWriteAllowed",
                         "mongoWriteAllowed", "sourceCandidateUnityApplyAllowed"
                     })
                RequireBooleanProperty(rawOverlay, name, false);
            if (rawOverlay["coordinateUnit"]?.Value<string>() != "millimeter"
                || rawOverlay["coordinateFrame"]?["method"]?.Value<string>()
                   != 행정동디오라마비공개차선표시Policy.CoordinateFrameMethod
                || rawOverlay["coordinateFrame"]?["metersPerUnit"]?.Value<double>() != 1d)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingCoordinateFrameInvalid");
        }

        private static void ValidatePrivateHorizontalContourTraceOverlay(JObject raw,
            ReviewBundle bundle, ReviewEntry entry)
        {
            var overlay = bundle.PrivateHorizontalContourTraceOverlays;
            var rawOverlay = raw["privateHorizontalContourTraceOverlays"] as JObject;
            if (!bundle.PrivateHorizontalContourTraceOverlayCaptureReady || overlay == null
                || rawOverlay == null
                || !string.Equals(bundle.PrivateHorizontalContourTraceOverlaySetHashSha256,
                    entry.PrivateHorizontalContourTraceOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(overlay.administrativeAreaStableId,
                    entry.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(overlay.overlaySetHashSha256,
                    entry.PrivateHorizontalContourTraceOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || overlay.candidateTraces == null
                || overlay.candidateTraces.Length
                   != entry.HorizontalContourTraceCandidateCount
                || bundle.HorizontalContourTraceCandidateCount
                   != entry.HorizontalContourTraceCandidateCount
                || bundle.HorizontalContourTraceFragmentCount
                   != entry.HorizontalContourTraceFragmentCount
                || bundle.HorizontalContourTracePathVertexCount
                   != entry.HorizontalContourTracePathVertexCount
                || bundle.HorizontalContourTraceSourceSegmentCount
                   != entry.HorizontalContourTraceSourceSegmentCount
                || bundle.HorizontalContourTraceRenderedSegmentCount
                   != entry.HorizontalContourTraceRenderedSegmentCount
                || bundle.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                   != entry.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                || bundle.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                   != entry.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewHorizontalContourTraceOverlayMismatch");
            RequireContentHash(rawOverlay, "overlaySetHashSha256",
                "AdministrativeDongDioramaReviewHorizontalContourTraceOverlayContentHashInvalid");
            foreach (var name in new[]
                     {
                         "historicalBoundaryBootstrapOnly", "privateReviewOnly",
                         "privateReviewVisualizationAllowed", "reviewVisualizationOnly",
                         "sourceHorizontalGeometryObserved"
                     })
                RequireBooleanProperty(rawOverlay, name, true);
            foreach (var name in new[]
                     {
                         "allSourceSegmentsRendered", "sourceAttributeDatabaseRead",
                         "sourceElevationValuesRead", "sourceElevationValuesSerialized",
                         "currentBoundaryEstablished", "currentTerrainEstablished",
                         "contourMeaningAuthorized", "elevationMeaningAuthorized",
                         "heightUnitVerified", "verticalDatumVerified",
                         "terrainSurfaceAuthorized", "terrainMeshAuthorized",
                         "colliderAuthorized", "publicDisplayAllowed", "databaseWriteAllowed",
                         "mongoWriteAllowed", "sourceCandidateUnityApplyAllowed",
                         "runtimeAuthorized", "traversalReady", "gameplayReady",
                         "currentPointerUsed", "currentPointerUpdated"
                     })
                RequireBooleanProperty(rawOverlay, name, false);
            var candidates = rawOverlay["candidateTraces"] as JArray;
            if (!string.Equals(rawOverlay.Value<string>("schemaVersion"),
                    행정동디오라마비공개수평등고선추적Policy.SchemaVersion,
                    StringComparison.Ordinal)
                || rawOverlay.Value<string>("sourceVintage") != "file-2025-03-20"
                || rawOverlay.Value<string>("coordinateUnit") != "millimeter"
                || rawOverlay["coordinateFrame"]?["method"]?.Value<string>()
                   != 행정동디오라마비공개수평등고선추적Policy.CoordinateFrameMethod
                || rawOverlay["coordinateFrame"]?["metersPerUnit"]?.Value<double>() != 1d
                || candidates == null
                || candidates.Count != entry.HorizontalContourTraceCandidateCount
                || candidates.Any(token =>
                {
                    if (token is not JObject candidate
                        || candidate.Properties().Select(property => property.Name)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .SequenceEqual(new[] { "candidateStableId", "fragments" }
                                .OrderBy(name => name, StringComparer.Ordinal)) == false
                        || candidate.Value<string>("candidateStableId")?.StartsWith(
                            "contour-trace:sha256:", StringComparison.Ordinal) != true
                        || candidate["fragments"] is not JArray fragments
                        || fragments.Count == 0)
                        return true;
                    return fragments.Any(fragmentToken =>
                    {
                        if (fragmentToken is not JObject fragment
                            || fragment.Properties().Select(property => property.Name)
                                .OrderBy(name => name, StringComparer.Ordinal)
                                .SequenceEqual(new[]
                                    {
                                        "commonEnuMillimeterPath", "fragmentOrdinal"
                                    }.OrderBy(name => name, StringComparer.Ordinal)) == false
                            || fragment["commonEnuMillimeterPath"] is not JArray path
                            || path.Count != 2)
                            return true;
                        return path.Any(point => point is not JObject coordinate
                            || coordinate.Properties().Select(property => property.Name)
                                .OrderBy(name => name, StringComparer.Ordinal)
                                .SequenceEqual(new[] { "x", "z" }
                                    .OrderBy(name => name, StringComparer.Ordinal)) == false);
                    });
                }))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewHorizontalContourTraceCoordinateFrameInvalid");
        }

        private static void ValidateBiotopeAudit(string indexPath, ReviewIndex owner)
        {
            var laneReview = IsLaneMarkingReview(owner.SchemaVersion)
                             || IsHorizontalContourTraceReview(owner.SchemaVersion);
            var expectedPath = laneReview ? "biotope-audit.json" : "audit.json";
            if (!string.Equals(owner.BiotopeAuditRelativePath, expectedPath,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeAuditPathInvalid");
            var path=ResolveInputFile(Path.GetDirectoryName(indexPath),owner.BiotopeAuditRelativePath,"AdministrativeDongDioramaReviewBiotopeAuditPathInvalid");
            if(!string.Equals(Sha256(path),owner.BiotopeAuditSha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeAuditHashMismatch");
            var raw=ParseJsonObject(File.ReadAllText(path),"AdministrativeDongDioramaReviewBiotopeAuditInvalid");
            RequireContentHash(raw,"contentHashSha256","AdministrativeDongDioramaReviewBiotopeAuditContentHashInvalid");
            RequireBooleanProperty(raw,"privateReviewVisualizationAllowed",true);
            RequireBooleanProperty(raw,"biotopeOutlineAuthorized",true);
            foreach(var flag in new[]{"allSourceSegmentsRendered","filledPolygonAuthorized","colliderAuthorized","legalEffectAuthorized","speciesObservationAuthorized","safetyAuthorized","passageAuthorized","publicDisplayAllowed","runtimeAuthorized","traversalReady","gameplayReady","currentPointerUsed","currentPointerUpdated","databaseWriteAllowed","mongoWriteAllowed","sourceCandidateUnityApplyAllowed"})
                RequireBooleanProperty(raw,flag,false);
            if(!string.Equals(raw["generationHashSha256"]?.Value<string>(),
                   laneReview ? ExpectedBiotopeGenerationHash : owner.GenerationHashSha256,
                   StringComparison.OrdinalIgnoreCase)
               || raw["biotopeManifestSha256"]?.Value<string>()!=owner.BiotopeSourceManifestSha256
               || raw["counts"]?["candidateCount"]?.Value<int>()!=ExpectedBiotopeCandidateCount
               || raw["counts"]?["polygonPartCount"]?.Value<int>()!=ExpectedBiotopePolygonPartCount
               || raw["counts"]?["interiorRingCount"]?.Value<int>()!=ExpectedBiotopeInteriorRingCount
               || raw["counts"]?["ringVertexCount"]?.Value<int>()!=ExpectedBiotopeRingVertexCount
               || raw["counts"]?["sourceLineSegmentCount"]?.Value<int>()!=ExpectedBiotopeSourceSegmentCount
               || raw["counts"]?["subMillimeterLineSegmentCount"]?.Value<int>()!=ExpectedBiotopeSkippedSegmentCount
               || raw["counts"]?["renderableLineSegmentCount"]?.Value<int>()!=ExpectedBiotopeRenderedSegmentCount
               || raw["allRingPointsWithinManifestBoundsQuantizationEnvelope"]?.Value<bool>()!=true
               || raw["renderGeometry"]?.Value<string>()!="ExteriorAndInteriorRingLineSegmentsOnly"
               || raw["shortSegmentPolicy"]?.Value<string>()!="SourceSegmentsAtOrBelowOneMillimeterPreservedButNotRenderedAndNeverEnlarged"
               || raw["manifestBoundsRoundingToleranceMillimeters"]?.Value<double>()!=.5d
               || raw["baseObservationWalkDirectionValuesPreserved"]?.Value<bool>()!=true)
                throw new InvalidOperationException("AdministrativeDongDioramaReviewBiotopeAuditMismatch");
        }

        private static void ValidateLaneMarkingAudit(string indexPath, ReviewIndex owner)
        {
            if (!string.Equals(owner.LaneMarkingAuditRelativePath,
                    "lane-marking-audit.json", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingAuditPathInvalid");
            var path = ResolveInputFile(Path.GetDirectoryName(indexPath),
                owner.LaneMarkingAuditRelativePath,
                "AdministrativeDongDioramaReviewLaneMarkingAuditPathInvalid");
            if (!string.Equals(Sha256(path), owner.LaneMarkingAuditSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingAuditHashMismatch");
            var raw = ParseJsonObject(File.ReadAllText(path),
                "AdministrativeDongDioramaReviewLaneMarkingAuditInvalid");
            RequireContentHash(raw, "contentHashSha256",
                "AdministrativeDongDioramaReviewLaneMarkingAuditContentHashInvalid");
            var counts = raw["counts"] as JObject;
            var perArea = raw["perAdministrativeArea"] as JObject;
            if (!string.Equals(raw.Value<string>("schemaVersion"),
                    "administrative-dong-diorama-unity-review-audit.v6",
                    StringComparison.Ordinal)
                || !string.Equals(raw.Value<string>("generationHashSha256"),
                    ExpectedLaneMarkingGenerationHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>(
                        "laneMarkingSourceGenerationHashSha256"),
                    owner.LaneMarkingSourceGenerationHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>("laneMarkingSourceAuditSha256"),
                    owner.LaneMarkingSourceAuditSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>("laneMarkingSourceManifestSha256"),
                    owner.LaneMarkingSourceManifestSha256,
                    StringComparison.OrdinalIgnoreCase)
                || counts == null || perArea == null
                || counts.Value<int>("administrativeAreas")
                   != owner.AdministrativeAreaCount
                || counts.Value<int>("laneMarkingCandidateCount")
                   != owner.LaneMarkingCandidateCount
                || counts.Value<int>("laneMarkingFragmentCount")
                   != owner.LaneMarkingFragmentCount
                || counts.Value<int>("laneMarkingPathVertexCount")
                   != owner.LaneMarkingPathVertexCount
                || counts.Value<int>("laneMarkingSourceSegmentCount")
                   != owner.LaneMarkingSourceSegmentCount
                || counts.Value<int>("laneMarkingRenderedSegmentCount")
                   != owner.LaneMarkingRenderedSegmentCount
                || counts.Value<int>(
                       "laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount")
                   != owner.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                || counts.Value<int>("laneMarkingDuplicatePhysicalSegmentInstanceCount")
                   != owner.LaneMarkingDuplicatePhysicalSegmentInstanceCount
                || perArea.Count != owner.Bundles.Length
                || owner.Bundles.Any(entry =>
                {
                    var item = perArea[entry.AdministrativeAreaStableId] as JObject;
                    return item == null
                           || item.Value<int>("laneMarkingCandidateCount")
                           != entry.LaneMarkingCandidateCount
                           || item.Value<int>("laneMarkingFragmentCount")
                           != entry.LaneMarkingFragmentCount
                           || item.Value<int>("laneMarkingPathVertexCount")
                           != entry.LaneMarkingPathVertexCount
                           || item.Value<int>("laneMarkingSourceSegmentCount")
                           != entry.LaneMarkingSourceSegmentCount
                           || item.Value<int>("laneMarkingRenderedSegmentCount")
                           != entry.LaneMarkingRenderedSegmentCount
                           || item.Value<int>(
                               "laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount")
                           != entry.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount
                           || item.Value<int>(
                               "laneMarkingDuplicatePhysicalSegmentInstanceCount")
                           != entry.LaneMarkingDuplicatePhysicalSegmentInstanceCount;
                }))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewLaneMarkingAuditMismatch");
            foreach (var name in new[]
                     {
                         "historicalBoundaryBootstrapOnly", "privateReviewOnly",
                         "privateReviewVisualizationAllowed", "reviewVisualizationOnly",
                         "laneOutlineAuthorized"
                     })
                RequireBooleanProperty(raw, name, true);
            foreach (var name in new[]
                     {
                         "allSourceSegmentsRendered", "laneMeaningAuthorized",
                         "directionAuthorized", "trafficAuthorized", "stopLineAuthorized",
                         "signalAuthorized", "colliderAuthorized", "publicDisplayAllowed",
                         "runtimeAuthorized", "traversalReady", "gameplayReady",
                         "currentPointerUsed", "currentPointerUpdated", "databaseWriteAllowed",
                         "mongoWriteAllowed", "sourceCandidateUnityApplyAllowed"
                     })
                RequireBooleanProperty(raw, name, false);
        }

        private static void ValidateHorizontalContourTraceAudit(string indexPath,
            ReviewIndex owner)
        {
            var path = ResolveInputFile(Path.GetDirectoryName(indexPath),
                owner.HorizontalContourTraceAuditRelativePath,
                "AdministrativeDongDioramaReviewHorizontalContourTraceAuditPathInvalid");
            if (!string.Equals(Sha256(path), owner.HorizontalContourTraceAuditSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewHorizontalContourTraceAuditHashMismatch");
            var raw = ParseJsonObject(File.ReadAllText(path),
                "AdministrativeDongDioramaReviewHorizontalContourTraceAuditInvalid");
            RequireContentHash(raw, "contentHashSha256",
                "AdministrativeDongDioramaReviewHorizontalContourTraceAuditContentHashInvalid");
            var counts = raw["counts"] as JObject;
            var perArea = raw["perAdministrativeArea"] as JObject;
            var policy = raw["sourceGeometryPolicy"] as JObject;
            if (!string.Equals(raw.Value<string>("schemaVersion"),
                    "administrative-dong-horizontal-contour-trace-unity-wrapper-audit.v1",
                    StringComparison.Ordinal)
                || !string.Equals(raw.Value<string>("generationHashSha256"),
                    owner.GenerationHashSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>("baseLaneReviewGenerationHashSha256"),
                    ExpectedLaneMarkingGenerationHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>(
                        "horizontalContourTraceSourceGenerationHashSha256"),
                    owner.HorizontalContourTraceSourceGenerationHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>(
                        "horizontalContourTraceSourceManifestSha256"),
                    owner.HorizontalContourTraceSourceManifestSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>(
                        "horizontalContourTraceSourceAuditSha256"),
                    owner.HorizontalContourTraceSourceAuditSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(raw.Value<string>("exporterSemanticRevision"),
                    owner.ExporterSemanticRevision, StringComparison.Ordinal)
                || !string.Equals(raw.Value<string>("exporterSourceHashSha256"),
                    owner.ExporterSourceHashSha256, StringComparison.OrdinalIgnoreCase)
                || counts == null || perArea == null || policy == null
                || counts.Value<int>("administrativeAreas") != owner.AdministrativeAreaCount
                || counts.Value<int>("horizontalContourTraceCandidateCount")
                   != owner.HorizontalContourTraceCandidateCount
                || counts.Value<int>("horizontalContourTraceFragmentCount")
                   != owner.HorizontalContourTraceFragmentCount
                || counts.Value<int>("horizontalContourTracePathVertexCount")
                   != owner.HorizontalContourTracePathVertexCount
                || counts.Value<int>("horizontalContourTraceSourceSegmentCount")
                   != owner.HorizontalContourTraceSourceSegmentCount
                || counts.Value<int>("horizontalContourTraceRenderedSegmentCount")
                   != owner.HorizontalContourTraceRenderedSegmentCount
                || counts.Value<int>(
                       "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount")
                   != owner.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                || counts.Value<int>(
                       "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount")
                   != owner.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount
                || counts.Value<int>("sourceBoundaryContainmentFailureCount") != 0
                || counts.Value<int>("commonBoundaryQuantizedOutsidePointCount") != 910
                || counts.Value<int>("commonBoundaryMaximumOutsideDistanceMillimeters") != 6
                || perArea.Count != owner.Bundles.Length
                || owner.Bundles.Any(entry =>
                {
                    var item = perArea[entry.AdministrativeAreaStableId] as JObject;
                    return item == null
                           || item.Value<int>("horizontalContourTraceCandidateCount")
                           != entry.HorizontalContourTraceCandidateCount
                           || item.Value<int>("horizontalContourTraceFragmentCount")
                           != entry.HorizontalContourTraceFragmentCount
                           || item.Value<int>("horizontalContourTracePathVertexCount")
                           != entry.HorizontalContourTracePathVertexCount
                           || item.Value<int>("horizontalContourTraceSourceSegmentCount")
                           != entry.HorizontalContourTraceSourceSegmentCount
                           || item.Value<int>("horizontalContourTraceRenderedSegmentCount")
                           != entry.HorizontalContourTraceRenderedSegmentCount
                           || item.Value<int>(
                               "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount")
                           != entry.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                           || item.Value<int>(
                               "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount")
                           != entry.HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount;
                })
                || policy.Value<bool>("flatHorizontalTraceOnly") != true
                || policy.Value<bool>("sourceDbfOpened")
                || policy.Value<bool>("contOrHeightValuesRead")
                || policy.Value<bool>("heightOrElevationFieldSerialized")
                || policy.Value<bool>("sourceCrsBoundaryContainmentRequired") != true
                || policy.Value<int>("sourceCrsBoundaryContainmentFailureCount") != 0
                || policy.Value<bool>("commonEnuQuantizationDifferenceAuditedNotPromoted")
                   != true
                || policy.Value<double>("minimumRenderableSegmentMillimeters") != 1d
                || policy.Value<bool>("sourceSegmentsEqualRenderedPlusSkippedPlusDuplicates")
                   != true
                || policy.Value<string>("physicalSegmentDeduplicationScope")
                   != "WithinAdministrativeArea"
                || policy.Value<bool>("physicalSegmentDirectionIgnored") != true
                || policy.Value<bool>("renderFragmentsAreTwoPointSegments") != true
                || raw.Value<bool>("baseObservationWalkDirectionBiotopeLaneValuesPreserved")
                   != true)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewHorizontalContourTraceAuditMismatch");
            foreach (var name in new[]
                     {
                         "privateReviewOnly", "historicalBoundaryBootstrapOnly",
                         "reviewVisualizationOnly", "privateReviewVisualizationAllowed",
                         "sourceHorizontalGeometryObserved"
                     })
                RequireBooleanProperty(raw, name, true);
            foreach (var name in new[]
                     {
                         "allSourceSegmentsRendered", "sourceAttributeDatabaseRead",
                         "sourceElevationValuesRead", "sourceElevationValuesSerialized",
                         "currentBoundaryEstablished", "currentTerrainEstablished",
                         "contourMeaningAuthorized", "elevationMeaningAuthorized",
                         "heightUnitVerified", "verticalDatumVerified",
                         "terrainSurfaceAuthorized", "terrainMeshAuthorized",
                         "colliderAuthorized", "publicDisplayAllowed", "databaseWriteAllowed",
                         "mongoWriteAllowed", "sourceCandidateUnityApplyAllowed",
                         "runtimeAuthorized", "traversalReady", "gameplayReady",
                         "currentPointerUsed", "currentPointerUpdated"
                     })
                RequireBooleanProperty(raw, name, false);
        }

        private static void ValidatePrivateRoadDirectionOverlay(JObject raw,
            ReviewBundle bundle, ReviewEntry entry)
        {
            var overlay = bundle.PrivateRoadDirectionOverlays;
            var rawOverlay = raw["privateRoadDirectionOverlays"] as JObject;
            if (!bundle.PrivateRoadDirectionOverlayCaptureReady
                || overlay == null || rawOverlay == null
                || bundle.RoadDirectionPointCandidateCount
                   != entry.RoadDirectionPointCandidateCount
                || bundle.RoadDirectionPointCandidateCount
                   <= 0
                || !string.Equals(bundle.PrivateRoadDirectionOverlaySetHashSha256,
                    entry.PrivateRoadDirectionOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(overlay.administrativeAreaStableId,
                    entry.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(overlay.overlaySetHashSha256,
                    entry.PrivateRoadDirectionOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || overlay.candidatePoints == null
                || overlay.candidatePoints.Length != entry.RoadDirectionPointCandidateCount)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewRoadDirectionOverlayMismatch");
            RequireContentHash(rawOverlay, "overlaySetHashSha256",
                "AdministrativeDongDioramaReviewRoadDirectionOverlayContentHashInvalid");
            var rawCandidates = rawOverlay["candidatePoints"] as JArray;
            if (rawCandidates == null || rawCandidates.Count != entry.RoadDirectionPointCandidateCount
                || rawCandidates.Any(token =>
                {
                    var point = token as JObject;
                    var coordinate = point?["commonEnuMillimeters"] as JObject;
                    return point == null || coordinate == null
                           || point["candidateStableId"]?.Type != JTokenType.String
                           || point["officialKindCode"]?.Type != JTokenType.String
                           || point["sourceDirectionNumericValue"]?.Type != JTokenType.Integer
                              && point["sourceDirectionNumericValue"]?.Type != JTokenType.Float
                           || coordinate["x"]?.Type != JTokenType.Integer
                              && coordinate["x"]?.Type != JTokenType.Float
                           || coordinate["z"]?.Type != JTokenType.Integer
                              && coordinate["z"]?.Type != JTokenType.Float
                           || point.Properties().Any(property =>
                               string.Equals(property.Name, "LENX", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(property.Name, "DRN", StringComparison.OrdinalIgnoreCase));
                }))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewRoadDirectionCandidateFieldsInvalid");
            RequireBooleanProperty(rawOverlay, "privateReviewVisualizationAllowed", true);
            RequireBooleanProperty(rawOverlay, "reviewVisualizationOnly", true);
            RequireBooleanProperty(rawOverlay, "historicalBoundaryBootstrapOnly", true);
            RequireBooleanProperty(rawOverlay, "angleSemanticVerified", false);
            RequireBooleanProperty(rawOverlay, "directionResolved", false);
            RequireBooleanProperty(rawOverlay, "renderedArrowAuthorized", false);
            RequireBooleanProperty(rawOverlay, "sourceLengthUnitVerified", false);
            RequireBooleanProperty(rawOverlay, "publicDisplayAllowed", false);
            RequireBooleanProperty(rawOverlay, "runtimeAuthorized", false);
            RequireBooleanProperty(rawOverlay, "traversalReady", false);
            RequireBooleanProperty(rawOverlay, "gameplayReady", false);
            RequireBooleanProperty(rawOverlay, "currentPointerUsed", false);
            RequireBooleanProperty(rawOverlay, "currentPointerUpdated", false);
            RequireBooleanProperty(rawOverlay, "sourceCandidateUnityApplyAllowed", false);
        }

        private static void ValidatePrivateObservationOverlay(JObject raw,
            ReviewBundle bundle, ReviewEntry entry)
        {
            var overlay = bundle.PrivateObservationOverlays;
            var rawOverlay = raw["privateObservationOverlays"] as JObject;
            if (!bundle.PrivateObservationOverlayCaptureReady
                || overlay == null || rawOverlay == null
                || !string.Equals(bundle.ContentHashSha256, entry.ContentHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || bundle.CrosswalkPointCandidateCount
                != entry.CrosswalkPointCandidateCount
                || bundle.IntersectionPointCandidateCount
                != entry.IntersectionPointCandidateCount
                || !string.Equals(overlay.administrativeAreaStableId,
                    entry.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(overlay.overlaySetHashSha256,
                    entry.PrivateObservationOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || overlay.crosswalkPoints == null || overlay.intersectionPoints == null
                || overlay.crosswalkPoints.Length != entry.CrosswalkPointCandidateCount
                || overlay.intersectionPoints.Length
                != entry.IntersectionPointCandidateCount
                || overlay.sourceReceipts == null || overlay.sourceReceipts.Length == 0
                || overlay.sourceReceipts.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.datasetId)
                    || string.IsNullOrWhiteSpace(value.officialPageUrl)
                    || string.IsNullOrWhiteSpace(value.fileRevision)
                    || !IsSha256(value.fileSha256)
                    || value.byteLength.HasValue && value.byteLength.Value <= 0
                    || string.IsNullOrWhiteSpace(value.licenseCode)
                    || string.IsNullOrWhiteSpace(value.coordinateReferenceStatus)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewPrivateOverlayMismatch");

            RequireContentHash(rawOverlay, "overlaySetHashSha256",
                "AdministrativeDongDioramaReviewPrivateOverlayContentHashInvalid");
            RequireBooleanProperty(rawOverlay, "historicalBoundaryBootstrapOnly", true);
            RequireBooleanProperty(rawOverlay, "privateReviewVisualizationAllowed", true);
            RequireBooleanProperty(rawOverlay, "reviewVisualizationOnly", true);
            RequireBooleanProperty(rawOverlay, "sourceCandidateUnityApplyAllowed", false);
            RequireBooleanProperty(rawOverlay, "publicDisplayAllowed", false);
            RequireBooleanProperty(rawOverlay, "runtimeAuthorized", false);
            RequireBooleanProperty(rawOverlay, "traversalReady", false);
            RequireBooleanProperty(rawOverlay, "gameplayReady", false);
            RequireBooleanProperty(rawOverlay, "currentPointerUsed", false);
            RequireBooleanProperty(rawOverlay, "currentPointerUpdated", false);
            RequireBooleanProperty(rawOverlay, "exactAddressIncluded", false);
            RequireBooleanProperty(rawOverlay, "businessIdentityIncluded", false);
            RequireBooleanProperty(rawOverlay, "personalDataIncluded", false);
        }

        private static void ValidatePrivateWalkNetworkOverlay(JObject raw,
            ReviewBundle bundle, ReviewEntry entry)
        {
            var overlay = bundle.PrivateWalkNetworkOverlays;
            var rawOverlay = raw["privateWalkNetworkOverlays"] as JObject;
            if (!bundle.PrivateWalkNetworkOverlayCaptureReady
                || overlay == null || rawOverlay == null
                || bundle.WalkNetworkNodePointCandidateCount
                != entry.WalkNetworkNodePointCandidateCount
                || bundle.WalkNetworkLinkFragmentCandidateCount
                != entry.WalkNetworkLinkFragmentCandidateCount
                || bundle.WalkNetworkLinkPathVertexCount
                != entry.WalkNetworkLinkPathVertexCount
                || bundle.WalkNetworkOutputPathVertexCount
                != entry.WalkNetworkOutputPathVertexCount
                || !string.Equals(bundle.PrivateWalkNetworkOverlaySetHashSha256,
                    entry.PrivateWalkNetworkOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(overlay.administrativeAreaStableId,
                    entry.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(overlay.overlaySetHashSha256,
                    entry.PrivateWalkNetworkOverlaySetHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                || overlay.nodePoints == null || overlay.linkFragments == null
                || overlay.nodePoints.Length != entry.WalkNetworkNodePointCandidateCount
                || overlay.linkFragments.Length
                != entry.WalkNetworkLinkFragmentCandidateCount
                || overlay.linkFragments.Sum(value =>
                    value?.commonEnuMillimeterPath?.Length ?? 0)
                != entry.WalkNetworkLinkPathVertexCount
                || overlay.nodePoints.Length + overlay.linkFragments.Sum(value =>
                    value?.commonEnuMillimeterPath?.Length ?? 0)
                != entry.WalkNetworkOutputPathVertexCount
                || overlay.sourceReceipts == null || overlay.sourceReceipts.Length == 0
                || overlay.sourceReceipts.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.datasetId)
                    || string.IsNullOrWhiteSpace(value.officialPageUrl)
                    || string.IsNullOrWhiteSpace(value.sourceReferenceVintage)
                    || !IsSha256(value.candidateSetHashSha256)
                    || !IsSha256(value.acquisitionReceiptSha256)
                    || string.IsNullOrWhiteSpace(value.licenseCode)
                    || string.IsNullOrWhiteSpace(value.sourceCoordinateReference)
                    || string.IsNullOrWhiteSpace(value.conversionCoordinateReference)
                    || string.IsNullOrWhiteSpace(value.coordinateReferenceStatus)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewPrivateWalkOverlayMismatch");

            RequireContentHash(rawOverlay, "overlaySetHashSha256",
                "AdministrativeDongDioramaReviewPrivateWalkOverlayContentHashInvalid");
            RequireBooleanProperty(rawOverlay, "historicalBoundaryBootstrapOnly", true);
            RequireBooleanProperty(rawOverlay, "privateReviewOnly", true);
            RequireBooleanProperty(rawOverlay, "privateReviewVisualizationAllowed", true);
            RequireBooleanProperty(rawOverlay, "reviewVisualizationOnly", true);
            RequireBooleanProperty(rawOverlay, "observationCandidateOnly", true);
            RequireBooleanProperty(rawOverlay, "sourceCandidateUnityApplyAllowed", false);
            RequireBooleanProperty(rawOverlay, "publicDisplayAllowed", false);
            RequireBooleanProperty(rawOverlay, "runtimeAuthorized", false);
            RequireBooleanProperty(rawOverlay, "traversalReady", false);
            RequireBooleanProperty(rawOverlay, "gameplayReady", false);
            RequireBooleanProperty(rawOverlay, "currentPointerUsed", false);
            RequireBooleanProperty(rawOverlay, "currentPointerUpdated", false);
            RequireBooleanProperty(rawOverlay, "sourceIdentifiersIncluded", false);
            RequireBooleanProperty(rawOverlay, "sourceBoroughIdentityIncluded", false);
            RequireBooleanProperty(rawOverlay, "legalAreaIdentityIncluded", false);
            RequireBooleanProperty(rawOverlay, "sourceTimestampsIncluded", false);
            RequireBooleanProperty(rawOverlay, "sourceEpsg5186CoordinatesIncluded", false);
            RequireBooleanProperty(rawOverlay, "exactAddressIncluded", false);
            RequireBooleanProperty(rawOverlay, "personalDataIncluded", false);
        }

        private sealed class AuditFileExpectation
        {
            public AuditFileExpectation(string relativePath, string sha256)
            { RelativePath=relativePath; Sha256=sha256; }
            public string RelativePath { get; }
            public string Sha256 { get; }
        }

        private static void ValidateCompletionFiles(string indexPath, ReviewIndex owner,
            ReviewFile[] files, params AuditFileExpectation[] auditFiles)
        {
            auditFiles = auditFiles ?? Array.Empty<AuditFileExpectation>();
            var additionalFileCount = auditFiles.Length;
            if (files == null || files.Length != owner.BundleCount + 1 + additionalFileCount)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewCompletionFilesInvalid");
            var expected = owner.Bundles.ToDictionary(value => value.RelativePath,
                value => new ReviewFile
                {
                    RelativePath = value.RelativePath,
                    Sha256 = value.Sha256,
                    ByteLength = value.ByteLength
                }, StringComparer.Ordinal);
            expected.Add("index.json", new ReviewFile
            {
                RelativePath = "index.json",
                Sha256 = Sha256(indexPath),
                ByteLength = new FileInfo(indexPath).Length
            });
            var inputRoot = Path.GetDirectoryName(indexPath);
            foreach (var auditFile in auditFiles)
            {
                if (auditFile == null || !IsSha256(auditFile.Sha256))
                    throw new InvalidOperationException("AdministrativeDongDioramaReviewCompletionAuditDescriptorInvalid");
                var auditPath = ResolveInputFile(inputRoot, auditFile.RelativePath,
                    "AdministrativeDongDioramaReviewCompletionAuditPathInvalid");
                if (!AuditFileHashMatches(auditFile.Sha256, Sha256(auditPath)))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewCompletionAuditHashMismatch");
                if (expected.ContainsKey(auditFile.RelativePath))
                    throw new InvalidOperationException("AdministrativeDongDioramaReviewCompletionAuditDescriptorDuplicate");
                expected.Add(auditFile.RelativePath, new ReviewFile
                {
                    RelativePath = auditFile.RelativePath,
                    Sha256 = auditFile.Sha256,
                    ByteLength = new FileInfo(auditPath).Length
                });
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.RelativePath)
                    || !seen.Add(file.RelativePath)
                    || !expected.TryGetValue(file.RelativePath, out var ownerFile)
                    || file.ByteLength != ownerFile.ByteLength
                    || !IsSha256(file.Sha256)
                    || !string.Equals(file.Sha256, ownerFile.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewCompletionFileEntryMismatch");
                var fullPath = ResolveInputFile(inputRoot, file.RelativePath,
                    "AdministrativeDongDioramaReviewCompletionFilePathInvalid");
                if (new FileInfo(fullPath).Length != file.ByteLength
                    || !string.Equals(Sha256(fullPath), file.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewCompletionFileHashMismatch");
            }
            var actualAuditPaths = files.Where(file => file != null
                    && (file.RelativePath.EndsWith("audit.json", StringComparison.Ordinal)
                        || file.RelativePath.EndsWith("road-direction-audit.json", StringComparison.Ordinal)))
                .Select(file => file.RelativePath);
            if (!seen.SetEquals(expected.Keys)
                || !AuditDescriptorPathsMatch(actualAuditPaths,
                    auditFiles.Select(value => value.RelativePath)))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewCompletionFileSetMismatch");
        }

        private static string ResolveInputFile(string inputRoot, string relativePath,
            string errorCode)
        {
            if (string.IsNullOrWhiteSpace(inputRoot)
                || string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath)
                || relativePath.Split('/', '\\').Any(part => part == ".."))
                throw new InvalidOperationException(errorCode);
            var fullPath = Path.GetFullPath(Path.Combine(inputRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rooted = Path.GetFullPath(inputRoot) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rooted, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(errorCode);
            return fullPath;
        }

        private static JObject ParseJsonObject(string json, string errorCode)
        {
            try
            {
                using var text = new StringReader(json);
                using var reader = new JsonTextReader(text)
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal
                };
                return JObject.Load(reader);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(errorCode, exception);
            }
        }

        private static void RequireContentHash(JObject value, string fieldName,
            string errorCode)
        {
            var expected = value?[fieldName]?.Value<string>();
            if (!IsSha256(expected)) throw new InvalidOperationException(errorCode);
            var content = (JObject)value.DeepClone();
            content.Remove(fieldName);
            if (!string.Equals(Sha256Text(CanonicalJson(content)), expected,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(errorCode);
        }

        private static string CanonicalJson(JToken value)
        {
            var builder = new StringBuilder();
            WriteCanonicalJson(builder, value);
            return builder.ToString();
        }

        private static void WriteCanonicalJson(StringBuilder builder, JToken value)
        {
            if (value is JObject owner)
            {
                builder.Append('{');
                var first = true;
                foreach (var property in owner.Properties()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!first) builder.Append(',');
                    first = false;
                    builder.Append(JsonConvert.ToString(property.Name));
                    builder.Append(':');
                    WriteCanonicalJson(builder, property.Value);
                }
                builder.Append('}');
                return;
            }
            if (value is JArray array)
            {
                builder.Append('[');
                for (var index = 0; index < array.Count; index++)
                {
                    if (index > 0) builder.Append(',');
                    WriteCanonicalJson(builder, array[index]);
                }
                builder.Append(']');
                return;
            }
            if (value == null || value.Type == JTokenType.Null
                              || value.Type == JTokenType.Undefined)
            {
                builder.Append("null");
                return;
            }
            if (value.Type == JTokenType.String)
            {
                builder.Append(JsonConvert.ToString(value.Value<string>()));
                return;
            }
            if (value.Type == JTokenType.Boolean)
            {
                builder.Append(value.Value<bool>() ? "true" : "false");
                return;
            }
            if (value.Type == JTokenType.Integer)
            {
                builder.Append(Convert.ToString(((JValue)value).Value,
                    CultureInfo.InvariantCulture));
                return;
            }
            if (value.Type == JTokenType.Float)
            {
                if (((JValue)value).Value is decimal decimalNumber)
                {
                    builder.Append(decimalNumber.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                var number = value.Value<double>();
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException(
                        "AdministrativeDongDioramaReviewNonFiniteJsonNumber");
                var text = number.ToString("R", CultureInfo.InvariantCulture)
                    .Replace('E', 'e');
                if (text.IndexOf('.') < 0 && text.IndexOf('e') < 0) text += ".0";
                builder.Append(text);
                return;
            }
            throw new InvalidOperationException(
                "AdministrativeDongDioramaReviewUnsupportedJsonToken:" + value.Type);
        }

        private static void RequireEnvelopeBooleanProperties(JObject value)
        {
            RequireBooleanProperty(value, "candidateEnvelopeIsPublicationAuthority", false);
            RequireBooleanProperty(value, "candidateEnvelopeReadinessGovernsPromotion", true);
            RequireBooleanProperty(value, "embeddedManifestReadinessIsInformational", true);
            RequireBooleanProperty(value, "publishBlocked", true);
            RequireBooleanProperty(value, "currentPointerUsed", false);
            RequireBooleanProperty(value, "currentPointerUpdated", false);
            RequireBooleanProperty(value, "observationPresentationOnly", true);
            RequireBooleanProperty(value, "distributionApproved", false);
            RequireBooleanProperty(value, "traversalReady", false);
            RequireBooleanProperty(value, "gameplayReady", false);
            RequireBooleanProperty(value, "baseGeometryCaptureReady", true);
            RequireBooleanProperty(value, "lifeDioramaCaptureReady", false);
            RequireBooleanProperty(value, "personalDataIncluded", false);
        }

        private static void RequireBooleanProperty(JObject value, string propertyName,
            bool expected)
        {
            var token = value?[propertyName];
            if (token == null || token.Type != JTokenType.Boolean
                              || token.Value<bool>() != expected)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnsafeFlagRejected:" + propertyName);
        }

        private static bool IsObservationReview(string schemaVersion)
            => string.Equals(schemaVersion, ObservationIndexSchema, StringComparison.Ordinal);

        private static bool IsWalkNetworkReview(string schemaVersion)
            => string.Equals(schemaVersion, WalkNetworkIndexSchema, StringComparison.Ordinal);

        private static bool IsRoadDirectionReview(string schemaVersion)
            => string.Equals(schemaVersion, RoadDirectionIndexSchema,
                StringComparison.Ordinal);
        private static bool IsBiotopeReview(string schemaVersion)
            => string.Equals(schemaVersion, BiotopeIndexSchema, StringComparison.Ordinal);

        private static bool IsLaneMarkingReview(string schemaVersion)
            => string.Equals(schemaVersion, LaneMarkingIndexSchema, StringComparison.Ordinal);

        private static bool IsHorizontalContourTraceReview(string schemaVersion)
            => string.Equals(schemaVersion, HorizontalContourTraceIndexSchema,
                StringComparison.Ordinal);

        private static void RequireEnvelope(string actualSchema, string expectedSchema,
            string desiredReadinessCode, bool candidateAuthority,
            bool readinessGovernsPromotion, bool embeddedReadinessInformational,
            bool publishBlocked, bool currentPointerUsed, bool currentPointerUpdated,
            bool observationOnly, bool distributionApproved, bool traversalReady,
            bool gameplayReady, bool baseReady, bool lifeReady, bool personalData,
            string[] missingLayers)
        {
            if (!string.Equals(actualSchema, expectedSchema, StringComparison.Ordinal)
                || !string.Equals(desiredReadinessCode,
                    AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary,
                    StringComparison.Ordinal)
                || candidateAuthority || !readinessGovernsPromotion
                || !embeddedReadinessInformational || !publishBlocked
                || currentPointerUsed || currentPointerUpdated || !observationOnly
                || distributionApproved || traversalReady || gameplayReady
                || !baseReady || lifeReady || personalData
                || !SetEquals(missingLayers, RequiredMissingLayers))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewUnsafeEnvelopeRejected");
        }

        private static void SuppressOtherPresentationSurfaces(GameObject owner)
        {
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera != null && !camera.transform.IsChildOf(owner.transform))
                    camera.enabled = false;
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                if (canvas != null && !canvas.transform.IsChildOf(owner.transform))
                    canvas.enabled = false;
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.transform.IsChildOf(owner.transform)) continue;
                var type = behaviour.GetType();
                if (string.Equals(type.Namespace, "Ssalddel.Unity.Presentation.World",
                        StringComparison.Ordinal)
                    && type.Name.IndexOf("사가정", StringComparison.Ordinal) >= 0)
                    behaviour.enabled = false;
            }
        }

        private static 행정동디오라마검토View RequireView()
            => Resources.FindObjectsOfTypeAll<행정동디오라마검토View>()
                   .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid())
               ?? throw new InvalidOperationException(
                   "AdministrativeDongDioramaReviewViewMissing");

        private static void ExitWhenStopped(int code)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scenePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..",
                CanonicalScenePath));
            var before = SessionState.GetString(SceneHashKey, string.Empty);
            var after = Sha256(scenePath);
            var dirty = EditorSceneManager.GetActiveScene().isDirty;
            if (code == 0 && (!string.Equals(before, after, StringComparison.OrdinalIgnoreCase)
                              || dirty))
                code = FailureExitCode;
            var manifestPath = Path.Combine(OutputDirectory(), "capture-manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonConvert.DeserializeObject<CaptureManifest>(
                    File.ReadAllText(manifestPath)) ?? new CaptureManifest();
                manifest.sceneSha256After = after;
                manifest.sceneHashUnchanged = string.Equals(before, after,
                    StringComparison.OrdinalIgnoreCase);
                manifest.sceneDirtyAfterPlay = dirty;
                File.WriteAllText(manifestPath,
                    JsonConvert.SerializeObject(manifest, Formatting.Indented));
            }
            SessionState.SetBool(ActiveKey, false);
            Debug.Log("[AdministrativeDongDioramaReviewCliCapture] 종료 · exit=" + code
                      + " · sceneHashUnchanged="
                      + string.Equals(before, after, StringComparison.OrdinalIgnoreCase));
            EditorApplication.Exit(code);
        }

        private static void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false)
                || (type != LogType.Error && type != LogType.Exception
                                          && type != LogType.Assert)) return;
            SessionState.SetInt(ErrorCountKey,
                SessionState.GetInt(ErrorCountKey, 0) + 1);
            if (string.IsNullOrEmpty(SessionState.GetString(FirstErrorKey, string.Empty)))
                SessionState.SetString(FirstErrorKey, condition ?? string.Empty);
        }

        private static void Fail(string code, Exception exception)
        {
            Debug.LogError("[AdministrativeDongDioramaReviewCliCapture] " + code
                           + " · " + exception);
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
            var value = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, value);
            return value >= required;
        }

        private static bool WaitForCapture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)
                                                || new FileInfo(path).Length == 0) return false;
            var started = long.Parse(SessionState.GetString(StartedKey, "0"),
                CultureInfo.InvariantCulture);
            return File.GetLastWriteTimeUtc(path).Ticks >= started;
        }

        private static void SetPhase(Phase value)
        {
            SessionState.SetInt(PhaseKey, (int)value);
            SessionState.SetInt(FrameKey, 0);
        }

        private static string OutputDirectory()
            => SessionState.GetString(OutputKey, string.Empty);

        private static string RelativeOutputPath(string path)
            => Path.GetRelativePath(OutputDirectory(), path).Replace('\\', '/');

        private static string AreaCode(string stableId)
            => stableId.Substring(stableId.LastIndexOf(':') + 1);

        private static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right)
            => new HashSet<string>(left ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(right ?? Array.Empty<string>());

        private static bool HasArgument(string value)
            => Environment.GetCommandLineArgs().Any(argument => string.Equals(argument,
                value, StringComparison.OrdinalIgnoreCase));

        private static string RequiredArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], name,
                        StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                    return arguments[index + 1];
            throw new InvalidOperationException(
                "AdministrativeDongDioramaReviewArgumentMissing:" + name);
        }

        private static bool IsSha256(string value)
            => value != null && value.Length == 64
                             && value.All(character => Uri.IsHexDigit(character));

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(stream)
                .Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
        }

        private static string Sha256Text(string value)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(item => item.ToString("X2", CultureInfo.InvariantCulture)));
        }

        [Serializable]
        private sealed class ReviewIndex
        {
            public string SchemaVersion { get; set; }
            public string ExporterSemanticRevision { get; set; }
            public string ExporterSourceHashSha256 { get; set; }
            public string GenerationHashSha256 { get; set; }
            public string ContentHashSha256 { get; set; }
            public string ScopeStableId { get; set; }
            public string Revision { get; set; }
            public string CandidateBatchId { get; set; }
            public string ProjectionSetHashSha256 { get; set; }
            public string BundleSetHashSha256 { get; set; }
            public string DesiredReadinessCode { get; set; }
            public bool CandidateEnvelopeIsPublicationAuthority { get; set; }
            public bool CandidateEnvelopeReadinessGovernsPromotion { get; set; }
            public bool EmbeddedManifestReadinessIsInformational { get; set; }
            public bool PublishBlocked { get; set; }
            public bool CurrentPointerUsed { get; set; }
            public bool CurrentPointerUpdated { get; set; }
            public bool ObservationPresentationOnly { get; set; }
            public bool DistributionApproved { get; set; }
            public bool TraversalReady { get; set; }
            public bool GameplayReady { get; set; }
            public bool BaseGeometryCaptureReady { get; set; }
            public bool LifeDioramaCaptureReady { get; set; }
            public bool PersonalDataIncluded { get; set; }
            public string[] MissingLayerCodes { get; set; }
            public int AdministrativeAreaCount { get; set; }
            public int BundleCount { get; set; }
            public int BuildingCount { get; set; }
            public int RoadSegmentCount { get; set; }
            public int TileCount { get; set; }
            public int UnresolvedBuildingCount { get; set; }
            public bool PrivateObservationOverlayCaptureReady { get; set; }
            public string PrivateObservationOverlaySetHashSha256 { get; set; }
            public int CrosswalkPointCandidateCount { get; set; }
            public int IntersectionPointCandidateCount { get; set; }
            public bool PrivateWalkNetworkOverlayCaptureReady { get; set; }
            public string PrivateWalkNetworkOverlaySetHashSha256 { get; set; }
            public int WalkNetworkNodePointCandidateCount { get; set; }
            public int WalkNetworkLinkFragmentCandidateCount { get; set; }
            public int WalkNetworkLinkPathVertexCount { get; set; }
            public int WalkNetworkOutputPathVertexCount { get; set; }
            public string WalkNetworkAuditSha256 { get; set; }
            public bool PrivateRoadDirectionOverlayCaptureReady { get; set; }
            public string PrivateRoadDirectionOverlaySetHashSha256 { get; set; }
            public int RoadDirectionPointCandidateCount { get; set; }
            public string RoadDirectionAuditRelativePath { get; set; }
            public string RoadDirectionAuditSha256 { get; set; }
            public string DirectionSourceGenerationHashSha256 { get; set; }
            public bool PrivateBiotopeOverlayCaptureReady { get; set; }
            public string PrivateBiotopeOverlaySetHashSha256 { get; set; }
            [JsonProperty("biotopeCandidateCount")] public int BiotopeCandidateCount { get; set; }
            [JsonProperty("biotopePolygonPartCount")] public int BiotopePolygonPartCount { get; set; }
            [JsonProperty("biotopeInteriorRingCount")] public int BiotopeInteriorRingCount { get; set; }
            [JsonProperty("biotopeRingVertexCount")] public int BiotopeRingVertexCount { get; set; }
            [JsonProperty("sourceLineSegmentCount")] public int BiotopeSourceSegmentCount { get; set; }
            [JsonProperty("subMillimeterLineSegmentCount")] public int BiotopeSkippedSegmentCount { get; set; }
            [JsonProperty("renderableLineSegmentCount")] public int BiotopeRenderedSegmentCount { get; set; }
            public string BiotopeAuditRelativePath { get; set; }
            public string BiotopeAuditSha256 { get; set; }
            public string BiotopeSourceManifestSha256 { get; set; }
            public bool PrivateLaneMarkingOverlayCaptureReady { get; set; }
            public string PrivateLaneMarkingOverlaySetHashSha256 { get; set; }
            public int LaneMarkingCandidateCount { get; set; }
            public int LaneMarkingFragmentCount { get; set; }
            public int LaneMarkingPathVertexCount { get; set; }
            public int LaneMarkingSourceSegmentCount { get; set; }
            public int LaneMarkingRenderedSegmentCount { get; set; }
            public int LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int LaneMarkingDuplicatePhysicalSegmentInstanceCount { get; set; }
            public string LaneMarkingAuditRelativePath { get; set; }
            public string LaneMarkingAuditSha256 { get; set; }
            public string LaneMarkingSourceManifestSha256 { get; set; }
            public string LaneMarkingSourceGenerationHashSha256 { get; set; }
            public string LaneMarkingSourceAuditSha256 { get; set; }
            public bool PrivateHorizontalContourTraceOverlayCaptureReady { get; set; }
            public string PrivateHorizontalContourTraceOverlaySetHashSha256 { get; set; }
            public int HorizontalContourTraceCandidateCount { get; set; }
            public int HorizontalContourTraceFragmentCount { get; set; }
            public int HorizontalContourTracePathVertexCount { get; set; }
            public int HorizontalContourTraceSourceSegmentCount { get; set; }
            public int HorizontalContourTraceRenderedSegmentCount { get; set; }
            public int HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount { get; set; }
            public string HorizontalContourTraceAuditRelativePath { get; set; }
            public string HorizontalContourTraceAuditSha256 { get; set; }
            public string HorizontalContourTraceSourceGenerationHashSha256 { get; set; }
            public string HorizontalContourTraceSourceManifestSha256 { get; set; }
            public string HorizontalContourTraceSourceAuditSha256 { get; set; }
            public bool SourceHorizontalGeometryObserved { get; set; }
            public bool SourceAttributeDatabaseRead { get; set; }
            public bool SourceElevationValuesRead { get; set; }
            public bool SourceElevationValuesSerialized { get; set; }
            public bool CurrentBoundaryEstablished { get; set; }
            public bool CurrentTerrainEstablished { get; set; }
            public bool ContourMeaningAuthorized { get; set; }
            public bool ElevationMeaningAuthorized { get; set; }
            public bool HeightUnitVerified { get; set; }
            public bool VerticalDatumVerified { get; set; }
            public bool TerrainSurfaceAuthorized { get; set; }
            public bool TerrainMeshAuthorized { get; set; }
            public ReviewEntry[] Bundles { get; set; }
        }

        [Serializable]
        private sealed class ReviewEntry
        {
            public string AdministrativeAreaStableId { get; set; }
            public string DisplayName { get; set; }
            public string RelativePath { get; set; }
            public string Sha256 { get; set; }
            public long ByteLength { get; set; }
            public string ContentHashSha256 { get; set; }
            public string ProjectionHashSha256 { get; set; }
            public int BuildingCount { get; set; }
            public int RoadSegmentCount { get; set; }
            public int TileCount { get; set; }
            public bool BaseGeometryCaptureReady { get; set; }
            public bool LifeDioramaCaptureReady { get; set; }
            public bool PrivateObservationOverlayCaptureReady { get; set; }
            public string PrivateObservationOverlaySetHashSha256 { get; set; }
            public int CrosswalkPointCandidateCount { get; set; }
            public int IntersectionPointCandidateCount { get; set; }
            public bool PrivateWalkNetworkOverlayCaptureReady { get; set; }
            public string PrivateWalkNetworkOverlaySetHashSha256 { get; set; }
            public int WalkNetworkNodePointCandidateCount { get; set; }
            public int WalkNetworkLinkFragmentCandidateCount { get; set; }
            public int WalkNetworkLinkPathVertexCount { get; set; }
            public int WalkNetworkOutputPathVertexCount { get; set; }
            public bool PrivateRoadDirectionOverlayCaptureReady { get; set; }
            public string PrivateRoadDirectionOverlaySetHashSha256 { get; set; }
            public int RoadDirectionPointCandidateCount { get; set; }
            public bool PrivateBiotopeOverlayCaptureReady { get; set; }
            public string PrivateBiotopeOverlaySetHashSha256 { get; set; }
            [JsonProperty("biotopeCandidateCount")] public int BiotopeCandidateCount { get; set; }
            [JsonProperty("biotopePolygonPartCount")] public int BiotopePolygonPartCount { get; set; }
            [JsonProperty("biotopeInteriorRingCount")] public int BiotopeInteriorRingCount { get; set; }
            [JsonProperty("biotopeRingVertexCount")] public int BiotopeRingVertexCount { get; set; }
            [JsonProperty("sourceLineSegmentCount")] public int BiotopeSourceSegmentCount { get; set; }
            [JsonProperty("subMillimeterLineSegmentCount")] public int BiotopeSkippedSegmentCount { get; set; }
            [JsonProperty("renderableLineSegmentCount")] public int BiotopeRenderedSegmentCount { get; set; }
            public bool PrivateLaneMarkingOverlayCaptureReady { get; set; }
            public string PrivateLaneMarkingOverlaySetHashSha256 { get; set; }
            public int LaneMarkingCandidateCount { get; set; }
            public int LaneMarkingFragmentCount { get; set; }
            public int LaneMarkingPathVertexCount { get; set; }
            public int LaneMarkingSourceSegmentCount { get; set; }
            public int LaneMarkingRenderedSegmentCount { get; set; }
            public int LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int LaneMarkingDuplicatePhysicalSegmentInstanceCount { get; set; }
            public bool PrivateHorizontalContourTraceOverlayCaptureReady { get; set; }
            public string PrivateHorizontalContourTraceOverlaySetHashSha256 { get; set; }
            public int HorizontalContourTraceCandidateCount { get; set; }
            public int HorizontalContourTraceFragmentCount { get; set; }
            public int HorizontalContourTracePathVertexCount { get; set; }
            public int HorizontalContourTraceSourceSegmentCount { get; set; }
            public int HorizontalContourTraceRenderedSegmentCount { get; set; }
            public int HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount { get; set; }
            public string[] MissingLayerCodes { get; set; }
        }

        [Serializable]
        private sealed class ReviewBundle
        {
            public string SchemaVersion { get; set; }
            public string ExporterSemanticRevision { get; set; }
            public string ExporterSourceHashSha256 { get; set; }
            public string ContentHashSha256 { get; set; }
            public string ScopeStableId { get; set; }
            public string Revision { get; set; }
            public string CandidateBatchId { get; set; }
            public string ProjectionSetHashSha256 { get; set; }
            public string AdministrativeAreaStableId { get; set; }
            public string ProjectionHashSha256 { get; set; }
            public string DesiredReadinessCode { get; set; }
            public string EmbeddedManifestReadinessCode { get; set; }
            public bool CandidateEnvelopeIsPublicationAuthority { get; set; }
            public bool CandidateEnvelopeReadinessGovernsPromotion { get; set; }
            public bool EmbeddedManifestReadinessIsInformational { get; set; }
            public bool PublishBlocked { get; set; }
            public bool CurrentPointerUsed { get; set; }
            public bool CurrentPointerUpdated { get; set; }
            public bool ObservationPresentationOnly { get; set; }
            public bool DistributionApproved { get; set; }
            public bool TraversalReady { get; set; }
            public bool GameplayReady { get; set; }
            public bool BaseGeometryCaptureReady { get; set; }
            public bool LifeDioramaCaptureReady { get; set; }
            public bool PersonalDataIncluded { get; set; }
            public int PublicBusinessCount { get; set; }
            public string[] MissingLayerCodes { get; set; }
            public int BuildingCount { get; set; }
            public int RoadSegmentCount { get; set; }
            public int TileCount { get; set; }
            public bool PrivateObservationOverlayCaptureReady { get; set; }
            public int CrosswalkPointCandidateCount { get; set; }
            public int IntersectionPointCandidateCount { get; set; }
            public bool PrivateWalkNetworkOverlayCaptureReady { get; set; }
            public string PrivateWalkNetworkOverlaySetHashSha256 { get; set; }
            public int WalkNetworkNodePointCandidateCount { get; set; }
            public int WalkNetworkLinkFragmentCandidateCount { get; set; }
            public int WalkNetworkLinkPathVertexCount { get; set; }
            public int WalkNetworkOutputPathVertexCount { get; set; }
            public bool PrivateRoadDirectionOverlayCaptureReady { get; set; }
            public string PrivateRoadDirectionOverlaySetHashSha256 { get; set; }
            public int RoadDirectionPointCandidateCount { get; set; }
            public bool PrivateBiotopeOverlayCaptureReady { get; set; }
            public string PrivateBiotopeOverlaySetHashSha256 { get; set; }
            [JsonProperty("biotopeCandidateCount")] public int BiotopeCandidateCount { get; set; }
            [JsonProperty("biotopePolygonPartCount")] public int BiotopePolygonPartCount { get; set; }
            [JsonProperty("biotopeInteriorRingCount")] public int BiotopeInteriorRingCount { get; set; }
            [JsonProperty("biotopeRingVertexCount")] public int BiotopeRingVertexCount { get; set; }
            [JsonProperty("sourceLineSegmentCount")] public int BiotopeSourceSegmentCount { get; set; }
            [JsonProperty("subMillimeterLineSegmentCount")] public int BiotopeSkippedSegmentCount { get; set; }
            [JsonProperty("renderableLineSegmentCount")] public int BiotopeRenderedSegmentCount { get; set; }
            public bool PrivateLaneMarkingOverlayCaptureReady { get; set; }
            public string PrivateLaneMarkingOverlaySetHashSha256 { get; set; }
            public int LaneMarkingCandidateCount { get; set; }
            public int LaneMarkingFragmentCount { get; set; }
            public int LaneMarkingPathVertexCount { get; set; }
            public int LaneMarkingSourceSegmentCount { get; set; }
            public int LaneMarkingRenderedSegmentCount { get; set; }
            public int LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int LaneMarkingDuplicatePhysicalSegmentInstanceCount { get; set; }
            public bool PrivateHorizontalContourTraceOverlayCaptureReady { get; set; }
            public string PrivateHorizontalContourTraceOverlaySetHashSha256 { get; set; }
            public int HorizontalContourTraceCandidateCount { get; set; }
            public int HorizontalContourTraceFragmentCount { get; set; }
            public int HorizontalContourTracePathVertexCount { get; set; }
            public int HorizontalContourTraceSourceSegmentCount { get; set; }
            public int HorizontalContourTraceRenderedSegmentCount { get; set; }
            public int HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount { get; set; }
            public AdministrativeDongDioramaManifest Manifest { get; set; }
            public AdministrativeDongDioramaTile[] Tiles { get; set; }
            public AdministrativeDongDisplayOverlayResponse DisplayOverlays { get; set; }
            public 행정동디오라마비공개관찰OverlaySetData PrivateObservationOverlays { get; set; }
            public 행정동디오라마비공개보행망OverlaySetData PrivateWalkNetworkOverlays { get; set; }
            public 행정동디오라마비공개도로방향OverlaySetData PrivateRoadDirectionOverlays { get; set; }
            public 행정동디오라마비공개비오톱OverlaySetData PrivateBiotopeOverlays { get; set; }
            public 행정동디오라마비공개차선표시OverlaySetData PrivateLaneMarkingOverlays { get; set; }
            public 행정동디오라마비공개수평등고선추적OverlaySetData
                PrivateHorizontalContourTraceOverlays { get; set; }
        }

        [Serializable]
        private sealed class ReviewCompletion
        {
            public string SchemaVersion { get; set; }
            public string ScopeStableId { get; set; }
            public string Revision { get; set; }
            public string GenerationHashSha256 { get; set; }
            public string IndexRelativePath { get; set; }
            public string IndexSha256 { get; set; }
            public string BundleSetHashSha256 { get; set; }
            public int BundleCount { get; set; }
            public string ExporterSemanticRevision { get; set; }
            public string ExporterSourceHashSha256 { get; set; }
            public string ContentHashSha256 { get; set; }
            public string DesiredReadinessCode { get; set; }
            public bool CandidateEnvelopeIsPublicationAuthority { get; set; }
            public bool CandidateEnvelopeReadinessGovernsPromotion { get; set; }
            public bool EmbeddedManifestReadinessIsInformational { get; set; }
            public bool PublishBlocked { get; set; }
            public bool CurrentPointerUsed { get; set; }
            public bool CurrentPointerUpdated { get; set; }
            public bool ObservationPresentationOnly { get; set; }
            public bool DistributionApproved { get; set; }
            public bool TraversalReady { get; set; }
            public bool GameplayReady { get; set; }
            public bool BaseGeometryCaptureReady { get; set; }
            public bool LifeDioramaCaptureReady { get; set; }
            public bool PersonalDataIncluded { get; set; }
            public string[] MissingLayerCodes { get; set; }
            public bool PrivateObservationOverlayCaptureReady { get; set; }
            public int CrosswalkPointCandidateCount { get; set; }
            public int IntersectionPointCandidateCount { get; set; }
            public bool PrivateWalkNetworkOverlayCaptureReady { get; set; }
            public int WalkNetworkNodePointCandidateCount { get; set; }
            public int WalkNetworkLinkFragmentCandidateCount { get; set; }
            public int WalkNetworkLinkPathVertexCount { get; set; }
            public int WalkNetworkOutputPathVertexCount { get; set; }
            public string WalkNetworkAuditSha256 { get; set; }
            public bool PrivateRoadDirectionOverlayCaptureReady { get; set; }
            public int RoadDirectionPointCandidateCount { get; set; }
            public string RoadDirectionAuditSha256 { get; set; }
            public string DirectionSourceGenerationHashSha256 { get; set; }
            public bool AngleSemanticVerified { get; set; }
            public bool DirectionResolved { get; set; }
            public bool RenderedArrowAuthorized { get; set; }
            public bool SourceLengthUnitVerified { get; set; }
            public bool PublicDisplayAllowed { get; set; }
            public bool SourceCandidateUnityApplyAllowed { get; set; }
            public bool PrivateBiotopeOverlayCaptureReady { get; set; }
            [JsonProperty("biotopeCandidateCount")] public int BiotopeCandidateCount { get; set; }
            [JsonProperty("biotopePolygonPartCount")] public int BiotopePolygonPartCount { get; set; }
            [JsonProperty("biotopeInteriorRingCount")] public int BiotopeInteriorRingCount { get; set; }
            [JsonProperty("biotopeRingVertexCount")] public int BiotopeRingVertexCount { get; set; }
            [JsonProperty("sourceLineSegmentCount")] public int BiotopeSourceSegmentCount { get; set; }
            [JsonProperty("subMillimeterLineSegmentCount")] public int BiotopeSkippedSegmentCount { get; set; }
            [JsonProperty("renderableLineSegmentCount")] public int BiotopeRenderedSegmentCount { get; set; }
            public string BiotopeAuditSha256 { get; set; }
            public string BiotopeSourceManifestSha256 { get; set; }
            public bool PrivateLaneMarkingOverlayCaptureReady { get; set; }
            public int LaneMarkingCandidateCount { get; set; }
            public int LaneMarkingFragmentCount { get; set; }
            public int LaneMarkingPathVertexCount { get; set; }
            public int LaneMarkingSourceSegmentCount { get; set; }
            public int LaneMarkingRenderedSegmentCount { get; set; }
            public int LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int LaneMarkingDuplicatePhysicalSegmentInstanceCount { get; set; }
            public string LaneMarkingAuditSha256 { get; set; }
            public string LaneMarkingSourceManifestSha256 { get; set; }
            public string LaneMarkingSourceGenerationHashSha256 { get; set; }
            public string LaneMarkingSourceAuditSha256 { get; set; }
            public bool PrivateHorizontalContourTraceOverlayCaptureReady { get; set; }
            public string PrivateHorizontalContourTraceOverlaySetHashSha256 { get; set; }
            public int HorizontalContourTraceCandidateCount { get; set; }
            public int HorizontalContourTraceFragmentCount { get; set; }
            public int HorizontalContourTracePathVertexCount { get; set; }
            public int HorizontalContourTraceSourceSegmentCount { get; set; }
            public int HorizontalContourTraceRenderedSegmentCount { get; set; }
            public int HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount { get; set; }
            public int HorizontalContourTraceDuplicatePhysicalSegmentInstanceCount { get; set; }
            public string HorizontalContourTraceAuditSha256 { get; set; }
            public string HorizontalContourTraceSourceGenerationHashSha256 { get; set; }
            public string HorizontalContourTraceSourceManifestSha256 { get; set; }
            public string HorizontalContourTraceSourceAuditSha256 { get; set; }
            public bool SourceHorizontalGeometryObserved { get; set; }
            public bool SourceAttributeDatabaseRead { get; set; }
            public bool SourceElevationValuesRead { get; set; }
            public bool SourceElevationValuesSerialized { get; set; }
            public bool CurrentBoundaryEstablished { get; set; }
            public bool CurrentTerrainEstablished { get; set; }
            public bool ContourMeaningAuthorized { get; set; }
            public bool ElevationMeaningAuthorized { get; set; }
            public bool HeightUnitVerified { get; set; }
            public bool VerticalDatumVerified { get; set; }
            public bool TerrainSurfaceAuthorized { get; set; }
            public bool TerrainMeshAuthorized { get; set; }
            public bool CurrentPointerCreated { get; set; }
            public bool RuntimeAuthorized { get; set; }
            public ReviewFile[] Files { get; set; }
        }

        [Serializable]
        private sealed class ReviewFile
        {
            public string RelativePath { get; set; }
            public string Sha256 { get; set; }
            public long ByteLength { get; set; }
        }

        [Serializable]
        private sealed class ModuleEvidence
        {
            public string administrativeAreaStableId;
            public string displayName;
            public string projectionHashSha256;
            public string bundleSha256;
            public string imageRelativePath;
            public string imageSha256;
            public string readinessCode;
            public int buildingCount;
            public int roadSegmentCount;
            public int boundarySegmentCount;
            public int tileCount;
            public int rendererCount;
            public int meshFilterCount;
            public int colliderCount;
            public int vertexCount;
            public int maximumBatchVertexCount;
            public bool privateObservationOverlayCaptureReady;
            public string privateObservationOverlaySetHashSha256;
            public int crosswalkPointCandidateCount;
            public int intersectionPointCandidateCount;
            public int privateObservationOverlayVertexCount;
            public bool privateWalkNetworkOverlayCaptureReady;
            public string privateWalkNetworkOverlaySetHashSha256;
            public int walkNetworkNodePointCandidateCount;
            public int walkNetworkLinkFragmentCandidateCount;
            public int walkNetworkLinkPathVertexCount;
            public int walkNetworkOutputPathVertexCount;
            public int privateWalkNetworkOverlayVertexCount;
            public bool privateRoadDirectionOverlayCaptureReady;
            public string privateRoadDirectionOverlaySetHashSha256;
            public int roadDirectionPointCandidateCount;
            public int roadDirectionUnresolvedCount;
            public int roadDirectionRenderedArrowCount;
            public int privateRoadDirectionOverlayVertexCount;
            public bool privateBiotopeOverlayCaptureReady;
            public string privateBiotopeOverlaySetHashSha256;
            public int biotopeCandidateCount;
            public int biotopePolygonPartCount;
            public int biotopeInteriorRingCount;
            public int biotopeRingVertexCount;
            public int biotopeSourceSegmentCount;
            public int biotopeRenderedSegmentCount;
            public int biotopeSkippedAtOrBelowOneMillimeterSegmentCount;
            public int privateBiotopeOverlayVertexCount;
            public int privateBiotopeOverlayIndexCount;
            public bool privateLaneMarkingOverlayCaptureReady;
            public string privateLaneMarkingOverlaySetHashSha256;
            public int laneMarkingCandidateCount;
            public int laneMarkingFragmentCount;
            public int laneMarkingPathVertexCount;
            public int laneMarkingSourceSegmentCount;
            public int laneMarkingRenderedSegmentCount;
            public int laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount;
            public int laneMarkingDuplicatePhysicalSegmentInstanceCount;
            public int privateLaneMarkingOverlayVertexCount;
            public int privateLaneMarkingOverlayIndexCount;
            public bool privateHorizontalContourTraceOverlayCaptureReady;
            public string privateHorizontalContourTraceOverlaySetHashSha256;
            public int horizontalContourTraceCandidateCount;
            public int horizontalContourTraceFragmentCount;
            public int horizontalContourTracePathVertexCount;
            public int horizontalContourTraceSourceSegmentCount;
            public int horizontalContourTraceRenderedSegmentCount;
            public int horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount;
            public int horizontalContourTraceDuplicatePhysicalSegmentInstanceCount;
            public int privateHorizontalContourTraceOverlayVertexCount;
            public int privateHorizontalContourTraceOverlayIndexCount;
            public int indexCount;
        }

        [Serializable]
        private sealed class CaptureManifest
        {
            public string schemaVersion;
            public string capturedAtUtc;
            public string unityVersion;
            public string canonicalScenePath;
            public string sceneSha256Before;
            public string sceneSha256After;
            public string sourceIndexPath;
            public string sourceIndexSha256;
            public string sourceIndexContentHashSha256;
            public string scopeStableId;
            public string revision;
            public string candidateBatchId;
            public string bundleSetHashSha256;
            public string projectionSetHashSha256;
            public string exporterSemanticRevision;
            public string exporterSourceHashSha256;
            public string generationHashSha256;
            public string completionMarkerRelativePath;
            public string completionMarkerSha256;
            public string completionMarkerContentHashSha256;
            public int administrativeAreaCount;
            public int buildingCount;
            public int roadSegmentCount;
            public int tileCount;
            public int unresolvedBuildingCount;
            public bool privateObservationOverlayCaptureReady;
            public string privateObservationOverlaySetHashSha256;
            public int crosswalkPointCandidateCount;
            public int intersectionPointCandidateCount;
            public bool privateWalkNetworkOverlayCaptureReady;
            public string privateWalkNetworkOverlaySetHashSha256;
            public int walkNetworkNodePointCandidateCount;
            public int walkNetworkLinkFragmentCandidateCount;
            public int walkNetworkLinkPathVertexCount;
            public int walkNetworkOutputPathVertexCount;
            public bool privateRoadDirectionOverlayCaptureReady;
            public string privateRoadDirectionOverlaySetHashSha256;
            public int roadDirectionPointCandidateCount;
            public int roadDirectionUnresolvedCount;
            public int roadDirectionRenderedArrowCount;
            public int renderedDirectionArrowCount;
            public int privateRoadDirectionOverlayVertexCount;
            public bool privateBiotopeOverlayCaptureReady;
            public string privateBiotopeOverlaySetHashSha256;
            public int biotopeCandidateCount;
            public int biotopePolygonPartCount;
            public int biotopeInteriorRingCount;
            public int biotopeRingVertexCount;
            public int biotopeSourceSegmentCount;
            public int biotopeRenderedSegmentCount;
            public int biotopeSkippedAtOrBelowOneMillimeterSegmentCount;
            public int privateBiotopeOverlayVertexCount;
            public int privateBiotopeOverlayIndexCount;
            public int actualMeshVertexCount;
            public int actualMeshIndexCount;
            public int actualRendererCount;
            public int actualMeshFilterCount;
            public int actualColliderCount;
            public int maximumModuleVertexCount;
            public int maximumBatchVertexCount;
            public int maximumAllowedBatchVertexCount;
            public string biotopeAuditSha256;
            public string biotopeSourceManifestSha256;
            public bool privateLaneMarkingOverlayCaptureReady;
            public string privateLaneMarkingOverlaySetHashSha256;
            public int laneMarkingCandidateCount;
            public int laneMarkingFragmentCount;
            public int laneMarkingPathVertexCount;
            public int laneMarkingSourceSegmentCount;
            public int laneMarkingRenderedSegmentCount;
            public int laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount;
            public int laneMarkingDuplicatePhysicalSegmentInstanceCount;
            public int privateLaneMarkingOverlayVertexCount;
            public int privateLaneMarkingOverlayIndexCount;
            public string laneMarkingAuditSha256;
            public string laneMarkingSourceManifestSha256;
            public string laneMarkingSourceGenerationHashSha256;
            public string laneMarkingSourceAuditSha256;
            public bool privateHorizontalContourTraceOverlayCaptureReady;
            public string privateHorizontalContourTraceOverlaySetHashSha256;
            public int horizontalContourTraceCandidateCount;
            public int horizontalContourTraceFragmentCount;
            public int horizontalContourTracePathVertexCount;
            public int horizontalContourTraceSourceSegmentCount;
            public int horizontalContourTraceRenderedSegmentCount;
            public int horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount;
            public int horizontalContourTraceDuplicatePhysicalSegmentInstanceCount;
            public int privateHorizontalContourTraceOverlayVertexCount;
            public int privateHorizontalContourTraceOverlayIndexCount;
            public string horizontalContourTraceAuditSha256;
            public string horizontalContourTraceSourceGenerationHashSha256;
            public string horizontalContourTraceSourceManifestSha256;
            public string horizontalContourTraceSourceAuditSha256;
            public string roadDirectionAuditSha256;
            public string directionSourceGenerationHashSha256;
            public bool directionUnresolvedOnly;
            public bool angleSemanticVerified;
            public bool directionResolved;
            public bool renderedArrowAuthorized;
            public bool sourceLengthUnitVerified;
            public int moduleOverviewCaptureCount;
            public string targetAdministrativeAreaStableId;
            public string targetOverviewRelativePath;
            public string targetOverviewSha256;
            public string targetDetailRelativePath;
            public string targetDetailSha256;
            public string targetSelectedBuildingStableId;
            public int screenWidth;
            public int screenHeight;
            public string overviewHudMode;
            public string detailHudMode;
            public int consoleErrorCountDuringRun;
            public string firstConsoleErrorDuringRun;
            public bool actualPlayMode;
            public bool actualGameViewCapture;
            public bool geometryOnlyCandidateEvidence;
            public bool privateObservationOverlayCandidateEvidence;
            public bool privateWalkNetworkOverlayCandidateEvidence;
            public bool privateRoadDirectionOverlayCandidateEvidence;
            public bool privateLaneMarkingOverlayCandidateEvidence;
            public bool privateHorizontalContourTraceOverlayCandidateEvidence;
            public bool laneMeaningAuthorized;
            public bool laneDirectionAuthorized;
            public bool laneTrafficAuthorized;
            public bool laneStopLineAuthorized;
            public bool laneSignalAuthorized;
            public bool flatHorizontalTraceOnly;
            public bool sourceHorizontalGeometryObserved;
            public bool sourceAttributeDatabaseRead;
            public bool sourceElevationValuesRead;
            public bool sourceElevationValuesSerialized;
            public bool currentBoundaryEstablished;
            public bool currentTerrainEstablished;
            public bool contourMeaningAuthorized;
            public bool elevationMeaningAuthorized;
            public bool heightUnitVerified;
            public bool verticalDatumVerified;
            public bool terrainSurfaceAuthorized;
            public bool terrainMeshAuthorized;
            public bool historicalBoundaryCandidate;
            public bool currentPointerUsed;
            public bool sceneSaved;
            public bool observationPresentationOnly;
            public bool distributionApproved;
            public bool traversalReady;
            public bool gameplayReady;
            public bool addressVerified;
            public bool parcelGeometryVerified;
            public bool entranceVerified;
            public bool laneSignalSidewalkVerified;
            public bool npcOrOperationalLifecycleVerified;
            public bool lifeDioramaParityVerified;
            public string[] missingLayerCodes;
            public bool sceneHashUnchanged;
            public bool sceneDirtyAfterPlay;
            public ModuleEvidence[] modules;
        }
    }
}
