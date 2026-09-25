#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using Ssalddel.Unity.Runtime.WorldMap;
namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Serializable] public sealed class Hs조회근거 { public string dataset,revision,recordKey,rawHash; }
    [Serializable] public sealed class Hs조회지표 { public bool available; public double value; public string unit; public Hs조회근거[] sources; }
    [Serializable] public sealed class Hs조회상대국 { public int m49; public string name; public Hs조회지표 valueUsd,netWeightKg; }
    [Serializable] public sealed class Hs조회품목 { public string hs6,name,status; public Hs조회상대국[] partners; public Hs조회근거[] classificationSources; }
    [Serializable] public sealed class Hs조회사본
    {
        public string schemaVersion,classification,hsEdition,flow,exportStatus,partnerCoverage,transportLink,sourceUrl;
        public int reporterM49,year; public bool localReviewOnly,distributionApproved; public Hs조회품목[] products;
        public void Validate()
        {
            if(schemaVersion!="hs-trade-index.v1"||classification!="H6"||hsEdition!="2022"||reporterM49!=410||year!=2025||flow!="import"||exportStatus!="NotCollected"||partnerCoverage!="WorldAndStoredTopPartners_NotAllCountries"||transportLink!="ContextOnly_NoHsModeBinding"||sourceUrl!="https://comtradeplus.un.org/"||!localReviewOnly||distributionApproved||products==null||products.Length==0||products.Length>10000)throw new ArgumentException("HsIndexInvalid");
            if(products.Select(p=>p.hs6).Distinct().Count()!=products.Length)throw new ArgumentException("HsDuplicateCode");
            foreach(var p in products)
            {
                if(p.hs6==null||p.hs6.Length!=6||p.hs6.Any(c=>c<'0'||c>'9')||string.IsNullOrWhiteSpace(p.name)||p.partners==null||p.partners.Length>250||p.partners.Select(x=>x.m49).Distinct().Count()!=p.partners.Length||p.classificationSources==null||p.classificationSources.Length==0||!(p.status=="Observed"||p.status=="NoReportedRows"||p.status=="NotCollected")||(p.status=="Observed")!=(p.partners.Length>0))throw new ArgumentException("HsProductInvalid");
                foreach(var x in p.partners){if(x.m49<0||x.m49>999||string.IsNullOrWhiteSpace(x.name))throw new ArgumentException("HsPartnerInvalid");Metric(x.valueUsd,"USD");Metric(x.netWeightKg,"kg");}
            }
        }
        static void Metric(Hs조회지표 m,string unit){if(m==null||m.unit!=unit||double.IsNaN(m.value)||double.IsInfinity(m.value)||m.value<0||(!m.available&&m.value!=0)||m.sources==null||m.sources.Length==0)throw new ArgumentException("HsMetricInvalid");}
    }
    public sealed class Hs무역조회View:MonoBehaviour
    {
        Hs조회사본 snapshot; Hs조회품목 selected;
        세계지구본View globe; 세계지구본Catalog catalog;
        string search="030214"; Vector2 scroll; Transform lines; Material material;
        public int ProductCount=>snapshot?.products.Length??0;
        public int ConnectionCount=>lines==null?0:lines.childCount;
        public string SelectedCode=>selected?.hs6;
        public void Load(string json,세계지구본View owner)
        {
            var candidate=JsonUtility.FromJson<Hs조회사본>(json);candidate.Validate();
            var c=JsonUtility.FromJson<세계지구본Catalog>(Resources.Load<TextAsset>(세계지구본View.CatalogResourceName).text);c.Validate();
            Clear();snapshot=candidate;globe=owner;catalog=c;
            material=new Material(Shader.Find("Sprites/Default")){color=new Color(.9f,.5f,1)};
            Select(candidate.products.Any(p=>p.hs6==search)?search:candidate.products[0].hs6);
            globe.GetComponent<해양생활연출View>()?.SetPresentationSuppressed(true);
        }
        세계국가관찰자료 Match(Hs조회상대국 p)=>p.m49==0?null:catalog.countries.FirstOrDefault(c=>string.Equals(c.englishName,p.name,StringComparison.OrdinalIgnoreCase));
        public void Select(string code)
        {
            var p=snapshot.products.Single(x=>x.hs6==code);selected=p;
            if(lines!=null)DestroyImmediate(lines.gameObject);
            lines=new GameObject("HS 품목 통계 관계 · 수단 미결속").transform;lines.SetParent(globe.transform.Find("GlobeVisualRoot"),false);lines.gameObject.hideFlags=HideFlags.DontSave;
            foreach(var partner in p.partners)
            {
                var c=Match(partner);if(c==null)continue;
                var obj=new GameObject(partner.name+" 수입 관계");obj.layer=세계지구본View.GlobeLayer;obj.transform.SetParent(lines,false);
                var line=obj.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=material;line.widthMultiplier=.045f;line.positionCount=49;
                var a=지구위경도선View.Coordinate(c.centerLatitude,c.centerLongitude,1);var b=지구위경도선View.Coordinate(36.3,127.8,1);
                for(var n=0;n<=48;n++){var t=n/48f;line.SetPosition(n,Vector3.Slerp(a,b,t).normalized*(18.4f+Mathf.Sin(t*Mathf.PI)*2));}
            }
        }
        void Update(){if(lines!=null&&globe!=null)lines.gameObject.SetActive(globe.IsGlobeVisible);}
        static string Value(Hs조회지표 m)=>m.available?m.value.ToString("N2")+" "+m.unit:"자료 없음 (0 아님)";
        void OnGUI()
        {
            if(snapshot==null||globe==null||!globe.IsGlobeVisible)return;
            GUILayout.BeginArea(new Rect(15,95,Mathf.Min(470,Screen.width-30),Mathf.Max(200,Screen.height-110)),GUI.skin.box);
            GUILayout.Label("HS 2022 · 한국 2025 수입 / 품목 "+ProductCount+"개");search=GUILayout.TextField(search);
            foreach(var p in snapshot.products.Where(p=>p.hs6.Contains(search)||p.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).Take(4))if(GUILayout.Button(p.hs6+" "+(p.name.Length>65?p.name.Substring(0,65)+"…":p.name)))Select(p.hs6);
            scroll=GUILayout.BeginScrollView(scroll);
            if(selected!=null)
            {
                GUILayout.Label(selected.hs6+"  "+selected.name+"\n"+selected.status);
                foreach(var p in selected.partners)
                {
                    GUILayout.Label((p.m49==0?"세계 합계":p.name+" / M49 "+p.m49)+"\n"+Value(p.valueUsd)+" · "+Value(p.netWeightKg));
                    var c=Match(p);if(c!=null){if(GUILayout.Button("지도에서 "+p.name+" 보기"))globe.SelectCountryAtForTests(c.centerLatitude,c.centerLongitude);}
                    else if(p.m49!=0)GUILayout.Label("지도 위치 미결속 — 임의 배치 안 함");
                }
                if(selected.partners.Length==0)GUILayout.Label("저장된 관측 없음 — 무역량 0이라는 의미 아님");
            }
            GUILayout.Label("세계 합계 + 저장된 상위 상대국 / 전체 국가 목록 아님\n수출: 미수집 · 선박/항공 품목별 물동량: 미결속\n보라색: 품목 통계 관계 / 실제 항로 아님\n출처 UN Comtrade · 로컬 검토용 / 배포 승인 전");
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void Clear(){if(lines!=null)DestroyImmediate(lines.gameObject);if(material!=null)DestroyImmediate(material);snapshot=null;selected=null;if(globe!=null)globe.GetComponent<해양생활연출View>()?.SetPresentationSuppressed(false);}
        void OnDestroy()=>Clear();
    }
}
#endif
