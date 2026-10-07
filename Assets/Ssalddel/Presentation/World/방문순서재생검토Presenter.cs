using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>이미 읽힌 완전 지도의 비공개 방문 순서 표현. 업무·주행·위치 확정 권위가 없다.</summary>
    [DisallowMultipleComponent]
    public sealed class 방문순서재생검토Presenter : MonoBehaviour
    {
        private readonly 방문순서재생Session session = new 방문순서재생Session();
        private readonly List<Object> focusResources = new List<Object>();
        private readonly List<Object> markerResources = new List<Object>();
        private 방문재생공간검토 context;
        private 방문재생준비Result prepared;
        private Camera framedCamera;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private float cameraSize, cameraNear, cameraFar;
        private Vector3 appliedCameraPosition;
        private Quaternion appliedCameraRotation;
        private float appliedCameraSize, appliedCameraNear, appliedCameraFar;

        public 행정동디오라마검토View BoundView { get; private set; }
        public Transform FocusRoot { get; private set; }
        public Transform MarkerTransform { get; private set; }
        public int MarkerCount => MarkerTransform != null ? 1 : 0;
        public 방문재생Frame CurrentFrame => session.CurrentFrame;
        public string DiagnosticCode { get; private set; } = string.Empty;
        public static double ClockSeconds => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        public bool TryShowFocus(행정동디오라마검토View view, 방문재생공간검토 value)
        {
            if (view == null || value == null || view.ModelRoot == null
                || !ReferenceEquals(view.Snapshot, value.Snapshot)) return Reject("VisitReviewLoadedMapRequired");
            if (prepared != null)
            {
                var issue = value.ValidatePrepared(prepared);
                if (issue.Length != 0) return Reject(issue);
            }
            // 준비된 표현을 만든 다음에만 직전 정상 표현을 교체한다. 원본 View는 건드리지 않는다.
            var pending = new List<Object>();
            GameObject candidate = null;
            try
            {
                candidate = NewObject("방문 검토 · 선택 범위", view.ModelRoot);
                candidate.SetActive(false);
                var vertices = new List<Vector3>();
                foreach (var road in value.SelectedRoads)
                { vertices.Add(Point(road.From)); vertices.Add(Point(road.To)); }
                foreach (var building in value.SelectedBuildings)
                    for (var i = 0; i < building.Footprint.Count; i++)
                    { vertices.Add(Point(building.Footprint[i])); vertices.Add(Point(building.Footprint[(i + 1) % building.Footprint.Count])); }
                var min = value.ToLocal(value.MinX, value.MinZ); var max = value.ToLocal(value.MaxX, value.MaxZ);
                var corners = new[] { new Vector3((float)min.X, .4f, (float)min.Z), new Vector3((float)max.X, .4f, (float)min.Z),
                    new Vector3((float)max.X, .4f, (float)max.Z), new Vector3((float)min.X, .4f, (float)max.Z) };
                for (var i = 0; i < 4; i++) { vertices.Add(corners[i]); vertices.Add(corners[(i + 1) % 4]); }
                var mesh = new Mesh { name = "방문 검토 원본 도형 강조", hideFlags = HideFlags.DontSave };
                pending.Add(mesh);
                mesh.SetVertices(vertices);
                var indices = new int[vertices.Count]; for (var i = 0; i < indices.Length; i++) indices[i] = i;
                mesh.SetIndices(indices, MeshTopology.Lines, 0); mesh.RecalculateBounds();
                Attach(candidate, mesh, new Color(.1f, .8f, .9f), pending);
                if (MarkerTransform != null) MarkerTransform.SetParent(candidate.transform, false);
                DestroyOwned(FocusRoot, focusResources);
                FocusRoot = candidate.transform; focusResources.AddRange(pending);
                context = value; BoundView = view; candidate.SetActive(true);
                FrameCamera(min, max); RenderFrame(); DiagnosticCode = string.Empty;
                return true;
            }
            catch (Exception)
            {
                if (candidate != null && candidate.transform != FocusRoot) DestroyOwned(candidate.transform, pending);
                return Reject("VisitReviewPresentationPreparationFailed");
            }
        }

        public bool TryLoad(방문재생준비Result value, double clock)
        {
            if (!MapAvailable()) return Reject("VisitReviewLoadedMapRequired");
            var issue = context.ValidatePrepared(value);
            if (issue.Length != 0) return Reject(issue);
            GameObject candidate = null;
            var pending = new List<Object>();
            try
            {
                if (MarkerTransform == null)
                {
                    candidate = NewObject("방문 순서 검토 표시 · 실제 주행 아님", FocusRoot);
                    candidate.SetActive(false);
                    var mesh = new Mesh { name = "방문 순서 검토 표시", hideFlags = HideFlags.DontSave };
                    pending.Add(mesh);
                    mesh.vertices = new[] { new Vector3(0, 5, 0), new Vector3(-3, 0, -3), new Vector3(3, 0, -3),
                        new Vector3(3, 0, 3), new Vector3(-3, 0, 3), new Vector3(0, -2, 0) };
                    mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    Attach(candidate, mesh, new Color(1f, .55f, .12f), pending);
                }
                if (!session.Load(value, clock))
                {
                    if (candidate != null) DestroyOwned(candidate.transform, pending);
                    return Reject(session.DiagnosticCode);
                }
                prepared = value;
                if (candidate != null) { MarkerTransform = candidate.transform; markerResources.AddRange(pending); candidate.SetActive(true); }
                RenderFrame(); DiagnosticCode = string.Empty; return true;
            }
            catch (Exception)
            {
                if (candidate != null && candidate.transform != MarkerTransform) DestroyOwned(candidate.transform, pending);
                return Reject("VisitReviewPresentationPreparationFailed");
            }
        }

        public bool Play(double clock) => Apply(() => session.Play(clock));
        public bool Pause(double clock) => Apply(() => session.Pause(clock));
        public bool SeekVisit(string visitId, double clock) => Apply(() => session.SeekVisit(visitId, clock));
        public bool Advance(double clock) => Apply(() => session.Advance(clock));

        private bool Apply(Func<bool> action)
        {
            if (!MapAvailable()) return Reject("VisitReviewLoadedMapRequired");
            if (!action()) return Reject(session.DiagnosticCode);
            RenderFrame(); DiagnosticCode = string.Empty; return true;
        }

        private bool MapAvailable() => context != null && BoundView != null && FocusRoot != null
            && FocusRoot.parent == BoundView.ModelRoot && ReferenceEquals(context.Snapshot, BoundView.Snapshot);

        private void RenderFrame()
        {
            if (MarkerTransform == null) return;
            var point = context.ToLocal(CurrentFrame.X, CurrentFrame.Z);
            MarkerTransform.localPosition = new Vector3((float)point.X, 3f, (float)point.Z);
        }

        private void FrameCamera(방문검토좌표 min, 방문검토좌표 max)
        {
            var camera = BoundView.ObservationCamera;
            if (camera == null) return;
            if (framedCamera != camera || !CameraStillOwned())
            {
                RestoreCamera(); framedCamera = camera; cameraPosition = camera.transform.position;
                cameraRotation = camera.transform.rotation; cameraSize = camera.orthographicSize;
                cameraNear = camera.nearClipPlane; cameraFar = camera.farClipPlane;
            }
            var center = BoundView.ModelRoot.TransformPoint(new Vector3((float)((min.X + max.X) / 2), 0, (float)((min.Z + max.Z) / 2)));
            var size = new Vector3((float)(max.X - min.X), 45, (float)(max.Z - min.Z));
            camera.orthographicSize = 행정동디오라마검토View.WholeViewOrthographicSize(new Bounds(center, size), camera.transform.rotation, Mathf.Max(.1f, camera.aspect));
            camera.transform.position = center - camera.transform.forward * 600;
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            appliedCameraPosition = camera.transform.position; appliedCameraRotation = camera.transform.rotation;
            appliedCameraSize = camera.orthographicSize; appliedCameraNear = camera.nearClipPlane; appliedCameraFar = camera.farClipPlane;
        }

        public void Clear()
        {
            DestroyOwned(MarkerTransform, markerResources); MarkerTransform = null;
            DestroyOwned(FocusRoot, focusResources); FocusRoot = null;
            RestoreCamera(); session.Clear(); prepared = null; context = null; BoundView = null;
            DiagnosticCode = string.Empty;
        }

        private void RestoreCamera()
        {
            // 검토 이후 사용자가 바꾼 카메라까지 이전 상태로 되돌리지 않는다.
            if (CameraStillOwned())
            {
                framedCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                framedCamera.orthographicSize = cameraSize; framedCamera.nearClipPlane = cameraNear; framedCamera.farClipPlane = cameraFar;
            }
            framedCamera = null;
        }
        private bool CameraStillOwned() => framedCamera != null
            && framedCamera.transform.position.Equals(appliedCameraPosition)
            && framedCamera.transform.rotation.Equals(appliedCameraRotation)
            && framedCamera.orthographicSize.Equals(appliedCameraSize)
            && framedCamera.nearClipPlane.Equals(appliedCameraNear) && framedCamera.farClipPlane.Equals(appliedCameraFar);
        private void Update() { if (UnityEngine.Application.isPlaying && CurrentFrame.State == 방문재생상태.Playing) Advance(ClockSeconds); }
        private void OnDestroy() => Clear();
        private bool Reject(string code) { DiagnosticCode = code; return false; }
        private static Vector3 Point(행정동디오라마검토Point p) => new Vector3((float)p.X, .4f, (float)p.Z);
        private static GameObject NewObject(string name, Transform parent)
        {
            var value = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = 행정동디오라마검토View.ObservationLayer };
            value.transform.SetParent(parent, false); return value;
        }
        private static void Attach(GameObject value, Mesh mesh, Color color, List<Object> resources)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("VisitReviewShaderUnavailable");
            var material = new Material(shader) { name = "방문 검토 전용", hideFlags = HideFlags.DontSave };
            resources.Add(material); material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            value.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static void DestroyOwned(Transform root, List<Object> resources)
        {
            if (root != null) DestroyValue(root.gameObject);
            foreach (var value in resources) if (value != null) DestroyValue(value);
            resources.Clear();
        }
        private static void DestroyValue(Object value) { if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
