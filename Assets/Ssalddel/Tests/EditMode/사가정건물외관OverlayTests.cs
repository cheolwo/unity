using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정건물외관OverlayTests
    {
        private TextAsset asset;
        private 공간참고자료 map;
        private 사가정공간표현Composition composition;
        private GameObject parent, prefab;
        private 법정동경관VisualCatalog catalog;
        private 사가정외관OverlayRenderer renderer;
        private 사가정외관ModuleReview[] modules;

        [SetUp]
        public void SetUp()
        {
            asset = Resources.Load<TextAsset>("SagajeongReference");
            map = JsonUtility.FromJson<공간참고자료>(asset.text);
            composition = 사가정공간표현OverlayComposer.ComposeOrFallback(null, map, asset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);
            parent = new GameObject("appearance-fixture-parent");
            prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "appearance-fixture-static-module";
            var entry = new 법정동경관VisualCatalogEntry();
            entry.Configure(법정동경관VisualKeys.Fence, "fixture-only", prefab,
                new[] { 법정동LandCoverCodes.Residential }, new[] { 법정동WorldRoleCodes.Town },
                Vector2.one, new Vector2(0f, 30f), 0, 0, new[] { "All" }, 0f,
                법정동경관CollisionPolicyCodes.None, 12, 1, 1, 0, 0, 0, false, true);
            catalog = ScriptableObject.CreateInstance<법정동경관VisualCatalog>();
            catalog.Configure("appearance-fixture.r1", new[] { entry });
            modules = new[] { new 사가정외관ModuleReview { visualKey = ModuleVisualKey,
                moduleKind = "Facade", catalogRevision = catalog.CatalogRevision,
                reviewerKind = "CodexVisualReview", reviewReference = "fixture:module-review" } };
            renderer = new 사가정외관OverlayRenderer(parent.transform,
                new 사가정외관검토Gate(map, composition, null), 0);
        }

        private static string ModuleVisualKey => 법정동경관VisualKeys.Fence;

        [TearDown]
        public void TearDown()
        {
            renderer?.Dispose();
            if (catalog != null) Object.DestroyImmediate(catalog);
            if (prefab != null) Object.DestroyImmediate(prefab);
            if (parent != null) Object.DestroyImmediate(parent);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void 상세공간이있으면_구형Synty로전체건물을바꾸지않는다(bool requested, bool overlay, bool expected)
            => Assert.That(사가정공간표현ModePolicy.UseLegacySynty(requested, overlay), Is.EqualTo(expected));

        [Test]
        public void 검토부품만추가하고_원본매스와선택식별자는유지하며_개별복구한다()
        {
            var before = JsonUtility.ToJson(map);
            var profile = Profile();
            Assert.That(renderer.TryApply(profile, catalog, modules, out var status), Is.True, status);
            Assert.That(renderer.Count, Is.EqualTo(1));
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(1));
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<MeshFilter>(true).Single().sharedMesh,
                Is.SameAs(prefab.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(renderer.VisualRoot.gameObject.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(JsonUtility.ToJson(map), Is.EqualTo(before));
            Assert.That(renderer.Remove(profile.profileStableId), Is.True);
            Assert.That(renderer.Count, Is.Zero);
            Assert.That(renderer.VisualRoot.childCount, Is.Zero);
            Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null, "원본 부품 자산은 변경하지 않는다.");
            Assert.That(renderer.Remove(profile.profileStableId), Is.False);
        }

        [TestCase("candidate", "AppearancePrivateReviewRequired")]
        [TestCase("study-scope", "AppearanceScopeRejected")]
        [TestCase("map-version", "AppearanceSpatialVersionMismatch")]
        [TestCase("spatial-version", "AppearanceSpatialVersionMismatch")]
        [TestCase("building-id", "AppearanceExactBuildingRequired")]
        [TestCase("observation", "AppearanceObservationRequired")]
        [TestCase("source-hash", "AppearanceObservationRequired")]
        [TestCase("pts-clock", "AppearanceObservationRequired")]
        [TestCase("raw-path", "AppearanceObservationRequired")]
        [TestCase("distribution", "AppearanceAuthorityRejected")]
        [TestCase("whole-building", "AppearanceModuleKindRejected")]
        [TestCase("catalog-version", "AppearanceCatalogVersionMismatch")]
        [TestCase("scale", "AppearanceTransformRejected")]
        [TestCase("nan", "AppearanceTransformRejected")]
        [TestCase("far-away", "AppearancePlacementOutsideBuilding")]
        public void 잘못된후속요청은_적용중인외관과바탕을보존한다(string mutation, string expected)
        {
            var profile = Profile();
            Assert.That(renderer.TryApply(profile, catalog, modules, out _), Is.True);
            var oldRoot = renderer.VisualRoot.GetChild(0);
            switch (mutation)
            {
                case "candidate": profile.reviewState = "Candidate"; break;
                case "study-scope": profile.scopeKind = 사가정건물외관Profile.PrivateStudyScope; break;
                case "map-version": profile.baseMapHashSha256 = new string('a', 64); break;
                case "spatial-version": profile.spatialContentHashSha256 = new string('b', 64); break;
                case "building-id": profile.targetBuildingId = "missing:building"; break;
                case "observation": profile.evidenceObservationId = ""; break;
                case "source-hash": profile.evidenceSourceSha256 = "not-a-sha"; break;
                case "pts-clock": profile.evidenceTimeBaseDenominator = 0; break;
                case "raw-path": profile.evidenceObservationId = "C:/private/source.mp4"; break;
                case "distribution": profile.distributionApproved = true; break;
                case "whole-building": profile.visualKey = 법정동경관VisualKeys.CityShop; break;
                case "catalog-version": profile.visualCatalogRevision = "stale.r0"; break;
                case "scale": profile.localScale = Vector3.zero; break;
                case "nan": profile.localPosition = new Vector3(float.NaN, 0, 0); break;
                case "far-away": profile.localPosition += Vector3.right * 10000f; break;
            }
            Assert.That(renderer.TryApply(profile, catalog, modules, out var status), Is.False);
            Assert.That(status, Is.EqualTo(expected));
            Assert.That(renderer.Count, Is.EqualTo(1));
            Assert.That(renderer.VisualRoot.GetChild(0), Is.SameAs(oldRoot));
            Assert.That(oldRoot.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void 부품허용검토가없거나중복이면_자동으로허용하지않는다()
        {
            Assert.That(renderer.TryApply(Profile(), catalog, Array.Empty<사가정외관ModuleReview>(),
                out var status), Is.False);
            Assert.That(status, Is.EqualTo("AppearanceModuleReviewRequired"));
            Assert.That(renderer.TryApply(Profile(), catalog, new[] { modules[0], modules[0] }, out status), Is.False);
            Assert.That(status, Is.EqualTo("AppearanceModuleReviewRequired"));
            Assert.That(renderer.Count, Is.Zero);
        }

        [Test]
        public void 새부품이너무크면_기존부품을지우지않고_실패한임시객체를제거한다()
        {
            Assert.That(renderer.TryApply(Profile(), catalog, modules, out _), Is.True);
            var oldRoot = renderer.VisualRoot.GetChild(0);
            prefab.transform.localScale = Vector3.one * 10000f;
            Assert.That(renderer.TryApply(Profile(), catalog, modules, out var status), Is.False);
            Assert.That(status, Is.EqualTo("AppearanceStaticGeometryRejected"));
            Assert.That(renderer.VisualRoot.childCount, Is.EqualTo(1));
            Assert.That(renderer.VisualRoot.GetChild(0), Is.SameAs(oldRoot));
        }

        [Test]
        public void 반복갱신과전체복구는_객체를중복누적하지않는다()
        {
            Assert.That(renderer.TryApply(Profile(), catalog, modules, out _), Is.True);
            Assert.That(renderer.TryApply(Profile(), catalog, modules, out _), Is.True);
            Assert.That(renderer.Count, Is.EqualTo(1));
            Assert.That(renderer.VisualRoot.childCount, Is.EqualTo(1));
            renderer.Clear();
            Assert.That(renderer.Count, Is.Zero);
            Assert.That(renderer.VisualRoot.childCount, Is.Zero);
            renderer.Dispose();
            Assert.That(renderer.TryApply(Profile(), catalog, modules, out var status), Is.False);
            Assert.That(status, Is.EqualTo("AppearanceRendererReleased"));
        }

        [Test]
        public void 기존자산대장을참조한차양도_부품검토전에는거절하고_판본변경때보류한다()
        {
            var source = ScriptableObject.CreateInstance<Synty전체자산ModuleCatalog>();
            var wrapper = ScriptableObject.CreateInstance<사가정외관ModuleCatalog>();
            try
            {
                var entry = new Synty자산ModuleEntry();
                entry.Configure("fixture:awning:1", Synty전체자산SourcePackCodes.City,
                    "synty-family:city:buildings:shop-cover", new[] { Synty자산ModuleCodes.ExteriorStructure },
                    Synty자산LifecycleCodes.NeedsReview, "", prefab, new string('e', 64));
                source.Configure("fixture:source.r1", new string('f', 64), new[] { entry });
                var module = new 사가정외관ModuleEntry
                {
                    visualKey = "appearance.awning.fixture.01", moduleKind = "Awning",
                    sourceAssetStableId = entry.AssetStableId, sourceFingerprintSha256 = entry.SourceFingerprintSha256
                };
                wrapper.Configure("fixture:appearance-modules.r1", source, new[] { module });
                var profile = Profile();
                profile.visualKey = module.visualKey;
                profile.moduleKind = "Awning";
                profile.visualCatalogRevision = wrapper.CatalogRevision;
                Assert.That(renderer.TryApply(profile, wrapper, out var status), Is.False);
                Assert.That(status, Is.EqualTo("AppearanceModuleReviewRequired"));
                module.reviewState = 사가정건물외관Profile.ReviewedState;
                module.reviewerKind = "CodexVisualReview";
                module.reviewReference = "fixture:module-private-review";
                Assert.That(renderer.TryApply(profile, wrapper, out status), Is.True, status);
                Assert.That(renderer.RemoveBuilding(profile.targetBuildingId), Is.EqualTo(1));
                source.Configure("fixture:source.r2", new string('f', 64), new[] { entry });
                Assert.That(renderer.TryApply(profile, wrapper, out status), Is.False);
                Assert.That(status, Is.EqualTo("AppearanceModuleAssetRejected"));
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void 연구전용Context와정본Profile은_서로승계되지않는다()
        {
            var profile = Profile();
            var studyHash = new string('1', 64);
            var bounds = new Bounds(profile.localPosition, new Vector3(8, 4, 8));
            var study = 사가정외관검토Gate.CreatePrivateStudy(studyHash,
                new Dictionary<string, Bounds> { [profile.targetBuildingId] = bounds });
            Assert.That(study.Validate(profile, catalog, modules, out _), Is.EqualTo("AppearanceScopeRejected"));
            profile.scopeKind = 사가정건물외관Profile.PrivateStudyScope;
            profile.baseMapHashSha256 = profile.spatialContentHashSha256 = studyHash;
            Assert.That(study.Validate(profile, catalog, modules, out _), Is.Empty);
            var canonical = new 사가정외관검토Gate(map, composition, null);
            Assert.That(canonical.Validate(profile, catalog, modules, out _), Is.EqualTo("AppearanceScopeRejected"));
        }

        [Test]
        public void 상세공간ID와구형OSM별칭을_외관대상으로자동합치지않는다()
        {
            var overlay = OverlayFixture();
            var detailed = 사가정공간표현OverlayComposer.ComposeOrFallback(overlay, map, asset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);
            Assert.That(detailed.OverlayApplied, Is.True, detailed.DiagnosticCode);
            var gate = new 사가정외관검토Gate(map, detailed, null);
            var profile = Profile();
            profile.spatialContentHashSha256 = detailed.ContentHash;
            Assert.That(gate.Validate(profile, catalog, modules, out _), Is.EqualTo("AppearanceExactBuildingRequired"));
            profile.targetBuildingId = overlay.buildings[0].id;
            Assert.That(gate.Validate(profile, catalog, modules, out _), Is.Empty);
        }

        [Test]
        public void Synty요청이어도_실제View는_검토된공간과선택바탕을유지한다()
        {
            var key = 사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey;
            var oldPath = Environment.GetEnvironmentVariable(key);
            var path = Path.GetTempFileName();
            var root = new GameObject("appearance-overlay-view-fixture");
            root.SetActive(false);
            try
            {
                using (var stream = File.Create(path))
                    new DataContractJsonSerializer(typeof(사가정공간표현OverlayData)).WriteObject(stream, OverlayFixture());
                Environment.SetEnvironmentVariable(key, path);
                var view = root.AddComponent<사가정운영디오라마View>();
                typeof(사가정운영디오라마View).GetField("useSyntyPresentation",
                    BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, true);
                view.Initialize();
                Assert.That(view.MapStatus, Is.EqualTo("Ready"));
                Assert.That(view.SpatialOverlayApplied, Is.True);
                Assert.That(view.SyntyPlacementCount, Is.Zero);
                Assert.That(view.SyntyStatus, Is.EqualTo(사가정공간표현ModePolicy.OverlayPreservedStatus));
                Assert.That(view.MiniatureBuildReport.OverlayApplied, Is.True);
                Assert.That(view.HSelectionBuildingCount, Is.EqualTo(602));
                Assert.That(view.AppearanceOverlayCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Environment.SetEnvironmentVariable(key, oldPath);
                File.Delete(path);
            }
        }

        private 사가정건물외관Profile Profile()
        {
            var building = map.buildings[0];
            return new 사가정건물외관Profile
            {
                profileStableId = "fixture:appearance:1", targetBuildingId = building.id,
                baseMapHashSha256 = 사가정공간표현OverlayComposer.BaseMapHash,
                spatialContentHashSha256 = composition.ContentHash,
                reviewState = 사가정건물외관Profile.ReviewedState, reviewerKind = "CodexVisualReview",
                reviewReference = "fixture:appearance-review", evidenceObservationId = "fixture:actual-pts-observation",
                evidenceSourceSha256 = new string('c', 64), evidenceSourcePts = 1001,
                evidenceTimeBaseNumerator = 1, evidenceTimeBaseDenominator = 30000,
                visualKey = ModuleVisualKey, visualCatalogRevision = catalog.CatalogRevision, moduleKind = "Facade",
                localPosition = new Vector3((building.points.Min(p => p.x) + building.points.Max(p => p.x)) * .5f,
                    사가정모형MeshBuilder.BuildingBaseElevation + Mathf.Max(2f, building.height) * .5f,
                    (building.points.Min(p => p.z) + building.points.Max(p => p.z)) * .5f),
                localScale = Vector3.one * .1f,
            };
        }

        // 합성 시험 자료이며 실제 주소·영상·사용자 승인 또는 모델링 권리의 근거가 아니다.
        private 사가정공간표현OverlayData OverlayFixture()
        {
            return new 사가정공간표현OverlayData
            {
                schemaVersion = 사가정공간표현OverlayComposer.SchemaVersion,
                revision = 사가정공간표현OverlayComposer.PrivateReviewRevision,
                status = "LocalPrivateReview", presentationOnly = true,
                baseMapRevision = map.revision, baseMapHash = 사가정공간표현OverlayComposer.BaseMapHash,
                coordinateMethod = map.coordinateMethod, originLatitude = 사가정공간표현OverlayComposer.OriginLatitude,
                originLongitude = 사가정공간표현OverlayComposer.OriginLongitude,
                offsetX = map.offsetX, offsetZ = map.offsetZ, halfExtent = map.halfExtent,
                contentHash = new string('d', 64), surfaces = Array.Empty<사가정공간표현SurfaceData>(),
                sourceReceipts = new[] { new 사가정공간표현SourceReceiptData
                {
                    provider = 사가정공간표현OverlayComposer.RequiredSpatialProvider,
                    datasetId = 사가정공간표현OverlayComposer.RequiredSpatialDatasetId,
                    sourceUrl = 사가정공간표현OverlayComposer.PublicSpatialSourceUrl,
                    licenseCode = "RightsConflictUnresolved", licenseUrl = "https://creativecommons.org/licenses/by-nc-nd/2.0/kr/",
                    datasetDate = "2026-08-09", fetchedAtUtc = "2026-09-13T00:00:00Z",
                    rawHash = 사가정공간표현OverlayComposer.RequiredSpatialRawHash,
                    rawLength = 사가정공간표현OverlayComposer.RequiredSpatialRawLength,
                    originalCrs = "EPSG:5186", reviewStatus = "LocalPrivateReview"
                } },
                buildings = new[] { new 사가정공간표현BuildingData
                {
                    id = "fixture:official-building:1", sourceFeatureId = "fixture:feature:1",
                    buildingKind = "detached-house", purposeName = "fixture",
                    heightMeters = 4, heightKind = "SymbolicFallback4m", heightPolicyRevision = "fixture:height.r1",
                    reviewStatus = "LocalPrivateReview", legacyOsmIds = new[] { map.buildings[0].id },
                    legacyMatch = new 사가정공간표현LegacyMatchData { method = "FootprintOverlap", ambiguous = false,
                        centroidDistanceMeters = 0, intersectionOverUnion = 1, smallerFootprintCoverage = 1 },
                    parts = new[] { new 사가정공간표현PolygonPartData
                    {
                        outer = new 사가정공간표현RingData { points = map.buildings[0].points
                            .Select(p => new 사가정공간표현PointData { x = p.x, z = p.z }).ToArray() },
                        holes = Array.Empty<사가정공간표현RingData>()
                    } }
                } }
            };
        }
    }
}
