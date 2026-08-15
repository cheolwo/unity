using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Npc업무행동PhaseCodes
    {
        public const string Scheduled = "Scheduled";
        public const string Navigating = "Navigating";
        public const string Working = "Working";
        public const string Completed = "Completed";
        public const string Blocked = "Blocked";

        public static bool IsKnown(string value)
            => value == Scheduled || value == Navigating || value == Working
                || value == Completed || value == Blocked;
    }

    [Serializable]
    public sealed class Npc업무행동ProjectionData
    {
        public string ProjectionStableId = string.Empty;
        public string ActorStableId = string.Empty;
        public string TaskStableId = string.Empty;
        public string FacilityStableId = string.Empty;
        public string InteractionPointKey = string.Empty;
        public string ActionVisualKey = string.Empty;
        public string PhaseCode = string.Empty;
        public decimal ProgressRate;
        public string[] BlockReasonCodes = Array.Empty<string>();
        public long Revision;
        public int WorldTick;
        public bool PresentationOnly = true;

        public bool Validate()
            => !string.IsNullOrWhiteSpace(ProjectionStableId)
                && !string.IsNullOrWhiteSpace(TaskStableId)
                && !string.IsNullOrWhiteSpace(FacilityStableId)
                && !string.IsNullOrWhiteSpace(InteractionPointKey)
                && !string.IsNullOrWhiteSpace(ActionVisualKey)
                && Npc업무행동PhaseCodes.IsKnown(PhaseCode)
                && (PhaseCode == Npc업무행동PhaseCodes.Blocked
                    || !string.IsNullOrWhiteSpace(ActorStableId))
                && ProgressRate is >= 0m and <= 1m
                && BlockReasonCodes != null
                && Revision >= 0
                && WorldTick >= 0
                && PresentationOnly;

        public Npc업무행동ProjectionData Clone()
            => new()
            {
                ProjectionStableId = ProjectionStableId,
                ActorStableId = ActorStableId,
                TaskStableId = TaskStableId,
                FacilityStableId = FacilityStableId,
                InteractionPointKey = InteractionPointKey,
                ActionVisualKey = ActionVisualKey,
                PhaseCode = PhaseCode,
                ProgressRate = ProgressRate,
                BlockReasonCodes = BlockReasonCodes.ToArray(),
                Revision = Revision,
                WorldTick = WorldTick,
                PresentationOnly = PresentationOnly,
            };
    }

    public sealed class Npc업무행동ProjectionStore
    {
        private readonly Dictionary<string, Npc업무행동ProjectionData> _projections =
            new(StringComparer.Ordinal);

        public IReadOnlyList<Npc업무행동ProjectionData> Current
            => _projections.Values
                .OrderBy(value => value.ProjectionStableId, StringComparer.Ordinal)
                .Select(value => value.Clone()).ToArray();

        public void Apply(IEnumerable<Npc업무행동ProjectionData> projections)
        {
            if (projections == null) throw new ArgumentNullException(nameof(projections));
            var incoming = projections.ToArray();
            if (incoming.Any(value => value == null || !value.Validate()))
                throw new InvalidOperationException("NpcWorkActionProjectionInvalid");
            if (incoming.Select(value => value.ProjectionStableId)
                .Distinct(StringComparer.Ordinal).Count() != incoming.Length)
                throw new InvalidOperationException("NpcWorkActionProjectionDuplicate");

            foreach (var value in incoming)
            {
                if (_projections.TryGetValue(value.ProjectionStableId, out var previous)
                    && (value.Revision < previous.Revision
                        || value.WorldTick < previous.WorldTick))
                    throw new InvalidOperationException("NpcWorkActionProjectionRegressed");
                _projections[value.ProjectionStableId] = value.Clone();
            }
        }
    }

    public static class 평창진부HubNpc업무행동Fixture
    {
        public const string ManagerActorStableId =
            "actor:sim:pyeongchang:jinbu-hub-manager";
        public const string InboundOperatorActorStableId =
            "actor:sim:pyeongchang:jinbu-inbound-operator";
        public const string AssistantActorStableId =
            "actor:sim:pyeongchang:jinbu-logistics-assistant";
        public const string FacilityStableId =
            "facility:sim:pyeongchang:jinbu-hub";
        public const string InteractionPointKey =
            "interaction-point:jinbu-hub:inbound-inspection";
        public const string ActionVisualKey =
            "action-visual:warehouse:inbound-inspection";

        public static Npc업무행동ProjectionData Create(
            string phaseCode = Npc업무행동PhaseCodes.Scheduled,
            int worldTick = 0,
            long revision = 1,
            decimal progressRate = 0m)
            => new()
            {
                ProjectionStableId =
                    "npc-action-projection:npc-assignment:task:freight-receipt:fixture",
                ActorStableId = InboundOperatorActorStableId,
                TaskStableId = "task:freight-receipt:fixture",
                FacilityStableId = FacilityStableId,
                InteractionPointKey = InteractionPointKey,
                ActionVisualKey = ActionVisualKey,
                PhaseCode = phaseCode,
                ProgressRate = progressRate,
                Revision = revision,
                WorldTick = worldTick,
                PresentationOnly = true,
            };
    }
}
