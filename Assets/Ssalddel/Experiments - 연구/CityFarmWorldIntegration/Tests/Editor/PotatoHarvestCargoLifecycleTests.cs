using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoHarvestCargoLifecycleTests
    {
        private PotatoHarvestCargoLifecyclePresenter presenter = null!;
        [SetUp] public void OpenScene(){EditorSceneManager.OpenScene(PotatoHarvestCargoLifecycleBuilder.ScenePath,OpenSceneMode.Single);presenter=GameObject.Find("WorldBootstrap/"+PotatoHarvestCargoLifecycleBuilder.RootName).GetComponent<PotatoHarvestCargoLifecyclePresenter>();presenter.ResetLifecycle();}

        [Test] public void CARGO1_View는7개명시적Action과Simulation경계를보존한다()
        { PotatoHarvestCargoLifecycleBuilder.ValidateOpenScene();var root=GameObject.Find("WorldBootstrap/"+PotatoHarvestCargoLifecycleBuilder.RootName);Assert.That(presenter.ValidateWiring(),Is.True);Assert.That(root.GetComponentsInChildren<PotatoHarvestCargoActionButton>(true),Has.Length.EqualTo(7));Assert.That(presenter.CurrentModel.SourceModeCode,Is.EqualTo("Simulation/Fixture"));Assert.That(presenter.CurrentModel.LimitationText,Does.Contain("운영 포장·운송 기준이 아닙니다")); }

        [Test] public void 포장Preview와Confirm은Tick전수량과Revision을바꾸지않는다()
        {var revision=presenter.CurrentSnapshot.DataRevision;presenter.ReviewPacking();presenter.ConfirmPreview();Assert.That(presenter.CurrentSnapshot.DataRevision,Is.EqualTo(revision));Assert.That(presenter.CurrentSnapshot.PackageLot,Is.Null);Assert.That(presenter.CurrentCommand,Is.Not.Null);}

        [Test] public void 포장과상차는15개상자300kgCargo를만든다()
        {presenter.FinishLoading();Assert.That(presenter.CurrentSnapshot.PackageLot!.PackageCount,Is.EqualTo(15));Assert.That(presenter.CurrentSnapshot.Cargo!.Quantity,Is.EqualTo(300m));Assert.That(presenter.CurrentModel.StateCode,Is.EqualTo("Loaded"));Assert.That(presenter.CurrentModel.LineageText,Does.Contain(presenter.CurrentSnapshot.HarvestLot.StableId));}

        [Test] public void GoldenPath는Package와CargoMarker를표시한다()
        {presenter.RunGoldenPath();var root=GameObject.Find("WorldBootstrap/"+PotatoHarvestCargoLifecycleBuilder.RootName);Assert.That(root.transform.Find("PackageLotMarker_15Boxes").gameObject.activeSelf,Is.True);Assert.That(root.transform.Find("CargoMarker_300kg").gameObject.activeSelf,Is.True);}
    }
}
