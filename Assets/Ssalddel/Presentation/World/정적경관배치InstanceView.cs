using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 정적경관배치InstanceView : MonoBehaviour
    {
        [SerializeField] private string placementStableId = string.Empty;
        [SerializeField] private string targetContainerStableId = string.Empty;
        [SerializeField] private string assetReferenceKindCode = string.Empty;
        [SerializeField] private string assetKey = string.Empty;
        [SerializeField] private string planHashSha256 = string.Empty;
        [SerializeField] private bool presentationOnly = true;

        public string PlacementStableId => placementStableId;
        public string TargetContainerStableId => targetContainerStableId;
        public string AssetReferenceKindCode => assetReferenceKindCode;
        public string AssetKey => assetKey;
        public string PlanHashSha256 => planHashSha256;
        public bool PresentationOnly => presentationOnly;

        public void Configure(정적경관배치ItemData placement, string planHash)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            placementStableId = placement.PlacementStableId;
            targetContainerStableId = placement.TargetContainerStableId;
            assetReferenceKindCode = placement.AssetReferenceKindCode;
            assetKey = placement.AssetKey;
            planHashSha256 = planHash ?? string.Empty;
            presentationOnly = true;
        }

        public bool ValidateWiring() =>
            !string.IsNullOrWhiteSpace(placementStableId)
            && !string.IsNullOrWhiteSpace(targetContainerStableId)
            && (assetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.VisualKey
                || assetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.CompositionKey)
            && !string.IsNullOrWhiteSpace(assetKey)
            && planHashSha256.Length == 64
            && presentationOnly;
    }
}
