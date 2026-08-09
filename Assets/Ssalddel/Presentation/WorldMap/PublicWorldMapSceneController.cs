using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Application.WorldMap;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public sealed class PublicWorldMapSceneController
    {
        private readonly LoadPublicWorldMapUseCase useCase;
        private readonly Action<PublicWorldMapSnapshot> render;
        private readonly Action clear;
        private readonly Action<PublicWorldMapSceneState> stateChanged;
        private readonly Action<PublicWorldMarker> showDetail;
        private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);
        private int initializationStarted;
        private PublicWorldMapSnapshot currentSnapshot;
        private PublicWorldMapSceneState currentState = new PublicWorldMapSceneState();

        public PublicWorldMapSceneController(
            LoadPublicWorldMapUseCase useCase,
            Action<PublicWorldMapSnapshot> render,
            Action clear = null,
            Action<PublicWorldMapSceneState> stateChanged = null,
            Action<PublicWorldMarker> showDetail = null)
        {
            this.useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
            this.render = render ?? throw new ArgumentNullException(nameof(render));
            this.clear = clear ?? (() => { });
            this.stateChanged = stateChanged ?? (_ => { });
            this.showDetail = showDetail ?? (_ => { });
        }

        public bool InitializationStarted => Volatile.Read(ref initializationStarted) != 0;
        public PublicWorldMapSceneState CurrentState => currentState;
        public PublicWorldMapSnapshot CurrentSnapshot => currentSnapshot;

        public async Task InitializeAsync(string datasetCode, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref initializationStarted, 1, 0) != 0)
                return;

            await LoadAsync(datasetCode, false, cancellationToken);
        }

        public Task RefreshAsync(string datasetCode, CancellationToken cancellationToken) =>
            LoadAsync(datasetCode, currentSnapshot != null, cancellationToken);

        public bool SelectMarker(string stableId)
        {
            var marker = currentSnapshot?.Markers?.FirstOrDefault(item =>
                string.Equals(item.StableId, stableId, StringComparison.Ordinal));
            if (marker == null) return false;
            showDetail(marker);
            return true;
        }

        private async Task LoadAsync(string datasetCode, bool isRefresh, CancellationToken cancellationToken)
        {
            if (!await requestGate.WaitAsync(0, cancellationToken)) return;
            try
            {
                Publish(isRefresh ? PublicWorldMapSceneStatus.Refreshing : PublicWorldMapSceneStatus.Loading);
                try
                {
                    var snapshot = await useCase.ExecuteAsync(datasetCode ?? string.Empty, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    currentSnapshot = snapshot;
                    render(snapshot);
                    Publish(PublicWorldMapSceneStatus.Success);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    if (isRefresh && currentSnapshot != null)
                    {
                        Publish(PublicWorldMapSceneStatus.RefreshError, exception.Message);
                        return;
                    }

                    currentSnapshot = null;
                    clear();
                    Publish(PublicWorldMapSceneStatus.InitialLoadError, exception.Message);
                }
            }
            finally
            {
                requestGate.Release();
            }
        }

        private void Publish(PublicWorldMapSceneStatus status, string errorMessage = "")
        {
            currentState = new PublicWorldMapSceneState
            {
                Status = status,
                ErrorMessage = errorMessage ?? string.Empty,
                MarkerCount = currentSnapshot?.Markers?.Length ?? 0,
                Revision = currentSnapshot?.Revision ?? string.Empty,
                GeneratedAtUtc = currentSnapshot?.GeneratedAtUtc
            };
            stateChanged(currentState);
        }
    }
}
