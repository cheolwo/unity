using System;
using System.Collections.Generic;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>동결 지리 자료의 Mesh 계산만 담당하며 업무 상태와 카메라를 소유하지 않습니다.</summary>
    public static class 사가정지리MeshBuilder
    {
        public static void Build(공간참고자료 data, Action<string,List<Vector3>,List<int>,Color> emit,
            건물높이보완조회 heights = null)
        {
            if (data == null || emit == null) throw new ArgumentNullException();
            BuildRoadMesh(data, emit, _ => true, "사가정 · 도로 선 · 폭은 기호",
                new Color(.29f,.32f,.34f), LegacyRoadWidth, .02f);
            BuildBuildingMesh(data, emit, heights, null, false);
        }

        public static void Build(공간참고자료 data,
            Action<string,List<Vector3>,List<int>,Color> emit,
            건물높이보완조회 heights,
            사가정Synty표현Plan visualPlan)
        {
            if (data == null || emit == null) throw new ArgumentNullException();
            BuildRoadMesh(data, emit, IsMajorRoad, "사가정 · 큰길 · 위계 표현",
                new Color(.22f,.25f,.28f), EnhancedRoadWidth, .035f);
            BuildRoadMesh(data, emit, IsLocalRoad, "사가정 · 생활도로 · 위계 표현",
                new Color(.34f,.36f,.37f), EnhancedRoadWidth, .03f);
            BuildRoadMesh(data, emit, IsPedestrianRoad, "사가정 · 보행로 · 위계 표현",
                new Color(.55f,.51f,.44f), EnhancedRoadWidth, .025f);
            BuildSidewalkMesh(data, emit);
            BuildBuildingMesh(data, emit, heights, visualPlan, true);
        }

        private static void BuildRoadMesh(공간참고자료 data,
            Action<string,List<Vector3>,List<int>,Color> emit,
            Func<참고도로,bool> include, string name, Color color,
            Func<string,float> widthForKind, float y)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d) {
                int i=vertices.Count; vertices.AddRange(new[]{a,b,c,d});
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});
            }
            foreach(var road in data.roads) {
                if (!include(road)) continue;
                var a=new Vector3(road.x1,y,road.z1);var b=new Vector3(road.x2,y,road.z2);
                if((b-a).sqrMagnitude<.01f)continue;
                // 선 위치는 원문, 폭은 판독용 기호. 차량 통행 계약을 만들지 않는다.
                var width=widthForKind(road.kind);
                var side=Vector3.Cross((b-a).normalized,Vector3.up)*width*.5f;
                Quad(a-side,a+side,b+side,b-side);
            }
            if(vertices.Count>0)emit(name,vertices,triangles,color);
        }

        private static void BuildSidewalkMesh(공간참고자료 data,
            Action<string,List<Vector3>,List<int>,Color> emit)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d) {
                var i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});
            }
            foreach(var road in data.roads)
            {
                if(IsPedestrianRoad(road))continue;
                var a=new Vector3(road.x1,.055f,road.z1);var b=new Vector3(road.x2,.055f,road.z2);
                if((b-a).sqrMagnitude<.01f)continue;
                var normal=Vector3.Cross((b-a).normalized,Vector3.up);
                var half=EnhancedRoadWidth(road.kind)*.5f;
                const float sidewalk=1.15f;
                foreach(var sign in new[]{-1f,1f})
                {
                    var inner=normal*(half+.08f)*sign;
                    var outer=normal*(half+sidewalk)*sign;
                    Quad(a+inner,a+outer,b+outer,b+inner);
                }
            }
            if(vertices.Count>0)emit("사가정 · 연석과 보도 · 표현 전용",vertices,triangles,
                new Color(.67f,.63f,.55f));
        }

        private static void BuildBuildingMesh(공간참고자료 data,
            Action<string,List<Vector3>,List<int>,Color> emit,
            건물높이보완조회 heights,
            사가정Synty표현Plan visualPlan,
            bool enhanced)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var midVertices=new List<Vector3>();var midTriangles=new List<int>();
            var highVertices=new List<Vector3>();var highTriangles=new List<int>();
            var plinthVertices=new List<Vector3>();var plinthTriangles=new List<int>();
            void Quad(List<Vector3> targetVertices,List<int> targetTriangles,
                Vector3 a,Vector3 b,Vector3 c,Vector3 d) {
                var i=targetVertices.Count;targetVertices.AddRange(new[]{a,b,c,d});
                targetTriangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});
            }
            // 현재는 전체 1km 구역 보존. 이동형 소비자도 같은 데이터/창 계산을 재사용할 수 있다.
            foreach(var building in 공간참고건물WindowQuery.Query(data,data.offsetX,data.offsetZ,
                new 공간동적MapWindowPolicyData {DetailRadius=1,ActiveRadius=1,PrefetchRadius=1})) {
                var points=building.points;
                var isSynty=visualPlan?.Replaces(building.id)==true;
                var height=heights?.Resolve(building) ?? building.height;
                var targetVertices=isSynty?plinthVertices
                    :enhanced&&height>20f?highVertices
                    :enhanced&&height>6f?midVertices:vertices;
                var targetTriangles=isSynty?plinthTriangles
                    :enhanced&&height>20f?highTriangles
                    :enhanced&&height>6f?midTriangles:triangles;
                var meshHeight=isSynty ? .6f : height;
                for(int i=1;i<points.Length;i++) {
                    var a=new Vector3(points[i-1].x,.06f,points[i-1].z);var b=new Vector3(points[i].x,.06f,points[i].z);
                    Quad(targetVertices,targetTriangles,a,b,b+Vector3.up*meshHeight,a+Vector3.up*meshHeight);
                }
                var polygon=new List<Vector2>();
                for(int i=0;i<points.Length-1;i++)polygon.Add(new Vector2(points[i].x,points[i].z));
                var roof=참고지붕삼각형(polygon);
                var start=targetVertices.Count;
                foreach(var p in polygon) targetVertices.Add(new Vector3(p.x,meshHeight+.06f,p.y));
                foreach(var t in roof) targetTriangles.Add(start+t);
            }
            if(vertices.Count>0)emit(enhanced?"사가정 · 건물 외곽 · 자료 높이와 fallback":"사가정 · 건물 외곽 · 미상 높이 4m",
                vertices,triangles,enhanced?new Color(.82f,.80f,.73f):new Color(.86f,.85f,.80f));
            if(midVertices.Count>0)emit("사가정 · 중층 건물 · 자료 높이와 fallback",
                midVertices,midTriangles,new Color(.78f,.72f,.63f));
            if(highVertices.Count>0)emit("사가정 · 고층 건물 · 자료 높이와 fallback",
                highVertices,highTriangles,new Color(.68f,.72f,.74f));
            if(plinthVertices.Count>0)emit("사가정 · Synty 접지 기단 · 원문 외곽",
                plinthVertices,plinthTriangles,new Color(.54f,.51f,.45f));
        }

        private static float LegacyRoadWidth(string kind)
            => kind=="primary"||kind=="secondary"?10:kind=="footway"||kind=="steps"?1.5f:4;

        private static float EnhancedRoadWidth(string kind) => kind switch
        {
            "primary" => 15f,
            "secondary" => 12f,
            "secondary_link" => 8f,
            "tertiary" => 8f,
            "residential" => 5.5f,
            "service" => 4.5f,
            "pedestrian" => 3.5f,
            "cycleway" => 2.4f,
            "footway" => 2.2f,
            "path" => 1.8f,
            "steps" => 1.6f,
            _ => 4f,
        };

        private static bool IsMajorRoad(참고도로 road)
            => road.kind is "primary" or "secondary" or "secondary_link" or "tertiary";
        private static bool IsPedestrianRoad(참고도로 road)
            => road.kind is "pedestrian" or "cycleway" or "footway" or "path" or "steps";
        private static bool IsLocalRoad(참고도로 road) => !IsMajorRoad(road)&&!IsPedestrianRoad(road);
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
