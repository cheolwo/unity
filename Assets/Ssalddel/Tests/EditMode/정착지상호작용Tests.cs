using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 정착지상호작용Tests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [TestCase(HarvestDispositionChoiceCodes.CooperativeShipment, 8, 30000, 240000, 2)]
        [TestCase(HarvestDispositionChoiceCodes.DirectOnlineSale, 18, 60000, 360000, 3)]
        [TestCase(HarvestDispositionChoiceCodes.ExportAgent, 24, 90000, 450000, 4)]
        [TestCase(HarvestDispositionChoiceCodes.ReserveStorage, 6, 15000, null, 1)]
        public async Task 네판로Preview는_snapshot을바꾸지않고서버정책후보를표시한다(
            string choiceCode,
            int labor,
            int cost,
            int? revenue,
            int duration)
        {
            var initial = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var coordinator = new 정착지상호작용Coordinator(
                new 정착지상호작용FixtureAuthorityClient(initial), initial);

            await coordinator.PreviewAsync(
                정착지상호작용BranchFixture.CreateEnvelope(choiceCode));

            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.PreviewReady));
            Assert.That(coordinator.CurrentSnapshot, Is.SameAs(initial));
            Assert.That(coordinator.CurrentSnapshot.Revision, Is.EqualTo(12));
            Assert.That(coordinator.CurrentSnapshot.WorldTick, Is.EqualTo(12));
            Assert.That(coordinator.CurrentPreview!.RequiredLabor, Is.EqualTo((decimal)labor));
            Assert.That(coordinator.CurrentPreview.SimulationCost, Is.EqualTo((decimal)cost));
            Assert.That(coordinator.CurrentPreview.ProjectedRevenue,
                Is.EqualTo(revenue.HasValue ? (decimal?)revenue.Value : null));
            Assert.That(coordinator.CurrentPreview.DurationTicks, Is.EqualTo(duration));
        }

        [Test]
        public async Task 비축Confirm은예약하고완료Tick만경제Effect를적용한다()
        {
            var initial = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var coordinator = new 정착지상호작용Coordinator(
                new 정착지상호작용FixtureAuthorityClient(initial), initial);
            await coordinator.PreviewAsync(
                정착지상호작용BranchFixture.CreateEnvelope(
                    HarvestDispositionChoiceCodes.ReserveStorage));

            await coordinator.ConfirmAsync();

            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.TaskReserved));
            Assert.That(coordinator.CurrentSnapshot.Revision, Is.EqualTo(13));
            Assert.That(coordinator.CurrentSnapshot.WorldTick, Is.EqualTo(12));
            Assert.That(coordinator.CurrentSnapshot.TreasuryBalance, Is.EqualTo(1_000_000m));
            Assert.That(coordinator.CurrentSnapshot.TreasuryReserved, Is.EqualTo(15_000m));
            Assert.That(coordinator.CurrentSnapshot.StorageOccupied, Is.EqualTo(1200m));
            Assert.That(coordinator.CurrentSnapshot.StorageReserved, Is.EqualTo(294m));
            Assert.That(coordinator.CurrentSnapshot.AllocationStateCode, Is.EqualTo("Reserved"));

            await coordinator.Refresh수확판로결과Async(
                coordinator.CurrentPreview!.HarvestLotStableId);
            Assert.That(coordinator.Current수확판로결과!.WorldRevision, Is.EqualTo(13));
            Assert.That(coordinator.Current수확판로결과.Routes.Single(value => value.IsSelected)
                .CurrentStageCode, Is.EqualTo(수확판로결과단계Codes.DispositionTaskScheduled));

            await coordinator.AdvanceToCompletionAsync();
            await coordinator.Refresh수확판로결과Async(
                coordinator.CurrentPreview.HarvestLotStableId);

            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.EffectApplied));
            Assert.That(coordinator.CurrentSnapshot.Revision, Is.EqualTo(14));
            Assert.That(coordinator.CurrentSnapshot.WorldTick, Is.EqualTo(13));
            Assert.That(coordinator.CurrentSnapshot.TreasuryBalance, Is.EqualTo(985_000m));
            Assert.That(coordinator.CurrentSnapshot.StorageOccupied, Is.EqualTo(1494m));
            Assert.That(coordinator.CurrentSnapshot.ReserveFoodEquivalent, Is.EqualTo(1552.8m));
            Assert.That(coordinator.CurrentSnapshot.FoodSecurityDays, Is.EqualTo(12.94m));
            Assert.That(coordinator.CurrentSnapshot.AllocationStateCode, Is.EqualTo("Applied"));
            Assert.That(coordinator.CurrentSnapshot.EffectStateCode, Is.EqualTo("Applied"));
            var card = new 수확판로결과Projector().Project(
                coordinator.Current수확판로결과!);
            Assert.That(card.SelectedRouteText, Does.Contain("정착지 비축 보관"));
            Assert.That(card.CurrentStageText, Is.EqualTo("비축 창고 보관 완료"));
            Assert.That(card.ResultText, Does.Contain("비축 294"));
            Assert.That(card.ResultText, Does.Contain("반영 재정 -15000 KRW"));
        }

        [TestCase(수확판로결과단계Codes.ExportDelivered, 300, 0,
            "가상 수출 판매 완료", "도착 300", "+1010000")]
        [TestCase(수확판로결과단계Codes.ExportDisruptedWithLoss, 0, 300,
            "가상 운송 손실 확정", "손실 300", "-790000")]
        public void 외부교역결과는_성공과손실을한국어카드로구분한다(
            string stageCode,
            int delivered,
            int lost,
            string stageText,
            string quantityText,
            string treasuryText)
        {
            var result = ExportResult(stageCode, delivered, lost,
                stageCode == 수확판로결과단계Codes.ExportDelivered ? 1_010_000m : -790_000m);

            var card = new 수확판로결과Projector().Project(result);

            Assert.That(card.Routes, Has.Length.EqualTo(4));
            Assert.That(card.Routes.Count(value => value.IsSelected), Is.EqualTo(1));
            Assert.That(card.SelectedRouteText, Does.Contain("외부 교역 준비"));
            Assert.That(card.CurrentStageText, Is.EqualTo(stageText));
            Assert.That(card.ResultText, Does.Contain(quantityText));
            Assert.That(card.ResultText, Does.Contain(treasuryText));
        }

        [Test]
        public async Task 재접속은_최신Session과판로목록을같은Revision으로맞춘다()
        {
            var observed = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var latest = observed.Clone();
            latest.Revision = 40;
            latest.WorldTick = 40;
            latest.GameDateLabel = "Year 1 · 05-10";
            latest.AllocationStateCode = "Applied";
            latest.TaskStateCode = "Completed";
            latest.EffectStateCode = "Applied";
            latest.TreasuryBalance = 1_360_000m;
            var outcome = ExportResult(
                수확판로결과단계Codes.ExportShipmentInTransit, 0m, 0m, 360_000m);
            var route = outcome.Routes.Single(value => value.IsSelected);
            route.ResolvedQuantity = 0m;
            route.RemainingQuantity = 300m;
            route.OutboundReservedQuantity = 300m;
            route.RiskResultCode = "Pending";
            var coordinator = new 정착지상호작용Coordinator(
                new 정착지상호작용FixtureAuthorityClient(latest, outcome),
                observed);

            await coordinator.Refresh수확판로결과목록Async();

            Assert.That(coordinator.CurrentSnapshot.Revision, Is.EqualTo(40));
            Assert.That(coordinator.CurrentSnapshot.WorldTick, Is.EqualTo(40));
            Assert.That(coordinator.Current수확판로결과목록, Has.Length.EqualTo(1));
            Assert.That(coordinator.Current수확판로결과, Is.Not.Null);
            Assert.That(coordinator.CurrentPreview, Is.Null);
            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.EffectApplied));
            var card = new 수확판로결과Projector().Project(
                coordinator.Current수확판로결과!);
            Assert.That(card.CurrentStageText, Is.EqualTo("가상 국제 운송 중"));
            Assert.That(card.ResultText, Does.Contain("출고 예약 300"));
        }

        [Test]
        public async Task 재접속한예약Task는_서버남은Tick으로계속진행한다()
        {
            var initial = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var authority = new 정착지상호작용FixtureAuthorityClient(initial);
            var beforeReconnect = new 정착지상호작용Coordinator(authority, initial);
            await beforeReconnect.PreviewAsync(
                정착지상호작용BranchFixture.CreateEnvelope(
                    HarvestDispositionChoiceCodes.DirectOnlineSale));
            await beforeReconnect.ConfirmAsync();
            var partial = await authority.AdvanceAsync(
                initial.SessionStableId,
                beforeReconnect.CurrentSnapshot.Revision,
                1,
                CancellationToken.None);
            var reconnected = new 정착지상호작용Coordinator(authority, initial);

            await reconnected.Refresh수확판로결과목록Async();

            Assert.That(partial.TaskStateCode, Is.EqualTo("InProgress"));
            Assert.That(reconnected.CurrentPreview, Is.Null);
            Assert.That(reconnected.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.TaskReserved));
            Assert.That(reconnected.CurrentSnapshot.AllocationTaskStableId,
                Is.EqualTo("task:fixture.harvest-route"));
            Assert.That(reconnected.CurrentSnapshot.TaskExpectedEndTick, Is.EqualTo(15));
            Assert.That(reconnected.CurrentSnapshot.TaskRemainingTicks, Is.EqualTo(2));
            Assert.That(reconnected.CanResumeReservedTask, Is.True);

            await reconnected.AdvanceToCompletionAsync();

            Assert.That(reconnected.CurrentSnapshot.WorldTick, Is.EqualTo(15));
            Assert.That(reconnected.CurrentSnapshot.Revision, Is.EqualTo(16));
            Assert.That(reconnected.CurrentSnapshot.AllocationStateCode, Is.EqualTo("Applied"));
            Assert.That(reconnected.CurrentSnapshot.TaskRemainingTicks, Is.Zero);
            Assert.That(reconnected.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.EffectApplied));
        }

        [Test]
        public void 재접속한예약Task의기간이유효하지않으면_기존화면을보존한다()
        {
            var observed = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var invalid = observed.Clone();
            invalid.Revision = 40;
            invalid.WorldTick = 40;
            invalid.AllocationStateCode = "Reserved";
            invalid.AllocationTaskStableId = "task:invalid";
            invalid.ActiveTaskCount = 1;
            invalid.TaskStateCode = "Scheduled";
            invalid.TaskScheduledStartTick = 40;
            invalid.TaskExpectedEndTick = 40;
            invalid.TaskRemainingTicks = 0;
            var outcome = ExportResult(
                수확판로결과단계Codes.ExportShipmentInTransit, 0m, 0m, 0m);
            var coordinator = new 정착지상호작용Coordinator(
                new 정착지상호작용FixtureAuthorityClient(invalid, outcome),
                observed);

            Assert.That(async () => await coordinator.Refresh수확판로결과목록Async(),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "HarvestRouteReservedTaskSnapshotInvalid"));
            Assert.That(coordinator.CurrentSnapshot, Is.SameAs(observed));
            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.LotSelected));
        }

        [Test]
        public void 판로목록Revision불일치는_기존화면Snapshot을보존한다()
        {
            var observed = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var latest = observed.Clone();
            latest.Revision = 41;
            latest.WorldTick = 41;
            latest.AllocationStateCode = "Applied";
            var staleOutcome = ExportResult(
                수확판로결과단계Codes.ExportShipmentInTransit, 0m, 0m, 360_000m);
            var coordinator = new 정착지상호작용Coordinator(
                new 정착지상호작용FixtureAuthorityClient(latest, staleOutcome),
                observed);

            Assert.That(async () => await coordinator.Refresh수확판로결과목록Async(),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "HarvestRouteRefreshOutcomeListInvalid"));
            Assert.That(coordinator.CurrentSnapshot, Is.SameAs(observed));
            Assert.That(coordinator.CurrentSnapshot.Revision, Is.EqualTo(12));
            Assert.That(coordinator.Current수확판로결과, Is.Null);
            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.LotSelected));
        }

        [Test]
        public async Task 여러Lot결과는_명시적으로요청한Lot만현재카드에연결한다()
        {
            const string firstLot = "harvest-lot:sim.potato.first";
            const string secondLot = "harvest-lot:sim.potato.second";
            var observed = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var latest = observed.Clone();
            latest.Revision = 50;
            latest.WorldTick = 20;
            latest.ActiveTaskCount = 1;
            latest.AllocationStateCode = string.Empty;
            latest.HarvestLotTasks = new[]
            {
                LotTask(firstLot, "Applied", "task:first", "Completed", 18, 20, 0, "Applied"),
                LotTask(secondLot, "Reserved", "task:second", "InProgress", 19, 23, 3, "Pending"),
            };
            var firstOutcome = ExportResult(
                수확판로결과단계Codes.ExportDelivered, 300m, 0m, 100m);
            firstOutcome.HarvestLotStableId = firstLot;
            firstOutcome.WorldTick = 20;
            firstOutcome.WorldRevision = 50;
            var secondOutcome = ExportResult(
                수확판로결과단계Codes.ExportShipmentInTransit, 0m, 0m, 0m);
            secondOutcome.HarvestLotStableId = secondLot;
            secondOutcome.WorldTick = 20;
            secondOutcome.WorldRevision = 50;
            secondOutcome.AllocationStateCode = "Reserved";
            var coordinator = new 정착지상호작용Coordinator(
                new 정착지상호작용FixtureAuthorityClient(
                    latest, firstOutcome, secondOutcome),
                observed);

            await coordinator.Refresh수확판로결과목록Async(secondLot);

            Assert.That(coordinator.Current수확판로결과목록, Has.Length.EqualTo(2));
            Assert.That(coordinator.Current수확판로결과!.HarvestLotStableId,
                Is.EqualTo(secondLot));
            Assert.That(coordinator.CurrentSnapshot.AllocationTaskStableId,
                Is.EqualTo("task:second"));
            Assert.That(coordinator.CurrentSnapshot.TaskRemainingTicks, Is.EqualTo(3));
            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.TaskReserved));

            coordinator.Select수확판로결과(firstLot);

            Assert.That(coordinator.Current수확판로결과.HarvestLotStableId,
                Is.EqualTo(firstLot));
            Assert.That(coordinator.CurrentSnapshot.AllocationTaskStableId,
                Is.EqualTo("task:first"));
            Assert.That(coordinator.PhaseCode,
                Is.EqualTo(정착지상호작용PhaseCodes.EffectApplied));
            Assert.That(() => coordinator.Select수확판로결과("harvest-lot:unknown"),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "HarvestRouteMappedOutcomeMissing"));
            Assert.That(coordinator.Current수확판로결과.HarvestLotStableId,
                Is.EqualTo(firstLot));
        }

        [Test]
        public void ObjectLotMapping은_양쪽StableId를일대일로유지한다()
        {
            var catalog = new 수확LotObjectMappingCatalog(
                new 수확LotObjectMappingData("object:harvest.first", "harvest-lot:first"),
                new 수확LotObjectMappingData("object:harvest.second", "harvest-lot:second"));

            Assert.That(catalog.TryResolve(
                "object:harvest.second", out var harvestLotStableId), Is.True);
            Assert.That(harvestLotStableId, Is.EqualTo("harvest-lot:second"));
            Assert.That(catalog.TryResolve("object:unknown", out _), Is.False);
            Assert.That(() => new 수확LotObjectMappingCatalog(
                    new 수확LotObjectMappingData("object:same", "harvest-lot:first"),
                    new 수확LotObjectMappingData("object:same", "harvest-lot:second")),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "HarvestRouteObjectLotMappingInvalid"));
        }

        [Test]
        public async Task HttpAuthorityClient는_여러Allocation을Lot별Task로보존한다()
        {
            var client = new 정착지상호작용AuthorityRepository(
                new StaticApiClient(MultiLotSnapshotJson));

            var snapshot = await client.RefreshAsync(
                SimulationWorldShellFixture.SessionStableId, CancellationToken.None);

            Assert.That(snapshot.HarvestLotTasks, Has.Length.EqualTo(2));
            Assert.That(snapshot.AllocationStateCode, Is.Empty);
            var second = snapshot.ForHarvestLot("harvest-lot:second");
            Assert.That(second.AllocationTaskStableId, Is.EqualTo("task:second"));
            Assert.That(second.TaskStateCode, Is.EqualTo("InProgress"));
            Assert.That(second.TaskExpectedEndTick, Is.EqualTo(23));
            Assert.That(second.TaskRemainingTicks, Is.EqualTo(3));
        }

        [Test]
        public void staleRevision과Confirm전Tick은차단된다()
        {
            var initial = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            var client = new 정착지상호작용FixtureAuthorityClient(initial);
            Assert.That(async () => await client.ConfirmAsync(
                    initial.SessionStableId,
                    11,
                    정착지상호작용BranchFixture.CreateEnvelope(
                        HarvestDispositionChoiceCodes.ReserveStorage).PreviewRequest,
                    CancellationToken.None),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "SimulationExpectedRevisionConflict"));

            var coordinator = new 정착지상호작용Coordinator(client, initial);
            Assert.That(async () => await coordinator.AdvanceToCompletionAsync(),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "SettlementInteractionReservedTaskRequired"));
        }

        [Test]
        public async Task HttpAuthorityClient는_공식PreviewConfirmTick경로와expectedRevision을사용한다()
        {
            var api = new StubApiClient();
            var client = new 정착지상호작용AuthorityRepository(api);
            var request = 정착지상호작용BranchFixture.CreateEnvelope(
                HarvestDispositionChoiceCodes.ReserveStorage).PreviewRequest;

            var preview = await client.PreviewAsync(
                SimulationWorldShellFixture.SessionStableId, request, CancellationToken.None);
            var confirmed = await client.ConfirmAsync(
                SimulationWorldShellFixture.SessionStableId, 12, request, CancellationToken.None);
            var completed = await client.AdvanceAsync(
                SimulationWorldShellFixture.SessionStableId, 13, 1, CancellationToken.None);
            var outcome = await client.Get수확판로결과Async(
                SimulationWorldShellFixture.SessionStableId,
                "harvest-lot:sim.potato.20260407.r1",
                CancellationToken.None);
            var outcomes = await client.Get수확판로결과목록Async(
                SimulationWorldShellFixture.SessionStableId,
                CancellationToken.None);

            Assert.That(preview.PolicyRevision, Is.EqualTo("harvest-impact:fixture-r1"));
            Assert.That(confirmed.AllocationStateCode, Is.EqualTo("Reserved"));
            Assert.That(confirmed.AllocationTaskStableId, Is.EqualTo("task:1"));
            Assert.That(confirmed.TaskScheduledStartTick, Is.EqualTo(13));
            Assert.That(confirmed.TaskExpectedEndTick, Is.EqualTo(13));
            Assert.That(confirmed.TaskRemainingTicks, Is.EqualTo(1));
            Assert.That(completed.AllocationStateCode, Is.EqualTo("Applied"));
            Assert.That(outcome.SelectedChoiceCode,
                Is.EqualTo(HarvestDispositionChoiceCodes.ReserveStorage));
            Assert.That(outcome.Routes.Single(value => value.IsSelected).StoredQuantity,
                Is.EqualTo(294m));
            Assert.That(outcomes, Has.Length.EqualTo(1));
            Assert.That(api.Requests, Has.Count.EqualTo(6));
            Assert.That(api.Requests[0].RelativePath,
                Is.EqualTo("api/simulation/v1/sessions/"
                    + Uri.EscapeDataString(SimulationWorldShellFixture.SessionStableId)));
            Assert.That(api.Requests[1].RelativePath, Does.EndWith(
                "/harvest-disposition-impact-previews"));
            Assert.That(api.Requests[2].RelativePath, Does.EndWith(
                "/harvest-disposition-impacts/confirm"));
            Assert.That(api.Requests[2].JsonBody, Does.Contain("\"ExpectedRevision\":12"));
            Assert.That(api.Requests[3].RelativePath, Does.EndWith("/ticks"));
            Assert.That(api.Requests[3].JsonBody, Does.Contain("\"TickCount\":1"));
            Assert.That(api.Requests[4].RelativePath, Does.EndWith(
                "/harvest-route-outcomes/harvest-lot%3Asim.potato.20260407.r1"));
            Assert.That(api.Requests[5].RelativePath,
                Does.EndWith("/harvest-route-outcomes"));
        }

        [Test]
        public void 저장Scene은_HarvestLot판로Card와여섯명시Action을가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var presenter = Find(scene).GetComponentInChildren<정착지상호작용Presenter>(true);
                Assert.That(presenter, Is.Not.Null);
                Assert.DoesNotThrow(() => presenter!.ValidateWiring());
                var card = Find(scene).transform.Find(
                    "PersistentUI/SimulationWorldHud/HarvestDispositionCard");
                Assert.That(card, Is.Not.Null);
                Assert.That(card!.GetComponentsInChildren<UnityEngine.UI.Button>(true),
                    Has.Length.EqualTo(6));
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

        private static 수확판로결과Data ExportResult(
            string stageCode,
            decimal delivered,
            decimal lost,
            decimal treasuryDelta)
            => new()
            {
                SessionStableId = SimulationWorldShellFixture.SessionStableId,
                WorldTick = 40,
                WorldRevision = 40,
                SettlementStableId = SimulationWorldShellFixture.SettlementStableId,
                AllocationStableId = "allocation:export-1",
                HarvestLotStableId = "harvest-lot:sim.potato.20260407.r1",
                HarvestLotRevision = 1,
                ProductStableId = "product:potato",
                Quantity = 300m,
                UnitCode = "kg",
                SelectedChoiceCode = HarvestDispositionChoiceCodes.ExportAgent,
                AllocationStateCode = "Applied",
                CurrencyCode = "KRW",
                Routes = new[]
                {
                    NotSelected(HarvestDispositionChoiceCodes.CooperativeShipment),
                    NotSelected(HarvestDispositionChoiceCodes.DirectOnlineSale),
                    NotSelected(HarvestDispositionChoiceCodes.ReserveStorage),
                    new 수확판로선택지결과Data
                    {
                        ChoiceCode = HarvestDispositionChoiceCodes.ExportAgent,
                        SelectionStateCode = "Selected",
                        IsSelected = true,
                        CurrentStageCode = stageCode,
                        SourceStateCode = stageCode,
                        Quantity = 300m,
                        ResolvedQuantity = 300m,
                        ExportDeliveredQuantity = delivered,
                        ExportLostQuantity = lost,
                        RecognizedTreasuryDelta = treasuryDelta,
                        CurrencyCode = "KRW",
                        RiskResultCode = delivered > 0
                            ? "DeliveredInSimulation"
                            : "DisruptedWithLossInSimulation",
                    },
                },
            };

        private static 수확판로선택지결과Data NotSelected(string choiceCode)
            => new()
            {
                ChoiceCode = choiceCode,
                SelectionStateCode = "NotSelected",
                CurrentStageCode = 수확판로결과단계Codes.NotSelected,
                Quantity = 300m,
                CurrencyCode = "KRW",
            };

        private static 수확LotTaskAuthorityData LotTask(
            string harvestLotStableId,
            string allocationStateCode,
            string taskStableId,
            string taskStateCode,
            int scheduledStartTick,
            int expectedEndTick,
            int remainingTicks,
            string effectStateCode)
            => new()
            {
                HarvestLotStableId = harvestLotStableId,
                AllocationStateCode = allocationStateCode,
                TaskStableId = taskStableId,
                TaskStateCode = taskStateCode,
                TaskScheduledStartTick = scheduledStartTick,
                TaskExpectedEndTick = expectedEndTick,
                TaskRemainingTicks = remainingTicks,
                EffectStateCode = effectStateCode,
            };

        private const string MultiLotSnapshotJson = "{\"sessionStableId\":\""
            + SimulationWorldShellFixture.SessionStableId
            + "\",\"currentTick\":20,\"revision\":50,"
            + "\"worldContext\":{\"worldTick\":20,\"gameDate\":\"2026-04-20T00:00:00Z\"},"
            + "\"tasks\":["
            + "{\"taskStableId\":\"task:first\",\"stateCode\":\"Completed\",\"scheduledStartTick\":18,\"expectedEndTick\":20},"
            + "{\"taskStableId\":\"task:second\",\"stateCode\":\"InProgress\",\"scheduledStartTick\":19,\"expectedEndTick\":23}],"
            + "\"effects\":["
            + "{\"causedByTaskStableId\":\"task:first\",\"stateCode\":\"Applied\"},"
            + "{\"causedByTaskStableId\":\"task:second\",\"stateCode\":\"Pending\"}],"
            + "\"settlement\":{\"treasuryBalance\":1000000,\"treasuryReserved\":0,"
            + "\"laborAvailable\":75,\"laborReserved\":25,\"storageOccupied\":1200,"
            + "\"storageReserved\":0,\"foodReserveEquivalent\":1200,\"foodSecurityDays\":10,"
            + "\"activeTaskStableIds\":[\"task:second\"],"
            + "\"marketSupplyByProduct\":[{\"productStableId\":\"product:potato\",\"quantity\":300}],"
            + "\"harvestLotAllocations\":["
            + "{\"harvestLotStableId\":\"harvest-lot:first\",\"taskStableId\":\"task:first\",\"stateCode\":\"Applied\"},"
            + "{\"harvestLotStableId\":\"harvest-lot:second\",\"taskStableId\":\"task:second\",\"stateCode\":\"Reserved\"}]}}";

        private sealed class StaticApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly string body;

            public StaticApiClient(string responseBody) => body = responseBody;

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
                => Task.FromResult(new UnityApiResponse { StatusCode = 200, Body = body });
        }

        private sealed class StubApiClient : ISimulationRehearsalUnityApiClient
        {
            public List<UnityApiRequest> Requests { get; } = new();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var body = request.RelativePath.EndsWith("/harvest-route-outcomes")
                    ? "[" + OutcomeJson + "]"
                    : request.RelativePath.Contains("/harvest-route-outcomes/")
                    ? OutcomeJson
                    : request.RelativePath.EndsWith("harvest-disposition-impact-previews")
                    ? PreviewJson
                    : request.RelativePath.EndsWith("harvest-disposition-impacts/confirm")
                        ? SnapshotJson(13, 12, "Reserved", "Scheduled", "Pending", 1)
                        : request.RelativePath.EndsWith("/ticks")
                            ? SnapshotJson(14, 13, "Applied", "Completed", "Applied", 0)
                            : SnapshotJson(12, 12, "", "", "", 0);
                return Task.FromResult(new UnityApiResponse { StatusCode = 200, Body = body });
            }

            private const string PreviewJson = "{\"choiceCode\":\"ReserveStorage\","
                + "\"nextWorkflowCode\":\"ReserveStockLotCandidate\","
                + "\"harvestLotStableId\":\"harvest-lot:sim.potato.20260407.r1\","
                + "\"quantity\":300,\"canonicalQuantityUnitCode\":\"KGM\","
                + "\"requiredLabor\":6,\"simulationCost\":15000,"
                + "\"projectedRevenue\":null,\"durationTicks\":1,"
                + "\"foodSecurityDaysBefore\":10,\"foodSecurityDaysCandidate\":12.94,"
                + "\"policyRevision\":\"harvest-impact:fixture-r1\","
                + "\"riskCodes\":[\"SimulationOutcomeOnly\"],"
                + "\"boundaryCodes\":[\"NoOperationalSale\"],"
                + "\"storageCandidate\":{\"expectedStoredQuantity\":294}}";

            private const string OutcomeJson = "{\"sessionStableId\":\""
                + SimulationWorldShellFixture.SessionStableId
                + "\",\"worldTick\":13,\"worldRevision\":14,"
                + "\"settlementStableId\":\"settlement:sim.home-1\","
                + "\"allocationStableId\":\"allocation:reserve-1\","
                + "\"harvestLotStableId\":\"harvest-lot:sim.potato.20260407.r1\","
                + "\"harvestLotRevision\":1,\"productStableId\":\"product:potato\","
                + "\"quantity\":300,\"unitCode\":\"kg\","
                + "\"selectedChoiceCode\":\"ReserveStorage\","
                + "\"allocationStateCode\":\"Applied\","
                + "\"currentTreasuryBalance\":985000,\"currencyCode\":\"KRW\","
                + "\"routes\":["
                + "{\"choiceCode\":\"CooperativeShipment\",\"selectionStateCode\":\"NotSelected\",\"isSelected\":false,\"currentStageCode\":\"NotSelected\",\"quantity\":300,\"currencyCode\":\"KRW\"},"
                + "{\"choiceCode\":\"DirectOnlineSale\",\"selectionStateCode\":\"NotSelected\",\"isSelected\":false,\"currentStageCode\":\"NotSelected\",\"quantity\":300,\"currencyCode\":\"KRW\"},"
                + "{\"choiceCode\":\"ReserveStorage\",\"selectionStateCode\":\"Selected\",\"isSelected\":true,\"currentStageCode\":\"ReserveStored\",\"sourceStateCode\":\"Applied\",\"quantity\":300,\"resolvedQuantity\":294,\"remainingQuantity\":6,\"storedQuantity\":294,\"recognizedTreasuryDelta\":-15000,\"currencyCode\":\"KRW\"},"
                + "{\"choiceCode\":\"ExportAgent\",\"selectionStateCode\":\"NotSelected\",\"isSelected\":false,\"currentStageCode\":\"NotSelected\",\"quantity\":300,\"currencyCode\":\"KRW\"}"
                + "],\"boundaryCodes\":[\"ProjectionOnly\"],\"sourceStableIds\":[\"harvest-lot:sim.potato.20260407.r1\"]}";

            private static string SnapshotJson(
                int revision,
                int tick,
                string allocation,
                string task,
                string effect,
                int active)
                => "{\"sessionStableId\":\"" + SimulationWorldShellFixture.SessionStableId
                    + "\",\"currentTick\":" + tick + ",\"revision\":" + revision
                    + ",\"worldContext\":{\"worldTick\":" + tick
                    + ",\"gameDate\":\"2026-04-13T00:00:00Z\"},"
                    + "\"tasks\":[{\"taskStableId\":\"task:1\",\"stateCode\":\"" + task
                    + "\",\"scheduledStartTick\":13,\"expectedEndTick\":13}],"
                    + "\"effects\":[{\"causedByTaskStableId\":\"task:1\",\"stateCode\":\"" + effect + "\"}],"
                    + "\"settlement\":{\"treasuryBalance\":1000000,\"treasuryReserved\":0,"
                    + "\"laborAvailable\":75,\"laborReserved\":25,\"storageOccupied\":1200,"
                    + "\"storageReserved\":0,\"foodReserveEquivalent\":1200,"
                    + "\"foodSecurityDays\":10,\"activeTaskStableIds\":"
                    + (active == 1 ? "[\"task:1\"]" : "[]")
                    + ",\"marketSupplyByProduct\":[{\"productStableId\":\"product:potato\",\"quantity\":300}],"
                    + "\"harvestLotAllocations\":"
                    + (string.IsNullOrEmpty(allocation)
                        ? "[]"
                        : "[{\"harvestLotStableId\":\"harvest-lot:sim.potato.20260407.r1\",\"taskStableId\":\"task:1\",\"stateCode\":\"" + allocation + "\"}]")
                    + "}}";
        }
    }
}
