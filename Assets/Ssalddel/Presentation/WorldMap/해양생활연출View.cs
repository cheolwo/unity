#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>Editor 전용 합성 해양 연출. 위치·주기는 관측/AIS/항행 가능성 또는 업무 결과가 아니다.</summary>
    public sealed class 해양생활연출View : MonoBehaviour
    {
        private readonly Transform[] fish=new Transform[3], tail=new Transform[3], ships=new Transform[가상해양항로Catalog.Count*2];
        private readonly bool[] routeAccepted=new bool[가상해양항로Catalog.Count];
        private readonly LineRenderer[] rings=new LineRenderer[3];
        private readonly List<Material> materials=new();
        private Transform root;
        private 세계지구본View globe;
        private 세계지구본Catalog catalog;
        private Text caption;
        private GameObject ui;
        private float elapsed;
        private bool presentationSuppressed;
        public void SetPresentationSuppressed(bool value)
        {
            presentationSuppressed=value;
            if(root!=null)root.gameObject.SetActive(Visible&&!value);
            if(ui!=null)ui.SetActive(!value);
        }
        public bool Visible { get; private set; }=true;
        public bool RouteAccepted { get; private set; }
        public int FishCount => fish[0]!=null?3:0;
        public int ShipCount { get {var count=0;foreach(var ship in ships)if(ship!=null)count++;return count;} }
        public int AcceptedRouteCount { get {var count=0;foreach(var accepted in routeAccepted)if(accepted)count++;return count;} }
        public float Elapsed => elapsed;
        public static float FishRadius(float time,int index) => 18f + .46f*Mathf.Sin(time*1.1f+index*2.1f);
        public static Vector2 ShipCoordinate(float time,int index) => 가상해양항로Catalog.Ship(0,time,index);
        public static Vector2 RoutePoint(float phase) => 가상해양항로Catalog.Point(0,phase);
        public static bool IsRouteOcean(Func<double,double,bool> isOcean) => 가상해양항로Catalog.IsOcean(0,isOcean);
        public void Initialize(세계지구본View owner,Transform surface,Canvas canvas,세계지구본Catalog map)
        {
            if(root!=null) return;
            globe=owner; catalog=map;
            root=new GameObject("가상 해양 생활 · 실제 항로 아님").transform; root.SetParent(surface,false);
            RouteAccepted=IsRouteOcean((lat,lon)=>catalog.FindAt(lat,lon)==null);
            var orange=Material(new Color(1,.58f,.12f)); var white=Material(new Color(.94f,.98f,1));
            var blue=Material(new Color(.1f,.65f,.95f)); var red=Material(new Color(.85f,.25f,.2f));
            var ripple=Material(new Color(.4f,.9f,1,.5f));
            for(var i=0;i<3;i++)
            {
                fish[i]=Group("가상 물고기 "+i);
                Shape(fish[i],PrimitiveType.Sphere,new Vector3(.21f,.16f,.52f),Vector3.zero,orange);
                tail[i]=Shape(fish[i],PrimitiveType.Cube,new Vector3(.25f,.04f,.14f),new Vector3(0,0,-.28f),orange);
                Shape(fish[i],PrimitiveType.Sphere,new Vector3(.025f,.025f,.025f),new Vector3(.1f,.045f,.12f),white);
                Shape(fish[i],PrimitiveType.Sphere,new Vector3(.025f,.025f,.025f),new Vector3(-.1f,.045f,.12f),white);
                var water=Group("부상 물결 "+i); rings[i]=water.gameObject.AddComponent<LineRenderer>();
                rings[i].useWorldSpace=false; rings[i].loop=true; rings[i].positionCount=32; rings[i].widthMultiplier=.018f; rings[i].sharedMaterial=ripple;
            }
            for(var routeIndex=0;routeIndex<가상해양항로Catalog.Count;routeIndex++)
            {
                routeAccepted[routeIndex]=가상해양항로Catalog.IsOcean(routeIndex,(lat,lon)=>catalog.FindAt(lat,lon)==null);
                if(!routeAccepted[routeIndex])continue;
                var route=Group("가상 항로 · "+가상해양항로Catalog.Name(routeIndex)).gameObject.AddComponent<LineRenderer>(); route.useWorldSpace=false; route.widthMultiplier=.014f; route.sharedMaterial=ripple; route.positionCount=385;
                for(var i=0;i<385;i++) {var p=가상해양항로Catalog.Point(routeIndex,i/384f);route.SetPosition(i,지구위경도선View.Coordinate(p.x,p.y,18.06f));}
                for(var i=0;i<2;i++)
                {
                    var index=routeIndex*2+i;
                    ships[index]=Group("가상 선박 · "+가상해양항로Catalog.Name(routeIndex)+" · "+i);
                    Shape(ships[index],PrimitiveType.Cube,new Vector3(.26f,.13f,.62f),Vector3.zero,i==0?blue:red);
                    Shape(ships[index],PrimitiveType.Cube,new Vector3(.17f,.15f,.22f),new Vector3(0,.14f,-.1f),white);
                    Shape(ships[index],PrimitiveType.Cube,new Vector3(.025f,.27f,.025f),new Vector3(0,.24f,.06f),white);
                    var p=가상해양항로Catalog.Point(routeIndex,i); var endpoint=Group("가상 해역 대기점 "+routeIndex+" · "+i);
                    endpoint.localPosition=지구위경도선View.Coordinate(p.x,p.y,18.10f);
                    Shape(endpoint,PrimitiveType.Sphere,Vector3.one*.09f,Vector3.zero,white);
                }
            }
            ui=new GameObject("해양 시연 안내",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));
            ui.transform.SetParent(canvas.transform,false); var rect=ui.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-15,15);rect.sizeDelta=new Vector2(460,98);
            ui.GetComponent<Image>().color=new Color(.02f,.09f,.15f,.94f);
            ui.GetComponent<Button>().onClick.AddListener(()=>SetVisible(!Visible));
            var label=new GameObject("문구",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(ui.transform,false);
            var tr=label.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(10,5);tr.offsetMax=new Vector2(-10,-5);
            caption=label.GetComponent<Text>();caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");caption.fontSize=18;caption.color=Color.white;caption.raycastTarget=false;
            SetVisible(true); Apply(0);
        }
        public void SetVisible(bool value)
        {
            Visible=value; if(root!=null)root.gameObject.SetActive(value&&!presentationSuppressed);
            if(caption!=null)caption.text=(value?"해양 시연 · 클릭하면 숨김":"해양 시연 · 클릭하면 표시")+"\n가상 유영·항로 / 크기·시간 과장 / 실제 운항 아님"+"\n"+AcceptedRouteCount+"개 항로 · "+ShipCount+"척 / 육지 판정 차단 "+(routeAccepted.Length-AcceptedRouteCount);
        }
        private void Update()
        {
            if(root==null || !UnityEngine.Application.isPlaying) return;
            elapsed+=Time.unscaledDeltaTime; Apply(elapsed);
        }
        public void Apply(float time)
        {
            if(root==null)return;
            var school=globe.GetComponent<해수면수온미리보기View>();
            for(var i=0;i<3;i++)
            {
                var lat=school!=null?school.School.Latitude:36.7+i*.7;
                var lon=school!=null?school.School.Longitude:132.1+i*.3;
                var valid=catalog.FindAt(lat,lon)==null;
                fish[i].gameObject.SetActive(valid); rings[i].gameObject.SetActive(valid && Visible);
                if(!valid)continue;
                var normal=지구위경도선View.Coordinate(lat,lon,1);
                var north=(지구위경도선View.Coordinate(lat+.01,lon,1)-normal).normalized;
                fish[i].localPosition=normal*FishRadius(time,i);
                fish[i].localRotation=Quaternion.LookRotation(north,normal)*Quaternion.Euler(Mathf.Cos(time*1.1f+i*2.1f)*28, i*95+Mathf.Sin(time*.3f)*15,0);
                tail[i].localRotation=Quaternion.Euler(0,Mathf.Sin(time*9+i)*28,0);
                rings[i].transform.localPosition=normal*18.07f; rings[i].transform.localRotation=Quaternion.LookRotation(north,normal);
                var radius=.13f+.2f*Mathf.Repeat(time*.55f+i*.3f,1);
                rings[i].enabled=Mathf.Abs(FishRadius(time,i)-18)<.3f;
                for(var k=0;k<32;k++) {var a=k*Mathf.PI/16;rings[i].SetPosition(k,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            }
            for(var i=0;i<ships.Length;i++)
            {
                if(ships[i]==null)continue;
                var p=가상해양항로Catalog.Ship(i/2,time,i%2);var next=가상해양항로Catalog.Ship(i/2,time+.08f,i%2);
                var n=지구위경도선View.Coordinate(p.x,p.y,1);
                var dir=지구위경도선View.Coordinate(next.x,next.y,1)-n;
                if(dir.sqrMagnitude<1e-12f)dir=지구위경도선View.Coordinate(p.x+.01,p.y,1)-n;
                ships[i].localPosition=n*(18.13f+.018f*Mathf.Sin(time*2+i));
                ships[i].localRotation=Quaternion.LookRotation(dir.normalized,n)*Quaternion.Euler(0,0,Mathf.Sin(time*1.5f+i)*4);
            }
        }
        private Transform Group(string name) {var go=new GameObject(name);go.layer=세계지구본View.GlobeLayer;go.transform.SetParent(root,false);return go.transform;}
        private Transform Shape(Transform parent,PrimitiveType type,Vector3 scale,Vector3 position,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.layer=세계지구본View.GlobeLayer;go.transform.SetParent(parent,false);go.transform.localScale=scale;go.transform.localPosition=position;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Release(collider);go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        private Material Material(Color color)
        {
            var m=new Material(Shader.Find("Sprites/Default")){color=color,hideFlags=HideFlags.DontSave};materials.Add(m);return m;
        }
        private void OnDestroy() {if(root!=null)Release(root.gameObject);if(ui!=null)Release(ui);foreach(var m in materials)Release(m);}
        private static void Release(UnityEngine.Object value) {if(value==null)return;if(UnityEngine.Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
#endif
