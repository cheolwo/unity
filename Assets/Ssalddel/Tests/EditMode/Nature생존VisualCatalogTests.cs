using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Nature생존VisualCatalogTests
    {
        private const string CatalogPath =
            "Assets/Ssalddel/Resources/Nature생존VisualCatalog.asset";

        [Test]
        public void Nature시각자산묶음은_오두막통나무위협을_Synty로고정한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<
                Nature생존VisualCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null,
                "Nature생존VisualCatalogBuilder로 자산 묶음을 먼저 생성해야 한다.");
            Assert.That(catalog!.IsComplete(), Is.True);
            Assert.That(catalog.TimberLogPrefab.name,
                Is.EqualTo("SM_Tree_Log_02"));
            Assert.That(catalog.CabinPrefab.name,
                Is.EqualTo("SM_Bld_GardenShed_01"));
            Assert.That(catalog.WorkbenchPrefab.name,
                Is.EqualTo("SM_Prop_Table_Saw_01"));
            Assert.That(catalog.SkeletonPrefab.name,
                Is.EqualTo("SM_Gen_Chr_Skeleton_01"));
            Assert.That(catalog.StoredTimberPrefab.name,
                Is.EqualTo("SM_Prop_Wood_Stack_01"));
            Assert.That(catalog.SleepBedPrefab.name,
                Is.EqualTo("SM_Prop_Bed_01"));
            Assert.That(catalog.SleepCampfirePrefab.name,
                Is.EqualTo("SM_Prop_CampFire_01"));
            Assert.That(catalog.ExpansionPlansPrefab.name,
                Is.EqualTo("SM_Prop_Plans_01"));
        }
    }
}
