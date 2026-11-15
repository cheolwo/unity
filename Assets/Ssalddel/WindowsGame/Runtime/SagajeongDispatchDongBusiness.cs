using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2026-11-14 행정동 깃발과 동 사업 — 화면 층. 사용자 방향: 영역은 행정동 깃발로 통일, 동마다 주문이 생기게.
    //  - 커널의 영역 모듈이 행정동 팩(unit = hjd)이면 켜진다. 예전 법정동 팩이면 이 파일은 아무것도 하지 않는다.
    //  - 동네 2.5D: 동의 밝기 = 영역 단계와 운영의 깊이. 깃발 꽂은 동에는 깃발이 서고, 이름표에 단계가 붙는다.
    //    동 이름표를 누르면 그 동에 맞추고, 위에 동 사업 칸이 뜬다(깃발 꽂기 · 기사 모집 · 제휴 · 배차 엔진).
    //  - 구 지도: 아직 법정동 판이다. 행정동 사본을 법정동으로 묶어 밝힌다(면목동 = 행정동 6개). 구 지도에서는 진출하지 않는다.
    //  - 주문·자금·비용은 모두 가상 값이다(동별 수요 팩). 음식 배달 Core의 주문·정산과 섞지 않는다.
    // 시계는 바꾸지 않는다. 명령은 커널에 보내고 사본만 읽는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string HjdRegionPrefix = "region:kr:hjd:";
        private const float DongTradeCardRoom = 96f;          // 맞춘 동 위에 동 사업 칸이 설 자리(px)
        private const float DongTradeCardWidth = 380f;
        private const float DongTradeCardHeight = 112f;
        // 지도를 돌려 보면 매장 이름표가 동의 위쪽 끝에 올 수 있다. 그때는 동 사업 칸을 이만큼 더 띄워 이름표와 겹치지 않게 한다.
        // 기본 방향에서는 0이라 화면이 전과 같다. 동을 화면에 맞출 때도 같은 만큼 자리를 더 남긴다(FocusDongModule).
        private const float DongTradeCardTurnedLift = 44f;
        // 동 사업 칸이 올라갈 수 있는 위쪽 한계: 위쪽 띠(인계 사슬 · 신호)가 떠 있으면 그 아래다. 띠가 없으면 전과 같이 140이다.
        private float DongTradeCardTop => Mathf.Max(140f,
            SignalBarVisible ? SignalBar.yMax + 6f : ChainStripVisible ? ChainStrip.yMax + 6f : 0f);
        private float DongTradeCardLift =>
            Mathf.Abs(Mathf.DeltaAngle((float)dioramaBaseYaw, (float)디오라마시점.DefaultYawDegrees)) < 1f &&
            Math.Abs(dioramaBasePitch - 디오라마시점.DefaultPitchDegrees) < 1.0 ? 0f : DongTradeCardTurnedLift;      // 바탕 시점으로 본다(가까이에서 눕는 각은 세지 않는다)

        private TerritorySnapshot dongTerritory;         // 행정동 영역 사본. 행정동 팩일 때만 있다
        private DongDemandSnapshot dongDemand;           // 동별 수요 사본
        private string dongLightSignature = string.Empty;
        private string dongNote;
        private bool dongNoteOk;
        private float dongNoteAt = -100f;
        private string dongCalendarSent;

        private bool DongFlagsActive => dongTerritory != null;

        // 영역 사본을 읽는다. 행정동 팩이면 원본을 dongTerritory에 두고, 구 지도에는 법정동으로 묶은 사본을 돌려준다.
        private TerritorySnapshot ReadTerritory()
        {
            var kernel = engine?.Kernel;
            var raw = kernel?.Find(BusinessIds.Territory)?.Snapshot() as TerritorySnapshot;
            dongTerritory = raw != null && raw.Unit == "hjd" ? raw : null;
            dongDemand = dongTerritory == null ? null : kernel.Find(BusinessIds.DongDemand)?.Snapshot() as DongDemandSnapshot;

            var signature = dongTerritory == null ? string.Empty : string.Join("|", dongTerritory.Dongs.Select(zone =>
                zone.Code + ":" + zone.Level + (zone.Candidate ? "c" : string.Empty) + ":" +
                Mathf.RoundToInt((float)(dongDemand?.Find(zone.Code)?.Depth ?? 0) * 50f)));
            if (signature != dongLightSignature)
            {
                dongLightSignature = signature;
                if (dongModules.Count > 0) ApplyDongDim();
            }
            PrimeDongSolo();     // 처음 한 번: 시작 동 하나만 보이게 (SagajeongDispatchDongSolo.cs)
            return dongTerritory == null ? raw : LegalTerritory(dongTerritory);
        }

        // 오늘의 요일을 동별 수요에 알린다(현실 달력, 하루에 한 번). 화면 알림은 건드리지 않는다.
        private void SyncDongCalendar()
        {
            if (dongDemand == null || engine?.Kernel == null) return;
            var today = DateTime.Now;
            var key = today.ToString("yyyy-MM-dd");
            if (dongDemand.DateKey == key || dongCalendarSent == key) return;
            dongCalendarSent = key;
            engine.Business(new GameCommand
            {
                OsId = BusinessIds.DongDemand, Action = DongDemandActions.Calendar,
                CommandId = "calendar-" + key, ElapsedMs = BusinessNow,
            }.With("weekday", (decimal)(((int)today.DayOfWeek + 6) % 7)).With("date", key));
        }

        // 구 지도는 아직 법정동 판이다. 행정동 사본을 법정동으로 묶는다. 묶음에 없는 법정동은 미지로 둔다.
        private TerritorySnapshot LegalTerritory(TerritorySnapshot raw)
        {
            var view = new TerritorySnapshot
            {
                Unit = "bjd-from-hjd", MaxEntering = raw.MaxEntering, NowMs = raw.NowMs,
                LastNote = raw.LastNote, LastNoteAtMs = raw.LastNoteAtMs,
                Operating = raw.Operating, Entering = raw.Entering,
                Branches = raw.Branches, Foods = raw.Foods, Cafes = raw.Cafes, Stores = raw.Stores,
            };
            var groups = new Dictionary<string, List<TerritoryDongView>>(StringComparer.Ordinal);
            foreach (var zone in raw.Dongs)
            {
                if (string.IsNullOrEmpty(zone.Legal)) continue;
                if (!groups.TryGetValue(zone.Legal, out var list)) groups[zone.Legal] = list = new List<TerritoryDongView>();
                list.Add(zone);
            }
            var done = new HashSet<string>(StringComparer.Ordinal);
            if (guSpace?.dongs != null)
            {
                foreach (var legal in guSpace.dongs)
                {
                    groups.TryGetValue(legal.code, out var children);
                    var sigName = guSpace.sigs?.FirstOrDefault(item => item.code == legal.sig)?.name;
                    view.Dongs.Add(LegalZone(legal.code, legal.name, legal.sig, sigName, legal.x, legal.y, children));
                    done.Add(legal.code);
                }
            }
            foreach (var pair in groups)
            {
                if (!done.Add(pair.Key)) continue;
                var first = pair.Value[0];
                view.Dongs.Add(LegalZone(pair.Key, pair.Key, first.Sig, first.SigName,
                    pair.Value.Average(item => item.X), pair.Value.Average(item => item.Y), pair.Value));
            }
            return view;
        }

        private static TerritoryDongView LegalZone(string code, string name, string sig, string sigName, double x, double y,
            List<TerritoryDongView> children)
        {
            var zone = new TerritoryDongView
            {
                Code = code, Name = name ?? code, Sig = sig ?? string.Empty, SigName = sigName ?? string.Empty, X = x, Y = y,
                EnterBlock = "행정동 깃발로 진출합니다. 이 동의 행정동 자료는 아직 준비 중입니다.",
            };
            if (children == null || children.Count == 0) return zone;
            var flagged = children.Count(item => item.Level >= TerritoryLevels.Entering);
            var operating = children.Count(item => item.Level >= TerritoryLevels.Operating);
            var settled = children.Count(item => item.Level >= TerritoryLevels.Settled);
            zone.Level = settled == children.Count ? TerritoryLevels.Settled : operating == children.Count ? TerritoryLevels.Operating :
                flagged > 0 ? TerritoryLevels.Entering : TerritoryLevels.Unknown;
            zone.IsHome = children.Any(item => item.IsHome);
            zone.Progress = zone.Level >= TerritoryLevels.Settled ? 1 : zone.Level == TerritoryLevels.Operating
                ? settled / (double)children.Count : operating / (double)children.Count;
            zone.EnterBlock = "행정동 깃발로 진출합니다. 동네 지도에서 깃발을 꽂으세요. (깃발 " + flagged + " / " + children.Count + "개 행정동)";
            zone.LevelAtMs = children.Max(item => item.LevelAtMs);
            zone.DimmedAtMs = children.Max(item => item.DimmedAtMs);
            foreach (var child in children)
            {
                zone.Pins.AddRange(child.Pins);
                if (child.Signal != null && (zone.Signal == null || child.Signal.IsDanger && !zone.Signal.IsDanger)) zone.Signal = child.Signal;
            }
            return zone;
        }

        private static string DongCodeOf(string regionId) =>
            regionId != null && regionId.StartsWith(HjdRegionPrefix, StringComparison.Ordinal) ? regionId.Substring(HjdRegionPrefix.Length) : null;

        private TerritoryDongView DongLand(string regionId)
        {
            var code = DongCodeOf(regionId);
            return code == null ? null : dongTerritory?.Find(code);
        }

        private DongDemandDongView DongTrade(string regionId)
        {
            var code = DongCodeOf(regionId);
            return code == null ? null : dongDemand?.Find(code);
        }

        // 동의 밝기(표현값). 팩에 없는 동은 어둡게 둔다.
        private float DongBrightness(DongModuleState module)
        {
            var land = DongLand(module.Entry.regionId);
            if (land == null) return 행정동모듈Policy.DarkLight;
            var trade = DongTrade(module.Entry.regionId);
            return 행정동모듈Policy.Brightness(land.Level, land.Candidate, trade == null ? 0 : trade.Depth);
        }

        private static string DongStageName(TerritoryDongView land) =>
            land.Level >= TerritoryLevels.Settled ? "자리 잡음" : land.Level == TerritoryLevels.Operating ? "운영" :
            land.Level == TerritoryLevels.Entering ? "깃발" : land.Candidate ? "진출 가능" : "미진출";

        // 동 이름표의 글과 색. flagged: 깃발을 세울 동인가.
        // 2026-11-14 첫 화면 정리: 글은 이름만 쓴다. 단계는 밝기·깃발·테두리 색이 알리고, 자세한 것은 동을 눌러 동 사업 칸에서 본다.
        private string DongPlate(DongModuleState module, out Color tone, out Color rim, out bool flagged)
        {
            tone = Paper;
            rim = Color.clear;
            flagged = false;
            var land = DongLand(module.Entry.regionId);
            if (land == null) return module.Entry.name;
            flagged = land.Level >= TerritoryLevels.Entering;
            if (land.Signal != null)
            {
                rim = SignalColor(land.Signal);
                return module.Entry.name;
            }
            if (flagged) rim = Mint;
            else if (land.Candidate)
            {
                rim = Gold;
                tone = Gold;
            }
            else tone = Fade(Soft, 0.75f);
            return module.Entry.name;
        }

        // 깃발: 깃대와 깃. GUI.BeginGroup(Field) 안의 좌표.
        private static void DrawDongFlag(Vector2 foot)
        {
            Paint(new Rect(foot.x - 5, foot.y - 2, 10, 4), Fade(Canvas, 0.75f), 2);
            Paint(new Rect(foot.x - 1, foot.y - 28, 2, 28), Paper);
            Paint(new Rect(foot.x + 1, foot.y - 28, 14, 9), Gold, 2);
        }

        // 지도 영역 안의 단추. 지도가 누름을 먼저 받으므로 HitMapMarker로 가린다.
        private bool DongCardButton(Rect rect, string label, bool enabled, Color background)
        {
            Paint(rect, enabled ? background : Tile, 6);
            Text(rect, label, 11, enabled ? Canvas : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
            return HitMapMarker(rect) && enabled;
        }

        private void SendDong(string osId, string action, string code)
        {
            if (engine?.Kernel == null) return;
            var result = SendBusiness(osId, action, command =>
            {
                if (!string.IsNullOrEmpty(code)) command.With("dong", code);
            });
            dongNote = result.Message;
            dongNoteOk = result.Ok;
            dongNoteAt = Time.unscaledTime;
            // 시계가 멈춰 있어도 결과가 바로 보이게 사본을 다시 읽는다.
            territory = ReadTerritory();
            signals = engine.Kernel.Signals.OrderByDescending(item => item.IsDanger).ThenBy(item => item.SinceMs).ToList();
            Debug.Log($"Hongdal dong business: {action} {code} → {(result.Ok ? "ok" : "blocked")} · {result.Message}");
        }

        // 신호 줄의 단추: 기사가 모자라면 모집, 제휴가 모자라면 제휴.
        private void SendDongSignalAction(Signal signal, string code)
        {
            SendDong(BusinessIds.DongDemand,
                signal.ReasonCode == DongDemandBottlenecks.Partner ? DongDemandActions.Contract : DongDemandActions.Recruit, code);
        }

        // 신호의 "가서 보기": 행정동 깃발이 켜져 있으면 동네 2.5D에서 그 동에 맞춘다.
        private bool FocusDongFlag(string code)
        {
            return DongFlagsActive && dongTerritory.Find(code) != null && FocusDongModule(HjdRegionPrefix + code);
        }

        private string DongLegalOf(string code)
        {
            var legal = dongTerritory?.Find(code)?.Legal;
            return string.IsNullOrEmpty(legal) ? code : legal;
        }

        private string DongBasisNote()
        {
            if (dongDemand == null) return string.Empty;
            var population = dongDemand.PopulationBasis == "floatingPopulation" ? "유동인구" :
                dongDemand.PopulationBasis == "residentialFloorArea" ? "주거 연면적" : "임시";
            var income = dongDemand.IncomeBasis == "monthlyIncome" ? "소득" : "소득 자료 전";
            return "바탕: " + population + " · " + income;
        }

        // 맞춘 동 위의 동 사업 칸. GUI.BeginGroup(Field) 안의 좌표. 그렸으면 true.
        private bool DrawDongBusinessCard(DongModuleState focus, Vector2 top)
        {
            var land = DongLand(focus.Entry.regionId);
            if (land == null) return false;
            if (PlaceVisible) return true;          // 홍달 플레이스 주문자 뷰: 지도 위의 동 사업 칸 · 이름 칸을 거둔다 (SagajeongDispatchPlace.cs)
            // 동 관리 창이 이 동을 말하고 있으면 지도 위의 칸은 거둔다. 동 이름표와 깃발은 남는다 (SagajeongDispatchDongDesk.cs).
            if (DongDeskVisible && BusinessDong()?.Entry.regionId == focus.Entry.regionId) return true;
            if (CloseNow > 0.5f) return true;       // 가까이 보기: 건물을 보는 중에는 지도 위의 칸을 거둔다. 동 이름표와 깃발은 남는다
            var trade = DongTrade(focus.Entry.regionId);
            var flagged = land.Level >= TerritoryLevels.Entering;
            var signal = land.Signal;
            // 양옆에 동을 넘기는 화살표가 설 자리(36)를 남긴다. 오른쪽 칸(건물·기사·설정·결정 카드)이 열려 있으면 그 왼쪽에 둔다.
            var rightLimit = selectedBuilding != null || actorFocus != null || dongPanelOpen || zonePanelOpen || SettingsVisible || DailyReportVisible || CycleCardVisible
                ? ExpansionPanel.x - Field.x - 8f : Field.width;
            var card = new Rect(Mathf.Clamp(top.x - DongTradeCardWidth / 2, 36, Mathf.Max(36, rightLimit - DongTradeCardWidth - 36)),
                Mathf.Max(top.y - DongTradeCardHeight - 8 - DongTradeCardLift, DongTradeCardTop - Field.y), DongTradeCardWidth, DongTradeCardHeight);
            var edge = signal != null ? SignalColor(signal) : flagged ? Mint : land.Candidate ? Gold : Rim;
            Paint(card, edge, 9);
            Paint(new Rect(card.x + 1.5f, card.y + 1.5f, card.width - 3, card.height - 3), Canvas, 8);

            if (DrawDongSoloArrows(card)) return true;

            // 1줄: 이름 · 단계 — 자금
            Text(new Rect(card.x + 12, card.y + 6, 230, 20), focus.Entry.name + " · " + DongStageName(land), 13, Paper, true);
            if (dongDemand != null)
                Text(new Rect(card.xMax - 162, card.y + 6, 150, 20), "자금 " + dongDemand.Funds.ToString("N0") + "원 (가상)", 11, Gold, true,
                    TextAnchor.MiddleRight);

            // 2줄: 지금 상태
            string line;
            if (!flagged)
                line = land.Candidate ? "맞닿은 동입니다. 깃발을 꽂으면 이 동에 주문이 들어오기 시작합니다." : land.EnterBlock;
            else if (trade != null)
                line = "기사 " + trade.Riders + "명 · 제휴 " + trade.Partners + "/" + trade.PartnerLimit + "곳 · 지금 주문 " +
                    trade.RateNow.ToString("0.0") + "건/시간 · 최근 받아냄 " + Mathf.RoundToInt((float)(1 - trade.UnmetRatio) * 100f) + "%";
            else
                line = TerritoryLevels.Name(land.Level);
            Text(new Rect(card.x + 12, card.y + 28, card.width - 24, 18), line, 11, Soft);

            // 3줄: 방금 한 일 → 신호 → 근거
            var recent = Time.unscaledTime - dongNoteAt < 4f && !string.IsNullOrEmpty(dongNote);
            var third = recent ? dongNote : signal != null ? signal.Message :
                trade != null ? "하루 주문 " + trade.DailyOrders.ToString("0") + "건(가상) · 음식점 " + trade.Restaurants + "곳 · " + DongBasisNote()
                : dongManifest?.label ?? "내부 검토용";
            Text(new Rect(card.x + 12, card.y + 47, card.width - 24, 16), third, 10,
                recent ? (dongNoteOk ? Mint : SignalRed) : signal != null ? SignalColor(signal) : Fade(Soft, 0.8f));

            // 4줄: 단추
            var row = new Rect(card.x + 10, card.y + 72, card.width - 20, 30);
            if (!flagged)
            {
                var label = land.CanEnter ? "깃발 꽂기 · " + focus.Entry.name : land.Candidate ? "아직 꽂을 수 없습니다" : "아직 닿지 않습니다";
                if (DongCardButton(row, label, land.CanEnter, Gold)) SendDong(BusinessIds.Territory, "enter", land.Code);
            }
            else if (DongDeskReady)
            {
                // 고용 · 건설 · 엔진은 왼쪽의 동 관리 창으로 갔다. 여기에는 그 창을 여는 단추 하나만 둔다.
                if (DongCardButton(row, "동 관리 열기 · 고용 · 건설", true, Mint)) OpenDongDesk(focus.Entry.regionId);
            }
            else if (trade != null && dongDemand != null)
            {
                var width = (row.width - 12) / 3f;
                var recruit = new Rect(row.x, row.y, width, row.height);
                var contract = new Rect(row.x + width + 6, row.y, width, row.height);
                var upgrade = new Rect(row.x + 2 * (width + 6), row.y, width, row.height);
                // 행정동 = 도시(사용자 방향 2026-11-14): 사람을 들이는 일은 「고용」, 거점을 늘리는 일은 「건설」로 부른다.
                if (DongCardButton(recruit, "고용 · 기사 " + dongDemand.RecruitCost.ToString("N0"), trade.CanRecruit, Mint))
                    SendDong(BusinessIds.DongDemand, DongDemandActions.Recruit, land.Code);
                if (DongCardButton(contract, "건설 · 제휴 " + dongDemand.ContractCost.ToString("N0"), trade.CanContract, Mint))
                    SendDong(BusinessIds.DongDemand, DongDemandActions.Contract, land.Code);
                var top5 = dongDemand.EngineLevel >= dongDemand.MaxEngineLevel;
                if (DongCardButton(upgrade, top5 ? "엔진 Lv" + dongDemand.EngineLevel : "엔진 Lv" + (dongDemand.EngineLevel + 1) + " " +
                        dongDemand.EngineCost.ToString("N0"), dongDemand.CanUpgrade, Gold))
                    SendDong(BusinessIds.DongDemand, DongDemandActions.Engine, null);
            }
            // 칸 안의 빈 곳을 눌러도 아래 건물이 골라지지 않게 한다. 두 번 누르면 동 관리 창이 열린다.
            if (HitMapMarker(card) && mapDoubleClick) OpenDongDesk(focus.Entry.regionId);
            return true;
        }
    }
}
