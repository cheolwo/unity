using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Bootstrap
{
    // 같은 관찰 Root/카메라 위의 읽기 전용 표시. Collider·업무 객체·새 Camera/Scene을 만들지 않는다.
    public sealed class 면목동주소연결View : MonoBehaviour
    {
        public const string LocalPath="C:/Users/user/source/repos/Hongdal/artifacts/local/public-data/myeonmok-spatial-link-20260908-r1/connection.json";
        public const string BusinessPath="C:/Users/user/source/repos/Hongdal/artifacts/local/public-data/myeonmok-business-20260908-r1/connection.json";
        public const string ProjectionPath="C:/Users/user/source/repos/Hongdal/artifacts/local/public-data/myeonmok-diorama-view-r1/presentation-projection.json";
        private readonly List<Material> markerMaterials=new List<Material>();
        private 주소공간연결자료 data;
        private 면목동디오라마표현자료 projection;
        private Camera observer;
        private readonly Dictionary<string,Vector3> points=new Dictionary<string,Vector3>(StringComparer.Ordinal);
        private bool open,history,detailedReview,showRoads=true,showBuildings=true,showActivities=true;
        private GameObject activityLayerRoot;
        private Vector2 scroll;
        private string filter="",error="",selected="";
        public int ConnectedRecords=>projection?.counts?.activityObservations??0;
        public int ConnectedBuildings=>projection?.buildingOverlays?.Length??0;
        public string[] ConnectedRecordIds()=>data?.groups.SelectMany(g=>g.records).Select(r=>r.id).ToArray()??Array.Empty<string>();
        public void Initialize(공간참고자료 map,byte[] mapBytes,Camera camera)
        {
            if(data!=null||points.Count!=0)throw new InvalidOperationException("AddressViewAlreadyInitialized");
            observer=camera;
#if UNITY_EDITOR
            try
            {
                projection=JsonUtility.FromJson<면목동디오라마표현자료>(Encoding.UTF8.GetString(면목동디오라마표현검사.ReadFrozen(ProjectionPath,mapBytes)));
                면목동디오라마표현검사.Validate(projection,map);
                var candidate=JsonUtility.FromJson<주소공간연결자료>(Encoding.UTF8.GetString(주소공간연결검사.ReadFrozen(BusinessPath,mapBytes,true)));
                주소공간연결검사.Validate(candidate,map,true);
                var byId=map.buildings.ToDictionary(x=>x.id,StringComparer.Ordinal);
                // 표시용 경계상자 가운데. 오목 외곽 내부/실제 출입구/접지의 증명이 아니다.
                foreach(var overlay in projection.buildingOverlays)
                {
                    var b=byId[overlay.buildingRef];
                    points.Add(overlay.buildingRef,new Vector3((b.points.Min(p=>p.x)+b.points.Max(p=>p.x))*.5f,b.height+2,
                        (b.points.Min(p=>p.z)+b.points.Max(p=>p.z))*.5f));
                }
                data=candidate;error="";
                if(UnityEngine.Application.isPlaying)BuildMarkers(map);
            }
            catch(Exception e){points.Clear();data=null;projection=null;error="주소 레이어 보류: "+e.Message;Debug.LogWarning(error);}
#else
            error="비공개 주소 검토는 로컬 Editor 전용입니다.";
#endif
        }
        private static string Kind(string kind)=>kind=="shop"?"상가":kind=="factory"?"제조업":kind=="restaurant"?"음식점":kind=="housing"?"공동주택":kind=="clinic"?"의원/치과/한의원":kind=="hospital"?"병원":kind=="traditional-market-source-observation"?"전통시장":"공공시설";
        private void BuildMarkers(공간참고자료 map)
        {
            var byId=map.buildings.ToDictionary(b=>b.id,StringComparer.Ordinal);
            activityLayerRoot=new GameObject("면목동 관측 활동 레이어");
            activityLayerRoot.layer=31;
            activityLayerRoot.transform.SetParent(transform,false);
            foreach(var overlay in projection.buildingOverlays)
            {
                var categories=overlay.activityCategories;
                // 복합 주소는 보라색. 실제 입주 확정이나 건물 용도 변경이 아니다.
                var color=categories.Length>1?new Color(.8f,.35f,1):categories[0]=="음식"?new Color(.78f,.28f,.22f):
                    categories[0]=="주거"?Color.cyan:categories[0]=="제조"?Color.white:
                    categories[0]=="의료"?Color.green:categories[0]=="전통시장"?new Color(.95f,.48f,.12f):new Color(1,.65f,.1f);
                var obj=new GameObject("주소 후보 · "+overlay.spatialId);obj.layer=31;obj.transform.SetParent(activityLayerRoot.transform,false);
                var line=obj.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.widthMultiplier=1.6f;
                line.startColor=color;line.endColor=color;
                var b=byId[overlay.buildingRef];line.positionCount=b.points.Length-1;
                for(int i=0;i<line.positionCount;i++)line.SetPosition(i,new Vector3(b.points[i].x,b.height+.3f,b.points[i].z));
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color"));
                material.color=Color.white;markerMaterials.Add(material);line.sharedMaterial=material;
            }
            ApplyLayerVisibility();
        }
        public void SetLayerVisibility(bool roads,bool buildings,bool activities)
        {
            showRoads=roads;showBuildings=buildings;showActivities=activities;ApplyLayerVisibility();
        }
        private void ApplyLayerVisibility()
        {
            var roads=transform.Find("사가정 · 도로 선 · 폭은 기호");if(roads!=null)roads.gameObject.SetActive(showRoads);
            foreach(Transform child in transform)
                if(child.name.StartsWith("사가정 · 건물 외곽 · ",StringComparison.Ordinal))child.gameObject.SetActive(showBuildings);
            if(activityLayerRoot!=null)activityLayerRoot.SetActive(showActivities);
        }
        private void OnDestroy(){foreach(var m in markerMaterials)if(m!=null)Destroy(m);}
        private bool Include(주소자료Item r)=>주소공간연결검사.Show(r,history);
        private bool Match(주소건물참고Group g)=>g.records.Any(Include)&&(filter.Length==0||g.addressKey.Contains(filter)||g.records.Any(r=>Include(r)&&r.name.Contains(filter)));
        private bool Match(면목동건물Overlay overlay)=>filter.Length==0||overlay.spatialId.Contains(filter)||overlay.activityCategories.Any(x=>x.Contains(filter));
        private static string Categories(면목동건물Overlay overlay)=>string.Join(" · ",overlay.activityCategories);
        private void OnGUI()
        {
            // 작은 화면에서는 기존 조작 영역을 덮지 않도록 패널을 명시적으로 열게 한다.
            var width=Mathf.Min(390,Screen.width-24);if(width<120)return;
            var button=new Rect(Screen.width-width-12,12,width,36);
            if(GUI.Button(button,open?"주소 자료 닫기":$"면목동 수집자료 · 건물 참고 {ConnectedBuildings}곳"))open=!open;
            if(!open)return;
            var panel=new Rect(button.x,54,width,Mathf.Max(80,Mathf.Min(520,Screen.height-155)));
            var style=new GUIStyle(GUI.skin.label){wordWrap=true,richText=false};
            var occupied=new List<Rect>();
            if(projection!=null&&observer!=null)
                foreach(var overlay in projection.buildingOverlays.Where(Match))
                {
                    var p=observer.WorldToViewportPoint(transform.TransformPoint(points[overlay.buildingRef]));if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)continue;
                    var pixel=observer.ViewportToScreenPoint(p);var rect=new Rect(pixel.x-12,Screen.height-pixel.y-12,24,24);
                    if(panel.Overlaps(rect)||button.Overlaps(rect))continue;
                    if(occupied.Any(r=>r.Overlaps(rect)))continue;
                    occupied.Add(new Rect(rect.x-3,rect.y-3,rect.width+6,rect.height+6));
                    if(GUI.Button(rect,overlay.observationCount.ToString()))selected=overlay.buildingRef;
                }
            GUILayout.BeginArea(panel,GUI.skin.box);
            if(data==null||projection==null){GUILayout.Label(error,style);GUILayout.EndArea();return;}
            GUILayout.Label($"주소 후보 {projection.counts.activityObservations}관측 / {projection.buildingOverlays.Length}건물",style);
            GUILayout.Label($"표본: 단일 {projection.counts.addressSingleCandidates} · 복수 {projection.counts.multipleCandidates} · 미연결 {projection.counts.unlinkedSamples} · 공식건물 {projection.counts.officialBuildingLinks}",style);
            GUILayout.Label("비공개 Editor 검토 · 입주/현재 영업/출입구 미확정\n기본은 업종·연결 상태만 표시합니다.",style);
            GUILayout.Label("윤곽: 빨강 음식 · 주황 소매/시장 · 초록 의료 · 파랑 주거 · 흰색 제조 · 보라 복합",style);
            var nextRoads=GUILayout.Toggle(showRoads,"도로·교통 레이어");
            var nextBuildings=GUILayout.Toggle(showBuildings,"건물 외곽·건물 종류 레이어");
            var nextActivities=GUILayout.Toggle(showActivities,"업종 관측 레이어");
            if(nextRoads!=showRoads||nextBuildings!=showBuildings||nextActivities!=showActivities)
                SetLayerVisibility(nextRoads,nextBuildings,nextActivities);
            GUILayout.Label("지역 타일·필지·게임 시나리오는 자료에는 분리되어 있으나 이번 화면 표본에는 아직 독립 도형이 없습니다.",style);
            filter=GUILayout.TextField(filter??"",80);
            detailedReview=GUILayout.Toggle(detailedReview,"로컬 상세 주소·상호 검토");
            if(detailedReview)history=GUILayout.Toggle(history,"병의원 폐업·비정상/미확인 이력 포함");
            scroll=GUILayout.BeginScrollView(scroll);
            if(!detailedReview)
            {
                foreach(var overlay in projection.buildingOverlays.Where(Match))
                {
                    if(GUILayout.Button(Categories(overlay)+" · "+overlay.observationCount+"관측"))selected=selected==overlay.buildingRef?"":overlay.buildingRef;
                    if(selected!=overlay.buildingRef)continue;
                    var operations=string.Join(" · ",overlay.operationEvidenceCounts.Select(x=>x.evidenceCode+" "+x.count));
                    GUILayout.Label($"공간 ID: {overlay.spatialId}\n건물 종류: {overlay.buildingUseCode} ({overlay.buildingUseEvidenceCode})\n연결: {overlay.connectionStatus} · 입주 미확정\n영업 근거: {operations}\n높이: {overlay.displayHeight:0.#} · {overlay.heightSemanticsCode}",style);
                }
            }
            else foreach(var g in data.groups.Where(Match))
            {
                if(GUILayout.Button(g.addressKey+" · "+g.records.Count(Include)+"자료"))selected=selected==g.buildingId?"":g.buildingId;
                if(selected!=g.buildingId)continue;
                foreach(var r in g.records.Where(Include))
                {
                    var state=string.IsNullOrEmpty(r.status)?"운영상태 미확인":"원천 상태: "+r.status;
                    GUILayout.Label($"{Kind(r.kind)} · {r.name}\n{r.roadAddress}\n{state}\n기준/관측: {r.evidenceAsOf}\n출처: {r.sourceId} / {r.datasetId}",style);
                }
            }
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
