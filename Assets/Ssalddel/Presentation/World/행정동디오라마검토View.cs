using System;
using System.Collections.Generic;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// canonical SimulationWorldShell 안에서 행정동 후보 도형 하나만 읽기 전용으로 표시한다.
    /// Scene·Prefab을 저장하거나 Simulation 상태를 쓰지 않으며 Collider를 만들지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 행정동디오라마검토View : MonoBehaviour
    {
        public const string CanonicalSceneName = "SimulationWorldShell";
        public const int ObservationLayer = 관찰표현LayerContract.사가정운영관찰Layer;
        private const float SelectionLineWidth = 1.2f;
        private const float Epsilon = .0001f;

        [SerializeField]
        private bool createObservationCamera = true;

        private readonly List<UnityEngine.Object> owned = new();
        private readonly List<UnityEngine.Object> selectionOwned = new();
        private Transform modelRoot;
        private Transform selectionRoot;
        private Material modelMaterial;
        private Bounds modelBounds;
        private bool shown = true;

        public 행정동디오라마검토Snapshot Snapshot { get; private set; }
        public 행정동디오라마검토MeshBuilder.BuildReport BuildReport { get; private set; }
        public 행정동디오라마검토Building SelectedBuilding { get; private set; }
        public Camera ObservationCamera { get; private set; }
        public Transform ModelRoot => modelRoot;
        public string ActiveAdministrativeAreaStableId =>
            Snapshot?.AdministrativeAreaStableId ?? string.Empty;
        public string DiagnosticCode { get; private set; } = "NotLoaded";
        public bool IsVisible => shown && modelRoot != null && modelRoot.gameObject.activeSelf;
        public bool HasSelection => SelectedBuilding != null;

        public bool TryApply(AdministrativeDongDioramaApplyResult source,
            out string diagnosticCode)
            => TryApply(source, createObservationCamera, out diagnosticCode);

        public bool TryApply(AdministrativeDongDioramaApplyResult source,
            bool includeObservationCamera, out string diagnosticCode)
        {
            if (!행정동디오라마검토Projection.TryCreate(source,
                    out var snapshot, out diagnosticCode))
            {
                ClearGenerated();
                Snapshot = null;
                DiagnosticCode = diagnosticCode;
                return false;
            }

            try
            {
                BuildFromSnapshot(snapshot, includeObservationCamera);
                DiagnosticCode = diagnosticCode;
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException
                                              || exception is ArgumentException)
            {
                ClearGenerated();
                Snapshot = null;
                diagnosticCode = string.IsNullOrWhiteSpace(exception.Message)
                    ? "AdministrativeDongReviewPresentationBuildFailed"
                    : exception.Message;
                DiagnosticCode = diagnosticCode;
                return false;
            }
        }

        public void BuildFromSnapshot(행정동디오라마검토Snapshot snapshot,
            bool includeObservationCamera = true)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.ObservationPresentationOnly || snapshot.DistributionApproved
                || snapshot.TraversalReady || snapshot.GameplayReady)
                throw new InvalidOperationException("AdministrativeDongReviewSnapshotAuthorityInvalid");

            ClearGenerated();
            Snapshot = snapshot;
            modelRoot = new GameObject(
                $"{snapshot.DisplayName} · 행정동 도형 검토 · {snapshot.ReadinessCode}")
                .transform;
            modelRoot.SetParent(transform, false);
            modelRoot.gameObject.layer = ObservationLayer;
            owned.Add(modelRoot.gameObject);
            modelMaterial = CreateMaterial();
            BuildReport = 행정동디오라마검토MeshBuilder.Build(snapshot, CreateMesh);
            modelBounds = BuildReport.ModelBounds;

            if (includeObservationCamera)
            {
                BuildObservationCamera();
                BuildObservationLight();
                ShowWhole();
            }
            Show(true);
        }

        public void Show(bool value)
        {
            shown = value;
            if (!value) ClearSelection();
            if (modelRoot != null) modelRoot.gameObject.SetActive(value);
            if (ObservationCamera != null) ObservationCamera.enabled = value;
        }

        public void ShowWhole()
        {
            ClearSelection();
            if (ObservationCamera == null || Snapshot == null) return;
            Frame(modelBounds, 34f, 40f);
        }

        public bool SelectBuilding(string buildingStableId, bool focus = false)
        {
            if (Snapshot == null || !Snapshot.TryGetBuilding(buildingStableId,
                    out var building))
            {
                ClearSelection();
                return false;
            }
            ApplySelection(building, focus);
            return true;
        }

        public bool TrySelectLocalPoint(Vector2 localXZ, bool focus = false)
        {
            if (Snapshot == null || !Snapshot.TrySelectBuildingAt(localXZ.x, localXZ.y,
                    out var building))
            {
                ClearSelection();
                return false;
            }
            ApplySelection(building, focus);
            return true;
        }

        public bool TrySelectBuildingAtScreen(Vector2 screenPosition, bool focus = false)
        {
            if (ObservationCamera == null || modelRoot == null)
            {
                ClearSelection();
                return false;
            }
            return TrySelectWorldRay(ObservationCamera.ScreenPointToRay(screenPosition), focus);
        }

        public bool TrySelectWorldRay(Ray worldRay, bool focus = false)
        {
            if (Snapshot == null || modelRoot == null)
            {
                ClearSelection();
                return false;
            }
            var origin = modelRoot.InverseTransformPoint(worldRay.origin);
            var next = modelRoot.InverseTransformPoint(worldRay.origin + worldRay.direction);
            var direction = next - origin;
            if (!Finite(origin) || !Finite(direction) || direction.sqrMagnitude <= Epsilon * Epsilon)
                throw new ArgumentException("AdministrativeDongReviewSelectionRayInvalid");

            행정동디오라마검토Building best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in Snapshot.Buildings)
            {
                if (!RayIntersectsBounds(origin, direction, candidate)
                    || !RayIntersectsPrism(origin, direction, candidate, out var distance)) continue;
                if (distance < bestDistance - Epsilon
                    || (Mathf.Abs(distance - bestDistance) <= Epsilon
                        && (best == null || candidate.FootprintArea < best.FootprintArea
                            || (Math.Abs(candidate.FootprintArea - best.FootprintArea) <= Epsilon
                                && string.CompareOrdinal(candidate.BuildingStableId,
                                    best.BuildingStableId) < 0))))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                ClearSelection();
                return false;
            }
            ApplySelection(best, focus);
            return true;
        }

        public void ClearSelection()
        {
            SelectedBuilding = null;
            if (selectionRoot != null) selectionRoot.gameObject.SetActive(false);
            for (var index = selectionOwned.Count - 1; index >= 0; index--)
                DestroyOwned(selectionOwned[index]);
            selectionOwned.Clear();
            selectionRoot = null;
        }

        public void Clear()
        {
            ClearGenerated();
            Snapshot = null;
            DiagnosticCode = "NotLoaded";
        }

        public static float WholeViewOrthographicSize(Bounds value, Quaternion rotation,
            float aspect)
        {
            if (!float.IsFinite(aspect) || aspect <= 0f)
                throw new ArgumentOutOfRangeException(nameof(aspect));
            var inverse = Quaternion.Inverse(rotation);
            var maximumHorizontal = 0f;
            var maximumVertical = 0f;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = value.center + Vector3.Scale(value.extents,
                    new Vector3(x, y, z));
                var local = inverse * (corner - value.center);
                maximumHorizontal = Mathf.Max(maximumHorizontal, Mathf.Abs(local.x));
                maximumVertical = Mathf.Max(maximumVertical, Mathf.Abs(local.y));
            }
            return Mathf.Max(maximumVertical, maximumHorizontal / aspect) / .94f;
        }

        private Material CreateMaterial()
        {
            var shader = Resources.Load<Shader>("역세권공간모형")
                         ?? Shader.Find("Ssalddel/StationDiorama")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("AdministrativeDongReviewShaderMissing");
            var material = new Material(shader)
            {
                name = "행정동 도형 검토 · 공통 vertex color",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            owned.Add(material);
            return material;
        }

        private void CreateMesh(행정동디오라마검토MeshBuilder.MeshBatch batch)
        {
            var value = new GameObject(batch.Name) { layer = ObservationLayer };
            value.transform.SetParent(modelRoot, false);
            var mesh = new Mesh
            {
                name = batch.Name,
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(batch.Vertices);
            mesh.SetTriangles(batch.Triangles, 0);
            mesh.SetColors(batch.Colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);
            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = value.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = modelMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void BuildObservationCamera()
        {
            var value = new GameObject("행정동 도형 검토 · 관찰 카메라")
                { layer = ObservationLayer };
            value.transform.SetParent(transform, false);
            owned.Add(value);
            ObservationCamera = value.AddComponent<Camera>();
            ObservationCamera.orthographic = true;
            ObservationCamera.depth = 240f;
            ObservationCamera.nearClipPlane = .3f;
            ObservationCamera.cullingMask = 1 << ObservationLayer;
            ObservationCamera.clearFlags = CameraClearFlags.SolidColor;
            ObservationCamera.backgroundColor = new Color(.39f, .48f, .54f);
            ObservationCamera.allowMSAA = true;
        }

        private void BuildObservationLight()
        {
            var value = new GameObject("행정동 도형 검토 · 관찰 조명")
                { layer = ObservationLayer };
            value.transform.SetParent(transform, false);
            value.transform.localRotation = Quaternion.Euler(46f, -32f, 0f);
            owned.Add(value);
            var light = value.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, .96f, .88f);
            light.cullingMask = 1 << ObservationLayer;
            light.shadows = LightShadows.Soft;
        }

        private void Frame(Bounds bounds, float pitch, float yaw)
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var aspect = Mathf.Max(.1f, ObservationCamera.aspect);
            ObservationCamera.orthographicSize = WholeViewOrthographicSize(
                bounds, rotation, aspect);
            var distance = Mathf.Max(500f, bounds.size.magnitude * 1.35f);
            ObservationCamera.nearClipPlane = Mathf.Max(.1f, distance * .01f);
            ObservationCamera.farClipPlane = distance * 2.5f + bounds.size.magnitude;
            ObservationCamera.transform.SetPositionAndRotation(
                transform.TransformPoint(bounds.center - rotation * Vector3.forward * distance),
                transform.rotation * rotation);
        }

        private void ApplySelection(행정동디오라마검토Building building, bool focus)
        {
            ClearSelection();
            SelectedBuilding = building;
            BuildSelectionOutline(building);
            if (!focus || ObservationCamera == null) return;
            var width = Mathf.Max(8f, (float)(building.MaxX - building.MinX));
            var depth = Mathf.Max(8f, (float)(building.MaxZ - building.MinZ));
            var bounds = new Bounds(
                new Vector3((float)building.CentroidX, (float)building.DisplayedHeight * .5f,
                    (float)building.CentroidZ),
                new Vector3(width * 2.4f, Mathf.Max(12f, (float)building.DisplayedHeight),
                    depth * 2.4f));
            Frame(bounds, 30f, 38f);
        }

        private void BuildSelectionOutline(행정동디오라마검토Building building)
        {
            selectionRoot = new GameObject(
                $"선택 건물 · {building.BuildingStableId}").transform;
            selectionRoot.SetParent(modelRoot, false);
            selectionRoot.gameObject.layer = ObservationLayer;
            selectionOwned.Add(selectionRoot.gameObject);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var y = (float)building.DisplayedHeight + .55f;
            var color = new Color(1f, .84f, .18f);
            for (var index = 0; index < building.Footprint.Count; index++)
            {
                var next = (index + 1) % building.Footprint.Count;
                AddStrip(vertices, triangles, colors,
                    Point(building.Footprint[index], y),
                    Point(building.Footprint[next], y), SelectionLineWidth, color);
            }
            var mesh = new Mesh
            {
                name = $"선택 건물 윤곽 · {building.BuildingStableId}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            selectionOwned.Add(mesh);
            selectionRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = selectionRoot.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = modelMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnGUI()
        {
            if (!shown || ObservationCamera == null || Snapshot == null) return;
            GUI.Box(new Rect(12f, 12f, 520f, 110f), GUIContent.none);
            GUI.Label(new Rect(24f, 20f, 490f, 24f),
                $"{Snapshot.DisplayName} · 행정동 도형 검토");
            GUI.Label(new Rect(24f, 45f, 490f, 68f),
                $"{Snapshot.ReadinessCode} · 읽기 전용 · 배포 불가\n"
                + "역사 경계 후보 · 주소/필지/통행/차선/신호/보도 미검증\n"
                + $"projection {Short(Snapshot.ProjectionHashSha256)}");

            if (BuildReport != null)
            {
                GUI.Box(new Rect(12f, Screen.height - 70f, 650f, 58f), GUIContent.none);
                GUI.Label(new Rect(24f, Screen.height - 62f, 625f, 44f),
                    $"건물 {BuildReport.BuildingCount:N0} · 도로 중심선 "
                    + $"{BuildReport.RoadSegmentCount:N0} · 경계 "
                    + $"{BuildReport.BoundarySegmentCount:N0} · Mesh "
                    + $"{BuildReport.RendererBatchCount:N0}\n"
                    + "표현 사본은 서버·Simulation·업무 상태를 변경하지 않음");
            }

            if (SelectedBuilding == null) return;
            GUI.Box(new Rect(Screen.width - 360f, 12f, 348f, 132f), GUIContent.none);
            GUI.Label(new Rect(Screen.width - 348f, 20f, 324f, 116f),
                $"검토 건물\n{SelectedBuilding.BuildingStableId}\n"
                + $"분류 {ValueOrUnknown(SelectedBuilding.CategoryCode)} · 높이 "
                + $"{SelectedBuilding.DisplayedHeight:0.#}u\n"
                + $"근거 {ValueOrUnknown(SelectedBuilding.EvidenceKindCode)}\n"
                + "주소·업체·H 역할 미결속");
        }

        private void ClearGenerated()
        {
            ClearSelection();
            if (modelRoot != null) modelRoot.gameObject.SetActive(false);
            if (ObservationCamera != null) ObservationCamera.enabled = false;
            ObservationCamera = null;
            BuildReport = null;
            modelRoot = null;
            modelMaterial = null;
            for (var index = owned.Count - 1; index >= 0; index--)
                DestroyOwned(owned[index]);
            owned.Clear();
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is GameObject gameObject) gameObject.SetActive(false);
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static bool RayIntersectsPrism(Vector3 origin, Vector3 direction,
            행정동디오라마검토Building building, out float bestDistance)
        {
            var hitDistance = float.PositiveInfinity;
            if (Mathf.Abs(direction.y) > Epsilon)
            {
                TestHorizontalPlane((float)building.DisplayedHeight + .16f);
                TestHorizontalPlane(.16f);
            }

            var rayDirection = new Vector2(direction.x, direction.z);
            var rayOrigin = new Vector2(origin.x, origin.z);
            for (var index = 0; index < building.Footprint.Count; index++)
            {
                var a = Point2(building.Footprint[index]);
                var b = Point2(building.Footprint[(index + 1) % building.Footprint.Count]);
                var edge = b - a;
                var denominator = Cross(rayDirection, edge);
                if (Mathf.Abs(denominator) <= Epsilon) continue;
                var delta = a - rayOrigin;
                var distance = Cross(delta, edge) / denominator;
                var edgeFactor = Cross(delta, rayDirection) / denominator;
                if (distance < -Epsilon || edgeFactor < -Epsilon
                    || edgeFactor > 1f + Epsilon) continue;
                var y = origin.y + direction.y * distance;
                if (y >= .16f - Epsilon
                    && y <= building.DisplayedHeight + .16f + Epsilon)
                    hitDistance = Mathf.Min(hitDistance, Mathf.Max(0f, distance));
            }
            bestDistance = hitDistance;
            return !float.IsPositiveInfinity(hitDistance);

            void TestHorizontalPlane(float y)
            {
                var distance = (y - origin.y) / direction.y;
                if (distance < -Epsilon) return;
                var point = new Vector2(origin.x + direction.x * distance,
                    origin.z + direction.z * distance);
                if (Contains(building.Footprint, point))
                    hitDistance = Mathf.Min(hitDistance, Mathf.Max(0f, distance));
            }
        }

        private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction,
            행정동디오라마검토Building building)
        {
            var near = 0f;
            var far = float.PositiveInfinity;
            return Axis(origin.x, direction.x, (float)building.MinX,
                       (float)building.MaxX, ref near, ref far)
                   && Axis(origin.y, direction.y, .16f,
                       (float)building.DisplayedHeight + .16f, ref near, ref far)
                   && Axis(origin.z, direction.z, (float)building.MinZ,
                       (float)building.MaxZ, ref near, ref far);
        }

        private static bool Axis(float origin, float direction, float min, float max,
            ref float near, ref float far)
        {
            if (Mathf.Abs(direction) <= Epsilon)
                return origin >= min - Epsilon && origin <= max + Epsilon;
            var first = (min - origin) / direction;
            var second = (max - origin) / direction;
            if (first > second) (first, second) = (second, first);
            near = Mathf.Max(near, first);
            far = Mathf.Min(far, second);
            return far + Epsilon >= near && far >= -Epsilon;
        }

        private static bool Contains(IReadOnlyList<행정동디오라마검토Point> polygon,
            Vector2 point)
        {
            var inside = false;
            for (int current = 0, previous = polygon.Count - 1;
                 current < polygon.Count; previous = current++)
            {
                var a = Point2(polygon[previous]);
                var b = Point2(polygon[current]);
                if (OnSegment(a, b, point)) return true;
                if ((a.y > point.y) == (b.y > point.y)) continue;
                var intersectX = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (point.x < intersectX) inside = !inside;
            }
            return inside;
        }

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 point)
        {
            if (Mathf.Abs(Cross(b - a, point - a)) > Epsilon) return false;
            return point.x >= Mathf.Min(a.x, b.x) - Epsilon
                   && point.x <= Mathf.Max(a.x, b.x) + Epsilon
                   && point.y >= Mathf.Min(a.y, b.y) - Epsilon
                   && point.y <= Mathf.Max(a.y, b.y) + Epsilon;
        }

        private static void AddStrip(List<Vector3> vertices, List<int> triangles,
            List<Color> colors, Vector3 a, Vector3 b, float width, Color color)
        {
            var direction = b - a;
            if (direction.sqrMagnitude <= .000001f) return;
            var side = Vector3.Cross(direction.normalized, Vector3.up) * (width * .5f);
            var first = vertices.Count;
            vertices.Add(a - side);
            vertices.Add(a + side);
            vertices.Add(b + side);
            vertices.Add(b - side);
            for (var index = 0; index < 4; index++) colors.Add(color);
            triangles.AddRange(new[]
            {
                first, first + 1, first + 2,
                first, first + 2, first + 3
            });
        }

        private static Vector3 Point(행정동디오라마검토Point value, float y)
            => new((float)value.X, y, (float)value.Z);

        private static Vector2 Point2(행정동디오라마검토Point value)
            => new((float)value.X, (float)value.Z);

        private static float Cross(Vector2 left, Vector2 right)
            => left.x * right.y - left.y * right.x;

        private static bool Finite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static string ValueOrUnknown(string value)
            => string.IsNullOrWhiteSpace(value) ? "미확정" : value;

        private static string Short(string value)
            => string.IsNullOrEmpty(value) ? "none"
                : value.Length <= 12 ? value : value.Substring(0, 12);

        private void OnDestroy() => Clear();
    }
}
