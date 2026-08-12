using Ssalddel.Unity.Runtime.Configuration;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ssalddel.Unity.Presentation.Configuration
{
    [CreateAssetMenu(fileName = "UnityClientRuntimeSettings", menuName = "Ssalddel/Unity Client Runtime Settings")]
    public sealed class UnityClientRuntimeSettings : ScriptableObject
    {
        [SerializeField] private string operationalApiBaseUrl = "https://localhost:7117/";
        [FormerlySerializedAs("apiBaseUrl")]
        [SerializeField] private string simulationRehearsalApiBaseUrl = "http://localhost:5204/";
        [SerializeField] private string detailBaseUrl = "http://localhost:5238";
        [SerializeField] private string executionMode = UnityExecutionModeCodes.Simulation;
        [SerializeField] private bool allowFixtureData;

        public UnityClientRuntimeOptions ToOptions()
        {
            var options = new UnityClientRuntimeOptions
            {
                OperationalApiBaseUrl = operationalApiBaseUrl,
                SimulationRehearsalApiBaseUrl = simulationRehearsalApiBaseUrl,
                DetailBaseUrl = detailBaseUrl,
                ExecutionMode = executionMode,
                AllowFixtureData = allowFixtureData,
            };
            options.Validate();
            return options;
        }
    }
}
