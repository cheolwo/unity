using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ssalddel.WindowsGame
{
    // 길안내(내비게이션) 규칙 (2026-11-18, Codex r287 「주소에서 주행거리와 예상시간까지」 · r279).
    // 사용자 방향: 「주소를 입력하거나 지도에 핀을 찍으면 도로를 따라갈 경로와 거리 · 예상시간 … 그 내비게이션 경로에 따라
    //            배달 기사님이 움직이는 것을 표현 … 이동 경로에 따라 빛이 있다가 사라지는」.
    // 여기는 순수 규칙이다. 화면도 커널도 모른다. 게임 상태를 읽지도 바꾸지도 않는다.
    //  - 주소 찾기: 건물 색인의 도로명 · 건물번호 · 건물 이름(도로명주소 2026-10)을 글자로 견준다. 지오코딩이 아니다.
    //  - 거리와 시간: 길 그래프(HRTE)의 길을 종류별 빠르기로 나눈 「시뮬레이션 추정」이다. 실제 교통 · 신호 · 공사를 모른다.
    //    일방통행 · 회전 제한은 아직 길 그래프에 없다(양방향). 화면이 그 사실을 함께 적는다.
    //  - 문: 공식 건물 출입구(도로명주소 출입구)와 가상 출입구를 구분해 말한다. 공식 건물 출입구는 점포의 픽업문이라는 뜻이 아니다.
    internal static class 길안내Policy
    {
        public const float WalkSpeed = 1.2f;       // 문에서 길까지 걷는 빠르기 (m/s, 표현값)
        public const int HitLimit = 5;             // 주소 후보를 보여 주는 수
        public const int MinQuery = 2;             // 이보다 짧은 물음은 찾지 않는다

        // 글자를 견주기 좋게 접는다: 공백 · 쉼표 · 점을 빼고 영문은 소문자로.
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var folded = new StringBuilder(text.Length);
            foreach (var letter in text)
            {
                if (char.IsWhiteSpace(letter) || letter == ',' || letter == '.' || letter == '·') continue;
                folded.Append(char.ToLowerInvariant(letter));
            }
            return folded.ToString();
        }

        // 건물 한 채의 주소 열쇠: 접은 도로명 + 건물번호. 도로명이 없으면 빈 글자.
        public static string Key(string road, string number) =>
            string.IsNullOrEmpty(road) ? string.Empty : Fold(road) + Fold(number);

        // 접은 물음과 건물 한 채가 얼마나 맞는가. 0이면 맞지 않는다.
        // 꼭 같은 주소 > 주소의 앞머리(남은 글자가 적을수록 앞) > 건물 이름에 들어 있음.
        public static int Score(string key, string nameKey, string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < MinQuery) return 0;
            if (!string.IsNullOrEmpty(key))
            {
                if (string.Equals(key, query, StringComparison.Ordinal)) return 1000;
                if (key.StartsWith(query, StringComparison.Ordinal)) return 600 - Math.Min(key.Length - query.Length, 200);
            }
            if (!string.IsNullOrEmpty(nameKey) && nameKey.IndexOf(query, StringComparison.Ordinal) >= 0)
                return 200 - Math.Min(nameKey.Length - query.Length, 100);
            return 0;
        }

        // 물음에 맞는 건물 번호들. 점수가 높은 차례, 같으면 주소 글자 차례, 그다음 건물 번호 차례. 같은 주소의 건물은 모두 후보다.
        public static List<int> Search(IReadOnlyList<string> keys, IReadOnlyList<string> nameKeys, string query, int limit)
        {
            var hits = new List<int>();
            var folded = Fold(query);
            if (keys == null || folded.Length < MinQuery || limit <= 0) return hits;
            var scored = new List<(int Score, int Index)>();
            for (var index = 0; index < keys.Count; index++)
            {
                var score = Score(keys[index], nameKeys != null && index < nameKeys.Count ? nameKeys[index] : null, folded);
                if (score > 0) scored.Add((score, index));
            }
            scored.Sort((a, b) =>
            {
                if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
                var byKey = string.CompareOrdinal(keys[a.Index] ?? string.Empty, keys[b.Index] ?? string.Empty);
                return byKey != 0 ? byKey : a.Index.CompareTo(b.Index);
            });
            for (var k = 0; k < scored.Count && k < limit; k++) hits.Add(scored[k].Index);
            return hits;
        }

        // 길의 길이(m)와, 종류별 빠르기로 나눈 시간(초). 길찾기가 고른 빠르기와 같은 값을 쓴다.
        public static void Measure(IReadOnlyList<(float X, float Z, byte Kind)> path, out float metres, out float seconds)
        {
            metres = 0f;
            seconds = 0f;
            if (path == null) return;
            for (var k = 1; k < path.Count; k++)
            {
                var dx = path[k].X - path[k - 1].X;
                var dz = path[k].Z - path[k - 1].Z;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                metres += length;
                seconds += length / 길찾기Policy.Speed(path[k].Kind);
            }
        }

        public static float WalkSeconds(float metres) => Math.Max(0f, metres) / WalkSpeed;

        public static string Metres(float metres) => metres >= 1000f
            ? (metres / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km"
            : Math.Round(Math.Max(0f, metres)).ToString("0", CultureInfo.InvariantCulture) + " m";

        public static string Time(float seconds)
        {
            var whole = (int)Math.Round(Math.Max(0f, seconds));
            if (whole < 60) return "약 " + whole + "초";
            return whole % 60 == 0 ? "약 " + whole / 60 + "분" : "약 " + whole / 60 + "분 " + whole % 60 + "초";
        }

        public static string DoorLabel(bool hasDoor, bool official) =>
            !hasDoor ? "출입구 미확인" : official ? "공식 건물 출입구" : "가상 출입구";
    }

    internal static class 길안내Checks
    {
        public static void Add(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            bool Close(float a, float b) => Math.Abs(a - b) < 0.01f;

            Check("길안내: 주소 글자는 공백 · 쉼표를 빼고 접음", 길안내Policy.Fold(" 면목로 23길, 15-2 ") == "면목로23길15-2" &&
                길안내Policy.Fold(null) == string.Empty && 길안내Policy.Key(string.Empty, "3") == string.Empty &&
                길안내Policy.Key("Test Road", "7") == "testroad7");

            var keys = new[]
            {
                길안내Policy.Key("가상로", "2"), 길안내Policy.Key("가상로", "23"), 길안내Policy.Key("가상로2길", "15"),
                길안내Policy.Key("시험대로", "2"), string.Empty, 길안내Policy.Key("가상로", "2"),
            };
            var names = new[] { string.Empty, string.Empty, string.Empty, 길안내Policy.Fold("시험 상가"), 길안내Policy.Fold("가상 빌라"), string.Empty };
            var exact = 길안내Policy.Search(keys, names, "가상로 2", 5);
            Check("길안내: 꼭 맞는 주소가 먼저이고, 같은 주소의 건물은 모두 후보", exact.Count == 4 && exact[0] == 0 && exact[1] == 5 &&
                exact[2] == 1 && exact[3] == 2, string.Join(",", exact));
            var byName = 길안내Policy.Search(keys, names, "시험상가", 5);
            Check("길안내: 건물 이름으로도 찾음", byName.Count == 1 && byName[0] == 3, string.Join(",", byName));
            Check("길안내: 두 글자보다 짧거나 맞는 것이 없으면 비어 있음", 길안내Policy.Search(keys, names, "가", 5).Count == 0 &&
                길안내Policy.Search(keys, names, "없는길 1", 5).Count == 0 && 길안내Policy.Search(null, null, "가상로", 5).Count == 0);
            Check("길안내: 후보 수는 한도까지만", 길안내Policy.Search(keys, names, "가상로", 2).Count == 2 &&
                길안내Policy.Search(keys, names, "가상로", 0).Count == 0);

            var path = new List<(float X, float Z, byte Kind)>
            {
                (0f, 0f, 길그래프.Road), (83f, 0f, 길그래프.Road), (83f, 42f, 길그래프.Alley),
            };
            길안내Policy.Measure(path, out var metres, out var seconds);
            Check("길안내: 거리는 길이의 합이고 시간은 종류별 빠르기로 나눈 합", Close(metres, 125f) &&
                Close(seconds, 83f / 길찾기Policy.RoadSpeed + 42f / 길찾기Policy.AlleySpeed), metres.ToString("0.0") + " m, " + seconds.ToString("0.0") + " s");
            길안내Policy.Measure(null, out var noMetres, out var noSeconds);
            Check("길안내: 길이 없으면 0이고, 걷는 시간은 걷는 빠르기로", noMetres == 0f && noSeconds == 0f &&
                Close(길안내Policy.WalkSeconds(12f), 10f) && 길안내Policy.WalkSeconds(-1f) == 0f);
            Check("길안내: 거리와 시간의 글", 길안내Policy.Metres(812.4f) == "812 m" && 길안내Policy.Metres(1234f) == "1.2 km" &&
                길안내Policy.Time(40.2f) == "약 40초" && 길안내Policy.Time(130f) == "약 2분 10초" && 길안내Policy.Time(120f) == "약 2분",
                길안내Policy.Metres(812.4f) + " / " + 길안내Policy.Metres(1234f) + " / " + 길안내Policy.Time(130f));
            Check("길안내: 문의 종류를 구분해 말함", 길안내Policy.DoorLabel(true, true) == "공식 건물 출입구" &&
                길안내Policy.DoorLabel(true, false) == "가상 출입구" && 길안내Policy.DoorLabel(false, true) == "출입구 미확인");
        }
    }
}
