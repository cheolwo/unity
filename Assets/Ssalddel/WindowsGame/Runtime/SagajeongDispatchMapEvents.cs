using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // r154·r157 (2026-11-13): 새 주문의 핀 낙하·착지 파동과 지도 효과 선택.
    //  - 새 주문은 픽업 매장에 꽂는다(사용자 선택). 배송지는 지금처럼 주문을 고르면 보인다.
    //  - 효과는 "간단히"(처음 값, 사용자 선택)와 "효과 사용" 둘이다. 간단히에서도 "새 주문 +N" 문구·매장 표식·대기 건수는 그대로다.
    //  - 같은 선택이 기존의 신호 파동, 구 지도의 표식 낙하·민트 파동·지사 둘레의 점에도 걸린다.
    //  - 무엇을 언제 그릴지는 순수 규칙(지도새주문표현Policy.cs)이 정한다. 이 파일은 그리기와 연결만 한다.
    //  - 효과 선택은 플레이어의 표현 취향이다. 업무 저장(confirmed-session)과 따로 PlayerPrefs에 둔다.
    public sealed partial class SagajeongDispatchView
    {
        private const string MapEffectsPreference = "hongdal.map-effects";
        private static readonly Rect MapEffectsControl = new(22, 594, 188, 26);
        private readonly 지도새주문표현Policy orderWatch = new();
        private readonly 지도새주문연출큐 orderEffects = new();
        private bool mapEffects;              // false = 간단히
        private bool mapEffectsPersistent;    // 검증 촬영(--sagajeong-*)에서는 취향을 쓰지 않는다

        private void LoadMapEffects()
        {
            mapEffectsPersistent = Array.TrueForAll(Environment.GetCommandLineArgs(),
                value => !value.StartsWith("--sagajeong-", StringComparison.Ordinal));
            mapEffects = mapEffectsPersistent && PlayerPrefs.GetInt(MapEffectsPreference, 0) == 1;
        }

        private void SetMapEffects(bool on)
        {
            if (mapEffects == on) return;
            mapEffects = on;
            if (!on)
            {
                // 진행 중인 움직임만 정리한다. 표식·문구·건수는 그대로이고, 다시 켜도 지난 것을 재생하지 않는다.
                orderEffects.StopMotion();
                SettleGuMotion();
            }
            if (mapEffectsPersistent)
            {
                PlayerPrefs.SetInt(MapEffectsPreference, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            Debug.Log("Hongdal map effects: " + (on ? "효과 사용" : "간단히"));
        }

        /// <summary>Update에서 상태 사본을 바꾼 직후 부른다. 업무 상태는 읽기만 한다.</summary>
        private void ObserveNewOrders(string action)
        {
            if (state?.orders == null) return;
            var received = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in state.events ?? Array.Empty<DispatchEvent>())
                if (entry.code == "order_received" && !string.IsNullOrEmpty(entry.orderId)) received.Add(entry.orderId);
            var orders = state.orders.Select(order => new 지도주문관찰(order.id, order.storeId, received.Contains(order.id))).ToList();
            if (action is "start" or "campaign-start")
            {
                // 새 회차이거나 저장을 불러왔다. 지금 있는 주문은 안정 표식이다.
                orderWatch.Baseline(orders);
                orderEffects.Clear();
                return;
            }
            var groups = orderWatch.Observe(orders);
            if (groups.Count == 0) return;
            orderEffects.Add(groups, Time.unscaledTimeAsDouble, mapEffects);
            Debug.Log("Hongdal new orders: " + string.Join(", ", groups.Select(group => group.StoreId + " +" + group.Count)) +
                (mapEffects ? " · 효과 사용" : " · 간단히"));
        }

        // ── 그리기 (동네 지도, GUI.BeginGroup(Field) 안) ──────────────────────────

        /// <summary>착지 파동. 경로·기사·표식보다 아래에 그린다. 누를 곳을 만들지 않는다.</summary>
        private void DrawOrderRipples()
        {
            if (Event.current.type != EventType.Repaint || orderEffects.Items.Count == 0) return;
            var now = Time.unscaledTimeAsDouble;
            foreach (var item in orderEffects.Items)
            {
                if (!TryStorePoint(item.StoreId, out var point)) continue;
                ResolveOrderEffect(item, point, now);
                if (!item.Animated) continue;
                var landed = now - item.StartedAt - 지도새주문연출큐.DropSeconds;
                for (var ring = 0; ring < 2; ring++)
                {
                    var spread = (float)((landed - ring * 0.22) / 지도새주문연출큐.RippleSeconds);
                    if (spread < 0f || spread >= 1f) continue;
                    var eased = 1f - Mathf.Pow(1f - spread, 2.2f);
                    Hoop(point, 9f + 40f * eased, Fade(Gold, (1f - spread) * (ring == 0 ? 0.9f : 0.55f)), 2.4f - 1.2f * spread);
                }
            }
        }

        /// <summary>내려오는 핀과 "새 주문 +N". 매장 표식 위, 기사 아래에 그린다. 장식 핀에는 누를 곳이 없다.</summary>
        private void DrawOrderDrops()
        {
            if (Event.current.type != EventType.Repaint) return;
            var now = Time.unscaledTimeAsDouble;
            orderEffects.Expire(now);
            var drop = (float)지도새주문연출큐.DropSeconds;
            var hold = (float)지도새주문연출큐.PinSeconds;
            foreach (var item in orderEffects.Items)
            {
                if (!TryStorePoint(item.StoreId, out var point)) continue;
                ResolveOrderEffect(item, point, now);
                var age = (float)(now - item.StartedAt);
                if (item.Animated && age < hold)
                {
                    var fall = age < drop ? -90f * (1f - Bounce(age / drop)) : 0f;
                    var fade = Mathf.Clamp01((hold - age) / 0.4f);
                    Paint(new Rect(point.x - 5, point.y - 1.5f, 10, 4), Fade(Color.black, 0.35f * Mathf.Clamp01(age / drop) * fade), 2);
                    DrawGuPin(point + new Vector2(0, fall), TerritoryPinKinds.Food, false, Mathf.Clamp01(age * 4f / drop) * fade);
                }
                // 효과 사용이면 착지한 뒤에, 간단히면 바로 보인다.
                if (now >= item.TagUntil || item.Animated && age < drop) continue;
                var text = "새 주문 +" + item.Count;
                var tag = new Rect(point.x + 52, point.y - 45, text.Length * 10.5f + 16f, 22);
                Paint(tag, Fade(Canvas, 0.92f), 6);
                Paint(new Rect(tag.x, tag.y + 3, 3, tag.height - 6), Gold, 1.5f);
                Text(tag, text, 11, Gold, true, TextAnchor.MiddleCenter);
            }
        }

        // 처음 그릴 때 한 번: 화면 밖이거나 패널에 가렸거나 늦었으면 움직임 없이 지나간다. 나중에 화면에 들어와도 재생하지 않는다.
        private void ResolveOrderEffect(새주문연출 item, Vector2 point, double now)
        {
            if (item.Resolved) return;
            var top = (expansionView && !mapOnly ? (SignalBarVisible ? SignalBar.yMax : 92) : 0) - Field.y + 30;
            var bottom = Field.height + Field.y - (expansionView && !mapOnly ? 110 : 30);
            var visible = mapLevel == MapLevel.Dong && point.x >= 24 && point.x <= Field.width - 24 &&
                point.y >= top && point.y <= bottom && !MapOverlayCovers(point + Field.position);
            orderEffects.Resolve(item, visible, now);
        }

        private bool TryStorePoint(string storeId, out Vector2 point)
        {
            point = default;
            var store = state?.stores?.FirstOrDefault(item => item.id == storeId);
            if (store == null) return false;
            point = PlotScene(store.latitude, store.longitude);
            return true;
        }

        // ── 효과 선택 (지도 왼쪽 아래) ──────────────────────────────────────────

        private bool MapEffectsControlCovers(Vector2 point) => expansionView && !mapOnly && !showDesk && MapEffectsControl.Contains(point);

        private void DrawMapEffectsControl()
        {
            var rect = MapEffectsControl;
            Paint(rect, Canvas, 7);
            Text(new Rect(rect.x + 9, rect.y, 54, rect.height), "지도 효과", 10, Soft);
            var simple = new Rect(rect.x + 62, rect.y + 3, 56, rect.height - 6);
            var effects = new Rect(simple.xMax + 4, rect.y + 3, rect.xMax - simple.xMax - 7, rect.height - 6);
            if (EffectsSegment(simple, "간단히", !mapEffects)) SetMapEffects(false);
            if (EffectsSegment(effects, "효과 사용", mapEffects)) SetMapEffects(true);
        }

        private bool EffectsSegment(Rect rect, string label, bool chosen)
        {
            Paint(rect, chosen ? Mint : Tile, 5);
            Text(rect, label, 11, chosen ? Canvas : Paper, chosen, TextAnchor.MiddleCenter);
            return Hit(rect);
        }

        // 효과를 끈 순간 구 지도의 움직임을 정리한다. 떨어지던 표식은 바로 꽂힌 자리로, 퍼지던 파동은 지운다.
        private void SettleGuMotion()
        {
            guWaves.Clear();
            var now = Time.unscaledTime;
            foreach (var id in guPinSeen.Keys.ToList())
                if (now - guPinSeen[id] < 10f) guPinSeen[id] = now - 10f;
        }
    }
}
