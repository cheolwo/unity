using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 국가무역관찰Tests
    {
        private static 국가무역Snapshot Sample()=>new 국가무역Snapshot {
            schemaVersion="country-trade-preview.v1",year=2025,localReviewOnly=true,transportMode="Unknown",
            sourceUrl="https://www.data.go.kr/data/15100475/openapi.do",collectedAtUtc="2026-09-23T01:00:00Z",
            items=국가무역Snapshot.Countries.SelectMany(c=>new[]{"export","import"}.Select(d=>new 국가무역Item{partner=c,direction=d,kg=12e9,usd=100,revision=new string('a',64)})).ToArray() };
        [Test] public void 열개흐름과공통배율(){var s=Sample();s.Validate();Assert.That(s.UnitKg,Is.EqualTo(3e9));Assert.That(s.items.Sum(s.Count),Is.EqualTo(40));}
        [Test] public void 중복흐름거부(){var s=Sample();s.items[1]=s.items[0];Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void 미확보수단을해상으로위장하지않는다(){var s=Sample();s.transportMode="Sea";Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void 누락중량은영이아니다(){var json=JsonUtility.ToJson(Sample()).Replace("\"kg\":12000000000.0,","").Replace("\"kg\":12000000000,","");Assert.That(json,Does.Not.Contain("\"kg\""));Assert.Throws<ArgumentException>(()=>JsonUtility.FromJson<국가무역Snapshot>(json).Validate());}
        [Test] public void 음수와무한수거부(){var s=Sample();s.items[0].kg=-1;Assert.Throws<ArgumentException>(()=>s.Validate());s.items[0].kg=double.PositiveInfinity;Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void 자료없음과실제영구분(){var s=Sample();foreach(var i in s.items)i.kg=0;s.Validate();Assert.That(s.items.Sum(s.Count),Is.Zero);s.items=null;Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void 잔량까지중량합계보존(){var s=Sample();s.items[0].kg=12.4e9;foreach(var i in s.items)Assert.That(Enumerable.Range(0,s.Count(i)).Sum(n=>s.Fraction(i,n)*s.UnitKg),Is.EqualTo(i.kg).Within(.001));Assert.That(s.items.Sum(s.Count),Is.LessThanOrEqualTo(50));}
        [Test] public void 수출입끝점은반대방향(){foreach(var c in 국가무역Snapshot.Countries){Assert.That(Vector3.Distance(국가무역관찰View.Position(c,"export",0),국가무역관찰View.Position(c,"import",1)),Is.LessThan(.001));Assert.That(Vector3.Distance(국가무역관찰View.Position(c,"export",1),국가무역관찰View.Position(c,"import",0)),Is.LessThan(.001));}}
        [Test] public void 곡선은지구밖통계관계이고결정적이다(){foreach(var c in 국가무역Snapshot.Countries)for(var p=0;p<=100;p++){var v=국가무역관찰View.Position(c,"export",p/100f);Assert.That(v.magnitude,Is.GreaterThanOrEqualTo(18.29f));Assert.That(v,Is.EqualTo(국가무역관찰View.Position(c,"export",p/100f)));}}
        [Test] public void 배포승인위장과출처변경거부(){var s=Sample();s.distributionApproved=true;Assert.Throws<ArgumentException>(()=>s.Validate());s.distributionApproved=false;s.sourceUrl="unknown";Assert.Throws<ArgumentException>(()=>s.Validate());}
    }
}
