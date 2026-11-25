using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 00 경영 — 오행 하향식 화면 (2026-11-17, Claude 제작실, 계획 r2 T1~T3 첫 묶음).
    //  - 아래 줄 맨 왼쪽 「00 경영」을 누르면 화면 가운데에 판이 뜬다. 다섯 열: 주문(목) · 음식점(화) · 배차(토) · 창고(금) · 배달·운송(수).
    //    오행은 열 머리의 색 띠 · 점과 괄호로만 쓴다(사용자 11-17). 숫자는 지금 있는 상태 사본에서 읽기만 한다. 모르면 「자료 준비 전」.
    //  - 「아래로」: 판 안에서 1호지사 → 기사 → 주문 한 건으로 내려가거나(경로 줄이 깊어짐), 지금 있는 창(07 · 08 · 09 · 10 · 14 · 15)을 연다.
    //    창을 열면 판은 닫히지만 경로는 남는다. 「00 경영」을 다시 누르면 보던 층으로 돌아온다.
    //  - 위 두 줄: 사업 경로(누구의 일인가)와 지도 위치(어디인가)를 따로 쓴다(r259 5절). 경로 조각을 누르면 그 층으로 올라간다.
    //  - 판이 떠 있는 동안 뒤의 지도 · 단추는 누르기 · 휠을 받지 않는다(행정동 이동 창과 같음). Esc · ×는 닫기.
    //  - 고르기 · 열기 · 위로 · 00으로는 조회다. 배차 · 고용 · 발주 · 정산 · 시계 · 저장은 바꾸지 않는다. 확정은 지금처럼 각 창과 턴 넘김에서.
    //  - 기사에게 동 이동을 지시하는 단추는 없다(r267). 지사는 아직 커널 단위가 아니어서 지금 운영 동 묶음(지사캐시)을 1호지사로 읽는다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect 경영판칸 = new(28, 62, 1224, 592);
        private static readonly Color 목색 = Ink(0x7CCF6A), 화색 = Ink(0xFF7A6B), 토색 = Ink(0xE8C25A), 금색 = Ink(0xD7DCE4), 수색 = Ink(0x6AA8FF);

        private bool 경영판열림;
        private readonly 경영경로 경영경로 = new();
        private string 경영판안내;                     // 내려가지 못한 이유 등 한 줄

        private bool 경영판가능 => TidyScreen && expansionView && state != null;
        private bool 경영판보임 => 경영판열림 && 경영판가능 && !SettingsVisible && !DongPickerVisible;

        private static Color 오행색(오행 행) => 행 switch
        {
            오행.목 => 목색, 오행.화 => 화색, 오행.토 => 토색, 오행.금 => 금색, _ => 수색,
        };

        private void 경영판열기()
        {
            if (!경영판가능)
            {
                notice = "00 경영 · 운영 화면에서 열 수 있어요.";
                return;
            }
            경영판열림 = true;
            경영판안내 = null;
            dongPickerOpen = false;
            helpOpen = false;
        }

        private bool 경영판Esc닫기()
        {
            if (!경영판보임) return false;
            경영판열림 = false;
            return true;
        }

        // ── 그리기 ─────────────────────────────────────────────────────────────

        private void Draw오행경영()
        {
            if (!경영판보임) return;
            if (Event.current.type == EventType.ScrollWheel && 경영판칸.Contains(Event.current.mousePosition)) Event.current.Use();
            플랫폼자료갱신();
            경영경로.사라진대상정리(경영대상있나);

            Paint(new Rect(0, 0, Width, Height), Fade(Canvas, 0.55f));
            var box = 경영판칸;
            Paint(box, Mint, 14);
            Paint(new Rect(box.x + 2, box.y + 2, box.width - 4, box.height - 4), Canvas, 13);

            // 위 두 줄: 사업 경로 · 지도 위치
            var crumbs = 경영경로.마디들;
            var x = box.x + 20;
            for (var i = 0; i < crumbs.Count; i++)
            {
                var last = i == crumbs.Count - 1;
                var label = crumbs[i].이름;
                var width = Mathf.Min(260f, 14f + label.Length * (i == 0 ? 15f : 12f));
                var piece = new Rect(x, box.y + 12, width, 28);
                Text(piece, label, i == 0 ? 17 : 14, last ? Paper : Mint, true);
                if (!last && Hit(piece)) 경영경로.여기로(i);
                x += width;
                if (!last)
                {
                    Text(new Rect(x, box.y + 12, 18, 28), "›", 15, Soft, align: TextAnchor.MiddleCenter);
                    x += 18;
                }
            }
            Text(new Rect(box.x + 20, box.y + 40, 620, 18), "위치  " + 경영위치글(), 11, Soft);
            var close = new Rect(box.xMax - 46, box.y + 12, 30, 30);
            Text(close, "×", 20, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                경영판열림 = false;
                return;
            }
            var keys = new Rect(box.xMax - 214, box.y + 16, 160, 22);
            Paint(keys, Rim, 11);
            Text(keys, 경영경로.깊이 > 0 ? "Esc 닫기 · 경로 조각 = 위로" : "Esc 닫기 · 보기만 해요", 10, Soft, align: TextAnchor.MiddleCenter);

            var body = new Rect(box.x + 16, box.y + 66, box.width - 32, box.height - 108);
            switch (경영경로.지금.층)
            {
                case 경영층.루트: 경영루트그리기(body); break;
                case 경영층.지사: 경영지사그리기(body); break;
                case 경영층.기사: 경영기사그리기(body); break;
                case 경영층.주문: 경영주문그리기(body); break;
            }
            if (!경영판보임) return;                    // 창을 열러 갔다

            var foot = new Rect(box.x + 20, box.yMax - 34, box.width - 40, 20);
            if (!string.IsNullOrEmpty(경영판안내)) Text(foot, 경영판안내, 11, Gold, true);
            else Text(foot, "업무 이름이 먼저, 오행은 괄호와 색으로만 · 고르기 · 열기 · 위로는 보기만 해요(배차 · 고용 · 정산은 각 창과 턴 넘김에서)", 10, Soft);
        }

        private string 경영위치글()
        {
            var dong = BusinessDong()?.Entry;
            var here = dong == null ? "사가정 배달권" : $"{dong.district} · {dong.name}";
            var 지사 = 지사캐시;
            return 경영경로.지금.층 switch
            {
                경영층.지사 => 지사 == null || 지사.담당.Count == 0 ? here : "담당 " + string.Join(" · ", 지사.담당.Select(item => item.이름)),
                _ => here,
            };
        }

        // 경로의 대상이 아직 있나. 사라졌으면 그 마디부터 걷어 낸다(다른 대상을 임의로 고르지 않음).
        private bool 경영대상있나(경로마디 node) => node.층 switch
        {
            경영층.지사 => true,
            경영층.기사 => state?.couriers != null && state.couriers.Any(item => item.id == node.대상),
            경영층.주문 => state?.orders != null && state.orders.Any(item => item.id == node.대상),
            _ => true,
        };

        // ── 00: 다섯 열 ────────────────────────────────────────────────────────

        private void 경영루트그리기(Rect body)
        {
            var columns = 오행경영Policy.열;
            const float gap = 10f;
            var columnWidth = (body.width - gap * (columns.Length - 1)) / columns.Length;
            var columnHeight = body.height - 64;
            for (var i = 0; i < columns.Length; i++)
            {
                var column = columns[i];
                var color = 오행색(column.행);
                var card = new Rect(body.x + i * (columnWidth + gap), body.y, columnWidth, columnHeight);
                Paint(card, Rim, 10);
                Paint(new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2), Ink(0x121E2A), 9);
                Paint(new Rect(card.x, card.y, card.width, 5), color, 3);
                Paint(new Rect(card.x + 14, card.y + 21, 12, 12), color, 6);
                Text(new Rect(card.x + 32, card.y + 13, card.width - 40, 28), column.업무, 17, Paper, true);
                Text(new Rect(card.x + 32 + column.업무.Length * 17f + 4, card.y + 15, 40, 26), "(" + 오행경영Policy.글자(column.행) + ")", 13, color, true);
                Text(new Rect(card.x + 14, card.y + 46, card.width - 24, 14), "지금", 9, Soft);
                Text(new Rect(card.x + 14, card.y + 61, card.width - 24, 36), 경영지금글(column.행), 11, Paper, wrap: true);
                Paint(new Rect(card.x + 12, card.y + 104, card.width - 24, 1), Rim);
                Text(new Rect(card.x + 14, card.y + 112, card.width - 24, 16), "아래로", 10, Soft, true);
                var y = card.y + 134;
                foreach (var down in column.아래로)
                {
                    var row = new Rect(card.x + 10, y, card.width - 20, 34);
                    var ready = 경영갈곳가능(down.갈곳, out var why);
                    var inside = down.갈곳 is 경영갈곳.지사안쪽 or 경영갈곳.진행주문안쪽 or 경영갈곳.대기주문안쪽;
                    Paint(row, ready && inside ? Ink(0x1B3A3A) : Tile, 7);
                    if (ready && inside) Paint(new Rect(row.x, row.y, 3, row.height), Mint, 2);
                    Text(new Rect(row.x + 10, row.y, row.width - 70, row.height), down.이름, 11, ready ? Paper : Soft, ready && inside);
                    Text(new Rect(row.xMax - 66, row.y, 58, row.height), ready ? down.번호 + "  ›" : "다음 단계", 10, ready ? Mint : Soft,
                        align: TextAnchor.MiddleRight);
                    if (Hit(row))
                    {
                        if (ready) 경영가기(down.갈곳);
                        else 경영판안내 = down.이름 + " · " + why;
                        if (!경영판보임) return;
                    }
                    y += 40;
                }
            }

            // 아래 두 칸: 계획 · 결과 모으기
            var half = (body.width - gap) / 2f;
            var plan = new Rect(body.x, body.yMax - 54, half, 50);
            Paint(plan, Rim, 9);
            Paint(new Rect(plan.x + 1, plan.y + 1, plan.width - 2, plan.height - 2), Ink(0x121E2A), 8);
            Text(new Rect(plan.x + 14, plan.y + 5, 200, 16), "계획", 10, Soft, true);
            Text(new Rect(plan.x + 14, plan.y + 22, plan.width - 100, 22), "12 다음 할 일 · 이번 턴에 담은 일", 12, Paper);
            var planReady = 경영갈곳가능(경영갈곳.다음할일창, out var planWhy);
            Text(new Rect(plan.xMax - 90, plan.y, 78, plan.height), planReady ? "열기 ›" : "다음 단계", 11, planReady ? Mint : Soft, true, TextAnchor.MiddleRight);
            if (Hit(plan))
            {
                if (planReady) 경영가기(경영갈곳.다음할일창);
                else 경영판안내 = "다음 할 일 · " + planWhy;
                if (!경영판보임) return;
            }
            var result = new Rect(plan.xMax + gap, plan.y, half, 50);
            Paint(result, Rim, 9);
            Paint(new Rect(result.x + 1, result.y + 1, result.width - 2, result.height - 2), Ink(0x121E2A), 8);
            Text(new Rect(result.x + 14, result.y + 5, 200, 16), "결과 모으기", 10, Soft, true);
            var report = new Rect(result.x + 14, result.y + 22, 150, 24);
            var ledger = new Rect(result.x + 180, result.y + 22, 150, 24);
            Text(report, "14 오늘 결산 ›", 12, Mint, true);
            Text(ledger, "15 본사 장부 ›", 12, 경영갈곳가능(경영갈곳.본사장부창, out _) ? Mint : Soft, true);
            if (Hit(report)) 경영가기(경영갈곳.결산창);
            else if (Hit(ledger))
            {
                if (경영갈곳가능(경영갈곳.본사장부창, out var why)) 경영가기(경영갈곳.본사장부창);
                else 경영판안내 = "본사 장부 · " + why;
            }
        }

        // 열 머리 아래 「지금」 한 줄. 지금 있는 상태 사본만 읽는다. 모르면 「자료 준비 전」.
        private string 경영지금글(오행 행)
        {
            var orders = state?.orders ?? Array.Empty<DispatchOrder>();
            var 지사 = 지사캐시;
            switch (행)
            {
                case 오행.목:
                    var waiting = orders.Count(order => order.stage == "Waiting");
                    var open = orders.Count(order => 오행경영Policy.단계순번(order.stage) is 0 or 1 or 2);
                    return $"기다림 {waiting}건 · 처리 중 {open}건" + (지사 == null ? string.Empty : $" · 하루 {지사.하루주문:0}건(가상)");
                case 오행.화:
                    var mine = MiniHomePlace();
                    return (mine == null ? "내 매장 없음" : "내 매장 있음") + (지사 == null ? " · 제휴 자료 준비 전" : $" · 제휴 {지사.제휴}곳");
                case 오행.토:
                    var couriers = state?.couriers?.Length ?? 0;
                    return (지사 == null ? "1호지사 · 자료 준비 전" : $"1호지사 · 소속 {지사.기사}명") + $" · 지금 보이는 기사 {couriers}명";
                case 오행.금:
                    return MiniHomePlace() == null ? "매장 재고 · 자료 준비 전" : "매장 재고 · 검수는 매장 창 재료 탭";
                default:
                    var moving = orders.Count(order => 오행경영Policy.진행중(order.stage));
                    var done = orders.Count(order => 오행경영Policy.단계순번(order.stage) == 3);
                    return $"진행 {moving}건 · 전달 {done}건" + (지사?.받아냄 == null ? string.Empty : " · 받아냄 " + 배달플랫폼Policy.비율글(지사.받아냄));
            }
        }

        private bool 경영갈곳가능(경영갈곳 갈곳, out string why)
        {
            why = null;
            switch (갈곳)
            {
                case 경영갈곳.지사안쪽:
                    return true;
                case 경영갈곳.대기주문안쪽:
                    why = "지금 기다리는 주문이 없어요";
                    return state?.orders != null && state.orders.Any(order => order.stage == "Waiting");
                case 경영갈곳.진행주문안쪽:
                    why = "지금 진행 중인 배달이 없어요";
                    return state?.orders != null && state.orders.Any(order => 오행경영Policy.진행중(order.stage));
                case 경영갈곳.지사창:
                case 경영갈곳.본사장부창:
                    why = "이 화면에서는 장부 칸을 열 수 없어요(배차 데스크가 열려 있으면 닫아 주세요)";
                    return 플랫폼장부가능;
                case 경영갈곳.배차창:
                    return true;
                case 경영갈곳.내매장창:
                case 경영갈곳.매장재료창:
                    why = "내 매장이 없거나 매장 창을 쓸 수 없는 화면이에요";
                    return StoreDeskReady && MiniHomePlace() != null;
                case 경영갈곳.동관리음식점:
                    why = "동 관리 창을 쓸 수 없는 화면이에요";
                    return DongDeskReady;
                case 경영갈곳.플레이스창:
                    why = "동 지도에서 열 수 있어요";
                    return PlaceAvailable;
                case 경영갈곳.결산창:
                    return true;
                case 경영갈곳.다음할일창:
                    why = "다음 할 일을 쓰는 운영에서 열려요";
                    return 할일보임;
                default:
                    why = "아직 연결 전이에요(다음 단계)";
                    return false;
            }
        }

        // 아래로 가기. 판 안쪽이면 경로를 깊게 하고, 창이면 판을 닫고 그 창을 연다(경로는 남김).
        private void 경영가기(경영갈곳 갈곳)
        {
            경영판안내 = null;
            switch (갈곳)
            {
                case 경영갈곳.지사안쪽:
                    경영경로.내려가기(경영층.지사, "branch-1", 지사캐시?.이름 is { Length: > 0 } name ? name : 배달플랫폼Policy.첫지사이름);
                    return;
                case 경영갈곳.대기주문안쪽:
                    var waiting = state.orders.FirstOrDefault(order => order.stage == "Waiting");
                    if (waiting != null) 경영경로.내려가기(경영층.주문, waiting.id, 경영주문이름(waiting));
                    return;
                case 경영갈곳.진행주문안쪽:
                    var moving = state.orders.FirstOrDefault(order => 오행경영Policy.진행중(order.stage));
                    if (moving != null) 경영경로.내려가기(경영층.주문, moving.id, 경영주문이름(moving));
                    return;
            }
            경영판열림 = false;
            switch (갈곳)
            {
                case 경영갈곳.지사창:
                case 경영갈곳.본사장부창:
                    본사쪽 = 갈곳 == 경영갈곳.지사창 ? 0 : 1;
                    플랫폼장부열기();
                    break;
                case 경영갈곳.배차창:
                    if (!showDesk) ToggleDesk();
                    break;
                case 경영갈곳.내매장창:
                case 경영갈곳.매장재료창:
                    var place = MiniHomePlace();
                    if (place != null) GoToPlace(place);
                    if (갈곳 == 경영갈곳.매장재료창 && StoreDeskVisible) storeDeskTab = StoreTabMaterials;
                    break;
                case 경영갈곳.동관리음식점:
                    SelectExpansion(BusinessRestaurant);
                    DongDeskShortcut(BusinessRestaurant);
                    break;
                case 경영갈곳.플레이스창:
                    OpenPlace();
                    break;
                case 경영갈곳.결산창:
                    if (!DailyReportVisible) ToggleDailyReport();
                    break;
                case 경영갈곳.다음할일창:
                    할일판열림 = true;
                    할일오른쪽비우기();
                    break;
            }
        }

        private string 경영주문이름(DispatchOrder order) => "주문 " + Letter(order.id) + (string.IsNullOrEmpty(order.menu) ? string.Empty : " · " + order.menu);

        private string 경영기사이름(DispatchCourier courier) => courier == null ? "기사" : CourierCaption(courier);

        // ── 07 1호지사 (판 안) ─────────────────────────────────────────────────

        private void 경영지사그리기(Rect body)
        {
            var 지사 = 지사캐시;
            var left = new Rect(body.x, body.y, body.width * 0.5f - 8, body.height);
            var right = new Rect(left.xMax + 16, body.y, body.width - left.width - 16, body.height);
            Text(new Rect(left.x, left.y, left.width, 30), (지사?.이름 is { Length: > 0 } name ? name : 배달플랫폼Policy.첫지사이름) + "  ·  " +
                오행경영Policy.표기("배차", 오행.토), 18, Paper, true);
            var tiles = new (string, string, Color)[]
            {
                ("소속 기사(동 수요표)", 지사 == null ? "자료 준비 전" : $"{지사.기사}명", Paper),
                ("지금 보이는 기사", $"{state.couriers?.Length ?? 0}명", Paper),
                ("배차 기다림", $"{state.orders?.Count(order => order.stage == "Waiting") ?? 0}건", Paper),
                ("최근 받아냄", 배달플랫폼Policy.비율글(지사?.받아냄), Mint),
            };
            var tileWidth = (left.width + 6) / tiles.Length;
            for (var i = 0; i < tiles.Length; i++)
            {
                var tile = new Rect(left.x + i * tileWidth, left.y + 38, tileWidth - 6, 44);
                Paint(tile, Tile, 7);
                Text(new Rect(tile.x + 8, tile.y + 4, tile.width - 12, 14), tiles[i].Item1, 9, Soft);
                Text(new Rect(tile.x + 8, tile.y + 19, tile.width - 12, 22), tiles[i].Item2, 15, tiles[i].Item3, true);
            }
            Text(new Rect(left.x, left.y + 92, left.width, 18), "기사 · 누르면 그 기사로 내려가요", 11, Soft, true);
            var y = left.y + 116;
            foreach (var courier in (state.couriers ?? Array.Empty<DispatchCourier>()).Take(7))
            {
                var row = new Rect(left.x, y, left.width, 44);
                Paint(row, Tile, 8);
                Paint(new Rect(row.x + 12, row.y + 15, 14, 14), RiderColor(courier.id), 7);
                Text(new Rect(row.x + 36, row.y + 3, row.width - 80, 20), 경영기사이름(courier), 13, Paper, true);
                var job = 경영기사주문(courier.id);
                Text(new Rect(row.x + 36, row.y + 23, row.width - 80, 16),
                    (job == null ? "지금 맡은 배달 없음" : StageLabel(job.stage)) + $" · 오늘 전달 {경영기사전달수(courier.id)}건", 10, Soft);
                Text(new Rect(row.xMax - 36, row.y, 24, row.height), "›", 16, Mint, true, TextAnchor.MiddleCenter);
                if (Hit(row))
                {
                    경영경로.내려가기(경영층.기사, courier.id, 경영기사이름(courier));
                    return;
                }
                y += 50;
            }
            if ((state.couriers?.Length ?? 0) == 0) Text(new Rect(left.x, y, left.width, 18), "지금 배차 엔진에 보이는 기사가 없어요", 11, Soft);

            // 오른쪽: 다른 창으로 · 모집 · 연구
            var go = new (string 이름, string 번호, 경영갈곳 갈곳)[]
            {
                ("기다리는 주문 한 건", "08", 경영갈곳.대기주문안쪽),
                ("배차 데스크에서 배정하기", "08", 경영갈곳.배차창),
                ("지사 화면(시간대 필요 기사 · 정산)", "07", 경영갈곳.지사창),
            };
            Text(new Rect(right.x, right.y + 4, right.width, 18), "여기서 더 내려가기", 11, Soft, true);
            y = right.y + 28;
            foreach (var item in go)
            {
                var row = new Rect(right.x, y, right.width, 38);
                var ready = 경영갈곳가능(item.갈곳, out var why);
                Paint(row, Tile, 8);
                Text(new Rect(row.x + 12, row.y, row.width - 90, row.height), item.이름, 12, ready ? Paper : Soft);
                Text(new Rect(row.xMax - 84, row.y, 72, row.height), ready ? item.번호 + "  ›" : "지금 없음", 10, ready ? Mint : Soft, align: TextAnchor.MiddleRight);
                if (Hit(row))
                {
                    if (ready) 경영가기(item.갈곳);
                    else 경영판안내 = item.이름 + " · " + why;
                    return;
                }
                y += 44;
            }
            var recruit = new Rect(right.x, y + 6, right.width, 92);
            Paint(recruit, Gold, 9);
            Paint(new Rect(recruit.x + 1, recruit.y + 1, recruit.width - 2, recruit.height - 2), Ink(0x2A2618), 8);
            Text(new Rect(recruit.x + 14, recruit.y + 8, recruit.width - 28, 18), "모집 · 연구", 12, Gold, true);
            Text(new Rect(recruit.x + 14, recruit.y + 30, recruit.width - 28, 18), "첫 연구 · 지사 스티커 홍보 (대상: 1호 기본 배달권 전체)", 11, Paper);
            Text(new Rect(recruit.x + 14, recruit.y + 50, recruit.width - 28, 36),
                "기사는 스스로 지원해요. 다른 동으로 옮기라고 지시하지 않아요(r267). 연구 화면 · 비용 · 효과는 다음 단계.", 10, Soft, wrap: true);
        }

        private DispatchOrder 경영기사주문(string courierId) =>
            state?.orders?.FirstOrDefault(order => order.driverId == courierId && 오행경영Policy.진행중(order.stage));

        private int 경영기사전달수(string courierId) =>
            state?.orders?.Count(order => order.driverId == courierId && 오행경영Policy.단계순번(order.stage) == 3) ?? 0;

        // ── 11-1 기사 (판 안) ──────────────────────────────────────────────────

        private void 경영기사그리기(Rect body)
        {
            var id = 경영경로.지금.대상;
            var courier = state.couriers?.FirstOrDefault(item => item.id == id);
            if (courier == null) return;                          // 사라진 대상은 위에서 걷어 낸다
            Paint(new Rect(body.x + 4, body.y + 4, 44, 44), RiderColor(courier.id), 22);
            Text(new Rect(body.x + 60, body.y, 400, 28), 경영기사이름(courier), 20, Paper, true);
            Text(new Rect(body.x + 60, body.y + 28, 500, 18), "1호지사 소속 · 소속 경로는 기사가 있는 동이 바뀌어도 그대로예요", 11, Soft);

            var job = 경영기사주문(courier.id);
            var now = new Rect(body.x, body.y + 64, body.width * 0.5f - 8, 120);
            Paint(now, job == null ? Rim : Mint, 9);
            Paint(new Rect(now.x + 1, now.y + 1, now.width - 2, now.height - 2), job == null ? Ink(0x121E2A) : Ink(0x15302F), 8);
            Text(new Rect(now.x + 14, now.y + 8, 200, 16), "지금 업무", 10, Soft, true);
            if (job == null) Text(new Rect(now.x + 14, now.y + 30, now.width - 28, 20), "맡은 배달이 없어요 · 배차를 기다리는 중", 13, Paper);
            else
            {
                Text(new Rect(now.x + 14, now.y + 28, now.width - 28, 22), 경영주문이름(job) + " · " + StageLabel(job.stage), 14, Paper, true);
                Text(new Rect(now.x + 14, now.y + 52, now.width - 28, 18), "픽업 " + (string.IsNullOrEmpty(job.storeName) ? "매장 " + Letter(job.storeId) : job.storeName), 11, Soft);
                var open = new Rect(now.xMax - 150, now.yMax - 34, 136, 26);
                Text(open, "이 주문 보기  ›", 12, Mint, true, TextAnchor.MiddleRight);
                if (Hit(open))
                {
                    경영경로.내려가기(경영층.주문, job.id, 경영주문이름(job));
                    return;
                }
            }
            var today = new Rect(now.xMax + 16, now.y, body.width - now.width - 16, 120);
            Paint(today, Tile, 9);
            Text(new Rect(today.x + 14, today.y + 8, 200, 16), "오늘", 10, Soft, true);
            Text(new Rect(today.x + 14, today.y + 30, today.width - 28, 22), $"전달 {경영기사전달수(courier.id)}건", 15, Paper, true);
            Text(new Rect(today.x + 14, today.y + 56, today.width - 28, 18), "위치 · " + (courier.phase is { Length: > 0 } phase ? StageLabel(phase) : "대기"), 11, Soft);

            var phone = new Rect(body.x, now.yMax + 18, 320, 40);
            var phoneReady = mapLevel == MapLevel.Dong;
            Paint(phone, phoneReady ? Mint : Rim, 8);
            Text(phone, phoneReady ? "기사 휴대폰으로 보기 · 11-1" : "기사 휴대폰은 동 지도에서 열려요", 13, phoneReady ? Canvas : Soft, true, TextAnchor.MiddleCenter);
            if (phoneReady && Hit(phone))
            {
                경영판열림 = false;
                OpenRolePhone(FocusKind.Courier, courier.id);
                return;
            }
            Text(new Rect(phone.xMax + 16, phone.y, body.width - phone.width - 16, phone.height),
                "휴대폰이 먼저 열리고 카메라 따라가기는 따로 골라요(r249). 판의 경로는 남아서 「00 경영」으로 돌아오면 여기예요.", 10, Soft, wrap: true);
        }

        // ── 주문 한 건 (판 안) ─────────────────────────────────────────────────

        private void 경영주문그리기(Rect body)
        {
            var id = 경영경로.지금.대상;
            var order = state.orders?.FirstOrDefault(item => item.id == id);
            if (order == null) return;
            Text(new Rect(body.x, body.y, 600, 28), 경영주문이름(order), 20, Paper, true);
            Text(new Rect(body.x, body.y + 28, 700, 18),
                "픽업 " + (string.IsNullOrEmpty(order.storeName) ? "매장 " + Letter(order.storeId) : order.storeName) + " · 지금 " + StageLabel(order.stage), 11, Soft);

            // 단계 줄: 주문(목) → 배정(토) → 픽업(수) → 전달(수)
            var at = 오행경영Policy.단계순번(order.stage);
            var steps = 오행경영Policy.주문단계;
            var line = new Rect(body.x + 20, body.y + 70, body.width * 0.55f, 80);
            var stride = (line.width - 40) / (steps.Length - 1);
            Paint(new Rect(line.x + 10, line.y + 19, line.width - 40, 2), Rim);
            for (var i = 0; i < steps.Length; i++)
            {
                var cx = line.x + 10 + i * stride;
                var color = 오행색(steps[i].행);
                var reached = at >= 0 && i <= at;
                Paint(new Rect(cx - 10, line.y + 10, 20, 20), reached ? color : Rim, 10);
                if (!reached) Paint(new Rect(cx - 8, line.y + 12, 16, 16), Canvas, 8);
                Text(new Rect(cx - 50, line.y + 36, 100, 20), steps[i].이름, 13, reached ? Paper : Soft, true, TextAnchor.MiddleCenter);
                Text(new Rect(cx - 50, line.y + 56, 100, 16), "(" + 오행경영Policy.글자(steps[i].행) + ")", 10, color, align: TextAnchor.MiddleCenter);
            }
            if (at < 0) Text(new Rect(line.x, line.yMax + 4, line.width, 18), "이 주문은 단계 밖이에요(" + (order.stage ?? "모름") + ")", 11, Gold);

            // 오른쪽: 기록(같은 주문 id의 사건) — 결과는 이 주문에 붙는다
            var log = new Rect(line.xMax + 30, body.y + 56, body.width - line.width - 50, body.height - 120);
            Paint(log, Tile, 9);
            Text(new Rect(log.x + 14, log.y + 8, 200, 16), "이 주문의 기록", 10, Soft, true);
            var y = log.y + 30;
            var events = (state.events ?? Array.Empty<DispatchEvent>()).Where(item => item.orderId == order.id).ToList();
            foreach (var item in events.Skip(Math.Max(0, events.Count - 9)))
            {
                Text(new Rect(log.x + 14, y, 80, 18), ClockLabel(item.at), 10, Soft);
                Text(new Rect(log.x + 92, y, log.width - 106, 18), EventLabel(item.code, item.text), 11, Paper);
                y += 22;
            }
            if (events.Count == 0) Text(new Rect(log.x + 14, y, log.width - 28, 18), "아직 기록이 없어요", 11, Soft);

            var result = new Rect(body.x, line.yMax + 30, line.width + 10, 64);
            Paint(result, Rim, 9);
            Paint(new Rect(result.x + 1, result.y + 1, result.width - 2, result.height - 2), Ink(0x121E2A), 8);
            Text(new Rect(result.x + 14, result.y + 6, 120, 16), "결과", 10, Soft, true);
            Text(new Rect(result.x + 14, result.y + 24, result.width - 28, 34),
                at == 3 ? "전달됐어요 · 00의 배달·운송(수) 열 「지금」에 같은 주문으로 들어가요" :
                "전달되면 같은 주문으로 여기에 붙어요 · 예상은 실적에 더하지 않아요", 11, at == 3 ? Mint : Paper, wrap: true);

            // 아래 단추: 기사로 · 00으로 · (기사가 있으면) 휴대폰
            var buttons = new Rect(body.x, body.yMax - 44, body.width, 40);
            var courier = string.IsNullOrEmpty(order.driverId) ? null : state.couriers?.FirstOrDefault(item => item.id == order.driverId);
            var x = buttons.x;
            if (courier != null)
            {
                var toRider = new Rect(x, buttons.y, 200, 38);
                Paint(toRider, Rim, 8);
                Text(toRider, "↑ " + 경영기사이름(courier), 13, Mint, true, TextAnchor.MiddleCenter);
                if (Hit(toRider))
                {
                    경영경로.내려가기(경영층.기사, courier.id, 경영기사이름(courier));
                    return;
                }
                x += 212;
            }
            var top = new Rect(x, buttons.y, 200, 38);
            Paint(top, Mint, 8);
            Text(top, "00 경영으로", 13, Canvas, true, TextAnchor.MiddleCenter);
            if (Hit(top))
            {
                경영경로.맨위로();
                return;
            }
            if (order.stage == "Waiting")
            {
                var desk = new Rect(top.xMax + 12, buttons.y, 240, 38);
                Paint(desk, Tile, 8);
                Text(desk, "배차 데스크에서 배정하기 · 08", 12, Paper, true, TextAnchor.MiddleCenter);
                if (Hit(desk)) 경영가기(경영갈곳.배차창);
            }
        }
    }
}
