using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "배치 객체의 Renderer Bounds 기반 표면 정렬 규칙을 검증한다.",
        Boundary = "자동 Bounds 검증은 실제 지형 경사·Play Mode·Game View 증거를 대신하지 않는다.")]
    public sealed class 배치객체표면정렬Tests
    {
        [Test]
        public void 피벗이_치우친_시각자산도_가시하단을_승인표면에_맞춘다()
        {
            var root = new GameObject("OffsetPivotRoot");
            try
            {
                root.transform.position = new Vector3(2f, 6f, -3f);
                var visible = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visible.transform.SetParent(root.transform, false);
                visible.transform.localPosition = new Vector3(0f, -1.4f, 0f);
                visible.transform.localScale = new Vector3(2f, 1.2f, 2f);

                배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(
                    root.transform, 1.75f);

                var result = 배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(root.transform, 1.75f);
                Assert.That(result.Passed, Is.True, result.ResultCode);
                Assert.That(result.VisibleBottomWorldY,
                    Is.EqualTo(1.78f).Within(.001f));
                Assert.That(result.Error, Is.EqualTo(0f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 크기와회전이_바뀐뒤_다시정렬하면_표면관문을_통과한다()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.name = "ScaledPlacementObject";
                root.transform.position = new Vector3(-4f, 3f, 7f);
                root.transform.localScale = new Vector3(2f, .25f, 1.5f);
                root.transform.rotation = Quaternion.Euler(0f, 32f, 14f);

                배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(
                    root.transform, .4f);
                root.transform.localScale = new Vector3(1.2f, 1.8f, .8f);
                root.transform.rotation = Quaternion.Euler(0f, 75f, -9f);
                배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(
                    root.transform, .4f);

                var result = 배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(root.transform, .4f);
                Assert.That(result.Passed, Is.True, result.ResultCode);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 가시하단이_표면아래면_차단원인을_반환한다()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.transform.position = new Vector3(0f, -.75f, 0f);

                var result = 배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(root.transform, 0f);

                Assert.That(result.Passed, Is.False);
                Assert.That(result.ResultCode,
                    Is.EqualTo("VisibleBottomBelowSurface"));
                Assert.That(result.Error, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
