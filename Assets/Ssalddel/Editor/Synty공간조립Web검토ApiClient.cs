using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Ssalddel.Unity.Editor
{
    internal sealed class Synty공간조립Web검토ApiClient
    {
        private readonly string baseUrl;
        private readonly string accessToken;

        public Synty공간조립Web검토ApiClient(string baseUrl, string accessToken)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException("운영 API 기준 주소가 올바르지 않습니다.", nameof(baseUrl));
            }

            this.baseUrl = baseUrl.TrimEnd('/') + "/";
            this.accessToken = string.IsNullOrWhiteSpace(accessToken)
                ? throw new ArgumentException("관리자 access token이 필요합니다.", nameof(accessToken))
                : accessToken.Trim();
        }

        public async Task<SyntyWeb검토InboxItem> GetNeedsRevisionItemAsync(string reviewItemStableId)
        {
            var url = baseUrl + Synty공간조립Web검토CapturePipeline.ApiRoute
                      + "?reviewStateCode=NeedsRevision&take=50";
            using var request = UnityWebRequest.Get(url);
            Authorize(request);
            var json = await SendAsync(request);
            var response = JsonUtility.FromJson<SyntyWeb검토InboxResponse>(json)
                           ?? throw new InvalidOperationException("SyntyReviewInboxResponseInvalid");
            return response.items?.FirstOrDefault(value =>
                string.Equals(value.reviewItemStableId, reviewItemStableId, StringComparison.Ordinal));
        }

        public async Task<SyntyWeb검토UploadResponse> UploadCaptureAsync(
            SyntyWeb검토CaptureBundle bundle,
            SyntyWeb검토LocalCapture capture)
        {
            var bytes = File.ReadAllBytes(capture.FilePath);
            var sections = new List<IMultipartFormSection>
            {
                Field("BatchStableId", bundle.BatchStableId),
                Field("ReviewItemStableId", bundle.ReviewItemStableId),
                Field("CaptureStableId", capture.CaptureStableId),
                Field("ViewCode", capture.ViewCode),
                Field("CaptureBundleHash", bundle.CaptureBundleHash),
                Field("ParentCaptureBundleHash", bundle.ParentCaptureBundleHash),
                Field("SourceCompositionHash", bundle.SourceCompositionHash),
                Field("ExpectedReviewItemRevision", bundle.ExpectedReviewItemRevision.ToString(CultureInfo.InvariantCulture)),
                Field("RenderingProfileHash", bundle.RenderingProfileHash),
                Field("ImageSha256", capture.ImageSha256),
                Field("Width", capture.Width.ToString(CultureInfo.InvariantCulture)),
                Field("Height", capture.Height.ToString(CultureInfo.InvariantCulture)),
                new MultipartFormFileSection("File", bytes, capture.FileName, "image/png"),
            };
            using var request = UnityWebRequest.Post(
                baseUrl + Synty공간조립Web검토CapturePipeline.ApiRoute + "/capture-uploads",
                sections);
            Authorize(request);
            var json = await SendAsync(request);
            return JsonUtility.FromJson<SyntyWeb검토UploadResponse>(json)
                   ?? throw new InvalidOperationException("SyntyReviewUploadResponseInvalid");
        }

        public async Task RegisterBatchAsync(
            SyntyWeb검토CaptureBundle bundle,
            IReadOnlyList<SyntyWeb검토UploadResponse> receipts)
        {
            if (receipts.Count != bundle.Captures.Count)
                throw new InvalidOperationException("CaptureReceiptCountMismatch");
            var requestBody = new BatchRequest
            {
                SchemaVersion = Synty공간조립Web검토CapturePipeline.BatchSchemaVersion,
                BatchStableId = bundle.BatchStableId,
                BatchRevision = "capture-" + bundle.CaptureBundleHash[..16],
                Title = "회복 발전소 A Normal · Web 시각 검토 1카드",
                GeneratedAtUtc = bundle.CapturedAtUtc,
                Items = new List<BatchItem>
                {
                    new()
                    {
                        ExpectedRevision = bundle.ExpectedReviewItemRevision,
                        ReviewItemStableId = bundle.ReviewItemStableId,
                        CompositionStableId = Synty공간조립Web검토CapturePipeline.CompositionStableId,
                        DisplayName = "회복 발전소 A · Normal",
                        H1StableId = Synty공간조립Web검토CapturePipeline.H1StableId,
                        H2StableId = Synty공간조립Web검토CapturePipeline.H2StableId,
                        H3StableId = Synty공간조립Web검토CapturePipeline.H3StableId,
                        VariantCode = "A",
                        StateProfileCode = "Normal",
                        CompositionInputHash = bundle.SourceCompositionHash,
                        PlanHash = bundle.PlanHash,
                        RenderingProfileId = Synty공간조립Web검토CapturePipeline.RenderingProfileId,
                        RenderingProfileRevision = Synty공간조립Web검토CapturePipeline.RenderingProfileRevision,
                        RenderingProfileHash = bundle.RenderingProfileHash,
                        ParentCaptureBundleHash = bundle.ParentCaptureBundleHash,
                        CaptureBundleHash = bundle.CaptureBundleHash,
                        PackUsages = PackUsages(),
                        Captures = bundle.Captures.Select((capture, index) =>
                            ToCapture(bundle, capture, receipts[index])).ToList(),
                    },
                },
            };
            var json = JsonUtility.ToJson(requestBody);
            using var request = new UnityWebRequest(
                baseUrl + Synty공간조립Web검토CapturePipeline.ApiRoute + "/batches",
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            Authorize(request);
            await SendAsync(request);
        }

        private static CaptureDto ToCapture(
            SyntyWeb검토CaptureBundle bundle,
            SyntyWeb검토LocalCapture capture,
            SyntyWeb검토UploadResponse receipt)
        {
            if (!string.Equals(receipt.batchStableId, bundle.BatchStableId, StringComparison.Ordinal)
                || !string.Equals(receipt.reviewItemStableId, bundle.ReviewItemStableId, StringComparison.Ordinal)
                || !string.Equals(receipt.captureStableId, capture.CaptureStableId, StringComparison.Ordinal)
                || !string.Equals(receipt.viewCode, capture.ViewCode, StringComparison.Ordinal)
                || !string.Equals(receipt.captureBundleHash, bundle.CaptureBundleHash, StringComparison.Ordinal)
                || !string.Equals(receipt.parentCaptureBundleHash, bundle.ParentCaptureBundleHash, StringComparison.Ordinal)
                || !string.Equals(receipt.sourceCompositionHash, bundle.SourceCompositionHash, StringComparison.Ordinal)
                || receipt.expectedReviewItemRevision != bundle.ExpectedReviewItemRevision
                || !string.Equals(receipt.renderingProfileHash, bundle.RenderingProfileHash, StringComparison.Ordinal)
                || !string.Equals(receipt.uploadedSourceSha256, capture.ImageSha256, StringComparison.Ordinal)
                || !IsSha256(receipt.storedImageSha256))
            {
                throw new InvalidOperationException("CaptureUploadReceiptMismatch");
            }

            return new CaptureDto
            {
                CaptureStableId = capture.CaptureStableId,
                ViewCode = capture.ViewCode,
                DisplayName = capture.DisplayName,
                CaptureUploadId = receipt.captureUploadId,
                StorageProviderCode = receipt.storageProviderCode,
                ContainerName = receipt.containerName,
                ObjectName = receipt.objectName,
                ImageUrl = receipt.imageUrl,
                ImageSha256 = receipt.storedImageSha256,
                ContentType = receipt.contentType,
                ContentLength = receipt.contentLength,
                ETag = receipt.eTag,
                Width = receipt.width,
                Height = receipt.height,
            };
        }

        private static List<PackUsage> PackUsages()
            => new()
            {
                new() { PackCode = "Construction", UsagePercent = 35, RoleCode = "StructureAndRecovery" },
                new() { PackCode = "Nature", UsagePercent = 20, RoleCode = "RecoveryAtmosphere" },
                new() { PackCode = "Farm", UsagePercent = 20, RoleCode = "ProductionAndPower" },
                new() { PackCode = "Town", UsagePercent = 10, RoleCode = "RestAndLifeTrace" },
                new() { PackCode = "City", UsagePercent = 15, RoleCode = "Infrastructure" },
            };

        private static MultipartFormDataSection Field(string name, string value)
            => new(name, value ?? string.Empty, Encoding.UTF8, "text/plain");

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private void Authorize(UnityWebRequest request)
            => request.SetRequestHeader("Authorization", "Bearer " + accessToken);

        private static Task<string> SendAsync(UnityWebRequest request)
        {
            var completion = new TaskCompletionSource<string>();
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    completion.TrySetResult(request.downloadHandler?.text ?? string.Empty);
                    return;
                }

                var response = request.downloadHandler?.text ?? string.Empty;
                if (response.Length > 1200)
                    response = response[..1200];
                completion.TrySetException(new InvalidOperationException(
                    $"SyntyReviewHttpFailed:{request.responseCode}:{request.error}:{response}"));
            };
            return completion.Task;
        }

        [Serializable]
        private sealed class BatchRequest
        {
            public string SchemaVersion;
            public string BatchStableId;
            public string BatchRevision;
            public string Title;
            public string GeneratedAtUtc;
            public List<BatchItem> Items;
        }

        [Serializable]
        private sealed class BatchItem
        {
            public long ExpectedRevision;
            public string ReviewItemStableId;
            public string CompositionStableId;
            public string DisplayName;
            public string H1StableId;
            public string H2StableId;
            public string H3StableId;
            public string VariantCode;
            public string StateProfileCode;
            public string CompositionInputHash;
            public string PlanHash;
            public string RenderingProfileId;
            public string RenderingProfileRevision;
            public string RenderingProfileHash;
            public string ParentCaptureBundleHash;
            public string CaptureBundleHash;
            public List<PackUsage> PackUsages;
            public List<CaptureDto> Captures;
        }

        [Serializable]
        private sealed class PackUsage
        {
            public string PackCode;
            public int UsagePercent;
            public string RoleCode;
        }

        [Serializable]
        private sealed class CaptureDto
        {
            public string CaptureStableId;
            public string ViewCode;
            public string DisplayName;
            public string CaptureUploadId;
            public string StorageProviderCode;
            public string ContainerName;
            public string ObjectName;
            public string ImageUrl;
            public string ImageSha256;
            public string ContentType;
            public long ContentLength;
            public string ETag;
            public int Width;
            public int Height;
        }
    }
}
