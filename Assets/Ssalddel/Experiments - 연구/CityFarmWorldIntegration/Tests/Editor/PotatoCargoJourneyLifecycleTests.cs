using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoCargoJourneyLifecycleTests
    {
        private PotatoCargoJourneyLifecyclePresenter presenter=null!;
        [SetUp]public void OpenScene(){EditorSceneManager.OpenScene(PotatoCargoJourneyLifecycleBuilder.ScenePath,OpenSceneMode.Single);presenter=GameObject.Find("WorldBootstrap/"+PotatoCargoJourneyLifecycleBuilder.RootName).GetComponent<PotatoCargoJourneyLifecyclePresenter>();presenter.ResetJourney();}
        [Test]public void JOURNEY1_View는7개Action과Loaded경계를보존한다(){PotatoCargoJourneyLifecycleBuilder.ValidateOpenScene();var root=GameObject.Find("WorldBootstrap/"+PotatoCargoJourneyLifecycleBuilder.RootName);Assert.That(root.GetComponentsInChildren<PotatoCargoJourneyActionButton>(true),Has.Length.EqualTo(7));Assert.That(presenter.CurrentModel.StateCode,Is.EqualTo("Loaded"));Assert.That(presenter.CurrentModel.LimitationText,Does.Contain("실제 운송 시간이나 인수"));}
        [Test]public void DispatchPreview와Confirm은Tick전Van과Revision을바꾸지않는다(){var revision=presenter.CurrentSnapshot.DataRevision;var hub=Object.FindAnyObjectByType<PotatoJourneyHubRoutePresenter>();var position=hub.RouteFollower.transform.position;presenter.ReviewDispatch();presenter.ConfirmDispatch();Assert.That(presenter.CurrentSnapshot.DataRevision,Is.EqualTo(revision));Assert.That(presenter.CurrentModel.StateCode,Is.EqualTo("Loaded"));Assert.That(hub.RouteFollower.transform.position,Is.EqualTo(position));}
        [Test]public void RouteTick은Van을중간으로옮기고CargoIdentity를보존한다(){var cargo=presenter.CurrentSnapshot.Cargo.StableId;presenter.ReviewDispatch();presenter.ConfirmDispatch();presenter.ApplyConfirmedTick();presenter.AdvanceRoute(1);Assert.That(presenter.CurrentModel.StateCode,Is.EqualTo("InTransit"));Assert.That(presenter.CurrentModel.NormalizedProgress,Is.EqualTo(1f/3f).Within(.001f));Assert.That(presenter.CurrentSnapshot.Cargo.StableId,Is.EqualTo(cargo));}
        [Test]public void GoldenPath는같은300kgCargo를Hub에도착시킨다(){var cargo=presenter.CurrentSnapshot.Cargo.StableId;presenter.RunGoldenPath();Assert.That(presenter.CurrentModel.StateCode,Is.EqualTo("ArrivedAtHub"));Assert.That(presenter.CurrentSnapshot.Cargo.StableId,Is.EqualTo(cargo));Assert.That(presenter.CurrentSnapshot.Cargo.Quantity,Is.EqualTo(300m));var hub=Object.FindAnyObjectByType<PotatoJourneyHubRoutePresenter>();Assert.That(Vector3.Distance(hub.RouteFollower.transform.position,hub.RouteFollower.RouteEnd.position),Is.LessThan(.01f));}
    }
}
