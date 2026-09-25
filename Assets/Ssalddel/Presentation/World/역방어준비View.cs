using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>전투 없는 준비 화면. 모든 배치 결과는 주입된 Core 사본으로 재표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class 역방어준비View : MonoBehaviour
    {
        private 사가정운영디오라마View map;
        private 운영관찰WorldView operations;
        private I역방어준비Runtime runtime;
        private 역방어배치자료 catalog;
        private GameObject visualRoot,unit;
        private readonly Dictionary<string,Renderer> markers=new();
        private readonly List<Material> materials=new();
        private string selectedId="",commandId="",status="준비 화면을 열어 주세요.";
        private long previewRevision;
        private GUIStyle titleStyle,textStyle;
        public bool IsOpen {get;private set;}
        public string Readiness {get;private set;}="NotLoaded";
        public 역방어준비Snapshot Snapshot=>runtime?.Read();
        public string SelectedId=>selectedId;
        public Transform UnitTransform=>unit!=null?unit.transform:null;

        public 역방어배치자료 LoadCatalog()
        {
            map=GetComponent<사가정운영디오라마View>();
            operations=GetComponent<운영관찰WorldView>();
            try
            {
                var source=Resources.Load<TextAsset>("SagajeongReference");
                var asset=Resources.Load<TextAsset>("StationDefensePlacement");
                if(source==null||asset==null)throw new ArgumentException("DefenseAssetMissing");
                var value=JsonUtility.FromJson<역방어배치자료>(asset.text);
                역방어배치검사.Validate(value,JsonUtility.FromJson<공간참고자료>(source.text),source.bytes);
                catalog=value;Readiness="Ready";return catalog;
            }
            catch(ArgumentException){Readiness="PlacementRejected";catalog=null;return null;}
        }
        public void Configure(I역방어준비Runtime value)
        {
            runtime=value;
            if(value==null){SetOpen(false);selectedId="";ReleaseVisuals();}
        }
        public void SetOpen(bool value)
        {
            if(value&&(runtime==null||catalog==null||map==null||map.MapStatus!="Ready"))
            {status="지도 또는 준비 자료를 사용할 수 없습니다.";return;}
            IsOpen=value;
            if(map!=null){map.GameOverlayVisible=value;if(value)map.Show(true);}
            operations?.SetPresentationVisible(!value);
            if(value){BuildVisuals();Refresh();map.ShowWhole();}
            if(visualRoot!=null)visualRoot.SetActive(value);
        }
        public bool SelectSlot(string id)
        {
            if(!IsOpen||runtime==null)return false;
            var before=runtime.Read();var preview=runtime.Preview(before.SessionId,id,before.Revision);
            if(!preview.Accepted){status=preview.Code;return false;}
            selectedId=id;previewRevision=before.Revision;commandId=Guid.NewGuid().ToString("N");
            var slot=catalog.slots.Single(x=>x.id==id);
            map.FocusPoint(transform.TransformPoint(new Vector3(slot.x,slot.height,slot.z)));
            status="미리보기 · 아직 배치하지 않았습니다.";Refresh();return true;
        }
        public bool Confirm()
        {
            if(!IsOpen||runtime==null||string.IsNullOrEmpty(selectedId))return false;
            var before=runtime.Read();
            var result=runtime.Assign(before.SessionId,commandId,selectedId,previewRevision);
            status=result.Accepted?"배치 완료 · 가상 방어 인원 1명":$"배치 보류: {result.Code} · 자리를 다시 선택해 주세요.";
            Refresh();return result.Accepted;
        }
        public void CancelSelection(){selectedId="";status="선택 취소 · 배치 상태는 유지됩니다.";Refresh();}
        private void BuildVisuals()
        {
            if(visualRoot!=null)return;
            visualRoot=new GameObject("역 방어 게임 레이어 · 준비 모판");visualRoot.transform.SetParent(transform,false);
            foreach(var slot in catalog.slots)
            {
                var go=Shape(PrimitiveType.Cylinder,slot.label+" · 가상 자리",new Vector3(slot.x,slot.height+.3f,slot.z),new Vector3(12,.2f,12),Color.cyan);
                markers.Add(slot.id,go.GetComponent<Renderer>());
            }
            unit=Shape(PrimitiveType.Capsule,"방어 인원 · 확대 기호",Vector3.zero,new Vector3(3,3,3),new Color(.12f,.48f,1));
            unit.SetActive(false);
        }
        private GameObject Shape(PrimitiveType kind,string label,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(kind);go.name=label;go.layer=사가정운영디오라마View.ObservationLayer;
            go.transform.SetParent(visualRoot.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var shader=Resources.Load<Shader>("관찰도형명암")??Shader.Find("Universal Render Pipeline/Lit");
            var material=new Material(shader){color=color};materials.Add(material);go.GetComponent<Renderer>().sharedMaterial=material;
            go.GetComponent<Collider>().enabled=false;return go;
        }
        private void Refresh()
        {
            if(runtime==null||visualRoot==null)return;
            var snapshot=runtime.Read();
            foreach(var slot in catalog.slots)
            {
                var color=slot.id==snapshot.AssignedSlotId?new Color(.12f,.48f,1):slot.id==selectedId?Color.yellow:Color.cyan;
                var properties=new MaterialPropertyBlock();properties.SetColor("_Color",color);properties.SetColor("_BaseColor",color);
                markers[slot.id].SetPropertyBlock(properties);
            }
            var assigned=catalog.slots.FirstOrDefault(x=>x.id==snapshot.AssignedSlotId);
            unit.SetActive(assigned!=null);
            if(assigned!=null)unit.transform.localPosition=new Vector3(assigned.x,assigned.height+3.5f,assigned.z);
        }
        private void OnGUI()
        {
            if (세계지도표현상태.GlobeOverlayVisible) return;
            GUI.depth=-110;
            if(map==null||!사가정운영디오라마View.IsVisible)return;
            if(!IsOpen)
            {
                GUI.enabled=runtime!=null&&Readiness=="Ready";
                if(GUI.Button(new Rect(Screen.width-240,52,226,34),"역 방어 준비"))SetOpen(true);
                GUI.enabled=true;return;
            }
            titleStyle??=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};
            textStyle??=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};
            GUI.Box(new Rect(12,12,332,420),GUIContent.none);
            GUI.Label(new Rect(24,22,308,34),"사가정역 방어 준비",titleStyle);
            GUI.Label(new Rect(24,62,308,52),"지휘형 준비 모판 · 전투는 아직 없습니다.\n옥상은 가상 배치 후보 · 실제 출입 정보 아님",textStyle);
            var state=runtime.Read();var assigned=catalog.slots.FirstOrDefault(x=>x.id==state.AssignedSlotId);
            GUI.Label(new Rect(24,120,308,48),$"방어 인원: 1명  |  배치: {(assigned==null?"대기":assigned.label)}\n판본 {state.Revision} · 배치 기록 {state.ActionCount}건",textStyle);
            for(var i=0;i<catalog.slots.Length;i++)
                if(GUI.Button(new Rect(24+i*156,178,148,36),catalog.slots[i].label+" 선택"))SelectSlot(catalog.slots[i].id);
            GUI.Label(new Rect(24,224,308,54),status,textStyle);
            GUI.enabled=!string.IsNullOrEmpty(selectedId)&&state.AssignedSlotId!=selectedId;
            if(GUI.Button(new Rect(24,284,148,38),"배치 확인"))Confirm();
            GUI.enabled=true;
            if(GUI.Button(new Rect(180,284,148,38),"선택 취소"))CancelSelection();
            GUI.Label(new Rect(24,336,308,84),"파랑: 배치 완료  ·  노랑: 선택 미리보기\n다른 자리에 재배치하면 이전 자리는 비워집니다.\n실제 주민·업무·재화에는 영향이 없습니다.",textStyle);
            if(GUI.Button(new Rect(Screen.width-240,12,110,34),"전체 보기"))map.ShowWhole();
            if(GUI.Button(new Rect(Screen.width-124,12,110,34),"운영 관찰"))SetOpen(false);
            GUI.Box(new Rect(12,Screen.height-70,410,58),GUIContent.none);
            GUI.Label(new Rect(24,Screen.height-64,388,48),"© OpenStreetMap contributors · ODbL 1.0\n표시는 확대 기호 · 현재 범위: 사가정 1km 동결 지도",textStyle);
            foreach(var slot in catalog.slots)
            {
                var point=map.ObservationCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(slot.x,slot.height+10,slot.z)));
                var rect=new Rect(point.x-60,Screen.height-point.y-30,120,28);
                if(point.z>0&&rect.xMin>352&&rect.xMax<Screen.width-12&&rect.yMin>52&&rect.yMax<Screen.height-80)
                    if(GUI.Button(rect,slot.label+(state.AssignedSlotId==slot.id?" · 배치됨":" · 후보")))SelectSlot(slot.id);
            }
        }
        private void ReleaseVisuals()
        {
            if(visualRoot!=null){visualRoot.SetActive(false);DestroyOwned(visualRoot);}
            visualRoot=null;unit=null;markers.Clear();
            foreach(var material in materials)if(material!=null)DestroyOwned(material);materials.Clear();
        }
        private static void DestroyOwned(UnityEngine.Object value)
        {if(UnityEngine.Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        private void OnDisable(){SetOpen(false);ReleaseVisuals();}
        private void OnDestroy(){ReleaseVisuals();}
    }
}
