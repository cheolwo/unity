using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;

namespace Ssalddel.Unity.Tests.EditMode
{
    public class 행정구역HierarchyTests
    {
        [Test] public void 코드접두사와관계없이부모로조회한다()
        {
            var h=new 행정구역Hierarchy(new[]{new 행정구역Node("province",null,"도"),new 행정구역Node("city","province","시"),new 행정구역Node("dong","city","동")});
            Assert.That(h.Children("province")[0].Id,Is.EqualTo("city"));
            Assert.That(h.IsChild("dong","city"),Is.True);
            Assert.That(h.IsChild("dong","province"),Is.False);
        }
        [Test] public void 구가없는시도읍면으로내려간다()
        {
            var h=new 행정구역Hierarchy(new[]{new 행정구역Node("city",null,"시"),new 행정구역Node("town","city","읍")});
            Assert.That(h.Children("city").Length,Is.EqualTo(1));
            Assert.That(h.HasChildren("town"),Is.False);
            Assert.That(h.Children("missing"),Is.Empty);
        }
        [Test] public void 고아부모를거절한다()=>Assert.Throws<ArgumentException>(()=>new 행정구역Hierarchy(new[]{new 행정구역Node("a","missing","동")}));
        [Test] public void 순환을거절한다()=>Assert.Throws<ArgumentException>(()=>new 행정구역Hierarchy(new[]{new 행정구역Node("a","b","도"),new 행정구역Node("b","a","시")}));
        [Test] public void 중복을거절한다()=>Assert.Throws<ArgumentException>(()=>new 행정구역Hierarchy(new[]{new 행정구역Node("a",null,"도"),new 행정구역Node("a",null,"시")}));
        [Test] public void 표시순서가입력순서에좌우되지않는다()
        {
            var h=new 행정구역Hierarchy(new[]{new 행정구역Node("b",null,"나"),new 행정구역Node("a",null,"가")});
            Assert.That(h.Children(null)[0].Id,Is.EqualTo("a"));
        }
    }
}
