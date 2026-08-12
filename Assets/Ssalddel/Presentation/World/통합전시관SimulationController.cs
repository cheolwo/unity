using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Application.Exhibition;
using Ssalddel.Unity.Runtime.Exhibition;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 통합전시관SimulationLoadStatus
    {
        Idle,
        Loading,
        Refreshing,
        Success,
        InitialLoadError,
        RefreshError,
    }

    public sealed class 통합전시관SimulationLoadState
    {
        public 통합전시관SimulationLoadStatus Status { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public long AcceptedRevision { get; set; } = -1;
    }

    public sealed class 통합전시관SimulationController
    {
        private readonly 통합전시관SimulationLoadUseCase useCase;
        private readonly Action<통합전시관ServerBoundSnapshot> render;
        private readonly Action<통합전시관SimulationLoadState> stateChanged;
        private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);
        private 통합전시관ServerBoundSnapshot? current;

        public 통합전시관SimulationController(
            통합전시관SimulationLoadUseCase loadUseCase,
            Action<통합전시관ServerBoundSnapshot> renderSnapshot,
            Action<통합전시관SimulationLoadState>? onStateChanged = null)
        {
            useCase = loadUseCase ?? throw new ArgumentNullException(nameof(loadUseCase));
            render = renderSnapshot ?? throw new ArgumentNullException(nameof(renderSnapshot));
            stateChanged = onStateChanged ?? (_ => { });
        }

        public 통합전시관ServerBoundSnapshot? Current => current;
        public 통합전시관SimulationLoadState State { get; private set; } =
            new 통합전시관SimulationLoadState();

        public Task InitializeAsync(string sessionStableId, CancellationToken cancellationToken)
            => LoadAsync(sessionStableId, false, cancellationToken);

        public Task RefreshAsync(string sessionStableId, CancellationToken cancellationToken)
            => LoadAsync(sessionStableId, current != null, cancellationToken);

        private async Task LoadAsync(
            string sessionStableId, bool isRefresh, CancellationToken cancellationToken)
        {
            if (!await requestGate.WaitAsync(0, cancellationToken)) return;
            try
            {
                Publish(isRefresh
                    ? 통합전시관SimulationLoadStatus.Refreshing
                    : 통합전시관SimulationLoadStatus.Loading);
                try
                {
                    var minimumRevision = current?.Session.Revision ?? -1;
                    var loaded = await useCase.ExecuteAsync(
                        sessionStableId, minimumRevision, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    current = loaded;
                    render(loaded);
                    Publish(통합전시관SimulationLoadStatus.Success);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Publish(current == null
                        ? 통합전시관SimulationLoadStatus.InitialLoadError
                        : 통합전시관SimulationLoadStatus.RefreshError,
                        exception.Message);
                }
            }
            finally
            {
                requestGate.Release();
            }
        }

        private void Publish(
            통합전시관SimulationLoadStatus status, string errorCode = "")
        {
            State = new 통합전시관SimulationLoadState
            {
                Status = status,
                ErrorCode = errorCode ?? string.Empty,
                AcceptedRevision = current?.Session.Revision ?? -1,
            };
            stateChanged(State);
        }
    }
}
