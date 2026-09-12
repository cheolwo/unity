using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "운영 상태 사본의 안정 식별자별 primitive 배치 객체를 공식 World Root 아래 조정한다.",
        Boundary = "서버 상태를 변경하지 않고 메모리 상태만 표현하며 실제 업무 완료 증거가 아니다.")]
    public sealed class 운영관찰WorldView : MonoBehaviour
    {
        private readonly Dictionary<string, 운영관찰ObjectView> objects =
            new(StringComparer.Ordinal);

        public int ObjectCount => objects.Count;
        public IReadOnlyList<운영관찰ObjectView> Objects => objects.Values
            .OrderBy(value => value.ObjectStableId, StringComparer.Ordinal)
            .ToArray();

        public void Apply(
            OperationalWorldPlacementPlan plan,
            IReadOnlyList<OperationalWorldSceneApplyDiagnostic> applyDiagnostics)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!plan.Accepted) return;
            RebuildIndex();
            var frozenIds = new HashSet<string>(
                (applyDiagnostics ?? Array.Empty<OperationalWorldSceneApplyDiagnostic>())
                    .Where(value => value.ExistingObjectFrozen)
                    .Select(value => value.SnapshotStableId),
                StringComparer.Ordinal);
            var incomingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instruction in plan.Instructions ?? Array.Empty<OperationalWorldPlacementInstruction>())
            {
                incomingIds.Add(instruction.ObjectStableId);
                if (!objects.TryGetValue(instruction.ObjectStableId, out var view))
                    view = Create(instruction);
                view.Apply(instruction, frozenIds.Contains(instruction.ObjectStableId));
            }

            foreach (var stale in objects.Keys.Where(id => !incomingIds.Contains(id)).ToArray())
            {
                var view = objects[stale];
                objects.Remove(stale);
                if (view != null) DestroyObject(view.gameObject);
            }
        }

        public void Clear()
        {
            RebuildIndex();
            foreach (var view in objects.Values.Where(value => value != null).ToArray())
                DestroyObject(view.gameObject);
            objects.Clear();
        }

        private 운영관찰ObjectView Create(OperationalWorldPlacementInstruction instruction)
        {
            var group = transform.Find(GroupName(instruction.OperatingSystemId));
            if (group == null)
            {
                var groupObject = new GameObject(GroupName(instruction.OperatingSystemId));
                groupObject.transform.SetParent(transform, false);
                group = groupObject.transform;
            }
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            primitive.name = "OperationalObject__" + SafeName(instruction.ObjectStableId);
            primitive.transform.SetParent(group, false);
            primitive.transform.localScale = new Vector3(.8f, .8f, .8f);
            var view = primitive.AddComponent<운영관찰ObjectView>();
            objects.Add(instruction.ObjectStableId, view);
            return view;
        }

        private void RebuildIndex()
        {
            objects.Clear();
            foreach (var view in GetComponentsInChildren<운영관찰ObjectView>(true))
                if (!string.IsNullOrWhiteSpace(view.ObjectStableId)
                    && !objects.ContainsKey(view.ObjectStableId))
                    objects.Add(view.ObjectStableId, view);
        }

        private static string GroupName(string operatingSystemId)
            => "OS__" + SafeName(operatingSystemId);

        private static string SafeName(string value)
            => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

        private static void DestroyObject(GameObject value)
        {
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }

    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "운영 상태 사본 한 건의 판본·생명주기·주의 상태를 primitive로 표시한다.",
        Boundary = "표현용 좌표만 사용하고 GPS, 정확 주소, 사용자 식별정보를 보유하지 않는다.")]
    public sealed class 운영관찰ObjectView : MonoBehaviour
    {
        [SerializeField] private string objectStableId = string.Empty;
        [SerializeField] private string operatingSystemId = string.Empty;
        [SerializeField] private string workStableId = string.Empty;
        [SerializeField] private string lifecycleStageCode = string.Empty;
        [SerializeField] private string attentionStateCode = string.Empty;
        [SerializeField] private string semanticPlaceStableId = string.Empty;
        [SerializeField] private string sourceKindCode = string.Empty;
        [SerializeField] private string scenarioRunStableId = string.Empty;
        [SerializeField] private long revision;
        [SerializeField] private bool frozen;

        public string ObjectStableId => objectStableId;
        public string OperatingSystemId => operatingSystemId;
        public string WorkStableId => workStableId;
        public string LifecycleStageCode => lifecycleStageCode;
        public string AttentionStateCode => attentionStateCode;
        public string SemanticPlaceStableId => semanticPlaceStableId;
        public string SourceKindCode => sourceKindCode;
        public string ScenarioRunStableId => scenarioRunStableId;
        public long Revision => revision;
        public bool Frozen => frozen;

        public void Apply(OperationalWorldPlacementInstruction instruction, bool isFrozen)
        {
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));
            objectStableId = instruction.ObjectStableId;
            operatingSystemId = instruction.OperatingSystemId;
            workStableId = instruction.WorkStableId;
            lifecycleStageCode = instruction.LifecycleStageCode;
            attentionStateCode = instruction.AttentionStateCode;
            semanticPlaceStableId = instruction.SemanticPlaceStableId;
            sourceKindCode = instruction.SourceKindCode;
            scenarioRunStableId = instruction.ScenarioRunStableId;
            revision = instruction.Revision;
            frozen = isFrozen;
            transform.localPosition = Position(instruction.AnchorKey, instruction.ObjectStableId);
            ApplyColor();
        }

        private void ApplyColor()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null) return;
            var color = frozen
                ? new Color(.45f, .45f, .45f)
                : attentionStateCode == OperationalWorldAttentionStateCodes.Recovered
                    ? new Color(1f, .65f, .12f)
                    : operatingSystemId switch
                    {
                        OperationalWorldOperatingSystemIds.FoodDelivery => new Color(.95f, .35f, .28f),
                        OperationalWorldOperatingSystemIds.DomesticCargoTransport => new Color(.2f, .55f, .95f),
                        OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment => new Color(.25f, .75f, .45f),
                        OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics => new Color(.75f, .4f, .9f),
                        _ => Color.white
                    };
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private static Vector3 Position(string anchorKey, string stableId)
        {
            var center = anchorKey switch
            {
                "world.area.food-delivery" => new Vector3(-12f, 1f, -8f),
                "world.area.cargo" => new Vector3(12f, 1f, -8f),
                "world.area.warehouse" => new Vector3(-12f, 1f, 8f),
                "world.area.mart" => new Vector3(12f, 1f, 8f),
                _ => Vector3.up
            };
            var hash = StableHash(stableId);
            return center + new Vector3(((hash & 7) - 3) * 1.25f, 0f, (((hash >> 3) & 7) - 3) * 1.25f);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return (int)(hash & int.MaxValue);
            }
        }
    }
}
