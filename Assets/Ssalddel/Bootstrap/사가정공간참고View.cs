using System;
using System.Collections.Generic;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        private readonly List<Mesh> 참고Meshes = new();

        private void 사가정참고조립()
        {
            var asset = Resources.Load<TextAsset>("SagajeongReference");
            if (asset == null) { Debug.LogError("사가정 공간 참고 자료가 없습니다."); return; }
            var data=JsonUtility.FromJson<공간참고자료>(asset.text);
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d) {
                int i=vertices.Count; vertices.AddRange(new[]{a,b,c,d});
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});
            }
            foreach(var road in data.roads) {
                var a=new Vector3(road.x1,.02f,road.z1);var b=new Vector3(road.x2,.02f,road.z2);
                if((b-a).sqrMagnitude<.01f)continue;
                // 선 위치는 원문, 폭은 판독용 기호. 차량 통행 계약을 만들지 않는다.
                float width=road.kind=="primary"||road.kind=="secondary"?10:road.kind=="footway"||road.kind=="steps"?1.5f:4;
                var side=Vector3.Cross((b-a).normalized,Vector3.up)*width*.5f;
                Quad(a-side,a+side,b+side,b-side);
            }
            참고Mesh("사가정 · 도로 선 · 폭은 기호",vertices,triangles,new Color(.32f,.35f,.37f));
            vertices.Clear();triangles.Clear();
            // 현재는 전체 1km 구역 보존. 이동형 소비자도 같은 데이터/창 계산을 재사용할 수 있다.
            foreach(var building in 공간참고건물WindowQuery.Query(data,data.offsetX,data.offsetZ,
                new 공간동적MapWindowPolicyData {DetailRadius=1,ActiveRadius=1,PrefetchRadius=1})) {
                var points=building.points;
                for(int i=1;i<points.Length;i++) {
                    var a=new Vector3(points[i-1].x,.06f,points[i-1].z);var b=new Vector3(points[i].x,.06f,points[i].z);
                    Quad(a,b,b+Vector3.up*building.height,a+Vector3.up*building.height);
                }
                var polygon=new List<Vector2>();
                for(int i=0;i<points.Length-1;i++)polygon.Add(new Vector2(points[i].x,points[i].z));
                var roof=참고지붕삼각형(polygon);
                int start=vertices.Count;
                foreach(var p in polygon) vertices.Add(new Vector3(p.x,building.height+.06f,p.y));
                foreach(var t in roof) triangles.Add(start+t);
            }
            참고Mesh("사가정 · 건물 외곽 · 미상 높이 4m",vertices,triangles,new Color(.68f,.73f,.76f));
            var station=Block("사가정역 기준점",new Vector3(550,5,8),new Vector3(14,10,14),new Color(.65f,.75f,.15f));
            Label(station,"사가정역\n1km × 1km",Color.white);
            var label=station.GetComponentInChildren<TextMesh>(); if(label!=null)label.transform.localScale*=8;
            // 자료가 없거나 판본이 바뀌면 주소 패널만 보류하고 기존 지도/업무 상태는 보존한다.
            var addressView=GetComponent<면목동주소연결View>()??gameObject.AddComponent<면목동주소연결View>();
            addressView.Initialize(data,asset.bytes,observer);
            var wide=GetComponent<면목동전체자료View>()??gameObject.AddComponent<면목동전체자료View>();
            wide.Initialize();
        }

        private void 참고Mesh(string name,List<Vector3> vertices,List<int> triangles,Color color)
        {
            var obj=new GameObject(name);obj.layer=31;obj.transform.SetParent(transform,false);
            var mesh=new Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            참고Meshes.Add(mesh);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var shader=Resources.Load<Shader>("관찰도형명암")??Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            var material=new Material(shader);material.color=color;materials.Add(material);
            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        // 단일 외곽 다각형의 오목 지붕. 실패 시 벽만 유지하며 잘못된 면을 채우지 않는다.
        private static List<int> 참고지붕삼각형(List<Vector2> points)
        {
            float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            var indices=new List<int>();for(int i=0;i<points.Count;i++)indices.Add(i);
            float area=0;for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];area+=a.x*b.y-b.x*a.y;}
            if(area<0)indices.Reverse();
            var result=new List<int>();int guard=points.Count*points.Count;
            while(indices.Count>2 && guard-->0) {
                bool found=false;
                for(int i=0;i<indices.Count;i++) {
                    int a=indices[(i+indices.Count-1)%indices.Count],b=indices[i],c=indices[(i+1)%indices.Count];
                    if(Cross(points[a],points[b],points[c])<=.0001f)continue;
                    bool occupied=false;
                    foreach(int n in indices)if(n!=a&&n!=b&&n!=c&&Cross(points[a],points[b],points[n])>=0&&Cross(points[b],points[c],points[n])>=0&&Cross(points[c],points[a],points[n])>=0){occupied=true;break;}
                    if(occupied)continue;
                    result.AddRange(new[]{a,c,b});indices.RemoveAt(i);found=true;break;
                }
                if(!found)return new List<int>();
            }
            return result;
        }
    }
}
