using Ssalddel.Unity.Runtime.Configuration;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.Configuration
{
    [CreateAssetMenu(fileName = "UnityClientRuntimeSettings", menuName = "Ssalddel/Unity Client Runtime Settings")]
    public sealed class UnityClientRuntimeSettings : ScriptableObject
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5104";
        [SerializeField] private string detailBaseUrl = "http://localhost:5238";
        [SerializeField] private string executionMode = UnityExecutionModeCodes.Simulation;
        [SerializeField] private bool allowFixtureData;

        public UnityClientRuntimeOptions ToOptions()
        {
            var options = new UnityClientRuntimeOptions
            {
                ApiBaseUrl = apiBaseUrl,
                DetailBaseUrl = detailBaseUrl,
                ExecutionMode = executionMode,
                AllowFixtureData = allowFixtureData,
            };
            options.Validate();
            return options;
        }
    }
}
