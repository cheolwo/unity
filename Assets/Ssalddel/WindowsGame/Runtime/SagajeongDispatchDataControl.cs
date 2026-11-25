using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 자료 창 (2026-11-23 19:55 사용자: 「분야별로 체크박스 같은 것들을 선택하면 그것과 관련한 데이터만 나오도록 하는 데이터 컨트롤러 창」).
    //  - 구 지도 왼쪽 아래 「자료 창」 단추 하나가 예전 지도 보기 단추 줄(노선 · 번화가 · 내력 · 번짐 · 길 · 버스)을 대신한다.
    //  - 창을 열면 분야(교통 · 시간과 날씨 · 동네 꾸밈 · 생활 시설)별로 체크박스. 고른 것만 지도에 보인다. 다음에 켜도 그대로 남는다.
    //  - 창이 열려 있는 동안 역 창 · 타일 창은 접어 둔다(같은 자리를 쓴다).
    //  - 표현만 바꾼다. 주문 · 돈 · 영역 규칙은 그대로다.
    // 2차(2026-11-23 20:15 사용자: 「사용자가 될 수 있으면 원하는 것만 활성화해서 볼 수 있도록」 · 「지금은 게임성보다 정보를 디오라마에서 잘 알아볼 수 있도록」).
    //  - 처음에는 지하철 노선만 켠다. 나머지는 사용자가 골라 켠다(설정 키를 v2로 바꿔 예전 「모두 켬」 저장을 쓰지 않는다).
    //  - 게임 요소(홍달 플레이스 키워드 창 · 번화가 · 내력 문양 · 빛의 번짐)는 「게임 요소 · 시험 값」으로 묶어 처음엔 끈다.
    //  - 같은 창을 동 디오라마에서도 연다(길찾기 단추 오른쪽). 동에서 안 보이는 줄은 흐리게 「구 지도에서」라고 적는다.
    //  - 「처음대로」는 노선만 남기고 다 끈다.
    public sealed partial class SagajeongDispatchView
    {
        private const string DataControlPreference = "hongdal.data.control.v2";
        // 비트: 1 역 불빛 · 2 낮과 밤 · 4 비 · 눈 · 8 오늘 칩 · 16 타일 지도 · 32 노선 · 64 번화가 · 128 내력 · 256 빛의 번짐 · 512 길 · 1024 버스 · 2048 홍달 플레이스 창
        //       · 4096 등급 이름(신사 · 문사 …) · 8192 위 띠 경 수치 (2026-11-23 20:47 사용자: 「빼기보다는 넣어서 할 사람은 하고 아니면 안 하는 걸로」)
        private const string FacilityMaskPreference = "hongdal.data.facility-groups.v2";  // 생활 시설 갈래 비트(처음 0 = 모두 끔)
        private const int DataDefaultBits = 32 | 16;                                    // 처음에는 지하철 노선과 타일 지도(지도 단계: 구 → 타일 → 동으로 내려가는 길)만
        private const int DataAllBits = 16383;
        private const int FacilityAllMask = (1 << 9) - 1;
        private bool dataPanelOpen;
        private bool dataControlRead;
        private bool stationPulseOn, skyOn, weatherOn, todayChipOn, hexLayerOn, placePanelOn, gradeNamesOn, headerGyeongOn;
        private int facilityGroupMask;

        private static readonly string[] FacilityGroupNames =
            { "보행등 · 횡단보도", "정류장", "보행 출입구", "그늘 · 쉼 · 물", "자전거 · 따릉이", "공원 · 나무", "시장 · 관광", "역 출입구", "그 밖" };

        private static int FacilityGroup(string key) => key switch
        {
            "pedestrian_signal" => 0,
            "bus_stop" => 1,
            "pedestrian_entrance" => 2,
            "heat_shade" or "heat_shelter" or "drinking_water" or "public_toilet" => 3,
            "bicycle_parking" or "bike_share" => 4,
            "park" or "protected_tree" or "campground" => 5,
            "seoul_market_inventory" or "designated_specialty_street" or "tourism_site" => 6,
            "metro_unnumbered_exit" or "station_elevator" => 7,
            _ => 8,
        };

        private bool FacilityKeyShown(int keyIndex) =>
            facilityPack?.keys != null && keyIndex >= 0 && keyIndex < facilityPack.keys.Length &&
            (facilityGroupMask & (1 << FacilityGroup(facilityPack.keys[keyIndex]))) != 0;

        private int DataBits() =>
            (stationPulseOn ? 1 : 0) | (skyOn ? 2 : 0) | (weatherOn ? 4 : 0) | (todayChipOn ? 8 : 0) | (hexLayerOn ? 16 : 0) |
            (subwayLinesOn ? 32 : 0) | (subwayTownOn ? 64 : 0) | (subwayLoreOn ? 128 : 0) | (subwayClashOn ? 256 : 0) |
            (roadLayerOn ? 512 : 0) | (busLayerOn ? 1024 : 0) | (placePanelOn ? 2048 : 0) | (gradeNamesOn ? 4096 : 0) | (headerGyeongOn ? 8192 : 0) |
            (floatOn ? FloatBit : 0);      // 16384 유동인구(SagajeongDispatchFloating.cs)

        private void SetDataBits(int bits)
        {
            stationPulseOn = (bits & 1) != 0;
            skyOn = (bits & 2) != 0;
            weatherOn = (bits & 4) != 0;
            todayChipOn = (bits & 8) != 0;
            hexLayerOn = (bits & 16) != 0;
            subwayLinesOn = (bits & 32) != 0;
            subwayTownOn = (bits & 64) != 0;
            subwayLoreOn = (bits & 128) != 0;
            subwayClashOn = (bits & 256) != 0;
            roadLayerOn = (bits & 512) != 0;
            busLayerOn = (bits & 1024) != 0;
            placePanelOn = (bits & 2048) != 0;
            gradeNamesOn = (bits & 4096) != 0;
            headerGyeongOn = (bits & 8192) != 0;
            floatOn = (bits & FloatBit) != 0;
        }

        // 3차(2026-11-23 21:30 사용자: 「단위로 구분을 하는 게 좋지. 그 단위로 했을 때 어울리는 데이터들이 있을 거 아니야」).
        //  - 체크를 단위(0 구 · 1 타일 · 2 동)마다 따로 기억한다. 지도를 오르내리면 그 단위의 묶음으로 바뀐다(SyncDataUnit).
        //  - 모든 단위에 같은 것(GlobalBits): 타일 지도(지도 단계) · 홍달 플레이스 창 · 등급 이름 · 위 띠 경 수치.
        //  - 입장 카드는 세 단위의 묶음을 한꺼번에 정한다(SagajeongDispatchStance.cs).
        private const int GlobalBits = 16 | 2048 | 4096 | 8192;
        private static readonly string[] DataUnitNames = { "구", "타일", "동" };
        private static readonly string[] DataUnitKeys = { "gu", "tile", "dong" };
        private const string DataControlV3 = "hongdal.data.control.v3.";
        private readonly int[] dataUnitBits = new int[3];
        private readonly int[] dataUnitFacilities = new int[3];
        private int dataGlobalBits;
        private int dataUnit = -1;
        private bool dataProofHold;

        // OnGUI가 지도를 그리기 전에 부른다. 예전 보기 옵션 · 길버스 설정은 여기서 덮는다.
        private void ReadDataControl()
        {
            if (dataControlRead) return;
            dataControlRead = true;
            subwayOptionsRead = true;
            dataLayersRead = true;
            if (PlayerPrefs.HasKey(DataControlV3 + "global"))
            {
                dataGlobalBits = PlayerPrefs.GetInt(DataControlV3 + "global", DataDefaultBits & GlobalBits) & GlobalBits;
                for (var u = 0; u < 3; u++)
                {
                    dataUnitBits[u] = PlayerPrefs.GetInt(DataControlV3 + DataUnitKeys[u], DataDefaultBits) & ~GlobalBits;
                    dataUnitFacilities[u] = PlayerPrefs.GetInt(DataControlV3 + DataUnitKeys[u] + ".facilities", 0);
                }
            }
            else
            {
                // 2차(v2) 저장이 있으면 세 단위에 똑같이 옮긴다.
                var old = PlayerPrefs.GetInt(DataControlPreference, DataDefaultBits);
                var oldFacilities = PlayerPrefs.GetInt(FacilityMaskPreference, 0);
                dataGlobalBits = old & GlobalBits;
                for (var u = 0; u < 3; u++) { dataUnitBits[u] = old & ~GlobalBits; dataUnitFacilities[u] = oldFacilities; }
            }
            SetDataBits(dataUnitBits[0] | dataGlobalBits);
            facilityGroupMask = dataUnitFacilities[0];
        }

        // 지금 보는 단위. 타일은 구 지도에서 칸이 드러났을 때(들어갈 때 0.6 · 나올 때 0.3 — 경계에서 깜박이지 않게).
        private int CurrentDataUnit()
        {
            if (mapLevel == MapLevel.Dong) return 2;
            if (!GuMapVisible) return 0;
            var alpha = HexTilesAlpha;
            return dataUnit == 1 ? (alpha >= 0.3f ? 1 : 0) : (alpha >= 0.6f ? 1 : 0);
        }

        private void CaptureDataUnit()
        {
            if (dataUnit < 0) return;
            var bits = DataBits();
            dataUnitBits[dataUnit] = bits & ~GlobalBits;
            dataGlobalBits = bits & GlobalBits;
            dataUnitFacilities[dataUnit] = facilityGroupMask;
        }

        private void LoadDataUnit(int unit)
        {
            dataUnit = unit;
            SetDataBits(dataUnitBits[unit] | dataGlobalBits);
            facilityGroupMask = dataUnitFacilities[unit];
        }

        // OnGUI가 ReadDataControl 다음에 부른다. 단위가 바뀌면 그 단위의 묶음을 꺼낸다.
        private void SyncDataUnit()
        {
            if (dataProofHold) return;
            var unit = CurrentDataUnit();
            if (unit == dataUnit) return;
            CaptureDataUnit();
            LoadDataUnit(unit);
        }

        private void SaveDataControl()
        {
            CaptureDataUnit();
            PlayerPrefs.SetInt(DataControlV3 + "global", dataGlobalBits);
            for (var u = 0; u < 3; u++)
            {
                PlayerPrefs.SetInt(DataControlV3 + DataUnitKeys[u], dataUnitBits[u]);
                PlayerPrefs.SetInt(DataControlV3 + DataUnitKeys[u] + ".facilities", dataUnitFacilities[u]);
            }
            PlayerPrefs.Save();
        }

        private sealed class DataSnapshot
        {
            public int Bits, Facilities, Global, Unit;
            public int[] UnitBits, UnitFacilities;
            public string Stance;
        }

        // 촬영용: 저장하지 않고 잠깐 바꾼다(그동안 단위가 바뀌어도 묶음을 바꾸지 않는다). 돌려받은 값을 ProofDataPop에 준다.
        private object ProofDataPush(int bits, int facilities)
        {
            ReadDataControl();
            var keep = new DataSnapshot
            {
                Bits = DataBits(), Facilities = facilityGroupMask, Global = dataGlobalBits, Unit = dataUnit,
                UnitBits = (int[])dataUnitBits.Clone(), UnitFacilities = (int[])dataUnitFacilities.Clone(), Stance = stanceKey,
            };
            dataProofHold = true;
            SetDataBits(bits);
            facilityGroupMask = facilities;
            return keep;
        }

        private void ProofDataPop(object keep)
        {
            if (keep is not DataSnapshot snapshot) return;
            Array.Copy(snapshot.UnitBits, dataUnitBits, 3);
            Array.Copy(snapshot.UnitFacilities, dataUnitFacilities, 3);
            dataGlobalBits = snapshot.Global;
            dataUnit = snapshot.Unit;
            stanceKey = snapshot.Stance;
            SetDataBits(snapshot.Bits);
            facilityGroupMask = snapshot.Facilities;
            dataProofHold = false;
        }

        private static readonly Rect DataPanelScreen = new(22, 218, 648, 358);      // 위 업무 띠(아래 끝 213) 밑에서 시작
        private static readonly Rect DongDataButton = new(430, 596, 96, 28);
        private static readonly Rect DongDataPanelScreen = new(330, 212, 648, 378);
        private static readonly Rect DongLegendScreen = new(532, 596, 446, 28);

        private bool DongDataVisible => !mapOnly && expansionView && mapLevel == MapLevel.Dong && DioramaVisible &&
            dongManifest != null && dongSpread <= 0.01f && !navOpen;

        private bool DataPanelCovers(Vector2 screenPoint)
        {
            if (GuMapVisible) return dataPanelOpen && DataPanelScreen.Contains(screenPoint);
            if (!DongDataVisible) return false;
            return DongDataButton.Contains(screenPoint) || DongLegendScreen.Contains(screenPoint) ||
                   dataPanelOpen && DongDataPanelScreen.Contains(screenPoint);
        }

        // 구 지도 왼쪽 아래의 단추 줄과 자료 창. DrawGuMap이 부른다. 눌렸으면 true(그 누름은 지도로 넘기지 않는다).
        private bool DrawGuSubwayOptions()
        {
            if (PortraitScreen) return false;      // 세로 화면은 아래 판이 대신한다 (SagajeongDispatchPortrait.cs)
            ReadDataControl();
            var x = 22f - Field.x;
            var y = 600f - Field.y;
            var repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                Paint(new Rect(x - 6f, y - 20f, 370f, 52f), Fade(Canvas, 0.86f), 8);
                Text(new Rect(x, y - 18f, 360f, 16f), DataSummary(), 10, Soft);
            }
            if (ExpansionButton(new Rect(x, y, 96f, 26f), dataPanelOpen ? "자료 창 ▼" : "자료 창 ▲", true, dataPanelOpen ? Gold : Mint))
            {
                dataPanelOpen = !dataPanelOpen;
                return true;
            }
            if (repaint)
                Text(new Rect(x + 104f, y, 256f, 26f), roadLayerOn ? $"길 = 10월 평일 {GameSeconds() / 3600}시 · 평소보다 느릴수록 붉고 굵게" : skyOn && NightFactor() > 0.05f ? "밤 · 그날 점등 시각에 켜짐 · 길 불빛 자리는 표현" : "볼 것만 골라 켭니다", 10, Soft);
            var panel = new Rect(DataPanelScreen.x - Field.x, DataPanelScreen.y - Field.y, DataPanelScreen.width, DataPanelScreen.height);
            return dataPanelOpen && DrawDataPanel(panel, false);
        }

        // 동 디오라마: OnGUI가 두 번 부른다. 지도보다 먼저(input: 누르기를 먼저 받는다) · 창들 다음(그리기: 맨 위에 보인다).
        private void DrawDongDataControl(bool input)
        {
            if (!DongDataVisible) return;
            var repaint = Event.current.type == EventType.Repaint;
            if (input == repaint) return;
            if (repaint) DrawDongLegend();
            if (ExpansionButton(DongDataButton, dataPanelOpen ? "자료 창 ▼" : "자료 창 ▲", true, dataPanelOpen ? Gold : Mint))
            {
                dataPanelOpen = !dataPanelOpen;
                return;
            }
            if (dataPanelOpen) DrawDataPanel(DongDataPanelScreen, true);
            if (!repaint && Event.current.isMouse && (DongDataButton.Contains(Event.current.mousePosition) || DongLegendScreen.Contains(Event.current.mousePosition)))
                Event.current.Use();
        }

        private string DataSummary()
        {
            var parts = new List<string>();
            if (subwayLinesOn) parts.Add("노선");
            if (stationPulseOn) parts.Add("역 불빛");
            if (busLayerOn) parts.Add("버스");
            if (roadLayerOn) parts.Add("길");
            var groups = 0;
            for (var i = 0; i < FacilityGroupNames.Length; i++) if ((facilityGroupMask & (1 << i)) != 0) groups++;
            if (groups > 0) parts.Add($"시설 {groups}/{FacilityGroupNames.Length}");
            if (skyOn) parts.Add("낮밤");
            if (weatherOn) parts.Add("비눈");
            if (seasonOn) parts.Add("계절");
            if (previewDate.HasValue) parts.Add("미리 보기 " + DateLabel(previewDate.Value));
            if (hexLayerOn) parts.Add("타일");
            if (floatOn) parts.Add("유동인구");
            if (placePanelOn || subwayTownOn || subwayLoreOn || subwayClashOn || gradeNamesOn || headerGyeongOn) parts.Add("게임 요소");
            if (parts.Count == 0) return "켠 자료 · 없음";
            // 2026-11-24 정리: 줄 폭(360) 안에서 끊고 나머지는 「외 n」(예전엔 끝이 잘렸다)
            var line = "켠 자료 · " + parts[0];
            for (var index = 1; index < parts.Count; index++)
            {
                var next = line + " · " + parts[index];
                var rest = parts.Count - index - 1;
                if (GuNameSize(next + (rest > 0 ? $" 외 {rest}" : string.Empty), 10, false).x > 350f) return line + $" 외 {parts.Count - index}";
                line = next;
            }
            return line;
        }

        private bool DrawDataPanel(Rect panel, bool dong)
        {
            var current = Event.current;
            var over = panel.Contains(current.mousePosition);
            Paint(new Rect(panel.x - 1.5f, panel.y - 1.5f, panel.width + 3f, panel.height + 3f), Fade(Mint, 0.6f), 13);
            Paint(panel, Panel, 12);
            var x = panel.x + 14f;
            var y = panel.y + 10f;
            var unitName = dataUnit >= 0 ? DataUnitNames[dataUnit] : "구";
            Text(new Rect(x, y, 230f, 20f), $"자료 창 · {unitName} 단위", 14, Paper, true);
            if (ExpansionButton(new Rect(panel.xMax - 40f, panel.y + 8f, 28f, 24f), "×", true, Tile)) { dataPanelOpen = false; return true; }
            var changed = false;
            if (ExpansionButton(new Rect(panel.xMax - 112f, panel.y + 8f, 66f, 24f), "처음대로", true, Tile))
            {
                for (var u = 0; u < 3; u++) { dataUnitBits[u] = DataDefaultBits & ~GlobalBits; dataUnitFacilities[u] = 0; }
                dataGlobalBits = DataDefaultBits & GlobalBits;
                if (dataUnit >= 0) LoadDataUnit(dataUnit);
                changed = true;
            }
            var stanceName = StanceName;
            if (ExpansionButton(new Rect(panel.xMax - 250f, panel.y + 8f, 132f, 24f), stanceName == null ? "입장 고르기" : "입장 · " + stanceName, true, Tile))
            {
                stancePickerOpen = true;           // 시작 화면의 입장 카드를 다시 연다 (SagajeongDispatchStance.cs)
                return true;
            }
            changed |= DrawStanceOptionalControls(panel);      // 「부가도 켜기」 · 필수 · 부가 범례(SagajeongDispatchStanceInfo.cs)
            Text(new Rect(x, y + 20f, panel.width - 28f, 14f),
                (dong ? "흐린 줄은 구 지도에서 보입니다 · " : "") + $"이 체크는 {unitName} 단위에서만 씁니다(단위마다 따로 기억) · 「모든 단위」 줄은 같이", 10, Soft);

            var left = x;
            var right = x + 210f;
            var third = x + 420f;
            var row = y + 42f;
            DataHeader(left, ref row, "교통");
            changed |= DataCheck(left, ref row, "지하철 노선", ref subwayLinesOn, "노선 · 역", null, true, "metro-station");
            changed |= DataCheck(left, ref row, "역 불빛", ref stationPulseOn, "시간대 승하차", null, !dong, "transit-volume");
            changed |= DataCheck(left, ref row, "버스 노선", ref busLayerOn, "중랑 47노선", null, true, "bus-stop");
            changed |= DataCheck(left, ref row, "길 속도", ref roadLayerOn, "10월 평일 · 시각별", null, true, "road");
            DataHeader(left, ref row, "시간 · 날씨");
            changed |= DataCheck(left, ref row, "낮과 밤", ref skyOn, "그날 점등 시각", null, true, "street-light");
            changed |= DataCheck(left, ref row, "비 · 눈", ref weatherOn, "강우 · 적설 관측", null, true, "weather");
            changed |= DataCheck(left, ref row, "오늘 칩", ref todayChipOn, "날짜 · 날씨 요약", null, !dong, "calendar");
            changed |= DrawSeasonPreview(left, ref row, dong);      // 계절 바탕 · 날짜 미리 보기 (SagajeongDispatchEnvironment.cs)
            // 셋째 칸: 게임 요소. 처음엔 끈다. 켜고 싶은 사람만 켠다.
            var rowThird = y + 42f;
            DataHeader(third, ref rowThird, "게임 요소 · 원하면 켜기");
            changed |= DataCheck(third, ref rowThird, "등급 이름", ref gradeNamesOn, "모든 단위");
            changed |= DataCheck(third, ref rowThird, "위 띠 경 수치", ref headerGyeongOn, "모든 단위");
            changed |= DataCheck(third, ref rowThird, "홍달 플레이스 창", ref placePanelOn, "모든 단위", null, !dong);
            changed |= DataCheck(third, ref rowThird, "번화가", ref subwayTownOn, "운영 깊이만큼", null, !dong);
            changed |= DataCheck(third, ref rowThird, "내력 문양", ref subwayLoreOn, "역 이름 유래", null, !dong);
            changed |= DataCheck(third, ref rowThird, "빛의 번짐", ref subwayClashOn, "먹은 키워드만큼", null, !dong);
            Text(new Rect(third, rowThird + 2f, 200f, 46f),
                "끄면 등급은 「레벨 1 · 2 · 3 …」, 경 수치는 기사 빛을 눌렀을 때 오른쪽 상태창에만 나옵니다.", 9, Soft, wrap: true);
            // 현실감 시험(2026-11-24, SagajeongDispatchRealism.cs): 해 그늘 · 옥상 · 창 · 렌즈. 끄면 전과 똑같다.
            rowThird += 52f;
            DataHeader(third, ref rowThird, "그림 · 시험");
            ReadRealismPreference();
            var realismWas = realismOn;
            changed |= DataCheck(third, ref rowThird, "현실감 시험", ref realismOn, "해 그늘 · 옥상 · 창 · 렌즈", Ink(0xF2C35B), dong);
            if (realismWas != realismOn) SaveRealismPreference();
            if (realismOn && dong)          // 공표 자료와 연출을 나눠 적는다(제안 3절 범례)
                Text(new Rect(third, rowThird + 8f, 210f, 40f),
                    "자료: 보안등 33곳(2022) · 정류장 · 승하차 · 막힘\n연출: 차 · 사람 자리 · 재질 · 보도 높이", 9, Soft, wrap: true);
            // 사람(2026-11-24 13:25 「유동인구 … 음식점 사장님들이 좀 알면 좋겠다」): 행정동 생활인구, 구 지도에서
            rowThird += realismOn && dong ? 48f : 8f;
            DataHeader(third, ref rowThird, "사람 · 동네의 하루");
            changed |= DataCheck(third, ref rowThird, "유동인구", ref floatOn, "7월 · 행정동 · 시각별", null, !dong);

            var rowRight = y + 42f;
            var pack = EnsureFacilities();
            var totals = new int[FacilityGroupNames.Length];
            if (pack?.k != null) foreach (var key in pack.k) totals[FacilityGroup(pack.keys[key])]++;
            AddGroundTotals(totals);                 // 위치 확인 표식(횡단보도 · 따릉이, SagajeongDispatchMapIcons.cs)
            Text(new Rect(right, rowRight, 140f, 16f), "생활 시설 · 중랑 16동", 11, Gold, true);
            var all = facilityGroupMask == FacilityAllMask;
            if (ExpansionButton(new Rect(right + 148f, rowRight - 2f, 52f, 18f), all ? "모두 끔" : "모두 켬", true, Tile))
            {
                facilityGroupMask = all ? 0 : FacilityAllMask;
                changed = true;
            }
            rowRight += 20f;
            for (var i = 0; i < FacilityGroupNames.Length; i++)
            {
                var on = (facilityGroupMask & (1 << i)) != 0;
                var was = on;
                DataCheck(right, ref rowRight, FacilityGroupNames[i], ref on, $"{totals[i]:N0}곳", FacilityGroupColour(i), true, FacilityGroupIconKey(i));
                if (on != was) { facilityGroupMask ^= 1 << i; changed = true; }
            }
            DataHeader(right, ref rowRight, "지도");
            changed |= DataCheck(right, ref rowRight, "타일 지도", ref hexLayerOn, "모든 단위", null, !dong, "grid");
            Text(new Rect(right, rowRight + 2f, 200f, 46f),
                dong ? "모양 위에 마우스를 올리면 이름이 나옵니다. 시설 자리는 「근처에 있다고 보고된 곳」입니다."
                     : "시설 점은 타일이 떠오를 만큼 가까이 다가가면 보입니다. 「근처에 있다고 보고된 곳」이며 실물 위치가 아닙니다.", 9, Soft, wrap: true);
            Text(new Rect(x, panel.yMax - 20f, panel.width - 28f, 14f), "출처 · 기준일은 각 창 맨 아래 줄과 오늘 칩에 있습니다", 9, Soft);

            if (changed)
            {
                SaveDataControl();
                return true;
            }
            if (!over || current.type == EventType.Repaint || current.type == EventType.Layout) return false;
            if (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag || current.type == EventType.ScrollWheel)
            {
                current.Use();
                return true;
            }
            return false;
        }

        private void DataHeader(float x, ref float row, string title)
        {
            Text(new Rect(x, row, 170f, 16f), title, 11, Gold, true);
            row += 18f;
        }

        // 체크박스 한 줄: 네모 · 이름 · 작은 설명. 줄 어디를 눌러도 바뀐다. 바뀌었으면 true.
        private bool DataCheck(float x, ref float row, string label, ref bool value, string note, Color? tint = null, bool here = true, string icon = null)
        {
            var line = new Rect(x - 2f, row - 1f, 204f, 19f);
            var box = new Rect(x, row + 2f, 13f, 13f);
            var dim = here ? 1f : 0.45f;
            var tier = DataRowTier(label);      // 지금 입장의 필수(금빛) · 부가(푸른) 막대(SagajeongDispatchStanceInfo.cs)
            if (tier > 0) Paint(new Rect(x - 7f, row + 2f, 3f, 13f), Fade(tier == 1 ? Gold : OptionalTint, dim), 1.5f);
            Paint(box, Fade(Rim, dim), 3);
            Paint(new Rect(box.x + 1.5f, box.y + 1.5f, box.width - 3f, box.height - 3f), value ? Fade(tint ?? Mint, dim) : Canvas, 2);
            var labelX = x + 19f;
            if (icon != null && DrawMapIcon(new Rect(x + 18f, row + 1.5f, 14f, 14f), icon, Fade(tint ?? Paper, value ? dim : 0.45f * dim))) labelX += 17f;
            Text(new Rect(labelX, row, 131f - (labelX - x), 17f), label, 11, Fade(value ? Paper : Soft, here ? 1f : 0.6f));
            Text(new Rect(x + 116f, row, 84f, 17f), here ? note : "구 지도에서", 9, Fade(Soft, here ? 1f : 0.7f), false, TextAnchor.MiddleRight);
            row += 20f;
            if (!Hit(line)) return false;
            value = !value;
            return true;
        }

        private static Color FacilityGroupColour(int group) => group switch
        {
            0 => Ink(0xF4F7F8),
            1 => Ink(0x6FE3C4),
            3 => Ink(0x6FA8FF),
            4 => Ink(0x7CDB72),
            5 => Ink(0x6FCF97),
            6 => Ink(0xFFD395),
            7 => Ink(0xC9D08A),
            _ => Ink(0xABC1CB),
        };
    }
}
