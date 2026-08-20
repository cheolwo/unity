using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    [Serializable]
    public sealed class SyntyH공간조립검토Job
    {
        public string BatchStableId;
        public string BatchTitle;
        public string ReviewItemStableId;
        public string CompositionStableId;
        public string DisplayName;
        public string ReviewTargetLevelCode = "H1";
        public string ReviewTargetStableId;
        public string H1StableId;
        public string H2StableId;
        public string H3StableId;
        public string H4StableId;
        public string VariantCode = "A";
        public string StateProfileCode = "Default";
        public string PlanHash;
    }

    public static partial class Synty공간조립Web검토CapturePipeline
    {
        public const string GenericBatchSchemaVersion = "synty-composition-review-batch.v3";
        private const string GenericRenderingProfileRevision = "r1";

        public static SyntyWeb검토CaptureBundle CaptureHierarchySelection(
            GameObject selectedRoot,
            SyntyH공간조립검토Job job,
            string parentCaptureBundleHash = "",
            long expectedReviewItemRevision = 0,
            string outputFolderOverride = null)
        {
            if (selectedRoot == null)
                throw new ArgumentNullException(nameof(selectedRoot));
            ValidateGenericJob(job);
            ValidateLineageInput(parentCaptureBundleHash, expectedReviewItemRevision);

            var views = GenericViews(job.ReviewTargetLevelCode);
            var sourceCompositionHash = ComputeSelectedCompositionHash(selectedRoot.transform, job);
            var renderingProfileId = "rendering-profile:synty-h-"
                                     + job.ReviewTargetLevelCode.ToLowerInvariant()
                                     + "-review.r1";
            var renderingProfileHash = Sha256(string.Join("|",
                renderingProfileId,
                GenericRenderingProfileRevision,
                CaptureWidth.ToString(CultureInfo.InvariantCulture),
                CaptureHeight.ToString(CultureInfo.InvariantCulture),
                ReviewOnlyLayer.ToString(CultureInfo.InvariantCulture),
                CaptureProfileCode(job.ReviewTargetLevelCode),
                "selected-root-clone",
                "ui=none"));
            var capturedAtUtc = DateTime.UtcNow;
            var outputFolder = string.IsNullOrWhiteSpace(outputFolderOverride)
                ? Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath,
                    "..",
                    LocalEvidenceRelativePath,
                    "h-hierarchy",
                    capturedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)))
                : Path.GetFullPath(outputFolderOverride);
            Directory.CreateDirectory(outputFolder);

            var previousActiveScene = SceneManager.GetActiveScene();
            var captureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(captureScene);
            try
            {
                var stageRoot = NewSceneObject("SyntyH검토CaptureStage_PresentationOnly", captureScene).transform;
                var clone = Object.Instantiate(selectedRoot);
                clone.name = selectedRoot.name + "_검토복제";
                SceneManager.MoveGameObjectToScene(clone, captureScene);
                clone.transform.SetParent(stageRoot, false);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                SetLayerRecursively(clone, ReviewOnlyLayer);

                var camera = CreateGenericCaptureCamera(captureScene, stageRoot);
                CreateGenericCaptureLights(captureScene, stageRoot);
                ValidateReviewOnlyStage(clone.transform, camera);
                var bounds = CalculateBounds(clone.transform);
                var captures = new List<SyntyWeb검토LocalCapture>(views.Count);
                foreach (var view in views)
                {
                    PositionCamera(camera, bounds, view.Direction);
                    var fileName = view.ViewCode + ".png";
                    var filePath = Path.Combine(outputFolder, fileName);
                    CapturePng(camera, filePath);
                    var bytes = File.ReadAllBytes(filePath);
                    captures.Add(new SyntyWeb검토LocalCapture
                    {
                        CaptureStableId = "capture:" + Sha256(job.ReviewItemStableId)[..16]
                                          + ":" + view.ViewCode.ToLowerInvariant() + ".r1",
                        ViewCode = view.ViewCode,
                        DisplayName = view.DisplayName,
                        FileName = fileName,
                        FilePath = filePath,
                        ImageSha256 = Sha256(bytes),
                        Width = CaptureWidth,
                        Height = CaptureHeight,
                    });
                }
                if (captures.Select(value => value.ImageSha256)
                    .Distinct(StringComparer.Ordinal).Count() != captures.Count)
                {
                    throw new InvalidOperationException(
                        "CaptureViewsAreNotDistinct: H 조합물 촬영 시점이 서로 달라야 합니다.");
                }

                var captureBundleHash = Sha256(string.Join("\n",
                    sourceCompositionHash,
                    renderingProfileHash,
                    parentCaptureBundleHash,
                    capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    string.Join("\n", captures.OrderBy(value => value.ViewCode, StringComparer.Ordinal)
                        .Select(value => value.ViewCode + "=" + value.ImageSha256))));
                var bundle = new SyntyWeb검토CaptureBundle
                {
                    SchemaVersion = GenericBatchSchemaVersion,
                    BatchStableId = job.BatchStableId,
                    BatchTitle = job.BatchTitle,
                    ReviewItemStableId = job.ReviewItemStableId,
                    CompositionStableId = job.CompositionStableId,
                    DisplayName = job.DisplayName,
                    H1StableId = job.H1StableId,
                    H2StableId = job.H2StableId,
                    H3StableId = job.H3StableId,
                    H4StableId = job.H4StableId,
                    ReviewTargetLevelCode = job.ReviewTargetLevelCode,
                    ReviewTargetStableId = job.ReviewTargetStableId,
                    CaptureProfileCode = CaptureProfileCode(job.ReviewTargetLevelCode),
                    VariantCode = job.VariantCode,
                    StateProfileCode = job.StateProfileCode,
                    RenderingProfileId = renderingProfileId,
                    RenderingProfileRevision = GenericRenderingProfileRevision,
                    SourceCompositionHash = sourceCompositionHash,
                    PlanHash = job.PlanHash,
                    RenderingProfileHash = renderingProfileHash,
                    ParentCaptureBundleHash = parentCaptureBundleHash,
                    CaptureBundleHash = captureBundleHash,
                    ExpectedReviewItemRevision = expectedReviewItemRevision,
                    CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    OutputFolder = outputFolder,
                    PackUsages = ComputePackUsages(selectedRoot.transform),
                    Captures = captures,
                };
                File.WriteAllText(
                    Path.Combine(outputFolder, "capture-manifest.json"),
                    JsonUtility.ToJson(bundle, true),
                    new System.Text.UTF8Encoding(false));
                return bundle;
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                EditorSceneManager.CloseScene(captureScene, true);
            }
        }

        public static async Task<SyntyWeb검토CaptureBundle> CaptureUploadAndRegisterHierarchySelectionAsync(
            GameObject selectedRoot,
            SyntyH공간조립검토Job job,
            string parentCaptureBundleHash = "",
            long expectedReviewItemRevision = 0)
        {
            var bundle = CaptureHierarchySelection(
                selectedRoot,
                job,
                parentCaptureBundleHash,
                expectedReviewItemRevision);
            await UploadAndRegisterBundleAsync(bundle);
            return bundle;
        }

        public static int ExpectedHierarchyCaptureCount(string hierarchyLevelCode)
            => GenericViews(hierarchyLevelCode).Count;

        public static string CreateHierarchyReviewPlanHash(SyntyH공간조립검토Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));
            return Sha256(System.Text.Encoding.UTF8.GetBytes(string.Join("|",
                "synty-h-review-plan.v1",
                job.ReviewTargetLevelCode ?? string.Empty,
                job.ReviewTargetStableId ?? string.Empty,
                job.H1StableId ?? string.Empty,
                job.H2StableId ?? string.Empty,
                job.H3StableId ?? string.Empty,
                job.H4StableId ?? string.Empty,
                job.VariantCode ?? string.Empty,
                job.StateProfileCode ?? string.Empty)));
        }

        public static string ComputeSelectedCompositionHash(
            Transform selectedRoot,
            SyntyH공간조립검토Job job)
        {
            if (selectedRoot == null)
                throw new ArgumentNullException(nameof(selectedRoot));
            ValidateGenericJob(job);
            var lines = new List<string>
            {
                job.CompositionStableId,
                job.ReviewTargetLevelCode,
                job.ReviewTargetStableId,
                job.H1StableId ?? string.Empty,
                job.H2StableId ?? string.Empty,
                job.H3StableId ?? string.Empty,
                job.H4StableId ?? string.Empty,
                "variant=" + job.VariantCode,
                "state=" + job.StateProfileCode,
            };
            foreach (var current in selectedRoot.GetComponentsInChildren<Transform>(true)
                         .OrderBy(value => RelativePath(selectedRoot, value), StringComparer.Ordinal))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(current.gameObject);
                var sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
                var isRoot = current == selectedRoot;
                var localPosition = isRoot ? Vector3.zero : current.localPosition;
                var localRotation = isRoot ? Quaternion.identity : current.localRotation;
                var localScale = isRoot ? Vector3.one : current.localScale;
                lines.Add(string.Join("|",
                    RelativePath(selectedRoot, current),
                    sourcePath,
                    sourcePath.Length == 0 ? string.Empty : AssetDatabase.AssetPathToGUID(sourcePath),
                    Number(localPosition.x), Number(localPosition.y), Number(localPosition.z),
                    Number(localRotation.x), Number(localRotation.y),
                    Number(localRotation.z), Number(localRotation.w),
                    Number(localScale.x), Number(localScale.y), Number(localScale.z),
                    current.gameObject.activeSelf ? "active" : "inactive"));
            }
            return Sha256(string.Join("\n", lines));
        }

        private static void ValidateGenericJob(SyntyH공간조립검토Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));
            foreach (var value in new[]
                     {
                         job.BatchStableId, job.BatchTitle, job.ReviewItemStableId,
                         job.CompositionStableId, job.DisplayName, job.ReviewTargetLevelCode,
                         job.ReviewTargetStableId, job.VariantCode, job.StateProfileCode, job.PlanHash,
                     })
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("H 조합물 촬영 작업의 필수 값이 비어 있습니다.");
            }
            if (job.VariantCode is not ("A" or "B" or "C"))
                throw new ArgumentException("VariantCode는 A, B, C 중 하나여야 합니다.");
            if (!IsSha256(job.PlanHash))
                throw new ArgumentException("PlanHash에는 64자리 SHA-256이 필요합니다.");
            var expectedTarget = job.ReviewTargetLevelCode switch
            {
                "H1" => job.H1StableId,
                "H2" => job.H2StableId,
                "H3" => job.H3StableId,
                "H4" => job.H4StableId,
                _ => throw new ArgumentException("검토 대상 계층은 H1~H4 중 하나여야 합니다."),
            };
            if (!string.Equals(job.ReviewTargetStableId, expectedTarget, StringComparison.Ordinal))
                throw new ArgumentException("주 검토 대상이 선택한 H 계보와 일치하지 않습니다.");
            var required = job.ReviewTargetLevelCode switch
            {
                "H1" => new[] { job.H1StableId },
                "H2" => new[] { job.H1StableId, job.H2StableId },
                "H3" => new[] { job.H1StableId, job.H2StableId, job.H3StableId },
                "H4" => new[] { job.H1StableId, job.H2StableId, job.H3StableId, job.H4StableId },
                _ => Array.Empty<string>(),
            };
            if (required.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException(job.ReviewTargetLevelCode + " 촬영에는 해당 단계까지의 H 계보가 필요합니다.");
        }

        private static IReadOnlyList<H촬영시점> GenericViews(string hierarchyLevelCode)
        {
            if (hierarchyLevelCode is not ("H1" or "H2" or "H3" or "H4"))
                throw new ArgumentException("검토 대상 계층은 H1~H4 중 하나여야 합니다.");
            var values = new List<H촬영시점>
            {
                new("FrontThreeQuarter", "정면 3/4", new Vector3(1f, .72f, -1f)),
                new("RearThreeQuarter", "후면 3/4", new Vector3(-1f, .68f, 1f)),
                new("LeftSide", "좌측면", new Vector3(-1f, .55f, -.08f)),
                new("TopOblique", "상부 사선", new Vector3(.75f, 1.55f, -.75f)),
            };
            if (hierarchyLevelCode is "H2" or "H3")
                values.Add(new H촬영시점("EntryAxis", "주 진입축", new Vector3(0f, .38f, -1f)));
            if (hierarchyLevelCode == "H3")
                values.Add(new H촬영시점("TopDown", "전체 위상", new Vector3(.06f, 2f, -.06f)));
            return values;
        }

        private static string CaptureProfileCode(string hierarchyLevelCode)
            => hierarchyLevelCode switch
            {
                "H1" => "H1PlaceFourViews",
                "H2" => "H2BlockFiveViews",
                "H3" => "H3LandscapeSixViews",
                "H4" => "H4WorldFourViews",
                _ => throw new ArgumentException("검토 대상 계층은 H1~H4 중 하나여야 합니다."),
            };

        private static Camera CreateGenericCaptureCamera(Scene scene, Transform parent)
        {
            var cameraObject = NewSceneObject("CaptureCamera_H계층검토", scene);
            cameraObject.transform.SetParent(parent, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << ReviewOnlyLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.58f, .72f, .8f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            return camera;
        }

        private static void CreateGenericCaptureLights(Scene scene, Transform parent)
        {
            foreach (var setup in new[]
                     {
                         ("CaptureLight_주광", new Vector3(48f, -32f, 0f), 1.15f, new Color(1f, .94f, .84f, 1f)),
                         ("CaptureLight_보조", new Vector3(35f, 145f, 0f), .55f, new Color(.72f, .82f, 1f, 1f)),
                     })
            {
                var lightObject = NewSceneObject(setup.Item1, scene);
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.rotation = Quaternion.Euler(setup.Item2);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = setup.Item3;
                light.color = setup.Item4;
                light.cullingMask = 1 << ReviewOnlyLayer;
            }
        }

        private static List<SyntyWeb검토PackUsage> ComputePackUsages(Transform root)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var pack in PackCodes(renderer))
                    counts[pack] = counts.TryGetValue(pack, out var count) ? count + 1 : 1;
            }
            if (counts.Count == 0)
                throw new InvalidOperationException("SelectedRootHasNoRecognizedSyntyRenderer");
            var total = counts.Values.Sum();
            var provisional = counts.OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => new
                {
                    value.Key,
                    Count = value.Value,
                    Percent = value.Value * 100 / total,
                    Remainder = value.Value * 100 % total,
                }).ToList();
            var remaining = 100 - provisional.Sum(value => value.Percent);
            var bonuses = provisional.OrderByDescending(value => value.Remainder)
                .ThenBy(value => value.Key, StringComparer.Ordinal)
                .Take(remaining)
                .Select(value => value.Key)
                .ToHashSet(StringComparer.Ordinal);
            var lead = provisional.OrderByDescending(value => value.Count)
                .ThenBy(value => value.Key, StringComparer.Ordinal).First().Key;
            return provisional.Select(value => new SyntyWeb검토PackUsage
            {
                PackCode = value.Key,
                UsagePercent = value.Percent + (bonuses.Contains(value.Key) ? 1 : 0),
                RoleCode = string.Equals(value.Key, lead, StringComparison.Ordinal) ? "Lead" : "Support",
            }).ToList();
        }

        private static IEnumerable<string> PackCodes(Renderer renderer)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer.gameObject);
            var sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(sourcePath))
            {
                paths.Add(sourcePath);
                foreach (var dependency in AssetDatabase.GetDependencies(sourcePath, true))
                    paths.Add(dependency);
            }
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;
                var materialPath = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(materialPath))
                    continue;
                paths.Add(materialPath);
                foreach (var dependency in AssetDatabase.GetDependencies(materialPath, true))
                    paths.Add(dependency);
            }
            return paths.Select(PackCode)
                .Where(value => value != null)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal);
        }

        private static string PackCode(string assetPath)
        {
            if (assetPath.Contains("/PolygonNature/", StringComparison.Ordinal)) return "Nature";
            if (assetPath.Contains("/PolygonFarm/", StringComparison.Ordinal)) return "Farm";
            if (assetPath.Contains("/PolygonTown/", StringComparison.Ordinal)) return "Town";
            if (assetPath.Contains("/PolygonCity/", StringComparison.Ordinal)) return "City";
            if (assetPath.Contains("/PolygonConstruction/", StringComparison.Ordinal)) return "Construction";
            return null;
        }

        private static string RelativePath(Transform root, Transform value)
        {
            if (value == root)
                return ".";
            var names = new Stack<string>();
            for (var current = value; current != null && current != root; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private readonly struct H촬영시점
        {
            public H촬영시점(string viewCode, string displayName, Vector3 direction)
            {
                ViewCode = viewCode;
                DisplayName = displayName;
                Direction = direction;
            }

            public string ViewCode { get; }
            public string DisplayName { get; }
            public Vector3 Direction { get; }
        }
    }
}
