using System;
using Ssalddel.Contracts.Common.Metadata;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E4, "전체 동네의 관찰 영역 맞춤", Boundary = "배치 Bounds와 실제 화면비만 읽으며 서버 좌표와 상태는 변경하지 않는다")]
    public static class 동네전체관찰CameraMath
    {
        public static Quaternion 기본회전 => Quaternion.LookRotation(new Vector3(1, -1, 1), Vector3.up);
        public static Vector3 근접관찰오프셋 => -(기본회전 * Vector3.forward) * 52f;
        public static Vector3[] 모서리(Bounds bounds)
        {
            var result = new Vector3[8]; var index = 0;
            foreach (var x in new[] { bounds.min.x, bounds.max.x })
                foreach (var y in new[] { bounds.min.y, bounds.max.y })
                    foreach (var z in new[] { bounds.min.z, bounds.max.z }) result[index++] = new Vector3(x, y, z);
            return result;
        }
        public static float 맞춤크기(Bounds bounds, Quaternion cameraRotation, float viewportAspect, float margin = .08f)
        {
            if (!float.IsFinite(viewportAspect) || viewportAspect <= 0 || !float.IsFinite(margin) || margin < 0
                || !유한(bounds.center) || !유한(bounds.size) || bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportAspect), "NeighborhoodCameraBoundsOrAspectInvalid");
            var inverse = Quaternion.Inverse(cameraRotation); var horizontal = 0f; var vertical = 0f;
            foreach (var corner in 모서리(bounds))
            {
                var projected = inverse * (corner - bounds.center);
                horizontal = Mathf.Max(horizontal, Mathf.Abs(projected.x));
                vertical = Mathf.Max(vertical, Mathf.Abs(projected.y));
            }
            return Mathf.Max(vertical, horizontal / viewportAspect) * (1 + margin);
        }
        private static bool 유한(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
