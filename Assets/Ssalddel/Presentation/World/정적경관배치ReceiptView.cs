using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 정적경관배치ReceiptView : MonoBehaviour
    {
        [SerializeField] private string planStableId = string.Empty;
        [SerializeField] private string basePlanHashSha256 = string.Empty;
        [SerializeField] private string overrideHashSha256 = string.Empty;
        [SerializeField] private string mergedPlanHashSha256 = string.Empty;
        [SerializeField] private string visualCatalogRevision = string.Empty;
        [SerializeField] private string renderingProfileStableId = string.Empty;
        [SerializeField] private string renderingProfileRevision = string.Empty;
        [SerializeField] private string renderingProfileHashSha256 = string.Empty;
        [SerializeField] private int placementCount;
        [SerializeField] private bool presentationOnly = true;

        public string PlanStableId => planStableId;
        public string BasePlanHashSha256 => basePlanHashSha256;
        public string OverrideHashSha256 => overrideHashSha256;
        public string MergedPlanHashSha256 => mergedPlanHashSha256;
        public string VisualCatalogRevision => visualCatalogRevision;
        public string RenderingProfileStableId => renderingProfileStableId;
        public string RenderingProfileRevision => renderingProfileRevision;
        public string RenderingProfileHashSha256 => renderingProfileHashSha256;
        public int PlacementCount => placementCount;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string stableId,
            string baseHash,
            string overrideHash,
            string mergedHash,
            string catalogRevision,
            string profileStableId,
            string profileRevision,
            string profileHash,
            int count)
        {
            planStableId = stableId ?? string.Empty;
            basePlanHashSha256 = baseHash ?? string.Empty;
            overrideHashSha256 = overrideHash ?? string.Empty;
            mergedPlanHashSha256 = mergedHash ?? string.Empty;
            visualCatalogRevision = catalogRevision ?? string.Empty;
            renderingProfileStableId = profileStableId ?? string.Empty;
            renderingProfileRevision = profileRevision ?? string.Empty;
            renderingProfileHashSha256 = profileHash ?? string.Empty;
            placementCount = count;
            presentationOnly = true;
        }

        public bool ValidateWiring() =>
            !string.IsNullOrWhiteSpace(planStableId)
            && basePlanHashSha256.Length == 64
            && overrideHashSha256.Length == 64
            && mergedPlanHashSha256.Length == 64
            && !string.IsNullOrWhiteSpace(visualCatalogRevision)
            && !string.IsNullOrWhiteSpace(renderingProfileStableId)
            && !string.IsNullOrWhiteSpace(renderingProfileRevision)
            && renderingProfileHashSha256.Length == 64
            && placementCount >= 0
            && presentationOnly;
    }
}
