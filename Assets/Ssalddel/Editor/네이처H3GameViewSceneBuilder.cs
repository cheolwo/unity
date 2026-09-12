using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>네이처 H3 프리팹을 실제 Game View에서 읽기 위한 비공식 검토 Scene을 만듭니다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public static class 네이처H3GameViewSceneBuilder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Scenes/NatureH3GameViewReview.unity";
        public const string RootName = "NatureH3_GameViewReview_PresentationOnly";
        public const string RiverPresentationRootName = "RiverPresentationRoot";
        public const string RiverTraversalRootName = "RiverTraversalRoot";
        public const string BridgeOpeningName = "BridgeOpening";
        public const string FordOpeningName = "FordOpening";

        private const string StarterPlayerPath =
            "Assets/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01.prefab";
        private const string NatureRoot = "Assets/Synty/PolygonNature/Prefabs/";
        private const string NatureWaterMaterialPath =
            "Assets/Synty/PolygonNature/Materials/Water/Water_01.mat";
        private const string ReviewWaterMaterialPath =
            "Assets/Ssalddel/Generated/Materials/NatureRiverReviewWater.mat";
        private const string ReviewWaterUnderlayMaterialPath =
            "Assets/Ssalddel/Generated/Materials/NatureRiverReviewUnderlay.mat";
        private const float RiverWidth = 6f;
        private const float CrossingClearance = 4.4f;

        private static readonly Vector3[] RiverControlPointValues =
        {
            new(70f, .02f, -12f),
            new(50f, .02f, 0f),
            new(32f, .02f, 9f),
            new(17f, .02f, 18f),
            new(-6f, .02f, 38f),
        };

        public static IReadOnlyList<Vector3> RiverControlPoints
            => RiverControlPointValues;

        [MenuItem("Ssalddel/검토실/네이처 공간/H3 Game View/Build Game View Review Scene")]
        public static void BuildReviewScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    네이처생활핵조우방어SceneBuilder.CompactPrefabPath) == null)
                네이처생활핵조우방어SceneBuilder.BuildCompactPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject(RootName).transform;
            var landscapePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                네이처생활핵조우방어SceneBuilder.CompactPrefabPath)
                ?? throw new InvalidOperationException("NatureH3CompactPrefabMissing");
            var landscape = PrefabUtility.InstantiatePrefab(landscapePrefab,
                root) as GameObject ?? throw new InvalidOperationException(
                "NatureH3CompactPrefabInstantiateFailed");
            landscape.name = 네이처생활핵조우방어SceneBuilder.RootName;
            var marker = landscape.GetComponent<네이처생활핵조우방어공간Root>()
                ?? throw new InvalidOperationException("NatureH3SpaceMarkerMissing");

            BuildGround(root);
            BuildBackdrop(root);
            var player = BuildPlayer(root);
            var camera = BuildCamera(root);
            BuildLighting(root);

            var home = marker.Nodes.Single(value =>
                value.NodeStableId == "h2-nature-home-core").Anchor;
            var encounter = marker.Nodes.Single(value =>
                value.NodeStableId == "h2-nature-encounter-route").Anchor;
            var defense = marker.Nodes.Single(value =>
                value.NodeStableId == "h2-nature-defense-ring").Anchor;
            var river = BuildRiver(root, home, encounter, defense);
            BuildForestZones(root, home, encounter, defense, river.Bridge,
                river.Ford);
            BuildLandmarks(root, home, encounter, defense);
            var director = root.gameObject.AddComponent<네이처H3GameViewDirector>();
            director.Configure(camera, player,
                player.GetComponent<CharacterController>(), home, encounter,
                defense, river.Bridge, river.Ford, false);

            ConfigureEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("NatureH3GameViewSceneSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateOpenScene();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("NATURE-SPACE-2 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/검토실/네이처 공간/H3 Game View/Validate Open Game View Scene")]
        public static void ValidateOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("NatureH3ReviewSceneNotOpen:" + scene.path);
            var root = scene.GetRootGameObjects().SingleOrDefault(value =>
                value.name == RootName) ?? throw new InvalidOperationException(
                "NatureH3GameViewRootMissing");
            var marker = root.GetComponentInChildren<네이처생활핵조우방어공간Root>(true)
                ?? throw new InvalidOperationException("NatureH3SpaceMarkerMissing");
            var director = root.GetComponent<네이처H3GameViewDirector>();
            if (!marker.Validate() || director == null || !director.Validate())
                throw new InvalidOperationException("NatureH3GameViewContractInvalid");
            if (root.GetComponentsInChildren<Camera>(true).Length != 1)
                throw new InvalidOperationException("NatureH3GameViewCameraCountInvalid");
            if (root.transform.Find("ReviewPlayer") == null)
                throw new InvalidOperationException("NatureH3GameViewPlayerMissing");
            ValidateRiver(root.transform);
        }

        private static void BuildGround(Transform root)
        {
            var ground = Child(root, "NatureGroundBackdrop");
            var points = new[]
            {
                new Vector3(-9f,-.45f,-10f), new Vector3(10f,-.45f,-10f),
                new Vector3(30f,-.45f,-10f), new Vector3(50f,-.45f,-10f),
                new Vector3(70f,-.45f,-10f),
                new Vector3(-9f,-.45f,10f), new Vector3(10f,-.45f,10f),
                new Vector3(30f,-.45f,10f), new Vector3(50f,-.45f,10f),
                new Vector3(70f,-.45f,10f),
                new Vector3(-4f,-.45f,29f), new Vector3(16f,-.45f,29f),
                new Vector3(36f,-.45f,29f), new Vector3(56f,-.45f,29f),
                new Vector3(76f,-.45f,29f),
                new Vector3(-4f,-.45f,48f), new Vector3(16f,-.45f,48f),
                new Vector3(36f,-.45f,48f), new Vector3(56f,-.45f,48f),
                new Vector3(76f,-.45f,48f),
            };
            for (var index = 0; index < points.Length; index++)
                NatureInstance(ground,
                    "Terrain/SM_Terrain_Ground_Mound_Large_02.prefab",
                    points[index], new Vector3(3.1f, .8f, 3.1f),
                    index * 29f, false);
        }

        private static void BuildBackdrop(Transform root)
        {
            var backdrop = Child(root, "NatureDistanceBackdrop");
            NatureInstance(backdrop, "Terrain/SM_Terrain_Mountain_01.prefab",
                new Vector3(-42f, -8f, 76f), Vector3.one * 3.2f, 20f, true);
            NatureInstance(backdrop, "Terrain/SM_Terrain_Mountain_02.prefab",
                new Vector3(24f, -10f, 94f), Vector3.one * 3.6f, -12f, true);
            NatureInstance(backdrop, "Terrain/SM_Terrain_Mountain_03.prefab",
                new Vector3(90f, -9f, 69f), Vector3.one * 3.1f, -35f, true);
            NatureInstance(backdrop, "Props/SM_Prop_Cloud_01.prefab",
                new Vector3(-18f, 36f, 62f), Vector3.one * 2.1f, 0f, true);
            NatureInstance(backdrop, "Props/SM_Prop_Cloud_02.prefab",
                new Vector3(55f, 42f, 77f), Vector3.one * 2.4f, 0f, true);
        }

        private static RiverBuildResult BuildRiver(Transform root, Transform home,
            Transform encounter, Transform defense)
        {
            var riverRoot = Child(root, "NatureRiverRoot");
            var presentation = Child(riverRoot, RiverPresentationRootName);
            var water = Child(presentation, "WaterSegments");
            var banks = Child(presentation, "RiverBanks");
            var rocks = Child(presentation, "RiverRocks");
            var vegetation = Child(presentation, "RiverVegetation");
            var effects = Child(presentation, "RiverEffects");
            var traversal = Child(riverRoot, RiverTraversalRootName);
            var blockers = Child(traversal, "RiverBlockers");

            var bridgePoint = RiverControlPointValues[3];
            var fordPoint = RiverControlPointValues[1];
            var bridgeDirection = Flatten(encounter.position - home.position).normalized;
            var fordDirection = Flatten(home.position - defense.position).normalized;
            var bridge = Anchor(traversal, BridgeOpeningName, bridgePoint,
                bridgeDirection);
            var ford = Anchor(traversal, FordOpeningName, fordPoint,
                fordDirection);
            var reviewWaterMaterial = GetOrCreateReviewWaterMaterial();
            var reviewWaterUnderlayMaterial = GetOrCreateReviewWaterUnderlayMaterial();

            for (var index = 0; index < RiverControlPointValues.Length - 1; index++)
            {
                var start = RiverControlPointValues[index];
                var end = RiverControlPointValues[index + 1];
                var waterSegment = FitLinearNatureInstance(water,
                    "Terrain/SM_River_Plane_01.prefab",
                    start + Vector3.up * .34f,
                    end + Vector3.up * .34f, 1.08f, 1.18f, false);
                waterSegment.name = $"RiverPlane_{index:00}";
                foreach (var renderer in waterSegment.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = reviewWaterMaterial;
                BuildRiverSurface(water, start, end, reviewWaterUnderlayMaterial,
                    $"RiverSurface_{index:00}");

                var direction = Flatten(end - start).normalized;
                var side = Vector3.Cross(Vector3.up, direction).normalized;
                FitLinearNatureInstance(banks,
                    "Terrain/SM_Terrain_RiverSide_01.prefab",
                    start + side * (RiverWidth * .52f),
                    end + side * (RiverWidth * .52f), 1.04f, .82f, false)
                    .name = $"RiverBank_Left_{index:00}";
                FitLinearNatureInstance(banks,
                    "Terrain/SM_Terrain_RiverSide_01.prefab",
                    end - side * (RiverWidth * .52f),
                    start - side * (RiverWidth * .52f), 1.04f, .82f, false)
                    .name = $"RiverBank_Right_{index:00}";
                BuildTraversalBlockers(blockers, start, end, bridgePoint,
                    fordPoint, index);
            }

            for (var index = 1; index < RiverControlPointValues.Length - 1; index++)
            {
                BuildRiverCornerSurface(water, RiverControlPointValues[index],
                    reviewWaterUnderlayMaterial, $"RiverSurfaceCorner_{index:00}");
                var cornerPath = index % 2 == 0
                    ? "Terrain/SM_Terrain_RiverSide_Corner_01.prefab"
                    : "Terrain/SM_Terrain_RiverSide_Corner_02.prefab";
                NatureInstance(banks, cornerPath, RiverControlPointValues[index]
                               + new Vector3(0f, -.04f, 0f),
                    Vector3.one * 1.2f, index * 53f, false)
                    .name = $"RiverBank_Corner_{index:00}";
            }

            BuildBridge(presentation, bridgePoint, bridgeDirection);
            BuildFord(rocks, effects, fordPoint, fordDirection);
            BuildRiverDetails(vegetation, rocks, effects, bridgePoint, fordPoint);
            return new RiverBuildResult(bridge, ford);
        }

        private static void BuildRiverSurface(Transform parent, Vector3 start,
            Vector3 end, Material material, string name)
        {
            var direction = Flatten(end - start);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = name;
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = (start + end) * .5f
                                              + Vector3.up * .3f;
            surface.transform.localRotation = Quaternion.LookRotation(
                direction.normalized, Vector3.up);
            surface.transform.localScale = new Vector3(
                RiverWidth * 1.08f / 10f, 1f,
                direction.magnitude * 1.08f / 10f);
            surface.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
        }

        private static void BuildRiverCornerSurface(Transform parent,
            Vector3 position, Material material, string name)
        {
            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = name;
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = position + Vector3.up * .3f;
            surface.transform.localScale = Vector3.one * (RiverWidth * .72f / 10f);
            surface.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
        }

        private static Material GetOrCreateReviewWaterMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(NatureWaterMaterialPath)
                ?? throw new InvalidOperationException("NatureReviewWaterMaterialMissing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ReviewWaterMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReviewWaterMaterialPath)!);
                material = new Material(source)
                {
                    name = "NatureRiverReviewWater",
                };
                AssetDatabase.CreateAsset(material, ReviewWaterMaterialPath);
            }

            material.shader = source.shader;
            material.SetColor("_Water_Shallow_Color",
                new Color(.16f, .56f, .66f, 1f));
            material.SetColor("_Water_Deep_Color",
                new Color(.035f, .22f, .33f, 1f));
            material.SetFloat("_Water_Opacity", .92f);
            material.SetFloat("_Water_Reflection", .28f);
            material.SetFloat("_Water_Smoothness", .74f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Material GetOrCreateReviewWaterUnderlayMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                ReviewWaterUnderlayMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException(
                        "NatureReviewWaterUnderlayShaderMissing");
                Directory.CreateDirectory(
                    Path.GetDirectoryName(ReviewWaterUnderlayMaterialPath)!);
                material = new Material(shader)
                {
                    name = "NatureRiverReviewUnderlay",
                };
                AssetDatabase.CreateAsset(material, ReviewWaterUnderlayMaterialPath);
            }

            material.SetColor("_BaseColor", new Color(.035f, .25f, .34f, 1f));
            material.SetFloat("_Smoothness", .62f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void BuildBridge(Transform presentation,
            Vector3 position, Vector3 crossingDirection)
        {
            var bridge = FitLinearNatureInstance(presentation,
                "Props/SM_Prop_Bridge_Curved_01.prefab",
                position - crossingDirection * 4.2f,
                position + crossingDirection * 4.2f, 1f, 1f, false);
            bridge.name = "NatureBridge_MainCrossing";
            bridge.transform.localPosition += Vector3.up * .38f;
            NatureInstance(presentation, "Props/SM_Prop_RoadSign_01.prefab",
                position - crossingDirection * 5.5f + Vector3.left * 1.5f,
                Vector3.one * 1.15f,
                Mathf.Atan2(crossingDirection.x, crossingDirection.z)
                * Mathf.Rad2Deg, false).name = "BridgeWayfindingSign";
        }

        private static void BuildFord(Transform rocks, Transform effects,
            Vector3 position, Vector3 crossingDirection)
        {
            for (var index = -3; index <= 3; index++)
            {
                var rockPath = index % 2 == 0
                    ? "Rocks/SM_Rock_Tile_01.prefab"
                    : "Rocks/SM_Rock_Small_01.prefab";
                NatureInstance(rocks, rockPath,
                    position + crossingDirection * (index * 1.05f)
                    + Vector3.up * .36f,
                    Vector3.one * (index % 2 == 0 ? .72f : .9f),
                    index * 41f, false).name = $"FordStone_{index + 3:00}";
            }

            NatureInstance(effects, "FX/FX_StreamParticle_Wide_01.prefab",
                position + Vector3.up * .34f, new Vector3(1.25f, 1f, 1.25f),
                0f, false).name = "FordStreamEffect";
            NatureInstance(effects, "FX/FX_Water_Ripple_01.prefab",
                position + crossingDirection * 2.1f + Vector3.up * .34f,
                Vector3.one * 1.25f, 0f, false).name = "FordRippleEffect";
        }

        private static void BuildRiverDetails(Transform vegetation,
            Transform rocks, Transform effects, Vector3 bridge, Vector3 ford)
        {
            var detailPoints = new[]
            {
                new Vector3(62f,0f,-4f), new Vector3(56f,0f,5f),
                new Vector3(42f,0f,3f), new Vector3(39f,0f,13f),
                new Vector3(27f,0f,7f), new Vector3(24f,0f,20f),
                new Vector3(7f,0f,21f), new Vector3(0f,0f,35f),
            };
            for (var index = 0; index < detailPoints.Length; index++)
            {
                var plant = index % 2 == 0
                    ? "Plants/SM_Plant_Reeds_01.prefab"
                    : "Plants/SM_Plant_Fern_02.prefab";
                NatureInstance(vegetation, plant, detailPoints[index],
                    Vector3.one * (1f + index % 3 * .16f), index * 43f, false)
                    .name = $"RiverDetailPlant_{index:00}";
                if (index % 2 != 0) continue;
                NatureInstance(rocks, "Rocks/SM_Rock_Small_02.prefab",
                    detailPoints[index] + new Vector3(1.4f, 0f, -.8f),
                    Vector3.one * .78f, index * 61f, false)
                    .name = $"RiverDetailRock_{index:00}";
            }

            NatureInstance(effects, "FX/FX_Water_Ripple_01.prefab",
                bridge + new Vector3(2.2f, .08f, -1.2f), Vector3.one, 0f,
                false).name = "BridgeRippleEffect";
            NatureInstance(effects, "FX/FX_StreamParticle_Small_01.prefab",
                ford + new Vector3(-1.4f, .08f, 1.1f), Vector3.one, 25f,
                false).name = "FordStreamDetailEffect";
        }

        private static void BuildTraversalBlockers(Transform parent,
            Vector3 start, Vector3 end, Vector3 bridge, Vector3 ford,
            int segmentIndex)
        {
            var flatStart = Flatten(start);
            var flatEnd = Flatten(end);
            var length = Vector3.Distance(flatStart, flatEnd);
            var direction = (flatEnd - flatStart).normalized;
            var cellCount = Mathf.Max(1, Mathf.CeilToInt(length / 2.25f));
            var cellLength = length / cellCount;
            for (var cell = 0; cell < cellCount; cell++)
            {
                var position = Vector3.Lerp(flatStart, flatEnd,
                    (cell + .5f) / cellCount);
                if (DistanceXZ(position, bridge) < CrossingClearance
                    || DistanceXZ(position, ford) < CrossingClearance)
                    continue;
                var blocker = new GameObject(
                    $"RiverBlocker_{segmentIndex:00}_{cell:00}");
                blocker.transform.SetParent(parent, false);
                blocker.transform.localPosition = position + Vector3.up * 1.05f;
                blocker.transform.localRotation = Quaternion.LookRotation(direction,
                    Vector3.up);
                var collider = blocker.AddComponent<BoxCollider>();
                collider.size = new Vector3(RiverWidth, 2.1f,
                    cellLength + .35f);
            }
        }

        private static void BuildForestZones(Transform root, Transform home,
            Transform encounter, Transform defense, Transform bridge,
            Transform ford)
        {
            var forest = Child(root, "NatureForestZones");
            var outer = Child(forest, "OuterForestZone");
            var riverBank = Child(forest, "RiverBankZone");
            var plants = Child(forest, "ForestUnderstoryZone");
            var rocks = Child(forest, "ForestRockZone");
            var reserved = new[]
            {
                (home.position, 7f), (encounter.position, 7f),
                (defense.position, 8f), (bridge.position, 6f),
                (ford.position, 6f),
            };
            var gameplayRoutes = new[]
            {
                new[] { home.position, bridge.position, encounter.position },
                new[] { encounter.position, defense.position },
                new[] { defense.position, ford.position, home.position },
            };

            var outerPoints = GenerateZonePoints(24, 31017,
                point => IsOuterForest(point), reserved, gameplayRoutes, 3.8f);
            var bankPoints = GenerateZonePoints(8, 31018,
                point => IsRiverBank(point), reserved, gameplayRoutes, 4.2f);
            var plantPoints = GenerateZonePoints(20, 31019,
                point => IsRiverBank(point) || IsOuterForest(point), reserved,
                gameplayRoutes, 1.7f);
            var rockPoints = GenerateZonePoints(16, 31020,
                point => IsRiverBank(point) || IsOuterForest(point), reserved,
                gameplayRoutes, 2.2f);

            var outerTreePaths = new[]
            {
                "Trees/SM_Tree_Pine_01.prefab",
                "Trees/SM_Tree_Pine_02.prefab",
                "Trees/SM_Tree_Birch_01.prefab",
                "Trees/SM_Tree_Round_02.prefab",
            };
            for (var index = 0; index < outerPoints.Count; index++)
                NatureInstance(outer, outerTreePaths[index % outerTreePaths.Length],
                    outerPoints[index], Vector3.one * (1.05f + index % 5 * .11f),
                    (index * 73f + 19f) % 360f, false)
                    .name = $"OuterForestTree_{index:00}";

            var willowPaths = new[]
            {
                "Trees/SM_Tree_Willow_Medium_01.prefab",
                "Trees/SM_Tree_Willow_Small_01.prefab",
            };
            for (var index = 0; index < bankPoints.Count; index++)
                NatureInstance(riverBank, willowPaths[index % willowPaths.Length],
                    bankPoints[index], Vector3.one * (1f + index % 3 * .12f),
                    (index * 59f + 11f) % 360f, false)
                    .name = $"RiverBankTree_{index:00}";

            var plantPaths = new[]
            {
                "Plants/SM_Plant_Fern_01.prefab",
                "Plants/SM_Plant_Fern_03.prefab",
                "Plants/SM_Plant_Bush_02.prefab",
                "Plants/SM_Plant_Undergrowth_01.prefab",
            };
            for (var index = 0; index < plantPoints.Count; index++)
                NatureInstance(plants, plantPaths[index % plantPaths.Length],
                    plantPoints[index], Vector3.one * (.9f + index % 4 * .1f),
                    (index * 47f + 7f) % 360f, false)
                    .name = $"ForestUnderstory_{index:00}";

            var rockPaths = new[]
            {
                "Rocks/SM_Rock_01.prefab", "Rocks/SM_Rock_02.prefab",
                "Rocks/SM_Rock_Pile_01.prefab",
                "Rocks/SM_Rock_Small_02.prefab",
            };
            for (var index = 0; index < rockPoints.Count; index++)
                NatureInstance(rocks, rockPaths[index % rockPaths.Length],
                    rockPoints[index], Vector3.one * (.68f + index % 4 * .09f),
                    (index * 83f + 31f) % 360f, false)
                    .name = $"ForestRock_{index:00}";
        }

        private static List<Vector3> GenerateZonePoints(int count, int seed,
            Func<Vector3, bool> zonePredicate,
            IReadOnlyList<(Vector3 Center, float Radius)> reserved,
            IReadOnlyList<Vector3[]> gameplayRoutes, float minimumSpacing)
        {
            var points = new List<Vector3>(count);
            var random = new System.Random(seed);
            for (var attempt = 0; attempt < count * 300 && points.Count < count;
                 attempt++)
            {
                var point = new Vector3(
                    Mathf.Lerp(-12f, 72f, (float)random.NextDouble()), 0f,
                    Mathf.Lerp(-14f, 42f, (float)random.NextDouble()));
                if (!zonePredicate(point)) continue;
                if (reserved.Any(value => DistanceXZ(point, value.Center)
                                          < value.Radius)) continue;
                if (gameplayRoutes.Any(route => DistanceToPolyline(point, route)
                                                < 3f)) continue;
                if (points.Any(value => DistanceXZ(point, value)
                                        < minimumSpacing)) continue;
                points.Add(point);
            }

            if (points.Count != count)
                throw new InvalidOperationException(
                    $"NatureForestZoneCapacityInsufficient:{points.Count}/{count}");
            return points;
        }

        private static bool IsOuterForest(Vector3 point)
            => point.x < -1f || point.x > 58f || point.z < -6f || point.z > 31f;

        private static bool IsRiverBank(Vector3 point)
        {
            var distance = DistanceToPolyline(point, RiverControlPointValues);
            return distance >= RiverWidth * .62f && distance <= RiverWidth * 1.45f;
        }

        private static float DistanceToPolyline(Vector3 point,
            IReadOnlyList<Vector3> line)
        {
            var best = float.MaxValue;
            for (var index = 0; index < line.Count - 1; index++)
                best = Mathf.Min(best,
                    DistanceToSegmentXZ(point, line[index], line[index + 1]));
            return best;
        }

        private static float DistanceToSegmentXZ(Vector3 point, Vector3 start,
            Vector3 end)
        {
            var flatPoint = Flatten(point);
            var flatStart = Flatten(start);
            var line = Flatten(end - start);
            var denominator = line.sqrMagnitude;
            if (denominator < .0001f) return Vector3.Distance(flatPoint, flatStart);
            var t = Mathf.Clamp01(Vector3.Dot(flatPoint - flatStart, line)
                                  / denominator);
            return Vector3.Distance(flatPoint, flatStart + line * t);
        }

        private static float DistanceXZ(Vector3 left, Vector3 right)
            => Vector3.Distance(Flatten(left), Flatten(right));

        private static Vector3 Flatten(Vector3 value)
            => new(value.x, 0f, value.z);

        private static void BuildLandmarks(Transform root, Transform home,
            Transform encounter, Transform defense)
        {
            var landmarks = Child(root, "NatureH3ReadableLandmarks");

            // 생활핵은 불빛과 머무를 자리를 중심으로 읽히게 한다.
            NatureInstance(landmarks, "Props/SM_Prop_CampFire_01.prefab",
                home.position + new Vector3(0f, .08f, 0f), Vector3.one * 1.35f,
                0f, true);
            NatureInstance(landmarks, "Trees/SM_Tree_Log_01.prefab",
                home.position + new Vector3(-2.8f, .05f, 1.4f),
                Vector3.one * 1.25f, 28f, true);
            NatureInstance(landmarks, "Trees/SM_Tree_Log_02.prefab",
                home.position + new Vector3(2.6f, .05f, 1.6f),
                Vector3.one * 1.1f, -24f, true);
            NatureInstance(landmarks, "FX/FX_Fireflies_01.prefab",
                home.position + new Vector3(0f, 1.2f, 0f), Vector3.one * 1.4f,
                0f, false);

            // 조우로는 표지와 문을 지나며 위험 쪽으로 좁아지는 통로다.
            NatureInstance(landmarks, "Props/SM_Prop_RoadSign_01.prefab",
                encounter.position + new Vector3(-5.5f, 0f, -1.4f),
                Vector3.one * 1.35f, 35f, true);
            NatureInstance(landmarks,
                "Props/SM_Prop_Pillar_Arch_Broken_Moss_01.prefab",
                encounter.position + new Vector3(1.5f, 0f, 2.4f),
                Vector3.one * 1.45f, 4f, true);
            NatureInstance(landmarks, "Rocks/SM_Rock_Pile_02.prefab",
                encounter.position + new Vector3(6.5f, 0f, -1.6f),
                Vector3.one * 1.05f, 18f, true);

            // 방어환은 실제로 둘러싸인 장소처럼 보이도록 낮은 석벽과 불빛을 둔다.
            for (var index = 0; index < 10; index++)
            {
                var angle = index * Mathf.PI * 2f / 10f;
                var offset = new Vector3(Mathf.Cos(angle) * 5.6f, 0f,
                    Mathf.Sin(angle) * 5.6f);
                var rockPath = index % 2 == 0
                    ? "Rocks/SM_Rock_01.prefab"
                    : "Rocks/SM_Rock_02.prefab";
                NatureInstance(landmarks, rockPath, defense.position + offset,
                    Vector3.one * .85f, index * 37f, true);
            }
            NatureInstance(landmarks, "Props/SM_Prop_TorchStick_01.prefab",
                defense.position + new Vector3(-3.8f, 0f, -3.8f),
                Vector3.one * 1.25f, 0f, true);
            NatureInstance(landmarks, "Props/SM_Prop_TorchStick_01.prefab",
                defense.position + new Vector3(3.8f, 0f, -3.8f),
                Vector3.one * 1.25f, 0f, true);
        }

        private static Transform BuildPlayer(Transform root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarterPlayerPath)
                ?? throw new InvalidOperationException("NatureReviewPlayerPrefabMissing");
            var player = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject
                ?? throw new InvalidOperationException("NatureReviewPlayerInstantiateFailed");
            player.name = "ReviewPlayer";
            player.transform.localPosition = new Vector3(0f, .15f, -2f);
            player.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            foreach (var existing in player.GetComponentsInChildren<Collider>(true))
                existing.enabled = false;
            var controller = player.GetComponent<CharacterController>();
            if (controller == null)
                controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = .38f;
            controller.stepOffset = .28f;
            controller.slopeLimit = 48f;
            return player.transform;
        }

        private static Camera BuildCamera(Transform root)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            camera.nearClipPlane = .15f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void BuildLighting(Transform root)
        {
            var sunObject = new GameObject("Sunset Directional Light");
            sunObject.transform.SetParent(root, false);
            sunObject.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .83f, .67f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.44f, .58f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.32f, .39f, .34f);
            RenderSettings.ambientGroundColor = new Color(.17f, .2f, .16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.53f, .62f, .64f);
            RenderSettings.fogStartDistance = 75f;
            RenderSettings.fogEndDistance = 210f;
        }

        private static GameObject FitLinearNatureInstance(Transform parent,
            string relativePath, Vector3 start, Vector3 end, float overlap,
            float widthScale, bool keepColliders)
        {
            var value = NatureInstance(parent, relativePath, Vector3.zero,
                Vector3.one, 0f, keepColliders);
            var renderers = value.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    "NatureReviewLinearPrefabRendererMissing:" + relativePath);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            var direction = Flatten(end - start);
            var targetLength = Mathf.Max(.1f, direction.magnitude * overlap);
            var longAxisIsX = bounds.size.x >= bounds.size.z;
            var sourceLength = Mathf.Max(.1f,
                longAxisIsX ? bounds.size.x : bounds.size.z);
            var scale = Vector3.one;
            if (longAxisIsX)
            {
                scale.x = targetLength / sourceLength;
                scale.z = widthScale;
                value.transform.localRotation = Quaternion.FromToRotation(
                    Vector3.right, direction.normalized);
            }
            else
            {
                scale.x = widthScale;
                scale.z = targetLength / sourceLength;
                value.transform.localRotation = Quaternion.LookRotation(
                    direction.normalized, Vector3.up);
            }
            value.transform.localScale = scale;
            value.transform.localPosition = (start + end) * .5f;
            return value;
        }

        private static Transform Anchor(Transform parent, string name,
            Vector3 position, Vector3 forward)
        {
            var anchor = Child(parent, name);
            anchor.localPosition = position;
            if (forward.sqrMagnitude > .001f)
                anchor.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            return anchor;
        }

        private static void ValidateRiver(Transform sceneRoot)
        {
            var presentation = sceneRoot.Find(
                "NatureRiverRoot/" + RiverPresentationRootName)
                ?? throw new InvalidOperationException(
                    "NatureRiverPresentationRootMissing");
            var traversal = sceneRoot.Find(
                "NatureRiverRoot/" + RiverTraversalRootName)
                ?? throw new InvalidOperationException(
                    "NatureRiverTraversalRootMissing");
            if (presentation.GetComponentsInChildren<Collider>(true)
                .Any(value => value.enabled))
                throw new InvalidOperationException(
                    "NatureRiverPresentationColliderMustBeDisabled");
            if (traversal.GetComponentsInChildren<Renderer>(true).Length != 0)
                throw new InvalidOperationException(
                    "NatureRiverTraversalRendererForbidden");
            var waterRenderers = presentation.GetComponentsInChildren<Renderer>(true)
                .Where(value => value.transform.name.StartsWith("RiverPlane_",
                    StringComparison.Ordinal)).ToArray();
            if (waterRenderers.Length != RiverControlPointValues.Length - 1)
                throw new InvalidOperationException(
                    "NatureRiverWaterSegmentCountInvalid");
            foreach (var renderer in waterRenderers)
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null
                    || material.shader.name == "Hidden/InternalErrorShader")
                    throw new InvalidOperationException(
                        "NatureRiverWaterShaderInvalid");
            if (traversal.Find(BridgeOpeningName) == null
                || traversal.Find(FordOpeningName) == null)
                throw new InvalidOperationException(
                    "NatureRiverCrossingOpeningMissing");
            if (traversal.Find("RiverBlockers")?
                    .GetComponentsInChildren<BoxCollider>(true).Length < 8)
                throw new InvalidOperationException(
                    "NatureRiverTraversalBlockersInsufficient");
        }

        private static GameObject NatureInstance(Transform parent,
            string relativePath, Vector3 position, Vector3 scale, float yaw,
            bool keepColliders)
        {
            var path = NatureRoot + relativePath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("NatureReviewPrefabMissing:" + path);
            var value = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject
                ?? throw new InvalidOperationException("NatureReviewPrefabInstantiateFailed:" + path);
            value.transform.localPosition = position;
            value.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            value.transform.localScale = scale;
            if (!keepColliders)
                foreach (var collider in value.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            return value;
        }

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private sealed class RiverBuildResult
        {
            public RiverBuildResult(Transform bridge, Transform ford)
            {
                Bridge = bridge;
                Ford = ford;
            }

            public Transform Bridge { get; }
            public Transform Ford { get; }
        }
    }
}
