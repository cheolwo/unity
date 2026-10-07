using System;
using System.Linq;
using System.Collections.Generic;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        private 사가정음식생활공간Binding 사가정생활공간;
        private 공간참고자료 사가정생활지도;
        private Bounds 사가정생활범위;
        private Transform 사가정음식점외관;
        private Vector3? 사가정선택시설;
        private readonly Dictionary<Behaviour, bool> 사가정기존화면상태 = new Dictionary<Behaviour, bool>();
        private bool 사가정화면복원중;
        private readonly List<Renderer> 사가정참고건물표현 = new List<Renderer>();
        private static readonly HashSet<string> 사가정격리표현형식 = new HashSet<string>(StringComparer.Ordinal)
        {
            "Ssalddel.Unity.Presentation.WorldMap.세계지구본View",
            "Ssalddel.Unity.Presentation.World.사가정운영디오라마View",
            "Ssalddel.Unity.Presentation.World.역방어준비View",
            "Ssalddel.Unity.Presentation.World.일인칭창고상호작용Controller",
            "Ssalddel.Unity.Presentation.World.플레이어경관Controller",
            "Ssalddel.Unity.Presentation.World.네이처조우Presenter",
            "Ssalddel.Unity.Presentation.World.플레이어전술조작Controller",
            "Ssalddel.Unity.Presentation.World.일인칭경관CameraController",
            "Ssalddel.Unity.Presentation.World.전투시점Controller",
            "Ssalddel.Unity.Presentation.World.오늘작업계획Presenter",
            "Ssalddel.Unity.Presentation.World.농장경영시점Controller"
        };

        private void 사가정음식생활준비()
        {
            var map = Resources.Load<TextAsset>("SagajeongReference");
            var binding = Resources.Load<TextAsset>("SagajeongFoodLifeBinding");
            if (map == null || binding == null) throw new ArgumentException("SagajeongFoodLifeResourceMissing");
            사가정생활지도 = JsonUtility.FromJson<공간참고자료>(map.text);
            사가정생활공간 = new 사가정음식생활공간Binding(
                JsonUtility.FromJson<사가정음식생활공간Data>(binding.text), 사가정생활지도, map.bytes);
        }

        private void BuildSagajeongFoodLife()
        {
            if (사가정생활공간 == null) throw new InvalidOperationException("SagajeongFoodLifeNotPrepared");
            var points = 사가정생활공간.Data.segments.SelectMany(s => s.gamePoints).Select(p => 위치(p, 0)).ToArray();
            사가정생활범위 = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) 사가정생활범위.Encapsulate(point);
            사가정생활범위.Expand(new Vector3(30, 16, 30));
            // 원문 형상은 보존하고 이번 작은 관찰 영역만 같은 좌표계로 그린다.
            // 담당 NPC를 읽을 수 있도록 대상 건물만 독립된 게임용 낮은 표식으로 표현한다.
            var viewMap = new 공간참고자료 {
                revision = 사가정생활지도.revision, coordinateMethod = 사가정생활지도.coordinateMethod,
                attribution = 사가정생활지도.attribution, offsetX = 사가정생활지도.offsetX, offsetZ = 사가정생활지도.offsetZ,
                halfExtent = 사가정생활지도.halfExtent,
                roads = 사가정생활지도.roads.Where(r => 경계안(r.x1, r.z1) || 경계안(r.x2, r.z2)).ToArray(),
                buildings = 사가정생활지도.buildings.Where(b => b.id != 사가정생활공간.Data.buildingId
                    && b.points.Any(p => 경계안(p.x, p.z))).ToArray()
            };
            Block("사가정 생활 관찰 바닥", 사가정생활범위.center + Vector3.down * .15f,
                new Vector3(사가정생활범위.size.x, .15f, 사가정생활범위.size.z), new Color(.66f, .71f, .63f));
            var buildings = viewMap.buildings;
            viewMap.buildings = Array.Empty<참고건물>();
            사가정지리MeshBuilder.Build(viewMap, 참고Mesh);
            viewMap.roads = Array.Empty<참고도로>();
            foreach (var building in buildings)
            {
                viewMap.buildings = new[] { building };
                사가정지리MeshBuilder.Build(viewMap, (name, vertices, triangles, color) => {
                    var uniqueName = building.id + " · " + name;
                    참고Mesh(uniqueName, vertices, triangles, color);
                    사가정참고건물표현.Add(transform.Find(uniqueName).GetComponent<Renderer>());
                });
            }
            foreach (var segment in 사가정생활공간.Data.segments)
                for (var i = 1; i < segment.gamePoints.Length; i++)
                {
                    var a = 위치(segment.gamePoints[i - 1], .10f); var b = 위치(segment.gamePoints[i], .10f);
                    var line = Block("게임 경로 · " + segment.id + ":" + i, (a + b) / 2,
                        new Vector3(segment.sourceRoadIds.Length > 0 ? .38f : .24f, .06f, Vector3.Distance(a, b)),
                        segment.sourceRoadIds.Length > 0 ? new Color(.30f, .72f, .53f) : new Color(.91f, .69f, .28f));
                    line.rotation = Quaternion.LookRotation(b - a);
                }
            var store = 위치(사가정생활공간.Anchor("restaurant"), .15f);
            var storePad = Block("샐러리아 · 건물 수준 기록 위치", store, new Vector3(7, .3f, 6), new Color(.87f, .80f, .65f));
            Label(storePad, "샐러리아 사가정점\n게임용 접근점", Color.black);
            var placeLabel = storePad.GetComponentInChildren<TextMesh>();
            placeLabel.transform.position = store + 동네전체관찰CameraMath.기본회전 * Vector3.right * 4 + Vector3.up * 4;
            placeLabel.characterSize = .09f;
            사가정음식점외관 = new GameObject("샐러리아 · 교체 가능한 게임용 외관 표식").transform;
            사가정음식점외관.SetParent(transform, false);
            var counter = Block("가상 음식점 작업대", store + new Vector3(0, .6f, 1.5f), new Vector3(4, 1.2f, .8f), new Color(.48f, .64f, .42f));
            counter.SetParent(사가정음식점외관, true);
            var back = Block("게임용 낮은 외관", store + new Vector3(0, .8f, 2.6f), new Vector3(7, 1.6f, .3f), new Color(.92f, .84f, .57f));
            back.SetParent(사가정음식점외관, true);
            사가정음식생활외관표시(사가정생활공간.Data.facadeVisible);
            foreach (var home in new[] { ("home-a", "주민 A · 가상 주거"), ("home-b", "주민 B · 가상 주거"), ("mart", "보조 마트 · 가상 접근점") })
            {
                var point = 위치(사가정생활공간.Anchor(home.Item1), .18f);
                var marker = Block(home.Item2, point, new Vector3(5, .35f, 5), new Color(.52f, .65f, .79f));
                Label(marker, home.Item2, Color.black);
            }
            foreach (var role in new[] { (가상동네생활기준.RestaurantActorId, "가상 음식점 담당", new Color(.82f, .37f, .27f)),
                ("participant:synthetic:a", "주민 A", new Color(.25f, .46f, .86f)),
                ("participant:synthetic:b", "주민 B", new Color(.45f, .32f, .79f)) })
            {
                var actor = Block(role.Item2, Vector3.zero, new Vector3(.9f, 1.8f, .9f), role.Item3);
                Label(actor, role.Item2, Color.black); 생활주체[role.Item1] = actor;
            }
            for (var i = 0; i < 3; i++)
            {
                fleetActors[i] = Block("가상 기사 " + (i + 1), Vector3.zero, new Vector3(.9f, 1.8f, .9f), new Color(.22f, .29f, .27f));
                fleetVehicles[i] = Block("기사 " + (i + 1) + " · 게임 차량", Vector3.zero, new Vector3(1.2f, .7f, 2.4f), new Color(.20f, .45f, .35f));
                fleetParcels[i] = Block("기사 " + (i + 1) + " · 운반 표식", Vector3.zero, Vector3.one * .45f, new Color(.95f, .71f, .25f));
                Label(fleetActors[i], "기사 " + (i + 1), Color.black);
                생활주체["actor:synthetic-courier:" + (i + 1)] = fleetActors[i];
            }
            courier = fleetActors[0]; vehicle = fleetVehicles[0]; parcel = fleetParcels[0];
            observer = new GameObject("사가정 음식 생활 · 관찰 카메라").AddComponent<Camera>();
            observer.transform.SetParent(transform, false); observer.depth = 100; observer.orthographic = true;
            observer.clearFlags = CameraClearFlags.SolidColor; observer.backgroundColor = new Color(.61f, .72f, .78f);
            observer.cullingMask = 1 << 31; observer.nearClipPlane = .1f; observer.farClipPlane = 1000;
            사가정음식생활카메라갱신();
        }

        private bool 경계안(float x, float z) => x >= 사가정생활범위.min.x && x <= 사가정생활범위.max.x && z >= 사가정생활범위.min.z && z <= 사가정생활범위.max.z;

        private void RenderSagajeongFoodLife()
        {
            var life = state?.WaitingFleet?.NeighborhoodLife;
            if (life == null || 사가정생활공간 == null) throw new InvalidOperationException("SagajeongFoodLifeSnapshotMissing");
            // 전체 새 표현을 검증한 뒤 한 번에 반영한다. 한 객체만 갱신한 상태로 오류를 숨기지 않는다.
            var positions = new Dictionary<string, Vector3>();
            foreach (var id in 생활주체.Keys)
            {
                var actors = life.Actors.Where(a => a.ActorId == id).ToArray();
                if (actors.Length != 1) throw new ArgumentException("SagajeongFoodLifeActorBindingMismatch");
                positions[id] = 위치(사가정생활공간.ProjectActor(actors[0]), .9f);
            }
            var vehiclePositions = state.WaitingFleet.Drivers.Select(d => 위치(사가정생활공간.Project(d.Courier.VehicleX, d.Courier.VehicleZ), .38f)).ToArray();
            if (vehiclePositions.Length != fleetActors.Length) throw new ArgumentException("SagajeongFoodLifeFleetBindingMismatch");
            foreach (var item in positions)
            {
                var view = 생활주체[item.Key]; view.position = item.Value;
                var actor = life.Actors.Single(a => a.ActorId == item.Key);
                var label = view.GetComponentInChildren<TextMesh>();
                var activity = Ssalddel.Unity.Observation.동네관찰Presenter.단계(actor.Stage);
                if (actor.Role == "Courier" && (actor.Stage == "Working" || actor.Stage == "Finishing"))
                    activity = Ssalddel.Unity.Observation.동네관찰Presenter.단계(state.WaitingFleet.Drivers.Single(d => d.Courier.ActorStableId == actor.ActorId).Courier.Stage);
                if (actor.Role == "RestaurantOperator" && actor.Stage == "Working")
                {
                    var pending = state.FoodDeliveries.Where(o => !o.PickedUpTick.HasValue && !string.IsNullOrEmpty(o.RestaurantResponseDecisionStableId)).ToArray();
                    activity = pending.Any(o => !o.ReadyForPickupTick.HasValue) ? "조리 중" : pending.Length > 0 ? "픽업 대기" : "다음 주문 대기";
                }
                if (label != null) label.text = (actor.Role == "RestaurantOperator" ? "가상 음식점 담당" : Ssalddel.Unity.Observation.동네관찰Presenter.이름(item.Key)) + "\n" + activity;
            }
            for (var i = 0; i < fleetActors.Length; i++)
            {
                fleetVehicles[i].position = vehiclePositions[i];
                fleetParcels[i].position = fleetActors[i].position + Vector3.up * .6f;
                fleetParcels[i].gameObject.SetActive(state.WaitingFleet.Drivers[i].Courier.Carrying);
            }
            사가정음식생활카메라갱신();
        }

        private void 사가정음식생활카메라갱신()
        {
            if (observer == null || 사가정생활공간 == null) return;
            사가정음식생활화면격리();
            var target = 사가정생활범위.center;
            if (follow && 선택생활주체.Length > 0 && 생활주체.TryGetValue(선택생활주체, out var actor)) { target = actor.position; 사가정선택시설 = null; }
            else if (follow && 사가정선택시설.HasValue) target = 사가정선택시설.Value;
            else if (follow && selectedDriver >= 0 && selectedDriver < fleetActors.Length && fleetActors[selectedDriver] != null) target = fleetActors[selectedDriver].position;
            var rotation = 동네전체관찰CameraMath.기본회전;
            var distance = follow ? 100 : Mathf.Max(200, 사가정생활범위.size.magnitude * 2);
            observer.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            observer.orthographicSize = follow ? 12 : 동네전체관찰CameraMath.맞춤크기(사가정생활범위, rotation,
                Mathf.Max(.1f, observer.pixelRect.width / Mathf.Max(1, observer.pixelRect.height)));
            // 원문 polygon/높이는 그대로 두고, 추적할 때 앞을 가리는 새 참고 벽만 잠시 숨긴다.
            // Collider/통행권위 없이 Renderer bounds로 판독용 가림만 판단한다.
            var ray = new Ray(observer.transform.position, (target - observer.transform.position).normalized);
            var focusDistance = Vector3.Distance(observer.transform.position, target);
            foreach (var building in 사가정참고건물표현)
            {
                if (building == null) continue;
                var expanded = building.bounds; expanded.Expand(2);
                building.enabled = !follow || !expanded.IntersectRay(ray, out var hitDistance) || hitDistance >= focusDistance - .1f;
            }
        }

        private bool 사가정음식생활시설관찰(string id)
        {
            var anchor = id == "facility:sim.restaurant-1" || id == "restaurant" ? "restaurant"
                : id == "facility:synthetic:a" || id == "home-a" ? "home-a"
                : id == "facility:synthetic:b" || id == "home-b" ? "home-b"
                : id == "facility:synthetic:mart" || id == "mart" ? "mart" : null;
            if (anchor == null || 사가정생활공간 == null) return false;
            선택생활주체 = ""; 사가정선택시설 = 위치(사가정생활공간.Anchor(anchor), 1); follow = true;
            사가정음식생활카메라갱신(); return true;
        }

        /// <summary>게임 외관만 중립 표식으로 교체한다. 이름·주소·역사 기록과 Core 상태는 보존한다.</summary>
        public void 사가정음식생활외관표시(bool visible)
        { if (사가정음식점외관 != null) 사가정음식점외관.gameObject.SetActive(visible); }

        // 기존 Scene의 카메라, Canvas, OnGUI 입력만 잠시 격리한다.
        // Core/운영 Controller를 비활성화하거나 Scene을 저장하지 않는다.
        private void 사가정음식생활화면격리()
        {
            if (사가정화면복원중 || 사가정생활공간 == null) return;
            foreach (var item in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item != null && 사가정격리표현형식.Contains(item.GetType().FullName)) 사가정화면끄기(item);
            foreach (var item in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item != observer) 사가정화면끄기(item);
            foreach (var item in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!item.transform.IsChildOf(transform)) 사가정화면끄기(item);
        }

        private void 사가정화면끄기(Behaviour item)
        {
            if (item == null) return;
            if (!사가정기존화면상태.ContainsKey(item)) 사가정기존화면상태.Add(item, item.enabled);
            if (item.enabled) item.enabled = false;
        }

        private void 사가정음식생활종료()
        {
            사가정화면복원중 = true;
            // 카메라를 마지막에 복원한다. OnEnable로 다시 조립되는 View의 생명주기도 따른다.
            foreach (var item in 사가정기존화면상태.Where(p => !(p.Key is Camera)).ToArray())
                if (item.Key != null) item.Key.enabled = item.Value;
            foreach (var item in 사가정기존화면상태.Where(p => p.Key is Camera).ToArray())
                if (item.Key != null) item.Key.enabled = item.Value;
            사가정기존화면상태.Clear();
        }

        private static Vector3 위치(사가정음식생활점 p, float y) => new Vector3((float)p.x, y, (float)p.z);
    }
}
