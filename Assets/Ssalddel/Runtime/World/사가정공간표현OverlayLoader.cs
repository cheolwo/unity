using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>lowerCamelCase JSON을 Unity 직렬화와 분리해 읽는다.</summary>
    public static class 사가정공간표현OverlayParser
    {
        public const int MaximumPayloadBytes = 64 * 1024 * 1024;

        public static 사가정공간표현OverlayData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("SpatialOverlayJsonMissing");

            byte[] bytes;
            try { bytes = new UTF8Encoding(false, true).GetBytes(json); }
            catch (EncoderFallbackException exception)
            {
                throw new ArgumentException("SpatialOverlayJsonEncodingInvalid", exception);
            }

            return Parse(bytes);
        }

        public static 사가정공간표현OverlayData Parse(byte[] utf8Json)
        {
            if (utf8Json == null || utf8Json.Length == 0)
                throw new ArgumentException("SpatialOverlayJsonMissing");
            if (utf8Json.Length > MaximumPayloadBytes)
                throw new ArgumentException("SpatialOverlayJsonBudgetExceeded");

            try
            {
                using var stream = new MemoryStream(utf8Json, false);
                var serializer = new DataContractJsonSerializer(
                    typeof(사가정공간표현OverlayData),
                    new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true
                    });
                return serializer.ReadObject(stream) as 사가정공간표현OverlayData
                    ?? throw new SerializationException("SpatialOverlayJsonNull");
            }
            catch (Exception exception) when (exception is SerializationException
                                               || exception is FormatException
                                               || exception is ArgumentException)
            {
                throw new ArgumentException("SpatialOverlayJsonInvalid", exception);
            }
        }
    }

    /// <summary>
    /// 로컬 비공개 파일 접근을 한 곳에 가둔다. 환경 변수와 EditorPrefs 값은
    /// 호출 순간에만 읽고 결과나 Scene 구성 요소에는 보존하지 않는다.
    /// </summary>
    internal static class 사가정공간표현LocalPrivateReviewLoader
    {
        internal const string EnvironmentKey = "SSALDDEL_SAGAJEONG_PRIVATE_OVERLAY";

        internal static byte[] Read(Func<string, string> preferenceReader = null)
        {
            var path = ResolvePath(preferenceReader);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("SpatialOverlayLocalPathMissing");

            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException
                                               || exception is NotSupportedException
                                               || exception is PathTooLongException)
            {
                throw new ArgumentException("SpatialOverlayLocalPathInvalid", exception);
            }

            try
            {
                for (var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath));
                     directory != null;
                     directory = directory.Parent)
                {
                    if (directory.Exists
                        && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new ArgumentException("SpatialOverlayLocalReparseRejected");
                }

                var file = new FileInfo(fullPath);
                if (!file.Exists || file.Length <= 0
                    || file.Length > 사가정공간표현OverlayParser.MaximumPayloadBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("SpatialOverlayLocalFileUnavailable");

                return File.ReadAllBytes(fullPath);
            }
            catch (ArgumentException) { throw; }
            catch (Exception exception) when (exception is IOException
                                               || exception is UnauthorizedAccessException
                                               || exception is System.Security.SecurityException)
            {
                // 로컬 절대 경로나 원자료 내용을 오류 문자열에 포함하지 않는다.
                throw new ArgumentException("SpatialOverlayLocalReadFailed", exception);
            }
        }

        private static string ResolvePath(Func<string, string> preferenceReader)
        {
            var environmentPath = Environment.GetEnvironmentVariable(EnvironmentKey);
            if (!string.IsNullOrWhiteSpace(environmentPath)) return environmentPath;

            var preferencePath = preferenceReader?.Invoke(EnvironmentKey);
            if (string.IsNullOrWhiteSpace(preferencePath))
                preferencePath = ReadEditorPreference(EnvironmentKey);
            return preferencePath;
        }

        private static string ReadEditorPreference(string key)
        {
#if UNITY_EDITOR
            try
            {
                var type = Type.GetType("UnityEditor.EditorPrefs, UnityEditor.CoreModule", false)
                           ?? Type.GetType("UnityEditor.EditorPrefs, UnityEditor", false);
                var method = type?.GetMethod("GetString", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string) }, null);
                return method?.Invoke(null, new object[] { key, string.Empty }) as string;
            }
            catch
            {
                // EditorPrefs를 읽을 수 없으면 비공개 자료를 자동 탐색하지 않는다.
            }
#endif
            return string.Empty;
        }
    }

    /// <summary>비공개 Editor 파일과 배포 승인 Resources 자료의 진입점을 분리한다.</summary>
    public static class 사가정공간표현OverlayPipeline
    {
        public const string LocalPrivateReviewPathKey = "SSALDDEL_SAGAJEONG_PRIVATE_OVERLAY";
        public const string ApprovedResourceName = "SagajeongSpatialPresentationOverlay";
        public const string ApprovedResourceHashName =
            "SagajeongSpatialPresentationOverlayApproval";

        // 이 목록은 별도 권리 검토를 마친 공개 자산의 판본과 실제 byte SHA-256만 담는다.
        // 동봉된 해시 파일은 전송 무결성일 뿐 승인 근거가 아니므로 현재 승인 자산이 없는
        // 상태에서는 의도적으로 비어 있다.
        private static readonly IReadOnlyDictionary<string, string> ApprovedResourceAssetHashes =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal));

        public static 사가정공간표현Composition LoadLocalPrivateReviewForEditor(
            공간참고자료 baseMap,
            byte[] baseMapBytes)
        {
            사가정공간표현OverlayComposer.ValidateBaseMap(baseMap, baseMapBytes);
#if UNITY_EDITOR
            try
            {
                var bytes = 사가정공간표현LocalPrivateReviewLoader.Read();
                return ParseAndCompose(bytes, baseMap, baseMapBytes,
                    사가정공간표현OverlayBoundary.LocalPrivateReview);
            }
            catch (ArgumentException exception)
            {
                return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                    SafeCode(exception, "SpatialOverlayLocalRejected"));
            }
#else
            return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                "SpatialOverlayPrivateEditorOnly");
#endif
        }

        /// <summary>
        /// 호출자가 Unity Resources.Load&lt;TextAsset&gt; 같은 배포 경계에서 제공한 byte만 읽는다.
        /// delegate에는 확장자 없는 고정 Resources 이름만 전달한다.
        /// </summary>
        public static 사가정공간표현Composition LoadApprovedResource(
            공간참고자료 baseMap,
            byte[] baseMapBytes,
            Func<string, byte[]> resourceReader)
        {
            사가정공간표현OverlayComposer.ValidateBaseMap(baseMap, baseMapBytes);
            if (resourceReader == null)
                return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                    "SpatialOverlayResourceReaderMissing");

            try
            {
                var bytes = resourceReader(ApprovedResourceName);
                if (bytes == null || bytes.Length == 0)
                    return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                        "SpatialOverlayResourceUnavailable");
                var hashReceipt = resourceReader(ApprovedResourceHashName);
                var assetHash = ValidateApprovedResourceHash(bytes, hashReceipt);
                var composition = ParseAndComposeCore(bytes, baseMap, baseMapBytes,
                    사가정공간표현OverlayBoundary.ApprovedResource);
                if (!composition.OverlayApplied) return composition;
                if (!ApprovedResourceAssetHashes.TryGetValue(composition.Revision,
                        out var approvedHash))
                    return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                        "SpatialOverlayResourceRevisionNotApproved");
                if (!string.Equals(assetHash, approvedHash, StringComparison.OrdinalIgnoreCase))
                    return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                        "SpatialOverlayResourceAssetNotApproved");
                return composition;
            }
            catch (ArgumentException exception)
            {
                return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                    SafeCode(exception, "SpatialOverlayResourceRejected"));
            }
            catch
            {
                return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                    "SpatialOverlayResourceReadFailed");
            }
        }

        public static 사가정공간표현Composition ParseAndCompose(
            byte[] overlayBytes,
            공간참고자료 baseMap,
            byte[] baseMapBytes,
            사가정공간표현OverlayBoundary boundary)
        {
            사가정공간표현OverlayComposer.ValidateBaseMap(baseMap, baseMapBytes);
            if (boundary == 사가정공간표현OverlayBoundary.ApprovedResource)
                return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                    "SpatialOverlayApprovedResourceMustUseLoader");
            return ParseAndComposeCore(overlayBytes, baseMap, baseMapBytes, boundary);
        }

        private static 사가정공간표현Composition ParseAndComposeCore(
            byte[] overlayBytes,
            공간참고자료 baseMap,
            byte[] baseMapBytes,
            사가정공간표현OverlayBoundary boundary)
        {
            try
            {
                var data = 사가정공간표현OverlayParser.Parse(overlayBytes);
                return 사가정공간표현OverlayComposer.ComposeOrFallback(
                    data, baseMap, baseMapBytes, boundary);
            }
            catch (ArgumentException exception)
            {
                return 사가정공간표현OverlayComposer.Fallback(baseMap, baseMapBytes,
                    SafeCode(exception, "SpatialOverlayRejected"));
            }
        }

        private static string ValidateApprovedResourceHash(
            byte[] overlayBytes, byte[] receiptBytes)
        {
            if (receiptBytes == null || receiptBytes.Length == 0)
                throw new ArgumentException("SpatialOverlayResourceHashReceiptMissing");
            if (receiptBytes.Length > 128)
                throw new ArgumentException("SpatialOverlayResourceHashReceiptInvalid");

            string expected;
            try { expected = new UTF8Encoding(false, true).GetString(receiptBytes).Trim(); }
            catch (DecoderFallbackException exception)
            {
                throw new ArgumentException("SpatialOverlayResourceHashReceiptInvalid", exception);
            }

            if (expected.Length != 64 || expected.Any(value => !Uri.IsHexDigit(value)))
                throw new ArgumentException("SpatialOverlayResourceHashReceiptInvalid");

            string actual;
            using (var sha = SHA256.Create())
            {
                actual = BitConverter.ToString(sha.ComputeHash(overlayBytes)).Replace("-", "");
            }
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("SpatialOverlayResourceHashMismatch");
            return actual;
        }

        private static string SafeCode(ArgumentException exception, string fallback)
        {
            var code = exception.Message;
            if (string.IsNullOrWhiteSpace(code) || code.IndexOfAny(new[] { '/', '\\' }) >= 0
                || code.Length > 96)
                return fallback;
            return code;
        }
    }
}
