using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Runtime.World
{
    public static class SimulationObservationScaleCodes
    {
        public const string WorldMap = "WorldMap";
        public const string Settlement = "Settlement";
        public const string District = "District";
        public const string Object = "Object";

        public static bool IsKnown(string value)
            => value == WorldMap || value == Settlement
                || value == District || value == Object;
    }

    public sealed class SimulationWorldDistrictNode
    {
        private readonly HashSet<string> objectStableIds;

        public SimulationWorldDistrictNode(
            string districtStableId,
            IEnumerable<string> containedObjectStableIds)
        {
            DistrictStableId = Required(districtStableId, "SimulationDistrictStableIdMissing");
            objectStableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stableId in containedObjectStableIds
                         ?? throw new ArgumentNullException(nameof(containedObjectStableIds)))
            {
                if (!objectStableIds.Add(Required(stableId, "SimulationObjectStableIdMissing")))
                    throw new InvalidOperationException("SimulationObjectStableIdDuplicate:" + stableId);
            }
        }

        public string DistrictStableId { get; }

        public bool ContainsObject(string stableId)
            => !string.IsNullOrWhiteSpace(stableId) && objectStableIds.Contains(stableId);

        private static string Required(string value, string error)
            => !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException(error);
    }

    public sealed class SimulationWorldSettlementNode
    {
        private readonly Dictionary<string, SimulationWorldDistrictNode> districts;

        public SimulationWorldSettlementNode(
            string settlementStableId,
            IEnumerable<SimulationWorldDistrictNode> districtNodes)
        {
            SettlementStableId = Required(settlementStableId, "SimulationSettlementStableIdMissing");
            districts = new Dictionary<string, SimulationWorldDistrictNode>(StringComparer.Ordinal);
            foreach (var district in districtNodes
                         ?? throw new ArgumentNullException(nameof(districtNodes)))
            {
                if (district == null)
                    throw new InvalidOperationException("SimulationDistrictNodeMissing");
                if (!districts.TryAdd(district.DistrictStableId, district))
                    throw new InvalidOperationException(
                        "SimulationDistrictStableIdDuplicate:" + district.DistrictStableId);
            }
        }

        public string SettlementStableId { get; }
        public int DistrictCount => districts.Count;

        public bool ContainsDistrict(string stableId)
            => !string.IsNullOrWhiteSpace(stableId) && districts.ContainsKey(stableId);

        public bool ContainsObject(string districtStableId, string objectStableId)
            => districts.TryGetValue(districtStableId, out var district)
                && district.ContainsObject(objectStableId);

        private static string Required(string value, string error)
            => !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException(error);
    }

    /// <summary>
    /// Immutable read model for the World Shell. Presentation does not calculate these values.
    /// </summary>
    public sealed class SimulationWorldShellSnapshot
    {
        private readonly Dictionary<string, SimulationWorldSettlementNode> settlements;

        public SimulationWorldShellSnapshot(
            string sessionStableId,
            long worldRevision,
            long worldTick,
            string gameDateLabel,
            decimal treasury,
            decimal laborAvailable,
            decimal laborReserved,
            decimal marketFoodSupplyKg,
            decimal reserveFoodKg,
            decimal foodSecurityDays,
            int activeTaskCount,
            string sourceModeCode,
            IEnumerable<SimulationWorldSettlementNode> settlementNodes)
        {
            SessionStableId = Required(sessionStableId, "SimulationSessionStableIdMissing");
            if (worldRevision < 0) throw new InvalidOperationException("SimulationWorldRevisionInvalid");
            if (worldTick < 0) throw new InvalidOperationException("SimulationWorldTickInvalid");
            if (treasury < 0 || laborAvailable < 0 || laborReserved < 0
                || marketFoodSupplyKg < 0 || reserveFoodKg < 0
                || foodSecurityDays < 0 || activeTaskCount < 0)
                throw new InvalidOperationException("SimulationWorldShellMetricInvalid");

            WorldRevision = worldRevision;
            WorldTick = worldTick;
            GameDateLabel = Required(gameDateLabel, "SimulationGameDateMissing");
            Treasury = treasury;
            LaborAvailable = laborAvailable;
            LaborReserved = laborReserved;
            MarketFoodSupplyKg = marketFoodSupplyKg;
            ReserveFoodKg = reserveFoodKg;
            FoodSecurityDays = foodSecurityDays;
            ActiveTaskCount = activeTaskCount;
            SourceModeCode = Required(sourceModeCode, "SimulationSourceModeMissing");

            settlements = new Dictionary<string, SimulationWorldSettlementNode>(StringComparer.Ordinal);
            foreach (var settlement in settlementNodes
                         ?? throw new ArgumentNullException(nameof(settlementNodes)))
            {
                if (settlement == null)
                    throw new InvalidOperationException("SimulationSettlementNodeMissing");
                if (!settlements.TryAdd(settlement.SettlementStableId, settlement))
                    throw new InvalidOperationException(
                        "SimulationSettlementStableIdDuplicate:" + settlement.SettlementStableId);
            }
            if (settlements.Count == 0)
                throw new InvalidOperationException("SimulationSettlementMissing");
        }

        public string SessionStableId { get; }
        public long WorldRevision { get; }
        public long WorldTick { get; }
        public string GameDateLabel { get; }
        public decimal Treasury { get; }
        public decimal LaborAvailable { get; }
        public decimal LaborReserved { get; }
        public decimal MarketFoodSupplyKg { get; }
        public decimal ReserveFoodKg { get; }
        public decimal FoodSecurityDays { get; }
        public int ActiveTaskCount { get; }
        public string SourceModeCode { get; }
        public int SettlementCount => settlements.Count;

        public bool ContainsSettlement(string stableId)
            => !string.IsNullOrWhiteSpace(stableId) && settlements.ContainsKey(stableId);

        public bool ContainsDistrict(string settlementStableId, string districtStableId)
            => settlements.TryGetValue(settlementStableId, out var settlement)
                && settlement.ContainsDistrict(districtStableId);

        public bool ContainsObject(
            string settlementStableId,
            string districtStableId,
            string objectStableId)
            => settlements.TryGetValue(settlementStableId, out var settlement)
                && settlement.ContainsObject(districtStableId, objectStableId);

        private static string Required(string value, string error)
            => !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException(error);
    }

    public sealed class SimulationWorldViewState
    {
        public string ObservationScaleCode { get; internal set; } =
            SimulationObservationScaleCodes.WorldMap;
        public string SelectedSettlementStableId { get; internal set; } = string.Empty;
        public string SelectedDistrictStableId { get; internal set; } = string.Empty;
        public string SelectedObjectStableId { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Presentation navigation state only. It has no command, tick or mutation port.
    /// </summary>
    public sealed class SimulationWorldShellStateMachine
    {
        public SimulationWorldShellStateMachine(SimulationWorldShellSnapshot initialSnapshot)
        {
            Snapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            State = new SimulationWorldViewState();
        }

        public SimulationWorldShellSnapshot Snapshot { get; private set; }
        public SimulationWorldViewState State { get; }

        public void ShowWorldMap()
        {
            State.ObservationScaleCode = SimulationObservationScaleCodes.WorldMap;
            State.SelectedDistrictStableId = string.Empty;
            State.SelectedObjectStableId = string.Empty;
        }

        public void ShowSettlement(string settlementStableId)
        {
            if (!Snapshot.ContainsSettlement(settlementStableId))
                throw new InvalidOperationException(
                    "SimulationSettlementSelectionUnknown:" + settlementStableId);
            State.ObservationScaleCode = SimulationObservationScaleCodes.Settlement;
            State.SelectedSettlementStableId = settlementStableId;
            State.SelectedDistrictStableId = string.Empty;
            State.SelectedObjectStableId = string.Empty;
        }

        public void ShowDistrict(string districtStableId)
        {
            if (!Snapshot.ContainsDistrict(State.SelectedSettlementStableId, districtStableId))
                throw new InvalidOperationException(
                    "SimulationDistrictSelectionUnknown:" + districtStableId);
            State.ObservationScaleCode = SimulationObservationScaleCodes.District;
            State.SelectedDistrictStableId = districtStableId;
            State.SelectedObjectStableId = string.Empty;
        }

        public void ShowObject(string objectStableId)
        {
            if (!Snapshot.ContainsObject(
                    State.SelectedSettlementStableId,
                    State.SelectedDistrictStableId,
                    objectStableId))
                throw new InvalidOperationException(
                    "SimulationObjectSelectionUnknown:" + objectStableId);
            State.ObservationScaleCode = SimulationObservationScaleCodes.Object;
            State.SelectedObjectStableId = objectStableId;
        }

        public void Back()
        {
            if (State.ObservationScaleCode == SimulationObservationScaleCodes.Object)
            {
                State.ObservationScaleCode = SimulationObservationScaleCodes.District;
                State.SelectedObjectStableId = string.Empty;
                return;
            }
            if (State.ObservationScaleCode == SimulationObservationScaleCodes.District)
            {
                State.ObservationScaleCode = SimulationObservationScaleCodes.Settlement;
                State.SelectedDistrictStableId = string.Empty;
                State.SelectedObjectStableId = string.Empty;
                return;
            }
            if (State.ObservationScaleCode == SimulationObservationScaleCodes.Settlement)
                ShowWorldMap();
        }

        public void ApplySnapshot(SimulationWorldShellSnapshot nextSnapshot)
        {
            if (nextSnapshot == null) throw new ArgumentNullException(nameof(nextSnapshot));
            var sessionChanged = !string.Equals(
                Snapshot.SessionStableId,
                nextSnapshot.SessionStableId,
                StringComparison.Ordinal);
            if (!sessionChanged && nextSnapshot.WorldRevision < Snapshot.WorldRevision)
                throw new InvalidOperationException("SimulationWorldSnapshotRevisionRegressed");

            Snapshot = nextSnapshot;
            if (sessionChanged
                || !Snapshot.ContainsSettlement(State.SelectedSettlementStableId))
            {
                ClearSelection();
                return;
            }

            if (!Snapshot.ContainsDistrict(
                    State.SelectedSettlementStableId,
                    State.SelectedDistrictStableId))
            {
                State.SelectedDistrictStableId = string.Empty;
                State.SelectedObjectStableId = string.Empty;
                if (State.ObservationScaleCode == SimulationObservationScaleCodes.District
                    || State.ObservationScaleCode == SimulationObservationScaleCodes.Object)
                    State.ObservationScaleCode = SimulationObservationScaleCodes.Settlement;
                return;
            }

            if (!Snapshot.ContainsObject(
                    State.SelectedSettlementStableId,
                    State.SelectedDistrictStableId,
                    State.SelectedObjectStableId))
            {
                State.SelectedObjectStableId = string.Empty;
                if (State.ObservationScaleCode == SimulationObservationScaleCodes.Object)
                    State.ObservationScaleCode = SimulationObservationScaleCodes.District;
            }
        }

        private void ClearSelection()
        {
            State.ObservationScaleCode = SimulationObservationScaleCodes.WorldMap;
            State.SelectedSettlementStableId = string.Empty;
            State.SelectedDistrictStableId = string.Empty;
            State.SelectedObjectStableId = string.Empty;
        }
    }

    public static class SimulationWorldShellFixture
    {
        public const string SessionStableId = "simulation-session:world-shell-0.fixture";
        public const string SettlementStableId = "settlement:fixture:first";

        public static SimulationWorldShellSnapshot CreateSnapshot()
            => new(
                SessionStableId,
                12,
                12,
                "Year 1 · 04-12",
                12500m,
                18m,
                6m,
                420m,
                980m,
                12.94m,
                2,
                "SimulationFixture",
                new[]
                {
                    new SimulationWorldSettlementNode(
                        SettlementStableId,
                        new[]
                        {
                            District("district:farm", "harvest-lot:potato-001"),
                            District("district:town"),
                            District("district:market"),
                            District("district:storage"),
                            District("district:logistics"),
                            District("district:residential"),
                            District("district:garrison"),
                            District("district:gate"),
                        }),
                });

        private static SimulationWorldDistrictNode District(
            string stableId,
            params string[] objectStableIds)
            => new(stableId, objectStableIds);
    }
}
