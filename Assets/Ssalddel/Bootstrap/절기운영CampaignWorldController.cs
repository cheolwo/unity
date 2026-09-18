using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Campaigns;
using Ssalddel.Unity.Data.Campaigns;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.Cards;
using Ssalddel.Unity.Observation;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// 기존 SimulationWorldShell 세션을 기준으로 절기 운영 Campaign을 읽고,
    /// 운영 장면 판본과 조합해 읽기 전용 View Socket에 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "절기 운영 Campaign의 인증 조회·원천 조합·World View Socket 전달을 조율한다.",
        Boundary = "읽기 전용 GET만 수행하며 Campaign Confirm·운영 Command·로컬 저장을 제공하지 않는다.")]
    public sealed class 절기운영CampaignWorldController : MonoBehaviour
    {
        public const string DefaultAreaStableId =
            "region:kr:hjd:1126057500";

        [SerializeField] private UnityClientRuntimeSettings runtimeSettings;
        [SerializeField] private SimulationWorldShellPresenter shell;
        [SerializeField] private 절기운영CampaignWorldView worldView;
        [SerializeField] private 운영관찰WorldController operationsController;
        [SerializeField] private 카드서랍Presenter cardPresenter;
        [SerializeField] private 가상배달관찰Controller neighborhoodController;
        [SerializeField] private string areaStableId = DefaultAreaStableId;
        [SerializeField, Min(30f)] private float refreshIntervalSeconds = 30f;
        [SerializeField] private string statusCode = "Idle";

        private readonly List<SeasonalCampaignObservationSourceUpdate>
            sourceUpdates = new();
        private readonly SeasonalCampaignObservationCoordinator coordinator =
            new();
        private readonly SeasonalCampaignPresenter presenter = new();
        private EnvironmentUnityAccessTokenProvider tokenProvider;
        private Simulation절기운영CampaignClient client;
        private CancellationTokenSource lifetime;
        private Task<Simulation절기운영CampaignApplyResult> pendingRefresh;
        private float nextRefreshAt;
        private string activeSessionStableId = string.Empty;

        public string StatusCode => statusCode;
        public bool ReadOnly => true;

        public void Configure(UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            절기운영CampaignWorldView view,
            운영관찰WorldController operationalObservation,
            string areaId = DefaultAreaStableId)
        {
            runtimeSettings = settings;
            shell = worldShell;
            worldView = view;
            operationsController = operationalObservation;
            areaStableId = string.IsNullOrWhiteSpace(areaId)
                ? DefaultAreaStableId : areaId.Trim();
            if (isActiveAndEnabled) BindObservationSources(true);
        }

        private void Awake()
        {
            shell ??= FindFirstObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            worldView ??= GetComponent<절기운영CampaignWorldView>();
            operationsController ??= GetComponent<운영관찰WorldController>();
            DiscoverObservationSources();
        }

        private void OnEnable()
        {
            ResetRuntime();
            BindObservationSources(true);
        }

        private void Update()
        {
            DiscoverObservationSources();
            if (pendingRefresh != null)
            {
                if (!pendingRefresh.IsCompleted) return;
                CompleteRefresh(pendingRefresh);
                pendingRefresh = null;
                nextRefreshAt = Time.unscaledTime
                    + Math.Max(30f, refreshIntervalSeconds);
                return;
            }
            if (Time.unscaledTime < nextRefreshAt) return;
            if (!TryPrepareClient())
            {
                nextRefreshAt = Time.unscaledTime
                    + Math.Max(30f, refreshIntervalSeconds);
                return;
            }
            var session = shell?.SessionStableId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(session)
                || session == SimulationWorldShellFixture.SessionStableId)
            {
                statusCode = "WaitingForAuthoritativeSession";
                nextRefreshAt = Time.unscaledTime
                    + Math.Max(30f, refreshIntervalSeconds);
                return;
            }
            if (!string.Equals(activeSessionStableId, session,
                    StringComparison.Ordinal))
            {
                client.Clear();
                coordinator.Clear();
                worldView?.Clear();
                sourceUpdates.Clear();
                activeSessionStableId = session;
            }
            statusCode = "Loading";
            pendingRefresh = client.RefreshAsync(session, areaStableId,
                lifetime.Token);
        }

        private bool TryPrepareClient()
        {
            if (client != null) return true;
            if (runtimeSettings == null || shell == null || worldView == null)
            {
                statusCode = "WiringUnavailable";
                return false;
            }
            tokenProvider = new EnvironmentUnityAccessTokenProvider();
            if (!tokenProvider.IsAvailable)
            {
                statusCode = "AuthenticationUnavailable";
                return false;
            }
            var api = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions(), tokenProvider);
            client = new Simulation절기운영CampaignClient(
                new 절기운영CampaignApiAdapter(api),
                new UnityJsonSimulation절기운영CampaignDecoder(),
                new Simulation절기운영CampaignInterpreter());
            return true;
        }

        private void CompleteRefresh(
            Task<Simulation절기운영CampaignApplyResult> refresh)
        {
            if (refresh.IsCanceled)
            {
                statusCode = "Cancelled";
                return;
            }
            var campaign = refresh.IsFaulted
                ? new Simulation절기운영CampaignApplyResult
                {
                    ErrorCode = "SeasonalCampaignRefreshFailed",
                }
                : refresh.Result;
            var result = coordinator.Compose(activeSessionStableId,
                areaStableId, campaign, sourceUpdates, DateTime.UtcNow);
            presenter.Present(result, worldView);
            statusCode = campaign.Accepted
                ? result.ScreenModel.ActionsDisabled
                    ? "SuccessWithRequiredSourceDelay" : "Success"
                : campaign.RetainedLastSuccessful
                    ? "RefreshDelayed" : "InitialLoadError";
        }

        private void OnOperationsApplied(
            OperationalWorldSceneApplyResult result)
        {
            var update = SeasonalCampaignObservationSourceAdapters
                .FromOperations(result, DateTime.UtcNow);
            UpsertSource(update);
        }

        private void OnCardsApplied(CardWorkspaceSnapshot snapshot)
            => UpsertSource(SeasonalCampaignObservationSourceAdapters
                .FromCards(snapshot, DateTime.UtcNow));

        private void OnNpcObservationApplied(동네관찰ScreenModel screenModel)
            => UpsertSource(SeasonalCampaignObservationSourceAdapters
                .FromNpc(screenModel, DateTime.UtcNow));

        private void UpsertSource(SeasonalCampaignObservationSourceUpdate update)
        {
            sourceUpdates.RemoveAll(value => string.Equals(value.SourceCode,
                update.SourceCode, StringComparison.Ordinal));
            sourceUpdates.Add(update);
        }

        private void DiscoverObservationSources()
        {
            var discoveredCards = cardPresenter == null
                ? FindFirstObjectByType<카드서랍Presenter>(
                    FindObjectsInactive.Include) : cardPresenter;
            var discoveredNeighborhood = neighborhoodController == null
                ? FindFirstObjectByType<가상배달관찰Controller>(
                    FindObjectsInactive.Include) : neighborhoodController;
            if (ReferenceEquals(discoveredCards, cardPresenter)
                && ReferenceEquals(discoveredNeighborhood,
                    neighborhoodController))
                return;

            BindObservationSources(false);
            cardPresenter = discoveredCards;
            neighborhoodController = discoveredNeighborhood;
            BindObservationSources(true);
        }

        private void BindObservationSources(bool bind)
        {
            BindOperations(bind);
            if (cardPresenter != null)
            {
                cardPresenter.SnapshotApplied -= OnCardsApplied;
                if (bind)
                {
                    cardPresenter.SnapshotApplied += OnCardsApplied;
                    if (cardPresenter.CurrentSnapshot.PresentationOnly)
                        OnCardsApplied(cardPresenter.CurrentSnapshot);
                }
            }
            if (neighborhoodController != null)
            {
                neighborhoodController.ObservationApplied -=
                    OnNpcObservationApplied;
                if (bind)
                {
                    neighborhoodController.ObservationApplied +=
                        OnNpcObservationApplied;
                    if (neighborhoodController.CurrentObservationModel != null)
                        OnNpcObservationApplied(
                            neighborhoodController.CurrentObservationModel);
                }
            }
        }

        private void BindOperations(bool bind)
        {
            if (operationsController == null) return;
            operationsController.SnapshotApplied -= OnOperationsApplied;
            if (bind)
                operationsController.SnapshotApplied += OnOperationsApplied;
        }

        private void ResetRuntime()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            pendingRefresh = null;
            nextRefreshAt = Time.unscaledTime;
            activeSessionStableId = string.Empty;
            sourceUpdates.Clear();
            client?.Clear();
            coordinator.Clear();
            worldView?.Clear();
            statusCode = "WaitingForConnection";
        }

        private void OnDisable()
        {
            BindObservationSources(false);
            lifetime?.Cancel();
            client?.Clear();
            coordinator.Clear();
            worldView?.Clear();
            sourceUpdates.Clear();
            pendingRefresh = null;
            activeSessionStableId = string.Empty;
        }

        private void OnDestroy()
        {
            BindObservationSources(false);
            lifetime?.Dispose();
        }
    }
}
