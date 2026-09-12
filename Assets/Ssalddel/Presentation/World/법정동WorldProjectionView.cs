using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 법정동WorldProjectionView : MonoBehaviour
    {
        [SerializeField] private 법정동WorldProjectionData projection = null!;
        [SerializeField] private 법정동경관PlanData scenicPlan = null!;
        [SerializeField] private Transform boundaryLayer = null!;
        [SerializeField] private Transform contourLayer = null!;
        [SerializeField] private Transform scenicLayer = null!;

        public 법정동WorldProjectionData Projection => projection;
        public 법정동경관PlanData ScenicPlan => scenicPlan;
        public Transform BoundaryLayer => boundaryLayer;
        public Transform ContourLayer => contourLayer;
        public Transform ScenicLayer => scenicLayer;

        public void Configure(법정동WorldProjectionData value)
        {
            법정동WorldProjectionValidator.Validate(value);
            projection = value;
        }

        public void ConfigureLayers(
            법정동경관PlanData plan,
            Transform boundaries,
            Transform contours,
            Transform scenery)
        {
            법정동경관PlanValidator.Validate(plan);
            scenicPlan = plan;
            boundaryLayer = boundaries;
            contourLayer = contours;
            scenicLayer = scenery;
            SetMapEvidenceVisible(false);
        }

        public void SetMapEvidenceVisible(bool visible)
        {
            if (boundaryLayer != null) boundaryLayer.gameObject.SetActive(visible);
            if (contourLayer != null) contourLayer.gameObject.SetActive(visible);
        }
    }
}
