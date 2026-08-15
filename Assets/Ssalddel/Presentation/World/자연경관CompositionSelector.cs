using System;
using System.Linq;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed class 자연경관CompositionSelector
    {
        public string ResolveVariant(
            string setName,
            string worldSlotStableKey,
            int seed)
        {
            if (!자연경관SetNames.All.Contains(setName, StringComparer.Ordinal))
                throw new ArgumentException("NatureSetUnknown", nameof(setName));
            if (string.IsNullOrWhiteSpace(worldSlotStableKey))
                throw new ArgumentException("WorldSlotStableKeyRequired",
                    nameof(worldSlotStableKey));

            var index = (int)(StableHash(seed + "|" + setName + "|"
                + worldSlotStableKey) % (uint)월드CompositionVariantCodes.All.Count);
            return 월드CompositionVariantCodes.All[index];
        }

        public bool CanPlace(
            자연경관CompositionCatalogEntry entry,
            string landCoverCode,
            float slopeDegrees,
            bool hasWaterMask,
            string seasonCode,
            string moodCode,
            float viewDistance)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return entry.Validate()
                && entry.AllowedLandCoverCodes.Contains(
                    landCoverCode, StringComparer.Ordinal)
                && slopeDegrees >= entry.MinimumSlopeDegrees
                && slopeDegrees <= entry.MaximumSlopeDegrees
                && (!entry.RequiresWaterMask || hasWaterMask)
                && entry.AllowedSeasonCodes.Contains(
                    seasonCode, StringComparer.Ordinal)
                && entry.AllowedMoodCodes.Contains(
                    moodCode, StringComparer.Ordinal)
                && viewDistance >= entry.MinimumViewDistance
                && viewDistance <= entry.MaximumViewDistance;
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
