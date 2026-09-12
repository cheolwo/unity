using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 플레이어전술조작Tests
    {
        [Test]
        public void 여덟Pixel미만은클릭이고_이상이면박스선택이다()
        {
            Assert.That(플레이어전술조작Math.IsDrag(
                Vector2.zero, new Vector2(7f, 0f), 8f), Is.False);
            Assert.That(플레이어전술조작Math.IsDrag(
                Vector2.zero, new Vector2(8f, 0f), 8f), Is.True);
        }

        [Test]
        public void 어느방향으로드래그해도정규화된화면사각형을만든다()
        {
            var value = 플레이어전술조작Math.ScreenRect(
                new Vector2(80f, 60f), new Vector2(20f, 10f));
            Assert.That(value.xMin, Is.EqualTo(20f));
            Assert.That(value.yMin, Is.EqualTo(10f));
            Assert.That(value.xMax, Is.EqualTo(80f));
            Assert.That(value.yMax, Is.EqualTo(60f));
        }

        [Test]
        public void Backspace는_사백오십Millisecond안의두번째입력만재집중한다()
        {
            Assert.That(플레이어전술조작Math.IsDoubleTap(
                10f, 10.45f, .45f), Is.True);
            Assert.That(플레이어전술조작Math.IsDoubleTap(
                10f, 10.451f, .45f), Is.False);
            Assert.That(플레이어전술조작Math.IsDoubleTap(
                -1f, 10f, .45f), Is.False);
        }
    }
}
