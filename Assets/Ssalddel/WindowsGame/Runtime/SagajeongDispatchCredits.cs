using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 출처 · 라이선스 화면 (2026-11-25 사용자 13:13: 「클라우드에 APK 올려 사람들이랑 공유 … 그것에 맞춰서도 조정」).
    // 나눠 보는 빌드는 배포다. CC BY · 공공누리 · ODbL은 출처를, 나눔고딕(OFL 1.1)은 라이선스 전문을 함께 실어야 한다.
    // 여는 곳: 지도 아래 출처 줄 · 디오라마 아래 출처 줄 · 가로 화면의 출처 판(누르면 열림). 닫기 = 「닫기」 · Esc · 뒤로.
    public sealed partial class SagajeongDispatchView
    {
        private bool creditsOpen;
        private Vector2 creditsScroll;
        private string[] creditsLicenses;      // 나눔고딕 OFL 전문 · 오픈소스(MIT · Apache-2.0) 전문 — IMGUI 글자 수 한도 때문에 덩어리마다 따로 그린다

        private static readonly string[] CreditLines =
        {
            "#지도 바탕",
            "땅 덮개: © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium — CC BY 4.0",
            "지형 음영: Copernicus DEM GLO-30 — © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved",
            "길: 국토교통부 표준노드링크(ITS 국가교통정보센터) — 이용허락범위 제한 없음",
            "행정동 경계 · 이름: 통계청 SGIS(공공누리 제1유형) · vuski/admdongkor(CC BY 4.0)",
            "전국 경계: Natural Earth(퍼블릭 도메인)",
            "세 구 바탕의 차선 · 산 음영: 서울특별시 열린데이터광장 서울시 차선 관련 정보(OA-15537) · 서울시 경사도(OA-22241) — 이용허락범위 제한 없음",
            "법정동 · 구 경계: 국토교통부 법정구역정보(브이월드 공간정보 오픈플랫폼) — CC BY",
            "사가정 시작 지도 · 법정동 판 · 동 미리보기의 길과 건물: © OpenStreetMap contributors. 이 게임에 든 OpenStreetMap 파생 자료는 ODbL 1.0으로 제공합니다(원본: openstreetmap.org/copyright, 파생 자료: github.com/cheolwo/unity)",
            "#건물 · 동 디오라마",
            "건물 모양 · 높이 · 층수 · 쓰임: 국토교통부 GIS건물통합정보(공간정보 오픈플랫폼 · 공공데이터포털 15083092) — 공공누리 제1유형(출처표시)",
            "녹지 · 물: ESA WorldCover(CC BY 4.0) · 나무 · 색 · 창 불빛 · 그림자는 게임 표현",
            "#생활 자료(서울)",
            "서울특별시 열린데이터광장: 버스 노선(버스정보시스템) · 통행속도(TOPIS) · 지하철 승하차(OA-12252) · 역사 정보(서울교통공사) · 생활인구(OA-14991, KT 통신 자료 기반 추정) · 생활 패턴(OA-23016) · 가로등 점등(OA-22308) · 횡단보도(OA-23081) · 생활 시설 13종 · 식품위생업 현황",
            "공공데이터포털 · 관세청 입출항 물동량 · 기상청 · 농촌진흥청 농사로 · 해양수산부 · 국가데이터처",
            "생활인구 · 생활 패턴 — 출처: 서울특별시·KT, 서울시 통신데이터(OA-14991 · OA-23016). 원자료가 아닌 시간대 평균 · 지수만 싣습니다",
            "보안등: 서울특별시 안심귀갓길 안전시설물(OA-21696 · 공공데이터포털 15112100) — 공공누리 제1유형 · 관세청 물동량 — 이용허락범위 제한 없음",
            "#그림",
            "타로 카드: 1909년 라이더–웨이트–스미스 원판(그림 파멜라 콜먼 스미스) — 퍼블릭 도메인(Wikimedia Commons 스캔)",
            "지붕 재질: Poly Haven(CC0) · 아이콘 · 사람 그림 · 지도 표현은 이 프로젝트에서 AI로 만들었습니다",
            "#오픈소스",
            "Newtonsoft.Json — Copyright (c) 2007 James Newton-King, MIT License",
            "Unity 엔진과 함께 실리는 Android 구성요소(AndroidX · Kotlin 표준 라이브러리 등) — Apache License 2.0 (apache.org/licenses/LICENSE-2.0)",
            "#글꼴",
            "나눔고딕 — Copyright NAVER Corporation, SIL Open Font License 1.1(전문은 아래)",
            "#알림",
            "가게 · 기사 · 주문 · 돈은 모두 가상입니다. 실제 점포 · 사람과 관계없습니다.",
            "이 게임은 코드 · 그림 · 자료 가공까지 AI(Anthropic Claude, OpenAI Codex)로 만들었습니다.",
            "공공 자료의 수치는 각 기관이 발표한 그대로이며, 게임이 덧붙인 표현(색 · 나무 · 불빛 · 칸의 성격)은 측정값이 아닙니다.",
        };

        // 출처 줄을 눌렀는가(그린 뒤에 부른다)
        private void CreditsTapArea(Rect rect)
        {
            var current = Event.current;
            if (current.type == EventType.MouseUp && current.button == 0 && rect.Contains(current.mousePosition))
            {
                current.Use();
                creditsOpen = true;
                creditsScroll = Vector2.zero;
            }
        }

        private void DrawCreditsScreen()
        {
            var logicalWidth = PortraitScreen ? PortraitWidth : (float)Width;
            var scale = Screen.width / logicalWidth;
            if (scale <= 0f) return;
            var logicalHeight = Screen.height / scale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var top = PortraitScreen ? portraitTopInset : 0f;
            Paint(new Rect(0f, 0f, logicalWidth, logicalHeight), Fade(Canvas, 0.98f));
            Text(new Rect(16f, top + 12f, logicalWidth - 130f, 28f), "출처 · 라이선스", 19, Paper, true);
            var close = new Rect(logicalWidth - 100f, top + 10f, 86f, 34f);
            Paint(close, Mint, 8);
            Text(close, "닫기", 13, Canvas, true, TextAnchor.MiddleCenter);
            var current = Event.current;
            if (current.type == EventType.MouseUp && current.button == 0 && close.Contains(current.mousePosition) ||
                current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                current.Use();
                creditsOpen = false;
            }
            creditsLicenses ??= new[]
            {
                Resources.Load<TextAsset>("hongdal-font-korean-OFL")?.text ?? "",
                Resources.Load<TextAsset>("hongdal-oss-licenses")?.text ?? "",
            };
            var width = logicalWidth - 32f;
            var view = new Rect(0f, top + 54f, logicalWidth, logicalHeight - top - 54f);
            var y = 0f;
            var heights = new float[CreditLines.Length];
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 12, wordWrap = true };
            for (var i = 0; i < CreditLines.Length; i++)
                heights[i] = CreditLines[i].StartsWith("#") ? 30f : style.CalcHeight(new GUIContent(CreditLines[i]), width) + 6f;
            foreach (var h in heights) y += h;
            var licenseStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 10, wordWrap = true };
            var licenseHeights = new float[creditsLicenses.Length];
            var licenseHeight = 0f;
            for (var i = 0; i < creditsLicenses.Length; i++)
            {
                licenseHeights[i] = creditsLicenses[i].Length == 0 ? 0f : licenseStyle.CalcHeight(new GUIContent(creditsLicenses[i]), width) + 20f;
                licenseHeight += licenseHeights[i];
            }
            var content = new Rect(0f, 0f, logicalWidth - 20f, y + licenseHeight + 40f);
            creditsScroll = GUI.BeginScrollView(view, creditsScroll, content, false, false);
            y = 0f;
            for (var i = 0; i < CreditLines.Length; i++)
            {
                var line = CreditLines[i];
                if (line.StartsWith("#")) Text(new Rect(16f, y + 8f, width, 20f), line.Substring(1), 14, Mint, true);
                else Text(new Rect(16f, y, width, heights[i]), line, 12, Paper, false, TextAnchor.UpperLeft, true);
                y += heights[i];
            }
            for (var i = 0; i < creditsLicenses.Length; i++)
            {
                if (licenseHeights[i] <= 0f) continue;
                Text(new Rect(16f, y + 14f, width, licenseHeights[i]), creditsLicenses[i], 10, Soft, false, TextAnchor.UpperLeft, true);
                y += licenseHeights[i];
            }
            GUI.EndScrollView();
            // 손가락으로 끌어 넘기기
            if (current.type == EventType.MouseDrag && view.Contains(current.mousePosition))
            {
                creditsScroll.y = Mathf.Max(0f, creditsScroll.y - current.delta.y);
                current.Use();
            }
            GUI.matrix = previous;
        }
    }
}
