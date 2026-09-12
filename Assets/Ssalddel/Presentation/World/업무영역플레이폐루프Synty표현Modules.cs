using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// Nature에서 검증한 표현 모듈 계약을 Farm·Town·Hub·City의 독립 폐루프로 확장한 골격이다.
    /// 영역 간 운송을 기본 폐루프로 만들지 않으며 실제 Scene 배치는 별도 E5 이상 증거가 필요하다.
    /// </summary>
    public static class 업무영역플레이폐루프Synty표현Modules
    {
        public static IReadOnlyList<플레이폐루프Synty표현Module> All { get; } =
            new[]
            {
                FarmCropCycle(),
                FarmPackStoreReturn(),
                TownOrderConsumeReturn(),
                HubInboundPutaway(),
                HubOutboundReadyReturn(),
                CityDemandServiceReturn(),
            };

        private static 플레이폐루프Synty표현Module FarmCropCycle() => Module(
            "synty-loop:farm-crop-cycle.v1",
            "playable-loop:farm-crop-cycle.v1",
            "farm-crop-synty.r1",
            new[] { "WI-FARM-01", "WI-FARM-02", "WI-FARM-03", "WI-FARM-04" },
            new[] { "CultivationWorkArea", "HarvestWorkArea" },
            Slot("farm.crop.prepare", "WI-FARM-01", 플레이폐루프표현MomentCodes.Entry,
                Synty표현PlacementRoleCodes.SpatialBase, "CultivationReady",
                "synty-family:farm:plants:potato"),
            Slot("farm.crop.plant", "WI-FARM-02", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "CropPlanted",
                "synty-family:farm:props:bucket"),
            Slot("farm.crop.grow", "WI-FARM-03", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.StateOverlay, "CropGrowing",
                "synty-family:farm:plants:potato"),
            Slot("farm.crop.harvest", "WI-FARM-04", 플레이폐루프표현MomentCodes.Return,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "CropHarvested",
                "synty-family:farm:props:crate"));

        private static 플레이폐루프Synty표현Module FarmPackStoreReturn() => Module(
            "synty-loop:farm-pack-store-return.v1",
            "playable-loop:farm-pack-store-return.v1",
            "farm-pack-store-synty.r1",
            new[] { "WI-FARM-05", "WI-FARM-06" },
            new[] { "CollectionWorkArea", "PackingWorkArea", "FarmInternalStorage" },
            Slot("farm.pack", "WI-FARM-05", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "FarmPackingActive",
                "synty-family:farm:props:crate"),
            Slot("farm.store.return", "WI-FARM-06", 플레이폐루프표현MomentCodes.Return,
                Synty표현PlacementRoleCodes.InteriorFixture, "FarmStorageUpdated",
                "synty-family:farm:props:rack"));

        private static 플레이폐루프Synty표현Module TownOrderConsumeReturn() => Module(
            "synty-loop:town-order-consume-return.v1",
            "playable-loop:town-order-consume-return.v1",
            "town-order-synty.r1",
            Enumerable.Range(1, 7).Select(value => $"WI-ORDER-{value:00}").ToArray(),
            new[] { "OrderWorkArea", "PickingWorkArea", "PackingWorkArea", "ResidentPickupArea", "ConsumptionPlace" },
            Slot("town.order.request", "WI-ORDER-01", 플레이폐루프표현MomentCodes.Entry,
                Synty표현PlacementRoleCodes.InteriorFixture, "TownOrderRequested",
                "synty-family:town:props:shopcounter"),
            Slot("town.order.accept", "WI-ORDER-02", 플레이폐루프표현MomentCodes.Choice,
                Synty표현PlacementRoleCodes.Actor, "TownOrderAccepted",
                "synty-family:town:characters:shopkeeper"),
            Slot("town.order.pick", "WI-ORDER-03", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.InteriorFixture, "TownPickingActive",
                "synty-family:town:props:shelf"),
            Slot("town.order.pack", "WI-ORDER-04", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.InteriorLooseItem, "TownPackingActive",
                "synty-family:town:props:cardboardbox"),
            Slot("town.order.handoff", "WI-ORDER-05", 플레이폐루프표현MomentCodes.Success,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "TownOrderHandedOff",
                "synty-family:town:props:cart"),
            Slot("town.order.consume", "WI-ORDER-06", 플레이폐루프표현MomentCodes.Success,
                Synty표현PlacementRoleCodes.InteriorLooseItem, "TownConsumptionActive",
                "synty-family:town:props:cake"),
            Slot("town.order.return", "WI-ORDER-07", 플레이폐루프표현MomentCodes.Return,
                Synty표현PlacementRoleCodes.FeedbackFx, "TownNextNeedAvailable",
                "synty-family:city:fx:steam"));

        private static 플레이폐루프Synty표현Module HubInboundPutaway() => Module(
            "synty-loop:hub-inbound-putaway.v1",
            "playable-loop:hub-inbound-putaway.v1",
            "hub-inbound-synty.r1",
            new[] { "WI-001", "WI-002" },
            new[] { "ReceivingWorkArea", "InspectionWorkArea", "StorageCapacity" },
            Slot("hub.inbound.inspect", "WI-001", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "InboundInspectionActive",
                "synty-family:construction:items:clipboard"),
            Slot("hub.inbound.putaway", "WI-002", 플레이폐루프표현MomentCodes.Return,
                Synty표현PlacementRoleCodes.InteriorFixture, "InboundPutawayCompleted",
                "synty-family:town:props:shelf"));

        private static 플레이폐루프Synty표현Module HubOutboundReadyReturn() => Module(
            "synty-loop:hub-outbound-ready-return.v1",
            "playable-loop:hub-outbound-ready-return.v1",
            "hub-outbound-synty.r1",
            new[] { "WI-HUB-03", "WI-HUB-04", "WI-HUB-05" },
            new[] { "StorageCapacity", "PickingWorkArea", "OutboundStagingArea" },
            Slot("hub.outbound.pick", "WI-HUB-03", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.InteriorFixture, "OutboundPickingActive",
                "synty-family:town:props:shelf"),
            Slot("hub.outbound.pack", "WI-HUB-04", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.InteriorLooseItem, "OutboundPackingActive",
                "synty-family:construction:props:cardboardbox"),
            Slot("hub.outbound.ready", "WI-HUB-05", 플레이폐루프표현MomentCodes.Return,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "OutboundReady",
                "synty-family:construction:props:pallet"));

        private static 플레이폐루프Synty표현Module CityDemandServiceReturn() => Module(
            "synty-loop:city-demand-service-return.v1",
            "playable-loop:city-demand-service-return.v1",
            "city-demand-synty.r1",
            new[] { "WI-CITY-01", "WI-CITY-02", "WI-CITY-03", "WI-CITY-04" },
            new[] { "UrbanDemandArea", "UrbanSortingArea", "LastMileHandoffArea", "UrbanRecoveryRoute" },
            Slot("city.demand", "WI-CITY-01", 플레이폐루프표현MomentCodes.Entry,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "UrbanDemandVisible",
                "synty-family:city:props:billboard"),
            Slot("city.sort", "WI-CITY-02", 플레이폐루프표현MomentCodes.Progress,
                Synty표현PlacementRoleCodes.InteriorLooseItem, "UrbanSortingActive",
                "synty-family:city:props:cardboardbox"),
            Slot("city.service", "WI-CITY-03", 플레이폐루프표현MomentCodes.Success,
                Synty표현PlacementRoleCodes.Actor, "LastMileServiceActive",
                "synty-family:city:vehicles:car-van"),
            Slot("city.return", "WI-CITY-04", 플레이폐루프표현MomentCodes.Return,
                Synty표현PlacementRoleCodes.FunctionalAnchor, "UrbanRecoveryAvailable",
                "synty-family:city:props:busstop"));

        private static 플레이폐루프Synty표현Module Module(
            string moduleStableId,
            string loopStableId,
            string revision,
            string[] worldInteractionIds,
            string[] requiredHCapabilities,
            params 플레이폐루프Synty표현Slot[] slots)
        {
            var module = new 플레이폐루프Synty표현Module(
                moduleStableId,
                loopStableId,
                revision,
                Array.Empty<string>(),
                worldInteractionIds,
                requiredHCapabilities,
                slots);
            module.Validate();
            return module;
        }

        private static 플레이폐루프Synty표현Slot Slot(
            string slotStableId,
            string worldInteractionId,
            string momentCode,
            string placementRoleCode,
            string authorityStateCode,
            params string[] assetFamilyIds) =>
            new 플레이폐루프Synty표현Slot(
                slotStableId,
                worldInteractionId,
                momentCode,
                placementRoleCode,
                new[] { authorityStateCode },
                assetFamilyIds);
    }
}
