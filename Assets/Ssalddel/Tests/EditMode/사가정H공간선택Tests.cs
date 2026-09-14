using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정H공간선택Tests
    {
        private static TextAsset MapAsset => Resources.Load<TextAsset>("SagajeongReference");

        [Test]
        public void 동결자료를_검증하고_602개참고건물을_표현전용으로색인한다()
        {
            var asset = MapAsset;
            var map = JsonUtility.FromJson<공간참고자료>(asset.text);
            var layer = 사가정H공간선택Layer.Create(map, asset.bytes);

            Assert.That(layer.SourceRevision, Is.EqualTo(사가정H공간선택Layer.ExpectedMapRevision));
            Assert.That(layer.SourceSha256, Is.EqualTo(사가정H공간선택Layer.ExpectedMapSha256));
            Assert.That(layer.BuildingCount, Is.EqualTo(602));
            Assert.That(layer.RoleBoundBuildingCount, Is.EqualTo(3));
            Assert.That(layer.ExplicitRoleCount, Is.EqualTo(3));
            Assert.That(layer.CandidateOnly, Is.True);
            Assert.That(layer.ActualE5, Is.False);
            Assert.That(layer.TraversalReady, Is.False);
            Assert.That(layer.GameplayReady, Is.False);
            Assert.That(layer.ServerProjectionReady, Is.False);
        }

        [Test]
        public void 음식점_창고_주거전달을_H와분리된역할Profile로명시결속한다()
        {
            var layer = ActualLayer();

            Assert.That(layer.TryGetByStableId("osm:way:1256772531", out var restaurant), Is.True);
            Assert.That(restaurant.RoleProfiles.Count, Is.EqualTo(1));
            Assert.That(restaurant.RoleProfiles[0].RoleCode,
                Is.EqualTo(사가정건물RoleCodes.Restaurant));
            Assert.That(restaurant.RoleProfiles[0].SemanticPlaceStableId,
                Is.EqualTo("synthetic-place:food-route-a"));
            Assert.That(restaurant.RoleProfiles[0].H1ActionStableIds,
                Is.EqualTo(new[] { "h1-stock:town-order-packing" }));
            Assert.That(restaurant.RoleProfiles[0].IsSyntheticFixture, Is.True);
            Assert.That(restaurant.RoleProfiles[0].IsPublicBusinessFact, Is.False);

            Assert.That(layer.TryGetByStableId("osm:way:470492219", out var warehouse), Is.True);
            Assert.That(warehouse.RoleProfiles.Single().RoleCode,
                Is.EqualTo(사가정건물RoleCodes.Warehouse));
            Assert.That(warehouse.RoleProfiles.Single().SemanticPlaceStableId,
                Is.EqualTo("synthetic-place:warehouse-a"));
            Assert.That(warehouse.RoleProfiles.Single().H1ActionStableIds,
                Is.EqualTo(new[]
                {
                    "h1-stock:hub-receiving-storage",
                    "h1-stock:hub-outbound-staging"
                }));
            Assert.That(warehouse.H1BindingStatus,
                Is.EqualTo(사가정H공간선택Layer.WaitingForGraphMapBinding));
            Assert.That(layer.TrySelectLocalPoint(new Vector2(361.94f, 319.89f),
                out var warehouseAtSemanticPoint), Is.True);
            Assert.That(warehouseAtSemanticPoint.BuildingStableId,
                Is.EqualTo("osm:way:470492219"));

            Assert.That(layer.TryGetByStableId("osm:way:1256772606", out var residence), Is.True);
            Assert.That(residence.RoleProfiles.Single().RoleCode,
                Is.EqualTo(사가정건물RoleCodes.ResidentialDelivery));
            Assert.That(residence.RoleProfiles.Single().SemanticPlaceStableId,
                Is.EqualTo("synthetic-place:food-route-b"));

            Assert.That(layer.TryGetByStableId("osm:way:1473893430", out var general), Is.True);
            Assert.That(general.RoleProfiles.Single().RoleCode,
                Is.EqualTo(사가정건물RoleCodes.GeneralUnbound));
            Assert.That(general.RoleProfiles.Single().HasFollowUp, Is.False);
        }

        [Test]
        public void 역할별후속조작은_준비된경로만선택하고_항상읽기전용이다()
        {
            var restaurant = 사가정건물RoleCatalog.Resolve("osm:way:1256772531").Single();
            Assert.That(사가정건물RoleFollowUpResolver.TryResolve(
                restaurant, Array.Empty<string>(), out var waiting), Is.True);
            Assert.That(waiting.StatusCode, Is.EqualTo("WaitingForReadOnlyRouteSnapshot"));
            Assert.That(waiting.RouteSnapshotAvailable, Is.False);
            Assert.That(waiting.ReadOnly, Is.True);
            Assert.That(waiting.ChangesAuthorityState, Is.False);

            Assert.That(사가정건물RoleFollowUpResolver.TryResolve(restaurant,
                new[] { 사가정건물RoleCatalog.FoodDeliveryRouteStableId },
                out var ready), Is.True);
            Assert.That(ready.StatusCode, Is.EqualTo("ReadOnlyRouteSnapshotSelected"));
            Assert.That(ready.RouteSnapshotAvailable, Is.True);

            var warehouse = 사가정건물RoleCatalog.Resolve("osm:way:470492219").Single();
            Assert.That(사가정건물RoleFollowUpResolver.TryResolve(
                warehouse, Array.Empty<string>(), out var preview), Is.True);
            Assert.That(preview.StatusCode, Is.EqualTo("SyntheticLifecyclePreviewOnly"));

            var general = 사가정건물RoleCatalog.Resolve("osm:way:1473893430").Single();
            Assert.That(사가정건물RoleFollowUpResolver.TryResolve(
                general, Array.Empty<string>(), out _), Is.False);
        }

        [Test]
        public void 동결자료의_판본이나_hash가다르면_색인을거절한다()
        {
            var asset = MapAsset;
            var map = JsonUtility.FromJson<공간참고자료>(asset.text);
            map.revision = "sagajeong-reference.stale";
            var revisionError = Assert.Throws<ArgumentException>(() =>
                사가정H공간선택Layer.Create(map, asset.bytes));
            Assert.That(revisionError.Message, Does.Contain("MapRevisionMismatch"));

            map = JsonUtility.FromJson<공간참고자료>(asset.text);
            var corrupted = asset.bytes.Concat(new byte[] { 10 }).ToArray();
            var hashError = Assert.Throws<ArgumentException>(() =>
                사가정H공간선택Layer.Create(map, corrupted));
            Assert.That(hashError.Message, Does.Contain("MapHashMismatch"));
        }

        [Test]
        public void 명시된두건만_H1후보에결속하고_나머지는GraphMap을기다린다()
        {
            var layer = ActualLayer();

            Assert.That(layer.TryGetByStableId("osm:way:1256772531", out var pickup), Is.True);
            Assert.That(pickup.SourceAddress, Is.EqualTo("사가정로 395"));
            Assert.That(pickup.H1StableId, Is.EqualTo("h1-stock:town-order-packing"));
            Assert.That(pickup.H1BindingStatus, Is.EqualTo(사가정H공간선택Layer.CandidateBinding));
            Assert.That(pickup.H2StableIds, Is.EqualTo(new[]
                { "h2-candidate:town-order-fulfillment", "h2-candidate:market-life-commerce" }));
            Assert.That(pickup.H3StableId, Is.EqualTo("h3-candidate:town-market-fulfillment"));
            Assert.That(pickup.H3RoleCode, Is.EqualTo("MarketFulfillment"));
            Assert.That(pickup.TheoryAreaSetRef, Is.EqualTo("area-set:theory:lowrise-market-region"));
            Assert.That(pickup.CompositionPatternRef, Is.EqualTo("area-set-composition:town:lowrise-life-market"));
            Assert.That(pickup.CompositionStateCode, Is.EqualTo("PartialCandidate"));
            Assert.That(pickup.AreaSetStableId, Is.Empty);
            Assert.That(pickup.SatisfiedRoleCodes, Is.EqualTo(new[] { "ResidentService", "MarketFulfillment" }));
            Assert.That(pickup.MissingRoleCodes,
                Is.EqualTo(new[] { "LowriseMarket", "CircularMarket", "ContaminationRelief" }));
            Assert.That(pickup.ServerProjectionReady, Is.False);

            Assert.That(layer.TryGetByStableId("osm:way:1256772606", out var receipt), Is.True);
            Assert.That(receipt.SourceAddress, Is.EqualTo("면목로44가길 7"));
            Assert.That(receipt.H1StableId, Is.EqualTo("h1-stock:town-resident-pickup"));
            Assert.That(receipt.H2StableIds,
                Is.EqualTo(new[] { "h2-candidate:lowrise-residential", "h2-candidate:town-residential-alley" }));
            Assert.That(receipt.H3StableId, Is.EqualTo("h3-candidate:town-resident-service-loop"));
            Assert.That(receipt.H3RoleCode, Is.EqualTo("ResidentService"));

            Assert.That(layer.TryGetByStableId("osm:way:1473893430", out var unbound), Is.True);
            Assert.That(unbound.SourceName, Is.EqualTo("칸스빌"));
            Assert.That(unbound.H1StableId, Is.Empty);
            Assert.That(unbound.H2StableIds, Is.Empty);
            Assert.That(unbound.H3StableId, Is.Empty);
            Assert.That(unbound.H1BindingStatus, Is.EqualTo(사가정H공간선택Layer.WaitingForGraphMapBinding));
            Assert.That(unbound.H4AreaSetBindingStatus, Is.EqualTo(사가정H공간선택Layer.WaitingForGraphMapBinding));
        }

        [Test]
        public void 겹친윤곽은_작은면적을먼저고르고_동률은StableId순으로고른다()
        {
            var layer = FixtureLayer(
                Building("z-large", 0, 0, 10, 10, 5),
                Building("z-small", 2, 2, 8, 8, 5),
                Building("a-small", 2, 2, 8, 8, 5));

            Assert.That(layer.TrySelectLocalPoint(new Vector2(5, 5), out var selected), Is.True);
            Assert.That(selected.BuildingStableId, Is.EqualTo("a-small"));
            Assert.That(layer.Selected, Is.SameAs(selected));
            layer.ClearSelection();
            Assert.That(layer.HasSelection, Is.False);
        }

        [Test]
        public void 윤곽경계는_건물을선택하고_외부점은_선택을해제한다()
        {
            var layer = FixtureLayer(Building("building:test", 1, 2, 9, 10, 7));

            Assert.That(layer.TrySelectLocalPoint(new Vector2(1, 6), out var boundary), Is.True);
            Assert.That(boundary.BuildingStableId, Is.EqualTo("building:test"));
            Assert.That(layer.TrySelectLocalPoint(new Vector2(10, 6), out var outside), Is.False);
            Assert.That(outside, Is.Null);
            Assert.That(layer.HasSelection, Is.False);
        }

        [Test]
        public void 카메라Ray와_LocalXZ조회가_같은건물을선택한다()
        {
            var layer = FixtureLayer(Building("building:test", 1, 2, 9, 10, 7));
            var root = new GameObject("map-root");
            try
            {
                root.transform.position = new Vector3(20, 3, -10);
                Assert.That(layer.TrySelectLocalPoint(new Vector2(5, 6), out var pointResult), Is.True);
                var worldTop = root.transform.TransformPoint(new Vector3(5, 7, 6));
                var ray = new Ray(worldTop + Vector3.up * 20, Vector3.down);
                Assert.That(layer.TrySelectWorldRay(ray, root.transform, out var rayResult), Is.True);
                Assert.That(rayResult.BuildingStableId, Is.EqualTo(pointResult.BuildingStableId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 높이보완을쓰는표현은_원본보다높은옆면에서도선택된다()
        {
            var building = Building("building:height-overlay", 1, 2, 9, 10, 5);
            building.heightKind = "SymbolicHeight";
            var map = new 공간참고자료
            {
                revision = "fixture.r1", offsetX = 5, offsetZ = 5, halfExtent = 10,
                roads = Array.Empty<참고도로>(), buildings = new[] { building },
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(map));
            using var sha = SHA256.Create();
            var hash = string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("X2")));
            var heights = 건물높이보완조회.Validate(new 건물높이보완자료
            {
                schema = "sagajeong-building-height-overlay.v1",
                revision = "fixture-height.r1",
                mapRevision = map.revision,
                mapHash = hash,
                sourceUrl = "https://www.data.go.kr/data/15134735/openapi.do",
                presentationOnly = true,
                buildings = new[]
                {
                    new 건물높이보완항목
                    {
                        id = building.id, street = building.street,
                        houseNumber = building.houseNumber, heightMeters = 17,
                        matchingStatus = "RoadAndParcelAgreeCandidate",
                        sourceHash = new string('A', 64), recordCreationDate = "20260914",
                    },
                },
            }, map, bytes);
            var layer = 사가정H공간선택Layer.Create(map, bytes, map.revision, hash);
            var displayedLayer = 사가정H공간선택Layer.Create(
                map, bytes, map.revision, hash, heights, null);

            var root = new GameObject("height-overlay-root");
            try
            {
                Assert.That(layer.TrySelectWorldRay(
                    new Ray(new Vector3(-5, 10, 6), Vector3.right), root.transform, out _), Is.False);
                Assert.That(displayedLayer.TrySelectWorldRay(
                    new Ray(new Vector3(-5, 10, 6), Vector3.right), root.transform, out var selected), Is.True);
                Assert.That(selected.SourceHeight, Is.EqualTo(5));
                Assert.That(selected.DisplayedHeight, Is.EqualTo(17));
                Assert.That(selected.DisplayedHeightKind, Is.EqualTo("BuildingHeightOverlay"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View선택은_기존표현객체를늘리지않고_전체보기와닫기에서해제된다()
        {
            var root = new GameObject("sagajeong-h-selection-view-test");
            try
            {
                var view = root.AddComponent<사가정운영디오라마View>();
                view.Initialize();
                var rendererCount = root.GetComponentsInChildren<Renderer>(true).Length;
                var materialCount = root.GetComponentsInChildren<Renderer>(true)
                    .Select(value => value.sharedMaterial).Where(value => value != null).Distinct().Count();

                Assert.That(view.HSelectionStatus, Does.Contain("건물 선택 602"));
                Assert.That(view.HSelectionStatus, Does.Contain("역할 후보 3"));
                Assert.That(view.HSelectionBuildingCount, Is.EqualTo(602));
                Assert.That(view.SelectBuilding("osm:way:1256772531"), Is.True);
                Assert.That(view.SelectedBuilding.BuildingStableId, Is.EqualTo("osm:way:1256772531"));
                Assert.That(view.SelectedBuildingRole.RoleCode,
                    Is.EqualTo(사가정건물RoleCodes.Restaurant));
                Assert.That(view.OpenSelectedBuildingRoleFollowUp(), Is.True);
                Assert.That(view.BuildingRoleFollowUpVisible, Is.True);
                Assert.That(view.BuildingRoleFollowUpStatus,
                    Is.EqualTo("WaitingForReadOnlyRouteSnapshot"));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(rendererCount));
                Assert.That(root.GetComponentsInChildren<Renderer>(true)
                    .Select(value => value.sharedMaterial).Where(value => value != null).Distinct().Count(),
                    Is.EqualTo(materialCount));

                view.ShowWhole();
                Assert.That(view.SelectedBuilding, Is.Null);
                Assert.That(view.BuildingRoleFollowUpVisible, Is.False);
                Assert.That(view.SelectBuilding("osm:way:1256772606", true), Is.True);
                Assert.That(view.SelectedBuilding.BuildingStableId, Is.EqualTo("osm:way:1256772606"));
                view.Show(false);
                Assert.That(view.SelectedBuilding, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static 사가정H공간선택Layer ActualLayer()
        {
            var asset = MapAsset;
            return 사가정H공간선택Layer.Create(
                JsonUtility.FromJson<공간참고자료>(asset.text), asset.bytes);
        }

        private static 사가정H공간선택Layer FixtureLayer(params 참고건물[] buildings)
        {
            var map = new 공간참고자료
            {
                revision = "fixture.r1",
                offsetX = 5,
                offsetZ = 5,
                halfExtent = 10,
                roads = Array.Empty<참고도로>(),
                buildings = buildings,
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(map));
            using var sha = SHA256.Create();
            var hash = string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("X2")));
            return 사가정H공간선택Layer.Create(map, bytes, map.revision, hash);
        }

        private static 참고건물 Building(string id, float minX, float minZ, float maxX, float maxZ, float height)
            => new()
            {
                id = id,
                street = "검증로",
                houseNumber = "1",
                buildingKind = "test",
                heightKind = "Fixture",
                version = 1,
                height = height,
                points = new[]
                {
                    new 참고점 { x = minX, z = minZ }, new 참고점 { x = maxX, z = minZ },
                    new 참고점 { x = maxX, z = maxZ }, new 참고점 { x = minX, z = maxZ },
                    new 참고점 { x = minX, z = minZ },
                },
            };
    }
}
