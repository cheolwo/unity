using System;
using System.Globalization;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 계절 · 변화 그림 구조 E0 (2026-11-23 23:49 사용자: 「좀 오래 걸려도 상관없으니까 그런 계절성이나 변화성을 잘 나타낼 수 있도록 하는 지도의 구조」).
    //  - 날짜 · 시각 · 관측에서 「지금의 바깥」을 한 곳(EnvState)에서 셈한다. 동 디오라마 셰이더(E1)와 구 지도 바탕(E2)이 같은 값을 읽는다.
    //  - 근거: Resources/hongdal-data-season.json(bake_season.py) — 서울 기상관측소 벚꽃 개화 · 만발, 북한산 첫 단풍 · 절정, 서울 평년 기온.
    //    관측이 없는 해는 평년 날짜, 날짜 사이는 팩의 rules(규칙 · 시험 값)로 잇는다. 눈 · 비 · 해 뜨고 지는 시각은 실제 달력(DataPacks)에서 온다.
    //  - 날짜 미리 보기: 자료 창에서 날짜 · 시각을 골라 그 철의 지도를 본다. 저장하지 않는다(다시 켜면 오늘). 표현뿐이다: 주문 · 돈 · 시계 규칙은 읽지도 바꾸지도 않는다.
    //  - 「계절 바탕」 체크(모든 단위)를 끄면 셰이더는 전과 똑같이 그린다(_HongdalEnvC.w = 0).
    public sealed partial class SagajeongDispatchView
    {
        private const string SeasonPreference = "hongdal.season.on";

        [Serializable] private sealed class SeasonPack { public string schema; public string title; public string unit; public SeasonSource[] sources; public SeasonYear[] years; public SeasonNormal normal; public float[] temp; public float[] wind; public SeasonRules rules; }
        [Serializable] private sealed class SeasonSource { public string what; public string source; }
        [Serializable] private sealed class SeasonYear { public int y; public string bloom, bloomPeak, fall, fallPeak, fallPeakFrom; }
        [Serializable] private sealed class SeasonNormal { public string bloom, bloomPeak, fall; }
        [Serializable] private sealed class SeasonRules
        {
            public float bloomPeakAfter = 4, bloomHold = 4, bloomFall = 7, leafStart = -3, leafFull = 35, freshUntil = 50;
            public float fallPeakAfter = 11, cityLag = 8, turnBefore = 10, dropAfter = 3, bareAfter = 26, snowFull = 5, iceBelow = 1;
            public float[] windFrom = { 300, 300, 285, 270, 255, 235, 230, 235, 270, 290, 300, 300 };
            public float windDaySpread = 0.45f, windRainBoost = 1.35f;
            public float[] azalea = { -7, -1, 9 }, royal = { 16, 26, 40 }, ipap = { 30, 38, 50 };
            public float petalFall = 9, leafFallDays = 18;
        }

        // 한 해의 계절 날짜와 그 날짜가 어디서 왔는지.
        private struct SeasonDates
        {
            public DateTime Bloom, BloomPeak, Fall, FallPeak;      // Fall · FallPeak = 북한산(관측). 동네 나무는 + cityLag
            public string BloomNote, FallNote;
        }

        // 지금의 바깥. 모두 0~1(Sun만 −1~1).
        private struct EnvState
        {
            public DateTime Date;
            public int Seconds;
            public float Night, Golden, Sun, Wet, Rain, Bloom, Leaf, Fresh, Autumn, Snow, Ice, Temp;
            public float Azalea, Royal, Ipap, PetalFall, LeafFall, Litter;       // 봄꽃 · 날리는 꽃잎 · 날리는 잎 · 쌓인 낙엽 (2026-11-24)
            public float WindSpeed, WindFrom, Gust;                             // 바람: m/s · 불어오는 쪽(도) · 돌풍(0~1)
            public Vector2 WindToward;                                          // 바람이 가는 쪽(동 x · 북 z)
            public Color SunTint;
            public SeasonDates Dates;
        }

        private SeasonPack seasonPack;
        private bool seasonTried;
        private bool seasonOn = true;
        private bool seasonRead;
        private EnvState envNow;
        private string envKey;
        private DateTime? previewDate;        // 날짜 미리 보기(자료 창). null = 게임의 오늘
        private int previewSeconds = -1;
        private int previewDrag = -1;         // 끌고 있는 미리 보기 막대(0 날짜 · 1 시각)

        private static readonly int EnvAId = Shader.PropertyToID("_HongdalEnvA");
        private static readonly int EnvBId = Shader.PropertyToID("_HongdalEnvB");
        private static readonly int EnvCId = Shader.PropertyToID("_HongdalEnvC");
        private static readonly int SunTintId = Shader.PropertyToID("_HongdalSunTint");
        private static readonly int EnvDId = Shader.PropertyToID("_HongdalEnvD");
        private static readonly int EnvEId = Shader.PropertyToID("_HongdalEnvE");
        private static readonly int WindId = Shader.PropertyToID("_HongdalWind");
        private static readonly int WindTimeId = Shader.PropertyToID("_HongdalWindTime");
        private static readonly DateTime PreviewFirst = new(2024, 1, 1);
        private static readonly DateTime PreviewLast = new(2026, 12, 31);

        private SeasonPack EnsureSeason()
        {
            if (seasonTried) return seasonPack;
            seasonTried = true;
            seasonPack = LoadPack<SeasonPack>("hongdal-data-season");
            if (seasonPack != null) seasonPack.rules ??= new SeasonRules();
            return seasonPack;
        }

        private void ReadSeasonPreference()
        {
            if (seasonRead) return;
            seasonRead = true;
            seasonOn = PlayerPrefs.GetInt(SeasonPreference, 1) != 0;
        }

        private void SaveSeasonPreference()
        {
            PlayerPrefs.SetInt(SeasonPreference, seasonOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static DateTime? ParseDay(string text) =>
            DateTime.TryParseExact(text ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : (DateTime?)null;

        private SeasonDates SeasonDatesFor(int year)
        {
            var pack = EnsureSeason();
            var rules = pack?.rules ?? new SeasonRules();
            SeasonYear seen = null;
            if (pack?.years != null) foreach (var y in pack.years) if (y.y == year) { seen = y; break; }
            DateTime Normal(string md, int fallbackMonth, int fallbackDay)
            {
                var parts = (md ?? "").Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var d)) return new DateTime(year, m, d);
                return new DateTime(year, fallbackMonth, fallbackDay);
            }
            var dates = new SeasonDates();
            var bloom = ParseDay(seen?.bloom);
            dates.Bloom = bloom ?? Normal(pack?.normal?.bloom, 4, 8);
            var bloomPeak = ParseDay(seen?.bloomPeak);
            dates.BloomPeak = bloomPeak ?? (bloom.HasValue ? dates.Bloom.AddDays(rules.bloomPeakAfter) : Normal(pack?.normal?.bloomPeak, 4, 10));
            dates.BloomNote = bloom.HasValue ? $"서울 관측소 {dates.Bloom.Month}/{dates.Bloom.Day} 개화" + (bloomPeak.HasValue ? $" · {dates.BloomPeak.Month}/{dates.BloomPeak.Day} 만발" : " · 만발은 규칙")
                                             : $"평년 {dates.Bloom.Month}/{dates.Bloom.Day} 개화(그해 관측 없음)";
            var fall = ParseDay(seen?.fall);
            dates.Fall = fall ?? Normal(pack?.normal?.fall, 10, 15);
            var fallPeak = ParseDay(seen?.fallPeak);
            dates.FallPeak = fallPeak ?? dates.Fall.AddDays(rules.fallPeakAfter);
            dates.FallNote = (fall.HasValue ? $"북한산 {dates.Fall.Month}/{dates.Fall.Day} 첫 단풍" : $"평년 {dates.Fall.Month}/{dates.Fall.Day} 첫 단풍") +
                             (fallPeak.HasValue ? $" · {dates.FallPeak.Month}/{dates.FallPeak.Day} 절정" : " · 절정은 규칙") + $" · 동네는 +{rules.cityLag:0}일(규칙)";
            return dates;
        }

        private static float Ramp(double t, double from, double to) => to <= from ? (t >= to ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)((t - from) / (to - from))));

        // 평년 월평균 기온을 달 가운데(15일)끼리 잇는다.
        private float NormalTemperature(DateTime date) => MonthlyAt(EnsureSeason()?.temp, date, 12f);

        private static float MonthlyAt(float[] values, DateTime date, float fallback, bool angle = false)
        {
            if (values == null || values.Length != 12) return fallback;
            var month = date.Month - 1;
            var t = (date.Day - 15) / 30f;
            var other = t >= 0 ? (month + 1) % 12 : (month + 11) % 12;
            return angle ? Mathf.LerpAngle(values[month], values[other], Mathf.Abs(t)) : Mathf.Lerp(values[month], values[other], Mathf.Abs(t));
        }

        // 봄꽃 한 철: 피기 시작 → 가득 → 다 짐(개화일 기준 날 수).
        private static float Bell(double t, float[] days)
        {
            if (days == null || days.Length < 3) return 0f;
            return Ramp(t, days[0], days[1]) * (1f - Ramp(t, days[1] + (days[2] - days[1]) * 0.4f, days[2]));
        }

        private static readonly string[] WindNames = { "북", "북북동", "북동", "동북동", "동", "동남동", "남동", "남남동", "남", "남남서", "남서", "서남서", "서", "서북서", "북서", "북북서" };
        private static string WindName(float from) => WindNames[Mathf.RoundToInt(Mathf.Repeat(from, 360f) / 22.5f) % 16] + "풍";

        private EnvState EnvNow()
        {
            var date = GameDate();
            var seconds = GameSeconds();
            ReadSeasonPreference();
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" + seconds / 60 + "|" + (skyOn ? 1 : 0) + (weatherOn ? 1 : 0) + (seasonOn ? 1 : 0);
            if (key == envKey) return envNow;
            envKey = key;
            var rules = EnsureSeason()?.rules ?? new SeasonRules();
            var env = new EnvState { Date = date, Seconds = seconds };
            var dates = SeasonDatesFor(date.Year);
            env.Dates = dates;
            var now = date.Date.AddSeconds(seconds);
            double Days(DateTime at) => (now - at).TotalDays;

            // 꽃: 개화 이틀 전부터 피어 만발에 가득, 며칠 머물다 진다.
            var tb = Days(dates.Bloom);
            var tp = Days(dates.BloomPeak);
            env.Bloom = tp < 0 ? 0.45f * Ramp(tb, -2, 0) + 0.55f * Ramp(tb, 0, (dates.BloomPeak - dates.Bloom).TotalDays)
                               : 1f - Ramp(tp, rules.bloomHold, rules.bloomHold + rules.bloomFall);
            // 잎: 봄에 나고(연두에서 짙은 녹색으로), 가을에 물들고, 진다. 동네 나무는 북한산보다 cityLag만큼 늦다.
            var cityFall = dates.Fall.AddDays(rules.cityLag);
            var cityPeak = dates.FallPeak.AddDays(rules.cityLag);
            var spring = Ramp(tb, rules.leafStart, rules.leafFull);
            var drop = 1f - Ramp(Days(cityPeak), rules.dropAfter, rules.bareAfter);
            env.Leaf = Mathf.Min(spring, drop);
            env.Fresh = spring > 0f ? 1f - Ramp(tb, 15, rules.freshUntil) : 0f;
            env.Autumn = Ramp(Days(cityFall), -rules.turnBefore, (cityPeak - cityFall).TotalDays);
            env.Temp = NormalTemperature(date);
            // 봄꽃(규칙): 서울 관측소 벚꽃 개화일 기준. 날리는 꽃잎 · 잎과 쌓인 낙엽도 같은 날짜에서.
            env.Azalea = Bell(tb, rules.azalea);
            env.Royal = Bell(tb, rules.royal);
            env.Ipap = Bell(tb, rules.ipap);
            env.PetalFall = Ramp(tp, rules.bloomHold - 2, rules.bloomHold + 1) * (1f - Ramp(tp, rules.bloomHold + rules.petalFall - 3, rules.bloomHold + rules.petalFall));
            var tc = Days(cityPeak);
            env.LeafFall = Ramp(tc, -rules.leafFallDays * 0.4f, 0) * (1f - Ramp(tc, rules.bareAfter - 6, rules.bareAfter + 2));
            env.Litter = Ramp(tc, -4, 10) * (1f - Ramp(tc, rules.bareAfter + 10, rules.bareAfter + 40));
            env.Ice = Mathf.Clamp01((rules.iceBelow - env.Temp) / 2.5f);

            // 눈 · 비: 실제 달력의 관측(없으면 그 앞 해의 같은 날, 「작년 같은 날」로 적힌다).
            var read = TodayRead();
            var snow = read.Snow;
            env.Snow = snow == null ? 0f : Mathf.Clamp01(Mathf.Max(snow.snow, snow.newSnow * 0.8f) / Mathf.Max(0.5f, rules.snowFull));
            env.Rain = weatherOn ? Mathf.Clamp01(RainNow() / 6f + (RainNow() > 0f ? 0.35f : 0f)) : 0f;
            env.Wet = WetNow(read);
            env.Litter *= 1f - env.Snow;
            // 바람: 평년 월평균 풍속 × 날마다 흔들림(표현) × 비 오는 시각. 방향은 달마다 규칙 ± 날마다 조금.
            var dayHash = SeasonHash(date.Year, date.DayOfYear, 61) / 65536f;
            var hourHash = SeasonHash(date.DayOfYear, seconds / 3600, 67) / 65536f;
            env.WindSpeed = MonthlyAt(EnsureSeason()?.wind, date, 2.3f) * (1f + rules.windDaySpread * (2f * dayHash - 1f)) * (0.85f + 0.3f * hourHash) *
                            (RainNow() > 0f ? rules.windRainBoost : 1f);
            env.WindFrom = MonthlyAt(rules.windFrom, date, 270f, true) + 50f * (SeasonHash(date.Year, date.DayOfYear, 63) / 65536f - 0.5f);
            var fromRad = env.WindFrom * Mathf.Deg2Rad;
            env.WindToward = new Vector2(-Mathf.Sin(fromRad), -Mathf.Cos(fromRad));
            env.Gust = Mathf.Clamp01(0.25f + 0.12f * env.WindSpeed);

            // 해: 그날 해 뜨는 · 지는 시각. 높이 0 = 지평선. 낮과 밤을 껐으면 늘 한낮처럼 그린다.
            var rise = read.Lamp?.rise > 0 ? read.Lamp.rise : 7 * 3600;
            var set = read.Lamp?.set > 0 ? read.Lamp.set : 18 * 3600;
            if (seconds >= rise && seconds <= set) env.Sun = Mathf.Sin(Mathf.PI * (seconds - rise) / Mathf.Max(1f, set - rise));
            else env.Sun = -Mathf.Clamp01(Mathf.Min(Mathf.Abs(seconds - rise), Mathf.Abs(seconds - set), Mathf.Abs(seconds + 86400 - rise)) / 5400f);
            env.Night = skyOn ? NightFactor() : 0f;
            env.Golden = skyOn ? Mathf.Clamp01(1f - Mathf.Abs(env.Sun) / 0.32f) : 0f;
            var tint = Color.white;
            if (skyOn)
            {
                var warm = new Color(1.10f, 0.90f, 0.74f);
                var cool = new Color(0.86f, 0.92f, 1.08f);
                tint = env.Sun >= 0f ? Color.Lerp(Color.white, warm, env.Golden) : Color.Lerp(warm, cool, Mathf.Clamp01(-env.Sun * 1.6f));
                if (env.Sun >= 0f) tint = Color.Lerp(tint, new Color(1.03f, 1.02f, 0.98f), Mathf.Clamp01(env.Sun - 0.5f));     // 한낮: 아주 조금 밝게
            }
            if (env.Snow > 0f) tint = Color.Lerp(tint, new Color(tint.r * 0.97f, tint.g * 1.0f, tint.b * 1.05f), env.Snow);   // 눈 온 날의 푸른 기운
            if (env.Wet > 0f && env.Sun > 0f) tint = Color.Lerp(tint, new Color(0.90f, 0.94f, 1.0f), 0.35f * env.Wet);        // 흐리고 젖은 날
            env.SunTint = tint;
            envNow = env;
            return env;
        }

        // 젖은 땅: 비가 온 시각에서 세 시간쯤에 걸쳐 마른다(표현). 그날 젖은 시간 비트와 전날 밤 비트를 본다.
        private float WetNow(DayRead read)
        {
            var rain = read.Rain;
            if (rain == null || rain.rain <= 0f) return 0f;
            var hour = GameSeconds() / 3600f;
            if (rain.wet == 0) return Mathf.Clamp01(rain.rain / 4f);
            var wet = 0f;
            for (var h = 0; h < 24; h++)
            {
                if ((rain.wet & (1 << h)) == 0 || h > hour) continue;
                var since = Mathf.Max(0f, hour - (h + 1));
                wet = Mathf.Max(wet, Mathf.Exp(-since / 3f));
            }
            return Mathf.Clamp01(wet * Mathf.Clamp01(0.4f + rain.rain / 10f));
        }

        // 동 디오라마 셰이더에 지금의 바깥을 알린다. LateUpdate(SagajeongDispatchDiorama.cs)가 매 화면 부른다.
        private void SyncEnvironment()
        {
            var env = EnvNow();
            var on = seasonOn ? 1f : 0f;
            Shader.SetGlobalVector(EnvAId, new Vector4(env.Night, env.Golden, env.Sun, env.Wet * on));
            Shader.SetGlobalVector(EnvBId, new Vector4(env.Bloom * on, env.Leaf, env.Autumn, env.Snow * on));
            Shader.SetGlobalVector(EnvCId, new Vector4(env.Fresh, env.Rain, env.Ice, on));
            Shader.SetGlobalVector(SunTintId, seasonOn ? (Vector4)env.SunTint : Vector4.one);
            Shader.SetGlobalVector(EnvDId, new Vector4(env.Litter, env.PetalFall, env.LeafFall, env.Ipap) * on);
            Shader.SetGlobalVector(EnvEId, new Vector4(env.Azalea, env.Royal, 0f, 0f) * on);
            Shader.SetGlobalVector(WindId, new Vector4(env.WindToward.x, env.WindToward.y, env.WindSpeed, env.Gust));
            Shader.SetGlobalFloat(WindTimeId, Time.unscaledTime);
            SyncDriftLeaves(env);
            SyncTreeMaterial();
        }

        // 철 한 줄: 오늘 칩 · 자료 창 · 동 범례가 쓴다.
        private string SeasonLine(EnvState env)
        {
            string tree;
            if (env.PetalFall > 0.3f && env.Bloom < 0.9f) tree = "벚꽃잎 날림";
            else if (env.Bloom > 0.05f) tree = env.Bloom > 0.9f ? "벚꽃 만발" : env.Date < env.Dates.BloomPeak ? "벚꽃 피는 중" : "벚꽃 지는 중";
            else if (env.Ipap > 0.3f) tree = "이팝 흰 꽃" + (env.Royal > 0.3f ? " · 철쭉" : "");
            else if (env.Royal > 0.3f) tree = "철쭉 · 새잎";
            else if (env.Azalea > 0.3f) tree = "진달래 · 새잎";
            else if (env.LeafFall > 0.3f && env.Leaf < 0.9f) tree = "낙엽 날림";
            else if (env.Leaf < 0.08f) tree = "잎 진 나무";
            else if (env.Autumn > 0.08f) tree = env.Leaf < 0.85f ? "단풍 지는 중" : env.Autumn > 0.9f ? "단풍 절정" : "단풍 드는 중";
            else if (env.Leaf < 0.9f) tree = "새잎 나는 중";
            else tree = env.Fresh > 0.35f ? "연두 잎" : "짙은 잎";
            if (env.Snow > 0.02f) tree = "눈 덮임 · " + tree;
            else if (env.Wet > 0.1f) tree = "젖은 땅 · " + tree;
            return tree;
        }

        private string SeasonSourceLine(EnvState env) =>
            env.Bloom > 0.05f || env.Date.Month <= 5 ? env.Dates.BloomNote : env.Dates.FallNote;

        private static Color SeasonChipColour(EnvState env)
        {
            if (env.Snow > 0.02f) return new Color(0.90f, 0.93f, 0.96f);
            if (env.Bloom > 0.05f || env.PetalFall > 0.3f) return new Color(0.97f, 0.76f, 0.85f);
            if (env.Ipap > 0.3f) return new Color(0.95f, 0.96f, 0.92f);
            if (env.Royal > 0.3f || env.Azalea > 0.3f) return new Color(0.88f, 0.42f, 0.72f);
            if (env.Leaf < 0.08f) return new Color(0.62f, 0.55f, 0.47f);
            if (env.Autumn > 0.08f) return new Color(0.92f, 0.55f, 0.20f);
            return env.Fresh > 0.35f ? new Color(0.62f, 0.82f, 0.36f) : new Color(0.36f, 0.66f, 0.40f);
        }

        private static string WindLine(EnvState env) => $"{WindName(env.WindFrom)} {env.WindSpeed:0.0} m/s";

        // 오늘 칩 · 범례용 짧은 근거.
        private string SeasonShortSource(EnvState env)
        {
            var d = env.Dates;
            if (env.Bloom > 0.05f || env.Date.Month <= 5) return d.BloomNote.StartsWith("평년") ? $"평년 {d.Bloom.Month}/{d.Bloom.Day} 개화" : $"관측소 {d.Bloom.Month}/{d.Bloom.Day} 개화";
            if (env.Date.Month >= 9) return (d.FallNote.StartsWith("평년") ? "평년" : "북한산") + $" {d.FallPeak.Month}/{d.FallPeak.Day} 절정 +{EnsureSeason()?.rules?.cityLag ?? 8:0}일";
            return "서울 평년 기온";
        }

        // ── 날짜 미리 보기 (자료 창 왼쪽 칸 아래) ─────────────────────────────────

        private static readonly (string Name, int Month, int Day, int Hour)[] PreviewSeasons =
        {
            ("봄", 4, 5, 14), ("여름", 7, 18, 10), ("가을", 11, 3, 15), ("겨울", 12, 4, 10),
        };

        // 왼쪽 칸 아래에 놓는다. 바뀌었으면 true.
        private bool DrawSeasonPreview(float x, ref float row, bool dong)
        {
            var changed = false;
            ReadSeasonPreference();
            DataHeader(x, ref row, "계절 · 날짜 미리 보기");
            var was = seasonOn;
            DataCheck(x, ref row, "계절 바탕", ref seasonOn, "꽃 · 잎 · 눈 · 젖음", Ink(0x9BD67A));
            if (was != seasonOn) { SaveSeasonPreference(); envKey = null; changed = true; }

            var shown = GameDate();
            var year = previewDate?.Year ?? shown.Year;
            var chipX = x;
            foreach (var season in PreviewSeasons)
            {
                var pickYear = season.Month == 12 ? Math.Min(year, 2025) : year > 2026 ? 2026 : year;
                var day = new DateTime(pickYear, season.Month, season.Day);
                var active = previewDate.HasValue && previewDate.Value.Month == season.Month && previewDate.Value.Day == season.Day;
                if (ExpansionButton(new Rect(chipX, row, 36f, 18f), season.Name, true, active ? Gold : Tile))
                {
                    previewDate = day;
                    previewSeconds = season.Hour * 3600;
                    changed = true;
                }
                chipX += 39f;
            }
            if (ExpansionButton(new Rect(chipX, row, 200f - (chipX - x), 18f), "오늘로", previewDate.HasValue, previewDate.HasValue ? Mint : Tile))
            {
                previewDate = null;
                previewSeconds = -1;
                changed = true;
            }
            row += 22f;

            var date = GameDate();
            var seconds = GameSeconds();
            if (ExpansionButton(new Rect(x, row, 20f, 18f), "◀", true, Tile)) { previewDate = date.AddDays(-1); previewSeconds = seconds; changed = true; }
            Text(new Rect(x + 24f, row, 130f, 18f), $"{date:yy}년 {DateLabel(date)} {seconds / 3600:00}시", 10, previewDate.HasValue ? Gold : Paper, true);
            if (ExpansionButton(new Rect(x + 156f, row, 20f, 18f), "▶", true, Tile)) { previewDate = date.AddDays(1); previewSeconds = seconds; changed = true; }
            Text(new Rect(x + 178f, row, 24f, 18f), previewDate.HasValue ? "미리" : "오늘", 9, previewDate.HasValue ? Gold : Soft, false, TextAnchor.MiddleRight);
            row += 21f;

            var span = (float)(PreviewLast - PreviewFirst).TotalDays;
            var at = Mathf.Clamp((float)(date - PreviewFirst).TotalDays, 0f, span);
            if (PreviewSlider(0, new Rect(x, row, 124f, 14f), ref at, 0f, span, Ink(0x9BD67A)))
            {
                previewDate = PreviewFirst.AddDays(Mathf.Round(at));
                previewSeconds = seconds;
                changed = true;
            }
            var hour = seconds / 3600f;
            if (PreviewSlider(1, new Rect(x + 132f, row, 68f, 14f), ref hour, 0f, 23f, Gold))
            {
                previewDate ??= date;
                previewSeconds = Mathf.RoundToInt(hour) * 3600;
                changed = true;
            }
            row += 18f;
            if (changed) { envKey = null; dongDataKey = -1; }
            return changed;
        }

        // 막대 하나. 컨트롤 번호 없이 끌기를 기억한다(동 화면은 누르기와 그리기를 서로 다른 때에 부르기 때문).
        private bool PreviewSlider(int id, Rect rect, ref float value, float min, float max, Color tint)
        {
            var current = Event.current;
            var t = Mathf.InverseLerp(min, max, value);
            if (current.type == EventType.Repaint)
            {
                Paint(new Rect(rect.x, rect.center.y - 2f, rect.width, 4f), Fade(Rim, 0.9f), 2);
                Paint(new Rect(rect.x, rect.center.y - 2f, rect.width * t, 4f), Fade(tint, 0.8f), 2);
                Paint(new Rect(rect.x + rect.width * t - 5f, rect.center.y - 6f, 10f, 12f), previewDrag == id ? Paper : tint, 4);
            }
            var hit = new Rect(rect.x - 6f, rect.y - 3f, rect.width + 12f, rect.height + 6f);
            if (current.type == EventType.MouseDown && current.button == 0 && hit.Contains(current.mousePosition)) previewDrag = id;
            else if (current.type == EventType.MouseUp && previewDrag == id) { previewDrag = -1; current.Use(); return false; }
            if (previewDrag != id || current.type != EventType.MouseDown && current.type != EventType.MouseDrag) return false;
            value = Mathf.Lerp(min, max, Mathf.Clamp01((current.mousePosition.x - rect.x) / Mathf.Max(1f, rect.width)));
            current.Use();
            return true;
        }
    }
}
