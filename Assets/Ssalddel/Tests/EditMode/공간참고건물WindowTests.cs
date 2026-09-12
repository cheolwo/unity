using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공간참고건물WindowTests
    {
        private static 공간참고자료 Load() => JsonUtility.FromJson<공간참고자료>(Resources.Load<TextAsset>("SagajeongReference").text);
        private static 공간동적MapWindowPolicyData Policy(int radius) => new 공간동적MapWindowPolicyData
            {DetailRadius=radius,ActiveRadius=radius,PrefetchRadius=radius};
        [Test] public void 전체구역은_기존602개와_주소정보를_보존한다()
        {
            var data=Load();var found=공간참고건물WindowQuery.Query(data,data.offsetX,data.offsetZ,Policy(1));
            Assert.That(found.Length,Is.EqualTo(602));
            Assert.That(found.Select(b=>b.id).Distinct().Count(),Is.EqualTo(602));
            Assert.That(found.Count(b=>!string.IsNullOrEmpty(b.street)&&!string.IsNullOrEmpty(b.houseNumber)),Is.EqualTo(580));
            Assert.That(found.Count(b=>!string.IsNullOrEmpty(b.levelsText)),Is.EqualTo(33));
            Assert.That(found.All(b=>string.IsNullOrEmpty(b.unit)),Is.True);
        }
        [Test] public void 이동과재조회는_원본과식별자를_변경하지않는다()
        {
            var data=Load();var before=JsonUtility.ToJson(data);
            var first=공간참고건물WindowQuery.Query(data,550,8,Policy(0));
            Assert.That(first.Length,Is.GreaterThan(0).And.LessThan(602));
            Assert.That(공간참고건물WindowQuery.Query(data,10000,10000,Policy(0)),Is.Empty);
            CollectionAssert.AreEqual(first.Select(b=>b.id),공간참고건물WindowQuery.Query(data,550,8,Policy(0)).Select(b=>b.id));
            Assert.That(JsonUtility.ToJson(data),Is.EqualTo(before));
        }
        [Test] public void 잘못된좌표와중복은_거부한다()
        {
            var data=Load();Assert.Throws<ArgumentException>(()=>공간참고건물WindowQuery.Query(data,double.NaN,0,Policy(0)));
            data.buildings[1].id=data.buildings[0].id;
            Assert.Throws<ArgumentException>(()=>공간참고건물WindowQuery.Query(data,550,8,Policy(1)));
        }
        [Test] public void 경계건물은_자르거나중복하지않는다()
        {
            var data=new 공간참고자료{halfExtent=500,buildings=new[]{new 참고건물{id="edge",points=new[]{
                new 참고점{x=-1,z=1},new 참고점{x=1,z=1},new 참고점{x=1,z=2},new 참고점{x=-1,z=1}}}}};
            Assert.That(공간참고건물WindowQuery.Query(data,1,1,Policy(1)).Length,Is.EqualTo(1));
            Assert.That(공간참고건물WindowQuery.Query(data,-1,1,Policy(0)).Length,Is.EqualTo(1));
        }
    }
}
