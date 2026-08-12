using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 물류이동Tests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public async Task Preview는_snapshot과재고를변경하지않는다()
        {
            var initial = 물류이동FixtureAuthorityClient.CreateInitialSnapshot();
            var coordinator = new 물류이동Coordinator(
                new 물류이동FixtureAuthorityClient(initial), initial);

            await coordinator.PreviewAsync();

            Assert.That(coordinator.PhaseCode, Is.EqualTo(물류이동PhaseCodes.PreviewReady));
            Assert.That(coordinator.CurrentSnapshot, Is.SameAs(initial));
            Assert.That(coordinator.CurrentSnapshot.SourceAvailableQuantity, Is.EqualTo(300m));
            Assert.That(coordinator.CurrentPreview!.DestinationStockCandidateStableId,
                Is.EqualTo("stock-candidate:arrival:cargo:sim.potato-1"));
            Assert.That(coordinator.CurrentPreview.RecommendedCarrierCandidateStableId,
                Is.EqualTo(화물배차Fixture.RecommendedCandidateStableId));
            Assert.That(coordinator.CurrentPreview.CandidateEvaluations, Has.Length.EqualTo(3));
            Assert.That(coordinator.CurrentPreview.CandidateEvaluations,
                Has.Exactly(1).Matches<화물배차후보평가Data>(value => value.IsRecommended));
        }

        [Test]
        public async Task Confirm과세Tick은_같은Cargo예약과도착을보존한다()
        {
            var initial = 물류이동FixtureAuthorityClient.CreateInitialSnapshot();
            var coordinator = new 물류이동Coordinator(
                new 물류이동FixtureAuthorityClient(initial), initial);
            await coordinator.PreviewAsync();
            await coordinator.ConfirmAsync();

            Assert.That(coordinator.CurrentSnapshot.MovementStateCode, Is.EqualTo("Reserved"));
            Assert.That(coordinator.CurrentSnapshot.ReservedQuantity, Is.EqualTo(300m));
            Assert.That(coordinator.CurrentSnapshot.SourceAvailableQuantity, Is.Zero);
            Assert.That(coordinator.CurrentSnapshot.CarrierCandidateStableId,
                Is.EqualTo(화물배차Fixture.RecommendedCandidateStableId));
            Assert.That(coordinator.CurrentSnapshot.DispatchStateCode, Is.EqualTo("배차확정"));

            await coordinator.AdvanceAsync();
            Assert.That(coordinator.CurrentSnapshot.CargoStableId,
                Is.EqualTo(물류이동Fixture.CargoStableId));
            Assert.That(coordinator.CurrentSnapshot.MovementStateCode, Is.EqualTo("InTransit"));
            Assert.That(coordinator.CurrentSnapshot.CompletedRouteTicks, Is.EqualTo(1));
            await coordinator.AdvanceAsync();
            await coordinator.AdvanceAsync();

            Assert.That(coordinator.PhaseCode, Is.EqualTo(물류이동PhaseCodes.Arrived));
            Assert.That(coordinator.CurrentSnapshot.CargoStableId,
                Is.EqualTo(물류이동Fixture.CargoStableId));
            Assert.That(coordinator.CurrentSnapshot.ReservedQuantity, Is.EqualTo(300m));
            Assert.That(coordinator.CurrentSnapshot.CompletedRouteTicks, Is.EqualTo(3));
            Assert.That(coordinator.CurrentSnapshot.TaskStateCode, Is.EqualTo("Completed"));
        }

        [Test]
        public async Task HttpAuthority는_공식물류PreviewConfirmTick경로를사용한다()
        {
            var api = new StubApiClient();
            var repository = new 물류이동AuthorityRepository(api);
            var request = 물류이동Fixture.CreateRequest();

            var preview = await repository.PreviewAsync(
                SimulationWorldShellFixture.SessionStableId, request, CancellationToken.None);
            var confirmed = await repository.ConfirmAsync(
                SimulationWorldShellFixture.SessionStableId, 15, preview, CancellationToken.None);
            var advanced = await repository.AdvanceAsync(
                SimulationWorldShellFixture.SessionStableId, 16, CancellationToken.None);

            Assert.That(preview.CargoStableId, Is.EqualTo(물류이동Fixture.CargoStableId));
            Assert.That(confirmed.MovementStateCode, Is.EqualTo("Reserved"));
            Assert.That(advanced.MovementStateCode, Is.EqualTo("InTransit"));
            Assert.That(api.Requests, Has.Count.EqualTo(4));
            Assert.That(preview.RecommendedCarrierCandidateStableId,
                Is.EqualTo(화물배차Fixture.RecommendedCandidateStableId));
            Assert.That(preview.CandidateEvaluations, Has.Length.EqualTo(3));
            Assert.That(confirmed.CarrierCandidateStableId,
                Is.EqualTo(화물배차Fixture.RecommendedCandidateStableId));
            Assert.That(api.Requests[1].RelativePath, Does.EndWith("/freight-dispatch-previews"));
            Assert.That(api.Requests[1].JsonBody, Does.Contain("\"Candidates\""));
            Assert.That(api.Requests[2].RelativePath, Does.EndWith("/freight-dispatches/confirm"));
            Assert.That(api.Requests[2].JsonBody, Does.Contain("\"ExpectedRevision\":15"));
            Assert.That(api.Requests[2].JsonBody,
                Does.Contain("\"SelectedCarrierCandidateStableId\":\"carrier-candidate:sim.waiting-truck\""));
            Assert.That(api.Requests[3].RelativePath, Does.EndWith("/ticks"));
            Assert.That(api.Requests[3].JsonBody, Does.Contain("\"TickCount\":1"));
        }

        [Test]
        public void 저장Scene은_Cargo선택과세단계ActionCard를가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene);
                var presenter = root.GetComponentInChildren<물류이동Presenter>(true);
                Assert.That(presenter, Is.Not.Null);
                Assert.DoesNotThrow(() => presenter!.ValidateWiring());
                var card = root.transform.Find("PersistentUI/SimulationWorldHud/LogisticsMovementCard");
                Assert.That(card, Is.Not.Null);
                Assert.That(card!.GetComponentsInChildren<UnityEngine.UI.Button>(true), Has.Length.EqualTo(3));
                var cargo = root.transform.Find(
                    "SettlementInteriorRoot/Districts/LogisticsDistrict/PotatoCargo");
                Assert.That(cargo, Is.Not.Null);
                Assert.That(cargo!.GetComponent<SimulationWorldNavigationTargetView>(), Is.Not.Null);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static UnityEngine.GameObject Find(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "SimulationWorldShell") return root;
            throw new InvalidOperationException("SimulationWorldShellMissing");
        }

        private sealed class StubApiClient : ISimulationRehearsalUnityApiClient
        {
            public List<UnityApiRequest> Requests { get; } = new();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var body = request.RelativePath.EndsWith("freight-dispatch-previews")
                    ? PreviewJson
                    : request.RelativePath.EndsWith("freight-dispatches/confirm")
                        ? SnapshotJson(16, 14, "Reserved", "Scheduled", 0)
                        : request.RelativePath.EndsWith("/ticks")
                            ? SnapshotJson(17, 15, "InTransit", "InProgress", 1)
                            : SnapshotJson(15, 14, "", "", 0, false);
                return Task.FromResult(new UnityApiResponse { StatusCode = 200, Body = body });
            }

            private const string PreviewJson = "{\"observedRevision\":15,\"observedWorldTick\":14,"
                + "\"transportRequestStableId\":\"freight-transport:sim.potato-1\","
                + "\"dispatchOfferStableId\":\"dispatch-offer:freight-transport:sim.potato-1\","
                + "\"recommendedCarrierCandidateStableId\":\"carrier-candidate:sim.waiting-truck\","
                + "\"ruleRevision\":\"freight-dispatch-candidate.v1\","
                + "\"candidateEvaluations\":["
                + "{\"carrierCandidateStableId\":\"carrier-candidate:sim.waiting-truck\",\"vehicleStableId\":\"vehicle:sim.truck-fresh\",\"isEligible\":true,\"isRecommended\":true,\"rank\":1,\"pickupDistanceKm\":6,\"vehicleCapacity\":400,\"vehicleCapacityUnitCode\":\"KGM\",\"reason\":\"대기 중인 지역 트럭\",\"blockReasonCodes\":[],\"score\":{\"baseScore\":34,\"driverWaitingScore\":9,\"totalScore\":43}},"
                + "{\"carrierCandidateStableId\":\"carrier-candidate:sim.small-van\",\"vehicleStableId\":\"vehicle:sim.van-small\",\"vehicleCapacity\":200,\"vehicleCapacityUnitCode\":\"KGM\",\"blockReasonCodes\":[\"VehicleCapacityExceeded\"],\"score\":{}},"
                + "{\"carrierCandidateStableId\":\"carrier-candidate:sim.stale-truck\",\"vehicleStableId\":\"vehicle:sim.truck-stale\",\"vehicleCapacity\":400,\"vehicleCapacityUnitCode\":\"KGM\",\"blockReasonCodes\":[\"CandidateLocationStale\"],\"score\":{}}],"
                + "\"logisticsMovement\":{\"cargoStableId\":\"cargo:sim.potato-1\","
                + "\"quantity\":300,\"unitCode\":\"KGM\",\"requiredRouteTicks\":3,"
                + "\"destinationStockCandidateStableId\":\"stock-candidate:arrival:cargo:sim.potato-1\","
                + "\"boundaryCodes\":[\"VehicleAnimationIsPresentationOnly\"]}}";

            private static string SnapshotJson(
                int revision, int tick, string movement, string task, int progress, bool include = true)
                => "{\"sessionStableId\":\"" + SimulationWorldShellFixture.SessionStableId
                    + "\",\"currentTick\":" + tick + ",\"revision\":" + revision
                    + ",\"worldContext\":{\"worldTick\":" + tick
                    + ",\"gameDate\":\"2026-04-15T00:00:00Z\"},"
                    + "\"tasks\":" + (include ? "[{\"taskStableId\":\"task:logistics:cargo:sim.potato-1\",\"stateCode\":\"" + task + "\"}]" : "[]") + ","
                    + "\"logisticsMovements\":" + (include ? "[{\"cargoStableId\":\"cargo:sim.potato-1\",\"stateCode\":\"" + movement + "\",\"sourceAllocationStableId\":\"allocation:harvest-lot:harvest-lot:potato-1\",\"taskStableId\":\"task:logistics:cargo:sim.potato-1\",\"quantity\":300,\"reservedQuantity\":300,\"completedRouteTicks\":" + progress + ",\"requiredRouteTicks\":3,\"routeStableId\":\"route:sim.farm-hub-1\",\"destinationStockCandidateStableId\":\"stock-candidate:arrival:cargo:sim.potato-1\"}]" : "[]") + ","
                    + "\"freightTransports\":" + (include ? "[{\"transportRequestStableId\":\"freight-transport:sim.potato-1\",\"dispatchStateCode\":\"배차확정\",\"carrierCandidateStableId\":\"carrier-candidate:sim.waiting-truck\",\"vehicleStableId\":\"vehicle:sim.truck-fresh\",\"dispatchDecision\":{\"ruleRevision\":\"freight-dispatch-candidate.v1\"}}]" : "[]") + ","
                    + "\"settlement\":{\"treasuryBalance\":1000000,\"treasuryReserved\":0,\"laborAvailable\":80,\"laborReserved\":0,\"storageOccupied\":1200,\"storageReserved\":0,\"foodReserveEquivalent\":1200,\"foodSecurityDays\":10,\"activeTaskStableIds\":" + (include ? "[\"task:logistics:cargo:sim.potato-1\"]" : "[]") + ",\"marketSupplyByProduct\":[],\"harvestLotAllocations\":[{\"allocationStableId\":\"allocation:harvest-lot:harvest-lot:potato-1\",\"stateCode\":\"Applied\",\"availableQuantity\":" + (include ? "0" : "300") + "}]}}";
        }
    }
}
