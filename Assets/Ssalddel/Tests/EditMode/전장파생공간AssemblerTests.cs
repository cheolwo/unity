using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 전장파생공간AssemblerTests
    {
        [Test]
        public void H5위치와무관하게_BattleLocalMeters_독립전장을조립한다()
        {
            var parent = new GameObject("H5와분리된전장표시Root");
            parent.transform.position = new Vector3(1842f, 0f, 937f);
            GameObject root = null;
            try
            {
                root = new 전장파생공간Assembler().Build(Battle(), parent.transform);

                Assert.That(root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(root.transform.position, Is.EqualTo(parent.transform.position));
                var anchor = root.GetComponentsInChildren<전장파생표시Tag>()
                    .Single(value => value.KindCode == "Anchor");
                Assert.That(anchor.transform.localPosition.x, Is.EqualTo(40f));
                Assert.That(anchor.transform.localPosition.z, Is.EqualTo(-20f));
                Assert.That(anchor.BattleStableId, Is.EqualTo("battle-anchor:farm-gate"));
                Assert.That(anchor.SourceStableId, Is.EqualTo("h1:farm-gate"));
                Assert.That(anchor.WorldEffectTargetStableId,
                    Is.EqualTo("h1:farm-gate"));
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(8));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void 확정Draft는_미리보기의지역문맥과파생입력해시만전달한다()
        {
            var battle = Battle();
            var preview = new BattleCreatePreviewApiModel
            {
                WorldRevision = 17,
                EncounterStableId = battle.EncounterStableId,
                BattlefieldDerivation = battle.BattlefieldDerivation,
                UnitRoster = battle.UnitRoster,
                CanConfirm = true,
            };

            var draft = BattleCreateCommandFactory.Create(preview,
                "command:unity:battle:create", "actor:commander");

            Assert.That(draft.ExpectedWorldRevision, Is.EqualTo(17));
            Assert.That(draft.ExpectedBattleWorldContextHashSha256,
                Is.EqualTo("context-hash"));
            Assert.That(draft.ExpectedBattlefieldDerivationInputHashSha256,
                Is.EqualTo("derivation-hash"));
        }

        private static BattleInstanceApiModel Battle() => new()
        {
            BattleStableId = "battle:fixture",
            EncounterStableId = "encounter:farm-gate",
            AreaStableId = "area:farm",
            SimulationOnly = true,
            BattlefieldDerivation = new BattlefieldDerivationApiModel
            {
                CanConfirm = true,
                BattlefieldDerivationInputHashSha256 = "derivation-hash",
                WorldContext = new BattleWorldContextApiModel
                {
                    ContextHashSha256 = "context-hash",
                    AnchorSetHashSha256 = "anchor-hash",
                    SimulationOnly = true,
                    Anchors = new[]
                    {
                        new BattlefieldAnchorApiModel
                        {
                            BattlefieldAnchorStableId = "battle-anchor:farm-gate",
                            SourceStableId = "h1:farm-gate",
                            WorldEffectTargetStableId = "h1:farm-gate",
                            PreservationPolicyCode = BattlefieldPresentationCodes.Required,
                        },
                    },
                },
                BattlefieldPlan = new BattlefieldPlanApiModel
                {
                    BattlefieldPlanStableId = "battlefield-plan:farm",
                    BattlefieldPlanHashSha256 = "plan-hash",
                    BattlefieldDerivationInputHashSha256 = "derivation-hash",
                    CoordinateSpaceCode = BattlefieldPresentationCodes.BattleLocalMeters,
                    WidthMeters = 500,
                    DepthMeters = 500,
                    GridCellSizeMeters = 4,
                    SimulationOnly = true,
                    AnchorPlacements = new[]
                    {
                        new BattlefieldAnchorPlacementApiModel
                        {
                            BattlefieldAnchorStableId = "battle-anchor:farm-gate",
                            BattlePose = Pose(40, -20),
                            WidthMeters = 12,
                            DepthMeters = 6,
                        },
                    },
                    Zones = new[]
                    {
                        new BattlefieldZoneApiModel
                        {
                            ZoneStableId = "zone:allied",
                            ZoneKindCode = "AlliedDeployment",
                            CenterPose = Pose(0, 180),
                            WidthMeters = 80,
                            DepthMeters = 40,
                        },
                    },
                    TerrainCells = new[]
                    {
                        new BattlefieldTerrainCellApiModel
                        { CellX = 0, CellZ = 0, TerrainCode = "Farm", Walkable = true },
                        new BattlefieldTerrainCellApiModel
                        { CellX = 1, CellZ = 0, TerrainCode = "Farm", Walkable = true },
                    },
                },
            },
            UnitRoster = new BattleUnitRosterApiModel
            {
                Units = new[]
                {
                    new BattleUnitApiModel
                    {
                        UnitStableId = "battle-unit:allied:000",
                        SideCode = BattlefieldPresentationCodes.Allied,
                        InitialPose = Pose(-40, 180),
                    },
                    new BattleUnitApiModel
                    {
                        UnitStableId = "battle-unit:hostile:000",
                        SideCode = BattlefieldPresentationCodes.Hostile,
                        InitialPose = Pose(40, -180),
                    },
                },
            },
        };

        private static BattleSpatialPoseApiModel Pose(double x, double z) => new()
        {
            CoordinateSpaceCode = BattlefieldPresentationCodes.BattleLocalMeters,
            XMeters = x,
            ZMeters = z,
        };
    }
}
