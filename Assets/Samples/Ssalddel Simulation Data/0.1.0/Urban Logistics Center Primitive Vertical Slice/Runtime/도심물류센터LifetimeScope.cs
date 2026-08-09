using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Perspectives;
using Ssalddel.Unity.Transport;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class 도심물류센터LifetimeScope : LifetimeScope
    {
        [SerializeField]
        private bool useOperationalApi;

        [SerializeField]
        private string operationalApiBaseUrl = "https://localhost:5001/";

        [SerializeField]
        private int operationalTimeoutSeconds = 15;

        [SerializeField]
        private RuntimeSessionAccessTokenProvider sessionTokenProvider = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            if (useOperationalApi)
            {
                if (sessionTokenProvider == null)
                {
                    throw new InvalidOperationException("OperationalSessionTokenProviderMissing");
                }

                builder.RegisterInstance(new OperationalWorldApiOptions
                {
                    BaseUrl = operationalApiBaseUrl,
                    TimeoutSeconds = Math.Max(1, operationalTimeoutSeconds),
                });
                builder.RegisterComponent(sessionTokenProvider)
                    .As<IRuntimeAccessTokenProvider>();
                builder.Register<UnityWebRequestWorldGetClient>(Lifetime.Scoped);
                builder.Register<OperationalUrbanLogisticsRoleApiClient>(Lifetime.Scoped)
                    .As<IRolePerspectiveApiClient>();
                builder.Register<OperationalUrbanLogisticsNpcApiClient>(Lifetime.Scoped)
                    .As<INpcMovementApiClient>();
                builder.Register<OperationalCargoWarehouseHandoffApiClient>(Lifetime.Scoped)
                    .As<ICargoWarehouseHandoffApiClient>();
            }
            else
            {
                builder.Register<SimulatedUrbanLogisticsRoleApiClient>(Lifetime.Scoped)
                    .As<IRolePerspectiveApiClient>();
                builder.Register<SimulatedUrbanLogisticsNpcApiClient>(Lifetime.Scoped)
                    .As<INpcMovementApiClient>();
                builder.Register<SimulatedCargoWarehouseHandoffApiClient>(Lifetime.Scoped)
                    .As<ICargoWarehouseHandoffApiClient>();
            }

            builder.Register<RolePerspectiveMapper>(Lifetime.Scoped);
            builder.Register<RolePerspectiveApiRepository>(Lifetime.Scoped)
                .As<I역할관점Repository>();
            builder.Register<역할관점조회UseCase>(Lifetime.Scoped);
            builder.Register<RolePerspectiveApplicator>(Lifetime.Scoped);
            builder.Register<RoleExperienceCoordinator>(Lifetime.Scoped);
            builder.Register<AuthorizedRoleProjectionQuery>(Lifetime.Scoped);
            builder.Register<RolePresentationPresenter>(Lifetime.Scoped);
            builder.Register<RolePresentationApplicator>(Lifetime.Scoped);
            builder.Register<RolePresentationPerspectiveCoordinator>(Lifetime.Scoped);

            builder.Register<NpcMovementMapper>(Lifetime.Scoped);
            builder.Register<NpcMovementApiRepository>(Lifetime.Scoped)
                .As<INpcMovementRepository>();
            builder.Register<NpcMovementQueryUseCase>(Lifetime.Scoped);
            builder.Register<NpcMovementInterpreter>(Lifetime.Scoped);
            builder.Register<NpcMovementPresenter>(Lifetime.Scoped);
            builder.Register<CargoWarehouseHandoffMapper>(Lifetime.Scoped);
            builder.Register<CargoWarehouseHandoffApiRepository>(Lifetime.Scoped)
                .As<ICargoWarehouseHandoffRepository>();
            builder.Register<CargoWarehouseHandoffQueryUseCase>(Lifetime.Scoped);
            builder.Register<CargoWarehouseHandoffApplicator>(Lifetime.Scoped);
            builder.Register<TransportCorridorProjector>(Lifetime.Scoped);
            builder.Register<TransportCorridorQueryUseCase>(Lifetime.Scoped);
            builder.Register<TransportCorridorPresenter>(Lifetime.Scoped);
            builder.Register<TruckMovementApplicator>(Lifetime.Scoped);
            builder.Register<LogisticsFacilityOverviewProjector>(Lifetime.Scoped);
            builder.Register<UrbanLogisticsCenterPresentationQueryUseCase>(Lifetime.Scoped);

            builder.RegisterComponentInHierarchy<도심물류센터View>();
            builder.RegisterComponentInHierarchy<도심물류센터SceneController>();
        }

        public void ConfigureOperationalApi(
            string baseUrl,
            RuntimeSessionAccessTokenProvider tokenProvider,
            int timeoutSeconds = 15)
        {
            useOperationalApi = true;
            operationalApiBaseUrl = baseUrl?.Trim() ?? string.Empty;
            operationalTimeoutSeconds = Math.Max(1, timeoutSeconds);
            sessionTokenProvider = tokenProvider;
        }

        public void ConfigureSimulationApi(RuntimeSessionAccessTokenProvider tokenProvider)
        {
            useOperationalApi = false;
            sessionTokenProvider = tokenProvider;
        }
    }
}
