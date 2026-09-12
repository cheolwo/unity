using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// LH가 준비한 셀·지면 문맥을 읽어 실외 배치 인계 자료를 만드는 별도 경계다.
    /// </summary>
    public interface I공간실외자산배치PlanProvider
    {
        공간LHPlacementData[] Plan(string worldSeed, string cellKey,
            int cellX, int cellY);
    }

    public sealed class 결정적공간실외자산배치PlanProvider
        : I공간실외자산배치PlanProvider
    {
        private static readonly string[] CompositionKeys =
        {
            "farm:감자밭 두렁:A", "farm:감자밭 두렁:B", "farm:혼합 작물밭:A",
            "nature:초지·야생화:A", "nature:숲 가장자리:A", "nature:침엽수림 군집:A",
        };

        private readonly I공간동적표면Provider surfaceProvider;
        private readonly 공간동적배치PolicyData placementPolicy;

        public 결정적공간실외자산배치PlanProvider(
            I공간동적표면Provider surface,
            공간동적배치PolicyData policy)
        {
            surfaceProvider = surface ?? throw new ArgumentNullException(nameof(surface));
            placementPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            placementPolicy.Validate();
        }

        public 공간LHPlacementData[] Plan(string worldSeed, string cellKey,
            int cellX, int cellY)
        {
            var values = new List<공간LHPlacementData>();
            AddFixedAnchor(values, cellKey, cellX, cellY,
                공간LHWorldCodes.CenterL3X, 공간LHWorldCodes.CenterL3Y,
                "farmhouse", "farm:헛간 작업마당:A",
                "h1-stock:farm-work-yard", -18d, 12d, 0d);
            AddFixedAnchor(values, cellKey, cellX, cellY,
                공간LHWorldCodes.CenterL3X + 1, 공간LHWorldCodes.CenterL3Y,
                "potato-field", "farm:감자밭 두렁:A",
                "h1-stock:farm-production", 4d, -6d, 0d);
            AddFixedAnchor(values, cellKey, cellX, cellY,
                공간LHWorldCodes.CenterL3X, 공간LHWorldCodes.CenterL3Y + 1,
                "work-yard", "farm:농산물 집하·직판장:A",
                "h1-stock:farm-work-yard", 10d, 8d, 90d);
            AddFixedAnchor(values, cellKey, cellX, cellY,
                공간LHWorldCodes.CenterL3X + 1,
                공간LHWorldCodes.CenterL3Y + 1,
                "farm-gate", "network:농촌도로 T자:A",
                "h1-stock:farm-loading-gate", -8d, -10d, 0d);

            var randomBytes = HashBytes(string.Join("|", new[]
            {
                worldSeed, 공간LHWorldCodes.GeneratorVersion, "L3Surface", cellKey,
            }));
            var count = 3 + randomBytes[0] % 3;
            var fixedPoints = values.Select(value => new 공간동적배치점Data
            {
                LocalXMeters = value.LocalXMeters,
                LocalZMeters = value.LocalZMeters,
            }).ToArray();
            var placementPlan = 공간동적ObjectPlacementPlanner.Plan(
                worldSeed, cellX, cellY, count, placementPolicy,
                surfaceProvider, fixedPoints);
            if (!placementPlan.IsComplete)
                throw new InvalidOperationException(
                    "ExteriorDynamicPlacementIncomplete:" + cellKey + ":"
                    + string.Join(",", placementPlan.거부원인Codes));
            foreach (var point in placementPlan.배치점)
            {
                var key = CompositionKeys[point.VariantIndex % CompositionKeys.Length];
                values.Add(new 공간LHPlacementData
                {
                    GeneratedStableId = "lh-object:"
                        + point.배치StableId.Substring("dynamic-object:".Length),
                    OwnerCellKey = cellKey,
                    LayerCode = "L3Surface",
                    CompositionKey = key,
                    H1StableId = key.StartsWith("farm:", StringComparison.Ordinal)
                        ? "h1-stock:farm-production" : string.Empty,
                    EvidenceKindCode = "ScenarioProcedural",
                    LocalXMeters = point.LocalXMeters,
                    LocalYMeters = point.LocalYMeters,
                    LocalZMeters = point.LocalZMeters,
                    RotationDegrees = point.RotationDegrees,
                    UniformScale = point.UniformScale,
                    PresentationOnly = true,
                    SurfaceSlopeDegrees = point.SurfaceSlopeDegrees,
                    PlacementRuleRevision = 공간동적ObjectPlacementPlanner.RuleRevision,
                    SurfaceRuleRevision = surfaceProvider.SurfaceRuleRevision,
                    PlacementValidationCode = point.ValidationCode,
                });
            }
            return values.OrderBy(value => value.GeneratedStableId,
                StringComparer.Ordinal).ToArray();
        }

        private void AddFixedAnchor(ICollection<공간LHPlacementData> values,
            string cellKey, int x, int y, int expectedX, int expectedY,
            string slot, string compositionKey, string h1StableId,
            double localX, double localZ, double rotation)
        {
            if (x != expectedX || y != expectedY) return;
            var worldX = x * placementPolicy.셀크기Meters + localX;
            var worldZ = y * placementPolicy.셀크기Meters + localZ;
            if (!surfaceProvider.TrySample(worldX, worldZ, out var surface)
                || surface == null || !surface.배치허용)
                throw new InvalidOperationException(
                    "ExteriorFixedAnchorSurfaceUnavailable:" + slot);
            if (surface.표면경사Degrees > placementPolicy.최대표면경사Degrees)
                throw new InvalidOperationException(
                    "ExteriorFixedAnchorSlopeTooSteep:" + slot);
            var identity = Hash(cellKey + "|H3Intent|" + slot);
            values.Add(new 공간LHPlacementData
            {
                GeneratedStableId = "lh-anchor:" + identity.Substring(0, 24),
                OwnerCellKey = cellKey,
                LayerCode = "H3Intent",
                CompositionKey = compositionKey,
                H1StableId = h1StableId,
                EvidenceKindCode = "ScenarioProcedural",
                LocalXMeters = localX,
                LocalYMeters = surface.표면높이Meters,
                LocalZMeters = localZ,
                RotationDegrees = rotation,
                UniformScale = 1d,
                FixedAnchor = true,
                PresentationOnly = true,
                SurfaceSlopeDegrees = surface.표면경사Degrees,
                PlacementRuleRevision = 공간동적ObjectPlacementPlanner.RuleRevision,
                SurfaceRuleRevision = surfaceProvider.SurfaceRuleRevision,
                PlacementValidationCode = "Passed",
            });
        }

        private static byte[] HashBytes(string value)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static string Hash(string value)
            => BitConverter.ToString(HashBytes(value)).Replace("-", string.Empty)
                .ToLowerInvariant();
    }
}
