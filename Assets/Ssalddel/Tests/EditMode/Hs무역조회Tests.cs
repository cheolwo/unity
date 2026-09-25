#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public class Hs무역조회Tests
    {
        static Hs조회사본 Fixture()=>new(){schemaVersion="hs-trade-index.v1",classification="H6",hsEdition="2022",reporterM49=410,year=2025,flow="import",exportStatus="NotCollected",partnerCoverage="WorldAndStoredTopPartners_NotAllCountries",transportLink="ContextOnly_NoHsModeBinding",sourceUrl="https://comtradeplus.un.org/",localReviewOnly=true,products=new[]{new Hs조회품목{hs6="030214",name="Salmon",status="NoReportedRows",partners=Array.Empty<Hs조회상대국>(),classificationSources=new[]{new Hs조회근거()}}}};
        [Test] public void ValidMissingIsNotZero(){Assert.DoesNotThrow(()=>Fixture().Validate());}
        [Test] public void WrongEdition(){var s=Fixture();s.hsEdition="2026";Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void LostLeadingZero(){var s=Fixture();s.products[0].hs6="30214";Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void NoInventedExport(){var s=Fixture();s.flow="export";Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void NoHsTransportAssertion(){var s=Fixture();s.transportLink="HsBound";Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void Duplicate(){var s=Fixture();s.products=new[]{s.products[0],s.products[0]};Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void ObservedNeedsRows(){var s=Fixture();s.products[0].status="Observed";Assert.Throws<ArgumentException>(()=>s.Validate());}
    }
}
#endif
