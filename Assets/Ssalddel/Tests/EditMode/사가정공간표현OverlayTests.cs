using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정공간표현OverlayTests
    {
        private static TextAsset MapAsset => Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map => JsonUtility.FromJson<공간참고자료>(MapAsset.text);

        [Test]
        public void 비공개검토Overlay는_r3를변경하지않고_건물표면높이근거를합성한다()
        {
            var map = Map;
            var before = JsonUtility.ToJson(map);
            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                Bytes(Valid()), map, MapAsset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);

            Assert.That(composition.OverlayApplied, Is.True);
            Assert.That(composition.SourceStatus, Is.EqualTo("LocalPrivateReview"));
            Assert.That(composition.DiagnosticCode, Is.EqualTo("Ready"));
            Assert.That(composition.SourceReceipts, Has.Count.EqualTo(2));
            Assert.That(composition.SourceReceipts.All(x => x.RawHash.Length == 64), Is.True);
            Assert.That(composition.Buildings.Single().HeightKind,
                Is.EqualTo("ObservedSourceHeight"));
            Assert.That(composition.Buildings.Single().HeightPolicyRevision,
                Is.EqualTo("sagajeong-height-policy.r1"));
            Assert.That(composition.Surfaces.Single().EvidenceKind,
                Is.EqualTo("PublicOpenData"));
            Assert.That(composition.LegacyBuildingBindingCount, Is.EqualTo(1));

            var baseline = map.buildings.Single(x => x.id == "osm:way:470492217");
            Assert.That(composition.ResolveHeight(baseline), Is.EqualTo(12.5f));
            Assert.That(composition.ResolveBuildingKind(baseline), Is.EqualTo("detached-house"));
            Assert.That(composition.TryResolveLegacyBuilding(baseline.id, out var overlay), Is.True);
            Assert.That(overlay.Parts.Single().Outer.Count, Is.EqualTo(baseline.points.Length));
            Assert.That(JsonUtility.ToJson(map), Is.EqualTo(before));
        }

        [Test]
        public void Editor비공개Loader는_환경변수를EditorPrefs보다먼저읽고_경로를결과에남기지않는다()
        {
            var path = Path.GetTempFileName();
            var oldEnvironment = Environment.GetEnvironmentVariable(
                사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey);
            var hadPreference = EditorPrefs.HasKey(
                사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey);
            var oldPreference = EditorPrefs.GetString(
                사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey, string.Empty);
            try
            {
                File.WriteAllBytes(path, Bytes(Valid()));
                EditorPrefs.SetString(사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey,
                    path + ".missing");
                Environment.SetEnvironmentVariable(
                    사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey, path);

                var composition = 사가정공간표현OverlayPipeline
                    .LoadLocalPrivateReviewForEditor(Map, MapAsset.bytes);

                Assert.That(composition.OverlayApplied, Is.True);
                Assert.That(composition.GetType().GetProperties()
                    .Any(x => x.Name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
                Assert.That(typeof(사가정공간표현Composition).IsSerializable, Is.False);
                Assert.That(typeof(사가정공간표현OverlayData).IsSerializable, Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey, oldEnvironment);
                if (hadPreference)
                    EditorPrefs.SetString(사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey,
                        oldPreference);
                else
                    EditorPrefs.DeleteKey(사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey);
                File.Delete(path);
            }
        }

        [Test]
        public void Editor비공개Loader는_명시경로가없으면자동탐색하지않고_r3로복귀한다()
        {
            var key = 사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey;
            var oldEnvironment = Environment.GetEnvironmentVariable(key);
            var hadPreference = EditorPrefs.HasKey(key);
            var oldPreference = EditorPrefs.GetString(key, string.Empty);
            try
            {
                Environment.SetEnvironmentVariable(key, null);
                EditorPrefs.DeleteKey(key);
                var composition = 사가정공간표현OverlayPipeline
                    .LoadLocalPrivateReviewForEditor(Map, MapAsset.bytes);
                Assert.That(composition.OverlayApplied, Is.False);
                Assert.That(composition.DiagnosticCode,
                    Is.EqualTo("SpatialOverlayLocalPathMissing"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, oldEnvironment);
                if (hadPreference) EditorPrefs.SetString(key, oldPreference);
                else EditorPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void 승인ResourcesLoader는_고정이름을읽지만_승인pin이없으면적용하지않는다()
        {
            var publicData = Valid("PublicOpenData",
                "sagajeong-spatial-presentation.public.r1", true);
            var requested = new System.Collections.Generic.List<string>();
            var resources = ApprovedResources(publicData);
            var composition = 사가정공간표현OverlayPipeline.LoadApprovedResource(
                Map, MapAsset.bytes, name =>
                {
                    requested.Add(name);
                    return resources(name);
                });

            Assert.That(requested, Is.EqualTo(new[]
            {
                "SagajeongSpatialPresentationOverlay",
                "SagajeongSpatialPresentationOverlayApproval"
            }));
            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.DiagnosticCode,
                Is.EqualTo("SpatialOverlayResourceRevisionNotApproved"));

            var rejected = 사가정공간표현OverlayPipeline.LoadApprovedResource(
                Map, MapAsset.bytes, ApprovedResources(Valid()));
            Assert.That(rejected.OverlayApplied, Is.False);
            Assert.That(rejected.Revision, Is.EqualTo("sagajeong-reference.r3"));
            Assert.That(rejected.DiagnosticCode,
                Is.EqualTo("SpatialOverlayResourceBoundaryMismatch"));
        }

        [TestCase("provider")]
        [TestCase("dataset")]
        [TestCase("raw-hash")]
        [TestCase("license")]
        [TestCase("source-url")]
        [TestCase("license-url")]
        [TestCase("raw-length")]
        [TestCase("crs")]
        [TestCase("review-status")]
        [TestCase("osm-license")]
        [TestCase("osm-raw-hash")]
        [TestCase("osm-review-status")]
        [TestCase("unknown-public-receipt")]
        public void 승인Resources는_공식원천영수증과공개권리상태를모두요구한다(string failure)
        {
            var data = Valid("PublicOpenData",
                "sagajeong-spatial-presentation.public.r1", true);
            switch (failure)
            {
                case "provider": data.sourceReceipts[0].provider = "unknown"; break;
                case "dataset": data.sourceReceipts[0].datasetId = "unknown"; break;
                case "raw-hash": data.sourceReceipts[0].rawHash = new string('0', 64); break;
                case "license": data.sourceReceipts[0].licenseCode = "RightsConflictUnresolved"; break;
                case "source-url": data.sourceReceipts[0].sourceUrl = "https://example.invalid/source"; break;
                case "license-url": data.sourceReceipts[0].licenseUrl = "https://example.invalid/license"; break;
                case "raw-length": data.sourceReceipts[0].rawLength += 1; break;
                case "crs": data.sourceReceipts[0].originalCrs = "EPSG:4326"; break;
                case "review-status": data.sourceReceipts[0].reviewStatus = "LocalPrivateReview"; break;
                case "osm-license": data.sourceReceipts[1].licenseCode = "CC-BY-4.0"; break;
                case "osm-raw-hash": data.sourceReceipts[1].rawHash = new string('0', 64); break;
                case "osm-review-status": data.sourceReceipts[1].reviewStatus = "LocalPrivateReview"; break;
                case "unknown-public-receipt": data.sourceReceipts[1].provider = "unknown"; break;
            }

            var composition = 사가정공간표현OverlayPipeline.LoadApprovedResource(
                Map, MapAsset.bytes, ApprovedResources(data));

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.Revision, Is.EqualTo("sagajeong-reference.r3"));
        }

        [Test]
        public void 공개표면의sourceId는_허용된공개영수증에결속되어야한다()
        {
            var data = Valid("PublicOpenData",
                "sagajeong-spatial-presentation.public.r1", true);
            data.surfaces[0].sourceId = "unbound:source";

            var composition = 사가정공간표현OverlayPipeline.LoadApprovedResource(
                Map, MapAsset.bytes, ApprovedResources(data));

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.DiagnosticCode,
                Is.EqualTo("SpatialOverlaySurfaceEvidenceInvalid"));
        }

        [Test]
        public void 공개경계는_승인Loader를우회해직접합성할수없다()
        {
            var data = Valid("PublicOpenData",
                "sagajeong-spatial-presentation.public.r1", true);

            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                Bytes(data), Map, MapAsset.bytes,
                사가정공간표현OverlayBoundary.ApprovedResource);

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.DiagnosticCode,
                Is.EqualTo("SpatialOverlayApprovedResourceMustUseLoader"));
        }

        [Test]
        public void 중복된공식건물sourceFeatureId는_원천결속모호성으로거부한다()
        {
            var data = Valid();
            var duplicate = 사가정공간표현OverlayParser.Parse(Bytes(data)).buildings[0];
            duplicate.id = "official-building:sample-2";
            data.buildings = new[] { data.buildings[0], duplicate };

            var composition = Compose(data);

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.DiagnosticCode,
                Is.EqualTo("SpatialOverlayBuildingEvidenceInvalid"));
        }

        [TestCase("missing", "SpatialOverlayResourceHashReceiptMissing")]
        [TestCase("malformed", "SpatialOverlayResourceHashReceiptInvalid")]
        [TestCase("mismatch", "SpatialOverlayResourceHashMismatch")]
        public void 승인Resources는_실제AssetSha256영수증이일치해야한다(
            string failure, string expectedDiagnostic)
        {
            var data = Valid("PublicOpenData",
                "sagajeong-spatial-presentation.public.r1", true);
            var overlay = Bytes(data);
            var composition = 사가정공간표현OverlayPipeline.LoadApprovedResource(
                Map, MapAsset.bytes, name =>
                {
                    if (name == 사가정공간표현OverlayPipeline.ApprovedResourceName)
                        return overlay;
                    return failure switch
                    {
                        "missing" => null,
                        "malformed" => Encoding.UTF8.GetBytes("not-a-sha256"),
                        _ => Encoding.UTF8.GetBytes(new string('0', 64))
                    };
                });

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.DiagnosticCode, Is.EqualTo(expectedDiagnostic));
        }

        [Test]
        public void 비공개검토는_동일원천hash의권리충돌미해결상태를Resources승인으로오인하지않는다()
        {
            var data = Valid();
            Assert.That(data.sourceReceipts[0].licenseCode,
                Is.EqualTo("RightsConflictUnresolved"));
            var composition = Compose(data);
            Assert.That(composition.OverlayApplied, Is.True);
            Assert.That(composition.SourceStatus, Is.EqualTo("LocalPrivateReview"));
        }

        [Test]
        public void Overture공개건물보조자료는_엄격한원천영수증으로_로컬검토에만합성한다()
        {
            var data = ValidOverture();

            var composition = Compose(data);

            Assert.That(composition.OverlayApplied, Is.True);
            Assert.That(composition.Revision,
                Is.EqualTo(사가정공간표현OverlayComposer.OverturePrivateReviewRevision));
            Assert.That(composition.SourceStatus, Is.EqualTo("LocalPrivateReview"));
            Assert.That(composition.SourceReceipts, Has.Count.EqualTo(1));
            Assert.That(composition.Surfaces, Is.Empty);
            Assert.That(composition.Buildings, Has.Count.EqualTo(1));
            Assert.That(composition.LegacyBuildingBindingCount, Is.Zero);
        }

        [Test]
        public void Overture공개건물보조자료의_동결hash가다르면_r3로복귀한다()
        {
            var data = ValidOverture();
            data.sourceReceipts[0].rawHash = new string('0', 64);

            var composition = Compose(data);

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.SourceStatus, Is.EqualTo("SagajeongReferenceR3Fallback"));
            Assert.That(composition.DiagnosticCode,
                Is.EqualTo("SpatialOverlayOvertureSourceReceiptMismatch"));
        }

        [TestCase("schema")]
        [TestCase("revision")]
        [TestCase("status")]
        [TestCase("distribution")]
        [TestCase("base-revision")]
        [TestCase("base-hash")]
        [TestCase("coordinate")]
        [TestCase("source-status")]
        [TestCase("source-hash")]
        [TestCase("content-hash")]
        public void 계약이나출처가다르면_r3로복귀한다(string failure)
        {
            var data = Valid();
            switch (failure)
            {
                case "schema": data.schemaVersion = "unknown"; break;
                case "revision": data.revision = "sagajeong-spatial-presentation.private-review.r3"; break;
                case "status": data.status = "PublicOpenData"; break;
                case "distribution": data.distributionApproved = true; break;
                case "base-revision": data.baseMapRevision = "sagajeong-reference.r4"; break;
                case "base-hash": data.baseMapHash = new string('0', 64); break;
                case "coordinate": data.coordinateMethod = "EPSG:5186"; break;
                case "source-status": data.sourceReceipts[0].reviewStatus = "Approved"; break;
                case "source-hash": data.sourceReceipts[0].rawHash = "not-a-hash"; break;
                case "content-hash": data.contentHash = "not-a-hash"; break;
            }

            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                Bytes(data), Map, MapAsset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.Revision, Is.EqualTo("sagajeong-reference.r3"));
            Assert.That(composition.SourceStatus, Is.EqualTo("SagajeongReferenceR3Fallback"));
            Assert.That(composition.Buildings, Is.Empty);
            Assert.That(composition.Surfaces, Is.Empty);
        }

        [TestCase("alias")]
        [TestCase("unknown-alias")]
        [TestCase("height")]
        [TestCase("height-evidence")]
        [TestCase("surface-evidence")]
        [TestCase("surface-authority")]
        [TestCase("open-ring")]
        [TestCase("out-of-bounds")]
        [TestCase("match-iou")]
        [TestCase("match-coverage")]
        [TestCase("match-distance")]
        [TestCase("match-geometry")]
        public void 건물표면높이근거가잘못되면_부분적용하지않고_r3로복귀한다(string failure)
        {
            var data = Valid();
            switch (failure)
            {
                case "alias": data.buildings[0].legacyOsmIds[0] = "way/470492217"; break;
                case "unknown-alias": data.buildings[0].legacyOsmIds[0] = "osm:way:999999999999"; break;
                case "height": data.buildings[0].heightMeters = 0; break;
                case "height-evidence": data.buildings[0].heightKind = ""; break;
                case "surface-evidence": data.surfaces[0].evidenceKind = ""; break;
                case "surface-authority": data.surfaces[0].presentationOnly = false; break;
                case "open-ring":
                    data.buildings[0].parts[0].outer.points[
                        data.buildings[0].parts[0].outer.points.Length - 1].x += 1;
                    break;
                case "out-of-bounds": data.surfaces[0].parts[0].outer.points[0].x = 5000; break;
                case "match-iou": data.buildings[0].legacyMatch.intersectionOverUnion = .1; break;
                case "match-coverage": data.buildings[0].legacyMatch.smallerFootprintCoverage = .1; break;
                case "match-distance": data.buildings[0].legacyMatch.centroidDistanceMeters = 100; break;
                case "match-geometry":
                    foreach (var point in data.buildings[0].parts[0].outer.points) point.x += 30;
                    break;
            }

            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                Bytes(data), Map, MapAsset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.LegacyBuildingBindingCount, Is.Zero);
        }

        [TestCase("AmbiguousGlobalAliasExcluded")]
        [TestCase("AmbiguousMultipleFootprintsExcluded")]
        [TestCase("WeakFootprintCandidateExcluded")]
        public void 모호한LegacyMatch는_별칭을제외한근거로보존하지만_r3조회에는넣지않는다(
            string method)
        {
            var data = Valid();
            data.buildings[0].legacyOsmIds = Array.Empty<string>();
            data.buildings[0].legacyMatch.method = method;
            data.buildings[0].legacyMatch.ambiguous = true;
            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                Bytes(data), Map, MapAsset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);

            Assert.That(composition.OverlayApplied, Is.True);
            Assert.That(composition.Buildings.Single().LegacyMatchAmbiguous, Is.True);
            Assert.That(composition.LegacyBuildingBindingCount, Is.Zero);
            Assert.That(composition.TryResolveLegacyBuilding("osm:way:470492217", out _), Is.False);
        }

        [Test]
        public void 같은r3별칭을_둘이상건물에결속하면_전체Overlay를거부한다()
        {
            var data = Valid();
            var duplicate = 사가정공간표현OverlayParser.Parse(Bytes(data)).buildings[0];
            duplicate.id = "official-building:sample-2";
            duplicate.sourceFeatureId = "sample-2";
            data.buildings = new[] { data.buildings[0], duplicate };

            var composition = Compose(data);

            Assert.That(composition.OverlayApplied, Is.False);
            Assert.That(composition.DiagnosticCode,
                Is.EqualTo("SpatialOverlayLegacyOsmAliasInvalid"));
        }

        [Test]
        public void 대응r3건물이없는LegacyMatchNone도_Overlay건물근거로보존한다()
        {
            var data = Valid();
            data.buildings[0].legacyOsmIds = Array.Empty<string>();
            data.buildings[0].legacyMatch = new 사가정공간표현LegacyMatchData
            {
                method = "None",
                ambiguous = false
            };

            var composition = Compose(data);

            Assert.That(composition.OverlayApplied, Is.True);
            Assert.That(composition.Buildings, Has.Count.EqualTo(1));
            Assert.That(composition.LegacyBuildingBindingCount, Is.Zero);
        }

        [Test]
        public void contentHash는선택이며_있으면64자리hex형식만검사한다()
        {
            var without = Valid();
            without.contentHash = null;
            Assert.That(Compose(without).OverlayApplied, Is.True);

            var formatOnly = Valid();
            formatOnly.contentHash = new string('B', 64);
            Assert.That(Compose(formatOnly).OverlayApplied, Is.True,
                "생성기의 canonical hash 계산은 원천 경계에서 맡고 Runtime은 형식만 검사한다.");
        }

        [Test]
        public void JSON파서는_lowerCamelCase필수필드가없으면거부한다()
        {
            Assert.Throws<ArgumentException>(() =>
                사가정공간표현OverlayParser.Parse("{\"SchemaVersion\":\"wrong-case\"}"));
            Assert.Throws<ArgumentException>(() =>
                사가정공간표현OverlayParser.Parse(Array.Empty<byte>()));
        }

        [Test]
        public void Overlay오류Fallback도_변경된r3를정상자료처럼받지않는다()
        {
            var changed = Map;
            changed.revision = "sagajeong-reference.r4";
            Assert.Throws<ArgumentException>(() =>
                사가정공간표현OverlayPipeline.ParseAndCompose(
                    new byte[] { 1 }, changed, MapAsset.bytes,
                    사가정공간표현OverlayBoundary.LocalPrivateReview));
        }

        private static 사가정공간표현Composition Compose(사가정공간표현OverlayData data)
            => 사가정공간표현OverlayPipeline.ParseAndCompose(Bytes(data), Map,
                MapAsset.bytes, 사가정공간표현OverlayBoundary.LocalPrivateReview);

        private static Func<string, byte[]> ApprovedResources(사가정공간표현OverlayData data)
        {
            var overlay = Bytes(data);
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(overlay)).Replace("-", "");
            var receipt = Encoding.UTF8.GetBytes(hash + "\n");
            return name => name switch
            {
                사가정공간표현OverlayPipeline.ApprovedResourceName => overlay,
                사가정공간표현OverlayPipeline.ApprovedResourceHashName => receipt,
                _ => null
            };
        }

        private static 사가정공간표현OverlayData Valid(
            string status = "LocalPrivateReview",
            string revision = "sagajeong-spatial-presentation.private-review.r2",
            bool distributionApproved = false)
        {
            var ring = new 사가정공간표현RingData
            {
                points = Map.buildings.Single(value => value.id == "osm:way:470492217")
                    .points.Select(value => new 사가정공간표현PointData
                    {
                        x = value.x,
                        z = value.z
                    }).ToArray()
            };
            return new 사가정공간표현OverlayData
            {
                schemaVersion = "ssalddel.spatial-presentation-overlay.v1",
                revision = revision,
                status = status,
                presentationOnly = true,
                distributionApproved = distributionApproved,
                baseMapRevision = "sagajeong-reference.r3",
                baseMapHash = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3",
                coordinateMethod = "WGS84-ECEF-ENU-at-zero-altitude",
                originLatitude = 37.5806971,
                originLongitude = 127.0884106,
                offsetX = 550,
                offsetZ = 8,
                halfExtent = 500,
                sourceReceipts = new[]
                {
                    new 사가정공간표현SourceReceiptData
                    {
                        provider = "국토교통부",
                        datasetId = "data-go-kr-15083092",
                        sourceUrl = "https://www.data.go.kr/data/15083092/fileData.do",
                        licenseCode = status == "PublicOpenData"
                            ? "KOGL-Type1"
                            : "RightsConflictUnresolved",
                        licenseUrl = status == "PublicOpenData"
                            ? "https://www.kogl.or.kr/info/licenseType1.do"
                            : "https://creativecommons.org/licenses/by-nc-nd/2.0/kr/",
                        datasetDate = "2026-08-09",
                        fetchedAtUtc = "2026-09-13T00:00:00Z",
                        rawHash = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755",
                        rawLength = 135675376,
                        originalCrs = "EPSG:5186",
                        reviewStatus = status
                    },
                    new 사가정공간표현SourceReceiptData
                    {
                        provider = "OpenStreetMap contributors",
                        datasetId = "sagajeong-r2-map",
                        sourceUrl = "https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854",
                        licenseCode = "ODbL-1.0",
                        licenseUrl = "https://www.openstreetmap.org/copyright",
                        datasetDate = "2026-09-08",
                        fetchedAtUtc = "2026-09-08T09:07:04.701Z",
                        rawHash = "3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3",
                        rawLength = 2993065,
                        originalCrs = "EPSG:4326",
                        reviewStatus = "PublicOpenData"
                    }
                },
                buildings = new[]
                {
                    new 사가정공간표현BuildingData
                    {
                        id = "official-building:sample-1",
                        sourceFeatureId = "sample-1",
                        buildingKind = "detached-house",
                        purposeName = "단독주택",
                        heightMeters = 12.5,
                        heightKind = "ObservedSourceHeight",
                        observedHeightMeters = 12.5,
                        observedAboveGroundFloors = 3,
                        heightPolicyRevision = "sagajeong-height-policy.r1",
                        reviewStatus = status,
                        legacyOsmIds = new[] { "osm:way:470492217" },
                        legacyMatch = new 사가정공간표현LegacyMatchData
                        {
                            method = "FootprintOverlap",
                            centroidDistanceMeters = 0,
                            intersectionOverUnion = 1,
                            smallerFootprintCoverage = 1,
                            ambiguous = false
                        },
                        parts = new[] { Part(ring) }
                    }
                },
                surfaces = new[]
                {
                    new 사가정공간표현SurfaceData
                    {
                        id = "surface:sample-1",
                        kind = "residential",
                        evidenceKind = "PublicOpenData",
                        presentationOnly = true,
                        sourceId = "openstreetmap:sagajeong-r2-map",
                        parts = new[] { Part(ring) }
                    }
                },
                contentHash = string.Empty
            };
        }

        private static 사가정공간표현OverlayData ValidOverture()
        {
            var data = Valid();
            data.revision = 사가정공간표현OverlayComposer.OverturePrivateReviewRevision;
            data.sourceReceipts = new[]
            {
                new 사가정공간표현SourceReceiptData
                {
                    provider = 사가정공간표현OverlayComposer.OvertureProvider,
                    datasetId = 사가정공간표현OverlayComposer.OvertureDatasetId,
                    sourceUrl = 사가정공간표현OverlayComposer.OvertureSourceUrl,
                    licenseCode = 사가정공간표현OverlayComposer.OvertureLicenseCode,
                    licenseUrl = 사가정공간표현OverlayComposer.OvertureLicenseUrl,
                    datasetDate = "2026-08-19",
                    fetchedAtUtc = "2026-09-21T01:18:39.804683Z",
                    rawHash = 사가정공간표현OverlayComposer.OvertureRawHash,
                    rawLength = 사가정공간표현OverlayComposer.OvertureRawLength,
                    originalCrs = "EPSG:4326",
                    reviewStatus = "LocalPrivateReview"
                }
            };
            data.surfaces = Array.Empty<사가정공간표현SurfaceData>();
            data.buildings[0].legacyOsmIds = Array.Empty<string>();
            data.buildings[0].legacyMatch = new 사가정공간표현LegacyMatchData
            {
                method = "None",
                ambiguous = false
            };
            return data;
        }

        private static 사가정공간표현PolygonPartData Part(사가정공간표현RingData outer)
            => new()
            {
                outer = outer,
                holes = Array.Empty<사가정공간표현RingData>()
            };

        private static 사가정공간표현RingData Ring(params (double x, double z)[] values)
            => new()
            {
                points = values.Select(value => new 사가정공간표현PointData
                {
                    x = value.x,
                    z = value.z
                }).ToArray()
            };

        private static byte[] Bytes(사가정공간표현OverlayData data)
        {
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(사가정공간표현OverlayData))
                .WriteObject(stream, data);
            return stream.ToArray();
        }
    }
}
