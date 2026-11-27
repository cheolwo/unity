using System;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 입장 고르기 (2026-11-23 20:50 사용자: 「시뮬레이션 시작하는 인트로에 각각의 입장에 대해서 선택을 하는 카드가 존재해서,
    //  그 카드를 클릭하면 그 입장과 관련한 정보들을 위주로 볼 수 있도록」).
    //  - 켤 때마다 시작 화면에 입장 카드(배달 기사 · 음식점 사장 · 동네 주민 · 화물 · 창고 · 홍달 경영자 · 직접 고르기).
    //  - 카드를 누르면 그 입장에 필요한 자료만 켠다(자료 창의 체크를 한 번에 바꾸는 것). 지도 · 하루 · 장부 · 규칙은 같다.
    //  - 지난번 입장에 「지난번」 표시. 자료 창 머리의 「입장」 단추로 언제든 다시 연다.
    //  - 패키지 고르기(내가 맡는 사업, 처음 한 번)와는 다르다: 이것은 「어떤 자료를 먼저 볼까」만 정한다.
    //  - 검증 · 방향 촬영에서는 뜨지 않는다(촬영이 직접 연다).
    // 3차(2026-11-24 12:54 사용자: 「관점이라니까 … 역지사지하는 그런 이제 카드에 그 입장에 대해서 생각을 하는 그런 카드에 … 놓일 수 있는 거니까
    //  그쪽으로 좀 정리가 되면 좋겠다」): 전국 지도 절기 장터의 관점 필터를 입장 카드에 합쳤다. 도매상 · 농가 카드를 더하고, 카드마다 「전국」 줄
    //  (장터에서 먼저 보는 것)을 적었다. 장터의 층 · 자료 줄 순위는 지금 입장을 따른다(SagajeongDispatchProduceFlow.cs). 카드는 네 장씩 두 줄.
    public sealed partial class SagajeongDispatchView
    {
        private const string StancePreference = "hongdal.stance";

        // 2차(21:30 「단위로 구분」): 입장마다 구 · 타일 · 동 세 단위의 묶음을 따로 둔다. 지도를 오르내리면 그 단위 묶음으로 바뀐다.
        //  정리 문서: docs/AI/Handoffs/2026-11-23-Claude-정리-입장별-단위별-자료.md
        private sealed class Stance
        {
            public string Key, Name, Who, Look, Where;
            public (string Unit, string Text)[] First;     // 카드에 단위별로 적는 「먼저 켜는 자료」
            public int[] Bits = new int[3];                // 구 · 타일 · 동
            public int[] Facilities = new int[3];
            public int Global = 16;                        // 모든 단위(타일 지도 · 플레이스 창 · 등급 이름 · 위 띠 경 수치)
            public Color Tint;
            // 4차(2026-11-24 15:02 필수 · 부가): 위 Bits · Facilities · Global은 「필수」(입장을 고르면 켬), 아래는 「부가」(자료 창 「부가도 켜기」).
            //  둘 다 Resources/hongdal-data-stance-info.json에서 온다(SagajeongDispatchStanceInfo.cs). 표가 없으면 위에 적힌 값 그대로.
            public int[] OptBits = new int[3];
            public int[] OptFacilities = new int[3];
            public int OptGlobal;
            public StanceInfoSet Info;
        }

        // 비트: 1 역 불빛 · 2 낮과 밤 · 4 비 · 눈 · 8 오늘 칩 · 16 타일 지도 · 32 노선 · 64 번화가 · 128 내력 · 256 빛의 번짐 · 512 길 · 1024 버스
        //       · 2048 홍달 플레이스 창 · 4096 등급 이름 · 8192 위 띠 경 수치
        // 갈래: 0 보행등 · 1 정류장 · 2 보행 출입구 · 3 그늘쉼물 · 4 자전거 · 5 공원나무 · 6 시장관광 · 7 역 출입구 · 8 그 밖
        private static int F(params int[] groups) { var mask = 0; foreach (var g in groups) mask |= 1 << g; return mask; }

        private static Stance[] stances;
        private static Stance[] Stances => stances ??= WithStanceInfo(new[]
        {
            new Stance
            {
                Key = "rider", Name = "배달 기사", Who = "나는 이 동네를 달리는 기사", Tint = Ink(0x7CDB72),
                Look = "길이 막히는 시각, 비 · 눈, 신호와 건물 입구, 쉴 곳",
                First = new[] { ("구", "길 속도 · 날씨 · 역 불빛"), ("타일", "건물 입구 · 쉼터"), ("동", "보행등 · 입구 · 쉼터 · 역 출입구"), ("늘", "위 띠 경 수치"), ("전국", "장터 제철 목록") },
                Where = "동 디오라마로 가까이 갈수록 자세해집니다",
                Bits = new[] { 32 | 512 | 4 | 8 | 2 | 1, 32 | 512 | 4 | 8 | 2, 32 | 512 | 4 | 2 },
                Facilities = new[] { 0, F(2, 3), F(0, 2, 3, 7) },
                Global = 16 | 8192,
            },
            new Stance
            {
                Key = "store", Name = "음식점 사장", Who = "나는 역 둘레에서 가게를 하는 사람", Tint = Ink(0xFFD395),
                Look = "사람이 언제 역을 드나드나, 오가는 길, 키워드 경쟁",
                First = new[] { ("구", "유동인구 · 역 불빛 · 버스 · 번화가"), ("타일", "정류장 · 시장"), ("동", "정류장 · 시장 · 역 출입구"), ("늘", "홍달 플레이스 창"), ("전국", "어디서 사나 · 도매값 · 동네 값") },
                Where = "구 지도에서 역을 눌러 보세요",
                Bits = new[] { 32 | 1 | 2 | 8 | 1024 | 64 | FloatBit, 32 | 1 | 2 | 8, 32 | 1024 | 2 },      // 구: 유동인구(13:25)
                Facilities = new[] { 0, F(1, 6), F(1, 6, 7) },
                Global = 16 | 2048,
            },
            new Stance      // 지사장(11-27): 세로 화면에서 고르면 구 지도 위 지사장 판 (SagajeongDispatchBranchBoard.cs)
            {
                Key = "branch", Name = "지사장", Who = "나는 구에서 배달 지사를 맡은 사람", Tint = Ink(0xF2C35B),
                Look = "동마다 내 몫, 기사와 가게, 다음 할 일",
                First = new[] { ("구", "동별 내 몫 · 지사 깃발 · 다음 할 일"), ("타일", "칸을 두 번 누르면 동 디오라마"), ("동", "동 디오라마") },
                Where = "폰 세로 화면에서 구 지도의 칸을 누르면 동 카드, 두 번 누르면 동 디오라마",
                Bits = new[] { 0, 0, 0 },
                Facilities = new[] { 0, 0, 0 },
                Global = 16,
            },
            new Stance
            {
                Key = "resident", Name = "동네 주민", Who = "나는 이 동네에 살며 장 보고 주문하는 사람", Tint = Ink(0x6FA8FF),
                Look = "걷고 · 쉬고 · 타는 동네의 생활 시설",
                First = new[] { ("구", "버스 · 낮밤 · 날씨"), ("타일", "공원 · 쉼터 · 시장"), ("동", "정류장 · 보행등 · 쉼터 · 공원 · 시장"), ("전국", "동네 장바구니 값 · 김장 셈") },
                Where = "동 디오라마에서 둘러보세요",
                Bits = new[] { 32 | 1024 | 2 | 4 | 8, 32 | 2 | 4 | 8, 32 | 1024 | 4 | 2 },
                Facilities = new[] { 0, F(3, 5, 6), F(0, 1, 3, 4, 5, 6) },
            },
            new Stance
            {
                Key = "cargo", Name = "화물 · 창고", Who = "나는 물건을 싣고 내리는 사람(트럭 기사)", Tint = Ink(0xF0913A),
                Look = "큰길이 언제 느린가, 날씨, 세울 곳 · 택배함",
                First = new[] { ("구", "길 속도 · 날씨"), ("타일", "주차장 · 택배함"), ("동", "길 이름 · 주차장 · 택배함"), ("전국", "산지 → 시장 노선 · 경매 시각") },
                Where = "구 지도에서 큰길 색을 보세요",
                Bits = new[] { 32 | 512 | 4 | 8, 32 | 512 | 4, 512 | 4 | 2 },
                Facilities = new[] { 0, F(8), F(8) },
            },
            new Stance
            {
                Key = "wholesale", Name = "도매상", Who = "나는 새벽 시장에서 사고파는 사람(중도매인)", Tint = Ink(0xE8B04A),
                Look = "오늘 밤 경매 값, 어디서 얼마나 들어오나, 큰길",
                First = new[] { ("구", "길 속도 · 낮밤 · 오늘 칩"), ("타일", "시장"), ("동", "시장 · 큰길"), ("전국", "경락 · 경매 시각 · 생산 전망") },
                Where = "전국 지도에서 절기 장터를 켜 보세요",
                Bits = new[] { 32 | 512 | 8 | 2, 32 | 512, 32 | 512 | 2 },
                Facilities = new[] { 0, F(6), F(6, 8) },
            },
            new Stance
            {
                Key = "farm", Name = "농가", Who = "나는 밭에서 길러 내보내는 사람", Tint = Ink(0x8FD16A),
                Look = "올해 얼마나 심었나, 남을까 모자랄까, 내가 받는 값",
                First = new[] { ("구", "날씨 · 낮과 밤"), ("타일", "시장"), ("동", "시장"), ("전국", "심은 넓이 · 수급 · 받는 값") },
                Where = "전국 지도에서 절기 장터를 켜 보세요",
                Bits = new[] { 32 | 4 | 2 | 8, 32 | 4, 32 | 4 | 2 },
                Facilities = new[] { 0, F(6), F(6) },
            },
            new Stance
            {
                // 지휘관(2026-11-25 13:43 「입장을 보는 카드에 지휘관을 추가 … 적군이랑 싸우는 식으로 … 데이터들이 불러와져가지고 … 플레이」):
                //  자료를 보는 데서 그치지 않고 할 일까지 바뀌는 첫 입장. 들어가면 동 디오라마에 판독 층(SagajeongDispatchOpsRead.cs).
                Key = "commander", Name = "지휘관", Who = "나는 동을 지키는 분대를 이끄는 사람", Tint = Ink(0xFF7A6B),
                Look = "적이 어디로 오나, 어디서 막나, 어디서 내려다보나",
                First = new[] { ("동", "진입로 · 길목 · 고지 · 사각"), ("늘", "키워드 순위(플레이스)"), ("구", "지하철 노선 · 타일") },
                Where = "타일을 두 번 누르면 그 동의 작전 지도",
                Bits = new[] { 32, 32, 32 | 2 },
                Facilities = new[] { 0, 0, 0 },
                Global = 16 | 2048,
            },
            new Stance
            {
                Key = "owner", Name = "홍달 경영자", Who = "나는 홍달을 키우는 사람(게임)", Tint = Ink(0xC6ACFF),
                Look = "게임 요소 · 시험 값까지 모두",
                First = new[] { ("구", "역 불빛 · 번화가 · 내력 · 번짐"), ("타일", "역 불빛 · 번화가"), ("동", "기사 빛 상태창"), ("늘", "플레이스 창 · 등급 · 경 수치"), ("전국", "장터 모든 층") },
                Where = "예전처럼 다 켠 화면입니다",
                Bits = new[] { 32 | 1 | 64 | 128 | 256 | FloatBit, 32 | 1 | 64, 32 },
                Facilities = new[] { 0, 0, 0 },
                Global = 16 | 2048 | 4096 | 8192,
            },
            new Stance
            {
                Key = "custom", Name = "직접 고르기", Who = "필요한 것만 하나씩", Tint = Ink(0x9AA7B2),
                Look = "지하철 노선만 켜고 자료 창을 엽니다",
                First = new[] { ("모두", "지하철 노선 · 타일 지도") },
                Where = "자료 창에서 단위마다 체크로 고릅니다",
                Bits = new[] { 32, 32, 32 },
                Facilities = new[] { 0, 0, 0 },
            },
        });

        private static Stance[] WithStanceInfo(Stance[] list)
        {
            foreach (var stance in list) ApplyStanceInfo(stance);      // 필수 · 부가 표(SagajeongDispatchStanceInfo.cs)
            return list;
        }

        private bool stancePickerOpen;
        private bool stanceAsked;
        private string stanceKey;

        private bool StancePickerVisible => TidyScreen && stancePickerOpen && !패키지고르기표시;

        private string StanceName => Array.Find(Stances, item => item.Key == stanceKey)?.Name;

        // Start에서 부른다. 검증 · 방향 촬영이 아니면 켤 때마다 묻는다.
        private void LoadStance()
        {
            stanceKey = PlayerPrefs.GetString(StancePreference, string.Empty);
            if (stanceAsked) return;
            stanceAsked = true;
            var proof = Array.Exists(Environment.GetCommandLineArgs(), value =>
                value.StartsWith("--sagajeong-", StringComparison.Ordinal) || value.StartsWith(ViewProofFlag, StringComparison.Ordinal));
            stancePickerOpen = !proof;
            portraitTitleOpen = !proof;      // 세로 화면: 시작 화면부터 (SagajeongDispatchBranchBoard.cs)
        }

        private void ApplyStance(Stance stance, bool save)
        {
            ReadDataControl();
            var chosen = StanceChosenOptional(stance);      // 부가: 기본은 모두 불러온다(16:28) · 카드 칩으로 뺀 것만 빼고
            for (var u = 0; u < 3; u++)
            {
                dataUnitBits[u] = (stance.Bits[u] | chosen.Bits[u]) & ~GlobalBits;
                dataUnitFacilities[u] = stance.Facilities[u] | chosen.Facilities[u];
            }
            dataGlobalBits = (stance.Global | chosen.Global) & GlobalBits;
            LoadDataUnit(dataUnit >= 0 ? dataUnit : CurrentDataUnit());
            stanceKey = stance.Key;
            stancePickerOpen = false;
            dataPanelOpen = stance.Key == "custom";
            if (!save) return;
            SaveDataControl();
            PlayerPrefs.SetString(StancePreference, stance.Key);
            PlayerPrefs.Save();
            notice = stance.Info != null
                ? $"입장 · {stance.Name} — 필수와 고른 부가를 불러왔습니다. 부가는 입장 카드의 칩이나 ESC → 화면 · 지도 「입장 자료」에서 빼고 넣습니다."
                : $"입장 · {stance.Name} — 구 · 타일 · 동 단위마다 이 입장의 자료를 켰습니다. 자료 창에서 단위별로 더 고칠 수 있습니다.";
        }

        private void DrawStancePicker()
        {
            if (!StancePickerVisible) return;
            Paint(new Rect(0, 0, Width, Height), Fade(Ink(0x080D13), 0.95f));
            Text(new Rect(0, 30, Width, 18), "홍달 경영 시뮬레이션 · 입장 고르기", 12, Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(0, 52, Width, 34), "누구의 눈으로 볼까요?", 24, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(0, 88, Width, 18), "역지사지 카드 — 하루 한 번 이상 마주치는 것(필수)과 주 · 달에 몇 번 보는 것(부가)을 함께 불러옵니다. 부가 칩을 누르면 빼고 넣습니다.", 12, Soft,
                align: TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(stanceKey))
            {
                var close = new Rect(Width - 64, 24, 40, 28);
                Text(close, "×", 22, Soft, align: TextAnchor.MiddleCenter);
                if (Hit(close)) stancePickerOpen = false;          // 지난번 자료 그대로
            }

            var list = Stances;
            const float left = 48f, top = 116f, gap = 12f, cardHeight = 276f;      // 276: 부가 칩 두 줄(16:28)
            var perRow = Mathf.Max(4, (list.Length + 1) / 2);      // 두 줄 그대로(아홉 장이면 다섯 장씩 · 11-25 지휘관)
            var width = (Width - left * 2 - gap * (perRow - 1)) / perRow;
            for (var i = 0; i < list.Length; i++)
            {
                var stance = list[i];
                var card = new Rect(left + i % perRow * (width + gap), top + i / perRow * (cardHeight + gap), width, cardHeight);
                var last = stance.Key == stanceKey;
                var over = card.Contains(Event.current.mousePosition);
                Paint(card, last || over ? stance.Tint : Rim, 12);
                Paint(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), Canvas, 11);
                var px = card.x + 14;
                var pw = card.width - 28;
                Paint(new Rect(px, card.y + 15, 10, 10), stance.Tint, 5);
                Text(new Rect(px + 16, card.y + 11, pw - 130, 16), $"입장 {i + 1}" + (last ? " · 지난번" : string.Empty), 10, last ? Gold : Soft, true);
                var go = new Rect(card.xMax - 122, card.y + 9, 108, 24);
                var chosen = ExpansionButton(go, last ? "이 입장으로 계속" : "이 입장으로", true, last ? Gold : Mint);
                Text(new Rect(px, card.y + 32, pw, 26), stance.Name, 19, Paper, true);
                Text(new Rect(px, card.y + 58, pw, 16), stance.Who, 11, Soft);
                Text(new Rect(px, card.y + 79, 50, 14), "보는 것", 10, stance.Tint, true);
                Text(new Rect(px + 50, card.y + 77, pw - 50, 32), stance.Look, 11, Paper, false, TextAnchor.UpperLeft, true);
                float rowY;
                if (stance.Info != null) rowY = DrawStanceTiers(stance, px, card.y + 112f, pw, card.yMax - 6f);      // 필수 · 부가(15:02) · 부가 칩(16:28)
                else
                {
                    Text(new Rect(px, card.y + 112, pw, 14), "먼저 켜는 자료", 10, stance.Tint, true);
                    rowY = card.y + 130;
                    foreach (var (unit, item) in stance.First)
                    {
                        var chip = new Rect(px, rowY + 1, 30, 16);
                        Paint(chip, Fade(stance.Tint, unit == "전국" ? 0.34f : 0.18f), 4);
                        Text(chip, unit, 9, stance.Tint, true, TextAnchor.MiddleCenter);
                        var lines = Mathf.Max(1, Mathf.CeilToInt(GuNameSize(item, 11, false).x / Mathf.Max(40f, pw - 40f)));      // 잰 글 폭으로 줄 수
                        Text(new Rect(px + 36, rowY + 1, pw - 36, 16 * lines + 2), item, 11, Paper, false, TextAnchor.UpperLeft, true);
                        rowY += 16 * lines + 4;
                    }
                }
                if (rowY < card.yMax - 24) Text(new Rect(px, card.yMax - 22, pw, 16), stance.Where, 10, Soft);
                if (chosen) BeginStanceLoading(stance, true);      // 불러오기 창 → 디오라마(16:52 · SagajeongDispatchDongFocus.cs)
                else if (Event.current.type == EventType.MouseUp && over && Event.current.button == 0 && !go.Contains(Event.current.mousePosition))
                {
                    BeginStanceLoading(stance, true);    // 카드 어디를 눌러도 고른다
                    Event.current.Use();
                }
            }
            var rows = (list.Length + perRow - 1) / perRow;
            Text(new Rect(0, top + rows * (cardHeight + gap) + 4, Width, 18),
                "규칙 · 돈 · 주문은 입장과 같이 바뀌지 않습니다. 뺀 부가는 켜기 전까지 읽지 않습니다. 고른 것은 입장마다 기억하고, ESC → 화면 · 지도 「입장 자료」나 자료 창 「입장」 단추로 다시 엽니다.",
                10, Soft, align: TextAnchor.MiddleCenter);
            if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel) Event.current.Use();
        }
    }
}
