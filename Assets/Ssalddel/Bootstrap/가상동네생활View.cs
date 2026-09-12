using System;
using System.Linq;
using System.Collections.Generic;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Application;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        private readonly Dictionary<string,Transform> 생활주체=new();
        private readonly Transform[] 창고재고표현=new Transform[2];
        private Transform 화물차,화물상자,창고입고물품,창고인계물품,마트입고물품,창고작업물품;
        private string 선택생활주체="";
        private bool 확장개요;

        private void BuildNeighborhoodLife()
        {
            사가정참고조립();
            var area = 동네좌표참고확장.구역;
            Block("같은 동네 · 좌표 참고 확장", new Vector3((float)area.x,-.16f,(float)area.z),
                new Vector3((float)area.size,.15f,(float)area.size), new Color(.25f,.34f,.27f));
            foreach (var point in 동네좌표참고확장.위치())
            {
                var marker = Block(point.id, new Vector3((float)point.x,2,(float)point.z), new Vector3(8,4,8), Color.white);
                Label(marker, point.name + "\n자료 위치 · 출입구 미확정", Color.black);
                var label = marker.GetComponentInChildren<TextMesh>();
                if (label != null) label.transform.localScale *= 5;
            }
            var floor=transform.Find("바닥");floor.position=new Vector3(0,-.12f,8);floor.localScale=new Vector3(90,.2f,58);
            배치도형("road:west","서쪽 보충 도로",0,.1f,Color.gray);
            var depot=배치도형("facility:synthetic:depot","보충창고",.1f,.2f,가상동네역할색상.창고);Label(depot,"보충창고",Color.black);
            Block("보충창고 후면",new Vector3(-26,.5f,-17),new Vector3(16,1,.2f),Color.white);
            foreach(var x in new[]{-26f,-14f})Block("하역 접근 "+x,new Vector3(x,.1f,-4),new Vector3(1.5f,.1f,8),new Color(.7f,.7f,.6f));
            foreach(var bay in new[]{new Vector3(-26,.12f,-3),new Vector3(-14,.12f,-3)})
                Block("화물 정차점 "+bay.x,bay,new Vector3(4.5f,.1f,2),new Color(.4f,.4f,.3f));
            foreach(var bay in new[]{new Vector3(-10,.12f,-2.5f),new Vector3(10,.12f,2.5f),new Vector3(10,.12f,22.5f),new Vector3(30,.12f,22.5f)})
                Block("배달 정차점 "+bay.x+" "+bay.z,bay,new Vector3(2,.1f,1),new Color(.4f,.4f,.3f));
            Block("마트 입고 내부 연결",new Vector3(-11,.1f,-14),new Vector3(6,.1f,1.5f),new Color(.7f,.7f,.6f));
            Block("마트 입고 수직 연결",new Vector3(-14,.1f,-11),new Vector3(1.5f,.1f,6),new Color(.7f,.7f,.6f));
            for(int i=0;i<2;i++){
                var rack=Block("보충 적재대 "+i,new Vector3(-30+i*4,.6f,-15),new Vector3(2,1.2f,1.5f),Color.white);Label(rack,"보관 "+(i+1),Color.black);
                창고재고표현[i]=Block("보충 재고 "+i,new Vector3(-30+i*4,1.5f,-15),Vector3.one,new Color(.8f,.6f,.35f));
            }
            Label(Block("보충 포장대",new Vector3(-22,.6f,-15),new Vector3(2,1.2f,1.5f),Color.white),"포장",Color.black);
            Label(Block("보충 인계대",new Vector3(-27,.6f,-8),new Vector3(1,1.2f,2),Color.white),"상차",Color.black);
            창고입고물품=Block("보충 입고 물품",new Vector3(-29,.6f,-8),new Vector3(2,1.2f,2),new Color(.8f,.6f,.35f));
            창고인계물품=Block("보충 인계 물품",new Vector3(-27,1.5f,-8),Vector3.one*.8f,new Color(.8f,.6f,.35f));
            마트입고물품=Block("마트 입고 물품",new Vector3(-15,.5f,-8),Vector3.one,new Color(.8f,.6f,.35f));
            foreach(var item in new[]{(가상동네생활기준.RestaurantActorId,"음식점 주인",가상동네역할색상.음식점),
                (가상동네생활기준.DepotWorkerId,"창고 작업자",Color.white),(가상동네생활기준.TruckDriverId,"화물 기사",Color.black),
                ("participant:synthetic:a","주민 A",가상동네역할색상.주문자),("participant:synthetic:b","주민 B",가상동네역할색상.주문자)})
            {var actor=Block(item.Item2,Vector3.zero,new Vector3(.6f,1.8f,.6f),item.Item3);Label(actor,item.Item2,item.Item3==Color.white?Color.black:Color.white);생활주체[item.Item1]=actor;}
            생활주체[가상동네생활기준.MartWorkerId]=martWorker;
            for(int i=0;i<3;i++)생활주체["actor:synthetic-courier:"+(i+1)]=fleetActors[i];
            화물차=Block("보충 화물차",new Vector3(-26,.9f,-3),new Vector3(4,1.8f,2),Color.black);Label(화물차,"화물",Color.white);
            화물상자=Block("화물차 적재 물품",Vector3.zero,new Vector3(2,1,1.6f),new Color(.8f,.6f,.35f));
            창고작업물품=Block("창고 작업자 물품",Vector3.zero,Vector3.one*.5f,new Color(.8f,.6f,.35f));
        }

        private void RenderNeighborhoodLife()
        {
            var life=state?.WaitingFleet?.NeighborhoodLife;
            if(life==null || 화물차==null)return;
            foreach(var actor in life.Actors)
                if(생활주체.TryGetValue(actor.ActorId,out var view) && actor.Role!="Courier" && actor.ActorId!=가상동네생활기준.MartWorkerId
                    && actor.ActorId!=가상동네생활기준.DepotWorkerId && actor.Role!="FreightDriver")view.position=new Vector3((float)actor.X,.9f,(float)actor.Z);
            var d=life.Depot;
            생활주체[가상동네생활기준.DepotWorkerId].position=new Vector3((float)d.X,.9f,(float)d.Z);
            생활주체[가상동네생활기준.TruckDriverId].position=new Vector3((float)d.Truck.X,.9f,(float)d.Truck.Z);
            화물차.position=new Vector3((float)d.Truck.VehicleX,.9f,(float)d.Truck.VehicleZ);
            화물상자.position=화물차.position+Vector3.up*1.2f;화물상자.gameObject.SetActive(d.Truck.Carrying);
            창고입고물품.gameObject.SetActive(d.Incoming.Sum()>0);
            창고인계물품.gameObject.SetActive(d.Shipments.Any(x=>x.ReadyTick.HasValue && !x.LoadedTick.HasValue));
            var inboundCarried=state.WaitingFleet.Mart.WorkerStage=="LifeInboundStore";
            마트입고물품.gameObject.SetActive(!inboundCarried && d.Shipments.Any(x=>x.UnloadedTick.HasValue && !x.ReceivedTick.HasValue));
            if(inboundCarried)workerParcel.gameObject.SetActive(true);
            창고작업물품.position=생활주체[가상동네생활기준.DepotWorkerId].position+Vector3.up*.6f;
            창고작업물품.gameObject.SetActive(d.Shipments.Any(x=>x.Id==d.ShipmentId && x.Cargo.Sum()>0 && !x.ReadyTick.HasValue));
            for(int i=0;i<2;i++){창고재고표현[i].gameObject.SetActive(d.Stock[i]>0);창고재고표현[i].localScale=new Vector3(1,Mathf.Min(3,d.Stock[i]*.1f),1);}
            if (life.DayEnabled)
                foreach (var actor in life.Actors)
                    if (생활주체.TryGetValue(actor.ActorId, out var actorView))
                    {
                        // 기존 도형을 이동한다. 차량/화물과 사람을 복제하거나 완료 상태를 변경하지 않는다.
                        actorView.position = new Vector3((float)actor.X,.9f,(float)actor.Z);
                        var label = actorView.GetComponentInChildren<TextMesh>();
                        if (label != null) label.text = Ssalddel.Unity.Observation.동네관찰Presenter.이름(actor.ActorId)
                            + "\n" + Ssalddel.Unity.Observation.동네관찰Presenter.단계(actor.Stage);
                    }
            동네입체카메라갱신();
        }

        private void 동네입체카메라갱신()
        {
            if (observer == null || state?.WaitingFleet?.NeighborhoodLife == null) return;
            var target = new Vector3(0, 0, 5);
            if (follow && 선택생활주체.Length > 0)
            {
                if (생활주체.TryGetValue(선택생활주체, out var chosen)) target = chosen.position;
                else { 선택생활주체 = ""; follow = false; }
            }
            else if (follow && selectedDriver >= 0 && selectedDriver < fleetActors.Length && fleetActors[selectedDriver] != null)
                target = fleetActors[selectedDriver].position;
            else if (follow) follow = false;
            var rotation = Ssalddel.Unity.Presentation.World.동네전체관찰CameraMath.기본회전;
            var bounds = new Bounds(new Vector3(0, 3, 8), new Vector3(90, 8, 58));
            if (확장개요 && !follow) {
                var area = 동네좌표참고확장.구역;
                bounds.Encapsulate(new Bounds(new Vector3((float)area.x,3,(float)area.z), new Vector3((float)area.size,8,(float)area.size)));
            }
            var wide=GetComponent<면목동전체자료View>();
            if(wide!=null&&wide.전체보기&&!follow)bounds.Encapsulate(wide.범위);
            var distance = follow ? 120 : Mathf.Max(120, bounds.extents.magnitude * 2);
            observer.farClipPlane = Mathf.Max(observer.farClipPlane, distance + bounds.size.magnitude);
            observer.transform.SetPositionAndRotation((follow ? target : bounds.center) - rotation * Vector3.forward * distance, rotation);
            observer.orthographicSize = follow ? 11 : Ssalddel.Unity.Presentation.World.동네전체관찰CameraMath.맞춤크기(bounds, rotation, Mathf.Max(.1f, observer.pixelRect.width / Mathf.Max(1, observer.pixelRect.height)));
        }

        private void 확장선택그리기()
        {
            if (state?.WaitingFleet?.NeighborhoodLife == null) return;
            if (확장개요) GUI.Label(new Rect(12, Screen.height - 78, 700, 28), "사가정역 중심 1km × 1km · © OpenStreetMap contributors · ODbL 1.0 · 폭/미상 높이는 기호");
            if (GUI.Button(new Rect(12, Screen.height - 44, 240, 32), 확장개요 ? "기존 동네 집중" : "사가정역 · 1km 확장 보기"))
            { 확장개요 = !확장개요; follow = false; 동네입체카메라갱신(); }
        }

    }
}
