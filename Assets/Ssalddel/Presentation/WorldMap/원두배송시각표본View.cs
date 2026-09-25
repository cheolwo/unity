#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    // 명시적으로 여는 시각 표본. 업무 Command/원장/서버와 연결하지 않는다.
    public sealed class 원두배송시각표본View : MonoBehaviour
    {
        public const string ShipmentId = "sample:coffee-delivery:01";
        public static float Progress(float seconds) => Mathf.Clamp01((seconds - 4f) / 26f);
        public static string Stage(float seconds) => seconds < 4 ? "출발 준비" : seconds < 30 ? "이동 중" : "인계 대기";
        public static Vector3 RoutePosition(float seconds)
        {
            float distance = Progress(seconds) * 35;
            return distance < 26 ? new Vector3(-16 + distance, 0, -2) : new Vector3(10, 0, -2 + distance - 26);
        }
        private readonly List<Material> materials = new List<Material>();
        private Camera previewCamera;
        private Transform truck, courier;
        private double started;
        private float elapsed;
        private bool paused, detail;
        private const int Layer = 28;
        private readonly Vector3 origin = new Vector3(15000, 0, 15000);
        public float Elapsed => elapsed;
        public Vector3 TruckPosition => truck.localPosition;
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += CloseBeforeReload;
        }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode) DestroyImmediate(gameObject);
        }
        private void CloseBeforeReload(){if(this!=null)DestroyImmediate(gameObject);}

        [MenuItem("Tools/Mirror/원두 배송 시각 샘플 열기")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("CoffeePreviewRequiresPlayMode");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SimulationWorldShell")
                throw new InvalidOperationException("CoffeePreviewRequiresCanonicalScene");
            if (Resources.FindObjectsOfTypeAll<원두배송시각표본View>().Length > 0) return;
            var root = new GameObject("원두 배송 — 가상 시각 표본") { hideFlags = HideFlags.DontSave };
            root.AddComponent<원두배송시각표본View>().Build();
        }
        private Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            materials.Add(material); return material;
        }
        private GameObject Shape(string label, Vector3 position, Vector3 size, Color color,
            Transform parent = null, PrimitiveType type = PrimitiveType.Cube)
        {
            var node = GameObject.CreatePrimitive(type); node.name = label; node.layer = Layer;
            node.transform.SetParent(parent == null ? transform : parent, false);
            node.transform.localPosition = position; node.transform.localScale = size;
            node.GetComponent<Renderer>().sharedMaterial = Material(color);
            Destroy(node.GetComponent<Collider>()); return node;
        }
        private void Building(string name, Vector3 position, Vector3 size, Color color)
        {
            Shape(name, position + Vector3.up * size.y / 2, size, color);
            Shape(name + " 옥상", position + Vector3.up * (size.y + .1f), new Vector3(size.x + .2f,.2f,size.z + .2f), new Color(.26f,.33f,.34f));
            for (int i = 0; i < 3; i++)
                Shape("창", position + new Vector3((i - 1) * size.x / 4, size.y * .6f, -size.z / 2 - .03f),
                    new Vector3(size.x / 7, size.y / 3, .08f), new Color(.94f,.78f,.43f));
        }
        private Transform Person(string name, Vector3 position, Color color)
        {
            var root = new GameObject(name); root.transform.SetParent(transform,false); root.transform.localPosition=position;
            Shape("몸", new Vector3(0,.65f,0),new Vector3(.45f,.65f,.4f),color,root.transform,PrimitiveType.Capsule);
            Shape("머리", new Vector3(0,1.25f,0),Vector3.one*.4f,new Color(.75f,.54f,.38f),root.transform,PrimitiveType.Sphere);
            return root.transform;
        }
        private void Build()
        {
            transform.position = origin;
            Shape("모형 지반",new Vector3(0,-.55f,1),new Vector3(45,1,29),new Color(.40f,.49f,.38f));
            Shape("합성 도로",new Vector3(0,.02f,-2),new Vector3(43,.12f,4),new Color(.19f,.23f,.25f));
            Shape("카페 진입 도로",new Vector3(10,.02f,4),new Vector3(4,.12f,12),new Color(.19f,.23f,.25f));
            for(int i=-20;i<22;i+=3) Shape("중앙선",new Vector3(i,.09f,-2),new Vector3(1.4f,.02f,.10f),new Color(.92f,.77f,.34f));
            Shape("보도",new Vector3(0,.05f,.6f),new Vector3(43,.18f,1.1f),new Color(.65f,.65f,.59f));
            Building("가상 원두 제조시설",new Vector3(-15,0,6),new Vector3(10,4,7),new Color(.76f,.76f,.66f));
            Shape("출고문",new Vector3(-15,1.4f,2.45f),new Vector3(3,2.8f,.13f),new Color(.17f,.36f,.40f));
            Building("가상 카페",new Vector3(15,0,7),new Vector3(6,3.8f,6),new Color(.86f,.73f,.54f));
            Shape("카페 차양",new Vector3(15,2.5f,3.5f),new Vector3(6.4f,.25f,1.5f),new Color(.73f,.36f,.20f));
            for(int i=0;i<4;i++) Building("배경 주택",new Vector3(-17+i*9,0,-10),new Vector3(5,3+i%2*2,5),new Color(.65f+i*.035f,.66f,.61f));
            for(int i=0;i<7;i++)
            {
                float x=-20+i*6;
                Shape("가로수 줄기",new Vector3(x,.8f,12),new Vector3(.25f,1.6f,.25f),new Color(.36f,.26f,.18f));
                Shape("가로수",new Vector3(x,2,12),new Vector3(1.8f,2.4f,1.8f),new Color(.23f,.40f,.23f),null,PrimitiveType.Sphere);
            }
            truck = new GameObject("배송 차량 01").transform; truck.SetParent(transform,false);
            Shape("적재함",new Vector3(-.4f,1.05f,0),new Vector3(2.4f,1.6f,1.5f),new Color(.91f,.90f,.83f),truck);
            Shape("운전석",new Vector3(1.35f,.85f,0),new Vector3(1.1f,1.2f,1.5f),new Color(.05f,.48f,.51f),truck);
            Shape("앞유리",new Vector3(1.92f,1.1f,0),new Vector3(.04f,.5f,1.25f),new Color(.15f,.25f,.29f),truck);
            foreach(float x in new[]{-1f,1.3f}) foreach(float z in new[]{-.78f,.78f})
                Shape("바퀴",new Vector3(x,.35f,z),new Vector3(.62f,.62f,.28f),new Color(.08f,.09f,.1f),truck,PrimitiveType.Sphere);
            courier=Person("배송 담당자",new Vector3(11.5f,0,6),new Color(.08f,.46f,.50f));
            Shape("원두 상자",new Vector3(.35f,.65f,0),Vector3.one*.55f,new Color(.65f,.43f,.23f),courier);
            Person("카페 담당자",new Vector3(14,0,3),new Color(.60f,.31f,.17f));
            var cameraNode=new GameObject("시각 표본 관찰 카메라");cameraNode.transform.SetParent(transform,false);
            previewCamera=cameraNode.AddComponent<Camera>();previewCamera.orthographic=true;previewCamera.depth=2000;
            previewCamera.cullingMask=1<<Layer;previewCamera.clearFlags=CameraClearFlags.SolidColor;
            previewCamera.backgroundColor=new Color(.34f,.48f,.58f);previewCamera.farClipPlane=150;previewCamera.nearClipPlane=.1f;
            var lamp=new GameObject("표본 조명");lamp.transform.SetParent(transform,false);lamp.transform.rotation=Quaternion.Euler(50,-35,0);
            var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<Layer;
            SetCloseView(false);started=Time.realtimeSinceStartupAsDouble;ApplyTime(0);
        }
        public void SetCloseView(bool close)
        {
            var focus=close?new Vector3(11,0,5):new Vector3(0,0,1);
            previewCamera.transform.localPosition=focus+(close?new Vector3(-24,32,-32):new Vector3(24,32,-32));
            previewCamera.transform.LookAt(origin+focus);previewCamera.orthographicSize=close?11:23;
        }
        public void ApplyTime(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            elapsed=Mathf.Max(0,seconds);truck.localPosition=RoutePosition(elapsed);
            truck.localRotation=Quaternion.Euler(0,Progress(elapsed)*35<26?0:-90,0);
            courier.gameObject.SetActive(elapsed>=30);
        }
        public void FreezeAt(float seconds){paused=true;ApplyTime(seconds);}
        private void Update(){if(!paused&&truck!=null)ApplyTime((float)(Time.realtimeSinceStartupAsDouble-started));}
        private void OnGUI()
        {
            GUI.depth=-10000;
            var previousColor=GUI.color;
            GUI.color=new Color(.10f,.16f,.20f,1);
            GUI.DrawTexture(new Rect(0,0,Screen.width,132),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height-95,Screen.width,95),Texture2D.whiteTexture);
            GUI.color=previousColor;
            var box=new GUIStyle(GUI.skin.box){fontSize=19,alignment=TextAnchor.MiddleLeft};
            GUI.Box(new Rect(16,16,560,62),"  원두 새벽 배송 · 가상 시각 샘플\n  실제 지도/운영 데이터 아님",box);
            if(GUI.Button(new Rect(16,88,110,36),"전체 보기"))SetCloseView(false);
            if(GUI.Button(new Rect(134,88,110,36),"카페 보기"))SetCloseView(true);
            if(GUI.Button(new Rect(252,88,110,36),"샘플 다시 보기")){started=Time.realtimeSinceStartupAsDouble;paused=false;ApplyTime(0);}
            if(GUI.Button(new Rect(370,88,80,36),"닫기"))Destroy(gameObject);
            if(GUI.Button(new Rect(16,Screen.height-65,280,45),"배송 차량 01 · "+Stage(elapsed)))detail=!detail;
            if(detail)GUI.Box(new Rect(310,Screen.height-90,470,70),"가상 원두 배송 / "+ShipmentId+"\n현재 연출: "+Stage(elapsed)+" · 업무 인수/재고 변경 없음",box);
        }
        private void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= CloseBeforeReload;
            foreach(var material in materials)if(material!=null)DestroyImmediate(material);
        }
    }
}
#endif
