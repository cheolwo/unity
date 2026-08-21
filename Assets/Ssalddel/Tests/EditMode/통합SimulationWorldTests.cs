using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 통합SimulationWorldTests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public void 저장Scene은_Farm플레이와Hub정보판을하나의진입점에가진다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == "SimulationWorldShell");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.transform.Find(
                    "WorldMapRoot/OfficialRegionProjectionRoot/"
                    + "SpatialPipeline_EPSG5186_TileAreaSet/"
                    + "L4_L7_Synty경관_PresentationOnly/"
                    + "CompletionArea_대관령면Farm_1km_L2_2x2"), Is.Not.Null);

                var player = root.GetComponentInChildren<플레이어경관Controller>(true);
                var inbound = root.GetComponentInChildren<진부Hub입고UiPresenter>(true);
                var mode = root.GetComponentInChildren<통합월드ModePresenter>(true);
                Assert.That(player, Is.Not.Null);
                Assert.That(inbound, Is.Not.Null);
                Assert.That(mode, Is.Not.Null);
                Assert.That(player.ValidateWiring(), Is.True);
                Assert.DoesNotThrow(() => inbound.ValidateWiring());
                Assert.DoesNotThrow(() => mode.ValidateWiring());

                var bar = root.transform.Find(
                    "PersistentUI/UnifiedWorldModeCanvas/UnifiedWorldModeBar");
                Assert.That(bar, Is.Not.Null);
                Assert.That(bar.GetComponentInParent<Canvas>().renderMode,
                    Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(bar.GetComponentsInChildren<Button>(false), Has.Length.EqualTo(3));
                Assert.That(bar.Find("JinbuInboundButton")!.gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 저장Scene은_인칭전환만남기고_진단상태Hud를숨긴다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == "SimulationWorldShell");
                Assert.That(root, Is.Not.Null);

                var hud = root.transform.Find("PersistentUI/SimulationWorldHud");
                Assert.That(hud!.gameObject.activeSelf, Is.False);
                Assert.That(hud.Find("WorldStatusPanel")!.gameObject.activeSelf, Is.False);
                Assert.That(hud.Find("NavigationPanel")!.gameObject.activeSelf, Is.False);
                Assert.That(hud.Find("ActualE5NetworkHud")!.gameObject.activeSelf, Is.False);

                var streamingCanvas = root.GetComponentsInChildren<Canvas>(true)
                    .Single(value => value.name == "DynamicTileStreamingStatusCanvas");
                Assert.That(streamingCanvas.gameObject.activeSelf, Is.False);

                var dailyPlans = root.GetComponentsInChildren<오늘작업계획Presenter>(true);
                Assert.That(dailyPlans, Is.Not.Empty);
                Assert.That(dailyPlans.All(value => !value.HudVisible), Is.True);
                var managementControllers = root
                    .GetComponentsInChildren<농장경영시점Controller>(true);
                Assert.That(managementControllers, Is.Not.Empty);
                Assert.That(managementControllers.All(value => !value.GuidanceVisible), Is.True);
                var players = root.GetComponentsInChildren<플레이어경관Controller>(true);
                Assert.That(players, Has.Length.EqualTo(1));
                Assert.That(players.All(value => !value.GuidanceVisible), Is.True);

                var bar = root.transform.Find(
                    "PersistentUI/UnifiedWorldModeCanvas/UnifiedWorldModeBar");
                Assert.That(bar, Is.Not.Null);
                var rect = bar!.GetComponent<RectTransform>();
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(bar.GetComponentsInChildren<Button>(false)
                    .Select(value => value.name), Is.EquivalentTo(new[]
                    {
                        "WorldOverviewButton", "FarmFirstPersonButton", "FarmTacticalButton",
                    }));

                var singlePlayerView = root.GetComponent<단일PlayerViewController>();
                Assert.That(singlePlayerView, Is.Not.Null);
                Assert.That(singlePlayerView!.ApplyOnPlay, Is.True);
                Assert.That(singlePlayerView.PresentationOnly, Is.True);
                Assert.That(singlePlayerView.Player, Is.SameAs(players[0]));
                Assert.That(singlePlayerView.ModeCanvas,
                    Is.SameAs(bar.GetComponentInParent<Canvas>()));
                Assert.DoesNotThrow(singlePlayerView.ValidateWiring);

                var mode = root.GetComponentInChildren<통합월드ModePresenter>(true);
                Assert.That(mode, Is.Not.Null);
                Assert.That(mode!.StartsInTacticalMode, Is.True);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 모드전환은_WorldTick과Revision을변경하지않는다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == "SimulationWorldShell");
                var shell = root.GetComponentInChildren<SimulationWorldShellPresenter>(true);
                var player = root.GetComponentInChildren<플레이어경관Controller>(true);
                var inbound = root.GetComponentInChildren<진부Hub입고UiPresenter>(true);
                var mode = root.GetComponentInChildren<통합월드ModePresenter>(true);
                shell.Initialize(SimulationWorldShellFixture.CreateSnapshot());
                var tick = shell.WorldTick;
                var revision = shell.WorldRevision;

                mode.ShowFarmFirstPerson();
                Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.FirstPerson));
                mode.ShowFarmTactical();
                Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.ThirdPerson));
                mode.ShowJinbuInbound();
                Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.Strategy));
                Assert.That(inbound.ContextVisible, Is.True);
                mode.ShowWorldOverview();

                Assert.That(mode.CurrentModeCode, Is.EqualTo(통합월드ModeCodes.WorldOverview));
                Assert.That(inbound.ContextVisible, Is.False);
                Assert.That(shell.WorldTick, Is.EqualTo(tick));
                Assert.That(shell.WorldRevision, Is.EqualTo(revision));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BuildSettings는_SimulationWorldShell하나만실행Scene으로사용한다()
        {
            var enabled = EditorBuildSettings.scenes.Where(value => value.enabled).ToArray();
            Assert.That(enabled, Has.Length.EqualTo(1));
            Assert.That(enabled[0].path, Is.EqualTo(ScenePath));
            Assert.That(EditorBuildSettings.scenes, Has.Length.EqualTo(1));
        }
    }
}
