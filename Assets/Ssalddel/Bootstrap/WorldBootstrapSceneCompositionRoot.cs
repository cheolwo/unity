using System;
using System.Threading;
using Ssalddel.Unity.Application.WorldMap;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Infrastructure.WorldMap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed class WorldBootstrapSceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings;
        [SerializeField] private PublicWorldMapPresenter markerPresenter;
        [SerializeField] private PublicWorldMapSceneView sceneView;
        [SerializeField] private PublicWorldMapDetailPanel detailPanel;
        [SerializeField] private string datasetCode = string.Empty;

        private CancellationTokenSource lifetimeCancellation;
        private PublicWorldMapSceneController controller;

        public PublicWorldMapSceneController Controller => controller;

        public void Configure(
            UnityClientRuntimeSettings settings,
            PublicWorldMapPresenter presenter,
            PublicWorldMapSceneView stateView,
            PublicWorldMapDetailPanel observationDetailPanel,
            string dataset = "")
        {
            runtimeSettings = settings;
            markerPresenter = presenter;
            sceneView = stateView;
            detailPanel = observationDetailPanel;
            datasetCode = dataset ?? string.Empty;
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (OperationCanceledException) when (lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested)
            {
                // Scene 종료에 따른 정상 취소입니다.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public async System.Threading.Tasks.Task InitializeAsync()
        {
            if (controller != null)
            {
                await controller.InitializeAsync(datasetCode, lifetimeCancellation.Token);
                return;
            }

            if (runtimeSettings == null) throw new InvalidOperationException("Unity runtime settings가 연결되지 않았습니다.");
            if (markerPresenter == null) throw new InvalidOperationException("공개 세계지도 marker presenter가 연결되지 않았습니다.");
            if (sceneView == null) throw new InvalidOperationException("공개 세계지도 상태 View가 연결되지 않았습니다.");
            if (detailPanel == null) throw new InvalidOperationException("공개 세계지도 상세 panel이 연결되지 않았습니다.");

            lifetimeCancellation = new CancellationTokenSource();
            var options = runtimeSettings.ToOptions();
            var apiClient = new UnityWebRequestApiClient(options);
            var repository = new CommunityWorldMapRepository(apiClient);
            var useCase = new LoadPublicWorldMapUseCase(repository);
            var navigator = new ObservationDetailNavigator(options.DetailBaseUrl);
            controller = new PublicWorldMapSceneController(
                useCase,
                markerPresenter.Apply,
                markerPresenter.Clear,
                HandleStateChanged,
                detailPanel.Show);
            markerPresenter.SetMarkerSelectedHandler(id => controller.SelectMarker(id));
            detailPanel.Bind(marker => navigator.Navigate(marker));
            sceneView.Bind(RetryFromView, RefreshFromView);
            await controller.InitializeAsync(datasetCode, lifetimeCancellation.Token);
        }

        private void HandleStateChanged(PublicWorldMapSceneState state)
        {
            sceneView.Apply(state);
            if (state.Status == PublicWorldMapSceneStatus.InitialLoadError
                || state.Status == PublicWorldMapSceneStatus.RefreshError)
                Debug.LogWarning($"공개 세계지도 상태: {state.Status}. {state.ErrorMessage}", this);
        }

        private async void RetryFromView() => await RefreshSafelyAsync();

        private async void RefreshFromView() => await RefreshSafelyAsync();

        private async System.Threading.Tasks.Task RefreshSafelyAsync()
        {
            if (controller == null || lifetimeCancellation == null) return;
            try
            {
                await controller.RefreshAsync(datasetCode, lifetimeCancellation.Token);
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }
    }
}
