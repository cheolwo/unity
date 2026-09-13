using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// canonical SimulationWorldShell 안에서 역세권 공간 snapshot을 관찰용 모형으로만 표시한다.
    /// 로컬 파일 경로나 원자료는 직렬화하지 않고, 통행·게임플레이 판정을 만들지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 역세권공간표현View : MonoBehaviour
    {
        public const string CanonicalSceneName = "SimulationWorldShell";

        [SerializeField]
        private string transitStationStableId = StationDioramaProfileCatalog.MyeonmokStationStableId;

        [SerializeField]
        private bool createObservationCamera = true;

        private readonly List<UnityEngine.Object> owned = new();
        private Transform modelRoot;
        private Material modelMaterial;
        private Bounds modelBounds;
        private bool shown = true;
        private 단일PlayerViewController singlePlayerViewController;
        private Transform registeredRuntimeEnvironmentRoot;

        public string TransitStationStableId => transitStationStableId;
        public bool SnapshotApplied => Composition?.SnapshotApplied == true;
        public string DataStatus => Composition?.DataStatus ?? "NotLoaded";
        public string CoverageStatus => Composition?.CoverageStatus ?? "NotLoaded";
        public string DiagnosticCode => Composition?.DiagnosticCode ?? "NotLoaded";
        public string SourceRevision => Composition?.Revision ?? string.Empty;
        public string SourceContentHash => Composition?.ContentHash ?? string.Empty;
        public StationDioramaSpatialComposition Composition { get; private set; }
        public 역세권공간표현MeshBuilder.BuildReport BuildReport { get; private set; }
        public Camera ObservationCamera { get; private set; }
        public Transform ModelRoot => modelRoot;
        public bool IsVisible => shown && modelRoot != null && modelRoot.gameObject.activeSelf;
        public string RuntimeEnvironmentDiagnostic { get; private set; } =
            "SinglePlayerViewNotPresent";

        private void Awake()
        {
            if (UnityEngine.Application.isPlaying) Initialize();
        }

        /// <summary>명시 경로가 없거나 자료가 거부돼도 중립 모형만 만들며 다른 역으로 대체하지 않는다.</summary>
        public void Initialize()
        {
            if (Composition != null) return;
            if (!StationDioramaProfileCatalog.TryResolve(transitStationStableId,
                    out var descriptor, out var presentation))
            {
                throw new InvalidOperationException("StationDioramaProfileNotFound");
            }

            var composition = StationDioramaSpatialPipeline.LoadLocalPrivateReviewForEditor(
                descriptor, presentation);
            BuildFromComposition(composition, createObservationCamera);
        }

        /// <summary>EditMode 및 일회성 관찰 실행에서 검증된 composition을 직접 표현한다.</summary>
        public void BuildFromComposition(StationDioramaSpatialComposition composition,
            bool includeObservationCamera = true)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            ClearGenerated();
            Composition = composition;
            transitStationStableId = composition.Descriptor.TransitStationStableId;

            modelRoot = new GameObject(
                $"{composition.Descriptor.DisplayName} · 역세권 공간 표현 · {composition.CoverageStatus}")
                .transform;
            modelRoot.SetParent(transform, false);
            modelRoot.gameObject.layer = composition.PresentationProfile.ObservationLayer;
            owned.Add(modelRoot.gameObject);

            modelMaterial = CreateMaterial(composition.PresentationProfile);
            BuildReport = 역세권공간표현MeshBuilder.Build(composition, CreateMesh);
            modelBounds = new Bounds(
                new Vector3((float)composition.Descriptor.OffsetX, 40f,
                    (float)composition.Descriptor.OffsetZ),
                new Vector3((float)(composition.Descriptor.HalfExtentMeters * 2d),
                    160f, (float)(composition.Descriptor.HalfExtentMeters * 2d)));
            RegisterRuntimeEnvironment();

            if (includeObservationCamera)
            {
                BuildObservationCamera(composition.PresentationProfile);
                BuildObservationLight(composition.PresentationProfile);
                ShowWhole();
            }
            Show(true);
        }

        public void Show(bool value)
        {
            shown = value;
            if (modelRoot != null) modelRoot.gameObject.SetActive(value);
            if (ObservationCamera != null) ObservationCamera.enabled = value;
        }

        public void ShowWhole()
        {
            if (ObservationCamera == null || Composition == null) return;
            var profile = Composition.PresentationProfile;
            var localRotation = Quaternion.Euler(profile.WholeViewPitch, profile.WholeViewYaw, 0f);
            var aspect = Mathf.Max(.1f, ObservationCamera.aspect);
            ObservationCamera.orthographicSize = WholeViewOrthographicSize(
                modelBounds, localRotation, aspect);
            var focus = modelBounds.center;
            ObservationCamera.transform.SetPositionAndRotation(
                transform.TransformPoint(focus - localRotation * Vector3.forward * 1800f),
                transform.rotation * localRotation);
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

        private Material CreateMaterial(StationDioramaPresentationProfile profile)
        {
            var shader = Resources.Load<Shader>("역세권공간모형")
                         ?? Shader.Find(profile.ShaderName)
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("StationDioramaShaderMissing");
            var material = new Material(shader)
            {
                name = "역세권 공간 표현 · 공통 vertex color",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            owned.Add(material);
            return material;
        }

        private void CreateMesh(역세권공간표현MeshBuilder.MeshBatch batch)
        {
            var value = new GameObject(batch.Name)
            {
                layer = Composition.PresentationProfile.ObservationLayer
            };
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

        private void BuildObservationCamera(StationDioramaPresentationProfile profile)
        {
            var value = new GameObject("역세권 공간 표현 · 관찰 카메라")
            {
                layer = profile.ObservationLayer
            };
            value.transform.SetParent(transform, false);
            owned.Add(value);
            ObservationCamera = value.AddComponent<Camera>();
            ObservationCamera.orthographic = true;
            ObservationCamera.depth = 210f;
            ObservationCamera.nearClipPlane = 10f;
            ObservationCamera.farClipPlane = 3000f;
            ObservationCamera.cullingMask = 1 << profile.ObservationLayer;
            ObservationCamera.clearFlags = CameraClearFlags.SolidColor;
            ObservationCamera.backgroundColor = new Color(.39f, .48f, .54f);
            ObservationCamera.allowMSAA = true;
        }

        private void BuildObservationLight(StationDioramaPresentationProfile profile)
        {
            var value = new GameObject("역세권 공간 표현 · 관찰 조명")
            {
                layer = profile.ObservationLayer
            };
            value.transform.SetParent(transform, false);
            value.transform.localRotation = Quaternion.Euler(46f, -32f, 0f);
            owned.Add(value);
            var light = value.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, .96f, .88f);
            light.cullingMask = 1 << profile.ObservationLayer;
            light.shadows = LightShadows.Soft;
        }

        private void OnGUI()
        {
            if (!shown || ObservationCamera == null || Composition == null) return;
            var descriptor = Composition.Descriptor;
            GUI.Box(new Rect(12f, 12f, 410f, 92f), GUIContent.none);
            GUI.Label(new Rect(24f, 20f, 380f, 24f),
                $"{descriptor.DisplayName} 1km 공간 표현");
            GUI.Label(new Rect(24f, 45f, 380f, 48f), SnapshotApplied
                ? $"{DataStatus} · {CoverageStatus}\n표현 전용 · 통행/게임플레이 권위 없음"
                : $"NoData · MissingCoverage\nsource 도형 0건 · 다른 역 대체 없음");

            if (BuildReport == null) return;
            GUI.Box(new Rect(12f, Screen.height - 70f, 610f, 58f), GUIContent.none);
            GUI.Label(new Rect(24f, Screen.height - 62f, 585f, 44f),
                $"건물 {BuildReport.BuildingCount:N0} · 도로 {BuildReport.RoadCount:N0} · "
                + $"표면 {BuildReport.SurfaceCount:N0} · MissingCoverage "
                + $"{BuildReport.MissingCoverageCellCount:N0}\n"
                + $"snapshot {Short(SourceContentHash)} · {DiagnosticCode}");
        }

        private static string Short(string value)
            => string.IsNullOrEmpty(value) ? "none"
                : value.Length <= 12 ? value : value.Substring(0, 12);

        private void ClearGenerated()
        {
            UnregisterRuntimeEnvironment();
            ObservationCamera = null;
            BuildReport = null;
            modelRoot = null;
            modelMaterial = null;
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                var value = owned[index];
                if (value == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(value);
                else DestroyImmediate(value);
            }
            owned.Clear();
        }

        private void RegisterRuntimeEnvironment()
        {
            RuntimeEnvironmentDiagnostic = "SinglePlayerViewNotPresent";
            if (modelRoot == null) return;

            var controller = GetComponentInParent<단일PlayerViewController>(true);
            if (controller == null) return;
            if (!controller.TryRegisterRuntimeEnvironment(modelRoot, this,
                    out var diagnostic))
            {
                RuntimeEnvironmentDiagnostic = diagnostic;
                return;
            }

            singlePlayerViewController = controller;
            registeredRuntimeEnvironmentRoot = modelRoot;
            RuntimeEnvironmentDiagnostic = diagnostic;
        }

        private void UnregisterRuntimeEnvironment()
        {
            if (singlePlayerViewController != null
                && registeredRuntimeEnvironmentRoot != null)
            {
                singlePlayerViewController.UnregisterRuntimeEnvironment(
                    registeredRuntimeEnvironmentRoot, this);
            }
            singlePlayerViewController = null;
            registeredRuntimeEnvironmentRoot = null;
        }

        private void OnDestroy() => ClearGenerated();
    }
}
