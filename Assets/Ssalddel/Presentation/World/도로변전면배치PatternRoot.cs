using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// H3가 도로 축을, H2가 전면선·후퇴선·출입구 폭을 결정하고,
    /// H1 도로·울타리·건물 모듈이 그 결정을 실제 표현으로 조립한 기록입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 도로변전면배치PatternRoot : MonoBehaviour
    {
        public const string RequiredStableId =
            "placement-pattern:roadside-frontage-homestead.v1";
        public const string RequiredRevision =
            "roadside-frontage-pattern.v1";

        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string revision = string.Empty;
        [SerializeField] private string areaRoleCode = string.Empty;
        [SerializeField] private string h3LandscapeNodeName = string.Empty;
        [SerializeField] private string h2BlockNodeName = string.Empty;
        [SerializeField] private string h3AxisPolicyCode = string.Empty;
        [SerializeField] private string h2FrontagePolicyCode = string.Empty;
        [SerializeField] private string roadCompositionKey = string.Empty;
        [SerializeField] private string buildingCompositionKey = string.Empty;
        [SerializeField] private Transform roadModule = null!;
        [SerializeField] private Transform leftFenceModule = null!;
        [SerializeField] private Transform rightFenceModule = null!;
        [SerializeField] private Transform buildingModule = null!;
        [SerializeField] private Transform roadConnector = null!;
        [SerializeField] private Transform buildingEntrance = null!;
        [SerializeField] private float entranceGapWidth;
        [SerializeField] private float roadFenceClearance;
        [SerializeField] private float buildingSetback;
        [SerializeField] private bool entrancePathClear;
        [SerializeField] private bool presentationOnly = true;

        public string StableId => stableId;
        public string Revision => revision;
        public string AreaRoleCode => areaRoleCode;
        public string H3LandscapeNodeName => h3LandscapeNodeName;
        public string H2BlockNodeName => h2BlockNodeName;
        public string H3AxisPolicyCode => h3AxisPolicyCode;
        public string H2FrontagePolicyCode => h2FrontagePolicyCode;
        public string RoadCompositionKey => roadCompositionKey;
        public string BuildingCompositionKey => buildingCompositionKey;
        public Transform RoadModule => roadModule;
        public Transform LeftFenceModule => leftFenceModule;
        public Transform RightFenceModule => rightFenceModule;
        public Transform BuildingModule => buildingModule;
        public Transform RoadConnector => roadConnector;
        public Transform BuildingEntrance => buildingEntrance;
        public float EntranceGapWidth => entranceGapWidth;
        public float RoadFenceClearance => roadFenceClearance;
        public float BuildingSetback => buildingSetback;
        public bool EntrancePathClear => entrancePathClear;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string areaRole,
            string h3Node,
            string h2Node,
            string roadKey,
            string buildingKey,
            Transform road,
            Transform leftFence,
            Transform rightFence,
            Transform building,
            Transform roadAccess,
            Transform entrance,
            float requiredGap)
        {
            stableId = RequiredStableId;
            revision = RequiredRevision;
            areaRoleCode = areaRole ?? string.Empty;
            h3LandscapeNodeName = h3Node ?? string.Empty;
            h2BlockNodeName = h2Node ?? string.Empty;
            h3AxisPolicyCode = "H3RoadAxis:X";
            h2FrontagePolicyCode =
                "H2RoadEdge-FenceLine-EntranceGap-BuildingSetback";
            roadCompositionKey = roadKey ?? string.Empty;
            buildingCompositionKey = buildingKey ?? string.Empty;
            roadModule = road;
            leftFenceModule = leftFence;
            rightFenceModule = rightFence;
            buildingModule = building;
            roadConnector = roadAccess;
            buildingEntrance = entrance;
            entranceGapWidth = HorizontalGap(
                RequiredBounds(leftFenceModule),
                RequiredBounds(rightFenceModule));
            var roadBounds = RequiredBounds(roadModule);
            var leftBounds = RequiredBounds(leftFenceModule);
            var rightBounds = RequiredBounds(rightFenceModule);
            roadFenceClearance = Mathf.Min(
                Mathf.Max(0f, leftBounds.min.z - roadBounds.max.z),
                Mathf.Max(0f, rightBounds.min.z - roadBounds.max.z));
            var buildingBounds = RequiredBounds(buildingModule);
            buildingSetback = Mathf.Max(0f, buildingBounds.min.z
                - Mathf.Max(leftBounds.max.z, rightBounds.max.z));
            entrancePathClear = entranceGapWidth >= requiredGap - .05f
                && Mathf.Abs(buildingEntrance.position.x
                    - (leftBounds.max.x + rightBounds.min.x) * .5f)
                <= requiredGap * .35f
                && buildingEntrance.position.z > roadConnector.position.z;
            presentationOnly = true;
            Validate();
        }

        public void Validate()
        {
            if (stableId != RequiredStableId || revision != RequiredRevision
                || areaRoleCode != "Farm"
                || !h3LandscapeNodeName.StartsWith("H3_",
                    StringComparison.Ordinal)
                || !h2BlockNodeName.StartsWith("H2_",
                    StringComparison.Ordinal)
                || h3AxisPolicyCode != "H3RoadAxis:X"
                || h2FrontagePolicyCode !=
                    "H2RoadEdge-FenceLine-EntranceGap-BuildingSetback"
                || string.IsNullOrWhiteSpace(roadCompositionKey)
                || string.IsNullOrWhiteSpace(buildingCompositionKey)
                || new[]
                {
                    roadModule, leftFenceModule, rightFenceModule,
                    buildingModule, roadConnector, buildingEntrance,
                }.Any(value => value == null || !value.IsChildOf(transform))
                || entranceGapWidth < 2.2f
                || roadFenceClearance < .35f
                || buildingSetback < 1.2f
                || !entrancePathClear || !presentationOnly)
                throw new InvalidOperationException(
                    "RoadsideFrontagePlacementPatternInvalid:"
                    + $"area={areaRoleCode};h3={h3LandscapeNodeName};"
                    + $"h2={h2BlockNodeName};gap={entranceGapWidth:F3};"
                    + $"roadFence={roadFenceClearance:F3};"
                    + $"setback={buildingSetback:F3};"
                    + $"pathClear={entrancePathClear}");
        }

        private static Bounds RequiredBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(value => value.enabled).ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    "RoadsideFrontageRendererMissing:" + root.name);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static float HorizontalGap(Bounds left, Bounds right) =>
            Mathf.Max(0f, right.min.x - left.max.x);
    }
}
