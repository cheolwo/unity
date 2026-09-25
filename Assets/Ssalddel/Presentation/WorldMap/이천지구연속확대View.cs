#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
namespace Ssalddel.Unity.Presentation.WorldMap
{
    // 로컬 검토 전용. 동일 지구·카메라에서 이천 위경도에 결속한다.
    public sealed class 이천지구연속확대View:MonoBehaviour
    {
        [Serializable] public class Geo { public double[] latitudes,longitudes; public int textureWidth; public int[] textureRgb; }
        const float Radius=18.076f, Scale=18f/6371000f;
        Camera camera; Transform surface; 세계지구본View globe; Mesh mesh; Material material;
        Vector3 oldPosition; Quaternion oldRotation; float oldNear,oldFar,oldFov; bool oldEnabled;
        float zoom,target; readonly List<(Renderer r,bool enabled)> hidden=new();
        readonly List<(Renderer r,bool enabled)> symbols=new(); Texture2D texture;
        public int SymbolCount=>symbols.Count;
        public static float EdgeOpacity(int row,int column)
        {return Mathf.SmoothStep(0,1,Mathf.Min(Mathf.Min(row,100-row),Mathf.Min(column,100-column))/8f);}
        한반도지형미리보기View terrain; Ssalddel.Unity.Runtime.WorldMap.한반도지형미리보기단계 oldStage; float oldBlend;
        public float Zoom=>zoom;
        public Vector3 FocusScreen=>camera.WorldToViewportPoint(surface.TransformPoint(지구위경도선View.Coordinate(37.27,127.44,Radius)));
        [MenuItem("Tools/Mirror/지구에서 이천 연속 확대")]
        public static void Choose(){var path=EditorUtility.OpenFilePanel("unity-preview.json 선택","","json");if(path.Length>0)Open(path);}
        public static void Open(string path)
        {
            if(!UnityEngine.Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="SimulationWorldShell")throw new InvalidOperationException("CanonicalPlayRequired");
            if(Resources.FindObjectsOfTypeAll<이천지구연속확대View>().Length>0)return;
            var text=File.ReadAllText(path);var d=JsonUtility.FromJson<이천입체지형검토View.Data>(text);이천입체지형검토View.Validate(d);
            var geo=JsonUtility.FromJson<Geo>(text);
            if(geo.latitudes?.Length!=10201||geo.longitudes?.Length!=10201)throw new InvalidDataException("GeoreferenceMissing");
            if(geo.textureWidth!=301||geo.textureRgb?.Length!=301*301*3)throw new InvalidDataException("TextureMissing");
            foreach(var channel in geo.textureRgb)if(channel<0||channel>255)throw new InvalidDataException("TextureInvalid");
            for(int i=0;i<10201;i++)if(!(geo.latitudes[i]>37.2&&geo.latitudes[i]<37.4&&geo.longitudes[i]>127.3&&geo.longitudes[i]<127.6))throw new InvalidDataException("CoordinateInvalid");
            var owner=FindFirstObjectByType<세계지구본View>();if(owner==null)throw new InvalidOperationException("GlobeMissing");
            var root=new GameObject("이천 지리 결속 연속 확대"){hideFlags=HideFlags.HideAndDontSave};root.AddComponent<이천지구연속확대View>().Build(owner,d,geo);
        }
        void Build(세계지구본View owner,이천입체지형검토View.Data d,Geo geo)
        {
            globe=owner;surface=owner.transform.Find(세계지구본View.GlobeVisualRootName);transform.SetParent(surface,false);gameObject.layer=세계지구본View.GlobeLayer;
            terrain=owner.GetComponent<한반도지형미리보기View>();if(terrain!=null){oldStage=terrain.Stage;oldBlend=terrain.ZoomBlend;terrain.SetStage(Ssalddel.Unity.Runtime.WorldMap.한반도지형미리보기단계.Relief);terrain.SetZoomBlend(1);}
            camera=owner.transform.Find(세계지구본View.GlobeCameraName).GetComponent<Camera>();oldPosition=camera.transform.position;oldRotation=camera.transform.rotation;oldNear=camera.nearClipPlane;oldFar=camera.farClipPlane;oldFov=camera.fieldOfView;oldEnabled=owner.enabled;owner.enabled=false;
            var vertices=new Vector3[10201];var colors=new Color[10201];var uv=new Vector2[10201];var triangles=new int[60000];int t=0;
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=지구위경도선View.Coordinate(geo.latitudes[i],geo.longitudes[i],Radius+(d.heights[i]-d.baseHeight)*Scale);
                // 경계 투명도는 자료 공백을 숨기는 지형 생성이 아닌 표현 블렌드다.
                colors[i]=new Color(1,1,1,EdgeOpacity(i/101,i%101));
                uv[i]=new Vector2((i%101*3+.5f)/301f,1-(i/101*3+.5f)/301f);
                if(i/101<100&&i%101<100){int a=i,b=i+1,c=i+101;
                    triangles[t++]=a;triangles[t++]=b;triangles[t++]=c;triangles[t++]=b;triangles[t++]=i+102;triangles[t++]=c;}
            }
            // 좌표계의 손잡이 방향에 관계없이 지구 바깥쪽 법선을 보장한다.
            for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),vertices[a])<0){triangles[i+1]=c;triangles[i+2]=b;}}
            mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.triangles=triangles;mesh.colors=colors;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
            texture=new Texture2D(301,301,TextureFormat.RGB24,true){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
            var pixels=new Color32[301*301];for(int row=0;row<301;row++)for(int col=0;col<301;col++){int source=(row*301+col)*3;pixels[(300-row)*301+col]=new Color32((byte)geo.textureRgb[source],(byte)geo.textureRgb[source+1],(byte)geo.textureRgb[source+2],255);}texture.SetPixels32(pixels);texture.Apply(true,true);
            material=new Material(Shader.Find("Sprites/Default")){hideFlags=HideFlags.HideAndDontSave,mainTexture=texture};gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            foreach(var name in new[]{"2025 해상·항공 통계 관계 · 실제 항로 아님","가상 해양 생활 · 실제 항로 아님"}){var root=surface.Find(name);if(root!=null)foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(!(r is LineRenderer))symbols.Add((r,r.enabled));}
            foreach(var r in surface.GetComponentsInChildren<Renderer>(true))if(r is LineRenderer){hidden.Add((r,r.enabled));r.enabled=false;}
            AssemblyReloadEvents.beforeAssemblyReload+=Close;Apply();
        }
        public void SetZoom(float value){target=Mathf.Clamp01(value);}
        void Update()
        {
            var mouse=Mouse.current;if(mouse!=null && mouse.position.ReadValue().y<Screen.height-100)target=Mathf.Clamp01(target+mouse.scroll.ReadValue().y/120f*.045f);
            zoom=Mathf.Lerp(zoom,target,1-Mathf.Exp(-Time.unscaledDeltaTime*5));Apply();
        }
        void Apply()
        {
            var p=지구위경도선View.Coordinate(37.27,127.44,Radius);var n=surface.TransformDirection(p.normalized);var north=surface.TransformDirection(Vector3.ProjectOnPlane(Vector3.up,p).normalized);
            var focus=surface.TransformPoint(p);float distance=Mathf.Exp(Mathf.Lerp(Mathf.Log(45),Mathf.Log(.014f),zoom));
            camera.transform.position=focus+n*distance-north*distance*.22f;camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position,north);
            camera.nearClipPlane=Mathf.Max(.00003f,distance*.005f);camera.farClipPlane=100;camera.fieldOfView=48;
            foreach(var s in symbols)if(s.r!=null)s.r.enabled=s.enabled&&zoom<.28f;
            material.color=new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.9f,zoom)));
        }
        void OnGUI(){GUI.depth=-21000;GUI.Box(new Rect(12,12,550,80),"지구 → 이천 · 같은 위경도 연속 확대\n마우스 휠로 확대/축소 · DSM 높이 1배 · 로컬 검토\n상세 경계/행정 정보 통합은 후속");if(GUI.Button(new Rect(570,12,70,35),"닫기"))Close();}
        void Close(){if(this!=null)DestroyImmediate(gameObject);}
        void OnDestroy(){AssemblyReloadEvents.beforeAssemblyReload-=Close;if(camera!=null){camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.nearClipPlane=oldNear;camera.farClipPlane=oldFar;camera.fieldOfView=oldFov;}if(globe!=null)globe.enabled=oldEnabled;if(terrain!=null){terrain.SetStage(oldStage);terrain.SetZoomBlend(oldBlend);}foreach(var x in hidden)if(x.r!=null)x.r.enabled=x.enabled;foreach(var x in symbols)if(x.r!=null)x.r.enabled=x.enabled;if(mesh!=null)DestroyImmediate(mesh);if(material!=null)DestroyImmediate(material);if(texture!=null)DestroyImmediate(texture);}
    }
}
#endif
