using System.Collections.Generic;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>서버 위치·업무와 분리된 합성 동네 표현 자료. 기존 블록을 유지하며 주변에 추가한다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E4, "확장 동네 배경 배치 자료", Boundary = "배경 시설·도로는 업무 주체·배달 목적지·통행 권한이 아니다")]
    public static class 업무배달동네배경Layout
    {
        public static Bounds 바닥Bounds => new(new Vector3(-4, -.12f, 8), new Vector3(120, .2f, 96));
        // 바닥 하단부터 건물·이름표 상단까지 포함한다. 서버 상태로 맵 크기를 줄이지 않는다.
        public static Bounds 관찰Bounds => new(new Vector3(-4, 2.89f, 8), new Vector3(120, 6.22f, 96));
        public static IReadOnlyList<도로배치> 도로 { get; } = new[] {
            new 도로배치("도로 동서", -4, 0, 48, 4),
            new 도로배치("기사 대기 연결 도로", -20, 4, 4, 12),
            new 도로배치("서쪽 동네 도로", -44, 0, 32, 4),
            new 도로배치("동쪽 동네 도로", 36, 0, 32, 4),
            new 도로배치("북쪽 생활 도로", -4, 24, 112, 4),
            new 도로배치("남쪽 생활 도로", -4, -30, 112, 4),
            new 도로배치("서쪽 교차 도로", -40, 1, 4, 62),
            new 도로배치("동쪽 교차 도로", 40, 1, 4, 62)
        };
        public static IReadOnlyList<건물배치> 건물 { get; } = 건물자료();
        private static 건물배치[] 건물자료()
        {
            var result = new List<건물배치> {
                new("음식점", "음식점", 0, 6, 6, 6, "음식점", true),
                new("주문자 집", "주문자", 16, 6, 6, 6, "주택", true)
            };
            var index = 0;
            foreach (var z in new[] { 31f, -23f })
                foreach (var x in new[] { -48f, -32f, -16f, 0f, 16f, 32f, 48f })
                    result.Add(new 건물배치("배경 주택 " + ++index, "주택 · 배경", x, z, 6, 6, "주택"));
            index = 0;
            foreach (var x in new[] { -48f, -32f, 32f, 48f })
                result.Add(new 건물배치("배경 음식점 " + ++index, "음식점 · 배경", x, 7, 6, 6, "음식점"));
            result.Add(new 건물배치("배경 살뜰마트", "살뜰마트 · 미연동", -3, -12, 18, 10, "창고"));
            result.Add(new 건물배치("배경 보충창고", "보충창고 · 미연동", -26, -12, 16, 10, "창고"));
            return result.ToArray();
        }
        public readonly struct 도로배치
        {
            public readonly string 이름;
            public readonly Vector2 중심, 크기;
            public Rect 영역 => new(중심 - 크기 / 2, 크기);
            public 도로배치(string 이름, float x, float z, float width, float depth)
            { this.이름 = 이름; 중심 = new Vector2(x, z); 크기 = new Vector2(width, depth); }
        }
        public readonly struct 건물배치
        {
            public readonly string 이름, 표시명, 종류;
            public readonly Vector2 중심, 크기;
            public readonly bool 서버연결;
            public bool 창고형 => 종류 == "창고";
            public Rect 영역 => new(중심 - 크기 / 2, 크기);
            public float 출입구방향 => 창고형 ? 1 : -1;
            public Vector2 출입구 => 중심 + new Vector2(0, 출입구방향 * 크기.y / 2);
            public Rect 접근로영역 {
                get {
                    var length = 창고형 ? 6f : 4f;
                    var center = 출입구 + new Vector2(0, 출입구방향 * length / 2);
                    return new Rect(center - new Vector2(.75f, length / 2), new Vector2(1.5f, length));
                }
            }
            public 건물배치(string 이름, string 표시명, float x, float z, float width, float depth, string 종류, bool 서버연결 = false)
            { this.이름 = 이름; this.표시명 = 표시명; 중심 = new Vector2(x, z); 크기 = new Vector2(width, depth); this.종류 = 종류; this.서버연결 = 서버연결; }
        }
    }

    public sealed partial class 가상배달관찰Controller
    {
        private void 업무배달동네배경조립()
        {
            var floor = 업무배달동네배경Layout.바닥Bounds;
            Block("업무 서버 관찰 바닥", floor.center, floor.size, new Color(.3f, .45f, .28f));
            foreach (var road in 업무배달동네배경Layout.도로)
                Block(road.이름, new Vector3(road.중심.x, 0, road.중심.y), new Vector3(road.크기.x, .1f, road.크기.y), Color.gray);
            foreach (var building in 업무배달동네배경Layout.건물)
            {
                if (building.창고형)
                {
                    var p = building.중심; var size = building.크기;
                    var root = Block(building.이름, new Vector3(p.x, .1f, p.y), new Vector3(size.x, .2f, size.y), Color.white);
                    Block(building.이름 + " 후면", new Vector3(p.x, .5f, p.y - size.y / 2), new Vector3(size.x, 1, .2f), Color.white);
                    Block(building.이름 + " 출입구", new Vector3(building.출입구.x, .3f, building.출입구.y), new Vector3(2, .2f, .3f), new Color(.7f, .7f, .6f));
                    var access = building.접근로영역;
                    Block(building.이름 + " 접근로", new Vector3(access.center.x, .07f, access.center.y), new Vector3(access.width, .1f, access.height), new Color(.7f, .7f, .6f));
                    업무배달건물이름표(root, building.표시명, Color.black, 1.8f);
                }
                else
                {
                    var roleColor = building.종류 == "음식점" ? 가상동네역할색상.음식점 : 가상동네역할색상.주문자;
                    var color = building.서버연결 ? roleColor : Color.Lerp(roleColor, Color.white, .55f);
                    Building(building.이름, building.중심.x, building.중심.y, color);
                    업무배달건물이름표(transform.Find(building.이름), building.표시명, Color.white, 4.8f);
                }
            }
            // 미연동 배경 안에 기존 마트와 동일한 내부를 만든다. 조회 전 작업/재고는 숨긴다.
            마트내부조립();
            마트동적표현숨김();
        }
        private void 업무배달건물이름표(Transform parent, string text, Color color, float height)
        {
            Label(parent, text, color);
            var label = parent.GetComponentInChildren<TextMesh>();
            label.characterSize = .36f;
            label.transform.position = new Vector3(parent.position.x, height, parent.position.z);
        }
    }
}
