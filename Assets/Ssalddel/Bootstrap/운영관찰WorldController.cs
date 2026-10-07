using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>공식 World에서 v2 운영 상태 사본을 30초마다 읽고 표현 조정기에 전달합니다.</summary>
    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "공식 SimulationWorldShell의 운영 관찰 상태 사본 조회와 표현 갱신을 조율한다.",
        Boundary = "읽기 전용 GET만 수행하며 Unity 입력으로 운영 상태를 변경하거나 사본을 영속하지 않는다.")]
    public sealed class 운영관찰WorldController : MonoBehaviour
    {
        [SerializeField] private 운영관찰WorldView worldView;
        [SerializeField, Min(30f)] private float refreshIntervalSeconds =
            OperationalWorldScenePolicy.RefreshAfterSeconds;
        [SerializeField] private string statusCode = "Idle";
        [SerializeField] private long cursor;

        private 운영관찰검증ApiTransport transport;
        private OperationalWorldSceneClient client;
        private OperationalWorldScenePlacementPlanner planner;
        private 다중Os생명주기검증Layer lifecycleView;
        private 생활관찰표현Layer livingView;
        private CancellationTokenSource lifetime;
        private Task<OperationalWorldSceneApplyResult> pendingRefresh;
        private float nextRefreshAt;

        public string StatusCode => statusCode;
        public long Cursor => cursor;
        public int ObjectCount => worldView != null ? worldView.ObjectCount : 0;
        public event Action<OperationalWorldSceneApplyResult> SnapshotApplied;

        public void Configure(운영관찰WorldView view)
        {
            worldView = view != null ? view : throw new ArgumentNullException(nameof(view));
            refreshIntervalSeconds = OperationalWorldScenePolicy.RefreshAfterSeconds;
        }

        private void Awake()
        {
            InitializeClient();
        }

        private void InitializeClient()
        {
            if (worldView == null) worldView = GetComponent<운영관찰WorldView>();
            lifecycleView = GetComponent<다중Os생명주기검증Layer>()
                            ?? gameObject.AddComponent<다중Os생명주기검증Layer>();
            lifecycleView.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            lifecycleView.Configure(worldView, GetComponent<사가정운영디오라마View>());
            livingView = GetComponent<생활관찰표현Layer>()
                         ?? gameObject.AddComponent<생활관찰표현Layer>();
            livingView.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            livingView.Configure(worldView, lifecycleView, GetComponent<사가정운영디오라마View>(),
                관찰운영검증RunnerArea.AreaStableId);
            if (client != null) return;
            transport = new 운영관찰검증ApiTransport();
            client = new OperationalWorldSceneClient(
                transport,
                new 운영관찰검증JsonDecoder(),
                new OperationalWorldSceneInterpreter(),
                OperationalWorldScenePolicy.SchemaVersionV2);
            planner = new OperationalWorldScenePlacementPlanner();
        }

        private void OnEnable()
        {
            InitializeClient();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            client?.Clear();
            cursor = 0;
            nextRefreshAt = Time.unscaledTime;
            statusCode = "WaitingForConnection";
        }

        private void Update()
        {
            GetComponent<사가정운영디오라마View>()?.SetStatus(statusCode);
            worldView?.Expire(DateTime.UtcNow);
            client?.Expire(DateTime.UtcNow);
            if (pendingRefresh != null)
            {
                if (!pendingRefresh.IsCompleted) return;
                CompleteRefresh(pendingRefresh);
                pendingRefresh = null;
                nextRefreshAt = Time.unscaledTime + Math.Max(30f, refreshIntervalSeconds);
                return;
            }
            if (Time.unscaledTime < nextRefreshAt) return;
            if (!운영관찰검증ApiTransport.ConnectionExists())
            {
                statusCode = "ConnectionUnavailable";
                nextRefreshAt = Time.unscaledTime + Math.Max(30f, refreshIntervalSeconds);
                return;
            }
            statusCode = cursor == 0 ? "Loading" : "Refreshing";
            pendingRefresh = client.RefreshAsync(
                관찰운영검증RunnerArea.AreaStableId,
                cursor,
                DateTime.UtcNow,
                lifetime.Token);
        }

        private void CompleteRefresh(Task<OperationalWorldSceneApplyResult> refresh)
        {
            if (refresh.IsCanceled)
            {
                statusCode = "Cancelled";
                return;
            }
            if (refresh.IsFaulted)
            {
                statusCode = cursor == 0 ? "InitialLoadError" : "RefreshError";
                return;
            }
            var result = refresh.Result;
            if (!result.Accepted)
            {
                statusCode = "SnapshotRejected";
                return;
            }
            cursor = result.Cursor;
            var placement = planner.Create(result);
            var livingAccepted = livingView.Apply(placement, result.Diagnostics, DateTime.UtcNow);
            SnapshotApplied?.Invoke(result);
            statusCode = !livingAccepted ? "SuccessWithLivingObservationHeld"
                : lifecycleView.StateCount > 0 && lifecycleView.GeneralizedPlacementCount > 0
                ? "SuccessWithGeneralizedLifecycleAnchors"
                : worldView.UnplacedCount > 0 ? "SuccessWithUnplacedObjects"
                : result.Diagnostics.Length == 0 ? "Success" : "SuccessWithFrozenObjects";
        }

        private void OnDisable()
        {
            lifetime?.Cancel();
            client?.Clear();
            livingView?.Clear();
            worldView?.Clear();
            lifecycleView?.Clear();
            cursor = 0;
            pendingRefresh = null;
            transport?.Dispose();
            transport = null;
            client = null;
            planner = null;
        }

        private void OnDestroy()
        {
            lifetime?.Dispose();
            transport?.Dispose();
        }
    }

    /// <summary>서버 검증기와 같은 공개 가능한 합성 지역 식별자입니다.</summary>
    public static class 관찰운영검증RunnerArea
    {
        public const string AreaStableId = "area:kr-seoul-jungnang-myeonmok";
    }
}
