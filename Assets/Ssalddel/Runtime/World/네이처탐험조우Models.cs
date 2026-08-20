using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 네이처탐험조우Codes
    {
        public const string Active = "Active";
        public const string NatureToFarm = "NatureToFarm";
        public const string NatureToTown = "NatureToTown";
        public const string NatureToCityHub = "NatureToCityHub";
    }

    [Serializable]
    public sealed class 네이처탐험조우ApiModel
    {
        public string EncounterStableId { get; set; } = string.Empty;
        public long EncounterRevision { get; set; }
        public string NatureRouteCode { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public string RiskBandCode { get; set; } = string.Empty;
        public int ThreatUnitCount { get; set; }
        public string PresentationKey { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class 네이처탐험조우StateApiModel
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long WorldRevision { get; set; }
        public 네이처탐험조우ApiModel[] Encounters { get; set; }
            = Array.Empty<네이처탐험조우ApiModel>();
        public bool SimulationOnly { get; set; } = true;
        public bool IsOperationalState { get; set; }

        public 네이처탐험조우ApiModel[] ActiveEncounters()
            => (Encounters ?? Array.Empty<네이처탐험조우ApiModel>())
                .Where(value => value != null
                    && value.StateCode == 네이처탐험조우Codes.Active
                    && !string.IsNullOrWhiteSpace(value.EncounterStableId))
                .OrderBy(value => value.EncounterStableId, StringComparer.Ordinal)
                .ToArray();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SessionStableId) || WorldRevision < 0
                || !SimulationOnly || IsOperationalState)
                throw new InvalidOperationException(
                    "NatureExplorationEncounterAuthorityBoundaryInvalid");
            if ((Encounters ?? Array.Empty<네이처탐험조우ApiModel>()).Any(value =>
                    value == null || string.IsNullOrWhiteSpace(value.EncounterStableId)
                    || value.EncounterRevision < 0 || value.ThreatUnitCount < 0))
                throw new InvalidOperationException(
                    "NatureExplorationEncounterStateInvalid");
        }
    }

    public interface I네이처탐험조우AuthorityClient
    {
        Task<네이처탐험조우StateApiModel> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken);
    }
}
