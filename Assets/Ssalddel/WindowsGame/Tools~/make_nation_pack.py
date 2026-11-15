"""전국 단계(N0·N1) 팩을 만든다: 바탕 그림 한 장(hongdal-nation-base.bytes)과 해상·항공 흐름 표(hongdal-nation-space.json).

자료
- 바탕: Natural Earth 10m admin-1 (public domain). 한국·북한·중국·일본·러시아·대만의 시도 경계. 시안 정밀도다(정식 해안선은 수집 필요).
- 흐름: 관세청 국가별 입항/출항물동량 2025.01~12 원본 XML 4개 (data.go.kr 15151805 해상, 15151809 항공).
  BL 건수와 TEU만 쓴다. 중량(blWght)은 원본 설명의 단위가 '통'이라 확정 전까지 넣지 않는다.
  항만명은 원본 그대로다(부산항·부산신항의 포함 관계는 확인 전이라 따로 센다).

손으로 정한 것(표현용, 사실 아님)
- 항만·공항 위치(대략 좌표), 바다 길목, 나라 출입구의 자리와 나라 묶음, 내 화물 회차(가상) 경로.
- 시안(mockups/해상항공-20261113)과 같은 값이다.

좌표
- 세계 좌표 = 시안 화면 좌표. 1280×720 논리 화면에서 위도 1도 = 88점, 경도는 cos 36°만큼 줄이고, 경위도 (127.6, 36.0)이 (600, 337)이다.
- 바탕 그림은 세계 좌표 1점 = 그림 BASE점이다. 게임은 이 그림을 확대·이동해서 쓴다.

검사(굽는 때)
- 해상 흐름의 선이 항만 앞바다(HARBOUR점)와 출입구 근처를 빼면 육지 위를 지나지 않는다.
- 흐름의 월별 값을 모두 더하면 그 항만·출입구에 묶인 원본 행의 합과 같다.

사용: python3 -I make_nation_pack.py <관세청 원본 폴더> <admin1 폴더> <Resources 폴더>
      원본 폴더에는 arrival-sea.xml · departure-sea.xml · arrival-air.xml · departure-air.xml이 있어야 한다.
"""
import json
import math
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from io import BytesIO
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gu_geo  # noqa: E402  (구 지도와 같은 shp/dbf 읽기)

VERSION = "2026.11.13-1"
LON0, LAT0, CX, CY, KY = 127.6, 36.0, 600.0, 337.0, 88.0
KX = KY * math.cos(math.radians(36.0))
BOX = (116.5, 29.5, 137.5, 43.0)          # 서, 남, 동, 북
BASE = 2                                   # 바탕 그림: 세계 1점 = 2점
SUPER = 2                                  # 계단을 줄이려고 두 배로 그린 뒤 줄인다
HARBOUR = 22.0                             # 항만 앞바다(세계 점). 이 안의 섬·곶은 검사에서 뺀다
ISLET = 5                                  # 검사에서 이보다 좁은 섬(세계 점, 약 6km)은 육지로 보지 않는다
STEP = 12.0                                # 흐름 선의 점 사이(세계 점)
KEEP = {"KOR", "PRK", "CHN", "JPN", "RUS", "TWN"}


def P(lon, lat):
    return [round(CX + (lon - LON0) * KX, 2), round(CY - (lat - LAT0) * KY, 2)]


WORLD_X, WORLD_Y = P(BOX[0], BOX[3])
WORLD_RIGHT, WORLD_BOTTOM = P(BOX[2], BOX[1])

# 거점. kind: port / airport. 위치는 대략이다(정식 좌표 대장은 수집 필요). label: 시안의 이름표 자리(dx, dy, 크기).
# coast: 바다 길목을 고르는 묶음. sea: 항만 앞바다의 한 점(경위도). 선은 항만 → 앞바다 → 길목 → 출입구로 간다.
HUBS = [
    ("busan-new", "부산신항", "부산신항", "port", 128.80, 35.07, (-14, 16, 12), "south", (128.80, 34.93), "부산항과 포함 관계 확인 전 · 따로 셈"),
    ("busan", "부산항", "부산항", "port", 129.06, 35.10, (14, 12, 11), "south", (129.15, 34.98), "부산신항과 포함 관계 확인 전 · 따로 셈"),
    ("incheon", "인천항", "인천항", "port", 126.60, 37.45, (16, 6, 11), "west", (126.12, 37.22), ""),
    ("gwangyang", "광양항", "광양항", "port", 127.70, 34.90, (-14, 14, 11), "south", (127.80, 34.62), ""),
    ("pyeongtaek", "평택항", "평택항", "port", 126.83, 36.97, (14, 4, 11), "west", (126.20, 37.02), ""),
    ("ulsan", "울산항", "울산항", "port", 129.39, 35.50, (12, 0, 10), "east", (129.62, 35.42), ""),
    ("gunsan", "군산항", "군산항", "port", 126.62, 35.98, (10, 0, 10), "west", (126.25, 35.92), ""),
    ("yeongilman", "영일만항", "영일만항", "port", 129.44, 36.10, (10, 0, 10), "east", (129.72, 36.12), "포항항·포항신항은 원본에서 따로 셈"),
    ("daesan", "대산항", "대산항", "port", 126.37, 37.00, (-10, 0, 10), "west", (126.05, 37.08), ""),
    ("donghae", "동해항", "동해항", "port", 129.12, 37.49, (10, 0, 10), "eastNorth", (129.38, 37.45), ""),
    ("masan", "마산항", "마산항", "port", 128.57, 35.18, (-10, -4, 10), "south", (128.52, 34.56), ""),
    ("icn", "인천국제공항", "인천공항", "airport", 126.44, 37.46, (-16, -6, 11), "", None, "항공은 BL 건수만 · 컨테이너(TEU) 없음"),
    ("gmp", "김포국제공항", "김포공항", "airport", 126.80, 37.56, (-4, -36, 10), "", None, "항공은 BL 건수만 · 컨테이너(TEU) 없음"),
    ("pus", "김해국제공항", "김해공항", "airport", 128.94, 35.18, (-14, -30, 10), "", None, "항공은 BL 건수만 · 컨테이너(TEU) 없음"),
]

# 나라 출입구. 실제 항로가 아니라 나라 방향의 묶음이다. 자리는 세계 좌표(시안 화면 좌표)다. 일본 상자는 일본 땅 위에 놓인다.
# 오른쪽 셋(러시아·미주·일본)은 시안보다 20점쯤 왼쪽이다. 게임의 오른쪽 카드(논리 x 986~)에 상자가 가리지 않게.
# 나라 이름은 원본 표기 그대로다. 묶지 않는 행: '한국'(뜻 확인 전), '기타국', 몽골과 아주 작은 나라들(TEU 0.04%).
GATES = [
    ("cn", "중국·홍콩·대만", "←", 282, 290, ["중국", "홍콩", "대만", "마카오"]),
    ("sea", "동남아·중동·유럽 등", "↙", 300, 560, [
        # 동남아·남아시아
        "베트남", "태국", "말레이시아", "싱가포르", "인도네시아", "필리핀", "인도", "캄보디아", "미얀마", "방글라데시", "스리랑카",
        "파키스탄", "라오스", "브루나이", "몰디브", "네팔",
        # 중동·중앙아시아
        "사우디아라비아", "아랍에미리트 연합", "이스라엘", "카타르", "이라크", "바레인", "오만", "이란", "요르단",
        "시리아", "레바논", "쿠웨이트", "예맨", "튀르키예", "카자흐스탄", "우즈베키스탄", "아제르바이잔", "조지아", "투르크멘", "키르기스스탄",
        # 유럽
        "독일", "영국", "네덜란드", "이탈리아", "프랑스", "스페인", "벨기에", "폴란드", "슬로베니아", "몰타", "스웨덴", "핀란드",
        "덴마크", "그리스", "라트비아", "노르웨이", "포루투갈", "리투아니아", "에스토니아", "아일랜드", "루마니아", "크로아티아",
        "체코공화국", "몬테네그로", "오스트리아", "불가리아", "룩셈부르그", "우크라이나", "헝가리", "슬로바키아", "스위스",
        "알바니아", "사이프러스", "아이슬란드",
        # 아프리카
        "이집트", "남아프리카공화국", "리비아", "토고", "탄자니아", "가나", "코트디봐르", "모로코", "나이지리아", "모잠비크", "케냐",
        "지부티", "알제리", "세네갈", "베닝", "튀니지", "마다카스카르", "앙골라", "카메룬", "모리셔스", "에티오피아"]),
    ("au", "호주·태평양", "↓", 700, 578, [
        "호주", "뉴질랜드", "괌", "북마리아나 군도", "파푸아 뉴기니", "사모아", "아메리칸 사모아", "피지", "바누아투", "솔로몬 군도",
        "키리바티", "뉴 칼레도니아", "마샬군도", "불령 폴리네시아", "통가", "팔라우", "투발루", "나우루", "마이크로네시아"]),
    ("jp", "일본", "↘", 876, 500, ["일본"]),
    ("am", "미주", "→", 872, 300, [
        "미국", "캐나다", "멕시코", "칠레", "브라질", "파나마", "페루", "콜롬비아", "에쿠아도르", "과테말라", "자마이카",
        "도미니카 공화국", "아르헨티나", "코스타리카", "바하마", "푸에르토리코", "엘살바도르", "온두라스", "니카라과", "우루과이",
        "마티니크", "수리남", "트리니다드 토바고", "베네주엘라", "파라과이", "쿠바", "아이티", "가이아나", "벨리제"]),
    ("ru", "러시아", "↗", 890, 172, ["러시아 연방"]),
]
GATE_LAND = {"jp": 80.0}                   # 출입구가 땅 위에 있으면 그 근처는 검사에서 뺀다(세계 점)

# 육지와 큰 섬을 가로지르지 않게 지나는 바다 길목(경위도). 표현용이며 항로가 아니다.
VIA = {"south": (127.30, 33.85), "farSouthWest": (125.20, 33.95), "strait": (129.75, 34.95), "west": (124.90, 35.70),
       "east": (130.40, 36.60), "eastOff": (129.95, 36.30),
       "channel": (129.20, 34.86)}          # channel: 부산과 대마도 사이
ROUTES = {
    "west": {"cn": [], "sea": ["west"], "am": ["west", "farSouthWest", "south", "strait", "east"],
             "jp": ["west", "farSouthWest", "south", "strait"], "au": ["west", "farSouthWest", "south"],
             "ru": ["west", "farSouthWest", "south", "strait", "east"]},
    "south": {"cn": ["south", "farSouthWest"], "sea": ["south"], "am": ["strait", "east"], "jp": ["strait"], "au": [],
              "ru": ["strait", "east"]},
    "east": {"cn": ["channel", "south", "farSouthWest"], "sea": ["channel", "south"], "am": [], "jp": ["strait"], "au": ["strait"],
             "ru": []},
    "eastNorth": {"cn": ["eastOff", "channel", "south", "farSouthWest"], "sea": ["eastOff", "channel", "south"], "am": [],
                  "jp": ["eastOff", "strait"], "au": ["eastOff", "strait"], "ru": []},
}

NAMES = [("서 해", 124.6, 36.4, 13, "sea"), ("동 해", 130.6, 38.1, 13, "sea"), ("남 해", 127.4, 33.95, 12, "sea"),
         ("대한해협", 129.55, 34.55, 10, "sea"), ("일본", 131.0, 33.0, 11, "country"), ("중국", 121.2, 37.2, 11, "country")]

# 내 화물 회차(가상). r155 전국 시범의 첫 회차 후보: 부산신항 → 수도권 물류센터(가상) → 사가정.
ROUTE = [(128.80, 35.07), (128.3, 35.75), (127.75, 36.55), (127.44, 37.27), (127.09, 37.58)]
SEOUL = (127.0, 37.56)

FILES = [("arrival-sea", "arvlPlcNm", "dptrPlcNm", 0, "sea"), ("departure-sea", "dptrPlcNm", "arvlPlcNm", 1, "sea"),
         ("arrival-air", "arvlPlcNm", "dptrPlcNm", 0, "air"), ("departure-air", "dptrPlcNm", "arvlPlcNm", 1, "air")]


def num(text):
    text = (text or "").strip().replace(",", "")
    return float(text) if text else 0.0


# ── 흐름 표 ──────────────────────────────────────────────────────────────────────

def read_rows(raw):
    """(장소, 상대국, 월 0~11, 방향 0=입 1=출, 방식) → [TEU, BL]"""
    rows = defaultdict(lambda: [0.0, 0.0])
    months = set()
    count = 0
    for name, place_tag, partner_tag, direction, mode in FILES:
        for item in ET.parse(raw / f"{name}.xml").getroot().iter("item"):
            get = lambda tag: (item.findtext(tag) or "").strip()
            month = get("yyyymm").replace(".", "").replace("-", "")      # 원본은 '2025.01' 꼴이다
            if len(month) != 6 or not month.startswith("2025") or not "01" <= month[4:] <= "12":
                raise SystemExit(f"{name}: 2025년 밖의 행 {get('yyyymm')!r}")
            months.add(month)
            index = int(month[4:]) - 1
            cell = rows[(get(place_tag), get(partner_tag), index, direction, mode)]
            cell[0] += num(get("teuGcnt"))
            cell[1] += num(get("blCnt"))
            count += 1
    if len(months) != 12:
        raise SystemExit(f"월이 12개가 아님: {sorted(months)}")
    return rows, sorted(months), count


def monthly_block(cells):
    """[입TEU, 출TEU, 입BL, 출BL] × 12달 → 48개. 월이 바깥 고리다(월 m의 네 값이 m*4..m*4+3)."""
    out = [0.0] * 48
    for (month, direction), (teu, bl) in cells.items():
        out[month * 4 + direction] += teu
        out[month * 4 + 2 + direction] += bl
    return [round(v, 1) for v in out]


def build_tables(rows):
    gate_of = {country: gate for gate, _, _, _, _, countries in GATES for country in countries}
    hub_names = {name for _, name, *_ in HUBS}
    hub_cells = defaultdict(lambda: defaultdict(lambda: [0.0, 0.0]))
    flow_cells = defaultdict(lambda: defaultdict(lambda: [0.0, 0.0]))
    partner_cells = defaultdict(lambda: defaultdict(lambda: [[0.0] * 12, [0.0] * 12]))
    ungrouped = defaultdict(lambda: [0.0, 0.0])
    for (place, partner, month, direction, mode), (teu, bl) in rows.items():
        if place not in hub_names:
            continue
        hub_cells[place][(month, direction)][0] += teu
        hub_cells[place][(month, direction)][1] += bl
        partner_cells[place][partner][0][month] += teu
        partner_cells[place][partner][1][month] += bl
        gate = gate_of.get(partner)
        if gate is None:
            ungrouped[partner][0] += teu
            ungrouped[partner][1] += bl
            continue
        flow_cells[(place, gate)][(month, direction)][0] += teu
        flow_cells[(place, gate)][(month, direction)][1] += bl
    return hub_cells, flow_cells, partner_cells, ungrouped


# ── 흐름 선 모양 ─────────────────────────────────────────────────────────────────

def bezier(p0, p1, p2, p3, t):
    u = 1 - t
    return [u * u * u * p0[k] + 3 * u * u * t * p1[k] + 3 * u * t * t * p2[k] + t * t * t * p3[k] for k in (0, 1)]


def sample(segments):
    """베지어 조각들을 STEP 간격 정도의 점으로 바꾼다."""
    points = [segments[0][0]]
    for p0, p1, p2, p3 in segments:
        length = math.dist(p0, p1) + math.dist(p1, p2) + math.dist(p2, p3)
        steps = max(2, math.ceil(length / STEP))
        for i in range(1, steps + 1):
            points.append(bezier(p0, p1, p2, p3, i / steps))
    return [[round(x, 1), round(y, 1)] for x, y in points]


def smooth(points):
    """경유점을 지나는 부드러운 곡선. 구심 Catmull-Rom(α = 0.5)을 베지어로 바꾼다.
    시안은 균일 Catmull-Rom이었는데, 항만과 앞바다 점처럼 가까운 점 사이에서 고리가 생겨 구심형으로 바꿨다."""
    pts = [points[0], *points, points[-1]]
    segments = []

    def control(p0, p1, p2, d01, d12):
        if d01 < 1e-6:                      # 끝점(같은 점을 두 번 넣음): 다음 점 쪽으로 곧게 나간다
            return [p1[0] + (p2[0] - p1[0]) / 6, p1[1] + (p2[1] - p1[1]) / 6]
        a, b = d01 * d01, d12 * d12
        return [(a * p2[k] - b * p0[k] + (2 * a + 3 * d01 * d12 + b) * p1[k]) / (3 * d01 * (d01 + d12)) for k in (0, 1)]

    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        d01, d12, d23 = (math.dist(p0, p1) ** 0.5, math.dist(p1, p2) ** 0.5, math.dist(p2, p3) ** 0.5)
        segments.append((p1, control(p0, p1, p2, d01, d12), control(p3, p2, p1, d23, d12), p2))
    return segments


def air_arc(a, b):
    """항공: 지도 가운데에서 바깥쪽으로 솟는 아치(r156). 바다 길목 없이 곧장."""
    mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    nx, ny = -dy / length, dx / length
    if nx * (mx - CX) + ny * (my - CY) < 0:
        nx, ny = -nx, -ny
    c = [mx + nx * length * 0.17, my + ny * length * 0.17]
    return [(a, [a[0] + (c[0] - a[0]) * 0.66, a[1] + (c[1] - a[1]) * 0.66], [b[0] + (c[0] - b[0]) * 0.66, b[1] + (c[1] - b[1]) * 0.66], b)]


# ── 바탕 그림 ────────────────────────────────────────────────────────────────────

def hexrgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def read_land(admin_dir):
    base = Path(admin_dir) / "ne_10m_admin_1_states_provinces"
    _, record, count = gu_geo.read_dbf(str(base) + ".dbf", encoding="utf-8")
    want = {}
    for index in range(count):
        row = record(index)
        country = (row.get("adm0_a3") or "").strip("\x00 ")
        if country in KEEP:
            want[index] = country
    regions = []
    for index, parts in gu_geo.read_shapes(str(base) + ".shp", box=BOX):
        if index in want:
            rings = [np.asarray(part, dtype=float) for part in parts if len(part) >= 4]
            if rings:
                regions.append((want[index], rings))
    return regions


def edge_key(a, b):
    a = (round(a[0], 6), round(a[1], 6))
    b = (round(b[0], 6), round(b[1], 6))
    return (a, b) if a <= b else (b, a)


def classify_edges(regions):
    """한국 경계의 선분을 해안선 / 시도 경계 / 나라 경계(북한과 맞닿음)로 나눈다. 같은 선분이 두 번 나오면 맞닿은 경계다."""
    owners = defaultdict(set)
    for number, (country, rings) in enumerate(regions):
        if country not in ("KOR", "PRK"):
            continue
        for ring in rings:
            for i in range(len(ring) - 1):
                owners[edge_key(ring[i], ring[i + 1])].add((country, number))
    def kind(a, b):
        found = owners.get(edge_key(a, b), set())
        countries = {c for c, _ in found}
        if len(found) <= 1:
            return "coast"
        return "border" if countries == {"KOR", "PRK"} else "province"
    return kind


def to_pixels(ring, scale):
    xs = (CX + (ring[:, 0] - LON0) * KX - WORLD_X) * scale
    ys = (CY - (ring[:, 1] - LAT0) * KY - WORLD_Y) * scale
    return list(zip(xs.tolist(), ys.tolist()))


def stroke(image, lines, color, alpha, width):
    mask = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(mask)
    for points in lines:
        if len(points) >= 2:
            draw.line(points, fill=int(round(255 * alpha)), width=max(1, int(round(width))), joint="curve")
    image.paste(color, (0, 0), mask)


def bake_base(regions, width, height):
    scale = BASE * SUPER
    big_w, big_h = width * SUPER, height * SUPER
    top, bottom = np.array(hexrgb("0E2233"), float), np.array(hexrgb("0A1B2A"), float)
    t = np.linspace(0, 1, big_h)[:, None]
    column = (top * (1 - t) + bottom * t).astype(np.uint8)
    image = Image.fromarray(np.repeat(column[:, None, :], big_w, axis=1), "RGB")
    sea = hexrgb("7CC6F0")
    grid = []
    for lon in range(118, 138, 2):
        x = (CX + (lon - LON0) * KX - WORLD_X) * scale
        grid.append([(x, 0), (x, big_h)])
    for lat in range(30, 44, 2):
        y = (CY - (lat - LAT0) * KY - WORLD_Y) * scale
        grid.append([(0, y), (big_w, y)])
    stroke(image, grid, sea, 0.06, scale)

    draw = ImageDraw.Draw(image)
    for country, rings in sorted(regions, key=lambda r: r[0] == "KOR"):
        fill = hexrgb("2A3A46") if country == "KOR" else hexrgb("1C2933")
        for ring in rings:
            draw.polygon(to_pixels(ring, scale), fill=fill)

    kind = classify_edges(regions)
    lines = defaultdict(list)       # 종류 → 이어진 점 목록들
    for country, rings in regions:
        for ring in rings:
            pixels = to_pixels(ring, scale)
            if country != "KOR":
                lines["other"].append(pixels)
                continue
            current, current_kind = [pixels[0]], None
            for i in range(len(ring) - 1):
                k = kind(ring[i], ring[i + 1])
                if k != current_kind and len(current) > 1:
                    lines[current_kind].append(current)
                    current = [pixels[i]]
                current_kind = k
                current.append(pixels[i + 1])
            if len(current) > 1:
                lines[current_kind].append(current)
    stroke(image, lines["other"], hexrgb("ABC1CB"), 0.10, 0.6 * scale)
    stroke(image, lines["province"], hexrgb("ECF5F8"), 0.16, 0.7 * scale)
    stroke(image, lines["border"], hexrgb("ECF5F8"), 0.34, 1.0 * scale)
    stroke(image, lines["coast"], sea, 0.38, 1.2 * scale)
    counts = {k: sum(len(p) for p in v) for k, v in lines.items()}
    return image.resize((width, height), Image.LANCZOS), counts


def land_mask(regions, width, height):
    """세계 1점 = 1칸. 작은 섬은 지운다(선이 작은 섬 위를 지나는 것은 이 축척에서 보이지 않는다)."""
    mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(mask)
    for _, rings in regions:
        for ring in rings:
            draw.polygon(to_pixels(ring, 1), fill=255)
    return np.asarray(mask.filter(ImageFilter.MinFilter(ISLET)))


# ── 만들기 ───────────────────────────────────────────────────────────────────────

def main(raw_dir, admin_dir, out_dir):
    raw, out = Path(raw_dir), Path(out_dir)
    rows, months, count = read_rows(raw)
    hub_cells, flow_cells, partner_cells, ungrouped = build_tables(rows)
    regions = read_land(admin_dir)
    world_w = math.ceil(WORLD_RIGHT - WORLD_X)
    world_h = math.ceil(WORLD_BOTTOM - WORLD_Y)
    mask = land_mask(regions, world_w, world_h)

    def on_land(x, y):
        px, py = int(x - WORLD_X), int(y - WORLD_Y)
        return 0 <= px < world_w and 0 <= py < world_h and mask[py, px] > 127

    hubs, flows, problems = [], [], []
    for hub_id, name, label, kind, lon, lat, (dx, dy, size), coast, offshore, note in HUBS:
        if name not in hub_cells:
            raise SystemExit(f"원본에 없는 거점: {name}")
        teu_year = {p: sum(v[0]) for p, v in partner_cells[name].items()}
        bl_year = {p: sum(v[1]) for p, v in partner_cells[name].items()}
        top = sorted(teu_year, key=lambda p: -teu_year[p])[:10] if kind == "port" else []
        top += [p for p in sorted(bl_year, key=lambda p: -bl_year[p])[:10] if p not in top]
        x, y = P(lon, lat)
        hubs.append({
            "id": hub_id, "name": name, "label": label, "kind": kind, "x": x, "y": y, "approx": True,
            "labelDx": dx, "labelDy": dy, "labelSize": size, "note": note,
            "monthly": monthly_block(hub_cells[name]),
            "partners": [{"name": p, "teu": [round(v, 1) for v in partner_cells[name][p][0]],
                          "bl": [round(v, 1) for v in partner_cells[name][p][1]]} for p in top
                         if teu_year[p] > 0 or bl_year[p] > 0],
        })
        for gate_id, _, _, gx, gy, _ in GATES:
            cells = flow_cells.get((name, gate_id))
            if not cells:
                continue
            start, end = [x, y], [float(gx), float(gy)]
            if kind == "airport":
                path = sample(air_arc(start, end))
            else:
                via = [P(*offshore)] + [P(*VIA[k]) for k in ROUTES[coast][gate_id]]
                path = sample(smooth([start, *via, end]))
                for px, py in path:
                    if math.dist((px, py), start) > HARBOUR and math.dist((px, py), end) > GATE_LAND.get(gate_id, 30.0) and \
                            on_land(px, py):
                        problems.append(f"{name}→{gate_id} ({px:.0f}, {py:.0f})")
            flows.append({"hub": hub_id, "gate": gate_id, "mode": "air" if kind == "airport" else "sea",
                          "monthly": monthly_block(cells), "path": [v for point in path for v in point]})

    # 검사: 흐름의 합 = 묶인 원본 행의 합
    gate_of = {country: gate for gate, _, _, _, _, countries in GATES for country in countries}
    names = {hub[1]: hub[0] for hub in HUBS}
    expect = defaultdict(float)
    for (place, partner, _, _, _), (teu, bl) in rows.items():
        if place in names and partner in gate_of:
            expect[(names[place], gate_of[partner])] += teu + bl
    for flow in flows:
        got = sum(flow["monthly"])
        want = expect[(flow["hub"], flow["gate"])]
        if abs(got - want) > 1.0 + want * 1e-9:
            problems.append(f"합이 다름 {flow['hub']}→{flow['gate']}: {got} ≠ {want}")
    if problems:
        print("검사 실패:", *problems[:20], sep="\n  ")
        raise SystemExit(1)

    centre, home = P(*ROUTE[3]), P(*ROUTE[4])
    route = sample(smooth([P(lon, lat) for lon, lat in ROUTE]))
    space = {
        "schema": "hongdal-nation-space.v1",
        "version": VERSION,
        "source": "관세청 국가별 입항/출항물동량 2025.01~12 (data.go.kr 15151805 해상 · 15151809 항공) · 경계 Natural Earth 10m admin-1",
        "notes": [
            "BL은 운송서류 건수 · 선박·항공기 수가 아님",
            "선은 항만↔나라 묶음의 통계 관계 · 항로·선박·항공기 위치 아님",
            "중량은 원본 단위('통') 확인 전이라 넣지 않음",
            "항만·공항 위치는 대략 · 바다 길목·출입구·나라 묶음은 표현용",
            "그 밖의 항만(감천·경인·포항·통영·여수 등)은 아직 거점으로 그리지 않음",
            "상대국 '한국' 행은 뜻 확인 전이라 출입구에 묶지 않음(거점 합계에는 들어감)",
        ],
        "worldX": WORLD_X, "worldY": WORLD_Y, "worldWidth": world_w, "worldHeight": world_h, "baseScale": BASE,
        "seoulX": P(*SEOUL)[0], "seoulY": P(*SEOUL)[1],
        "months": [f"{m[:4]}.{m[4:]}" for m in months],
        "hubs": hubs,
        "gates": [{"id": g, "label": label, "arrow": arrow, "x": float(x), "y": float(y), "countries": countries}
                  for g, label, arrow, x, y, countries in GATES],
        "flows": flows,
        "route": {"label": "부산신항 → 수도권 물류센터 → 사가정", "note": "가상 · 전국 시범(r155) 후보 · 업무 상태와 연결 전",
                  "path": [v for point in route for v in point], "centreX": centre[0], "centreY": centre[1],
                  "homeX": home[0], "homeY": home[1], "centreLabel": "수도권 물류센터 (가상)", "homeLabel": "사가정 · 내 동네"},
        "names": [{"text": t, "x": P(lon, lat)[0], "y": P(lon, lat)[1], "size": s, "kind": k} for t, lon, lat, s, k in NAMES],
        "ungrouped": [{"name": p, "teu": round(v[0], 1), "bl": round(v[1], 1)}
                      for p, v in sorted(ungrouped.items(), key=lambda kv: -(kv[1][0] + kv[1][1]))[:12]],
    }
    width, height = world_w * BASE, world_h * BASE
    image, edges = bake_base(regions, width, height)
    buffer = BytesIO()
    image.save(buffer, "PNG", optimize=True)
    data = buffer.getvalue()
    (out / "hongdal-nation-base.bytes").write_bytes(data)
    text = json.dumps(space, ensure_ascii=False, separators=(",", ":"))
    (out / "hongdal-nation-space.json").write_text(text, encoding="utf-8")
    print(f"원본 행 {count:,} · 거점 {len(hubs)} · 흐름 {len(flows)} · 바탕 {width}×{height} ({len(data):,} bytes) · 표 {len(text.encode()):,} bytes")
    print("경계 점:", edges)
    print("묶음 밖 상대국:", [(u['name'], u['bl'], u['teu']) for u in space['ungrouped'][:6]])


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    main(*sys.argv[1:4])
