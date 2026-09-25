using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public class 원두배송시각표본Tests
    {
        [TestCase(0,"출발 준비")][TestCase(4,"이동 중")][TestCase(30,"인계 대기")][TestCase(900,"인계 대기")]
        public void 표본단계는완료를확정하지않는다(float t,string stage)=>Assert.AreEqual(stage,원두배송시각표본View.Stage(t));
        [Test] public void 끝점에서반복하지않는다()=>Assert.AreEqual(원두배송시각표본View.RoutePosition(30),원두배송시각표본View.RoutePosition(900));
        [Test] public void 출발점과도착점(){Assert.AreEqual(new Vector3(-16,0,-2),원두배송시각표본View.RoutePosition(0));Assert.AreEqual(new Vector3(10,0,7),원두배송시각표본View.RoutePosition(30));}
    }
}
