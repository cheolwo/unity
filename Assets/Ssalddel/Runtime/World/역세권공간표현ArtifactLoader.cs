using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 역별 로컬 비공개 산출물을 명시 설정에서만 읽는다. 경로는 호출 순간에만 사용하며
    /// 반환 상태·Scene·Player 자료에는 포함하지 않는다.
    /// </summary>
    internal static class StationDioramaLocalArtifactReader
    {
        internal const int MaximumPayloadBytes = 128 * 1024 * 1024;

        internal static byte[] Read(StationDioramaDatasetDescriptor descriptor,
            Func<string, string> preferenceReader = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (!descriptor.HasPinnedSnapshotFingerprint)
                throw new ArgumentException("StationDioramaSnapshotFingerprintMissing");

            var path = Environment.GetEnvironmentVariable(descriptor.LocalPrivateReviewPathKey);
            if (string.IsNullOrWhiteSpace(path))
                path = preferenceReader?.Invoke(descriptor.LocalPrivateReviewPathKey);
            if (string.IsNullOrWhiteSpace(path))
                path = ReadEditorPreference(descriptor.LocalPrivateReviewPathKey);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("StationDioramaLocalPathMissing");

            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException
                                               || exception is NotSupportedException
                                               || exception is PathTooLongException)
            {
                throw new ArgumentException("StationDioramaLocalPathInvalid", exception);
            }

            try
            {
                var parent = Path.GetDirectoryName(fullPath);
                for (var directory = string.IsNullOrWhiteSpace(parent)
                         ? null
                         : new DirectoryInfo(parent);
                     directory != null;
                     directory = directory.Parent)
                {
                    if (directory.Exists
                        && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new ArgumentException("StationDioramaLocalReparseRejected");
                }

                var file = new FileInfo(fullPath);
                if (!file.Exists || file.Length <= 0 || file.Length > MaximumPayloadBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("StationDioramaLocalFileUnavailable");

                var bytes = File.ReadAllBytes(fullPath);
                if (!string.Equals(Hash(bytes), descriptor.ExpectedAssetHashSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("StationDioramaSnapshotAssetHashMismatch");
                return bytes;
            }
            catch (ArgumentException) { throw; }
            catch (Exception exception) when (exception is IOException
                                               || exception is UnauthorizedAccessException
                                               || exception is System.Security.SecurityException)
            {
                throw new ArgumentException("StationDioramaLocalReadFailed", exception);
            }
        }

        internal static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
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
                // EditorPrefs 접근 실패도 자동 파일 탐색으로 대체하지 않는다.
            }
#endif
            return string.Empty;
        }
    }
}
