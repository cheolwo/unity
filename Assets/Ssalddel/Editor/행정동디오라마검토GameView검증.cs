using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
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

        private const string IndexSchema =
            "administrative-dong-diorama-unity-review-index.v1";
        private const string BundleSchema =
            "administrative-dong-diorama-unity-review-bundle.v1";
        private const string CompletionSchema =
            "administrative-dong-diorama-unity-review-completion.v1";
        private const string ExporterRevision =
            "administrative-dong-diorama-unity-review-exporter.r2";
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
                return;
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
            if (!view.TryApply(applied, true, out var diagnostic))
                throw new InvalidOperationException(diagnostic);
            if (view.Snapshot.Buildings.Count != activeEntry.BuildingCount
                || view.Snapshot.Roads.Count != activeEntry.RoadSegmentCount
                || view.Snapshot.TileCount != activeEntry.TileCount
                || view.GetComponentsInChildren<Collider>(true).Length != 0
                || view.BuildReport.RendererBatchCount > 4)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewPresentationCountMismatch");
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
                maximumBatchVertexCount = view.BuildReport.MaximumBatchVertexCount,
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
            if (!view.TryApply(applied, true, out var diagnostic))
                throw new InvalidOperationException(diagnostic);
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
            if (evidence.Count != 30
                || evidence.Select(value => value.administrativeAreaStableId)
                    .Distinct(StringComparer.Ordinal).Count() != 30
                || evidence.Any(value => value.colliderCount != 0
                                         || value.rendererCount > 5))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewEvidenceSetInvalid");
            var view = RequireView();
            var manifest = new CaptureManifest
            {
                schemaVersion = "administrative-dong-diorama-gameview-evidence.v1",
                capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = UnityEngine.Application.unityVersion,
                canonicalScenePath = CanonicalScenePath,
                sceneSha256Before = SessionState.GetString(SceneHashKey, string.Empty),
                sourceIndexPath = SessionState.GetString(IndexKey, string.Empty),
                sourceIndexSha256 = SessionState.GetString(IndexHashKey, string.Empty),
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
                administrativeAreaCount = index.AdministrativeAreaCount,
                buildingCount = index.BuildingCount,
                roadSegmentCount = index.RoadSegmentCount,
                tileCount = index.TileCount,
                unresolvedBuildingCount = index.UnresolvedBuildingCount,
                moduleOverviewCaptureCount = evidence.Count,
                targetAdministrativeAreaStableId = TargetArea,
                targetOverviewRelativePath = RelativeOutputPath(targetOverviewPath),
                targetOverviewSha256 = Sha256(targetOverviewPath),
                targetDetailRelativePath = RelativeOutputPath(pendingImagePath),
                targetDetailSha256 = Sha256(pendingImagePath),
                targetSelectedBuildingStableId = view.SelectedBuilding.BuildingStableId,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                consoleErrorCountDuringRun = SessionState.GetInt(ErrorCountKey, 0),
                firstConsoleErrorDuringRun = SessionState.GetString(FirstErrorKey, string.Empty),
                actualPlayMode = true,
                actualGameViewCapture = true,
                geometryOnlyCandidateEvidence = true,
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

        private static ReviewIndex LoadAndValidateIndex()
        {
            var path = SessionState.GetString(IndexKey, string.Empty);
            if (!string.Equals(Sha256(path), SessionState.GetString(IndexHashKey, string.Empty),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewIndexChangedAfterStart");
            var value = JsonConvert.DeserializeObject<ReviewIndex>(File.ReadAllText(path))
                        ?? throw new InvalidOperationException(
                            "AdministrativeDongDioramaReviewIndexInvalid");
            RequireEnvelope(value.SchemaVersion, IndexSchema, value.DesiredReadinessCode,
                value.CandidateEnvelopeIsPublicationAuthority,
                value.CandidateEnvelopeReadinessGovernsPromotion,
                value.EmbeddedManifestReadinessIsInformational, value.PublishBlocked,
                value.CurrentPointerUsed, value.CurrentPointerUpdated,
                value.ObservationPresentationOnly, value.DistributionApproved,
                value.TraversalReady, value.GameplayReady, value.BaseGeometryCaptureReady,
                value.LifeDioramaCaptureReady, value.PersonalDataIncluded,
                value.MissingLayerCodes);
            if (!string.Equals(value.ExporterSemanticRevision, ExporterRevision,
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
                || value.Bundles.Any(item => !item.BaseGeometryCaptureReady
                                             || item.LifeDioramaCaptureReady
                                             || !SetEquals(item.MissingLayerCodes,
                                                 RequiredMissingLayers))
                || value.Bundles.Sum(item => item.BuildingCount) != value.BuildingCount
                || value.Bundles.Sum(item => item.RoadSegmentCount) != value.RoadSegmentCount
                || value.Bundles.Sum(item => item.TileCount) != value.TileCount)
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewIndexCountInvalid");
            ValidateCompletionMarker(path, value);
            value.Bundles = value.Bundles
                .OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                .ToArray();
            return value;
        }

        private static void ValidateCompletionMarker(string indexPath, ReviewIndex owner)
        {
            var markerPath = Path.Combine(Path.GetDirectoryName(indexPath), "complete.json");
            if (!File.Exists(markerPath))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewCompletionMarkerMissing");
            var marker = JsonConvert.DeserializeObject<ReviewCompletion>(
                             File.ReadAllText(markerPath))
                         ?? throw new InvalidOperationException(
                             "AdministrativeDongDioramaReviewCompletionMarkerInvalid");
            RequireEnvelope(marker.SchemaVersion, CompletionSchema,
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
        }

        private static ReviewBundle LoadAndValidateBundle(ReviewIndex owner, ReviewEntry entry)
        {
            var inputRoot = Path.GetDirectoryName(SessionState.GetString(IndexKey, string.Empty));
            if (string.IsNullOrWhiteSpace(entry.RelativePath)
                || Path.IsPathRooted(entry.RelativePath)
                || entry.RelativePath.Split('/', '\\').Any(part => part == ".."))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewBundlePathInvalid");
            var fullPath = Path.GetFullPath(Path.Combine(inputRoot,
                entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rooted = Path.GetFullPath(inputRoot)
                         + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rooted, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath)
                || new FileInfo(fullPath).Length != entry.ByteLength
                || !string.Equals(Sha256(fullPath), entry.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "AdministrativeDongDioramaReviewBundleHashInvalid");
            var value = JsonConvert.DeserializeObject<ReviewBundle>(File.ReadAllText(fullPath))
                        ?? throw new InvalidOperationException(
                            "AdministrativeDongDioramaReviewBundleInvalid");
            RequireEnvelope(value.SchemaVersion, BundleSchema, value.DesiredReadinessCode,
                value.CandidateEnvelopeIsPublicationAuthority,
                value.CandidateEnvelopeReadinessGovernsPromotion,
                value.EmbeddedManifestReadinessIsInformational, value.PublishBlocked,
                value.CurrentPointerUsed, value.CurrentPointerUpdated,
                value.ObservationPresentationOnly, value.DistributionApproved,
                value.TraversalReady, value.GameplayReady, value.BaseGeometryCaptureReady,
                value.LifeDioramaCaptureReady, value.PersonalDataIncluded,
                value.MissingLayerCodes);
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
            return value;
        }

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

        [Serializable]
        private sealed class ReviewIndex
        {
            public string SchemaVersion { get; set; }
            public string ExporterSemanticRevision { get; set; }
            public string ExporterSourceHashSha256 { get; set; }
            public string GenerationHashSha256 { get; set; }
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
            public string ProjectionHashSha256 { get; set; }
            public int BuildingCount { get; set; }
            public int RoadSegmentCount { get; set; }
            public int TileCount { get; set; }
            public bool BaseGeometryCaptureReady { get; set; }
            public bool LifeDioramaCaptureReady { get; set; }
            public string[] MissingLayerCodes { get; set; }
        }

        [Serializable]
        private sealed class ReviewBundle
        {
            public string SchemaVersion { get; set; }
            public string ExporterSemanticRevision { get; set; }
            public string ExporterSourceHashSha256 { get; set; }
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
            public AdministrativeDongDioramaManifest Manifest { get; set; }
            public AdministrativeDongDioramaTile[] Tiles { get; set; }
            public AdministrativeDongDisplayOverlayResponse DisplayOverlays { get; set; }
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
            public int administrativeAreaCount;
            public int buildingCount;
            public int roadSegmentCount;
            public int tileCount;
            public int unresolvedBuildingCount;
            public int moduleOverviewCaptureCount;
            public string targetAdministrativeAreaStableId;
            public string targetOverviewRelativePath;
            public string targetOverviewSha256;
            public string targetDetailRelativePath;
            public string targetDetailSha256;
            public string targetSelectedBuildingStableId;
            public int screenWidth;
            public int screenHeight;
            public int consoleErrorCountDuringRun;
            public string firstConsoleErrorDuringRun;
            public bool actualPlayMode;
            public bool actualGameViewCapture;
            public bool geometryOnlyCandidateEvidence;
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
