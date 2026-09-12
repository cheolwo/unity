using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Synty전체자산ModuleCatalogTests
    {
        [Test]
        public void 현재_13개_팩의_모든_Prefab은_기능_Module이나_보류_이유를_가진다()
        {
            var sources = Synty전체자산ModuleCatalogBuilder.DiscoverSourcePaths().ToArray();

            Assert.That(sources, Has.Length.EqualTo(4211));
            Assert.That(sources.Select(value => value.PackCode).Distinct(),
                Is.EquivalentTo(Synty전체자산SourcePackCodes.All));
            foreach (var source in sources)
            {
                var classification = Synty자산ModuleClassifier.Classify(
                    source.PackCode, source.Path);
                Assert.That(classification.Validate(), Is.True, source.Path);
                Assert.That(classification.ModuleCodes.Count > 0
                    || !string.IsNullOrWhiteSpace(classification.HoldReasonCode),
                    Is.True, source.Path);
            }
        }

        [Test]
        public void 신규_구매_6팩은_기존_영역을_대체하지_않는_검토_후보이다()
        {
            Assert.That(Synty구매자산활용Profiles.All.Count, Is.EqualTo(6));
            Assert.That(Synty구매자산활용Profiles.All.Select(value => value.SourcePackCode),
                Is.EquivalentTo(new[]
                {
                    Synty전체자산SourcePackCodes.AnimationBaseLocomotion,
                    Synty전체자산SourcePackCodes.AnimationEmotesAndTaunts,
                    Synty전체자산SourcePackCodes.AnimationSwordCombat,
                    Synty전체자산SourcePackCodes.AlpineMountain,
                    Synty전체자산SourcePackCodes.DungeonRealms,
                    Synty전체자산SourcePackCodes.DwarvenDungeonMap,
                }));
            foreach (var profile in Synty구매자산활용Profiles.All)
            {
                Assert.DoesNotThrow(profile.Validate);
                Assert.That(profile.LifecycleCode,
                    Is.EqualTo(Synty자산LifecycleCodes.NeedsReview));
                Assert.That(profile.PresentationOnly, Is.True);
                Assert.That(profile.RequiredHCapabilities, Is.Not.Empty);
            }
        }

        [Test]
        public void Construction은_독립_영역이_아니라_공유_상태_Module이다()
        {
            var classification = Synty자산ModuleClassifier.Classify(
                Synty전체자산SourcePackCodes.Construction,
                "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Wood_Frame.prefab");

            Assert.That(classification.ModuleCodes,
                Does.Contain(Synty자산ModuleCodes.ConstructionState));
        }

        [Test]
        public void 사람이_읽는_분류는_실외_실내_공통_표현과_한국어_기능군을_사용한다()
        {
            Assert.That(Synty표현범위Codes.All, Is.EqualTo(new[]
            {
                "Outdoor", "Interior", "Shared",
            }));
            Assert.That(Synty표현범위Codes.All.Select(Synty표현범위Codes.사람이름),
                Is.EqualTo(new[] { "실외 표현", "실내 표현", "공통 표현" }));
            Assert.That(Synty자산기능분류기준.All, Has.Count.EqualTo(12));
            Assert.That(Synty자산기능분류기준.All.Select(value => value.기능군Code),
                Is.EquivalentTo(Synty자산ModuleCodes.All));
            Assert.That(Synty자산기능분류기준.찾기(Synty자산ModuleCodes.InteriorFixture).사람이읽는경로,
                Is.EqualTo("실내 표현 → 실내 설비"));
            Assert.That(Synty자산기능분류기준.찾기(Synty자산ModuleCodes.ConstructionState).사람이읽는경로,
                Is.EqualTo("공통 표현 → 건설·복구 표현"));
        }

        [Test]
        public void Generic과_Starter의_수명주기를_생산_승인으로_오인하지_않는다()
        {
            var generic = Synty자산ModuleClassifier.Classify(
                Synty전체자산SourcePackCodes.Generic,
                "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Prop_Table.prefab");
            var starter = Synty자산ModuleClassifier.Classify(
                Synty전체자산SourcePackCodes.Starter,
                "Assets/Synty/PolygonStarter/Prefabs/SM_Prop.prefab");

            Assert.That(generic.LifecycleCode, Is.EqualTo(Synty자산LifecycleCodes.SharedBase));
            Assert.That(starter.LifecycleCode, Is.EqualTo(Synty자산LifecycleCodes.PrototypeFallback));
        }

        [Test]
        public void 기존_CompositionKey는_읽기_호환_Module_참조로만_변환한다()
        {
            var refs = LegacyCompositionAdapter.ToModuleRefs(
                "legacy:nature-road-construction:A");

            Assert.That(refs.Select(value => value.ModuleCode), Does.Contain(Synty자산ModuleCodes.NatureVegetation));
            Assert.That(refs.Select(value => value.ModuleCode), Does.Contain(Synty자산ModuleCodes.WorldNetwork));
            Assert.That(refs.Select(value => value.ModuleCode), Does.Contain(Synty자산ModuleCodes.ConstructionState));
        }

        [Test]
        public void Farm_Town_Hub_City는_서로_독립된_폐루프_Module_골격을_가진다()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath);
            Assert.That(inventory, Is.Not.Null);

            Assert.That(업무영역플레이폐루프Synty표현Modules.All, Has.Count.EqualTo(6));
            foreach (var module in 업무영역플레이폐루프Synty표현Modules.All)
            {
                Assert.DoesNotThrow(module.Validate);
                foreach (var slot in module.Slots)
                {
                    Assert.That(slot.AssetModuleCodes, Is.Not.Empty, slot.SlotStableId);
                    foreach (var familyId in slot.AssetFamilyIds)
                    {
                        Assert.That(inventory!.Entries.Any(value => value.AssetFamilyId == familyId),
                            Is.True, familyId);
                    }
                }
            }
        }

        [Test]
        public void 신규_표현_Module은_Legacy_CompositionKey를_직접_읽지_않는다()
        {
            var worldFolder = Path.Combine(UnityEngine.Application.dataPath,
                "Ssalddel", "Presentation", "World");
            foreach (var fileName in new[]
            {
                "플레이폐루프Synty표현Module.cs",
                "Nature플레이폐루프Synty표현Modules.cs",
                "업무영역플레이폐루프Synty표현Modules.cs",
                "Synty전체자산ModuleCatalog.cs",
            })
            {
                var source = File.ReadAllText(Path.Combine(worldFolder, fileName));
                Assert.That(source, Does.Not.Contain("CompositionKey"), fileName);
            }
        }
    }
}
