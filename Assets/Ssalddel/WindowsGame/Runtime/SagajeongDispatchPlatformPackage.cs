using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 배달 플랫폼 패키지 뼈대 (2026-11-16, 사용자: 「일단 뼈대 정도 한번 구현 … 시안 정해준 것대로 큰 틀」).
    //  - 패키지 고르기(시안 P0): 처음 한 번, 또는 아래 줄 왼쪽의 「배달 플랫폼 ▼」을 누르면 뜬다. 고른 것은 표현 취향처럼 PlayerPrefs에 둔다
    //    (방향 촬영 · 검증 촬영에서는 읽지도 쓰지도 않는다). 업무 저장(confirmed-session)은 건드리지 않는다.
    //  - 아래 줄(시안 P1): 지사 · 기사 · 배차 · 제휴 매장 · 정산 · 본사 장부. 지사만 새 화면이고 나머지는 지금 있는 창으로 간다.
    //    「모든 사업 보기」를 누르면 지금까지의 아래 줄로 돌아가고, 맨 왼쪽 「◀ 배달 플랫폼」으로 다시 온다.
    //  - 본사 띠(시안 P1 위): 첫 인계 사슬 띠 자리에 본사 숫자와 오늘 정할 일을 쓴다.
    //  - 지사 쪽(시안 P2 뼈대): 플랫폼 장부 칸 자리에서 지사 = 지금 운영 중인 행정동 묶음을 보여 준다.
    //  - 지사는 아직 커널의 단위가 아니다(Kernel 변경 없음). 동별 수요 사본을 묶어 읽기만 하고, 명령은 지금 있는 창(동 관리 · 배차)으로 보낸다.
    //  - 모르는 값은 「자료 준비 전」· 「다음 단계」로 쓴다. 0으로 채우지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private 패키지종류 패키지;
        private bool 패키지저장가능;
        private bool 패키지고르기열림;
        private bool 모든사업보기;
        private int 본사쪽;                       // 0 = 지사, 1 = 본사 장부(지금의 플랫폼 장부)
        private DongDemandPack 플랫폼수요팩;
        private bool 플랫폼수요팩읽음;
        private DongDemandSnapshot 플랫폼수요캐시;
        private 지사요약 지사캐시;
        private int[] 시간대필요캐시;
        private float 플랫폼조회시각 = -1f;

        private bool 플랫폼패키지 => TidyScreen && 패키지 == 패키지종류.배달플랫폼;
        private bool 플랫폼줄표시 => 플랫폼패키지 && !모든사업보기;
        private bool 패키지고르기표시 => TidyScreen && 패키지고르기열림;
        private float 플랫폼돌아가기폭 => 플랫폼패키지 && 모든사업보기 ? 118f : 0f;

        private void LoadPackage()
        {
            패키지저장가능 = Array.TrueForAll(Environment.GetCommandLineArgs(), value =>
                !value.StartsWith("--sagajeong-", StringComparison.Ordinal) && !value.StartsWith(ViewProofFlag, StringComparison.Ordinal));
            if (!패키지저장가능)
            {
                패키지 = 패키지종류.없음;
                return;
            }
            패키지 = 배달플랫폼Policy.읽기(PlayerPrefs.GetString(배달플랫폼Policy.저장키, string.Empty));
            패키지고르기열림 = TidyScreen && !PlayerPrefs.HasKey(배달플랫폼Policy.저장키);      // 처음 한 번만 묻는다
        }

        private void 패키지정하기(패키지종류 종류)
        {
            패키지 = 종류;
            패키지고르기열림 = false;
            모든사업보기 = false;
            if (패키지저장가능)
            {
                PlayerPrefs.SetString(배달플랫폼Policy.저장키, 종류 == 패키지종류.없음 ? "all" : 배달플랫폼Policy.저장(종류));
                PlayerPrefs.Save();
            }
            Debug.Log("Hongdal package: " + (배달플랫폼Policy.정의(종류)?.이름 ?? "모든 사업 보기"));
        }

        // ── 자료 ────────────────────────────────────────────────────────────────

        private DongDemandPack 수요팩()
        {
            if (플랫폼수요팩읽음) return 플랫폼수요팩;
            플랫폼수요팩읽음 = true;
            var asset = Resources.Load<TextAsset>(HongdalKernelHost.DongDemandPackResource);
            if (asset == null) return null;
            try
            {
                플랫폼수요팩 = DongDemandPack.Parse(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hongdal package: 동 수요표를 읽지 못했습니다. " + exception.Message);
            }
            Resources.UnloadAsset(asset);
            return 플랫폼수요팩;
        }

        // 화면이 여러 곳에서 읽으므로 0.5초에 한 번만 다시 묶는다. 업무 상태는 읽기만 한다.
        private void 플랫폼자료갱신()
        {
            if (Time.unscaledTime - 플랫폼조회시각 < 0.5f && 플랫폼조회시각 >= 0f) return;
            플랫폼조회시각 = Time.unscaledTime;
            플랫폼수요캐시 = ModuleSnapshot<DongDemandSnapshot>(BusinessIds.DongDemand);
            지사캐시 = 플랫폼수요캐시 == null ? null : 배달플랫폼Policy.지사묶기(플랫폼수요캐시.Dongs.Select(dong => new 플랫폼동줄
            {
                코드 = dong.Code, 이름 = dong.Name, 운영 = dong.Open, 기사 = dong.Riders, 제휴 = dong.Partners,
                하루주문 = dong.DailyOrders, 놓친비율 = dong.UnmetRatio,
            }));
            시간대필요캐시 = 시간대필요(지사캐시, 플랫폼수요캐시);
        }

        // 지사의 시간대별 필요 기사. 동 수요표의 시간대 몫 × 지금 하루 주문. 팩이 없거나 시간대가 다르면 null.
        private int[] 시간대필요(지사요약 지사, DongDemandSnapshot demand)
        {
            var pack = 수요팩();
            var bands = 배달플랫폼Policy.시간대;
            if (pack == null || 지사 == null || pack.bandHours == null || pack.bandHours.Count != bands.Length + 1) return null;
            var boost = demand != null && demand.EngineBoost > 0 ? demand.EngineBoost : 1.0;
            var result = new int[bands.Length];
            for (var i = 0; i < bands.Length; i++)
            {
                double orders = 0;
                foreach (var 동 in 지사.담당)
                {
                    var info = pack.dongs.FirstOrDefault(item => item.code == 동.코드);
                    if (info?.bands == null || info.bands.Count <= i) continue;
                    orders += 동.하루주문 * info.bands[i];
                }
                result[i] = 배달플랫폼Policy.필요기사(orders, 1.0, pack.bandHours[i + 1] - pack.bandHours[i], pack.rules.riderOrdersPerHour, boost);
            }
            return result;
        }

        private int 저녁필요 => 시간대필요캐시 == null ? 0 : 시간대필요캐시[4];

        // 담당 동 가운데 최근 놓친 주문의 막힘이 제휴(조리 여력)인 동의 수
        private int 제휴부족동 => 플랫폼수요캐시 == null || 지사캐시 == null ? 0 :
            지사캐시.담당.Count(dong => 플랫폼수요캐시.Find(dong.코드)?.Bottleneck == DongDemandBottlenecks.Partner);

        private string 누계차액글 => 플랫폼수요캐시 == null ? "자료 준비 전" :
            (플랫폼수요캐시.Revenue - 플랫폼수요캐시.RiderPay - 플랫폼수요캐시.Spent).ToString("+#,0;-#,0;0") + "원";

        // ── 아래 줄 ─────────────────────────────────────────────────────────────

        private void 플랫폼줄그리기()
        {
            플랫폼자료갱신();
            Paint(ExpansionRail, Canvas, 10);
            var root = new Rect(ExpansionRail.x + 8, ExpansionRail.y + 6, 66, 32);     // 00 경영: 오행에서 위→아래로 (SagajeongDispatch오행경영.cs)
            Paint(root, 경영판보임 ? Mint : Gold, 7);
            Text(root, "00 경영", 12, Canvas, true, TextAnchor.MiddleCenter);
            if (Hit(root))
            {
                if (경영판보임) 경영판열림 = false;
                else 경영판열기();
            }
            var chip = new Rect(root.xMax + 6, ExpansionRail.y + 6, 108, 32);
            Paint(chip, Mint, 7);
            Text(chip, "배달 플랫폼 ▼", 12, Canvas, true, TextAnchor.MiddleCenter);
            if (Hit(chip)) 패키지고르기열림 = true;
            var other = new Rect(ExpansionRail.xMax - 124, ExpansionRail.y + 6, 116, 32);
            var left = chip.xMax + 8;
            var stride = (other.x - 8 - left) / 배달플랫폼Policy.플랫폼메뉴.Length;
            for (var index = 0; index < 배달플랫폼Policy.플랫폼메뉴.Length; index++)
            {
                var tab = new Rect(left + index * stride, ExpansionRail.y + 6, stride - 6, 32);
                var on = 플랫폼탭켜짐(index);
                Paint(tab, on ? Mint : Ink(0x2A4A4A), 7);
                Paint(new Rect(tab.x + 1, tab.y + 1, tab.width - 2, tab.height - 2), on ? Ink(0x183236) : Ink(0x13202C), 6);
                Text(new Rect(tab.x + 12, tab.y, 80, tab.height), 배달플랫폼Policy.플랫폼메뉴[index], 13, Paper, true);
                Text(new Rect(tab.x + 70, tab.y, tab.width - 80, tab.height), 플랫폼탭글(index), 10, Soft, align: TextAnchor.MiddleRight);
                if (Hit(tab)) 플랫폼탭누름(index);
            }
            if (ExpansionButton(other, "모든 사업 보기", true, Tile)) 모든사업보기 = true;
        }

        // 「모든 사업 보기」일 때 지금까지의 아래 줄 맨 왼쪽에 둔다 (SagajeongDispatchExpansion.cs DrawExpansionRail)
        private void 플랫폼돌아가기그리기()
        {
            if (플랫폼돌아가기폭 <= 0f) return;
            var back = new Rect(ExpansionRail.x + 8, ExpansionRail.y + 6, 110, 32);
            if (ExpansionButton(back, "◀ 배달 플랫폼", true, Mint)) 모든사업보기 = false;
        }

        private bool 플랫폼탭켜짐(int index) => index switch
        {
            0 => 플랫폼장부표시 && 본사쪽 == 0,
            1 => ExpansionPanelVisible && expansionSelection == BusinessFood,
            2 => showDesk,
            3 => ExpansionPanelVisible && expansionSelection == BusinessRestaurant,
            4 => DailyReportVisible,
            5 => 플랫폼장부표시 && 본사쪽 == 1,
            _ => false,
        };

        private string 플랫폼탭글(int index)
        {
            var 지사 = 지사캐시;
            return index switch
            {
                0 => 지사 == null ? "자료 준비 전" : 지사.담당.Count == 0 ? "운영 동 없음" : $"1곳 · 동 {지사.담당.Count}",
                1 => 지사 == null ? "–" : $"{지사.기사}명",
                2 => state?.orders == null ? "–" : $"대기 {state.orders.Count(order => order?.stage == "Waiting")}",
                3 => 지사 == null ? "–" : $"{지사.제휴}곳",
                4 => "오늘",
                5 => "가상",
                _ => string.Empty,
            };
        }

        private void 플랫폼탭누름(int index)
        {
            switch (index)
            {
                case 0:
                case 5:
                    var 쪽 = index == 0 ? 0 : 1;
                    if (플랫폼장부표시 && 본사쪽 == 쪽) 플랫폼장부열림 = false;
                    else
                    {
                        본사쪽 = 쪽;
                        플랫폼장부열기();
                    }
                    break;
                case 1:
                    SelectExpansion(BusinessFood);
                    DongDeskShortcut(BusinessFood);           // 기사 고용은 동 관리 창에서 한다
                    break;
                case 2:
                    ToggleDesk();                              // 지금의 배차 데스크
                    break;
                case 3:
                    SelectExpansion(BusinessRestaurant);
                    DongDeskShortcut(BusinessRestaurant);     // 제휴 음식점은 동 관리 창의 건설 묶음
                    break;
                case 4:
                    ToggleDailyReport();
                    break;
            }
        }

        // ── 본사 띠 (첫 인계 사슬 띠 자리) ──────────────────────────────────────

        private void 본사띠그리기(Rect strip)
        {
            플랫폼자료갱신();
            var 지사 = 지사캐시;
            Paint(strip, Mint, 10);
            Paint(new Rect(strip.x + 1, strip.y + 1, strip.width - 2, strip.height - 2), Ink(0x0B131C), 9);
            // 위 줄: 동네 높이에서는 띠가 이어 보기 띠(왼쪽 위)와 같은 높이에 오므로 그 오른쪽에서 시작한다
            var x0 = strip.y < 124f ? Mathf.Max(strip.x + 12, 362f) : strip.x + 12;
            Text(new Rect(x0, strip.y + 6, 132, 22), "본사 · 배달 플랫폼", 13, Paper, true);
            var todo = 배달플랫폼Policy.정할일(지사, 저녁필요, 제휴부족동);
            Text(new Rect(x0 + 136, strip.y + 6, 80, 22), "오늘 정할 일", 11, Gold, true);
            Text(new Rect(x0 + 210, strip.y + 6, strip.xMax - x0 - 218, 22),
                지사 == null ? "동별 수요 자료 준비 전" : todo.Count == 0 ? "지금은 막힌 것이 없어요" : string.Join("  ·  ", todo), 11,
                todo.Count > 0 && todo[0].Contains("부족") ? Red : Paper);
            // 아래 줄: 숫자 네 칸
            var cells = new (string 이름, string 값, Color 색)[]
            {
                ("지사", 지사 == null ? "자료 준비 전" : 지사.담당.Count == 0 ? "운영 동 없음" : $"1곳 · 동 {지사.담당.Count}", Paper),
                ("소속 기사", 지사 == null ? "자료 준비 전" : $"{지사.기사}명", Paper),
                ("최근 받아냄", 배달플랫폼Policy.비율글(지사?.받아냄), Mint),
                ("누계 차액 · " + (state?.campaign == null ? "사가정 권역" : $"{state.campaign.day}일차"), 누계차액글, Gold),
            };
            var x = strip.x + 8;
            var cell = (strip.width - 16) / cells.Length;
            for (var i = 0; i < cells.Length; i++)
            {
                var box = new Rect(x + i * cell, strip.y + 34, cell - 6, 38);
                Paint(box, Ink(0x142130), 6);
                Text(new Rect(box.x + 9, box.y + 3, box.width - 14, 13), cells[i].이름, 9, Soft);
                Text(new Rect(box.x + 9, box.y + 17, box.width - 14, 19), cells[i].값, 14, cells[i].색, true);
            }
        }

        // ── 지사 쪽 (플랫폼 장부 칸 자리) ───────────────────────────────────────

        private void 지사쪽그리기()
        {
            플랫폼자료갱신();
            var 지사 = 지사캐시;
            var panel = 플랫폼장부칸;
            Paint(panel, Rim, 10);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 9);
            var x = panel.x + 12;
            var width = panel.width - 24;
            Text(new Rect(x, panel.y + 10, 150, 26), 지사?.이름 ?? 배달플랫폼Policy.첫지사이름, 19, Paper, true);
            var badge = new Rect(x + 132, panel.y + 15, 44, 18);
            Paint(badge, 지사 != null && 지사.담당.Count > 0 ? Mint : Tile, 9);
            Text(badge, 지사 != null && 지사.담당.Count > 0 ? "운영" : "준비 전", 10, Canvas, true, TextAnchor.MiddleCenter);
            if (ExpansionButton(new Rect(panel.xMax - 51, panel.y + 12, 39, 23), "닫기", true, Tile))
                플랫폼장부열림 = false;
            if (지사 == null)
            {
                Text(new Rect(x, panel.y + 48, width, 60), "동별 수요 자료 준비 전 · 행정동 팩이 있는 실행판에서 지사를 묶어 보여 줍니다.", 11, Soft, wrap: true);
                return;
            }
            Text(new Rect(x, panel.y + 38, width, 32),
                지사.담당.Count == 0 ? "운영 중인 동이 없어요" : "담당 " + string.Join(" · ", 지사.담당.Select(dong => dong.이름)), 10, Soft, wrap: true);

            // 숫자 네 칸
            var tiles = new (string, string, Color)[]
            {
                ("소속 기사", $"{지사.기사}", Paper), ("제휴 매장", $"{지사.제휴}", Paper),
                ("하루 주문", $"{지사.하루주문:0}", Paper), ("최근 받아냄", 배달플랫폼Policy.비율글(지사.받아냄), Mint),
            };
            var tileWidth = (width + 6) / tiles.Length;
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = new Rect(x + i * tileWidth, panel.y + 70, tileWidth - 6, 38);
                Paint(tile, Tile, 7);
                Text(new Rect(tile.x + 7, tile.y + 3, tile.width - 10, 13), tiles[i].Item1, 9, Soft);
                Text(new Rect(tile.x + 7, tile.y + 16, tile.width - 10, 20), tiles[i].Item2, 15, tiles[i].Item3, true);
            }

            // 시간대: 필요 기사(동 수요표 · 가상) 대 지금 소속 기사. 출근 예정은 다음 단계(r130)
            var y = panel.y + 116;
            묶음머리(x, y, width, "▼ 시간대 필요 기사", "흰 선 = 필요 · 민트 = 소속", Paper);
            y += 26;
            var need = 시간대필요캐시;
            if (need == null) Text(new Rect(x, y, width, 17), "시간대 몫 자료 준비 전", 11, Soft);
            else
            {
                var scale = Mathf.Max(6, need.Max(), 지사.기사);
                for (var i = 1; i < need.Length; i++)       // 새벽은 주문이 적어 접는다
                {
                    var (이름, 시작, 끝) = 배달플랫폼Policy.시간대[i];
                    Text(new Rect(x, y, 74, 16), $"{이름} {시작}~{끝}", 10, Soft);
                    var bar = new Rect(x + 76, y + 3, width - 140, 10);
                    Paint(bar, Ink(0x1A2A39), 3);
                    Paint(new Rect(bar.x, bar.y, bar.width * Mathf.Min(1f, 지사.기사 / (float)scale), bar.height), Fade(Mint, 0.75f), 3);
                    var mark = bar.x + bar.width * Mathf.Min(1f, need[i] / (float)scale);
                    Paint(new Rect(mark - 1, bar.y - 2, 2, bar.height + 4), Paper);
                    Text(new Rect(bar.xMax + 4, y, 60, 16), $"필요 {need[i]}", 10, 지사.기사 < need[i] ? Red : Paper, true, TextAnchor.MiddleRight);
                    y += 17;
                }
            }
            Text(new Rect(x, panel.y + 227, width, 14), "출근 예정 · 출근 요청은 다음 단계(시간대 출근)", 9, Soft);

            // 기사 모집: 지금은 동 관리 창의 고용으로 보낸다
            y = panel.y + 248;
            묶음머리(x, y, width, "▼ 기사 모집", "지원 → 교육 → 첫 배달 · 다음 단계", Paper);
            if (ExpansionButton(new Rect(x, y + 26, width, 24), "기사 고용 · 동 관리 창에서", DongDeskReady, Tile)) DongDeskShortcut(BusinessFood);

            // 지사 정산(동 수요 누계). 지사 운영보상은 기준 미정
            y = panel.y + 306;
            묶음머리(x, y, width, "▼ 지사 정산 · 누계", "가상", Paper);
            var demand = 플랫폼수요캐시;
            장부줄(x - 8, y + 26, width + 8, "수입", demand.Revenue.ToString("N0") + "원");
            장부줄(x - 8, y + 42, width + 8, "기사 보수", demand.RiderPay.ToString("N0") + "원");
            장부줄(x - 8, y + 58, width + 8, "고용 · 제휴 지출", demand.Spent.ToString("N0") + "원");
            장부줄(x - 8, y + 74, width + 8, "지사 운영보상", "기준 미정");

            // 지사 밖: 다음 지사 후보
            y = panel.y + 402;
            묶음머리(x, y, width, "▼ 지사 밖 동", $"하루 {지사.지사밖하루주문:0}건", Gold);
            y += 26;
            if (지사.지사밖.Count == 0) Text(new Rect(x, y, width, 16), "모든 동을 담당하고 있어요", 10, Soft);
            foreach (var dong in 지사.지사밖.Take(3))
            {
                Text(new Rect(x + 4, y, width - 8, 16), dong.이름, 11, Paper);
                Text(new Rect(x + 4, y, width - 8, 16), $"하루 {dong.하루주문:0}건", 10, Soft, align: TextAnchor.MiddleRight);
                y += 16;
            }
            Text(new Rect(x, panel.yMax - 30, width, 26), "다음 지사 개설 · 계약 형태(직영 / 위탁)는 다음 단계 · 숫자는 가상", 9, Soft, wrap: true);
        }

        private void 묶음머리(float x, float y, float width, string 이름, string 오른쪽, Color 색)
        {
            Paint(new Rect(x, y, width, 22), Ink(0x142130), 6);
            Text(new Rect(x + 8, y, 150, 22), 이름, 11, 색, true);
            Text(new Rect(x + 96, y, width - 104, 22), 오른쪽, 9, 색 == Gold ? Gold : Soft, align: TextAnchor.MiddleRight);
        }

        // ── 패키지 고르기 (시안 P0) ─────────────────────────────────────────────

        private void DrawPackagePicker()
        {
            if (!패키지고르기표시) return;
            Paint(new Rect(0, 0, Width, Height), Fade(Ink(0x080D13), 0.95f));
            Text(new Rect(0, 30, Width, 18), "홍달 경영 시뮬레이션 · 패키지 고르기", 12, Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(0, 52, Width, 34), "어느 자리에서 시작할까요?", 24, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(0, 90, Width, 18), "패키지마다 내가 맡는 자리와 키우는 것이 다르다. 세계(지도 · 하루 · 장부)는 같다.", 12, Soft,
                align: TextAnchor.MiddleCenter);
            if (패키지 != 패키지종류.없음 || PlayerPrefs.HasKey(배달플랫폼Policy.저장키) || !패키지저장가능)
            {
                var close = new Rect(Width - 64, 24, 40, 28);
                Text(close, "×", 22, Soft, align: TextAnchor.MiddleCenter);
                if (Hit(close)) 패키지고르기열림 = false;
            }

            var packs = 배달플랫폼Policy.패키지들;
            const float left = 60f, top = 128f, gap = 14f, cardHeight = 360f;
            var unit = (Width - left * 2 - gap * (packs.Length - 1)) / (packs.Length + 0.35f);
            var cx = left;
            for (var i = 0; i < packs.Length; i++)
            {
                var pack = packs[i];
                var w = unit * (i == 0 ? 1.35f : 1f);
                var card = new Rect(cx, top, w, cardHeight);
                cx += w + gap;
                Paint(card, pack.열림 ? Mint : Rim, 12);
                Paint(new Rect(card.x + (pack.열림 ? 2 : 1), card.y + (pack.열림 ? 2 : 1), card.width - (pack.열림 ? 4 : 2), card.height - (pack.열림 ? 4 : 2)), Canvas, 11);
                var px = card.x + 15;
                var pw = card.width - 30;
                Text(new Rect(px, card.y + 14, pw, 14), $"패키지 {i + 1}" + (pack.열림 ? " · 먼저 여는 것" : string.Empty), 10, Gold, true);
                Text(new Rect(px, card.y + 32, pw, 28), pack.이름, 20, pack.열림 ? Paper : Soft, true);
                Text(new Rect(px, card.y + 62, pw, 16), pack.나는, 11, Soft);
                var rowY = card.y + 92;
                foreach (var (이름, 값) in new[] { ("키우는 것", pack.키우는것), ("처음 자리", pack.처음자리), ("성과", pack.성과) })
                {
                    Text(new Rect(px, rowY, 60, 16), 이름, 10, Soft);
                    Text(new Rect(px + 62, rowY, pw - 62, 48), 값, 11, pack.열림 && 이름 == "성과" ? Mint : Paper, wrap: true);
                    rowY += 54;
                }
                var go = new Rect(px, card.yMax - 50, pw, 34);
                if (pack.열림)
                {
                    if (ExpansionButton(go, 패키지 == pack.종류 ? "이 패키지로 계속" : "이 패키지로 시작", true, Mint)) 패키지정하기(pack.종류);
                }
                else
                {
                    Paint(go, Rim, 6);
                    Paint(new Rect(go.x + 1, go.y + 1, go.width - 2, go.height - 2), Canvas, 5);
                    Text(go, "준비 중", 11, Soft, true, TextAnchor.MiddleCenter);
                }
            }

            var boxTop = top + cardHeight + 16;
            var notes = new[]
            {
                ("모든 패키지가 같이 쓰는 것", "지도(전국 · 구 · 동) · 하루와 결산 · 업무 장부 · 관찰 보기 · 중화 버튼 · 켜 두면 저절로 자라는 운영"),
                ("패키지마다 다른 것", "시작 자리 · 목표 · 아래 줄 메뉴 · 처음 열리는 연구 · 성과 숫자 · 결산 화면의 첫 줄"),
                ("지금 고르면", "업무 저장은 그대로다. 아래 줄과 위 띠만 이 패키지에 맞게 바뀐다. 아래 줄 「모든 사업 보기」로 언제든 돌아간다."),
            };
            var boxWidth = (Width - left * 2 - gap * 2) / 3f;
            for (var i = 0; i < notes.Length; i++)
            {
                var box = new Rect(left + i * (boxWidth + gap), boxTop, boxWidth, 72);
                Paint(box, Ink(0x23364A), 10);
                Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 9);
                Text(new Rect(box.x + 12, box.y + 8, box.width - 24, 16), notes[i].Item1, 11, Gold, true);
                Text(new Rect(box.x + 12, box.y + 27, box.width - 24, 40), notes[i].Item2, 11, Paper, wrap: true);
            }
            var all = new Rect(Width / 2f - 110, boxTop + 86, 220, 28);
            if (ExpansionButton(all, "지금처럼 모든 사업 보기", true, Tile)) 패키지정하기(패키지종류.없음);
        }
    }
}
