using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 유동인구(생활인구) 층 (2026-11-24 13:25 사용자: 「유동인구 같은 거는 음식점 사장님들이 좀 알면 좋겠다 싶어가지고 그거를 좀 추가를 해주면 좋겠네」).
    //  - 구 지도에 행정동마다 원 하나: 크기 = 그 시각(게임 시계) 그 동에 머문 사람 수(평일 · 주말 평균), 색 = 주민등록 인구와 견준 배율
    //    (주황 = 사는 사람보다 많이 모인다 · 파랑 = 빠져나갔다 · 금빛 = 비슷하다).
    //  - 왼쪽 카드: 마우스를 올린 동(없으면 사가정역 둘레 동)의 24시간 곡선(평일 · 주말), 지금 인원과 배율, 점심 · 저녁 나이 구성, 장사 갈래 한 줄(셈).
    //  - 자료 창 셋째 칸 「사람」의 「유동인구」(비트 16384). 입장 카드: 음식점 사장 1순위(구 단위에서 켬) · 홍달 경영자 · 배달 기사 3순위(직접 켜기).
    //  자료: Resources/hongdal-data-floating.json ← artifacts/local/public-data/floating-population-20261124/build_floating.py
    //        (서울시 행정동 생활인구 OA-14991 2026-07 · KT 통신 자료 기반 추정 공표값 · 주민등록 2026-10-31)
    public sealed partial class SagajeongDispatchView
    {
        private const string FloatResource = "hongdal-data-floating";
        private const int FloatBit = 16384;

        [Serializable] private sealed class FloatPack { public string schema, month, note, source; public int weekdays, weekendDays; public FloatDong[] dongs; }
        [Serializable] private sealed class FloatDong
        {
            public string code, name, gu;
            public float lon, lat, km2;
            public int res;
            public int[] wd, we;
            public int lYoung, lMid, lOld, lFemale, dYoung, dMid, dOld, dFemale;
        }

        private bool floatOn;
        private FloatPack floatPack;
        private bool floatTried;
        private Vector2[] floatMetres;            // 행정동 가운데(구 지도 미터)
        private object floatMetresSpace;          // 위 값을 셈한 구 지도 판
        private int floatHover = -1;
        private int floatHome = -1;               // 사가정역 둘레 동
        private int floatClockProof = -1;         // 촬영용 게임 시각(분)

        private bool LoadFloat()
        {
            if (floatTried) return floatPack != null;
            floatTried = true;
            var pack = ReadMapResource<FloatPack>(FloatResource);
            if (pack?.dongs == null || pack.dongs.Length == 0)
            {
                Debug.LogWarning("Hongdal floating: 생활인구 자료가 없어 층을 켜지 않습니다.");
                return false;
            }
            floatPack = pack;
            Debug.Log($"Hongdal floating: {pack.month} · 행정동 {pack.dongs.Length} · 평일 {pack.weekdays}일 · 주말 {pack.weekendDays}일");
            return true;
        }

        private int FloatHour()
        {
            var minutes = floatClockProof >= 0 ? floatClockProof : FlowClockMinutes();      // 게임 시계(SagajeongDispatchProduceFlow.cs)
            return minutes < 0 ? 12 : Mathf.Clamp(minutes / 60, 0, 23);
        }

        private bool FloatWeekend => GameDate().DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        private int FloatCount(FloatDong dong, int hour) => (FloatWeekend ? dong.we : dong.wd) is { Length: 24 } line ? line[hour] : 0;

        private bool FloatReady()
        {
            if (!floatOn || !LoadFloat() || guSpace == null) return false;
            if (floatMetres != null && ReferenceEquals(floatMetresSpace, guSpace)) return true;
            floatMetres = new Vector2[floatPack.dongs.Length];
            var best = float.MaxValue;
            var home = 이어지는지도Policy.구미터(127.0885, 37.5809, guSpace.west, guSpace.south);
            for (var i = 0; i < floatPack.dongs.Length; i++)
            {
                floatMetres[i] = 이어지는지도Policy.구미터(floatPack.dongs[i].lon, floatPack.dongs[i].lat, guSpace.west, guSpace.south);
                var distance = (floatMetres[i] - home).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                floatHome = i;
            }
            floatMetresSpace = guSpace;
            return true;
        }

        // 원 크기: 사람 수의 제곱근(넓이가 사람 수에 비례) · 구 지도 배율을 따른다
        private float FloatRadius(int count) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0, count)) * 1.5f * guScale, 2.5f, 44f);

        private Rect FloatCardRect => PortraitScreen ? PortraitFloatRect(304f, 252f) : new Rect(22f - Field.x, 318f - Field.y, 304f, 252f);      // 왼쪽 카드(Field 안쪽 좌표)

        private static Color FloatTint(float ratio) => ratio >= 1.3f ? Ink(0xFF9F5A) : ratio <= 0.8f ? Ink(0x6FA8FF) : Ink(0xF2C35B);

        // ── 구 지도: 행정동 원 (Field 안쪽 좌표 · Repaint) ──────────────────────────

        private void DrawGuFloating(float now)
        {
            if (!FloatReady()) return;
            var hour = FloatHour();
            var mouse = Event.current.mousePosition;
            var mouseFree = !MapOverlayCovers(mouse + Field.position);
            floatHover = -1;
            var bestHover = float.MaxValue;
            var view = new Rect(-60f, -60f, Field.width + 120f, Field.height + 120f);
            var shown = new System.Collections.Generic.List<(int Index, Vector2 Point, int Count)>();
            for (var i = 0; i < floatPack.dongs.Length; i++)
            {
                var dong = floatPack.dongs[i];
                var point = GuPoint(floatMetres[i].x, floatMetres[i].y);
                if (!view.Contains(point)) continue;
                var count = FloatCount(dong, hour);
                var radius = FloatRadius(count);
                var ratio = count / (float)Mathf.Max(1, dong.res);
                var tint = FloatTint(ratio);
                Disc(point, radius, Fade(tint, 0.13f));
                Hoop(point, radius, Fade(tint, 0.7f), 1.1f);
                Disc(point, 1.6f, tint);
                if (point.x > 50f && point.y > 30f && point.x < Field.width - 50f && point.y < Field.height - 20f &&
                    !MapOverlayCovers(point + Field.position) && !FloatCardRect.Contains(point))
                    shown.Add((i, point, count));      // 이름표 후보: 화면 안 · 창 · 카드에 가리지 않는 것
                if (!mouseFree) continue;
                var distance = Vector2.Distance(mouse, point);
                if (distance <= radius + 6f && distance < bestHover)
                {
                    bestHover = distance;
                    floatHover = i;
                }
            }
            foreach (var (index, point, count) in shown.OrderByDescending(entry => entry.Count).Take(3))      // 이 화면에서 가장 붐비는 셋
                GuPlate(point + new Vector2(0f, -14f), $"{floatPack.dongs[index].name} {count / 10000f:0.0}만", 10, Ink(0xFFD395), 0.88f);
            if (floatHover >= 0)
            {
                var point = GuPoint(floatMetres[floatHover].x, floatMetres[floatHover].y);
                Hoop(point, FloatRadius(FloatCount(floatPack.dongs[floatHover], hour)) + 3f, Mint, 2f);
            }
        }

        // ── 구 지도 왼쪽 카드 (논리 좌표 → Field 안쪽) ──────────────────────────────

        private void DrawGuFloatingCard()
        {
            if (!FloatReady()) return;
            if (PortraitScreen && PortraitFloatCardHidden) return;      // 세로 화면: 원을 누른 동만, 다른 창이 없을 때
            var index = floatHover >= 0 ? floatHover : floatHome;
            if (index < 0) return;
            var dong = floatPack.dongs[index];
            var hour = FloatHour();
            var weekend = FloatWeekend;
            var count = FloatCount(dong, hour);
            var ratio = count / (float)Mathf.Max(1, dong.res);
            var card = FloatCardRect;
            Paint(card, Fade(Panel, 0.96f), 10);
            var x = card.x + 12f;
            var width = card.width - 24f;
            var y = card.y + 8f;
            Text(new Rect(x, y, width, 16f), "유동인구 · 음식점 사장의 눈", 10, Gold, true);
            Text(new Rect(x, y + 15f, width, 24f), dong.name, 17, Paper, true);
            Text(new Rect(x + 90f, y + 19f, width - 90f, 16f), $"{dong.gu} · {(floatHover >= 0 ? "마우스" : "사가정 둘레")}", 9, Soft, false, TextAnchor.MiddleRight);
            y += 42f;
            Text(new Rect(x, y, width, 16f), $"지금 {hour}시({(weekend ? "주말" : "평일")}) {count:#,0}명", 12, Paper, true);
            Text(new Rect(x + width - 86f, y + 1f, 86f, 12f), "━ 평일  ━ 주말", 8, Soft, false, TextAnchor.MiddleRight);
            Paint(new Rect(x + width - 75f, y + 6f, 9f, 2f), Gold);
            Paint(new Rect(x + width - 33f, y + 6f, 9f, 2f), Ink(0x6FA8FF));
            Text(new Rect(x, y + 16f, width, 15f), $"주민등록 {dong.res:#,0}명의 {ratio:0.00}배 · " + (ratio >= 1.3f ? "사람이 모이는 시각" : ratio <= 0.8f ? "사람이 빠져나간 시각" : "사는 만큼 머문다"),
                10, FloatTint(ratio), true);
            y += 36f;
            var plot = new Rect(x, y, width, 70f);
            Paint(plot, Fade(Tile, 0.55f), 6);
            var top = Mathf.Max(1, Mathf.Max(dong.wd.Max(), dong.we.Max()));
            var low = Mathf.Min(dong.wd.Min(), dong.we.Min()) * 0.9f;      // 바닥을 0이 아니라 가장 적을 때 가까이에 둔다(오르내림이 보이게)
            var span = Mathf.Max(1f, top - low);
            Vector2 At(int h, int value) => new(plot.x + 6f + (plot.width - 12f) * h / 23f, plot.yMax - 6f - (plot.height - 14f) * (value - low) / span);
            foreach (var mark in new[] { 12, 19 })      // 점심 · 저녁
            {
                var px = At(mark, top).x;
                Paint(new Rect(px - 0.5f, plot.y + 4f, 1f, plot.height - 8f), Fade(Soft, 0.35f));
            }
            for (var h = 1; h < 24; h++)
            {
                Capsule(At(h - 1, dong.we[h - 1]), At(h, dong.we[h]), Fade(Ink(0x6FA8FF), weekend ? 0.95f : 0.55f), weekend ? 2.2f : 1.4f);
                Capsule(At(h - 1, dong.wd[h - 1]), At(h, dong.wd[h]), Fade(Gold, weekend ? 0.55f : 0.95f), weekend ? 1.4f : 2.2f);
            }
            var now = At(hour, weekend ? dong.we[hour] : dong.wd[hour]);
            Paint(new Rect(now.x - 0.75f, plot.y + 3f, 1.5f, plot.height - 6f), Mint);
            Disc(now, 3.4f, Mint);
            Text(new Rect(plot.x + 4f, plot.yMax + 1f, 60f, 12f), "0시", 8, Fade(Soft, 0.8f));
            Text(new Rect(At(12, top).x - 16f, plot.yMax + 1f, 32f, 12f), "점심", 8, Soft, false, TextAnchor.MiddleCenter);
            Text(new Rect(At(19, top).x - 16f, plot.yMax + 1f, 32f, 12f), "저녁", 8, Soft, false, TextAnchor.MiddleCenter);
            y += 86f;
            var lunch = dong.wd[12];
            var dinner = dong.wd[19];
            var night = dong.wd[3];
            var kind = lunch >= night * 1.3f ? "점심에 사람이 몰린다(일터 · 상가) → 점심 장사" :
                dinner > lunch && night >= lunch ? "낮엔 빠지고 저녁에 돌아온다(주거) → 저녁 · 배달 장사" : "하루 내내 고른 편 → 시간대보다 단골";
            Text(new Rect(x, y, width, 15f), kind, 10, Mint, true);
            Text(new Rect(x, y + 16f, width, 14f), $"평일 점심 {lunch:#,0} · 저녁 {dinner:#,0} · 새벽 {night:#,0}명", 9, Paper);
            Text(new Rect(x, y + 31f, width, 14f), $"점심 12시 나이 · 20~30대 {dong.lYoung}% · 40~50대 {dong.lMid}% · 60+ {dong.lOld}% · 여성 {dong.lFemale}%", 9, Soft);
            Text(new Rect(x, card.yMax - 17f, width, 13f), $"자료 · 서울시 생활인구(통신 추정) {floatPack.month} 평일 {floatPack.weekdays}일 · 주말 {floatPack.weekendDays}일 평균", 8,
                Fade(Soft, 0.85f));
        }
    }
}
