#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    // 지형 위 상징적 이동. 도로/업무 상태/실제 위치가 아니다.
    public sealed class 한반도물류시각표본View : MonoBehaviour
    {
        public const float Radius=18.18f;
        public static Vector3 Position(float seconds)
        {
            float t=Mathf.Clamp01(seconds/40f);
            var a=지구위경도선View.Coordinate(37.27,127.44,1);
            var b=지구위경도선View.Coordinate(37.58,127.08,1);
            return Vector3.Slerp(a,b,t).normalized*Radius;
        }
        private 세계지구본View globe;
        private Transform surface,truck,start,end;
        private Camera camera;
        private Vector3 oldPosition;
        private Quaternion oldRotation;
        private float oldFov,elapsed;
        private bool oldEnabled,regional;
        private double began;
        private readonly List<Material> materials=new();
        private readonly List<(Renderer renderer,bool enabled)> hidden=new();
        private readonly List<(Behaviour component,bool enabled)> paused=new();
        private 한반도지형미리보기View terrain;
        private Ssalddel.Unity.Runtime.WorldMap.한반도지형미리보기단계 oldStage;
        private float oldBlend;
        public float Elapsed=>elapsed;
        public Vector3 TruckPosition=>truck.localPosition;
        [MenuItem("Tools/Mirror/한반도 지형 위 물류 샘플")]
        public static void Open()
        {
            if(!UnityEngine.Application.isPlaying)throw new System.InvalidOperationException("TerrainLogisticsRequiresPlay");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="SimulationWorldShell")throw new System.InvalidOperationException("CanonicalSceneRequired");
            if(Resources.FindObjectsOfTypeAll<한반도물류시각표본View>().Length>0)return;
            var g=FindFirstObjectByType<세계지구본View>();
            if(g==null)throw new System.InvalidOperationException("ExistingGlobeRequired");
            var node=new GameObject("한반도 물류 — 합성 연결 표본"){hideFlags=HideFlags.DontSave};
            var v=node.AddComponent<한반도물류시각표본View>();v.Build(g);
        }
        private Material Mat(Color color)
        {
            var m=new Material(Shader.Find("Sprites/Default")){color=color};materials.Add(m);return m;
        }
        private Transform Part(Transform parent,string name,Vector3 pos,Vector3 size,Color color)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.layer=세계지구본View.GlobeLayer;
            Destroy(o.GetComponent<Collider>());o.transform.SetParent(parent,false);o.transform.localPosition=pos;o.transform.localScale=size;
            o.GetComponent<Renderer>().sharedMaterial=Mat(color);return o.transform;
        }
        private void Build(세계지구본View owner)
        {
            globe=owner;surface=globe.transform.Find(세계지구본View.GlobeVisualRootName);
            transform.SetParent(surface,false);
            camera=globe.transform.Find(세계지구본View.GlobeCameraName).GetComponent<Camera>();
            oldPosition=camera.transform.position;oldRotation=camera.transform.rotation;oldFov=camera.fieldOfView;oldEnabled=globe.enabled;
            globe.enabled=false;
            terrain=globe.GetComponent<한반도지형미리보기View>();
            if(terrain!=null){oldStage=terrain.Stage;oldBlend=terrain.ZoomBlend;terrain.SetStage(Ssalddel.Unity.Runtime.WorldMap.한반도지형미리보기단계.Rivers);terrain.SetZoomBlend(1);}
            foreach(var behaviour in globe.GetComponents<MonoBehaviour>())
                if(behaviour is 해양생활연출View || behaviour is 지구위경도선View)
                {paused.Add((behaviour,behaviour.enabled));behaviour.enabled=false;}
            foreach(var renderer in surface.GetComponentsInChildren<Renderer>(true))
                if(renderer is LineRenderer || renderer.transform.IsChildOf(surface.Find("가상 해양 생활 · 실제 항로 아님")??transform))
                {hidden.Add((renderer,renderer.enabled));renderer.enabled=false;}
            truck=new GameObject("가상 원두 배송 01").transform;truck.SetParent(transform,false);
            Part(truck,"적재함",new Vector3(0,.3f,-.3f),new Vector3(.8f,.65f,1.2f),new Color(.96f,.94f,.8f));
            Part(truck,"운전석",new Vector3(0,.2f,.65f),new Vector3(.8f,.6f,.6f),new Color(.05f,.85f,.85f));
            foreach(float x in new[]{-.4f,.4f})foreach(float z in new[]{-.65f,.6f})Part(truck,"바퀴",new Vector3(x,0,z),new Vector3(.2f,.25f,.3f),Color.black);
            start=Part(transform,"이천권 가상 공급 거점",Position(0),Vector3.one*.018f,new Color(1,.65f,.1f));
            end=Part(transform,"서울권 가상 도착 거점",Position(40),Vector3.one*.018f,Color.cyan);
            var path=new GameObject("가상 연결선 — 실제 도로 아님");path.layer=세계지구본View.GlobeLayer;path.transform.SetParent(transform,false);
            var line=path.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=81;line.widthMultiplier=.0015f;line.sharedMaterial=Mat(new Color(1,.75f,.15f));
            for(int i=0;i<=80;i++)line.SetPosition(i,Position(i*.5f));
            began=Time.realtimeSinceStartupAsDouble;SetRegional(false);
            EditorApplication.playModeStateChanged+=PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=Close;
        }
        public void SetRegional(bool value){regional=value;Aim();}
        private void Aim()
        {
            if(camera==null)return;
            var center=regional?지구위경도선View.Coordinate(37.45,127.25,Radius):지구위경도선View.Coordinate(38,127.3,Radius);
            var target=surface.TransformPoint(center);
            var normal=surface.TransformDirection(center.normalized);
            var north=surface.TransformDirection(Vector3.ProjectOnPlane(Vector3.up,center.normalized).normalized);
            camera.transform.position=target+normal*8;
            camera.transform.rotation=Quaternion.LookRotation(-normal,north);
            camera.fieldOfView=regional?4f:40f;
        }
        private void Update()
        {
            if(truck==null)return;
            elapsed=(float)(Time.realtimeSinceStartupAsDouble-began);
            var p=Position(elapsed);truck.localPosition=p;
            var direction=Position(Mathf.Min(elapsed,39.9f)+.1f)-Position(Mathf.Min(elapsed,39.9f));
            truck.localRotation=Quaternion.LookRotation(direction.normalized,p.normalized);
            truck.localScale=Vector3.one*(regional?.018f:.033f);
            start.localScale=end.localScale=Vector3.one*(regional?.006f:.023f);
        }
        private void OnGUI()
        {
            if(camera==null)return;
            GUI.depth=-9000;
            var oldColor=GUI.color;GUI.color=new Color(.08f,.13f,.17f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,116),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,Screen.height-70,Screen.width,70),Texture2D.whiteTexture);GUI.color=oldColor;
            GUI.Box(new Rect(12,12,470,66),"한반도 지형 위 물류 · 가상 표본\n이천권 → 서울권 / 실제 도로·운행 아님");
            if(GUI.Button(new Rect(12,82,105,30),"한반도 보기"))SetRegional(false);
            if(GUI.Button(new Rect(122,82,105,30),"수도권 확대"))SetRegional(true);
            if(GUI.Button(new Rect(232,82,105,30),"다시 보기"))began=Time.realtimeSinceStartupAsDouble;
            if(GUI.Button(new Rect(342,82,65,30),"닫기"))Close();
            if(regional){Label(start,"이천권 · 가상 공급");Label(end,"서울권 · 가상 도착");}
            else Label(end,"수도권 · 원두 배송");
            GUI.Box(new Rect(12,Screen.height-45,340,32),elapsed<40?"원두 배송 01 · 이동 연출":"원두 배송 01 · 도착 대기 (인수 아님)");
        }
        private void Label(Transform point,string label)
        {
            if(point==null)return;var p=camera.WorldToScreenPoint(point.position);
            if(p.z>0)GUI.Label(new Rect(p.x+9,Screen.height-p.y-12,180,25),label);
        }
        private void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Close();}
        private void Close(){if(this!=null)DestroyImmediate(gameObject);}
        private void OnDestroy()
        {
            EditorApplication.playModeStateChanged-=PlayChanged;AssemblyReloadEvents.beforeAssemblyReload-=Close;
            if(camera!=null){camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=oldFov;}
            if(globe!=null)globe.enabled=oldEnabled;
            if(terrain!=null){terrain.SetStage(oldStage);terrain.SetZoomBlend(oldBlend);}
            foreach(var item in hidden)if(item.renderer!=null)item.renderer.enabled=item.enabled;
            foreach(var item in paused)if(item.component!=null)item.component.enabled=item.enabled;
            foreach(var m in materials)if(m!=null)DestroyImmediate(m);
        }
    }
}
#endif
