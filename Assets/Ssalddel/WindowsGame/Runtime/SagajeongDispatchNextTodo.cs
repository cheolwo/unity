using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 다음 할 일 — 화에서 고르고(이번 턴 계획), 턴을 넘겨 확정하고, 중에서 결과를 본다 (2026-11-16, Codex r247 ·
    // Claude 개발 계획 docs/AI/Handoffs/2026-11-16-Claude-개발계획-다음할일-위험알림과고를일-r247.md, 사용자 「구현 진행」).
    //  - 오른쪽 아래 결정 순회(가상 안건) 자리와 Enter를 이어받는다. 일반 실행에서만 켠다(검증 촬영은 예전 화면 그대로).
    //  - 누르면 오른쪽에 안내판: 위험 알림 · 고를 일 · 지켜볼 것. 커널 신호와 첫 사슬의 다음 행동만 읽는다(다음할일Policy).
    //  - 「고르러 가기」는 그 대상의 창을 연다(매장 재료 탭 · 배차 데스크 · 동 관리). 여는 것만으로 아무것도 보내지 않는다.
    //  - 화에서는 그 창들의 확정 단추가 「이번 턴 계획에 담기」가 된다. 담기 · 바꾸기 · 빼기 · 미룸은 커널에 아무것도 보내지 않는다.
    //  - 「턴 넘기기」에서만 보낸다: 고르지 않은 일이 있으면 한 번 묻고 미룸 → 항목마다 다시 확인 → 통과한 것만 기존 명령으로 한 번
    //    → 중으로 자동 귀환 → 「지난 턴에 고른 것」이 같은 원 ID의 진행을 읽는다. 막힌 것은 이유와 함께 실패로 남고 대신 고르지 않는다.
    //  - 화에서 안내판이나 계획이 열린 동안 시계를 멈추고 넘기면 원래 배속으로 이어 간다(사용자가 배속을 누르면 그것을 따른다).
    //  - 계획 · 결과 · 미룸은 저장하지 않는다(r247 저장 범위 미정). 게임을 다시 켜면 비어 있다.
    //  - 한 단추 순환(2026-11-17 사용자 「버튼만 계속 누르면 뭘 할지 알 수 있도록」 · 「모듈 줄만 걷기」 · 「안내판 아래 바로 가기」):
    //    맨 아래 사업 줄을 걷고, 오른쪽 아래 두 줄 단추가 누를 때마다 다음 일로 간다. Enter도 같다.
    //    화: 다음 일의 창을 연다 → 다 고르거나 미루면 같은 자리가 「턴 넘기기」. 중: 「다음 턴 준비 · 화로」(화로 바꾸고 첫 일을 연다).
    //    단추 오른쪽 끝 「목록」은 안내판만 펴고 접는다. 걷은 줄의 00 경영 · 지사·장부 · 플레이스 · 역할 보기 · 사업 선택은 안내판 아래 바로 가기로.
    public sealed partial class SagajeongDispatchView
    {
        private const string 배차대기신호 = "signal:food:waiting:";
        private static readonly Rect 할일단추칸 = new(986, 610, 272, 50);      // 두 줄 단추: 작은 지도 아래 · 걷은 사업 줄 자리까지
        private static readonly Rect 할일목록칸 = new(1206, 610, 52, 50);      // 단추 오른쪽 끝: 안내판만 펴고 접기
        // 안내판: 작은 지도 자리에서 위로. 위쪽 띠(인계 사슬 · 관찰 띠 · 신호 줄)가 x 976까지 오므로 그 아래에서 시작한다.
        private Rect 할일판칸
        {
            get
            {
                var top = 132f;
                if (ChainStripVisible) top = Mathf.Max(top, ChainStrip.yMax + 6);
                if (관찰띠표시) top = Mathf.Max(top, 관찰띠칸.yMax + 6);
                if (SignalBarVisible) top = Mathf.Max(top, SignalBar.yMax + 6);
                top = Mathf.Min(top, 300f);
                return new Rect(906, top, 352, 602 - top);
            }
        }
        private static readonly Rect 턴확인칸 = new(440, 230, 400, 200);
        private static readonly string[] 발주칸 = { "발주", "센터 출고", "화물", "검수" };
        private static readonly string[] 배달칸 = { "지정", "가게 도착", "픽업", "전달" };

        private bool 다음할일촬영;                          // 방향 촬영의 다음 할 일 장면에서만 켠다
        private readonly 이번턴계획 턴계획 = new();
        private List<다음할일항목> 할일목록 = new();
        private readonly Dictionary<string, Signal> 할일신호 = new();
        private readonly HashSet<string> 미룬할일 = new();     // 지난 턴에 미룬 명령 키
        private readonly HashSet<string> 이번턴미룸 = new();    // 이번 턴에 「미룸」을 누른 명령 키
        private readonly List<턴결과> 지난턴결과 = new();
        private bool 할일판열림;
        private bool 할일결과접힘;
        private string 할일강조;                            // 지금 고르는 항목의 명령 키
        private string 할일전환대기;                        // 중에서 「고르러 가기」를 눌러 화 전환을 묻는 동안의 명령 키
        private bool 턴넘김확인;
        private bool 턴넘기는중;
        private int 할일턴 = 1;
        private int 턴시계보류;                             // 계획하는 동안 멈춰 둔 배속 (0이면 멈춘 적 없음)
        private string 할일알림;
        private float 할일알림때;
        private bool 할일판그림;                            // 이번 그리기에서 안내판을 그렸나 (촬영이 읽는다)
        private bool 할일단추그림;
        private string 할일열린키;                          // 단추 · Enter로 마지막에 창을 연 일 (다음 누름은 그다음 일로)
        private string 단추마지막;                          // 이번 그리기의 단추 상태 (촬영이 읽는다)
        private int 바로가기그림;                           // 이번 그리기에서 그린 바로 가기 칸 수 (촬영이 읽는다)
        private bool 아래줄그림;                            // 맨 아래 사업 줄을 그렸나 (SagajeongDispatchExpansion.cs, 촬영이 읽는다)

        private sealed class 턴결과
        {
            public string 명령키;
            public 계획종류 종류;
            public string 제목;
            public bool 미룸;
            public string 실패;
            public string 공급주문Id;
            public string 주문Id;
            public string 기사;
            public string 완료문장;
            public (int 칸, 결과상태 상태, string 문장)? 마지막;
        }

        private bool 다음할일사용 => 다음할일촬영 || autopilotPersistent && TidyScreen;
        private bool 턴계획모드 => 다음할일사용 && !autopilotOn;
        private bool 아래줄걷음 => 다음할일사용;          // 한 단추 순환: 다음 할 일이 켜진 화면에서는 맨 아래 사업 줄을 걷는다
        private bool 할일보임 => 다음할일사용 && expansionView && !mapOnly && state != null;
        private bool 할일오른쪽비었나 => actorFocus == null && selectedBuilding == null && !dongPanelOpen && !zonePanelOpen &&
            !DailyReportVisible && !ExpansionPanelVisible && !SettingsVisible && !DongPickerVisible && !PlaceVisible;
        private bool 할일판보임 => 할일보임 && 할일판열림 && 할일오른쪽비었나;
        private bool 다음할일Covers(Vector2 point) => 할일보임 && (할일단추칸.Contains(point) || 할일판보임 && 할일판칸.Contains(point));
        private string 할일매장자리 => engine?.Pack?.network?.store?.placeId;

        // ── 항목 모으기 (Layout에서 한 번) ──
        private void 할일목록갱신()
        {
            if (Event.current.type != EventType.Layout) return;
            할일신호.Clear();
            var 원천 = new List<할일원천>();
            foreach (var signal in signals)
            {
                if (signal == null || signal.ResolvedMs != 0) continue;
                var 배차대기 = signal.ReasonCode == "dispatch-waiting";
                if (배차대기 && autopilotOn) continue;       // 중에서는 맡김이 배차한다(신호 줄과 같음)
                할일신호[signal.Id] = signal;
                원천.Add(new 할일원천
                {
                    키 = signal.Id, 업무 = signal.OsId, 사유 = signal.ReasonCode, 장소 = signal.PlaceId ?? string.Empty,
                    위험 = signal.IsDanger, 단추말 = signal.ActionLabel ?? string.Empty, 제목 = signal.Title ?? string.Empty,
                    문구 = signal.Message ?? string.Empty, 시작Ms = signal.SinceMs, 판본 = Math.Max(signal.SinceMs, signal.EscalatedMs),
                    주문Id = 배차대기 && signal.Id.StartsWith(배차대기신호, StringComparison.Ordinal) ? signal.Id.Substring(배차대기신호.Length) : null,
                });
            }
            if (chain != null && chain.ActionKey is "order" or "inspect" or "produce" && 할일매장자리 != null)
                원천.Add(new 할일원천
                {
                    키 = "chain:" + chain.ActionKey, 업무 = BusinessIds.StoreOperations, 사유 = chain.ActionKey, 사슬 = true,
                    장소 = 할일매장자리, 제목 = StoreRole(할일매장자리)?.title ?? "내 매장", 문구 = chain.Headline, 시작Ms = BusinessNow,
                });
            할일목록 = 다음할일Policy.모으기(원천, 원 => (원.업무 == BusinessIds.StoreOperations || 원.사슬 ? 할일매장자리 : null,
                원.장소 != null && 원.장소.StartsWith("dong:", StringComparison.Ordinal) ? 원.장소.Substring(5) : null), 미룬할일);
            foreach (var 결과 in 지난턴결과) 결과.마지막 = 결과읽기(결과);
        }

        private int 할일수 => 다음할일Policy.셀수(할일목록);
        private int 위험수 => 할일목록.Count(item => item.묶음 == 할일묶음.위험);
        private int 고를일수 => 할일목록.Count(item => item.묶음 == 할일묶음.고를일);
        private List<다음할일항목> 남은할일 => 다음할일Policy.고르지않은(할일목록, 턴계획).Where(item => !이번턴미룸.Contains(item.명령키)).ToList();

        // ── 그리기 (DrawCycle이 부른다) ──
        private void DrawNextTodo()
        {
            할일판그림 = false;
            할일단추그림 = false;
            바로가기그림 = 0;
            if (!할일보임) return;
            할일목록갱신();
            할일전환이어가기();
            HandleNextTodoKey();
            if (할일판보임) DrawNextTodoPanel();
            DrawNextTodoDock();
        }

        private void HandleNextTodoKey()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown || 턴넘김확인) return;
            if (current.keyCode != KeyCode.Return && current.keyCode != KeyCode.KeypadEnter) return;
            if (CampaignPolicyOpen || SettingsVisible || DongPickerVisible || PlaceVisible || 경영판보임 || GUIUtility.keyboardControl != 0) return;
            current.Use();
            할일단추누름();                       // 단추와 같다: 누를 때마다 다음 일로 (한 단추 순환)
        }

        // 단추가 다음에 열 일: 화에서 아직 고르지 않은 일 중 다음 것. 담고 나서 금색으로 짚어 둔 일이 있으면 그것부터.
        private 다음할일항목 단추다음일()
        {
            var 남은 = 남은할일;
            if (남은.Count == 0) return null;
            if (할일강조 != null && 할일강조 != 할일열린키)
            {
                var 짚은 = 남은.FirstOrDefault(item => item.명령키 == 할일강조);
                if (짚은 != null) return 짚은;
            }
            var 지금 = 남은.FindIndex(item => item.명령키 == (할일열린키 ?? 할일강조));
            return 남은[(지금 + 1) % 남은.Count];
        }

        private string 단추상태 => 턴넘기는중 ? "넘기는중" : autopilotOn ? "다음턴준비" : 단추다음일() != null ? "다음일" : "턴넘기기";

        // 한 단추 순환: 화 → 다음 일의 창 → … → (다 고르거나 미루면) 턴 넘기기 → 중 → 다음 턴 준비 → 화 …
        private void 할일단추누름()
        {
            if (턴넘기는중 || 턴넘김확인) return;
            if (autopilotOn)
            {
                // 중 → 화: 단추가 「다음 턴 준비 · 화로」라고 말하므로 바로 바꾼다. 첫 일이 있으면 그 창을 연다.
                SetAutopilot(false);
                할일판열림 = true;
                할일오른쪽비우기();
                할일열린키 = null;
                var 첫 = 단추다음일();
                if (첫 != null) 할일열기(첫);
                return;
            }
            var 다음 = 단추다음일();
            if (다음 != null)
            {
                할일열기(다음);
                return;
            }
            턴넘김요청();                         // 다 고르거나 미뤘다: 같은 자리가 턴 넘기기
        }

        private void DrawNextTodoDock()
        {
            var dock = 할일단추칸;
            할일단추그림 = true;
            var 상태 = 단추상태;
            단추마지막 = 상태;
            var 목록 = 할일목록칸;
            var 본 = new Rect(dock.x, dock.y, 목록.x - dock.x - 4, dock.height);
            var 위험 = 위험수;
            string 윗줄, 꼬리, 아랫줄;
            Color 바탕;
            switch (상태)
            {
                case "넘기는중":
                    바탕 = Tile;
                    윗줄 = "턴을 넘기는 중";
                    꼬리 = string.Empty;
                    아랫줄 = "다시 확인하고 보냅니다";
                    break;
                case "다음턴준비":
                {
                    var 수 = 할일수;
                    var 보냄 = 지난턴결과.Count(r => !r.미룸 && r.실패 == null);
                    바탕 = 수 > 0 ? Gold : Tile;
                    윗줄 = "다음 턴 준비 · 화로";
                    꼬리 = "중 · Enter";
                    아랫줄 = (수 > 0 ? "할 일 " + 수 + (위험 > 0 ? " · 위험 " + 위험 : string.Empty) : "할 일 없음 · 지켜보는 중") +
                        (지난턴결과.Count > 0 ? " · 지난 턴 보냄 " + 보냄 : string.Empty);
                    break;
                }
                case "다음일":
                {
                    var 다음 = 단추다음일();
                    var 고르는중 = 다음.명령키 == 할일열린키 && 할일강조 == 할일열린키;
                    바탕 = Gold;
                    윗줄 = "다음 할 일 · " + 남은할일.Count;
                    꼬리 = (위험 > 0 ? "위험 " + 위험 + " · " : string.Empty) + (턴계획.Count > 0 ? "계획 " + 턴계획.Count : "Enter");
                    아랫줄 = (고르는중 ? "고르는 중 · " : "→ ") + 다음.제목 + " · " + 다음.고를것;
                    break;
                }
                default:
                {
                    var 미룸 = 다음할일Policy.고르지않은(할일목록, 턴계획).Count;
                    바탕 = Mint;
                    윗줄 = "턴 넘기기 · " + 할일턴 + "턴";
                    꼬리 = "Enter";
                    아랫줄 = 턴계획.Count > 0 ? "계획 " + 턴계획.Count + "건 보내고 중으로" + (미룸 > 0 ? " · 미룸 " + 미룸 : string.Empty)
                        : 미룸 > 0 ? "모두 미룸 " + 미룸 + " · 중으로" : "고를 일 없음 · 중으로 지켜보기";
                    break;
                }
            }
            var 진함 = 바탕 == Gold || 바탕 == Mint;
            var ink = 진함 ? Canvas : Paper;
            Paint(본, 바탕, 8);
            if (상태 == "다음일" && 위험 > 0) Paint(new Rect(본.x + 1, 본.y + 7, 4, 본.height - 14), SignalRed, 2);
            Text(new Rect(본.x + 12, 본.y + 4, 본.width - 96, 20), 윗줄, 13, ink, true);
            Text(new Rect(본.xMax - 90, 본.y + 4, 84, 20), 꼬리, 9, Fade(ink, 0.8f), align: TextAnchor.MiddleRight);
            Text(new Rect(본.x + 12, 본.y + 25, 본.width - 18, 20), 줄임(아랫줄, 20), 11, 진함 ? Fade(Canvas, 0.85f) : Soft);
            Paint(목록, 할일판열림 ? Rim : Tile, 8);
            Text(new Rect(목록.x, 목록.y + 6, 목록.width, 20), "목록", 11, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(목록.x, 목록.y + 25, 목록.width, 18), 할일판열림 ? "▾ 접기" : "▴ 펴기", 9, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(목록))
            {
                할일판열림 = !할일판열림;
                if (할일판열림) 할일오른쪽비우기();
            }
            else if (상태 != "넘기는중" && Hit(본)) 할일단추누름();
        }

        // 걷은 사업 줄의 입구를 안내판 아래 한 줄로 (2026-11-17 사용자 「안내판 아래 바로 가기」).
        private void 바로가기그리기(Rect line)
        {
            var 칸들 = new (string 말, bool 됨, Action 열기)[]
            {
                ("00 경영", 경영판가능, 경영판열기),
                ("지사·장부", 플랫폼장부가능, () =>
                {
                    본사쪽 = 플랫폼패키지 ? 0 : 1;
                    플랫폼장부열기();
                }),
                ("플레이스", PlaceAvailable, () => OpenPlace()),
                ("역할 보기", 역할입구가능, 역할목록열기),
                ("사업 선택", TidyScreen, () => 패키지고르기열림 = true),
            };
            const float gap = 4f;
            var w = (line.width - gap * (칸들.Length - 1)) / 칸들.Length;
            for (var i = 0; i < 칸들.Length; i++)
            {
                var (말, 됨, 열기) = 칸들[i];
                var chip = new Rect(line.x + i * (w + gap), line.y, w, line.height);
                바로가기그림++;
                if (!ExpansionButton(chip, 말, 됨, Tile)) continue;
                할일판열림 = false;
                열기();
                return;
            }
        }

        // 안내판을 펼 때 오른쪽 칸(카드 · 휴대폰 · 동 패널)을 비운다. 왼쪽 창은 그대로.
        private void 할일오른쪽비우기()
        {
            if (autopilotOn) return;
            actorFocus = null;
            selectedBuilding = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
            dailyReportOpen = false;
            expansionPanelOpen = false;
        }

        private void DrawNextTodoPanel()
        {
            var panel = 할일판칸;
            할일판그림 = true;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);
            var x = panel.x + 12;
            var width = panel.width - 24;
            var 화 = !autopilotOn;
            Text(new Rect(x, panel.y + 8, 120, 24), "다음 할 일", 16, Paper, true);
            Text(new Rect(x + 92, panel.y + 12, 160, 18), 화 ? "화 · " + 할일턴 + "턴" : "중 · 보기만", 10, 화 ? Gold : Mint, true);
            var 닫기 = new Rect(panel.xMax - 70, panel.y + 9, 58, 20);
            Text(닫기, "닫기  Esc", 10, Soft, align: TextAnchor.MiddleRight);
            if (Hit(닫기))
            {
                할일판열림 = false;
                return;
            }
            // 고르지 않은 일이 남아도 넘기고 싶을 때(한 번 묻고 미룸). 다 고르면 아래 단추가 「턴 넘기기」가 된다.
            if (화 && 아래줄걷음 && 남은할일.Count > 0 && !턴넘기는중 &&
                ExpansionButton(new Rect(panel.xMax - 154, panel.y + 9, 78, 20), "턴 넘기기", true, Tile))
            {
                턴넘김요청();
                return;
            }
            var 지켜볼 = 할일목록.Count(item => item.묶음 == 할일묶음.지켜볼것);
            var 머리 = "위험 " + 위험수 + " · 고를 일 " + 고를일수 + " · 지켜볼 것 " + 지켜볼;
            if (화 && 턴계획.Count > 0) 머리 += " · 계획 " + 턴계획.Count;
            if (화 && 턴시계보류 != 0) 머리 += " · 시계 멈춤";
            Text(new Rect(x, panel.y + 32, width, 16), 머리, 10, Soft);
            var y = panel.y + 54;
            var bottom = panel.yMax - (아래줄걷음 ? 54 : 26);

            if (지난턴결과.Count > 0)
                y = 할일결과칸(x, y, width, bottom);
            if (화 && 턴계획.Count > 0)
                y = 할일계획칸(x, y, width, bottom);

            할일묶음? 지난묶음 = null;
            var 숨김 = 0;
            for (var i = 0; i < 할일목록.Count; i++)
            {
                var item = 할일목록[i];
                if (화 && item.명령키 != null && 턴계획.찾기(item.명령키) != null) continue;      // 계획에 담은 일은 위 「이번 턴 계획」에
                var 높이 = item.묶음 == 할일묶음.지켜볼것 ? 40f : 58f;
                if (지난묶음 != item.묶음) 높이 += 20f;
                if (y + 높이 > bottom)
                {
                    숨김++;
                    continue;
                }
                if (지난묶음 != item.묶음)
                {
                    지난묶음 = item.묶음;
                    var 색 = item.묶음 == 할일묶음.위험 ? SignalRed : item.묶음 == 할일묶음.고를일 ? Gold : Sky;
                    Paint(new Rect(x + 2, y + 6, 8, 8), 색, 4);
                    Text(new Rect(x + 16, y, width - 16, 18), item.묶음 == 할일묶음.위험 ? "위험 알림" :
                        item.묶음 == 할일묶음.고를일 ? "고를 일" : "지켜볼 것 · 세지 않음", 11, 색, true);
                    y += 20;
                }
                y = 할일줄(item, x, y, width);
            }
            if (할일목록.Count == 0)
                Text(new Rect(x, y, width, 34), "지금 커널 신호와 매장 사슬에 고를 일이 없습니다. 지켜보다가 일이 생기면 여기에 뜹니다.", 10, Soft, wrap: true);
            if (숨김 > 0) Text(new Rect(x, bottom - 2, width, 14), "+ " + 숨김 + "건 더 · 위의 일을 고르면 이어서 보입니다", 9, Soft);
            if (아래줄걷음) 바로가기그리기(new Rect(x, panel.yMax - 50, width, 22));
            var 발 = 화 ? "여기서 누르는 것은 창을 여는 것뿐 · 돈 · 재고 · 배차는 턴을 넘길 때만"
                : "중에서는 보기만 · 고르려면 「고르러 가기」로 화로 바꿉니다";
            if (!string.IsNullOrEmpty(할일알림) && Time.unscaledTime - 할일알림때 < 4f) 발 = 할일알림;
            Text(new Rect(x, panel.yMax - 22, width, 16), 발, 9, Fade(Soft, 0.9f));
            if (Event.current.type == EventType.Repaint) 할일번호그리기();
        }

        // 항목 한 줄. 다음 y를 돌려준다.
        private float 할일줄(다음할일항목 item, float x, float y, float width)
        {
            var 지켜볼 = item.묶음 == 할일묶음.지켜볼것;
            var 높이 = 지켜볼 ? 36f : 54f;
            var row = new Rect(x, y, width, 높이);
            var 고름 = item.명령키 != null && item.명령키 == 할일강조;
            var 미룸 = item.명령키 != null && 이번턴미룸.Contains(item.명령키);
            Paint(row, 고름 ? Gold : Rim, 8);
            Paint(new Rect(row.x + 1, row.y + 1, row.width - 2, row.height - 2), 고름 ? Ink(0x2C281E) : Panel, 7);
            var 색 = 미룸 ? Rim : item.묶음 == 할일묶음.위험 ? SignalRed : item.묶음 == 할일묶음.고를일 ? Gold : Sky;
            Paint(new Rect(row.x + 1, row.y + 4, 3, row.height - 8), 색, 1);
            var 번호 = 할일목록.IndexOf(item) + 1;
            var 제목 = 번호 + "  " + (string.IsNullOrEmpty(item.제목) ? "—" : item.제목) + (item.지난턴미룸 ? " · 지난 턴 미룸" : string.Empty);
            Text(new Rect(row.x + 10, row.y + 2, row.width - 20, 18), 줄임(제목, 30), 12, 미룸 ? Soft : Paper, true);
            Text(new Rect(row.x + 10, row.y + 19, row.width - 20, 14), 줄임(item.문구, 40), 9, Soft);
            if (지켜볼)
            {
                var 보기 = new Rect(row.xMax - 60, row.y + 8, 52, 20);
                if (할일신호.ContainsKey(item.키) && ExpansionButton(보기, "가서 보기", true, Tile)) 할일가서보기(item);
                return y + 높이 + 4;
            }
            Text(new Rect(row.x + 10, row.y + 35, row.width - 150, 16), 줄임("고를 것: " + item.고를것 + " · " + item.창이름, 26), 9,
                미룸 ? Soft : Gold, true);
            var 오른쪽 = row.xMax - 8;
            if (autopilotOn)
            {
                var 가기 = new Rect(오른쪽 - 76, row.y + 33, 76, 18);
                if (ExpansionButton(가기, "고르러 가기", true, Tile)) 할일열기(item);
                return y + 높이 + 4;
            }
            if (미룸)
            {
                Text(new Rect(오른쪽 - 150, row.y + 34, 80, 16), "이번 턴은 미룸", 9, Lilac, true, TextAnchor.MiddleRight);
                var 되돌림 = new Rect(오른쪽 - 64, row.y + 33, 64, 18);
                if (ExpansionButton(되돌림, "다시 고르기", true, Tile)) 이번턴미룸.Remove(item.명령키);
                return y + 높이 + 4;
            }
            if (고름)
            {
                Text(new Rect(오른쪽 - 140, row.y + 34, 140, 16), "← 왼쪽에서 고르는 중", 9, Gold, true, TextAnchor.MiddleRight);
                return y + 높이 + 4;
            }
            var 고르기 = new Rect(오른쪽 - 72, row.y + 33, 72, 18);
            var 미루기 = new Rect(고르기.x - 44, row.y + 33, 40, 18);
            if (ExpansionButton(고르기, "고르러 가기", true, Mint)) 할일열기(item);
            else if (ExpansionButton(미루기, "미룸", true, Tile))
            {
                이번턴미룸.Add(item.명령키);
                if (할일강조 == item.명령키) 할일강조 = null;
            }
            return y + 높이 + 4;
        }

        private float 할일계획칸(float x, float y, float width, float bottom)
        {
            Paint(new Rect(x + 2, y + 6, 8, 8), Mint, 4);
            Text(new Rect(x + 16, y, width - 16, 18), "이번 턴 계획 · " + 턴계획.Count, 11, Mint, true);
            Text(new Rect(x + 16, y, width - 16, 18), "넘기기 전까지 바꿀 수 있음", 9, Soft, align: TextAnchor.MiddleRight);
            y += 20;
            foreach (var 항목 in 턴계획.항목.ToList())
            {
                if (y + 44 > bottom) break;
                var row = new Rect(x, y, width, 40);
                Paint(row, Rim, 8);
                Paint(new Rect(row.x + 1, row.y + 1, row.width - 2, row.height - 2), Panel, 7);
                Paint(new Rect(row.x + 1, row.y + 4, 3, row.height - 8), Mint, 1);
                Text(new Rect(row.x + 10, row.y + 2, row.width - 110, 18), 줄임(항목.제목, 28), 11, Paper, true);
                Text(new Rect(row.x + 10, row.y + 20, row.width - 110, 16), 줄임(항목.예상, 30), 9, Gold);
                var 빼기 = new Rect(row.xMax - 44, row.y + 10, 38, 19);
                var 바꾸기 = new Rect(빼기.x - 50, row.y + 10, 46, 19);
                if (ExpansionButton(바꾸기, "바꾸기", true, Tile)) 계획다시열기(항목);
                else if (ExpansionButton(빼기, "빼기", true, Tile))
                {
                    턴계획.빼기(항목.명령키);
                    할일알림 = "계획에서 뺐습니다 · " + 항목.제목;
                    할일알림때 = Time.unscaledTime;
                }
                y += 44;
            }
            return y + 4;
        }

        private float 할일결과칸(float x, float y, float width, float bottom)
        {
            Paint(new Rect(x + 2, y + 6, 8, 8), Lilac, 4);
            Text(new Rect(x + 16, y, width - 16, 18), "지난 턴에 고른 것 · " + 지난턴결과.Count(r => !r.미룸), 11, Lilac, true);
            var 접기 = new Rect(x + width - 40, y, 40, 18);
            Text(접기, 할일결과접힘 ? "펼치기" : "접기", 9, Soft, align: TextAnchor.MiddleRight);
            if (Hit(접기)) 할일결과접힘 = !할일결과접힘;
            y += 20;
            if (할일결과접힘) return y + 2;
            foreach (var 결과 in 지난턴결과)
            {
                var 읽음 = 결과.마지막 ?? 결과읽기(결과);
                var 칸들 = 결과.미룸 || 결과.실패 != null ? null : 결과.종류 == 계획종류.발주 ? 발주칸 : 결과.종류 == 계획종류.배차 ? 배달칸 : null;
                var 높이 = 칸들 == null ? 40f : 58f;
                if (y + 높이 > bottom) break;
                var row = new Rect(x, y, width, 높이);
                var 실패 = 읽음.상태 == 결과상태.실패;
                Paint(row, 실패 ? Ink(0x6E3C3C) : Rim, 8);
                Paint(new Rect(row.x + 1, row.y + 1, row.width - 2, row.height - 2), 실패 ? Ink(0x2C1C1E) : Panel, 7);
                var 색 = 읽음.상태 switch
                {
                    결과상태.완료 => Mint,
                    결과상태.진행중 => Mint,
                    결과상태.대기 => Gold,
                    결과상태.미룸 => Lilac,
                    _ => SignalRed,
                };
                Text(new Rect(row.x + 10, row.y + 2, row.width - 90, 18), 줄임((결과.미룸 ? "미룸 · " : string.Empty) + 결과.제목, 28), 11,
                    결과.미룸 ? Lilac : Paper, true);
                if (칸들 != null && 할일따라갈수있나(결과))
                {
                    var 따라 = new Rect(row.xMax - 72, row.y + 3, 64, 18);
                    if (ExpansionButton(따라, "따라가기", true, Tile)) 할일따라가기(결과);
                }
                if (칸들 != null)
                {
                    var sx = row.x + 20;
                    var ex = row.xMax - 30;
                    var sy = row.y + 27;
                    Paint(new Rect(sx, sy - 1, ex - sx, 2), Rim);
                    var 끝 = 읽음.상태 == 결과상태.완료 ? 칸들.Length - 1 : 읽음.칸;
                    Paint(new Rect(sx, sy - 1, (ex - sx) * 끝 / (칸들.Length - 1f), 2), Mint);
                    for (var i = 0; i < 칸들.Length; i++)
                    {
                        var px = sx + (ex - sx) * i / (칸들.Length - 1f);
                        var 지남 = i < 끝 || 읽음.상태 == 결과상태.완료;
                        Paint(new Rect(px - 5, sy - 5, 10, 10), i <= 끝 ? Mint : Rim, 5);
                        if (!지남 && i == 끝) Paint(new Rect(px - 3, sy - 3, 6, 6), Canvas, 3);
                        Text(new Rect(px - 30, sy + 4, 60, 13), 칸들[i], 8, i <= 끝 ? Paper : Soft, i == 끝, TextAnchor.MiddleCenter);
                    }
                }
                var 상태말 = 읽음.상태 switch
                {
                    결과상태.완료 => "완료 · ",
                    결과상태.진행중 => "진행 중 · ",
                    결과상태.대기 => "대기 · ",
                    결과상태.미룸 => string.Empty,
                    _ => "실패 · ",
                };
                Text(new Rect(row.x + 10, row.yMax - 18, row.width - 20, 16), 줄임(상태말 + 읽음.문장, 40), 9, 색, true);
                y += 높이 + 4;
            }
            return y + 2;
        }

        private static string 줄임(string text, int max) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text.Substring(0, Math.Max(1, max - 1)) + "…";

        // 지도 위 번호(표현): 신호의 자리에 안내판의 번호를 붙인다. 자리를 모르는 신호(동 깃발)는 그리지 않는다.
        private void 할일번호그리기()
        {
            if (mapLevel != MapLevel.Dong) return;
            for (var i = 0; i < 할일목록.Count; i++)
            {
                var item = 할일목록[i];
                if (item.묶음 == 할일묶음.지켜볼것 || !할일신호.TryGetValue(item.키, out var signal) || !TrySignalPoint(signal, out var point)) continue;
                var at = new Vector2(Field.x + point.x, Field.y + point.y);
                if (!new Rect(Field.x + 20, Field.y + 90, Field.width - 40, Field.height - 200).Contains(at) || 할일판칸.Contains(at)) continue;
                var 색 = item.묶음 == 할일묶음.위험 ? SignalRed : Gold;
                Hoop(at, 15f, 색, 2f);
                var tag = new Rect(at.x + 14, at.y - 26, 24, 18);
                Paint(tag, 색, 9);
                Text(tag, (i + 1).ToString(), 11, Canvas, true, TextAnchor.MiddleCenter);
            }
        }

        // ── 열기 ──
        private void 할일열기(다음할일항목 item)
        {
            if (item == null) return;
            if (item.명령키 == null)
            {
                할일가서보기(item);
                return;
            }
            if (autopilotOn)
            {
                할일전환대기 = item.명령키;
                직접업무가능();                 // 「화 · 집중으로 바꿀까요?」 (SagajeongDispatchAutopilot.cs)
                return;
            }
            할일강조 = item.명령키;
            할일열린키 = item.명령키;
            이번턴미룸.Remove(item.명령키);
            selectedBuilding = null;
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
            switch (item.창)
            {
                case 할일창.매장재료:
                    할일매장열기();
                    break;
                case 할일창.배차데스크:
                    storeDeskPlace = null;
                    dongDeskOpen = false;
                    showDesk = true;              // 배차 데스크: 기사를 고르고 주문을 누르면 계획에 담긴다
                    selectedDriver = null;
                    break;
                case 할일창.동관리:
                    showDesk = false;
                    var code = item.장소 != null && item.장소.StartsWith("dong:", StringComparison.Ordinal) ? item.장소.Substring(5) : null;
                    if (code == null) break;
                    if (mapLevel != MapLevel.Dong) ReturnToDong();
                    FocusDongFlag(code);
                    OpenDongDesk(HjdRegionPrefix + code, item.명령키.StartsWith("contract:", StringComparison.Ordinal) ? DeskFold.Build : DeskFold.Hire);
                    break;
                default:
                    할일가서보기(item);
                    break;
            }
            할일판열림 = true;
        }

        private void 할일매장열기()
        {
            showDesk = false;
            var place = 할일매장자리;
            if (place == null) return;
            LoadRoleBook();
            if (mapLevel != MapLevel.Dong) ReturnToDong();
            ShowDongOfPlace(place);
            if (StoreRole(place) == null || !StoreDeskReady) return;
            OpenStoreDesk(place);
            StoreTab(StoreTabMaterials);
        }

        private void 할일가서보기(다음할일항목 item)
        {
            if (!할일신호.TryGetValue(item.키, out var signal)) return;
            showDesk = false;
            GoToSignal(signal);
        }

        // 계획의 「바꾸기」: 그 일의 창을 다시 연다.
        private void 계획다시열기(턴계획항목 항목)
        {
            var item = 할일목록.FirstOrDefault(i => i.명령키 == 항목.명령키);
            if (item != null)
            {
                할일열기(item);
                return;
            }
            할일강조 = 항목.명령키;
            switch (항목.종류)
            {
                case 계획종류.발주:
                case 계획종류.검수:
                case 계획종류.제조:
                    할일매장열기();
                    break;
                case 계획종류.배차:
                    storeDeskPlace = null;
                    dongDeskOpen = false;
                    showDesk = true;
                    break;
                default:
                    showDesk = false;
                    OpenDongDesk(HjdRegionPrefix + 항목.대상, 항목.종류 == 계획종류.제휴 ? DeskFold.Build : DeskFold.Hire);
                    break;
            }
        }

        // 중에서 「고르러 가기」 → 화 전환을 고르면 그 일을 연다. 중 유지면 잊는다.
        private void 할일전환이어가기()
        {
            if (할일전환대기 == null || 집중전환대기) return;
            var 키 = 할일전환대기;
            할일전환대기 = null;
            if (autopilotOn) return;
            var item = 할일목록.FirstOrDefault(i => i.명령키 == 키);
            if (item != null) 할일열기(item);
        }

        // 왼쪽 창의 강조: 지금 고르는 일이면 금색 테두리와 꼬리표. 창의 좌표계에서 부른다.
        private void 할일강조그리기(Rect box, string 명령키)
        {
            if (!턴계획모드 || 명령키 == null) return;
            var 담김 = 턴계획.찾기(명령키) != null;
            if (할일강조 != 명령키 && !담김) return;
            var 색 = 할일강조 == 명령키 ? Gold : Mint;
            Paint(new Rect(box.x - 2, box.y - 2, box.width + 4, 2), 색);
            Paint(new Rect(box.x - 2, box.yMax, box.width + 4, 2), 색);
            Paint(new Rect(box.x - 2, box.y, 2, box.height), 색);
            Paint(new Rect(box.xMax, box.y, 2, box.height), 색);
            var tag = new Rect(box.xMax - 112, box.y - 9, 110, 16);
            Paint(tag, 색, 8);
            Text(tag, 담김 ? "이번 턴 계획에 담김" : "여기서 고르기", 9, Canvas, true, TextAnchor.MiddleCenter);
        }

        private string 확정말(string 원래, string 명령키) =>
            !턴계획모드 ? 원래 : 턴계획.찾기(명령키) == null ? "이번 턴 계획에 담기" : "계획 바꾸기 (담긴 것을 이것으로)";

        // 동 관리 창의 작은 단추 말(세 글자 이하면 좁은 단추).
        private string 동확정말(string 원래, string 명령키) => !턴계획모드 ? 원래 : 턴계획.찾기(명령키) == null ? "담기" : "바꿈";

        // ── 담기 (화에서 창의 확정 단추가 부른다. 커널에는 아무것도 보내지 않는다) ──
        private void 할일담기(턴계획항목 항목)
        {
            var 새로 = 턴계획.담기(항목);
            이번턴미룸.Remove(항목.명령키);
            미룬할일.Remove(항목.명령키);
            할일판열림 = true;
            할일알림 = (새로 ? "이번 턴 계획에 담음 · " : "계획을 바꿈 · ") + 항목.제목;
            할일알림때 = Time.unscaledTime;
            var 다음 = 다음할일Policy.고르지않은(할일목록, 턴계획).FirstOrDefault(item => item.명령키 != 항목.명령키 && !이번턴미룸.Contains(item.명령키));
            할일강조 = 다음?.명령키;              // 다음 일은 안내판에서 금색으로. 창은 Enter · 「고르러 가기」로 연다
            Debug.Log("Hongdal next todo: plan " + (새로 ? "add " : "replace ") + 항목.명령키 + " · " + 항목.제목);
        }

        private void 매장계획담기(계획종류 종류)
        {
            var place = storeDeskPlace ?? 할일매장자리;
            if (place == null) return;
            var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            var supply = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply);
            var 이름 = StoreRole(place)?.title ?? store?.StoreName ?? "내 매장";
            var 항목 = new 턴계획항목 { 종류 = 종류, 대상 = place };
            switch (종류)
            {
                case 계획종류.발주:
                {
                    항목.명령키 = 다음할일Policy.발주키(place);
                    항목.수량 = orderQuantity;
                    항목.제목 = 이름 + " · " + supply?.ItemName + " " + orderQuantity + supply?.Unit + " 본부 발주";
                    var preview = engine.BusinessPreview(new GameCommand { OsId = BusinessIds.FranchiseSupply, Action = "order" }
                        .With("quantity", orderQuantity));
                    항목.예상 = "예상 · " + preview.Message + " · 지급 전";
                    break;
                }
                case 계획종류.검수:
                {
                    var receipt = store?.Receipts.LastOrDefault(item => item.Stage == ReceiptStages.Waiting);
                    if (receipt == null) return;
                    항목.명령키 = 다음할일Policy.검수키(place);
                    항목.입고Id = receipt.Id;
                    항목.인수 = inspectAccept;
                    항목.격리 = inspectQuarantine;
                    항목.거절 = inspectReject;
                    항목.제목 = 이름 + " · 검수 인수 " + inspectAccept + " · 격리 " + inspectQuarantine + " · 거절 " + inspectReject;
                    항목.예상 = "비용 없음 · 인수한 만큼 재고에";
                    break;
                }
                default:
                {
                    항목.명령키 = 다음할일Policy.제조키(place);
                    항목.수량 = 1;
                    항목.제목 = 이름 + " · " + store?.Menu + " 1잔 제조";
                    var preview = engine.BusinessPreview(new GameCommand { OsId = BusinessIds.StoreOperations, Action = "produce" }.With("cups", 1m));
                    항목.예상 = "예상 · " + preview.Message;
                    break;
                }
            }
            할일담기(항목);
        }

        private void 동계획담기(string region, bool 제휴)
        {
            var land = DongLand(region);
            if (land == null) return;
            var 이름 = DongModuleById(region)?.Entry.name ?? land.Code;
            var 비용 = dongDemand == null ? "자료 미확인" : (제휴 ? dongDemand.ContractCost : dongDemand.RecruitCost).ToString("N0") + "원";
            할일담기(new 턴계획항목
            {
                종류 = 제휴 ? 계획종류.제휴 : 계획종류.모집,
                명령키 = 제휴 ? 다음할일Policy.제휴키(land.Code) : 다음할일Policy.모집키(land.Code),
                대상 = land.Code,
                제목 = 이름 + (제휴 ? " · 제휴 음식점 한 곳" : " · 기사 한 명 모집"),
                예상 = "예상 " + 비용 + " · 동 자금에서 · 넘길 때 다시 확인",
            });
        }

        // 배차 데스크에서 기사를 고르고 주문을 누르면(화) 보내지 않고 계획에 담는다.
        private void 배차계획담기(DispatchOrder order, string driverId)
        {
            var 키 = 다음할일Policy.배차키(order.id);
            if (턴계획.기사겹침(driverId, 키))
            {
                notice = "기사 " + Letter(driverId) + "는 이번 턴 계획의 다른 배차에 이미 있습니다.";
                return;
            }
            할일담기(new 턴계획항목
            {
                종류 = 계획종류.배차,
                명령키 = 키,
                대상 = order.id,
                기사 = driverId,
                판본 = order.revision,
                제목 = (order.storeName ?? "매장") + " · " + PhoneOrderNumber(order.id) + " → 기사 " + Letter(driverId),
                예상 = (driverId == order.recommendedDriverId ? "추천대로" : "추천과 다르게") + " · 비용 없음 · 넘길 때 배차",
            });
            notice = "이번 턴 계획에 담았습니다. 턴을 넘기면 배차합니다.";
            selectedDriver = null;
            actorFocus = null;                   // 오른쪽 칸을 비워 안내판(계획)이 보이게
        }

        // 배차 데스크의 주문 줄 위 표시. (DrawOrder가 부른다)
        private void 할일주문표시(DispatchOrder order, Rect rect)
        {
            if (!턴계획모드 || order == null) return;
            var 키 = 다음할일Policy.배차키(order.id);
            var 담긴 = 턴계획.찾기(키);
            if (할일강조 != 키 && 담긴 == null) return;
            할일강조그리기(rect, 키);
            if (담긴 != null)
                Text(new Rect(rect.x + 11, rect.yMax - 18, rect.width - 22, 16), "계획: 기사 " + Letter(담긴.기사) + " · 넘길 때 배차", 10, Mint, true);
        }

        // ── 턴 넘기기 ──
        private void 턴넘김요청()
        {
            if (턴넘기는중 || autopilotOn) return;
            if (남은할일.Count > 0)
            {
                턴넘김확인 = true;
                return;
            }
            StartCoroutine(턴넘기기());
        }

        private bool 턴넘김입력처리()
        {
            var 입력 = Event.current;
            if (!턴넘김확인 || 입력.type is EventType.Repaint or EventType.Layout) return false;
            if (입력.type == EventType.KeyDown && 입력.keyCode == KeyCode.Escape) 턴넘김확인 = false;
            else 턴넘김창그리기();
            if (입력.type != EventType.Used) 입력.Use();
            return true;
        }

        private void 턴넘김창그리기()
        {
            if (!턴넘김확인) return;
            var 남은 = 남은할일;
            var box = 턴확인칸;
            Paint(new Rect(0, 0, Width, Height), Fade(Canvas, 0.5f));
            Paint(box, Gold, 10);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 9);
            Text(new Rect(box.x + 20, box.y + 14, 360, 28), "고르지 않은 " + 남은.Count + "건을 미루고 넘길까요?", 18, Paper, true);
            var 목록 = string.Join(" · ", 남은.Take(3).Select(item => item.제목 + " " + item.고를것)) + (남은.Count > 3 ? " 외 " + (남은.Count - 3) + "건" : string.Empty);
            Text(new Rect(box.x + 20, box.y + 50, 360, 40), 목록, 12, Soft, wrap: true);
            Text(new Rect(box.x + 20, box.y + 92, 360, 40), "미룬 일은 해결되지 않습니다. 지연 · 부족은 원래 규칙대로 남고, 다음에 화로 오면 다시 안내합니다.", 11, Soft, wrap: true);
            if (ExpansionButton(new Rect(box.x + 20, box.yMax - 50, 160, 32), "돌아가서 고르기", true, Tile))
                턴넘김확인 = false;
            if (ExpansionButton(new Rect(box.x + 192, box.yMax - 50, 188, 32), "이번 턴은 미루고 넘기기", true, Gold))
            {
                턴넘김확인 = false;
                StartCoroutine(턴넘기기());
            }
        }

        private bool 할일Esc닫기()
        {
            if (턴넘김확인)
            {
                턴넘김확인 = false;
                return true;
            }
            if (!할일판보임) return false;
            할일판열림 = false;
            할일강조 = null;
            할일열린키 = null;
            return true;
        }

        private IEnumerator 턴넘기기()
        {
            if (턴넘기는중) yield break;
            턴넘기는중 = true;
            턴넘김확인 = false;
            var 결과 = new List<턴결과>();
            var 남은 = 다음할일Policy.고르지않은(할일목록, 턴계획);
            var 순서 = 턴계획.실행순서(키 => 할일목록.FirstOrDefault(item => item.명령키 == 키)?.묶음);
            Debug.Log("Hongdal next todo: turn " + 할일턴 + " · plan " + 순서.Count + " · deferred " + 남은.Count);
            foreach (var 항목 in 순서)
            {
                var 하나 = new 턴결과 { 명령키 = 항목.명령키, 종류 = 항목.종류, 제목 = 항목.제목 };
                if (항목.종류 == 계획종류.배차) yield return 배차보내기(항목, 하나);
                else 업무보내기(항목, 하나);
                Debug.Log("Hongdal next todo: " + 항목.명령키 + " → " + (하나.실패 == null ? "sent" : "blocked · " + 하나.실패));
                결과.Add(하나);
            }
            미룬할일.Clear();
            foreach (var 일 in 남은)
            {
                미룬할일.Add(일.명령키);
                결과.Add(new 턴결과 { 명령키 = 일.명령키, 제목 = 일.제목 + " · " + 일.고를것, 미룸 = true });
            }
            지난턴결과.Clear();
            지난턴결과.AddRange(결과);
            var 보냄 = 결과.Count(r => !r.미룸 && r.실패 == null);
            할일알림 = 할일턴 + "턴을 넘겼습니다 · 보냄 " + 보냄 + " · 실패 " + 결과.Count(r => r.실패 != null) + " · 미룸 " + 남은.Count;
            할일알림때 = Time.unscaledTime;
            foreach (var 하나 in 지난턴결과) 하나.마지막 = 결과읽기(하나);
            턴계획.비우기();
            이번턴미룸.Clear();
            할일강조 = null;
            할일열린키 = null;
            할일턴++;
            할일결과접힘 = false;
            할일판열림 = true;
            selectedDriver = null;
            SetAutopilot(true);                  // 중으로 자동 귀환 (r247)
            턴넘기는중 = false;
        }

        private void 업무보내기(턴계획항목 항목, 턴결과 하나)
        {
            string 업무 = null, 행동 = null;
            Action<GameCommand> 채우기 = null;
            switch (항목.종류)
            {
                case 계획종류.발주:
                    업무 = BusinessIds.FranchiseSupply;
                    행동 = "order";
                    채우기 = command => command.With("quantity", 항목.수량);
                    break;
                case 계획종류.검수:
                {
                    var 대기 = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations)?.Receipts
                        .LastOrDefault(item => item.Stage == ReceiptStages.Waiting);
                    if (대기 == null || 대기.Id != 항목.입고Id)
                    {
                        하나.실패 = 대기 == null ? "검수를 기다리는 화물이 없습니다" : "검수할 화물이 바뀌었습니다 · 다시 고르기(화)";
                        return;
                    }
                    업무 = BusinessIds.StoreOperations;
                    행동 = "inspect";
                    채우기 = command => command.With("accept", 항목.인수).With("quarantine", 항목.격리).With("reject", 항목.거절);
                    break;
                }
                case 계획종류.제조:
                    업무 = BusinessIds.StoreOperations;
                    행동 = "produce";
                    채우기 = command => command.With("cups", 1m);
                    break;
                case 계획종류.모집:
                    업무 = BusinessIds.DongDemand;
                    행동 = DongDemandActions.Recruit;
                    채우기 = command => command.With("dong", 항목.대상);
                    break;
                case 계획종류.제휴:
                    업무 = BusinessIds.DongDemand;
                    행동 = DongDemandActions.Contract;
                    채우기 = command => command.With("dong", 항목.대상);
                    break;
            }
            if (업무 == null || engine?.Kernel == null)
            {
                하나.실패 = "보낼 업무를 찾지 못했습니다";
                return;
            }
            var 미리 = new GameCommand { OsId = 업무, Action = 행동 };
            채우기(미리);
            var preview = engine.BusinessPreview(미리);
            if (!preview.CanConfirm)
            {
                하나.실패 = string.IsNullOrEmpty(preview.Message) ? "지금은 할 수 없습니다" : preview.Message;
                return;
            }
            var 발주전 = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders.Count ?? 0;
            var result = SendBusiness(업무, 행동, 채우기);
            if (업무 == BusinessIds.DongDemand)
            {
                territory = ReadTerritory();
                signals = engine.Kernel.Signals.OrderByDescending(item => item.IsDanger).ThenBy(item => item.SinceMs).ToList();
            }
            if (!result.Ok)
            {
                하나.실패 = string.IsNullOrEmpty(result.Message) ? "받아들여지지 않았습니다" : result.Message;
                return;
            }
            if (항목.종류 == 계획종류.발주)
            {
                var 발주 = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders;
                if (발주 != null && 발주.Count > 발주전) 하나.공급주문Id = 발주[발주.Count - 1].OrderId;
                else 하나.실패 = "발주가 기록되지 않았습니다";
            }
            else 하나.완료문장 = result.Message;
        }

        private IEnumerator 배차보내기(턴계획항목 항목, 턴결과 하나)
        {
            하나.주문Id = 항목.대상;
            하나.기사 = 항목.기사;
            var deadline = Time.realtimeSinceStartupAsDouble + 6;
            while ((pending != null || queuedAssignment != null) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            var order = state?.orders?.FirstOrDefault(item => item.id == 항목.대상);
            if (!AssignmentOpen) 하나.실패 = "지금은 배차할 수 없는 때입니다(영업 마감 · 방침)";
            else if (order == null) 하나.실패 = "주문 기록을 찾을 수 없습니다";
            else if (order.stage != "Waiting") 하나.실패 = "이미 " + StageLabel(order.stage) + " · 다시 고를 일 없음";
            else if (!CanSelect(항목.기사)) 하나.실패 = "기사 " + Letter(항목.기사) + "가 다른 일을 맡았습니다";
            else if (MindBlocksAssignment(항목.기사, out var mind)) 하나.실패 = mind;
            else if (pending != null) 하나.실패 = "다른 요청을 처리하는 중이라 보내지 못했습니다";
            if (하나.실패 != null) yield break;
            notice = null;
            Send(new DispatchRequest
            {
                action = "assign",
                orderId = order.id,
                driverId = 항목.기사,
                revision = order.revision,
                commandId = "unity-" + Guid.NewGuid().ToString("N"),
                elapsedMs = Elapsed(Time.realtimeSinceStartupAsDouble),
            });
            while (pending != null && Time.realtimeSinceStartupAsDouble < deadline + 4) yield return null;
            var after = state?.orders?.FirstOrDefault(item => item.id == 항목.대상);
            if (after == null || after.driverId != 항목.기사)
                하나.실패 = string.IsNullOrEmpty(notice) ? "배차가 받아들여지지 않았습니다" : notice;
        }

        // ── 결과 읽기 ──
        private (int 칸, 결과상태 상태, string 문장) 결과읽기(턴결과 하나)
        {
            if (하나.미룸) return (0, 결과상태.미룸, "아직 그대로 · 다음에 화로 오면 다시 안내");
            if (하나.실패 != null) return (0, 결과상태.실패, 하나.실패);
            switch (하나.종류)
            {
                case 계획종류.발주:
                {
                    var center = ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter);
                    var cargo = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport);
                    var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
                    var outbound = center?.Outbounds.LastOrDefault(item => item.OrderId == 하나.공급주문Id);
                    var trip = cargo?.Trips.LastOrDefault(item => item.OrderId == 하나.공급주문Id);
                    var receipt = store?.Receipts.LastOrDefault(item => item.OrderId == 하나.공급주문Id);
                    return 다음할일Policy.발주진행(outbound != null, outbound?.Stage == OutboundStages.Loaded, trip != null,
                        trip != null && (trip.Stage == cargo.ArrivedStage || trip.Stage == cargo.ReceivedStage),
                        receipt?.Stage == ReceiptStages.Waiting, receipt?.Stage == ReceiptStages.Inspected);
                }
                case 계획종류.배차:
                {
                    var order = state?.orders?.FirstOrDefault(item => item.id == 하나.주문Id);
                    if (order == null && 하나.마지막 is { } 전 && 전.상태 != 결과상태.실패)
                        return (전.칸, 결과상태.완료, "지난 날로 넘어갔습니다 · " + 전.문장);
                    return 다음할일Policy.배달진행(order?.stage, order?.driverId, 하나.기사);
                }
            }
            return (0, 결과상태.완료, string.IsNullOrEmpty(하나.완료문장) ? "끝났습니다" : 하나.완료문장);
        }

        private bool 할일따라갈수있나(턴결과 하나) =>
            하나.실패 == null && !하나.미룸 && (하나.종류 == 계획종류.배차 ? !string.IsNullOrEmpty(하나.기사) :
                하나.종류 == 계획종류.발주 && !string.IsNullOrEmpty(하나.공급주문Id));

        // 「따라가기」: 관찰(중)의 역할 보기와 추적을 그대로 쓴다 (SagajeongDispatchObservation.cs · ObservationFollow.cs).
        private void 할일따라가기(턴결과 하나)
        {
            if (!autopilotOn) return;
            할일판열림 = false;
            if (하나.종류 == 계획종류.배차)
            {
                역할휴대폰열기(new 역할관찰행(FocusKind.Courier, 하나.기사, "음식 배달기사", "기사 " + Letter(하나.기사), string.Empty));
                관찰주문선택(하나.주문Id);
                관찰다시따라가기();
                return;
            }
            var trip = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport)?.Trips.LastOrDefault(item => item.OrderId == 하나.공급주문Id);
            if (trip != null)
            {
                물류관찰시작(new 역할관찰행(FocusKind.Cargo, "cargo", "화물 기사", trip.VehicleName, string.Empty));
                물류관찰건선택(FocusKind.Cargo, trip.Id);
                관찰다시따라가기();
                return;
            }
            var outbound = ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds.LastOrDefault(item => item.OrderId == 하나.공급주문Id);
            if (outbound == null) return;
            물류관찰시작(new 역할관찰행(FocusKind.Center, "center", "물류센터 출고", "물류센터", string.Empty));
            물류관찰건선택(FocusKind.Center, outbound.Id);
            관찰위치보기();
        }

        // ── 시계: 화에서 안내판 · 계획이 열린 동안 멈춘다 (설정 화면과 같은 방식) ──
        private void 턴시계맞추기()
        {
            var 멈춤 = 할일보임 && 다음할일Policy.시계멈춤(!autopilotOn, 할일판열림, 턴계획.Count);
            if (멈춤)
            {
                if (턴시계보류 != 0 || !campaignMode || !running || error != null || CampaignPolicyOpen || campaignJumpPending ||
                    campaignSpeed == 0) return;
                var speed = campaignSpeed;
                SetCampaignSpeed(0);
                if (campaignSpeed == 0) 턴시계보류 = speed;
                return;
            }
            if (턴시계보류 == 0) return;
            if (!running || campaignSpeed != 0)
            {
                턴시계보류 = 0;                   // 새 회차를 열었거나 사용자가 배속을 눌렀다
                return;
            }
            SetCampaignSpeed(턴시계보류);
            if (campaignSpeed != 0) 턴시계보류 = 0;
        }
    }
}
