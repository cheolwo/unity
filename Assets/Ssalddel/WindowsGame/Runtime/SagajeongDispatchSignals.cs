using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2026-11-12 위험 신호의 화면(G3c).
    // 신호는 커널에 있다(업무 모듈이 올리고 커널이 저장한다). 화면은 읽어서 보여 주기만 한다.
    //  - 그 자리에서 울린다: 문제가 생긴 매장·점포에서 파동이 퍼진다. 위험은 빨강으로 빠르게, 주의는 금색으로 천천히.
    //  - 한 줄로 알린다: 위쪽 신호 줄에 급한 순서로 세 건까지. "가서 보기"는 그 자리로 데려간다.
    //  - 위험이 새로 생기면 짧은 소리가 한 번 난다. 끌 수 있다.
    public sealed partial class SagajeongDispatchView
    {
        private const int SignalRows = 3;
        private const float SignalRowHeight = 30;
        private const float DangerBeat = 1.05f;    // 위험 파동이 한 번 퍼지는 시간(초)
        private const float CautionBeat = 1.9f;
        private static readonly Color SignalRed = Ink(0xFF8A7A);

        private List<Signal> signals = new();                              // 위험 먼저, 그 안에서는 오래된 것 먼저
        private TerritorySnapshot territory;
        private readonly HashSet<string> signalHeard = new();
        private readonly Dictionary<string, float> signalSeenAt = new();   // 화면이 처음 본 실제 시각. 파동의 박자를 여기서 센다
        private AudioSource signalAudio;
        private AudioClip signalChime;
        private bool signalSound = true;

        private bool SignalBarVisible => expansionView && !mapOnly && !showDesk && BarSignals.Count > 0 && mapLevel != MapLevel.Nation;

        private Rect SignalBar
        {
            get
            {
                var top = ChainStripVisible ? ChainStrip.yMax + 6 : mapLevel == MapLevel.Dong ? 92 : 136;
                return new Rect(TopStripLeft, top, 976f - TopStripLeft, Mathf.Min(BarSignals.Count, SignalRows) * SignalRowHeight + 6);
            }
        }

        private bool SignalBarCovers(Vector2 point) => SignalBarVisible && SignalBar.Contains(point);

        private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, Mathf.Clamp01(alpha));

        private static void Disc(Vector2 centre, float radius, Color color) =>
            Paint(Centered(centre, radius * 2, radius * 2), color, radius);

        // 속이 빈 원. 테두리 두께를 주면 안쪽은 그리지 않는다.
        private static void Hoop(Vector2 centre, float radius, Color color, float width) =>
            GUI.DrawTexture(Centered(centre, radius * 2, radius * 2), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                color, width, radius);

        private static Color SignalColor(Signal signal) => signal.IsDanger ? SignalRed : Gold;

        private void RefreshSignals()
        {
            if (Event.current.type != EventType.Layout) return;
            var kernel = engine?.Kernel;
            signals = kernel == null ? new List<Signal>()
                : kernel.Signals.Select(SoftenForAutopilot)        // 카1: 맡김이 켜져 있으면 배차 대기 위험은 화면에서 주의로 (SagajeongDispatchDayClose.cs)
                    .OrderByDescending(item => item.IsDanger).ThenBy(item => item.SinceMs).ToList();
            territory = ReadTerritory();      // 행정동 팩이면 구 지도용으로 법정동 묶음 사본이 온다 (SagajeongDispatchDongBusiness.cs)
            SyncDongCalendar();

            var now = Time.unscaledTime;
            var ring = false;
            foreach (var signal in signals)
            {
                if (!signalSeenAt.ContainsKey(signal.Id)) signalSeenAt[signal.Id] = now;
                if (signal.IsDanger && signalHeard.Add(signal.Id)) ring = true;
            }
            if (signalSeenAt.Count > signals.Count)
            {
                foreach (var id in signalSeenAt.Keys.Where(id => signals.All(item => item.Id != id)).ToList())
                {
                    signalSeenAt.Remove(id);
                    signalHeard.Remove(id);
                }
            }
            if (ring) PlaySignalChime();
        }

        // 파동의 박자(0~1). 신호를 처음 본 때부터 센다.
        private float SignalPhase(Signal signal)
        {
            var seen = signalSeenAt.TryGetValue(signal.Id, out var at) ? at : Time.unscaledTime;
            return (Time.unscaledTime - seen) / (signal.IsDanger ? DangerBeat : CautionBeat);
        }

        private void DrawRipple(Vector2 centre, Signal signal, float size)
        {
            var color = SignalColor(signal);
            if (!mapEffects)
            {
                // r157 간단히: 움직이지 않는 고리 하나. 신호의 자리와 색(위험·주의)은 그대로 보인다.
                Disc(centre, 30 * size, Fade(color, 0.13f));
                Hoop(centre, 24 * size, Fade(color, 0.85f), 2.2f);
                return;
            }
            var phase = SignalPhase(signal);
            Disc(centre, 34 * size, Fade(color, 0.11f + 0.05f * Mathf.Sin(phase * 2f * Mathf.PI)));
            for (var index = 0; index < 3; index++)
            {
                var spread = Mathf.Repeat(phase + index / 3f, 1f);
                Hoop(centre, (12 + 70 * spread) * size, Fade(color, Mathf.Pow(1f - spread, 1.6f) * 0.95f), 2.8f - 1.4f * spread);
            }
        }

        private void DrawSignalBadge(Vector2 centre, Signal signal)
        {
            var pulse = mapEffects ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * (signal.IsDanger ? 9f : 5f)) : 1f;
            Disc(centre, 9 * pulse, Canvas);
            Disc(centre, 7.5f * pulse, SignalColor(signal));
            Text(Centered(centre, 16, 16), "!", 11, Canvas, true, TextAnchor.MiddleCenter);
        }

        // 동네 지도에서 신호가 울리는 자리. 매장은 매장 표식, 그 밖의 곳은 건물 지붕 가운데다.
        private bool TrySignalPoint(Signal signal, out Vector2 point)
        {
            point = default;
            var place = signal.PlaceId ?? string.Empty;
            if (place.StartsWith("store:", StringComparison.Ordinal) && state?.stores != null)
            {
                var id = place.Substring(6);
                var store = state.stores.FirstOrDefault(item => item.id == id);
                if (store != null)
                {
                    point = PlotScene(store.latitude, store.longitude);
                    return true;
                }
            }
            if (!buildingByPlace.TryGetValue(place, out var index)) return false;
            point = RoofCentre(buildingShapes[index]);
            return true;
        }

        // 동네 지도(GUI.BeginGroup(Field) 안)에서 부른다. 화면 밖에서 울리면 가장자리에 작게 보인다.
        private void DrawSignalRipples()
        {
            if (Event.current.type != EventType.Repaint || signals.Count == 0) return;
            LoadRoleBook();
            var top = (expansionView && !mapOnly ? (SignalBarVisible ? SignalBar.yMax : 92) : 0) - Field.y + 30;
            var bottom = Field.height + Field.y - (expansionView && !mapOnly ? 110 : 30);
            foreach (var signal in signals)
            {
                if (!TrySignalPoint(signal, out var point) || point.x < -50000f) continue;   // 숨긴 행정동의 신호는 그리지 않는다
                var inside = point.x >= 24 && point.x <= Field.width - 24 && point.y >= top && point.y <= bottom;
                if (inside)
                {
                    DrawRipple(point, signal, 1f);
                    DrawSignalBadge(point + new Vector2(30, -4), signal);     // 이름표(위쪽)를 가리지 않게 오른쪽에 둔다
                    continue;
                }
                var edge = new Vector2(Mathf.Clamp(point.x, 34, Field.width - 34), Mathf.Clamp(point.y, top, bottom));
                DrawRipple(edge, signal, 0.45f);
                DrawSignalBadge(edge, signal);
            }
        }

        private void DrawSignalBar()
        {
            if (!SignalBarVisible) return;
            var bar = SignalBar;
            var signals = BarSignals;      // 맡김이 켜져 있으면 맡김이 하는 일은 뺀 목록 (SagajeongDispatchNotices.cs)
            Paint(bar, signals[0].IsDanger ? SignalRed : Rim, 10);
            Paint(new Rect(bar.x + 1, bar.y + 1, bar.width - 2, bar.height - 2), Canvas, 9);
            var shown = Mathf.Min(signals.Count, SignalRows);
            for (var index = 0; index < shown; index++)
            {
                var signal = signals[index];
                var color = SignalColor(signal);
                var row = new Rect(bar.x + 6, bar.y + 3 + index * SignalRowHeight, bar.width - 12, SignalRowHeight - 2);
                var beat = mapEffects ? Mathf.Repeat(SignalPhase(signal), 1f) : 0.4f;
                var dot = new Vector2(row.x + 13, row.center.y);
                Hoop(dot, 5 + 6 * beat, Fade(color, 1f - beat), 1.5f);
                Disc(dot, 4.5f, color);

                var right = row.xMax;
                if (TidyScreen)
                {
                    // 정리한 첫 화면: 소리 단추는 설정 칸으로 갔다.
                }
                else if (index == 0)
                {
                    var sound = new Rect(right - 54, row.y + 2, 54, row.height - 4);
                    if (ExpansionButton(sound, signalSound ? "소리 켬" : "소리 끔", true, Tile)) signalSound = !signalSound;
                    right = sound.x - 6;
                }
                else
                    right -= 60;
                var go = new Rect(right - 74, row.y + 2, 74, row.height - 4);
                if (ExpansionButton(go, "가서 보기", true, Tile))
                {
                    GoToSignal(signal);
                    return;     // 지도 단계가 바뀌면 이 줄의 자리도 바뀐다. 남은 줄은 다음 화면에서 그린다
                }
                right = go.x - 6;
                var dong = SignalDong(signal);
                if (dong != null && !string.IsNullOrEmpty(signal.ActionLabel))
                {
                    var act = new Rect(right - 96, row.y + 2, 96, row.height - 4);
                    if (ExpansionButton(act, signal.ActionLabel, true, Tile))
                    {
                        // 동별 수요의 신호는 실제로 기사·제휴를 늘려야 꺼진다. 예전 가상 매장의 신호는 처리만 하면 꺼진다.
                        if (signal.OsId == BusinessIds.DongDemand) SendDongSignalAction(signal, dong);
                        else SendTerritory("respond", dong);
                        return;     // 신호 목록이 바뀌었다
                    }
                    right = act.x - 6;
                }
                var more = index == shown - 1 && signals.Count > shown ? $"  외 {signals.Count - shown}건" : string.Empty;
                Text(new Rect(row.x + 28, row.y, 168, row.height), (signal.IsDanger ? "위험 · " : "주의 · ") + signal.Title, 12, color, true);
                Text(new Rect(row.x + 198, row.y, right - row.x - 204, row.height), signal.Message + more, 11, Paper);
            }
        }

        private static string SignalDong(Signal signal) =>
            signal.PlaceId != null && signal.PlaceId.StartsWith("dong:", StringComparison.Ordinal) ? signal.PlaceId.Substring(5) : null;

        private void GoToSignal(Signal signal)
        {
            var dong = SignalDong(signal);
            if (dong != null)
            {
                // 행정동 깃발이면 동네 2.5D에서 그 동에 맞춘다. 안 되면 구 지도에서 그 동이 속한 법정동을 고른다.
                if (!FocusDongFlag(dong)) ShowTerritoryDong(DongLegalOf(dong));
                return;
            }
            var place = signal.PlaceId ?? string.Empty;
            LoadRoleBook();
            if (mapLevel != MapLevel.Dong) ReturnToDong();
            ShowDongOfPlace(place);      // 한 동만 보는 중이면 그 자리가 든 동으로 넘어간다
            if (signal.ReasonCode == "dispatch-waiting" && place.StartsWith("store:", StringComparison.Ordinal))
            {
                // 기사를 지정해야 하는 일이다. 매장을 고른 상태로 둔다.
                actorFocus = (FocusKind.Store, place.Substring(6));
                showDetails = true;
                dongPanelOpen = false;
                zonePanelOpen = false;
            }
            else
                GoToPlace(place);        // 내 매장이면 왼쪽 매장 운영 창 (SagajeongDispatchStoreDesk.cs)
            if (mapLevel != MapLevel.Dong) return;
            // 그 자리가 화면 밖이면 동네 전체 보기로 물러난다. (자리는 지도 영역 안의 좌표다.)
            var visible = TrySignalPoint(signal, out var point) &&
                new Rect(24, 100 - Field.y, Field.width - 48, Field.height + 2 * Field.y - 220).Contains(point);
            if (!visible) ShowWholeDong();
        }

        private void PlaySignalChime()
        {
            if (!signalSound) return;
            if (signalAudio == null)
            {
                if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
                signalAudio = gameObject.AddComponent<AudioSource>();
                signalAudio.playOnAwake = false;
                signalAudio.spatialBlend = 0f;
                // 두 음이 이어지는 짧은 소리를 그 자리에서 만든다. 소리 파일을 싣지 않는다.
                const int rate = 44100;
                var samples = new float[(int)(rate * 0.42f)];
                for (var index = 0; index < samples.Length; index++)
                {
                    var time = index / (float)rate;
                    var second = time >= 0.16f;
                    var local = second ? time - 0.16f : time;
                    samples[index] = Mathf.Sin(2f * Mathf.PI * (second ? 660f : 880f) * time) * Mathf.Exp(-local * 14f) * 0.5f;
                }
                signalChime = AudioClip.Create("Hongdal signal", samples.Length, 1, rate, false);
                signalChime.SetData(samples, 0);
            }
            signalAudio.PlayOneShot(signalChime, 0.6f);
        }

        private void ReleaseSignals()
        {
            if (signalChime != null) Destroy(signalChime);
            signalChime = null;
        }
    }
}
