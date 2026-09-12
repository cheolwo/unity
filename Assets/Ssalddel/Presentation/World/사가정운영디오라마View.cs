using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>지역 배경과 관찰 카메라. 지리 자료는 동결 자산이며 업무 사본은 이 객체에 저장하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class 사가정운영디오라마View : MonoBehaviour
    {
        public const int ObservationLayer = 30;
        public static bool IsVisible { get; private set; }
        public string MapStatus { get; private set; } = "NotLoaded";
        public string HeightStatus { get; private set; } = "NotLoaded";
        public string RoadNameStatus { get; private set; } = "NotLoaded";
        public string SyntyStatus { get; private set; } = "NotLoaded";
        public string VisualTargetRevision => syntyPlan?.Revision ?? "NotLoaded";
        public int SyntyPlacementCount => syntyPlan?.AcceptedCount ?? 0;
        public int SyntyFallbackCount => syntyPlan?.FallbackCount ?? 0;
        public IReadOnlyList<사가정Synty표현Placement> SyntyPlacements
            => syntyPlan?.Placements ?? Array.Empty<사가정Synty표현Placement>();
        public Transform SyntyVisualRoot { get; private set; }
        public Dictionary<string, 운영관찰배치지점> Places { get; private set; }
        public Camera ObservationCamera { get; private set; }
        private readonly List<UnityEngine.Object> owned = new();
        private 공간참고자료 map;
        private Transform geography;
        private Bounds bounds;
        private Vector3 focus;
        private float wholeSize;
        private 운영관찰WorldView world;
        private 운영관찰ObjectView selected;
        private 도로명주소표현조회 roadNames;
        private 사가정Synty표현Plan syntyPlan;
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
                syntyPlan = LoadSyntyPlan(map, heights);
                geography = new GameObject("사가정 지리 배경").transform;
                geography.SetParent(transform, false);
                SyntyVisualRoot = new GameObject("Synty VisualRoot · 표현 전용").transform;
                SyntyVisualRoot.SetParent(geography, false);
                사가정지리MeshBuilder.Build(map, AddMesh, heights, syntyPlan);
                BuildSyntyVisuals();
                Block(geography, "지도 바탕 · 자료 없는 영역은 미확인", new Vector3(map.offsetX, -2, map.offsetZ),
                    new Vector3(map.halfExtent * 2, 4, map.halfExtent * 2), new Color(.43f,.49f,.38f));
                Block(geography, "사가정역 기준 표식", new Vector3(map.offsetX, 1, map.offsetZ),
                    new Vector3(8,2,8), new Color(.74f,.78f,.38f));
                bounds = new Bounds(transform.TransformPoint(new Vector3(map.offsetX, 35, map.offsetZ)),
                    new Vector3(map.halfExtent * 2, 130, map.halfExtent * 2));
                var cameraObject = new GameObject("사가정 운영 관찰 카메라");
                cameraObject.transform.SetParent(transform, false);
                ObservationCamera = cameraObject.AddComponent<Camera>();
                ObservationCamera.orthographic = true;
                ObservationCamera.depth = 200;
                ObservationCamera.cullingMask = 1 << ObservationLayer;
                ObservationCamera.clearFlags = CameraClearFlags.SolidColor;
                ObservationCamera.backgroundColor = new Color(.47f,.59f,.69f);
                ObservationCamera.nearClipPlane = .1f;
                ObservationCamera.farClipPlane = 4000;
                BuildPresentationLight();
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
            light.cullingMask = 1 << ObservationLayer;
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
            wholeSize = 동네전체관찰CameraMath.맞춤크기(bounds, 동네전체관찰CameraMath.기본회전,
                Mathf.Max(.1f, ObservationCamera.aspect), .12f);
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
            var rotation = 동네전체관찰CameraMath.기본회전;
            ObservationCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 1800, rotation);
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

        private void CreateMesh(string name, List<Vector3> vertices, List<int> triangles, Color color)
        {
            var go = new GameObject(name) { layer = ObservationLayer };
            go.transform.SetParent(geography, false);
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Material(color);
        }

        private Material Material(Color color)
        {
            var shader = Resources.Load<Shader>("관찰도형명암")
                ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var value = new Material(shader) { color = color };
            value.enableInstancing = true;
            owned.Add(value);
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
            var footerWidth=Mathf.Min(430,Screen.width*.30f);
            GUI.Box(new Rect(12,Screen.height-110,footerWidth,98), GUIContent.none);
            GUI.Label(new Rect(24,Screen.height-106,footerWidth-24,20), "음식 배달  ·  화물 운송  ·  창고  ·  마트",smallStyle);
            GUI.Label(new Rect(24,Screen.height-84,footerWidth-24,20),
                $"{world?.ObjectCount ?? 0}개 업무 결과  |  {statusMessage} / {MapStatus}", smallStyle);
            GUI.Label(new Rect(24,Screen.height-62,footerWidth-24,44),
                $"© OSM contributors · ODbL 1.0 / 국토교통부 건축HUB\n{RoadNameStatus} · {HeightStatus}", smallStyle);
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
                    if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 80 || point.y > Screen.height-120) continue;
                    var label = Places.TryGetValue(item.SemanticPlaceStableId, out var p) ? p.label : "업무";
                    var rect=PlaceLabel(new Rect(point.x-58,Screen.height-point.y,116,26),occupiedLabels,124,Screen.height-120);
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
                if(point.z<=0||point.x<0||point.x>Screen.width||point.y<80||point.y>Screen.height-120)continue;
                var preferred=new Rect(point.x-52,Screen.height-point.y-11,104,23);
                var rect=PlaceLabel(preferred,occupied,124,Screen.height-120);if(rect.width<=0)continue;
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
            ObservationCamera = null;
            Places = null;
            roadNames = null;
            selectedRoad = "";
            SelectedAddress = "";
            addressPage = 0;
            MapStatus = "NotLoaded";
            HeightStatus = "NotLoaded";
            RoadNameStatus = "NotLoaded";
            SyntyStatus = "NotLoaded";
            SyntyVisualRoot = null;
            syntyPlan = null;
            performanceMeasurementActive = false;
            performanceFrameTimes.Clear();
            foreach (var item in owned) if (item != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(item); else DestroyImmediate(item);
            }
            owned.Clear();
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
