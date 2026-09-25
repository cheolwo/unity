#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public class 운송수단관찰Tests
    {
        static 운송수단관측 Item()=>new(){partner="CN",direction="export",mode="Sea",month="2025.12",port="항만",rawWeight="100",blCount=10,revision=new string('a',64),stableId="sample",sourceUrl="https://www.data.go.kr/data/15151809/openapi.do"};
        static 운송수단사본 Snapshot()=>new(){schemaVersion="transport-observation-preview.v1",selectionRule="LatestMonthLargestBlRow",year=2025,localReviewOnly=true,collectedAtUtc="2026-09-23T00:00:00Z",items=new[]{Item()}};
        [Test] public void Valid(){Assert.DoesNotThrow(()=>Snapshot().Validate());}
        [Test] public void Duplicate(){var s=Snapshot();s.items=new[]{Item(),Item()};Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void UnknownMode(){var s=Snapshot();s.items[0].mode="Unknown";Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void InvalidNumber(){var s=Snapshot();s.items[0].blCount=double.NaN;Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void PublicDenied(){var s=Snapshot();s.distributionApproved=true;Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void Reverse(){var a=Item();var end=운송수단관찰View.Position(a,1);a.direction="import";Assert.Less(Vector3.Distance(end,운송수단관찰View.Position(a,0)),.001f);}
        [Test] public void AirHigher(){var a=Item();var sea=운송수단관찰View.Position(a,.5f).magnitude;a.mode="Air";Assert.Greater(운송수단관찰View.Position(a,.5f).magnitude,sea);}
        [Test] public void SourceMismatch(){var s=Snapshot();s.items[0].sourceUrl="https://example.com";Assert.Throws<ArgumentException>(()=>s.Validate());}
    }
}
#endif
