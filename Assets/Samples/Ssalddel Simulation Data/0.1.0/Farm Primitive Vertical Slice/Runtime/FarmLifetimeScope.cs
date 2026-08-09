using System;
using Ssalddel.Unity.Farm;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmLifetimeScope : LifetimeScope
    {
        [SerializeField] private bool useOperationalApi;
        [SerializeField] private string operationalApiBaseUrl = "https://localhost:5001/";
        [SerializeField] private int operationalTimeoutSeconds = 15;
        [SerializeField] private FarmSessionTokenProvider sessionTokenProvider = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            if (useOperationalApi)
            {
                if (sessionTokenProvider == null)
                {
                    throw new InvalidOperationException("FarmSessionProviderMissing");
                }

                builder.RegisterInstance(new FarmApiOptions
                {
                    BaseUrl = operationalApiBaseUrl,
                    TimeoutSeconds = Math.Max(1, operationalTimeoutSeconds),
                });
                builder.RegisterComponent(sessionTokenProvider);
                builder.Register<OperationalFarmProducerApiClient>(Lifetime.Scoped)
                    .As<IFarmProducerPerspectiveApiClient>();
            }
            else
            {
                builder.Register<SimulatedFarmProducerApiClient>(Lifetime.Scoped)
                    .As<IFarmProducerPerspectiveApiClient>();
            }

            builder.Register<FarmProducerPerspectiveMapper>(Lifetime.Scoped);
            builder.Register<FarmProducerPerspectiveApiRepository>(Lifetime.Scoped)
                .As<IFarmProducerPerspectiveRepository>();
            builder.Register<FarmProducerPerspectiveQueryUseCase>(Lifetime.Scoped);
            builder.Register<FarmProducerPerspectiveApplicator>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<FarmView>();
            builder.RegisterComponentInHierarchy<FarmSceneController>();
        }

        public void ConfigureSimulationApi(FarmSessionTokenProvider tokenProvider)
        {
            useOperationalApi = false;
            sessionTokenProvider = tokenProvider;
        }
    }
}
