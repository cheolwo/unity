using System;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 오른쪽 아래: 작은 지도 ↔ 업무 요약 (r217 확정 · r219의 M 키, 2026-11-15 키3).
    //  - 기본은 작은 지도다. 머리의 「업무」로 같은 자리를 업무 요약으로 바꾸고, 「지도」로 돌아온다(M은 2026-11-15 r236 행정동 이동 창으로 옮겼다).
    //    같은 실행 동안 고른 보기를 기억한다(다시 실행할 때까지 남길지는 미정이라 저장하지 않는다).
    //  - 업무 요약은 지금 있는 상태만 읽는다: 막힘(신호) · 할 일(사슬의 다음 행동) · 오는 중(화물). 없으면 「없음」이다.
    //    가상의 건수를 채우지 않는다.
    //  - 단추는 보러 가기(신호의 자리 · 매장 창의 재료 탭)만 한다. 발주 · 검수 · 제조를 확정하지 않는다.
    //  - 접기와 업무 요약은 다른 일이다. 접은 줄은 전과 같다.
    public sealed partial class SagajeongDispatchView
    {
        private bool miniWork;                       // true = 업무 요약을 보는 중
        private bool workSummaryDrawn;               // 이번 그리기에서 업무 요약을 그렸나 (촬영이 읽는다)

        // 작은 지도의 머리에서 「업무」 단추. (DrawMiniMap)
        private void MiniWorkToggle(Rect panel)
        {
            if (ExpansionButton(new Rect(panel.x + 141, panel.y + 6, 38, 19), "업무", true, Tile)) miniWork = true;
        }

        // 작은 지도 자리에 업무 요약을 그린다. (DrawMiniMap이 접히지 않았을 때 부른다)
        private void DrawWorkSummary()
        {
            workSummaryDrawn = false;
            var panel = MiniMapPanel;
            Paint(panel, Ink(0x3B5669), 9);
            Paint(new Rect(panel.x + 1.2f, panel.y + 1.2f, panel.width - 2.4f, panel.height - 2.4f), Fade(Canvas, 0.96f), 8);
            Text(new Rect(panel.x + 10, panel.y + 5, 80, 22), "업무 요약", 12, Paper, true);
            if (ExpansionButton(new Rect(panel.x + 141, panel.y + 6, 38, 19), "지도", true, Mint)) miniWork = false;
            if (ExpansionButton(new Rect(panel.xMax - 50, panel.y + 6, 42, 19), "접기", true, Tile)) miniMapFolded = true;

            var x = panel.x + 10;
            var width = panel.width - 20;
            var y = panel.y + 32;

            // 지금 막힘: 신호(위험 먼저) 두 건까지
            var open = signals.Where(signal => signal.ResolvedMs == 0).ToList();
            Text(new Rect(x, y, width, 16), "지금 막힘 · " + open.Count, 9, Soft);
            y += 17;
            if (open.Count == 0)
            {
                Text(new Rect(x + 4, y, width, 18), "없음", 10, Fade(Soft, 0.8f));
                y += 20;
            }
            foreach (var signal in open.Take(2))
            {
                var row = new Rect(x, y, width, 24);
                Paint(row, Tile, 6);
                Icon(new Rect(row.x + 5, row.y + 4, 16, 16), "alert", SignalColor(signal));
                var title = string.IsNullOrEmpty(signal.Title) ? signal.Message : signal.Title;
                Text(new Rect(row.x + 25, row.y, row.width - 68, row.height), title, 10, Paper);
                var go = new Rect(row.xMax - 40, row.y + 3, 36, 18);
                if (ExpansionButton(go, "보기", true, Mint)) GoToSignal(signal);
                y += 27;
            }
            y += 3;

            // 할 일: 사슬의 다음 행동 (지금 있는 것만)
            var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            var job = chain.ActionKey == "order" ? "본부 발주" : chain.ActionKey == "inspect" ? "점포 검수" : chain.ActionKey == "produce" ? "제조" : null;
            Text(new Rect(x, y, width, 16), "할 일 · " + (job == null ? 0 : 1), 9, Soft);
            y += 17;
            if (job == null)
            {
                Text(new Rect(x + 4, y, width, 18), "없음", 10, Fade(Soft, 0.8f));
                y += 20;
            }
            else
            {
                var row = new Rect(x, y, width, 24);
                Paint(row, Tile, 6);
                Icon(new Rect(row.x + 5, row.y + 4, 16, 16), chain.ActionKey == "inspect" ? "eye" : chain.ActionKey == "produce" ? "machine" : "receipt", Gold);
                Text(new Rect(row.x + 25, row.y, row.width - 68, row.height), job + " · " + (store?.StoreName ?? "내 매장"), 10, Paper);
                var go = new Rect(row.xMax - 40, row.y + 3, 36, 18);
                if (ExpansionButton(go, "열기", true, Mint)) PressShortcut(KeyCode.F4);      // 재료 탭에서 한다(여는 것만)
                y += 27;
            }
            y += 3;

            // 오는 중: 화물
            Text(new Rect(x, y, width, 16), "오는 중", 9, Soft);
            y += 17;
            var line = StoreTruckLine();
            Icon(new Rect(x + 4, y + 1, 14, 14), "truck", line == "없음" ? Fade(Soft, 0.6f) : Sky);
            Text(new Rect(x + 24, y, width - 24, 18), line, 10, line == "없음" ? Fade(Soft, 0.8f) : Paper);

            if (y + 20 < panel.yMax - 22)
                Text(new Rect(x, panel.yMax - 22, width, 16), "M · 작은 지도로", 9, Fade(Soft, 0.75f), align: TextAnchor.MiddleRight);
            workSummaryDrawn = true;
        }
    }
}
