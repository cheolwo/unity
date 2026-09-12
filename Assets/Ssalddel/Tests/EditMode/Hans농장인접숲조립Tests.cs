using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Hans농장인접숲조립Tests
    {
        [Test]
        public void 동일_지표면과_자산은_같은_배치_hash를_만든다()
        {
            var surface = AreaSet배치AuthoringPipeline.BuildSurfaceSnapshot();
            var catalog = Hans농장인접숲Authoring.BuildCatalog();
            var first = Hans농장인접숲Authoring.CreateDraft(surface, catalog);
            var second = Hans농장인접숲Authoring.CreateDraft(surface, catalog);
            Assert.That(AreaSet배치Hash.ComputeDraft(first),
                Is.EqualTo(AreaSet배치Hash.ComputeDraft(second)));
        }

        [Test]
        public void 권위_수리울타리_세좌표는_정적경계에서_중복하지_않는다()
        {
            var surface = AreaSet배치AuthoringPipeline.BuildSurfaceSnapshot();
            var catalog = Hans농장인접숲Authoring.BuildCatalog();
            var draft = Hans농장인접숲Authoring.CreateDraft(surface, catalog);
            var boundary = draft.Placements.Where(x => x.AssetKey == "wood-fence").ToArray();
            Assert.That(boundary.Any(x => x.LocalZ == 2.2f && x.LocalX == -2.4f), Is.False);
            Assert.That(boundary.Any(x => x.LocalZ == 2.2f && x.LocalX == 0f), Is.False);
            Assert.That(boundary.Any(x => x.LocalZ == 2.2f && x.LocalX == 2.4f), Is.False);
        }

        [Test]
        public void 실제_Player_시작점은_숲길_경로에_포함된다()
        {
            var surface = AreaSet배치AuthoringPipeline.BuildSurfaceSnapshot();
            var catalog = Hans농장인접숲Authoring.BuildCatalog();
            var draft = Hans농장인접숲Authoring.CreateDraft(surface, catalog);
            var trail = draft.Routes.Single(x => x.RouteStableId == "gm-edge:forest-trail-to-farm-entry");
            Assert.That(trail.Points.Any(x => x.X == 15.4f && x.Z == 10.25f), Is.True);
        }

        [Test]
        public void 손상주택은_native_scale_1이며_배치검사를_통과한다()
        {
            var surface = AreaSet배치AuthoringPipeline.BuildSurfaceSnapshot();
            var catalog = Hans농장인접숲Authoring.BuildCatalog();
            var draft = Hans농장인접숲Authoring.CreateDraft(surface, catalog);
            Assert.That(draft.Placements.Single(x => x.AssetKey == "damaged-home").UniformScale, Is.EqualTo(1f));
            var receipt = AreaSet배치DraftValidator.Validate(draft, surface, catalog);
            Assert.That(receipt.ErrorCount, Is.Zero,
                string.Join("|", receipt.Issues.Select(x => x.IssueCode + ":" + x.PlacementStableId)));
        }

        [Test]
        public void 손상주택은_현관을_막는_전체_AABB_충돌체를_사용하지_않는다()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                "Assets/SsalddelGenerated/ProjectOwnedVariants/farm-residential-home-damaged-variant/farm-residential-home-damaged-variant.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<UnityEngine.BoxCollider>(), Is.Null);
        }

        [Test]
        public void 중앙_지형은_멀리_배치된_한스_후보_지형을_덮는것으로_판정하지_않는다()
        {
            var canonical = new UnityEngine.Bounds(
                new UnityEngine.Vector3(0f, 1f, 0f),
                new UnityEngine.Vector3(62f, 1.7f, 46f));
            var candidate = new UnityEngine.Bounds(
                new UnityEngine.Vector3(-152.38f, 1f, 22.55f),
                new UnityEngine.Vector3(62f, 1.7f, 46f));

            Assert.That(Hans농장인접숲Authoring.CanonicalTerrainCoversCandidate(canonical, candidate), Is.False);
            Assert.That(Hans농장인접숲Authoring.CanonicalTerrainCoversCandidate(candidate, candidate), Is.True);
        }
    }
}
