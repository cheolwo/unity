using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// Synty 조합물을 일반 Game View와 분리된 임시 촬영 Stage에서 렌더링하고,
    /// 서버의 불변 촬영 영수증을 거쳐 Web 검토 원장에 등록한다.
    /// 이 도구는 Scene 적용 승인이나 E5 증거를 만들지 않는다.
    /// </summary>
    public static partial class Synty공간조립Web검토CapturePipeline
    {
        public const string BatchStableId = "review-batch:synty-power-plant-one-card.r1";
        public const string ReviewItemStableId = "review-item:nature-recovery-plant-a-normal.r1";
        public const string CompositionStableId = "composition:nature-recovery-plant-a.r1";
        public const string H1StableId = "h1-action:nature-recovery-plant-core.r1";
        public const string H2StableId = "h2-composition:nature-restoration-recovery.r1";
        public const string H3StableId = "h3-candidate:nature-threat-recovery";
        public const string RenderingProfileId = "rendering-profile:synty-web-review-normal.r1";
        public const string RenderingProfileRevision = "r1";
        public const string BatchSchemaVersion = "synty-composition-review-batch.v2";
        public const string NeedsRevisionStateCode = "NeedsRevision";
        public const int CaptureWidth = 1600;
        public const int CaptureHeight = 900;
        public const int ReviewOnlyLayer = 31;

        private const string DefaultApiBaseUrl = "https://localhost:7117/";
        private const string ApiBaseUrlEnvironmentName = "SSALDDEL_OPERATIONAL_API_BASE_URL";
        private const string AccessTokenEnvironmentName = "SSALDDEL_UNITY_ADMIN_ACCESS_TOKEN";
        internal const string ApiRoute = "api/v1/platform/world-composition-reviews";
        private const string LocalEvidenceRelativePath = "artifacts/local/synty-web-review";
        private const string PlanRevision = "synty-five-pack-h1-h3-assembly-plan.r1";

        public static string SourceCompositionHash => ComputeSourceCompositionHash();

        public static string RenderingProfileHash => Sha256(string.Join("|",
            RenderingProfileId,
            RenderingProfileRevision,
            CaptureWidth.ToString(CultureInfo.InvariantCulture),
            CaptureHeight.ToString(CultureInfo.InvariantCulture),
            ReviewOnlyLayer.ToString(CultureInfo.InvariantCulture),
            "transparent=false",
            "ui=review-metadata-on-web-only",
            "camera=four-fixed-bounds-relative-views"));

        [MenuItem("Ssalddel/Synty Web 검토/01 회복 발전소 A Normal · 4시점 로컬 촬영 _F10")]
        public static void CaptureOneCardMenu()
        {
            var bundle = CaptureOneCard(string.Empty, 0);
            Debug.Log($"Synty Web 검토용 4시점 촬영 완료: {bundle.OutputFolder}\n"
                      + $"CaptureBundleHash={bundle.CaptureBundleHash}");
            EditorUtility.RevealInFinder(bundle.OutputFolder);
        }

        [MenuItem("Ssalddel/Synty Web 검토/02 회복 발전소 A Normal · 최초 업로드 및 등록")]
        public static async void UploadInitialOneCardMenu()
        {
            try
            {
                await CaptureUploadAndRegisterAsync(string.Empty, 0);
                Debug.Log("Synty Web 검토 최초 1카드가 ReadyForReview로 등록되었습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Ssalddel/Synty Web 검토/03 NeedsRevision 1카드 · 재촬영 및 등록")]
        public static async void RecaptureNeedsRevisionOneCardMenu()
        {
            try
            {
                var api = CreateApiClient();
                var pending = await api.GetNeedsRevisionItemAsync(ReviewItemStableId);
                if (pending == null)
                    throw new InvalidOperationException("NeedsRevision 상태인 회복 발전소 A Normal 검토 항목이 없습니다.");
                if (!string.Equals(pending.composition.compositionInputHash,
                        SourceCompositionHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "원본 조립 입력이 바뀌어 재촬영으로 되살릴 수 없습니다. 서버에서 Stale 갱신을 먼저 처리해야 합니다.");
                }

                await CaptureUploadAndRegisterAsync(
                    pending.composition.captureBundleHash,
                    pending.revision,
                    api);
                Debug.Log("NeedsRevision 1카드의 부모 촬영 묶음과 revision을 확인하고 재등록했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static SyntyWeb검토CaptureBundle CaptureOneCard(
            string parentCaptureBundleHash,
            long expectedReviewItemRevision)
        {
            ValidateLineageInput(parentCaptureBundleHash, expectedReviewItemRevision);
            ValidateRequiredPrefabs();
            var capturedAtUtc = DateTime.UtcNow;
            var outputFolder = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                LocalEvidenceRelativePath,
                capturedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(outputFolder);

            var previousActiveScene = SceneManager.GetActiveScene();
            var captureScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            SceneManager.SetActiveScene(captureScene);
            var temporaryMaterials = new List<Material>();
            try
            {
                var stage = BuildReviewOnlyStage(captureScene, temporaryMaterials);
                ValidateReviewOnlyStage(stage.CompositionRoot, stage.Camera);
                var bounds = CalculateBounds(stage.CompositionRoot);
                var captures = new List<SyntyWeb검토LocalCapture>(Views.Length);
                foreach (var view in Views)
                {
                    PositionCamera(stage.Camera, bounds, view.Direction);
                    var fileName = view.ViewCode + ".png";
                    var filePath = Path.Combine(outputFolder, fileName);
                    CapturePng(stage.Camera, filePath);
                    var bytes = File.ReadAllBytes(filePath);
                    captures.Add(new SyntyWeb검토LocalCapture
                    {
                        CaptureStableId = $"capture:nature-recovery-a-normal:{view.ViewCode.ToLowerInvariant()}.r1",
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
                    .Distinct(StringComparer.Ordinal).Count() != Views.Length)
                {
                    throw new InvalidOperationException(
                        "CaptureViewsAreNotDistinct: URP가 빈 frame 또는 같은 시점을 반복 저장했습니다.");
                }

                var captureBundleHash = Sha256(string.Join("\n",
                    SourceCompositionHash,
                    RenderingProfileHash,
                    parentCaptureBundleHash,
                    capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    string.Join("\n", captures.OrderBy(value => value.ViewCode, StringComparer.Ordinal)
                        .Select(value => value.ViewCode + "=" + value.ImageSha256))));
                var bundle = new SyntyWeb검토CaptureBundle
                {
                    SchemaVersion = BatchSchemaVersion,
                    BatchStableId = BatchStableId,
                    ReviewItemStableId = ReviewItemStableId,
                    SourceCompositionHash = SourceCompositionHash,
                    PlanHash = Sha256(PlanRevision),
                    RenderingProfileHash = RenderingProfileHash,
                    ParentCaptureBundleHash = parentCaptureBundleHash,
                    CaptureBundleHash = captureBundleHash,
                    ExpectedReviewItemRevision = expectedReviewItemRevision,
                    CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    OutputFolder = outputFolder,
                    Captures = captures,
                };
                File.WriteAllText(
                    Path.Combine(outputFolder, "capture-manifest.json"),
                    JsonUtility.ToJson(bundle, true),
                    new UTF8Encoding(false));
                return bundle;
            }
            finally
            {
                foreach (var material in temporaryMaterials)
                    Object.DestroyImmediate(material);
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                EditorSceneManager.CloseScene(captureScene, true);
            }
        }

        public static string Sha256(string value)
            => Sha256(Encoding.UTF8.GetBytes(value ?? string.Empty));

        public static string Sha256(byte[] value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(value))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static async Task CaptureUploadAndRegisterAsync(
            string parentCaptureBundleHash,
            long expectedReviewItemRevision,
            Synty공간조립Web검토ApiClient api = null)
        {
            api ??= CreateApiClient();
            var bundle = CaptureOneCard(parentCaptureBundleHash, expectedReviewItemRevision);
            var receipts = new List<SyntyWeb검토UploadResponse>(bundle.Captures.Count);
            foreach (var capture in bundle.Captures)
                receipts.Add(await api.UploadCaptureAsync(bundle, capture));
            await api.RegisterBatchAsync(bundle, receipts);
        }

        private static Synty공간조립Web검토ApiClient CreateApiClient()
        {
            var baseUrl = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentName);
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = DefaultApiBaseUrl;
            var token = Environment.GetEnvironmentVariable(AccessTokenEnvironmentName);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"관리자 access token이 없습니다. Unity를 시작하기 전에 {AccessTokenEnvironmentName} 환경 변수에 설정하세요.");
            }

            return new Synty공간조립Web검토ApiClient(baseUrl, token);
        }

    }
}
