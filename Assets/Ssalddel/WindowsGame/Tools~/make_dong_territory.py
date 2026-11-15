"""행정동 영역 팩 굽기 (2026-11-13). 영역(깃발)의 단위를 법정동에서 행정동으로 통일한다.

입력 (모두 이 모듈의 Resources에 있는 것):
  hongdal-dong-modules.json     행정동 모듈 목록(경계·이름·코드). 2023 역사 경계 후보 · 내부 검토용
  hongdal-dong-buildings.json   모듈 건물 색인(표식 자리 후보)
  hongdal-building-roles.json   기존 매장·카페·창고의 역할표(가상)
출력:
  hongdal-territory-hjd.json    TerritoryPack(hongdal-territory.v1)과 같은 형식 + unit·legal·startDongs

좌표는 구 지도와 같다(Tools~/gu_geo.py): x = (경도 - WEST) * MX, y = (위도 - SOUTH) * MY (미터).
맞닿음은 두 경계 사이 가장 가까운 거리가 NEAR_METRES 안일 때다(경계는 단순화되어 있어 조금 떨어져 보일 수 있다).
rules는 모두 임시 값이다. 기획이 정하면 이 파일의 RULES만 바꾼다.

쓰기:  python -I make_dong_territory.py <Resources 폴더> [<출력 파일>]
"""
import hashlib, json, math, sys
from pathlib import Path

WEST, SOUTH, MX, MY = 126.940, 37.523, 88224.0, 111320.0       # gu_geo.py와 같아야 한다
NEAR_METRES = 15.0
SPOTS = 4
SPOT_GAP = 80.0
NON_RESIDENTIAL = ("al-d010:03000", "al-d010:04000")           # 제1종·제2종 근린생활
LEGAL_OF = {"1126052000": "1126010100", "1126054000": "1126010100", "1126055000": "1126010100",
            "1126056500": "1126010100", "1126057000": "1126010100", "1126057500": "1126010100"}   # 면목 6개 행정동 → 법정동 면목동
RULES = {
    "operateAfterMs": 1200000,     # 깃발을 꽂고 이만큼(가상) 받아내면 운영
    "settleAfterMs": 5400000,      # 운영하고 이만큼 더 받아내면 자리 잡음
    "maxEntering": 2,              # 한꺼번에 깃발 단계일 수 있는 동의 수
    "eventEveryMs": 0,             # 0: 지어낸 사건을 만들지 않는다. 신호는 동별 수요가 올린다
    "escalateAfterMs": 600000,
    "neglectAfterMs": 900000,
    "maxStepMs": 60000,
}


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def gu(lat, lon):
    return (lon - WEST) * MX, (lat - SOUTH) * MY


def centroid(ring):
    a = cx = cy = 0.0
    for i in range(len(ring)):
        x0, y0 = ring[i]
        x1, y1 = ring[(i + 1) % len(ring)]
        c = x0 * y1 - x1 * y0
        a += c
        cx += (x0 + x1) * c
        cy += (y0 + y1) * c
    a *= 0.5
    if abs(a) < 1e-9:
        return sum(p[0] for p in ring) / len(ring), sum(p[1] for p in ring) / len(ring)
    return cx / (6 * a), cy / (6 * a)


def seg_dist(p, a, b):
    vx, vy = b[0] - a[0], b[1] - a[1]
    length = vx * vx + vy * vy
    t = 0.0 if length == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / length))
    return math.hypot(p[0] - a[0] - t * vx, p[1] - a[1] - t * vy)


def ring_gap(r1, r2):
    best = 1e18
    for ring_a, ring_b in ((r1, r2), (r2, r1)):
        for p in ring_a:
            for i in range(len(ring_b)):
                d = seg_dist(p, ring_b[i], ring_b[(i + 1) % len(ring_b)])
                if d < best:
                    best = d
    return best


def inside(ring, p):
    c = False
    j = len(ring) - 1
    for i in range(len(ring)):
        xi, yi = ring[i]
        xj, yj = ring[j]
        if (yi > p[1]) != (yj > p[1]) and p[0] < (xj - xi) * (p[1] - yi) / (yj - yi) + xi:
            c = not c
        j = i
    return c


def main(resources, out_path):
    resources = Path(resources)
    modules_path = resources / "hongdal-dong-modules.json"
    buildings_path = resources / "hongdal-dong-buildings.json"
    roles_path = resources / "hongdal-building-roles.json"
    manifest = json.loads(modules_path.read_text(encoding="utf-8"))
    index = json.loads(buildings_path.read_text(encoding="utf-8"))
    roles = json.loads(roles_path.read_text(encoding="utf-8"))
    lat0, lon0 = index["origin"]["latitude"], index["origin"]["longitude"]
    mlon, mlat = index["metresPerDegreeLongitude"], index["metresPerDegreeLatitude"]

    def module_to_gu(x, z):
        return gu(lat0 + z / mlat, lon0 + x / mlon)

    dongs = []
    for m in manifest["modules"]:
        b = m["boundary"]
        ring = [gu(b[i], b[i + 1]) for i in range(0, len(b), 2)]
        dongs.append({"code": m["code"], "name": m["name"], "district": m["district"], "region": m["regionId"], "ring": ring,
                      "centre": centroid(ring)})
    checks = []

    def check(name, ok, detail=""):
        checks.append({"name": name, "ok": bool(ok), "detail": detail})

    # 맞닿음
    gaps = {}
    for i, a in enumerate(dongs):
        for b in dongs[i + 1:]:
            gaps[(a["code"], b["code"])] = ring_gap(a["ring"], b["ring"])
    near = {d["code"]: [] for d in dongs}
    for (a, b), gap in gaps.items():
        if gap <= NEAR_METRES:
            near[a].append(b)
            near[b].append(a)
    check("모든 동이 적어도 한 동과 맞닿는다", all(near[d["code"]] for d in dongs), json.dumps({k: len(v) for k, v in near.items()}))
    # 한 덩어리인지(어느 동에서 시작해도 모두 닿는지)
    seen, stack = set(), [dongs[0]["code"]]
    while stack:
        c = stack.pop()
        if c in seen:
            continue
        seen.add(c)
        stack.extend(near[c])
    check("맞닿음이 한 덩어리다", len(seen) == len(dongs), f"{len(seen)}/{len(dongs)}")

    # 표식 자리: 넓이 중심에서 가까운 근린생활 건물, 서로 SPOT_GAP 이상 떨어지게
    by_region = {}
    for building in index["buildings"]:
        by_region.setdefault(building["region"], []).append(building)
    for d in dongs:
        cx, cy = d["centre"]
        candidates = []
        for building in by_region.get(d["region"], []):
            if building.get("kind") not in NON_RESIDENTIAL or building.get("name"):
                continue                                        # 이름이 붙은 건물에는 가상 표식을 얹지 않는다
            xz = building["xz"]
            bx = sum(xz[0::2]) / (len(xz) // 2)
            bz = sum(xz[1::2]) / (len(xz) // 2)
            px, py = module_to_gu(bx, bz)
            candidates.append((math.hypot(px - cx, py - cy), px, py))
        candidates.sort()
        spots = []
        for _, px, py in candidates:
            if all(math.hypot(px - sx, py - sy) >= SPOT_GAP for sx, sy in spots):
                spots.append((px, py))
            if len(spots) == SPOTS:
                break
        d["spots"] = spots
    check("동마다 표식 자리가 넷 있다", all(len(d["spots"]) == SPOTS for d in dongs), str([len(d["spots"]) for d in dongs]))
    check("표식 자리는 그 동 경계 안이다", all(inside(d["ring"], s) for d in dongs for s in d["spots"]))

    # 처음부터 운영하는 동: 기존 가상 매장(매장 A·B, 카페 1호점, 도심 창고)이 있는 동
    role_map = {e["from"]: e["to"] for e in manifest.get("roleMap", []) if e.get("to")}
    by_id = {b["id"]: b for b in index["buildings"]}
    kind_of = {"restaurant": "food", "franchise": "cafe", "warehouse": "store"}
    start = {}
    for role in roles["roles"]:
        target = by_id.get(role_map.get(role["buildingId"], ""))
        if target is None:
            continue
        xz = target["xz"]
        px, py = module_to_gu(sum(xz[0::2]) / (len(xz) // 2), sum(xz[1::2]) / (len(xz) // 2))
        code = target["region"].rsplit(":", 1)[-1]
        start.setdefault(code, []).append({"id": "pin:start:" + role["placeId"], "kind": kind_of.get(role["business"], "food"),
                                           "placeId": role["placeId"], "title": role["title"], "x": round(px, 1), "y": round(py, 1)})
    check("기존 가상 매장 넷이 모두 어느 동에 놓였다", sum(len(v) for v in start.values()) == len(roles["roles"]),
          json.dumps({k: [p["title"] for p in v] for k, v in start.items()}, ensure_ascii=False))
    # 음식점(매장 A·B)이 있는 동이 홈이다. 음식 배달 Core의 실제 신호가 이 동을 얼린다
    home = max(start, key=lambda code: sum(1 for p in start[code] if p["kind"] == "food"))
    start_dongs = [{"code": code, "pins": pins} for code, pins in sorted(start.items(), key=lambda kv: kv[0] != home)]
    check("처음 동들은 서로 맞닿는다", all(any(o["code"] in near[s["code"]] for o in start_dongs if o is not s) for s in start_dongs)
          or len(start_dongs) == 1)

    pack = {
        "schema": "hongdal-territory.v1",
        "unit": "hjd",
        "label": "2023 역사 경계 후보 · 내부 검토용 · 배포 아님",
        "note": "영역의 판은 행정동이다. rules는 임시 값. 맞닿음은 경계 사이 %.0f m 안. 표식 자리는 이름 없는 근린생활 건물(가상 표식, 실제 쓰임과 무관)." % NEAR_METRES,
        "sources": {"modules": sha256(modules_path), "buildings": sha256(buildings_path), "roles": sha256(roles_path)},
        "homeDong": home,
        "rules": RULES,
        "homePins": start[home],
        "startDongs": start_dongs,
        "dongs": [{
            "code": d["code"], "name": d["name"], "sig": d["code"][:5], "sigName": d["district"], "legal": LEGAL_OF.get(d["code"], ""),
            "region": d["region"], "x": round(d["centre"][0], 1), "y": round(d["centre"][1], 1), "near": sorted(near[d["code"]]),
            "spots": [round(v, 1) for s in d["spots"] for v in s],
        } for d in dongs],
    }
    check("법정동 대응이 모두 있다", all(d["legal"] for d in pack["dongs"]))
    failed = [c for c in checks if not c["ok"]]
    Path(out_path).write_text(json.dumps(pack, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    report = {"checks": checks, "allChecksPassed": not failed, "gapsMetres": {a + "-" + b: round(g, 1) for (a, b), g in gaps.items()},
              "home": home, "start": [s["code"] for s in start_dongs], "output": str(out_path), "outputSha256": sha256(out_path)}
    print(json.dumps(report, ensure_ascii=False, indent=1))
    return 1 if failed else 0


if __name__ == "__main__":
    res = sys.argv[1]
    sys.exit(main(res, sys.argv[2] if len(sys.argv) > 2 else str(Path(res) / "hongdal-territory-hjd.json")))
