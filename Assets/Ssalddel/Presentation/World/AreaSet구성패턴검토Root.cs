using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 역할 슬롯 기반 AreaSet 구성 패턴의 Unity 검토 계보만 보존한다.
    /// 구성 선택과 Simulation 상태의 권위가 아니다.
    /// </summary>
    public sealed class AreaSet구성패턴검토Root : MonoBehaviour
    {
        [SerializeField] private string compositionPatternStableId = string.Empty;
        [SerializeField] private string patternCode = string.Empty;
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string areaRoleCode = string.Empty;
        [SerializeField] private string patternKindCode = string.Empty;
        [SerializeField] private string sourceRevision = string.Empty;
        [SerializeField] private string sourceHashSha256 = string.Empty;
        [SerializeField] private string patternHashSha256 = string.Empty;
        [SerializeField] private string[] roleSlotCodes = Array.Empty<string>();
        [SerializeField] private Transform h3Boundaries;
        [SerializeField] private Transform h2Boundaries;
        [SerializeField] private Transform connectors;
        [SerializeField] private Transform wiRoutes;
        [SerializeField] private Transform syntyPresentation;
        [SerializeField] private bool presentationOnly = true;

        public string CompositionPatternStableId => compositionPatternStableId;
        public string PatternCode => patternCode;
        public string Title => title;
        public string AreaRoleCode => areaRoleCode;
        public string PatternKindCode => patternKindCode;
        public string SourceRevision => sourceRevision;
        public string SourceHashSha256 => sourceHashSha256;
        public string PatternHashSha256 => patternHashSha256;
        public string[] RoleSlotCodes => roleSlotCodes;
        public Transform H3Boundaries => h3Boundaries;
        public Transform H2Boundaries => h2Boundaries;
        public Transform Connectors => connectors;
        public Transform WiRoutes => wiRoutes;
        public Transform SyntyPresentation => syntyPresentation;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string stableId,
            string code,
            string displayTitle,
            string roleCode,
            string kindCode,
            string revision,
            string sourceHash,
            string patternHash,
            string[] slots,
            Transform h3Root,
            Transform h2Root,
            Transform connectorRoot,
            Transform routeRoot,
            Transform syntyRoot)
        {
            compositionPatternStableId = stableId ?? string.Empty;
            patternCode = code ?? string.Empty;
            title = displayTitle ?? string.Empty;
            areaRoleCode = roleCode ?? string.Empty;
            patternKindCode = kindCode ?? string.Empty;
            sourceRevision = revision ?? string.Empty;
            sourceHashSha256 = sourceHash ?? string.Empty;
            patternHashSha256 = patternHash ?? string.Empty;
            roleSlotCodes = slots ?? Array.Empty<string>();
            h3Boundaries = h3Root;
            h2Boundaries = h2Root;
            connectors = connectorRoot;
            wiRoutes = routeRoot;
            syntyPresentation = syntyRoot;
            presentationOnly = true;
        }

        public void ApplyVisibility(
            bool showH3,
            bool showH2,
            bool showConnectors,
            bool showWiRoutes,
            bool showSynty)
        {
            if (h3Boundaries != null) h3Boundaries.gameObject.SetActive(showH3);
            if (h2Boundaries != null) h2Boundaries.gameObject.SetActive(showH2);
            if (connectors != null) connectors.gameObject.SetActive(showConnectors);
            if (wiRoutes != null) wiRoutes.gameObject.SetActive(showWiRoutes);
            if (syntyPresentation != null) syntyPresentation.gameObject.SetActive(showSynty);
        }

        public bool Validate()
        {
            return compositionPatternStableId.StartsWith(
                       "area-set-composition:", StringComparison.Ordinal)
                   && !string.IsNullOrWhiteSpace(patternCode)
                   && !string.IsNullOrWhiteSpace(title)
                   && areaRoleCode is "NatureHome" or "Farm" or "Town" or "CityHub"
                   && patternKindCode is "Baseline" or "Variant"
                   && !string.IsNullOrWhiteSpace(sourceRevision)
                   && IsHash(sourceHashSha256)
                   && IsHash(patternHashSha256)
                   && roleSlotCodes.Length >= 3
                   && roleSlotCodes.Distinct(StringComparer.Ordinal).Count()
                   == roleSlotCodes.Length
                   && h3Boundaries != null && h2Boundaries != null
                   && connectors != null && wiRoutes != null
                   && syntyPresentation != null
                   && presentationOnly;
        }

        private static bool IsHash(string value) =>
            value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
