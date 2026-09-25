#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Serializable] public sealed class 국가무역Item
    {
        public string partner, direction, revision;
        public double kg=double.NaN, usd=double.NaN;
    }
    [Serializable] public sealed class 국가무역Snapshot
    {
        public string schemaVersion, transportMode, sourceUrl, collectedAtUtc;
        public int year;
        public bool localReviewOnly, distributionApproved;
        public 국가무역Item[] items;
        public static readonly string[] Countries={"CN","JP","US","AU","VN"};
        public void Validate()
        {
            if(schemaVersion!="country-trade-preview.v1"||year!=2025||!localReviewOnly||distributionApproved||transportMode!="Unknown"
                ||sourceUrl!="https://www.data.go.kr/data/15100475/openapi.do"||!DateTimeOffset.TryParse(collectedAtUtc,CultureInfo.InvariantCulture,DateTimeStyles.None,out _)
                ||items==null||items.Length!=10) throw new ArgumentException("TradeSnapshotInvalid");
            var keys=new HashSet<string>();
            foreach(var i in items)
                if(i==null||!Countries.Contains(i.partner)||(i.direction!="export"&&i.direction!="import")||!keys.Add(i.partner+":"+i.direction)
                    ||double.IsNaN(i.kg)||double.IsInfinity(i.kg)||i.kg<0||i.kg>1e15||double.IsNaN(i.usd)||double.IsInfinity(i.usd)||i.usd<0||i.usd>1e15
                    ||i.revision==null||i.revision.Length!=64||i.revision.Any(c=>!"0123456789abcdef".Contains(c))) throw new ArgumentException("TradeItemInvalid");
        }
        // Same linear scale for every flow; the final cube's volume represents the remainder.
        public double UnitKg=>Math.Max(1e9,Math.Ceiling(items.Sum(i=>i.kg)/40/1e9)*1e9);
        public int Count(국가무역Item i)=>(int)Math.Ceiling(i.kg/UnitKg);
        public double Fraction(국가무역Item i,int index)=>Math.Min(1,Math.Max(0,i.kg/UnitKg-index));
    }
    /// <summary>Private Editor preview of aggregate trade. Curves are statistical relationships, not navigable routes.</summary>
    public sealed class 국가무역관찰View:MonoBehaviour
    {
        private 국가무역Snapshot snapshot;
        private 세계지구본View globe;
        private Transform root;
        private GameObject canvasRoot, panel;
        private Text detail;
        private readonly List<Transform> cargo=new();
        private readonly List<int> flowIndices=new(), ordinal=new();
        private readonly List<Material> materials=new();
        private float elapsed;
        public int ObjectCount=>cargo.Count;
        public double UnitKg=>snapshot.UnitKg;
        public string SelectedCountry {get;private set;}="CN";
        private static readonly string[] Names={"중국","일본","미국","호주","베트남"};
        private static readonly Vector2[] Points={new(31,121),new(35,139),new(34,-118),new(-33,151),new(16,108)};
        public static Vector3 Position(string partner,string direction,float progress)
        {
            var index=Array.IndexOf(국가무역Snapshot.Countries,partner);
            if(index<0||(direction!="export"&&direction!="import"))throw new ArgumentException("TradeFlowInvalid");
            var t=direction=="export"?Mathf.Clamp01(progress):1-Mathf.Clamp01(progress);
            var start=지구위경도선View.Coordinate(36.3,127.8,1);
            var end=지구위경도선View.Coordinate(Points[index].x,Points[index].y,1);
            return Vector3.Slerp(start,end,t).normalized*(18.3f+Mathf.Sin(t*Mathf.PI)*(direction=="export"?1.6f:2.3f));
        }
        public void Load(string json,세계지구본View owner)
        {
            var candidate=JsonUtility.FromJson<국가무역Snapshot>(json);candidate.Validate();
            Clear();snapshot=candidate;globe=owner;
            var surface=owner.transform.Find("GlobeVisualRoot");
            if(surface==null)throw new InvalidOperationException("TradeGlobeSurfaceMissing");
            root=new GameObject("2025 국가 무역 · 통계 관계 / 실제 항로 아님").transform;root.SetParent(surface,false);
            root.gameObject.hideFlags=HideFlags.DontSave;
            var export=CreateMaterial(new Color(1,.58f,.16f));var import=CreateMaterial(new Color(.2f,.8f,1));
            for(var f=0;f<snapshot.items.Length;f++)
            {
                var item=snapshot.items[f];var material=item.direction=="export"?export:import;
                var lineObject=new GameObject(item.partner+" "+item.direction+" 통계 연결선");lineObject.transform.SetParent(root,false);lineObject.layer=세계지구본View.GlobeLayer;
                var line=lineObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=material;line.widthMultiplier=.024f;line.positionCount=65;
                for(var p=0;p<=64;p++)line.SetPosition(p,Position(item.partner,item.direction,p/64f));
                for(var n=0;n<snapshot.Count(item);n++)
                {
                    var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=item.partner+" "+item.direction+" 상징 화물 "+n;
                    DestroyImmediate(obj.GetComponent<Collider>());obj.transform.SetParent(root,false);obj.layer=세계지구본View.GlobeLayer;
                    obj.transform.localScale=Vector3.one*(.36f*(float)Math.Pow(snapshot.Fraction(item,n),1d/3));
                    obj.GetComponent<Renderer>().sharedMaterial=material;cargo.Add(obj.transform);flowIndices.Add(f);ordinal.Add(n);
                }
            }
            globe.GetComponent<해양생활연출View>()?.SetPresentationSuppressed(true);
            BuildUi();Apply(0);SelectCountry("CN");panel.SetActive(false);
        }
        private Material CreateMaterial(Color color){var m=new Material(Shader.Find("Sprites/Default")){color=color};materials.Add(m);return m;}
        public void Apply(float time)
        {
            for(var n=0;n<cargo.Count;n++)
            {
                var i=snapshot.items[flowIndices[n]];var phase=Mathf.Repeat(time/20f+ordinal[n]/(float)snapshot.Count(i),1);
                cargo[n].localPosition=Position(i.partner,i.direction,phase);
                cargo[n].localRotation=Quaternion.LookRotation(cargo[n].localPosition.normalized);
            }
        }
        private void Update()
        {
            if(root==null||globe==null)return;
            var visible=globe.IsGlobeVisible;root.gameObject.SetActive(visible);canvasRoot.SetActive(visible);
            elapsed+=Time.unscaledDeltaTime;Apply(elapsed);
        }
        private void BuildUi()
        {
            canvasRoot=new GameObject("2025 무역 관찰 안내",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasRoot.hideFlags=HideFlags.DontSave;
            var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            var scaler=canvasRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1000,850);scaler.matchWidthOrHeight=1;
            var toggle=Box("무역 통계 펼치기",canvasRoot.transform,18,110,390,70);toggle.AddComponent<Button>().onClick.AddListener(()=>panel.SetActive(!panel.activeSelf));
            Label(toggle.transform,"2025 한국 ↔ 5개국 무역\n주황 수출 / 하늘 수입 · 클릭하여 상세",18);
            panel=Box("무역 상세",canvasRoot.transform,18,190,390,485);
            for(var c=0;c<5;c++)
            {
                var code=국가무역Snapshot.Countries[c];var b=Box(code,panel.transform,8+c*76,8,72,42);Label(b.transform,Names[c],18);b.AddComponent<Button>().onClick.AddListener(()=>SelectCountry(code));
            }
            var body=Box("상세 내용",panel.transform,8,60,374,410);body.GetComponent<Image>().color=Color.clear;detail=Label(body.transform,"",19);
        }
        private static GameObject Box(string name,Transform parent,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));obj.transform.SetParent(parent,false);
            var r=obj.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
            obj.GetComponent<Image>().color=new Color(.025f,.08f,.15f,.95f);return obj;
        }
        private static Text Label(Transform parent,string value,int size)
        {
            var obj=new GameObject("문구",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));obj.transform.SetParent(parent,false);
            var r=obj.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(9,5);r.offsetMax=new Vector2(-9,-5);
            var t=obj.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=Color.white;t.text=value;t.raycastTarget=false;return t;
        }
        public void SelectCountry(string code)
        {
            var index=Array.IndexOf(국가무역Snapshot.Countries,code);if(index<0)throw new ArgumentException("TradeCountryInvalid");SelectedCountry=code;
            var a=snapshot.items.Single(i=>i.partner==code&&i.direction=="export");var b=snapshot.items.Single(i=>i.partner==code&&i.direction=="import");
            string Weight(double n)=>(n/1e6).ToString("N1",CultureInfo.InvariantCulture)+" 천 톤";
            string Money(double n)=>(n/1e8).ToString("N2",CultureInfo.InvariantCulture)+" 억 USD";
            detail.text="한국 ↔ "+Names[index]+" · 2025년 연간\n\n수출  "+Weight(a.kg)+"\n금액  "+Money(a.usd)+"\n\n수입  "+Weight(b.kg)+"\n금액  "+Money(b.usd)+"\n\n화물 1개 최대 "+Weight(UnitKg)+"\n잔량은 부피에 비례 / 총 "+ObjectCount+"개\n운송수단 미확인 · 실제 항로 아님\n\n관세청 품목·국가별 수출입실적\n수출 FOB / 수입 CIF\n수집 "+snapshot.collectedAtUtc.Substring(0,10)+"\n로컬 검토용 · 배포 승인 전";
        }
        private void Clear()
        {
            if(root!=null)DestroyImmediate(root.gameObject);if(canvasRoot!=null)DestroyImmediate(canvasRoot);
            foreach(var m in materials)if(m!=null)DestroyImmediate(m);materials.Clear();cargo.Clear();flowIndices.Clear();ordinal.Clear();
            if(globe!=null)globe.GetComponent<해양생활연출View>()?.SetPresentationSuppressed(false);
            snapshot=null;
        }
        private void OnDestroy()=>Clear();
    }
}
#endif
