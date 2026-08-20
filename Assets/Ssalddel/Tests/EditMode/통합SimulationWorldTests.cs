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
                Assert.That(bar.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(4));
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
