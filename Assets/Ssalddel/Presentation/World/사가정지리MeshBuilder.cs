using System;
using System.Collections.Generic;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>동결 지리 자료의 Mesh 계산만 담당하며 업무 상태와 카메라를 소유하지 않습니다.</summary>
    public static class 사가정지리MeshBuilder
    {
        public static void Build(공간참고자료 data, Action<string,List<Vector3>,List<int>,Color> emit)
        {
            if (data == null || emit == null) throw new ArgumentNullException();
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
            emit("사가정 · 도로 선 · 폭은 기호",vertices,triangles,new Color(.29f,.32f,.34f));
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
            emit("사가정 · 건물 외곽 · 미상 높이 4m",vertices,triangles,new Color(.86f,.85f,.80f));

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
