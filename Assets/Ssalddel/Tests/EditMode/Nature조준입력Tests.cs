using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Nature조준입력Tests
    {
        private static IEnumerable<TestCaseData> Cases()
        {
            for (var bits = 0; bits < 16; bits++)
                yield return new TestCaseData(bits).SetName("Nature조준_분류_" + bits);
        }

        [TestCaseSource(nameof(Cases))]
        public void 모달전이와배치를제외한_잠긴일인칭만_중앙조준한다(int bits)
        {
            Assert.That(Nature생존Controller.UsesLockedNatureAim(
                (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0),
                Is.EqualTo(bits == 3));
        }

        [Test]
        public void 자유포인터와삼인칭의_실제하단영역의미는유지한다()
        {
            Assert.That(플레이어경관Controller.IsPointerInPersistentModeBand(Vector2.zero, 470f), Is.True);
            Assert.That(Nature생존Controller.UsesLockedNatureAim(true, false, false, false), Is.False);
            Assert.That(Nature생존Controller.UsesLockedNatureAim(false, true, false, false), Is.False);
            // Player가 먼저 잠근 경우와 Nature가 먼저 자유포인터를 검사한 경우를 분리한다.
            Assert.That(Nature생존Controller.UsesLockedNatureAim(true, true, false, false), Is.True);
            Assert.That(Nature생존Controller.UsesLockedNatureAim(true, true, true, false), Is.False);
        }

        [Test]
        public void UI전이보호는_잠금우회보다먼저이고_두포인터검사를함께분기한다()
        {
            // 실행 순서의 구조 회귀이며 OS 입력이나 Raycast 성공을 대신하지 않는다.
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "Ssalddel/Presentation/World/Nature생존Controller.cs"));
            var start = source.IndexOf("if (!primaryPressed)", System.StringComparison.Ordinal);
            var end = source.IndexOf("HandlePrimaryPressed();", start, System.StringComparison.Ordinal);
            var body = source.Substring(start, end - start);
            Assert.That(body.IndexOf("if (BlocksWorldPointerForEquipment) return;", System.StringComparison.Ordinal),
                Is.LessThan(body.IndexOf("if (!UsesLockedNatureAim", System.StringComparison.Ordinal)));
            StringAssert.Contains("cabinPlacementMode || workbenchPlacementMode", body);
            StringAssert.Contains("if (TryHandleHudPrimaryPressed()) return;", body);
            StringAssert.Contains("if (PointerOverHud()) return;", body);
        }

        [Test]
        public void 짧은클릭edge는_같은프레임한번만소비한다()
        {
            var consumed = -1;
            // r82의 down/up 동시 갱신처럼 현재 held=false여도 native down edge는 true다.
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(true, 7343, ref consumed), Is.True);
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(true, 7343, ref consumed), Is.False);
            Assert.That(consumed, Is.EqualTo(7343));
        }

        [Test]
        public void release와유지에는새클릭을만들지않고_다음edge는소비한다()
        {
            var consumed = -1;
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(true, 10, ref consumed), Is.True);
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(false, 11, ref consumed), Is.False);
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(false, 12, ref consumed), Is.False);
            Assert.That(consumed, Is.EqualTo(10));
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(true, 13, ref consumed), Is.True);
        }

        [Test]
        public void 눌림없는조회는_같은프레임의나중edge를미리소비하지않는다()
        {
            var consumed = -1;
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(false, 20, ref consumed), Is.False);
            Assert.That(consumed, Is.EqualTo(-1));
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(true, 20, ref consumed), Is.True);
        }

        [Test]
        public void 음수프레임은_소비기준선을바꾸지않는다()
        {
            var consumed = 25;
            Assert.That(Nature생존Controller.TryConsumePrimaryPress(true, -1, ref consumed), Is.False);
            Assert.That(consumed, Is.EqualTo(25));
        }

        [Test]
        public void native클릭연결은_권위작업유지와시험중복보호를분리한다()
        {
            // 실제 OS 전달 대신 소스 연결과 기존 유지 의미를 검사한다.
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "Ssalddel/Presentation/World/Nature생존Controller.cs"));
            StringAssert.Contains("inputMouse?.leftButton.wasPressedThisFrame == true", source);
            StringAssert.Contains("Time.frameCount, ref primaryConsumedFrame", source);
            StringAssert.Contains("var mouseHeld = physicalMouseHeld", source);
            StringAssert.Contains("primaryWasHeld = physicalMouseHeld;", source);
            StringAssert.Contains("engine.AdvanceRealtime(1, mouseHeld && !higherPriority, paused);", source);
            StringAssert.DoesNotContain("physicalMouseHeld && !primaryWasHeld", source);
            var start = source.IndexOf("public void ProcessCurrentInputForTest()", System.StringComparison.Ordinal);
            var end = source.IndexOf("private bool ValidateWiring", start, System.StringComparison.Ordinal);
            if (end < 0) end = source.IndexOf("public bool ValidateWiring", start, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            StringAssert.DoesNotContain("primaryConsumedFrame =", source.Substring(start, end - start));
        }
    }
}
