using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class AreaSet배치AuthoringPipelineTests
    {
        [Test]
        public void 삼각분할지표면을Unity좌표로보간한다()
        {
            var sampler = new AreaSet지표면Sampler(Surface());

            Assert.That(sampler.TrySample(.25f, .25f, out var first), Is.True);
            Assert.That(first, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(sampler.TrySample(.75f, .75f, out var second), Is.True);
            Assert.That(second, Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(sampler.TrySample(2f, 2f, out _), Is.False);
        }

        [Test]
        public void 자동접지는지표면과Prefab바닥교정값으로Y를결정한다()
        {
            var (draft, surface, catalog) = ValidFixture();

            var receipt = AreaSet배치DraftValidator.Validate(draft, surface, catalog);

            Assert.That(receipt.ErrorCount, Is.EqualTo(0),
                string.Join("|", receipt.Issues.Select(x => x.IssueCode)));
            Assert.That(receipt.ResolvedPlacements.Single().SurfaceHeightMeters,
                Is.EqualTo(1f).Within(.0001f));
            Assert.That(receipt.ResolvedPlacements.Single().LocalY,
                Is.EqualTo(2f).Within(.0001f));
        }

        [Test]
        public void 오래된지형판본은배치를차단한다()
        {
            var (draft, surface, catalog) = ValidFixture();
            draft.SurfaceHashSha256 = "STALE";

            var receipt = AreaSet배치DraftValidator.Validate(draft, surface, catalog);

            Assert.That(receipt.CanStage, Is.False);
            Assert.That(receipt.Issues.Any(x => x.IssueCode == "SurfaceSnapshotStale"), Is.True);
        }

        [Test]
        public void 높이예외는자산허용과사유가모두필요하다()
        {
            var (draft, surface, catalog) = ValidFixture();
            draft.Placements[0].HeightPolicyCode = AreaSet배치HeightPolicyCodes.SurfaceOffset;
            draft.Placements[0].SurfaceOffsetMeters = .5f;

            var rejected = AreaSet배치DraftValidator.Validate(draft, surface, catalog);
            Assert.That(rejected.Issues.Any(x => x.IssueCode == "SurfaceOffsetNotApproved"), Is.True);

            catalog.Items[0].SupportsSurfaceOffset = true;
            catalog.Items[0].AssetFingerprint = AreaSet배치Hash.ComputeCalibrationFingerprint(catalog.Items[0]);
            catalog.CatalogHashSha256 = AreaSet배치Hash.ComputeCatalog(catalog);
            draft.Placements[0].AssetFingerprint = catalog.Items[0].AssetFingerprint;
            draft.CalibrationCatalogHashSha256 = catalog.CatalogHashSha256;
            draft.Placements[0].HeightExceptionReason = "의도적인 단상 높이";

            var accepted = AreaSet배치DraftValidator.Validate(draft, surface, catalog);
            Assert.That(accepted.ErrorCount, Is.EqualTo(0),
                string.Join("|", accepted.Issues.Select(x => x.IssueCode)));
            Assert.That(accepted.ResolvedPlacements.Single().LocalY,
                Is.EqualTo(2.5f).Within(.0001f));
        }

        [Test]
        public void 충돌대상중첩은자동이동없이차단한다()
        {
            var (draft, surface, catalog) = ValidFixture();
            var duplicate = AreaSet배치Json.Deserialize<AreaSet배치DraftItemData>(
                AreaSet배치Json.Serialize(draft.Placements[0]));
            duplicate.PlacementStableId = "placement-02";
            draft.Placements = new[] { draft.Placements[0], duplicate };

            var receipt = AreaSet배치DraftValidator.Validate(draft, surface, catalog);

            Assert.That(receipt.Issues.Any(x => x.IssueCode == "PlacementFootprintOverlap"), Is.True);
            Assert.That(receipt.ResolvedPlacements.All(x => x.LocalX == .5f && x.LocalZ == .5f), Is.True);
        }

        private static (AreaSet배치DraftData Draft, AreaSet지표면SnapshotData Surface,
            AreaSet배치자산CatalogData Catalog) ValidFixture()
        {
            var surface = Surface();
            var asset = new AreaSet배치자산CalibrationData
            {
                AssetKey = "asset-01",
                H1StableId = "h1:test",
                PlacementKindCode = AreaSet배치KindCodes.WorkSurface,
                PrefabAssetPath = "Assets/Test.prefab",
                ModelAssetPath = "Assets/Test.fbx",
                AssetGuid = "guid",
                AssetDependencyHash = "dependency",
                BoundsMinimumX = -.1f,
                BoundsMinimumY = -.5f,
                BoundsMinimumZ = -.1f,
                BoundsCenterX = 0f,
                BoundsCenterY = 0f,
                BoundsCenterZ = 0f,
                BoundsSizeX = .2f,
                BoundsSizeY = 1f,
                BoundsSizeZ = .2f,
                MaximumSlopeDegrees = 90f,
                MaximumHeightSpreadMeters = 10f,
                CollisionEligible = true,
            };
            asset.AssetFingerprint = AreaSet배치Hash.ComputeCalibrationFingerprint(asset);
            var catalog = new AreaSet배치자산CatalogData
            {
                CatalogStableId = "catalog:test",
                CatalogRevision = "r1",
                Items = new[] { asset },
            };
            catalog.CatalogHashSha256 = AreaSet배치Hash.ComputeCatalog(catalog);
            var draft = new AreaSet배치DraftData
            {
                DraftStableId = "draft:test",
                DraftRevision = "r1",
                AreaSetStableId = surface.AreaSetStableId,
                SurfaceSnapshotStableId = surface.SnapshotStableId,
                SurfaceHashSha256 = surface.SurfaceHashSha256,
                CalibrationCatalogStableId = catalog.CatalogStableId,
                CalibrationCatalogHashSha256 = catalog.CatalogHashSha256,
                Placements = new[]
                {
                    new AreaSet배치DraftItemData
                    {
                        PlacementStableId = "placement-01",
                        H1StableId = asset.H1StableId,
                        AssetKey = asset.AssetKey,
                        AssetFingerprint = asset.AssetFingerprint,
                        LocalX = .5f,
                        LocalZ = .5f,
                        UniformScale = 2f,
                    },
                },
            };
            return (draft, surface, catalog);
        }

        private static AreaSet지표면SnapshotData Surface()
        {
            var value = new AreaSet지표면SnapshotData
            {
                SnapshotStableId = "surface:test",
                SnapshotRevision = "r1",
                AreaSetStableId = "area-set:test",
                SourceMeshAssetPath = "Assets/Test.asset",
                SourceMeshDependencyHash = "mesh-hash",
                Columns = 2,
                Rows = 2,
                MinimumX = 0f,
                MaximumX = 1f,
                MinimumZ = 0f,
                MaximumZ = 1f,
                Heights = new[] { 0f, 1f, 1f, 2f },
                Triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
            value.SurfaceHashSha256 = AreaSet배치Hash.ComputeSurface(value);
            return value;
        }
    }
}
