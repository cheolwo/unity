using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 입장 × 정보 · 필수 · 부가 (2026-11-24 15:02 사용자: 「필수적인 정보랑 부가적인 정보 이렇게 구분을 하면은 좋고, 필수적인 거는 내가 하루에도
    //  그 사람 입장이 되었을 때 하루에 뭐 한 번 이상 마주하는 … 한 달에 2-3번 이렇게 만난다거나 일주일에 1-2번 정도 이렇게 마주치는 것들은 좀 옵션,
    //  부가적인 정보를 이렇게 분류를 해가지고 그 정보에 대해서 관리를 해가지고 각 입장에 맞게 각 입장과 그 정보를 이렇게 묶어두면 좋겠다」)
    //  - 원본은 하나: Resources/hongdal-data-stance-info.json ← artifacts/local/public-data/_catalog/build_stance_info.py(읽는 표: 입장별-필수부가.md).
    //    입장마다 정보 한 줄 = 필수(1) · 부가(2) · 얼마나 자주 · 왜 · 켜는 단위(구 · 타일 · 동).
    //  - 입장을 고르면 필수만 켠다(SagajeongDispatchStance.cs ApplyStance). 부가는 자료 창 「부가도 켜기」로 그 단위에 함께 켠다.
    //  - 입장 카드: 「필수 · 하루 한 번 이상」 · 「부가 · 주 1~2번 · 달 2~3번」 두 칸.
    //  - 자료 창: 줄 왼쪽 막대(금빛 = 지금 입장의 필수 · 푸른 = 부가).
    //  - 전국 장터: 자료 줄을 필수 칸 → 부가 칸으로 나눠 놓는다(SagajeongDispatchProduceFlow.cs) · 집중 카드도 필수 먼저.
    //  - 표가 없으면 예전처럼 코드에 적힌 묶음을 쓴다.
    // 2차(2026-11-24 16:28 사용자: 「부가 정보 같은 경우는 이제 사용자가 선택을 해가지고 데이터를 시뮬레이션에 로드를 할지 말지를 좀 그렇게 결정을 할 수
    //  있도록 … 기본값은 … 전체 정보를 … 그 입장에 맞춰서 다 불러오는 거를 이제 디폴트로」):
    //  - 입장을 고르면 필수 + 부가 모두를 불러온다(기본). 카드의 부가 칩을 눌러 빼거나 다시 넣는다(입장마다 기억 · PlayerPrefs hongdal.stance.skip.<입장>).
    //  - 뺀 부가는 켜지 않으므로 그 자료(버스 · 길 · 유동인구 등)는 켤 때까지 읽지도 않는다(자료마다 처음 켤 때 읽음).
    //  - 나중에: ESC → 화면 · 지도 「입장 자료」 줄(입장 카드 다시 열기 · 부가까지 모두 · 필수만) · 자료 창 「부가 끄기 / 부가도 켜기」.
    public sealed partial class SagajeongDispatchView
    {
        private const string StanceInfoResource = "hongdal-data-stance-info";
        private static readonly Color OptionalTint = new Color32(0x6F, 0xA8, 0xFF, 0xFF);

        [Serializable] private sealed class StanceInfoPack { public string schema, topic, title, note; public StanceInfoItem[] items; public StanceInfoSet[] stances; }
        [Serializable] private sealed class StanceInfoItem { public string id, name, brief, kind, where; public int value; }
        [Serializable] private sealed class StanceInfoSet { public string key, name; public StanceInfoEntry[] entries; public StancePackage package; }
        // 입장 패키지(2026-11-25 13:45 「역할을 … 선택을 하면은 관련한 데이터들을 이제 불러오는 식으로 … 패키지화」): 자료(entries) + 지도 조각 · 할 일 · 화면
        [Serializable] private sealed class StancePackage { public string home, homeName, homeGu, todo, first; public string[] layers, steps; }
        [Serializable] private sealed class StanceInfoEntry { public string id, freq, why; public int tier; public int[] units; }

        private static StanceInfoPack stanceInfo;
        private static bool stanceInfoTried;

        private static StanceInfoPack StanceInfo
        {
            get
            {
                if (stanceInfoTried) return stanceInfo;
                stanceInfoTried = true;
                var pack = LoadPack<StanceInfoPack>(StanceInfoResource);
                if (pack?.items == null || pack.stances == null)
                {
                    Debug.LogWarning("Hongdal stance info: 입장 × 정보 표가 없어 코드에 적힌 묶음을 씁니다.");
                    return null;
                }
                stanceInfo = pack;
                Debug.Log($"Hongdal stance info: 입장 {pack.stances.Length} · 정보 {pack.items.Length} · 줄 {pack.stances.Sum(set => set.entries?.Length ?? 0)}");
                return stanceInfo;
            }
        }

        private static StanceInfoItem StanceInfoItemOf(string id) => StanceInfo?.items?.FirstOrDefault(item => item.id == id);
        private static StanceInfoSet StanceInfoSetOf(string key) =>
            string.IsNullOrEmpty(key) ? null : StanceInfo?.stances?.FirstOrDefault(set => set.key == key);

        // 지금 입장에서 그 정보: 1 필수 · 2 부가 · 0 목록에 없음
        private static int StanceTier(string key, string id)
        {
            var entry = StanceInfoSetOf(key)?.entries?.FirstOrDefault(item => item.id == id);
            return entry?.tier ?? 0;
        }

        private static bool StanceBase(StanceInfoEntry entry) => entry.freq != null && entry.freq.Contains("바탕");

        // 입장 묶음에 필수 · 부가를 입힌다(Stances를 처음 만들 때 한 번)
        private static void ApplyStanceInfo(Stance stance)
        {
            var set = StanceInfoSetOf(stance.Key);
            if (set?.entries == null) return;
            var bits = new int[3];
            var optional = new int[3];
            var facilities = new int[3];
            var optionalFacilities = new int[3];
            int global = 0, optionalGlobal = 0;
            foreach (var entry in set.entries)
            {
                var item = StanceInfoItemOf(entry.id);
                if (item == null) continue;
                var need = entry.tier == 1;
                switch (item.kind)
                {
                    case "bit":
                    case "facility":
                        foreach (var unit in entry.units ?? Array.Empty<int>())
                        {
                            if (unit < 0 || unit > 2) continue;
                            var mask = item.kind == "bit" ? item.value : 1 << item.value;
                            if (item.kind == "bit")
                            {
                                if (need) bits[unit] |= mask;
                                else optional[unit] |= mask;
                            }
                            else if (need) facilities[unit] |= mask;
                            else optionalFacilities[unit] |= mask;
                        }
                        break;
                    case "global":
                        if (need) global |= item.value;
                        else optionalGlobal |= item.value;
                        break;
                }
            }
            stance.Bits = bits;
            stance.Facilities = facilities;
            stance.Global = global;
            stance.OptBits = optional;
            stance.OptFacilities = optionalFacilities;
            stance.OptGlobal = optionalGlobal;
            stance.Info = set;
        }

        // ── 부가 고르기(16:28) ─────────────────────────────────────────────────
        private const string StanceSkipPreference = "hongdal.stance.skip.";
        private readonly Dictionary<string, HashSet<string>> stanceSkips = new Dictionary<string, HashSet<string>>();

        private HashSet<string> StanceSkips(string key)
        {
            if (string.IsNullOrEmpty(key)) return new HashSet<string>();
            if (stanceSkips.TryGetValue(key, out var set)) return set;
            set = new HashSet<string>(PlayerPrefs.GetString(StanceSkipPreference + key, string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            stanceSkips[key] = set;
            return set;
        }

        private static List<StanceInfoEntry> StanceOptionalEntries(Stance stance) =>
            stance?.Info?.entries == null ? new List<StanceInfoEntry>() : stance.Info.entries.Where(entry => entry.tier == 2).ToList();

        private bool StanceOptionalChosen(string key, string id) => !StanceSkips(key).Contains(id);

        private void SaveStanceSkips(Stance stance)
        {
            PlayerPrefs.SetString(StanceSkipPreference + stance.Key, string.Join(",", StanceSkips(stance.Key)));
            PlayerPrefs.Save();
        }

        private void SetStanceSkip(Stance stance, string id, bool skip, bool save = true)
        {
            var set = StanceSkips(stance.Key);
            if (skip) set.Add(id);
            else set.Remove(id);
            if (save) SaveStanceSkips(stance);
            Debug.Log($"Hongdal stance info: {stance.Name} 부가 {id} {(skip ? "뺌" : "넣음")}");
        }

        private void SetStanceSkipAll(Stance stance, bool skip, bool save = true)
        {
            foreach (var entry in StanceOptionalEntries(stance)) SetStanceSkip(stance, entry.id, skip, false);
            if (save) SaveStanceSkips(stance);
        }

        // 고른(빼지 않은) 부가의 단위별 묶음
        private (int[] Bits, int[] Facilities, int Global) StanceChosenOptional(Stance stance)
        {
            var bits = new int[3];
            var facilities = new int[3];
            var global = 0;
            foreach (var entry in StanceOptionalEntries(stance))
            {
                if (!StanceOptionalChosen(stance.Key, entry.id)) continue;
                var item = StanceInfoItemOf(entry.id);
                if (item == null) continue;
                if (item.kind == "global")
                {
                    global |= item.value;
                    continue;
                }
                if (item.kind != "bit" && item.kind != "facility") continue;
                foreach (var unit in entry.units ?? Array.Empty<int>())
                {
                    if (unit < 0 || unit > 2) continue;
                    if (item.kind == "bit") bits[unit] |= item.value;
                    else facilities[unit] |= 1 << item.value;
                }
            }
            return (bits, facilities, global);
        }

        // 카드에 적을 짧은 이름 목록(바탕은 빼고) · 폭과 줄 수 안에서 끊고 「외 n」
        private (string Text, int Lines) StanceBriefs(Stance stance, int tier, float width, int size, int maxLines)
        {
            var names = stance.Info.entries.Where(entry => entry.tier == tier && !StanceBase(entry))
                .Select(entry => StanceInfoItemOf(entry.id)?.brief).Where(name => !string.IsNullOrEmpty(name)).ToList();
            if (names.Count == 0) return (tier == 1 ? "지도 바탕만" : "없음", 1);
            var text = names[0];
            for (var index = 1; index < names.Count; index++)
            {
                var next = text + " · " + names[index];
                var rest = names.Count - index - 1;
                if (ListLines(next + (rest > 0 ? $" 외 {rest}" : string.Empty), width, size) > maxLines)
                    return (text + $" 외 {names.Count - index}", ListLines(text + $" 외 {names.Count - index}", width, size));
                text = next;
            }
            return (text, ListLines(text, width, size));
        }

        private int ListLines(string text, float width, int size) =>
            Mathf.Max(1, Mathf.CeilToInt(GuNameSize(text, size, false).x * 1.04f / Mathf.Max(40f, width)));

        // 입장 카드 아래쪽: 필수 · 부가 두 칸. 부가는 누르면 빼고 넣는 칩(기본은 모두 넣음). 다 쓰고 난 y를 돌려준다.
        private float DrawStanceTiers(Stance stance, float px, float y, float pw, float maxY)
        {
            Text(new Rect(px, y, pw, 14), "필수 · 하루 한 번 이상", 10, stance.Tint, true);
            var (need, needLines) = StanceBriefs(stance, 1, pw, 11, 4);
            Text(new Rect(px, y + 16, pw, 16 * needLines + 2), need, 11, Paper, false, TextAnchor.UpperLeft, true);
            y += 16 + 16 * needLines + 6;
            var optional = StanceOptionalEntries(stance).Where(entry => !StanceBase(entry)).ToList();
            var chosen = optional.Count(entry => StanceOptionalChosen(stance.Key, entry.id));
            Text(new Rect(px, y, pw - 62, 14), optional.Count == 0 ? "부가 · 없음" : $"부가 · 주 · 달 몇 번 · 불러옴 {chosen}/{optional.Count}", 10, OptionalTint, true);
            if (optional.Count == 0) return y + 18;
            var all = chosen == optional.Count;
            var toggle = new Rect(px + pw - 58, y - 1, 58, 16);
            Paint(toggle, Fade(Tile, 0.95f), 4);
            Text(toggle, all ? "모두 빼기" : "모두 넣기", 9, Paper, true, TextAnchor.MiddleCenter);
            if (Hit(toggle)) SetStanceSkipAll(stance, all);
            y += 19;
            var cx = px;
            foreach (var entry in optional)
            {
                var name = StanceInfoItemOf(entry.id)?.brief;
                if (string.IsNullOrEmpty(name)) continue;
                var width = GuNameSize(name, 10, false).x + 14f;
                if (cx > px && cx + width > px + pw)
                {
                    cx = px;
                    y += 20;
                }
                if (y + 17 > maxY) break;
                var chip = new Rect(cx, y, width, 17);
                var on = StanceOptionalChosen(stance.Key, entry.id);
                Paint(chip, on ? Fade(OptionalTint, 0.9f) : Fade(Rim, 0.9f), 5);
                Paint(new Rect(chip.x + 1, chip.y + 1, chip.width - 2, chip.height - 2), on ? Ink(0x1E3550) : Canvas, 4);
                Text(chip, name, 10, on ? Paper : Fade(Soft, 0.55f), on, TextAnchor.MiddleCenter);
                if (Hit(chip)) SetStanceSkip(stance, entry.id, on);
                cx += width + 4f;
            }
            return y + 21;
        }

        // ESC 설정 · 화면 · 지도 탭의 「입장 자료」 줄(SagajeongDispatchSettings.cs가 부른다)
        private void DrawSettingsStanceRow(float x, float y)
        {
            ScreenLabel(x, y, "입장 자료");
            var stance = CurrentStance;
            if (ExpansionButton(ScreenCell(x, y, 0, 3), stance == null ? "입장 고르기" : "입장 · " + stance.Name, true, Tile))
            {
                settingsOpen = false;
                stancePickerOpen = true;
                return;
            }
            var optional = StanceOptionalEntries(stance);
            if (optional.Count == 0)
            {
                Text(ScreenCell(x, y, 1, 3), stance == null ? "입장을 고르면 바꿀 수 있음" : "이 입장은 부가 없음", 10, Soft, false, TextAnchor.MiddleCenter);
                return;
            }
            var chosen = optional.Count(entry => StanceOptionalChosen(stance.Key, entry.id));
            if (EffectsSegment(ScreenCell(x, y, 1, 3), $"부가까지 모두 {optional.Count}", chosen == optional.Count))
            {
                SetStanceSkipAll(stance, false);
                settingsOpen = false;
                BeginStanceLoading(stance, true);
            }
            if (EffectsSegment(ScreenCell(x, y, 2, 3), chosen > 0 && chosen < optional.Count ? $"필수만(지금 부가 {chosen})" : "필수만", chosen == 0))
            {
                SetStanceSkipAll(stance, true);
                settingsOpen = false;
                BeginStanceLoading(stance, true);
            }
        }

        // ── 자료 창: 줄 왼쪽 막대 · 「부가도 켜기」 ─────────────────────────────────

        private static readonly Dictionary<string, string> DataRowIds = new Dictionary<string, string>
        {
            { "지하철 노선", "subway" }, { "역 불빛", "station-pulse" }, { "버스 노선", "bus" }, { "길 속도", "road" },
            { "낮과 밤", "day-night" }, { "비 · 눈", "weather" }, { "오늘 칩", "today" }, { "유동인구", "floating" },
            { "번화가", "downtown" }, { "내력 문양", "lore" }, { "빛의 번짐", "spread" }, { "타일 지도", "tiles" },
            { "홍달 플레이스 창", "place" }, { "등급 이름", "grade" }, { "위 띠 경 수치", "gyeong" },
        };

        private int DataRowTier(string label)
        {
            if (string.IsNullOrEmpty(stanceKey) || StanceInfo == null) return 0;
            if (DataRowIds.TryGetValue(label, out var id)) return StanceTier(stanceKey, id);
            var group = Array.IndexOf(FacilityGroupNames, label);
            return group >= 0 ? StanceTier(stanceKey, "fac:" + group) : 0;
        }

        private Stance CurrentStance => string.IsNullOrEmpty(stanceKey) ? null : Array.Find(Stances, item => item.Key == stanceKey);

        private bool StanceHasOptional(Stance stance, int unit)
        {
            if (stance?.Info == null) return false;
            var chosen = StanceChosenOptional(stance);
            return chosen.Bits[unit] != 0 || chosen.Facilities[unit] != 0 || chosen.Global != 0;
        }

        private bool StanceOptionalOn(Stance stance, int unit)
        {
            var chosen = StanceChosenOptional(stance);
            return (dataUnitBits[unit] & chosen.Bits[unit]) == chosen.Bits[unit] &&
                   (dataUnitFacilities[unit] & chosen.Facilities[unit]) == chosen.Facilities[unit] &&
                   (dataGlobalBits & chosen.Global) == chosen.Global;
        }

        // 지금 단위에서 그 입장의 부가를 함께 켜거나 끈다(필수는 그대로)
        private void ToggleStanceOptional()
        {
            var stance = CurrentStance;
            var unit = dataUnit >= 0 ? dataUnit : CurrentDataUnit();
            if (!StanceHasOptional(stance, unit)) return;
            CaptureDataUnit();
            var on = StanceOptionalOn(stance, unit);
            var chosen = StanceChosenOptional(stance);      // 카드에서 뺀 부가는 여기서도 켜지 않는다
            if (on)
            {
                dataUnitBits[unit] &= ~chosen.Bits[unit];
                dataUnitFacilities[unit] &= ~chosen.Facilities[unit];
                dataGlobalBits &= ~(chosen.Global & ~stance.Global);
            }
            else
            {
                dataUnitBits[unit] |= chosen.Bits[unit] & ~GlobalBits;
                dataUnitFacilities[unit] |= chosen.Facilities[unit];
                dataGlobalBits |= chosen.Global & GlobalBits;
            }
            LoadDataUnit(unit);
            Debug.Log($"Hongdal stance info: {stance.Name} 부가 {(on ? "끔" : "켬")} · {DataUnitNames[unit]} 단위");
        }

        // 자료 창 머리: 「부가도 켜기」 단추 · 아래 줄 범례. 눌렸으면 true.
        private bool DrawStanceOptionalControls(Rect panel)
        {
            var stance = CurrentStance;
            if (stance?.Info == null) return false;
            var unit = dataUnit >= 0 ? dataUnit : CurrentDataUnit();
            var legend = new Rect(panel.xMax - 292f, panel.yMax - 21f, 278f, 16f);
            Paint(new Rect(legend.x, legend.y + 3f, 3f, 11f), Gold, 1.5f);
            Text(new Rect(legend.x + 6f, legend.y, 70f, 16f), "필수(매일)", 9, Gold, true);
            Paint(new Rect(legend.x + 70f, legend.y + 3f, 3f, 11f), OptionalTint, 1.5f);
            Text(new Rect(legend.x + 76f, legend.y, 200f, 16f), $"부가(주 · 달) · 입장 {stance.Name}", 9, OptionalTint, true);
            if (!StanceHasOptional(stance, unit)) return false;
            var on = StanceOptionalOn(stance, unit);
            if (!ExpansionButton(new Rect(panel.xMax - 342f, panel.y + 8f, 86f, 24f), on ? "부가 끄기" : "부가도 켜기", true, on ? OptionalTint : Tile)) return false;
            ToggleStanceOptional();
            return true;
        }
    }
}
