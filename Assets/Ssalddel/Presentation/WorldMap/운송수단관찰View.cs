#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Serializable] public sealed class 운송수단관측
    {
        public string partner,direction,mode,month,port,rawWeight,revision,stableId,sourceUrl;
        public double blCount=double.NaN;
    }
    [Serializable] public sealed class 운송수단사본
    {
        public string schemaVersion,selectionRule,collectedAtUtc;
        public int year;
        public bool localReviewOnly,distributionApproved;
        public 운송수단관측[] items;
        public void Validate()
        {
            if(schemaVersion!="transport-observation-preview.v1"||selectionRule!="LatestMonthLargestBlRow"||year!=2025||!localReviewOnly||distributionApproved||!DateTimeOffset.TryParse(collectedAtUtc,out _)||items==null||items.Length==0||items.Length>20)throw new ArgumentException("TransportSnapshotInvalid");
            var keys=new HashSet<string>();
            foreach(var i in items)
            {
                if(i==null||!국가무역Snapshot.Countries.Contains(i.partner)||(i.direction!="import"&&i.direction!="export")||(i.mode!="Sea"&&i.mode!="Air")||!keys.Add(i.partner+i.direction+i.mode)
                    ||double.IsNaN(i.blCount)||double.IsInfinity(i.blCount)||i.blCount<0||i.blCount!=Math.Floor(i.blCount)||i.blCount>1e12
                    ||!Enumerable.Range(1,12).Any(m=>i.month=="2025."+m.ToString("00"))||string.IsNullOrWhiteSpace(i.port)||i.port.Length>80||string.IsNullOrWhiteSpace(i.rawWeight)||i.rawWeight.Length>40
                    ||string.IsNullOrWhiteSpace(i.stableId)||i.revision==null||i.revision.Length!=64||i.revision.Any(c=>!"0123456789abcdef".Contains(c))
                    ||i.sourceUrl!=(i.direction=="import"?"https://www.data.go.kr/data/15151805/openapi.do":"https://www.data.go.kr/data/15151809/openapi.do"))throw new ArgumentException("TransportItemInvalid");
            }
        }
    }
    /// <summary>통계 관계의 상징 모형. 실제 항로/대수/속도 및 업무 완료를 나타내지 않는다.</summary>
    public sealed class 운송수단관찰View:MonoBehaviour
    {
        private 운송수단사본 snapshot;
        private 세계지구본View globe;
        private Transform root;
        private readonly List<Transform> symbols=new();
        private readonly List<Material> materials=new();
        private readonly List<LineRenderer> routes=new();
        private Vector2 pointerDown;
        private float elapsed;
        private int selected=-1;
        private bool expanded;
        public int ObjectCount=>symbols.Count;
        public int SelectedIndex=>selected;
        public void SelectSymbol(int index)
        {
            if(index < -1 || index >= symbols.Count)throw new ArgumentOutOfRangeException(nameof(index));
            selected=index;expanded=false;
            for(var n=0;n<symbols.Count;n++){symbols[n].localScale=Vector3.one*(n==index?2.2f:1.6f);routes[n].enabled=n==index;}
        }
        public int PickSymbol(Camera camera,Vector2 screenPoint)
        {
            var best=-1;var distance=28f*28f;
            for(var n=0;n<symbols.Count;n++)
            {
                var position=symbols[n].position;var p=camera.WorldToScreenPoint(position);if(p.z<=0)continue;
                // 카메라와 모형 사이에 지구 구체가 있으면 뒷면 모형 선택을 제외한다.
                var delta=position-camera.transform.position;var length=delta.magnitude;var ray=delta/length;
                var offset=camera.transform.position-root.position;var radius=18f*root.lossyScale.x;
                var b=Vector3.Dot(offset,ray);var c=offset.sqrMagnitude-radius*radius;var disc=b*b-c;
                if(disc>=0){var hit=-b-Mathf.Sqrt(disc);if(hit>0&&hit<length-.05f)continue;}
                var d=((Vector2)p-screenPoint).sqrMagnitude;if(d<distance){distance=d;best=n;}
            }
            return best;
        }
        public static Vector3 Position(운송수단관측 item,float t)
        {
            var p=국가무역관찰View.Position(item.partner,item.direction,t);
            return item.mode=="Air"?p.normalized*(p.magnitude+1.2f):p;
        }
        public void Load(string json,세계지구본View owner)
        {
            var candidate=JsonUtility.FromJson<운송수단사본>(json);candidate.Validate();
            var surface=owner.transform.Find("GlobeVisualRoot");if(surface==null)throw new InvalidOperationException("TransportSurfaceMissing");
            Clear();snapshot=candidate;globe=owner;selected=-1;expanded=false;elapsed=0;
            root=new GameObject("2025 해상·항공 통계 관계 · 실제 항로 아님").transform;root.SetParent(surface,false);root.gameObject.hideFlags=HideFlags.DontSave;
            foreach(var item in snapshot.items)
            {
                var material=new Material(Shader.Find("Sprites/Default")){color=item.mode=="Sea"?new Color(.2f,.85f,1):new Color(1,.75f,.25f)};materials.Add(material);
                var symbol=new GameObject(item.partner+" "+item.mode+" "+item.direction).transform;symbol.SetParent(root,false);symbols.Add(symbol);
                Part(symbol,"본체",new Vector3(0,0,0),new Vector3(.20f,.14f,.65f),material);
                if(item.mode=="Sea")
                {
                    Part(symbol,"선실",new Vector3(0,.15f,-.10f),new Vector3(.16f,.20f,.23f),material);
                    Part(symbol,"적재함",new Vector3(0,.10f,.15f),new Vector3(.18f,.12f,.20f),material);
                }
                else
                {
                    Part(symbol,"날개",new Vector3(0,0,.03f),new Vector3(.78f,.04f,.18f),material);
                    Part(symbol,"꼬리날개",new Vector3(0,.03f,-.26f),new Vector3(.34f,.06f,.12f),material);
                }
                var lineObj=new GameObject("통계 관계선");lineObj.transform.SetParent(root,false);lineObj.layer=세계지구본View.GlobeLayer;
                var line=lineObj.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.widthMultiplier=.018f;line.positionCount=65;
                routes.Add(line);
                for(var n=0;n<=64;n++)line.SetPosition(n,Position(item,n/64f));
            }
            globe.GetComponent<해양생활연출View>()?.SetPresentationSuppressed(true);Apply(0);SelectSymbol(-1);
        }
        static void Part(Transform parent,string name,Vector3 offset,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;DestroyImmediate(part.GetComponent<Collider>());part.layer=세계지구본View.GlobeLayer;part.transform.SetParent(parent,false);part.transform.localPosition=offset;part.transform.localScale=scale;part.GetComponent<Renderer>().sharedMaterial=material;
        }
        public void Apply(float time)
        {
            for(var n=0;n<symbols.Count;n++)
            {
                var item=snapshot.items[n];var t=Mathf.Repeat(time/24f+n*.073f,1);var p=Position(item,t);
                var a=Position(item,Mathf.Max(0,t-.001f));var b=Position(item,Mathf.Min(1,t+.001f));
                symbols[n].localPosition=p;symbols[n].localRotation=Quaternion.LookRotation((b-a).normalized,p.normalized);
            }
        }
        void Update(){if(root==null||globe==null)return;root.gameObject.SetActive(globe.IsGlobeVisible);elapsed+=Time.unscaledDeltaTime;Apply(elapsed);}
        void OnGUI()
        {
            if(snapshot==null||globe==null||!globe.IsGlobeVisible)return;
            var height=expanded?270:90;
            var card=new Rect(18,Screen.height-height-16,Mathf.Min(440,Screen.width-36),height);
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0)pointerDown=e.mousePosition;
            if(e.type==EventType.MouseUp&&e.button==0&&(selected<0||!card.Contains(e.mousePosition))&&Vector2.Distance(pointerDown,e.mousePosition)<7)
            {
                var camera=globe.transform.Find(세계지구본View.GlobeCameraName)?.GetComponent<Camera>();
                if(camera!=null){SelectSymbol(PickSymbol(camera,new Vector2(e.mousePosition.x,Screen.height-e.mousePosition.y)));e.Use();}
            }
            if(selected<0){GUI.Label(new Rect(18,Screen.height-30,420,24),"통계 기반 상징 이동 · 모형을 누르면 자료 보기");return;}
            GUILayout.BeginArea(card,GUI.skin.box);
            var item=snapshot.items[selected];
            var country=new[]{"중국","일본","미국","호주","베트남"}[Array.IndexOf(국가무역Snapshot.Countries,item.partner)];
            GUILayout.BeginHorizontal();GUILayout.Label((item.mode=="Sea"?"선박":"비행기")+" · "+(item.direction=="export"?"한국 → "+country:country+" → 한국"));
            if(GUILayout.Button("닫기",GUILayout.Width(50)))SelectSymbol(-1);GUILayout.EndHorizontal();
            if(GUILayout.Button(expanded?"자료 접기":"근거 자료 보기"))expanded=!expanded;
            if(expanded&&selected>=0)
            {
                var i=snapshot.items[selected];GUILayout.Label($"한국 ↔ {i.partner} · {i.mode} · {i.direction}\n대표 관측: {i.month} / {i.port}\nB/L {i.blCount:N0}건 (선박·항공기 수 아님)\n중량 원문: {i.rawWeight} (단위 검토 중)\n\n최신 월에서 B/L 최다인 원자료 한 행\n국가 전체 합계 아님 / 흐름당 모형 1개\n24초 이동은 연출 시간\n출처: 관세청 국가별 입항·출항물동량\n수집: {snapshot.collectedAtUtc}\n로컬 검토용 · 배포 승인 전");
            }
            GUILayout.EndArea();
        }
        void Clear(){if(root!=null)DestroyImmediate(root.gameObject);foreach(var m in materials)if(m!=null)DestroyImmediate(m);materials.Clear();symbols.Clear();routes.Clear();snapshot=null;selected=-1;if(globe!=null)globe.GetComponent<해양생활연출View>()?.SetPresentationSuppressed(false);}
        void OnDestroy()=>Clear();
    }
}
#endif
