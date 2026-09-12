using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class World공간자산배치RuntimeTests
    {
        [Test]
        public void 실외Runtime은_같은StableId를중복생성하지않고_상태Prefab만교체한다()
        {
            var host = new GameObject("WorldAssetRuntimeTestHost");
            var cell = new GameObject("WorldAssetRuntimeTestCell");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.SetParent(cell.transform, false);
                floor.transform.localScale = new Vector3(125f, .2f, 125f);
                var runtime = host.AddComponent<실외자산배치Runtime>();
                var plan = CellPlan("Nature.Tree.Standing");

                Assert.That(runtime.Apply(plan, cell.transform, floor, null),
                    Is.True);
                Assert.That(runtime.ObjectCount(plan.CellKey), Is.EqualTo(1));
                Assert.That(runtime.TryGetObject(plan.CellKey,
                    "nature-resource:tree:01", out var standing), Is.True);
                Assert.That(standing.GetComponent<
                    Nature생존상호작용표식>(), Is.Not.Null);

                plan.WorldAssetAssembly.ExteriorPlacement.Placements[0]
                    .CompositionKey = "Nature.Tree.Stump";
                plan.WorldAssetAssembly.ExteriorPlacement.Placements[0]
                    .StateCode = SimulationNatureSurvivalCodes.Stump;
                Assert.That(runtime.Apply(plan, cell.transform, floor, null),
                    Is.True);
                Assert.That(runtime.ObjectCount(plan.CellKey), Is.EqualTo(1));
                Assert.That(runtime.TryGetObject(plan.CellKey,
                    "nature-resource:tree:01", out var stump), Is.True);
                Assert.That(stump, Is.Not.SameAs(standing));
                Assert.That(stump.GetComponent<
                    Nature생존상호작용표식>(), Is.Null);
                Assert.That(배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(stump.transform,
                        floor.GetComponent<Renderer>().bounds.max.y).Passed,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cell);
            }
        }

        [Test]
        public void 실내Runtime은_오두막아래에_침대선반작업대와보관Overlay를조립한다()
        {
            var host = new GameObject("WorldInteriorRuntimeTestHost");
            var cell = new GameObject("WorldInteriorRuntimeTestCell");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.transform.SetParent(cell.transform, false);
                floor.transform.localScale = new Vector3(125f, .2f, 125f);
                var exterior = host.AddComponent<실외자산배치Runtime>();
                var interior = host.AddComponent<실내자산배치Runtime>();
                var plan = CabinPlan();

                Assert.That(exterior.Apply(plan, cell.transform, floor, null),
                    Is.True);
                Assert.That(interior.Apply(plan, cell.transform), Is.True);
                var names = cell.GetComponentsInChildren<Transform>(true)
                    .Select(value => value.name).ToArray();
                Assert.That(names.Any(value => value.StartsWith(
                    "InteriorPlacement_cabin:bed",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(names.Any(value => value.StartsWith(
                    "InteriorPlacement_cabin:rack",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(names.Any(value => value.StartsWith(
                    "InteriorPlacement_cabin:table",
                    StringComparison.Ordinal)), Is.True);
                Assert.That(names.Any(value => value.StartsWith(
                    "InteriorOverlay_cabin:stored-timber",
                    StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cell);
            }
        }

        private static 공간LHCellPlanData CellPlan(string compositionKey)
        {
            var hash = new string('a', 64);
            var exterior = new Simulation실외자산배치Plan
            {
                CellStableId = "kr5186:l3:2801:4581",
                SourceWorldRevision = 7,
                SourceLhBasePlanHashSha256 = hash,
                SourceCombinedPlanHashSha256 = hash,
                ExteriorPlacementPlanHashSha256 = new string('b', 64),
                Placements = new[]
                {
                    new Simulation세계자산PlacementSnapshot
                    {
                        PlacementStableId = "nature-resource:tree:01",
                        OwnerCellStableId = "kr5186:l3:2801:4581",
                        CategoryCode = "NatureResourceNode",
                        CompositionKey = compositionKey,
                        AuthorityKindCode =
                            Simulation세계자산배치Codes.SimulationEntity,
                        StateCode = compositionKey.EndsWith("Stump",
                            StringComparison.Ordinal)
                            ? SimulationNatureSurvivalCodes.Stump
                            : SimulationNatureSurvivalCodes.Standing,
                        LocalXMeters = 2,
                        LocalZMeters = 3,
                        UniformScale = 1,
                        SourceChangeStableIds = new[] { "tree:01" },
                    },
                },
            };
            return new 공간LHCellPlanData
            {
                CellKey = exterior.CellStableId,
                CellX = 2801,
                CellY = 4581,
                WindowRoleCode = 공간LHWorldCodes.Detail,
                ContentSourceCode = 공간LHWorldCodes.ScenarioProcedural,
                BasePlanHashSha256 = hash,
                PresentationHashSha256 = new string('c', 64),
                PresentationOnly = true,
                WorldAssetAssembly = new SimulationWorldCellAssemblyResponse
                {
                    CellStableId = exterior.CellStableId,
                    SourceWorldRevision = 7,
                    SourceLhBasePlanHashSha256 = hash,
                    ExteriorPlacement = exterior,
                    InteriorPlacement = new Simulation실내자산배치Plan
                    {
                        CellStableId = exterior.CellStableId,
                        SourceLhBasePlanHashSha256 = hash,
                        InteriorPlacementPlanHashSha256 =
                            new string('d', 64),
                    },
                    IsAvailable = true,
                    AssemblyHashSha256 = new string('e', 64),
                },
            };
        }

        private static 공간LHCellPlanData CabinPlan()
        {
            var plan = CellPlan("Nature.Cabin.Completed");
            var exterior = plan.WorldAssetAssembly.ExteriorPlacement;
            exterior.Placements[0].PlacementStableId =
                "nature-cabin:facility:nature-cabin";
            exterior.Placements[0].CategoryCode = "NatureCabin";
            exterior.Placements[0].StateCode =
                SimulationNatureSurvivalCodes.Completed;
            exterior.Placements[0].SourceChangeStableIds =
                new[] { "facility:nature-cabin" };
            var body = new SimulationInteriorPlacementPlanBodySnapshot
            {
                BuildingPlacementStableId =
                    "nature-cabin:facility:nature-cabin",
                BodyHashSha256 = new string('1', 64),
                Placements = new[]
                {
                    Item("cabin:bed", "Nature.Shelter.Bedroll"),
                    Item("cabin:rack", "Nature.Storage.Rack.Small"),
                    Item("cabin:table", "Nature.Work.Table.Small"),
                },
            };
            plan.WorldAssetAssembly.InteriorPlacement =
                new Simulation실내자산배치Plan
                {
                    CellStableId = plan.CellKey,
                    SourceWorldRevision = 7,
                    SourceLhBasePlanHashSha256 =
                        plan.BasePlanHashSha256,
                    InteriorPlanBodies = new[] { body },
                    OverlayPlacements = new[]
                    {
                        new Simulation세계자산PlacementSnapshot
                        {
                            PlacementStableId = "cabin:stored-timber",
                            ParentPlacementStableId = "cabin:rack",
                            CompositionKey =
                                "Nature.Storage.Timber.Half",
                            UniformScale = 1,
                        },
                    },
                    InteriorPlacementPlanHashSha256 =
                        new string('2', 64),
                };
            return plan;
        }

        private static SimulationInteriorPlacementBodyItemSnapshot Item(
            string id, string visualKey)
            => new()
            {
                PlacementStableId = id,
                VisualKey = visualKey,
                UniformScale = 1,
            };
    }
}
