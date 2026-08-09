using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Transport;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    [Serializable]
    public sealed class LogisticsFacilityAreaBinding
    {
        public string AreaCode = string.Empty;
        public GameObject VisualRoot = null!;
        public Transform CargoAnchor = null!;
        public Renderer StatusRenderer = null!;
        public TextMesh StatusLabel = null!;
    }

    [Serializable]
    public sealed class LogisticsFacilityStateMaterialBinding
    {
        public string StateCode = string.Empty;
        public Material Material = null!;
    }

    /// <summary>
    /// 차량 접근부터 보관까지 하나의 handoff를 공간으로 표현합니다.
    /// 건물·차량·팔레트 외형은 VisualRoot 아래에서 교체합니다.
    /// </summary>
    public sealed class LogisticsFacilityOverviewView : MonoBehaviour
    {
        [SerializeField] private GameObject buildingVisualRoot = null!;
        [SerializeField] private GameObject cargoVisualRoot = null!;
        [SerializeField] private TextMesh summaryText = null!;
        [SerializeField] private TextMesh boundaryText = null!;
        [SerializeField] private LogisticsFacilityAreaBinding[] areas =
            Array.Empty<LogisticsFacilityAreaBinding>();
        [SerializeField] private LogisticsFacilityStateMaterialBinding[] stateMaterials =
            Array.Empty<LogisticsFacilityStateMaterialBinding>();

        public GameObject BuildingVisualRoot => buildingVisualRoot;
        public GameObject CargoVisualRoot => cargoVisualRoot;
        public int AreaCount => areas?.Length ?? 0;

        public void Configure(
            GameObject building,
            GameObject cargo,
            TextMesh summary,
            TextMesh boundary,
            LogisticsFacilityAreaBinding[] areaBindings,
            LogisticsFacilityStateMaterialBinding[] materialBindings)
        {
            buildingVisualRoot = building;
            cargoVisualRoot = cargo;
            summaryText = summary;
            boundaryText = boundary;
            areas = areaBindings ?? Array.Empty<LogisticsFacilityAreaBinding>();
            stateMaterials = materialBindings ?? Array.Empty<LogisticsFacilityStateMaterialBinding>();
        }

        public void Apply(LogisticsFacilityOverviewPresentationModel? model)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("LogisticsFacilityOverviewViewWiringInvalid");
            if (model == null)
            {
                summaryText.text = "현재 입고 handoff 없음";
                boundaryText.text = "서버 Projection에서 활성 handoff가 제공되지 않았습니다.";
                cargoVisualRoot.SetActive(false);
                foreach (var area in areas)
                    ApplyArea(area, area.AreaCode, LogisticsFacilityAreaStateCodes.Idle);
                return;
            }

            summaryText.text = model.SummaryText + "\n운송 " + model.TransportTaskStableId
                + " · 입고 " + model.InboundTaskStableId;
            boundaryText.text = model.BoundaryText;
            var incoming = model.Areas.ToDictionary(value => value.AreaCode, StringComparer.Ordinal);
            foreach (var area in areas)
            {
                if (!incoming.TryGetValue(area.AreaCode, out var presentation))
                    throw new InvalidOperationException("LogisticsFacilityAreaPresentationMissing:" + area.AreaCode);
                ApplyArea(area, presentation.LabelText, presentation.ColorToken);
            }

            var current = areas.SingleOrDefault(value => value.AreaCode == model.CurrentAreaCode)
                ?? throw new InvalidOperationException(
                    "LogisticsFacilityCurrentAreaMissing:" + model.CurrentAreaCode);
            cargoVisualRoot.transform.position = current.CargoAnchor.position;
            cargoVisualRoot.SetActive(true);
        }

        public bool ValidateWiring()
        {
            if (buildingVisualRoot == null || cargoVisualRoot == null
                || summaryText == null || boundaryText == null
                || areas == null || stateMaterials == null)
                return false;
            var expectedAreas = new HashSet<string>(new[]
            {
                LogisticsFacilityAreaCodes.VehicleGate,
                LogisticsFacilityAreaCodes.InboundDock,
                LogisticsFacilityAreaCodes.Inspection,
                LogisticsFacilityAreaCodes.Storage,
            }, StringComparer.Ordinal);
            var expectedStates = new HashSet<string>(new[]
            {
                LogisticsFacilityAreaStateCodes.Idle,
                LogisticsFacilityAreaStateCodes.Next,
                LogisticsFacilityAreaStateCodes.Active,
                LogisticsFacilityAreaStateCodes.Completed,
            }, StringComparer.Ordinal);
            return areas.Length == expectedAreas.Count
                && areas.All(value => value != null && value.VisualRoot != null
                    && value.CargoAnchor != null && value.StatusRenderer != null
                    && value.StatusLabel != null && expectedAreas.Remove(value.AreaCode))
                && expectedAreas.Count == 0
                && stateMaterials.Length == expectedStates.Count
                && stateMaterials.All(value => value != null && value.Material != null
                    && expectedStates.Remove(value.StateCode))
                && expectedStates.Count == 0;
        }

        private void ApplyArea(
            LogisticsFacilityAreaBinding area,
            string label,
            string colorToken)
        {
            var material = stateMaterials.SingleOrDefault(value => value.StateCode == colorToken)?.Material
                ?? throw new InvalidOperationException("LogisticsFacilityStateMaterialMissing:" + colorToken);
            area.StatusRenderer.sharedMaterial = material;
            area.StatusLabel.text = label + "\n" + colorToken;
            area.VisualRoot.SetActive(true);
        }
    }
}
