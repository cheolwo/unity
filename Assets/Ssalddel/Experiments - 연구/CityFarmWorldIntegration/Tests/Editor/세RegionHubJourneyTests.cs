using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class 세RegionHubJourneyTests
    {
        [SetUp]
        public void OpenScene()
            => EditorSceneManager.OpenScene(세RegionHubJourneyBuilder.ScenePath, OpenSceneMode.Single);

        [Test]
        public void FarmTownCity와Hub는_독립Anchor넷으로조립된다()
        {
            var map = Map();
            Assert.That(map.RegionAndHubAnchors.Count, Is.EqualTo(4));
            Assert.That(map.RegionAndHubAnchors.Select(value => value.Descriptor.PackCode),
                Is.EquivalentTo(new[]
                {
                    월드CompositionPackCodes.Farm,
                    월드CompositionPackCodes.Town,
                    월드CompositionPackCodes.City,
                    월드CompositionPackCodes.RegionalLogisticsHub,
                }));
            Assert.That(map.RegionAndHubAnchors.Select(value => value.transform.position)
                .Distinct().Count(), Is.EqualTo(4));
            Assert.That(map.RegionAndHubAnchors,
                Has.All.Matches<거점CompositionSetView>(value =>
                    value.ValidateWiring() && PrefabUtility.IsPartOfPrefabInstance(value)));
        }

        [Test]
        public void 세Region은_서로약삼백미터완충거리를가진다()
        {
            var anchors = Map().RegionAndHubAnchors;
            var farm = anchors.Single(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.Farm);
            var town = anchors.Single(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.Town);
            var city = anchors.Single(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.City);
            var hub = anchors.Single(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.RegionalLogisticsHub);

            Assert.That(Vector3.Distance(farm.transform.position, town.transform.position),
                Is.InRange(280f, 310f));
            Assert.That(Vector3.Distance(farm.transform.position, hub.transform.position),
                Is.GreaterThanOrEqualTo(330f));
            Assert.That(Vector3.Distance(town.transform.position, hub.transform.position),
                Is.GreaterThanOrEqualTo(350f));
            Assert.That(Vector3.Distance(hub.transform.position, city.transform.position),
                Is.InRange(280f, 300f));
        }

        [Test]
        public void 사람경계넷과_화물Gate여섯은_CMP3signature를보존한다()
        {
            var gates = Map().BoundaryAndFreightGates;
            Assert.That(gates.Count, Is.EqualTo(10));
            var external = gates.SelectMany(value => value.Descriptor.Connectors)
                .Where(value => value.ExpansionSocket).ToArray();
            Assert.That(external.Count(value =>
                value.RouteSignature.StartsWith("boundary.", StringComparison.Ordinal)), Is.EqualTo(8));
            Assert.That(external.Count(value =>
                value.RouteSignature.StartsWith("freight.", StringComparison.Ordinal)), Is.EqualTo(6));
            Assert.That(external.GroupBy(value => value.RouteSignature)
                .All(group => group.Count() == 2), Is.True);
        }

        [Test]
        public void 기존감자CargoIdentity와_여섯Lineage를_Hub보관까지재사용한다()
        {
            var farm = Map().CargoJourneys.Single(value =>
                value.OriginRegionCode == 월드CompositionPackCodes.Farm);
            Assert.That(farm.CargoStableId,
                Is.EqualTo(세RegionHubJourneyBuilder.ExistingFarmCargoStableId));
            Assert.That(farm.ProductStableId, Is.EqualTo("product:potato"));
            Assert.That(farm.CurrentStageCode, Is.EqualTo(화물JourneyStageCodes.HubStored));
            Assert.That(farm.OutboundAllocated, Is.False);
            Assert.That(farm.SourceStableIds, Is.EquivalentTo(new[]
            {
                "farm-handoff:sim.potato.1",
                "product:potato",
                "cargo:transport-71",
                "cargo-handoff:transport-71.inbound-91",
                "transport-task:71",
                "inbound-task:91",
            }));
        }

        [Test]
        public void 명시적Allocation이있는TownCargo만_CityOutbound로움직인다()
        {
            var map = Map();
            var town = map.CargoJourneys.Single(value => value.OutboundAllocated);
            var farm = map.CargoJourneys.Single(value => !value.OutboundAllocated);
            Assert.That(town.SourceStableIds.Any(value =>
                value.StartsWith("outbound-allocation:", StringComparison.Ordinal)), Is.True);
            Assert.That(town.CurrentStageCode, Is.EqualTo(화물JourneyStageCodes.CityOutbound));
            Assert.That(town.OutboundFollower, Is.Not.Null);
            Assert.That(town.OutboundFollower!.enabled, Is.True);
            Assert.That(farm.OutboundFollower, Is.Null);

            var stage = town.CurrentStageCode;
            var sources = town.SourceStableIds.ToArray();
            var before = town.OutboundFollower.transform.position;
            town.OutboundFollower.TickPresentation(.5f);
            Assert.That(town.OutboundFollower.transform.position, Is.Not.EqualTo(before));
            Assert.That(town.CurrentStageCode, Is.EqualTo(stage));
            Assert.That(town.SourceStableIds, Is.EqualTo(sources));
        }

        [Test]
        public void Allocation없는CityOutboundModel은거부된다()
        {
            var invalid = new 화물JourneyPresentationModel
            {
                CargoStableId = "cargo:forged",
                OriginRegionCode = 월드CompositionPackCodes.Farm,
                ProductStableId = "product:potato",
                CurrentStageCode = 화물JourneyStageCodes.CityOutbound,
                AcceptedAtHub = true,
                StoredAtHub = true,
                OutboundAllocated = true,
                SourceStableIds = new[]
                {
                    "cargo:forged", "product:potato", "inbound:a", "storage:a",
                },
            };
            Assert.That(invalid.Validate(), Is.False);
            var view = new GameObject("invalid").AddComponent<화물PresentationJourneyView>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => view.Apply(invalid));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void 사람Journey는_화물마당남쪽을지나며_RootMotion을사용하지않는다()
        {
            var passengers = Map().PassengerJourneys;
            Assert.That(passengers.Count, Is.EqualTo(2));
            Assert.That(passengers, Has.All.Matches<공용ActorRouteFollower>(value =>
                value.ValidateWiring()
                && value.AnimationAdapter.RootMotionDisabled
                && !value.AnimationAdapter.Animator.applyRootMotion));
            var townCity = passengers.Single(value => value.name == "Passenger_TownCity");
            var midpoint = (townCity.RouteStart.position + townCity.RouteEnd.position) * .5f;
            Assert.That(midpoint.z, Is.LessThan(-15f));
        }

        [Test]
        public void 저장Scene은_업무권위없이PerspectiveOverview를제공한다()
        {
            var map = Map();
            Assert.That(map.ValidateWiring(), Is.True);
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(behaviours.Select(value => value.GetType().Name),
                Has.None.Matches<string>(name =>
                    name.Contains("Controller", StringComparison.Ordinal)
                    || name.Contains("UseCase", StringComparison.Ordinal)
                    || name.Contains("Repository", StringComparison.Ordinal)
                    || name.Contains("SimulationTick", StringComparison.Ordinal)));
            var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single();
            Assert.That(camera.orthographic, Is.False);
            var rig = camera.GetComponent<DioramaTopDownCameraRig>();
            Assert.That(rig, Is.Not.Null);
            rig!.ApplyNowForTests();
            Assert.That(rig.CurrentFocusAnchorId, Is.EqualTo(세RegionHubJourneyBuilder.WorldFocusId));
            Assert.That(rig.ConfiguredMaxDistance, Is.EqualTo(850f));
            Assert.That(rig.ConfiguredWorldDistance, Is.EqualTo(830f));
            Assert.That(rig.ConfiguredZoneDistance, Is.EqualTo(95f));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
        }

        [Test]
        public void ART1은_Region색면과연속도로와Landscape계층을제공한다()
        {
            var root = GameObject.Find("CMP5 Three Region Hub Journey");
            Assert.That(root, Is.Not.Null);
            var grounds = root!.transform.Find("ART1 Region Grounds");
            var roads = root.transform.Find("ART1 Continuous Roads");
            var landscape = root.transform.Find("ART1 Landscape Clusters");
            var landmarks = root.transform.Find("ART1 Transition Landmarks");
            var roadside = root.transform.Find("ART1 Roadside Clusters");
            var relief = root.transform.Find("ART1 Terrain Relief");
            Assert.That(grounds, Is.Not.Null);
            Assert.That(roads, Is.Not.Null);
            Assert.That(landscape, Is.Not.Null);
            Assert.That(landmarks, Is.Not.Null);
            Assert.That(roadside, Is.Not.Null);
            Assert.That(relief, Is.Not.Null);
            Assert.That(new[] { "FarmGround", "TownGround", "HubGround", "CityGround" },
                Has.All.Matches<string>(name => grounds!.Find(name) != null));
            Assert.That(roads!.Cast<Transform>().Count(value =>
                value.name.EndsWith("_Surface", StringComparison.Ordinal)), Is.EqualTo(5));
            Assert.That(landscape!.GetComponentsInChildren<WorldVisualInstanceView>(true).Length,
                Is.GreaterThanOrEqualTo(70));
            Assert.That(landscape.Cast<Transform>().Count(value =>
                value.name.StartsWith("Environment_TownHouse", StringComparison.Ordinal)),
                Is.EqualTo(3));
            Assert.That(landmarks!.childCount, Is.GreaterThanOrEqualTo(18));
            Assert.That(roadside!.childCount, Is.EqualTo(5));
            Assert.That(roadside.Cast<Transform>().SelectMany(value =>
                    value.Cast<Transform>()).Count(value =>
                    value.name.StartsWith("Environment_RoadsideHouse", StringComparison.Ordinal)),
                Is.EqualTo(10));
            Assert.That(roadside.GetComponentsInChildren<WorldVisualInstanceView>(true).Length,
                Is.GreaterThanOrEqualTo(30));
            Assert.That(relief!.childCount, Is.EqualTo(14));
            Assert.That(relief.GetComponentsInChildren<MeshFilter>(true),
                Has.All.Matches<MeshFilter>(value => value.sharedMesh.bounds.size.y >= 4f));
        }

        [Test]
        public void ART2와ART3은_RegionPalette와공통Sun을고정한다()
        {
            var materials = new[] { "FarmGround", "TownGround", "HubGround", "CityGround" }
                .Select(name => GameObject.Find(name)!.GetComponent<Renderer>().sharedMaterial)
                .ToArray();
            Assert.That(materials, Has.All.Not.Null);
            Assert.That(materials.Select(value => value.GetColor("_BaseColor"))
                .Distinct().Count(), Is.EqualTo(4));

            var light = GameObject.Find("WorldDirectionalLight")!.GetComponent<Light>();
            Assert.That(light.type, Is.EqualTo(LightType.Directional));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(light.shadowStrength, Is.InRange(.76f, .8f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight));
            Assert.That(RenderSettings.fog, Is.True);
            var time = Object.FindFirstObjectByType<월드시간대Presenter>();
            Assert.That(time, Is.Not.Null);
            Assert.That(time!.SourceMode, Is.EqualTo(월드시간대SourceMode.FixedReference));
            Assert.That(time.NormalizedTime, Is.EqualTo(12.5f / 24f).Within(.0001f));
            Assert.That(time.ValidateWiring(), Is.True);
        }

        [Test]
        public void 야간Monster약탈은_SimulationPresentation만변경한다()
        {
            var map = Map();
            var raid = map.NightRaidPresenter;
            var cargo = map.CargoJourneys.Single(value => value.OutboundAllocated);
            var stage = cargo.CurrentStageCode;
            var sources = cargo.SourceStableIds.ToArray();

            Assert.That(raid, Is.Not.Null);
            Assert.That(raid.SourceMode, Is.EqualTo("Simulation"));
            Assert.That(raid.MonsterCount, Is.EqualTo(3));
            Assert.That(raid.CarriedCargoCount, Is.EqualTo(2));
            Assert.That(raid.ValidateWiring(), Is.True);

            raid.ApplyDayPreview();
            Assert.That(raid.IsRaidVisible, Is.False);
            Assert.That(cargo.OutboundFollower!.enabled, Is.True);

            raid.ApplyNightPreview(.62f);
            Assert.That(raid.IsRaidVisible, Is.True);
            Assert.That(raid.IsLootVisible, Is.True);
            Assert.That(cargo.OutboundFollower.enabled, Is.False);
            Assert.That(cargo.CurrentStageCode, Is.EqualTo(stage));
            Assert.That(cargo.SourceStableIds, Is.EqualTo(sources));
            Assert.That(Object.FindFirstObjectByType<월드시간대Presenter>()!.CurrentModel.PreviousAnchor,
                Is.EqualTo(월드시간대AnchorCode.GoldenDusk));

            raid.ApplyDayPreview();
        }

        [Test]
        public void HubCity약탈지점은_독립ZoneCameraFocus를제공한다()
        {
            var rig = Object.FindFirstObjectByType<DioramaTopDownCameraRig>();
            Assert.That(rig, Is.Not.Null);
            rig!.Focus(세RegionHubJourneyBuilder.NightRaidFocusId);
            rig.ApplyNowForTests();
            Assert.That(rig.CurrentFocusAnchorId, Is.EqualTo(세RegionHubJourneyBuilder.NightRaidFocusId));
        }

        [Test]
        public void Overview는_좁은DataRoute와숨긴WorldText를사용한다()
        {
            var routes = new[]
            {
                "DataRoute_Passenger_FarmTown", "DataRoute_Passenger_TownCity",
                "DataRoute_Freight_FarmHub", "DataRoute_Freight_TownHub",
                "DataRoute_Freight_HubCity",
            }.Select(GameObject.Find).ToArray();
            Assert.That(routes, Has.All.Not.Null);
            Assert.That(routes.Max(value => value!.transform.localScale.x), Is.LessThan(.3f));
            Assert.That(Object.FindObjectsByType<TextMesh>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None),
                Has.All.Matches<TextMesh>(value => !value.gameObject.activeInHierarchy));
        }

        private static 세RegionHubJourneyView Map()
        {
            var map = Object.FindFirstObjectByType<세RegionHubJourneyView>();
            Assert.That(map, Is.Not.Null);
            return map;
        }
    }
}
