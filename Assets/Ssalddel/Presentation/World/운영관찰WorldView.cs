using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

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
        private readonly Dictionary<string, (bool Visible, bool Retain)> 운영계통별표현 =
            new(StringComparer.Ordinal);

        private Dictionary<string, 운영관찰배치지점> places;
        private bool presentationVisible = true;
        public void SetPresentationVisible(bool value)
        {
            presentationVisible = value;
            foreach (var item in objects.Values)
                if (item != null) item.gameObject.SetActive(표현표시(item.OperatingSystemId));
        }

        /// <summary>업무 상태를 변경하지 않고 해당 OS의 표현 보존과 표시만 조정합니다.</summary>
        public void SetOperatingSystemPresentation(string operatingSystemId, bool visible, bool retain)
        {
            if (string.IsNullOrWhiteSpace(operatingSystemId))
                throw new ArgumentException("OperatingSystemIdMissing", nameof(operatingSystemId));
            if (운영계통별표현.TryGetValue(operatingSystemId, out var previous)
                && previous.Visible == visible && previous.Retain == retain) return;
            운영계통별표현[operatingSystemId] = (visible, retain);
            RebuildIndex();
            foreach (var item in objects.Values.Where(value => value != null
                         && string.Equals(value.OperatingSystemId, operatingSystemId,
                             StringComparison.Ordinal)).ToArray())
            {
                if (retain) item.gameObject.SetActive(표현표시(operatingSystemId));
                else
                {
                    objects.Remove(item.ObjectStableId);
                    표현해제(item);
                }
            }
        }
        public int UnplacedCount { get; private set; }
        public string PlacementStatus { get; private set; } = "NotLoaded";

        private bool LoadPlaces()
        {
            if (places != null) return true;
            try
            {
                var map = Resources.Load<TextAsset>("SagajeongReference");
                var catalog = Resources.Load<TextAsset>("SagajeongObservationPlacement");
                if (map == null || catalog == null) throw new ArgumentException();
                places = 운영관찰지역배치검사.Validate(JsonUtility.FromJson<운영관찰지역배치자료>(catalog.text),
                    JsonUtility.FromJson<공간참고자료>(map.text), map.bytes);
                PlacementStatus = "Ready";
                return true;
            }
            catch (ArgumentException) { PlacementStatus = "MapOrPlacementRejected"; return false; }
        }

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
            if (!LoadPlaces()) { UnplacedCount = plan.Instructions?.Length ?? 0; Clear(); return; }
            UnplacedCount = 0;
            RebuildIndex();
            var frozenIds = new HashSet<string>(
                (applyDiagnostics ?? Array.Empty<OperationalWorldSceneApplyDiagnostic>())
                    .Where(value => value.ExistingObjectFrozen)
                    .Select(value => value.SnapshotStableId),
                StringComparer.Ordinal);
            var incomingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instruction in plan.Instructions ?? Array.Empty<OperationalWorldPlacementInstruction>())
            {
                if (instruction == null || string.IsNullOrWhiteSpace(instruction.SemanticPlaceStableId)
                    || !places.TryGetValue(instruction.SemanticPlaceStableId, out var place))
                { UnplacedCount++; continue; }
                if (instruction.ExpiresAtUtc <= DateTime.UtcNow) continue;
                if (!표현유지(instruction.OperatingSystemId)) continue;
                incomingIds.Add(instruction.ObjectStableId);
                if (!objects.TryGetValue(instruction.ObjectStableId, out var view))
                    view = Create(instruction, place);
                view.Apply(instruction, frozenIds.Contains(instruction.ObjectStableId), place);
                view.gameObject.SetActive(표현표시(view.OperatingSystemId));
            }

            foreach (var stale in objects.Keys.Where(id => !incomingIds.Contains(id)).ToArray())
            {
                var view = objects[stale];
                objects.Remove(stale);
                if (view != null) 표현해제(view);
            }
        }

        public void Clear()
        {
            RebuildIndex();
            foreach (var view in objects.Values.Where(value => value != null).ToArray())
                표현해제(view);
            objects.Clear();
        }

        public void Expire(DateTime utcNow)
        {
            foreach (var stale in objects.Values.Where(x => x != null && x.ExpiresAtUtc <= utcNow).ToArray())
            {
                objects.Remove(stale.ObjectStableId);
                표현해제(stale);
            }
        }

        private 운영관찰ObjectView Create(OperationalWorldPlacementInstruction instruction, 운영관찰배치지점 place)
        {
            var group = transform.Find(GroupName(instruction.OperatingSystemId));
            if (group == null)
            {
                var groupObject = new GameObject(GroupName(instruction.OperatingSystemId));
                groupObject.transform.SetParent(transform, false);
                group = groupObject.transform;
            }
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = "OperationalObject__" + SafeName(instruction.ObjectStableId);
            primitive.transform.SetParent(group, false);
            primitive.layer = 사가정운영디오라마View.ObservationLayer;
            primitive.transform.localScale = place.role == "cargo" ? new Vector3(14,6,8) : new Vector3(12,8,12);
            var shader = Resources.Load<Shader>("관찰도형명암") ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            var view = primitive.AddComponent<운영관찰ObjectView>();
            view.OwnMaterial(material);
            // 지도 위 역할 표식이며 실제 시설 또는 차량의 현재 위치가 아닙니다.
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = place.role == "cargo" ? "차량 역할 표식" : "시설 역할 표식";
            roof.layer = primitive.layer; roof.transform.SetParent(primitive.transform,false);
            roof.transform.localPosition = new Vector3(0,.62f,0);
            roof.transform.localScale = new Vector3(1.15f,.2f,1.15f);
            roof.GetComponent<Renderer>().sharedMaterial = material;
            objects.Add(instruction.ObjectStableId, view);
            primitive.SetActive(표현표시(instruction.OperatingSystemId));
            return view;
        }

        private bool 표현유지(string operatingSystemId)
            => !운영계통별표현.TryGetValue(operatingSystemId, out var state) || state.Retain;

        private bool 표현표시(string operatingSystemId)
            => presentationVisible && (!운영계통별표현.TryGetValue(operatingSystemId, out var state)
                                        || state.Visible && state.Retain);

        private void 표현해제(운영관찰ObjectView view)
        {
            var group = view.transform.parent;
            var ownedGroup = group != null && group.parent == transform
                             && group.name == GroupName(view.OperatingSystemId);
            view.gameObject.SetActive(false);
            view.ReleaseOwnedMaterial();
            DestroyObject(view.gameObject);
            if (ownedGroup && group.childCount == 0) DestroyObject(group.gameObject);
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
            // Play Mode의 지연 파기 전에 색인과 활성 표현에서 즉시 분리합니다.
            value.SetActive(false);
            value.transform.SetParent(null, false);
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

        private DateTime expiresAtUtc;
        private Material ownedMaterial;
        public DateTime ExpiresAtUtc => expiresAtUtc;
        public void OwnMaterial(Material material) { ownedMaterial = material; }

        /// <summary>실행되지 않은 EditMode 객체도 소유 재료를 해제하도록 파기 전에 명시적으로 호출합니다.</summary>
        public void ReleaseOwnedMaterial()
        {
            var material = ownedMaterial;
            ownedMaterial = null;
            if (material == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }

        private void OnDestroy() => ReleaseOwnedMaterial();

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

        public void Apply(OperationalWorldPlacementInstruction instruction, bool isFrozen, 운영관찰배치지점 place)
        {
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));
            if (revision > 0 && instruction.Revision < revision) { frozen = true; ApplyColor(); return; }
            var keepFrozen = frozen && instruction.Revision <= revision;
            objectStableId = instruction.ObjectStableId;
            operatingSystemId = instruction.OperatingSystemId;
            workStableId = instruction.WorkStableId;
            lifecycleStageCode = instruction.LifecycleStageCode;
            attentionStateCode = instruction.AttentionStateCode;
            semanticPlaceStableId = instruction.SemanticPlaceStableId;
            sourceKindCode = instruction.SourceKindCode;
            scenarioRunStableId = instruction.ScenarioRunStableId;
            revision = instruction.Revision;
            frozen = isFrozen || keepFrozen;
            expiresAtUtc = instruction.ExpiresAtUtc;
            transform.localPosition = new Vector3(place.x,place.y,place.z);
            transform.localRotation = Quaternion.Euler(0,place.yaw,0);
            ApplyColor();
        }

        private void ApplyColor()
        {
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
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
            }
        }

    }
}
