"""행정동별 주문 수요 팩 굽기 (2026-11-13).

동마다 주문이 생기게 하는 바탕이다. 인구·소득 자료가 있으면 그것으로, 없으면 그 사실을 밝히고 건물 자료로 대신한다.

  인구   서울 열린데이터 OA-22178 「길단위인구-행정동」 CSV (분기 추정 유동인구, 시간대 6칸·요일 7칸).
         분기 집계 추정값이며 주민 수·실시간 보행자·주문 수가 아니다. 여기서는 동 사이의 **비율**과 시간대·요일 **모양**만 쓴다.
  소득   서울 열린데이터 OA-22166 「소득소비-행정동」 CSV (월 평균 소득 금액). 아직 수집 전이면 모든 동이 1.0이다.
  대신   인구 CSV가 없으면 모듈 건물 색인(AL_D010)의 주거 연면적(단독·공동주택 넓이 × 층수)을 비율로 쓴다.
         시간대·요일 모양은 임시 값(FALLBACK_BANDS·FALLBACK_WEEKDAYS)이다.

하루 주문 수는 가상 값이다: 묶음 전체의 하루 주문(RULES.areaDailyOrders)을 동의 비율로 나눈다.
  비율 ∝ 인구 바탕 × 소득 지수 ** INCOME_ELASTICITY

쓰기:
  python -I make_dong_demand.py <Resources 폴더> [--population <OA-22178.csv>] [--income <OA-22166.csv>]
                                [--restaurants <동별 음식점 수.json>] [--quarter 20262] [--out <파일>]
CSV는 UTF-8 또는 CP949. 필요한 열이 없거나 동 이름이 코드와 맞지 않으면 실패한다(조용히 대신하지 않는다).
"""
import argparse, csv, hashlib, io, json, re, sys
from pathlib import Path

INCOME_ELASTICITY = 0.5
BAND_HOURS = [0, 6, 11, 14, 17, 21, 24]
BAND_COLUMNS = ["시간대_00_06_유동인구_수", "시간대_06_11_유동인구_수", "시간대_11_14_유동인구_수",
                "시간대_14_17_유동인구_수", "시간대_17_21_유동인구_수", "시간대_21_24_유동인구_수"]
WEEKDAY_COLUMNS = ["월요일_유동인구_수", "화요일_유동인구_수", "수요일_유동인구_수", "목요일_유동인구_수",
                   "금요일_유동인구_수", "토요일_유동인구_수", "일요일_유동인구_수"]
QUARTER_COLUMN, CODE_COLUMN, NAME_COLUMN, TOTAL_COLUMN = "기준_년분기_코드", "행정동_코드", "행정동_코드_명", "총_유동인구_수"
INCOME_COLUMN = "월_평균_소득_금액"
RESIDENTIAL = ("al-d010:01000", "al-d010:02000")               # 단독주택, 공동주택
FALLBACK_BANDS = [0.03, 0.10, 0.27, 0.12, 0.38, 0.10]          # 임시: 음식 주문의 하루 모양(점심·저녁에 몰림)
FALLBACK_WEEKDAYS = [0.94, 0.94, 0.95, 0.97, 1.05, 1.10, 1.05]  # 임시: 월~일 (평균 1)
RULES = {
    "areaDailyOrders": 240.0,      # 이 팩의 동 전체에서 하루에 생기는 가상 주문 수(평균 요일)
    "riderOrdersPerHour": 3.5,     # 기사 한 명이 한 시간에 해내는 주문(r132: 3~4건)
    "partnerOrdersPerHour": 4.0,   # 제휴 음식점 한 곳이 한 시간에 낼 수 있는 주문(임시)
    "revenuePerOrder": 4800,       # 주문 한 건의 가상 수입
    "riderPayPerOrder": 3600,      # 주문 한 건의 가상 기사 보수
    "recruitCost": 20000,          # 기사 한 명 모집(임시)
    "contractCost": 6000,          # 제휴 한 곳 맺기(임시)
    "engineCost": 60000,           # 배차 엔진 한 단계(임시, 단계마다 곱절)
    "engineBoostPerLevel": 0.12,   # 엔진 한 단계가 기사 처리량에 더하는 비율(임시)
    "maxEngineLevel": 5,
    "startFunds": 60000,           # 처음 가진 가상 자금
    "maxRidersPerDong": 12,
    "maxPartnerShare": 0.6,        # 그 동 음식점 수의 이 비율까지만 제휴
    "windowMs": 1800000,           # 신호를 볼 때 돌아보는 가상 시간(30분)
    "minWindowOrders": 0.5,        # 창 안의 수요가 이보다 적으면 신호를 올리지 않는다
    "cautionUnmetRatio": 0.35,     # 못 받은 주문이 이 비율을 넘으면 주의
    "clearUnmetRatio": 0.15,       # 이 아래로 내려가면 신호를 끈다
    "escalateAfterMs": 600000,     # 주의를 이만큼 두면 위험
    "deepenServeRatio": 0.5,       # 이만큼은 받아내야 동이 깊어진다(밝아진다)
    "maxStepMs": 60000,            # 시각이 크게 건너뛰어도 한 번에 쌓이는 시간
    "startHour": 11.5,             # 가상 시각 0 = 11:30 (음식 배달 Core와 같다)
    "startRiders": 2,              # 처음부터 운영하는 동(홈)에 이미 있는 기사
    "startPartners": 2,            # 처음부터 운영하는 동마다 이미 있는 제휴
}


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def read_csv(path):
    raw = Path(path).read_bytes()
    for encoding in ("utf-8-sig", "cp949"):
        try:
            text = raw.decode(encoding)
            break
        except UnicodeDecodeError:
            continue
    else:
        raise SystemExit(f"{path}: UTF-8도 CP949도 아닙니다")
    rows = list(csv.DictReader(io.StringIO(text)))
    return [{(k or "").strip(): (v or "").strip() for k, v in row.items()} for row in rows]


def need(rows, columns, path):
    missing = [c for c in columns if not rows or c not in rows[0]]
    if missing:
        raise SystemExit(f"{path}: 필요한 열이 없습니다: {missing}. 있는 열: {list(rows[0]) if rows else []}")


def plain(name):
    """면목제3.8동 = 면목3.8동 = 면목3·8동"""
    return re.sub(r"[\s·.]", "", name.replace("제", ""))


def number(text):
    return float(text.replace(",", ""))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("resources")
    parser.add_argument("--population")
    parser.add_argument("--income")
    parser.add_argument("--restaurants")
    parser.add_argument("--quarter")
    parser.add_argument("--out")
    args = parser.parse_args()
    resources = Path(args.resources)
    territory_path = resources / "hongdal-territory-hjd.json"
    buildings_path = resources / "hongdal-dong-buildings.json"
    territory = json.loads(territory_path.read_text(encoding="utf-8"))
    dongs = [{"code": d["code"], "name": d["name"], "region": d["region"]} for d in territory["dongs"]]
    start = {s["code"] for s in territory.get("startDongs", [])} or {territory["homeDong"]}
    home = territory["homeDong"]
    checks, sources = [], {"territory": sha256(territory_path)}

    def check(name, ok, detail=""):
        checks.append({"name": name, "ok": bool(ok), "detail": detail})

    # ── 인구 바탕 ──
    if args.population:
        rows = read_csv(args.population)
        need(rows, [QUARTER_COLUMN, CODE_COLUMN, NAME_COLUMN, TOTAL_COLUMN] + BAND_COLUMNS + WEEKDAY_COLUMNS, args.population)
        quarter = args.quarter or max(r[QUARTER_COLUMN] for r in rows)
        latest = {r[CODE_COLUMN]: r for r in rows if r[QUARTER_COLUMN] == quarter}
        for d in dongs:
            row = latest.get(d["code"][:8])
            if row is None:
                raise SystemExit(f"{d['name']}({d['code'][:8]})의 {quarter} 유동인구 행이 없습니다")
            if plain(row[NAME_COLUMN]) != plain(d["name"]):
                raise SystemExit(f"코드 {d['code'][:8]}의 이름이 다릅니다: 자료 {row[NAME_COLUMN]} / 모듈 {d['name']}")
            d["population"] = number(row[TOTAL_COLUMN])
            bands = [number(row[c]) for c in BAND_COLUMNS]
            days = [number(row[c]) for c in WEEKDAY_COLUMNS]
            d["bands"] = [b / sum(bands) for b in bands]
            d["weekdays"] = [v * 7 / sum(days) for v in days]
        population_basis = {"kind": "floatingPopulation", "dataset": "OA-22178", "quarter": quarter, "sha256": sha256(args.population),
                            "note": "서울 열린데이터 길단위인구-행정동. 분기 추정 집계이며 주민 수·실시간 보행자·주문 수가 아니다. 동 사이 비율과 시간대·요일 모양만 썼다."}
        sources["population"] = population_basis["sha256"]
    else:
        index = json.loads(buildings_path.read_text(encoding="utf-8"))
        area = {}
        for b in index["buildings"]:
            if b.get("kind") in RESIDENTIAL:
                area[b["region"]] = area.get(b["region"], 0.0) + b.get("area", 0.0) * max(1, int(b.get("floors") or 1))
        for d in dongs:
            d["population"] = round(area.get(d["region"], 0.0), 1)
            d["bands"] = list(FALLBACK_BANDS)
            d["weekdays"] = list(FALLBACK_WEEKDAYS)
        population_basis = {"kind": "residentialFloorArea", "dataset": "AL_D010 건물 색인", "sha256": sha256(buildings_path),
                            "note": "인구 자료를 넣지 않아 주거 연면적(단독·공동주택 넓이 × 층수, ㎡)을 비율로 썼다. 시간대·요일 모양은 임시 값이다."}
        sources["buildings"] = population_basis["sha256"]
    check("모든 동에 인구 바탕 값이 있다", all(d["population"] > 0 for d in dongs), str([d["population"] for d in dongs]))

    # ── 소득 지수 ──
    if args.income:
        rows = read_csv(args.income)
        need(rows, [QUARTER_COLUMN, CODE_COLUMN, NAME_COLUMN, INCOME_COLUMN], args.income)
        quarter = max(r[QUARTER_COLUMN] for r in rows)
        latest = {r[CODE_COLUMN]: r for r in rows if r[QUARTER_COLUMN] == quarter}
        incomes = []
        for d in dongs:
            row = latest.get(d["code"][:8])
            if row is None or plain(row[NAME_COLUMN]) != plain(d["name"]):
                raise SystemExit(f"{d['name']}({d['code'][:8]})의 소득 행이 없거나 이름이 다릅니다")
            incomes.append(number(row[INCOME_COLUMN]))
        mean = sum(incomes) / len(incomes)
        for d, income in zip(dongs, incomes):
            d["incomeIndex"] = round(income / mean, 4)
        income_basis = {"kind": "monthlyIncome", "dataset": "OA-22166", "quarter": quarter, "sha256": sha256(args.income),
                        "note": "서울 열린데이터 소득소비-행정동의 월 평균 소득 금액. 이 팩의 동 평균을 1로 둔 지수만 썼다."}
        sources["income"] = income_basis["sha256"]
    else:
        for d in dongs:
            d["incomeIndex"] = 1.0
        income_basis = {"kind": "none", "note": "소득 자료를 아직 수집하지 않았다. 모든 동이 1.0이다(OA-22166 수집 뒤 다시 굽는다)."}

    # ── 음식점 수(제휴 후보의 상한) ──
    if args.restaurants:
        counts = json.loads(Path(args.restaurants).read_text(encoding="utf-8"))
        for d in dongs:
            d["restaurants"] = int(counts[d["code"]])
        restaurant_basis = {"kind": "licences", "sha256": sha256(args.restaurants),
                            "note": "서울 인허가 「자료상 영업」 일반·휴게음식점 원문행을 건물에 붙여 센 수. 좌표계는 확정 전 후보다."}
        sources["restaurants"] = restaurant_basis["sha256"]
    else:
        for d in dongs:
            d["restaurants"] = 0
        restaurant_basis = {"kind": "none", "note": "음식점 수를 넣지 않았다. 제휴 상한은 rules.maxRidersPerDong의 두 배로 본다."}

    # ── 비율과 하루 주문 ──
    raw = [d["population"] * d["incomeIndex"] ** INCOME_ELASTICITY for d in dongs]
    for d, value in zip(dongs, raw):
        d["weight"] = value / sum(raw)
        d["dailyOrders"] = round(RULES["areaDailyOrders"] * d["weight"], 2)
        d["start"] = d["code"] in start
        # 처음 동에 이미 있는 것: 홈(매장 A·B, 기사 2명)과 그 밖의 처음 동(가게 한 곳, 기사 한 명으로 본다 · 임시)
        d["startRiders"] = (RULES["startRiders"] if d["code"] == home else 1) if d["start"] else 0
        d["startPartners"] = (RULES["startPartners"] if d["code"] == home else 1) if d["start"] else 0
    check("비율의 합은 1이다", abs(sum(d["weight"] for d in dongs) - 1) < 1e-9)
    check("시간대 비율의 합은 1이다", all(abs(sum(d["bands"]) - 1) < 1e-9 and len(d["bands"]) == 6 for d in dongs))
    check("요일 값의 평균은 1이다", all(abs(sum(d["weekdays"]) / 7 - 1) < 1e-9 for d in dongs))
    check("하루 주문의 합이 묶음 값과 같다", abs(sum(d["dailyOrders"] for d in dongs) - RULES["areaDailyOrders"]) < 0.1)
    check("처음부터 운영하는 동이 있다", any(d["start"] for d in dongs))

    pack = {
        "schema": "hongdal-dong-demand.v1",
        "label": "가상 주문 · 내부 검토용 · 배포 아님",
        "note": "하루 주문 수·비용·보수는 모두 가상 값이다. 인구·소득 자료는 동 사이의 비율과 시간대·요일 모양에만 쓴다.",
        "basis": {"population": population_basis, "income": income_basis, "restaurants": restaurant_basis,
                  "incomeElasticity": INCOME_ELASTICITY},
        "sources": sources,
        "bandHours": BAND_HOURS,
        "rules": RULES,
        "dongs": [{"code": d["code"], "name": d["name"], "population": d["population"], "incomeIndex": d["incomeIndex"],
                   "restaurants": d["restaurants"], "weight": round(d["weight"], 6), "dailyOrders": d["dailyOrders"],
                   "bands": [round(v, 6) for v in d["bands"]], "weekdays": [round(v, 6) for v in d["weekdays"]], "start": d["start"],
                   "startRiders": d["startRiders"], "startPartners": d["startPartners"]}
                  for d in dongs],
    }
    out = Path(args.out) if args.out else resources / "hongdal-dong-demand.json"
    out.write_text(json.dumps(pack, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    failed = [c for c in checks if not c["ok"]]
    print(json.dumps({"checks": checks, "allChecksPassed": not failed, "output": str(out), "outputSha256": sha256(out),
                      "populationBasis": population_basis["kind"], "incomeBasis": income_basis["kind"]}, ensure_ascii=False, indent=1))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
