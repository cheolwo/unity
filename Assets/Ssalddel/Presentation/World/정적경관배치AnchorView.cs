using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 정적경관배치AnchorView : MonoBehaviour
    {
        [SerializeField] private string containerStableId = string.Empty;
        [SerializeField] private Vector2 minimumLocalXZ;
        [SerializeField] private Vector2 maximumLocalXZ;
        [SerializeField] private Vector2 worldAnchorXZ;
        [SerializeField] private float localToAnchorScale = 1f;
        [SerializeField] private float localRotationY;
        [SerializeField] private float heightOffset;
        [SerializeField] private bool presentationOnly = true;

        public string ContainerStableId => containerStableId;
        public Vector2 MinimumLocalXZ => minimumLocalXZ;
        public Vector2 MaximumLocalXZ => maximumLocalXZ;
        public Vector2 WorldAnchorXZ => worldAnchorXZ;
        public float LocalToAnchorScale => localToAnchorScale;
        public float LocalRotationY => localRotationY;
        public float HeightOffset => heightOffset;
        public bool PresentationOnly => presentationOnly;

        public void Configure(정적경관배치ContainerTransformData value)
        {
            정적경관배치PlanValidator.ValidateContainerTransform(value);
            containerStableId = value.ContainerStableId;
            minimumLocalXZ = new Vector2(
                value.AuthoringMinimumX, value.AuthoringMinimumZ);
            maximumLocalXZ = new Vector2(
                value.AuthoringMaximumX, value.AuthoringMaximumZ);
            worldAnchorXZ = new Vector2(value.WorldAnchorX, value.WorldAnchorZ);
            localToAnchorScale = value.LocalToAnchorScale;
            localRotationY = value.LocalRotationY;
            heightOffset = value.HeightOffset;
            presentationOnly = true;
        }

        public bool Contains(float x, float z) =>
            x >= minimumLocalXZ.x && x <= maximumLocalXZ.x
            && z >= minimumLocalXZ.y && z <= maximumLocalXZ.y;

        public bool ValidateWiring() =>
            !string.IsNullOrWhiteSpace(containerStableId)
            && minimumLocalXZ.x < maximumLocalXZ.x
            && minimumLocalXZ.y < maximumLocalXZ.y
            && localToAnchorScale > 0f
            && presentationOnly;
    }
}
