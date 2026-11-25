using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정상세건물표현Tests
    {
        private const string Id = "study:exact-building-1";
        private static string Hash => new('a', 64);
        private GameObject parent, fallback, detailed;
        private 사가정상세건물표현Renderer renderer;
        private 사가정상세건물표현Asset asset;
        private Bounds bounds;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("private-study-parent") { hideFlags = HideFlags.DontSave };
            fallback = Cube("fallback-building", parent.transform);
            detailed = Cube("detailed-source", parent.transform); detailed.SetActive(false);
            bounds = new Bounds(Vector3.zero, Vector3.one * 20);
            renderer = new 사가정상세건물표현Renderer(parent.transform, Hash,
                new Dictionary<string, 사가정상세건물표현Target>
                { [Id] = new(Id, bounds, fallback.transform) }, 0);
            asset = new 사가정상세건물표현Asset("private.building.detail.fixture", "fixture.r1", detailed, fallback);
        }
        [TearDown]
        public void TearDown()
        {
            renderer?.Dispose();
            if (parent != null) Object.DestroyImmediate(parent);
        }
        private static GameObject Cube(string name, Transform targetParent)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name;
            value.hideFlags = HideFlags.DontSave; value.transform.SetParent(targetParent, false); return value;
        }
        private static 사가정건물외관Profile Profile() => new()
        {
            scopeKind = 사가정건물외관Profile.PrivateStudyScope,
            profileStableId = "private:detail-profile-1", targetBuildingId = Id,
            baseMapHashSha256 = Hash, spatialContentHashSha256 = Hash,
            reviewState = 사가정건물외관Profile.ReviewedState, reviewerKind = "CodexVisualReview",
            reviewReference = "private:fixture-review", evidenceObservationId = "fixture:source-pts",
            evidenceSourceSha256 = new string('b',64), evidenceSourcePts = 2520518,
            evidenceTimeBaseNumerator = 1, evidenceTimeBaseDenominator = 30000,
            visualKey = "private.building.detail.fixture", visualCatalogRevision = "fixture.r1",
            moduleKind = 사가정상세건물표현Renderer.ModuleKind, presentationOnly = true,
            localPosition = Vector3.zero, localEulerAngles = Vector3.zero, localScale = Vector3.one
        };

        [Test]
        public void 건물별정확대상만_소유하고_공유모형Root는_거부한다()
        {
            Assert.Throws<ArgumentException>(() => new 사가정상세건물표현Renderer(parent.transform, Hash,
                new Dictionary<string, 사가정상세건물표현Target>
                {
                    [Id] = new(Id, bounds, fallback.transform),
                    ["study:other"] = new("study:other", bounds, fallback.transform)
                }, 0));
            Assert.Throws<ArgumentException>(() => new 사가정상세건물표현Renderer(parent.transform, Hash,
                new Dictionary<string, 사가정상세건물표현Target> { [Id] = new(Id,bounds,parent.transform) },0));
        }

        [TestCase("canonical", "DetailedBuildingPrivateScopeRequired")]
        [TestCase("distribution", "DetailedBuildingAuthorityRejected")]
        [TestCase("review", "DetailedBuildingReviewRequired")]
        [TestCase("source", "DetailedBuildingObservationRequired")]
        [TestCase("pts", "DetailedBuildingObservationRequired")]
        [TestCase("spatial", "DetailedBuildingSpatialVersionMismatch")]
        [TestCase("target", "DetailedBuildingExactTargetRequired")]
        [TestCase("visual", "DetailedBuildingVisualVersionMismatch")]
        public void 연구근거와_정확대상판본이_맞지않으면_원래화면을_유지한다(string change, string expected)
        {
            var profile=Profile();
            switch(change)
            {
                case "canonical": profile.scopeKind=사가정건물외관Profile.CanonicalScope; break;
                case "distribution": profile.distributionApproved=true; break;
                case "review": profile.reviewState="Candidate"; break;
                case "source": profile.evidenceSourceSha256="unknown"; break;
                case "pts": profile.evidenceTimeBaseDenominator=0; break;
                case "spatial": profile.spatialContentHashSha256=new string('c',64); break;
                case "target": profile.targetBuildingId="study:nearby-building"; break;
                case "visual": profile.visualCatalogRevision="fixture.r2"; break;
            }
            Assert.That(renderer.TryApply(profile,asset,out var code), Is.False);
            Assert.That(code, Is.EqualTo(expected)); Assert.That(fallback.activeSelf, Is.True);
            Assert.That(renderer.Count, Is.Zero);
        }

        [Test]
        public void 비활성입력을_정적으로복사하고_스크립트와Collider는_복제하지않는다()
        {
            detailed.AddComponent<AudioSource>();
            var originalMesh=detailed.GetComponent<MeshFilter>().sharedMesh;
            var originalMaterials=detailed.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.That(renderer.TryApply(Profile(),asset,out var status),Is.True,status);
            Assert.That(fallback.activeSelf,Is.False);
            Assert.That(detailed.activeSelf,Is.False,"입력 모델을 직접 켜지 않는다.");
            var copied=renderer.VisualRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Single(r=>r.name=="detailed-source");
            Assert.That(copied.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(originalMesh));
            Assert.That(copied.sharedMaterials,Is.EqualTo(originalMaterials));
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<AudioSource>(true),Is.Empty);
            Assert.That(renderer.VisualRoot.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty);
            Assert.That(detailed.GetComponent<Collider>(),Is.Not.Null);
            Assert.That(detailed.GetComponent<AudioSource>(),Is.Not.Null);
        }

        [Test]
        public void 가까움_중간_먼거리에서_한가지외형만_표시하고_공유입력을_변경하지않는다()
        {
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            var near=renderer.Snapshot().Single();
            Assert.That(near.level,Is.EqualTo("Detailed"));
            Assert.That(near.detailedActive && !near.middleActive && !near.fallbackActive,Is.True);
            renderer.RefreshForObserver(new Vector3(80,0,0));
            var middle=renderer.Snapshot().Single();
            Assert.That(middle.level,Is.EqualTo("Middle"));
            Assert.That(!middle.detailedActive && middle.middleActive && !middle.fallbackActive,Is.True);
            renderer.RefreshForObserver(new Vector3(300,0,0));
            var far=renderer.Snapshot().Single();
            Assert.That(far.level,Is.EqualTo("Fallback"));
            Assert.That(!far.detailedActive && !far.middleActive && far.fallbackActive,Is.True);
            renderer.RefreshForObserver(Vector3.zero);
            Assert.That(renderer.Snapshot().Single().level,Is.EqualTo("Detailed"));
            Assert.That(detailed.activeSelf,Is.False);
            Assert.Throws<ArgumentException>(()=>renderer.RefreshForObserver(new Vector3(float.NaN,0,0)));
        }

        [Test]
        public void 외관별PropertyBlock색과_원본재질참조는_그대로_보존한다()
        {
            var original=detailed.GetComponent<MeshRenderer>();
            var block=new MaterialPropertyBlock(); var color=new Color(.07f,.18f,.42f);
            block.SetColor("_ReviewColor",color); original.SetPropertyBlock(block);
            // Unity의 Color 저장 변환 이후 실제 보관 값을 비교한다. 리터럴 왕복 반올림은 별도다.
            var stored = block.GetVector("_ReviewColor");
            var material=original.sharedMaterial;
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            var copied=renderer.VisualRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Single(r=>r.name=="detailed-source");
            var read=new MaterialPropertyBlock(); copied.GetPropertyBlock(read);
            Assert.That(read.GetVector("_ReviewColor"),Is.EqualTo(stored));
            Assert.That(copied.sharedMaterial,Is.SameAs(material));
            renderer.Dispose();
            Assert.That(original.sharedMaterial,Is.SameAs(material));
            original.GetPropertyBlock(read); Assert.That(read.GetVector("_ReviewColor"),Is.EqualTo(stored));
        }

        [Test]
        public void 새상세기하가_대상범위를_벗어나면_기존모형으로_복귀한다()
        {
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            detailed.transform.localPosition=new Vector3(1000,0,0);
            Assert.That(renderer.TryApply(Profile(),asset,out var code),Is.False);
            Assert.That(code,Is.EqualTo("DetailedBuildingStaticAssetRejected"));
            Assert.That(renderer.Count,Is.Zero); Assert.That(fallback.activeSelf,Is.True);
            Assert.That(renderer.VisualRoot.childCount,Is.Zero);
        }

        [Test]
        public void 자기출력을_새입력으로_돌려넣거나_중첩LOD로_혼합하지않는다()
        {
            var recursive=new 사가정상세건물표현Asset(asset.VisualKey,asset.CatalogRevision,
                renderer.VisualRoot.gameObject,fallback);
            Assert.That(renderer.TryApply(Profile(),recursive,out var recursiveCode),Is.False);
            Assert.That(recursiveCode,Is.EqualTo("DetailedBuildingStaticAssetRejected"));
            detailed.AddComponent<LODGroup>();
            Assert.That(renderer.TryApply(Profile(),asset,out var lodCode),Is.False);
            Assert.That(lodCode,Is.EqualTo("DetailedBuildingStaticAssetRejected"));
            Assert.That(fallback.activeSelf,Is.True); Assert.That(renderer.Count,Is.Zero);
        }

        [Test]
        public void 거부된다른판본은_기존유효한상세표현을_지우지않는다()
        {
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            var changed=Profile(); changed.visualCatalogRevision="unknown.r2";
            Assert.That(renderer.TryApply(changed,asset,out _),Is.False);
            Assert.That(renderer.Count,Is.EqualTo(1)); Assert.That(fallback.activeSelf,Is.False);
            Assert.That(renderer.Snapshot().Single().level,Is.EqualTo("Detailed"));
        }

        [Test]
        public void 재적용_제거_해제는_원래Active상태와_참조를_복구한다()
        {
            var originalMesh=fallback.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            Assert.That(renderer.Count,Is.EqualTo(1)); Assert.That(renderer.VisualRoot.childCount,Is.EqualTo(1));
            Assert.That(renderer.RemoveBuilding(Id),Is.True);
            Assert.That(renderer.RemoveBuilding(Id),Is.False); Assert.That(fallback.activeSelf,Is.True);
            Assert.That(fallback.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(originalMesh));
            renderer.Dispose(); renderer.Dispose();
            Assert.That(renderer.TryApply(Profile(),asset,out var released),Is.False);
            Assert.That(released,Is.EqualTo("DetailedBuildingRendererReleased"));
        }

        [Test]
        public void 소유출력이_먼저사라져도_다음갱신에서_간략모형으로_복귀한다()
        {
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            Assert.That(fallback.activeSelf,Is.False);
            Object.DestroyImmediate(renderer.VisualRoot.gameObject);
            Assert.DoesNotThrow(()=>renderer.RefreshForObserver(Vector3.zero));
            Assert.That(fallback.activeSelf,Is.True);
            Assert.That(renderer.Count,Is.Zero);
            Assert.That(renderer.StatusCode,Is.EqualTo("DetailedBuildingRendererReleased"));
        }

        [Test]
        public void 처음부터숨겨둔_간략모형은_해제후에도_숨김상태를_유지한다()
        {
            renderer.Dispose(); fallback.SetActive(false);
            renderer=new 사가정상세건물표현Renderer(parent.transform,Hash,
                new Dictionary<string,사가정상세건물표현Target>{[Id]=new(Id,bounds,fallback.transform)},0);
            Assert.That(renderer.TryApply(Profile(),asset,out _),Is.True);
            renderer.Dispose();
            Assert.That(fallback.activeSelf,Is.False);
        }
    }
}
