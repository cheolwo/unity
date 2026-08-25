using System;
using System.Linq;
using Ssalddel.Interior.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class Hub창고배치객체Record : MonoBehaviour
    {
        [SerializeField] private string placementStableId = string.Empty;
        [SerializeField] private string zoneStableId = string.Empty;
        [SerializeField] private string owningH1StableId = string.Empty;
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private string sourcePrefabPath = string.Empty;
        [SerializeField] private string sourceAssetFingerprintSha256 = string.Empty;
        [SerializeField] private string planHashSha256 = string.Empty;
        [SerializeField] private Vector3 requestedPosition;
        [SerializeField] private Vector3 appliedPosition;
        [SerializeField] private float appliedRotationDegrees;
        [SerializeField] private float appliedUniformScale = 1f;
        [SerializeField] private string adjustmentStableId = string.Empty;
        [SerializeField] private string[] validationCodes = Array.Empty<string>();
        [SerializeField] private Transform sourcePrefabRoot = null!;
        [SerializeField] private bool placementAccepted;
        [SerializeField] private bool presentationOnly = true;

        public string PlacementStableId => placementStableId;
        public string ZoneStableId => zoneStableId;
        public string OwningH1StableId => owningH1StableId;
        public string VisualKey => visualKey;
        public string SourcePrefabPath => sourcePrefabPath;
        public string PlanHashSha256 => planHashSha256;
        public Vector3 RequestedPosition => requestedPosition;
        public Vector3 AppliedPosition => appliedPosition;
        public float AppliedRotationDegrees => appliedRotationDegrees;
        public float AppliedUniformScale => appliedUniformScale;
        public Transform SourcePrefabRoot => sourcePrefabRoot;
        public bool PlacementAccepted => placementAccepted;
        public bool PresentationOnly => presentationOnly;

        public void Configure(InteriorPlacement placement, string sourcePath,
            string interiorPlanHash, Transform sourceRoot)
        {
            placementStableId = placement.PlacementStableId;
            zoneStableId = placement.ZoneStableId;
            owningH1StableId = placement.OwningH1StableId;
            visualKey = placement.VisualKey;
            sourcePrefabPath = sourcePath;
            sourceAssetFingerprintSha256 =
                placement.SourceAssetFingerprintSha256;
            planHashSha256 = interiorPlanHash;
            requestedPosition = ToVector3(
                placement.RequestedTransform.LocalPosition);
            appliedPosition = ToVector3(
                placement.AppliedTransform.LocalPosition);
            appliedRotationDegrees =
                (float)placement.AppliedTransform.LocalRotationDegrees;
            appliedUniformScale =
                (float)placement.AppliedTransform.UniformScale;
            adjustmentStableId = placement.AdjustmentStableId;
            validationCodes = placement.ValidationCodes.ToArray();
            sourcePrefabRoot = sourceRoot;
            placementAccepted = validationCodes.Contains(
                InteriorLayoutCodes.PlacementAccepted,
                StringComparer.Ordinal);
            presentationOnly = true;
            Validate();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(placementStableId)
                || string.IsNullOrWhiteSpace(zoneStableId)
                || string.IsNullOrWhiteSpace(owningH1StableId)
                || string.IsNullOrWhiteSpace(visualKey)
                || !sourcePrefabPath.StartsWith("Assets/Synty/",
                    StringComparison.Ordinal)
                || sourcePrefabPath.Contains("Truck",
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(sourceAssetFingerprintSha256)
                || string.IsNullOrWhiteSpace(planHashSha256)
                || sourcePrefabRoot == null
                || sourcePrefabRoot.parent != transform
                || sourcePrefabRoot.localPosition != Vector3.zero
                || sourcePrefabRoot.localRotation != Quaternion.identity
                || sourcePrefabRoot.localScale != Vector3.one
                || Vector3.Distance(transform.localPosition, appliedPosition) > .001f
                || Mathf.Abs(Mathf.DeltaAngle(transform.localEulerAngles.y,
                    appliedRotationDegrees)) > .01f
                || Mathf.Abs(transform.localScale.x
                    - appliedUniformScale) > .001f
                || !placementAccepted || !presentationOnly)
                throw new InvalidOperationException(
                    "HubWarehousePlacementReceiptInvalid:"
                    + placementStableId);
        }

        private static Vector3 ToVector3(InteriorVector3 value) =>
            new((float)value.X, (float)value.Y, (float)value.Z);
    }
}
