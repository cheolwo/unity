using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 기사 무념·경 시간·등급(r124~r126), 경 시간당 순소득(r133), 생산·소비 흐름(r134)의 화면.
    // 2단계(2026-11-14): 계산은 커널의 기사무념Module·기사무념Policy가 하고 커널 저장에 담긴다.
    // 이 파일은 상태 사본을 읽어 그리고, 출발 전 점검만 명령으로 보낸다. 규칙을 다시 계산하지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        // r126 등급별 오토바이 색.
        private static readonly Color[] GradeColors =
        {
            Ink(0xE5534B), Ink(0xF0913A), Ink(0xF2C94C), Ink(0x3DBE76), Ink(0x4C8DF0),
            Ink(0x5B67D8), Ink(0xA77BEA), Ink(0xF4F7F8), Ink(0x22272B)
        };
        private static readonly 기사무념RiderView NoRiderMind = new 기사무념RiderView
        {
            GradeName = 기사무념Policy.GradeNames[0], Capacity = 기사무념Policy.GradeMinutes[0], Remaining = 기사무념Policy.GradeMinutes[0],
        };

        private readonly Dictionary<string, Texture2D> tarotArt = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private string mindWarning;

        private 기사무념Snapshot MindSnapshot() => engine?.Kernel?.Find(기사무념Module.Id)?.Snapshot() as 기사무념Snapshot;

        private 기사무념RiderView MindOf(string courierId) => MindSnapshot()?.Find(courierId) ?? NoRiderMind;

        // 예전에는 여기서 화면이 직접 계산했다. 지금은 커널 모듈이 음식 배달 상태를 스스로 읽는다.
        private void UpdateMind()
        {
        }

        // 새 회차의 기록은 커널이 회차를 열 때 비운다. 화면에는 아직 내보내지 않은 경고만 남아 있다.
        private void ResetMind()
        {
            mindWarning = null;
        }

        private void CheckRiderMind(string courierId)
        {
            var result = engine.Business(new GameCommand
            {
                OsId = 기사무념Module.Id,
                Action = "check",
                CommandId = "game-" + Guid.NewGuid().ToString("N"),
                ElapsedMs = state?.elapsedMs ?? 0,
            }.With("rider", courierId));
            notice = result.Ok
                ? $"기사 {Letter(courierId)} 출발 전 점검 · 지금 마음을 살피고 무념으로 하루를 시작합니다."
                : result.Message;
        }

        // r125: 남은 경 시간이 예상 업무 + 여유 1분보다 적으면 다른 기사나 휴식을 권한다. 무념이 깨진 기사는 새 배차를 받지 않는다.
        // 판정은 커널 모듈의 미리보기가 한다.
        private bool MindBlocksAssignment(string courierId, out string message)
        {
            message = null;
            var preview = engine.BusinessPreview(new GameCommand { OsId = 기사무념Module.Id, Action = "assign" }.With("rider", courierId));
            if (preview.BlockCode == 기사무념배차판정.Broken)
            {
                message = $"기사 {Letter(courierId)} · {preview.Message}";
                return true;
            }
            if (preview.BlockCode == 기사무념배차판정.Low) mindWarning = preview.Message;
            return false;
        }

        private string WithMindWarning(string message)
        {
            if (string.IsNullOrEmpty(mindWarning)) return message;
            var combined = string.IsNullOrEmpty(message) ? mindWarning : message + "  " + mindWarning;
            mindWarning = null;
            return combined;
        }

        // 상단 지표: 오늘 기사들이 무념을 유지한 시간과, 그 구간에 연결된 가상 순소득의 시간당 값(비용 미반영).
        private (string Maintained, string PerHour, string Note) MindHeadline()
        {
            var mind = MindSnapshot();
            if (mind == null || mind.Riders.Count == 0) return ("기록 대기", "산정 대기", null);
            var maintained = mind.MaintainedMinutes;
            var linked = mind.LinkedIncome;
            var note = mind.BrokenCount > 0 ? $"무념 깨짐 {mind.BrokenCount}명" : $"무념 유지 {mind.Riders.Count}명";
            return (maintained > 0 ? MindDuration(maintained) : "기록 대기",
                maintained > 0 && linked > 0 ? $"{linked / (maintained / 60):N0}원" : "산정 대기", note);
        }

        // r134: 오늘 주문(소비)·매장 준비 여력(생산)·배송 여력(연결)을 비교한다.
        private (int Orders, int Prepare, int Deliver, int Limit, string Focus) MindFlow()
        {
            var mind = MindSnapshot();
            if (state == null || mind == null || !mind.Observed) return (0, 0, 0, 0, "회차를 불러오는 중입니다.");
            return (mind.FlowOrders, mind.FlowPrepare, mind.FlowDeliver, mind.FlowLimit, mind.FlowFocus);
        }

        private void DrawMindCard(float x, float y, string courierId)
        {
            var mind = MindOf(courierId);
            Paint(new Rect(x, y, 224, 130), Tile, 8);

            DrawTarot(new Rect(x + 8, y + 8, 44, 74), mind.TarotId, mind.Broken);
            var tx = x + 60;
            Paint(new Rect(tx, y + 11, 11, 11), GradeColors[mind.Grade], 6);
            Text(new Rect(tx + 16, y + 6, 148, 20), $"{mind.GradeName} · 개인 타로 {mind.TarotName}", 12, Paper, true);
            Text(new Rect(tx, y + 27, 160, 18), mind.Broken ? "역방향 · 무념 상태가 깨졌습니다" : "정방향 · 무념 유지",
                11, mind.Broken ? Red : Mint, true);

            var ratio = mind.Capacity > 0 ? Mathf.Clamp01((float)(mind.Remaining / mind.Capacity)) : 0;
            var bar = new Rect(tx, y + 50, 156, 7);
            Paint(bar, Canvas, 3);
            var barColor = mind.Broken ? Red : mind.Low ? Gold : Mint;
            if (ratio > 0) Paint(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), barColor, 3);
            Text(new Rect(tx, y + 59, 160, 18), $"경 {MindDuration(mind.Remaining)} 남음 / {MindDuration(mind.Capacity)}", 11, Soft);

            var next = mind.HasNextGrade
                ? $"다음 {mind.NextGradeName} · {MindDuration(mind.NextGradeMinutes)} 이상 {mind.Qualifying}/{기사무념Policy.ShiftsForPromotion}"
                : "최고 등급";
            Text(new Rect(x + 8, y + 86, 210, 16),
                $"오늘 유지 {MindDuration(mind.Maintained)} · 정산 {mind.Settled}건 · 유효 {mind.ValidDays}일", 10, Soft);
            Text(new Rect(x + 8, y + 102, 140, 16), next, 10, Soft);

            var check = new Rect(x + 150, y + 99, 66, 23);
            Paint(check, mind.CheckedToday ? Canvas : Mint, 5);
            Text(check, mind.CheckedToday ? "점검 완료" : "출발 전 점검", 10, mind.CheckedToday ? Mint : Canvas, true, TextAnchor.MiddleCenter);
            if (!mind.CheckedToday && Hit(check)) CheckRiderMind(courierId);
        }

        private void DrawTarot(Rect rect, string tarotId, bool reversed)
        {
            if (!tarotArt.TryGetValue(tarotId, out var art))
            {
                art = Resources.Load<Texture2D>("SagajeongDispatch/tarot/" + tarotId);
                tarotArt[tarotId] = art;
            }
            Paint(rect, reversed ? Red : Rim, 4);
            var inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
            if (art == null)
            {
                Text(inner, "타로", 10, Soft, align: TextAnchor.MiddleCenter);
                return;
            }
            var matrix = GUI.matrix;
            if (reversed) RotateLocal(180, inner.center);
            GUI.DrawTexture(inner, art, ScaleMode.ScaleAndCrop);
            GUI.matrix = matrix;
        }

        private void DrawMindMarker(DispatchCourier courier, Vector2 position)
        {
            var mind = MindOf(courier.id);
            var dot = Centered(position + new Vector2(17, -17), 12, 12);
            Paint(Centered(dot.center, 15, 15), Canvas, 8);
            Paint(dot, GradeColors[mind.Grade], 6);
            if (!mind.Broken) return;
            var badge = Centered(position + new Vector2(-18, -18), 16, 16);
            Paint(badge, Red, 8);
            Text(badge, "!", 12, Canvas, true, TextAnchor.MiddleCenter);
        }

        private static string MindDuration(double minutes) => 기사무념Policy.Duration(minutes);
    }
}
