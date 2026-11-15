using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 역할 앱 휴대폰의 틀과 홍달 기사 (2026-11-15, 사용자: 「구현 작업 진행」 — 시안 R-4, 같은 날 「음식점 · 창고 · 화물 기사님도」).
    // 제안: Hongdal docs/AI/Handoffs/2026-11-15-Claude-제안-역할앱을게임속으로.md 9 · 11절(앱 이름 홍달 · 오른쪽 휴대폰 · 직접 누르기 보류 ·
    // 정보 순서와 문구만 실제 앱과 같게 · 상태가 바뀌면 휴대폰이 그 일을 재연 · 넣는 까닭은 관찰을 쉽게).
    //  - 이 파일: 모든 역할 휴대폰이 함께 쓰는 틀(베젤 · 상태줄 · 머리줄 · 아래 탭 · 위 알림 · 아래 안내 · 손가락 · 아래에서 올라오는 카드)과
    //    재연 큐, 그리고 홍달 기사(음식 배달 기사). 화물 · 창고 · 가게는 SagajeongDispatchRolePhones.cs.
    //  - 지도에서 배차 기사를 누르면(빛으로 보는 동 지도) 오른쪽 칸에 그 기사의 「홍달 기사」 휴대폰이 뜬다. 예전 기사 카드는 「카드」로 바꿔 볼 수 있다.
    //  - 화면 짜임 · 문구는 기사 앱(FDriverApp/Pages/MainPage.xaml): 배달 · 내 정보, 현위치 · 픽업: 음식점, 진행 상황, 픽업지, 전달지,
    //    주문 정보, 배달료 산정 근거, 현장 처리 · 현장 중단, 배달 요청(거절 · 수락), 최근 완료 결과, 신규 배차 받기. 색 · 간격은 역할 앱 시각 기준.
    //  - 한 방향: 그 사람의 일의 단계가 바뀐 뒤에 휴대폰이 그 일을 재연한다(역할앱재연Policy). 보라 손가락은 가상 인물이 한 일이다.
    //    휴대폰의 단추는 아무것도 하지 않는다(보기 전용). 시계 · 원장 · 주문 · 배차를 바꾸지 않는다.
    //  - 열 때는 재연하지 않고 지금 화면부터 보인다. 「방금 한 일 다시 보기」는 마지막 재연만 다시 튼다(보기만).
    //  - 재연은 실제 시간으로 흐른다(게임 배속과 무관). 변화가 밀리면 빠르게, 셋 이상이면 마지막 상태로 건너뛴다.
    //  - 배차 엔진: 주문이 기다리는 동안 홍달 배차 엔진(Core 추천 · 동 관리의 엔진 Lv)이 고른 기사를 기억해 두고(보기만),
    //    배달 요청 카드에 「엔진 Lv · 추천대로 / 추천과 다르게」로 보인다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect PhoneFrame = new(1002, 110, 240, 490);      // 아래 순회 단추(CycleDockCompact, y 608)보다 위에서 끝난다
        private static readonly Rect PhoneHint = new(986, 90, 272, 18);

        // 역할 앱 시각 기준(RoleAppVisualDesignStandard)의 색. 다른 파일의 정적 색에 기대지 않는다.
        private static readonly Color PhonePrimary = Ink(0x0F766E);
        private static readonly Color PhonePrimaryDark = Ink(0x0B5C56);
        private static readonly Color PhonePrimaryLight = Ink(0xEAF5F3);
        private static readonly Color PhoneBack = Ink(0xF5F7FA);
        private static readonly Color PhoneSurface = Ink(0xFFFFFF);
        private static readonly Color PhoneText = Ink(0x172B36);
        private static readonly Color PhoneSub = Ink(0x586B78);
        private static readonly Color PhoneBorder = Ink(0xDCE5E9);
        private static readonly Color PhoneSuccess = Ink(0x166534);
        private static readonly Color PhoneInfo = Ink(0x1D4ED8);
        private static readonly Color PhoneWarn = Ink(0x92400E);
        private static readonly Color PhoneLoad = Ink(0xD97706);                   // 화물 기사 앱: 주황 상차 · 파랑 하차
        private static readonly Color PhoneBezel = Ink(0x0C0E12);
        private static readonly Color PhoneFinger = Ink(0xC6ACFF);

        private bool phoneCardView;                    // 예전 카드로 보기(기사 · 가게)
        private string phoneKey;                       // 휴대폰이 따라가는 사람(앱 · 대상)
        private string phoneSubject;                   // 그 사람의 지금 일(주문 · 운행 · 출고)
        private string phoneStage;                     //   그 일의 단계
        private readonly List<List<재연장면>> phoneQueue = new();
        private 재연장면 phoneScene;                   // 지금 틀고 있는 장면
        private 재연장면 phonePrevious;                // 바로 앞 장면(아래 카드가 올라오는 모습)
        private float phoneSceneStart;
        private float? phoneHold;                      // 방향 촬영: 장면의 진행을 이 값에 멈춘다
        private List<재연장면> phoneLastScript;
        private string phonePayText;
        private int phoneDrawn;                        // 이번 그림에서 휴대폰을 그렸나 (촬영이 읽는다)
        private int phoneLastFrame = -10;              // 마지막으로 그린 프레임: 닫았다 다시 열면 재연하지 않는다
        private readonly Dictionary<string, string> phoneRecommended = new();     // 주문 → 기다리는 동안 배차 엔진이 추천한 기사

        private static bool PhoneKind(FocusKind kind) => kind is FocusKind.Courier or FocusKind.Store or FocusKind.Cargo or FocusKind.Center;
        private static bool PhoneHasCard(FocusKind kind) => kind is FocusKind.Courier or FocusKind.Store;

        // 휴대폰이 서는 화면: 확장 화면의 오른쪽 칸. 기사 · 가게는 동 지도에서, 화물 · 창고(인계 사슬에서 엶)는 전국 지도가 아니면.
        private bool PhoneArea(FocusKind kind) => expansionView &&
            (kind is FocusKind.Cargo or FocusKind.Center ? mapLevel != MapLevel.Nation : mapLevel == MapLevel.Dong);

        private bool RolePhoneOn(FocusKind kind) => PhoneKind(kind) && PhoneArea(kind) && !(phoneCardView && PhoneHasCard(kind));

        private bool PhoneCovers(Vector2 point) => actorFocus is { } focus && RolePhoneOn(focus.Kind) &&
            (PhoneFrame.Contains(point) || PhoneHint.Contains(point));

        private void OpenRolePhone(FocusKind kind, string id)
        {
            dongPanelOpen = false;
            zonePanelOpen = false;
            showDetails = true;
            phoneCardView = false;
            actorFocus = (kind, id);
        }

        private void DrawRolePhone(FocusKind kind, string id)
        {
            if (kind == FocusKind.Courier) DrawRiderPhone(id);
            else if (kind == FocusKind.Store) DrawStorePhone(id);
            else if (kind == FocusKind.Cargo) DrawCargoPhone();
            else if (kind == FocusKind.Center) DrawCenterPhone();
        }

        // ── 배차 엔진의 추천을 기억한다(보기만) ──

        // 주문이 기다리는 동안만 Core가 추천 기사를 준다. 배정된 뒤에도 「추천대로였나」를 보이려고 화면 쪽에서 기억해 둔다(저장하지 않음).
        private void ObserveRecommendations()
        {
            if (state?.orders == null) return;
            foreach (var order in state.orders)
                if (order.stage == "Waiting" && !string.IsNullOrEmpty(order.recommendedDriverId)) phoneRecommended[order.id] = order.recommendedDriverId;
            if (phoneRecommended.Count > 64)
            {
                var live = new HashSet<string>(state.orders.Select(order => order.id));
                foreach (var key in phoneRecommended.Keys.Where(key => !live.Contains(key)).ToList()) phoneRecommended.Remove(key);
            }
        }

        private string EngineLine(DispatchOrder order)
        {
            var head = dongDemand != null ? "엔진 Lv" + dongDemand.EngineLevel : "홍달 배차 엔진";
            if (order == null) return head;
            if (order.stage == "Waiting")
                return string.IsNullOrEmpty(order.recommendedDriverId) ? head + " · 추천 없음" : head + " · 추천 기사 " + Letter(order.recommendedDriverId);
            if (string.IsNullOrEmpty(order.driverId) || !phoneRecommended.TryGetValue(order.id, out var recommended)) return head;
            return recommended == order.driverId ? head + " · 추천대로" : head + " · 추천(" + Letter(recommended) + ")과 다르게";
        }

        // ── 재연 큐 ──

        // 그 사람의 일과 단계를 읽고, 바뀌었으면 재연 대본을 큐에 넣는다. 처음 열 때(사람이 바뀌거나 닫았다 열 때)는 재연하지 않는다.
        private void SyncPhone(string key, string subject, string stage, Func<string, string, List<재연장면>> script)
        {
            var fresh = phoneKey != key || Time.frameCount - phoneLastFrame > 2;
            phoneLastFrame = Time.frameCount;
            if (fresh)
            {
                phoneKey = key;
                phoneSubject = subject;
                phoneStage = stage;
                phoneQueue.Clear();
                phoneScene = null;
                phonePrevious = null;
                phoneLastScript = null;
                phonePayText = null;
                return;
            }
            if (subject == phoneSubject && stage == phoneStage) return;
            var from = subject == phoneSubject ? phoneStage : null;
            phoneSubject = subject;
            phoneStage = stage;
            EnqueuePhoneScript(script(from, stage));
        }

        private void EnqueuePhoneScript(List<재연장면> script)
        {
            if (script == null || script.Count == 0) return;
            phoneLastScript = script;
            phoneQueue.Add(new List<재연장면>(script));
            var (_, skip) = 역할앱재연Policy.Pace(phoneQueue.Count);
            if (!skip) return;
            var count = phoneQueue.Count;
            var last = script[script.Count - 1];
            phoneQueue.Clear();
            phoneScene = null;
            phoneQueue.Add(new List<재연장면> { 역할앱재연Policy.Skipped(last.Screen, last.Step, last.Stage, count) });
        }

        // 지금 장면과 그 진행(0~1). 장면이 끝나면 다음으로 넘긴다.
        private float AdvancePhoneScene()
        {
            var now = Time.unscaledTime;
            while (true)
            {
                if (phoneScene == null)
                {
                    if (phoneQueue.Count == 0) return 0;
                    if (phoneQueue[0].Count == 0)
                    {
                        phoneQueue.RemoveAt(0);
                        continue;
                    }
                    phoneScene = phoneQueue[0][0];
                    phoneQueue[0].RemoveAt(0);
                    phoneSceneStart = now;
                }
                if (phoneHold is { } hold) return hold;
                var (speed, _) = 역할앱재연Policy.Pace(phoneQueue.Count);      // 틀고 있는 대본까지 센다
                var progress = (now - phoneSceneStart) * speed / Mathf.Max(0.1f, phoneScene.Seconds);
                if (progress < 1f) return progress;
                phonePrevious = phoneScene;
                phoneScene = null;
                if (phoneQueue.Count > 0 && phoneQueue[0].Count == 0) phoneQueue.RemoveAt(0);
            }
        }

        private void ReplayLastPhoneScript()
        {
            if (phoneLastScript == null || phoneScene != null || phoneQueue.Count > 0) return;
            phoneQueue.Add(new List<재연장면>(phoneLastScript));
        }

        private bool PhonePressed(재연장면 scene, float progress, string label) =>
            scene?.Tap == label && progress > 0.55f && progress < 0.85f;

        // ── 틀 ──

        // 베젤 · 상태줄 · 머리줄(앱 이름 · 카드 · 닫기). 닫았거나 카드로 바꿨으면 null.
        private Rect? PhoneShell(string app, bool card, string hint)
        {
            if (Event.current.type == EventType.Repaint) phoneDrawn++;
            if (!string.IsNullOrEmpty(hint)) Text(PhoneHint, hint, 11, Mint, true, TextAnchor.MiddleCenter);
            var frame = PhoneFrame;
            Paint(frame, PhoneBezel, 30);
            var screen = new Rect(frame.x + 5, frame.y + 5, frame.width - 10, frame.height - 10);
            Paint(screen, PhoneBack, 25);
            var x = screen.x + 10;
            var clock = ClockLabel(state.now);
            Text(new Rect(screen.x + 16, screen.y + 6, 60, 16), clock.Length >= 5 ? clock.Substring(0, 5) : "—", 9, PhoneText, true);
            Paint(new Rect(screen.center.x - 26, screen.y + 6, 52, 13), PhoneBezel, 6.5f);
            Text(new Rect(screen.xMax - 96, screen.y + 6, 84, 16), "게임 속 가상 앱", 8, PhoneSub, align: TextAnchor.MiddleRight);

            var head = screen.y + 26;
            Paint(new Rect(x, head, 18, 18), PhonePrimary, 5);
            Text(new Rect(x, head, 18, 18), "홍", 10, Color.white, true, TextAnchor.MiddleCenter);
            Text(new Rect(x + 24, head - 1, 120, 20), app, 13, PhoneText, true);
            var close = new Rect(screen.xMax - 40, head, 30, 18);
            if (card)
            {
                var cardButton = new Rect(screen.xMax - 78, head, 34, 18);
                Paint(cardButton, PhoneSurface, 9);
                Text(cardButton, "카드", 9, PhoneSub, align: TextAnchor.MiddleCenter);
                if (Hit(cardButton))
                {
                    phoneCardView = true;
                    return null;
                }
            }
            Paint(close, PhoneSurface, 9);
            Text(close, "닫기", 9, PhoneSub, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                actorFocus = null;
                return null;
            }
            return screen;
        }

        // 위쪽 둘로 나눈 칸(배달 · 내 정보 같은). 왼쪽이 고른 칸이면 left = true.
        private void PhoneSegment(float x, float y, float w, string leftLabel, string rightLabel, bool left = true)
        {
            Paint(new Rect(x, y, w, 22), Ink(0xE8EDF0), 11);
            Paint(new Rect(left ? x + 2 : x + w / 2 + 2, y + 2, w / 2 - 4, 18), PhoneSurface, 9);
            Text(new Rect(x, y, w / 2, 22), leftLabel, 10, left ? PhoneText : PhoneSub, left, TextAnchor.MiddleCenter);
            Text(new Rect(x + w / 2, y, w / 2, 22), rightLabel, 10, left ? PhoneSub : PhoneText, !left, TextAnchor.MiddleCenter);
        }

        // 아래 탭 · 보기 전용 · 다시 보기. 탭 줄의 y를 돌려준다.
        private float PhoneFooter(Rect screen, string leftTab, string rightTab, string note, bool left = true)
        {
            var tabs = new Rect(screen.x, screen.yMax - 30, screen.width, 30);
            Paint(new Rect(screen.x, tabs.y - 1, screen.width, 1), PhoneBorder);
            Text(new Rect(tabs.x, tabs.y, tabs.width / 2, tabs.height), leftTab, 10, left ? PhonePrimary : PhoneSub, left, TextAnchor.MiddleCenter);
            Text(new Rect(tabs.center.x, tabs.y, tabs.width / 2, tabs.height), rightTab, 10, left ? PhoneSub : PhonePrimary, !left, TextAnchor.MiddleCenter);
            Text(new Rect(screen.x, tabs.y - 32, screen.width, 12), note, 8, PhoneSub, align: TextAnchor.MiddleCenter);
            if (phoneLastScript != null && phoneScene == null && phoneQueue.Count == 0)
            {
                var again = new Rect(screen.center.x - 60, tabs.y - 19, 120, 15);
                Text(again, "방금 한 일 다시 보기", 8, PhonePrimary, true, TextAnchor.MiddleCenter);
                if (Hit(again)) ReplayLastPhoneScript();
            }
            return tabs.y;
        }

        // 아래에서 올라오는 카드(배달 요청 · 배차 추천). 가상 인물이 누르는 단추의 가운데를 돌려준다.
        private Vector2? PhoneSheet(Rect screen, float tabsY, 재연장면 scene, float progress, string sheetScreen, string title,
            (string Label, string Value)[] rows, string reject, string accept)
        {
            var entering = scene != null && scene.Screen == sheetScreen && scene.Tap == null && phonePrevious?.Screen != sheetScreen;
            var rise = entering ? Ease01(progress * 2f) : 1f;
            Paint(new Rect(screen.x, screen.y + 20, screen.width, tabsY - screen.y - 20), Fade(PhoneBezel, 0.35f * rise), 0);
            var sheetHeight = 88f + rows.Length * 20f;
            var sheet = new Rect(screen.x + 3, tabsY - sheetHeight * rise, screen.width - 6, sheetHeight);
            Paint(sheet, PhoneSurface, 16);
            Text(new Rect(sheet.x + 12, sheet.y + 8, 160, 18), title, 13, PhoneText, true);
            for (var i = 0; i < rows.Length; i++)
            {
                Text(new Rect(sheet.x + 12, sheet.y + 34 + i * 20, 60, 16), rows[i].Label, 9, PhoneSub);
                Text(new Rect(sheet.x + 56, sheet.y + 34 + i * 20, sheet.width - 68, 16), rows[i].Value, 9, PhoneText, false, TextAnchor.MiddleRight);
            }
            var half = (sheet.width - 30) / 2;
            var buttonsY = sheet.y + 44 + rows.Length * 20;
            PhoneButton(new Rect(sheet.x + 12, buttonsY, half, 30), reject, false, false);
            var acceptButton = new Rect(sheet.x + 18 + half, buttonsY, half, 30);
            PhoneButton(acceptButton, accept, true, PhonePressed(scene, progress, accept));
            return scene?.Tap == accept ? acceptButton.center : (Vector2?)null;
        }

        // 위 알림 · 아래 짧은 안내 · 가상 인물의 손가락.
        private void PhoneOverlays(Rect screen, float tabsY, 재연장면 scene, float progress, Vector2? tapAt, string app, string fingerLabel)
        {
            if (scene?.Push != null)
            {
                var drop = Ease01(progress * 3f);
                var banner = new Rect(screen.x + 6, screen.y + 20 - 44 * (1 - drop), screen.width - 12, 36);
                if (banner.yMax > screen.y + 4)
                {
                    Paint(banner, Ink(0x1E242C), 12);
                    Paint(new Rect(banner.x + 8, banner.y + 9, 18, 18), PhonePrimary, 5);
                    Text(new Rect(banner.x + 8, banner.y + 9, 18, 18), "홍", 9, Color.white, true, TextAnchor.MiddleCenter);
                    Text(new Rect(banner.x + 32, banner.y + 3, banner.width - 40, 13), app + " · 지금", 8, Ink(0xB4BEC8));
                    Text(new Rect(banner.x + 32, banner.y + 16, banner.width - 40, 16), scene.Push, 10, Color.white, true);
                }
            }
            if (scene?.Toast != null)
            {
                var toast = new Rect(screen.center.x - 90, tabsY - 64, 180, 24);
                var alpha = Mathf.Clamp01(progress * 4f);
                Paint(toast, Fade(Ink(0x1E242C), alpha), 12);
                Text(toast, scene.Toast, 9, Fade(Color.white, alpha), true, TextAnchor.MiddleCenter);
            }
            if (tapAt is { } target)
            {
                var approach = Ease01(progress / 0.55f);
                var at = Vector2.Lerp(target + new Vector2(46, 60), target, approach);
                var press = progress > 0.55f && progress < 0.85f;
                var size = press ? 26f : 20f;
                Paint(Centered(at, size + 10, size + 10), Fade(PhoneFinger, 0.3f), (size + 10) / 2);
                Paint(Centered(at, size, size), Fade(PhoneFinger, 0.75f), size / 2);
                Text(new Rect(at.x - 70, at.y + 16, 140, 12), fingerLabel, 8, Ink(0x6E50C8), true, TextAnchor.MiddleCenter);
            }
        }

        private static float Ease01(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));

        // 작은 지도: a(출발) → b(들르는 곳) → c(도착). along은 0 = a, 1 = b, 2 = c. 길 · 건물 자료가 아니라 그림이다.
        private void DrawPhoneMap(Rect area, bool working, float along, string tagB, Color colorB, string tagC, Color colorC)
        {
            Paint(area, Ink(0xE2EAE6), 10);
            for (var i = 0; i < 6; i++)
                Line(new Vector2(area.x + 14 + i * 40, area.y + 2), new Vector2(area.x + 4 + i * 40, area.yMax - 2), Ink(0xF0F4F1), 4);
            Line(new Vector2(area.x + 2, area.y + 38), new Vector2(area.xMax - 2, area.y + 26), Ink(0xF0F4F1), 5);
            var a = new Vector2(area.x + 36, area.y + 44);
            var b = new Vector2(area.x + area.width * 0.58f, area.y + 22);
            var c = new Vector2(area.xMax - 26, area.y + 42);
            if (working)
            {
                Line(a, b, PhonePrimary, 2.5f, true);
                Line(b, c, Ink(0x78AAA5), 2.5f, true);
                Paint(Centered(b, 11, 11), Color.white, 5.5f);
                Paint(Centered(b, 8, 8), colorB, 4);
                Paint(Centered(c, 11, 11), Color.white, 5.5f);
                Paint(Centered(c, 8, 8), colorC, 4);
                var tagWidth = PlaceTextWidth(tagB, 8, true) + 14;
                var tag = new Rect(b.x - tagWidth / 2, area.y + 3, tagWidth, 13);
                Paint(tag, colorB, 6.5f);
                Text(tag, tagB, 8, Color.white, true, TextAnchor.MiddleCenter);
                if (!string.IsNullOrEmpty(tagC))
                {
                    var endWidth = PlaceTextWidth(tagC, 8, true) + 14;
                    var tagEnd = new Rect(Mathf.Min(c.x - endWidth / 2, area.xMax - endWidth - 4), c.y - 23, endWidth, 13);
                    Paint(tagEnd, colorC, 6.5f);
                    Text(tagEnd, tagC, 8, Color.white, true, TextAnchor.MiddleCenter);
                }
            }
            var t = working ? Mathf.Clamp(along, 0f, 2f) : 0f;
            var dot = t <= 1f ? Vector2.Lerp(a, b, t) : Vector2.Lerp(b, c, t - 1f);
            Paint(Centered(dot, 13, 13), Color.white, 6.5f);
            Paint(Centered(dot, 9, 9), PhoneInfo, 4.5f);
            // 「현위치」는 파란 점 바로 아래에 붙인다(지도 칸 안으로 당김).
            var here = new Rect(Mathf.Clamp(dot.x - 18, area.x + 4, area.xMax - 40), Mathf.Min(dot.y + 8, area.yMax - 15), 36, 13);
            Paint(here, PhoneInfo, 6.5f);
            Text(here, "현위치", 8, Color.white, true, TextAnchor.MiddleCenter);
        }

        private void DrawPhoneSteps(float x, float y, float width, int step, string[] names)
        {
            var gap = width / (names.Length - 1);
            Paint(new Rect(x, y - 1, width, 2), PhoneBorder);
            Paint(new Rect(x, y - 1, gap * Mathf.Clamp(step, 0, names.Length - 1), 2), PhonePrimary);
            for (var i = 0; i < names.Length; i++)
            {
                var point = new Vector2(x + gap * i, y);
                var reached = i <= step;
                var now = i == step;
                var size = now ? 10f : 7f;
                Paint(Centered(point, size, size), reached ? PhonePrimary : PhoneBorder, size / 2);
                if (!reached) Paint(Centered(point, size - 3, size - 3), PhoneSurface, (size - 3) / 2);
                // 처음 · 끝 이름은 칸 밖으로 나가지 않게 왼쪽 · 오른쪽에 붙인다.
                var first = i == 0;
                var last = i == names.Length - 1;
                var label = first ? new Rect(point.x - 6, y + 6, gap, 12) : last ? new Rect(point.x - gap + 6, y + 6, gap, 12)
                    : new Rect(point.x - gap / 2 - 4, y + 6, gap + 8, 12);
                Text(label, names[i], names.Length > 5 ? 7 : 8, now ? PhoneText : PhoneSub, now,
                    first ? TextAnchor.MiddleLeft : last ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);
            }
        }

        private void PhoneCard(Rect rect)
        {
            Paint(rect, PhoneBorder, 10);
            Paint(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), PhoneSurface, 9);
        }

        private void PhonePill(float x, float y, string text, Color ink, Color fill)
        {
            var width = PlaceTextWidth(text, 8, false) + 12;
            Paint(new Rect(x, y, width, 13), fill, 6.5f);
            Text(new Rect(x, y, width, 13), text, 8, ink, align: TextAnchor.MiddleCenter);
        }

        // 보기 전용 단추: 그리기만 하고 누름을 받지 않는다.
        private void PhoneButton(Rect rect, string label, bool primary, bool pressed)
        {
            if (primary)
            {
                Paint(rect, pressed ? PhonePrimaryDark : PhonePrimary, 8);
                Text(rect, label, 10, Color.white, true, TextAnchor.MiddleCenter);
            }
            else
            {
                Paint(rect, PhoneBorder, 8);
                Paint(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), PhoneSurface, 7);
                Text(rect, label, 10, PhoneSub, align: TextAnchor.MiddleCenter);
            }
        }

        private string PhonePlaceName => BusinessDong()?.Entry.name ?? "동네";

        // ── 홍달 기사 ──

        // 그 기사의 지금 주문: 끝나지 않은 주문 가운데 가장 나중 것, 없으면 가장 나중에 정산한 주문.
        private DispatchOrder PhoneOrderOf(string courierId)
        {
            if (state?.orders == null) return null;
            DispatchOrder open = null, done = null;
            foreach (var order in state.orders)
            {
                if (order.driverId != courierId) continue;
                if (order.stage == "Settled") done = order;
                else open = order;
            }
            return open ?? done;
        }

        private string PhonePay()
        {
            if (phonePayText != null) return phonePayText;
            var summary = engine?.CompletedSummary();
            phonePayText = summary != null && summary.completedOrders > 0 && summary.virtualDriverPay > 0
                ? $"{summary.virtualDriverPay / summary.completedOrders:N0}원 (가상)" : "가상 정산";
            return phonePayText;
        }

        private void DrawRiderPhone(string courierId)
        {
            var courier = state.couriers?.FirstOrDefault(item => item.id == courierId);
            if (courier == null) return;
            var order = PhoneOrderOf(courierId);
            SyncPhone("rider:" + courierId, order?.id, order?.stage, (from, to) =>
            {
                if (to == "Settled") phonePayText = null;
                return 역할앱재연Policy.RiderScript(from, to, to == "Settled" ? "정산 · " + PhonePay() : null);
            });
            var progress = AdvancePhoneScene();
            var rest = 역할앱재연Policy.RiderScreen(order?.stage);
            var scene = phoneScene;
            var view = scene?.Screen ?? rest.Screen;
            var step = scene?.Step ?? rest.Step;
            var shell = PhoneShell("홍달 기사", true, selectedDriver == courierId ? "배차 선택됨 · 대기 매장을 누르세요." : null);
            if (shell is not { } screen) return;
            var x = screen.x + 10;
            var w = screen.width - 20;
            var y = screen.y + 52;
            PhoneSegment(x, y, w, "배달", "내 정보");
            y += 30;
            var working = view == 기사화면.요청 || view == 기사화면.진행;
            var moving = scene != null && scene.Tap == null && scene.Toast == null && scene.Push == null ? Ease01(progress) : 1f;
            var along = step <= 0 ? 0f : step == 1 ? (scene?.Step == 1 ? 0.15f + 0.8f * moving : 0.9f) : step == 2 ? 1f : step == 3 ? 1.5f : 2f;
            DrawPhoneMap(new Rect(x, y, w, 62), working, along, "픽업: 음식점", PhonePrimaryDark, null, PhoneWarn);
            y += 70;

            var (pickupName, menu) = order == null ? ("—", "—") : (order.storeName ?? "음식점", order.menu ?? "메뉴");
            Vector2? tapAt = null;
            if (view != 기사화면.진행)      // 배달 요청 카드는 기다리던 화면 위로 올라온다
            {
                PhoneCard(new Rect(x, y, w, 60));
                PhonePill(x + 10, y + 9, "운행 중", PhoneSuccess, Ink(0xE3F4E9));
                Text(new Rect(x + 10, y + 28, w - 20, 16), "가까운 배달 요청을 기다리고 있어요", 10, PhoneText, true);
                Text(new Rect(x + 10, y + 44, w - 20, 12), "현재 위치 · " + PhonePlaceName, 8, PhoneSub);
                y += 68;
                if (view == 기사화면.완료 && order != null)
                {
                    PhoneCard(new Rect(x, y, w, 46));
                    Text(new Rect(x + 10, y + 6, w - 20, 12), "최근 완료 결과", 8, PhoneSub);
                    Text(new Rect(x + 10, y + 22, w - 90, 16), "전달 완료 · " + menu, 10, PhoneText, true);
                    Text(new Rect(x + w - 90, y + 22, 80, 16), PhonePay(), 9, PhonePrimaryDark, true, TextAnchor.MiddleRight);
                    y += 54;
                }
                PhoneButton(new Rect(x, y, w, 26), "신규 배차 받기", false, false);
            }
            else
            {
                PhoneCard(new Rect(x, y, w, 44));
                Text(new Rect(x + 10, y + 4, 80, 12), "진행 상황", 8, PhoneSub);
                DrawPhoneSteps(x + 14, y + 24, w - 28, step, 역할앱재연Policy.StepNames);
                y += 50;
                PhoneCard(new Rect(x, y, w, 46));
                Text(new Rect(x + 10, y + 4, 80, 12), "픽업지", 8, PhoneSub);
                Text(new Rect(x + 10, y + 16, w - 20, 16), pickupName, 11, PhoneText, true);
                var ready = step >= 3 ? ("픽업 완료", PhoneSub, Ink(0xECF0F3)) : step == 2 ? ("픽업 준비 확인", PhoneSuccess, Ink(0xE3F4E9)) : ("음식 준비 중", PhoneWarn, Ink(0xFDF1E2));
                PhonePill(x + 10, y + 32, ready.Item1, ready.Item2, ready.Item3);
                y += 52;
                PhoneCard(new Rect(x, y, w, 36));
                Text(new Rect(x + 10, y + 3, 80, 12), "전달지", 8, PhoneSub);
                Text(new Rect(x + 10, y + 16, w - 80, 16), PhonePlaceName + " · 가상 수령지", 10, PhoneText, true);
                Text(new Rect(x + w - 74, y + 17, 64, 14), "실제 주소 아님", 8, PhoneSub, align: TextAnchor.MiddleRight);
                y += 42;
                PhoneCard(new Rect(x, y, w, 40));
                Text(new Rect(x + 10, y + 4, 90, 14), "주문 정보", 8, PhoneSub);
                Text(new Rect(x + 90, y + 4, w - 100, 14), menu, 9, PhoneText, align: TextAnchor.MiddleRight);
                Text(new Rect(x + 10, y + 21, 90, 14), "배달료 산정 근거", 8, PhoneSub);
                Text(new Rect(x + 90, y + 21, w - 100, 14), PhonePay(), 9, PhonePrimaryDark, true, TextAnchor.MiddleRight);
                y += 48;
                var main = new Rect(x, y, w * 0.6f, 28);
                PhoneButton(main, "현장 처리", true, PhonePressed(scene, progress, "현장 처리"));
                PhoneButton(new Rect(x + w * 0.6f + 6, y, w * 0.4f - 6, 28), "현장 중단", false, false);
                if (scene?.Tap == "현장 처리") tapAt = main.center;
            }

            var tabsY = PhoneFooter(screen, "배달", "내 정보", "보기 전용 · 가상 기사가 한 일을 재연");
            if (view == 기사화면.요청)
            {
                var rows = new[]
                {
                    ("픽업", pickupName + " · " + menu),
                    ("전달지", PhonePlaceName + " · 가상 수령지"),
                    ("배달료", PhonePay()),
                    ("배차", EngineLine(order)),
                };
                tapAt = PhoneSheet(screen, tabsY, scene, progress, 기사화면.요청, "배달 요청", rows, "거절", "수락") ?? tapAt;
            }
            PhoneOverlays(screen, tabsY, scene, progress, tapAt, "홍달 기사", "가상 기사가 누른 곳");
        }

        // 방향 촬영: 배차 기사 하나를 눌러 휴대폰이 뜨는지(열 때는 재연하지 않음) 보고, 배정 → 수락 대본을 넣어
        // 「수락」을 누르는 장면을 찍은 뒤 재연이 끝나는지 본다. 대본은 보기만 하므로 시계 · 원장 · 주문 단계는 그대로여야 한다.
        private IEnumerator ViewProofRiderPhone(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var courier = state?.couriers?.FirstOrDefault(item => state.orders.Any(order => order.driverId == item.id)) ?? state?.couriers?.FirstOrDefault();
            if (courier == null) yield break;
            var expansionBefore = expansionView;
            var detailsBefore = showDetails;
            var mapOnlyBefore = mapOnly;
            expansionView = true;                 // 빛으로 보는 동 지도의 오른쪽 칸에서 뜬다
            showDetails = true;
            mapOnly = false;
            receipt.phoneLevel = mapLevel.ToString();
            var stagesBefore = string.Join(",", state.orders.Select(order => order.id + ":" + order.stage));
            phoneCardView = false;
            phoneKey = null;
            SelectCourier(courier.id);
            for (var frame = 0; frame < 5; frame++) yield return null;
            phoneDrawn = 0;
            yield return null;
            yield return null;
            receipt.phoneShown = phoneDrawn > 0 && RolePhoneOn(FocusKind.Courier);
            receipt.phoneCourier = courier.id;
            receipt.phoneRestScreen = 역할앱재연Policy.RiderScreen(PhoneOrderOf(courier.id)?.stage).Screen;
            receipt.phoneIdleOnOpen = phoneQueue.Count == 0 && phoneScene == null;
            yield return SaveFrame(directory, "98-rider-phone.png");

            var script = 역할앱재연Policy.RiderScript("Waiting", "Accepted", null);
            receipt.phoneReplayScenes = script.Count;
            EnqueuePhoneScript(script);
            var deadline = Time.realtimeSinceStartup + 6f;
            while (phoneScene?.Tap != "수락" && Time.realtimeSinceStartup < deadline) yield return null;
            phoneHold = 0.7f;
            for (var frame = 0; frame < 3; frame++) yield return null;
            receipt.phoneReplayTap = phoneScene?.Tap ?? string.Empty;
            yield return SaveFrame(directory, "99-rider-phone-accept.png");
            phoneHold = null;
            deadline = Time.realtimeSinceStartup + 8f;
            while ((phoneScene != null || phoneQueue.Count > 0) && Time.realtimeSinceStartup < deadline) yield return null;
            receipt.phoneReplayDrained = phoneScene == null && phoneQueue.Count == 0;
            phoneCardView = true;                 // 「카드」: 예전 기사 카드로
            yield return null;
            yield return null;
            receipt.phoneCardToggle = !RolePhoneOn(FocusKind.Courier) && FocusCovers(new Vector2(1100, 300));
            phoneCardView = false;
            actorFocus = null;
            selectedDriver = null;
            yield return ViewProofRolePhones(directory, receipt);      // 화물 · 창고 · 가게 (SagajeongDispatchRolePhones.cs)
            expansionView = expansionBefore;
            showDetails = detailsBefore;
            mapOnly = mapOnlyBefore;
            yield return null;
            receipt.phoneUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore &&
                stagesBefore == string.Join(",", state.orders.Select(order => order.id + ":" + order.stage));
            holdProofClock = false;
        }
    }
}
