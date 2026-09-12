using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class 산수몽지도ViewState
    {
        public long Revision { get; set; }
        public string CurrentTileKey { get; set; } = string.Empty;
        public SimulationMapKnowledgeEntrySnapshot[] FullMapEntries { get; set; }
            = Array.Empty<SimulationMapKnowledgeEntrySnapshot>();
        public SimulationMapKnowledgeEntrySnapshot[] MiniMapEntries { get; set; }
            = Array.Empty<SimulationMapKnowledgeEntrySnapshot>();
    }

    public static class 산수몽지도Projection
    {
        public static 산수몽지도ViewState Create(
            SimulationWorldExplorationStateSnapshot snapshot,
            string actorStableId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var entries = (snapshot.MapKnowledgeEntries
                    ?? Array.Empty<SimulationMapKnowledgeEntrySnapshot>())
                .OrderBy(value => value.KindCode, StringComparer.Ordinal)
                .ThenBy(value => value.SpatialStableId, StringComparer.Ordinal)
                .ToArray();
            var currentTile = (snapshot.ActorTilePositions
                    ?? Array.Empty<SimulationActorTilePositionSnapshot>())
                .SingleOrDefault(value => string.Equals(value.ActorStableId,
                    actorStableId, StringComparison.Ordinal))?.CurrentL2TileKey
                ?? string.Empty;
            return new 산수몽지도ViewState
            {
                Revision = snapshot.Revision,
                CurrentTileKey = currentTile,
                FullMapEntries = entries,
                MiniMapEntries = entries.Where(value =>
                        value.KindCode == SimulationMapKnowledgeKindCodes.Route
                        || (value.KnowledgeLevelCode
                                == SimulationMapKnowledgeLevelCodes.Confirmed
                            && IsNear(value, currentTile)))
                    .ToArray(),
            };
        }

        private static bool IsNear(SimulationMapKnowledgeEntrySnapshot entry,
            string currentTile)
        {
            if (string.IsNullOrWhiteSpace(currentTile)) return false;
            if (!TryParse(currentTile, out var currentX, out var currentY))
                return entry.RelatedL2TileKeys.Contains(currentTile,
                    StringComparer.Ordinal);
            return (entry.RelatedL2TileKeys ?? Array.Empty<string>()).Any(tile =>
                TryParse(tile, out var x, out var y)
                && Math.Abs(x - currentX) <= 1 && Math.Abs(y - currentY) <= 1);
        }

        private static bool TryParse(string tileKey, out int x, out int y)
        {
            x = 0; y = 0;
            var parts = (tileKey ?? string.Empty).Split(':');
            return parts.Length == 4 && int.TryParse(parts[2], out x)
                && int.TryParse(parts[3], out y);
        }
    }
}
