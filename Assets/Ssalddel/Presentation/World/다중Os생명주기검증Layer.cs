using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "네 공간형 OS 상태 사본의 단계·보류·회복·완료를 사가정 디오라마에 runtime-only로 표시한다.",
        Boundary = "운영 Command와 저장을 제공하지 않으며 실제 업체·사람 위치 또는 업무 완료 근거가 아니다.")]
    public sealed class 다중Os생명주기검증Layer : MonoBehaviour
    {
        public const string RuntimeRootName = "OSLifecycleValidationRuntime";

        private readonly 다중Os생명주기표현Projector projector = new();
        private readonly Dictionary<string, 다중Os생명주기MarkerView> markers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, (bool Visible, bool Retain)> 운영계통별표현 =
            new(StringComparer.Ordinal);
        private readonly List<다중Os생명주기표현Diagnostic> diagnostics = new();
        private 운영관찰WorldView worldView;
        private 사가정운영디오라마View diorama;
        private 사가정음식점아이콘검증Layer restaurantLayer;
        private Transform runtimeRoot;
        private 다중Os생명주기표현State[] currentStates =
            Array.Empty<다중Os생명주기표현State>();
        private GUIStyle titleStyle;
        private GUIStyle rowStyle;
        private GUIStyle noteStyle;
        private bool overlayVisible = true;
        private bool restaurantFixtureAttempted;

        public int StateCount => currentStates.Length;
        public int MarkerCount => markers.Count;
        public int DiagnosticCount => diagnostics.Count;
        public int GeneralizedPlacementCount { get; private set; }
        public Transform RuntimeRoot => runtimeRoot;
        public IReadOnlyList<다중Os생명주기표현State> CurrentStates
            => currentStates.ToArray();
        public IReadOnlyList<다중Os생명주기표현Diagnostic> Diagnostics
            => diagnostics.ToArray();
        public string RestaurantIconInputStatus
            => restaurantLayer != null ? restaurantLayer.StatusCode
                : 다중Os생명주기검증Policy.RestaurantIconInputStatus;
        public int RestaurantIconCount => restaurantLayer != null
            ? restaurantLayer.IconCount : 0;
        public string RestaurantFixtureHash => restaurantLayer != null
            ? restaurantLayer.ContentHashSha256 : string.Empty;

        public void Configure(운영관찰WorldView value, 사가정운영디오라마View mapView = null)
        {
            worldView = value != null ? value : throw new ArgumentNullException(nameof(value));
            diorama = mapView;
            EnsureRestaurantLayer();
            if (UnityEngine.Application.isPlaying && !restaurantFixtureAttempted)
                TryLoadRestaurantFixture();
        }

        public void SetOverlayVisible(bool value) => overlayVisible = value;

        /// <summary>최신 상태 사본은 유지하고 해당 OS marker의 표시와 보존만 조정합니다.</summary>
        public void SetOperatingSystemPresentation(string operatingSystemId, bool visible, bool retain)
        {
            if (string.IsNullOrWhiteSpace(operatingSystemId))
                throw new ArgumentException("OperatingSystemIdMissing", nameof(operatingSystemId));
            if (운영계통별표현.TryGetValue(operatingSystemId, out var previous)
                && previous.Visible == visible && previous.Retain == retain) return;
            운영계통별표현[operatingSystemId] = (visible, retain);
            foreach (var marker in markers.Values.Where(value => value != null
                         && string.Equals(value.OperatingSystemId, operatingSystemId,
                             StringComparison.Ordinal)).ToArray())
            {
                if (retain) marker.gameObject.SetActive(visible);
                else
                {
                    markers.Remove(marker.ObjectStableId);
                    DestroyRuntimeObject(marker.gameObject);
                }
            }
        }

        /// <summary>권한 철회·관찰 종료 시 해당 OS의 메모리 사본과 표현을 즉시 삭제합니다.</summary>
        public void ClearOperatingSystem(string operatingSystemId)
        {
            if (string.IsNullOrWhiteSpace(operatingSystemId))
                throw new ArgumentException("OperatingSystemIdMissing", nameof(operatingSystemId));
            foreach (var marker in markers.Values.Where(value => value != null
                         && string.Equals(value.OperatingSystemId, operatingSystemId,
                             StringComparison.Ordinal)).ToArray())
            {
                markers.Remove(marker.ObjectStableId);
                DestroyRuntimeObject(marker.gameObject);
            }
            currentStates = currentStates.Where(value => !string.Equals(value.OperatingSystemId,
                operatingSystemId, StringComparison.Ordinal)).ToArray();
            diagnostics.RemoveAll(value => string.Equals(value.OperatingSystemId,
                operatingSystemId, StringComparison.Ordinal));
            GeneralizedPlacementCount = diagnostics.Count(value =>
                value.Code == "LifecycleSyntheticPlaceGeneralized");
        }

        public void Expire(DateTime utcNow)
        {
            var expired = new HashSet<string>(currentStates
                .Where(value => value.ExpiresAtUtc <= utcNow)
                .Select(value => value.ObjectStableId), StringComparer.Ordinal);
            if (expired.Count == 0) return;
            foreach (var id in expired)
            {
                if (!markers.TryGetValue(id, out var marker)) continue;
                markers.Remove(id);
                if (marker != null) DestroyRuntimeObject(marker.gameObject);
            }
            currentStates = currentStates.Where(value => !expired.Contains(value.ObjectStableId)).ToArray();
            diagnostics.RemoveAll(value => expired.Contains(value.ObjectStableId));
            GeneralizedPlacementCount = diagnostics.Count(value =>
                value.Code == "LifecycleSyntheticPlaceGeneralized");
        }

        public bool TryLoadRestaurantFixture(string requestedPath = null)
        {
            EnsureRestaurantLayer();
            restaurantFixtureAttempted = true;
            try
            {
                return restaurantLayer.TryLoadLocalFixture(requestedPath);
            }
            catch (Exception error) when (error is FormatException
                                          || error is InvalidOperationException
                                          || error is IOException
                                          || error is UnauthorizedAccessException)
            {
                restaurantLayer.Reject(error.Message);
                return false;
            }
        }

        public void Apply(OperationalWorldPlacementPlan source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (worldView == null) worldView = GetComponent<운영관찰WorldView>();
            if (diorama == null) diorama = GetComponent<사가정운영디오라마View>();
            if (worldView == null) throw new InvalidOperationException("MultiOsLifecycleWorldViewMissing");

            var plan = projector.Create(source);
            diagnostics.Clear();
            diagnostics.AddRange(plan.Diagnostics);
            if (!plan.Accepted) return;

            EnsureRuntimeRoot();
            GeneralizedPlacementCount = 0;
            var worldObjects = worldView.Objects.ToDictionary(
                value => value.ObjectStableId, StringComparer.Ordinal);
            var resolved = new List<ResolvedPlacement>();
            var accepted = new List<다중Os생명주기표현State>();
            foreach (var state in plan.States)
            {
                if (!표현유지(state.OperatingSystemId))
                {
                    // 표현 해제는 검증된 업무 상태 사본을 삭제하거나 위치를 만들어내지 않습니다.
                    accepted.Add(state);
                    continue;
                }
                if (string.Equals(state.SourceKindCode,
                        Ssalddel.WorkflowRules.Contracts.OperationalWorldSceneSourceKinds.VerificationSample,
                        StringComparison.Ordinal)
                    && diorama?.Places != null
                    && 다중Os생명주기검증Policy.TryResolveVerificationAnchor(
                        state.OperatingSystemId, state.SemanticPlaceStableId,
                        out var generalizedAnchor)
                    && diorama.Places.TryGetValue(
                        generalizedAnchor.BaseAnchorStableId, out var anchor))
                {
                    resolved.Add(new ResolvedPlacement(state,
                        "generalized:" + generalizedAnchor.SemanticPlaceStableId,
                        new Vector3(anchor.x + generalizedAnchor.OffsetX,
                            anchor.y + generalizedAnchor.OffsetY,
                            anchor.z + generalizedAnchor.OffsetZ), false));
                    GeneralizedPlacementCount++;
                    diagnostics.Add(new 다중Os생명주기표현Diagnostic
                    {
                        ObjectStableId = state.ObjectStableId,
                        OperatingSystemId = state.OperatingSystemId,
                        Code = "LifecycleSyntheticPlaceGeneralized"
                    });
                    continue;
                }
                if (worldObjects.TryGetValue(state.ObjectStableId, out var sourceObject))
                {
                    resolved.Add(new ResolvedPlacement(state,
                        "object:" + state.ObjectStableId,
                        transform.InverseTransformPoint(sourceObject.transform.position),
                        sourceObject.Frozen));
                    continue;
                }
                diagnostics.Add(new 다중Os생명주기표현Diagnostic
                {
                    ObjectStableId = state.ObjectStableId,
                    OperatingSystemId = state.OperatingSystemId,
                    Code = "LifecyclePlacementObjectMissing"
                });
            }
            var offsets = BuildOffsets(resolved);
            var incoming = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in resolved)
            {
                var state = placement.State;
                incoming.Add(state.ObjectStableId);
                accepted.Add(state);
                if (!markers.TryGetValue(state.ObjectStableId, out var marker))
                    marker = CreateMarker(state);
                var localPosition = placement.LocalPosition + offsets[state.ObjectStableId];
                marker.Apply(state, placement.Frozen, localPosition,
                    diorama != null ? diorama.ObservationCamera : null);
                marker.gameObject.SetActive(표현표시(state.OperatingSystemId));
            }

            foreach (var stale in markers.Keys.Where(value => !incoming.Contains(value)).ToArray())
            {
                var marker = markers[stale];
                markers.Remove(stale);
                if (marker != null) DestroyRuntimeObject(marker.gameObject);
            }
            currentStates = accepted
                .OrderBy(value => value.OperatingSystemId, StringComparer.Ordinal)
                .ThenBy(value => value.ObjectStableId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool Focus(string objectStableId)
        {
            if (diorama == null || string.IsNullOrWhiteSpace(objectStableId))
                return false;
            var item = worldView?.Objects.FirstOrDefault(value =>
                string.Equals(value.ObjectStableId, objectStableId, StringComparison.Ordinal));
            if (item != null)
            {
                diorama.Focus(item);
                return true;
            }
            if (!markers.TryGetValue(objectStableId, out var marker) || marker == null)
                return false;
            diorama.FocusPoint(marker.transform.position);
            return true;
        }

        public bool FocusOperatingSystem(string operatingSystemId)
        {
            var state = currentStates.FirstOrDefault(value =>
                string.Equals(value.OperatingSystemId, operatingSystemId, StringComparison.Ordinal));
            return state != null && Focus(state.ObjectStableId);
        }

        public bool FocusCase(string caseCode)
        {
            if (string.IsNullOrWhiteSpace(caseCode)) return false;
            var suffix = ":" + caseCode;
            var state = currentStates.FirstOrDefault(value =>
                value.ObjectStableId.EndsWith(suffix, StringComparison.Ordinal));
            return state != null && Focus(state.ObjectStableId);
        }

        public bool TryGetMarkerPosition(string objectStableId, out Vector3 position)
        {
            if (markers.TryGetValue(objectStableId, out var marker) && marker != null)
            {
                position = marker.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        public void ClearLifecycleStates()
        {
            foreach (var marker in markers.Values.Where(value => value != null).ToArray())
                DestroyRuntimeObject(marker.gameObject);
            markers.Clear();
            currentStates = Array.Empty<다중Os생명주기표현State>();
            diagnostics.Clear();
            GeneralizedPlacementCount = 0;
        }

        public void Clear()
        {
            ClearLifecycleStates();
            restaurantLayer?.Clear();
            var rootToDestroy = runtimeRoot;
            runtimeRoot = null;
            if (rootToDestroy != null)
            {
                rootToDestroy.SetParent(null, false);
                DestroyRuntimeObject(rootToDestroy.gameObject);
            }
            restaurantFixtureAttempted = false;
        }

        private void OnDestroy() => Clear();

        private void EnsureRuntimeRoot()
        {
            if (runtimeRoot != null) return;
            var existing = transform.Find(RuntimeRootName);
            if (existing != null)
            {
                runtimeRoot = existing;
                runtimeRoot.gameObject.hideFlags = RuntimeHideFlags;
                return;
            }
            var root = new GameObject(RuntimeRootName)
            {
                hideFlags = RuntimeHideFlags,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            root.transform.SetParent(transform, false);
            runtimeRoot = root.transform;
        }

        private void EnsureRestaurantLayer()
        {
            EnsureRuntimeRoot();
            restaurantLayer = GetComponent<사가정음식점아이콘검증Layer>()
                              ?? gameObject.AddComponent<사가정음식점아이콘검증Layer>();
            restaurantLayer.hideFlags = RuntimeHideFlags;
            restaurantLayer.Configure(runtimeRoot,
                diorama != null ? diorama.ObservationCamera : null);
        }

        private 다중Os생명주기MarkerView CreateMarker(다중Os생명주기표현State state)
        {
            var root = new GameObject("Lifecycle__" + SafeName(state.ObjectStableId))
            {
                hideFlags = RuntimeHideFlags,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            root.transform.SetParent(runtimeRoot, false);
            var marker = root.AddComponent<다중Os생명주기MarkerView>();
            marker.BuildVisual(state.OperatingSystemId);
            markers.Add(state.ObjectStableId, marker);
            return marker;
        }

        private sealed class ResolvedPlacement
        {
            public ResolvedPlacement(다중Os생명주기표현State state,
                string anchorKey, Vector3 localPosition, bool frozen)
            {
                State = state;
                AnchorKey = anchorKey;
                LocalPosition = localPosition;
                Frozen = frozen;
            }

            public 다중Os생명주기표현State State { get; }
            public string AnchorKey { get; }
            public Vector3 LocalPosition { get; }
            public bool Frozen { get; }
        }

        private static Dictionary<string, Vector3> BuildOffsets(
            IEnumerable<ResolvedPlacement> values)
        {
            var result = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            foreach (var group in values.GroupBy(value => value.AnchorKey,
                         StringComparer.Ordinal))
            {
                var ordered = group.OrderBy(value => value.State.ObjectStableId,
                    StringComparer.Ordinal).ToArray();
                for (var index = 0; index < ordered.Length; index++)
                {
                    var centered = index - (ordered.Length - 1) * .5f;
                    result.Add(ordered[index].State.ObjectStableId,
                        new Vector3(centered * 18f, 0f, 0f));
                }
            }
            return result;
        }

        private void OnGUI()
        {
            if (!overlayVisible || !UnityEngine.Application.isPlaying
                                || currentStates.Length == 0)
                return;
            EnsureStyles();
            GUI.depth = -130;
            var width = Mathf.Min(520f, Screen.width - 24f);
            var height = 64f + currentStates.Length * 42f;
            var x = Mathf.Max(12f, (Screen.width - width) * .5f);
            var y = GetComponent<생활관찰표현Layer>() != null ? 124f : 12f;
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.Label(new Rect(x + 12f, y + 6f, width - 24f, 24f),
                "사가정 · 네 OS 생명주기 관찰", titleStyle);
            GUI.Label(new Rect(x + 12f, y + 29f, width - 24f, 18f),
                다중Os생명주기검증Policy.Disclosure + " · " + RestaurantIconInputStatus,
                noteStyle);
            for (var index = 0; index < currentStates.Length; index++)
            {
                var state = currentStates[index];
                var marker = markers.TryGetValue(state.ObjectStableId, out var value) ? value : null;
                var frozen = marker != null && marker.Frozen ? " · 갱신 보류" : string.Empty;
                var label = $"{state.OperatingSystemLabel}  {state.ActivityLabel}"
                            + $" · {state.LifecycleStageLabel}"
                            + $"  [{state.AttentionLabel}{frozen}]  r{state.Revision}";
                if (GUI.Button(new Rect(x + 12f, y + 50f + index * 42f,
                        width - 24f, 34f), label, rowStyle))
                    Focus(state.ObjectStableId);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            rowStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                padding = new RectOffset(10, 8, 4, 4)
            };
            noteStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(.95f, .82f, .5f) }
            };
        }

        private static string SafeName(string value)
            => new(value.Select(character => char.IsLetterOrDigit(character)
                ? character : '_').ToArray());

        private bool 표현유지(string operatingSystemId)
            => !운영계통별표현.TryGetValue(operatingSystemId, out var state) || state.Retain;

        private bool 표현표시(string operatingSystemId)
            => !운영계통별표현.TryGetValue(operatingSystemId, out var state)
               || state.Visible && state.Retain;

        internal static HideFlags RuntimeHideFlags
            => HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        internal static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is GameObject gameObject)
            {
                gameObject.SetActive(false);
                gameObject.transform.SetParent(null, false);
            }
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }

    [DisallowMultipleComponent]
    public sealed class 다중Os생명주기MarkerView : MonoBehaviour
    {
        public const float StageAnchorInterpolationSeconds = 1.2f;

        [SerializeField] private string objectStableId = string.Empty;
        [SerializeField] private string operatingSystemId = string.Empty;
        [SerializeField] private string lifecycleStageCode = string.Empty;
        [SerializeField] private string activityCode = string.Empty;
        [SerializeField] private string attentionStateCode = string.Empty;
        [SerializeField] private long revision;
        [SerializeField] private bool frozen;
        private Vector3 displayedBaseLocalPosition;
        private Vector3 transitionFromLocalPosition;
        private Vector3 targetLocalPosition;
        private float transitionStartedAt;
        private bool hasPlacement;
        private bool interpolating;
        private Vector3 baseScale = Vector3.one;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private TextMesh label;
        private Camera observationCamera;

        public string ObjectStableId => objectStableId;
        public string OperatingSystemId => operatingSystemId;
        public string LifecycleStageCode => lifecycleStageCode;
        public string ActivityCode => activityCode;
        public string AttentionStateCode => attentionStateCode;
        public long Revision => revision;
        public bool Frozen => frozen;
        public bool IsInterpolating => interpolating;
        public Vector3 DisplayedBaseLocalPosition => displayedBaseLocalPosition;
        public Vector3 TargetLocalPosition => targetLocalPosition;

        public void BuildVisual(string os)
        {
            if (transform.childCount > 0) return;
            var pedestal = Primitive("진행 고리", PrimitiveType.Cylinder, transform,
                new Vector3(0f, .7f, 0f), new Vector3(7f, .7f, 7f));
            pedestal.name = "단계 진행 받침";

            var type = os == OperationalWorldOperatingSystemIds.FoodDelivery
                ? PrimitiveType.Sphere
                : os == OperationalWorldOperatingSystemIds.DomesticCargoTransport
                    ? PrimitiveType.Cube
                    : os == OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment
                        ? PrimitiveType.Cylinder
                        : PrimitiveType.Capsule;
            var scale = os == OperationalWorldOperatingSystemIds.DomesticCargoTransport
                ? new Vector3(8f, 3f, 4f)
                : os == OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment
                    ? new Vector3(5f, 3f, 5f)
                    : new Vector3(4f, 4f, 4f);
            Primitive("OS 단계 표식", type, transform, new Vector3(0f, 5f, 0f), scale);

            var labelObject = new GameObject("단계 문구")
            {
                hideFlags = 다중Os생명주기검증Layer.RuntimeHideFlags,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 12f, 0f);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 52;
            label.characterSize = .18f;
            label.color = Color.white;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void Apply(다중Os생명주기표현State state, bool isFrozen,
            Vector3 localPosition, Camera camera)
            => ApplyAt(state, isFrozen, localPosition, camera, Time.unscaledTime);

        public bool ApplyAt(다중Os생명주기표현State state, bool isFrozen,
            Vector3 localPosition, Camera camera, float unscaledTime)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (transform.childCount == 0) BuildVisual(state.OperatingSystemId);
            if (hasPlacement && state.Revision < revision)
            {
                frozen = true;
                observationCamera = camera;
                ApplyColor(operatingSystemId, attentionStateCode);
                ApplyPresentation(unscaledTime);
                return false;
            }
            if (hasPlacement && state.Revision == revision)
            {
                frozen = frozen || isFrozen;
                observationCamera = camera;
                ApplyColor(operatingSystemId, attentionStateCode);
                ApplyPresentation(unscaledTime);
                return true;
            }

            var previousBase = hasPlacement
                ? EvaluateBaseLocalPosition(unscaledTime)
                : localPosition;
            objectStableId = state.ObjectStableId;
            operatingSystemId = state.OperatingSystemId;
            lifecycleStageCode = state.LifecycleStageCode;
            activityCode = state.ActivityCode;
            attentionStateCode = state.AttentionStateCode;
            revision = state.Revision;
            frozen = isFrozen;
            observationCamera = camera;
            transitionFromLocalPosition = previousBase;
            targetLocalPosition = localPosition;
            transitionStartedAt = unscaledTime;
            interpolating = hasPlacement
                            && PlanarDistance(previousBase, targetLocalPosition) > .001f;
            hasPlacement = true;
            displayedBaseLocalPosition = interpolating
                ? transitionFromLocalPosition : targetLocalPosition;
            transform.localPosition = displayedBaseLocalPosition;
            transform.localRotation = Quaternion.identity;
            baseScale = Vector3.one * Mathf.Lerp(.82f, 1.18f,
                Mathf.Clamp01(state.Progress01));
            transform.localScale = baseScale;
            if (label != null)
            {
                label.text = state.OperatingSystemLabel + "\n"
                             + state.ActivityLabel + "\n"
                             + state.LifecycleStageLabel + " · "
                             + (frozen ? "갱신 보류" : state.AttentionLabel);
            }
            ApplyColor(state);
            ApplyPresentation(unscaledTime);
            return true;
        }

        private void Update() => ApplyPresentation(Time.unscaledTime);

        public void ApplyPresentation(float unscaledTime)
        {
            displayedBaseLocalPosition = EvaluateBaseLocalPosition(unscaledTime);
            var hold = attentionStateCode == OperationalWorldAttentionStateCodes.RecoveryPending;
            var recovered = attentionStateCode == OperationalWorldAttentionStateCodes.Recovered;
            var moving = !hold
                         && attentionStateCode != OperationalWorldAttentionStateCodes.Completed;
            var bob = moving ? Mathf.Sin(unscaledTime * 2.4f) * 1.4f
                : recovered ? Mathf.Abs(Mathf.Sin(unscaledTime * 3.2f)) * 1.8f : 0f;
            transform.localPosition = displayedBaseLocalPosition + Vector3.up * bob;
            var pulse = hold ? 1f + Mathf.Abs(Mathf.Sin(unscaledTime * 4f)) * .18f : 1f;
            transform.localScale = baseScale * pulse;
            if (label != null && observationCamera != null)
                label.transform.rotation = observationCamera.transform.rotation;
        }

        private Vector3 EvaluateBaseLocalPosition(float unscaledTime)
        {
            if (!hasPlacement || !interpolating) return targetLocalPosition;
            if (unscaledTime >= transitionStartedAt
                                + StageAnchorInterpolationSeconds - .0001f)
            {
                interpolating = false;
                return targetLocalPosition;
            }
            var progress = Mathf.Clamp01((unscaledTime - transitionStartedAt)
                                         / StageAnchorInterpolationSeconds);
            var smoothed = progress * progress * (3f - 2f * progress);
            var value = Vector3.LerpUnclamped(transitionFromLocalPosition,
                targetLocalPosition, smoothed);
            return value;
        }

        private static float PlanarDistance(Vector3 from, Vector3 to)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void ApplyColor(다중Os생명주기표현State state)
            => ApplyColor(state.OperatingSystemId, state.AttentionStateCode);

        private void ApplyColor(string os, string attention)
        {
            var osColor = os switch
            {
                OperationalWorldOperatingSystemIds.FoodDelivery => new Color(.96f, .29f, .2f),
                OperationalWorldOperatingSystemIds.DomesticCargoTransport => new Color(.16f, .55f, .98f),
                OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment => new Color(.2f, .78f, .42f),
                OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics => new Color(.75f, .35f, .92f),
                _ => Color.white
            };
            var color = frozen ? new Color(.4f, .4f, .4f)
                : attention == OperationalWorldAttentionStateCodes.RecoveryPending
                    ? new Color(1f, .42f, .12f)
                    : attention == OperationalWorldAttentionStateCodes.Recovered
                        ? Color.Lerp(osColor, new Color(1f, .83f, .18f), .55f)
                        : attention == OperationalWorldAttentionStateCodes.Completed
                            ? Color.Lerp(osColor, new Color(.35f, 1f, .62f), .45f)
                            : osColor;
            foreach (var renderer in renderers)
            {
                if (renderer is MeshRenderer && renderer.GetComponent<TextMesh>() != null)
                    continue;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
            }
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.hideFlags = 다중Os생명주기검증Layer.RuntimeHideFlags;
            value.layer = 사가정운영디오라마View.ObservationLayer;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localRotation = Quaternion.identity;
            value.transform.localScale = localScale;
            var collider = value.GetComponent<Collider>();
            if (collider != null) 다중Os생명주기검증Layer.DestroyRuntimeObject(collider);
            return value;
        }
    }
}
