using System;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class PublicWorldMarkerView : MonoBehaviour
    {
        private Action<string> selected;

        public string StableId { get; private set; } = string.Empty;
        public PublicWorldMarker Marker { get; private set; }

        public void Initialize(PublicWorldMarker marker, Action<string> onSelected)
        {
            Marker = marker ?? throw new ArgumentNullException(nameof(marker));
            marker.Validate();
            StableId = marker.StableId;
            selected = onSelected;
            name = $"Marker_{marker.StableId}_{marker.SourceName}";
        }

        public void Select() => selected?.Invoke(StableId);

        private void OnMouseDown() => Select();
    }
}
