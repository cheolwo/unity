using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class Nature감각표현PlayModeTests
    {
        [UnityTest]
        public IEnumerator 권위상태사본이_도끼벌목AnimationAudioFx로만투영된다()
        {
            var saveRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-sensory-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable("SSALDDEL_LOCAL_SAVE_ROOT", saveRoot);
            try
            {
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                Nature감각표현AutoBootstrap.InstallForCurrentScene();
                yield return WaitForPresenter();

                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var presenter = controller.GetComponent<
                    Nature감각표현Presenter>();
                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter!.IsConfigured, Is.True);
                Assert.That(presenter.PresentationOnly, Is.True);
                Assert.That(presenter.AxePlacementNormalized, Is.True);
                Assert.That(presenter.ProceduralActionCueCount, Is.EqualTo(4),
                    "도끼 획득·타격·취소·나무 낙하 절차형 대체 효과음이 필요하다.");
                Assert.That(presenter.Receipt.QualificationStatusCode,
                    Is.EqualTo("WiringAccepted"));
                Assert.That(presenter.Receipt.RuntimeObserved, Is.False,
                    "Adapter 배선만으로 실제 E7 관찰 증거를 주장하면 안 된다.");
                Assert.That(presenter.Receipt.AdvisoryCodes,
                    Does.Contain("OptionalAreaMusicPending"));
                Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None),
                    Is.Not.Empty, "공간 효과음을 들을 Player Audio Listener가 필요하다.");

                controller.AcquireAxeForTest();
                controller.EquipAxeForTest();
                var acquiredRevision = controller.State.Revision;
                presenter.RefreshForTest();
                Assert.That(presenter.CurrentPhaseCode,
                    Is.EqualTo(감각표현단계Codes.Acquired));
                Assert.That(presenter.LastAudioCueCode,
                    Is.EqualTo(감각표현CueCodes.AxePickup));
                Assert.That(controller.State.Revision, Is.EqualTo(acquiredRevision),
                    "표현 갱신은 권위 개정을 바꾸면 안 된다.");

                controller.BeginHarvestForTest("resource:nature-tree:01");
                presenter.RefreshForTest();
                controller.HoldWorkForTest(1);
                var impactRevision = controller.State.Revision;
                presenter.RefreshForTest();
                Assert.That(presenter.CurrentAnimationIntentCode,
                    Is.EqualTo("animation:nature:axe-swing"));
                Assert.That(presenter.LastAudioCueCode,
                    Is.EqualTo(감각표현CueCodes.AxeImpact));
                Assert.That(presenter.LastFxCueCode,
                    Is.EqualTo(감각표현CueCodes.WoodChip));
                Assert.That(controller.State.Revision, Is.EqualTo(impactRevision));

                controller.CancelActiveWorkForTest();
                presenter.RefreshForTest();
                Assert.That(presenter.CurrentPhaseCode,
                    Is.EqualTo(감각표현단계Codes.Cancelled));
                Assert.That(controller.State.ResourceNodes[0].StateCode,
                    Is.EqualTo(Ssalddel.Unity.Runtime.World.Nature생존Codes.Standing));

                controller.BeginHarvestForTest("resource:nature-tree:01");
                presenter.RefreshForTest();
                controller.HoldWorkForTest(4);
                presenter.RefreshForTest();
                Assert.That(presenter.CurrentPhaseCode,
                    Is.EqualTo(감각표현단계Codes.Completed));
                Assert.That(presenter.LastAudioCueCode,
                    Is.EqualTo(감각표현CueCodes.TreeFall));
                Assert.That(presenter.LastFxCueCode,
                    Is.EqualTo(감각표현CueCodes.TreeFallDust));
                Assert.That(controller.State.ResourceNodes[0].StateCode,
                    Is.EqualTo(Ssalddel.Unity.Runtime.World.Nature생존Codes.Stump));
            }
            finally
            {
                foreach (var controller in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    controller.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                Environment.SetEnvironmentVariable("SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable("SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
            }
        }

        private static IEnumerator WaitForPresenter()
        {
            var timeout = 10f;
            Nature감각표현Presenter? presenter = null;
            while (timeout > 0f)
            {
                presenter = UnityEngine.Object.FindAnyObjectByType<
                    Nature감각표현Presenter>(FindObjectsInactive.Include);
                if (presenter != null && presenter.IsConfigured) break;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter!.IsConfigured, Is.True);
        }
    }
}
