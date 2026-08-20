using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버가 확정한 네이처 조우를 현재 경관 위에 표현하고 접근 거리만 관찰합니다.
    /// 조우 생성·피해·승패는 결정하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 네이처조우Presenter : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private Transform encounterRoot = null!;
        [SerializeField] private GameObject monsterPrefab = null!;
        [SerializeField, Min(1f)] private float spawnRadius = 16f;
        [SerializeField, Min(.1f)] private float approachSpeed = 1.8f;
        [SerializeField, Min(.5f)] private float responseDistance = 3.4f;
        [SerializeField] private bool presentationOnly = true;

        private readonly Dictionary<string, EncounterVisual> visuals =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> requested = new(StringComparer.Ordinal);
        private string lastStatus = "네이처 경관 탐험 중";

        public event Action<string> EncounterResponseRequested = delegate { };

        public int ActiveEncounterCount => visuals.Count;
        public bool PresentationOnly => presentationOnly;
        public bool UsesPlaceholderVisual => monsterPrefab == null;
        public string LastStatus => lastStatus;

        public void Configure(플레이어경관Controller playerController,
            Transform root, GameObject? assignedMonsterPrefab)
        {
            player = playerController;
            encounterRoot = root;
            monsterPrefab = assignedMonsterPrefab!;
            presentationOnly = true;
            if (!ValidateWiring())
                throw new ArgumentException("NatureEncounterPresentationWiringInvalid");
        }

        public bool ValidateWiring()
            => player != null && encounterRoot != null && presentationOnly;

        public void Apply(네이처탐험조우StateApiModel state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.Validate();
            var active = state.ActiveEncounters();
            var activeIds = active.Select(value => value.EncounterStableId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var removed in visuals.Keys.Where(value => !activeIds.Contains(value))
                         .ToArray())
                Remove(removed);
            for (var index = 0; index < active.Length; index++)
            {
                var encounter = active[index];
                if (visuals.ContainsKey(encounter.EncounterStableId)) continue;
                visuals.Add(encounter.EncounterStableId,
                    CreateVisual(encounter, index));
            }
            lastStatus = active.Length == 0
                ? "네이처 경관 탐험 중 · 활성 위협 없음"
                : "네이처 경관 위협 접근 · " + active.Sum(value =>
                    value.ThreatUnitCount) + "개체";
        }

        public void MarkResolved(string encounterStableId)
        {
            Remove(encounterStableId);
            lastStatus = "위협 대응 완료 · 네이처 탐험으로 복귀";
        }

        public void AllowResponseRetry(string encounterStableId, string reasonCode)
        {
            requested.Remove(encounterStableId ?? string.Empty);
            lastStatus = "위협 대응 대기 · " + (string.IsNullOrWhiteSpace(reasonCode)
                ? "서버 상태 재확인 필요" : reasonCode.Trim());
        }

        public void EvaluateApproach(float deltaTime)
        {
            if (!ValidateWiring() || player.CurrentMode == 플레이어시점Mode.Strategy)
                return;
            foreach (var visual in visuals.Values.OrderBy(value => value.StableId,
                         StringComparer.Ordinal))
            {
                var target = player.transform.position;
                target.y = visual.Root.position.y;
                var distance = Vector3.Distance(visual.Root.position, target);
                if (distance > responseDistance)
                {
                    visual.Root.position = Vector3.MoveTowards(visual.Root.position,
                        target, Mathf.Max(0f, deltaTime) * approachSpeed);
                    var facing = target - visual.Root.position;
                    if (facing.sqrMagnitude > .001f)
                        visual.Root.rotation = Quaternion.LookRotation(facing.normalized,
                            Vector3.up);
                    distance = Vector3.Distance(visual.Root.position, target);
                    if (distance > responseDistance) continue;
                }
                if (requested.Add(visual.StableId))
                {
                    lastStatus = "위협 조우 · 1/3인칭 대응 준비";
                    EncounterResponseRequested(visual.StableId);
                }
            }
        }

        private void Update() => EvaluateApproach(Time.deltaTime);

        private EncounterVisual CreateVisual(네이처탐험조우ApiModel encounter,
            int ordinal)
        {
            var root = new GameObject("NatureThreat_" + SafeName(
                encounter.EncounterStableId)).transform;
            root.SetParent(encounterRoot, false);
            root.localPosition = SpawnDirection(encounter.NatureRouteCode, ordinal)
                * spawnRadius;
            root.position = new Vector3(root.position.x, player.transform.position.y,
                root.position.z);
            var count = Mathf.Clamp(encounter.ThreatUnitCount, 1, 5);
            for (var index = 0; index < count; index++)
            {
                GameObject visual;
                if (monsterPrefab != null)
                    visual = Instantiate(monsterPrefab, root);
                else
                    visual = CreatePlaceholder(root, index);
                visual.name = "ThreatUnit_" + (index + 1);
                visual.transform.localPosition = new Vector3(
                    (index - (count - 1) * .5f) * 1.05f, 0f,
                    -(index % 2) * .65f);
            }
            return new EncounterVisual(encounter.EncounterStableId, root);
        }

        private static GameObject CreatePlaceholder(Transform parent, int ordinal)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            value.transform.SetParent(parent, false);
            value.transform.localScale = new Vector3(.72f, 1.05f, .72f);
            var collider = value.GetComponent<Collider>();
            if (collider != null) DestroyRuntimeSafe(collider);
            var renderer = value.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                material.color = ordinal % 2 == 0
                    ? new Color(.24f, .07f, .09f)
                    : new Color(.13f, .16f, .09f);
                renderer.sharedMaterial = material;
            }
            return value;
        }

        private static Vector3 SpawnDirection(string routeCode, int ordinal)
        {
            var value = routeCode switch
            {
                네이처탐험조우Codes.NatureToFarm => new Vector3(1f, 0f, .65f),
                네이처탐험조우Codes.NatureToTown => new Vector3(-1f, 0f, .55f),
                네이처탐험조우Codes.NatureToCityHub => new Vector3(.2f, 0f, -1f),
                _ => new Vector3(.55f, 0f, 1f),
            };
            return Quaternion.Euler(0f, ordinal * 19f, 0f) * value.normalized;
        }

        private void Remove(string stableId)
        {
            if (!visuals.Remove(stableId, out var visual)) return;
            requested.Remove(stableId);
            if (visual.Root != null) DestroyRuntimeSafe(visual.Root.gameObject);
        }

        private static void DestroyRuntimeSafe(UnityEngine.Object value)
        {
            if (UnityEngine.Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private static string SafeName(string value)
            => string.Concat((value ?? string.Empty).Select(character =>
                char.IsLetterOrDigit(character) ? character : '_'));

        private void OnGUI()
        {
            if (ActiveEncounterCount == 0 && !lastStatus.Contains("복귀")) return;
            GUI.color = new Color(.045f, .065f, .045f, .9f);
            GUI.DrawTexture(new Rect(20f, Screen.height - 112f, 440f, 86f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, Screen.height - 104f, 410f, 22f), lastStatus);
            GUI.Label(new Rect(34f, Screen.height - 80f, 410f, 22f),
                "F2 1인칭 직접 대응 · F3 3인칭 전술 대응");
            GUI.Label(new Rect(34f, Screen.height - 56f, 410f, 22f),
                UsesPlaceholderVisual
                    ? "대체 위협 형상 · 실제 Synty 몬스터 자산 미연결"
                    : "서버 조우 상태와 연결된 몬스터 표현");
        }

        private sealed class EncounterVisual
        {
            public EncounterVisual(string stableId, Transform root)
            {
                StableId = stableId;
                Root = root;
            }

            public string StableId { get; }
            public Transform Root { get; }
        }
    }
}
