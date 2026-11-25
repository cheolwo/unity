using System;
using System.Collections;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 단위 × 자료 시각 검증 촬영 (2026-11-23 21:43 사용자: 「각각의 단위별로 정리된 데이터들이 시각적으로도 잘 표현이 되는지 검증」,
    //  「각각의 데이터별로 데이터들 특색을 맞춰 예쁘게 아름답게」).
    //  - 자료 하나씩만 켠 장면을 단위(구 · 타일 · 동)마다 찍는다. 날짜 · 시각을 고정해 낮 · 저녁 · 밤 · 비 · 눈을 본다.
    //  - 저장하지 않는다(ProofDataPush / Pop). 파일 이름: v-<단위>-<차례>-<자료>.png
    public sealed partial class SagajeongDispatchView
    {
        private static readonly DateTime AuditDay = new(2026, 10, 15);      // 평일 · 비 없음 · 점등 18:03
        private static readonly DateTime AuditNight = new(2026, 11, 22);
        private static readonly DateTime AuditRain = new(2026, 7, 18);
        private static readonly DateTime AuditSnow = new(2025, 12, 4);

        private IEnumerator AuditFrame(string directory, string file, int bits, int facilities, DateTime date, int seconds)
        {
            SetDataBits(bits);
            facilityGroupMask = facilities;
            proofDate = date;
            proofSeconds = seconds;
            for (var frame = 0; frame < 3; frame++) yield return null;
            yield return SaveFrame(directory, file);
        }

        // NationProof: 구 지도 가까이(116-gu-close의 자리), 역을 고르지 않은 채로 부른다. 끝나면 타일 배율에 머문다.
        private IEnumerator ViewProofVisualAuditGu(string directory)
        {
            var keep = ProofDataPush(32 | 16, 0);
            const int Base = 32 | 16;
            var morning = 8 * 3600 + 20 * 60;
            var noon = 14 * 3600;
            yield return AuditFrame(directory, "v-gu-01-lines.png", Base, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-gu-02-pulse-0820.png", Base | 1, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-gu-03-pulse-1400.png", Base | 1, 0, AuditDay, noon);
            yield return AuditFrame(directory, "v-gu-04-bus.png", Base | 1024, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-gu-05-road-0820.png", Base | 512, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-gu-06-road-1400.png", Base | 512, 0, AuditDay, noon);
            yield return AuditFrame(directory, "v-gu-07-dusk-1810.png", Base | 2 | 8, 0, AuditDay, 18 * 3600 + 10 * 60);
            yield return AuditFrame(directory, "v-gu-08-night-2100.png", Base | 2 | 8, 0, AuditNight, 21 * 3600);
            yield return AuditFrame(directory, "v-gu-09-rain.png", Base | 4 | 8, 0, AuditRain, 9 * 3600 + 30 * 60);
            yield return AuditFrame(directory, "v-gu-10-snow.png", Base | 4 | 8, 0, AuditSnow, 10 * 3600);
            yield return AuditFrame(directory, "v-gu-11-game.png", Base | 1 | 64 | 128 | 256, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-gu-12-all-night.png", Base | 1 | 2 | 4 | 8 | 512 | 1024, 0, AuditNight, 21 * 3600);
            yield return ViewProofSeasonGu(directory, Base);

            // 타일 단위: 사가정역 칸으로 다가간다(기본 검사와 같은 배율 0.24).
            if (EnsureHexTiles() != null)
            {
                var station = HexInfluenceTile(out _);
                if (station >= 0)
                {
                    PlaceGuOnHex(hexPack.tiles[station], 0.24f);
                    for (var frame = 0; frame < 8; frame++) yield return null;
                    yield return AuditFrame(directory, "v-tile-01-tiles.png", Base, 0, AuditDay, morning);
                    yield return AuditFrame(directory, "v-tile-02-facilities.png", Base, FacilityAllMask, AuditDay, morning);
                    yield return AuditFrame(directory, "v-tile-03-road-weather.png", Base | 512 | 2 | 8, 0, AuditDay, morning);
                    hexSelected = HexNearestOfKind(station, 8, 2);
                    yield return AuditFrame(directory, "v-tile-04-card.png", Base, FacilityAllMask, AuditDay, morning);
                    hexSelected = -1;
                    PlaceGuOnHex(hexPack.tiles[station], 0.32f);
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    yield return AuditFrame(directory, "v-tile-05-close-facilities.png", Base, FacilityAllMask, AuditDay, morning);
                }
            }
            proofDate = null;
            proofSeconds = -1;
            ProofDataPop(keep);
        }

        // CloseView: 카페 1호점 가운데 560 m 보기(62-close-mid)에서 부른다. 가장 가까이(CloseViewWidthMetres)도 한 번 찍는다.
        private IEnumerator ViewProofVisualAuditDong(string directory, double latitude, double longitude)
        {
            var keep = ProofDataPush(32 | 16, 0);
            const int Base = 32 | 16;
            var morning = 8 * 3600 + 20 * 60;
            yield return AuditFrame(directory, "v-dong-01-lines.png", Base, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-dong-02-road-0820.png", Base | 512, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-dong-03-bus.png", Base | 1024, 0, AuditDay, morning);
            yield return AuditFrame(directory, "v-dong-04-facilities.png", Base, FacilityAllMask, AuditDay, morning);
            yield return AuditFrame(directory, "v-dong-05-rain.png", Base | 4 | 512, 0, AuditRain, 9 * 3600 + 30 * 60);
            yield return AuditFrame(directory, "v-dong-06-snow.png", Base | 4, 0, AuditSnow, 10 * 3600);
            ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            for (var frame = 0; frame < 4; frame++) yield return null;
            yield return AuditFrame(directory, "v-dong-07-near-facilities.png", Base, FacilityAllMask, AuditDay, morning);
            yield return AuditFrame(directory, "v-dong-08-near-night.png", Base | 512 | 2, FacilityAllMask, AuditNight, 21 * 3600);
            proofDate = AuditDay;
            proofSeconds = 8 * 3600 + 20 * 60;
            yield return ViewProofStanceFlow(directory, latitude, longitude);      // 입장 → 불러오기 → 디오라마 → 집중(2026-11-24 16:52)
            ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / 560f));
            for (var frame = 0; frame < 4; frame++) yield return null;
            yield return AuditFrame(directory, "v-dong-09-dusk-mid.png", Base | 2 | 512, 0, AuditDay, 18 * 3600 + 10 * 60);
            yield return AuditFrame(directory, "v-dong-10-night-mid.png", Base | 2 | 512 | 1024, 0, AuditNight, 21 * 3600);
            yield return ViewProofSeasonDong(directory, Base, latitude, longitude);
            yield return ViewProofRealism(directory, Base);
            ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / 560f));
            for (var frame = 0; frame < 4; frame++) yield return null;
            proofDate = null;
            proofSeconds = -1;
            ProofDataPop(keep);
        }

        // 계절 · 변화 그림 구조(E4 · 2026-11-23): 같은 자리를 철마다, 낮 · 밤으로. 「계절 바탕」을 켠 채로 찍고 끝나면 저장된 값으로 돌린다.
        private static readonly DateTime SeasonSpring = new(2026, 4, 5);       // 서울 관측소 3/29 개화 · 만발 뒤
        private static readonly DateTime SeasonSummer = new(2026, 7, 18);      // 99 mm 비
        private static readonly DateTime SeasonAutumn = new(2026, 11, 3);      // 북한산 10/26 절정 + 8일(동네 규칙)
        private static readonly DateTime SeasonWinter = new(2025, 12, 4);      // 적설 5.1 cm

        private IEnumerator SeasonFrame(string directory, string file, int bits, DateTime date, int seconds, bool on = true)
        {
            seasonOn = on;
            envKey = null;
            yield return AuditFrame(directory, file, bits, 0, date, seconds);
        }

        private IEnumerator ViewProofSeasonGu(string directory, int baseBits)
        {
            yield return SeasonFrame(directory, "v-season-gu-1-spring.png", baseBits | 2 | 8, SeasonSpring, 14 * 3600);
            yield return SeasonFrame(directory, "v-season-gu-2-summer-rain.png", baseBits | 2 | 4 | 8, SeasonSummer, 10 * 3600);
            yield return SeasonFrame(directory, "v-season-gu-3-autumn.png", baseBits | 2 | 8, SeasonAutumn, 15 * 3600);
            yield return SeasonFrame(directory, "v-season-gu-4-winter-snow.png", baseBits | 2 | 4 | 8, SeasonWinter, 10 * 3600);
            yield return SeasonFrame(directory, "v-season-gu-5-autumn-night.png", baseBits | 2 | 8, SeasonAutumn, 19 * 3600 + 30 * 60);
            yield return SeasonFrame(directory, "v-season-gu-6-winter-night.png", baseBits | 2 | 4 | 8, SeasonWinter, 21 * 3600);
            yield return SeasonFrame(directory, "v-season-gu-7-autumn-off.png", baseBits | 2 | 8, SeasonAutumn, 15 * 3600, false);
            dataPanelOpen = true;
            previewDate = SeasonSpring;
            previewSeconds = 14 * 3600;
            yield return SeasonFrame(directory, "v-season-gu-8-preview-panel.png", baseBits | 8, SeasonSpring, 14 * 3600);
            dataPanelOpen = false;
            previewDate = null;
            previewSeconds = -1;
            seasonRead = false;
            ReadSeasonPreference();
            envKey = null;
        }

        // 바람 · 흩날림(2026-11-24, 사용자: 「2.5D라도 폴리곤을 써서 바람에 흩날리는 비 · 나뭇잎」).
        // 같은 자리를 짧은 간격으로 이어 찍어 움직임을 본다(w-<차례>-<장면>-<번호>.png → 움직이는 그림으로 묶는다).
        private IEnumerator WindSequence(string directory, string name, int bits, DateTime date, int seconds, int frames)
        {
            seasonOn = true;
            envKey = null;
            SetDataBits(bits);
            facilityGroupMask = 0;
            proofDate = date;
            proofSeconds = seconds;
            for (var frame = 0; frame < 4; frame++) yield return null;
            for (var k = 0; k < frames; k++)
            {
                yield return SaveFrame(directory, $"{name}-{k:00}.png");
                yield return new WaitForSecondsRealtime(0.09f);
            }
        }

        private IEnumerator ViewProofWindDong(string directory, int baseBits)
        {
            // 용마산 자락을 조금 더 가까이(가로 320 m): 숲과 길가 나무가 함께 든다.
            ViewProofCentre(SeasonDongLatitude, SeasonDongLongitude, (float)(Width * metresPerPixel / 320f));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return WindSequence(directory, "w-1-leaf-fall", baseBits | 2, new DateTime(2026, 11, 12), 14 * 3600, 12);
            yield return WindSequence(directory, "w-2-petal-fall", baseBits | 2, new DateTime(2026, 4, 8), 13 * 3600, 12);
            yield return WindSequence(directory, "w-3-rain-wind", baseBits | 2 | 4, SeasonSummer, 10 * 3600, 10);
            yield return WindSequence(directory, "w-4-snow-wind", baseBits | 2 | 4, SeasonWinter, 10 * 3600, 10);
            yield return SeasonFrame(directory, "w-5-azalea-0328.png", baseBits | 2, new DateTime(2026, 3, 28), 14 * 3600);
            yield return SeasonFrame(directory, "w-6-royal-0426.png", baseBits | 2, new DateTime(2026, 4, 26), 14 * 3600);
            yield return SeasonFrame(directory, "w-7-ipap-0506.png", baseBits | 2, new DateTime(2026, 5, 6), 14 * 3600);
        }

        // 현실감 시험(2026-11-24 01:41 사용자: 「시안을 아까 뽑은 게 괜찮았었거든. 한번 적용을 해줘봐봐. 그리고 적용된 화면을 나한테 한번 보여줘.」)
        // 사가정역 둘레를 시안과 같은 시각으로: 한낮 · 오후 · 아침 · 겨울 · 노을 · 밤, 가까이(옥상 · 벽 · 창), 멀리. 몇 장은 끈 그림과 나란히.
        private const double RealismLatitude = 37.580912, RealismLongitude = 127.088502;

        private IEnumerator RealismFrame(string directory, string file, int bits, DateTime date, int seconds, bool on)
        {
            realismRead = true;
            realismOn = on;
            yield return SeasonFrame(directory, file, bits, date, seconds);
        }

        private const double SurveyLampLatitude = 37.58177, SurveyLampLongitude = 127.08782;      // 보안등 줄(골목) 가운데

        private IEnumerator ViewProofRealism(string directory, int baseBits)
        {
            WriteRealismDongs(directory);
            var june = new DateTime(2026, 6, 12);
            ViewProofCentre(RealismLatitude, RealismLongitude, (float)(Width * metresPerPixel / 560f));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return RealismFrame(directory, "r-01-mid-noon.png", baseBits | 2, june, 12 * 3600 + 30 * 60, true);
            yield return RealismFrame(directory, "r-02-mid-noon-off.png", baseBits | 2, june, 12 * 3600 + 30 * 60, false);
            yield return RealismFrame(directory, "r-03-mid-1700.png", baseBits | 2, june, 17 * 3600, true);
            yield return RealismFrame(directory, "r-04-mid-0800.png", baseBits | 2, june, 8 * 3600, true);
            yield return RealismFrame(directory, "r-05-mid-dec-noon.png", baseBits | 2, new DateTime(2025, 12, 12), 12 * 3600 + 30 * 60, true);
            yield return RealismFrame(directory, "r-06-mid-dusk.png", baseBits | 2, AuditDay, 17 * 3600 + 35 * 60, true);
            yield return RealismFrame(directory, "r-07-mid-night.png", baseBits | 2, AuditNight, 21 * 3600, true);
            yield return RealismFrame(directory, "r-08-mid-night-off.png", baseBits | 2, AuditNight, 21 * 3600, false);
            ViewProofCentre(RealismLatitude, RealismLongitude, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return RealismFrame(directory, "r-09-near-1500.png", baseBits | 2, june, 15 * 3600, true);
            yield return RealismFrame(directory, "r-10-near-1500-off.png", baseBits | 2, june, 15 * 3600, false);
            yield return RealismFrame(directory, "r-11-near-night.png", baseBits | 2, AuditNight, 21 * 3600, true);
            yield return RealismFrame(directory, "r-11b-near-night-off.png", baseBits | 2, AuditNight, 21 * 3600, false);
            yield return RealismFrame(directory, "r-12-near-autumn.png", baseBits | 2, SeasonAutumn, 15 * 3600, true);
            // G4: 조사된 보안등이 줄지어 선 골목(역 북쪽 약 95 m, OA-21696 2022-08)을 가까이, 밤 · 낮
            ViewProofCentre(SurveyLampLatitude, SurveyLampLongitude, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return RealismFrame(directory, "r-15-lamps-night.png", baseBits | 2, AuditNight, 21 * 3600, true);
            yield return RealismFrame(directory, "r-16-lamps-night-off.png", baseBits | 2, AuditNight, 21 * 3600, false);
            yield return RealismFrame(directory, "r-17-lamps-day.png", baseBits | 2, june, 15 * 3600, true);
            // R4: 역 남서쪽 정류장 줄(사가정역 약 80 m) 가까이, 낮 · 밤(쉘터 · 차)
            ViewProofCentre(37.58065, 127.08762, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return RealismFrame(directory, "r-18-bus-stop-1800.png", baseBits | 2, june, 18 * 3600, true);
            yield return RealismFrame(directory, "r-19-bus-stop-night.png", baseBits | 2, AuditNight, 21 * 3600, true);
            // (다른 「가」 동 장면은 뺐다: 역 보기는 600 m 창 밖을 어둡게 · 비워 그려 견줄 수 없었다(g2b · g2c). 같은 규칙이 든 동 수는 읽기 기록 「refined (가)」로 본다.)
            ViewProofCentre(RealismLatitude, RealismLongitude, (float)(Width * metresPerPixel / 1400f));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return RealismFrame(directory, "r-13-far-1600.png", baseBits | 2, june, 16 * 3600, true);
            yield return RealismFrame(directory, "r-14-far-1600-off.png", baseBits | 2, june, 16 * 3600, false);
            yield return RealismFrame(directory, "r-13b-far-night.png", baseBits | 2, AuditNight, 21 * 3600, true);      // 역둘레 B1: 멀리 밤(녹은 창의 불빛)
            realismRead = false;
            ReadRealismPreference();
            seasonRead = false;
            ReadSeasonPreference();
            envKey = null;
        }

        // 면목4동 용마산 자락(숲 · 공원과 집이 반씩 섞인 곳, 구 색 바탕으로 고름).
        private const double SeasonDongLatitude = 37.57685, SeasonDongLongitude = 127.09034;

        private IEnumerator ViewProofSeasonDong(string directory, int baseBits, double cafeLatitude, double cafeLongitude)
        {
            ViewProofCentre(SeasonDongLatitude, SeasonDongLongitude, (float)(Width * metresPerPixel / 560f));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return SeasonFrame(directory, "v-season-dong-1-spring.png", baseBits | 2, SeasonSpring, 14 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-2-summer-rain.png", baseBits | 2 | 4, SeasonSummer, 10 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-3-autumn.png", baseBits | 2, SeasonAutumn, 15 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-4-winter-snow.png", baseBits | 2 | 4, SeasonWinter, 10 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-5-spring-dusk.png", baseBits | 2, SeasonSpring, 18 * 3600 + 50 * 60);
            yield return SeasonFrame(directory, "v-season-dong-6-summer-night.png", baseBits | 2 | 4, SeasonSummer, 21 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-7-autumn-night.png", baseBits | 2, SeasonAutumn, 19 * 3600 + 30 * 60);
            yield return SeasonFrame(directory, "v-season-dong-8-winter-night.png", baseBits | 2 | 4, SeasonWinter, 21 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-9-autumn-off.png", baseBits | 2, SeasonAutumn, 15 * 3600, false);
            // 빽빽한 동네(카페 1호점 둘레)를 가까이: 길가 나무가 철을 보여 준다.
            ViewProofCentre(cafeLatitude, cafeLongitude, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return SeasonFrame(directory, "v-season-dong-10-near-spring.png", baseBits | 2, SeasonSpring, 14 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-11-near-summer.png", baseBits | 2, new DateTime(2026, 6, 20), 11 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-12-near-autumn.png", baseBits | 2, SeasonAutumn, 15 * 3600);
            yield return SeasonFrame(directory, "v-season-dong-13-near-winter.png", baseBits | 2 | 4, SeasonWinter, 10 * 3600);
            yield return ViewProofWindDong(directory, baseBits);
            ViewProofCentre(cafeLatitude, cafeLongitude, (float)(Width * metresPerPixel / 560f));
            for (var frame = 0; frame < 4; frame++) yield return null;
            seasonRead = false;
            ReadSeasonPreference();
            envKey = null;
        }
    }
}
