using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Ssalddel.Unity.Editor
{
    public static class AreaSet배치CoordinateCodes
    {
        public const string ScenarioLocalMeters = "ScenarioLocalMeters";
        public const string UnityAxis = "XRightYUpZForward";
    }

    public static class AreaSet배치HeightPolicyCodes
    {
        public const string SurfaceSnap = "SurfaceSnap";
        public const string SurfaceOffset = "SurfaceOffset";
    }

    public static class AreaSet배치KindCodes
    {
        public const string Building = "Building";
        public const string WorkSurface = "WorkSurface";
        public const string Cultivation = "Cultivation";
        public const string RouteSurface = "RouteSurface";
        public const string Boundary = "Boundary";
        public const string BoundaryPassage = "BoundaryPassage";
        public const string Vegetation = "Vegetation";
    }

    [Serializable]
    public sealed class AreaSet지표면SnapshotData
    {
        public int SchemaVersion { get; set; } = 1;
        public string SnapshotStableId { get; set; } = string.Empty;
        public string SnapshotRevision { get; set; } = string.Empty;
        public string AreaSetStableId { get; set; } = string.Empty;
        public string CoordinateSpaceCode { get; set; } = AreaSet배치CoordinateCodes.ScenarioLocalMeters;
        public string AxisCode { get; set; } = AreaSet배치CoordinateCodes.UnityAxis;
        public float MetersPerUnit { get; set; } = 1f;
        public string SourceMeshAssetPath { get; set; } = string.Empty;
        public string SourceMeshDependencyHash { get; set; } = string.Empty;
        public int Columns { get; set; }
        public int Rows { get; set; }
        public float MinimumX { get; set; }
        public float MaximumX { get; set; }
        public float MinimumZ { get; set; }
        public float MaximumZ { get; set; }
        public float[] Heights { get; set; } = Array.Empty<float>();
        public int[] Triangles { get; set; } = Array.Empty<int>();
        public string SurfaceHashSha256 { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class AreaSet배치자산CalibrationData
    {
        public string AssetKey { get; set; } = string.Empty;
        public string H1StableId { get; set; } = string.Empty;
        public string PlacementKindCode { get; set; } = string.Empty;
        public string PrefabAssetPath { get; set; } = string.Empty;
        public string ModelAssetPath { get; set; } = string.Empty;
        public string TextureAssetPath { get; set; } = string.Empty;
        public string AssetGuid { get; set; } = string.Empty;
        public string AssetDependencyHash { get; set; } = string.Empty;
        public string AssetFingerprint { get; set; } = string.Empty;
        public float BoundsMinimumX { get; set; }
        public float BoundsMinimumY { get; set; }
        public float BoundsMinimumZ { get; set; }
        public float BoundsCenterX { get; set; }
        public float BoundsCenterY { get; set; }
        public float BoundsCenterZ { get; set; }
        public float BoundsSizeX { get; set; }
        public float BoundsSizeY { get; set; }
        public float BoundsSizeZ { get; set; }
        public float MaximumSlopeDegrees { get; set; } = 8f;
        public float MaximumHeightSpreadMeters { get; set; } = .45f;
        public bool SupportsSurfaceOffset { get; set; }
        public bool CollisionEligible { get; set; } = true;
    }

    [Serializable]
    public sealed class AreaSet배치자산CatalogData
    {
        public int SchemaVersion { get; set; } = 1;
        public string CatalogStableId { get; set; } = string.Empty;
        public string CatalogRevision { get; set; } = string.Empty;
        public AreaSet배치자산CalibrationData[] Items { get; set; } =
            Array.Empty<AreaSet배치자산CalibrationData>();
        public string CatalogHashSha256 { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class AreaSet배치CameraRecipeData
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float TargetZ { get; set; }
        public float OrthographicSize { get; set; } = 52f;
        public int PixelWidth { get; set; } = 1600;
        public int PixelHeight { get; set; } = 1000;
    }

    [Serializable]
    public sealed class AreaSet배치DraftItemData
    {
        public string PlacementStableId { get; set; } = string.Empty;
        public string H1StableId { get; set; } = string.Empty;
        public string AssetKey { get; set; } = string.Empty;
        public string AssetFingerprint { get; set; } = string.Empty;
        public float LocalX { get; set; }
        public float LocalZ { get; set; }
        public float RotationYDegrees { get; set; }
        public float UniformScale { get; set; } = 1f;
        public string HeightPolicyCode { get; set; } = AreaSet배치HeightPolicyCodes.SurfaceSnap;
        public float SurfaceOffsetMeters { get; set; }
        public string HeightExceptionReason { get; set; } = string.Empty;
        public string ClusterKey { get; set; } = string.Empty;
        public string[] AllowOverlapWithPlacementStableIds { get; set; } = Array.Empty<string>();
        public bool Enabled { get; set; } = true;
    }

    [Serializable]
    public sealed class AreaSet배치RoutePointData
    {
        public float X { get; set; }
        public float Z { get; set; }

        public AreaSet배치RoutePointData()
        {
        }

        public AreaSet배치RoutePointData(float x, float z)
        {
            X = x;
            Z = z;
        }
    }

    [Serializable]
    public sealed class AreaSet배치RouteData
    {
        public string RouteStableId { get; set; } = string.Empty;
        public float WidthMeters { get; set; } = 4f;
        public AreaSet배치RoutePointData[] Points { get; set; } = Array.Empty<AreaSet배치RoutePointData>();
    }

    [Serializable]
    public sealed class AreaSet배치DraftData
    {
        public int SchemaVersion { get; set; } = 1;
        public string DraftStableId { get; set; } = string.Empty;
        public string DraftRevision { get; set; } = string.Empty;
        public string AreaSetStableId { get; set; } = string.Empty;
        public string CoordinateSpaceCode { get; set; } = AreaSet배치CoordinateCodes.ScenarioLocalMeters;
        public string AxisCode { get; set; } = AreaSet배치CoordinateCodes.UnityAxis;
        public float MetersPerUnit { get; set; } = 1f;
        public string SurfaceSnapshotStableId { get; set; } = string.Empty;
        public string SurfaceHashSha256 { get; set; } = string.Empty;
        public string CalibrationCatalogStableId { get; set; } = string.Empty;
        public string CalibrationCatalogHashSha256 { get; set; } = string.Empty;
        public AreaSet배치CameraRecipeData Camera { get; set; } = new();
        public AreaSet배치DraftItemData[] Placements { get; set; } = Array.Empty<AreaSet배치DraftItemData>();
        public AreaSet배치RouteData[] Routes { get; set; } = Array.Empty<AreaSet배치RouteData>();
    }

    [Serializable]
    public sealed class AreaSet배치검증IssueData
    {
        public string SeverityCode { get; set; } = string.Empty;
        public string IssueCode { get; set; } = string.Empty;
        public string PlacementStableId { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class AreaSet배치ResolvedItemData
    {
        public string PlacementStableId { get; set; } = string.Empty;
        public string H1StableId { get; set; } = string.Empty;
        public string AssetKey { get; set; } = string.Empty;
        public string AssetFingerprint { get; set; } = string.Empty;
        public float LocalX { get; set; }
        public float LocalY { get; set; }
        public float LocalZ { get; set; }
        public float RotationYDegrees { get; set; }
        public float UniformScale { get; set; }
        public float SurfaceHeightMeters { get; set; }
        public float HeightSpreadMeters { get; set; }
        public float SlopeDegrees { get; set; }
    }

    [Serializable]
    public sealed class AreaSet배치검증ReceiptData
    {
        public int SchemaVersion { get; set; } = 1;
        public string DraftStableId { get; set; } = string.Empty;
        public string DraftRevision { get; set; } = string.Empty;
        public string DraftHashSha256 { get; set; } = string.Empty;
        public string SurfaceHashSha256 { get; set; } = string.Empty;
        public string CalibrationCatalogHashSha256 { get; set; } = string.Empty;
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public bool CanStage => ErrorCount == 0;
        public string StagingPrefabAssetPath { get; set; } = string.Empty;
        public string UnityStagingImagePath { get; set; } = string.Empty;
        public AreaSet배치ResolvedItemData[] ResolvedPlacements { get; set; } =
            Array.Empty<AreaSet배치ResolvedItemData>();
        public AreaSet배치검증IssueData[] Issues { get; set; } = Array.Empty<AreaSet배치검증IssueData>();
    }

    public static class AreaSet배치Json
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
        };

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings) + "\n";

        public static T Deserialize<T>(string value) where T : class =>
            JsonConvert.DeserializeObject<T>(value, Settings)
            ?? throw new InvalidOperationException("AreaSetPlacementJsonEmpty:" + typeof(T).Name);
    }

    public static class AreaSet배치Hash
    {
        public static string ComputeSurface(AreaSet지표면SnapshotData value)
        {
            var builder = new StringBuilder();
            Append(builder, value.SchemaVersion, value.SnapshotStableId, value.SnapshotRevision,
                value.AreaSetStableId, value.CoordinateSpaceCode, value.AxisCode,
                Format(value.MetersPerUnit), value.SourceMeshAssetPath,
                value.SourceMeshDependencyHash, value.Columns, value.Rows,
                Format(value.MinimumX), Format(value.MaximumX),
                Format(value.MinimumZ), Format(value.MaximumZ));
            foreach (var height in value.Heights) Append(builder, Format(height));
            foreach (var triangle in value.Triangles) Append(builder, triangle);
            return Sha256(builder.ToString());
        }

        public static string ComputeCatalog(AreaSet배치자산CatalogData value)
        {
            var builder = new StringBuilder();
            Append(builder, value.SchemaVersion, value.CatalogStableId, value.CatalogRevision);
            foreach (var item in value.Items.OrderBy(item => item.AssetKey, StringComparer.Ordinal))
                Append(builder, item.AssetKey, item.H1StableId, item.PlacementKindCode,
                    item.PrefabAssetPath, item.ModelAssetPath, item.TextureAssetPath,
                    item.AssetGuid, item.AssetDependencyHash, item.AssetFingerprint,
                    Format(item.BoundsMinimumX), Format(item.BoundsMinimumY), Format(item.BoundsMinimumZ),
                    Format(item.BoundsCenterX), Format(item.BoundsCenterY), Format(item.BoundsCenterZ),
                    Format(item.BoundsSizeX), Format(item.BoundsSizeY), Format(item.BoundsSizeZ),
                    Format(item.MaximumSlopeDegrees), Format(item.MaximumHeightSpreadMeters),
                    item.SupportsSurfaceOffset, item.CollisionEligible);
            return Sha256(builder.ToString());
        }

        public static string ComputeDraft(AreaSet배치DraftData value)
        {
            var builder = new StringBuilder();
            Append(builder, value.SchemaVersion, value.DraftStableId, value.DraftRevision,
                value.AreaSetStableId, value.CoordinateSpaceCode, value.AxisCode,
                Format(value.MetersPerUnit), value.SurfaceSnapshotStableId,
                value.SurfaceHashSha256, value.CalibrationCatalogStableId,
                value.CalibrationCatalogHashSha256);
            Append(builder, Format(value.Camera.PositionX), Format(value.Camera.PositionY),
                Format(value.Camera.PositionZ), Format(value.Camera.TargetX),
                Format(value.Camera.TargetY), Format(value.Camera.TargetZ),
                Format(value.Camera.OrthographicSize), value.Camera.PixelWidth, value.Camera.PixelHeight);
            foreach (var placement in value.Placements.OrderBy(item => item.PlacementStableId, StringComparer.Ordinal))
            {
                Append(builder, placement.PlacementStableId, placement.H1StableId,
                    placement.AssetKey, placement.AssetFingerprint,
                    Format(placement.LocalX), Format(placement.LocalZ),
                    Format(placement.RotationYDegrees), Format(placement.UniformScale),
                    placement.HeightPolicyCode, Format(placement.SurfaceOffsetMeters),
                    placement.HeightExceptionReason, placement.ClusterKey, placement.Enabled);
                foreach (var allowed in placement.AllowOverlapWithPlacementStableIds.OrderBy(x => x, StringComparer.Ordinal))
                    Append(builder, allowed);
            }
            foreach (var route in value.Routes.OrderBy(item => item.RouteStableId, StringComparer.Ordinal))
            {
                Append(builder, route.RouteStableId, Format(route.WidthMeters));
                foreach (var point in route.Points) Append(builder, Format(point.X), Format(point.Z));
            }
            return Sha256(builder.ToString());
        }

        public static string ComputeCalibrationFingerprint(AreaSet배치자산CalibrationData value)
        {
            var builder = new StringBuilder();
            Append(builder, value.AssetKey, value.PrefabAssetPath, value.ModelAssetPath,
                value.AssetGuid, value.AssetDependencyHash,
                Format(value.BoundsMinimumX), Format(value.BoundsMinimumY), Format(value.BoundsMinimumZ),
                Format(value.BoundsCenterX), Format(value.BoundsCenterY), Format(value.BoundsCenterZ),
                Format(value.BoundsSizeX), Format(value.BoundsSizeY), Format(value.BoundsSizeZ));
            return Sha256(builder.ToString());
        }

        private static void Append(StringBuilder builder, params object[] values)
        {
            foreach (var value in values) builder.Append(value).Append('\u001f');
            builder.Append('\u001e');
        }

        private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(x => x.ToString("X2")));
        }
    }

    public sealed class AreaSet지표면Sampler
    {
        private readonly AreaSet지표면SnapshotData _surface;
        private readonly float _stepX;
        private readonly float _stepZ;

        public AreaSet지표면Sampler(AreaSet지표면SnapshotData surface)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            if (surface.Columns < 2 || surface.Rows < 2
                || surface.Heights.Length != surface.Columns * surface.Rows)
                throw new InvalidOperationException("AreaSetSurfaceGridInvalid");
            _stepX = (surface.MaximumX - surface.MinimumX) / (surface.Columns - 1);
            _stepZ = (surface.MaximumZ - surface.MinimumZ) / (surface.Rows - 1);
            if (_stepX <= 0f || _stepZ <= 0f)
                throw new InvalidOperationException("AreaSetSurfaceGridStepInvalid");
        }

        public bool TrySample(float x, float z, out float height)
        {
            height = 0f;
            if (x < _surface.MinimumX || x > _surface.MaximumX
                || z < _surface.MinimumZ || z > _surface.MaximumZ)
                return false;

            var xPosition = (x - _surface.MinimumX) / _stepX;
            var zPosition = (z - _surface.MinimumZ) / _stepZ;
            var xIndex = Math.Min((int)Math.Floor(xPosition), _surface.Columns - 2);
            var zIndex = Math.Min((int)Math.Floor(zPosition), _surface.Rows - 2);
            var u = Clamp01(xPosition - xIndex);
            var v = Clamp01(zPosition - zIndex);
            var a = Height(xIndex, zIndex);
            var b = Height(xIndex + 1, zIndex);
            var c = Height(xIndex, zIndex + 1);
            var d = Height(xIndex + 1, zIndex + 1);
            height = u + v <= 1f
                ? a + u * (b - a) + v * (c - a)
                : d + (1f - u) * (c - d) + (1f - v) * (b - d);
            return true;
        }

        private float Height(int x, int z) => _surface.Heights[z * _surface.Columns + x];

        private static float Clamp01(float value)
            => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    public static class AreaSet배치DraftValidator
    {
        public static AreaSet배치검증ReceiptData Validate(
            AreaSet배치DraftData draft,
            AreaSet지표면SnapshotData surface,
            AreaSet배치자산CatalogData catalog)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var issues = new List<AreaSet배치검증IssueData>();
            var resolved = new List<AreaSet배치ResolvedItemData>();
            var surfaceHash = AreaSet배치Hash.ComputeSurface(surface);
            var catalogHash = AreaSet배치Hash.ComputeCatalog(catalog);
            Require(string.Equals(surface.SurfaceHashSha256, surfaceHash, StringComparison.OrdinalIgnoreCase),
                issues, "SurfaceSnapshotSelfHashMismatch", string.Empty, surface.SurfaceHashSha256);
            Require(string.Equals(catalog.CatalogHashSha256, catalogHash, StringComparison.OrdinalIgnoreCase),
                issues, "CalibrationCatalogSelfHashMismatch", string.Empty, catalog.CatalogHashSha256);
            Require(draft.CoordinateSpaceCode == AreaSet배치CoordinateCodes.ScenarioLocalMeters
                    && draft.AxisCode == AreaSet배치CoordinateCodes.UnityAxis
                    && Math.Abs(draft.MetersPerUnit - 1f) <= .0001f,
                issues, "CoordinateContractMismatch", string.Empty,
                $"{draft.CoordinateSpaceCode}/{draft.AxisCode}/{draft.MetersPerUnit}");
            Require(draft.AreaSetStableId == surface.AreaSetStableId,
                issues, "AreaSetReferenceMismatch", string.Empty, draft.AreaSetStableId);
            Require(draft.SurfaceSnapshotStableId == surface.SnapshotStableId
                    && string.Equals(draft.SurfaceHashSha256, surfaceHash, StringComparison.OrdinalIgnoreCase),
                issues, "SurfaceSnapshotStale", string.Empty, draft.SurfaceHashSha256);
            Require(draft.CalibrationCatalogStableId == catalog.CatalogStableId
                    && string.Equals(draft.CalibrationCatalogHashSha256, catalogHash, StringComparison.OrdinalIgnoreCase),
                issues, "CalibrationCatalogStale", string.Empty, draft.CalibrationCatalogHashSha256);

            var assets = catalog.Items.ToDictionary(item => item.AssetKey, StringComparer.Ordinal);
            var sampler = new AreaSet지표면Sampler(surface);
            var footprints = new List<Footprint>();
            foreach (var placement in draft.Placements.Where(item => item.Enabled))
            {
                if (string.IsNullOrWhiteSpace(placement.PlacementStableId)
                    || string.IsNullOrWhiteSpace(placement.H1StableId))
                {
                    Error(issues, "PlacementIdentityMissing", placement.PlacementStableId, placement.H1StableId);
                    continue;
                }
                if (!assets.TryGetValue(placement.AssetKey, out var asset))
                {
                    Error(issues, "PlacementAssetMissing", placement.PlacementStableId, placement.AssetKey);
                    continue;
                }
                if (!string.Equals(placement.AssetFingerprint, asset.AssetFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    Error(issues, "PlacementAssetFingerprintStale", placement.PlacementStableId,
                        $"draft={placement.AssetFingerprint};current={asset.AssetFingerprint}");
                    continue;
                }
                if (float.IsNaN(placement.UniformScale)
                    || float.IsInfinity(placement.UniformScale)
                    || placement.UniformScale <= 0f)
                {
                    Error(issues, "PlacementScaleInvalid", placement.PlacementStableId,
                        placement.UniformScale.ToString(CultureInfo.InvariantCulture));
                    continue;
                }
                if (placement.HeightPolicyCode != AreaSet배치HeightPolicyCodes.SurfaceSnap
                    && placement.HeightPolicyCode != AreaSet배치HeightPolicyCodes.SurfaceOffset)
                {
                    Error(issues, "HeightPolicyInvalid", placement.PlacementStableId, placement.HeightPolicyCode);
                    continue;
                }
                if (placement.HeightPolicyCode == AreaSet배치HeightPolicyCodes.SurfaceOffset
                    && (!asset.SupportsSurfaceOffset
                        || string.IsNullOrWhiteSpace(placement.HeightExceptionReason)
                        || Math.Abs(placement.SurfaceOffsetMeters) > 2f))
                {
                    Error(issues, "SurfaceOffsetNotApproved", placement.PlacementStableId,
                        $"offset={placement.SurfaceOffsetMeters};reason={placement.HeightExceptionReason}");
                    continue;
                }

                var footprint = Footprint.Create(placement, asset);
                var samples = footprint.SamplePoints().ToArray();
                var heights = new List<float>();
                var outside = false;
                foreach (var point in samples)
                {
                    if (!sampler.TrySample(point.X, point.Z, out var value))
                    {
                        outside = true;
                        break;
                    }
                    heights.Add(value);
                }
                if (outside || !sampler.TrySample(placement.LocalX, placement.LocalZ, out var centerHeight))
                {
                    Error(issues, "PlacementOutsideSurface", placement.PlacementStableId,
                        $"x={placement.LocalX};z={placement.LocalZ}");
                    continue;
                }
                var spread = heights.Max() - heights.Min();
                if (spread > asset.MaximumHeightSpreadMeters)
                    Error(issues, "PlacementHeightSpreadExceeded", placement.PlacementStableId,
                        $"spread={spread:0.###};max={asset.MaximumHeightSpreadMeters:0.###}");
                var slope = ResolveSlope(sampler, placement.LocalX, placement.LocalZ);
                if (slope > asset.MaximumSlopeDegrees)
                    Error(issues, "PlacementSlopeExceeded", placement.PlacementStableId,
                        $"slope={slope:0.###};max={asset.MaximumSlopeDegrees:0.###}");

                var offset = placement.HeightPolicyCode == AreaSet배치HeightPolicyCodes.SurfaceOffset
                    ? placement.SurfaceOffsetMeters : 0f;
                resolved.Add(new AreaSet배치ResolvedItemData
                {
                    PlacementStableId = placement.PlacementStableId,
                    H1StableId = placement.H1StableId,
                    AssetKey = placement.AssetKey,
                    AssetFingerprint = placement.AssetFingerprint,
                    LocalX = placement.LocalX,
                    LocalY = centerHeight - asset.BoundsMinimumY * placement.UniformScale + offset,
                    LocalZ = placement.LocalZ,
                    RotationYDegrees = placement.RotationYDegrees,
                    UniformScale = placement.UniformScale,
                    SurfaceHeightMeters = centerHeight,
                    HeightSpreadMeters = spread,
                    SlopeDegrees = slope,
                });
                if (asset.CollisionEligible) footprints.Add(footprint);
            }

            ValidateOverlaps(footprints, issues);
            ValidateRouteClearance(draft.Routes, footprints, issues);
            return new AreaSet배치검증ReceiptData
            {
                DraftStableId = draft.DraftStableId,
                DraftRevision = draft.DraftRevision,
                DraftHashSha256 = AreaSet배치Hash.ComputeDraft(draft),
                SurfaceHashSha256 = surfaceHash,
                CalibrationCatalogHashSha256 = catalogHash,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ErrorCount = issues.Count(item => item.SeverityCode == "Error"),
                WarningCount = issues.Count(item => item.SeverityCode == "Warning"),
                ResolvedPlacements = resolved.ToArray(),
                Issues = issues.ToArray(),
            };
        }

        private static float ResolveSlope(AreaSet지표면Sampler sampler, float x, float z)
        {
            const float distance = .25f;
            if (!sampler.TrySample(x - distance, z, out var left)
                || !sampler.TrySample(x + distance, z, out var right)
                || !sampler.TrySample(x, z - distance, out var back)
                || !sampler.TrySample(x, z + distance, out var front)) return 90f;
            var dx = (right - left) / (distance * 2f);
            var dz = (front - back) / (distance * 2f);
            return (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180d / Math.PI);
        }

        private static void ValidateOverlaps(
            IReadOnlyList<Footprint> footprints,
            ICollection<AreaSet배치검증IssueData> issues)
        {
            for (var leftIndex = 0; leftIndex < footprints.Count; leftIndex++)
            for (var rightIndex = leftIndex + 1; rightIndex < footprints.Count; rightIndex++)
            {
                var left = footprints[leftIndex];
                var right = footprints[rightIndex];
                if (!string.IsNullOrWhiteSpace(left.ClusterKey) && left.ClusterKey == right.ClusterKey) continue;
                if (left.Allows(right.StableId) || right.Allows(left.StableId)) continue;
                if (left.MaximumX <= right.MinimumX + .02f || right.MaximumX <= left.MinimumX + .02f
                    || left.MaximumZ <= right.MinimumZ + .02f || right.MaximumZ <= left.MinimumZ + .02f) continue;
                Error(issues, "PlacementFootprintOverlap", left.StableId, right.StableId);
            }
        }

        private static void ValidateRouteClearance(
            IEnumerable<AreaSet배치RouteData> routes,
            IEnumerable<Footprint> footprints,
            ICollection<AreaSet배치검증IssueData> issues)
        {
            foreach (var route in routes)
            {
                if (route.Points.Length < 2 || route.WidthMeters <= 0f)
                {
                    Error(issues, "RouteContractInvalid", string.Empty, route.RouteStableId);
                    continue;
                }
                foreach (var footprint in footprints.Where(item =>
                             item.KindCode != AreaSet배치KindCodes.RouteSurface
                             && item.KindCode != AreaSet배치KindCodes.BoundaryPassage
                             && item.KindCode != AreaSet배치KindCodes.Vegetation))
                {
                    for (var i = 0; i < route.Points.Length - 1; i++)
                    {
                        var distance = DistanceToSegment(footprint.CenterX, footprint.CenterZ,
                            route.Points[i], route.Points[i + 1]);
                        if (distance >= route.WidthMeters * .5f + footprint.Radius) continue;
                        Error(issues, "RouteClearanceBlocked", footprint.StableId, route.RouteStableId);
                        break;
                    }
                }
            }
        }

        private static float DistanceToSegment(float x, float z,
            AreaSet배치RoutePointData start, AreaSet배치RoutePointData end)
        {
            var dx = end.X - start.X;
            var dz = end.Z - start.Z;
            var lengthSquared = dx * dx + dz * dz;
            if (lengthSquared <= .000001f)
                return (float)Math.Sqrt((x - start.X) * (x - start.X) + (z - start.Z) * (z - start.Z));
            var raw = ((x - start.X) * dx + (z - start.Z) * dz) / lengthSquared;
            var t = raw < 0f ? 0f : raw > 1f ? 1f : raw;
            var px = start.X + t * dx;
            var pz = start.Z + t * dz;
            return (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
        }

        private static void Require(bool condition, ICollection<AreaSet배치검증IssueData> issues,
            string code, string placement, string detail)
        {
            if (!condition) Error(issues, code, placement, detail);
        }

        private static void Error(ICollection<AreaSet배치검증IssueData> issues,
            string code, string placement, string detail) => issues.Add(new AreaSet배치검증IssueData
        {
            SeverityCode = "Error",
            IssueCode = code,
            PlacementStableId = placement,
            Detail = detail,
        });

        private sealed class Footprint
        {
            public string StableId { get; private set; } = string.Empty;
            public string KindCode { get; private set; } = string.Empty;
            public string ClusterKey { get; private set; } = string.Empty;
            public string[] Allowed { get; private set; } = Array.Empty<string>();
            public float CenterX { get; private set; }
            public float CenterZ { get; private set; }
            public float MinimumX { get; private set; }
            public float MaximumX { get; private set; }
            public float MinimumZ { get; private set; }
            public float MaximumZ { get; private set; }
            public float Radius { get; private set; }
            private (float X, float Z)[] Corners { get; set; } = Array.Empty<(float, float)>();

            public static Footprint Create(AreaSet배치DraftItemData placement,
                AreaSet배치자산CalibrationData asset)
            {
                var halfX = asset.BoundsSizeX * placement.UniformScale * .5f;
                var halfZ = asset.BoundsSizeZ * placement.UniformScale * .5f;
                var angle = placement.RotationYDegrees * Math.PI / 180d;
                var cosine = (float)Math.Cos(angle);
                var sine = (float)Math.Sin(angle);
                var corners = new[] { (-halfX, -halfZ), (halfX, -halfZ), (-halfX, halfZ), (halfX, halfZ) }
                    .Select(point =>
                    {
                        var x = point.Item1 * cosine - point.Item2 * sine + placement.LocalX;
                        var z = point.Item1 * sine + point.Item2 * cosine + placement.LocalZ;
                        return (X: x, Z: z);
                    }).ToArray();
                return new Footprint
                {
                    StableId = placement.PlacementStableId,
                    KindCode = asset.PlacementKindCode,
                    ClusterKey = placement.ClusterKey,
                    Allowed = placement.AllowOverlapWithPlacementStableIds,
                    CenterX = placement.LocalX,
                    CenterZ = placement.LocalZ,
                    MinimumX = corners.Min(point => point.X),
                    MaximumX = corners.Max(point => point.X),
                    MinimumZ = corners.Min(point => point.Z),
                    MaximumZ = corners.Max(point => point.Z),
                    Radius = (float)Math.Sqrt(halfX * halfX + halfZ * halfZ),
                    Corners = corners,
                };
            }

            public IEnumerable<(float X, float Z)> SamplePoints()
            {
                yield return (CenterX, CenterZ);
                foreach (var corner in Corners) yield return corner;
            }

            public bool Allows(string stableId) => Allowed.Contains(stableId, StringComparer.Ordinal);
        }
    }
}
