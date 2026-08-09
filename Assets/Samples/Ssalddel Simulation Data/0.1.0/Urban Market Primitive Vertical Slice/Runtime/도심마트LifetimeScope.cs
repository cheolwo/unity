using System;
using Ssalddel.Unity.Application;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트LifetimeScope : LifetimeScope
    {
        [SerializeField]
        private bool useOperationalApi;

        [SerializeField]
        private string operationalApiBaseUrl = "https://localhost:5001/";

        [SerializeField]
        private int operationalTimeoutSeconds = 15;

        [SerializeField]
        private bool useManagerExperience;

        protected override void Configure(IContainerBuilder builder)
        {
            if (useManagerExperience)
            {
                ConfigureManager(builder);
                return;
            }

            if (useOperationalApi)
            {
                builder.RegisterInstance(new UrbanMarketApiOptions
                {
                    BaseUrl = operationalApiBaseUrl,
                    TimeoutSeconds = Math.Max(1, operationalTimeoutSeconds),
                });
                builder.Register<OperationalUrbanMarketApiClient>(Lifetime.Scoped)
                    .As<I도심마트ApiClient>();
                builder.Register<도심마트ApiMapper>(Lifetime.Scoped);
                builder.Register<도심마트ApiRepository>(Lifetime.Scoped)
                    .As<I도심마트Repository>();
                builder.Register<Operational도심마트조회UseCase>(Lifetime.Scoped)
                    .As<I도심마트조회UseCase>();
            }
            else
            {
                builder.Register<Simulated도심마트조회UseCase>(Lifetime.Scoped)
                    .As<I도심마트조회UseCase>();
            }

            builder.Register<도심마트ScreenModelValidator>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<도심마트View>();
            builder.RegisterComponentInHierarchy<도심마트SceneController>();
        }

        public void ConfigureOperationalApi(string baseUrl, int timeoutSeconds = 15)
        {
            useOperationalApi = true;
            operationalApiBaseUrl = baseUrl?.Trim() ?? string.Empty;
            operationalTimeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public void ConfigureSimulationApi()
        {
            useOperationalApi = false;
        }

        public void ConfigureManagerSimulation()
        {
            useManagerExperience = true;
            useOperationalApi = false;
        }

        private static void ConfigureManager(IContainerBuilder builder)
        {
            builder.RegisterInstance(new 도심마트ManagerRuntimeConfiguration(CreateManagerDataContext()));
            builder.Register<Simulated도심마트운영DataQuery>(Lifetime.Scoped)
                .As<I도심마트운영DataQuery>();
            builder.Register<도심마트운영SharedWorldInterpreter>(Lifetime.Scoped);
            builder.Register<도심마트진열보충Interpreter>(Lifetime.Scoped);
            builder.RegisterInstance(도심마트ReplenishmentRuleSet.SimulationDefault());
            builder.Register<도심마트운영업무SharedWorldInterpreter>(Lifetime.Scoped);
            builder.Register<마트관리자PerspectiveInterpreter>(Lifetime.Scoped);
            builder.Register<도심마트ManagerVisualPolicy>(Lifetime.Scoped);
            builder.Register<도심마트PresentationProjector>(Lifetime.Scoped);
            builder.Register<도심마트PresentationChangeSetCalculator>(Lifetime.Scoped);
            builder.RegisterInstance(new 도심마트ManagerPresentationContext());
            builder.Register<SelectionStateStore>(Lifetime.Scoped);
            builder.Register<도심마트ManagerRuntime>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<도심마트ManagerSurfaceView>();
            builder.RegisterComponentInHierarchy<도심마트ManagerSceneController>();
        }

        private static WorldDataQueryContext CreateManagerDataContext()
            => WorldDataQueryContext.ForAuthorizedUserWorld(
                도심마트DataSetKeys.ManagerOperations,
                new WorldDataContext(
                    new UserSessionContext(
                        new SessionScopeId("session:urban-market-manager-simulation"),
                        "identity:urban-market-manager-simulation"),
                    new WorldContext(
                        new WorldContextId("world:urban-market-demo"),
                        "world-revision:simulation:1",
                        DataRuntimeMode.Simulation),
                    new DataAuthorizationContext(
                        new AuthorizationScopeId("authorization:urban-market-manager-simulation"),
                        new[] { 마트관리자PerspectiveCodes.Role },
                        new[] { 도심마트CapabilityCodes.CreateShelfReplenishment },
                        "authorization-revision:simulation:1")));
    }

    public sealed class 도심마트ManagerRuntimeConfiguration
    {
        public 도심마트ManagerRuntimeConfiguration(
            WorldDataQueryContext dataContext,
            int refreshIntervalSeconds = 30)
        {
            DataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            RefreshIntervalSeconds = Math.Max(1, refreshIntervalSeconds);
        }

        public WorldDataQueryContext DataContext { get; }
        public int RefreshIntervalSeconds { get; }
    }
}
