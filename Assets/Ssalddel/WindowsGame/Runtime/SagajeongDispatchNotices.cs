using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 떴다 사라지는 알림 (2026-11-15, 알1 · 알2 · 알4 + 하루 마감 알림).
    // 계획: Hongdal docs/AI/Handoffs/2026-11-15-Claude-개발계획-떴다사라지는알림.md
    // 사용자 결정: 「빛 플러스 짧은 글 … 배달 완료 때는 금액 … 맡김이 못하는 것만 일단 예외 처리 … 하루 마감 누르면 카드가 열리는 방식」.
    //  - 일이 처리되면 그 자리의 빛이 잠깐 밝아지고 짧은 글이 떴다 사라진다. 빛의 크기는 그대로다(크기는 명덕의 몫, r205 · r209).
    //  - 배차: 매장 → 기사로 빛 알갱이 하나(효과 사용일 때) · 둘 다 한 번 밝아짐 · 「기사 A · 배차」(맡김이 했으면 「맡김 · 기사 A 배차」).
    //  - 배달 완료: 배송지에서 빛이 한 번 퍼짐 · 「배달 완료」. 엔진이 정산을 기록하면 그 매장 빛이 밝아지고 「정산 +1,200원」.
    //    금액은 엔진의 내보내기 요약(정산을 마친 주문의 가상 차액 합)에서 늘어난 만큼이다. 화면이 금액을 계산하지 않는다.
    //  - 하루 마감: 지도가 한 번 어두워졌다 돌아오고 위쪽에 「n일차 영업 마감 …」. 누르면 하루 마감 카드가 열린다.
    //  - 맡김이 켜져 있으면 맡김이 하는 일(배차 대기)은 위쪽 신호 줄에서 뺀다. 지도의 박동과 업무 요약에는 그대로 있다.
    //  - 표현만 바뀐다. 시간 · 돈 · 주문 · 원장은 그대로다. 검증 촬영에서는 「그대로」(지금까지의 화면)로 둔다.
    public sealed partial class SagajeongDispatchView
    {
        private const float FlashSeconds = 1.2f;
        private const float BreathSeconds = 2.4f;
        private const float NoticeStackWidth = 540f;
        private const int NoticeStackRows = 3;

        private readonly 알림큐 notices = new();
        private readonly 정산몫 settleShare = new();
        private 알림정도 noticeLevel = 알림정도.그대로;
        private bool noticeBaselineStale = true;
        private readonly Dictionary<string, (string Stage, string Driver)> noticeSeen = new(StringComparer.Ordinal);
        private readonly List<(char Kind, string Key, string Other, double At)> noticeFlashes = new();
        private readonly List<Rect> noticeStackHits = new();
        private double breathAt = -100;
        private bool breathDim;
        private List<Signal> barSource;
        private List<Signal> barCache = new();

        private bool NoticesOn => 알림큐.Active(noticeLevel);

        private void LoadNotices()
        {
            var persistent = Array.TrueForAll(Environment.GetCommandLineArgs(), value =>
                !value.StartsWith("--sagajeong-", StringComparison.Ordinal) && !value.StartsWith(ViewProofFlag, StringComparison.Ordinal));
            noticeLevel = persistent ? 알림정도.보통 : 알림정도.그대로;
        }

        // 알림 줄(왼쪽 아래)은 알림을 쓰면 4초 머물고 0.8초에 흐려진다. 쓰지 않으면 지금처럼 6초.
        private float SideNoteSeconds => NoticesOn ? 4.8f : 6f;

        private float SideNoteAlpha => NoticesOn && error == null
            ? Mathf.Clamp01((SideNoteSeconds - (Time.unscaledTime - sideNoteAt)) / 0.8f) : 1f;

        // ── 신호 줄: 맡김이 하는 일은 빼고 ─────────────────────────────────────

        private static bool AutopilotHandles(Signal signal) => signal.ReasonCode == "dispatch-waiting";

        /// <summary>위쪽 신호 줄 · 머리줄에 남는 신호. 맡김이 켜져 있으면 맡김이 하는 일은 뺀다(사용자: 맡김이 못 하는 것만 남김).</summary>
        private List<Signal> BarSignals
        {
            get
            {
                if (!NoticesOn || !autopilotOn) return signals;
                if (!ReferenceEquals(barSource, signals))
                {
                    barSource = signals;
                    barCache = signals.Where(signal => !AutopilotHandles(signal)).ToList();
                }
                return barCache;
            }
        }

        // ── 사건을 안다 (Update, 응답을 받은 뒤. 상태는 읽기만 한다) ─────────────────

        private void ObserveNotices(string action, DispatchCampaign previousCampaign)
        {
            if (state?.orders == null) return;
            var now = Time.unscaledTimeAsDouble;
            var starting = action is "start" or "campaign-start";
            var speak = NoticesOn && !starting && !noticeBaselineStale;
            var byAutopilot = autopilotSentAction == "assign";
            var settled = 0;
            string settledStore = null;
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var order in state.orders)
            {
                present.Add(order.id);
                var known = noticeSeen.TryGetValue(order.id, out var seen);
                noticeSeen[order.id] = (order.stage, order.driverId);
                if (!speak || !known) continue;
                if (!string.IsNullOrEmpty(order.driverId) && string.IsNullOrEmpty(seen.Driver))
                {
                    var rider = "기사 " + Letter(order.driverId);
                    notices.Add(byAutopilot ? 알림종류.맡김배차 : 알림종류.배차, order.storeId,
                        byAutopilot ? $"맡김 · {rider} 배차" : $"{rider} · 배차", 0, now);
                    Flash('L', order.storeId, order.driverId, now);
                }
                if (Delivered(order.stage) && !Delivered(seen.Stage))
                {
                    notices.Add(알림종류.전달, order.id, "배달 완료", 0, now);
                    Flash('D', order.id, null, now);
                }
                if (order.stage == "Settled" && seen.Stage != "Settled")
                {
                    settled++;
                    settledStore = settled == 1 ? order.storeId : settledStore == order.storeId ? settledStore : string.Empty;
                }
            }
            if (noticeSeen.Count > present.Count + 64)
                foreach (var gone in noticeSeen.Keys.Where(seenId => !present.Contains(seenId)).ToList()) noticeSeen.Remove(gone);

            if (!NoticesOn)
            {
                noticeBaselineStale = true;
                return;
            }
            var day = state.campaign?.day ?? 0;
            if (starting || noticeBaselineStale)
            {
                var baseline = engine.CompletedSummary();
                settleShare.Baseline(day, baseline?.completedOrders ?? 0, baseline?.virtualBaseBalance ?? 0);
                noticeBaselineStale = false;
                return;
            }
            if (settled > 0)
            {
                var summary = engine.CompletedSummary();
                var amount = summary == null ? 0 : settleShare.Take(day, summary.completedOrders, summary.virtualBaseBalance);
                var key = string.IsNullOrEmpty(settledStore) ? string.Empty : settledStore;
                var item = notices.Add(알림종류.정산, key, null, amount, now);
                if (settled > 1) item.Count += settled - 1;
                if (!string.IsNullOrEmpty(key)) Flash('S', key, null, now);
            }

            var before = previousCampaign;
            var after = state.campaign;
            if (before == null || after == null) return;
            if (before.phase == "open" && after.phase != "open" && after.day == before.day)
            {
                notices.Add(알림종류.마감, string.Empty, DayCloseLine(after), 0, now, longHold: true);
                breathAt = now;
                breathDim = true;
            }
            else if (after.phase == "open" && after.day > before.day)
            {
                notices.Add(알림종류.영업시작, string.Empty, $"{after.day}일차 영업 시작", 0, now);
                breathAt = now;
                breathDim = false;
            }
        }

        private static bool Delivered(string stage) => stage is "Delivered" or "Received" or "Settled";

        private string DayCloseLine(DispatchCampaign campaign)
        {
            var summary = campaign.latestSummary != null && campaign.latestSummary.day == campaign.day ? campaign.latestSummary : null;
            if (summary == null) return $"{campaign.day}일차 영업 마감";
            var bonus = state?.settlement != null && state.settlement.bonus > 0 ? $" (빠른 배차 +{state.settlement.bonus:N0}원 포함)" : string.Empty;
            return $"{campaign.day}일차 영업 마감 · 정산 {summary.completed}/{summary.totalOrders}건 · 가상 차액 {summary.balance:N0}원{bonus}";
        }

        // 맡김이 한 점검 · 방침 (SagajeongDispatchAutopilot.cs가 부른다).
        private void NoticeAutopilot(맡김종류 kind, string courierId, string text = null)
        {
            if (!NoticesOn) return;
            var now = Time.unscaledTimeAsDouble;
            if (kind == 맡김종류.점검)
            {
                notices.Add(알림종류.맡김점검, string.Empty, "맡김 · 출발 전 점검", 0, now);
                if (!string.IsNullOrEmpty(courierId)) Flash('C', courierId, null, now);
            }
            else if (kind == 맡김종류.방침)
                notices.Add(알림종류.맡김방침, string.Empty, "맡김 · " + (text ?? "방침을 이어 갔습니다"), 0, now);
        }

        private void Flash(char kind, string key, string other, double now)
        {
            noticeFlashes.Add((kind, key, other, now));
            if (noticeFlashes.Count > 32) noticeFlashes.RemoveAt(0);
        }

        private void OpenDayCard()
        {
            if (state?.campaign == null) return;
            dayCardOpenDay = state.campaign.day;
            dayCardDismissed = -1;
        }

        private bool NoticeCovers(Vector2 point) =>
            noticeStackHits.Any(rect => rect.Contains(point)) || DayCardVisible && DayCard.Contains(point);

        // ── 그리기: 지도 위 (동네 지도, GUI.BeginGroup(Field) 안) ─────────────────────

        private bool TryDropPoint(string orderId, out Vector2 point)
        {
            point = default;
            var drop = state?.destinations?.FirstOrDefault(item => item.id == orderId);
            if (drop == null) return false;
            point = PlotScene(drop.latitude, drop.longitude);
            return true;
        }

        private bool TryCourierPoint(string courierId, out Vector2 point)
        {
            point = default;
            var courier = state?.couriers?.FirstOrDefault(item => item.id == courierId);
            if (courier == null) return false;
            point = PlotScene(courier.latitude, courier.longitude);
            return true;
        }

        /// <summary>순간 밝아짐. 안쪽 빛(지름 28)만 한 번 더 밝힌다. 바깥 빛의 크기는 그대로다.</summary>
        private void DrawNoticeLights()
        {
            if (!NoticesOn || Event.current.type != EventType.Repaint || noticeFlashes.Count == 0) return;
            var now = Time.unscaledTimeAsDouble;
            noticeFlashes.RemoveAll(item => now - item.At >= FlashSeconds);
            EnsureRoamGlow();
            foreach (var (kind, key, other, at) in noticeFlashes)
            {
                var age = (float)(now - at);
                var left = Mathf.Clamp01(1f - age / FlashSeconds);
                var glow = left * left;
                switch (kind)
                {
                    case 'L':
                        if (!TryStorePoint(key, out var store)) break;
                        GlowNow(store, 28f, Fade(Gold, 0.75f * glow));
                        if (!TryCourierPoint(other, out var rider)) break;
                        GlowNow(rider, 28f, Fade(RiderColor(other), 0.75f * glow));
                        var travel = Mathf.Clamp01(age / 0.6f);
                        if (mapEffects && travel < 1f)
                            GlowNow(Vector2.Lerp(store, rider, 1f - Mathf.Pow(1f - travel, 2f)), 16f, Fade(Paper, 0.9f * (1f - 0.5f * travel)));
                        break;
                    case 'D':
                        if (!TryDropPoint(key, out var drop)) break;
                        GlowNow(drop, 28f, Fade(DropHalo, 0.85f * glow));
                        if (mapEffects)
                        {
                            var spread = age / FlashSeconds;
                            Hoop(drop, 10f + 34f * (1f - Mathf.Pow(1f - spread, 2.2f)), Fade(Mint, 0.8f * (1f - spread)), 2.2f - spread);
                        }
                        break;
                    case 'S':
                        if (TryStorePoint(key, out var shop)) GlowNow(shop, 28f, Fade(Mint, 0.8f * glow));
                        break;
                    case 'C':
                        if (TryCourierPoint(key, out var checkedRider))
                            GlowNow(checkedRider, 28f, Fade(RiderColor(key), 0.7f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / FlashSeconds))));
                        break;
                }
            }
        }

        /// <summary>그 자리의 짧은 글. 매장 · 배송지의 왼쪽 위에 뜬다(새 주문 +N은 오른쪽 위). 누를 곳이 없다.</summary>
        private void DrawNoticeTags()
        {
            if (!NoticesOn || Event.current.type != EventType.Repaint) return;
            var now = Time.unscaledTimeAsDouble;
            notices.Expire(now);
            if (!알림큐.ShowsText(noticeLevel)) return;
            var rows = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in notices.Items)
            {
                if (string.IsNullOrEmpty(item.Key)) continue;
                Vector2 point;
                if (item.Kind == 알림종류.전달 ? !TryDropPoint(item.Key, out point) : !TryStorePoint(item.Key, out point)) continue;
                if (point.x < 0 || point.y < 0 || point.x > Field.width || point.y > Field.height) continue;
                var alpha = 알림큐.Alpha(item, now);
                if (alpha <= 0f) continue;
                rows.TryGetValue(item.Key, out var row);
                rows[item.Key] = row + 1;
                var text = 알림큐.Line(item);
                var colour = item.Kind == 알림종류.정산 ? Mint : item.Kind == 알림종류.전달 ? DropHalo : Paper;
                var rise = item.Kind == 알림종류.정산 ? 12f * Mathf.Clamp01((float)(now - item.StartedAt) / 1.2f) : 0f;
                var width = text.Length * 11f + 18f;
                var tag = new Rect(point.x - 20f - width, point.y - 34f - row * 26f - rise, width, 22);
                Paint(tag, Fade(Canvas, 0.9f * alpha), 6);
                Paint(new Rect(tag.xMax - 3, tag.y + 3, 3, tag.height - 6), Fade(colour, alpha), 1.5f);
                Text(tag, text, 11, Fade(colour, alpha), true, TextAnchor.MiddleCenter);
            }
        }

        // ── 그리기: 화면 (OnGUI) ─────────────────────────────────────────────

        /// <summary>하루 마감 · 영업 시작: 지도 전체가 한 번 어두워졌다(밝아졌다) 돌아온다.</summary>
        private void DrawNoticeBreath()
        {
            if (!NoticesOn || Event.current.type != EventType.Repaint) return;
            var age = (float)(Time.unscaledTimeAsDouble - breathAt);
            if (age < 0f || age >= BreathSeconds) return;
            var wave = Mathf.Sin(Mathf.PI * age / BreathSeconds);
            Paint(Field, breathDim ? Fade(Canvas, 0.3f * wave) : Fade(Paper, 0.07f * wave));
        }

        // 아래 가운데: 역 이름 칸 위에서 위로 쌓는다. 위쪽 가운데는 신호 줄 · 고른 동 카드와 겹친다(촬영 85~88 첫 판에서 확인).
        private const float NoticeStackBottom = 541f;
        private bool closedChipDrawn;          // 이번 그림에서 「n일차 영업 마감 ▸」 칩을 그렸나 (촬영이 읽는다)

        private bool ClosedChipVisible => NoticesOn && state?.campaign != null && state.campaign.phase == "closed" && !DayCardVisible &&
            !returnCardOpen && !CampaignPolicyOpen && !notices.Items.Any(item => item.Kind == 알림종류.마감);

        /// <summary>하루 마감 알림이나 칩을 누름: 카드를 열고 마감 알림은 거둔다.</summary>
        private void PressDayCloseNotice()
        {
            OpenDayCard();
            foreach (var item in notices.Items.Where(item => item.Kind == 알림종류.마감).ToList()) notices.Remove(item);
        }

        /// <summary>자리가 없는 알림(마감 · 영업 시작 · 맡김 · 여러 건의 정산)은 아래 가운데에 세 줄까지. 마감은 누르면 카드.
        /// 마감 알림이 사라진 뒤에도 영업 마감 동안에는 작은 칩이 남아 누르면 카드가 열린다.</summary>
        private void DrawNoticeStack()
        {
            if (Event.current.type == EventType.Repaint)
            {
                noticeStackHits.Clear();
                closedChipDrawn = false;
            }
            if (!NoticesOn || state == null || SettingsVisible) return;
            var now = Time.unscaledTimeAsDouble;
            var rows = new List<알림>();
            foreach (var item in notices.Items)
            {
                if (!string.IsNullOrEmpty(item.Key)) continue;
                if (!알림큐.ShowsText(noticeLevel) && item.Kind != 알림종류.마감) continue;
                if (알림큐.Alpha(item, now) <= 0f) continue;
                rows.Add(item);
            }
            if (rows.Count > NoticeStackRows) rows.RemoveRange(0, rows.Count - NoticeStackRows);
            var chip = ClosedChipVisible;
            var y = NoticeStackBottom - (rows.Count - 1 + (chip ? 1 : 0)) * 32f;
            foreach (var item in rows)
            {
                var alpha = 알림큐.Alpha(item, now);
                var clickable = item.Kind == 알림종류.마감 && state.campaign != null && state.campaign.phase != "open";
                var text = 알림큐.Line(item) + (clickable ? "   ▸ 눌러서 보기" : string.Empty);
                var rect = new Rect(Width / 2f - NoticeStackWidth / 2f, y, NoticeStackWidth, 28);
                var accent = item.Kind switch
                {
                    알림종류.마감 => Gold,
                    알림종류.영업시작 => Paper,
                    알림종류.맡김방침 => Lilac,
                    _ => Mint,
                };
                Paint(rect, Fade(Canvas, 0.93f * alpha), 8);
                Paint(new Rect(rect.x, rect.y + 4, 3, rect.height - 8), Fade(accent, alpha), 1.5f);
                Text(rect, text, 12, Fade(item.Kind == 알림종류.마감 ? Gold : Paper, alpha), item.Kind is 알림종류.마감 or 알림종류.영업시작,
                    TextAnchor.MiddleCenter);
                if (clickable)
                {
                    if (Event.current.type == EventType.Repaint) noticeStackHits.Add(rect);
                    if (Hit(rect))
                    {
                        PressDayCloseNotice();
                        return;
                    }
                }
                y += 32;
            }
            if (!chip) return;
            var label = $"{state.campaign.day}일차 영업 마감 · 카드 보기 ▸";
            var width = label.Length * 11f + 28f;
            var box = new Rect(Width / 2f - width / 2f, NoticeStackBottom, width, 28);
            Paint(box, Fade(Canvas, 0.9f), 8);
            Paint(new Rect(box.x, box.y + 4, 3, box.height - 8), Gold, 1.5f);
            Text(box, label, 12, Gold, true, TextAnchor.MiddleCenter);
            if (Event.current.type == EventType.Repaint)
            {
                noticeStackHits.Add(box);
                closedChipDrawn = true;
            }
            if (Hit(box)) PressDayCloseNotice();
        }


        private IEnumerator Settle(double seconds, double deadline)
        {
            var until = Time.realtimeSinceStartupAsDouble + seconds;
            yield return Until(() => Time.realtimeSinceStartupAsDouble >= until, deadline);
        }

        // ── 방향 촬영 ───────────────────────────────────────────────────────

        // 알림을 켜고(보통) 한 주문의 배차 → 배달 완료 → 정산, 그리고 하루 마감을 본다. 마감 카드는 알림을 눌러야 열린다.
        private IEnumerator ViewProofNotices(string directory, ViewProofReceipt receipt)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 45;
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            actorFocus = null;
            autopilotOn = false;
            if (!running) BeginRound();
            campaignSpeed = Math.Max(1, campaignSpeed);
            campaignResumeSpeed = Math.Max(1, campaignResumeSpeed);
            receipt.noticeDefaultOff = !NoticesOn;

            // 새 날의 영업으로: 기사는 하루에 한 건이라, 앞 장면들이 배차한 날을 넘겨 빈 기사가 있는 날에서 시작한다.
            var startDay = state.campaign?.day ?? 0;
            for (var step = 0; step < 8 && error == null && state.campaign != null &&
                 !(state.campaign.phase == "open" && state.campaign.day > startDay); step++)
            {
                if (CampaignPolicyOpen) break;
                var target = state.campaign.nextBoundaryMs > state.elapsedMs ? state.campaign.nextBoundaryMs : state.elapsedMs + 600_000;
                Send(new DispatchRequest { action = "tick", elapsedMs = target });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            receipt.noticeDay = state.campaign?.day ?? 0;
            noticeLevel = 알림정도.보통;
            notices.Clear();
            noticeFlashes.Clear();
            noticeBaselineStale = true;
            Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 1_000 });     // 기준선을 잡는다
            yield return Until(() => pending == null, deadline);
            yield return null;

            // 기다리는 주문과 빈 기사
            DispatchOrder order = null;
            string rider = null;
            for (var step = 0; step < 20 && error == null; step++)
            {
                order = state.orders?.FirstOrDefault(item => item.stage == "Waiting" && string.IsNullOrEmpty(item.driverId));
                rider = order == null ? null : !string.IsNullOrEmpty(order.recommendedDriverId) && CanSelect(order.recommendedDriverId) ? order.recommendedDriverId :
                    state.couriers?.Select(courier => courier.id).FirstOrDefault(CanSelect);
                if (order != null && rider != null || state.campaign == null || state.campaign.phase != "open") break;
                Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 60_000 });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            receipt.noticeOrder = order?.id ?? string.Empty;
            if (order == null || rider == null)
            {
                noticeLevel = 알림정도.그대로;
                holdProofClock = false;
                yield break;
            }
            var store = order.storeId;
            var before = engine.CompletedSummary();
            Send(new DispatchRequest
            {
                action = "assign", orderId = order.id, driverId = rider, revision = order.revision,
                commandId = "proof-notice-" + Guid.NewGuid().ToString("N"), elapsedMs = state.elapsedMs,
            });
            yield return Until(() => pending == null, deadline);
            yield return null;
            yield return null;
            var assigned = notices.Items.FirstOrDefault(item => item.Kind == 알림종류.배차 && item.Key == store);
            receipt.noticeAssignShown = assigned != null && noticeFlashes.Any(item => item.Kind == 'L' && item.Key == store);
            receipt.noticeAssignText = assigned == null ? string.Empty : 알림큐.Line(assigned);
            yield return Settle(0.4, deadline);       // 나타남(0.2초)이 끝난 뒤 찍는다
            yield return SaveFrame(directory, "85-notice-assign.png");

            // 배달 완료 → 정산(엔진이 기록할 때까지 30초씩)
            var id = order.id;
            for (var step = 0; step < 80 && error == null; step++)
            {
                var current = state.orders?.FirstOrDefault(item => item.id == id);
                if (current == null || current.stage == "Settled") break;
                Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 30_000 });
                yield return Until(() => pending == null, deadline);
                yield return null;
                if (!receipt.noticeDeliveredShown)
                    receipt.noticeDeliveredShown = notices.Items.Any(item => item.Kind == 알림종류.전달 && item.Key == id);
            }
            yield return null;
            yield return null;
            var after = engine.CompletedSummary();
            var paid = notices.Items.LastOrDefault(item => item.Kind == 알림종류.정산);
            receipt.noticeSettleShown = paid != null;
            receipt.noticeSettleAmount = paid?.Amount ?? -1;
            receipt.noticeSettleText = paid == null ? string.Empty : 알림큐.Line(paid);
            receipt.noticeSettleEngineDelta = before == null || after == null ? -1 : after.virtualBaseBalance - before.virtualBaseBalance;
            yield return Settle(0.4, deadline);
            yield return SaveFrame(directory, "86-notice-settle.png");

            // 맡김을 켜면 배차 대기 신호는 위쪽 줄에서 빠진다(맡김이 하는 일). 커널의 신호는 그대로다.
            autopilotOn = true;
            lastHandsTime = Time.realtimeSinceStartupAsDouble;
            yield return null;
            receipt.noticeWaitingInKernel = engine.Kernel.Signals.Count(item => AutopilotHandles(item));
            receipt.noticeWaitingInBar = BarSignals.Count(AutopilotHandles);
            autopilotOn = false;

            // 하루 마감: 위쪽에 알림, 카드는 아직 닫혀 있다 → 알림을 누르면 열린다
            for (var step = 0; step < 4 && error == null && state.campaign != null && state.campaign.phase == "open"; step++)
            {
                var target = state.campaign.nextBoundaryMs > state.elapsedMs ? state.campaign.nextBoundaryMs : state.elapsedMs + 600_000;
                Send(new DispatchRequest { action = "tick", elapsedMs = target });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            yield return null;
            yield return null;
            var closing = notices.Items.FirstOrDefault(item => item.Kind == 알림종류.마감);
            receipt.noticeDayCloseShown = closing != null;
            receipt.noticeDayCloseText = closing?.Text ?? string.Empty;
            receipt.noticeDayCardHidden = !DayCardVisible;
            yield return Settle(0.9, deadline);       // 지도가 한 번 어두워지는 숨의 가운데쯤
            yield return SaveFrame(directory, "87-notice-dayclose.png");
            PressDayCloseNotice();  // 알림을 누른 것과 같다(DrawNoticeStack의 Hit)
            yield return null;
            yield return null;
            receipt.noticeDayCardOnClick = DayCardVisible;
            yield return SaveFrame(directory, "88-notice-daycard.png");

            // 카드를 ×로 닫으면 아래 가운데에 「n일차 영업 마감 · 카드 보기」 칩이 남고, 누르면 다시 열린다.
            dayCardDismissed = state.campaign?.day ?? -1;
            dayCardOpenDay = -1;
            yield return null;
            yield return null;
            receipt.noticeClosedChip = closedChipDrawn;
            yield return SaveFrame(directory, "89-notice-chip.png");
            PressDayCloseNotice();
            yield return null;
            yield return null;
            receipt.noticeChipReopens = DayCardVisible;

            dayCardDismissed = state.campaign?.day ?? -1;
            dayCardOpenDay = -1;
            notices.Clear();
            noticeFlashes.Clear();
            noticeLevel = 알림정도.그대로;
            holdProofClock = false;
        }
    }
}
