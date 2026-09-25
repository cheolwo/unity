#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>관찰용 합성 경유점. 실제 항로·항만·AIS 또는 항행 가능성을 뜻하지 않는다.</summary>
    public static class 가상해양항로Catalog
    {
        private static readonly string[] Names = { "동해", "중국 연안", "중국·동남아", "일본 주변", "일본·미국 태평양", "서태평양·호주" };
        private static readonly Vector2[][] Routes = {
            new[]{new Vector2(35.2f,129.7f),new Vector2(35.4f,131),new Vector2(36.8f,132),new Vector2(37.5f,130.4f)},
            new[]{new Vector2(34,123),new Vector2(32,124),new Vector2(30,124)},
            new[]{new Vector2(31,124),new Vector2(28,125),new Vector2(25,123),new Vector2(21,123),new Vector2(20,120),new Vector2(18,116),new Vector2(10,111),new Vector2(3,105)},
            new[]{new Vector2(30,135),new Vector2(32,140),new Vector2(35,142)},
            new[]{new Vector2(35,142),new Vector2(32,145),new Vector2(27,155),new Vector2(24,180),new Vector2(30,-150),new Vector2(33,-120)},
            new[]{new Vector2(12,130),new Vector2(12,145),new Vector2(2,150),new Vector2(-10,157),new Vector2(-23,156),new Vector2(-32,154)}
        };
        public static int Count => Routes.Length;
        public static string Name(int route) => Names[route];
        public static Vector2 Interpolate(Vector2 a,Vector2 b,float t)
            => new Vector2(Mathf.Lerp(a.x,b.x,t),Mathf.Repeat(a.y+Mathf.DeltaAngle(a.y,b.y)*Mathf.Clamp01(t)+180,360)-180);
        public static Vector2 Point(int route,float phase)
        {
            var points=Routes[route];var p=Mathf.Clamp01(phase)*(points.Length-1);var i=Mathf.Min((int)p,points.Length-2);
            return Interpolate(points[i],points[i+1],p-i);
        }
        public static Vector2 Ship(int route,float time,int ship)
        {
            var duration=route==0?50f:150f;var t=Mathf.Repeat(time+ship*duration*.28f,duration);
            var travel=(duration-6)/2;var phase=t<3?0:t<3+travel?(t-3)/travel:t<6+travel?1:1-(t-6-travel)/travel;
            return Point(route,phase);
        }
        public static bool IsOcean(int route,Func<double,double,bool> ocean)
        {
            var points=Routes[route];
            for(var j=0;j<points.Length-1;j++)
            {
                var steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(points[j+1].x-points[j].x),Mathf.Abs(Mathf.DeltaAngle(points[j].y,points[j+1].y)))*8));
                for(var i=0;i<=steps;i++){var p=Interpolate(points[j],points[j+1],i/(float)steps);if(!ocean(p.x,p.y))return false;}
            }
            return true;
        }
    }
}
#endif
