using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 공간문법ManifestExporter
    {
        public const string ManifestPath =
            "Assets/Ssalddel/Data/WorldSeedbeds/pyeongchang-landscape-grammar.v1.json";
        public const string ExternalOutputEnvironmentVariable =
            "SSALDDEL_LANDSCAPE_GRAMMAR_OUTPUT";

        [MenuItem("Ssalddel/World Placement/공간 문법 안전 Manifest 내보내기")]
        public static string Export()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                    공간문법CompositionCatalogBuilder.CatalogPath)
                ?? throw new InvalidOperationException("LandscapeGrammarCatalogMissing");
            catalog.Validate();

            var manifest = new SafeManifest
            {
                schemaVersion = 1,
                catalogRevision = catalog.CatalogRevision,
                catalogHashSha256 = string.Empty,
                presentationOnly = true,
                entries = catalog.Entries
                    .OrderBy(value => value.CompositionKey, StringComparer.Ordinal)
                    .Select(ToSafeEntry).ToArray(),
            };
            manifest.catalogHashSha256 = ComputeSha256(BuildHashMaterial(manifest));
            var json = JsonUtility.ToJson(manifest, true) + Environment.NewLine;
            ValidateSafeJson(json, manifest);

            Write(ManifestPath, json);
            var external = Environment.GetEnvironmentVariable(ExternalOutputEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(external)) Write(external, json);

            AssetDatabase.Refresh();
            Debug.Log($"LandscapeGrammarSafeManifestExported:{manifest.entries.Length}:"
                + manifest.catalogHashSha256);
            return manifest.catalogHashSha256;
        }

        public static void ExportFromCommandLine()
        {
            Export();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static SafeEntry ToSafeEntry(공간문법CompositionCatalogEntry source)
            => new()
            {
                compositionKey = source.CompositionKey,
                sourceCompositionKey = source.SourceCompositionKey,
                setName = source.Descriptor.SetName,
                variantCode = source.Descriptor.VariantCode,
                familyCode = source.Descriptor.PackCode,
                topologyCode = source.TopologyCode,
                assemblyScaleCode = source.AssemblyScaleCode,
                footprintX = source.Descriptor.Footprint.x,
                footprintY = source.Descriptor.Footprint.y,
                paddingMeters = source.PaddingMeters,
                minimumSlopeDegrees = source.SlopeRange.x,
                maximumSlopeDegrees = source.SlopeRange.y,
                requiresWaterMask = source.RequiresWaterMask,
                hlodEligible = source.HlodEligible,
                edgeProfiles = source.EdgeProfiles
                    .OrderBy(value => value.DirectionCode, StringComparer.Ordinal)
                    .Select(value => new SafeEdge
                    {
                        directionCode = value.DirectionCode,
                        profileCode = value.ProfileCode,
                        required = value.Required,
                    }).ToArray(),
                connectors = source.Descriptor.Connectors
                    .OrderBy(value => value.ConnectorCode, StringComparer.Ordinal)
                    .Select(value => new SafeConnector
                    {
                        connectorCode = value.ConnectorCode,
                        connectorTypeCode = value.ConnectorKindCode,
                        directionCode = value.DirectionCode,
                        routeSignature = value.RouteSignature,
                        localX = value.LocalPosition.x,
                        localY = value.LocalPosition.y,
                        localZ = value.LocalPosition.z,
                        localYaw = value.LocalYaw,
                        width = value.Width,
                        required = value.ExpansionSocket,
                    }).ToArray(),
                allowRepeat = source.RepeatRules.AllowRepeat,
                maxConsecutive = source.RepeatRules.MaxConsecutive,
                recentWindowSize = source.RepeatRules.RecentWindowSize,
                neighborDiversityWeight = source.RepeatRules.NeighborDiversityWeight,
                rotationCodes = source.RepeatRules.RotationCodes.ToArray(),
                mirrorAllowed = source.RepeatRules.MirrorAllowed,
                preferredNeighborTopologyCodes =
                    source.AdjacencyRules.PreferredNeighborTopologyCodes.ToArray(),
                allowedNeighborTopologyCodes =
                    source.AdjacencyRules.AllowedNeighborTopologyCodes.ToArray(),
                forbiddenNeighborTopologyCodes =
                    source.AdjacencyRules.ForbiddenNeighborTopologyCodes.ToArray(),
                canTile = source.ExpansionRules.CanTile,
                canChain = source.ExpansionRules.CanChain,
                canTerminate = source.ExpansionRules.CanTerminate,
                terminationCompositionKeys =
                    source.ExpansionRules.TerminationCompositionKeys.ToArray(),
                seedVersion = source.InternalGeneration.SeedVersion,
                detailGeneratorRevision = source.InternalGeneration.DetailGeneratorRevision,
                allowedLandCoverCodes = source.AllowedLandCoverCodes.ToArray(),
                allowedRegionRoleCodes = source.AllowedRegionRoleCodes.ToArray(),
                triangleCount = source.TriangleCount,
                materialSlotCount = source.MaterialSlotCount,
                rendererCount = source.RendererCount,
                shadowCasterCount = source.ShadowCasterCount,
                colliderCount = source.ColliderCount,
                animatorCount = source.AnimatorCount,
                legacyCompositionKeys = source.LegacyCompositionKeys.ToArray(),
                presentationOnly = source.PresentationOnly,
            };

        private static string BuildHashMaterial(SafeManifest manifest)
        {
            var builder = new StringBuilder()
                .Append(manifest.schemaVersion).Append('|')
                .Append(manifest.catalogRevision).Append('|')
                .Append(manifest.presentationOnly ? '1' : '0').AppendLine();
            foreach (var entry in manifest.entries)
            {
                builder.Append(entry.compositionKey).Append('|')
                    .Append(entry.sourceCompositionKey).Append('|')
                    .Append(entry.topologyCode).Append('|')
                    .Append(entry.assemblyScaleCode).Append('|')
                    .Append(entry.footprintX.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.footprintY.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.minimumSlopeDegrees.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.maximumSlopeDegrees.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                    .Append(string.Join(",", entry.edgeProfiles.Select(value =>
                        value.directionCode + ":" + value.profileCode + ":" + (value.required ? "1" : "0"))))
                    .Append('|').Append(entry.maxConsecutive).Append('|')
                    .Append(entry.recentWindowSize).Append('|')
                    .Append(string.Join(",", entry.rotationCodes)).Append('|')
                    .Append(entry.seedVersion).Append('|')
                    .Append(entry.detailGeneratorRevision).Append('|')
                    .Append(entry.triangleCount).Append('|')
                    .Append(entry.materialSlotCount).AppendLine();
            }
            return builder.ToString();
        }

        private static void ValidateSafeJson(string json, SafeManifest manifest)
        {
            if (manifest.entries.Length != 공간문법CompositionCatalog.ExpectedEntryCount
                || manifest.entries.Select(value => value.compositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != manifest.entries.Length
                || json.Contains("Assets/", StringComparison.Ordinal)
                || json.Contains(".prefab", StringComparison.OrdinalIgnoreCase)
                || json.Contains("guid", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("LandscapeGrammarSafeManifestInvalid");
        }

        private static string ComputeSha256(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(value2 => value2.ToString("x2")));
        }

        private static void Write(string path, string json)
        {
            var absolute = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, json, new UTF8Encoding(false));
        }

        [Serializable]
        private sealed class SafeManifest
        {
            public int schemaVersion;
            public string catalogRevision = string.Empty;
            public string catalogHashSha256 = string.Empty;
            public bool presentationOnly;
            public SafeEntry[] entries = Array.Empty<SafeEntry>();
        }

        [Serializable]
        private sealed class SafeEntry
        {
            public string compositionKey = string.Empty;
            public string sourceCompositionKey = string.Empty;
            public string setName = string.Empty;
            public string variantCode = string.Empty;
            public string familyCode = string.Empty;
            public string topologyCode = string.Empty;
            public string assemblyScaleCode = string.Empty;
            public float footprintX;
            public float footprintY;
            public float paddingMeters;
            public float minimumSlopeDegrees;
            public float maximumSlopeDegrees;
            public bool requiresWaterMask;
            public bool hlodEligible;
            public SafeEdge[] edgeProfiles = Array.Empty<SafeEdge>();
            public SafeConnector[] connectors = Array.Empty<SafeConnector>();
            public bool allowRepeat;
            public int maxConsecutive;
            public int recentWindowSize;
            public float neighborDiversityWeight;
            public string[] rotationCodes = Array.Empty<string>();
            public bool mirrorAllowed;
            public string[] preferredNeighborTopologyCodes = Array.Empty<string>();
            public string[] allowedNeighborTopologyCodes = Array.Empty<string>();
            public string[] forbiddenNeighborTopologyCodes = Array.Empty<string>();
            public bool canTile;
            public bool canChain;
            public bool canTerminate;
            public string[] terminationCompositionKeys = Array.Empty<string>();
            public string seedVersion = string.Empty;
            public string detailGeneratorRevision = string.Empty;
            public string[] allowedLandCoverCodes = Array.Empty<string>();
            public string[] allowedRegionRoleCodes = Array.Empty<string>();
            public int triangleCount;
            public int materialSlotCount;
            public int rendererCount;
            public int shadowCasterCount;
            public int colliderCount;
            public int animatorCount;
            public string[] legacyCompositionKeys = Array.Empty<string>();
            public bool presentationOnly;
        }

        [Serializable]
        private sealed class SafeEdge
        {
            public string directionCode = string.Empty;
            public string profileCode = string.Empty;
            public bool required;
        }

        [Serializable]
        private sealed class SafeConnector
        {
            public string connectorCode = string.Empty;
            public string connectorTypeCode = string.Empty;
            public string directionCode = string.Empty;
            public string routeSignature = string.Empty;
            public float localX;
            public float localY;
            public float localZ;
            public float localYaw;
            public float width;
            public bool required;
        }
    }
}
