using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class WorldManager
    {
        private readonly IReadOnlyList<IWorldStateProvider> _providers;

        public WorldManager(IReadOnlyList<IWorldStateProvider> providers)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            if (_providers.Count == 0)
            {
                throw new ArgumentException("World provider가 하나 이상 필요합니다.", nameof(providers));
            }
        }

        public WorldSnapshot Current { get; private set; }

        public async Task<WorldSnapshot> LoadAsync(
            WorldLoadContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            var fragments = new List<WorldStateFragment>(_providers.Count);
            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fragment = await provider.LoadAsync(context, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("World provider가 빈 결과를 반환했습니다.");
                fragment.Validate();
                fragments.Add(fragment);
            }

            var mismatched = fragments.FirstOrDefault(fragment =>
                !string.Equals(fragment.WorldId, context.WorldId, StringComparison.Ordinal)
                || fragment.WorldRevision != context.ExpectedRevision);
            if (mismatched != null)
            {
                throw new InvalidOperationException("World provider의 ID 또는 revision이 일치하지 않습니다.");
            }

            Current = new WorldSnapshot
            {
                WorldId = context.WorldId,
                Revision = context.ExpectedRevision,
                ExecutionMode = context.ExecutionMode,
                SeasonCode = fragments.Select(x => x.SeasonCode).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                WorldTime = fragments.Select(x => x.WorldTime).FirstOrDefault(x => x.HasValue),
                GeneratedAt = DateTimeOffset.UtcNow,
                Fragments = fragments.ToArray(),
            };
            return Current;
        }
    }
}
