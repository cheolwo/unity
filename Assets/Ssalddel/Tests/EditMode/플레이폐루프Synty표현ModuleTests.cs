using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 플레이폐루프Synty표현ModuleTests
    {
        [Test]
        public void Nature핵심폐루프는_A_B_C강제없이_WI와H를표현슬롯으로묶는다()
        {
            var modules = Nature플레이폐루프Synty표현Modules.All;

            Assert.That(modules.Count, Is.EqualTo(4));
            Assert.That(modules.Select(value => value.LoopStableId).Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(modules.SelectMany(value => value.Slots).Count(), Is.EqualTo(21));
            Assert.That(modules.SelectMany(value => value.Slots)
                .Any(value => value.AssetFamilyIds.Count == 1), Is.True,
                "적합한 후보 하나를 세 변형으로 억지 확장하지 않아야 한다.");
            Assert.That(modules.SelectMany(value => value.Slots)
                .Any(value => value.AssetFamilyIds.Count == 3), Is.True);

            foreach (var module in modules)
                Assert.DoesNotThrow(module.Validate);
        }

        [Test]
        public void 같은상태와Seed는_같은자산계열을결정한다()
        {
            var module = Nature플레이폐루프Synty표현Modules.Find(
                Nature플레이폐루프Synty표현Modules.ShelterFoundation);
            var request = new 플레이폐루프Synty표현SelectionRequest(
                7421,
                module.LoopStableId,
                "WI-NATURE-06",
                "nature-shelter.harvest-progress",
                "placement:nature-home:tree-01",
                "TreeHarvestTaskActive");

            var first = 플레이폐루프Synty표현Resolver.Resolve(module, request);
            var second = 플레이폐루프Synty표현Resolver.Resolve(module, request);

            Assert.That(second.AssetFamilyId, Is.EqualTo(first.AssetFamilyId));
            Assert.That(second.SelectionHash, Is.EqualTo(first.SelectionHash));
            Assert.That(first.AssetFamilyId,
                Does.StartWith("synty-family:nature:trees:"));
        }

        [TestCase("nature-night.day2-workbench-plan", "WorkbenchExpansionPlanSelected",
            "synty-family:farm:props:table", "synty-family:construction:props:toolbox")]
        [TestCase("nature-night.day2-storage-plan", "StorageExpansionPlanSelected",
            "synty-family:farm:props:rack", "synty-family:construction:props:crate-stack")]
        [TestCase("nature-night.day2-barricade-plan", "BarricadeExpansionPlanSelected",
            "synty-family:farm:props:fence-wood", "synty-family:construction:props:barrier")]
        public void Day2선택은_계획종류에맞는자산계열에서만선택한다(
            string slotStableId,
            string stateCode,
            string firstAllowed,
            string secondAllowed)
        {
            var module = Nature플레이폐루프Synty표현Modules.Find(
                Nature플레이폐루프Synty표현Modules.NightDay2);
            var selection = 플레이폐루프Synty표현Resolver.Resolve(
                module,
                new 플레이폐루프Synty표현SelectionRequest(
                    7421,
                    module.LoopStableId,
                    "WI-NATURE-15",
                    slotStableId,
                    "placement:nature-home:day2-plan",
                    stateCode));

            Assert.That(selection.AssetFamilyId,
                Is.EqualTo(firstAllowed).Or.EqualTo(secondAllowed));
        }

        [Test]
        public void 선택된자산계열은_기술대장의Prefab으로결정적으로해석된다()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                "Assets/Ssalddel/Presentation/World/Catalogs/"
                + "SyntyPackAssetInventoryCatalog.asset");
            Assert.That(inventory, Is.Not.Null);

            var module = Nature플레이폐루프Synty표현Modules.Find(
                Nature플레이폐루프Synty표현Modules.NightDay2);
            var selection = 플레이폐루프Synty표현Resolver.Resolve(
                module,
                new 플레이폐루프Synty표현SelectionRequest(
                    7421,
                    module.LoopStableId,
                    "WI-NATURE-13",
                    "nature-night.storage-empty",
                    "placement:nature-home:storage",
                    "ShelterStorageAvailable"));

            var first = 플레이폐루프Synty표현Resolver.ResolvePrefabEntry(
                inventory, selection, "placement:nature-home:storage");
            var second = 플레이폐루프Synty표현Resolver.ResolvePrefabEntry(
                inventory, selection, "placement:nature-home:storage");

            Assert.That(first.InventoryId, Is.EqualTo(second.InventoryId));
            Assert.That(first.AssetFamilyId, Is.EqualTo(selection.AssetFamilyId));
            Assert.That(first.Prefab, Is.Not.Null);
        }

        [Test]
        public void Nature모듈의모든자산계열은_기술대장에존재한다()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                "Assets/Ssalddel/Presentation/World/Catalogs/"
                + "SyntyPackAssetInventoryCatalog.asset");
            Assert.That(inventory, Is.Not.Null);
            inventory.Validate();
            var knownFamilies = inventory.Entries
                .Select(value => value.AssetFamilyId)
                .ToHashSet(System.StringComparer.Ordinal);

            var missing = Nature플레이폐루프Synty표현Modules.All
                .SelectMany(value => value.Slots)
                .SelectMany(value => value.AssetFamilyIds)
                .Distinct(System.StringComparer.Ordinal)
                .Where(value => !knownFamilies.Contains(value))
                .ToArray();

            Assert.That(missing, Is.Empty,
                "폐루프 표현 모듈은 실제 Synty 기술 대장 자산 계열만 참조해야 한다.");
        }
    }
}
