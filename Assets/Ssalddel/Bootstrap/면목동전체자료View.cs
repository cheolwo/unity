using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Bootstrap
{
    // 개인 검토용 위치 표식. 건물 외곽/층고/출입구나 운영 권위가 아니다.
    public sealed class 면목동전체자료View : MonoBehaviour
    {
        [Serializable] private sealed class Data { public string revision,sourceHash; public Point[] markers; }
        [Serializable] private sealed class Point { public string id,name,address,category,floor,source,date; public float x,z; }
        public bool 전체보기;
        public Bounds 범위 {get;private set;}
        public int Count=>data?.markers.Length??0;
        private Data data;
        private string error="",search="";
        private Vector2 scroll;
        private GameObject detailRoot,densityRoot;
        private Camera observer;
        public int 중복표식제외수 {get;private set;}
        public int 밀집셀수 {get;private set;}
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly List<Material> materials=new List<Material>();
        public void Initialize()
        {
#if UNITY_EDITOR
            try {
                const string path="C:/Users/user/source/repos/Hongdal/artifacts/local/public-data/myeonmok-business-20260908-r1/wide-reference.json";
                var bytes=System.IO.File.ReadAllBytes(path);
                if(주소공간연결검사.Hash(bytes)!="E2420E86BDBCD323E7E52BA7431CF941091C44E0F2D74701C747B810F5FDF928")throw new Exception("WideHashMismatch");
                data=JsonUtility.FromJson<Data>(System.Text.Encoding.UTF8.GetString(bytes));
                if(data.revision!="myeonmok-wide-reference.r1"||data.markers.Length!=5411||data.markers.Select(p=>p.id).Distinct().Count()!=5411)throw new Exception("WideContractMismatch");
                var bounds=new Bounds(new Vector3(550,4,8),new Vector3(1000,8,1000));
                foreach(var p in data.markers)bounds.Encapsulate(new Vector3(p.x,8,p.z));
                범위=bounds;
                observer=GetComponentsInChildren<Camera>().FirstOrDefault(c=>c.name=="배달 관찰 카메라");
                if(observer==null)observer=FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.name=="배달 관찰 카메라");
                var linked=new HashSet<string>(GetComponent<면목동주소연결View>()?.ConnectedRecordIds()??Array.Empty<string>(),StringComparer.Ordinal);
                var standalone=data.markers.Where(p=>!linked.Contains(p.id)).ToArray();
                중복표식제외수=data.markers.Length-standalone.Length;
                detailRoot=new GameObject("면목동 근거리 참고");detailRoot.transform.SetParent(transform,false);
                densityRoot=new GameObject("면목동 원거리 밀집도 · 지형 아님");densityRoot.transform.SetParent(transform,false);
                // 개별 표식은 작게, 넓은 참고판은 제거. 조회 데이터는 그대로 보존한다.
                foreach(var group in standalone.GroupBy(p=>p.category)) {
                    var vertices=new List<Vector3>();var indices=new List<int>();
                    foreach(var p in group) {
                        int i=vertices.Count;float y=1,s=1.5f;
                        vertices.AddRange(new[]{new Vector3(p.x-s,y,p.z-s),new Vector3(p.x+s,y,p.z-s),new Vector3(p.x+s,y,p.z+s),new Vector3(p.x-s,y,p.z+s),new Vector3(p.x,4,p.z)});
                        indices.AddRange(new[]{i,i+4,i+1,i+1,i+4,i+2,i+2,i+4,i+3,i+3,i+4,i,i,i+2,i+1,i,i+3,i+2});
                    }
                    var obj=new GameObject("면목동 위치 참고 · "+group.Key);obj.layer=31;obj.transform.SetParent(detailRoot.transform,false);
                    var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                    obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var material=new Material(Resources.Load<Shader>("관찰도형명암")??Shader.Find("Unlit/Color"));
                    material.color=group.Key=="음식"?new Color(.65f,.38f,.29f):group.Key=="소매"?new Color(.67f,.61f,.4f):new Color(.38f,.58f,.62f);
                    material.SetColor("_BaseColor",material.color);
                    materials.Add(material);obj.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                var cells=standalone.GroupBy(p=>new Vector2Int(Mathf.FloorToInt((p.x-550)/100),Mathf.FloorToInt((p.z-8)/100))).ToArray();
                밀집셀수=cells.Length;
                foreach(var cell in cells) {
                    var obj=new GameObject("100m 참고 밀집 · "+cell.Count()+"관측");obj.layer=31;obj.transform.SetParent(densityRoot.transform,false);
                    float x=550+cell.Key.x*100+50,z=8+cell.Key.y*100+50,s=12+Mathf.Min(30,Mathf.Sqrt(cell.Count())*4);
                    var mesh=new Mesh();mesh.vertices=new[]{new Vector3(x-s,-.1f,z-s),new Vector3(x-s,-.1f,z+s),new Vector3(x+s,-.1f,z+s),new Vector3(x+s,-.1f,z-s)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                    obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var material=new Material(Resources.Load<Shader>("관찰도형명암"));material.color=Color.Lerp(new Color(.4f,.49f,.46f),new Color(.65f,.67f,.48f),Mathf.Clamp01(cell.Count()/60f));materials.Add(material);obj.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                LateUpdate();
            }catch(Exception e){data=null;error=e.Message;Debug.LogWarning("면목동 전체 자료 보류: "+error);}
#endif
        }
        private void LateUpdate()
        {
            bool distant=observer==null||observer.orthographicSize>350;
            if(detailRoot!=null)detailRoot.SetActive(전체보기&&!distant);
            if(densityRoot!=null)densityRoot.SetActive(전체보기&&distant);
        }
        private void OnGUI()
        {
            if(GUI.Button(new Rect(12,12,280,34),전체보기?"기존 구역으로 · 면목동 전체보기 끄기":"면목동 전체 자료 보기 · "+Count)) {
                전체보기=!전체보기;SendMessage("동네입체카메라갱신",SendMessageOptions.DontRequireReceiver);
            }
            if(!전체보기)return;
            GUILayout.BeginArea(new Rect(12,52,330,Mathf.Min(360,Screen.height-150)),GUI.skin.box);
            GUILayout.Label("면목동 자료 "+Count+"건 · 기존 건물 연결 "+중복표식제외수+"건\n멀리서는 100m 단위 밀집도 / 가까이서는 작은 표식\n밀집도는 건물·지형·영업 확정이 아닙니다.");
            GUILayout.Label("위경도 WGS84 가정 · 원천 측지기준 미확인\n기존 1km 외곽지도 보존 / 바깥 건물 미확보");
            if(data==null)GUILayout.Label(error);
            else {
                search=GUILayout.TextField(search,80);scroll=GUILayout.BeginScrollView(scroll);
                if(search.Length>0)foreach(var p in data.markers.Where(p=>p.name.Contains(search)||p.address.Contains(search)).Take(20))
                    GUILayout.Label(p.name+" · "+p.category+"\n"+p.address+"\n층 원문: "+p.floor+" · "+p.date,new GUIStyle(GUI.skin.label){wordWrap=true});
                else GUILayout.Label("이름/주소 검색 · 최대 20건 표시\nDB 재조회 사본 / 현재 영업 미확정");
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }
        private void OnDestroy(){foreach(var m in meshes)Destroy(m);foreach(var m in materials)Destroy(m);}
    }
}
