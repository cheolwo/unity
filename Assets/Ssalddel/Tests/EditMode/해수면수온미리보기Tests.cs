using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 해수면수온미리보기Tests
    {
        private static 해수면수온PreviewSnapshot Sample()
        {
            var first=new DateTimeOffset(2026,9,1,12,0,0,TimeSpan.Zero);
            return new 해수면수온PreviewSnapshot {
                schemaVersion="marine-sst-preview.v1",sourceId="noaa-coastwatch",datasetId="noaacwBLENDEDsstDNDaily",
                sourceUrl="https://coastwatch.noaa.gov/erddap/info/noaacwBLENDEDsstDNDaily/index.json",
                sourceHash=new string('a',64),resultHash=new string('b',64),collectedAtUtc="2026-09-22T00:00:00Z",
                localReviewOnly=true,distributionApproved=false,unit="degree_C",gridDegrees=.05,sourceKind="L4Analysis",
                cells=(from day in Enumerable.Range(0,7) from lat in new[]{36.975,37.025,37.075} from lon in new[]{130.025,130.075,130.125}
                    select new 해수면수온PreviewCell {time=first.AddDays(day).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),latitude=lat,longitude=lon,
                        hasValue=true,celsius=27+day/10.0,hasError=true,errorCelsius=.2,mask=1}).ToArray()
            };
        }
        [Test] public void 정확한날짜와북쪽부터격자를선택한다()
        {
            var s=Sample(); s.Validate(); Assert.That(s.Frame(0).Length,Is.EqualTo(9));
            Assert.That(s.Frame(6)[0].time,Is.EqualTo("2026-09-07T12:00:00Z"));
            Assert.That(s.Frame(0)[0].latitude,Is.EqualTo(37.075));
            Assert.Throws<ArgumentOutOfRangeException>(()=>s.Frame(7));
        }
        [Test] public void 결측을영도관측으로바꾸지않는다()
        {
            var s=Sample(); s.cells[0].hasValue=false; s.cells[0].celsius=0; s.Validate();
            Assert.That(s.Frame(0).Count(c=>c.hasValue),Is.EqualTo(8));
        }
        [TestCase("unit")][TestCase("source")][TestCase("duplicate")][TestCase("land")]
        [TestCase("nan")][TestCase("position")][TestCase("date")][TestCase("publication")]
        public void 잘못된사본을거절한다(string failure)
        {
            var s=Sample();
            switch(failure) {
                case "unit":s.unit="K";break; case "source":s.sourceHash="bad";break;
                case "duplicate":s.cells[1]=s.cells[0];break; case "land":s.cells[0].mask=2;break;
                case "nan":s.cells[0].celsius=double.NaN;break; case "position":s.cells[0].longitude=129;break;
                case "date":s.cells[0].time="2026-09-08T12:00:00Z";break; case "publication":s.distributionApproved=true;break;
            }
            Assert.Throws<InvalidOperationException>(()=>s.Validate());
        }
    }
}
