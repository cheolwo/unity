using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 역세권공간표현Tests
    {
        [Test]
        public void 면목Profile은_자가완결1km와_최종Fingerprint를지키고_사가정지도를가리키지않는다()
        {
            var value = StationDioramaProfileCatalog.Myeonmok;

            Assert.That(value.TransitStationStableId,
                Is.EqualTo("station:kr:kric:s1107:0721"));
            Assert.That(value.SchemaVersion,
                Is.EqualTo("ssalddel.station-spatial-snapshot.v1"));
            Assert.That(value.PrivateReviewRevision,
                Is.EqualTo("myeonmok-station-spatial-snapshot.private-review.r1"));
            Assert.That(value.LocalPrivateReviewPathKey,
                Is.EqualTo("SSALDDEL_MYEONMOK_STATION_SPATIAL_SNAPSHOT"));
            Assert.That(value.ExpectedContentHashSha256,
                Is.EqualTo("48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A"));
            Assert.That(value.ExpectedAssetHashSha256,
                Is.EqualTo("4B8D45AE3414218228FEB1436AE8E99AB3999C83A30607292AC594C0099530B1"));
            Assert.That(value.ExpectedOpenStreetMapSourceReceiptId,
                Is.EqualTo("openstreetmap:myeonmok-station-0721-r1"));
            Assert.That(value.OriginLatitude, Is.EqualTo(37.588671d));
            Assert.That(value.OriginLongitude, Is.EqualTo(127.087503d));
            Assert.That(value.OffsetX, Is.Zero);
            Assert.That(value.OffsetZ, Is.Zero);
            Assert.That(value.HalfExtentMeters, Is.EqualTo(500d));
            Assert.That(value.UsesLegacyBaseMap, Is.False);
            Assert.That(value.LegacyBaseMapResourceName, Is.Empty);
            Assert.That(value.LegacyBaseMapRevision, Is.Empty);
            Assert.That(value.LegacyBaseMapHashSha256, Is.Empty);
            Assert.That(StationDioramaProfileCatalog.Sagajeong.UsesLegacyBaseMap, Is.True);
        }

        [Test]
        public void 용마산Profile은_자가완결1km와_동결Fingerprint를지킨다()
        {
            var value = StationDioramaProfileCatalog.Yongmasan;

            Assert.That(value.TransitStationStableId,
                Is.EqualTo("station:kr:kric:s1107:0723"));
            Assert.That(value.DisplayName, Is.EqualTo("용마산역"));
            Assert.That(value.SchemaVersion,
                Is.EqualTo("ssalddel.station-spatial-snapshot.v1"));
            Assert.That(value.PrivateReviewRevision,
                Is.EqualTo("yongmasan-station-spatial-snapshot.private-review.r1"));
            Assert.That(value.LocalPrivateReviewPathKey,
                Is.EqualTo("SSALDDEL_YONGMASAN_STATION_SPATIAL_SNAPSHOT"));
            Assert.That(value.ExpectedContentHashSha256,
                Is.EqualTo("43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D"));
            Assert.That(value.ExpectedAssetHashSha256,
                Is.EqualTo("6AC2E8595B913B6AE80D6426476F334225491E89555CEB9A5C1A17BD568120B6"));
            Assert.That(value.ExpectedOpenStreetMapSourceReceiptId,
                Is.EqualTo("openstreetmap:yongmasan-station-0723-r1"));
            Assert.That(value.OriginLatitude, Is.EqualTo(37.573752d));
            Assert.That(value.OriginLongitude, Is.EqualTo(127.086802d));
            Assert.That(value.OffsetX, Is.Zero);
            Assert.That(value.OffsetZ, Is.Zero);
            Assert.That(value.HalfExtentMeters, Is.EqualTo(500d));
            Assert.That(value.UsesLegacyBaseMap, Is.False);
            Assert.That(StationDioramaProfileCatalog.TryResolve(
                StationDioramaProfileCatalog.YongmasanStationStableId,
                out var resolved, out var presentation), Is.True);
            Assert.That(resolved, Is.SameAs(value));
            Assert.That(presentation, Is.SameAs(
                StationDioramaProfileCatalog.StandardOneKilometer));
        }

        [Test]
        public void 유효한자가완결Snapshot은_근거와결손을_부분Coverage로만합성한다()
        {
            var source = ValidSnapshot(true);
            var bytes = Bytes(source);
            var descriptor = Descriptor(bytes, source.contentHash);

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes, descriptor);

            Assert.That(value.SnapshotApplied, Is.True);
            Assert.That(value.DataStatus, Is.EqualTo("LocalPrivateReview"));
            Assert.That(value.CoverageStatus, Is.EqualTo("PartialCoverage"));
            Assert.That(value.DiagnosticCode, Is.EqualTo("Ready"));
            Assert.That(value.SourceReceipts, Has.Count.EqualTo(4));
            Assert.That(value.Buildings, Has.Count.EqualTo(1));
            Assert.That(value.Buildings.Single().HeightKind,
                Is.EqualTo("SymbolicFallback4m"));
            Assert.That(value.Roads, Has.Count.EqualTo(1));
            Assert.That(value.Surfaces, Has.Count.EqualTo(1));
            Assert.That(value.AdministrativeAreas, Has.Count.EqualTo(1));
            Assert.That(value.CoverageCells, Has.Count.EqualTo(100));
            Assert.That(value.CoverageCells.Count(x => x.Classification == "MissingCoverage"),
                Is.EqualTo(1));
            Assert.That(value.MissingCoverageItems.Select(x => x.Code),
                Does.Contain("TraversalAuthorityUnavailable"));
            Assert.That(value.GetType().GetProperties().Any(x =>
                x.Name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(typeof(StationDioramaSpatialComposition).IsSerializable, Is.False);
            Assert.That(typeof(StationSpatialSnapshotData).IsSerializable, Is.False);
        }

        [Test]
        public void 완전미관측셀의_빈EvidenceMix는_MissingCoverage로보존한다()
        {
            var source = ValidSnapshot(true);
            var missing = source.coverage.cells.Single(value =>
                value.classification == "MissingCoverage");
            missing.evidenceMix = Array.Empty<string>();
            var bytes = Bytes(source);

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes,
                Descriptor(bytes, source.contentHash));

            Assert.That(value.SnapshotApplied, Is.True, value.DiagnosticCode);
            Assert.That(value.CoverageStatus, Is.EqualTo("PartialCoverage"));
            Assert.That(value.DiagnosticCode, Is.EqualTo("Ready"));
            var preserved = value.CoverageCells.Single(cell => cell.Id == missing.id);
            Assert.That(preserved.Classification, Is.EqualTo("MissingCoverage"));
            Assert.That(preserved.EvidenceMix, Is.Empty);
            Assert.That(preserved.UnknownCoverageRatio, Is.EqualTo(1d));
            Assert.That(value.CoverageCells.Count(cell =>
                cell.Classification == "MissingCoverage"), Is.EqualTo(1));
            Assert.That(value.MissingCoverageItems.SelectMany(item => item.AffectedCellIds),
                Does.Contain(missing.id));
        }

        [Test]
        public void 확인근거가필요한셀의_빈EvidenceMix는_거부한다()
        {
            var source = ValidSnapshot(true);
            source.coverage.cells.First(value =>
                value.classification == "ConfirmedBuilt").evidenceMix = Array.Empty<string>();
            var bytes = Bytes(source);

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes,
                Descriptor(bytes, source.contentHash));

            Assert.That(value.SnapshotApplied, Is.False);
            Assert.That(value.DiagnosticCode,
                Is.EqualTo("StationDioramaCoverageCellInvalid"));
            Assert.That(value.CoverageStatus, Is.EqualTo("MissingCoverage"));
            Assert.That(value.Buildings, Is.Empty);
            Assert.That(value.CoverageCells, Is.Empty);
        }

        [Test]
        public void 명시경로가없으면_사가정대체없이_NoData와_도형0건을반환한다()
        {
            var descriptor = StationDioramaProfileCatalog.Myeonmok;
            var key = descriptor.LocalPrivateReviewPathKey;
            var oldEnvironment = Environment.GetEnvironmentVariable(key);
            var hadPreference = EditorPrefs.HasKey(key);
            var oldPreference = EditorPrefs.GetString(key, string.Empty);
            try
            {
                Environment.SetEnvironmentVariable(key, null);
                EditorPrefs.DeleteKey(key);

                var value = StationDioramaSpatialPipeline.LoadLocalPrivateReviewForEditor(
                    descriptor);

                Assert.That(value.SnapshotApplied, Is.False);
                Assert.That(value.DataStatus, Is.EqualTo("NoData"));
                Assert.That(value.CoverageStatus, Is.EqualTo("MissingCoverage"));
                Assert.That(value.DiagnosticCode,
                    Is.EqualTo("StationDioramaLocalPathMissing"));
                Assert.That(value.Buildings, Is.Empty);
                Assert.That(value.Roads, Is.Empty);
                Assert.That(value.Surfaces, Is.Empty);
                Assert.That(value.AdministrativeAreas, Is.Empty);
                Assert.That(value.CoverageCells, Is.Empty);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, oldEnvironment);
                if (hadPreference) EditorPrefs.SetString(key, oldPreference);
                else EditorPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void 용마산명시경로가없으면_면목과사가정을_대체자료로사용하지않는다()
        {
            var descriptor = StationDioramaProfileCatalog.Yongmasan;
            var key = descriptor.LocalPrivateReviewPathKey;
            var myeonmokKey = StationDioramaProfileCatalog.Myeonmok.LocalPrivateReviewPathKey;
            var sagajeongKey = StationDioramaProfileCatalog.Sagajeong.LocalPrivateReviewPathKey;
            var oldEnvironment = Environment.GetEnvironmentVariable(key);
            var oldMyeonmokEnvironment = Environment.GetEnvironmentVariable(myeonmokKey);
            var oldSagajeongEnvironment = Environment.GetEnvironmentVariable(sagajeongKey);
            var hadPreference = EditorPrefs.HasKey(key);
            var oldPreference = EditorPrefs.GetString(key, string.Empty);
            try
            {
                Environment.SetEnvironmentVariable(key, null);
                EditorPrefs.DeleteKey(key);
                Environment.SetEnvironmentVariable(myeonmokKey,
                    "C:/station-diorama-test/myeonmok-must-not-be-used.json");
                Environment.SetEnvironmentVariable(sagajeongKey,
                    "C:/station-diorama-test/sagajeong-must-not-be-used.json");

                var value = StationDioramaSpatialPipeline.LoadLocalPrivateReviewForEditor(
                    descriptor);

                Assert.That(value.Descriptor, Is.SameAs(descriptor));
                Assert.That(value.Descriptor, Is.Not.SameAs(
                    StationDioramaProfileCatalog.Myeonmok));
                Assert.That(value.Descriptor, Is.Not.SameAs(
                    StationDioramaProfileCatalog.Sagajeong));
                Assert.That(value.SnapshotApplied, Is.False);
                Assert.That(value.DiagnosticCode,
                    Is.EqualTo("StationDioramaLocalPathMissing"));
                Assert.That(value.Buildings, Is.Empty);
                Assert.That(value.Roads, Is.Empty);
                Assert.That(value.Surfaces, Is.Empty);
                Assert.That(value.CoverageCells, Is.Empty);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, oldEnvironment);
                Environment.SetEnvironmentVariable(myeonmokKey, oldMyeonmokEnvironment);
                Environment.SetEnvironmentVariable(sagajeongKey, oldSagajeongEnvironment);
                if (hadPreference) EditorPrefs.SetString(key, oldPreference);
                else EditorPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void EditorLoader는_환경변수를EditorPrefs보다먼저읽고_경로를결과에보존하지않는다()
        {
            var source = ValidSnapshot(false);
            var bytes = Bytes(source);
            var descriptor = Descriptor(bytes, source.contentHash,
                "SSALDDEL_TEST_STATION_SNAPSHOT_" + Guid.NewGuid().ToString("N"));
            var path = Path.GetTempFileName();
            var key = descriptor.LocalPrivateReviewPathKey;
            try
            {
                File.WriteAllBytes(path, bytes);
                EditorPrefs.SetString(key, path + ".missing");
                Environment.SetEnvironmentVariable(key, path);

                var value = StationDioramaSpatialPipeline.LoadLocalPrivateReviewForEditor(
                    descriptor);

                Assert.That(value.SnapshotApplied, Is.True);
                Assert.That(value.GetType().GetProperties().Any(x =>
                    x.Name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
                EditorPrefs.DeleteKey(key);
                File.Delete(path);
            }
        }

        [TestCase("schema")]
        [TestCase("station")]
        [TestCase("gameplay")]
        [TestCase("traversal")]
        [TestCase("coordinate")]
        [TestCase("contentHash")]
        public void 계약변조는_부분도형없이_MissingCoverage로되돌린다(string mutation)
        {
            var source = ValidSnapshot(true);
            switch (mutation)
            {
                case "schema": source.schemaVersion = "ssalddel.station-spatial-snapshot.v0"; break;
                case "station": source.transitStationStableId =
                    StationDioramaProfileCatalog.SagajeongStationStableId; break;
                case "gameplay": source.gameplayReady = true; break;
                case "traversal": source.roads[0].traversalReady = true; break;
                case "coordinate": source.coordinateFrame.offsetX = 1d; break;
                case "contentHash": source.contentHash = new string('C', 64); break;
            }
            var bytes = Bytes(source);
            var descriptor = Descriptor(bytes, new string('A', 64));

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes, descriptor);

            Assert.That(value.SnapshotApplied, Is.False);
            Assert.That(value.CoverageStatus, Is.EqualTo("MissingCoverage"));
            Assert.That(value.Buildings, Is.Empty);
            Assert.That(value.Roads, Is.Empty);
            Assert.That(value.Surfaces, Is.Empty);
        }

        [TestCase("wrong-station")]
        [TestCase("duplicate")]
        public void OpenStreetMapReceipt는_Descriptor와일치하는_한건만허용한다(
            string mutation)
        {
            var source = ValidSnapshot(true);
            if (mutation == "wrong-station")
            {
                var receipt = source.sourceReceipts.Single(value =>
                    value.sourceId.StartsWith("openstreetmap:", StringComparison.Ordinal));
                receipt.sourceId = "openstreetmap:yongmasan-station-0723-r1";
                source.surfaces[0].sourceId = receipt.sourceId;
            }
            else
            {
                source.sourceReceipts = source.sourceReceipts.Concat(new[]
                {
                    Receipt("openstreetmap:yongmasan-station-0723-r1")
                }).ToArray();
            }
            var bytes = Bytes(source);

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes,
                Descriptor(bytes, source.contentHash));

            Assert.That(value.SnapshotApplied, Is.False);
            Assert.That(value.DiagnosticCode,
                Is.EqualTo("StationDioramaRequiredSourceReceiptMissing"));
            Assert.That(value.Buildings, Is.Empty);
            Assert.That(value.Roads, Is.Empty);
            Assert.That(value.Surfaces, Is.Empty);
        }

        [Test]
        public void 파일Hash가다르면_JSON을적용하지않는다()
        {
            var source = ValidSnapshot(true);
            var bytes = Bytes(source);
            var descriptor = new StationDioramaDatasetDescriptor(
                StationDioramaProfileCatalog.MyeonmokStationStableId, "면목역",
                source.schemaVersion, source.revision, "SSALDDEL_TEST_HASH_MISMATCH",
                source.coordinateMethod, 37.588671d, 127.087503d, 0d, 0d, 500d,
                source.contentHash, new string('F', 64),
                expectedOpenStreetMapSourceReceiptId:
                "openstreetmap:myeonmok-station-0721-r1");

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes, descriptor);

            Assert.That(value.SnapshotApplied, Is.False);
            Assert.That(value.DiagnosticCode,
                Is.EqualTo("StationDioramaSnapshotAssetHashMismatch"));
            Assert.That(value.Buildings, Is.Empty);
            Assert.That(value.Roads, Is.Empty);
        }

        [Test]
        public void 사가정Descriptor를자가완결Pipeline에넣어도_사가정자료를fallback하지않는다()
        {
            var bytes = Bytes(ValidSnapshot(true));

            var value = StationDioramaSpatialPipeline.ParseAndCompose(bytes,
                StationDioramaProfileCatalog.Sagajeong);

            Assert.That(value.SnapshotApplied, Is.False);
            Assert.That(value.DiagnosticCode,
                Is.EqualTo("StationDioramaSelfContainedSnapshotRequired"));
            Assert.That(value.Buildings, Is.Empty);
            Assert.That(value.Roads, Is.Empty);
            Assert.That(value.Surfaces, Is.Empty);
        }

        [Test]
        public void 중립fallback은_source도형0건과_4x4받침대만표현한다()
        {
            var source = ValidSnapshot(false);
            var bytes = Bytes(source);
            var wrong = new StationDioramaDatasetDescriptor(
                StationDioramaProfileCatalog.MyeonmokStationStableId, "면목역",
                source.schemaVersion, source.revision, "SSALDDEL_TEST_NEUTRAL",
                source.coordinateMethod, 37.588671d, 127.087503d, 0d, 0d, 500d,
                source.contentHash, new string('0', 64),
                expectedOpenStreetMapSourceReceiptId:
                "openstreetmap:myeonmok-station-0721-r1");
            var missing = StationDioramaSpatialPipeline.ParseAndCompose(bytes, wrong);
            var batches = new List<역세권공간표현MeshBuilder.MeshBatch>();

            var report = 역세권공간표현MeshBuilder.Build(missing, batches.Add);

            Assert.That(missing.SnapshotApplied, Is.False);
            Assert.That(report.ChunkCount, Is.EqualTo(16));
            Assert.That(report.RendererBatchCount, Is.EqualTo(16));
            Assert.That(report.BuildingCount, Is.Zero);
            Assert.That(report.RoadCount, Is.Zero);
            Assert.That(report.SurfaceCount, Is.Zero);
            Assert.That(batches.SelectMany(x => x.Vertices), Is.Not.Empty);
            Assert.That(batches.All(x => x.Triangles.All(i => i >= 0
                && i < x.Vertices.Count)), Is.True);
        }

        [Test]
        public void 실제근거형상은_표현예산안에서_결정적인Mesh와View로조립된다()
        {
            var source = ValidSnapshot(true);
            var bytes = Bytes(source);
            var composition = StationDioramaSpatialPipeline.ParseAndCompose(bytes,
                Descriptor(bytes, source.contentHash));
            var first = new List<역세권공간표현MeshBuilder.MeshBatch>();
            var second = new List<역세권공간표현MeshBuilder.MeshBatch>();

            var report = 역세권공간표현MeshBuilder.Build(composition, first.Add);
            역세권공간표현MeshBuilder.Build(composition, second.Add);

            Assert.That(report.BuildingCount, Is.EqualTo(1));
            Assert.That(report.RoadCount, Is.EqualTo(1));
            Assert.That(report.SurfaceCount, Is.EqualTo(1));
            Assert.That(report.MissingCoverageCellCount, Is.EqualTo(1));
            Assert.That(report.RendererBatchCount,
                Is.LessThanOrEqualTo(composition.PresentationProfile.MaximumRendererCount));
            Assert.That(first.Select(x => (x.Name, x.Vertices.Count, x.Triangles.Count)),
                Is.EqualTo(second.Select(x => (x.Name, x.Vertices.Count, x.Triangles.Count))));
            Assert.That(first.SelectMany(x => x.Vertices).All(x => float.IsFinite(x.x)
                && float.IsFinite(x.y) && float.IsFinite(x.z)), Is.True);

            var host = new GameObject("면목 역세권 표현 EditMode 검증");
            try
            {
                var view = host.AddComponent<역세권공간표현View>();
                view.BuildFromComposition(composition, false);

                Assert.That(view.SnapshotApplied, Is.True);
                Assert.That(view.BuildReport.RendererBatchCount,
                    Is.LessThanOrEqualTo(composition.PresentationProfile.MaximumRendererCount));
                Assert.That(view.ObservationCamera, Is.Null);
                Assert.That(host.GetComponentsInChildren<MeshRenderer>(true),
                    Has.Length.EqualTo(view.BuildReport.RendererBatchCount));
                Assert.That(host.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void 역세권View는_단일Player표시정책에_동적환경으로등록한다()
        {
            var source = ValidSnapshot(true);
            var bytes = Bytes(source);
            var composition = StationDioramaSpatialPipeline.ParseAndCompose(bytes,
                Descriptor(bytes, source.contentHash));
            var owner = new GameObject("역세권 동적 환경 등록 검증");
            try
            {
                var playerObject = new GameObject("Player");
                playerObject.transform.SetParent(owner.transform, false);
                var player = playerObject.AddComponent<플레이어경관Controller>();
                var visual = new GameObject("VisualRoot");
                visual.transform.SetParent(playerObject.transform, false);
                typeof(플레이어경관Controller).GetField("visualRoot",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(player, visual.transform);

                var canvasObject = new GameObject("ModeCanvas");
                canvasObject.transform.SetParent(owner.transform, false);
                var canvas = canvasObject.AddComponent<Canvas>();
                var policy = owner.AddComponent<단일PlayerViewController>();
                policy.Configure(player, canvas, false);

                var unrelated = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unrelated.name = "승인 밖 표현";
                unrelated.transform.SetParent(owner.transform, false);
                var host = new GameObject("역세권 표현");
                host.transform.SetParent(owner.transform, false);
                var view = host.AddComponent<역세권공간표현View>();
                view.BuildFromComposition(composition, false);

                policy.ApplyView();

                Assert.That(view.RuntimeEnvironmentDiagnostic,
                    Is.EqualTo("SinglePlayerRuntimeEnvironmentRegistered"));
                Assert.That(host.GetComponentsInChildren<MeshRenderer>(true)
                    .All(value => value.enabled), Is.True);
                Assert.That(unrelated.GetComponent<MeshRenderer>().enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void 용마산실제동결Snapshot은_Parser와Mesh예산을통과한다()
        {
            var descriptor = StationDioramaProfileCatalog.Yongmasan;
            var path = Environment.GetEnvironmentVariable(
                descriptor.LocalPrivateReviewPathKey);
            if (string.IsNullOrWhiteSpace(path))
                path = EditorPrefs.GetString(descriptor.LocalPrivateReviewPathKey,
                    string.Empty);
            if (string.IsNullOrWhiteSpace(path))
                Assert.Ignore("용마산 로컬 비공개 snapshot 경로를 명시해야 하는 독립 시험");
            Assert.That(File.Exists(path), Is.True,
                "명시한 용마산 로컬 비공개 snapshot 파일이 없습니다.");

            var composition = StationDioramaSpatialPipeline
                .LoadLocalPrivateReviewForEditor(descriptor);
            var batches = new List<역세권공간표현MeshBuilder.MeshBatch>();
            var report = 역세권공간표현MeshBuilder.Build(composition, batches.Add);

            Assert.That(composition.SnapshotApplied, Is.True,
                composition.DiagnosticCode);
            Assert.That(composition.Revision,
                Is.EqualTo("yongmasan-station-spatial-snapshot.private-review.r1"));
            Assert.That(composition.ContentHash,
                Is.EqualTo("43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D"));
            Assert.That(composition.DataStatus, Is.EqualTo("LocalPrivateReview"));
            Assert.That(composition.CoverageStatus, Is.EqualTo("PartialCoverage"));
            Assert.That(composition.DiagnosticCode, Is.EqualTo("Ready"));
            Assert.That(composition.SourceReceipts, Has.Count.EqualTo(5));
            Assert.That(composition.SourceReceipts.Count(value =>
                    value.SourceId.StartsWith("openstreetmap:", StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(composition.SourceReceipts.Single(value =>
                    value.SourceId.StartsWith("openstreetmap:", StringComparison.Ordinal)).SourceId,
                Is.EqualTo(descriptor.ExpectedOpenStreetMapSourceReceiptId));
            Assert.That(composition.Buildings, Has.Count.EqualTo(2859));
            Assert.That(composition.Roads, Has.Count.EqualTo(116));
            Assert.That(composition.Surfaces, Has.Count.EqualTo(41));
            Assert.That(composition.AdministrativeAreas, Has.Count.EqualTo(4));
            Assert.That(composition.CoverageCells, Has.Count.EqualTo(100));
            Assert.That(composition.CoverageCells.Count(value =>
                value.Classification == "MissingCoverage"), Is.EqualTo(10));
            var evidenceFreeCells = composition.CoverageCells.Where(value =>
                value.EvidenceMix.Count == 0).ToArray();
            Assert.That(evidenceFreeCells, Has.Length.EqualTo(5));
            Assert.That(evidenceFreeCells.All(value =>
                value.Classification == "MissingCoverage"
                && value.UnknownCoverageRatio == 1d), Is.True);
            Assert.That(composition.MissingCoverageItems, Has.Count.EqualTo(6));

            Assert.That(report.SnapshotApplied, Is.True);
            Assert.That(report.ChunkCount, Is.EqualTo(16));
            Assert.That(report.RendererBatchCount, Is.EqualTo(16));
            Assert.That(report.RendererBatchCount,
                Is.LessThanOrEqualTo(composition.PresentationProfile.MaximumRendererCount));
            Assert.That(report.BuildingCount, Is.EqualTo(2859));
            Assert.That(report.RoadCount, Is.EqualTo(116));
            Assert.That(report.SurfaceCount, Is.EqualTo(41));
            Assert.That(report.MissingCoverageCellCount, Is.EqualTo(10));
            Assert.That(batches.SelectMany(value => value.Vertices).All(value =>
                float.IsFinite(value.x) && float.IsFinite(value.y)
                                        && float.IsFinite(value.z)), Is.True);
            Assert.That(batches.All(value => value.Triangles.All(index =>
                index >= 0 && index < value.Vertices.Count)), Is.True);
        }

        [Test]
        public void 전체보기는_1kmBounds를유한한직교크기로맞춘다()
        {
            var size = 역세권공간표현View.WholeViewOrthographicSize(
                new Bounds(new Vector3(0f, 40f, 0f), new Vector3(1000f, 160f, 1000f)),
                Quaternion.Euler(34f, 40f, 0f), 16f / 9f);

            Assert.That(float.IsFinite(size), Is.True);
            Assert.That(size, Is.GreaterThan(450f));
            Assert.That(size, Is.LessThan(1000f));
        }

        private static StationDioramaDatasetDescriptor Descriptor(byte[] bytes,
            string contentHash, string key = "SSALDDEL_TEST_STATION_SPATIAL_SNAPSHOT")
            => new(StationDioramaProfileCatalog.MyeonmokStationStableId, "면목역",
                "ssalddel.station-spatial-snapshot.v1",
                "myeonmok-station-spatial-snapshot.private-review.r1", key,
                "WGS84-ECEF-ENU-at-zero-altitude", 37.588671d, 127.087503d,
                0d, 0d, 500d, contentHash, Hash(bytes),
                expectedOpenStreetMapSourceReceiptId:
                "openstreetmap:myeonmok-station-0721-r1");

        private static StationSpatialSnapshotData ValidSnapshot(bool includeGeometry)
        {
            const string contentHash =
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            var cells = new List<StationSpatialCoverageCellData>();
            for (var z = 0; z < 10; z++)
                for (var x = 0; x < 10; x++)
                {
                    var missing = x == 0 && z == 0;
                    cells.Add(new StationSpatialCoverageCellData
                    {
                        id = $"cell:{x}:{z}", xIndex = x, zIndex = z,
                        bounds = Bounds(-500d + x * 100d, -500d + z * 100d,
                            -400d + x * 100d, -400d + z * 100d),
                        buildingCoverageRatio = missing ? 0d : .2d,
                        openCoverageRatio = 0d,
                        surfaceCoverageRatio = 0d,
                        unknownCoverageRatio = missing ? 1d : .8d,
                        classification = missing ? "MissingCoverage" : "ConfirmedBuilt",
                        evidenceMix = new[] { missing ? "MissingSurfaceCoverage" : "BuildingFootprint" },
                        boundaryClipped = false,
                        incompleteSourceFeatureIds = Array.Empty<string>()
                    });
                }

            return new StationSpatialSnapshotData
            {
                schemaVersion = "ssalddel.station-spatial-snapshot.v1",
                revision = "myeonmok-station-spatial-snapshot.private-review.r1",
                contentHash = contentHash,
                status = "LocalPrivateReview",
                transitStationStableId = StationDioramaProfileCatalog.MyeonmokStationStableId,
                stationName = "면목역",
                service = new StationSpatialServiceData { lineCode = "s1107", stationCode = "0721" },
                presentationOnly = true,
                distributionApproved = false,
                gameplayReady = false,
                traversalReady = false,
                coordinateMethod = "WGS84-ECEF-ENU-at-zero-altitude",
                coordinateFrame = new StationSpatialCoordinateFrameData
                {
                    originLatitude = 37.588671d, originLongitude = 127.087503d,
                    offsetX = 0d, offsetZ = 0d, metersPerUnit = 1d,
                    halfExtentMeters = 500d
                },
                window = new StationSpatialWindowData
                {
                    widthMeters = 1000d, depthMeters = 1000d,
                    bounds = Bounds(-500d, -500d, 500d, 500d),
                    wgs84Bounds = new StationSpatialWgs84BoundsData
                    {
                        minLongitude = 127.08d, minLatitude = 37.58d,
                        maxLongitude = 127.10d, maxLatitude = 37.60d,
                        corners = new[]
                        {
                            Corner("south-west", 127.08d, 37.58d),
                            Corner("north-west", 127.08d, 37.60d),
                            Corner("north-east", 127.10d, 37.60d),
                            Corner("south-east", 127.10d, 37.58d)
                        }
                    }
                },
                sourceReceipts = RequiredReceipts(),
                buildings = includeGeometry ? new[]
                {
                    new StationSpatialBuildingData
                    {
                        id = "building:fixture:1", sourceFeatureId = "fixture-building-1",
                        legalDongCode = "1126010100", buildingKind = "detached-house",
                        purposeName = "단독주택", heightMeters = 4d,
                        heightKind = "SymbolicFallback4m", observedHeightMeters = null,
                        observedAboveGroundFloors = null,
                        heightPolicyRevision = "station-building-height-presentation.r1",
                        reviewStatus = "LocalPrivateReview", boundaryClipped = false,
                        parts = new[] { Polygon(-30d, -30d, -10d, -10d) }
                    }
                } : Array.Empty<StationSpatialBuildingData>(),
                roads = includeGeometry ? new[]
                {
                    new StationSpatialRoadData
                    {
                        id = "road:fixture:1", sourceRecordIndex = 1,
                        fromNodeId = "node:a", toNodeId = "node:b", roadName = "검증로",
                        laneCount = 2, updatedDate = "2026-09-13",
                        reviewStatus = "LocalPrivateReview", presentationOnly = true,
                        traversalReady = false, boundaryClipped = false,
                        parts = new[] { new StationSpatialLinePartData
                        {
                            points = new[] { Point(-40d, 0d), Point(40d, 0d) }
                        } }
                    }
                } : Array.Empty<StationSpatialRoadData>(),
                surfaces = includeGeometry ? new[]
                {
                    new StationSpatialSurfaceData
                    {
                        id = "surface:fixture:1", kind = "park",
                        evidenceKind = "PublicOpenData", presentationOnly = true,
                        sourceId = "openstreetmap:myeonmok-station-0721-r1",
                        boundaryClipped = false,
                        parts = new[] { Polygon(10d, 10d, 40d, 40d) }
                    }
                } : Array.Empty<StationSpatialSurfaceData>(),
                administrativeAreas = new[]
                {
                    new StationSpatialAdministrativeAreaData
                    {
                        administrativeAreaStableId = "region:kr:hjd:1126056500",
                        displayName = "면목본동", sourceVintage = "2026-09-13",
                        containsStation = true, coverageSquareMeters = 1000000d,
                        coverageRatio = 1d, boundaryClipped = true,
                        parts = new[] { Polygon(-500d, -500d, 500d, 500d) }
                    }
                },
                tiles = Tiles(cells),
                coverage = new StationSpatialCoverageData
                {
                    cellSizeMeters = 100d, columns = 10, rows = 10,
                    buildingCoverageThreshold = .01d,
                    qualifiedOpenCoverageThreshold = .9d,
                    cells = cells.ToArray(),
                    summary = new StationSpatialCoverageSummaryData
                    {
                        classificationCounts = new Dictionary<string, int>
                        {
                            ["ConfirmedBuilt"] = 99,
                            ["ConfirmedOpen"] = 0,
                            ["IncompleteSurfaceEvidence"] = 0,
                            ["MissingCoverage"] = 1
                        },
                        buildingCoverageSquareMeters = 198000d,
                        surfaceCoverageSquareMeters = 0d,
                        qualifiedOpenCoverageSquareMeters = 0d,
                        incompleteRelationCount = 0
                    }
                },
                missingCoverage = new[]
                {
                    Missing("BuildingRightsConflictUnresolved", "buildings"),
                    Missing("LegalDongBoundaryCoverageUnverified", "administrativeAreas"),
                    Missing("RoadWidthUnavailable", "roads"),
                    Missing("SurfaceCoverageIncomplete", "surfaces", "cell:0:0"),
                    Missing("TraversalAuthorityUnavailable", "roads")
                }
            };
        }

        private static StationSpatialSourceReceiptData[] RequiredReceipts()
            => new[]
            {
                Receipt("vworld:gis-building-integrated-information"),
                Receipt("data-go-kr:standard-nodelink"),
                Receipt("seoul-open-data:administrative-dong-boundary"),
                Receipt("openstreetmap:myeonmok-station-0721-r1")
            };

        private static StationSpatialSourceReceiptData Receipt(string id)
            => new()
            {
                sourceId = id, datasetId = "fixture:" + id,
                sourceUrl = "https://example.com/source",
                sourceRevision = "fixture.r1", rawHash = new string('B', 64),
                rawLength = 1, originalCrs = "EPSG:4326", licenseCode = "fixture",
                licenseUrl = "https://example.com/license", reviewStatus = "LocalPrivateReview",
                distributionApproved = false, limitations = new[] { "검증 fixture" }
            };

        private static StationSpatialTileData[] Tiles(
            IReadOnlyList<StationSpatialCoverageCellData> cells)
        {
            var result = new List<StationSpatialTileData>();
            foreach (var tileZ in new[] { -1, 0 })
                foreach (var tileX in new[] { -1, 0 })
                {
                    var xStart = tileX < 0 ? 0 : 5;
                    var zStart = tileZ < 0 ? 0 : 5;
                    result.Add(new StationSpatialTileData
                    {
                        tileStableId = $"tile:{tileX}:{tileZ}",
                        tileIndexX = tileX, tileIndexZ = tileZ,
                        bounds = Bounds(tileX * 500d, tileZ * 500d,
                            tileX * 500d + 500d, tileZ * 500d + 500d),
                        tileHash = new string('D', 64),
                        buildingCount = 0, roadCount = 0, surfaceCount = 0,
                        coverageCellIds = cells.Where(x => x.xIndex >= xStart
                                                           && x.xIndex < xStart + 5
                                                           && x.zIndex >= zStart
                                                           && x.zIndex < zStart + 5)
                            .Select(x => x.id).ToArray()
                    });
                }
            return result.ToArray();
        }

        private static StationSpatialMissingCoverageData Missing(string code, string layer,
            params string[] affected)
            => new()
            {
                code = code, layer = layer, detail = "fixture limitation",
                affectedCellIds = affected ?? Array.Empty<string>()
            };

        private static StationSpatialPolygonPartData Polygon(double minX, double minZ,
            double maxX, double maxZ)
            => new()
            {
                outer = new StationSpatialRingData
                {
                    points = new[]
                    {
                        Point(minX, minZ), Point(minX, maxZ), Point(maxX, maxZ),
                        Point(maxX, minZ), Point(minX, minZ)
                    }
                },
                holes = Array.Empty<StationSpatialRingData>()
            };

        private static StationSpatialPointData Point(double x, double z)
            => new() { x = x, z = z };

        private static StationSpatialBoundsData Bounds(double minX, double minZ,
            double maxX, double maxZ)
            => new() { minX = minX, minZ = minZ, maxX = maxX, maxZ = maxZ };

        private static StationSpatialWgs84CornerData Corner(string name, double longitude,
            double latitude)
            => new() { name = name, longitude = longitude, latitude = latitude };

        private static byte[] Bytes(StationSpatialSnapshotData value)
        {
            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(
                typeof(StationSpatialSnapshotData),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            serializer.WriteObject(stream, value);
            return stream.ToArray();
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }
    }
}
