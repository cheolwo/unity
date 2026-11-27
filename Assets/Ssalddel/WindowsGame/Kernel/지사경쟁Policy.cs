using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-27 지사장 판 J1 — 동마다 운영자별 점유율(순수 규칙 · 화면 없음 · 아직 커널 모듈에 꽂지 않음).
    // 사용자: 「악의 세력이라고 하기보다는 … 점유를 하려는 그런 비중을 높이는 쪽으로」(10:44) ·
    //         「깨어있는 사람이 몇 명이냐 이런 거는 … 철학자의 눈으로」(10:59) — 이 식에 깨어 있음 · 명덕은 없다.
    // 개발 계획: docs/AI/Handoffs/2026-11-27-Claude-개발계획-지사장판-점유율과입장더하기.md 2-2절
    // 계약(J0):  docs/AI/Handoffs/2026-11-27-Claude-계약-지사장판-J0.md
    //  - 동(행정동) 하나의 회차 주문을 그 동에 지사를 낸 운영자들과 「플랫폼 밖」(전화 · 포장)이 나눠 갖는다. 합은 늘 100%.
    //  - 매력 = 입점 · 속도 · 배달비 · 행사 · 신뢰(가중치 합 1). 새 몫 = 매력 / 매력 합. 실제 몫 = 앞 회차 × 관성 + 새 몫 × (1 − 관성).
    //  - 지사마다 회차 운영비가 나간다. 다 내지 못하면 자본은 0에서 멎고 신뢰가 깎인다(자본은 음수가 되지 않는다).
    //  - 받은 주문 = min(몫 × 회차 주문, 처리력). 처리력 = min(기사, 입점 음식점)의 처리량. 못 받은 주문은 신뢰를 깎는다.
    //  - 나와 AI는 같은 함수(수두기)로 같은 값을 낸다. 씨앗이 같으면 결과가 같다.
    // 값은 모두 가안이다. 기획이 정하면 Rules만 바꾼다. 돈은 게임 안 돈이다.

    public enum 경쟁전략 { 사람, 할인형, 속도형, 입점형, 동네밀착형 }

    public enum 경쟁수종류 { 쉼, 기사모집, 음식점영업, 행사, 새동진출 }

    public sealed class 지사경쟁Rules
    {
        // 매력 가중치(합 1)
        public double 입점가중 = 0.30;
        public double 속도가중 = 0.30;
        public double 배달비가중 = 0.15;
        public double 행사가중 = 0.15;
        public double 신뢰가중 = 0.10;
        /// <summary>앞 회차 몫을 남기는 비율. 한 회차에 몫이 (1 − 관성)보다 크게 움직이지 않는다.</summary>
        public double 관성 = 0.70;
        /// <summary>플랫폼을 쓰지 않는 주문(전화 · 포장)의 매력. 운영자가 혼자여도 100%가 되지 않는다.</summary>
        public double 플랫폼밖매력 = 0.25;

        public double 기사회차처리 = 14;
        public double 음식점회차처리 = 8;
        public int 동기사한도 = 24;
        /// <summary>입점 음식점이 그 동 음식점의 이 비율이면 입점 점수 1.</summary>
        public double 입점포화 = 0.10;
        public int 영업한번입점 = 4;

        public long 기사모집비 = 20000;
        public long 영업비 = 12000;
        public long 진출비 = 60000;
        /// <summary>지사 한 곳을 한 회차 여는 데 드는 돈(자리 · 관리). 지사를 마구 늘리지 못하게 한다.</summary>
        public long 지사회차운영비 = 15000;
        /// <summary>운영비를 다 내지 못한 회차에 모든 지사의 신뢰가 깎이는 폭.</summary>
        public double 운영비밀림신뢰 = 0.15;
        public long 행사단위 = 10000;
        /// <summary>행사 점수 = 1 − exp(−남은 행사 예산 / (회차 주문 × 행사건당)).</summary>
        public double 행사건당 = 90;
        /// <summary>회차가 지나면 남는 행사 예산의 비율.</summary>
        public double 행사남음 = 0.5;

        public long 건당지사몫 = 400;
        public double 처음신뢰 = 0.5;
        public double 신뢰회복 = 0.10;
        public double 신뢰깎임 = 0.60;
        /// <summary>기사 한 명의 회차 주문이 이보다 적으면 한 명이 떠난다(일감 부족).</summary>
        public double 기사일감하한 = 4;
        public int 진출기사 = 2;
        public int 진출입점 = 8;
        /// <summary>지사 한 곳이 회차마다 내는 연구 점수. 깨어 있는 기사 수는 넣지 않는다(11-27 정한 것).</summary>
        public int 지사연구점수 = 2;

        public int AI최대수 = 3;
        public long AI남길돈 = 10000;
        /// <summary>AI가 수의 값을 셀 때 내다보는 회차 수.</summary>
        public double AI내다봄 = 10;

        public double 가중합 => 입점가중 + 속도가중 + 배달비가중 + 행사가중 + 신뢰가중;
    }

    public sealed class 경쟁운영자
    {
        public string Id = string.Empty;
        public string 이름 = string.Empty;
        public 경쟁전략 전략 = 경쟁전략.사람;
        public long 자본;
        /// <summary>주문자 배달비 점수 0~1(쌀수록 1). 본사가 정한다. 지사장은 못 바꾼다.</summary>
        public double 배달비점수 = 0.5;
        public long 연구점수;
        public bool 나 => 전략 == 경쟁전략.사람;
    }

    public sealed class 경쟁동
    {
        public string Code = string.Empty;
        public string 이름 = string.Empty;
        public double 회차주문;
        public int 음식점수;
        public readonly List<string> 이웃 = new List<string>();
        /// <summary>플랫폼 밖 몫. 운영자 몫과 더하면 1.</summary>
        public double 밖몫 = 1.0;
    }

    /// <summary>운영자 하나가 동 하나에 낸 지사의 상태.</summary>
    public sealed class 경쟁자리
    {
        public string 운영자 = string.Empty;
        public string 동 = string.Empty;
        public int 기사;
        public int 입점;
        public double 행사예산;
        public double 신뢰;
        public double 몫;
        public double 받을주문;
        public double 받은주문;
        public double 놓친주문;
        public int 진출회차;
        /// <summary>받아냄 = 받은 주문 / 받을 주문. 받을 주문이 없으면 null(배달플랫폼Policy.지사요약.받아냄과 같은 뜻).</summary>
        public double? 받아냄 => 받을주문 > 1e-9 ? 받은주문 / 받을주문 : (double?)null;
    }

    public sealed class 경쟁수
    {
        public 경쟁수종류 종류;
        public string 동 = string.Empty;
        public long 금액;
        /// <summary>왜 이 수를 두는가(지사장이 쓰는 말로 한 줄).</summary>
        public string 왜 = string.Empty;
    }

    public sealed class 경쟁소식
    {
        public int 회차;
        public string 운영자 = string.Empty;
        public string 동 = string.Empty;
        public string 글 = string.Empty;
    }

    public sealed class 지사경쟁판
    {
        public 지사경쟁Rules Rules = new 지사경쟁Rules();
        /// <summary>지금 회차(1부터). 회차넘기기를 한 번 부르면 1 오른다.</summary>
        public int 회차 = 1;
        public ulong 씨앗;
        public readonly List<경쟁운영자> 운영자들 = new List<경쟁운영자>();
        public readonly List<경쟁동> 동들 = new List<경쟁동>();
        public readonly List<경쟁자리> 자리들 = new List<경쟁자리>();
        public readonly List<경쟁소식> 소식 = new List<경쟁소식>();

        public 경쟁운영자 운영자(string id) => 운영자들.FirstOrDefault(o => o.Id == id);
        public 경쟁동 동(string code) => 동들.FirstOrDefault(d => d.Code == code);
        public 경쟁자리 자리(string 운영자Id, string 동Code) => 자리들.FirstOrDefault(s => s.운영자 == 운영자Id && s.동 == 동Code);
        public IEnumerable<경쟁자리> 운영자자리(string 운영자Id) => 자리들.Where(s => s.운영자 == 운영자Id);
        public IEnumerable<경쟁자리> 동자리(string 동Code) => 자리들.Where(s => s.동 == 동Code);
    }

    public static class 지사경쟁Policy
    {
        /// <summary>새 판. 처음에는 모든 동이 플랫폼 밖 100%다.</summary>
        public static 지사경쟁판 새판(지사경쟁Rules rules, IEnumerable<경쟁동> 동들, IEnumerable<경쟁운영자> 운영자들, ulong 씨앗)
        {
            var 판 = new 지사경쟁판 { Rules = rules ?? new 지사경쟁Rules(), 씨앗 = 씨앗 == 0 ? 1UL : 씨앗 };
            if (동들 != null) 판.동들.AddRange(동들.Where(d => d != null).OrderBy(d => d.Code, StringComparer.Ordinal));
            if (운영자들 != null) 판.운영자들.AddRange(운영자들.Where(o => o != null).OrderBy(o => o.Id, StringComparer.Ordinal));
            foreach (var 동 in 판.동들) 동.밖몫 = 1.0;
            return 판;
        }

        /// <summary>판을 시작할 때 지사를 놓는다(돈을 받지 않는다). 이미 있으면 그대로 둔다.</summary>
        public static 경쟁자리 처음자리(지사경쟁판 판, string 운영자Id, string 동Code, int 기사, int 입점)
        {
            if (판 == null || 판.운영자(운영자Id) == null || 판.동(동Code) == null) return null;
            var 있음 = 판.자리(운영자Id, 동Code);
            if (있음 != null) return 있음;
            var 자리 = new 경쟁자리
            {
                운영자 = 운영자Id, 동 = 동Code, 기사 = Math.Max(0, 기사), 입점 = Math.Max(0, 입점),
                신뢰 = 판.Rules.처음신뢰, 진출회차 = 판.회차,
            };
            판.자리들.Add(자리);
            판.자리들.Sort((a, b) =>
            {
                var c = string.CompareOrdinal(a.동, b.동);
                return c != 0 ? c : string.CompareOrdinal(a.운영자, b.운영자);
            });
            return 자리;
        }

        // ── 읽기 ────────────────────────────────────────────────────────────

        public static double 점유율(지사경쟁판 판, string 운영자Id, string 동Code)
        {
            var 자리 = 판?.자리(운영자Id, 동Code);
            return 자리 == null ? 0.0 : 자리.몫;
        }

        public static int 지사수(지사경쟁판 판, string 운영자Id) => 판 == null ? 0 : 판.운영자자리(운영자Id).Count();

        public static double 처리력(지사경쟁Rules rules, int 기사, int 입점) =>
            Math.Min(Math.Max(0, 기사) * rules.기사회차처리, Math.Max(0, 입점) * rules.음식점회차처리);

        public static double 입점점수(지사경쟁Rules rules, 경쟁동 동, int 입점)
        {
            var 포화 = Math.Max(1.0, 동.음식점수 * rules.입점포화);
            return Clamp01(입점 / 포화);
        }

        /// <summary>속도 점수: 지금 몫만큼의 주문을 처리력이 얼마나 넉넉히 받는가. 몫이 아주 작아도 회차 주문의 5%는 받을 채비로 본다.</summary>
        public static double 속도점수(지사경쟁Rules rules, 경쟁동 동, double 몫, int 기사, int 입점)
        {
            var 받을 = Math.Max(몫, 0.05) * Math.Max(0.0, 동.회차주문);
            if (받을 <= 1e-9) return 0.0;
            return Clamp01(처리력(rules, 기사, 입점) / 받을);
        }

        public static double 행사점수(지사경쟁Rules rules, 경쟁동 동, double 행사예산)
        {
            var 바닥 = Math.Max(1.0, 동.회차주문 * rules.행사건당);
            return Clamp01(1.0 - Math.Exp(-Math.Max(0.0, 행사예산) / 바닥));
        }

        public static double 매력(지사경쟁Rules rules, 경쟁운영자 운영자, 경쟁동 동, double 몫, int 기사, int 입점, double 행사예산, double 신뢰)
        {
            return rules.입점가중 * 입점점수(rules, 동, 입점)
                 + rules.속도가중 * 속도점수(rules, 동, 몫, 기사, 입점)
                 + rules.배달비가중 * Clamp01(운영자.배달비점수)
                 + rules.행사가중 * 행사점수(rules, 동, 행사예산)
                 + rules.신뢰가중 * Clamp01(신뢰);
        }

        public static double 자리매력(지사경쟁판 판, 경쟁자리 자리) =>
            매력(판.Rules, 판.운영자(자리.운영자), 판.동(자리.동), 자리.몫, 자리.기사, 자리.입점, 자리.행사예산, 자리.신뢰);

        /// <summary>관성을 뺀 새 몫(매력 / 매력 합). 가정 자리를 주면 그 자리만 바꿔서 본다(AI의 셈 · 미리 보기).</summary>
        public static double 새몫(지사경쟁판 판, string 운영자Id, string 동Code, 경쟁자리 가정 = null)
        {
            var 합 = 판.Rules.플랫폼밖매력;
            var 내것 = 0.0;
            var 넣음 = false;
            foreach (var 자리 in 판.동자리(동Code))
            {
                var 볼 = 가정 != null && 자리.운영자 == 운영자Id ? 가정 : 자리;
                var a = 자리매력(판, 볼);
                합 += a;
                if (자리.운영자 == 운영자Id) { 내것 = a; 넣음 = true; }
            }
            if (!넣음 && 가정 != null)
            {
                내것 = 자리매력(판, 가정);
                합 += 내것;
            }
            return 합 <= 1e-12 ? 0.0 : 내것 / 합;
        }

        // ── 수 ─────────────────────────────────────────────────────────────

        public static long 수비용(지사경쟁Rules rules, 경쟁수 수)
        {
            switch (수.종류)
            {
                case 경쟁수종류.기사모집: return rules.기사모집비;
                case 경쟁수종류.음식점영업: return rules.영업비;
                case 경쟁수종류.새동진출: return rules.진출비;
                case 경쟁수종류.행사: return Math.Max(0L, 수.금액);
                default: return 0L;
            }
        }

        /// <summary>둘 수 있는가. 못 두면 이유를 준다. 판을 바꾸지 않는다.</summary>
        public static bool 둘수있나(지사경쟁판 판, string 운영자Id, 경쟁수 수, out string 이유)
        {
            이유 = string.Empty;
            var 운영자 = 판?.운영자(운영자Id);
            if (운영자 == null || 수 == null) { 이유 = "운영자나 수가 없습니다"; return false; }
            if (수.종류 == 경쟁수종류.쉼) return true;
            var 동 = 판.동(수.동);
            if (동 == null) { 이유 = "없는 동입니다"; return false; }
            var 자리 = 판.자리(운영자Id, 수.동);
            var rules = 판.Rules;
            switch (수.종류)
            {
                case 경쟁수종류.새동진출:
                    if (자리 != null) { 이유 = "이미 지사가 있습니다"; return false; }
                    if (판.운영자자리(운영자Id).Any() && !판.운영자자리(운영자Id).Any(s => 동.이웃.Contains(s.동)))
                    { 이유 = "내 지사와 맞닿은 동이 아닙니다"; return false; }
                    break;
                case 경쟁수종류.기사모집:
                    if (자리 == null) { 이유 = "지사가 없는 동입니다"; return false; }
                    if (자리.기사 >= rules.동기사한도) { 이유 = "기사 자리가 다 찼습니다"; return false; }
                    break;
                case 경쟁수종류.음식점영업:
                    if (자리 == null) { 이유 = "지사가 없는 동입니다"; return false; }
                    if (자리.입점 >= 동.음식점수) { 이유 = "영업할 음식점이 남지 않았습니다"; return false; }
                    break;
                case 경쟁수종류.행사:
                    if (자리 == null) { 이유 = "지사가 없는 동입니다"; return false; }
                    if (수.금액 <= 0) { 이유 = "행사 예산이 없습니다"; return false; }
                    break;
            }
            if (수비용(rules, 수) > 운영자.자본) { 이유 = "자본이 모자랍니다"; return false; }
            return true;
        }

        /// <summary>수를 둔다. 나와 AI가 같은 함수를 쓴다. 못 두면 판을 바꾸지 않고 false.</summary>
        public static bool 수두기(지사경쟁판 판, string 운영자Id, 경쟁수 수, out string 이유)
        {
            if (!둘수있나(판, 운영자Id, 수, out 이유)) return false;
            if (수.종류 == 경쟁수종류.쉼) return true;
            var 운영자 = 판.운영자(운영자Id);
            var rules = 판.Rules;
            운영자.자본 -= 수비용(rules, 수);
            var 동 = 판.동(수.동);
            var 자리 = 판.자리(운영자Id, 수.동);
            string 글;
            switch (수.종류)
            {
                case 경쟁수종류.새동진출:
                    처음자리(판, 운영자Id, 수.동, rules.진출기사, rules.진출입점);
                    글 = "지사를 냄";
                    break;
                case 경쟁수종류.기사모집:
                    자리.기사 += 1;
                    글 = "기사 모집 → " + 자리.기사 + "명";
                    break;
                case 경쟁수종류.음식점영업:
                    자리.입점 = Math.Min(동.음식점수, 자리.입점 + rules.영업한번입점);
                    글 = "음식점 영업 → 입점 " + 자리.입점 + "곳";
                    break;
                default:
                    자리.행사예산 += 수.금액;
                    글 = "행사 " + 수.금액.ToString("#,0", CultureInfo.InvariantCulture) + "원";
                    break;
            }
            판.소식.Add(new 경쟁소식
            {
                회차 = 판.회차, 운영자 = 운영자Id, 동 = 수.동,
                글 = 동.이름 + " " + 글 + (string.IsNullOrEmpty(수.왜) ? string.Empty : " — " + 수.왜),
            });
            return true;
        }

        // ── 회차 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 회차를 넘긴다. AI가 수를 두고(지사경쟁Ai), 몫 · 받은 주문 · 돈 · 신뢰 · 연구 점수를 한 번 셈한다.
        /// 같은 판에서 한 번 부르면 회차가 1 오른다. 조회 함수는 판을 바꾸지 않는다.
        /// </summary>
        public static void 회차넘기기(지사경쟁판 판)
        {
            if (판 == null) return;
            var rules = 판.Rules;
            foreach (var 운영자 in 판.운영자들.Where(o => !o.나))
            {
                foreach (var 수 in 지사경쟁Ai.수고르기(판, 운영자.Id))
                {
                    string 이유;
                    수두기(판, 운영자.Id, 수, out 이유);
                }
            }

            foreach (var 동 in 판.동들)
            {
                var 자리들 = 판.동자리(동.Code).ToList();
                var 매력들 = 자리들.Select(s => 자리매력(판, s)).ToList();
                var 합 = rules.플랫폼밖매력 + 매력들.Sum();
                var 운영자몫합 = 0.0;
                for (var i = 0; i < 자리들.Count; i++)
                {
                    var 새 = 합 <= 1e-12 ? 0.0 : 매력들[i] / 합;
                    자리들[i].몫 = 자리들[i].몫 * rules.관성 + 새 * (1.0 - rules.관성);
                    운영자몫합 += 자리들[i].몫;
                }
                동.밖몫 = Math.Max(0.0, 1.0 - 운영자몫합);

                foreach (var 자리 in 자리들)
                {
                    var 운영자 = 판.운영자(자리.운영자);
                    자리.받을주문 = 자리.몫 * Math.Max(0.0, 동.회차주문);
                    자리.받은주문 = Math.Min(자리.받을주문, 처리력(rules, 자리.기사, 자리.입점));
                    자리.놓친주문 = 자리.받을주문 - 자리.받은주문;
                    운영자.자본 += (long)Math.Round(자리.받은주문 * rules.건당지사몫, MidpointRounding.AwayFromZero);

                    var 놓친비율 = 자리.받을주문 > 1e-9 ? 자리.놓친주문 / 자리.받을주문 : 0.0;
                    자리.신뢰 = Clamp01(자리.신뢰 + rules.신뢰회복 * (1.0 - 자리.신뢰) - rules.신뢰깎임 * 놓친비율);
                    자리.행사예산 *= rules.행사남음;
                    if (자리.행사예산 < 1.0) 자리.행사예산 = 0.0;

                    if (놓친비율 > 0.10)
                        판.소식.Add(new 경쟁소식 { 회차 = 판.회차, 운영자 = 자리.운영자, 동 = 동.Code,
                            글 = 동.이름 + " 놓친 주문 " + Math.Round(놓친비율 * 100) + "%" });
                    if (자리.기사 > rules.진출기사 && 자리.받은주문 / 자리.기사 < rules.기사일감하한)
                    {
                        자리.기사 -= 1;
                        판.소식.Add(new 경쟁소식 { 회차 = 판.회차, 운영자 = 자리.운영자, 동 = 동.Code,
                            글 = 동.이름 + " 기사 1명이 일감이 적어 떠남 → " + 자리.기사 + "명" });
                    }
                }
            }

            foreach (var 운영자 in 판.운영자들)
            {
                var 지사 = 지사수(판, 운영자.Id);
                var 운영비 = rules.지사회차운영비 * 지사;
                if (운영자.자본 >= 운영비) 운영자.자본 -= 운영비;
                else
                {
                    운영자.자본 = 0;
                    foreach (var 자리 in 판.운영자자리(운영자.Id)) 자리.신뢰 = Clamp01(자리.신뢰 - rules.운영비밀림신뢰);
                    판.소식.Add(new 경쟁소식 { 회차 = 판.회차, 운영자 = 운영자.Id, 글 = "지사 운영비를 다 내지 못함 · 신뢰가 깎임" });
                }
                운영자.연구점수 += (long)rules.지사연구점수 * 지사;
            }
            판.회차 += 1;
        }

        // ── 지문 · 도구 ──────────────────────────────────────────────────────

        /// <summary>판의 지문. 같은 판이면 같은 글자다(씨앗 재현 검사 · 조회가 판을 바꾸지 않는지 검사).</summary>
        public static string 지문(지사경쟁판 판)
        {
            var text = new StringBuilder();
            text.Append(판.회차).Append('|').Append(판.씨앗).Append('|');
            foreach (var o in 판.운영자들) text.Append(o.Id).Append(':').Append(o.자본).Append(':').Append(o.연구점수).Append(';');
            foreach (var d in 판.동들) text.Append(d.Code).Append(':').Append(d.밖몫.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            foreach (var s in 판.자리들)
                text.Append(s.운영자).Append('@').Append(s.동).Append(':').Append(s.기사).Append(':').Append(s.입점).Append(':')
                    .Append(s.행사예산.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(s.신뢰.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(s.몫.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            ulong hash = 14695981039346656037UL;
            foreach (var ch in text.ToString())
            {
                hash ^= ch;
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        /// <summary>씨앗과 글자로 0~1 값을 낸다(동률 깨기). System.Random에 기대지 않는다.</summary>
        public static double 흔들림(ulong 씨앗, int 회차, string 글)
        {
            ulong x = 씨앗 ^ ((ulong)(uint)회차 * 0x9E3779B97F4A7C15UL);
            foreach (var ch in 글 ?? string.Empty)
            {
                x ^= ch;
                x *= 1099511628211UL;
            }
            x ^= x >> 33; x *= 0xFF51AFD7ED558CCDUL; x ^= x >> 33; x *= 0xC4CEB9FE1A85EC53UL; x ^= x >> 33;
            return (x >> 11) / (double)(1UL << 53);
        }

        internal static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
    }
}
