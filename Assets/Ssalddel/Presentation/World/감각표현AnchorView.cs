using System;
using System.Linq;
using Ssalddel.Unity.PresentationContracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 감각표현AnchorView : MonoBehaviour
    {
        [SerializeField] private string anchorStableId = string.Empty;
        [SerializeField] private string placementStableId = string.Empty;
        [SerializeField] private string owningH1StableId = string.Empty;
        [SerializeField] private string roleCode = string.Empty;
        [SerializeField] private Vector3 requestedLocalPosition;
        [SerializeField] private Vector3 appliedLocalPosition;
        [SerializeField] private bool accepted;
        [SerializeField] private string rejectionReasonCode = string.Empty;
        [SerializeField] private bool presentationOnly = true;

        public string AnchorStableId => anchorStableId;
        public string PlacementStableId => placementStableId;
        public string OwningH1StableId => owningH1StableId;
        public string RoleCode => roleCode;
        public Vector3 RequestedLocalPosition => requestedLocalPosition;
        public Vector3 AppliedLocalPosition => appliedLocalPosition;
        public bool Accepted => accepted;
        public string RejectionReasonCode => rejectionReasonCode;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string stableId, string placementId, string h1Id,
            string anchorRoleCode, Vector3 requestedPosition)
        {
            anchorStableId = stableId ?? string.Empty;
            placementStableId = placementId ?? string.Empty;
            owningH1StableId = h1Id ?? string.Empty;
            roleCode = anchorRoleCode ?? string.Empty;
            requestedLocalPosition = requestedPosition;
            appliedLocalPosition = Quantize(requestedPosition, .01f);
            transform.localPosition = appliedLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            presentationOnly = true;
            rejectionReasonCode = ValidateReason();
            accepted = string.IsNullOrEmpty(rejectionReasonCode);
        }

        public void Validate()
        {
            var reason = ValidateReason();
            if (!string.IsNullOrEmpty(reason) || !accepted)
                throw new InvalidOperationException(
                    "SensoryPresentationAnchorInvalid:" + (reason.Length == 0
                        ? rejectionReasonCode : reason));
        }

        private string ValidateReason()
        {
            if (string.IsNullOrWhiteSpace(anchorStableId)) return "StableId";
            if (string.IsNullOrWhiteSpace(placementStableId)) return "Placement";
            if (string.IsNullOrWhiteSpace(owningH1StableId)) return "H1";
            if (!배치표현AnchorRoleCodes.All.Contains(roleCode,
                    StringComparer.Ordinal)) return "Role";
            if (!ApproximatelyUniform(transform.localScale)) return "Scale";
            if ((transform.localPosition - appliedLocalPosition).sqrMagnitude
                > .0001f) return "Transform";
            if (!presentationOnly) return "Authority";
            return string.Empty;
        }

        private static Vector3 Quantize(Vector3 value, float step)
            => new(Mathf.Round(value.x / step) * step,
                Mathf.Round(value.y / step) * step,
                Mathf.Round(value.z / step) * step);

        private static bool ApproximatelyUniform(Vector3 value)
            => Mathf.Abs(value.x - value.y) <= .001f
               && Mathf.Abs(value.y - value.z) <= .001f;
    }

    [DisallowMultipleComponent]
    public sealed class 감각표현검증ReceiptView : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string placementBindingPlanHash = string.Empty;
        [SerializeField] private string axePlanHash = string.Empty;
        [SerializeField] private string harvestPlanHash = string.Empty;
        [SerializeField] private string qualificationStatusCode = string.Empty;
        [SerializeField] private string[] advisoryCodes = Array.Empty<string>();
        [SerializeField] private 감각표현AnchorView[] anchors =
            Array.Empty<감각표현AnchorView>();
        [SerializeField] private bool runtimeObserved;
        [SerializeField] private bool presentationOnly = true;

        public string StableId => stableId;
        public string PlacementBindingPlanHash => placementBindingPlanHash;
        public string AxePlanHash => axePlanHash;
        public string HarvestPlanHash => harvestPlanHash;
        public string QualificationStatusCode => qualificationStatusCode;
        public string[] AdvisoryCodes => advisoryCodes;
        public 감각표현AnchorView[] Anchors => anchors;
        public bool RuntimeObserved => runtimeObserved;
        public bool PresentationOnly => presentationOnly;

        public void Configure(감각표현AnchorView[] value,
            bool optionalAreaAudioAvailable)
        {
            var placementPlan = Nature플레이어감각표현Fixture
                .CreatePlacementPlan();
            var axePlan = Nature플레이어감각표현Fixture.CreateAxePlan();
            var harvestPlan = Nature플레이어감각표현Fixture
                .CreateHarvestPlan();
            var validator = new 플레이어감각표현Validator();
            validator.Validate(placementPlan);
            validator.Validate(axePlan);
            validator.Validate(harvestPlan);

            stableId = "presentation-receipt:nature-axe-harvest.v1";
            placementBindingPlanHash = placementPlan.PlanHashSha256;
            axePlanHash = axePlan.PlanHashSha256;
            harvestPlanHash = harvestPlan.PlanHashSha256;
            anchors = value ?? Array.Empty<감각표현AnchorView>();
            qualificationStatusCode = anchors.Length > 0
                && anchors.All(anchor => anchor != null && anchor.Accepted)
                ? "WiringAccepted" : "Rejected";
            advisoryCodes = optionalAreaAudioAvailable
                ? Array.Empty<string>()
                : new[] { "OptionalAreaAmbientPending", "OptionalAreaMusicPending" };
            runtimeObserved = false;
            presentationOnly = true;
            Validate();
        }

        public void MarkRuntimeObserved()
        {
            if (qualificationStatusCode != "WiringAccepted")
                throw new InvalidOperationException(
                    "SensoryPresentationWiringRequired");
            runtimeObserved = true;
        }

        public void Validate()
        {
            if (stableId != "presentation-receipt:nature-axe-harvest.v1"
                || placementBindingPlanHash.Length != 64
                || axePlanHash.Length != 64 || harvestPlanHash.Length != 64
                || qualificationStatusCode != "WiringAccepted"
                || anchors.Length == 0 || anchors.Any(value => value == null)
                || !presentationOnly)
                throw new InvalidOperationException(
                    "SensoryPresentationReceiptInvalid");
            foreach (var anchor in anchors) anchor.Validate();
        }
    }
}
