using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 수산물통계미리보기Tests
    {
        private static 수산물통계PreviewSnapshot Sample() => new()
        {
            schemaVersion = "marine-trade-preview.v1", country = "KR", sourceId = "kcs", month = "202606",
            localReviewOnly = true, distributionApproved = false, batchId = "test", collectedAtUtc = "2026-09-22T08:08:06Z",
            sourceUrl = "https://www.data.go.kr/data/15100475/openapi.do",
            items = new[] { Item("0302140000", "NO"), Item("0303540000", "NO"), Item("0303630000", "RU") }
        };
        private static 수산물통계PreviewItem Item(string hs, string partner) => new()
        { hsCode = hs, partner = partner, name = "검증용 품목", importKg = 10, importUsd = 20,
            weightRevision = new string('a', 64), amountRevision = new string('b', 64) };
        [Test] public void 로컬검토사본만허용한다() => Assert.DoesNotThrow(Sample().Validate);
        [Test] public void 공개승인으로위장된사본은거절한다() { var s = Sample(); s.distributionApproved = true; Assert.Throws<InvalidOperationException>(s.Validate); }
        [Test] public void 미확보수온을표시하지않는다() { var s = Sample(); s.temperatureAvailable = true; Assert.Throws<InvalidOperationException>(s.Validate); }
        [Test] public void 다른국가또는출처는거절한다() { var s = Sample(); s.country = "NO"; Assert.Throws<InvalidOperationException>(s.Validate); }
        [Test] public void 중복과잘못된값은거절한다() { var s = Sample(); s.items[0].importKg = double.NaN; Assert.Throws<InvalidOperationException>(s.Validate); s = Sample(); s.items[1] = s.items[0]; Assert.Throws<InvalidOperationException>(s.Validate); }
        [Test] public void 틀린원본판본은거절한다() { var s = Sample(); s.items[0].weightRevision = "unknown"; Assert.Throws<InvalidOperationException>(s.Validate); }
    }
}
