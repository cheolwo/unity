using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Interior.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class Hub창고업무구역Record
    {
        public string ZoneStableId = string.Empty;
        public string OwningH1StableId = string.Empty;
        public string RoleCode = string.Empty;
        public Vector3 Center;
        public Vector3 Size;
    }

    /// <summary>
    /// Hub 독립 300 KGM 업무 폐루프를 위한 25x20m 창고의 표현 전용 배치 증거입니다.
    /// 업무 완료 권위는 Simulation에 있고 이 Root는 승인된 계획의 Synty 표현만 보존합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hub창고정밀배치Root : MonoBehaviour
    {
        public const string RequiredStableId =
            "hub-warehouse-placement:jinbu-independent-300kg.v1";
        public const string RequiredSchemaVersion =
            InteriorLayoutCodes.SchemaVersionV2;

        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string schemaVersion = string.Empty;
        [SerializeField] private string buildingPlacementStableId = string.Empty;
        [SerializeField] private string placementControlRuleRevision = string.Empty;
        [SerializeField] private string interiorPlanHashSha256 = string.Empty;
        [SerializeField] private string baseInteriorPlanHashSha256 = string.Empty;
        [SerializeField] private string visualMetricCatalogHashSha256 = string.Empty;
        [SerializeField] private Vector3 buildingSize;
        [SerializeField] private Hub창고업무구역Record[] workZones =
            Array.Empty<Hub창고업무구역Record>();
        [SerializeField] private Hub창고배치객체Record[] placementReceipts =
            Array.Empty<Hub창고배치객체Record>();
        [SerializeField] private Transform[] routeAnchors = Array.Empty<Transform>();
        [SerializeField] private Hub창고Npc업무경로View[] npcWorkRoutes =
            Array.Empty<Hub창고Npc업무경로View>();
        [SerializeField] private Transform cutawayRoofRoot = null!;
        [SerializeField] private bool traversalValidated;
        [SerializeField] private bool presentationOnly = true;

        public string StableId => stableId;
        public string SchemaVersion => schemaVersion;
        public string BuildingPlacementStableId => buildingPlacementStableId;
        public string PlacementControlRuleRevision => placementControlRuleRevision;
        public string InteriorPlanHashSha256 => interiorPlanHashSha256;
        public Vector3 BuildingSize => buildingSize;
        public Hub창고업무구역Record[] WorkZones => workZones;
        public Hub창고배치객체Record[] PlacementReceipts => placementReceipts;
        public Transform[] RouteAnchors => routeAnchors;
        public Hub창고Npc업무경로View[] NpcWorkRoutes => npcWorkRoutes;
        public Transform CutawayRoofRoot => cutawayRoofRoot;
        public bool TraversalValidated => traversalValidated;
        public bool PresentationOnly => presentationOnly;

        public void Configure(InteriorPlacementPlan plan,
            InteriorStructureDefinition structure,
            Vector3 exteriorBuildingSize,
            Hub창고배치객체Record[] receipts,
            Transform[] routes,
            Hub창고Npc업무경로View[] npcRoutes,
            Transform roofRoot)
        {
            stableId = RequiredStableId;
            schemaVersion = plan.SchemaVersion;
            buildingPlacementStableId = plan.BuildingPlacementStableId;
            placementControlRuleRevision = plan.PlacementControlRuleRevision;
            interiorPlanHashSha256 = plan.InteriorPlacementPlanHashSha256;
            baseInteriorPlanHashSha256 =
                plan.BaseInteriorPlacementPlanHashSha256;
            visualMetricCatalogHashSha256 =
                plan.VisualMetricCatalogHashSha256;
            if (Mathf.Abs((float)structure.UsableBounds.Size.X - 24f) > .01f
                || Mathf.Abs((float)structure.UsableBounds.Size.Z - 19f) > .01f)
                throw new InvalidOperationException(
                    "HubWarehouseUsableBoundsInvalid");
            buildingSize = exteriorBuildingSize;
            workZones = plan.Zones.Select(zone => new Hub창고업무구역Record
            {
                ZoneStableId = zone.ZoneStableId,
                OwningH1StableId = zone.OwningH1StableId,
                RoleCode = zone.RoleCode,
                Center = ToVector3(zone.Bounds.Center),
                Size = ToVector3(zone.Bounds.Size),
            }).ToArray();
            placementReceipts = receipts;
            routeAnchors = routes;
            npcWorkRoutes = npcRoutes;
            cutawayRoofRoot = roofRoot;
            traversalValidated = plan.TraversalValidated;
            presentationOnly = true;
            Validate();
        }

        public void Validate()
        {
            var issues = new List<string>();
            if (stableId != RequiredStableId) issues.Add("StableId");
            if (schemaVersion != RequiredSchemaVersion) issues.Add("Schema");
            if (string.IsNullOrWhiteSpace(buildingPlacementStableId))
                issues.Add("BuildingPlacement");
            if (placementControlRuleRevision
                != 배치통제계층Root.InteriorRevision) issues.Add("RuleRevision");
            if (string.IsNullOrWhiteSpace(interiorPlanHashSha256))
                issues.Add("PlanHash");
            if (string.IsNullOrWhiteSpace(baseInteriorPlanHashSha256))
                issues.Add("BasePlanHash");
            if (string.IsNullOrWhiteSpace(visualMetricCatalogHashSha256))
                issues.Add("VisualMetricHash");
            if (Mathf.Abs(buildingSize.x - 25f) > .01f
                || Mathf.Abs(buildingSize.z - 20f) > .01f)
                issues.Add("BuildingSize");
            if (workZones.Length != 7) issues.Add("ZoneCount");
            if (workZones.Count(value => !value.RoleCode.Contains(
                    "Aisle", StringComparison.Ordinal)) != 5)
                issues.Add("WorkZoneCount");
            if (workZones.Select(value => value.ZoneStableId)
                    .Distinct(StringComparer.Ordinal).Count() != 7)
                issues.Add("ZoneStableId");
            if (workZones.Any(value => string.IsNullOrWhiteSpace(
                    value.OwningH1StableId))) issues.Add("ZoneH1Owner");
            if (placementReceipts.Length != 7) issues.Add("ReceiptCount");
            if (placementReceipts.Any(value => value == null
                || value.PlanHashSha256 != interiorPlanHashSha256
                || !value.PlacementAccepted)) issues.Add("ReceiptLineage");
            if (routeAnchors.Length < 5
                || routeAnchors.Any(value => value == null))
                issues.Add("RouteAnchors");
            if (npcWorkRoutes.Length != 3
                || npcWorkRoutes.Any(value => value == null
                    || !value.PresentationOnly)) issues.Add("NpcRoutes");
            if (cutawayRoofRoot == null) issues.Add("CutawayRoof");
            if (!traversalValidated) issues.Add("Traversal");
            if (!presentationOnly) issues.Add("AuthorityBoundary");
            if (issues.Count > 0)
                throw new InvalidOperationException(
                    "HubWarehousePrecisionPlacementInvalid:"
                    + $"zones={workZones.Length};placements={placementReceipts.Length};"
                    + $"routes={routeAnchors.Length};npcRoutes={npcWorkRoutes.Length};"
                    + $"traversal={traversalValidated};issues="
                    + string.Join(",", issues));
        }

        private static Vector3 ToVector3(InteriorVector3 value) =>
            new((float)value.X, (float)value.Y, (float)value.Z);

        private static Vector3 ToVector3(InteriorSize3 value) =>
            new((float)value.X, (float)value.Y, (float)value.Z);
    }
}
