using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 역할CharacterVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator 서버역할변경은_같은인물외형계열을유지하며_VisualRoot만교체한다()
        {
            yield return SceneManager.LoadSceneAsync(
                "SimulationWorldShell", LoadSceneMode.Single);
            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>();
            var switcher = Object.FindObjectsByType<역할CharacterVisualSwitcher>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.GetComponent<역할CharacterVisualInstanceView>()
                    .Assignment.ActorRoleCode == WorldActorRoleCodes.FarmerProducer);
            var view = switcher.GetComponent<역할CharacterVisualInstanceView>();
            var beforeFamily = view.AppearanceProfile.SelectedAppearanceFamilyCode;
            var beforeVisualRoot = view.VisualRoot;
            var beforeInstance = view.PrefabInstanceRoot;
            var beforeTick = shell.WorldTick;
            var beforeRevision = shell.WorldRevision;

            var result = switcher.ApplyServerRole(
                "화주", WorldActorWorkflowContextCodes.FreightDelivery);
            yield return null;

            Assert.That(result.ActorRoleCode, Is.EqualTo(WorldActorRoleCodes.Shipper));
            Assert.That(result.AppearanceFamilyCode, Is.EqualTo(beforeFamily));
            Assert.That(view.VisualRoot, Is.SameAs(beforeVisualRoot));
            Assert.That(view.PrefabInstanceRoot, Is.Not.SameAs(beforeInstance));
            Assert.That(view.PrefabInstanceRoot.transform.IsChildOf(beforeVisualRoot), Is.True);
            Assert.That(view.ValidateWiring(), Is.True);
            Assert.That(switcher.ValidateWiring(), Is.True);
            Assert.That(switcher.PresentationOnly, Is.True);
            Assert.That(shell.WorldTick, Is.EqualTo(beforeTick));
            Assert.That(shell.WorldRevision, Is.EqualTo(beforeRevision));
        }
    }
}
