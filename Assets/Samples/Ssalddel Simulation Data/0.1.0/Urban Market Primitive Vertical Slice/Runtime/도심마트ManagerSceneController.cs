using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Application;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;
using VContainer;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트ManagerSceneController : MonoBehaviour
    {
        private 도심마트ManagerRuntime runtime = null!;
        private 도심마트ManagerRuntimeConfiguration configuration = null!;
        private 도심마트ManagerSurfaceView view = null!;
        private CancellationTokenSource lifetime = null!;
        private Task? inFlight;
        private float nextRefreshAt;

        [Inject]
        public void Construct(
            도심마트ManagerRuntime managerRuntime,
            도심마트ManagerRuntimeConfiguration runtimeConfiguration,
            도심마트ManagerSurfaceView surfaceView)
        {
            runtime = managerRuntime;
            configuration = runtimeConfiguration;
            view = surfaceView;
        }

        private void Awake() => lifetime = new CancellationTokenSource();

        private async void Start()
        {
            view.ShelfSelected += HandleShelfSelected;
            try { await RefreshAsync(); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }

        private void Update()
        {
            if (inFlight != null && !inFlight.IsCompleted) return;
            if (nextRefreshAt <= 0f || Time.unscaledTime < nextRefreshAt) return;
            _ = RefreshAsync();
        }

        public Task RefreshAsync()
        {
            if (inFlight != null && !inFlight.IsCompleted) return inFlight;
            inFlight = RefreshCoreAsync();
            return inFlight;
        }

        private async Task RefreshCoreAsync()
        {
            if (!view.ValidateWiring()) throw new InvalidOperationException("UrbanMarketManagerViewWiringInvalid");
            view.ShowLoading(runtime.CurrentStatus.IsShowingLastSuccess);
            var result = await runtime.RefreshAsync(configuration.DataContext, lifetime.Token);
            view.Apply(result);
            nextRefreshAt = Time.unscaledTime
                            + configuration.RefreshIntervalSeconds;
        }

        private void HandleShelfSelected(WorldStableId shelfWorldId)
        {
            try { view.Apply(runtime.Select(shelfWorldId)); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        private void OnDestroy()
        {
            if (view != null) view.ShelfSelected -= HandleShelfSelected;
            lifetime.Cancel();
            lifetime.Dispose();
        }
    }
}
