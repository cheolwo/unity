using System;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 정착지상호작용AuthorityBoundaryTests
    {
        [Test]
        public void 권위가없으면_선택할수없고_자동Fixture를_활성화할수없다()
        {
            var root = new GameObject("SettlementInteractionAuthorityBoundary");
            try
            {
                var presenter = root.AddComponent<정착지상호작용Presenter>();

                Assert.That(presenter.권위연결됨, Is.False);
                Assert.That(presenter.CanSelectFarmChoice("ReserveStorage"), Is.False);
                var error = Assert.Throws<InvalidOperationException>(() =>
                    presenter.Set자동Fixture시작(true));
                Assert.That(error!.Message, Is.EqualTo(
                    "SettlementInteractionImplicitFixtureAuthorityDisabled"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 공식CompositionRoot의_기본권위는_Server다()
        {
            var root = new GameObject("SettlementInteractionCompositionBoundary");
            try
            {
                var composition = root.AddComponent<
                    정착지상호작용SceneCompositionRoot>();

                Assert.That(composition.AuthorityMode,
                    Is.EqualTo(정착지상호작용AuthorityMode.Server));
                Assert.That(composition.서버기준사용중, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 턴마감도_명시적권위없이_자동Fixture를_시작하지않는다()
        {
            var root = new GameObject("TurnClosingAuthorityBoundary");
            try
            {
                var presenter = root.AddComponent<턴마감Presenter>();

                Assert.That(presenter.권위연결됨, Is.False);
                var error = Assert.Throws<InvalidOperationException>(() =>
                    presenter.Set자동시작(true));
                Assert.That(error!.Message, Is.EqualTo(
                    "TurnClosingImplicitFixtureAuthorityDisabled"));

                presenter.SetAuthorityForTests(new 턴마감FixtureAuthorityClient());
                presenter.Set자동시작(true);
                Assert.That(presenter.권위연결됨, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
