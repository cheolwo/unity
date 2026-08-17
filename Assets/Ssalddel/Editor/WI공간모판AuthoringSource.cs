using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    internal static class WI공간모판AuthoringSource
    {
        internal const string AuthoritativeRelativeRoot =
            "eng/world-seedbeds/wi-spatial-seedbeds";

        internal static string AuthoritativeRoot()
        {
            var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "source", "repos", "Hongdal", AuthoritativeRelativeRoot));
            if (!Directory.Exists(path))
                throw new InvalidOperationException("WiSpatialSeedbedAuthoritativeRootMissing:" + path);
            return path;
        }

        internal static T ReadJson<T>(string path)
        {
            var value = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            return value ?? throw new InvalidOperationException("WiSpatialSeedbedJsonInvalid:" + path);
        }

        internal static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(stream))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        internal static void ValidateRelativeJsonPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)
                || value.Contains("..", StringComparison.Ordinal)
                || !value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WiSpatialSeedbedDefinitionRefInvalid:" + value);
        }

        internal static T Required<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("RequiredAssetMissing:" + path);

        internal static void EnsureAssetFolder(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            var parts = normalized.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        internal static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("WiSpatialSeedbedBuilderRequiresEditMode");
        }
    }
}
