using System;
using Ssalddel.Unity.Exhibition;

namespace Ssalddel.Unity.Runtime.ExhibitionFixtures
{
    /// <summary>
    /// 서버 연결 전에도 통합 전시관의 데이터 연결과 공개 범위를 검증할 수 있는
    /// 읽기 전용 예행 연습 상태를 만든다. 실운영 서버 상태를 대신하지 않는다.
    /// </summary>
    public static class 통합전시관FixtureApiModelFactory
    {
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
