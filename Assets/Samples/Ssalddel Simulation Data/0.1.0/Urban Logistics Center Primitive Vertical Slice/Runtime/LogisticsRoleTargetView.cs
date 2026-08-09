using System;
using Ssalddel.Unity.Perspectives;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class LogisticsRoleTargetView : MonoBehaviour, IRolePerspectiveTarget, IRolePresentationTarget
    {
        [SerializeField]
        private string stableId = string.Empty;

        [SerializeField]
        private GameObject roleBadgeRoot = null!;

        [SerializeField]
        private TextMesh roleLabel = null!;

        [SerializeField]
        private Renderer roleIndicator = null!;

        public string StableId => stableId;

        public void Configure(
            string targetStableId,
            GameObject badgeRoot,
            TextMesh label,
            Renderer indicator)
        {
            stableId = targetStableId?.Trim() ?? string.Empty;
            roleBadgeRoot = badgeRoot;
            roleLabel = label;
            roleIndicator = indicator;
        }

        public void ClearRolePerspective()
        {
            roleBadgeRoot.SetActive(false);
        }

        public void ClearRolePresentation()
        {
            ClearRolePerspective();
        }

        public void ApplyRolePerspective(역할Object관점 perspective)
        {
            if (!string.Equals(perspective.TargetStableId, stableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("다른 World Object의 Role Perspective입니다.");
            }

            roleLabel.text = perspective.Label;
            roleIndicator.material.color = ResolveColor(perspective.EmphasisCode);
            roleBadgeRoot.SetActive(true);
        }

        public void ApplyRolePresentation(RoleObjectPresentationModel model)
        {
            if (!string.Equals(model.TargetStableId, stableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("다른 World Object의 Role Presentation입니다.");
            }

            roleLabel.text = model.LabelText;
            roleIndicator.material.color = ResolveColor(model.EmphasisCode);
            roleBadgeRoot.SetActive(true);
        }

        public bool ValidateWiring()
        {
            return !string.IsNullOrWhiteSpace(stableId)
                && roleBadgeRoot != null
                && roleLabel != null
                && roleIndicator != null;
        }

        private static Color ResolveColor(string emphasisCode)
        {
            switch (emphasisCode)
            {
                case RoleObjectEmphasisCodes.Primary:
                    return new Color(0.12f, 0.72f, 0.95f);
                case RoleObjectEmphasisCodes.Destination:
                    return new Color(1f, 0.72f, 0.12f);
                case RoleObjectEmphasisCodes.Muted:
                    return Color.gray;
                default:
                    return new Color(0.38f, 0.82f, 0.44f);
            }
        }
    }
}
