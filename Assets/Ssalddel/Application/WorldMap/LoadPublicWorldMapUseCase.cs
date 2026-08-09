using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Application.WorldMap
{
    public sealed class LoadPublicWorldMapUseCase
    {
        private readonly ICommunityWorldMapRepository repository;

        public LoadPublicWorldMapUseCase(ICommunityWorldMapRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<PublicWorldMapSnapshot> ExecuteAsync(string datasetCode, CancellationToken cancellationToken)
        {
            var snapshot = await repository.LoadAsync(datasetCode ?? string.Empty, cancellationToken);
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Revision))
                throw new InvalidOperationException("공개 세계지도 snapshot 또는 revision이 없습니다.");

            foreach (var marker in snapshot.Markers ?? Array.Empty<PublicWorldMarker>()) marker.Validate();
            return snapshot;
        }
    }
}
