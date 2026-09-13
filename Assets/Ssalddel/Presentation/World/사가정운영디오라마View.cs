using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>지역 배경과 관찰 카메라. 지리 자료는 동결 자산이며 업무 사본은 이 객체에 저장하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class 사가정운영디오라마View : MonoBehaviour
    {
        public const int ObservationLayer = 관찰표현LayerContract.사가정운영관찰Layer;
        public const float WholeViewPitch = 34f;
        public const float WholeViewYaw = 40f;
        public const float WholeMapWidthRatio = .94f;
        public const float PostExposure = .15f;
        public const float PostContrast = 12f;
        public const float PostSaturation = 8f;
        public const float PostVignette = .12f;
        [SerializeField] private bool useSyntyPresentation;
        [SerializeField] private bool showDecorativeGreenery = true;
        public bool UsesSyntyPresentation => useSyntyPresentation;
        public static bool IsVisible { get; private set; }
        public string MapStatus { get; private set; } = "NotLoaded";
        public string HeightStatus { get; private set; } = "NotLoaded";
        public string RoadNameStatus { get; private set; } = "NotLoaded";
        public string SyntyStatus { get; private set; } = "NotLoaded";
        public string VisualTargetRevision => MapStatus == "NotLoaded" ? "NotLoaded"
            : useSyntyPresentation ? syntyPlan?.Revision ?? "NotLoaded" : 사가정모형MeshBuilder.Revision;
        public int SyntyPlacementCount => syntyPlan?.AcceptedCount ?? 0;
        public int SyntyFallbackCount => syntyPlan?.FallbackCount ?? 0;
        public IReadOnlyList<사가정Synty표현Placement> SyntyPlacements
            => syntyPlan?.Placements ?? Array.Empty<사가정Synty표현Placement>();
        public Transform SyntyVisualRoot { get; private set; }
        public Dictionary<string, 운영관찰배치지점> Places { get; private set; }
        public Camera ObservationCamera { get; private set; }
        public Volume PresentationVolume { get; private set; }
        public 사가정모형MeshBuilder.BuildReport MiniatureBuildReport { get; private set; }
        public bool StationDetailsVisible => stationDetailRoot != null && stationDetailRoot.gameObject.activeSelf;
        public bool SpatialOverlayApplied => !useSyntyPresentation
            && spatialComposition?.OverlayApplied == true;
        public string SpatialPresentationRevision => useSyntyPresentation
            ? 사가정공간표현OverlayComposer.BaseMapRevision
            : spatialComposition?.Revision ?? "NotLoaded";
        public string SpatialPresentationContentHash => useSyntyPresentation
            ? 사가정공간표현OverlayComposer.BaseMapHash
            : spatialComposition?.ContentHash ?? "NotLoaded";
        public string SpatialPresentationStatus => useSyntyPresentation
            ? "SagajeongReferenceR3Base · SpatialOverlayNotRenderedInSyntyMode"
            : spatialComposition == null ? "NotLoaded"
            : $"{spatialComposition.SourceStatus} · {spatialComposition.DiagnosticCode}";
        public string SpatialPresentationAttribution { get; private set; } = "NotLoaded";
        private readonly List<UnityEngine.Object> owned = new();
        private readonly Dictionary<string, Material> materialCache = new(StringComparer.Ordinal);
        private readonly Transform[] miniatureChunks = new Transform[
            사가정모형MeshBuilder.ChunkAxisCount * 사가정모형MeshBuilder.ChunkAxisCount];
        private 공간참고자료 map;
        private Transform geography;
        private Transform miniatureChunkRoot;
        private Transform stationDetailRoot;
        private Bounds bounds;
        private Vector3 focus;
        private float wholeSize;
        private 운영관찰WorldView world;
        private 운영관찰ObjectView selected;
        private 도로명주소표현조회 roadNames;
        private 사가정Synty표현Plan syntyPlan;
        private 사가정공간표현Composition spatialComposition;
        private string selectedRoad = "";
        private int addressPage;
        public const int AddressPageSize = 6;
        public string SelectedRoad => selectedRoad;
        public string SelectedAddress { get; private set; } = "";
        public int AddressPage => addressPage;
        public int AddressPageCount => Math.Max(1, ((roadNames?.Addresses(selectedRoad).Count ?? 0) + AddressPageSize - 1) / AddressPageSize);
        private GUIStyle titleStyle, smallStyle;
        private bool shown = true;
        private 단일PlayerViewController viewPolicy;
        private string statusMessage = "WaitingForConnection";
        private readonly List<float> performanceFrameTimes = new();
        private bool performanceMeasurementActive;
        public int PerformanceSampleCount => performanceFrameTimes.Count;
        public float AverageFps => performanceFrameTimes.Count == 0 ? 0f
            : performanceFrameTimes.Count / performanceFrameTimes.Sum();
        public float FrameTime95Ms
        {
            get
            {
                if (performanceFrameTimes.Count == 0) return 0f;
                var ordered = performanceFrameTimes.OrderBy(value => value).ToArray();
                var index = Mathf.Clamp(Mathf.CeilToInt(ordered.Length * .95f) - 1, 0, ordered.Length - 1);
                return ordered[index] * 1000f;
            }
        }
        public bool GameOverlayVisible { get; set; }
        public void SetStatus(string value) { statusMessage = value; }

        private void Awake() { Initialize(); }

        public void Initialize()
        {
            if (MapStatus != "NotLoaded") return;
            world = GetComponent<운영관찰WorldView>();
            try
            {
                var asset = Resources.Load<TextAsset>("SagajeongReference");
                var placements = Resources.Load<TextAsset>("SagajeongObservationPlacement");
                if (asset == null || placements == null) throw new ArgumentException("MapAssetMissing");
                map = JsonUtility.FromJson<공간참고자료>(asset.text);
                Places = 운영관찰지역배치검사.Validate(
                    JsonUtility.FromJson<운영관찰지역배치자료>(placements.text), map, asset.bytes);
                roadNames = LoadRoadNames(map, asset.bytes);
                var heights = LoadHeights(map, asset.bytes);
                spatialComposition = useSyntyPresentation
                    ? null
                    : LoadSpatialComposition(map, asset.bytes);
                SpatialPresentationAttribution = useSyntyPresentation
                    ? heights == null
                        ? "© OpenStreetMap contributors · ODbL 1.0"
                        : "© OpenStreetMap contributors · ODbL 1.0 / 국토교통부 건축HUB"
                    : Attribution(spatialComposition, heights != null);
                syntyPlan = useSyntyPresentation ? LoadSyntyPlan(map, heights) : 사가정Synty표현Plan.Empty();
                if (!useSyntyPresentation) SyntyStatus = "원문 윤곽 모형 표현";
                geography = new GameObject("사가정 지리 배경").transform;
                geography.SetParent(transform, false);
                SyntyVisualRoot = new GameObject("Synty VisualRoot · 표현 전용").transform;
                SyntyVisualRoot.SetParent(geography, false);
                if (useSyntyPresentation)
                {
                    사가정지리MeshBuilder.Build(map, AddMesh, heights, syntyPlan);
                    BuildSyntyVisuals();
                }
                else
                {
                    PrepareMiniatureRoots();
                    MiniatureBuildReport = 사가정모형MeshBuilder.BuildBatches(
                        map, heights, spatialComposition, CreateMiniatureMesh, showDecorativeGreenery);
                }
                Block(geography, "모형 받침대", new Vector3(map.offsetX, -15f, map.offsetZ),
                    new Vector3(map.halfExtent * 2 + 2, 30, map.halfExtent * 2 + 2), new Color(.41f,.43f,.42f));
                Block(geography, "모형 촬영 배경", new Vector3(map.offsetX,-33,map.offsetZ),
                    new Vector3(6000,1,6000),new Color(.41f,.61f,.80f));
                Block(geography, "사가정역 기준 표식", new Vector3(map.offsetX, 1, map.offsetZ),
                    new Vector3(8,2,8), new Color(.74f,.78f,.38f));
                bounds = new Bounds(transform.TransformPoint(new Vector3(map.offsetX, 35, map.offsetZ)),
                    new Vector3(map.halfExtent * 2, 130, map.halfExtent * 2));
                var cameraObject = new GameObject("사가정 운영 관찰 카메라");
                cameraObject.transform.SetParent(transform, false);
                ObservationCamera = cameraObject.AddComponent<Camera>();
                ObservationCamera.orthographic = true;
                ObservationCamera.depth = 200;
                ObservationCamera.cullingMask = 관찰표현LayerContract.사가정운영관찰Mask;
                ObservationCamera.clearFlags = CameraClearFlags.SolidColor;
                ObservationCamera.backgroundColor = new Color(.38f,.56f,.72f);
                ObservationCamera.allowMSAA = true;
                var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = true;
                cameraData.volumeLayerMask = 관찰표현LayerContract.사가정운영관찰Mask;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                ObservationCamera.nearClipPlane = 700f;
                ObservationCamera.farClipPlane = 3000f;
                BuildPresentationLight();
                BuildPresentationVolume();
                MapStatus = "Ready";
                RegisterEnvironment();
                Show(true);
                ShowWhole();
            }
            catch (ArgumentException)
            {
                Places = null;
                MapStatus = "MapOrPlacementRejected";
                Debug.LogWarning("사가정 관찰: 지도/배치 판본 검증 실패. 임의 배치를 하지 않습니다.");
            }
        }

        private static 사가정공간표현Composition LoadSpatialComposition(
            공간참고자료 data, byte[] mapBytes)
        {
            var approved = 사가정공간표현OverlayPipeline.LoadApprovedResource(data, mapBytes,
                name => Resources.Load<TextAsset>(name)?.bytes);
            if (approved.OverlayApplied) return approved;

            var local = 사가정공간표현OverlayPipeline.LoadLocalPrivateReviewForEditor(data, mapBytes);
            return local;
        }

        private static string Attribution(
            사가정공간표현Composition composition,
            bool heightOverlayApplied)
        {
            var sources = new List<string>
            {
                "© OpenStreetMap contributors · ODbL 1.0"
            };
            if (heightOverlayApplied) sources.Add("국토교통부 건축HUB");
            if (composition?.OverlayApplied == true)
            {
                sources.AddRange(composition.SourceReceipts
                    .Select(value => $"{value.Provider} · {value.LicenseCode}"));
            }
            return string.Join(" / ", sources.Distinct(StringComparer.Ordinal));
        }

        private 사가정Synty표현Plan LoadSyntyPlan(공간참고자료 data, 건물높이보완조회 heights)
        {
            try
            {
                var catalog = Resources.Load<법정동경관VisualCatalog>("SagajeongSyntyVisualCatalog");
                if (catalog == null) throw new ArgumentException("SyntyCatalogMissing");
                var plan = 사가정Synty표현Planner.Build(data, heights, catalog,
                    data.halfExtent * 1.5f, 120);
                SyntyStatus = $"Synty 혼합 표현 {plan.AcceptedCount}동 · fallback {plan.FallbackCount}동";
                return plan;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                SyntyStatus = "Synty 대장 미연결 · 외곽 Mesh 유지";
                Debug.LogWarning("사가정 Synty 표현 계획을 만들지 못해 원문 외곽 Mesh를 유지합니다: "
                                 + exception.Message);
                return 사가정Synty표현Plan.Empty();
            }
        }

        private void BuildSyntyVisuals()
        {
            if (SyntyVisualRoot == null || syntyPlan == null) return;
            foreach (var placement in syntyPlan.Placements.Where(value => value.Ready))
            {
                var buildingRoot = new GameObject("Synty 건물 · " + placement.SourceBuildingId)
                    { layer = ObservationLayer };
                buildingRoot.transform.SetParent(SyntyVisualRoot, false);
                var rotation = Quaternion.Euler(0f, placement.RotationY, 0f);
                var scaledHeight = placement.PrefabLocalBounds.size.y * placement.UniformScale;
                for (var index = 0; index < placement.VerticalCopies; index++)
                {
                    var instance = Instantiate(placement.Prefab, buildingRoot.transform);
                    instance.name = placement.VisualKey + " · " + (index + 1);
                    instance.transform.localRotation = rotation;
                    instance.transform.localScale = Vector3.one * placement.UniformScale;
                    var centerOffset = rotation * new Vector3(
                        placement.PrefabLocalBounds.center.x * placement.UniformScale,
                        0f,
                        placement.PrefabLocalBounds.center.z * placement.UniformScale);
                    instance.transform.localPosition = new Vector3(
                        placement.LocalPosition.x - centerOffset.x,
                        placement.LocalPosition.y + index * scaledHeight
                            - placement.PrefabLocalBounds.min.y * placement.UniformScale,
                        placement.LocalPosition.z - centerOffset.z);
                    PreparePresentationInstance(instance,
                        SyntyTint(placement.VisualKey, placement.SourceBuildingId));
                }
            }
            // geography는 원자료의 큰 지역 좌표를 가진다. 런타임 정적 배치는 자식 정점을
            // 월드 좌표로 굽는 과정에서 이 부모 변환을 다시 적용할 수 있으므로 사용하지 않는다.
            // 후보 수를 제한하고 공유 prefab/material을 유지해 SRP Batcher와 GPU instancing에 맡긴다.
        }

        private static void PreparePresentationInstance(GameObject instance, Color tint)
        {
            foreach (var item in instance.GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = ObservationLayer;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(block);
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                if (UnityEngine.Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
        }

        private static Color SyntyTint(string visualKey, string stableId)
        {
            var color = visualKey switch
            {
                var value when value == 법정동경관VisualKeys.CityApartment
                    => new Color(.88f, .91f, .96f),
                var value when value == 법정동경관VisualKeys.CityOffice
                    => new Color(.79f, .88f, .86f),
                var value when value == 법정동경관VisualKeys.CityCivic
                    => new Color(.96f, .78f, .43f),
                _ => new Color(.96f, .70f, .52f),
            };
            var hash = 17;
            foreach (var character in stableId ?? string.Empty) hash = unchecked(hash * 31 + character);
            var factor = .94f + Mathf.Abs(hash % 13) / 100f;
            return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
        }

        private void BuildPresentationLight()
        {
            var lightObject = new GameObject("사가정 사선 표현광") { layer = ObservationLayer };
            lightObject.transform.SetParent(geography, false);
            lightObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .90f, .76f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .72f;
            light.cullingMask = 관찰표현LayerContract.사가정운영관찰Mask;
        }

        private void BuildPresentationVolume()
        {
            // Collider가 필요한 local-volume 대신 전용 layer에서만 읽는 camera-local global volume을 쓴다.
            // 다른 카메라와 전역 환경의 후처리 상태에는 영향을 주지 않는다.
            var volumeObject = new GameObject("사가정 카메라 전용 색보정") { layer = ObservationLayer };
            volumeObject.transform.SetParent(geography, false);
            PresentationVolume = volumeObject.AddComponent<Volume>();
            PresentationVolume.isGlobal = true;
            PresentationVolume.priority = 200f;
            PresentationVolume.weight = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "사가정 카메라 전용 ACES 색보정";
            owned.Add(profile);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(PostExposure);
            color.contrast.Override(PostContrast);
            color.saturation.Override(PostSaturation);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(PostVignette);
            vignette.smoothness.Override(.32f);
            // DepthOfField는 의도적으로 profile에 추가하지 않는다. 전체/역세권 모두 정보 판독이 우선이다.
            PresentationVolume.sharedProfile = profile;
        }

        private void PrepareMiniatureRoots()
        {
            miniatureChunkRoot = new GameObject("사가정 4x4 표현 chunks").transform;
            miniatureChunkRoot.SetParent(geography, false);
            for (var z = 0; z < 사가정모형MeshBuilder.ChunkAxisCount; z++)
                for (var x = 0; x < 사가정모형MeshBuilder.ChunkAxisCount; x++)
                {
                    var index = z * 사가정모형MeshBuilder.ChunkAxisCount + x;
                    miniatureChunks[index] = new GameObject($"chunk {x}:{z}").transform;
                    miniatureChunks[index].SetParent(miniatureChunkRoot, false);
                }
            stationDetailRoot = new GameObject("사가정역 관찰 전용 상세").transform;
            stationDetailRoot.SetParent(geography, false);
            stationDetailRoot.gameObject.SetActive(false);
        }

        private 건물높이보완조회 LoadHeights(공간참고자료 data, byte[] bytes)
        {
            try
            {
                var asset = Resources.Load<TextAsset>("SagajeongBuildingHeightOverlay");
                if (asset == null) throw new ArgumentException("HeightOverlayMissing");
                var heights = 건물높이보완조회.Validate(JsonUtility.FromJson<건물높이보완자료>(asset.text), data, bytes);
                HeightStatus = $"대장 높이 후보 연결 {heights.Count}건";
                return heights;
            }
            catch (ArgumentException)
            {
                HeightStatus = "높이 보완 거부 · 원본 유지";
                Debug.LogWarning("사가정 높이 보완층 검증 실패. 원본 높이를 유지하며 확정 자료로 대체하지 않습니다.");
                return null;
            }
        }

        private 도로명주소표현조회 LoadRoadNames(공간참고자료 data, byte[] bytes)
        {
            try
            {
                var asset = Resources.Load<TextAsset>("SagajeongRoadNameOverlay");
                if (asset == null) throw new ArgumentException("RoadNameOverlayMissing");
                var result = 도로명주소표현조회.Validate(JsonUtility.FromJson<도로명표현자료>(asset.text), data, bytes);
                RoadNameStatus = $"도로명 {result.Labels.Count} · 주소 건물 {result.LinkedBuildingCount}";
                return result;
            }
            catch (ArgumentException)
            {
                RoadNameStatus = "도로명 보완 거부 · 지도 유지";
                Debug.LogWarning("사가정 도로명 보완층 검증 실패. 지도는 유지하고 도로명·건물번호 표현만 보류합니다.");
                return null;
            }
        }

        public void Show(bool value)
        {
            shown = value;
            IsVisible = value && ObservationCamera != null;
            // 관찰을 닫으면 전용 layer의 geometry, 조명, global Volume까지 함께 비활성화해
            // 다른 카메라의 culling/volume mask 설정과 무관하게 기존 화면에 새지 않게 한다.
            if (geography != null) geography.gameObject.SetActive(IsVisible);
            if (ObservationCamera != null) ObservationCamera.enabled = IsVisible;
        }

        private void RegisterEnvironment()
        {
            viewPolicy = GetComponentInParent<단일PlayerViewController>(true);
            if (viewPolicy != null && !viewPolicy.TryRegisterRuntimeEnvironment(transform,this,out var diagnostic))
                throw new InvalidOperationException(diagnostic);
        }

        public void ShowWhole()
        {
            if (ObservationCamera == null) return;
            selected = null;
            selectedRoad = "";
            SelectedAddress = "";
            addressPage = 0;
            focus = bounds.center;
            wholeSize = WholeViewOrthographicSize(bounds, 관찰회전,
                Mathf.Max(.1f, ObservationCamera.aspect));
            ObservationCamera.orthographicSize = wholeSize;
            UpdateCamera();
        }

        public void Focus(운영관찰ObjectView value)
        {
            if (value == null || ObservationCamera == null) return;
            selectedRoad = "";
            SelectedAddress = "";
            addressPage = 0;
            selected = value;
            FocusPoint(value.transform.position);
        }

        public void FocusPoint(Vector3 point)
        {
            if (ObservationCamera == null) return;
            focus = point;
            ObservationCamera.orthographicSize = 90;
            UpdateCamera();
        }

        public bool SelectRoad(string name)
        {
            if (roadNames == null || ObservationCamera == null) return false;
            foreach (var road in roadNames.Labels)
                if (road.name == name)
                {
                    selected = null;
                    selectedRoad = name;
                    SelectedAddress = "";
                    addressPage = 0;
                    FocusPoint(transform.TransformPoint(new Vector3(road.x, 0, road.z)));
                    return true;
                }
            return false;
        }

        public void SetAddressPage(int page) => addressPage = Mathf.Clamp(page, 0, AddressPageCount - 1);

        public bool SelectAddress(string buildingId)
        {
            if (roadNames == null || ObservationCamera == null) return false;
            var addresses = roadNames.Addresses(selectedRoad);
            for (var i = 0; i < addresses.Count; i++)
                if (addresses[i].BuildingId == buildingId)
                {
                    var address = addresses[i];
                    SelectedAddress = address.Label;
                    addressPage = i / AddressPageSize;
                    FocusPoint(transform.TransformPoint(new Vector3(address.X, address.Y, address.Z)));
                    return true;
                }
            return false;
        }

        private void UpdateCamera()
        {
            var rotation = 관찰회전;
            ObservationCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 1800, rotation);
            UpdateStationDetailVisibility();
        }
        private static Quaternion 관찰회전 => Quaternion.Euler(WholeViewPitch, WholeViewYaw, 0f);

        public static float WholeViewOrthographicSize(Bounds value, Quaternion rotation, float aspect)
        {
            if (aspect <= 0f || !float.IsFinite(aspect)) throw new ArgumentOutOfRangeException(nameof(aspect));
            var inverse = Quaternion.Inverse(rotation);
            var maximumHorizontal = 0f;
            var maximumVertical = 0f;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = value.center + Vector3.Scale(value.extents, new Vector3(x, y, z));
                        var local = inverse * (corner - value.center);
                        maximumHorizontal = Mathf.Max(maximumHorizontal, Mathf.Abs(local.x));
                        maximumVertical = Mathf.Max(maximumVertical, Mathf.Abs(local.y));
                    }
            return Mathf.Max(maximumVertical, maximumHorizontal / aspect) / WholeMapWidthRatio;
        }

        private void UpdateStationDetailVisibility()
        {
            if (stationDetailRoot == null || ObservationCamera == null || map == null) return;
            var localFocus3 = transform.InverseTransformPoint(focus);
            var horizontalFocus = new Vector2(localFocus3.x, localFocus3.z);
            var station = new Vector2(map.offsetX, map.offsetZ);
            var visible = ObservationCamera.orthographicSize <= 120f
                          && (horizontalFocus - station).sqrMagnitude
                          <= 사가정모형MeshBuilder.StationDetailRadius
                          * 사가정모형MeshBuilder.StationDetailRadius;
            if (stationDetailRoot.gameObject.activeSelf != visible)
                stationDetailRoot.gameObject.SetActive(visible);
        }

        private void AddMesh(string name, List<Vector3> vertices, List<int> triangles, Color color)
        {
            // 고정 표현광의 단순 접지 그림자. 전역 조명/날씨 설정과 실제 높이 판정을 바꾸지 않는다.
            if (name.Contains("건물"))
            {
                var shadowVertices = new List<Vector3>(vertices.Count);
                foreach (var v in vertices) shadowVertices.Add(new Vector3(v.x + v.y*.35f,.045f,v.z+v.y*.4f));
                CreateMesh("건물 접지 그림자 · 표현용",shadowVertices,triangles,new Color(.30f,.35f,.28f));
            }
            CreateMesh(name,vertices,triangles,color);
        }

        private void CreateMiniatureMesh(사가정모형MeshBuilder.MeshBatch batch)
        {
            var index = batch.ChunkZ * 사가정모형MeshBuilder.ChunkAxisCount + batch.ChunkX;
            if (index < 0 || index >= miniatureChunks.Length || miniatureChunks[index] == null)
                throw new InvalidOperationException("SagajeongMiniatureChunkMissing");
            var parent = batch.IsStationDetail ? stationDetailRoot : miniatureChunks[index];
            var go = new GameObject(batch.Name) { layer = ObservationLayer };
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = batch.Name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(batch.Vertices);
            mesh.SetTriangles(batch.Triangles, 0);
            mesh.SetColors(batch.Colors);
            mesh.SetUVs(1, batch.SurfaceData);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(Color.white, batch.IsShadow, true);
            renderer.shadowCastingMode = batch.IsShadow
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = !batch.IsShadow;
        }

        private void CreateMesh(string name, List<Vector3> vertices, List<int> triangles, Color color)
        {
            var go = new GameObject(name) { layer = ObservationLayer };
            go.transform.SetParent(geography, false);
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer=go.AddComponent<MeshRenderer>();
            var shadow = name.Contains("접지 그림자");
            renderer.sharedMaterial = Material(color, shadow, false);
            renderer.shadowCastingMode = shadow
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = !shadow;
        }

        private Material Material(Color color, bool shadow = false, bool useVertexColor = false)
        {
            var shader = shadow ? Resources.Load<Shader>("사가정모형그림자")
                : Resources.Load<Shader>("사가정모형명암");
            shader ??= Resources.Load<Shader>("관찰도형명암")
                ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var rgba = (Color32)color;
            var key = $"{shader.name}|{rgba.r:X2}{rgba.g:X2}{rgba.b:X2}{rgba.a:X2}|{useVertexColor}";
            if (materialCache.TryGetValue(key, out var cached)) return cached;
            var value = new Material(shader) { color = color };
            if (value.HasProperty("_BaseColor")) value.SetColor("_BaseColor", color);
            if (value.HasProperty("_Color")) value.SetColor("_Color", color);
            if (value.HasProperty("_VertexColorWeight"))
                value.SetFloat("_VertexColorWeight", useVertexColor ? 1f : 0f);
            value.enableInstancing = true;
            owned.Add(value);
            materialCache.Add(key, value);
            return value;
        }

        public void BeginPerformanceMeasurement()
        {
            performanceFrameTimes.Clear();
            performanceMeasurementActive = true;
        }

        public void EndPerformanceMeasurement() => performanceMeasurementActive = false;

        private void Update()
        {
            if (!performanceMeasurementActive || !IsVisible) return;
            var value = Time.unscaledDeltaTime;
            if (value > 0f && float.IsFinite(value)) performanceFrameTimes.Add(value);
        }

        private void Block(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = ObservationLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            if (UnityEngine.Application.isPlaying) Destroy(go.GetComponent<Collider>());
            else DestroyImmediate(go.GetComponent<Collider>());
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            if (!shown)
            {
                if (GUI.Button(new Rect(12,12,180,32), "사가정 운영 관찰")) Show(true);
                return;
            }
            if (GameOverlayVisible) { HandleCameraInput(); return; }
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            GUI.Box(new Rect(12,12,370,106), GUIContent.none);
            GUI.Label(new Rect(24,20,345,32), "사가정 생활 디오라마", titleStyle);
            GUI.Label(new Rect(24,56,345,52), "가상 업무 표본 · 실제 위치 추적 아님\n휠: 확대  |  오른쪽 드래그: 이동  |  표식: 선택", smallStyle);
            if (GUI.Button(new Rect(Screen.width-240,12,110,32), "전체 보기")) ShowWhole();
            if (GUI.Button(new Rect(Screen.width-124,12,110,32), "기존 화면")) Show(false);
            var footerWidth=Mathf.Min(560,Screen.width*.42f);
            GUI.Box(new Rect(12,Screen.height-144,footerWidth,132), GUIContent.none);
            GUI.Label(new Rect(24,Screen.height-140,footerWidth-24,20), "음식 배달  ·  화물 운송  ·  창고  ·  마트",smallStyle);
            GUI.Label(new Rect(24,Screen.height-118,footerWidth-24,20),
                $"{world?.ObjectCount ?? 0}개 업무 결과  |  {statusMessage} / {MapStatus}", smallStyle);
            GUI.Label(new Rect(24,Screen.height-96,footerWidth-24,20),
                $"공간 표현: {SpatialPresentationStatus} · {SpatialPresentationRevision} · "
                + (SpatialPresentationContentHash.Length >= 12
                    ? SpatialPresentationContentHash.Substring(0, 12)
                    : SpatialPresentationContentHash), smallStyle);
            GUI.Label(new Rect(24,Screen.height-74,footerWidth-24,22),
                $"근거: {SpatialPresentationAttribution}", smallStyle);
            GUI.Label(new Rect(24,Screen.height-50,footerWidth-24,28),
                $"{RoadNameStatus} · {HeightStatus}", smallStyle);
            if (!string.IsNullOrEmpty(selectedRoad))
            {
                var addresses=roadNames?.Addresses(selectedRoad)??Array.Empty<도로명건물주소>();
                // 방어 준비 진입 버튼(상단 52~86px) 아래에 두고 모든 주소에 페이지로 접근한다.
                GUI.Box(new Rect(Screen.width-300,96,286,326),GUIContent.none);
                GUI.Label(new Rect(Screen.width-288,106,210,22),selectedRoad+" · 공개 OSM",smallStyle);
                if(GUI.Button(new Rect(Screen.width-74,104,48,24),"닫기")) { selectedRoad=""; SelectedAddress=""; addressPage=0; }
                GUI.Label(new Rect(Screen.width-288,129,260,38),
                    $"건물번호 {addresses.Count}곳 · 대표점은 라벨용\n실제 출입구·입주·길찾기 정보가 아닙니다.",smallStyle);
                for(var i=0;i<AddressPageSize && addressPage*AddressPageSize+i<addresses.Count;i++)
                {
                    var address=addresses[addressPage*AddressPageSize+i];
                    if(GUI.Button(new Rect(Screen.width-288,170+i*29,260,25),address.Label))
                        SelectAddress(address.BuildingId);
                }
                GUI.enabled=addressPage>0;
                if(GUI.Button(new Rect(Screen.width-288,348,66,25),"이전"))SetAddressPage(addressPage-1);
                GUI.enabled=true;
                GUI.Label(new Rect(Screen.width-212,350,100,22),$"{addressPage+1} / {AddressPageCount}쪽",smallStyle);
                GUI.enabled=addressPage+1<AddressPageCount;
                if(GUI.Button(new Rect(Screen.width-96,348,68,25),"다음"))SetAddressPage(addressPage+1);
                GUI.enabled=true;
                GUI.Label(new Rect(Screen.width-288,381,260,32),string.IsNullOrEmpty(SelectedAddress)?"건물번호를 선택하면 확대합니다.":"선택: "+SelectedAddress,smallStyle);
            }
            else if (selected != null)
            {
                GUI.Box(new Rect(Screen.width-288,58,274,158), GUIContent.none);
                var label = Places != null && Places.TryGetValue(selected.SemanticPlaceStableId, out var p) ? p.label : "업무 결과";
                var state = selected.AttentionStateCode == "Recovered" ? "중단 후 회복 완료" : "완료";
                GUI.Label(new Rect(Screen.width-276,68,250,138),
                    $"{label}\n완료 업무 관찰 · 실존 업소와 무관\n상태: {state}\n판본: {selected.Revision} / {(selected.Frozen ? "갱신 보류" : "정상")}\n가상 표식이며 현재 위치가 아닙니다.", smallStyle);
            }
            var occupiedLabels=new List<Rect>();
            if (!string.IsNullOrEmpty(selectedRoad)) occupiedLabels.Add(new Rect(Screen.width-300,96,286,326));
            else if (selected != null) occupiedLabels.Add(new Rect(Screen.width-288,58,274,158));
            DrawRoadLabels(occupiedLabels);
            if (world != null && ObservationCamera != null)
                foreach (var item in world.Objects)
                {
                    var point = ObservationCamera.WorldToScreenPoint(item.transform.position + Vector3.up*15);
                    if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 80 || point.y > Screen.height-154) continue;
                    var label = Places.TryGetValue(item.SemanticPlaceStableId, out var p) ? p.label : "업무";
                    var rect=PlaceLabel(new Rect(point.x-58,Screen.height-point.y,116,26),occupiedLabels,124,Screen.height-154);
                    if (rect.width<=0) continue;
                    occupiedLabels.Add(rect);
                    if (GUI.Button(rect, label)) Focus(item);
                }
            HandleCameraInput();
        }

        private void DrawRoadLabels(List<Rect> occupied)
        {
            if(roadNames==null||ObservationCamera==null)return;
            var maximum=ObservationCamera.orthographicSize<=90?24:18;
            foreach(var road in roadNames.SelectLabels(ObservationCamera.orthographicSize,focus.x,focus.z,maximum))
            {
                var point=ObservationCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(road.x,.5f,road.z)));
                if(point.z<=0||point.x<0||point.x>Screen.width||point.y<80||point.y>Screen.height-154)continue;
                var preferred=new Rect(point.x-52,Screen.height-point.y-11,104,23);
                var rect=PlaceLabel(preferred,occupied,124,Screen.height-154);if(rect.width<=0)continue;
                occupied.Add(rect);
                var label=road.importanceCode=="Major"?"◆ "+road.name:road.name;
                if(GUI.Button(rect,label))
                {
                    SelectRoad(road.name);
                }
            }
        }

        private void HandleCameraInput()
        {
            if (ObservationCamera == null || !shown) return;
            var e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                ObservationCamera.orthographicSize = Mathf.Clamp(ObservationCamera.orthographicSize * Mathf.Exp(e.delta.y*.07f), 35, wholeSize);
                UpdateStationDetailVisibility();
                e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 1)
            {
                var right = ObservationCamera.transform.right; right.y = 0;
                var up = Vector3.ProjectOnPlane(ObservationCamera.transform.up,Vector3.up).normalized;
                focus += (-right * e.delta.x + up * e.delta.y) * ObservationCamera.orthographicSize / Mathf.Max(1,Screen.height)*2;
                focus.x = Mathf.Clamp(focus.x,bounds.min.x,bounds.max.x);
                focus.z = Mathf.Clamp(focus.z,bounds.min.z,bounds.max.z);
                UpdateCamera(); e.Use();
            }
        }

        public static Rect PlaceLabel(Rect preferred,IReadOnlyList<Rect> occupied,float minY,float maxY)
        {
            for(var step=0;step<25;step++)
            {
                var candidate=preferred;
                candidate.y+=step==0 ? 0 : ((step+1)/2)*28*(step%2==0 ? 1 : -1);
                if(candidate.y<minY || candidate.yMax>maxY)continue;
                var overlaps=false;
                foreach(var rect in occupied)if(candidate.Overlaps(rect)){overlaps=true;break;}
                if(!overlaps)return candidate;
            }
            return Rect.zero;
        }

        private void OnDisable()
        {
            IsVisible = false;
            if (ObservationCamera != null) ObservationCamera.enabled = false;
            if (viewPolicy != null) viewPolicy.UnregisterRuntimeEnvironment(transform,this);
            ReleaseGeometry();
        }
        private void OnEnable()
        {
            Initialize();
            if (ObservationCamera != null) { RegisterEnvironment(); Show(shown); }
        }
        private void ReleaseGeometry()
        {
            if (geography != null) ReleaseObject(geography.gameObject);
            if (ObservationCamera != null) ReleaseObject(ObservationCamera.gameObject);
            geography = null;
            miniatureChunkRoot = null;
            stationDetailRoot = null;
            Array.Clear(miniatureChunks, 0, miniatureChunks.Length);
            ObservationCamera = null;
            PresentationVolume = null;
            MiniatureBuildReport = null;
            Places = null;
            roadNames = null;
            selectedRoad = "";
            SelectedAddress = "";
            addressPage = 0;
            MapStatus = "NotLoaded";
            HeightStatus = "NotLoaded";
            RoadNameStatus = "NotLoaded";
            SyntyStatus = "NotLoaded";
            spatialComposition = null;
            SpatialPresentationAttribution = "NotLoaded";
            SyntyVisualRoot = null;
            syntyPlan = null;
            performanceMeasurementActive = false;
            performanceFrameTimes.Clear();
            foreach (var item in owned) if (item != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(item); else DestroyImmediate(item);
            }
            owned.Clear();
            materialCache.Clear();
        }
        private static void ReleaseObject(GameObject value)
        {
            value.SetActive(false);
            value.name += "__released";
            if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void OnDestroy() { IsVisible = false; ReleaseGeometry(); }
    }
}
