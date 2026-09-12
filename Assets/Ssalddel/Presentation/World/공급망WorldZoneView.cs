using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 공급망WorldZoneView : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string presentationZoneCode = string.Empty;
        [SerializeField] private string canonicalWorldZoneCode = string.Empty;
        [SerializeField] private int flowOrder;
        [SerializeField] private Transform focusAnchor = null!;
        [SerializeField] private Transform visualRoot = null!;

        public string StableId => stableId;
        public string PresentationZoneCode => presentationZoneCode;
        public string CanonicalWorldZoneCode => canonicalWorldZoneCode;
        public int FlowOrder => flowOrder;
        public Transform FocusAnchor => focusAnchor;
        public Transform VisualRoot => visualRoot;

        public void Configure(
            공급망WorldZoneDefinition definition,
            Transform anchor,
            Transform visuals)
        {
            stableId = definition.StableId;
            presentationZoneCode = definition.PresentationZoneCode;
            canonicalWorldZoneCode = definition.CanonicalWorldZoneCode;
            flowOrder = definition.FlowOrder;
            focusAnchor = anchor;
            visualRoot = visuals;
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId)
                && 공급망PresentationZoneCodes.IsKnown(presentationZoneCode)
                && !string.IsNullOrWhiteSpace(canonicalWorldZoneCode)
                && flowOrder >= 0
                && focusAnchor != null
                && visualRoot != null;
    }
}
