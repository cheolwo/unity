using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// H5 상대 배치 안에 H3 경관, H2 블록, H1 표현을 조립한 Scene 증거입니다.
    /// 서버 상태나 AreaSet 이동 권위를 만들지 않는 표현 전용 상태 사본입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class H계층배치증거Root : MonoBehaviour
    {
        [SerializeField] private string evidenceStableId = string.Empty;
        [SerializeField] private string worldLayoutStableId = string.Empty;
        [SerializeField] private int worldLayoutRevision;
        [SerializeField] private string worldLayoutHashSha256 = string.Empty;
        [SerializeField] private string compositionRevision = string.Empty;
        [SerializeField] private string compositionHashSha256 = string.Empty;
        [SerializeField] private string placementPolicyCode = string.Empty;
        [SerializeField] private float scenarioMetersToUnityUnits;
        [SerializeField] private AreaSet구성패턴검토Root[] areaSetRoots =
            Array.Empty<AreaSet구성패턴검토Root>();
        [SerializeField] private Transform corridorRoot = null!;
        [SerializeField] private H공간배치규칙증거Root placementRuleEvidence = null!;
        [SerializeField] private int h3PlacementCount;
        [SerializeField] private int h2PlacementCount;
        [SerializeField] private int h1ExpressionCount;
        [SerializeField] private bool showHierarchyOverlayOnPlay;
        [SerializeField] private bool presentationOnly = true;

        public string EvidenceStableId => evidenceStableId;
        public string WorldLayoutStableId => worldLayoutStableId;
        public int WorldLayoutRevision => worldLayoutRevision;
        public string WorldLayoutHashSha256 => worldLayoutHashSha256;
        public string CompositionRevision => compositionRevision;
        public string CompositionHashSha256 => compositionHashSha256;
        public string PlacementPolicyCode => placementPolicyCode;
        public float ScenarioMetersToUnityUnits => scenarioMetersToUnityUnits;
        public AreaSet구성패턴검토Root[] AreaSetRoots => areaSetRoots;
        public Transform CorridorRoot => corridorRoot;
        public H공간배치규칙증거Root PlacementRuleEvidence => placementRuleEvidence;
        public int H3PlacementCount => h3PlacementCount;
        public int H2PlacementCount => h2PlacementCount;
        public int H1ExpressionCount => h1ExpressionCount;
        public bool ShowHierarchyOverlayOnPlay => showHierarchyOverlayOnPlay;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string stableId,
            string layoutStableId,
            int layoutRevision,
            string layoutHash,
            string sourceCompositionRevision,
            string sourceCompositionHash,
            string placementPolicy,
            float scale,
            AreaSet구성패턴검토Root[] roots,
            Transform corridors,
            H공간배치규칙증거Root rules)
        {
            evidenceStableId = stableId ?? string.Empty;
            worldLayoutStableId = layoutStableId ?? string.Empty;
            worldLayoutRevision = layoutRevision;
            worldLayoutHashSha256 = layoutHash ?? string.Empty;
            compositionRevision = sourceCompositionRevision ?? string.Empty;
            compositionHashSha256 = sourceCompositionHash ?? string.Empty;
            placementPolicyCode = placementPolicy ?? string.Empty;
            scenarioMetersToUnityUnits = scale;
            areaSetRoots = roots ?? Array.Empty<AreaSet구성패턴검토Root>();
            corridorRoot = corridors;
            placementRuleEvidence = rules;
            h3PlacementCount = CountNamed(areaSetRoots, value => value.H3Boundaries, "H3_");
            h2PlacementCount = CountNamed(areaSetRoots, value => value.H2Boundaries, "H2_");
            h1ExpressionCount = areaSetRoots.Sum(value =>
                value == null || value.SyntyPresentation == null
                    ? 0 : value.SyntyPresentation.childCount);
            showHierarchyOverlayOnPlay = false;
            presentationOnly = true;
            Validate();
            ApplyHierarchyOverlay();
        }

        public void SetHierarchyOverlay(bool visible)
        {
            showHierarchyOverlayOnPlay = visible;
            ApplyHierarchyOverlay();
        }

        private void Awake() => ApplyHierarchyOverlay();

        private void ApplyHierarchyOverlay()
        {
            // 전이회랑은 지지면이 아니라 H 관계 진단 표시다. 충돌/활성/계보는 보존한다.
            if (corridorRoot != null)
                foreach (var renderer in corridorRoot.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = showHierarchyOverlayOnPlay;
            foreach (var root in areaSetRoots)
                if (root != null)
                {
                    root.ApplyVisibility(showHierarchyOverlayOnPlay,
                        showHierarchyOverlayOnPlay,
                        showHierarchyOverlayOnPlay,
                        showHierarchyOverlayOnPlay,
                        true);
                    DisableDuplicatedGlobalBackdrops(root);
                }
        }

        private static void DisableDuplicatedGlobalBackdrops(
            AreaSet구성패턴검토Root root)
        {
            if (root.SyntyPresentation == null) return;
            foreach (var renderer in root.SyntyPresentation
                         .GetComponentsInChildren<Renderer>(true))
                if (renderer.name.Contains("Skybox",
                        StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;
        }

        public void Validate()
        {
            if (evidenceStableId !=
                    "scene-evidence:simulation-world-shell:h1-h3-baselines.v1"
                || worldLayoutStableId !=
                    "world-layout:sim:pyeongchang:nature-farm-hub-town.v1"
                || worldLayoutRevision <= 0
                || !IsHash(worldLayoutHashSha256)
                || string.IsNullOrWhiteSpace(compositionRevision)
                || !IsHash(compositionHashSha256)
                || placementPolicyCode != "FarmCenteredScenarioLocalMeters"
                || scenarioMetersToUnityUnits <= 0f
                || areaSetRoots.Length != 4
                || areaSetRoots.Any(value => value == null || !value.Validate()
                    || value.PatternKindCode != "Baseline")
                || areaSetRoots.Select(value => value.AreaRoleCode)
                    .Distinct(StringComparer.Ordinal).Count() != 4
                || corridorRoot == null || corridorRoot.childCount != 3
                || placementRuleEvidence == null
                || h3PlacementCount <= 0 || h2PlacementCount <= 0
                || h1ExpressionCount <= 0 || !presentationOnly)
                throw new InvalidOperationException("HHierarchySceneEvidenceInvalid");
            placementRuleEvidence.Validate();
        }

        private static int CountNamed(
            AreaSet구성패턴검토Root[] roots,
            Func<AreaSet구성패턴검토Root, Transform> selector,
            string prefix) => roots.Sum(root => root == null ? 0 :
            selector(root).Cast<Transform>().Count(child =>
                child.name.StartsWith(prefix, StringComparison.Ordinal)));

        private static bool IsHash(string value) =>
            value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
