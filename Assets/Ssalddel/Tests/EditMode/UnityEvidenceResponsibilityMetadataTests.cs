using System.Linq;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Editor;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E3,
        "Unity 제품 모듈의 E 책임 전수 분류와 단계 정의를 검증한다.",
        Boundary = "메타데이터 시험은 실제 플레이 증거를 대신하지 않는다.")]
    public sealed class UnityEvidenceResponsibilityMetadataTests
    {
        [Test]
        public void 제품Unity후보는_대표E또는제외사유를가진다()
        {
            var snapshot = UnityEvidenceResponsibilityMapBuilder.BuildSnapshot(
                requireCoverage: true);

            Assert.That(snapshot.Coverage.CandidateTypeCount, Is.GreaterThan(0));
            Assert.That(snapshot.Coverage.UncoveredTypeCount, Is.EqualTo(0));
            Assert.That(snapshot.Diagnostics.Any(item =>
                item.Severity == SsalddelEvidenceCoverageDiagnosticSeverity.Error
                    .ToString()), Is.False);
            Assert.That(snapshot.Stages.Select(item => item.EvidenceStage),
                Is.EqualTo(new[] { "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9" }));
        }
    }
}
