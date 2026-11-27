using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-27 지사장 판 J2 — 이웃 AI 지사장(규칙 기반 · 순수 규칙).
    // 사용자: 「문명도 보면 AI가 움직이는 그런 게 있잖아. 상대방 측으로」(10:41). Codex 행정동 도시와 이웃 AI r1: 이웃은 스스로 판단하고 같은 규칙을 쓴다.
    //  - 회차마다 둘 수 있는 수를 늘어놓고, 값(늘어날 받은 주문 × 건당 지사 몫 × 내다봄 ÷ 비용)에 전략 가중을 곱해 높은 것부터 1~3개 둔다.
    //  - 전략 넷은 가중만 다르다. 식 · 비용 · 한도는 나와 같다(지사경쟁Policy.수두기). 씨앗은 동률을 깨는 데만 쓴다.
    //  - 수마다 「왜」를 지사장이 쓰는 말로 한 줄 남긴다. 깨어 있음 · 명덕 같은 말은 쓰지 않는다.
    public static class 지사경쟁Ai
    {
        public static double 전략가중(경쟁전략 전략, 경쟁수종류 종류)
        {
            switch (전략)
            {
                case 경쟁전략.할인형:
                    return 종류 == 경쟁수종류.행사 ? 3.0 : 종류 == 경쟁수종류.새동진출 ? 1.1 : 0.8;
                case 경쟁전략.속도형:
                    return 종류 == 경쟁수종류.기사모집 ? 1.9 : 종류 == 경쟁수종류.행사 ? 0.5 : 1.0;
                case 경쟁전략.입점형:
                    return 종류 == 경쟁수종류.음식점영업 ? 1.9 : 종류 == 경쟁수종류.행사 ? 0.5 : 1.0;
                case 경쟁전략.동네밀착형:
                    return 종류 == 경쟁수종류.새동진출 ? 0.25 : 종류 == 경쟁수종류.행사 ? 0.7 : 1.35;
                default:
                    return 1.0;
            }
        }

        /// <summary>이번 회차에 이 운영자가 둘 수(0~AI최대수개). 판을 바꾸지 않는다(가정 자리로만 셈한다).</summary>
        public static List<경쟁수> 수고르기(지사경쟁판 판, string 운영자Id)
        {
            var 고른 = new List<경쟁수>();
            var 운영자 = 판?.운영자(운영자Id);
            if (운영자 == null) return 고른;
            var rules = 판.Rules;
            // 두 회차 운영비는 남겨 둔다(운영비가 밀리면 신뢰가 깎인다)
            var 쓸돈 = 운영자.자본 - Math.Max(rules.AI남길돈, 2 * rules.지사회차운영비 * 지사경쟁Policy.지사수(판, 운영자Id));
            var 가정들 = 판.운영자자리(운영자Id).ToDictionary(s => s.동, s => 복사(s));

            for (var n = 0; n < Math.Max(0, rules.AI최대수); n++)
            {
                경쟁수 최고 = null;
                var 최고값 = 0.0;
                foreach (var 후보 in 후보들(판, 운영자, 가정들))
                {
                    var 비용 = 지사경쟁Policy.수비용(rules, 후보);
                    if (비용 <= 0 || 비용 > 쓸돈) continue;
                    var 값 = 수의값(판, 운영자, 후보, 가정들) / 비용 * 전략가중(운영자.전략, 후보.종류);
                    값 *= 1.0 + 0.02 * 지사경쟁Policy.흔들림(판.씨앗, 판.회차, 운영자Id + "|" + 후보.종류 + "|" + 후보.동 + "|" + n);
                    if (값 > 최고값) { 최고값 = 값; 최고 = 후보; }
                }
                if (최고 == null || 최고값 <= 1.0) break;       // 내다보는 동안 본전을 못 뽑으면 쉰다
                최고.왜 = 왜(판, 운영자, 최고, 가정들);
                고른.Add(최고);
                쓸돈 -= 지사경쟁Policy.수비용(rules, 최고);
                가정에반영(판, 운영자, 최고, 가정들);
            }
            return 고른;
        }

        private static IEnumerable<경쟁수> 후보들(지사경쟁판 판, 경쟁운영자 운영자, Dictionary<string, 경쟁자리> 가정들)
        {
            var rules = 판.Rules;
            foreach (var 쌍 in 가정들.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var 동 = 판.동(쌍.Key);
                var 자리 = 쌍.Value;
                if (자리.기사 < rules.동기사한도) yield return new 경쟁수 { 종류 = 경쟁수종류.기사모집, 동 = 동.Code };
                if (자리.입점 < 동.음식점수) yield return new 경쟁수 { 종류 = 경쟁수종류.음식점영업, 동 = 동.Code };
                yield return new 경쟁수 { 종류 = 경쟁수종류.행사, 동 = 동.Code, 금액 = rules.행사단위 };
            }
            var 이웃들 = 가정들.Keys.SelectMany(code => 판.동(code).이웃).Distinct().Where(code => !가정들.ContainsKey(code) && 판.동(code) != null)
                .OrderBy(code => code, StringComparer.Ordinal).ToList();
            foreach (var code in 이웃들) yield return new 경쟁수 { 종류 = 경쟁수종류.새동진출, 동 = code };
        }

        /// <summary>수의 값 = 내다보는 동안 더 받을 주문 × 건당 지사 몫. 행사는 예산이 줄어드니 절반만 친다.</summary>
        private static double 수의값(지사경쟁판 판, 경쟁운영자 운영자, 경쟁수 수, Dictionary<string, 경쟁자리> 가정들)
        {
            var rules = 판.Rules;
            var 동 = 판.동(수.동);
            경쟁자리 앞;
            가정들.TryGetValue(수.동, out 앞);
            var 뒤 = 앞 == null
                ? new 경쟁자리 { 운영자 = 운영자.Id, 동 = 수.동, 기사 = rules.진출기사, 입점 = rules.진출입점, 신뢰 = rules.처음신뢰 }
                : 복사(앞);
            switch (수.종류)
            {
                case 경쟁수종류.기사모집: 뒤.기사 += 1; break;
                case 경쟁수종류.음식점영업: 뒤.입점 = Math.Min(동.음식점수, 뒤.입점 + rules.영업한번입점); break;
                case 경쟁수종류.행사: 뒤.행사예산 += 수.금액; break;
            }
            var 앞받음 = 앞 == null ? 0.0 : 받을것(판, 운영자.Id, 동, 앞);
            var 뒤받음 = 받을것(판, 운영자.Id, 동, 뒤);
            // 기사와 입점은 모자란 쪽이 병목이라 하나만 늘려서는 받는 주문이 안 늘 때가 있다.
            // 그때는 둘을 함께 늘렸을 때 이 수가 보탠 몫(함께 − 저것만)을 비용 비율로 나눠 친다. 저것만으로 되는 일에는 값을 주지 않는다.
            if (수.종류 == 경쟁수종류.기사모집 || 수.종류 == 경쟁수종류.음식점영업)
            {
                var 저것만 = 복사(앞);
                var 둘 = 복사(뒤);
                long 이비용, 저비용;
                if (수.종류 == 경쟁수종류.기사모집)
                {
                    저것만.입점 = Math.Min(동.음식점수, 저것만.입점 + rules.영업한번입점);
                    둘.입점 = Math.Min(동.음식점수, 둘.입점 + rules.영업한번입점);
                    이비용 = rules.기사모집비; 저비용 = rules.영업비;
                }
                else
                {
                    저것만.기사 = Math.Min(rules.동기사한도, 저것만.기사 + 1);
                    둘.기사 = Math.Min(rules.동기사한도, 둘.기사 + 1);
                    이비용 = rules.영업비; 저비용 = rules.기사모집비;
                }
                var 보탠 = 받을것(판, 운영자.Id, 동, 둘) - 받을것(판, 운영자.Id, 동, 저것만);
                var 나눈 = 앞받음 + Math.Max(0.0, 보탠) * 이비용 / (double)(이비용 + 저비용);
                if (나눈 > 뒤받음) 뒤받음 = 나눈;
            }
            // 새 지사는 다 자랐을 때를 보고 값을 친다(기사 · 입점을 다섯 번씩 더 늘린 모습). 그만큼 키우는 돈과 운영비를 뺀다.
            if (수.종류 == 경쟁수종류.새동진출)
            {
                var 자란 = 복사(뒤);
                자란.기사 = Math.Min(rules.동기사한도, 자란.기사 + 5);
                자란.입점 = Math.Min(동.음식점수, 자란.입점 + 5 * rules.영업한번입점);
                자란.신뢰 = Math.Max(자란.신뢰, 0.7);
                자란.몫 = 지사경쟁Policy.새몫(판, 운영자.Id, 동.Code, 자란);
                var 벌이 = (받을것(판, 운영자.Id, 동, 자란) * rules.건당지사몫 - rules.지사회차운영비) * rules.AI내다봄;
                var 키우는돈 = 5 * rules.기사모집비 + 5 * rules.영업비;
                var 진출밀착 = 운영자.전략 == 경쟁전략.동네밀착형 && 가정들.Values.All(s => s.몫 >= 0.40) ? 4.0 : 1.0;
                return Math.Max(0.0, (벌이 - 키우는돈) * 진출밀착);
            }
            var 내다봄 = rules.AI내다봄 * (수.종류 == 경쟁수종류.행사 ? 0.5 : 1.0);
            return Math.Max(0.0, 뒤받음 - 앞받음) * rules.건당지사몫 * 내다봄;
        }

        private static double 받을것(지사경쟁판 판, string 운영자Id, 경쟁동 동, 경쟁자리 가정)
        {
            var 몫 = 지사경쟁Policy.새몫(판, 운영자Id, 동.Code, 가정);
            return Math.Min(몫 * 동.회차주문, 지사경쟁Policy.처리력(판.Rules, 가정.기사, 가정.입점));
        }

        private static void 가정에반영(지사경쟁판 판, 경쟁운영자 운영자, 경쟁수 수, Dictionary<string, 경쟁자리> 가정들)
        {
            var rules = 판.Rules;
            경쟁자리 자리;
            if (!가정들.TryGetValue(수.동, out 자리))
            {
                자리 = new 경쟁자리 { 운영자 = 운영자.Id, 동 = 수.동, 기사 = rules.진출기사, 입점 = rules.진출입점, 신뢰 = rules.처음신뢰 };
                가정들[수.동] = 자리;
                return;
            }
            switch (수.종류)
            {
                case 경쟁수종류.기사모집: 자리.기사 += 1; break;
                case 경쟁수종류.음식점영업: 자리.입점 = Math.Min(판.동(수.동).음식점수, 자리.입점 + rules.영업한번입점); break;
                case 경쟁수종류.행사: 자리.행사예산 += 수.금액; break;
            }
        }

        private static string 왜(지사경쟁판 판, 경쟁운영자 운영자, 경쟁수 수, Dictionary<string, 경쟁자리> 가정들)
        {
            var rules = 판.Rules;
            var 동 = 판.동(수.동);
            경쟁자리 자리;
            가정들.TryGetValue(수.동, out 자리);
            var inv = CultureInfo.InvariantCulture;
            switch (수.종류)
            {
                case 경쟁수종류.기사모집:
                {
                    var 받을 = Math.Max(자리.몫, 0.05) * 동.회차주문;
                    var 힘 = 자리.기사 * rules.기사회차처리;
                    return 힘 < 받을
                        ? "받을 주문 " + 받을.ToString("0", inv) + "건인데 기사 처리력이 " + 힘.ToString("0", inv) + "건"
                        : "배달 시간을 줄여 점유율을 올리려고";
                }
                case 경쟁수종류.음식점영업:
                    return "입점 " + 자리.입점 + "곳 · 고를 가게를 늘려 점유율을 올리려고";
                case 경쟁수종류.행사:
                    return "점유율 " + Math.Round(자리.몫 * 100).ToString("0", inv) + "% · 행사로 주문을 끌어오려고";
                case 경쟁수종류.새동진출:
                    return "회차 주문 " + 동.회차주문.ToString("0", inv) + "건 · 먼저 들어온 지사 " + 판.동자리(동.Code).Count() + "곳";
                default:
                    return string.Empty;
            }
        }

        private static 경쟁자리 복사(경쟁자리 s) => new 경쟁자리
        {
            운영자 = s.운영자, 동 = s.동, 기사 = s.기사, 입점 = s.입점, 행사예산 = s.행사예산, 신뢰 = s.신뢰, 몫 = s.몫,
            받을주문 = s.받을주문, 받은주문 = s.받은주문, 놓친주문 = s.놓친주문, 진출회차 = s.진출회차,
        };
    }
}
