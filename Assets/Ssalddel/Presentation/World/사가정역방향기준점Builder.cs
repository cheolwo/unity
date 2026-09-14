using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 검증된 사가정역 출구 좌표를 일반화된 디오라마 표식으로 조립한다.
    /// 구조물 방향은 역 기준점을 향한 읽기 보조값이며 실제 출입 방향이나 통행 경로가 아니다.
    /// </summary>
    public static class 사가정역방향기준점Builder
    {
        public const string VisualRevision = "sagajeong-station-exit-visual.r1";

        public sealed class BuildReport
        {
            internal BuildReport(Transform root,
                IReadOnlyList<역세권방향기준점> anchors, int visualPartCount)
            {
                Root = root;
                Anchors = anchors;
                VisualPartCount = visualPartCount;
            }

            public Transform Root { get; }
            public IReadOnlyList<역세권방향기준점> Anchors { get; }
            public int AnchorCount => Anchors.Count;
            public int VisualPartCount { get; }
        }

        public static BuildReport Build(사가정역방향기준점조회 source,
            공간참고자료 map, Transform parent,
            Action<Transform, string, Vector3, Vector3, Color> block)
        {
            if (source == null || map == null || parent == null || block == null)
                throw new ArgumentException("SagajeongStationExitBuildInputMissing");

            var root = new GameObject("사가정역 출구 방향 기준점 · 표현 전용").transform;
            root.SetParent(parent, false);
            var visualPartCount = 0;
            foreach (var anchor in source.Anchors)
            {
                var group = new GameObject($"사가정역 {anchor.exitNumber}번 출구 · 방향 기준점")
                    .transform;
                group.SetParent(root, false);
                group.localPosition = new Vector3(anchor.x, 0f, anchor.z);
                var towardStation = new Vector3(map.offsetX - anchor.x, 0f, map.offsetZ - anchor.z);
                if (towardStation.sqrMagnitude > .001f)
                    group.localRotation = Quaternion.LookRotation(towardStation.normalized, Vector3.up);

                // 출구 사진을 복제하지 않는 중립 모형. 낮은 받침·출입구 매스·7호선 색상 표식만 둔다.
                block(group, "출구 보행 받침 · 통행 권위 없음",
                    new Vector3(0f, .18f, 0f), new Vector3(6.4f, .36f, 4.8f),
                    new Color(.66f, .67f, .62f));
                block(group, "출구 중립 구조",
                    new Vector3(0f, 1.35f, .25f), new Vector3(4.2f, 2.35f, 3.2f),
                    new Color(.84f, .86f, .80f));
                block(group, "출구 차양",
                    new Vector3(0f, 2.75f, .15f), new Vector3(4.8f, .35f, 3.7f),
                    new Color(.25f, .31f, .22f));
                block(group, "7호선 번호 표식 기둥",
                    new Vector3(-2.15f, 2.15f, -1.35f), new Vector3(.38f, 4.3f, .38f),
                    new Color(.38f, .42f, .10f));
                block(group, $"{anchor.exitNumber}번 출구 번호판",
                    new Vector3(-2.15f, 4.1f, -1.35f), new Vector3(2.1f, 1.3f, .32f),
                    new Color(.70f, .76f, .18f));
                visualPartCount += 5;
            }

            return new BuildReport(root, source.Anchors, visualPartCount);
        }
    }
}
