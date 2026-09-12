using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 월드진단단축키Tests
    {
        private static IEnumerable<TestCaseData> ModifierCases()
        {
            for (var bits = 0; bits < 32; bits++)
                yield return new TestCaseData(bits).SetName("진단키_modifier_" + bits);
        }

        [TestCaseSource(nameof(ModifierCases))]
        public void 진단키는_보조키에따라_배타적으로분배한다(int bits)
        {
            var pressed = (bits & 1) != 0;
            var shift = (bits & 2) != 0;
            var control = (bits & 4) != 0;
            var alt = (bits & 8) != 0;
            var meta = (bits & 16) != 0;
            var nature = 월드진단단축키Policy.ShouldToggleNature(pressed, shift, control, alt, meta);
            var streaming = 월드진단단축키Policy.ShouldToggleStreaming(pressed, shift, control, alt, meta);
            Assert.That(nature, Is.EqualTo(bits == 1));
            Assert.That(streaming, Is.EqualTo(bits == 3));
            Assert.That(nature && streaming, Is.False);
        }

        [Test]
        public void 동일frame직접조회와자동조회는_한번만소비한다()
        {
            var consumed = -1;
            Assert.That(월드진단단축키Policy.TryConsume(true, 10, ref consumed), Is.True);
            Assert.That(월드진단단축키Policy.TryConsume(true, 10, ref consumed), Is.False);
            Assert.That(월드진단단축키Policy.TryConsume(false, 11, ref consumed), Is.False);
            Assert.That(consumed, Is.EqualTo(10));
            Assert.That(월드진단단축키Policy.TryConsume(true, 12, ref consumed), Is.True);
        }

        [Test]
        public void 길게누른중보조키만놓으면_새진단입력이되지않는다()
        {
            var consumed = -1;
            Assert.That(월드진단단축키Policy.TryConsume(
                월드진단단축키Policy.ShouldToggleNature(true, true, false, false, false), 1, ref consumed), Is.False);
            Assert.That(월드진단단축키Policy.TryConsume(
                월드진단단축키Policy.ShouldToggleNature(false, false, false, false, false), 2, ref consumed), Is.False);
            Assert.That(consumed, Is.EqualTo(-1));
            Assert.That(월드진단단축키Policy.TryConsume(
                월드진단단축키Policy.ShouldToggleNature(true, false, false, false, false), 3, ref consumed), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 기존저장라벨은_공간의존성없이도_즉시현재단축키로갱신한다(bool visible)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("OwnedDiagnosticFixture");
                SceneManager.MoveGameObjectToScene(root, scene);
                var panel = new GameObject("Panel");
                panel.transform.SetParent(root.transform);
                panel.SetActive(visible);
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(root.transform);
                var label = labelObject.GetComponent<Text>();
                label.text = "진단 트리 닫기  F8";
                var presenter = root.AddComponent<공간StreamingTreeDiagnosticPresenter>();
                presenter.Configure(null, null, null, panel, null, label);
                Assert.That(label.text, Is.EqualTo(visible ? "진단 트리 닫기  Shift+F8" : "진단 트리 열기  Shift+F8"));
                presenter.Toggle();
                Assert.That(panel.activeSelf, Is.EqualTo(!visible));
                Assert.That(label.text, Is.EqualTo(!visible ? "진단 트리 닫기  Shift+F8" : "진단 트리 열기  Shift+F8"));
                presenter.RefreshNow();
                Assert.That(presenter.PresentationOnly, Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void 기존촬영메뉴는남고_자동F8등록만제거된다()
        {
            // 메뉴 본문은 Camera/Canvas/파일 효과가 있어 실행하지 않는다.
            var source = Read("Editor/대한민국법정동WorldBuilder.cs");
            StringAssert.Contains("[MenuItem(\"Ssalddel/검증/화면 증거/대관령 Farm 1인칭\")]", source);
            StringAssert.Contains("public static void CaptureFarmFirstPersonPlayGameView()", source);
            StringAssert.DoesNotContain("1인칭 _F8", source);
            StringAssert.Contains("_F7", source);
            StringAssert.Contains("_F9", source);
        }

        [Test]
        public void 두소비자는_nativeedge와양쪽보조키를조회하고_초기문구도일치한다()
        {
            foreach (var file in new[] { "Nature생존Controller.cs", "공간StreamingTreeDiagnosticPresenter.cs" })
            {
                var source = Read("Presentation/World/" + file);
                StringAssert.Contains("f8Key.wasPressedThisFrame", source);
                StringAssert.DoesNotContain("f8Key.isPressed", source);
                foreach (var key in new[] { "Shift", "Ctrl", "Alt", "Meta" })
                {
                    StringAssert.Contains("left" + key + "Key.isPressed", source);
                    StringAssert.Contains("right" + key + "Key.isPressed", source);
                }
                StringAssert.Contains("ref diagnosticConsumedFrame", source);
            }
            StringAssert.Contains("진단 트리 닫기  Shift+F8", Read("Editor/공간TileStreamingBuilder.cs"));
        }

        private static string Read(string path)
            => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Ssalddel", path));
    }
}
