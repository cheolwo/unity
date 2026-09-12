using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 사가정Synty표현상태Codes
    {
        public const string Ready = "Ready";
        public const string CatalogMissing = "CatalogMissing";
        public const string FootprintRejected = "FootprintRejected";
        public const string HeightRejected = "HeightRejected";
        public const string PrefabBoundsRejected = "PrefabBoundsRejected";
    }

    public sealed class 사가정Synty표현Placement
    {
        public string SourceBuildingId { get; set; } = string.Empty;
        public string PlacementStableId { get; set; } = string.Empty;
        public string VisualKey { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public GameObject Prefab { get; set; } = null!;
        public Vector3 LocalPosition { get; set; }
        public Bounds PrefabLocalBounds { get; set; }
        public float RotationY { get; set; }
        public float UniformScale { get; set; }
        public int VerticalCopies { get; set; }
        public float TargetHeight { get; set; }
        public float ActualHeight { get; set; }
        public bool Ready => StatusCode == 사가정Synty표현상태Codes.Ready;
    }

    public sealed class 사가정Synty표현Plan
    {
        private readonly HashSet<string> replacedBuildingIds;

        public 사가정Synty표현Plan(
            string revision,
            IReadOnlyList<사가정Synty표현Placement> placements,
            IReadOnlyList<사가정Synty표현Placement> rejected)
        {
            Revision = revision;
            Placements = placements ?? Array.Empty<사가정Synty표현Placement>();
            Rejected = rejected ?? Array.Empty<사가정Synty표현Placement>();
            replacedBuildingIds = new HashSet<string>(
                Placements.Where(value => value.Ready).Select(value => value.SourceBuildingId),
                StringComparer.Ordinal);
        }

        public string Revision { get; }
        public IReadOnlyList<사가정Synty표현Placement> Placements { get; }
        public IReadOnlyList<사가정Synty표현Placement> Rejected { get; }
        public int CandidateCount => Placements.Count + Rejected.Count;
        public int AcceptedCount => Placements.Count(value => value.Ready);
        public int FallbackCount => Rejected.Count;
        public IReadOnlyCollection<string> ReplacedBuildingIds => replacedBuildingIds;
        public bool Replaces(string buildingId) => replacedBuildingIds.Contains(buildingId);

        public static 사가정Synty표현Plan Empty(string revision = "sagajeong-synty-visual.none")
            => new(revision, Array.Empty<사가정Synty표현Placement>(),
                Array.Empty<사가정Synty표현Placement>());
    }

    /// <summary>
    /// 동결된 사가정 건물 자료에서 표현 후보만 선택합니다. 위치·높이·업무 상태를 확정하지 않고,
    /// 기존 법정동 VisualKey 연결기를 통해 Synty Prefab을 읽기 전용 표현으로 해석합니다.
    /// </summary>
    public static class 사가정Synty표현Planner
    {
        public const string Revision = "sagajeong-synty-visual.r1";
        public const float MinimumUniformScale = .8f;
        public const float MaximumUniformScale = 1.25f;
        public const float MaximumHeightErrorRatio = .10f;

        public static 사가정Synty표현Plan Build(
            공간참고자료 data,
            건물높이보완조회 heights,
            법정동경관VisualCatalog catalog,
            float radiusMeters,
            int maximumCandidates)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (catalog == null) return 사가정Synty표현Plan.Empty();
            if (!float.IsFinite(radiusMeters) || radiusMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));

            catalog.Validate();
            var connector = new 법정동Synty후단연결기();
            var candidates = SelectCandidates(data, heights, radiusMeters, maximumCandidates);
            var accepted = new List<사가정Synty표현Placement>();
            var rejected = new List<사가정Synty표현Placement>();

            foreach (var candidate in candidates)
            {
                var visualKey = ResolveVisualKey(candidate.Building, candidate.TargetHeight);
                var source = new 법정동경관PlacementData
                {
                    PlacementStableId = "sagajeong:synty:" + candidate.Building.id,
                    RegionStableId = "region:kr:sagajeong-reference",
                    VisualKey = visualKey,
                    LandCoverCode = 법정동LandCoverCodes.Residential,
                    RegionRoleCode = 법정동WorldRoleCodes.Town,
                    EvidenceCode = 법정동WorldEvidenceCodes.SimulationScenario,
                    LocalX = candidate.Center.x,
                    LocalZ = candidate.Center.y,
                    RotationY = candidate.RotationY,
                    Scale = 1f,
                    DensityTier = 1,
                    LodGroup = 1,
                    PresentationOnly = true,
                };
                var linked = connector.연결(source, catalog, 0f);
                if (!linked.연결가능여부)
                {
                    rejected.Add(Rejected(candidate, source, linked.StatusCode));
                    continue;
                }

                if (!TryCalculatePrefabLocalBounds(linked.Prefab, out var prefabBounds))
                {
                    rejected.Add(Rejected(candidate, source,
                        사가정Synty표현상태Codes.PrefabBoundsRejected));
                    continue;
                }

                var fit = Fit(candidate, source, linked.Prefab, prefabBounds);
                if (!fit.Ready) rejected.Add(fit); else accepted.Add(fit);
            }

            return new 사가정Synty표현Plan(Revision, accepted, rejected);
        }

        private static IReadOnlyList<Candidate> SelectCandidates(
            공간참고자료 data,
            건물높이보완조회 heights,
            float radius,
            int maximum)
        {
            var all = data.buildings.Select(building => Candidate.Create(data, building, heights))
                .Where(value => value.Distance <= radius)
                .ToArray();

            // 역세권 첫 관문은 높이 근거 7동과 가장 가까운 commercial/civic 6동을 고정한다.
            if (radius <= 150.001f && maximum == 13)
            {
                var selected = all.Where(value => value.HasHeightEvidence)
                    .OrderBy(value => value.Distance)
                    .ThenBy(value => value.Building.id, StringComparer.Ordinal)
                    .ToList();
                selected.AddRange(all.Where(value => !selected.Contains(value)
                        && IsPilotLowRise(value.Building.buildingKind))
                    .OrderBy(value => value.Distance)
                    .ThenBy(value => value.Building.id, StringComparer.Ordinal)
                    .Take(Math.Max(0, maximum - selected.Count)));
                return selected.Take(maximum).ToArray();
            }

            return all.OrderByDescending(value => value.HasHeightEvidence)
                .ThenByDescending(value => IsTypedBuilding(value.Building.buildingKind))
                .ThenBy(value => value.MajorRoadDistance)
                .ThenByDescending(value => value.FootprintArea)
                .ThenBy(value => value.Building.id, StringComparer.Ordinal)
                .Take(maximum)
                .ToArray();
        }

        private static 사가정Synty표현Placement Fit(
            Candidate candidate,
            법정동경관PlacementData source,
            GameObject prefab,
            Bounds prefabBounds)
        {
            var sourceWidth = Mathf.Max(.01f, prefabBounds.size.x);
            var sourceDepth = Mathf.Max(.01f, prefabBounds.size.z);
            var sourceHeight = Mathf.Max(.01f, prefabBounds.size.y);
            var targetWidth = Mathf.Max(.01f, candidate.Size.x - 1f);
            var targetDepth = Mathf.Max(.01f, candidate.Size.y - 1f);

            var scaleA = Mathf.Min(targetWidth / sourceWidth, targetDepth / sourceDepth);
            var scaleB = Mathf.Min(targetWidth / sourceDepth, targetDepth / sourceWidth);
            var rotate = scaleB > scaleA;
            var footprintScale = Mathf.Max(scaleA, scaleB);
            if (footprintScale < MinimumUniformScale)
                return Rejected(candidate, source, 사가정Synty표현상태Codes.FootprintRejected);

            var maximumScale = Mathf.Min(footprintScale, MaximumUniformScale);
            if (maximumScale < MinimumUniformScale)
                return Rejected(candidate, source, 사가정Synty표현상태Codes.FootprintRejected);

            var copies = 1;
            var scale = MinimumUniformScale;
            var bestError = float.PositiveInfinity;
            var maximumCopies = Mathf.Clamp(
                Mathf.CeilToInt(candidate.TargetHeight / (sourceHeight * MinimumUniformScale)) + 1,
                1, 24);
            for (var candidateCopies = 1; candidateCopies <= maximumCopies; candidateCopies++)
            {
                var candidateScale = Mathf.Clamp(
                    candidate.TargetHeight / (sourceHeight * candidateCopies),
                    MinimumUniformScale, maximumScale);
                var candidateHeight = sourceHeight * candidateScale * candidateCopies;
                var candidateError = Mathf.Abs(candidateHeight - candidate.TargetHeight);
                if (candidateError >= bestError) continue;
                copies = candidateCopies;
                scale = candidateScale;
                bestError = candidateError;
            }

            var actualHeight = sourceHeight * scale * copies;
            var heightError = Mathf.Abs(actualHeight - candidate.TargetHeight)
                              / Mathf.Max(1f, candidate.TargetHeight);
            if (heightError > MaximumHeightErrorRatio)
                return Rejected(candidate, source, 사가정Synty표현상태Codes.HeightRejected);

            return new 사가정Synty표현Placement
            {
                SourceBuildingId = candidate.Building.id,
                PlacementStableId = source.PlacementStableId,
                VisualKey = source.VisualKey,
                StatusCode = 사가정Synty표현상태Codes.Ready,
                Prefab = prefab,
                LocalPosition = new Vector3(candidate.Center.x, .66f, candidate.Center.y),
                PrefabLocalBounds = prefabBounds,
                RotationY = candidate.RotationY + (rotate ? 90f : 0f),
                UniformScale = scale,
                VerticalCopies = copies,
                TargetHeight = candidate.TargetHeight,
                ActualHeight = actualHeight,
            };
        }

        private static 사가정Synty표현Placement Rejected(
            Candidate candidate,
            법정동경관PlacementData source,
            string status)
            => new()
            {
                SourceBuildingId = candidate.Building.id,
                PlacementStableId = source.PlacementStableId,
                VisualKey = source.VisualKey,
                StatusCode = status,
                LocalPosition = new Vector3(candidate.Center.x, .66f, candidate.Center.y),
                RotationY = candidate.RotationY,
                TargetHeight = candidate.TargetHeight,
            };

        private static bool TryCalculatePrefabLocalBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            if (prefab == null) return false;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(value => value.sharedMesh != null).ToArray();
            if (filters.Length == 0) return false;

            var rootInverse = prefab.transform.worldToLocalMatrix;
            var hasPoint = false;
            foreach (var filter in filters)
            {
                var matrix = rootInverse * filter.transform.localToWorldMatrix;
                var source = filter.sharedMesh.bounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var point = matrix.MultiplyPoint3x4(source.center
                        + Vector3.Scale(source.extents, new Vector3(x, y, z)));
                    if (!hasPoint) { bounds = new Bounds(point, Vector3.zero); hasPoint = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return hasPoint && bounds.size.x > 0f && bounds.size.y > 0f && bounds.size.z > 0f;
        }

        private static string ResolveVisualKey(참고건물 building, float height)
        {
            if (building.buildingKind == "commercial") return 법정동경관VisualKeys.CityShop;
            if (building.buildingKind is "civic" or "school" or "hospital" or "kindergarten" or "church")
                return 법정동경관VisualKeys.CityCivic;
            if (building.buildingKind == "apartments") return 법정동경관VisualKeys.CityApartment;
            return height >= 15f ? 법정동경관VisualKeys.CityOffice : 법정동경관VisualKeys.CityShop;
        }

        private static bool IsPilotLowRise(string kind) => kind is "commercial" or "civic";
        private static bool IsTypedBuilding(string kind)
            => kind is "commercial" or "civic" or "apartments" or "school"
                or "hospital" or "kindergarten" or "church";

        private sealed class Candidate
        {
            public 참고건물 Building { get; set; } = null!;
            public Vector2 Center { get; set; }
            public Vector2 Size { get; set; }
            public float RotationY { get; set; }
            public float Distance { get; set; }
            public float MajorRoadDistance { get; set; }
            public float FootprintArea { get; set; }
            public float TargetHeight { get; set; }
            public bool HasHeightEvidence { get; set; }

            public static Candidate Create(공간참고자료 data, 참고건물 building,
                건물높이보완조회 heights)
            {
                var points = building.points.Take(Math.Max(0, building.points.Length - 1)).ToArray();
                var minX = points.Min(value => value.x);
                var maxX = points.Max(value => value.x);
                var minZ = points.Min(value => value.z);
                var maxZ = points.Max(value => value.z);
                var center = new Vector2(points.Average(value => value.x), points.Average(value => value.z));
                var longest = Vector2.right;
                var longestLength = 0f;
                for (var index = 1; index < building.points.Length; index++)
                {
                    var edge = new Vector2(building.points[index].x - building.points[index - 1].x,
                        building.points[index].z - building.points[index - 1].z);
                    if (edge.sqrMagnitude <= longestLength) continue;
                    longest = edge;
                    longestLength = edge.sqrMagnitude;
                }
                var targetHeight = heights?.Resolve(building) ?? building.height;
                var evidence = building.heightKind == "OsmHeightTag"
                               || Mathf.Abs(targetHeight - building.height) > .001f;
                return new Candidate
                {
                    Building = building,
                    Center = center,
                    Size = new Vector2(maxX - minX, maxZ - minZ),
                    RotationY = Mathf.Atan2(longest.x, longest.y) * Mathf.Rad2Deg,
                    Distance = Vector2.Distance(center, new Vector2(data.offsetX, data.offsetZ)),
                    MajorRoadDistance = ComputeMajorRoadDistance(data.roads, center),
                    FootprintArea = Mathf.Abs(PolygonArea(points)),
                    TargetHeight = targetHeight,
                    HasHeightEvidence = evidence,
                };
            }

            private static float ComputeMajorRoadDistance(IEnumerable<참고도로> roads, Vector2 point)
            {
                var result = float.PositiveInfinity;
                foreach (var road in roads)
                {
                    if (road.kind is not ("primary" or "secondary" or "secondary_link" or "tertiary"))
                        continue;
                    var a = new Vector2(road.x1, road.z1);
                    var b = new Vector2(road.x2, road.z2);
                    var direction = b - a;
                    var denominator = direction.sqrMagnitude;
                    var t = denominator <= .0001f ? 0f
                        : Mathf.Clamp01(Vector2.Dot(point - a, direction) / denominator);
                    result = Mathf.Min(result, Vector2.Distance(point, a + direction * t));
                }
                return result;
            }

            private static float PolygonArea(IReadOnlyList<참고점> points)
            {
                var result = 0f;
                for (var index = 0; index < points.Count; index++)
                {
                    var next = (index + 1) % points.Count;
                    result += points[index].x * points[next].z - points[next].x * points[index].z;
                }
                return result * .5f;
            }
        }
    }
}
