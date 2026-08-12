using System;
using System.Linq;
using Ssalddel.Unity.Exhibition;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 통합전시관ActionCode
    {
        public const string 첫번째전시 = "ShowExhibit0";
        public const string 두번째전시 = "ShowExhibit1";
        public const string 세번째전시 = "ShowExhibit2";
        public const string 네번째전시 = "ShowExhibit3";
        public const string 다섯번째전시 = "ShowExhibit4";
        public const string 여섯번째전시 = "ShowExhibit5";
    }

    [DisallowMultipleComponent]
    public sealed class 통합전시관Presenter : MonoBehaviour
    {
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text summaryText = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text evidenceText = null!;
        [SerializeField] private Text boundaryText = null!;
        [SerializeField] private Text footerText = null!;
        [SerializeField] private Button[] exhibitButtons = Array.Empty<Button>();
        [SerializeField] private Renderer[] zoneBeacons = Array.Empty<Renderer>();
        [SerializeField] private string initialExhibitStableId = "exhibit:city:food-delivery";

        private 통합전시관Snapshot snapshot = null!;
        private string selectedExhibitStableId = string.Empty;
        private bool listenersBound;

        public int 전시수 => snapshot?.Exhibits.Length ?? 0;
        public string 선택ExhibitStableId => selectedExhibitStableId;
        public string ManifestRevision => snapshot?.Revision ?? string.Empty;
        public bool 운영Command제공여부 => false;

        public void Configure(
            Text title,
            Text summary,
            Text state,
            Text detail,
            Text evidence,
            Text boundary,
            Text footer,
            Button[] buttons,
            Renderer[] beacons)
        {
            titleText = title;
            summaryText = summary;
            stateText = state;
            detailText = detail;
            evidenceText = evidence;
            boundaryText = boundary;
            footerText = footer;
            exhibitButtons = buttons ?? Array.Empty<Button>();
            zoneBeacons = beacons ?? Array.Empty<Renderer>();
        }

        private void Start() => Initialize();

        public void Initialize()
        {
            ValidateWiring();
            snapshot = new 통합전시관Mapper().Map(CreateFixtureApiModel());
            BindListeners();
            if (!string.IsNullOrWhiteSpace(initialExhibitStableId)
                && snapshot.Exhibits.Any(value => value.ExhibitStableId == initialExhibitStableId))
                SelectExhibit(initialExhibitStableId);
            else
                SelectVisible(0);
        }

        private void OnDestroy()
        {
            if (!listenersBound) return;
            foreach (var button in exhibitButtons) button.onClick.RemoveAllListeners();
        }

        public void Execute(string actionCode)
        {
            switch (actionCode)
            {
                case 통합전시관ActionCode.첫번째전시:
                    SelectVisible(0);
                    break;
                case 통합전시관ActionCode.두번째전시:
                    SelectVisible(1);
                    break;
                case 통합전시관ActionCode.세번째전시:
                    SelectVisible(2);
                    break;
                case 통합전시관ActionCode.네번째전시:
                    SelectVisible(3);
                    break;
                case 통합전시관ActionCode.다섯번째전시:
                    SelectVisible(4);
                    break;
                case 통합전시관ActionCode.여섯번째전시:
                    SelectVisible(5);
                    break;
                default:
                    SelectExhibit(actionCode);
                    break;
            }
        }

        public void SelectExhibit(string exhibitStableId)
        {
            if (snapshot == null) throw new InvalidOperationException("IntegratedExhibitionNotInitialized");
            if (snapshot.Exhibits.All(value => value.ExhibitStableId != exhibitStableId))
                throw new InvalidOperationException("IntegratedExhibitionSelectionUnknown:" + exhibitStableId);
            selectedExhibitStableId = exhibitStableId;
            Render();
        }

        public void ValidateWiring()
        {
            if (titleText == null || summaryText == null || stateText == null
                || detailText == null || evidenceText == null || boundaryText == null
                || footerText == null || exhibitButtons == null || exhibitButtons.Length != 6
                || exhibitButtons.Any(value => value == null)
                || zoneBeacons == null || zoneBeacons.Length != 6
                || zoneBeacons.Any(value => value == null))
                throw new InvalidOperationException("IntegratedExhibitionPresenterWiringMissing");
        }

        private void SelectVisible(int index)
        {
            if (snapshot == null) throw new InvalidOperationException("IntegratedExhibitionNotInitialized");
            if (index < 0 || index >= snapshot.Exhibits.Length) return;
            SelectExhibit(snapshot.Exhibits[index].ExhibitStableId);
        }

        private void Render()
        {
            var selected = snapshot.Exhibits.Single(value =>
                value.ExhibitStableId == selectedExhibitStableId);
            titleText.text = "통합 모판·전시관 · EXH-5";
            summaryText.text = "모판 1 · 읽기 전시 1 · Simulation 계약 4 · 운영 실행 0\n"
                + "같은 manifest에서 근거·업무·표현을 보되 권위를 합치지 않습니다.";
            stateText.text = "DATA  " + StateLabel(selected.DataStateCode)
                + "    MODE  " + ModeLabel(selected.ExperienceModeCode)
                + "    STATE  " + CompletionLabel(selected.CompletionStateCode)
                + "    SCOPE  " + selected.AuthorizationScopeCode;

            for (var i = 0; i < exhibitButtons.Length; i++)
            {
                var exhibit = snapshot.Exhibits[i];
                exhibitButtons[i].GetComponentInChildren<Text>().text = exhibit.DisplayName + "\n"
                    + StateLabel(exhibit.DataStateCode) + " · " + ModeLabel(exhibit.ExperienceModeCode);
                exhibitButtons[i].image.color = exhibit.ExhibitStableId == selectedExhibitStableId
                    ? new Color(.86f, .57f, .18f, 1f)
                    : new Color(.1f, .16f, .18f, .96f);
                zoneBeacons[i].sharedMaterial.color = exhibit.ExhibitStableId == selectedExhibitStableId
                    ? new Color(1f, .66f, .2f)
                    : StateColor(exhibit.DataStateCode);
            }

            var source = selected.SourcePlan[0];
            var lineage = selected.WorkflowCheckpoints.Length == 0
                ? string.Join("\n", selected.CanonicalRecordRelations.Select(value =>
                    value.SourceStableId + "\n  " + value.RelationCode + " → " + value.TargetStableId
                    + "  [expected " + value.ExpectedTargetRevision + "]"))
                : selected.WorkflowCheckpoints[0].LineageStableId + "\n"
                  + string.Join(" → ", selected.WorkflowCheckpoints.Select(value =>
                      value.StateCode + "[" + DisclosureLabel(value.DisclosureScopeCode) + "]"
                      + (value.RequiresSeparateConfirmation ? "*" : string.Empty)))
                  + "\n* 별도 Confirm이 필요한 업무 경계";
            detailText.text = selected.DisplayName + "\n"
                + selected.ExhibitStableId + "\n\n"
                + "출처 계획\n" + source.SourceKey + "\n"
                + source.SourceStableId.Value + " @ " + source.SourceRevision + "\n\n"
                + "계보·checkpoint\n" + lineage;

            evidenceText.text = "독립 증거 축\n" + string.Join("\n", selected.Evidence.Select(value =>
                "[" + EvidenceLabel(value.StatusCode) + "] " + EvidenceKindLabel(value.EvidenceKindCode)
                + "  " + value.Reference));

            boundaryText.text = "허용된 상호작용\n"
                + string.Join(" · ", selected.AllowedInteractionIntentCodes)
                + "\n\n차단·한계\n"
                + (selected.BlockedReasonCodes.Length == 0
                    ? "명시적 차단 없음"
                    : string.Join("\n", selected.BlockedReasonCodes));
            footerText.text = selected.ExhibitKindCode == "FoodDeliveryLineage"
                ? "기사 후보는 전달 권역만 · 기사 수락 뒤 상세 · 전달 완료 ≠ 주문자 수령 확인 · 운영 Command 0"
                : selected.ExhibitKindCode == "OrdererGroupUrbanMarketLineage"
                    ? "개인 의향 비공개 · 집단화 Preview ≠ 참여 확정 · 판매가 ≠ KAMIS 관측 · 운영 Command 0"
                    : "관람 전용 · generic Confirm 없음 · 도착 ≠ 입고 ≠ 검수 ≠ 보관 · 운영 Command 0";
            RefreshTextGeometry();
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            for (var i = 0; i < exhibitButtons.Length; i++)
            {
                var index = i;
                exhibitButtons[i].onClick.AddListener(() => SelectVisible(index));
            }
            listenersBound = true;
        }

        private void RefreshTextGeometry()
        {
            foreach (var text in GetComponentsInChildren<Text>(true)) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }

        private static string StateLabel(string code)
            => code == 통합전시관DataStateCodes.Fixture ? "FIXTURE"
                : code == 통합전시관DataStateCodes.Uncollected ? "미수집"
                : code == 통합전시관DataStateCodes.Live ? "LIVE"
                : code == 통합전시관DataStateCodes.Cached ? "CACHED"
                : code == 통합전시관DataStateCodes.Failed ? "FAILED"
                : code;

        private static string ModeLabel(string code)
            => code == 통합전시관ExperienceModeCodes.Research ? "모판 연구"
                : code == 통합전시관ExperienceModeCodes.ReadOnly ? "읽기 전시"
                : code == 통합전시관ExperienceModeCodes.Simulation ? "Simulation"
                : "운영 인계";

        private static string CompletionLabel(string code)
            => code == 통합전시관CompletionStateCodes.Verified ? "검증됨"
                : code == 통합전시관CompletionStateCodes.Blocked ? "차단"
                : code == 통합전시관CompletionStateCodes.Linked ? "연결됨"
                : code == 통합전시관CompletionStateCodes.Promoted ? "승격됨"
                : "후보";

        private static string EvidenceLabel(string code)
            => code == 통합전시관EvidenceStatusCodes.Verified ? "확인"
                : code == 통합전시관EvidenceStatusCodes.Partial ? "부분"
                : code == 통합전시관EvidenceStatusCodes.NotApplicable ? "해당없음"
                : "미확인";

        private static string EvidenceKindLabel(string code)
            => code == 통합전시관EvidenceKindCodes.Code ? "코드"
                : code == 통합전시관EvidenceKindCodes.FocusedTest ? "집중 test"
                : code == 통합전시관EvidenceKindCodes.Runtime ? "Runtime"
                : "운영 연결";

        private static string DisclosureLabel(string code)
            => code == 통합전시관DisclosureScopeCodes.OwnerPrivate ? "본인 비공개"
                : code == 통합전시관DisclosureScopeCodes.PrivacySafeAggregate ? "개인정보 제거 집계"
                : code == 통합전시관DisclosureScopeCodes.OrdererPublic ? "주문자 공개"
                : code == 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized ? "마트 운영자 전용"
                : code == 통합전시관DisclosureScopeCodes.RestaurantAuthorized ? "음식점 전용"
                : code == 통합전시관DisclosureScopeCodes.DriverCandidateApproximate ? "기사 후보 권역 축약"
                : code == 통합전시관DisclosureScopeCodes.AssignedDriverAuthorized ? "확정 기사 전용"
                : code;

        private static Color StateColor(string code)
            => code == 통합전시관DataStateCodes.Uncollected
                ? new Color(.78f, .25f, .2f)
                : code == 통합전시관DataStateCodes.Fixture
                    ? new Color(.2f, .55f, .72f)
                    : new Color(.35f, .62f, .38f);

        public static 통합전시관ApiModel CreateFixtureApiModel()
        {
            var generatedAt = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
            return new 통합전시관ApiModel
            {
                StableId = "exhibition-manifest:integrated-seedbed",
                Revision = "exhibition:exh5-fixture-r1",
                GeneratedAtUtc = generatedAt,
                IsReadOnly = true,
                Exhibits = new[]
                {
                    Exhibit(
                        "exhibit:asset-lab:synty", "신티 에셋 모판", "AssetSeedbed",
                        "CommunityTrust", "0.0", "Researcher", "Public",
                        "zone:exhibition:seedbed", "world-object:asset-study-lab",
                        "source:asset-index:synty", "synty-prefab-index:1535-r1", "AssetInventory",
                        통합전시관DataStateCodes.Fixture, 통합전시관ExperienceModeCodes.Research,
                        통합전시관CompletionStateCodes.Verified,
                        new[] { 통합전시관InteractionIntentCodes.Observe, 통합전시관InteractionIntentCodes.ViewLineage, 통합전시관InteractionIntentCodes.Compare },
                        Array.Empty<string>(), new[] { "exhibition.asset-study.sample" },
                        new[] { "Farm", "Town", "City" },
                        "AssetIndex", "asset-index:synty:prefabs", "IndexedAs", "Exhibit", "exhibit:asset-lab:synty",
                        "Verified", "Verified", "Verified", "Verified", "NotApplicable",
                        "unity-change:asset-study-town-city"),
                    Exhibit(
                        "exhibit:farm:potato-lifecycle", "감자 재배·수확 체험", "SimulationLifecycle",
                        "SimulationWorld", "3.5-dev", "Producer", "Personal",
                        "zone:exhibition:farm", "world-object:potato-field-6x6",
                        "source:simulation:potato-cultivation", "potato-cultivation-fixture:r1", "SimulationFixture",
                        통합전시관DataStateCodes.Fixture, 통합전시관ExperienceModeCodes.Simulation,
                        통합전시관CompletionStateCodes.Verified,
                        new[] { 통합전시관InteractionIntentCodes.Observe, 통합전시관InteractionIntentCodes.ViewLineage,
                            통합전시관InteractionIntentCodes.SimulationPreview, 통합전시관InteractionIntentCodes.SimulationConfirm,
                            통합전시관InteractionIntentCodes.RefreshCanonical },
                        new[] { "OperationalCultivationNotConnected" },
                        new[] { "farm.plot.potato-6x6", "farm.harvest-lot.potato" }, new[] { "Farm" },
                        "Product", "product:potato", "CultivatedAs", "CultivationCycle", "cultivation:potato:fixture",
                        "SimulationLinked", "Verified", "Verified", "Verified", "Unverified",
                        "unity-change:potato-cultivation-lifecycle"),
                    CargoHubWarehouseExhibit(),
                    Exhibit(
                        "exhibit:public-data:potato-observation", "감자 현실 관측", "PublicObservation",
                        "CommunityTrust", "0.0", "PublicObserver", "Public",
                        "zone:exhibition:public-data-hall", "world-object:potato-observation-table",
                        "source:public-data:kamis-potato", "kamis-potato-observation:uncollected-r1", "PublicObservation",
                        통합전시관DataStateCodes.Uncollected, 통합전시관ExperienceModeCodes.ReadOnly,
                        통합전시관CompletionStateCodes.Blocked,
                        new[] { 통합전시관InteractionIntentCodes.Observe, 통합전시관InteractionIntentCodes.ViewLineage, 통합전시관InteractionIntentCodes.Compare },
                        new[] { "ActualObservationNotCollected" }, new[] { "public-data.observation.potato" },
                        new[] { "Farm", "Shared" },
                        "Product", "product:potato", "ObservedBy", "PublicObservation", "public-observation:kamis:potato",
                        "Unverified", "Partial", "Verified", "Verified", "Unverified",
                        "unity-change:asset-soil-seedbed"),
                    OrdererGroupUrbanMarketExhibit(),
                    FoodDeliveryExhibit(),
                },
            };
        }

        private static 통합전시관ExhibitApiModel Exhibit(
            string stableId, string name, string kind, string workflow, string version,
            string perspective, string scope, string zoneId, string objectId,
            string sourceId, string sourceRevision, string sourceMode,
            string dataState, string experienceMode, string completionState,
            string[] intents, string[] blockers, string[] visualKeys, string[] packRoles,
            string sourceRecordKind, string sourceRecordId, string relationCode,
            string targetRecordKind, string targetRecordId, string relationStatus,
            string codeEvidence, string testEvidence, string runtimeEvidence,
            string operationalEvidence, string runtimeReference)
            => new 통합전시관ExhibitApiModel
            {
                ExhibitStableId = stableId,
                DisplayName = name,
                ExhibitKindCode = kind,
                WorkflowKey = workflow,
                ProductVersionCode = version,
                PerspectiveCode = perspective,
                AuthorizationScopeCode = scope,
                WorldStableId = "world:integrated-seedbed-exhibition:fixture",
                ZoneStableId = zoneId,
                ObjectStableIds = new[] { objectId },
                CanonicalRecordRelations = new[]
                {
                    new 통합전시관CanonicalRecordRelationApiModel
                    {
                        RelationStableId = "relation:" + stableId,
                        SourceRecordKindCode = sourceRecordKind,
                        SourceStableId = sourceRecordId,
                        SourceRevision = sourceRevision,
                        RelationCode = relationCode,
                        TargetRecordKindCode = targetRecordKind,
                        TargetStableId = targetRecordId,
                        TargetRevision = "exhibit-contract:r1",
                        ExpectedTargetRevision = "exhibit-contract:r1",
                        VerificationStatusCode = relationStatus,
                    },
                },
                WorkflowCheckpoints = Array.Empty<통합전시관WorkflowCheckpointApiModel>(),
                SourcePlan = new[]
                {
                    new 통합전시관SourcePlanSegmentApiModel
                    {
                        SourceKey = sourceId,
                        SourceStableId = sourceId,
                        SourceRevision = sourceRevision,
                        SourceModeCode = sourceMode,
                    },
                },
                SourceRevision = sourceRevision,
                ProjectionRevision = "integrated-exhibition-projector:r1",
                DataStateCode = dataState,
                ExperienceModeCode = experienceMode,
                CompletionStateCode = completionState,
                AllowedInteractionIntentCodes = intents,
                BlockedReasonCodes = blockers,
                VisualKeys = visualKeys,
                PackRoleCodes = packRoles,
                Evidence = new[]
                {
                    Evidence(통합전시관EvidenceKindCodes.Code, codeEvidence, "repo:integrated-exhibition"),
                    Evidence(통합전시관EvidenceKindCodes.FocusedTest, testEvidence, "validation:focused"),
                    Evidence(통합전시관EvidenceKindCodes.Runtime, runtimeEvidence, runtimeReference),
                    Evidence(통합전시관EvidenceKindCodes.Operational, operationalEvidence, "operation:not-asserted"),
                },
            };

        private static 통합전시관ExhibitApiModel CargoHubWarehouseExhibit()
        {
            const string cargo = "cargo:sim.potato.20260407.r3";
            const string request = "shipper-request-candidate:sim.potato.farm-hub.r1";
            const string journey = "cargo-journey:sim.potato.farm-hub";
            const string receiving = "hub-receiving:sim.potato";
            const string handoff = "cargo-handoff:sim.potato.20260407.r3.inbound-91";
            const string warehouse = "warehouse-zone:7";
            var exhibit = Exhibit(
                "exhibit:logistics:cargo-hub-warehouse", "화물·Hub·창고 계보", "CargoHubWarehouseLineage",
                "WarehouseFulfillment", "3.5-dev", "ShipperWarehouse", "RoleScopedFixture",
                "zone:exhibition:cargo-hub-warehouse", cargo,
                "source:simulation:potato-cargo-journey", "potato-cargo-hub-warehouse-fixture:r1", "SimulationFixture",
                통합전시관DataStateCodes.Fixture, 통합전시관ExperienceModeCodes.Simulation,
                통합전시관CompletionStateCodes.Linked,
                new[] { 통합전시관InteractionIntentCodes.Observe, 통합전시관InteractionIntentCodes.ViewLineage,
                    통합전시관InteractionIntentCodes.SimulationPreview, 통합전시관InteractionIntentCodes.RefreshCanonical },
                new[] { "OperationalCargoSnapshotNotLoaded", "WarehouseReceivingCommandNotExposedInExhibition" },
                new[] { "logistics.cargo-truck", "logistics.hub-inbound", "warehouse.inbound-dock" },
                new[] { "Farm", "Town", "City", "Shared" },
                "ShipperRequestCandidate", request, "RequestsTransportOf", "Cargo", cargo,
                "SimulationLinked", "Verified", "Verified", "Verified", "Partial",
                "unity-change:integrated-exhibition-exh3");
            exhibit.ObjectStableIds = new[] { request, cargo, journey, receiving, handoff, warehouse };
            exhibit.SourcePlan = new[]
            {
                Source("simulation:potato-cargo-journey", "potato-cargo-journey-fixture:r1", "SimulationFixture"),
                Source("projection:cargo-warehouse-handoff", "cargo-warehouse-handoff-contract:r1", "OperationalContract"),
                Source("projection:warehouse-world-snapshot", "warehouse-world-snapshot-contract:r1", "AuthorizedOperationalContract"),
            };
            exhibit.CanonicalRecordRelations = new[]
            {
                Relation("request-cargo", "ShipperRequestCandidate", request, "1", "RequestsTransportOf", "Cargo", cargo, "3"),
                Relation("cargo-journey", "Cargo", cargo, "3", "MovedBy", "CargoJourney", journey, "1"),
                Relation("journey-receiving", "CargoJourney", journey, "4", "ArrivesForInspectionAt", "HubReceiving", receiving, "1"),
                Relation("receiving-handoff", "HubReceiving", receiving, "1", "HandsOffThrough", "WarehouseHandoff", handoff, "2"),
                Relation("handoff-warehouse", "WarehouseHandoff", handoff, "2", "ProjectedInto", "WarehouseWorldSnapshot", warehouse, "warehouse-revision-1"),
            };
            exhibit.WorkflowCheckpoints = new[]
            {
                Checkpoint(1, "ShipperRequestCandidate", "Candidate", cargo, request, "1", false, "ShipperRequestDoesNotCreateCargo", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                Checkpoint(2, "CargoJourney", "Loaded", cargo, journey, "1", true, "DispatchConfirmRequired", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                Checkpoint(3, "CargoJourney", "InTransit", cargo, journey, "2", false, "RouteTickOnly", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                Checkpoint(4, "CargoJourney", "ArrivedAtHub", cargo, journey, "4", false, "ArrivalIsNotReceiving", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                Checkpoint(5, "HubReceiving", "Inspection", cargo, receiving, "2", true, "InspectionConfirmRequired", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                Checkpoint(6, "WarehouseHandoff", "ArrivedAtWarehouse", cargo, handoff, "2", true, "WarehouseArrivalIsNotReceiving", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                Checkpoint(7, "WarehouseHandoff", "ReceivingCompleted", cargo, warehouse, "warehouse-revision-1", false, "ReceivingCommandRequired", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
            };
            return exhibit;
        }

        private static 통합전시관ExhibitApiModel OrdererGroupUrbanMarketExhibit()
        {
            const string lineage = "demand-lineage:sim.potato.town-city";
            const string intent = "individual-intent:sim.potato.owner-private";
            const string preview = "grouping-preview:sim.potato.town";
            const string group = "orderer-group-summary:sim.potato.town";
            const string demand = "market-demand-signal:sim.potato.city";
            const string product = "mart-product:sim.potato.public";
            const string inventory = "market-inventory:sim.potato.operator";
            const string task = "market-task:sim.potato.shelf";
            const string kamis = "public-observation:kamis:potato";
            var exhibit = Exhibit(
                "exhibit:town-city:orderer-group-urban-market", "주문자 집단·도심마트 경계",
                "OrdererGroupUrbanMarketLineage", "GroupPurchaseDemand", "3.5-dev",
                "OrdererMarketOperator", "PrivacyPartitionedFixture",
                "zone:exhibition:town-city-market", lineage,
                "simulation:orderer-group-urban-market", "orderer-group-urban-market-fixture:r1", "SimulationFixture",
                통합전시관DataStateCodes.Fixture, 통합전시관ExperienceModeCodes.Simulation,
                통합전시관CompletionStateCodes.Linked,
                new[] { 통합전시관InteractionIntentCodes.Observe, 통합전시관InteractionIntentCodes.ViewLineage,
                    통합전시관InteractionIntentCodes.Compare, 통합전시관InteractionIntentCodes.SimulationPreview },
                new[] { "ExplicitParticipationConsentNotExecuted", "OperationalMarketSnapshotNotLoaded",
                    "SalePriceIsNotKamisObservation", "PublicQuantityIsNotPhysicalInventory" },
                new[] { "town.orderer-group.aggregate", "city.market.public-product", "city.market.operator-inventory" },
                new[] { "Town", "City", "Shared" },
                "IndividualIntent", intent, "AggregatedPrivatelyAs", "GroupingPreview", preview,
                "SimulationLinked", "Verified", "Verified", "Verified", "Partial",
                "unity-change:integrated-exhibition-exh4");
            exhibit.ObjectStableIds = new[] { lineage, preview, group, demand, product, inventory, task, kamis };
            exhibit.SourcePlan = new[]
            {
                Source("simulation:individual-intent-grouping", "orderer-grouping-v2-buyer-context", "SimulationFixture"),
                Source("projection:orderer-group-public-summary", "orderer-group-public-contract:r1", "PrivacySafeAggregateContract"),
                Source("projection:urban-market-public-products", "urban-market-public-products.v1", "OrdererPublicContract"),
                Source("projection:urban-market-operations", "urban-market-operations.v1", "AuthorizedOperationalContract"),
                Source("public-data:kamis-potato-observation", "kamis-potato-observation:uncollected:r1", "PublicObservation"),
            };
            exhibit.CanonicalRecordRelations = new[]
            {
                TownCityRelation("intent-preview", "IndividualIntent", intent, "1", "AggregatedPrivatelyAs", "GroupingPreview", preview, "preview-r1"),
                TownCityRelation("preview-group", "GroupingPreview", preview, "preview-r1", "RequiresConsentBefore", "OrdererGroupSummary", group, "group-r1"),
                TownCityRelation("group-demand", "OrdererGroupSummary", group, "group-r1", "ProjectedAs", "MarketDemandSignal", demand, "demand-r1"),
                TownCityRelation("demand-product", "MarketDemandSignal", demand, "demand-r1", "PresentedAlongside", "MartPublicProduct", product, "product-r1"),
                TownCityRelation("product-inventory", "MartPublicProduct", product, "product-r1", "DoesNotReveal", "MarketOperationalInventory", inventory, "inventory-r1"),
                TownCityRelation("kamis-product", "KamisObservation", kamis, "uncollected-r1", "ComparedWithNotUsedAsSalePrice", "MartPublicProduct", product, "product-r1"),
            };
            exhibit.WorkflowCheckpoints = new[]
            {
                TownCityCheckpoint(1, "IndividualIntent", "Withdrawable", lineage, intent, "1", true, "ParticipationConsentNotGranted", 통합전시관DisclosureScopeCodes.OwnerPrivate),
                TownCityCheckpoint(2, "GroupingPreview", "Candidate", lineage, preview, "1", true, "PreviewDoesNotEnroll", 통합전시관DisclosureScopeCodes.PrivacySafeAggregate),
                TownCityCheckpoint(3, "OrdererGroupSummary", "Recruiting", lineage, group, "1", true, "ExplicitParticipationRequired", 통합전시관DisclosureScopeCodes.PrivacySafeAggregate),
                TownCityCheckpoint(4, "MartPublicProduct", "PublishedProjection", lineage, product, "1", false, "SalePriceIsNotKamisObservation", 통합전시관DisclosureScopeCodes.OrdererPublic),
                TownCityCheckpoint(5, "MarketInventory", "AuthorizedProjection", lineage, inventory, "1", false, "PublicQuantityIsNotPhysicalInventory", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
                TownCityCheckpoint(6, "ShelfTask", "Candidate", lineage, task, "1", true, "OperationalCommandNotExposed", 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized),
            };
            return exhibit;
        }

        private static 통합전시관ExhibitApiModel FoodDeliveryExhibit()
        {
            const string order = "food-order:sim.city-meal.001";
            const string preparation = "restaurant-preparation:sim.city-meal.001";
            const string dispatch = "food-dispatch:sim.city-meal.001";
            const string offer = "food-driver-offer:sim.city-meal.001";
            const string assignment = "food-driver-assignment:sim.city-meal.001";
            const string pickup = "food-pickup-handoff:sim.city-meal.001";
            const string delivery = "food-delivery-handoff:sim.city-meal.001";
            const string receipt = "food-orderer-receipt:sim.city-meal.001";

            var exhibit = Exhibit(
                "exhibit:city:food-delivery", "음식점·기사·주문자 인계", "FoodDeliveryLineage",
                "FoodDelivery", "3.0-dev", "FoodOrderParticipants", "ParticipantPartitionedFixture",
                "zone:exhibition:city-food-delivery", order,
                "simulation:food-delivery", "food-delivery-fixture:r1", "SimulationFixture",
                통합전시관DataStateCodes.Fixture, 통합전시관ExperienceModeCodes.Simulation,
                통합전시관CompletionStateCodes.Linked,
                new[] { 통합전시관InteractionIntentCodes.Observe, 통합전시관InteractionIntentCodes.ViewLineage,
                    통합전시관InteractionIntentCodes.SimulationPreview },
                new[] { "ApproximateDropoffBeforeDriverAcceptance", "DeliveryCompletionIsNotReceiptConfirmation",
                    "OperationalFoodDeliverySnapshotNotLoaded", "FoodDeliveryCommandNotExposedInExhibition" },
                new[] { "city.restaurant.preparation", "city.food-driver.route", "city.orderer.receipt" },
                new[] { "City", "Town", "Shared" },
                "FoodOrder", order, "PreparedBy", "RestaurantPreparation", preparation,
                "SimulationLinked", "Verified", "Verified", "Verified", "Partial",
                "unity-change:integrated-exhibition-exh5");
            exhibit.ObjectStableIds = new[] { order, preparation, dispatch, offer, assignment, pickup, delivery, receipt };
            exhibit.SourcePlan = new[]
            {
                Source("simulation:food-delivery", "simulation-food-delivery-contract:r1", "SimulationFixture"),
                Source("projection:food-order", "food-order-contract:r1", "ParticipantOperationalContract"),
                Source("projection:food-driver-workspace", "food-driver-workspace-contract:r1", "DriverCandidateApproximateContract"),
                Source("projection:orderer-food-delivery-progress", "orderer-food-delivery-progress:r1", "OwnerAuthorizedContract"),
            };
            exhibit.CanonicalRecordRelations = new[]
            {
                FoodRelation("order-preparation", "FoodOrder", order, "1", "PreparedBy", "RestaurantPreparation", preparation, "1"),
                FoodRelation("preparation-dispatch", "RestaurantPreparation", preparation, "2", "RequestsDeliveryThrough", "FoodDispatchQueue", dispatch, "1"),
                FoodRelation("dispatch-offer", "FoodDispatchQueue", dispatch, "1", "RecommendedAs", "DriverOffer", offer, "1"),
                FoodRelation("offer-assignment", "DriverOffer", offer, "1", "RequiresDriverAcceptanceFor", "DriverAssignment", assignment, "1"),
                FoodRelation("assignment-pickup", "DriverAssignment", assignment, "1", "AuthorizesPickupOf", "FoodPickupHandoff", pickup, "1"),
                FoodRelation("pickup-delivery", "FoodPickupHandoff", pickup, "1", "DeliveredThrough", "FoodDeliveryHandoff", delivery, "1"),
                FoodRelation("delivery-receipt", "FoodDeliveryHandoff", delivery, "1", "RequiresSeparateReceiptConfirmation", "OrdererReceipt", receipt, "1"),
            };
            exhibit.WorkflowCheckpoints = new[]
            {
                FoodCheckpoint(1, "FoodOrder", "주문대기", order, order, "1", true, "OrderConfirmRequired", 통합전시관DisclosureScopeCodes.OwnerPrivate),
                FoodCheckpoint(2, "RestaurantPreparation", "조리중", order, preparation, "1", true, "RestaurantAcceptanceRequired", 통합전시관DisclosureScopeCodes.RestaurantAuthorized),
                FoodCheckpoint(3, "RestaurantPreparation", "픽업대기", order, preparation, "2", true, "RestaurantPickupReadyRequired", 통합전시관DisclosureScopeCodes.RestaurantAuthorized),
                FoodCheckpoint(4, "DriverOffer", "추천중", order, offer, "1", false, "ApproximateDropoffBeforeDriverAcceptance", 통합전시관DisclosureScopeCodes.DriverCandidateApproximate),
                FoodCheckpoint(5, "DriverAssignment", "기사배정", order, assignment, "1", true, "DriverSelfAcceptanceRequired", 통합전시관DisclosureScopeCodes.AssignedDriverAuthorized),
                FoodCheckpoint(6, "FoodDelivery", "픽업완료", order, pickup, "1", true, "AssignedDriverPickupRequired", 통합전시관DisclosureScopeCodes.AssignedDriverAuthorized),
                FoodCheckpoint(7, "FoodDelivery", "전달완료", order, delivery, "1", true, "DeliveryCompletionIsNotReceiptConfirmation", 통합전시관DisclosureScopeCodes.AssignedDriverAuthorized),
                FoodCheckpoint(8, "OrdererReceipt", "수령확인", order, receipt, "1", true, "OrdererReceiptConfirmationRequired", 통합전시관DisclosureScopeCodes.OwnerPrivate),
            };
            return exhibit;
        }

        private static 통합전시관SourcePlanSegmentApiModel Source(string stableId, string revision, string mode)
            => new 통합전시관SourcePlanSegmentApiModel
            {
                SourceKey = stableId,
                SourceStableId = stableId,
                SourceRevision = revision,
                SourceModeCode = mode,
            };

        private static 통합전시관CanonicalRecordRelationApiModel Relation(
            string key, string sourceKind, string sourceId, string sourceRevision,
            string code, string targetKind, string targetId, string targetRevision)
            => new 통합전시관CanonicalRecordRelationApiModel
            {
                RelationStableId = "relation:exhibit-logistics:" + key,
                SourceRecordKindCode = sourceKind,
                SourceStableId = sourceId,
                SourceRevision = sourceRevision,
                RelationCode = code,
                TargetRecordKindCode = targetKind,
                TargetStableId = targetId,
                TargetRevision = targetRevision,
                ExpectedTargetRevision = targetRevision,
                VerificationStatusCode = "SimulationLinked",
            };

        private static 통합전시관CanonicalRecordRelationApiModel TownCityRelation(
            string key, string sourceKind, string sourceId, string sourceRevision,
            string code, string targetKind, string targetId, string targetRevision)
        {
            var value = Relation(key, sourceKind, sourceId, sourceRevision,
                code, targetKind, targetId, targetRevision);
            value.RelationStableId = "relation:exhibit-town-city:" + key;
            return value;
        }

        private static 통합전시관CanonicalRecordRelationApiModel FoodRelation(
            string key, string sourceKind, string sourceId, string sourceRevision,
            string code, string targetKind, string targetId, string targetRevision)
        {
            var value = Relation(key, sourceKind, sourceId, sourceRevision,
                code, targetKind, targetId, targetRevision);
            value.RelationStableId = "relation:exhibit-food-delivery:" + key;
            return value;
        }

        private static 통합전시관WorkflowCheckpointApiModel Checkpoint(
            int sequence, string machine, string state, string lineage, string canonical,
            string revision, bool confirm, string boundary, string disclosureScope)
            => new 통합전시관WorkflowCheckpointApiModel
            {
                CheckpointStableId = "checkpoint:exhibit-logistics:" + sequence,
                Sequence = sequence,
                StateMachineCode = machine,
                StateCode = state,
                LineageStableId = lineage,
                CanonicalRecordStableId = canonical,
                Revision = revision,
                AuthorityCode = 통합전시관CheckpointAuthorityCodes.SimulationFixture,
                DisclosureScopeCode = disclosureScope,
                RequiresSeparateConfirmation = confirm,
                BoundaryCode = boundary,
            };

        private static 통합전시관WorkflowCheckpointApiModel TownCityCheckpoint(
            int sequence, string machine, string state, string lineage, string canonical,
            string revision, bool confirm, string boundary, string disclosureScope)
        {
            var value = Checkpoint(sequence, machine, state, lineage, canonical,
                revision, confirm, boundary, disclosureScope);
            value.CheckpointStableId = "checkpoint:exhibit-town-city:" + sequence;
            return value;
        }

        private static 통합전시관WorkflowCheckpointApiModel FoodCheckpoint(
            int sequence, string machine, string state, string lineage, string canonical,
            string revision, bool confirm, string boundary, string disclosureScope)
        {
            var value = Checkpoint(sequence, machine, state, lineage, canonical,
                revision, confirm, boundary, disclosureScope);
            value.CheckpointStableId = "checkpoint:exhibit-food-delivery:" + sequence;
            return value;
        }

        private static 통합전시관EvidenceApiModel Evidence(string kind, string status, string reference)
            => new 통합전시관EvidenceApiModel
            {
                EvidenceKindCode = kind,
                StatusCode = status,
                Reference = reference,
                Note = "EXH-0 독립 증거 축",
            };
    }
}
