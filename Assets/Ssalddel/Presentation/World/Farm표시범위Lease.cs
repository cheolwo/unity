using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 저장 Scene을 바꾸지 않고 기존 Farm의 정적 검토 표현만 잠시 노출합니다.
    /// 전투·Actor·Lot·배치 작성자는 허용 목록 밖에 둡니다.
    /// </summary>
    public sealed class Farm표시범위Lease : IDisposable
    {
        private static readonly string[] AllowedDirectChildren =
        {
            "DistrictSurface", "Label_FARM",
            "FarmPlot_0_0", "FarmPlot_0_1", "FarmPlot_0_2", "FarmPlot_0_3", "FarmPlot_0_4",
            "FarmPlot_1_0", "FarmPlot_1_1", "FarmPlot_1_2", "FarmPlot_1_3", "FarmPlot_1_4",
            "FarmManagementHighlight_0_0", "FarmManagementHighlight_0_1",
            "FarmManagementHighlight_0_2", "FarmManagementHighlight_0_3",
            "FarmManagementHighlight_0_4", "FarmManagementHighlight_1_0",
            "FarmManagementHighlight_1_1", "FarmManagementHighlight_1_2",
            "FarmManagementHighlight_1_3", "FarmManagementHighlight_1_4",
            "VisualBaseRoot",
        };

        private readonly Transform settlement;
        private readonly Transform districts;
        private readonly Transform farm;
        private readonly Dictionary<GameObject, bool> originalActive = new();
        private readonly Dictionary<GameObject, bool> appliedActive = new();
        private readonly Dictionary<Transform, Transform> originalParents;
        private readonly Dictionary<Transform, Transform> ancestorParents = new();
        private bool acquired;

        private Farm표시범위Lease(Transform settlementRoot, Transform districtsRoot, Transform farmRoot)
        {
            settlement = settlementRoot;
            districts = districtsRoot;
            farm = farmRoot;
            originalParents = settlement.GetComponentsInChildren<Transform>(true)
                .ToDictionary(value => value, value => value.parent);
            for (var ancestor = settlement.parent; ancestor != null; ancestor = ancestor.parent)
                ancestorParents.Add(ancestor, ancestor.parent);
        }

        public Transform FarmRoot => farm;
        public static IReadOnlyList<string> RequiredChildNames => Array.AsReadOnly(AllowedDirectChildren);
        public bool IsAcquired => acquired;
        public Transform[] AllowedRoots => AllowedDirectChildren.Select(farm.Find).ToArray();

        public static bool TryAcquire(Transform settlementRoot,
            out Farm표시범위Lease lease, out string diagnostic)
        {
            lease = null!;
            diagnostic = "FarmViewHierarchyMissing";
            if (settlementRoot == null) return false;
            var districtsRoot = settlementRoot.Find("Districts");
            var farmRoot = districtsRoot?.Find("FarmDistrict");
            if (districtsRoot == null || farmRoot == null) return false;
            if (settlementRoot.gameObject.activeSelf)
            {
                diagnostic = "FarmViewSettlementAlreadyInUse";
                return false;
            }
            if (settlementRoot.GetComponents<Component>().Any(value => value is not Transform)
                || districtsRoot.GetComponents<Component>().Any(value => value is not Transform))
            {
                diagnostic = "FarmViewAncestorHasRuntimeComponent";
                return false;
            }

            var current = farmRoot.Cast<Transform>().Select(value => value.name).ToArray();
            if (AllowedDirectChildren.Except(current, StringComparer.Ordinal).Any())
            {
                diagnostic = "FarmViewAllowedChildMissing";
                return false;
            }
            if (current.Length != current.Distinct(StringComparer.Ordinal).Count())
            {
                diagnostic = "FarmViewDuplicateChild";
                return false;
            }
            if (farmRoot.GetComponents<Component>().Any(value => value is not Transform
                    && value is not SimulationWorldDistrictView)
                || AllowedDirectChildren.Select(farmRoot.Find).Any(root =>
                    root.GetComponentsInChildren<Component>(true).Any(value => !IsStaticComponent(value))))
            {
                diagnostic = "FarmViewUnsupportedComponent";
                return false;
            }

            var candidate = new Farm표시범위Lease(settlementRoot, districtsRoot, farmRoot);
            try
            {
                candidate.Acquire();
                lease = candidate;
                diagnostic = "FarmViewStaticPresentationAcquired";
                return true;
            }
            catch (Exception error)
            {
                candidate.Dispose();
                diagnostic = "FarmViewAcquireRejected:" + error.Message;
                return false;
            }
        }

        public bool ValidateTopology()
            => settlement != null && originalParents.Count
                == settlement.GetComponentsInChildren<Transform>(true).Length
                && originalParents.All(entry => HasOriginalAncestry(entry.Key));

        private bool HasOriginalAncestry(Transform value)
        {
            if (value == null) return false;
            for (var current = value; current != null; current = current.parent)
            {
                if (!originalParents.TryGetValue(current, out var parent)
                    && !ancestorParents.TryGetValue(current, out parent)) return false;
                if (current.parent != parent) return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (!acquired) return;
            // 하위를 복원하기 전에 상위를 숨겨 제외 객체가 한 프레임이라도 나타나지 않게 한다.
            if (HasOriginalAncestry(settlement) && originalActive.ContainsKey(settlement.gameObject)
                && settlement.gameObject.activeSelf == appliedActive[settlement.gameObject])
                settlement.gameObject.SetActive(false);
            foreach (var entry in originalActive.Reverse())
                if (entry.Key != null && HasOriginalAncestry(entry.Key.transform)
                    && entry.Key.activeSelf == appliedActive[entry.Key])
                    entry.Key.SetActive(entry.Value);
            // 외부로 옮겨진 항목은 건드리지 않고 이번 임대 기록만 소비한다.
            originalActive.Clear();
            appliedActive.Clear();
            acquired = false;
        }

        private void Acquire()
        {
            acquired = true;
            foreach (Transform child in settlement)
                if (child != districts) RecordAndSet(child.gameObject, false);
            foreach (Transform child in districts)
                if (child != farm) RecordAndSet(child.gameObject, false);
            foreach (Transform child in farm)
                if (!AllowedDirectChildren.Contains(child.name, StringComparer.Ordinal))
                    RecordAndSet(child.gameObject, false);
            // 원래 비활성인 highlight 등 허용 자식은 건드리지 않는다.
            RecordAndSet(farm.gameObject, true);
            RecordAndSet(districts.gameObject, true);
            RecordAndSet(settlement.gameObject, true);
        }

        private void RecordAndSet(GameObject value, bool active)
        {
            if (value.activeSelf == active) return;
            if (!originalActive.ContainsKey(value)) originalActive.Add(value, value.activeSelf);
            appliedActive[value] = active;
            value.SetActive(active);
        }

        private static bool IsStaticComponent(Component value)
            => value is Transform || value is MeshFilter || value is MeshRenderer
                || value is BoxCollider || value is MeshCollider || value is TextMesh
                || value is SimulationWorldNavigationTargetView
                || value is 농장경영선택대상View || value is WorldVisualInstanceView;
    }
}
