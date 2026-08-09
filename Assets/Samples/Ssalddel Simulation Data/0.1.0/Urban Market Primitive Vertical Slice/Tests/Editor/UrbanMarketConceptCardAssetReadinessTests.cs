using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Samples.UrbanMarket.Editor;
using Unity.AI.Navigation;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Ssalddel.Unity.Samples.UrbanMarket.Tests.Editor
{
    public sealed class UrbanMarketConceptCardAssetReadinessTests
    {
        [SetUp]
        public void SetUp()
            => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void TearDown() => NavMesh.RemoveAllNavMeshData();

        [Test]
        public void Primitive구성은_대표Npc와7장Card및NavMesh를배선한다()
        {
            도심마트ManagerPrimitiveSceneBuilder.CreateAssetReadinessObjectsForTests();

            var representative = Object.FindFirstObjectByType<공동주택대표NpcView>();
            var deck = Object.FindFirstObjectByType<ConceptCardDeckView>();
            var controller = Object.FindFirstObjectByType<도심마트ConceptCardSceneController>();
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();

            Assert.That(representative, Is.Not.Null);
            Assert.That(representative!.ValidateWiring(), Is.True);
            Assert.That(representative.VisualRoot, Is.EqualTo(representative.gameObject));
            Assert.That(deck, Is.Not.Null);
            Assert.That(deck!.ValidateWiring(), Is.True);
            Assert.That(deck.SlotCount, Is.EqualTo(7));
            Assert.That(deck.DisplayedCardCount, Is.EqualTo(7));
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller!.ValidateWiring(), Is.True);
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface!.navMeshData, Is.Not.Null);
        }

        [Test]
        public void Card선택은_업무값을재계산하지않고선택표시만바꾼다()
        {
            도심마트ManagerPrimitiveSceneBuilder.CreateAssetReadinessObjectsForTests();
            var deck = Object.FindFirstObjectByType<ConceptCardDeckView>();
            const string target = "concept-card:intent-demand:orderer-group:residential:potato:1";

            Assert.That(deck!.SelectCard(target), Is.True);
            Assert.That(deck.SelectedCardStableId, Is.EqualTo(target));
            Assert.That(deck.DisplayedCardCount, Is.EqualTo(7));
        }

        [Test]
        public void PrimitiveAnimator는_Synty교체전에필요한MecanimParameter를제공한다()
        {
            도심마트ManagerPrimitiveSceneBuilder.CreateAssetReadinessObjectsForTests();
            var animator = Object.FindFirstObjectByType<공동주택대표NpcView>()!
                .GetComponent<Animator>();
            var controller = animator.runtimeAnimatorController as AnimatorController;

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller!.parameters.Select(value => value.name),
                Does.Contain("Speed").And.Contain("WaitForManagerReview"));
        }
    }
}
