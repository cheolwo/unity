using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.Runtime.Configuration;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class WorldLoadContext
    {
        public string WorldId { get; set; } = string.Empty;
        public long ExpectedRevision { get; set; }
        public string ExecutionMode { get; set; } = UnityExecutionModeCodes.Simulation;

        public void Validate()
        {
            StableDataId.EnsureValid(WorldId, nameof(WorldId));
            if (ExpectedRevision < 0)
            {
                throw new InvalidOperationException("예상 World revision은 음수일 수 없습니다.");
            }

            if (!UnityExecutionModeCodes.IsSupported(ExecutionMode))
            {
                throw new InvalidOperationException("지원하지 않는 World 실행 모드입니다.");
            }
        }
    }

    public sealed class WorldStateFragment
    {
        public string WorldId { get; set; } = string.Empty;
        public long WorldRevision { get; set; }
        public string ProviderKey { get; set; } = string.Empty;
        public string SourceCode { get; set; } = UnityDataSourceCodes.Live;
        public DateTimeOffset ObservedAt { get; set; }
        public string SeasonCode { get; set; } = string.Empty;
        public DateTimeOffset? WorldTime { get; set; }
        public string[] EvidenceIds { get; set; } = Array.Empty<string>();

        public void Validate()
        {
            StableDataId.EnsureValid(WorldId, nameof(WorldId));
            StableDataId.EnsureValid(ProviderKey, nameof(ProviderKey));
            if (WorldRevision < 0)
            {
                throw new InvalidOperationException("World revision은 음수일 수 없습니다.");
            }

            if (!UnityDataSourceCodes.IsSupported(SourceCode))
            {
                throw new InvalidOperationException("지원하지 않는 World source입니다.");
            }
        }
    }

    public sealed class WorldSnapshot
    {
        public string WorldId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string ExecutionMode { get; set; } = UnityExecutionModeCodes.Simulation;
        public string SeasonCode { get; set; } = string.Empty;
        public DateTimeOffset? WorldTime { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public WorldStateFragment[] Fragments { get; set; } = Array.Empty<WorldStateFragment>();
    }

    public interface IWorldStateProvider
    {
        Task<WorldStateFragment> LoadAsync(
            WorldLoadContext context,
            CancellationToken cancellationToken);
    }
}
