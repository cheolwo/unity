using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 동결된 사가정 참고 건물 윤곽을 CPU에서 조회한다. Collider, Renderer, GameObject를 만들지 않으며
    /// 선택 결과는 관찰 UI가 읽는 후보 정보일 뿐 Graph Map 또는 Simulation 상태가 아니다.
    /// </summary>
    public sealed class 사가정H공간선택Layer
    {
        public const string ExpectedMapRevision = "sagajeong-reference.r3";
        public const string ExpectedMapSha256 = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
        public const string WaitingForGraphMapBinding = "WaitingForGraphMapBinding";
        public const string CandidateBinding = "CandidateBinding";
        public const int ChunkAxisCount = 4;
        private const float Epsilon = .0001f;

        private static readonly IReadOnlyDictionary<string, HBindingDefinition> HBindings =
            new Dictionary<string, HBindingDefinition>(StringComparer.Ordinal)
            {
                ["osm:way:1256772531"] = new HBindingDefinition(
                    "h1-stock:town-order-packing",
                    new[] { "h2-candidate:town-order-fulfillment", "h2-candidate:market-life-commerce" },
                    "h3-candidate:town-market-fulfillment", "MarketFulfillment"),
                ["osm:way:1256772606"] = new HBindingDefinition(
                    "h1-stock:town-resident-pickup",
                    new[] { "h2-candidate:lowrise-residential", "h2-candidate:town-residential-alley" },
                    "h3-candidate:town-resident-service-loop", "ResidentService"),
            };

        private readonly IReadOnlyList<BuildingIndexEntry> buildings;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<BuildingIndexEntry>> chunks;
        private readonly float mapLeft;
        private readonly float mapBottom;
        private readonly float chunkSize;

        private 사가정H공간선택Layer(공간참고자료 source, string sourceSha256,
            IReadOnlyList<BuildingIndexEntry> buildings,
            IReadOnlyDictionary<int, IReadOnlyList<BuildingIndexEntry>> chunks)
        {
            SourceRevision = source.revision;
            SourceSha256 = sourceSha256;
            this.buildings = buildings;
            this.chunks = chunks;
            mapLeft = source.offsetX - source.halfExtent;
            mapBottom = source.offsetZ - source.halfExtent;
            chunkSize = source.halfExtent * 2f / ChunkAxisCount;
        }

        public string SourceRevision { get; }
        public string SourceSha256 { get; }
        public int BuildingCount => buildings.Count;
        public int RoleBoundBuildingCount => 사가정건물RoleCatalog.ExplicitBuildingCount;
        public int ExplicitRoleCount => 사가정건물RoleCatalog.ExplicitRoleCount;
        public bool CandidateOnly => true;
        public bool ActualE5 => false;
        public bool TraversalReady => false;
        public bool GameplayReady => false;
        public bool ServerProjectionReady => false;
        public bool HasSelection => Selected != null;
        public 사가정건물H선택Result Selected { get; private set; }

        public static 사가정H공간선택Layer Create(공간참고자료 source, byte[] sourceBytes)
            => Create(source, sourceBytes, ExpectedMapRevision, ExpectedMapSha256, null, null);

        /// <summary>
        /// 현재 View가 실제로 그리는 높이·공간 보완을 동일하게 적용해 화면 메시와 선택 기둥을 맞춘다.
        /// 선택 결과의 식별자와 원본 메타데이터는 r3 참고 건물을 계속 사용한다.
        /// </summary>
        public static 사가정H공간선택Layer Create(공간참고자료 source, byte[] sourceBytes,
            건물높이보완조회 heights, 사가정공간표현Composition composition)
            => Create(source, sourceBytes, ExpectedMapRevision, ExpectedMapSha256, heights, composition);

        /// <summary>시험 fixture도 동일한 판본/hash 검사를 거치도록 예상값을 명시하는 생성 진입점.</summary>
        public static 사가정H공간선택Layer Create(공간참고자료 source, byte[] sourceBytes,
            string expectedRevision, string expectedSha256)
            => Create(source, sourceBytes, expectedRevision, expectedSha256, null, null);

        /// <summary>표현 보완을 포함한 시험 fixture도 판본/hash 검사를 우회하지 않는 진입점.</summary>
        public static 사가정H공간선택Layer Create(공간참고자료 source, byte[] sourceBytes,
            string expectedRevision, string expectedSha256, 건물높이보완조회 heights,
            사가정공간표현Composition composition)
        {
            if (source == null) throw new ArgumentException("MapMissing");
            if (sourceBytes == null || sourceBytes.Length == 0) throw new ArgumentException("MapBytesMissing");
            if (string.IsNullOrWhiteSpace(expectedRevision)) throw new ArgumentException("ExpectedMapRevisionMissing");
            if (!IsSha256(expectedSha256)) throw new ArgumentException("ExpectedMapHashInvalid");

            var actualHash = Sha256(sourceBytes);
            if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("MapHashMismatch");
            if (!string.Equals(source.revision, expectedRevision, StringComparison.Ordinal))
                throw new ArgumentException("MapRevisionMismatch");
            if (!IsFinite(source.offsetX) || !IsFinite(source.offsetZ)
                || !IsFinite(source.halfExtent) || source.halfExtent <= 0f)
                throw new ArgumentException("MapBoundsInvalid");
            if (source.buildings == null) throw new ArgumentException("MapBuildingsMissing");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<BuildingIndexEntry>(source.buildings.Length);
            foreach (var building in source.buildings)
            {
                if (building == null || string.IsNullOrWhiteSpace(building.id) || !ids.Add(building.id))
                    throw new ArgumentException("MapBuildingIdentityInvalid");
                if (!IsFinite(building.height) || building.height <= 0f
                    || building.points == null || building.points.Length < 4)
                    throw new ArgumentException("MapBuildingGeometryInvalid");

                var footprints = new[] { NormalizeFootprint(building.points) };
                var displayedHeight = Mathf.Max(2f, heights?.Resolve(building) ?? building.height);
                var displayedHeightKind = heights != null && Mathf.Abs(displayedHeight - building.height) > Epsilon
                    ? "BuildingHeightOverlay"
                    : building.heightKind ?? string.Empty;
                var displayGeometrySource = "SagajeongReference.r3";
                if (composition?.OverlayApplied == true
                    && composition.TryResolveLegacyBuilding(building.id, out var overlay))
                {
                    footprints = overlay.Parts.Select(part => NormalizeFootprint(part.Outer)).ToArray();
                    displayedHeight = Mathf.Max(2f, (float)overlay.HeightMeters);
                    displayedHeightKind = overlay.HeightKind ?? string.Empty;
                    displayGeometrySource = "SpatialPresentationOverlay";
                }

                if (footprints.Length == 0 || footprints.Any(footprint =>
                        footprint.Length < 3 || Math.Abs(SignedArea(footprint)) <= Epsilon))
                    throw new ArgumentException("MapBuildingPresentationGeometryInvalid");
                entries.Add(new BuildingIndexEntry(building, footprints, displayedHeight,
                    displayedHeightKind, displayGeometrySource));
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.Source.id, right.Source.id));
            var chunkSize = source.halfExtent * 2f / ChunkAxisCount;
            var mapLeft = source.offsetX - source.halfExtent;
            var mapBottom = source.offsetZ - source.halfExtent;
            var chunkLists = new Dictionary<int, List<BuildingIndexEntry>>();
            foreach (var entry in entries)
            {
                var minX = ChunkIndex(entry.MinX, mapLeft, chunkSize);
                var maxX = ChunkIndex(entry.MaxX, mapLeft, chunkSize);
                var minZ = ChunkIndex(entry.MinZ, mapBottom, chunkSize);
                var maxZ = ChunkIndex(entry.MaxZ, mapBottom, chunkSize);
                for (var z = minZ; z <= maxZ; z++)
                for (var x = minX; x <= maxX; x++)
                {
                    var key = x + z * ChunkAxisCount;
                    if (!chunkLists.TryGetValue(key, out var list)) chunkLists[key] = list = new List<BuildingIndexEntry>();
                    list.Add(entry);
                }
            }

            var frozenChunks = chunkLists.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyList<BuildingIndexEntry>)pair.Value
                    .OrderBy(entry => entry.Area)
                    .ThenBy(entry => entry.Source.id, StringComparer.Ordinal).ToArray());
            return new 사가정H공간선택Layer(source, actualHash, entries.ToArray(), frozenChunks);
        }

        public void ClearSelection() => Selected = null;

        public bool TryGetByStableId(string buildingStableId, out 사가정건물H선택Result result)
        {
            var entry = buildings.FirstOrDefault(candidate =>
                string.Equals(candidate.Source.id, buildingStableId, StringComparison.Ordinal));
            return Select(entry, out result);
        }

        public bool TrySelectLocalPoint(Vector2 localXZ, out 사가정건물H선택Result result)
        {
            if (!IsFinite(localXZ.x) || !IsFinite(localXZ.y))
                throw new ArgumentException("SelectionPointInvalid");
            var chunkX = ChunkIndex(localXZ.x, mapLeft, chunkSize);
            var chunkZ = ChunkIndex(localXZ.y, mapBottom, chunkSize);
            if (!chunks.TryGetValue(chunkX + chunkZ * ChunkAxisCount, out var candidates))
                return Select(null, out result);

            BuildingIndexEntry best = null;
            foreach (var candidate in candidates)
            {
                if (localXZ.x < candidate.MinX - Epsilon || localXZ.x > candidate.MaxX + Epsilon
                    || localXZ.y < candidate.MinZ - Epsilon || localXZ.y > candidate.MaxZ + Epsilon
                    || !candidate.Footprints.Any(footprint => Contains(footprint, localXZ))) continue;
                if (best == null || candidate.Area < best.Area - Epsilon
                    || (Mathf.Abs(candidate.Area - best.Area) <= Epsilon
                        && string.CompareOrdinal(candidate.Source.id, best.Source.id) < 0)) best = candidate;
            }
            return Select(best, out result);
        }

        public bool TrySelectWorldRay(Ray worldRay, Transform mapRoot, out 사가정건물H선택Result result)
        {
            if (mapRoot == null) throw new ArgumentException("MapRootMissing");
            var origin = mapRoot.InverseTransformPoint(worldRay.origin);
            var next = mapRoot.InverseTransformPoint(worldRay.origin + worldRay.direction);
            var direction = next - origin;
            if (!IsFinite(origin) || !IsFinite(direction) || direction.sqrMagnitude <= Epsilon * Epsilon)
                throw new ArgumentException("SelectionRayInvalid");

            BuildingIndexEntry best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in buildings)
            {
                if (!RayIntersectsBounds(origin, direction, candidate, out _)
                    || !RayIntersectsPrism(origin, direction, candidate, out var distance)) continue;
                if (distance < bestDistance - Epsilon
                    || (Mathf.Abs(distance - bestDistance) <= Epsilon
                        && (best == null || candidate.Area < best.Area - Epsilon
                            || (Mathf.Abs(candidate.Area - best.Area) <= Epsilon
                                && string.CompareOrdinal(candidate.Source.id, best.Source.id) < 0))))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return Select(best, out result);
        }

        private bool Select(BuildingIndexEntry entry, out 사가정건물H선택Result result)
        {
            if (entry == null)
            {
                Selected = result = null;
                return false;
            }

            HBindings.TryGetValue(entry.Source.id, out var binding);
            var address = string.Join(" ", new[] { entry.Source.street, entry.Source.houseNumber, entry.Source.unit }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            Selected = result = new 사가정건물H선택Result(
                entry.Source.id, entry.Source.name ?? string.Empty, address,
                entry.Source.buildingKind ?? string.Empty, entry.Source.height,
                entry.Source.heightKind ?? string.Empty, entry.Source.levelsText ?? string.Empty,
                entry.Source.version, entry.Centroid, entry.Area,
                entry.DisplayedHeight, entry.DisplayedHeightKind, entry.DisplayGeometrySource,
                binding?.H1StableId ?? string.Empty,
                binding?.H2StableIds ?? Array.Empty<string>(),
                 binding?.H3StableId ?? string.Empty,
                 binding?.H3RoleCode ?? string.Empty,
                 binding == null ? WaitingForGraphMapBinding : CandidateBinding,
                 사가정건물RoleCatalog.Resolve(entry.Source.id));
            return true;
        }

        private static bool RayIntersectsPrism(Vector3 origin, Vector3 direction,
            BuildingIndexEntry entry, out float bestDistance)
        {
            var hitDistance = float.PositiveInfinity;
            if (Mathf.Abs(direction.y) > Epsilon)
            {
                TestHorizontalPlane(entry.DisplayedHeight);
                TestHorizontalPlane(0f);
            }

            var rayDirection = new Vector2(direction.x, direction.z);
            var rayOrigin = new Vector2(origin.x, origin.z);
            foreach (var footprint in entry.Footprints)
            for (var index = 0; index < footprint.Length; index++)
            {
                var a = footprint[index];
                var b = footprint[(index + 1) % footprint.Length];
                var edge = b - a;
                var denominator = Cross(rayDirection, edge);
                if (Mathf.Abs(denominator) <= Epsilon) continue;
                var delta = a - rayOrigin;
                var distance = Cross(delta, edge) / denominator;
                var edgeFactor = Cross(delta, rayDirection) / denominator;
                if (distance < -Epsilon || edgeFactor < -Epsilon || edgeFactor > 1f + Epsilon) continue;
                var y = origin.y + direction.y * distance;
                if (y >= -Epsilon && y <= entry.DisplayedHeight + Epsilon)
                    hitDistance = Mathf.Min(hitDistance, Mathf.Max(0f, distance));
            }
            bestDistance = hitDistance;
            return !float.IsPositiveInfinity(hitDistance);

            void TestHorizontalPlane(float y)
            {
                var distance = (y - origin.y) / direction.y;
                if (distance < -Epsilon) return;
                var point = new Vector2(origin.x + direction.x * distance, origin.z + direction.z * distance);
                if (entry.Footprints.Any(footprint => Contains(footprint, point)))
                    hitDistance = Mathf.Min(hitDistance, Mathf.Max(0f, distance));
            }
        }

        private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction,
            BuildingIndexEntry entry, out float entryDistance)
        {
            var min = new Vector3(entry.MinX, 0f, entry.MinZ);
            var max = new Vector3(entry.MaxX, entry.DisplayedHeight, entry.MaxZ);
            var near = 0f;
            var far = float.PositiveInfinity;
            if (!Axis(origin.x, direction.x, min.x, max.x, ref near, ref far)
                || !Axis(origin.y, direction.y, min.y, max.y, ref near, ref far)
                || !Axis(origin.z, direction.z, min.z, max.z, ref near, ref far))
            {
                entryDistance = 0f;
                return false;
            }
            entryDistance = near;
            return true;
        }

        private static bool Axis(float origin, float direction, float min, float max, ref float near, ref float far)
        {
            if (Mathf.Abs(direction) <= Epsilon) return origin >= min - Epsilon && origin <= max + Epsilon;
            var first = (min - origin) / direction;
            var second = (max - origin) / direction;
            if (first > second) (first, second) = (second, first);
            near = Mathf.Max(near, first);
            far = Mathf.Min(far, second);
            return far + Epsilon >= near;
        }

        private static 참고점[] ValidatePoints(참고점[] source)
        {
            foreach (var point in source)
                if (point == null || !IsFinite(point.x) || !IsFinite(point.z))
                    throw new ArgumentException("MapBuildingPointInvalid");
            return source;
        }

        private static Vector2[] NormalizeFootprint(참고점[] source)
        {
            ValidatePoints(source);
            var length = source.Length;
            if (length > 1 && Mathf.Abs(source[0].x - source[length - 1].x) <= Epsilon
                && Mathf.Abs(source[0].z - source[length - 1].z) <= Epsilon) length--;
            var result = new Vector2[length];
            for (var index = 0; index < length; index++) result[index] = new Vector2(source[index].x, source[index].z);
            return result;
        }

        private static Vector2[] NormalizeFootprint(
            IReadOnlyList<사가정공간표현Point> source)
        {
            if (source == null || source.Count < 3)
                throw new ArgumentException("MapBuildingPresentationGeometryInvalid");
            var length = source.Count;
            if (length > 1 && Math.Abs(source[0].X - source[length - 1].X) <= Epsilon
                           && Math.Abs(source[0].Z - source[length - 1].Z) <= Epsilon) length--;
            var result = new Vector2[length];
            for (var index = 0; index < length; index++)
            {
                var x = (float)source[index].X;
                var z = (float)source[index].Z;
                if (!IsFinite(x) || !IsFinite(z))
                    throw new ArgumentException("MapBuildingPresentationGeometryInvalid");
                result[index] = new Vector2(x, z);
            }
            return result;
        }

        private static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
            {
                var a = polygon[previous];
                var b = polygon[current];
                if (PointOnSegment(point, a, b)) return true;
                if ((a.y > point.y) == (b.y > point.y)) continue;
                var crossingX = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (point.x < crossingX) inside = !inside;
            }
            return inside;
        }

        private static bool PointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var edge = b - a;
            var delta = point - a;
            if (Mathf.Abs(Cross(edge, delta)) > Epsilon) return false;
            var dot = Vector2.Dot(delta, edge);
            return dot >= -Epsilon && dot <= edge.sqrMagnitude + Epsilon;
        }

        private static float SignedArea(IReadOnlyList<Vector2> footprint)
        {
            var sum = 0f;
            for (var index = 0; index < footprint.Count; index++)
            {
                var next = (index + 1) % footprint.Count;
                sum += footprint[index].x * footprint[next].y - footprint[next].x * footprint[index].y;
            }
            return sum * .5f;
        }

        private static Vector2 Centroid(IReadOnlyList<Vector2> footprint, float signedArea)
        {
            var x = 0f;
            var z = 0f;
            for (var index = 0; index < footprint.Count; index++)
            {
                var next = (index + 1) % footprint.Count;
                var factor = footprint[index].x * footprint[next].y - footprint[next].x * footprint[index].y;
                x += (footprint[index].x + footprint[next].x) * factor;
                z += (footprint[index].y + footprint[next].y) * factor;
            }
            var divisor = 6f * signedArea;
            return new Vector2(x / divisor, z / divisor);
        }

        private static int ChunkIndex(float coordinate, float start, float size)
            => Mathf.Clamp(Mathf.FloorToInt((coordinate - start) / size), 0, ChunkAxisCount - 1);

        private static float Cross(Vector2 left, Vector2 right) => left.x * right.y - left.y * right.x;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

        private static string Sha256(byte[] source)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(source)
                .Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
        }

        private sealed class BuildingIndexEntry
        {
            public BuildingIndexEntry(참고건물 source, IReadOnlyList<Vector2[]> footprints,
                float displayedHeight, string displayedHeightKind, string displayGeometrySource)
            {
                Source = source;
                Footprints = footprints;
                DisplayedHeight = displayedHeight;
                DisplayedHeightKind = displayedHeightKind;
                DisplayGeometrySource = displayGeometrySource;
                var weightedCentroid = Vector2.zero;
                var area = 0f;
                foreach (var footprint in footprints)
                {
                    var signedArea = SignedArea(footprint);
                    var partArea = Mathf.Abs(signedArea);
                    weightedCentroid += 사가정H공간선택Layer.Centroid(footprint, signedArea) * partArea;
                    area += partArea;
                }
                Area = area;
                Centroid = weightedCentroid / area;
                MinX = footprints.SelectMany(value => value).Min(point => point.x);
                MaxX = footprints.SelectMany(value => value).Max(point => point.x);
                MinZ = footprints.SelectMany(value => value).Min(point => point.y);
                MaxZ = footprints.SelectMany(value => value).Max(point => point.y);
            }

            public 참고건물 Source { get; }
            public IReadOnlyList<Vector2[]> Footprints { get; }
            public float Area { get; }
            public float DisplayedHeight { get; }
            public string DisplayedHeightKind { get; }
            public string DisplayGeometrySource { get; }
            public float MinX { get; }
            public float MaxX { get; }
            public float MinZ { get; }
            public float MaxZ { get; }
            public Vector2 Centroid { get; }
        }

        private sealed class HBindingDefinition
        {
            public HBindingDefinition(string h1StableId, string[] h2StableIds,
                string h3StableId, string h3RoleCode)
            {
                H1StableId = h1StableId;
                H2StableIds = h2StableIds;
                H3StableId = h3StableId;
                H3RoleCode = h3RoleCode;
            }

            public string H1StableId { get; }
            public string[] H2StableIds { get; }
            public string H3StableId { get; }
            public string H3RoleCode { get; }
        }
    }

    public sealed class 사가정건물H선택Result
    {
        internal 사가정건물H선택Result(string buildingStableId, string sourceName,
            string sourceAddress, string buildingKind, float sourceHeight,
            string heightKind, string levelsText, int sourceVersion, Vector2 localCentroid,
            float footprintArea, float displayedHeight, string displayedHeightKind,
            string displayGeometrySource, string h1StableId, IReadOnlyList<string> h2StableIds,
             string h3StableId, string h3RoleCode, string bindingStatus,
             IReadOnlyList<사가정건물RoleProfile> roleProfiles)
        {
            BuildingStableId = buildingStableId;
            SourceName = sourceName;
            SourceAddress = sourceAddress;
            BuildingKind = buildingKind;
            SourceHeight = sourceHeight;
            HeightKind = heightKind;
            LevelsText = levelsText;
            SourceVersion = sourceVersion;
            LocalCentroid = localCentroid;
            FootprintArea = footprintArea;
            DisplayedHeight = displayedHeight;
            DisplayedHeightKind = displayedHeightKind;
            DisplayGeometrySource = displayGeometrySource;
            H1StableId = h1StableId;
            H2StableIds = h2StableIds;
            H3StableId = h3StableId;
             H3RoleCode = h3RoleCode;
            if (roleProfiles == null || roleProfiles.Count == 0
                || roleProfiles.Any(value => value == null)
                || roleProfiles.Select(value => value.RoleStableId)
                    .Distinct(StringComparer.Ordinal).Count() != roleProfiles.Count)
                throw new ArgumentException("BuildingRoleProfilesInvalid");
            RoleProfiles = roleProfiles.ToArray();
            H1BindingStatus = bindingStatus;
            H2BindingStatus = bindingStatus;
            H3BindingStatus = bindingStatus;
            if (bindingStatus == 사가정H공간선택Layer.CandidateBinding)
            {
                TheoryAreaSetRef = "area-set:theory:lowrise-market-region";
                CompositionPatternRef = "area-set-composition:town:lowrise-life-market";
                CompositionStateCode = "PartialCandidate";
                SatisfiedRoleCodes = new[] { "ResidentService", "MarketFulfillment" };
                MissingRoleCodes = new[] { "LowriseMarket", "CircularMarket", "ContaminationRelief" };
                AreaSetBindingStatus = 사가정H공간선택Layer.CandidateBinding;
            }
            else
            {
                TheoryAreaSetRef = string.Empty;
                CompositionPatternRef = string.Empty;
                CompositionStateCode = 사가정H공간선택Layer.WaitingForGraphMapBinding;
                SatisfiedRoleCodes = Array.Empty<string>();
                MissingRoleCodes = Array.Empty<string>();
                AreaSetBindingStatus = 사가정H공간선택Layer.WaitingForGraphMapBinding;
            }
        }

        public string BuildingStableId { get; }
        public string SourceName { get; }
        public string SourceAddress { get; }
        public string BuildingKind { get; }
        public float SourceHeight { get; }
        public string HeightKind { get; }
        public string LevelsText { get; }
        public int SourceVersion { get; }
        public Vector2 LocalCentroid { get; }
        public float FootprintArea { get; }
        public float DisplayedHeight { get; }
        public string DisplayedHeightKind { get; }
        public string DisplayGeometrySource { get; }
        public string H1StableId { get; }
        public string H1BindingStatus { get; }
        public IReadOnlyList<string> H2StableIds { get; }
        public string H2BindingStatus { get; }
        public string H3StableId { get; }
        public string H3RoleCode { get; }
        public string H3BindingStatus { get; }
        public IReadOnlyList<사가정건물RoleProfile> RoleProfiles { get; }
        public string AreaSetStableId => string.Empty;
        public string TheoryAreaSetRef { get; }
        public string CompositionPatternRef { get; }
        public string CompositionStateCode { get; }
        public IReadOnlyList<string> SatisfiedRoleCodes { get; }
        public IReadOnlyList<string> MissingRoleCodes { get; }
        public string AreaSetBindingStatus { get; }
        public string H4AreaSetBindingStatus => AreaSetBindingStatus;
        public bool CandidateOnly => true;
        public bool ActualE5 => false;
        public bool TraversalReady => false;
        public bool GameplayReady => false;
        public bool ServerProjectionReady => false;
    }
}
