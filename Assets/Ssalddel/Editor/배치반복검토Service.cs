using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    [Serializable]
    public sealed class 배치검토설정
    {
        public float 지면여유 = .03f;
        public float 하단허용오차 = .015f;
        public float 최대경사 = 5f;
        public float 최대높이편차 = .15f;
        public float 최소이격 = .55f;
        public float 최대높이보정 = 2f;
        public float 플레이어높이 = 1.8f;
    }

    public sealed class 배치검토결과
    {
        public readonly List<string> 문제 = new();
        public readonly List<string> 안내 = new();
        public readonly List<Vector3> 지지표본 = new();
        public Bounds 가시범위;
        public float 최고지면;
        public float 높이편차;
        public float 경사;
        public float 높이보정량;
        public float 플레이어대비높이;
        public bool 높이보정가능;
        public bool 표본검사통과 => 문제.Count == 0;
    }

    /// <summary>
    /// 배치 엔진 출력의 공통 사전 검토. 명시한 지지면·주변·보호 구역만 읽는다.
    /// 9개 표본과 AABB는 메시 교차·NavMesh·시각 E7 증거가 아니다.
    /// </summary>
    public static class 배치반복검토Service
    {
        public static 배치검토결과 검사(Transform 대상, Collider 지지면,
            배치검토설정 설정, IReadOnlyList<Transform> 주변 = null,
            IReadOnlyList<Collider> 통행보호 = null)
        {
            var 결과 = new 배치검토결과();
            if (설정 == null || !설정유효(설정)) return 차단(결과, "검토 설정 오류");
            if (대상 == null || EditorUtility.IsPersistent(대상)
                || PrefabStageUtility.GetPrefabStage(대상.gameObject) != null
                || !대상.gameObject.scene.IsValid() || !대상.gameObject.activeInHierarchy)
                return 차단(결과, "활성 Scene 배치 객체 필요");
            if (지지면 == null || !지지면.enabled || 지지면.isTrigger
                || !지지면.gameObject.activeInHierarchy
                || 지지면.gameObject.scene != 대상.gameObject.scene
                || 관련계층(대상, 지지면.transform))
                return 차단(결과, "독립된 같은 Scene 지지 Collider 필요");
            if (!배치객체표면정렬Utility.TryGetVisibleBounds(대상, out var 범위)
                || !범위유효(범위)) return 차단(결과, "유효한 활성 Renderer 범위 필요");

            결과.가시범위 = 범위;
            결과.플레이어대비높이 = 범위.size.y / 설정.플레이어높이;
            if (!대상.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger))
                결과.안내.Add("대상 Collider 없음: 이동·상호작용 가능성 미검증");
            if (주변 == null || 주변.Count == 0) 결과.안내.Add("주변 객체 미지정: 이격 검증 범위 없음");
            if (통행보호 == null || 통행보호.Count == 0) 결과.안내.Add("통행 보호 구역 미지정: 출입 동선 미검증");

            Physics.SyncTransforms();
            var 지면범위 = 지지면.bounds;
            var 시작높이 = Mathf.Max(범위.max.y, 지면범위.max.y) + 1f;
            var 거리 = 시작높이 - Mathf.Min(범위.min.y, 지면범위.min.y) + 1f;
            var 최저 = float.PositiveInfinity;
            var 최고 = float.NegativeInfinity;
            for (var x = 0; x < 3; x++)
            for (var z = 0; z < 3; z++)
            {
                var 시작 = new Vector3(Mathf.Lerp(범위.min.x, 범위.max.x, x * .5f),
                    시작높이, Mathf.Lerp(범위.min.z, 범위.max.z, z * .5f));
                if (!지지면.Raycast(new Ray(시작, Vector3.down), out var hit, 거리)) continue;
                결과.지지표본.Add(hit.point);
                최저 = Mathf.Min(최저, hit.point.y);
                최고 = Mathf.Max(최고, hit.point.y);
                결과.경사 = Mathf.Max(결과.경사, Vector3.Angle(hit.normal, Vector3.up));
            }
            if (결과.지지표본.Count != 9) 결과.문제.Add("지지 표본 누락: " + 결과.지지표본.Count + "/9");
            if (결과.지지표본.Count > 0)
            {
                결과.최고지면 = 최고;
                결과.높이편차 = 최고 - 최저;
                결과.높이보정량 = 최고 + 설정.지면여유 - 범위.min.y;
                if (결과.경사 > 설정.최대경사) 결과.문제.Add("경사 한도 초과: 위치 재선택 필요");
                if (결과.높이편차 > 설정.최대높이편차) 결과.문제.Add("지지 높이 편차 초과: 기초 또는 위치 재검토 필요");
            }

            foreach (var 이웃 in 주변 ?? Array.Empty<Transform>())
            {
                if (이웃 == null || 관련계층(대상, 이웃)
                    || 이웃.gameObject.scene != 대상.gameObject.scene
                    || !배치객체표면정렬Utility.TryGetVisibleBounds(이웃, out var 이웃범위)
                    || !범위유효(이웃범위))
                { 결과.문제.Add("주변 객체 입력 오류"); continue; }
                var dx = Mathf.Max(0f, Mathf.Max(범위.min.x - 이웃범위.max.x, 이웃범위.min.x - 범위.max.x));
                var dz = Mathf.Max(0f, Mathf.Max(범위.min.z - 이웃범위.max.z, 이웃범위.min.z - 범위.max.z));
                // 바닥 점유 영역은 높이가 달라도 보수적으로 충돌 후보로 남긴다.
                if ((dx == 0f && dz == 0f) || Mathf.Sqrt(dx * dx + dz * dz) < 설정.최소이격)
                    결과.문제.Add("주변 간격 부족: " + 이웃.name);
            }
            foreach (var 구역 in 통행보호 ?? Array.Empty<Collider>())
            {
                if (구역 == null || !구역.enabled || !구역.gameObject.activeInHierarchy
                    || 구역.gameObject.scene != 대상.gameObject.scene || 관련계층(대상, 구역.transform))
                { 결과.문제.Add("통행 보호 구역 입력 오류"); continue; }
                var 보호 = 구역.bounds;
                if (범위.min.x <= 보호.max.x && 범위.max.x >= 보호.min.x
                    && 범위.min.z <= 보호.max.z && 범위.max.z >= 보호.min.z)
                    결과.문제.Add("통행 보호 구역 침범: " + 구역.name);
            }

            결과.높이보정가능 = 결과.문제.Count == 0
                && Mathf.Abs(결과.높이보정량) <= 설정.최대높이보정;
            if (결과.지지표본.Count == 9)
            {
                var 하단 = 배치객체표면정렬Utility.ValidateVisibleBottomClearance(
                    대상, 최고, 설정.지면여유, 설정.하단허용오차);
                if (!하단.Passed) 결과.문제.Add(하단.Error < 0f ? "가시 하단 매몰" : "가시 하단 부유");
                if (Mathf.Abs(결과.높이보정량) > 설정.최대높이보정) 결과.문제.Add("높이 보정 한도 초과");
            }
            return 결과;
        }

        public static 배치검토결과 높이보정(Transform 대상, Collider 지지면,
            배치검토설정 설정, IReadOnlyList<Transform> 주변 = null,
            IReadOnlyList<Collider> 통행보호 = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Mode에서는 배치를 보정하지 않습니다.");
            // 과거 검사 결과를 믿지 않고 지지면/크기/주변 변경까지 다시 검사한다.
            var 이전 = 검사(대상, 지지면, 설정, 주변, 통행보호);
            if (!이전.높이보정가능) throw new InvalidOperationException(string.Join(" / ", 이전.문제));
            if (Mathf.Abs(이전.높이보정량) <= 설정.하단허용오차) return 이전;
            Undo.RecordObject(대상, "배치 객체 지면 높이 보정");
            배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(대상, 이전.최고지면, 설정.지면여유);
            if (PrefabUtility.IsPartOfPrefabInstance(대상))
                PrefabUtility.RecordPrefabInstancePropertyModifications(대상);
            return 검사(대상, 지지면, 설정, 주변, 통행보호);
        }

        private static 배치검토결과 차단(배치검토결과 결과, string 사유)
        { 결과.문제.Add(사유); return 결과; }
        private static bool 관련계층(Transform a, Transform b) => a == b || a.IsChildOf(b) || b.IsChildOf(a);
        private static bool 유한(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool 범위유효(Bounds b) => 유한(b.center.x) && 유한(b.center.y)
            && 유한(b.center.z) && 유한(b.size.x) && 유한(b.size.y) && 유한(b.size.z)
            && b.size.x > 0f && b.size.z > 0f && b.size.y >= 0f;
        private static bool 설정유효(배치검토설정 s) =>
            new[] { s.지면여유, s.하단허용오차, s.최대경사, s.최대높이편차, s.최소이격, s.최대높이보정, s.플레이어높이 }
                .All(유한) && s.지면여유 >= 0 && s.하단허용오차 >= 0 && s.최대경사 >= 0
            && s.최대경사 < 90 && s.최대높이편차 >= 0 && s.최소이격 >= 0
            && s.최대높이보정 >= 0 && s.플레이어높이 > 0;
    }
}
