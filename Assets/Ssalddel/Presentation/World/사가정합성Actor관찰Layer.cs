using System;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 검증된 합성 기사 사본 하나를 사가정 관찰용 표시와 카드로 그린다.
    /// 토글·선택·카드는 Simulation Command나 정산 쓰기를 제공하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 사가정합성Actor관찰Layer : MonoBehaviour
    {
        public const string RuntimeRootName = "SagajeongSyntheticActorObservationRuntime";
        public const string Disclosure = "합성 Simulation 자료 · 실제 기사·위치·정산 아님";
        public const string ReadyStatus = "SagajeongSyntheticActorObservationReady";
        public const string HiddenStatus = "SagajeongSyntheticActorObservationHidden";
        public const string WaitingForCoordinateBindingStatus =
            "WaitingForSyntheticActorCoordinateBinding";
        public const string WaitingForSnapshotStatus =
            "WaitingForSyntheticActorObservation";
        public const string ShaderMissingStatus =
            "SagajeongSyntheticActorObservationShaderMissing";
        public const float SelectionRadiusPixels = 42f;

        private readonly 사가정합성Actor관찰Interpreter interpreter = new();
        private Camera observationCamera;
        private Transform runtimeRoot;
        private Transform actorMarker;
        private Material markerMaterial;
        private Mesh markerMesh;
        private Vector3 coordinateOrigin;
        private float coordinateScale = 1f;
        private bool coordinateBindingConfigured;
        private bool hostVisible = true;
        private bool displayVisible = true;
        private bool blocked;
        private string selectedActorStableId = string.Empty;
        private string statusCode = WaitingForCoordinateBindingStatus;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle noteStyle;

        public Camera ObservationCamera => observationCamera;
        public bool HostVisible => hostVisible;
        public bool DisplayVisible => displayVisible;
        public bool EffectiveVisible => hostVisible && displayVisible;
        public bool IsConfigured => coordinateBindingConfigured
                                    && observationCamera != null;
        public bool HasAcceptedObservation => !blocked && IsConfigured
                                              && interpreter.Current != null;
        public bool IsSelected => !string.IsNullOrEmpty(selectedActorStableId);
        public string SelectedActorStableId => selectedActorStableId;
        public string StatusCode => statusCode;
        public long AppliedRevision => interpreter.Current?.SourceSessionRevision ?? -1;
        public string AppliedFingerprint => interpreter.Current?.ProjectionFingerprintSha256
                                            ?? string.Empty;
        public int RuntimeVisualCount => actorMarker != null ? 1 : 0;
        public 사가정합성기사관찰Snapshot Current => interpreter.Current;

        public void Configure(Camera camera, Vector3 localCoordinateOrigin,
            float localCoordinateScale = 1f)
        {
            observationCamera = camera != null
                ? camera : throw new ArgumentNullException(nameof(camera));
            if (!Finite(localCoordinateOrigin) || !Finite(localCoordinateScale)
                || localCoordinateScale <= 0f)
                throw new ArgumentException("SagajeongSyntheticActorCoordinateBindingInvalid");
            coordinateOrigin = localCoordinateOrigin;
            coordinateScale = localCoordinateScale;
            coordinateBindingConfigured = true;
            var current = interpreter.Current;
            if (current != null && EnsureVisual()) ApplyPosition(current);
            UpdateStatusCode();
            RefreshVisibility();
        }

        public 사가정합성Actor관찰ApplyResult Apply(
            사가정합성기사관찰Snapshot snapshot)
        {
            var result = interpreter.Apply(snapshot);
            if (!result.Accepted)
            {
                blocked = true;
                statusCode = result.ErrorCode;
                RefreshVisibility();
                return result;
            }

            blocked = false;
            if (IsConfigured && EnsureVisual()) ApplyPosition(result.Snapshot);
            UpdateStatusCode();
            RefreshVisibility();
            return result;
        }

        /// <summary>
        /// 역세권 모듈 Host의 활성 역 전환을 반영한다. 사용자가 고른 로컬 표시
        /// 설정은 바꾸지 않으므로 다시 활성화해도 이전 토글 값이 유지된다.
        /// </summary>
        public void SetHostVisible(bool value)
        {
            hostVisible = value;
            UpdateStatusCode();
            RefreshVisibility();
        }

        public void SetDisplayVisible(bool value)
        {
            displayVisible = value;
            UpdateStatusCode();
            RefreshVisibility();
        }

        public bool SelectCurrentActor()
        {
            var current = interpreter.Current;
            if (!EffectiveVisible || !HasAcceptedObservation
                || actorMarker == null || current == null)
                return false;
            selectedActorStableId = current.ActorStableId;
            return true;
        }

        public bool TrySelectActorAtScreen(Vector2 screenPosition,
            bool pointerOverUserInterface = false)
        {
            if (pointerOverUserInterface || !EffectiveVisible || blocked
                || !IsConfigured
                || observationCamera == null || actorMarker == null
                || interpreter.Current == null
                || !observationCamera.pixelRect.Contains(screenPosition))
                return false;
            var projected = observationCamera.WorldToScreenPoint(actorMarker.position);
            if (projected.z <= 0f) return false;
            var delta = new Vector2(projected.x, projected.y) - screenPosition;
            if (delta.sqrMagnitude > SelectionRadiusPixels * SelectionRadiusPixels)
                return false;
            selectedActorStableId = interpreter.Current.ActorStableId;
            return true;
        }

        public void CloseCard() => selectedActorStableId = string.Empty;

        public void Clear()
        {
            interpreter.Clear();
            selectedActorStableId = string.Empty;
            blocked = false;
            UpdateStatusCode();
            DestroyRuntimeObjects();
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;
            var pointerOverUi = EventSystem.current != null
                                && EventSystem.current.IsPointerOverGameObject();
            TrySelectActorAtScreen(Mouse.current.position.ReadValue(), pointerOverUi);
        }

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying || !hostVisible) return;
            EnsureStyles();
            GUI.depth = -170;
            var toggleWidth = 176f;
            var toggleX = Mathf.Max(12f, Screen.width - toggleWidth - 12f);
            const float toggleY = 96f;
            if (GUI.Button(new Rect(toggleX, toggleY, toggleWidth, 32f),
                    displayVisible ? "업무 Actor 표시 끄기" : "업무 Actor 표시 켜기"))
                SetDisplayVisible(!displayVisible);

            if (!displayVisible)
            {
                GUI.Label(new Rect(toggleX - 90f, toggleY + 36f,
                        toggleWidth + 90f, 24f),
                    "표시만 꺼짐 · Simulation은 계속", noteStyle);
                return;
            }
            var value = interpreter.Current;
            if (!HasAcceptedObservation || value == null
                || selectedActorStableId != value.ActorStableId)
                return;

            var width = Mathf.Min(390f, Screen.width - 24f);
            var height = 344f;
            var x = Mathf.Max(12f, Screen.width - width - 12f);
            const float y = 136f;
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.Label(new Rect(x + 16f, y + 12f, width - 68f, 28f),
                value.RoleDisplayName + " · " + value.DisplayAlias, titleStyle);
            if (GUI.Button(new Rect(x + width - 46f, y + 10f, 32f, 28f), "×"))
            {
                CloseCard();
                return;
            }

            var progress = value.RouteProgressRatio.HasValue
                ? $"{value.RouteProgressRatio.Value * 100d:0}%"
                : "해당 없음";
            var failure = value.FailedDeliveryCount.HasValue
                ? value.FailedDeliveryCount.Value.ToString()
                : "미추적";
            var recovery = value.RecoveredDeliveryCount.HasValue
                ? value.RecoveredDeliveryCount.Value.ToString()
                : "미추적";
            var order = string.IsNullOrEmpty(value.CurrentOrderStableId)
                ? "현재 주문 없음"
                : value.CurrentOrderStateCode;
            var lines =
                $"현재: {value.CurrentActionDisplayName}\n" +
                $"업무 단계: {value.CurrentStageDisplayName} · {order}\n" +
                $"이동 진행: {progress} · {value.RouteProgressMeters:0.0}/{value.RouteLengthMeters:0.0}m\n" +
                $"이번 시나리오: 완료 {value.CompletedDeliveryCount}건 · 진행 {value.InProgressDeliveryCount}건\n" +
                $"실패 {failure} · 회복 {recovery}\n" +
                $"모의 예상 지급: 계산식 미승인\n" +
                $"Tick {value.CurrentTick}/{value.DurationTicks} · Session r{value.SourceSessionRevision}";
            GUI.Label(new Rect(x + 16f, y + 50f, width - 32f, 188f), lines, bodyStyle);
            GUI.Label(new Rect(x + 16f, y + 244f, width - 32f, 70f),
                Disclosure + "\n"
                + value.SyntheticCompensation.BlockReasonCode + "\n"
                + "선택·토글은 업무·Simulation revision을 변경하지 않음",
                noteStyle);
            if (GUI.Button(new Rect(x + 16f, y + height - 42f, width - 32f, 30f),
                    "카드 닫기 · 전체 보기"))
                CloseCard();
        }

        private bool EnsureVisual()
        {
            if (runtimeRoot != null && actorMarker != null) return true;
            var shader = FindShader();
            if (shader == null)
            {
                blocked = true;
                statusCode = ShaderMissingStatus;
                return false;
            }
            var root = new GameObject(RuntimeRootName)
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            root.transform.SetParent(transform, false);
            runtimeRoot = root.transform;

            var marker = new GameObject("합성 배달 기사 관찰 표시")
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            marker.transform.SetParent(runtimeRoot, false);
            actorMarker = marker.transform;
            markerMesh = CreateMarkerMesh();
            marker.AddComponent<MeshFilter>().sharedMesh = markerMesh;
            markerMaterial = new Material(shader)
            {
                name = "사가정 합성 기사 관찰 주황",
                color = new Color(.98f, .34f, .08f, 1f)
            };
            marker.AddComponent<MeshRenderer>().sharedMaterial = markerMaterial;
            return true;
        }

        private void ApplyPosition(사가정합성기사관찰Snapshot value)
        {
            if (actorMarker == null || value == null) return;
            actorMarker.localPosition = coordinateOrigin + new Vector3(
                (float)value.ActorX * coordinateScale,
                2.2f,
                (float)value.ActorZ * coordinateScale);
            actorMarker.localRotation = Quaternion.identity;
            actorMarker.localScale = Vector3.one;
        }

        private void RefreshVisibility()
        {
            if (runtimeRoot != null)
                runtimeRoot.gameObject.SetActive(EffectiveVisible
                                                  && HasAcceptedObservation);
        }

        private void UpdateStatusCode()
        {
            if (blocked) return;
            if (!IsConfigured)
            {
                statusCode = WaitingForCoordinateBindingStatus;
                return;
            }
            if (interpreter.Current == null)
            {
                statusCode = WaitingForSnapshotStatus;
                return;
            }
            statusCode = EffectiveVisible ? ReadyStatus : HiddenStatus;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(.95f, .95f, .95f) }
            };
            noteStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(1f, .82f, .44f) }
            };
        }

        private static Mesh CreateMarkerMesh()
        {
            var mesh = new Mesh { name = "합성 기사 저폴리 표시" };
            mesh.vertices = new[]
            {
                new Vector3(-.65f, 0f, -.45f), new Vector3(.65f, 0f, -.45f),
                new Vector3(.65f, 0f, .45f), new Vector3(-.65f, 0f, .45f),
                new Vector3(-.42f, 2.2f, -.32f), new Vector3(.42f, 2.2f, -.32f),
                new Vector3(.42f, 2.2f, .32f), new Vector3(-.42f, 2.2f, .32f),
                new Vector3(0f, 3.1f, 0f)
            };
            mesh.triangles = new[]
            {
                0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7,
                0, 3, 2, 0, 2, 1, 4, 5, 8, 5, 6, 8,
                6, 7, 8, 7, 4, 8
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Shader FindShader()
            => Resources.Load<Shader>("사가정모형명암")
               ?? Shader.Find("Universal Render Pipeline/Unlit")
               ?? Shader.Find("Unlit/Color");

        private void DestroyRuntimeObjects()
        {
            var root = runtimeRoot;
            runtimeRoot = null;
            actorMarker = null;
            DestroyObject(root != null ? root.gameObject : null);
            DestroyObject(markerMaterial);
            DestroyObject(markerMesh);
            markerMaterial = null;
            markerMesh = null;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDestroy() => DestroyRuntimeObjects();

        private static bool Finite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Finite(Vector3 value)
            => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
