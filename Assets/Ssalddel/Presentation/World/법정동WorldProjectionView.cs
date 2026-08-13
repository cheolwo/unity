using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
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
