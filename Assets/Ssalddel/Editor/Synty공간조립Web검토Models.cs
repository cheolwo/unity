using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Editor
{
    [Serializable]
    public sealed class SyntyWeb검토CaptureBundle
    {
        public string SchemaVersion;
        public string BatchStableId;
        public string ReviewItemStableId;
        public string SourceCompositionHash;
        public string PlanHash;
        public string RenderingProfileHash;
        public string ParentCaptureBundleHash;
        public string CaptureBundleHash;
        public long ExpectedReviewItemRevision;
        public string CapturedAtUtc;
        public string OutputFolder;
        public List<SyntyWeb검토LocalCapture> Captures = new();
    }

    [Serializable]
    public sealed class SyntyWeb검토LocalCapture
    {
        public string CaptureStableId;
        public string ViewCode;
        public string DisplayName;
        public string FileName;
        [NonSerialized] public string FilePath;
        public string ImageSha256;
        public int Width;
        public int Height;
    }

    [Serializable]
    internal sealed class SyntyWeb검토UploadResponse
    {
        public string captureUploadId;
        public string batchStableId;
        public string reviewItemStableId;
        public string captureStableId;
        public string viewCode;
        public string captureBundleHash;
        public string parentCaptureBundleHash;
        public string sourceCompositionHash;
        public long expectedReviewItemRevision;
        public string renderingProfileHash;
        public string storageProviderCode;
        public string containerName;
        public string objectName;
        public string imageUrl;
        public string uploadedSourceSha256;
        public string storedImageSha256;
        public string contentType;
        public long contentLength;
        public string eTag;
        public int width;
        public int height;
    }

    [Serializable]
    internal sealed class SyntyWeb검토InboxResponse
    {
        public List<SyntyWeb검토InboxItem> items;
    }

    [Serializable]
    internal sealed class SyntyWeb검토InboxItem
    {
        public string reviewItemStableId;
        public long revision;
        public string reviewStateCode;
        public SyntyWeb검토InboxComposition composition;
    }

    [Serializable]
    internal sealed class SyntyWeb검토InboxComposition
    {
        public string compositionInputHash;
        public string captureBundleHash;
    }
}
