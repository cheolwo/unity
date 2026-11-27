using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Ssalddel.WindowsGame
{
    // 입장 → 자료 불러오기 → 디오라마 등장 → 아이콘 집중 (2026-11-24 16:52 사용자: 「각각의 입장의 카드로 선택을 해서 들어갈 때 데이터가 로드가 되는 창이 있고,
    //  그렇게 로드가 되면은 디오라마가 등장을 해서 이제 뭐 데이터들이 디오라마 상에 표현이 되고, 어떤 아이콘을 딱 클릭을 했을 때 그것과 관련해서 집중이 돼가지고
    //  뭔가 이게 강조가 된다거나 그런 느낌이 있고, 그것과 관련한 정보들도 강조 표시가 되는 뭔가 그런 작용이 일어나는지 한번 검증을 … 안 돼 있으면 보완을 해주고」)
    //  점검(16:55): 불러오기 창 없음 · 디오라마로 가지 않음 · 디오라마 시설 모양은 이름표(마우스 올림)만 있고 누르기 없음 → 셋을 만든다.
    //  1) 불러오기 창: 입장 카드를 고르면 그 입장의 필수 + 고른 부가 자료를 묶음별 한 줄씩 차례로 읽는다(읽은 수 · 걸린 시간). 뺀 부가는 「읽지 않음」.
    //  2) 디오라마 등장: 다 읽으면 동 디오라마로(전국 · 구에 있었으면 동네로) 들어가 모듈이 모이고, 자료가 천천히 떠오른다. 위에 「올린 자료」 띠.
    //  3) 아이콘 집중: 디오라마의 생활 시설 모양을 누르면 그 시설에 집중 — 빛 기둥 · 고리, 이어진 것(같은 갈래 · 짝 갈래 둘레)은 고리, 나머지는 옅게.
    //     정류장 · 역 출입구 · 시장은 지나는 버스 노선을, 보행등 · 입구 · 주차 · 택배함은 가까운 길을 밝게 덧그린다. 옆에 집중 카드(입장의 필수 · 부가 표시 ·
    //     둘레 수 · 버스 · 길 속도 · 가까운 역 시간대 승하차). 같은 모양이나 빈 곳을 누르면 풀린다. 뺀 부가(버스 · 길) 자료는 읽지 않은 채로 둔다.
    //  검증(17:05 ~ 17:39, v-flow-1~6 · v-flow-trace.txt): 집중은 걸렸는데 카드가 위 업무 띠 밑에 숨어 안 보였다 → 카드는 판 · 위 띠 · 고른 매장 표시 · 입장 띠를
    //     피해 집중한 것 가까이 자리를 잡고(16점 칸 누적 합), 지도 표시를 다 그린 뒤에 그린다(DrawDongFocusCardLate). 집중하면 둘레를 한 겹 어둡게.
    //     입장 띠는 디오라마 아래쪽(552)으로. 촬영은 고른 시설을 화면 가운데로 옮겨 찍는다.
    public sealed partial class SagajeongDispatchView
    {
        // ── 1) 불러오기 창 ────────────────────────────────────────────────────

        private sealed class StanceLoadStep
        {
            public string Label, Items, Result;
            public int Tier;            // 1 필수 · 2 부가 · 3 뺀 부가(읽지 않음) · 0 디오라마
            public Func<string> Load;
            public float Seconds;
            public bool Done;
        }

        private bool stanceLoadingOpen;
        private Stance stanceLoading;
        private readonly List<StanceLoadStep> stanceLoadSteps = new List<StanceLoadStep>();
        private int stanceLoadIndex;
        private float stanceLoadNext;
        private float stanceLoadDoneAt = -1f;
        private int stanceLoadHold = -1;            // 촬영: 이 줄에서 멈춘다
        private float stanceEnterAt = -100f;
        private string stanceEnterNote;
        private const float StanceStepGap = 0.16f;

        private bool StanceLoadingVisible => stanceLoadingOpen;
        private static readonly Rect StanceEnterBand = new Rect(Width * 0.5f - 330f, 552f, 660f, 30f);
        private bool StanceEnterBandShown => stanceEnterNote != null && !stanceLoadingOpen && Time.unscaledTime - stanceEnterAt < 4.5f;

        // 입장 카드 · ESC 설정이 부른다: 자료 묶음을 켜고(ApplyStance) 불러오기 창을 연다
        private void BeginStanceLoading(Stance stance, bool save)
        {
            ApplyStance(stance, save);
            stanceLoading = stance;
            stanceLoadIndex = 0;
            stanceLoadDoneAt = -1f;
            stanceLoadNext = Time.unscaledTime + 0.3f;
            stanceLoadingOpen = true;
            BuildStanceLoadSteps(stance);
            Debug.Log($"Hongdal stance loading: {stance.Name} · {stanceLoadSteps.Count}줄");
        }

        private static string StancePackOf(string id) => id switch
        {
            "road" => "road",
            "bus" => "bus",
            "station-pulse" => "station",
            "day-night" or "weather" or "today" => "calendar",
            "floating" => "floating",
            "tiles" => "tiles",
            "subway" => "subway",
            "place" or "grade" or "gyeong" or "downtown" or "lore" or "spread" => "game",
            "season-list" => "market",
            _ when id.StartsWith("ops:", StringComparison.Ordinal) => "ops",      // 지휘관 작전 자료(11-25)
            _ => id.StartsWith("fac:", StringComparison.Ordinal) ? "facilities" : id.StartsWith("fact:", StringComparison.Ordinal) ? "market" : "game",
        };

        private (string Label, Func<string> Load) StancePackLoader(string pack) => pack switch
        {
            "subway" => ("지하철 노선 · 역", () => $"역 {EnsureSubwayGu()?.stations?.Length ?? 0}곳"),
            "calendar" => ("하루 자료(점등 · 비 · 눈)", () => $"{EnsureCalendar()?.days?.Length ?? 0}일"),
            "road" => ("길 속도(10월 평일 · 시각별)", () => $"구간 {EnsureRoads()?.links?.Length ?? 0}"),
            "bus" => ("버스 노선(중랑을 지나는)", () => $"{EnsureBus()?.routes?.Length ?? 0}노선"),
            "station" => ("역의 하루(시간대 승하차)", () => $"{EnsureStationHours()?.stations?.Length ?? 0}역"),
            "facilities" => ("생활 시설 · 위치 표식", () => $"{(EnsureFacilities()?.k?.Length ?? 0) + (EnsureGroundMarks()?.k?.Length ?? 0):N0}곳"),
            "floating" => ("유동인구(서울 행정동)", () => LoadFloat() ? $"{floatPack.dongs.Length}동" : "없음"),
            "tiles" => ("타일 지도", () => EnsureHexTiles() != null ? "읽음" : "없음"),
            "market" => ("절기 장터 · 농수산물 흐름", () => $"품목 {(LoadJangteo() ? jangteo.items.Length : 0)} · 자료 줄 {(LoadFlow() ? flow.facts.Length : 0)}"),
            "ops" => ("작전 자료(키워드 · 진입로 · 길목 · 고지)", () => "동에 들어가면 그 동의 길 · 건물로 셈"),
            _ => ("게임 요소(경 수치 · 플레이스 · 등급 …)", () => "바로"),
        };

        private void BuildStanceLoadSteps(Stance stance)
        {
            stanceLoadSteps.Clear();
            var order = new List<string>();
            var tiers = new Dictionary<string, int>();
            var names = new Dictionary<string, List<string>>();
            void Add(string id, int tier)
            {
                var pack = StancePackOf(id);
                if (!tiers.ContainsKey(pack))
                {
                    order.Add(pack);
                    tiers[pack] = tier;
                    names[pack] = new List<string>();
                }
                else tiers[pack] = Mathf.Min(tiers[pack], tier);
                var brief = StanceInfoItemOf(id)?.brief;
                if (!string.IsNullOrEmpty(brief) && !names[pack].Contains(brief)) names[pack].Add(tier == 1 ? brief : tier == 2 ? brief + "(부가)" : brief + "(뺌)");
            }
            if (stance.Info?.entries != null)
                foreach (var entry in stance.Info.entries)
                    Add(entry.id, entry.tier == 1 ? 1 : StanceOptionalChosen(stance.Key, entry.id) ? 2 : 3);
            else Add("subway", 1);
            foreach (var pack in order.OrderBy(pack => tiers[pack]))
            {
                var (label, load) = StancePackLoader(pack);
                var tier = tiers[pack];
                stanceLoadSteps.Add(new StanceLoadStep
                {
                    Label = label, Items = string.Join(" · ", names[pack]), Tier = tier,
                    Load = tier == 3 ? null : load, Result = tier == 3 ? "읽지 않음(부가에서 뺌)" : null,
                });
            }
            stanceLoadSteps.Add(new StanceLoadStep { Label = "디오라마 세우기", Items = "동네 모형에 자료를 올린다", Tier = 0, Load = EnterStanceDiorama });
        }

        // Update에서: 한 줄씩 읽는다(화면이 따라올 틈을 두고)
        private void StepStanceLoading()
        {
            if (!stanceLoadingOpen) return;
            var now = Time.unscaledTime;
            if (stanceLoadIndex >= stanceLoadSteps.Count)
            {
                if (stanceLoadDoneAt < 0f) stanceLoadDoneAt = now;
                if (now - stanceLoadDoneAt > 0.7f && stanceLoadHold < 0) stanceLoadingOpen = false;
                return;
            }
            if (now < stanceLoadNext || stanceLoadHold >= 0 && stanceLoadIndex >= stanceLoadHold) return;
            var step = stanceLoadSteps[stanceLoadIndex];
            if (step.Load != null)
            {
                var watch = Stopwatch.StartNew();
                try { step.Result = step.Load(); }
                catch (Exception error)
                {
                    step.Result = "읽지 못함";
                    Debug.LogWarning("Hongdal stance loading: " + step.Label + " · " + error.Message);
                }
                step.Seconds = (float)watch.Elapsed.TotalSeconds;
            }
            step.Done = true;
            stanceLoadIndex++;
            stanceLoadNext = now + (step.Tier == 3 ? 0.05f : StanceStepGap);
        }

        private string EnterStanceDiorama()
        {
            // 입장 패키지(11-25 13:45): 기본 판(폰 포함)은 동네 지도가 닫혀 있다 → 패키지의 집 동을 공개판 디오라마로 연다.
            //  지휘관은 판독 층이 공개판 디오라마에 있으므로 전체판에서도 공개판으로 들어간다.
            var package = stanceLoading?.Info?.package;
            if ((!dongMapOpen || stanceLoading?.Key == "commander") && !string.IsNullOrEmpty(package?.home))
            {
                stanceEnterAt = Time.unscaledTime;
                stanceEnterNote = stanceLoading.Key == "commander"
                    ? $"입장 · 지휘관 — {package.homeName} 작전 지도 · 판독 층을 켰습니다(진입로 · 길목 · 고지)"
                    : $"입장 · {stanceLoading.Name} — {package.homeName} 디오라마에 자료를 올렸습니다";
                if (OpenPublicDong(package.home, package.homeName, package.homeGu)) return package.homeName + " 디오라마(공개판)";
                stanceEnterNote = null;
                return "지도(디오라마를 열지 못함)";
            }
            if (mapLevel != MapLevel.Dong) ReturnToDong();
            if (dioramaRoot != null && !dioramaOn) dioramaOn = true;
            LoadDataUnit(CurrentDataUnit());                                  // 동 단위 묶음을 바로 꺼낸다(동 시설 · 입구 등)
            if (DongModulesActive && dongSpread >= 0f) dongSpread = 1f;      // 모듈이 벌어졌다 모이며 등장
            stanceEnterAt = Time.unscaledTime;
            var need = stanceLoadSteps.Count(step => step.Tier == 1);
            var extra = stanceLoadSteps.Count(step => step.Tier == 2);
            stanceEnterNote = $"입장 · {stanceLoading?.Name} — 필수 {need}묶음 · 부가 {extra}묶음을 디오라마에 올렸습니다 · 시설 모양을 누르면 집중";
            dongFocusIndex = -1;
            return mapLevel == MapLevel.Dong ? (viewedDong?.name ?? "동네") + " 디오라마" : "지도";
        }

        // 입장 뒤 자료가 떠오르는 정도(0~1)
        private float DongDataRise()
        {
            var since = Time.unscaledTime - stanceEnterAt;
            return since > 2.5f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((since - 0.45f) / 0.9f));
        }

        // OnGUI 맨 위(논리 좌표): 불러오기 창 · 들어간 뒤의 띠
        private void DrawStanceLoading()
        {
            var since = Time.unscaledTime - stanceEnterAt;
            if (!stanceLoadingOpen && since < 4.5f && stanceEnterNote != null && !StancePickerVisible)
            {
                var alpha = Mathf.Clamp01((4.5f - since) / 0.8f) * Mathf.Clamp01(since / 0.3f);
                var band = StanceEnterBand;                                     // 디오라마 아래쪽(위 띠 · 알림과 겹치지 않게)
                Paint(band, Fade(Canvas, 0.92f * alpha), 9);
                Paint(new Rect(band.x, band.y, 4f, band.height), Fade(stanceLoading?.Tint ?? Mint, alpha), 2);
                Text(band, stanceEnterNote, 12, Fade(Paper, alpha), true, TextAnchor.MiddleCenter);
            }
            if (!stanceLoadingOpen || stanceLoading == null) return;
            var stance = stanceLoading;
            Paint(new Rect(0, 0, Width, Height), Fade(Ink(0x080D13), 0.93f));
            var rows = stanceLoadSteps.Count;
            var panel = new Rect(Width * 0.5f - 330f, Mathf.Max(70f, Height * 0.5f - (rows * 34f + 150f) * 0.5f), 660f, rows * 34f + 150f);
            Paint(panel, Fade(stance.Tint, 0.85f), 14);
            Paint(new Rect(panel.x + 2, panel.y + 2, panel.width - 4, panel.height - 4), Canvas, 13);
            var done = stanceLoadIndex >= stanceLoadSteps.Count;
            Paint(new Rect(panel.x + 24, panel.y + 26, 12, 12), stance.Tint, 6);
            Text(new Rect(panel.x + 44, panel.y + 18, 400, 26), $"입장 · {stance.Name}", 18, Paper, true);
            Text(new Rect(panel.x + 24, panel.y + 46, panel.width - 48, 18), done ? "다 불러왔습니다 · 디오라마로 들어갑니다" : "자료를 불러오는 중 · 필수 → 부가 차례", 12, done ? Mint : Soft, true);
            var need = stanceLoadSteps.Count(step => step.Tier == 1);
            var extra = stanceLoadSteps.Count(step => step.Tier == 2);
            var skipped = stanceLoadSteps.Count(step => step.Tier == 3);
            Text(new Rect(panel.x + 24, panel.y + 46, panel.width - 48, 18), $"필수 {need} · 부가 {extra}" + (skipped > 0 ? $" · 뺀 부가 {skipped}" : string.Empty), 11, Soft, false,
                TextAnchor.MiddleRight);
            var y = panel.y + 74;
            for (var index = 0; index < stanceLoadSteps.Count; index++)
            {
                var step = stanceLoadSteps[index];
                var row = new Rect(panel.x + 18, y, panel.width - 36, 30);
                var current = index == stanceLoadIndex && !done;
                Paint(row, current ? Fade(stance.Tint, 0.16f) : Fade(Tile, step.Done ? 0.55f : 0.3f), 6);
                var tint = step.Tier == 1 ? Gold : step.Tier == 2 ? OptionalTint : step.Tier == 3 ? Fade(Soft, 0.5f) : Mint;
                Paint(new Rect(row.x, row.y + 6, 3, row.height - 12), tint, 1.5f);
                var tag = step.Tier == 1 ? "필수" : step.Tier == 2 ? "부가" : step.Tier == 3 ? "뺌" : "끝";
                Text(new Rect(row.x + 10, row.y, 30, row.height), tag, 9, tint, true, TextAnchor.MiddleLeft);
                Text(new Rect(row.x + 42, row.y + 1, 230, 16), step.Label, 11, step.Tier == 3 ? Fade(Soft, 0.6f) : Paper, true);
                Text(new Rect(row.x + 42, row.y + 15, 330, 14), step.Items, 9, Soft);
                var status = step.Tier == 3 ? step.Result
                    : step.Done ? $"✓ {step.Result} · {step.Seconds * 1000f:0} ms"
                    : current ? "읽는 중 …" : "기다림";
                Text(new Rect(row.xMax - 230, row.y, 222, row.height), status, 10, step.Done && step.Tier != 3 ? Mint : Soft, step.Done, TextAnchor.MiddleRight);
                y += 34;
            }
            var bar = new Rect(panel.x + 24, panel.yMax - 44, panel.width - 48, 8);
            Paint(bar, Fade(Tile, 0.9f), 4);
            var fraction = stanceLoadSteps.Count == 0 ? 1f : (float)stanceLoadIndex / stanceLoadSteps.Count;
            Paint(new Rect(bar.x, bar.y, Mathf.Max(8f, bar.width * fraction), bar.height), stance.Tint, 4);
            Text(new Rect(panel.x + 24, panel.yMax - 32, panel.width - 48, 18),
                "뺀 부가는 읽지 않습니다 · 부가는 입장 카드의 칩이나 ESC → 화면 · 지도 「입장 자료」에서 바꿉니다", 10, Soft, false, TextAnchor.MiddleCenter);
            if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel) Event.current.Use();
        }

        // ── 3) 디오라마 아이콘 집중 ──────────────────────────────────────────────

        private int dongFocusIndex = -1;            // 집중한 생활 시설(EnsureFacilities 차례)
        private int dongHoverFacility = -1;         // 지난 그리기에서 마우스 아래 시설
        private int dongFocusProofGroup = -1;       // 촬영: 화면 가운데에 가장 가까운 이 갈래 시설에 집중
        private int dongFocusPrepared = -2;
        private readonly HashSet<int> dongFocusRelated = new HashSet<int>();
        private readonly List<int> dongFocusBuses = new List<int>();
        private readonly List<(int Link, float Metres)> dongFocusRoads = new List<(int, float)>();
        private string dongFocusStation;
        private float dongFocusStationMetres;
        private int dongFocusSame;
        private float dongFocusNearest;
        private bool dongFocusOnScreen;
        private Vector2 dongFocusFoot, dongFocusUp;
        private string dongFocusLabel;
        private int dongFocusGroup;

        private static int[] DongFocusPartners(int group) => group switch
        {
            0 => new[] { 0 },
            1 => new[] { 1 },
            2 => new[] { 2, 0 },
            3 => new[] { 3, 5 },
            5 => new[] { 5, 3 },
            6 => new[] { 6, 1 },
            7 => new[] { 7, 1 },
            _ => new[] { group },
        };

        private static float DongFocusRadius(int group) => group == 7 ? 400f : group is 0 or 2 ? 150f : 250f;
        private static bool DongFocusBusGroup(int group) => group is 1 or 6 or 7;
        private static bool DongFocusRoadGroup(int group) => group is 0 or 2 or 8;

        // 지도 누르기(SagajeongDispatchMap.cs): 시설 모양 위면 집중(같은 모양이면 풀기) · 빈 곳이면 풀고 건물 고르기로
        private bool PickDongFocus()
        {
            if (!mapClick || mapLevel != MapLevel.Dong) return false;
            if (dongHoverFacility >= 0)
            {
                dongFocusIndex = dongHoverFacility == dongFocusIndex ? -1 : dongHoverFacility;
                mapClick = false;
                Debug.Log("Hongdal dong focus: " + (dongFocusIndex < 0 ? "풀림(같은 모양)" : "시설 " + dongFocusIndex));
                return true;
            }
            if (dongFocusIndex >= 0)
            {
                dongFocusIndex = -1;
                Debug.Log("Hongdal dong focus: 풀림(빈 곳)");
            }
            return false;
        }

        private int DongFocusMark(int index)
        {
            if (dongFocusIndex < 0) return 0;
            if (index == dongFocusIndex) return 1;
            return dongFocusRelated.Contains(index) ? 2 : 3;
        }

        private static float PolylineMetres(float[] p, float x, float y)
        {
            var best = float.MaxValue;
            if (p == null) return best;
            for (var i = 0; i + 3 < p.Length; i += 2)
            {
                var a = new Vector2(p[i], p[i + 1]);
                var b = new Vector2(p[i + 2], p[i + 3]);
                var ab = b - a;
                var t = ab.sqrMagnitude < 0.01f ? 0f : Mathf.Clamp01(Vector2.Dot(new Vector2(x, y) - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(new Vector2(x, y), a + ab * t));
            }
            return best;
        }

        // 집중이 바뀌면 이어진 것을 한 번 셈한다(시설 · 버스 · 길 · 역)
        private void PrepareDongFocus(FacilityPack pack)
        {
            if (dongFocusPrepared == dongFocusIndex) return;
            dongFocusPrepared = dongFocusIndex;
            dongFocusRelated.Clear();
            dongFocusBuses.Clear();
            dongFocusRoads.Clear();
            dongFocusStation = null;
            dongFocusSame = 0;
            dongFocusNearest = float.MaxValue;
            if (dongFocusIndex < 0 || pack?.k == null || dongFocusIndex >= pack.k.Length) return;
            var group = FacilityGroup(pack.keys[pack.k[dongFocusIndex]]);
            dongFocusGroup = group;
            var x = pack.x[dongFocusIndex];
            var y = pack.y[dongFocusIndex];
            var partners = DongFocusPartners(group);
            var radius = DongFocusRadius(group);
            for (var i = 0; i < pack.k.Length; i++)
            {
                if (i == dongFocusIndex) continue;
                var other = FacilityGroup(pack.keys[pack.k[i]]);
                if (Array.IndexOf(partners, other) < 0) continue;
                var metres = Vector2.Distance(new Vector2(x, y), new Vector2(pack.x[i], pack.y[i]));
                if (metres > radius) continue;
                dongFocusRelated.Add(i);
                if (other != group) continue;
                dongFocusSame++;
                dongFocusNearest = Mathf.Min(dongFocusNearest, metres);
            }
            // 버스: 읽어 둔 것만(뺀 부가는 읽지 않는다)
            var bus = busLayerOn ? EnsureBus() : busPack;
            if (DongFocusBusGroup(group) && bus?.routes != null)
            {
                var reach = group == 1 ? 40f : 150f;
                for (var r = 0; r < bus.routes.Length; r++)
                {
                    if (bus.routes[r]?.parts == null) continue;
                    if (bus.routes[r].parts.Any(part => PolylineMetres(part?.p, x, y) <= reach)) dongFocusBuses.Add(r);
                }
            }
            var roads = roadLayerOn ? EnsureRoads() : roadPack;
            if (DongFocusRoadGroup(group) && roads?.links != null)
            {
                for (var i = 0; i < roads.links.Length; i++)
                {
                    var metres = PolylineMetres(roads.links[i]?.p, x, y);
                    if (metres <= 80f) dongFocusRoads.Add((i, metres));
                }
                dongFocusRoads.Sort((a, b) => a.Metres.CompareTo(b.Metres));
                if (dongFocusRoads.Count > 2) dongFocusRoads.RemoveRange(2, dongFocusRoads.Count - 2);
            }
            if (group is 1 or 6 or 7)
            {
                var lon = 126.94 + x / 88224.0;
                var lat = 37.523 + y / 111320.0;
                var best = 600.0;
                foreach (var station in EnsureSubwayGu()?.stations ?? Array.Empty<SubwayStation>())
                {
                    if (station?.name == null) continue;
                    var dx = (station.longitude - lon) * 88224.0;
                    var dy = (station.latitude - lat) * 111320.0;
                    var metres = Math.Sqrt(dx * dx + dy * dy);
                    if (metres >= best) continue;
                    best = metres;
                    dongFocusStation = station.name;
                }
                dongFocusStationMetres = (float)best;
            }
            Debug.Log($"Hongdal dong focus: {FacilityGroupNames[group]} · 이어진 시설 {dongFocusRelated.Count} · 버스 {dongFocusBuses.Count} · 길 {dongFocusRoads.Count} · 역 {dongFocusStation ?? "-"}");
        }

        private void NoteDongFocusScreen(Vector2 foot, Vector2 up, string label, int group)
        {
            dongFocusOnScreen = true;
            dongFocusFoot = foot;
            dongFocusUp = up;
            dongFocusLabel = label;
            dongFocusGroup = group;
        }

        private void DrawDongFocusRelated(Vector2 foot, int group, float fade)
        {
            var colour = FacilityGroupColour(group);
            Hoop(foot, 8f, Fade(colour, 0.9f * fade), 1.6f);
            Hoop(foot, 12f, Fade(colour, 0.3f * fade), 1.2f);
        }

        private void DrawDongFocusLine(DongPolyline line, Color colour, Rect view)
        {
            var hasPrevious = WorldScreen(line.world[0], out var previous) && WorldShown(line.world[0]);
            for (var i = 1; i < line.world.Length; i++)
            {
                var ok = WorldScreen(line.world[i], out var point) && WorldShown(line.world[i]);
                if (ok && hasPrevious && (view.Contains(previous) || view.Contains(point)))
                {
                    Capsule(previous, point, Fade(colour, 0.28f), 9f);
                    Capsule(previous, point, colour, 3.2f);
                }
                previous = point;
                hasPrevious = ok;
            }
        }

        // 집중 카드 자리: 화면 판에 덮이지 않고 집중한 것을 가리지 않는 곳 중 가장 가까운 곳(16:52 점검 — 위 업무 띠 밑에 숨었던 것을 고침)
        private Rect dongFocusCard;
        private int dongFocusCardFor = -2;
        private Vector2 dongFocusCardFoot, dongFocusCardMark;
        private float dongFocusCardAt = -10f;

        private Rect storeMarkRect;                 // 고른 매장 고리 · 이름표(SagajeongDispatchStoreDesk.cs StoreDeskMark가 적는다)
        private int storeMarkFrame = -10;

        private bool StoreMarkFresh => Time.frameCount - storeMarkFrame <= 2;

        // 위 띠(인계 사슬 · 신호 줄)는 책상을 편 동안에도 그려지므로 늘 피한다(MapOverlayCovers는 그때 띠를 세지 않는다)
        private bool TopStripsCover(Rect r) =>
            !mapOnly && mapLevel != MapLevel.Nation && (r.Overlaps(ChainStrip) || BarSignals.Count > 0 && r.Overlaps(SignalBar) || r.Overlaps(new Rect(TopStripLeft, 88f, 980f - TopStripLeft, 128f)));

        // 카드 자리 셈의 한 칸(지도 안 좌표): 화면 가장자리 · 입장 띠 · 고른 매장 · 위 띠 · 판
        private bool DongCellCovered(Rect box)
        {
            var screen = new Rect(box.position + Field.position, box.size);
            if (screen.y < 88f || screen.yMax > Height - 70f || screen.x < 6f || screen.xMax > Width - 6f) return true;
            if (StanceEnterBandShown && screen.Overlaps(StanceEnterBand)) return true;
            if (StoreMarkFresh && box.Overlaps(storeMarkRect)) return true;
            if (TopStripsCover(screen)) return true;
            return MapOverlayCovers(screen.center) || MapOverlayCovers(screen.position + new Vector2(2f, 2f)) || MapOverlayCovers(new Vector2(screen.xMax - 2f, screen.yMax - 2f));
        }

        // 촬영: 시설 발 · 빛 기둥 꼭대기 · 둘레가 판에 가리지 않는가
        private bool DongSpotClear(Vector2 foot, Vector2 up)
        {
            if (foot.y < 100f || foot.y > Field.height - 90f || foot.x < 40f || foot.x > Field.width - 40f) return false;
            var near = new Rect(foot.x - 30f, foot.y - 30f + Mathf.Min(0f, up.y * 9f), 60f, 60f + Mathf.Abs(up.y * 9f));
            var nearScreen = new Rect(near.position + Field.position, near.size);
            if (StanceEnterBandShown && nearScreen.Overlaps(StanceEnterBand)) return false;
            if (StoreMarkFresh && near.Overlaps(storeMarkRect)) return false;
            if (TopStripsCover(nearScreen)) return false;
            foreach (var point in new[] { foot, foot + up * 9f, foot + new Vector2(-24f, 0f), foot + new Vector2(24f, 0f), foot + new Vector2(0f, 24f), foot + new Vector2(0f, -24f) })
                if (MapOverlayCovers(point + Field.position)) return false;
            return true;
        }

        private Rect PlaceDongFocusCard(float width, float height)
        {
            var now = Time.unscaledTime;
            var foot = dongFocusOnScreen ? dongFocusFoot : new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            if (dongFocusCardFor == dongFocusIndex && Mathf.Abs(dongFocusCard.height - height) < 1f &&
                (foot - dongFocusCardFoot).sqrMagnitude < 576f && (storeMarkRect.position - dongFocusCardMark).sqrMagnitude < 576f && now - dongFocusCardAt < 0.5f) return dongFocusCard;
            // 지도를 16점 칸으로 나눠 덮인 칸을 한 번 세고(누적 합), 10점 간격 자리 중 덮인 칸이 없고 가장 가까운 곳. 없으면 덜 덮인 곳.
            const float Cell = 16f;
            var cols = Mathf.CeilToInt(Field.width / Cell);
            var rows = Mathf.CeilToInt(Field.height / Cell);
            var sum = new int[rows + 1, cols + 1];
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    sum[r + 1, c + 1] = sum[r, c + 1] + sum[r + 1, c] - sum[r, c] + (DongCellCovered(new Rect(c * Cell, r * Cell, Cell, Cell)) ? 1 : 0);
            var best = new Rect(Field.width - width - 14f, 96f, width, height);
            var bestCount = int.MaxValue;
            var bestDistance = float.MaxValue;
            for (var y = 10f; y + height <= Field.height - 10f; y += 10f)
                for (var x = 10f; x + width <= Field.width - 10f; x += 10f)
                {
                    var rect = new Rect(x, y, width, height);
                    if (dongFocusOnScreen && new Rect(rect.x - 28f, rect.y - 28f, rect.width + 56f, rect.height + 56f).Contains(foot)) continue;   // 집중한 것은 가리지 않는다
                    var c0 = Mathf.Clamp(Mathf.FloorToInt(rect.x / Cell), 0, cols - 1);
                    var c1 = Mathf.Clamp(Mathf.FloorToInt((rect.xMax - 0.01f) / Cell), 0, cols - 1);
                    var r0 = Mathf.Clamp(Mathf.FloorToInt(rect.y / Cell), 0, rows - 1);
                    var r1 = Mathf.Clamp(Mathf.FloorToInt((rect.yMax - 0.01f) / Cell), 0, rows - 1);
                    var count = sum[r1 + 1, c1 + 1] - sum[r0, c1 + 1] - sum[r1 + 1, c0] + sum[r0, c0];
                    var distance = Vector2.Distance(rect.center, foot);
                    if (count > bestCount || count == bestCount && distance >= bestDistance) continue;
                    best = rect;
                    bestCount = count;
                    bestDistance = distance;
                }
            dongFocusCard = best;
            dongFocusCardFor = dongFocusIndex;
            dongFocusCardFoot = foot;
            dongFocusCardMark = storeMarkRect.position;
            dongFocusCardAt = now;
            return best;
        }

        // DrawDongDataLayers 끝(Repaint): 이어진 버스 · 길 덧그리기, 빛 기둥, 집중 카드
        private void DrawDongFocusOverlay(Rect view, float now)
        {
            if (dongFocusIndex < 0) return;
            var bus = busLayerOn ? EnsureBus() : busPack;
            if (dongFocusBuses.Count > 0 && dongBusLines != null)
                foreach (var line in dongBusLines)
                    if (dongFocusBuses.Contains(line.index)) DrawDongFocusLine(line, Ink(0x5FA2FF), view);
            var hour = GameSeconds() / 3600;
            if (dongFocusRoads.Count > 0 && dongRoadLines != null)
                foreach (var line in dongRoadLines)
                    if (dongFocusRoads.Any(entry => entry.Link == line.index))
                    {
                        var jam = RoadJam(line.index, hour, out _);
                        DrawDongFocusLine(line, JamColour(Mathf.Max(0.05f, jam)), view);
                    }
            var colour = FacilityGroupColour(dongFocusGroup);
            var card = new Rect(Field.width - 352f, 70f, 330f, 0f);
            if (dongFocusOnScreen)
            {
                var pulse = mapEffects ? 0.5f + 0.5f * Mathf.Sin(now * 3f) : 0.6f;
                var top = dongFocusFoot + dongFocusUp * 9f;
                Capsule(dongFocusFoot, top, Fade(colour, 0.3f), 7f);
                Capsule(dongFocusFoot, top, Fade(colour, 0.95f), 2f);
                Hoop(dongFocusFoot, 12f + 4f * pulse, Fade(colour, 0.95f), 2.2f);
                Hoop(dongFocusFoot, 22f + 6f * pulse, Fade(colour, 0.35f), 1.4f);
            }
            var lines = new List<(string Text, Color Colour, bool Bold)>();
            lines.Add(($"같은 갈래 둘레 {DongFocusRadius(dongFocusGroup):0} m 안 {dongFocusSame}곳" +
                       (dongFocusSame > 0 ? $" · 가장 가까운 {dongFocusNearest:0} m" : string.Empty), Gold, false));
            if (DongFocusBusGroup(dongFocusGroup))
            {
                if (bus?.routes == null) lines.Add(("버스 노선 자료를 읽지 않음(부가에서 뺌)", Fade(Soft, 0.7f), false));
                else
                {
                    var names = dongFocusBuses.Take(6).Select(r => bus.routes[r].n).ToList();
                    lines.Add((dongFocusBuses.Count == 0 ? "가까이 지나는 버스 노선 없음" :
                        $"지나는 버스 {dongFocusBuses.Count}노선 · {string.Join(" · ", names)}{(dongFocusBuses.Count > 6 ? " …" : string.Empty)}", Ink(0x8FC0FF), dongFocusBuses.Count > 0));
                }
            }
            if (DongFocusRoadGroup(dongFocusGroup))
            {
                var roads = roadLayerOn ? EnsureRoads() : roadPack;
                if (roads?.links == null) lines.Add(("길 속도 자료를 읽지 않음", Fade(Soft, 0.7f), false));
                else if (dongFocusRoads.Count == 0) lines.Add(("80 m 안에 속도를 아는 길 없음", Soft, false));
                else
                    foreach (var (link, metres) in dongFocusRoads)
                    {
                        var jam = RoadJam(link, hour, out var kmh);
                        lines.Add(($"가까운 길 {roads.links[link].n} {(kmh < 0f ? "-" : $"{kmh:0} km/h")} · {hour:00}시" +
                                   (jam >= 0.5f ? " · 막힘" : jam >= 0.2f ? " · 더딤" : string.Empty) + $" · {metres:0} m", JamColour(Mathf.Max(0.05f, jam)), true));
                    }
            }
            if (dongFocusStation != null)
            {
                var hours = StationHours(dongFocusStation);
                lines.Add((hours.HasValue
                    ? $"{dongFocusStation} {dongFocusStationMetres:0} m · {hour:00}시 승차 {hours.Value.Board[hour]:N0} · 하차 {hours.Value.Alight[hour]:N0}({hours.Value.Note})"
                    : $"{dongFocusStation} {dongFocusStationMetres:0} m · 역의 하루 자료를 읽지 않음", Ink(0xF2C35B), hours.HasValue));
            }
            card = PlaceDongFocusCard(card.width, 76f + lines.Count * 18f);   // 판(업무 띠 · 매장 판 · 요약 지도 …)에 가리지 않는 자리, 집중한 것 가까이
            if (dongFocusOnScreen)
            {
                var anchor = new Vector2(card.center.x < dongFocusFoot.x ? card.xMax : card.x, card.y + 24f);
                Capsule(dongFocusFoot + dongFocusUp * 6f, anchor, Fade(colour, 0.7f), 1.4f);
            }
            dongFocusCardLines.Clear();
            dongFocusCardLines.AddRange(lines);
            dongFocusCardColour = colour;
            dongFocusCardFrame = Time.frameCount;
        }

        // 집중 카드는 지도 위 표시(고른 매장 고리 · 이름표 · 빛)를 다 그린 뒤에 그린다(View.cs: DrawMap 다음). 판들은 그 뒤에 그려지고, 카드는 판을 피해 자리를 잡는다.
        private readonly List<(string Text, Color Colour, bool Bold)> dongFocusCardLines = new List<(string, Color, bool)>();
        private Color dongFocusCardColour;
        private int dongFocusCardFrame = -1;

        private void DrawDongFocusCardLate()
        {
            if (Event.current.type != EventType.Repaint || dongFocusIndex < 0 || dongFocusCardFrame != Time.frameCount) return;
            var card = new Rect(dongFocusCard.position + Field.position, dongFocusCard.size);     // 지도 안 좌표 → 화면 좌표(이 카드는 Field 그룹 밖에서 그린다)
            var colour = dongFocusCardColour;
            var lines = dongFocusCardLines;
            Paint(new Rect(card.x - 1.5f, card.y - 1.5f, card.width + 3f, card.height + 3f), Fade(colour, 0.85f), 10);
            Paint(card, Fade(Canvas, 0.96f), 9);
            var icon = FacilityGroupIconKey(dongFocusGroup);
            var lead = icon != null && DrawMapIcon(new Rect(card.x + 12f, card.y + 10f, 20f, 20f), icon, colour) ? 26f : 0f;
            Text(new Rect(card.x + 12f + lead, card.y + 8f, card.width - 130f - lead, 22f), dongFocusLabel ?? "시설", 14, Paper, true);
            var tier = StanceTier(stanceKey, "fac:" + dongFocusGroup);
            var stanceName = CurrentStance?.Name;
            var badge = new Rect(card.xMax - 112f, card.y + 10f, 100f, 18f);
            Paint(badge, Fade(tier == 1 ? Gold : tier == 2 ? OptionalTint : Tile, tier == 0 ? 0.9f : 0.22f), 5);
            Text(badge, tier == 1 ? $"필수 · {stanceName}" : tier == 2 ? $"부가 · {stanceName}" : stanceName == null ? "입장 없음" : "입장 목록 밖", 9,
                tier == 1 ? Gold : tier == 2 ? OptionalTint : Soft, true, TextAnchor.MiddleCenter);
            Text(new Rect(card.x + 12f, card.y + 32f, card.width - 24f, 15f), $"{FacilityGroupNames[dongFocusGroup]} 갈래 · 근처에 있다고 보고된 곳 · 둘레 이어진 곳은 고리", 9, Soft);
            var y = card.y + 50f;
            foreach (var (text, tint, bold) in lines)
            {
                Paint(new Rect(card.x + 12f, y + 3f, 3f, 12f), tint, 1.5f);
                Text(new Rect(card.x + 20f, y, card.width - 30f, 17f), text, 10, bold ? Paper : Soft, bold);
                y += 18f;
            }
            Text(new Rect(card.x + 12f, card.yMax - 21f, card.width - 24f, 16f), "같은 모양이나 빈 곳을 누르면 풀림", 9, Fade(Soft, 0.8f));
        }
        // ── 촬영: 입장 → 불러오기 → 디오라마 → 집중(VisualAudit의 동 가까이 보기에서 부른다) ─────────────
        private System.Collections.IEnumerator ViewProofStanceFlow(string directory, double latitude, double longitude)
        {
            var keepKey = stanceKey;
            var rider = Array.Find(Stances, item => item.Key == "rider");
            stanceLoadHold = 4;
            BeginStanceLoading(rider, false);
            for (var wait = 0; wait < 240 && stanceLoadIndex < 4; wait++) yield return null;
            yield return SaveFrame(directory, "v-flow-1-loading.png");
            var trace = new System.Text.StringBuilder();
            void Trace(string at) => trace.AppendLine($"{at} · 단계 {mapLevel} · 디오라마 {dioramaOn}/{dioramaCamera != null} · 벌림 {dongSpread:0.00} · 단위 {dataUnit} · 자료 {DataBits()} · 시설 {facilityGroupMask} · 그린 시설 [{string.Join(",", dongPropSeen)}] · 버스 {dongBusSeen} · 길 {dongRoadSeen} · 집중 {dongFocusIndex}/{dongFocusOnScreen} · 떠오름 {DongDataRise():0.00} · 입장 {stanceKey} · 시설 자료 {facilityPack?.k?.Length ?? -1} · 발 {dongFocusFoot} · 카드 {dongFocusCard} · 이어진 {dongFocusRelated.Count} · 버스 {dongFocusBuses.Count} · 길 {dongFocusRoads.Count} · 역 {dongFocusStation} · 매장 표시 {storeMarkRect}/{StoreMarkFresh} · Field {Field}");
            Trace("1");
            stanceLoadHold = -1;
            for (var wait = 0; wait < 600 && stanceLoadingOpen; wait++) yield return null;
            ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            yield return new WaitForSecondsRealtime(0.9f);
            yield return SaveFrame(directory, "v-flow-2-diorama-enter.png");
            Trace("2");
            yield return new WaitForSecondsRealtime(1.8f);
            yield return SaveFrame(directory, "v-flow-3-diorama-data.png");
            Trace("3");
            foreach (var (group, file) in new[] { (1, "v-flow-4-focus-stop.png"), (7, "v-flow-5-focus-exit.png"), (2, "v-flow-6-focus-entrance.png") })
            {
                dongFocusProofGroup = group;
                dongFocusIndex = -1;                 // 그 갈래가 화면에 없으면 집중 없이 찍힌다(앞 장면이 남지 않게)
                for (var frame = 0; frame < 6; frame++) yield return null;
                if (dongFocusIndex >= 0 && facilityPack?.x != null && dongFocusIndex < facilityPack.x.Length)
                {
                    // 사람이 누를 때처럼 판에 가리지 않은 자리에서: 고른 시설을 화면 가운데로 옮기고 찍는다
                    dongFocusProofGroup = -1;
                    ViewProofCentre(37.523 + facilityPack.y[dongFocusIndex] / 111320.0, 126.94 + facilityPack.x[dongFocusIndex] / 88224.0, mapZoom);
                    for (var frame = 0; frame < 8; frame++) yield return null;
                }
                yield return SaveFrame(directory, file);
                Trace(file);
            }
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "v-flow-trace.txt"), trace.ToString()); } catch (Exception error) { Debug.LogWarning(error.Message); }
            dongFocusProofGroup = -1;
            dongFocusIndex = -1;
            stanceEnterNote = null;
            stanceKey = keepKey;
        }
    }
}
