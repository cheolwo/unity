using System;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// 관전 기능의 서버 연결만 조립한다. 대상 선택과 관전 시작은 UI 또는
    /// simulation interaction 계층이 명시적으로 호출한다.
    /// </summary>
    [DefaultExecutionOrder(-840)]
    [DisallowMultipleComponent]
    public sealed class 팀원관전CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private 팀원관전Coordinator coordinator = null!;

        public void Configure(
            팀원관전Coordinator observationCoordinator,
            UnityClientRuntimeSettings settings)
        {
            coordinator = observationCoordinator;
            runtimeSettings = settings;
        }

        private void Awake()
        {
            try
            {
                Initialize();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public void Initialize()
        {
            if (runtimeSettings == null || coordinator == null)
                throw new InvalidOperationException(
                    "TeamObservationCompositionWiringMissing");
            var apiClient = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions());
            coordinator.Initialize(new 팀원관전ServerRepository(apiClient));
        }
    }
}
