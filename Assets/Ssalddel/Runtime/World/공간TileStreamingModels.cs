using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 공간TileStreamingCodes
    {
        public const string RecipeStableId =
            "world-stream:kr:51760:daegwallyeong-farm.v1";
        public const string WaitingForSpatialArtifact = "WaitingForSpatialArtifact";
        public const string Available = "Available";
        public const string ElevationLayer = "elevation";
        public const string LandCoverLayer = "land-cover";
        public const string PlacementMaskLayer = "placement-mask";
        public const string Fixture = "Fixture";
        public const string SimulationServer = "SimulationServer";
        public const string Scenario = "Scenario";
        public const string BuildingObject = "Building";
        public const int CenterX = 700;
        public const int CenterY = 1145;
    }

    [Serializable]
    public sealed class 공간TileStreamRecipeData
    {
        public string RecipeStableId = string.Empty;
        public string RecipeRevision = string.Empty;
        public string RecipeHashSha256 = string.Empty;
        public string CoordinateReferenceSystem = string.Empty;
        public int TileLevel;
        public int TileSizeMeters;
        public int DetailRadius;
        public int ActiveRadius;
        public int PrefetchRadius;
        public int MaxConcurrentTileLoads;
        public double BoundaryPrefetchFraction;
        public int CenterTileX;
        public int CenterTileY;
        public string[] CoverageTileKeys = Array.Empty<string>();
        public string[] LayerCodes = Array.Empty<string>();
        public bool IsOperationalState;
        public string EvidenceKindCode = string.Empty;

        public void Validate()
        {
            if (RecipeStableId != 공간TileStreamingCodes.RecipeStableId
                || RecipeHashSha256 == null || RecipeHashSha256.Length != 64
                || CoordinateReferenceSystem != "EPSG:5186"
                || TileLevel != 2 || TileSizeMeters != 500
                || DetailRadius != 1 || ActiveRadius != 2 || PrefetchRadius != 4
                || MaxConcurrentTileLoads != 4
                || Math.Abs(BoundaryPrefetchFraction - .25d) > .000001d
                || CoverageTileKeys == null || CoverageTileKeys.Length != 121
                || IsOperationalState)
            {
                throw new InvalidOperationException("WorldTileStreamRecipeInvalid");
            }
        }
    }

    [Serializable]
    public sealed class 공간TileStreamManifestData
    {
        public string RecipeStableId = string.Empty;
        public string TileKey = string.Empty;
        public int TileLevel;
        public int TileX;
        public int TileY;
        public int HaloMeters;
        public string ManifestRevision = string.Empty;
        public string ManifestHashSha256 = string.Empty;
        public 공간TileStreamLayerData[] Layers = Array.Empty<공간TileStreamLayerData>();
        public bool IsOperationalState;

        public bool IsWaitingForSpatialArtifact =>
            Layers != null && Layers.Any(value =>
                value != null && value.StatusCode == 공간TileStreamingCodes.WaitingForSpatialArtifact);

        public void Validate()
        {
            if (RecipeStableId != 공간TileStreamingCodes.RecipeStableId
                || TileKey != 공간TileWindowPlanner.TileKey(TileX, TileY)
                || TileLevel != 2 || HaloMeters != 60
                || ManifestHashSha256 == null || ManifestHashSha256.Length != 64
                || Layers == null || Layers.Length == 0 || IsOperationalState)
            {
                throw new InvalidOperationException("WorldTileStreamManifestInvalid");
            }

            foreach (var layer in Layers) layer.Validate();
        }
    }

    [Serializable]
    public sealed class 공간TileStreamLayerData
    {
        public string LayerCode = string.Empty;
        public string StatusCode = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public string SourceRevision = string.Empty;
        public string ArtifactHashSha256;
        public string ArtifactRelativePath;
        public string ArtifactContentPath;
        public string SourceHashSha256;
        public string SourceReferenceDate;
        public string HorizontalCrsCode;
        public string VerticalDatumCode;
        public decimal? ResolutionMeters;
        public string NoDataValue;
        public string ArtifactFormatCode;
        public long? ArtifactByteLength;
        public int? SampleWidth;
        public int? SampleHeight;
        public bool PresentationOnly;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LayerCode)
                || string.IsNullOrWhiteSpace(StatusCode)
                || string.IsNullOrWhiteSpace(EvidenceKindCode)
                || string.IsNullOrWhiteSpace(SourceRevision))
                throw new InvalidOperationException("WorldTileStreamLayerInvalid");

            if (StatusCode == 공간TileStreamingCodes.WaitingForSpatialArtifact
                && (!string.IsNullOrWhiteSpace(ArtifactHashSha256)
                    || !string.IsNullOrWhiteSpace(ArtifactRelativePath)
                    || !string.IsNullOrWhiteSpace(ArtifactContentPath)))
                throw new InvalidOperationException("WaitingSpatialArtifactMustNotInventLocation");
            if (StatusCode == 공간TileStreamingCodes.Available
                && (ArtifactHashSha256 == null || ArtifactHashSha256.Length != 64
                    || SourceHashSha256 == null || SourceHashSha256.Length != 64
                    || string.IsNullOrWhiteSpace(ArtifactContentPath)
                    || Uri.TryCreate(ArtifactContentPath, UriKind.Absolute, out _)
                    || ArtifactContentPath.Contains("..")
                    || HorizontalCrsCode != "EPSG:5186"
                    || string.IsNullOrWhiteSpace(ArtifactFormatCode)
                    || ArtifactByteLength <= 0
                    || SampleWidth <= 0 || SampleHeight <= 0))
                throw new InvalidOperationException("AvailableSpatialArtifactInvalid");
        }
    }

    public sealed class 공간TileArtifactPayloadData
    {
        public string TileKey = string.Empty;
        public string LayerCode = string.Empty;
        public string ArtifactHashSha256 = string.Empty;
        public string ArtifactFormatCode = string.Empty;
        public int SampleWidth;
        public int SampleHeight;
        public byte[] Bytes = Array.Empty<byte>();

        public void Validate()
        {
            if (!공간TileWindowPlanner.TryParse(TileKey, out _, out _)
                || string.IsNullOrWhiteSpace(LayerCode)
                || ArtifactHashSha256 == null || ArtifactHashSha256.Length != 64
                || string.IsNullOrWhiteSpace(ArtifactFormatCode)
                || SampleWidth <= 0 || SampleHeight <= 0 || Bytes == null)
                throw new InvalidOperationException("WorldTileArtifactPayloadInvalid");
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(Bytes)).Replace("-", string.Empty);
            if (!actual.Equals(ArtifactHashSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WorldTileArtifactPayloadHashMismatch");
        }
    }

    [Serializable]
    public sealed class 공간TileActivityData
    {
        public string TileKey = string.Empty;
        public long ActivityRevision;
        public int WorldTick;
        public string[] ActivityStableIds = Array.Empty<string>();
        public bool PresentationOnly;
        public bool IsOperationalState;
    }

    [Serializable]
    public sealed class 공간TileObjectProjectionData
    {
        public string TileKey = string.Empty;
        public string PlacementRevision = string.Empty;
        public string PlacementHashSha256 = string.Empty;
        public 공간TileObjectPlacementData[] Objects = Array.Empty<공간TileObjectPlacementData>();
        public bool PresentationOnly;
        public bool IsOperationalState;

        public void Validate()
        {
            if (!공간TileWindowPlanner.TryParse(TileKey, out _, out _)
                || string.IsNullOrWhiteSpace(PlacementRevision)
                || PlacementHashSha256 == null || PlacementHashSha256.Length != 64
                || Objects == null || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("WorldTileObjectProjectionInvalid");
            foreach (var item in Objects) item.Validate();
            if (Objects.Select(value => value.ObjectStableId)
                    .Distinct(StringComparer.Ordinal).Count() != Objects.Length)
                throw new InvalidOperationException("WorldTileObjectStableIdDuplicate");
        }
    }

    [Serializable]
    public sealed class 공간TileObjectPlacementData
    {
        public string ObjectStableId = string.Empty;
        public string ObjectTypeCode = string.Empty;
        public string VisualKey = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public string LandCoverCode = string.Empty;
        public string RegionRoleCode = string.Empty;
        public double LocalOffsetXMeters;
        public double LocalOffsetYMeters;
        public double RotationDegrees;
        public double FootprintWidthMeters;
        public double FootprintDepthMeters;
        public double HeightMeters;
        public bool CollisionEligible;
        public bool PresentationOnly;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ObjectStableId)
                || ObjectTypeCode != 공간TileStreamingCodes.BuildingObject
                || string.IsNullOrWhiteSpace(VisualKey)
                || EvidenceKindCode != 공간TileStreamingCodes.Scenario
                || string.IsNullOrWhiteSpace(LandCoverCode)
                || string.IsNullOrWhiteSpace(RegionRoleCode)
                || LocalOffsetXMeters < -250d || LocalOffsetXMeters > 250d
                || LocalOffsetYMeters < -250d || LocalOffsetYMeters > 250d
                || FootprintWidthMeters <= 0d || FootprintDepthMeters <= 0d
                || HeightMeters <= 0d || CollisionEligible || !PresentationOnly)
                throw new InvalidOperationException("WorldTileObjectPlacementInvalid");
        }
    }

    public interface I공간TileStreamRepository
    {
        string SourceModeCode { get; }

        Task<공간TileStreamRecipeData> LoadRecipeAsync(
            string recipeStableId, CancellationToken cancellationToken);

        Task<공간TileStreamManifestData> LoadManifestAsync(
            string tileKey, CancellationToken cancellationToken);

        Task<공간TileActivityData> LoadActivitiesAsync(
            string tileKey, CancellationToken cancellationToken);

        Task<공간TileObjectProjectionData> LoadObjectsAsync(
            string tileKey, CancellationToken cancellationToken);

        Task<공간TileArtifactPayloadData> LoadArtifactContentAsync(
            string tileKey,
            string layerCode,
            CancellationToken cancellationToken);
    }

    public static class 공간TileWindowPlanner
    {
        public static string TileKey(int x, int y) => $"kr5186:l2:{x}:{y}";

        public static string[] CreateWindow(int centerX, int centerY, int radius)
        {
            if (radius < 0 || radius > 8)
                throw new ArgumentOutOfRangeException(nameof(radius));
            var keys = new List<string>((radius * 2 + 1) * (radius * 2 + 1));
            for (var y = centerY - radius; y <= centerY + radius; y++)
            for (var x = centerX - radius; x <= centerX + radius; x++)
                keys.Add(TileKey(x, y));
            return keys.ToArray();
        }

        public static bool TryParse(string tileKey, out int x, out int y)
        {
            x = 0;
            y = 0;
            var parts = tileKey == null ? Array.Empty<string>() : tileKey.Split(':');
            return parts.Length == 4 && parts[0] == "kr5186" && parts[1] == "l2"
                && int.TryParse(parts[2], out x) && int.TryParse(parts[3], out y);
        }

        public static void ResolveDirectionalPrefetchCenter(
            int currentX,
            int currentY,
            double normalizedOffsetX,
            double normalizedOffsetY,
            double movementX,
            double movementY,
            double boundaryPrefetchFraction,
            out int prefetchX,
            out int prefetchY)
        {
            if (boundaryPrefetchFraction <= 0d || boundaryPrefetchFraction >= .5d)
                throw new ArgumentOutOfRangeException(nameof(boundaryPrefetchFraction));
            var threshold = .5d - boundaryPrefetchFraction;
            prefetchX = currentX + DirectionalStep(normalizedOffsetX, movementX, threshold);
            prefetchY = currentY + DirectionalStep(normalizedOffsetY, movementY, threshold);
        }

        private static int DirectionalStep(double offset, double movement, double threshold)
        {
            if (Math.Abs(offset) < threshold) return 0;
            if (Math.Abs(movement) <= .000001d) return Math.Sign(offset);
            if (movement > 0d && offset > 0d) return 1;
            if (movement < 0d && offset < 0d) return -1;
            return 0;
        }
    }

    /// <summary>
    /// 서버가 없는 Editor/PlayMode 검증용이다. 실제 지형을 만들지 않고 자료 대기 Manifest만 제공한다.
    /// </summary>
    public sealed class 대관령Farm공간TileStreamFixtureRepository : I공간TileStreamRepository,
        I공간TileLandscapeCompositionRepository
    {
        private const string Hash =
            "9d6e927e758d8f22d19ca45fea98e1198ec42b7ba6c581fa1b97389710600e4e";
        private readonly HashSet<string> coverage = new HashSet<string>(
            공간TileWindowPlanner.CreateWindow(
                공간TileStreamingCodes.CenterX, 공간TileStreamingCodes.CenterY, 5),
            StringComparer.Ordinal);

        public string SourceModeCode => 공간TileStreamingCodes.Fixture;

        public Task<공간TileStreamRecipeData> LoadRecipeAsync(
            string recipeStableId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recipeStableId != 공간TileStreamingCodes.RecipeStableId)
                throw new InvalidOperationException("WorldTileStreamRecipeNotFound");
            var value = new 공간TileStreamRecipeData
            {
                RecipeStableId = recipeStableId,
                RecipeRevision = "world-stream.pyeongchang-farm.fixture.r2",
                RecipeHashSha256 = Hash,
                CoordinateReferenceSystem = "EPSG:5186",
                TileLevel = 2,
                TileSizeMeters = 500,
                DetailRadius = 1,
                ActiveRadius = 2,
                PrefetchRadius = 4,
                MaxConcurrentTileLoads = 4,
                BoundaryPrefetchFraction = .25d,
                CenterTileX = 공간TileStreamingCodes.CenterX,
                CenterTileY = 공간TileStreamingCodes.CenterY,
                CoverageTileKeys = coverage.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                LayerCodes = new[] { "elevation", "land-cover", "placement-mask" },
                IsOperationalState = false,
                EvidenceKindCode = "Derived",
            };
            value.Validate();
            return Task.FromResult(value);
        }

        public Task<공간TileStreamManifestData> LoadManifestAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!coverage.Contains(tileKey)
                || !공간TileWindowPlanner.TryParse(tileKey, out var x, out var y))
                throw new InvalidOperationException("WorldTileStreamTileNotFound");
            var value = new 공간TileStreamManifestData
            {
                RecipeStableId = 공간TileStreamingCodes.RecipeStableId,
                TileKey = tileKey,
                TileLevel = 2,
                TileX = x,
                TileY = y,
                HaloMeters = 60,
                ManifestRevision = "world-stream.tile-manifest.fixture.r1",
                ManifestHashSha256 = Hash,
                Layers = new[]
                {
                    Waiting("elevation", "Observed", "dem-source-registered.runtime-artifact-missing"),
                    Waiting("land-cover", "Derived", "land-cover-runtime-artifact-missing"),
                    Waiting("placement-mask", "Derived", "placement-mask-runtime-artifact-missing"),
                },
                IsOperationalState = false,
            };
            value.Validate();
            return Task.FromResult(value);
        }

        public Task<공간TileActivityData> LoadActivitiesAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!coverage.Contains(tileKey))
                throw new InvalidOperationException("WorldTileStreamTileNotFound");
            return Task.FromResult(new 공간TileActivityData
            {
                TileKey = tileKey,
                ActivityRevision = 0,
                WorldTick = 0,
                ActivityStableIds = Array.Empty<string>(),
                PresentationOnly = true,
                IsOperationalState = false,
            });
        }

        public Task<공간TileObjectProjectionData> LoadObjectsAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!coverage.Contains(tileKey))
                throw new InvalidOperationException("WorldTileStreamTileNotFound");
            var value = new 공간TileObjectProjectionData
            {
                TileKey = tileKey,
                PlacementRevision = "world-stream.object-placement.fixture.r1",
                PlacementHashSha256 = Hash,
                Objects = ScenarioObjects(tileKey),
                PresentationOnly = true,
                IsOperationalState = false,
            };
            value.Validate();
            return Task.FromResult(value);
        }

        public Task<공간TileArtifactPayloadData> LoadArtifactContentAsync(
            string tileKey,
            string layerCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("WorldTileStreamArtifactNotFound");
        }

        public Task<공간LandscapeCompositionTileData> LoadLandscapeCompositionsAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!coverage.Contains(tileKey))
                throw new InvalidOperationException("WorldTileStreamTileNotFound");
            var value = new 공간LandscapeCompositionTileData
            {
                SchemaVersion = 공간LandscapeCompositionCodes.SchemaVersion,
                TileKey = tileKey,
                AreaSetStableId = "pyeongchang-farm-hub-town-v1",
                GraphBuildStableId = "landscape-graph:pyeongchang-farm-hub-town-v1:" + tileKey,
                GrammarRevision = 공간LandscapeCompositionCodes.GrammarRevision,
                StatusCode = 공간LandscapeCompositionCodes.WaitingForSpatialArtifact,
                Unresolved = new[]
                {
                    new 공간LandscapeUnresolvedData
                    {
                        UnresolvedStableId = "unresolved:" + tileKey + ":fixture",
                        ReasonCode = 공간LandscapeCompositionCodes.WaitingForSpatialArtifact,
                        RequiredSemanticCode = "spatial-layer-and-landscape-grammar",
                        EvidenceKindCode = "Derived",
                        Detail = "Fixture는 실제 공간 Graph를 꾸며내지 않습니다.",
                    },
                },
                PresentationOnly = true,
                IsOperationalState = false,
            };
            value.Validate();
            return Task.FromResult(value);
        }

        private static 공간TileStreamLayerData Waiting(
            string code, string evidence, string sourceRevision)
            => new 공간TileStreamLayerData
            {
                LayerCode = code,
                StatusCode = 공간TileStreamingCodes.WaitingForSpatialArtifact,
                EvidenceKindCode = evidence,
                SourceRevision = sourceRevision,
                ArtifactHashSha256 = null,
                ArtifactRelativePath = null,
                PresentationOnly = false,
            };

        private static 공간TileObjectPlacementData[] ScenarioObjects(string tileKey)
        {
            공간TileObjectPlacementData Building(
                string stableId, string visualKey, double offsetX, double offsetY,
                double rotation, double width, double depth, double height)
                => new 공간TileObjectPlacementData
                {
                    ObjectStableId = stableId,
                    ObjectTypeCode = 공간TileStreamingCodes.BuildingObject,
                    VisualKey = visualKey,
                    EvidenceKindCode = 공간TileStreamingCodes.Scenario,
                    LandCoverCode = 법정동LandCoverCodes.Cropland,
                    RegionRoleCode = 법정동WorldRoleCodes.Farm,
                    LocalOffsetXMeters = offsetX,
                    LocalOffsetYMeters = offsetY,
                    RotationDegrees = rotation,
                    FootprintWidthMeters = width,
                    FootprintDepthMeters = depth,
                    HeightMeters = height,
                    CollisionEligible = false,
                    PresentationOnly = true,
                };

            if (tileKey == 공간TileWindowPlanner.TileKey(700, 1145))
                return new[]
                {
                    Building("scenario-object:pyeongchang-farm:barn-a",
                        법정동경관VisualKeys.Barn, 78d, 42d, 18d, 32d, 24d, 18d),
                    Building("scenario-object:pyeongchang-farm:silo-a",
                        법정동경관VisualKeys.Silo, 132d, 58d, 0d, 14d, 14d, 26d),
                };
            if (tileKey == 공간TileWindowPlanner.TileKey(701, 1145))
                return new[]
                {
                    Building("scenario-object:pyeongchang-farm:farmhouse-east",
                        법정동경관VisualKeys.Farmhouse, -122d, 36d, -12d, 26d, 20d, 15d),
                };
            if (tileKey == 공간TileWindowPlanner.TileKey(699, 1145))
                return new[]
                {
                    Building("scenario-object:pyeongchang-farm:greenhouse-west",
                        법정동경관VisualKeys.Greenhouse, 118d, -48d, 8d, 34d, 16d, 11d),
                };
            if (tileKey == 공간TileWindowPlanner.TileKey(700, 1146))
                return new[]
                {
                    Building("scenario-object:pyeongchang-farm:produce-stand-north",
                        법정동경관VisualKeys.ProduceStand, -68d, -136d, 24d, 16d, 12d, 9d),
                };
            return Array.Empty<공간TileObjectPlacementData>();
        }
    }
}
