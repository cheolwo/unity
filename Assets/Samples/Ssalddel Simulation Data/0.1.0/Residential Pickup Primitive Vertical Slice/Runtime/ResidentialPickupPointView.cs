using Ssalddel.Unity.ResidentialPickup;
using UnityEngine;

namespace Ssalddel.Unity.Samples.ResidentialPickup
{
    public sealed class ResidentialPickupPointView
        : MonoBehaviour, IResidentialPickupPointTarget
    {
        [SerializeField]
        private string stableId = string.Empty;

        [SerializeField]
        private Renderer bodyRenderer = null!;

        [SerializeField]
        private TextMesh label = null!;

        [SerializeField]
        private GameObject roleBadge = null!;

        public string StableId => stableId;

        public void Configure(
            string targetStableId,
            Renderer renderer,
            TextMesh text,
            GameObject badge)
        {
            stableId = targetStableId;
            bodyRenderer = renderer;
            label = text;
            roleBadge = badge;
        }

        public void Apply(ResidentialPickupPointSnapshot point, string authorizedRoleCode)
        {
            gameObject.SetActive(true);
            label.text = point.PickupPointLabel
                + "\n" + point.RoleLabel
                + "\n" + point.ProductLabel + " × " + point.Quantity
                + "\n" + point.StatusCode;
            ApplyColor(bodyRenderer, StatusColor(point.StatusCode));
            ApplyColor(roleBadge.GetComponent<Renderer>(), string.Equals(
                authorizedRoleCode,
                ResidentialPickupRoleCodes.Orderer,
                System.StringComparison.Ordinal)
                ? new Color(0.25f, 0.58f, 0.92f)
                : new Color(0.95f, 0.58f, 0.18f));
            roleBadge.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public bool ValidateWiring()
        {
            return !string.IsNullOrWhiteSpace(stableId)
                && bodyRenderer != null
                && label != null
                && roleBadge != null;
        }

        private static Color StatusColor(string statusCode)
        {
            if (statusCode == ResidentialPickupStatusCodes.Completed)
            {
                return new Color(0.25f, 0.65f, 0.35f);
            }

            if (statusCode == ResidentialPickupStatusCodes.Arrived)
            {
                return new Color(0.2f, 0.55f, 0.85f);
            }

            return new Color(0.6f, 0.62f, 0.65f);
        }

        private static void ApplyColor(Renderer target, Color color)
        {
            var block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            var material = target.sharedMaterial;
            if (material != null && material.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", color);
            else
                block.SetColor("_Color", color);
            target.SetPropertyBlock(block);
        }
    }
}
