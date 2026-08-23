using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// H5 중심 간격을 H4 외곽 한도로 내리고, 실제 Renderer 발자국을 이용해
    /// H1의 이격·능선 회피·울타리 연속성을 결정적으로 적용합니다.
    /// </summary>
    public static class H공간배치규칙EditorEngine
    {
        public const float AreaCoreRadiusRatio = .36f;
        public const float MinimumObjectClearance = .55f;
        public const float BuildingRidgeClearance = 3f;
        public const float MaximumBuildingSlopeDegrees = 5f;
        public const float MaximumRidgeEnvelope = 12f;
        public const float MaximumFenceSegmentGap = 1.5f;
        public const float RoadsideEntranceGapWidth = 2.6f;

        private const string AnchorCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/거점CompositionCatalog.asset";
        private const string RoadCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/도로GateCompositionCatalog.asset";
        private const string FencePrefabPath =
            "Assets/Synty/PolygonTown/Prefabs/Environment/SM_Env_Fence_White_Straight_01.prefab";

        public static H공간배치규칙증거Root Apply(
            Transform evidenceRoot,
            IReadOnlyList<AreaSet구성패턴검토Root> areas,
            플레이어경관Controller player)
        {
            if (evidenceRoot == null || areas == null || areas.Count != 4
                || player == null || player.VisualRoot == null)
                throw new InvalidOperationException(
                    "HSpatialPlacementRuleInputInvalid");

            var playerHeight = MeasurePlayerHeight(player);
            DisableGlobalBackdropRenderers(areas);
            var areaRecords = NormalizeAreaSetEnvelopes(areas);
            var frontage = BuildRoadsideFrontagePattern(areas);
            var objects = CollectAndNormalizeObjects(areas, playerHeight);
            var farmCoreAnchorCount = ApplyFarmPlayerCoreAnchors(objects,
                player.transform.position);
            var violations = new List<string>();
            ResolveObjectClearance(objects, areaRecords, violations);
            var fenceChains = ValidateFenceChains(objects, violations);
            ConfigureRoadsideFrontagePattern(frontage);

            var records = objects.Select(value => new H1배치규칙Record
            {
                AreaRoleCode = value.Area.AreaRoleCode,
                H1Name = value.Transform.name,
                CategoryCode = value.Category,
                LandscapeNodeName = NearestChildName(
                    value.Area.H3Boundaries, value.Bounds.center),
                BlockNodeName = NearestChildName(
                    value.Area.H2Boundaries, value.Bounds.center),
                ScaleBandCode = value.Category,
                OriginalPosition = value.OriginalPosition,
                AppliedPosition = value.Transform.position,
                FootprintCenterAfter = value.Bounds.center,
                FootprintAfter = new Vector2(value.Bounds.size.x,
                    value.Bounds.size.z),
                HeightAfter = value.Bounds.size.y,
                HeightToPlayerRatio = value.Bounds.size.y / playerHeight,
                FootprintToPlayerRatio = Mathf.Max(value.Bounds.size.x,
                    value.Bounds.size.z) / playerHeight,
                AppliedScale = value.AppliedScale,
                // 현재 실증 바닥은 H3 경계에서 만든 평탄 Collider입니다.
                // 실제 E5/E6 지형에서는 placement-mask 경사 표본으로 교체해야 합니다.
                SampledSlopeDegrees = 0f,
                Accepted = !violations.Any(issue => issue.EndsWith(
                    ":" + value.StableKey, StringComparison.Ordinal)),
            }).ToArray();

            var hierarchyObject = new GameObject("PlacementControlHierarchy");
            hierarchyObject.transform.SetParent(evidenceRoot, false);
            var hierarchy = hierarchyObject.AddComponent<배치통제계층Root>();
            hierarchy.Configure(evidenceRoot, player, playerHeight);

            var component = evidenceRoot.gameObject
                .AddComponent<H공간배치규칙증거Root>();
            component.Configure(
                AreaCoreRadiusRatio,
                MinimumObjectClearance,
                BuildingRidgeClearance,
                MaximumBuildingSlopeDegrees,
                MaximumRidgeEnvelope,
                MaximumFenceSegmentGap,
                areaRecords,
                records,
                hierarchy,
                new[] { frontage.Root },
                fenceChains,
                farmCoreAnchorCount,
                objects.Count(value => Vector3.Distance(value.OriginalPosition,
                    value.Transform.position) > .01f),
                objects.Count(value => value.AppliedScale < .999f),
                violations.ToArray());
            return component;
        }

        private static HAreaSet배치규칙Record[] NormalizeAreaSetEnvelopes(
            IReadOnlyList<AreaSet구성패턴검토Root> areas)
        {
            var output = new List<HAreaSet배치규칙Record>();
            foreach (var area in areas.OrderBy(value => value.AreaRoleCode,
                         StringComparer.Ordinal))
            {
                var nearest = areas.Where(value => value != area)
                    .Min(value => HorizontalDistance(area.transform.position,
                        value.transform.position));
                var before = CoreRadius(area);
                var allowed = Mathf.Clamp(nearest * AreaCoreRadiusRatio, 18f, 65f);
                var scale = Mathf.Clamp(allowed / before, .12f, 1f);
                area.transform.localScale *= scale;
                NormalizeTerrainUnderlay(area, allowed * .96f);
                output.Add(new HAreaSet배치규칙Record
                {
                    AreaRoleCode = area.AreaRoleCode,
                    NearestAreaCenterDistance = nearest,
                    CoreRadiusBefore = before,
                    AllowedCoreRadius = allowed,
                    AppliedScale = scale,
                });
            }
            return output.ToArray();
        }

        private static List<PlacementObject> CollectAndNormalizeObjects(
            IReadOnlyList<AreaSet구성패턴검토Root> areas,
            float playerHeight)
        {
            var output = new List<PlacementObject>();
            foreach (var area in areas)
            foreach (Transform h1 in area.SyntyPresentation)
            {
                if (!TryBounds(h1, out var bounds)) continue;
                var category = Category(h1, bounds);
                var maximumHorizontalRatio = category switch
                {
                    "Ridge" => 8f,
                    "Building" => 4.2f,
                    "FenceChain" => 4f,
                    "RoadFrontage" => 12f,
                    _ => 3.8f,
                };
                var maximumHeightRatio = category switch
                {
                    "Ridge" => 8f,
                    "Building" => 3.5f,
                    "FenceChain" => 1.2f,
                    "RoadFrontage" => 3.5f,
                    _ => 2.5f,
                };
                var horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
                var horizontalScale = playerHeight * maximumHorizontalRatio
                                      / horizontal;
                var heightScale = playerHeight * maximumHeightRatio
                                  / bounds.size.y;
                var scale = Mathf.Min(1f, horizontalScale, heightScale);
                if (category == "Ridge")
                    scale = Mathf.Min(scale,
                        MaximumRidgeEnvelope / horizontal);
                if (scale < .999f)
                {
                    h1.localScale *= scale;
                    TryBounds(h1, out bounds);
                }
                output.Add(new PlacementObject(area, h1, category, bounds,
                    h1.position, scale));
            }
            return output;
        }

        private static PendingRoadsideFrontage BuildRoadsideFrontagePattern(
            IReadOnlyList<AreaSet구성패턴검토Root> areas)
        {
            var farm = areas.Single(value => value.AreaRoleCode == "Farm");
            var h3Node = farm.H3Boundaries.Cast<Transform>().Single(value =>
                value.name == "H3_HomeProcessing");
            var h2Node = farm.H2Boundaries.Cast<Transform>().Single(value =>
                value.name == "H2_h2-candidate:farm-worker-support");
            var roadCatalog = AssetDatabase.LoadAssetAtPath<도로GateCompositionCatalog>(
                                  RoadCatalogPath)
                              ?? throw new InvalidOperationException(
                                  "RoadsideFrontageRoadCatalogMissing");
            var anchorCatalog = AssetDatabase.LoadAssetAtPath<거점CompositionCatalog>(
                                    AnchorCatalogPath)
                                ?? throw new InvalidOperationException(
                                    "RoadsideFrontageAnchorCatalogMissing");
            var roadEntry = roadCatalog.Resolve(
                도로GateCompositionSetNames.농촌도로직선);
            var buildingEntry = anchorCatalog.Resolve(
                거점CompositionSetNames.타운기본주택);
            var fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                                  FencePrefabPath)
                              ?? throw new InvalidOperationException(
                                  "RoadsideFrontageFencePrefabMissing");

            var patternObject = new GameObject(
                "H1_RoadsideFrontage_Homestead_A");
            patternObject.transform.SetParent(farm.SyntyPresentation, true);
            patternObject.transform.position = new Vector3(
                h2Node.position.x,
                farm.transform.position.y,
                h2Node.position.z + 3f);
            var pattern = patternObject
                .AddComponent<도로변전면배치PatternRoot>();

            var road = InstantiatePrefab(roadEntry.Prefab,
                patternObject.transform, "H1_RoadModule_RuralStraight");
            road.localPosition = Vector3.zero;
            road.localRotation = Quaternion.identity;
            road.localScale = Vector3.one * .55f;
            GroundAt(road, patternObject.transform.position.y);
            var roadView = road.GetComponent<도로GateCompositionSetView>()
                           ?? throw new InvalidOperationException(
                               "RoadsideFrontageRoadViewMissing");
            var roadConnector = roadView.FindConnector("vehicle-north")
                                ?? throw new InvalidOperationException(
                                    "RoadsideFrontageRoadConnectorMissing");

            var building = InstantiatePrefab(buildingEntry.Prefab,
                patternObject.transform, "H1_BuildingModule_Homestead");
            building.localRotation = Quaternion.identity;
            building.localScale = Vector3.one * .35f;
            foreach (var renderer in building
                         .GetComponentsInChildren<Renderer>(true)
                         .Where(value => value.name.Contains("Fence",
                             StringComparison.OrdinalIgnoreCase)))
                renderer.enabled = false;
            GroundAt(building, patternObject.transform.position.y);
            var buildingView = building.GetComponent<거점CompositionSetView>()
                               ?? throw new InvalidOperationException(
                                   "RoadsideFrontageBuildingViewMissing");
            var buildingConnector = buildingView.FindConnector("vehicle-south")
                                    ?? throw new InvalidOperationException(
                                        "RoadsideFrontageBuildingConnectorMissing");
            MoveHorizontal(building,
                roadConnector.position + Vector3.forward * 2f
                - buildingConnector.position);
            var entrance = buildingView.FindSocket(
                               "town.interaction.entrance")
                           ?? throw new InvalidOperationException(
                               "RoadsideFrontageBuildingEntranceMissing");
            var buildingVisual = building
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name.Contains("Bld_House",
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    "RoadsideFrontageBuildingVisualMissing");

            var leftFence = InstantiatePrefab(fencePrefab,
                patternObject.transform, "H1_FenceModule_Left");
            var rightFence = InstantiatePrefab(fencePrefab,
                patternObject.transform, "H1_FenceModule_Right");
            PrepareFence(leftFence, 3.8f);
            PrepareFence(rightFence, 3.8f);
            var roadBounds = RequiredBounds(road);
            var fenceZ = roadBounds.max.z + .5f;
            PlaceFence(leftFence,
                entrance.position.x - RoadsideEntranceGapWidth * .5f,
                fenceZ, true, patternObject.transform.position.y);
            PlaceFence(rightFence,
                entrance.position.x + RoadsideEntranceGapWidth * .5f,
                fenceZ, false, patternObject.transform.position.y);

            return new PendingRoadsideFrontage(
                pattern,
                farm,
                h3Node.name,
                h2Node.name,
                roadEntry.CompositionKey,
                buildingEntry.CompositionKey,
                road,
                leftFence,
                rightFence,
                buildingVisual,
                roadConnector,
                entrance);
        }

        private static void ConfigureRoadsideFrontagePattern(
            PendingRoadsideFrontage value)
        {
            value.Root.Configure(
                value.Area.AreaRoleCode,
                value.H3NodeName,
                value.H2NodeName,
                value.RoadCompositionKey,
                value.BuildingCompositionKey,
                value.Road,
                value.LeftFence,
                value.RightFence,
                value.Building,
                value.RoadConnector,
                value.Entrance,
                RoadsideEntranceGapWidth);
        }

        private static Transform InstantiatePrefab(GameObject prefab,
            Transform parent, string name)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject
                           ?? throw new InvalidOperationException(
                               "RoadsideFrontagePrefabInstantiateFailed:" + name);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance.transform;
        }

        private static void PrepareFence(Transform fence, float length)
        {
            fence.localRotation = Quaternion.identity;
            fence.localScale = Vector3.one;
            var bounds = RequiredBounds(fence);
            if (bounds.size.z > bounds.size.x)
            {
                fence.localRotation = Quaternion.Euler(0f, 90f, 0f);
                bounds = RequiredBounds(fence);
            }
            fence.localScale *= length / Mathf.Max(bounds.size.x,
                bounds.size.z);
        }

        private static void PlaceFence(Transform fence, float gapEdgeX,
            float z, bool left, float groundY)
        {
            var bounds = RequiredBounds(fence);
            fence.position += new Vector3(
                gapEdgeX - (left ? bounds.max.x : bounds.min.x),
                0f,
                z - bounds.center.z);
            GroundAt(fence, groundY);
        }

        private static void GroundAt(Transform root, float groundY)
        {
            var bounds = RequiredBounds(root);
            root.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static void MoveHorizontal(Transform root, Vector3 delta) =>
            root.position += new Vector3(delta.x, 0f, delta.z);

        private static Bounds RequiredBounds(Transform root)
        {
            if (!TryBounds(root, out var bounds))
                throw new InvalidOperationException(
                    "RoadsideFrontageRendererMissing:" + root.name);
            return bounds;
        }

        private static float MeasurePlayerHeight(플레이어경관Controller player)
        {
            var renderers = player.VisualRoot
                .GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                    bounds.Encapsulate(renderers[index].bounds);
                if (bounds.size.y is >= .8f and <= 3f) return bounds.size.y;
            }
            var fallback = player.Profile?.CapsuleHeight ?? 0f;
            if (fallback is >= .8f and <= 3f) return fallback;
            throw new InvalidOperationException(
                "PlacementControlPlayerReferenceHeightInvalid");
        }

        private static string NearestChildName(Transform root, Vector3 position)
        {
            if (root == null || root.childCount == 0)
                throw new InvalidOperationException(
                    "PlacementControlHierarchyNodeMissing");
            return root.Cast<Transform>()
                .OrderBy(value => HorizontalDistance(value.position, position))
                .ThenBy(value => value.name, StringComparer.Ordinal)
                .First().name;
        }

        private static int ApplyFarmPlayerCoreAnchors(
            IEnumerable<PlacementObject> objects,
            Vector3 playerPosition)
        {
            var farm = objects.Where(value => value.Area.AreaRoleCode == "Farm")
                .ToArray();
            var anchors = new[]
            {
                (Token: "farm-production", Offset: new Vector3(-6f, 0f, 4f)),
                (Token: "farm-maintenance-yard", Offset: new Vector3(-6f, 0f, -4f)),
                (Token: "farm-work-yard", Offset: new Vector3(0f, 0f, -7f)),
                (Token: "farm-harvest-staging", Offset: new Vector3(6f, 0f, -4f)),
                (Token: "farm-seed-preparation", Offset: new Vector3(6f, 0f, 4f)),
            };
            var applied = 0;
            foreach (var anchor in anchors)
            {
                var value = farm.FirstOrDefault(candidate =>
                    candidate.Transform.name.Contains(anchor.Token,
                        StringComparison.Ordinal));
                if (value == null) continue;
                var position = playerPosition + anchor.Offset;
                position.y = value.Transform.position.y;
                value.MoveTo(position);
                applied++;
            }
            return applied;
        }

        private static void NormalizeTerrainUnderlay(
            AreaSet구성패턴검토Root area,
            float allowedRadius)
        {
            var root = area.transform.Find("H3TerrainUnderlay");
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(value => value.enabled).ToArray();
            if (renderers.Length == 0) return;
            var radius = renderers.Select(value => value.bounds)
                .Select(bounds => Mathf.Max(
                    Mathf.Abs(bounds.min.x - area.transform.position.x),
                    Mathf.Abs(bounds.max.x - area.transform.position.x),
                    Mathf.Abs(bounds.min.z - area.transform.position.z),
                    Mathf.Abs(bounds.max.z - area.transform.position.z)))
                .Max();
            if (radius > allowedRadius)
                root.localScale *= allowedRadius / radius;
        }

        private static void ResolveObjectClearance(
            List<PlacementObject> objects,
            IReadOnlyList<HAreaSet배치규칙Record> areaRecords,
            ICollection<string> violations)
        {
            var areaByRole = areaRecords.ToDictionary(value => value.AreaRoleCode,
                StringComparer.Ordinal);
            var accepted = new List<PlacementObject>();
            foreach (var current in objects.OrderBy(value => Priority(value.Category))
                         .ThenBy(value => value.StableKey, StringComparer.Ordinal))
            {
                var areaRadius = areaByRole[current.Area.AreaRoleCode]
                    .AllowedCoreRadius;
                var original = current.Transform.position;
                var resolved = CandidatePositions(original,
                        current.Category == "Ridge" ? 1.8f : 1.1f, 36)
                    .FirstOrDefault(candidate => FitsArea(current, candidate,
                            areaRadius)
                        && accepted.All(other => HasRequiredClearance(
                            current, candidate, other)));
                if (resolved == default && original != default
                    && (!FitsArea(current, default, areaRadius)
                        || accepted.Any(other => !HasRequiredClearance(
                            current, default, other))))
                {
                    violations.Add("NoPlacementCandidate:" + current.StableKey);
                    continue;
                }
                current.MoveTo(resolved);
                accepted.Add(current);
            }
        }

        private static IEnumerable<Vector3> CandidatePositions(
            Vector3 origin,
            float step,
            int maximumRing)
        {
            yield return origin;
            for (var ring = 1; ring <= maximumRing; ring++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    yield return origin + new Vector3(x * step, 0f,
                        -ring * step);
                    yield return origin + new Vector3(x * step, 0f,
                        ring * step);
                }
                for (var z = -ring + 1; z < ring; z++)
                {
                    yield return origin + new Vector3(-ring * step, 0f,
                        z * step);
                    yield return origin + new Vector3(ring * step, 0f,
                        z * step);
                }
            }
        }

        private static bool FitsArea(PlacementObject value, Vector3 candidate,
            float allowedRadius)
        {
            var translated = value.TranslatedBounds(candidate);
            var radius = Mathf.Max(translated.extents.x, translated.extents.z);
            return HorizontalDistance(translated.center,
                       value.Area.transform.position) + radius <= allowedRadius;
        }

        private static bool HasRequiredClearance(PlacementObject current,
            Vector3 candidate, PlacementObject other)
        {
            var clearance = current.Category == "Ridge"
                            && other.Category == "Building"
                            || current.Category == "Building"
                            && other.Category == "Ridge"
                ? BuildingRidgeClearance
                : MinimumObjectClearance;
            return HorizontalBoundsDistance(current.TranslatedBounds(candidate),
                other.Bounds) >= clearance;
        }

        private static int ValidateFenceChains(
            IEnumerable<PlacementObject> objects,
            ICollection<string> violations)
        {
            var count = 0;
            foreach (var value in objects.Where(value =>
                         value.Category == "FenceChain"))
            {
                var segments = value.Transform
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled
                        && renderer.gameObject.activeInHierarchy
                        && renderer.name.Contains("Fence",
                            StringComparison.OrdinalIgnoreCase))
                    .Select(renderer => renderer.bounds).ToArray();
                if (segments.Length == 0) continue;
                count++;
                if (!IsConnected(segments, MaximumFenceSegmentGap))
                    violations.Add("FenceChainDisconnected:" + value.StableKey);
            }
            return count;
        }

        private static bool IsConnected(Bounds[] segments, float maximumGap)
        {
            if (segments.Length <= 1) return true;
            var visited = new HashSet<int> { 0 };
            var queue = new Queue<int>();
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var index = 0; index < segments.Length; index++)
                {
                    if (visited.Contains(index)
                        || HorizontalBoundsDistance(segments[current],
                            segments[index]) > maximumGap) continue;
                    visited.Add(index);
                    queue.Enqueue(index);
                }
            }
            return visited.Count == segments.Length;
        }

        private static void DisableGlobalBackdropRenderers(
            IEnumerable<AreaSet구성패턴검토Root> areas)
        {
            foreach (var renderer in areas.SelectMany(value =>
                         value.SyntyPresentation
                             .GetComponentsInChildren<Renderer>(true)))
                if (renderer.name.Contains("Skybox",
                        StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;
        }

        private static float CoreRadius(AreaSet구성패턴검토Root area)
        {
            var renderers = area.SyntyPresentation
                .GetComponentsInChildren<Renderer>(true)
                .Where(value => value.enabled
                    && value.gameObject.activeInHierarchy
                    && !IsRidge(value.transform))
                .ToArray();
            if (renderers.Length == 0) return 1f;
            return renderers.Select(value => value.bounds)
                .Select(bounds => Mathf.Max(
                    Mathf.Abs(bounds.min.x - area.transform.position.x),
                    Mathf.Abs(bounds.max.x - area.transform.position.x),
                    Mathf.Abs(bounds.min.z - area.transform.position.z),
                    Mathf.Abs(bounds.max.z - area.transform.position.z)))
                .Max();
        }

        private static string Category(Transform root, Bounds bounds)
        {
            if (root.GetComponent<도로변전면배치PatternRoot>() != null)
                return "RoadFrontage";
            if (IsRidge(root)) return "Ridge";
            var names = root.GetComponentsInChildren<Transform>(true)
                .Select(value => value.name).ToArray();
            if (names.Any(value => value.Contains("_Bld_",
                    StringComparison.OrdinalIgnoreCase)
                || value.Contains("Station",
                    StringComparison.OrdinalIgnoreCase))) return "Building";
            if (names.Any(value => value.Contains("Fence",
                    StringComparison.OrdinalIgnoreCase))) return "FenceChain";
            return bounds.size.y > 4f ? "Building" : "General";
        }

        private static bool IsRidge(Transform value) =>
            value.GetComponentsInChildren<Transform>(true).Any(child =>
                child.name.Contains("Mountain", StringComparison.OrdinalIgnoreCase)
                || child.name.Contains("Cliff", StringComparison.OrdinalIgnoreCase))
            || value.name.Contains("산 능선", StringComparison.Ordinal);

        private static bool TryBounds(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(value => value.enabled
                    && value.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return true;
        }

        private static float HorizontalBoundsDistance(Bounds left, Bounds right)
        {
            var dx = Mathf.Max(0f, Mathf.Max(left.min.x - right.max.x,
                right.min.x - left.max.x));
            var dz = Mathf.Max(0f, Mathf.Max(left.min.z - right.max.z,
                right.min.z - left.max.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            var dx = left.x - right.x;
            var dz = left.z - right.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int Priority(string category) => category switch
        {
            "RoadFrontage" => 0,
            "Building" => 0,
            "FenceChain" => 1,
            "General" => 2,
            _ => 3,
        };

        private sealed class PendingRoadsideFrontage
        {
            public PendingRoadsideFrontage(
                도로변전면배치PatternRoot root,
                AreaSet구성패턴검토Root area,
                string h3NodeName,
                string h2NodeName,
                string roadCompositionKey,
                string buildingCompositionKey,
                Transform road,
                Transform leftFence,
                Transform rightFence,
                Transform building,
                Transform roadConnector,
                Transform entrance)
            {
                Root = root;
                Area = area;
                H3NodeName = h3NodeName;
                H2NodeName = h2NodeName;
                RoadCompositionKey = roadCompositionKey;
                BuildingCompositionKey = buildingCompositionKey;
                Road = road;
                LeftFence = leftFence;
                RightFence = rightFence;
                Building = building;
                RoadConnector = roadConnector;
                Entrance = entrance;
            }

            public 도로변전면배치PatternRoot Root { get; }
            public AreaSet구성패턴검토Root Area { get; }
            public string H3NodeName { get; }
            public string H2NodeName { get; }
            public string RoadCompositionKey { get; }
            public string BuildingCompositionKey { get; }
            public Transform Road { get; }
            public Transform LeftFence { get; }
            public Transform RightFence { get; }
            public Transform Building { get; }
            public Transform RoadConnector { get; }
            public Transform Entrance { get; }
        }

        private sealed class PlacementObject
        {
            public PlacementObject(AreaSet구성패턴검토Root area,
                Transform transform, string category, Bounds bounds,
                Vector3 originalPosition, float appliedScale)
            {
                Area = area;
                Transform = transform;
                Category = category;
                Bounds = bounds;
                OriginalPosition = originalPosition;
                AppliedScale = appliedScale;
                StableKey = area.AreaRoleCode + "/" + transform.name;
            }

            public AreaSet구성패턴검토Root Area { get; }
            public Transform Transform { get; }
            public string Category { get; }
            public Bounds Bounds { get; private set; }
            public Vector3 OriginalPosition { get; }
            public float AppliedScale { get; }
            public string StableKey { get; }

            public Bounds TranslatedBounds(Vector3 candidate)
            {
                var result = Bounds;
                result.center += candidate - Transform.position;
                return result;
            }

            public void MoveTo(Vector3 position)
            {
                Bounds = TranslatedBounds(position);
                Transform.position = position;
            }
        }
    }
}
