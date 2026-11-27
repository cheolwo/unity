using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 기본 판(1.0) 시험 촬영 (2026-11-19 22:37).
    //  - 디오라마 자료를 뺀 시험 빌드(auto-build-base.cmd)가 구 지도에서 제대로 켜지는지 본다.
    //  - 실행: HongdalSimulation.exe --hongdal-view-proof=<폴더> --hongdal-base
    //    (--hongdal-view-proof가 함께 있어 저장한 회차를 읽지도 쓰지도 않는다. --hongdal-base가 있으면 긴 방향 촬영 대신 이것을 한다.)
    //  - 동네 지도의 길은 기본 판처럼 닫아 둔다. 디오라마를 기다리지 않는다.
    // 화면을 찍고 적을 뿐이다. 규칙을 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        internal const string BaseProofFlag = "--hongdal-base";

        [Serializable]
        private sealed class BaseProofReceipt
        {
            public bool passed;
            public string failure = string.Empty;
            public bool stateArrived;
            public bool startedOnGu;
            public bool guReady;
            public bool staysOnGu;
            public bool dioramaDataFound;      // 뺐다면 false여야 한다
            public int dongDataFound;          // 뺐다면 0이어야 한다
            public bool subwayShown;
            public string error = string.Empty;
            public string level = string.Empty;
            public int panelsShot;             // 디오라마 없이 열어 본 창의 수
            public int logErrors;              // 촬영하는 동안 난 오류 · 예외의 수
            public string[] logErrorLines = Array.Empty<string>();      // 처음 12줄(어느 창에서 났는지 앞에 적는다)
            public int hexTiles;               // 타일 지도(2026-11-22): 읽은 타일 수
            public float hexAlpha;             // 사가정역 둘레로 다가갔을 때 타일이 드러난 정도(1이면 다 보임)
            public bool hexCardShown;          // 타일을 고르면 그릇 창이 뜨는가
            public bool capitalReady;          // 수도권 바탕(2026-11-25, SagajeongDispatchCapital.cs)
            public int capitalHexTiles;        // 수도권 육각 타일(공개판, SagajeongDispatchCapitalHex.cs)
            public bool capitalHexCard;
            public bool pdongOpened;           // 공개판 동 디오라마(SagajeongDispatchPublicDong.cs)
            public string pdongFromTile;
            public int opsEntries, opsPeaks, opsMouths;      // 지휘관 판독 층(11-25)
            public bool commanderInPicker;
            public int pdongBuildings;
            public int pdongTrees;
            public string pdongSource;
            public int capitalTilesLoaded;
            public float capitalFullMetresPerPoint;
            public int portraitShots;          // 세로 화면(폰)으로 찍은 장 수 (SagajeongDispatchPortrait.cs)
            public int portraitScreenWidth, portraitScreenHeight;
            public string hexDoubleNotice = string.Empty;      // 두 번 누르기에 뜬 안내(기본 판은 「다음 판」 안내만)
        }

        private static bool BaseProofRequested =>
            Environment.GetCommandLineArgs().Any(item => item.StartsWith(BaseProofFlag, StringComparison.Ordinal));

        private IEnumerator CaptureBaseProof(string directory)
        {
            Directory.CreateDirectory(directory);
            var receipt = new BaseProofReceipt();
            dongMapOpen = 판Policy.동지도;
            yield return Until(() => state != null && pending == null, Time.realtimeSinceStartupAsDouble + 60);
            receipt.stateArrived = state != null;
            yield return Until(() => baseEditionStarted && GuMapVisible, Time.realtimeSinceStartupAsDouble + 30);
            for (var frame = 0; frame < 20; frame++) yield return null;
            receipt.startedOnGu = mapLevel == MapLevel.District;
            receipt.guReady = GuReady;
            receipt.subwayShown = EnsureSubwayGu() != null;
            receipt.dioramaDataFound = Resources.Load<TextAsset>("hongdal-diorama") != null;
            receipt.dongDataFound = Resources.LoadAll<TextAsset>(string.Empty).Count(asset => asset != null && asset.name.StartsWith("hongdal-dong-", StringComparison.Ordinal));
            yield return SaveFrame(directory, "base-01-start.png");

            // 동네 지도로 데려가던 길을 불러도 구 지도에 머무는가
            ReturnToDong();
            for (var frame = 0; frame < 10; frame++) yield return null;
            receipt.staysOnGu = mapLevel == MapLevel.District;
            yield return SaveFrame(directory, "base-02-stays-gu.png");

            // 조금 흐른 뒤(주문 · 기사 빛이 도는 모습)
            yield return new WaitForSecondsRealtime(4f);
            yield return SaveFrame(directory, "base-03-after-4s.png");

            // 남겨 둔 창을 디오라마 없이 하나씩 열어 본다. 오류 · 예외는 세어 적는다(창 이름과 함께).
            var problems = new System.Collections.Generic.List<string>();
            var scene = "시작";
            void Watch(string condition, string stack, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                receipt.logErrors++;
                if (problems.Count < 12) problems.Add("[" + scene + "] " + condition.Split('\n')[0]);
            }
            Application.logMessageReceived += Watch;
            var panels = new (string File, string Name, Action Open, Action Close)[]
            {
                ("base-04-riders.png", "기사 운영", () => { SelectExpansion(0); DongDeskShortcut(0); }, () => expansionPanelOpen = false),
                ("base-05-food.png", "음식점", () => { SelectExpansion(1); DongDeskShortcut(1); }, () => expansionPanelOpen = false),
                ("base-06-franchise.png", "가맹점", () => { SelectExpansion(2); DongDeskShortcut(2); }, () => expansionPanelOpen = false),
                ("base-07-store.png", "창고·화물", () => { SelectExpansion(3); DongDeskShortcut(3); }, () => expansionPanelOpen = false),
                ("base-08-desk.png", "배차 보기", ToggleDesk, () => { if (showDesk) ToggleDesk(); }),
                ("base-09-settings.png", "설정", ToggleSettings, () => { if (settingsOpen) ToggleSettings(); }),
                ("base-10-report.png", "오늘 결산", ToggleDailyReport, () => { if (dailyReportOpen) ToggleDailyReport(); }),
                ("base-11-ledger.png", "플랫폼 장부", 플랫폼장부열기, () => 플랫폼장부열림 = false),
                ("base-12-status.png", "운영 현황", () => sidePanelsOpen = true, () => sidePanelsOpen = false),
            };
            foreach (var panel in panels)
            {
                scene = panel.Name;
                try { panel.Open(); }
                catch (Exception exception) { receipt.logErrors++; if (problems.Count < 12) problems.Add("[" + scene + "] 열기: " + exception.Message); }
                for (var frame = 0; frame < 8; frame++) yield return null;
                yield return SaveFrame(directory, panel.File);
                try { panel.Close(); }
                catch (Exception exception) { receipt.logErrors++; if (problems.Count < 12) problems.Add("[" + scene + "] 닫기: " + exception.Message); }
                for (var frame = 0; frame < 4; frame++) yield return null;
                receipt.panelsShot++;
            }

            // 운영을 시작하고, 순회를 눌러 본다(동네 지도로 데려가던 흐름).
            scene = "운영 시작";
            if (state != null && pending == null && !running) BeginRound();
            yield return new WaitForSecondsRealtime(4f);
            yield return SaveFrame(directory, "base-13-running.png");
            scene = "순회";
            try { PressCycle(); }
            catch (Exception exception) { receipt.logErrors++; if (problems.Count < 12) problems.Add("[" + scene + "] " + exception.Message); }
            for (var frame = 0; frame < 10; frame++) yield return null;
            yield return SaveFrame(directory, "base-14-cycle.png");
            receipt.staysOnGu = receipt.staysOnGu && mapLevel == MapLevel.District;
            // 역 거점의 칸(행정동, 2026-11-20): 사가정역을 고르고 그 자리로 다가가 본다.
            scene = "역 칸";
            if (EnsureSubwayGu() != null && GuReady)
            {
                expansionPanelOpen = false;
                var home = 이어지는지도Policy.구미터(127.088502, 37.580912, guSpace.west, guSpace.south);
                ZoomGu(0.16f, GuPoint(home.x, home.y));
                guOrigin += new Vector2(Field.width * 0.5f, Field.height * 0.5f) - GuPoint(home.x, home.y);
                ClampGu();
                guManual = true;
                guStation = "사가정역";
                for (var frame = 0; frame < 10; frame++) yield return null;
                yield return SaveFrame(directory, "base-15-station-cells.png");
                guStation = "회기역";
                var hoegi = 이어지는지도Policy.구미터(127.0577, 37.5897, guSpace.west, guSpace.south);
                guOrigin += new Vector2(Field.width * 0.5f, Field.height * 0.5f) - GuPoint(hoegi.x, hoegi.y);
                ClampGu();
                for (var frame = 0; frame < 10; frame++) yield return null;
                yield return SaveFrame(directory, "base-16-station-cells-hoegi.png");
                guStation = null;
            }
            // 타일 지도(구와 동 사이 겹, 2026-11-22): 사가정역 둘레로 다가가 타일을 보고, 한 칸을 골라 그릇 창을 열고, 두 번 눌러 본다.
            // 기본 판은 동 지도가 닫혀 있어 두 번 누르기는 안내만 하고 구 지도에 머물러야 한다(SagajeongDispatchHexTiles.cs).
            scene = "타일 지도";
            if (GuReady && EnsureHexTiles() != null)
            {
                expansionPanelOpen = false;
                guStation = null;
                receipt.hexTiles = hexPack.tiles.Length;
                var stationTile = HexInfluenceTile(out _);
                if (stationTile >= 0)
                {
                    PlaceGuOnHex(hexPack.tiles[stationTile], 0.24f);
                    for (var frame = 0; frame < 10; frame++) yield return null;
                    receipt.hexAlpha = HexTilesAlpha;
                    yield return SaveFrame(directory, "base-17-tiles.png");
                    var pick = HexNearestOfKind(stationTile, 8, 2);      // 가까운 번화가 칸(없으면 역 칸)
                    hexSelected = pick;
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    receipt.hexCardShown = HexCardVisible;
                    yield return SaveFrame(directory, "base-18-tile-card.png");
                    OpenHexTileDong(pick);
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    receipt.hexDoubleNotice = hexNoticeShown ?? string.Empty;
                    receipt.staysOnGu = receipt.staysOnGu && mapLevel == MapLevel.District;
                    yield return SaveFrame(directory, "base-19-tile-double.png");
                    if (PublicDongOpen)      // 기본 판도 이제 공개판 디오라마로 들어간다 → 닫고 이어서 찍는다
                    {
                        receipt.pdongFromTile = pdongCode;
                        ClosePublicDong();
                        for (var frame = 0; frame < 4; frame++) yield return null;
                    }
                    hexSelected = -1;
                    PlaceGuOnHex(hexPack.tiles[stationTile], 0.32f);      // 가장 가까운 쪽: 칸마다 성격 이름
                    for (var frame = 0; frame < 8; frame++) yield return null;
                    yield return SaveFrame(directory, "base-20-tiles-near.png");
                    PlaceGuOnHex(hexPack.tiles[stationTile], 0.19f);      // 떠오르는 중간(구 지도와 겹친 모습)
                    for (var frame = 0; frame < 8; frame++) yield return null;
                    yield return SaveFrame(directory, "base-21-tiles-fade.png");
                }
            }

            // 수도권 바탕(2026-11-25): 가장 넓게 · 수원 둘레 · 인천 둘레 (SagajeongDispatchCapital.cs)
            scene = "수도권";
            receipt.capitalReady = CapitalReady;
            if (receipt.capitalReady && GuReady)
            {
                expansionPanelOpen = false;
                hexSelected = -1;
                guStation = null;
                receipt.capitalFullMetresPerPoint = 1f / Mathf.Max(0.000001f, GuFullScale);
                var spots = new (string File, double Lon, double Lat, float Scale)[]
                {
                    ("base-22-capital-all.png", 127.1, 37.6, GuFullScale),
                    ("base-23-capital-suwon.png", 127.03, 37.27, 0.06f),
                    ("base-24-capital-incheon.png", 126.70, 37.46, 0.03f),
                    ("base-25-capital-edge-gu.png", 127.15, 37.60, 0.09f),
                    ("base-29-nation-busan.png", 129.06, 35.16, 0.035f),      // 전국 바탕(있을 때): 부산 · 제주
                    ("base-30-nation-jeju.png", 126.55, 33.38, 0.012f),
                };
                foreach (var spot in spots)
                {
                    var at = 이어지는지도Policy.구미터(spot.Lon, spot.Lat, guSpace.west, guSpace.south);
                    guScale = Mathf.Max(GuFullScale, spot.Scale);
                    guOrigin = new Vector2(Field.width * 0.5f - at.x * guScale, Field.height * 0.5f - (guSpace.height - at.y) * guScale);
                    guManual = true;
                    ClampGu();
                    for (var frame = 0; frame < 40; frame++) yield return null;      // 조각을 프레임마다 몇 개씩 읽는다
                    yield return SaveFrame(directory, spot.File);
                }
                receipt.capitalTilesLoaded = capitalTiles.Count;
                // 수도권 칸 → 두 번 누르기 → 공개판 동 디오라마 (수원 행궁동 둘레)
                if (EnsureCapitalHex())
                {
                    receipt.capitalHexTiles = capitalHexCount;
                    var at = 이어지는지도Policy.구미터(127.0136, 37.2826, guSpace.west, guSpace.south);
                    guScale = 0.12f;
                    guOrigin = new Vector2(Field.width * 0.5f - at.x * guScale, Field.height * 0.5f - (guSpace.height - at.y) * guScale);
                    ClampGu();
                    for (var frame = 0; frame < 30; frame++) yield return null;
                    chSelected = CapitalHexAt(new Vector2(at.x, at.y));
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    receipt.capitalHexCard = CapitalHexCardVisible;
                    yield return SaveFrame(directory, "base-26-capital-hex.png");
                    if (chSelected >= 0) OpenCapitalHexDong(chSelected);
                    for (var frame = 0; frame < 30; frame++) yield return null;
                    receipt.pdongOpened = PublicDongOpen;
                    receipt.pdongBuildings = pdongBuildings;
                    receipt.pdongTrees = pdongTrees;
                    receipt.pdongSource = pdongSource ?? string.Empty;
                    yield return SaveFrame(directory, "base-27-pdong.png");
                    if (PublicDongOpen)
                    {
                        pdYaw = 140f; pdPitch = 34f; pdDistance *= 0.42f;
                        for (var frame = 0; frame < 10; frame++) yield return null;
                        yield return SaveFrame(directory, "base-28-pdong-close.png");
                        ClosePublicDong();
                        for (var frame = 0; frame < 4; frame++) yield return null;
                    }
                    // 전국 칸 → 부산 중앙동(PC 굽기 폴더가 있을 때, 2026-11-25)
                    var busan = 이어지는지도Policy.구미터(129.0335, 35.1036, guSpace.west, guSpace.south);
                    guScale = 0.12f;
                    guOrigin = new Vector2(Field.width * 0.5f - busan.x * guScale, Field.height * 0.5f - (guSpace.height - busan.y) * guScale);
                    ClampGu();
                    for (var frame = 0; frame < 30; frame++) yield return null;
                    chSelected = CapitalHexAt(new Vector2(busan.x, busan.y));
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    yield return SaveFrame(directory, "base-31-nation-hex-busan.png");
                    if (chSelected >= 0) OpenCapitalHexDong(chSelected);
                    for (var frame = 0; frame < 30; frame++) yield return null;
                    if (PublicDongOpen)
                    {
                        yield return SaveFrame(directory, "base-32-pdong-busan.png");
                        ClosePublicDong();
                        for (var frame = 0; frame < 4; frame++) yield return null;
                    }
                    chSelected = -1;
                }
                guManual = false;
            }

            // 지휘관 입장 · 판독 층(2026-11-25, SagajeongDispatchOpsRead.cs): 입장 카드(아홉 장) → 면목3·8동(사가정역) 작전 지도
            scene = "지휘관";
            {
                var keepStance = stanceKey;
                receipt.commanderInPicker = Array.Exists(Stances, item => item.Key == "commander" && item.Info?.package != null);
                stancePickerOpen = true;
                for (var frame = 0; frame < 6; frame++) yield return null;
                yield return SaveFrame(directory, "base-31-stance-picker.png");
                stancePickerOpen = false;
                stanceKey = "commander";
                if (OpenPublicDong("1126057500", "면목3·8동", "중랑구"))
                {
                    for (var frame = 0; frame < 30; frame++) yield return null;
                    receipt.opsEntries = opsEntries.Count;
                    receipt.opsPeaks = opsPeaks.Count;
                    receipt.opsMouths = opsMouths;
                    yield return SaveFrame(directory, "base-32-commander-ops.png");
                    pdPitch = 64f; pdDistance *= 0.62f;
                    for (var frame = 0; frame < 10; frame++) yield return null;
                    yield return SaveFrame(directory, "base-33-commander-near.png");
                    ClosePublicDong();
                    for (var frame = 0; frame < 4; frame++) yield return null;
                }
                stanceKey = keepStance;
            }

            // 세로 화면(폰): 입장으로 자료 보기(2026-11-25, SagajeongDispatchPortrait.cs)
            scene = "세로 화면";
            yield return CapturePortraitProof(directory, receipt);

            Application.logMessageReceived -= Watch;
            receipt.logErrorLines = problems.ToArray();

            receipt.level = mapLevel.ToString();
            receipt.error = error ?? string.Empty;
            receipt.passed = receipt.stateArrived && receipt.startedOnGu && receipt.guReady && receipt.staysOnGu && string.IsNullOrEmpty(receipt.error) &&
                receipt.hexTiles > 0 && receipt.hexCardShown && receipt.hexAlpha > 0.9f;
            if (!receipt.passed)
                receipt.failure = !receipt.stateArrived ? "게임 상태가 오지 않았습니다." : !receipt.guReady ? "구 지도 자료가 열리지 않았습니다." :
                    !receipt.startedOnGu ? "구 지도에서 시작하지 않았습니다." : !receipt.staysOnGu ? "동네 지도 요청에 구 지도를 떠났습니다." :
                    receipt.hexTiles == 0 ? "타일 지도 팩을 읽지 못했습니다." : !(receipt.hexCardShown && receipt.hexAlpha > 0.9f) ? "타일 지도나 그릇 창이 보이지 않았습니다." :
                    "오류: " + receipt.error;
            File.WriteAllText(Path.Combine(directory, "base-proof.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("Hongdal base proof: " + (receipt.passed ? "passed" : "failed: " + receipt.failure));
            Application.Quit(receipt.passed ? 0 : 2);
        }
    }
}
