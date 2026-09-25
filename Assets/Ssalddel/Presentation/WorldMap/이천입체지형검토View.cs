#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    // 외부 검토 자료만 읽는다. 빌드/Scene 저장/운영 상태에 포함되지 않는다.
    public sealed class 이천입체지형검토View : MonoBehaviour
    {
        [Serializable] public sealed class Data
        {
            public string schema, review; public int width; public float spacing, baseHeight, heightScale;
            public float[] heights, rgb;
        }
        private Mesh mesh; private Material material; private Camera view;
        private float yaw=20, pitch=38, distance=4200;
        private static readonly Vector3 Center=new Vector3(100000,0,100000);
        public int VertexCount=>mesh==null?0:mesh.vertexCount;
        public float ReliefHeight=>mesh==null?0:mesh.bounds.size.y;
        [MenuItem("Tools/Mirror/이천 실제 높이 지형 검토")]
        public static void Choose()
        {
            var path=EditorUtility.OpenFilePanel("unity-preview.json 선택", "", "json");
            if(!string.IsNullOrEmpty(path))Open(path);
        }
        public static void Open(string path)
        {
            if(!UnityEngine.Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="SimulationWorldShell")
                throw new InvalidOperationException("CanonicalPlayModeRequired");
            var data=JsonUtility.FromJson<Data>(File.ReadAllText(path));
            Validate(data);
            foreach(var prior in Resources.FindObjectsOfTypeAll<이천입체지형검토View>())DestroyImmediate(prior.gameObject);
            var root=new GameObject("이천 실제 DSM — 로컬 검토"){hideFlags=HideFlags.HideAndDontSave};
            root.AddComponent<이천입체지형검토View>().Build(data);
        }
        public static void Validate(Data d)
        {
            if(d==null || d.schema!="icheon-local-relief.r1" || d.review!="LocalEditorOnly_NotForDistribution"
                || d.width!=101 || d.spacing!=30 || d.heightScale!=1 || d.heights?.Length!=10201 || d.rgb?.Length!=30603)
                throw new InvalidDataException("TerrainContractInvalid");
            foreach(float value in d.heights)if(float.IsNaN(value)||float.IsInfinity(value)||value<d.baseHeight||value>d.baseHeight+1000)throw new InvalidDataException("HeightInvalid");
            foreach(float value in d.rgb)if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>255)throw new InvalidDataException("ColorInvalid");
        }
        private void Build(Data d)
        {
            transform.position=Center;gameObject.layer=31;
            int n=d.width;var vertices=new Vector3[n*n];var colors=new Color[n*n];var indices=new int[(n-1)*(n-1)*6];int t=0;
            for(int r=0;r<n;r++)for(int c=0;c<n;c++)
            {
                int i=r*n+c;vertices[i]=new Vector3((c-50)*d.spacing,d.heights[i]-d.baseHeight,(50-r)*d.spacing);
                colors[i]=new Color(d.rgb[i*3]/255f,d.rgb[i*3+1]/255f,d.rgb[i*3+2]/255f,1);
                if(r<n-1&&c<n-1){indices[t++]=i;indices[t++]=i+1;indices[t++]=i+n;indices[t++]=i+1;indices[t++]=i+n+1;indices[t++]=i+n;}
            }
            mesh=new Mesh{name="실측 DSM 상대 높이 1배",hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            // 위성 색에 메시 법선 음영을 더한다. 밝기를 높이로 사용하지 않는다.
            var normals=mesh.normals;var sun=new Vector3(-.4f,1,-.5f).normalized;
            for(int i=0;i<colors.Length;i++)colors[i]*=.55f+.45f*Mathf.Max(0,Vector3.Dot(normals[i],sun));
            mesh.colors=colors;gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Shader.Find("Sprites/Default")){hideFlags=HideFlags.HideAndDontSave};gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            var cameraNode=new GameObject("이천 검토 카메라"){hideFlags=HideFlags.HideAndDontSave};cameraNode.transform.SetParent(transform,false);
            view=cameraNode.AddComponent<Camera>();view.depth=10000;view.cullingMask=1<<31;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.12f,.18f,.24f);view.nearClipPlane=5;view.farClipPlane=12000;view.fieldOfView=48;Aim();
            AssemblyReloadEvents.beforeAssemblyReload+=Close;
        }
        public void SetView(float angle,float elevation,float range){yaw=angle;pitch=Mathf.Clamp(elevation,15,85);distance=Mathf.Clamp(range,900,6500);Aim();}
        private void Aim(){if(view==null)return;view.transform.position=Center+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);view.transform.LookAt(Center+Vector3.up*70);}
        private void OnGUI()
        {
            GUI.depth=-20000;
            GUI.Box(new Rect(12,12,560,84),"이천 실제 높이 지형 · 로컬 검토 / 높이 1배\nCopernicus DSM + Sentinel-2 (2025-11-12)\n3km × 3km / 30m 격자 · 건물/수목 포함 · 실제 공장/도로 아님");
            if(GUI.Button(new Rect(12,102,85,32),"회전"))SetView(yaw+30,pitch,distance);
            if(GUI.Button(new Rect(102,102,85,32),"확대"))SetView(yaw,pitch,distance*.75f);
            if(GUI.Button(new Rect(192,102,85,32),"축소"))SetView(yaw,pitch,distance/ .75f);
            if(GUI.Button(new Rect(282,102,85,32),"위에서"))SetView(yaw,80,distance);
            if(GUI.Button(new Rect(372,102,85,32),"닫기"))Close();
        }
        private void Close(){if(this!=null)DestroyImmediate(gameObject);}
        private void OnDestroy(){AssemblyReloadEvents.beforeAssemblyReload-=Close;if(mesh!=null)DestroyImmediate(mesh);if(material!=null)DestroyImmediate(material);}
    }
}
#endif
