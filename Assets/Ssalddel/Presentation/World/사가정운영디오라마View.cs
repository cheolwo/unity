using System;
using System.Collections.Generic;
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
        private GUIStyle titleStyle, smallStyle;
        private bool shown = true;
        private 단일PlayerViewController viewPolicy;
        private string statusMessage = "WaitingForConnection";
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
                geography = new GameObject("사가정 지리 배경").transform;
                geography.SetParent(transform, false);
                사가정지리MeshBuilder.Build(map, AddMesh);
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
            focus = bounds.center;
            wholeSize = 동네전체관찰CameraMath.맞춤크기(bounds, 동네전체관찰CameraMath.기본회전,
                Mathf.Max(.1f, ObservationCamera.aspect), .12f);
            ObservationCamera.orthographicSize = wholeSize;
            UpdateCamera();
        }

        public void Focus(운영관찰ObjectView value)
        {
            if (value == null || ObservationCamera == null) return;
            selected = value;
            focus = value.transform.position;
            ObservationCamera.orthographicSize = 90;
            UpdateCamera();
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
            owned.Add(value);
            return value;
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
                "© OpenStreetMap contributors · ODbL 1.0\n미상 높이는 기호 · 업무 표식은 가상", smallStyle);
            if (selected != null)
            {
                GUI.Box(new Rect(Screen.width-288,58,274,158), GUIContent.none);
                var label = Places != null && Places.TryGetValue(selected.SemanticPlaceStableId, out var p) ? p.label : "업무 결과";
                var state = selected.AttentionStateCode == "Recovered" ? "중단 후 회복 완료" : "완료";
                GUI.Label(new Rect(Screen.width-276,68,250,138),
                    $"{label}\n완료 업무 관찰 · 실존 업소와 무관\n상태: {state}\n판본: {selected.Revision} / {(selected.Frozen ? "갱신 보류" : "정상")}\n가상 표식이며 현재 위치가 아닙니다.", smallStyle);
            }
            var occupiedLabels=new List<Rect>();
            if (selected != null) occupiedLabels.Add(new Rect(Screen.width-288,58,274,158));
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
            MapStatus = "NotLoaded";
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
