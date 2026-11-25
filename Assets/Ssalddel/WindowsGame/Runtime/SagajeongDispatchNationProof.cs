using System;
using System.Collections;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 전국 지도 촬영 (2026-11-16, 전국 물류시설 빛 시안의 바탕). 113 · 114는 해상 · 항공 층을 끈 모습. 115~117은 구 지도 · 서울 지도. 관찰용이며 실패 판정에 넣지 않는다.
    //  - 지금 판의 전국 지도를 처음 보기와 수도권 확대(최대 배율)로 찍고, 세계 → 화면 맞춤 값을 받은 값에 적는다.
    //    시안은 이 값으로 공공 시설 좌표(경위도 → 팩의 세계 좌표 → 화면)를 정확히 얹는다.
    //  - 지도 높이만 바꾼다. 업무 · 시계 · 저장은 그대로(시계는 촬영이 잡아 둔다). 끝나면 동네 지도로 돌아온다.
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator ViewProofNationLights(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            selectedBuilding = null;
            showDesk = false;
            mapOnly = false;
            LoadMapLevel(MapLevel.Nation);
            for (var frame = 0; frame < 6; frame++) yield return null;
            receipt.nationShown = NationMapVisible;
            if (!receipt.nationShown)
            {
                holdProofClock = false;
                yield break;
            }
            receipt.nationFieldX = Field.x;
            receipt.nationFieldY = Field.y;
            receipt.nationOriginX = nationOrigin.x;
            receipt.nationOriginY = nationOrigin.y;
            receipt.nationScale = nationScale;
            yield return SaveFrame(directory, "111-nation.png");

            // 수도권: 서울을 가운데 두고 가장 크게
            var seoul = new Vector2(nationSpace.seoulX, nationSpace.seoulY);
            ZoomNation(NationMaxScale, NationPoint(seoul));
            var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            nationOrigin += centre - NationPoint(seoul);
            ClampNation();
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.nationZoomOriginX = nationOrigin.x;
            receipt.nationZoomOriginY = nationOrigin.y;
            receipt.nationZoomScale = nationScale;
            yield return SaveFrame(directory, "112-nation-capital.png");

            // 흐름을 끈 모습(물류 시설 빛 시안의 바탕): 해상 · 항공 층을 잠시 끄고 같은 자리와 전국 처음 보기를 찍는다. 찍고 나면 되돌린다.
            var seaWas = nationSeaOn;
            var airWas = nationAirOn;
            nationSeaOn = false;
            nationAirOn = false;
            for (var frame = 0; frame < 4; frame++) yield return null;
            yield return SaveFrame(directory, "113-nation-capital-quiet.png");
            nationManual = false;
            for (var frame = 0; frame < 6; frame++) yield return null;
            yield return SaveFrame(directory, "114-nation-quiet.png");
            nationSeaOn = seaWas;
            nationAirOn = airWas;

            // 절기 장터(2026-11-24): 소설(11/24) 제철 목록 · 품목 카드(감) · 수산(삼치) · 대설(12/10)의 귤. 찍고 나면 끈다.
            if (LoadJangteo())
            {
                nationSeasonOn = true;
                nationHoverQuiet = true;       // 실제 마우스 자리에 따라 이름표가 뜨지 않게
                jangteoPreview = -1;
                foreach (var (file, date, pick) in new[]
                {
                    ("114s-nation-season.png", new DateTime(2026, 11, 24), (string)null),
                    ("114t-nation-season-item.png", new DateTime(2026, 11, 24), "감"),
                    ("114u-nation-season-sea.png", new DateTime(2026, 11, 24), "삼치"),
                    ("114v-nation-season-dec.png", new DateTime(2026, 12, 10), "귤"),
                    ("114w-nation-season-dec-list.png", new DateTime(2026, 12, 10), (string)null),
                })
                {
                    proofDate = date;
                    jangteoPick = pick;
                    for (var frame = 0; frame < 4; frame++) yield return null;
                    yield return SaveFrame(directory, file);
                }
                jangteoPick = null;
                // 관점 필터(2026-11-24 12:39 · 12:54 입장 카드로 합침): 같은 날 · 게임 시각 23:10(가락 경매 중)으로 다섯 입장
                if (LoadFlow())
                {
                    var keepKey = stanceKey;
                    proofDate = new DateTime(2026, 11, 24);
                    jangteoClockProof = 23 * 60 + 10;
                    foreach (var (file, key) in new[] { ("114x-lens-home.png", "resident"), ("114y-lens-trade.png", "wholesale"), ("114z-lens-truck.png", "cargo"),
                                 ("114za-lens-farm.png", "farm"), ("114zb-lens-shop.png", "store") })
                    {
                        stanceKey = key;          // 입장 열쇠만 바꾼다(자료 창 체크는 그대로)
                        for (var frame = 0; frame < 4; frame++) yield return null;
                        yield return SaveFrame(directory, file);
                    }
                    // 아이콘 집중 보기(2026-11-24 14:15): 가락(도매상) · 해남(농가) · 동네(동네 주민) · 위판장 여수 · 해남 가까운 배추 산지유통센터
                    foreach (var (file, key, focusKey) in new[]
                    {
                        ("114zc-focus-garak.png", "wholesale", "m:가락"),
                        ("114zd-focus-haenam.png", "farm", "o:해남"),
                        ("114ze-focus-home.png", "resident", "home"),
                        ("114zf-focus-port.png", (string)null, "p:여수"),
                        ("114zg-focus-apc.png", (string)null, FocusApcProofKey()),
                    })
                    {
                        stanceKey = key;
                        flowFocus = focusKey;
                        for (var frame = 0; frame < 4; frame++) yield return null;
                        yield return SaveFrame(directory, file);
                    }
                    flowFocus = null;
                    // 정리(2026-11-24 14:42): 마우스를 올린 모습(여수 위판장)
                    stanceKey = null;
                    nationHoverProof = "p:여수";
                    for (var frame = 0; frame < 4; frame++) yield return null;
                    yield return SaveFrame(directory, "114zh-hover-port.png");
                    nationHoverProof = null;
                    stanceKey = keepKey;
                    jangteoClockProof = -1;
                }
                nationSeasonOn = false;
                nationHoverQuiet = false;
                proofDate = null;
            }

            // 구 지도와 서울 지도(연속 지도 시안의 바탕, 2026-11-16): 처음 맞춤 · 사가정 가까이 · 서울 전역을 찍고 화면 맞춤 값을 적는다.
            LoadMapLevel(MapLevel.District);
            for (var frame = 0; frame < 8; frame++) yield return null;
            receipt.guShown = GuMapVisible;
            if (receipt.guShown)
            {
                receipt.guFieldX = Field.x;
                receipt.guFieldY = Field.y;
                receipt.guScale = guScale;
                receipt.guOriginX = guOrigin.x;
                receipt.guOriginY = guOrigin.y;
                yield return SaveFrame(directory, "115-gu.png");
                var home = new Vector2((float)((127.0884106 - guSpace.west) * 88224.0), (float)((37.5806971 - guSpace.south) * 111320.0));
                ZoomGu(0.12f, GuPoint(home.x, home.y));
                guOrigin += new Vector2(Field.width * 0.5f, Field.height * 0.5f) - GuPoint(home.x, home.y);
                ClampGu();
                for (var frame = 0; frame < 4; frame++) yield return null;
                receipt.guCloseScale = guScale;
                receipt.guCloseOriginX = guOrigin.x;
                receipt.guCloseOriginY = guOrigin.y;
                yield return SaveFrame(directory, "116-gu-close.png");
                // 유동인구(2026-11-24 13:25 「음식점 사장님들이 좀 알면 좋겠다」): 음식점 사장 입장 · 평일(7/15) 점심 12:30 · 저녁 19:30
                if (LoadFloat())
                {
                    var keepScale = guScale;
                    var keepOrigin = guOrigin;
                    var keepFloat = floatOn;
                    var keepKey = stanceKey;
                    var keepDate = proofDate;
                    ZoomGu(0.05f, GuPoint(home.x, home.y));
                    guOrigin += new Vector2(Field.width * 0.5f, Field.height * 0.5f) - GuPoint(home.x, home.y);
                    ClampGu();
                    floatOn = true;
                    stanceKey = "store";
                    proofDate = new DateTime(2026, 7, 15);
                    foreach (var (file, minutes) in new[] { ("115f-gu-floating-lunch.png", 12 * 60 + 30), ("115g-gu-floating-evening.png", 19 * 60 + 30) })
                    {
                        floatClockProof = minutes;
                        for (var frame = 0; frame < 4; frame++) yield return null;
                        yield return SaveFrame(directory, file);
                    }
                    floatClockProof = -1;
                    floatOn = keepFloat;
                    stanceKey = keepKey;
                    proofDate = keepDate;
                    guScale = keepScale;
                    guOrigin = keepOrigin;
                    ClampGu();
                    for (var frame = 0; frame < 2; frame++) yield return null;
                }
                // 역 창(2026-11-19): 사가정역을 고른 모습. 고른 것은 다음 장면 전에 푼다.
                if (EnsureSubwayGu() != null)
                {
                    guStation = "사가정역";
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "116a-gu-station-card.png");
                    // 자료 창 처음 모습(2026-11-23 20:15): 지하철 노선만 켠 채로 자료 창을 연다. 다음 장면들은 다 켜고 찍는다(저장하지 않음).
                    var keepData = ProofDataPush(DataDefaultBits, 0);
                    // 입장 고르기(2026-11-23 20:50): 시작 화면의 입장 카드. 촬영에서는 저절로 뜨지 않아 직접 연다.
                    var keepStance = stanceKey;
                    stanceKey = "rider";
                    stancePickerOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "158-stance-intro.png");
                    // 「동네 주민」을 고른 모습(저장하지 않음)
                    ApplyStance(Array.Find(Stances, item => item.Key == "resident"), false);
                    dataPanelOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "158b-stance-resident.png");
                    // 필수 · 부가(2026-11-24 15:02): 「음식점 사장」을 고르고 구 단위에서 「부가도 켜기」를 누른 모습(버스 · 번화가가 더 켜짐)
                    ApplyStance(Array.Find(Stances, item => item.Key == "store"), false);
                    dataPanelOpen = true;
                    ToggleStanceOptional();
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "158c-stance-store-extra.png");
                    // 부가 고르기(16:28): 음식점 사장 카드에서 부가 셋(버스 · 번화가 · 가락 몫)을 뺀 모습(저장하지 않음) · ESC 설정의 「입장 자료」 줄
                    var storeStance = Array.Find(Stances, item => item.Key == "store");
                    var skipped = new[] { "bus", "downtown", "fact:garak" };
                    foreach (var id in skipped) SetStanceSkip(storeStance, id, true, false);
                    dataPanelOpen = false;
                    stanceKey = "store";
                    stancePickerOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "158d-stance-pick-optional.png");
                    stancePickerOpen = false;
                    settingsOpen = true;
                    settingsTab = SettingsTabMap;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "158e-settings-stance.png");
                    settingsOpen = false;
                    foreach (var id in skipped) SetStanceSkip(storeStance, id, false, false);
                    stanceKey = keepStance;
                    ProofDataPush(DataDefaultBits, 0);
                    dataPanelOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "159-data-default.png");
                    dataPanelOpen = false;
                    ProofDataPush(DataAllBits, FacilityAllMask);
                    // 생활 자료(2026-11-23): 실제 달력의 날짜 · 시각을 고정해 역의 하루 · 밤 · 비 · 눈을 찍는다(SagajeongDispatchDataPacks.cs).
                    foreach (var (file, date, seconds) in new[]
                    {
                        ("160-data-morning.png", new DateTime(2026, 10, 15), 8 * 3600 + 20 * 60),
                        ("161-data-dusk.png", new DateTime(2026, 10, 15), 18 * 3600 + 10 * 60),
                        ("162-data-rain.png", new DateTime(2026, 7, 18), 9 * 3600 + 30 * 60),
                        ("163-data-snow.png", new DateTime(2025, 12, 4), 10 * 3600),
                        ("164-data-night.png", new DateTime(2026, 11, 22), 21 * 3600),
                    })
                    {
                        proofDate = date;
                        proofSeconds = seconds;
                        for (var frame = 0; frame < 3; frame++) yield return null;
                        yield return SaveFrame(directory, file);
                    }
                    // 역 속 창(D6)과 자료 창: 평일 아침, 사가정역에 하행 열차가 서 있는 시각.
                    proofDate = new DateTime(2026, 10, 15);
                    proofSeconds = InsideProofSeconds;
                    stationInsideOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "165-data-station-inside.png");
                    stationInsideOpen = false;
                    dataPanelOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "166-data-panel.png");
                    dataPanelOpen = false;
                    ProofDataPop(keepData);
                    proofDate = null;
                    proofSeconds = -1;
                    guStation = null;
                    // 단위 × 자료 시각 검증(2026-11-23 21:43): 자료 하나씩 · 낮밤비눈 · 타일 (SagajeongDispatchVisualAudit.cs)
                    yield return ViewProofVisualAuditGu(directory);
                }
            }
            LoadMapLevel(MapLevel.Seoul);
            for (var frame = 0; frame < 6; frame++) yield return null;
            receipt.seoulShown = mapLevel == MapLevel.Seoul;
            if (receipt.seoulShown) yield return SaveFrame(directory, "117-seoul.png");

            ReturnToDong();
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.nationBack = mapLevel == MapLevel.Dong;
            holdProofClock = false;
        }
    }
}
