using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class SimulationWorldDistrictView : MonoBehaviour
    {
        [SerializeField] private string districtStableId = string.Empty;
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private bool presentationPlaceholder;

        public string DistrictStableId => districtStableId;
        public string VisualKey => visualKey;
        public bool PresentationPlaceholder => presentationPlaceholder;

        public void Configure(string stableId, string semanticVisualKey, bool isPlaceholder)
        {
            districtStableId = stableId;
            visualKey = semanticVisualKey;
            presentationPlaceholder = isPlaceholder;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(districtStableId))
                throw new InvalidOperationException("SimulationDistrictViewStableIdMissing");
            if (string.IsNullOrWhiteSpace(visualKey))
                throw new InvalidOperationException("SimulationDistrictViewVisualKeyMissing");
        }
    }
}
