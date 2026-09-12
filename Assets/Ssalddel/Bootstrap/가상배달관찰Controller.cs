using System;
using System.Linq;
using System.Threading.Tasks;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Warehouse;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>명시적 합성 프로필만 조립한다. 위치·주문 권위는 공통 Session에 있다.</summary>
    public sealed partial class 가상배달관찰Controller : MonoBehaviour
    {
        private SimulationWorldLocalRuntimeScope scope;
        private 경영SimulationSessionSnapshot state;
        private Transform courier, vehicle, parcel;
        private Transform martWorker, workerParcel, handoffParcel;
        private readonly Transform[] shelfGoods = new Transform[2];
        private readonly Transform[] fleetActors = new Transform[3], fleetVehicles = new Transform[3], fleetParcels = new Transform[3];
        private int selectedDriver;
        private Vector2 panelScroll;
        private string ActiveSlot => scope?.SlotStableId ?? 가상배달관찰표본.SlotId;
        private Camera observer;
        private bool playing, busy, follow;
        private bool 저장복구필요;
        private float elapsed;
        private string lastCourierPresentationSessionStableId = string.Empty;
        private long lastCourierPresentationSessionRevision = -1;
        private string lastCourierActorStableId = string.Empty;
        private 음식배달ActorPresentationModel courierPresentation;
        private 음식배달ActorPresentationModel lastVerifiedWorkflowPresentation;
        private bool singleCourierObservation;
        private bool stopAfterOneOrder = true;
        private string observedOrderStableId = string.Empty;
        private string message = "준비 중";
        private readonly System.Collections.Generic.List<Material> materials = new();
        private void OnApplicationPause(bool paused) { if (paused) { playing = false; elapsed = 0; 동네실행?.정지(); } }
        private void OnDestroy()
        {
            foreach (var mesh in 참고Meshes) if(mesh != null) { if(UnityEngine.Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            마트서버관찰종료();
            if (운영지도중) 운영지도카메라복원();
            업무서버관찰종료();
            동네실행?.Dispose();
            foreach (var material in materials)
                if (material != null) { if (UnityEngine.Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SimulationWorldLocalRuntimeScope.OfflineLifeRequested || !SimulationWorldLocalRuntimeScope.SyntheticDeliveryRequested
                || SceneManager.GetActiveScene().name != "SimulationWorldShell"
                || FindAnyObjectByType<가상배달관찰Controller>() != null) return;
            new GameObject("가상 동네 배달 관찰").AddComponent<가상배달관찰Controller>();
        }

        private async void Start()
        {
            if (SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested) { 업무서버관찰시작(); return; }
            try
            {
                scope = SimulationWorldLocalRuntimeScope.Install();
                singleCourierObservation = scope.LaunchProfile == Nature생존LaunchProfile.SyntheticDelivery;
                state = await scope.Runtime.Sessions.GetAsync(scope.SessionStableId);
                Build();
                if (Render()) message = "정지 · 재생을 눌러 시작하세요. 종료 시간은 따라잡지 않습니다.";
                if (scope.LaunchProfile == Nature생존LaunchProfile.NeighborhoodLife) 휴대폰연결();
            }
            catch (Exception e) { message = "시작 실패: " + e.Message; Debug.LogException(e); }
        }

        private async void Update()
        {
            if (SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested) { 업무서버관찰갱신(); return; }
            if (동네실행 != null) { await 동네실행.시간진행Async(Time.unscaledDeltaTime); return; }
            if (!playing || busy || 저장복구필요 || state == null || state.IsCompleted) { elapsed = 0; return; }
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f);
            if (elapsed < 1f) return;
            elapsed = 0;
            await Tick();
        }

        private async Task Tick()
        {
            if (busy || 저장복구필요 || state == null || state.IsCompleted) return;
            busy = true;
            try
            {
                state = await scope.Runtime.Sessions.AdvanceWorldTickAsync(state.SessionStableId,
                    new 경영SimulationTick진행Request { CommandId = "command:observer:" + Guid.NewGuid().ToString("N"), ExpectedRevision = state.Revision, TickCount = 1 });
                // 명령 성공 후 같은 원장을 재조회한다. 저장 실패는 숨기지 않고 재생을 멈춘다.
                state = await scope.Runtime.Sessions.GetAsync(state.SessionStableId);
                await scope.Runtime.Sessions.SaveSlotAsync(state.SessionStableId,
                    new SimulationLocalSaveSlotRequest { SlotStableId = ActiveSlot, ExpectedRevision = state.Revision });
                if (this != null)
                {
                    if (Render() && !TryStopAfterObservedOrder()) message = "저장 완료";
                }
            }
            catch (Exception e) { playing = false; 저장복구필요 = true; message = "진행/저장 실패 · 저장 재시도 필요: " + e.Message; Debug.LogException(e); }
            finally { busy = false; elapsed = 0; }
        }

        private void OnGUI()
        {
            확장선택그리기();
            if (SimulationWorldLocalRuntimeScope.NeighborhoodLifeRequested || SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested) return;
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(360, Screen.width-24), Mathf.Min(500, Screen.height-24)), GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("가상 동네 · 음식점/살뜰마트 → 주택 A/B");
            GUILayout.Label("합성 자료 / 실제 주문·정산 없음");
            GUILayout.Label("빨강 음식점 · 파랑 주문자 · 하양 창고 · 검정 기사");
            GUI.enabled = state != null && !state.IsCompleted && !busy && !저장복구필요;
            if (GUILayout.Button(playing ? "일시정지" : "재생")) { playing = !playing; elapsed = 0; }
            if (GUILayout.Button("1초 진행")) { playing = false; _ = Tick(); }
            GUI.enabled = state != null && !busy;
            if (GUILayout.Button("저장 · 실패 시 다시 시도")) _ = Save();
            if (GUILayout.Button(follow ? "전체 보기" : "기사 따라가기")) { follow = !follow; Render(); }
            if (singleCourierObservation && GUILayout.Button(stopAfterOneOrder ? "한 건 관찰: 켬" : "한 건 관찰: 끔"))
            {
                stopAfterOneOrder = !stopAfterOneOrder;
                observedOrderStableId = stopAfterOneOrder
                    ? state?.SyntheticCourier?.OrderStableId?.Trim() ?? string.Empty
                    : string.Empty;
            }
            if (state != null && state.IsCompleted && GUILayout.Button("새 표본 시작 · 전용 저장 교체")) _ = Restart();
            GUI.enabled = true;
            GUILayout.Label(message);
            if (state != null)
            {
                GUILayout.Label($"시간 {state.CurrentTick}/{state.DurationTicks}초 · 묶음 {state.WaitingFleet?.Batch ?? state.SyntheticCourier?.Batch ?? 0}");
                GUILayout.Label("기사: " + (state.SyntheticCourier?.Stage ?? "준비"));
                if (singleCourierObservation && courierPresentation != null)
                {
                    GUILayout.Label("현재 행동: " + courierPresentation.CourierActivityDisplayName);
                    GUILayout.Label($"업무 흐름 엔진: {courierPresentation.WorkflowEngineCode} · 분류 {courierPresentation.WorkflowEngineClassificationPrimaryCode} · 보조 {string.Join(',', courierPresentation.WorkflowEngineClassificationSupportCodes)}");
                    GUILayout.Label("읽기 전용 의미 · 주문/배차/배송 실행 권위 없음");
                    var workflow = string.Equals(courierPresentation.WorkflowObservationStatusCode,
                            음식배달ActorPresentationProjector.WorkflowObservationVerified, StringComparison.Ordinal)
                        ? courierPresentation
                        : lastVerifiedWorkflowPresentation;
                    if (workflow == null)
                    {
                        GUILayout.Label("최근 확정 전이: 대기/미관측");
                    }
                    else
                    {
                        GUILayout.Label($"최근 확정 전이: {workflow.LatestTransitionFromStateCode} → {workflow.LatestTransitionToStateCode} · {workflow.LatestTransitionWorldTick}초");
                        GUILayout.Label($"업무 의미: {workflow.WorkflowModuleCode} / 분류 {workflow.WorkflowModuleClassificationPrimaryCode} · {workflow.WorkflowMeaningRevision}");
                        GUILayout.Label($"규칙 {workflow.WorkflowRuleRevision} · 주문 {workflow.OrderStableId} · Session r{workflow.SessionRevision}");
                    }
                    GUILayout.Label("기사 표현 단계와 주문 확정 전이는 서로 다른 관찰 시점입니다.");
                    GUILayout.Label("창고 TAE는 이번 음식 배달 흐름에서 사용하지 않습니다.");
                }
                if (state.WaitingFleet != null)
                {
                    GUILayout.Label(state.WaitingFleet.NeighborhoodLife!=null ? "30분 생활 / 구간·출입구별 순차 통행" : "마트 주변·도로변 대기 / 통행 묶음 1대 제한");
                    for (int i=0;i<state.WaitingFleet.Drivers.Length;i++) {
                        var d=state.WaitingFleet.Drivers[i];
                        if(GUILayout.Button($"기사 {i+1}: {d.Courier.Stage} · {d.WaitReason}")) {selectedDriver=i;선택생활주체="";follow=true;Render();}
                    }
                    GUILayout.Label(state.WaitingFleet.Decision);
                    var mart = state.WaitingFleet.Mart;
                    if (mart != null) {
                        GUILayout.Label($"마트 작업자: {MartStage(mart.WorkerStage)} · 재고 {mart.Stock[0]}/{mart.Stock[1]} · 예약 {mart.Reserved[0]}/{mart.Reserved[1]}");
                        foreach (var order in mart.Orders.Skip(state.WaitingFleet.NeighborhoodLife!=null?Math.Max(0,mart.Orders.Length-5):0))
                            GUILayout.Label($"{order.OrderId} · {MartStage(order.Stage)} · {order.WaitReason}");
                    }
                    var flow = state.WaitingFleet.OrderFlow;
                    if (flow != null) {
                        GUILayout.Label("주문 원장 → 음식점 수락 → 배차 대기");
                        foreach (var person in flow.Orderers)
                            GUILayout.Label($"주문자 {person.Residence.ToUpperInvariant()} · 다음 차례 {person.NextTick}초 · 미완료 주문이 있으면 건너뜀");
                        foreach (var entry in flow.Entries.Skip(state.WaitingFleet.NeighborhoodLife!=null?Math.Max(0,flow.Entries.Length-5):0))
                            GUILayout.Label($"{entry.OrderId} · {주문연결표시(entry.State)} · {entry.WaitReason}");
                    }
                }
                foreach (var order in state.FoodDeliveries.Skip(state.WaitingFleet?.NeighborhoodLife!=null?Math.Max(0,state.FoodDeliveries.Length-5):0))
                    GUILayout.Label(order.FoodOrderStableId.Replace("food-order:synthetic:", "주문 ") + " · " + order.StateCode);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        private void Build()
        {
            Block("바닥", new Vector3(12,-.12f,8), new Vector3(58,.2f,58), new Color(.3f,.45f,.28f));
            배치도형("road:east", "도로 동서", 0,.1f,Color.gray);
            배치도형("road:trunk", "도로 남북", 0,.1f,Color.gray);
            배치도형("road:homes", "주택 도로", 0,.1f,Color.gray);
            배치건물("facility:sim.restaurant-1","음식점",가상동네역할색상.음식점);
            배치건물("facility:synthetic:a","주택 A",가상동네역할색상.주문자);
            배치건물("facility:synthetic:b","주택 B",가상동네역할색상.주문자);
            if (SimulationWorldLocalRuntimeScope.OrderFlowRequested) {
                Label(transform.Find("음식점"), "수신함", Color.white);
                Label(transform.Find("주택 A"), "주문자 A", Color.white);
                Label(transform.Find("주택 B"), "주문자 B", Color.white);
            }
            courier = Block("기사", new Vector3(0,.9f,0), new Vector3(.6f,1.8f,.6f), 가상동네역할색상.배송기사);
            vehicle = Block("배달 차량", new Vector3(0,.5f,0), new Vector3(1.2f,1,2.4f), 가상동네역할색상.배송기사);
            Label(courier, "1", Color.white);
            parcel = Block("음식 상자", new Vector3(0,1.5f,0), Vector3.one*.35f, new Color(.8f,.6f,.35f));
            observer = new GameObject("배달 관찰 카메라").AddComponent<Camera>();
            observer.transform.SetParent(transform); observer.depth = 100; observer.orthographic = true;
            observer.clearFlags = CameraClearFlags.SolidColor; observer.backgroundColor = new Color(.5f,.65f,.8f);
            observer.cullingMask = 1 << 31;
            observer.nearClipPlane = .1f; observer.farClipPlane = 300;
            if (SimulationWorldLocalRuntimeScope.WaitingFleetRequested)
            {
                fleetActors[0]=courier; fleetVehicles[0]=vehicle; fleetParcels[0]=parcel;
                for(int i=1;i<3;i++) {
                    fleetActors[i]=Block("기사 "+(i+1),Vector3.zero,new Vector3(.6f,1.8f,.6f),가상동네역할색상.배송기사);
                    fleetVehicles[i]=Block("배달 차량 "+(i+1),Vector3.zero,new Vector3(1.2f,1,2.4f),가상동네역할색상.배송기사);
                    Label(fleetActors[i], (i+1).ToString(), Color.white);
                    fleetParcels[i]=Block("음식 상자 "+(i+1),Vector3.zero,Vector3.one*.35f,new Color(.8f,.6f,.35f));
                }
                if (SimulationWorldLocalRuntimeScope.MartCenterRequested) BuildMart();
                else Block("살뜰마트 대기 거점",new Vector3(-3,2,-8),new Vector3(10,4,4),가상동네역할색상.창고);
                var slots=가상동네배치기준.대기점().Select(p=>new Vector3((float)p.x,0,(float)p.z)).ToArray();
                for(int i=0;i<slots.Length;i++) {
                    Block((i<3?"마트 대기 ":"도로변 대기 ")+i,slots[i]+Vector3.up*.08f,new Vector3(3,.1f,3),new Color(.6f,.65f,.4f));
                    var target=i<3?new Vector3(i==2?10:0,0,0):new Vector3(slots[i].x,0,20);
                    var connector=Block("정차 연결 "+i,(slots[i]+target)/2+Vector3.up*.03f,new Vector3(2,.05f,Vector3.Distance(slots[i],target)),Color.gray);
                    connector.rotation=Quaternion.LookRotation(target-slots[i]);
                }
            }
            if(SimulationWorldLocalRuntimeScope.NeighborhoodLifeRequested) BuildNeighborhoodLife();
        }
        private void Building(string title,float x,float z,Color color)
        {
            var open=SimulationWorldLocalRuntimeScope.NeighborhoodLifeRequested && title=="음식점";
            Block(title,new Vector3(x,open ? .1f : 2f,z),new Vector3(6,open ? .2f : 4f,6),color);
            Block(title+" 출입구",new Vector3(x,1,z-3.05f),new Vector3(1,2,.1f),Color.white);
            Block(title+" 접근로",new Vector3(x,.07f,z-5),new Vector3(1.5f,.1f,4),new Color(.7f,.7f,.6f));
        }
        private async Task Restart()
        {
            if (busy || 저장복구필요) return;
            busy = true; playing = false; elapsed = 0;
            try
            {
                state = await scope.Runtime.Sessions.CreateAsync(scope.LaunchProfile == Nature생존LaunchProfile.NeighborhoodLife
                    ? 가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid(), dayEnabled: true) : scope.LaunchProfile == Nature생존LaunchProfile.OrderFlow
                    ? 가상배달관찰표본.CreateOrderFlow(Guid.NewGuid()) : scope.LaunchProfile == Nature생존LaunchProfile.MartCenter
                    ? 가상배달관찰표본.CreateMart(Guid.NewGuid()) : scope.LaunchProfile == Nature생존LaunchProfile.WaitingFleet
                    ? 가상배달관찰표본.CreateWaiting(Guid.NewGuid()) : 가상배달관찰표본.Create(Guid.NewGuid()));
                lastCourierPresentationSessionStableId = string.Empty;
                lastCourierPresentationSessionRevision = -1;
                lastCourierActorStableId = string.Empty;
                courierPresentation = null;
                lastVerifiedWorkflowPresentation = null;
                observedOrderStableId = string.Empty;
                await scope.Runtime.Sessions.SaveSlotAsync(state.SessionStableId,
                    new SimulationLocalSaveSlotRequest { SlotStableId = ActiveSlot, ExpectedRevision = state.Revision });
                if (Render()) message = "새 표본 준비 · 재생을 누르세요.";
            }
            catch (Exception error) { 저장복구필요 = true; message = "새 표본 시작 실패: " + error.Message; }
            finally { busy = false; }
        }

        private bool TryStopAfterObservedOrder()
        {
            if (!singleCourierObservation || !stopAfterOneOrder
                || state?.SyntheticCourier == null
                || string.IsNullOrWhiteSpace(observedOrderStableId)
                || !string.Equals(state.SyntheticCourier.Stage, "Idle", StringComparison.Ordinal))
            {
                return false;
            }

            var matches = (state.FoodDeliveries ?? Array.Empty<Simulation음식배달Snapshot>())
                .Where(order => order != null && string.Equals(order.FoodOrderStableId,
                    observedOrderStableId, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length != 1)
            {
                playing = false;
                elapsed = 0;
                message = "한 건 관찰 차단: 완료 주문 식별 불일치";
                return true;
            }
            if (matches[0].ReceivedTick == null) return false;

            playing = false;
            elapsed = 0;
            message = "한 건 관찰 완료 · 수령 확인 뒤 기사 복귀 · 재생하면 다음 주문을 이어갑니다.";
            return true;
        }
        private async Task Save()
        {
            if (busy || state == null) return;
            busy = true; playing = false; elapsed = 0;
            try
            {
                state = await scope.Runtime.Sessions.GetAsync(state.SessionStableId);
                await scope.Runtime.Sessions.SaveSlotAsync(state.SessionStableId,
                    new SimulationLocalSaveSlotRequest { SlotStableId = ActiveSlot, ExpectedRevision = state.Revision });
                저장복구필요 = false;
                if (Render()) message = "저장 완료";
            }
            catch (Exception error) { 저장복구필요 = true; message = "저장 실패: " + error.Message; }
            finally { busy = false; }
        }
        private Transform Block(string title,Vector3 position,Vector3 size,Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.layer = 31; item.GetComponent<Collider>().enabled = false;
            item.name = title; item.transform.SetParent(transform); item.transform.position = position; item.transform.localScale = size;
            var shader = Resources.Load<Shader>("관찰도형명암") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader); materials.Add(material); material.color = color; item.GetComponent<Renderer>().sharedMaterial = material;
            return item.transform;
        }
        private bool Render()
        {
            if (observer == null) return false;
            var data = state.SyntheticCourier;
            if (data == null) { courier.position = new Vector3(0,.9f,0); vehicle.position = new Vector3(0,.5f,0); parcel.gameObject.SetActive(false); }
            if (data != null)
            {
                if (!음식배달ActorPresentationProjector.TryProject(
                    state,
                    data,
                    lastCourierPresentationSessionStableId,
                    lastCourierPresentationSessionRevision,
                    lastCourierActorStableId,
                    out var actorPresentation,
                    out var failureCode))
                {
                    playing = false;
                    message = "배달기사 표현 차단: " + failureCode;
                    return false;
                }
                courierPresentation = actorPresentation;
                if (string.Equals(actorPresentation.WorkflowObservationStatusCode,
                    음식배달ActorPresentationProjector.WorkflowObservationVerified, StringComparison.Ordinal))
                {
                    lastVerifiedWorkflowPresentation = actorPresentation;
                }
                if (singleCourierObservation && stopAfterOneOrder
                    && string.IsNullOrWhiteSpace(observedOrderStableId)
                    && !string.IsNullOrWhiteSpace(actorPresentation.OrderStableId))
                {
                    observedOrderStableId = actorPresentation.OrderStableId;
                }
                courier.position = new Vector3((float)actorPresentation.ActorX,.9f,(float)actorPresentation.ActorZ);
                vehicle.position = new Vector3((float)actorPresentation.VehicleX,.5f,(float)actorPresentation.VehicleZ);
                parcel.position = courier.position + Vector3.up*.6f; parcel.gameObject.SetActive(actorPresentation.Carrying);
                lastCourierPresentationSessionStableId = actorPresentation.SessionStableId;
                lastCourierPresentationSessionRevision = actorPresentation.SessionRevision;
                lastCourierActorStableId = actorPresentation.ActorStableId;
            }
            if(state.WaitingFleet != null)
                for(int i=0;i<state.WaitingFleet.Drivers.Length;i++) {
                    var d=state.WaitingFleet.Drivers[i].Courier;
                    if(fleetActors[i]==null) continue;
                    fleetActors[i].position=new Vector3((float)d.X,.9f,(float)d.Z);
                    fleetVehicles[i].position=new Vector3((float)d.VehicleX,.5f,(float)d.VehicleZ);
                    fleetParcels[i].position=fleetActors[i].position+Vector3.up*.6f;fleetParcels[i].gameObject.SetActive(d.Carrying);
                }
            var mart = state.WaitingFleet?.Mart;
            var flow = state.WaitingFleet?.OrderFlow;
            if (flow != null) {
                var inbox = transform.Find("음식점/표시 수신함")?.GetComponent<TextMesh>();
                if (inbox != null) inbox.text = "미확인 " + flow.Entries.Count(x => x.SourceKind == "Restaurant" && x.ResponseCode == "Pending");
                foreach (var person in flow.Orderers) {
                    var label = transform.Find("주택 " + person.Residence.ToUpperInvariant() + "/표시 주문자 " + person.Residence.ToUpperInvariant())?.GetComponent<TextMesh>();
                    var order = state.FoodDeliveries.LastOrDefault(x => x.OrdererStableId == "participant:synthetic:" + person.Residence);
                    if (label != null) label.text = "주문자 " + person.Residence.ToUpperInvariant() + "\n" + (order?.StateCode ?? "주문 예정");
                }
            }
            if (mart != null && martWorker != null) {
                martWorker.position = new Vector3((float)mart.X,.9f,(float)mart.Z);
                var active = Array.Find(mart.Orders, x => x.OrderId == mart.WorkerOrderId);
                workerParcel.position = martWorker.position + Vector3.up*.6f;
                workerParcel.gameObject.SetActive(active != null && active.PickedUnits > 0 && !active.ReadyTick.HasValue);
                handoffParcel.gameObject.SetActive(Array.Exists(mart.Orders, x => x.ReadyTick.HasValue && !x.PickedUpTick.HasValue));
                for(int i=0;i<2;i++) {
                    shelfGoods[i].gameObject.SetActive(mart.Stock[i] > 0);
                    shelfGoods[i].localScale = new Vector3(.7f,.3f*mart.Stock[i],.7f);
                }
            }
            var target = follow ? (state.WaitingFleet != null && fleetActors[selectedDriver]!=null ? fleetActors[selectedDriver].position : courier.position) : new Vector3(12,0,5);
            observer.orthographicSize = follow ? 10 : 36;
            observer.transform.position = target + Ssalddel.Unity.Presentation.World.동네전체관찰CameraMath.근접관찰오프셋; observer.transform.LookAt(target);
            RenderNeighborhoodLife();
            return true;
        }

        private static string MartStage(string value) => value switch {
            "Pending" => "접수/재고 대기", "Picking" => "피킹 중", "Packing" => "포장 중", "Packed" => "포장 완료",
            "Ready" => "기사 인계 대기", "Assigned" => "기사 도착", "PickedUp" => "기사 픽업", "Delivered" => "전달 완료",
            "Received" => "수령 완료", "Idle" => "대기", "WalkShelf" => "적재대로 이동", "Pick" => "상품 수집",
            "WalkPack" => "포장대로 이동", "Pack" => "포장", "WalkHandoff" => "인계대로 이동", "Return" => "복귀", _ => value };

        private static string 주문연결표시(string value) => value switch {
            "Inbox" => "음식점 확인 대기", "Accepted" => "수락·배차 등록 예정", "Rejected" => "거절",
            "Preparation" => "피킹·포장 대기", "WaitingDispatch" => "배차 대기", "Assigned" => "기사 배정", "Completed" => "수령 완료", _ => value };

        private void BuildMart()
        {
            배치도형("facility:synthetic:mart","살뜰마트 배송센터",.08f,.16f,가상동네역할색상.창고);
            Block("마트 후면",new Vector3(-3,.4f,-17),new Vector3(18,.8f,.2f),가상동네역할색상.창고);
            Block("마트 접근로",new Vector3(-10,.1f,-4),new Vector3(1.5f,.1f,8),new Color(.7f,.7f,.6f));
            마트내부조립();
        }

        private void Label(Transform parent, string text, Color color)
        {
            var item = new GameObject("표시 "+text); item.layer=31;
            item.transform.position = parent.position+Vector3.up*1.5f;
            item.transform.rotation = Ssalddel.Unity.Presentation.World.동네전체관찰CameraMath.기본회전;
            item.transform.SetParent(parent,true);
            var label=item.AddComponent<TextMesh>(); label.text=text; label.color=color;
            label.fontSize=48; label.characterSize=.12f; label.anchor=TextAnchor.MiddleCenter;
        }
    }
}
