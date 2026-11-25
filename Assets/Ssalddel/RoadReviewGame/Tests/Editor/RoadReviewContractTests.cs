using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Ssalddel.RoadReviewGame.Tests
{
    /// <summary>합성 자료만 사용합니다. HTTP·실제 승인·사용자 검토 상태 변경은 실행하지 않습니다.</summary>
    public sealed class RoadReviewContractTests
    {
        private const string SourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SnapshotSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string RequestId = "10d2ad2b-b587-4c13-a388-b41ec13ce736";

        private static JObject Candidate() => new JObject
        {
            ["approvable"] = true,
            ["contextCurrent"] = true,
            ["sourceId"] = "sha256:" + SourceSha,
            ["sourceSha256"] = SourceSha,
            ["candidateId"] = "fixture-candidate-only",
            ["snapshotSha256"] = SnapshotSha,
            ["version"] = 0,
            ["holdReasons"] = new JArray(),
            ["conflictingApprovedCandidateIds"] = new JArray(),
            ["sourceInSeconds"] = 10.01,
            ["sourceOutSeconds"] = 20.02,
            ["segments"] = new JArray(
                new JObject { ["segmentId"] = "fixture-segment-a", ["direction"] = "forward" },
                new JObject { ["segmentId"] = "fixture-segment-b", ["direction"] = "reverse" })
        };

        private static JObject Listing() => new JObject
        {
            ["sourceId"] = "sha256:" + SourceSha,
            ["sourceSha256"] = SourceSha,
            ["snapshotSha256"] = SnapshotSha
        };

        private static JObject Build(JObject candidate = null, JObject listing = null, string[] selected = null, string requestId = RequestId)
            => RoadReviewContract.BuildApproval(candidate ?? Candidate(), listing ?? Listing(), selected ?? new[] { "fixture-segment-a" }, requestId);

        [Test]
        public void 기존_로컬_검토서버_두_호스트만_허용한다()
        {
            Assert.That(RoadReviewContract.IsAllowedServer("http://localhost:8791"), Is.True);
            Assert.That(RoadReviewContract.IsAllowedServer("http://127.0.0.1:8791/"), Is.True);
        }

        [Test]
        public void 외부주소_다른포트_경로_자격정보_질의_조각을_거절한다()
        {
            var denied = new[] { "https://localhost:8791/", "http://example.com:8791/", "http://localhost:8792/", "http://localhost:8791/path", "http://user:pass@localhost:8791/", "http://localhost:8791/?x=1", "http://localhost:8791/#x", "file:///tmp/", "invalid" };
            foreach (var address in denied) Assert.That(RoadReviewContract.IsAllowedServer(address), Is.False, address);
        }

        [Test]
        public void 첫_판본_정수0과_요청식별자를_그대로_유지한다()
        {
            var result = Build();
            Assert.That(result.Value<int>("version"), Is.Zero);
            Assert.That(result.Value<string>("requestId"), Is.EqualTo(RequestId));
            Assert.That(result.Value<string>("op"), Is.EqualTo("approve"));
        }

        [Test]
        public void 등록불가_후보를_거절한다()
        {
            var candidate = Candidate(); candidate["approvable"] = false;
            Assert.Throws<InvalidOperationException>(() => Build(candidate));
            Assert.Throws<InvalidOperationException>(() => RoadReviewContract.BuildApproval(null, Listing(), new[] { "fixture-segment-a" }, RequestId));
        }

        [Test]
        public void 오래되었거나_현재성이_없는_후보를_거절한다()
        {
            var candidate = Candidate(); candidate["contextCurrent"] = false;
            Assert.Throws<InvalidOperationException>(() => Build(candidate));
            candidate.Remove("contextCurrent");
            Assert.Throws<InvalidOperationException>(() => Build(candidate));
        }

        [Test]
        public void 필수_원본_후보_판본_식별자_누락을_거절한다()
        {
            foreach (var key in new[] { "sourceId", "sourceSha256", "candidateId", "snapshotSha256", "version" })
            {
                var candidate = Candidate(); candidate.Remove(key);
                Assert.Throws<InvalidOperationException>(() => Build(candidate), key);
            }
        }

        [Test]
        public void 잘못된_SHA와_원본ID의_불일치를_거절한다()
        {
            foreach (var field in new[] { "sourceSha256", "snapshotSha256", "sourceId" })
            {
                var candidate = Candidate(); candidate[field] = "invalid";
                Assert.Throws<InvalidOperationException>(() => Build(candidate), field);
            }
        }

        [Test]
        public void 없거나_빈_요청GUID를_거절한다()
        {
            foreach (var requestId in new[] { null, "", "not-a-guid", Guid.Empty.ToString() })
                Assert.Throws<InvalidOperationException>(() => Build(requestId: requestId));
        }

        [Test]
        public void 판본은_정수나_비어있지않은_문자열이어야한다()
        {
            foreach (var version in new JToken[] { JValue.CreateNull(), new JValue(true), new JObject(), new JValue(1.5), new JValue(" ") })
            {
                var candidate = Candidate(); candidate["version"] = version;
                Assert.Throws<InvalidOperationException>(() => Build(candidate));
            }
        }

        [Test]
        public void 조회목록과_후보의_원본_판본이_달라지면_거절한다()
        {
            foreach (var key in new[] { "sourceId", "sourceSha256", "snapshotSha256" })
            {
                var listing = Listing(); listing[key] = "changed";
                Assert.Throws<InvalidOperationException>(() => Build(listing: listing), key);
            }
        }

        [Test]
        public void 보류_근거가_있으면_승인요청을_만들지않는다()
        {
            var candidate = Candidate(); candidate["holdReasons"] = new JArray("fixture-protected-window");
            Assert.Throws<InvalidOperationException>(() => Build(candidate));
        }

        [Test]
        public void 상호배타_승인과_충돌하면_승인요청을_만들지않는다()
        {
            var candidate = Candidate(); candidate["conflictingApprovedCandidateIds"] = new JArray("fixture-other-candidate");
            Assert.Throws<InvalidOperationException>(() => Build(candidate));
        }

        [Test]
        public void 후보에_포함되지않은_선분을_거절한다()
            => Assert.Throws<InvalidOperationException>(() => Build(selected: new[] { "fixture-unknown-segment" }));

        [Test]
        public void 선택이_없으면_승인요청을_만들지않는다()
        {
            Assert.Throws<InvalidOperationException>(() => Build(selected: Array.Empty<string>()));
            Assert.Throws<InvalidOperationException>(() => RoadReviewContract.BuildApproval(Candidate(), Listing(), null, RequestId));
        }

        [Test]
        public void 선택_중복을_제거하고_현재_후보순서를_보존한다()
        {
            var result = Build(selected: new[] { "fixture-segment-b", "fixture-segment-a", "fixture-segment-b" });
            Assert.That(result["segmentIds"].Values<string>().ToArray(), Is.EqualTo(new[] { "fixture-segment-a", "fixture-segment-b" }));
        }

        [Test]
        public void 후보정의에_중복선분이_있으면_모호하므로_거절한다()
        {
            var candidate = Candidate();
            candidate["segments"][1]["segmentId"] = "fixture-segment-a";
            Assert.Throws<InvalidOperationException>(() => Build(candidate));
        }

        [Test]
        public void 공통관측창을_선분별시간_경로확정_공개허가로_승격하지않는다()
        {
            var candidate = Candidate();
            candidate["routeTraversalConfirmed"] = true;
            candidate["publicationReady"] = true;
            candidate["perSegmentTimesAssigned"] = true;
            var result = Build(candidate, selected: new[] { "fixture-segment-a", "fixture-segment-b" });
            Assert.That(result.Properties().Select(x => x.Name).ToArray(), Is.EquivalentTo(new[] { "op", "sourceId", "sourceSha256", "candidateId", "segmentIds", "version", "snapshotSha256", "requestId" }));
            Assert.That(result["sourceInSeconds"], Is.Null);
            Assert.That(result["sourceOutSeconds"], Is.Null);
            Assert.That(result["routeTraversalConfirmed"], Is.Null);
            Assert.That(result["publicationReady"], Is.Null);
        }

        [Test]
        public void 여러_실제좌표_선분은_geometryLines를_우선_읽는다()
        {
            var segment = JObject.Parse("{\"geometryLines\":[[[127.0901,37.5884],[127.0903,37.5885]],[[127.0903,37.5885],[127.0908,37.5886]]],\"geometry\":[[0,0],[1,1]]}");
            var lines = RoadReviewContract.ReadLines(segment);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0][0].x, Is.EqualTo(127.0901f).Within(.00002f));
            Assert.That(lines[0][0].y, Is.EqualTo(37.5884f).Within(.00002f));
            Assert.That(lines.All(x => x.Count == 2), Is.True);
        }

        [Test]
        public void GeoJSON_LineString의_경도_위도_순서를_유지한다()
        {
            var segment = JObject.Parse("{\"geometry\":{\"type\":\"LineString\",\"coordinates\":[[127.09,37.58],[127.091,37.581]]}}");
            var line = RoadReviewContract.ReadLines(segment).Single();
            Assert.That(line[0].x, Is.EqualTo(127.09f).Within(.00002f));
            Assert.That(line[0].y, Is.EqualTo(37.58f).Within(.00002f));
            Assert.That(line[1].x, Is.GreaterThan(line[0].x));
            Assert.That(line[1].y, Is.GreaterThan(line[0].y));
        }

        [Test]
        public void 좌표가_없거나_한점뿐이면_가짜_도로를_만들지않는다()
        {
            Assert.That(RoadReviewContract.ReadLines(new JObject()), Is.Empty);
            Assert.That(RoadReviewContract.ReadLines(JObject.Parse("{\"geometry\":[[127.09,37.58]]}")), Is.Empty);
        }
    }
}
