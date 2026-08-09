using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class SensorView : MonoBehaviour, IFarmSensorTarget
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private Renderer sensorRenderer = null!;
        [SerializeField] private TextMesh label = null!;

        public string StableId => stableId;

        public void Configure(string id, Renderer rendererValue, TextMesh labelValue)
        {
            stableId = id;
            sensorRenderer = rendererValue;
            label = labelValue;
        }

        public void Apply(FarmSensorSnapshot sensor)
        {
            gameObject.SetActive(true);
            var observation = sensor.LatestObservation;
            if (observation == null)
            {
                sensorRenderer.material.color = Color.gray;
                label.text = sensor.SensorTypeCode + "\n관측 없음";
                return;
            }

            sensorRenderer.material.color = ConditionColor(observation.ConditionCode);
            label.text = sensor.SensorTypeCode + " " + observation.Value + " " + observation.UnitCode
                + "\n" + observation.ConditionCode + " · " + observation.FreshnessStatusCode
                + "\n규칙 " + observation.AssessmentRuleRevision
                + " · 근거 " + (observation.EvidenceCardId ?? "없음");
        }

        public void Hide() => gameObject.SetActive(false);

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId) && sensorRenderer != null && label != null;

        private static Color ConditionColor(string conditionCode)
        {
            switch (conditionCode)
            {
                case FarmSensorConditionCodes.Normal: return new Color(0.2f, 0.75f, 0.35f);
                case FarmSensorConditionCodes.Dry: return new Color(0.95f, 0.65f, 0.2f);
                case FarmSensorConditionCodes.Critical: return new Color(0.9f, 0.2f, 0.2f);
                case FarmSensorConditionCodes.Waterlogged: return new Color(0.2f, 0.55f, 0.9f);
                default: return Color.gray;
            }
        }
    }
}
