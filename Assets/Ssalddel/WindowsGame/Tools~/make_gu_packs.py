#!/usr/bin/env python3
"""구 공간 팩과 영역 팩을 만든다 (2026-11-12 G1 · G3a, 2026-11-13 G2).

차례 (중간 자료 폴더 하나에서 한다)
  1) 구지도-스크립트/prep.py      → gu.js, cover.png        차선·경계·토지피복
  2) prep_gu_roads.py              → roads.json              서울 밖의 길, 큰길 이름 자리 (ITS 표준 노드링크)
  3) prep_gu_relief.py             → shade.png, peaks.json   산의 음영, 봉우리 자리 (서울시 등고선·표고점)
  4) make_gu_packs.py --shade <폴더>  → cover_shaded.png     토지피복에 음영을 입힌다
  5) node bake_gu.js <폴더>        → base_overview.png, base_detail.png   바탕 굽기
  6) make_gu_packs.py <폴더> <Resources>  → 아래 출력

출력 (Resources/)
  hongdal-gu-base.bytes         색 바탕 JPEG. 세 구 범위, 1점 = 2.5m. 법정동 모양으로 오려 내 영역에 쓴다
  hongdal-gu-base-grey.bytes    무채색 바탕 JPEG. 전체 범위, 가로 4096점 (미지의 땅)
  hongdal-gu-detail-grey.bytes  무채색 바탕 JPEG. 세 구 범위, 1점 = 2.5m (가까이 볼 때)
  hongdal-gu-space.json         화면이 읽는 공간 팩: 범위, 법정동·구 경계, 이름 자리, 큰길 이름
  hongdal-territory.json        커널이 읽는 영역 팩: 인접 표, 표식 자리, 임시 규칙

좌표: 범위의 남서쪽 모서리에서 동쪽 x, 북쪽 y (미터). x = (경도 - west) * 88224, y = (위도 - south) * 111320.
"""
import json, math, os, sys
from PIL import Image, ImageEnhance

if sys.argv[1] == '--shade':
    # 토지피복 색에 산의 음영을 곱한다. 길과 경계는 그 위에 굽기 때문에 어두워지지 않는다.
    import numpy as np
    folder = sys.argv[2]
    flat = Image.open(os.path.join(folder, 'cover.png')).convert('RGB')
    shade = Image.open(os.path.join(folder, 'shade.png')).resize(flat.size, Image.BILINEAR)
    gain = np.clip(1.0 + (np.asarray(shade, dtype=np.float32) - 128.0) / 128.0 * 0.8, 0.62, 1.5)
    Image.fromarray(np.clip(np.asarray(flat, dtype=np.float32) * gain[..., None], 0, 255).astype(np.uint8)).save(os.path.join(folder, 'cover_shaded.png'))
    print('cover_shaded.png', flat.size)
    sys.exit(0)

SRC, OUT = sys.argv[1], sys.argv[2]
DETAIL = {'x': 7100, 'y': 0, 'width': 8850, 'height': 11115}      # bake_gu.js의 DETAIL과 같아야 한다
WEST, EAST, SOUTH, NORTH = 126.940, 127.175, 37.523, 37.627
MX, MY = 88224.0, 111320.0
FOCUS = {'11260': '중랑구', '11230': '동대문구', '11215': '광진구'}
HOME_DONG, HOME_NAME = '1126010100', '면목동'
NEAR = {'중곡동': ['능동', '구의동', '장안동', '면목동'], '군자동': ['능동', '화양동'], '광장동': ['구의동'], '능동': ['중곡동', '군자동', '화양동', '구의동'],
        '화양동': ['군자동', '능동', '구의동', '자양동'], '구의동': ['중곡동', '광장동', '능동', '화양동', '자양동'], '자양동': ['화양동', '구의동'],
        '회기동': ['전농동', '이문동', '휘경동', '청량리동'], '전농동': ['답십리동', '휘경동', '청량리동', '용두동', '장안동'], '답십리동': ['전농동', '용두동', '장안동'],
        '제기동': ['청량리동', '용두동'], '이문동': ['회기동', '휘경동', '중화동'], '휘경동': ['회기동', '전농동', '이문동', '장안동', '면목동'],
        '청량리동': ['회기동', '전농동', '제기동', '휘경동'], '용두동': ['전농동', '답십리동', '제기동', '신설동'], '신설동': ['용두동'],
        '장안동': ['중곡동', '전농동', '답십리동', '면목동'], '신내동': ['망우동', '묵동', '상봉동'], '망우동': ['신내동', '면목동', '상봉동'],
        '면목동': ['중곡동', '휘경동', '장안동', '망우동', '상봉동'], '묵동': ['신내동', '상봉동', '중화동'],
        '상봉동': ['휘경동', '신내동', '망우동', '면목동', '묵동', '중화동'], '중화동': ['이문동', '묵동', '상봉동']}   # 경계를 공유하는 동 (prep 단계에서 계산)
PEAKS = [('용마산', 37.5725, 127.0955), ('아차산', 37.5535, 127.1030), ('망우산', 37.5930, 127.1090), ('봉화산', 37.6075, 127.0870), ('배봉산', 37.5800, 127.0625)]   # 이름은 손으로 붙였다
WATERS = [('중랑천', 37.5890, 127.0712, -76.6), ('한 강', 37.5612, 127.1215, -41.3)]   # 손으로 놓고 토지피복의 물 칸으로 확인한 자리
RULES = {'operateAfterMs': 1200000, 'settleAfterMs': 5400000, 'maxEntering': 1, 'eventEveryMs': 1500000,
         'escalateAfterMs': 600000, 'neglectAfterMs': 900000, 'maxStepMs': 60000}

text = open(os.path.join(SRC, 'gu.js'), encoding='utf-8').read()
G = json.loads(text[text.index('{'):text.rindex('}') + 1])
metres = lambda lat, lon: ((lon - WEST) * MX, (lat - SOUTH) * MY)
cover = Image.open(os.path.join(SRC, 'cover.png')).convert('RGB'); cw, ch = cover.size; px = cover.load()

def built(x, y):   # 토지피복이 시가지(50)인 칸인가
    i, j = int(x / G['w'] * cw), int((G['h'] - y) / G['h'] * ch)
    if i < 0 or j < 0 or i >= cw or j >= ch: return False
    r, g, b = px[i, j]; return abs(r - 38) < 7 and abs(g - 50) < 9 and abs(b - 60) < 9

def inside(rings, x, y):
    c = False
    for r in rings:
        n = len(r); j = n - 2
        for i in range(0, n, 2):
            ax, ay, bx, by = r[i], r[i + 1], r[j], r[j + 1]
            if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax: c = not c
            j = i
    return c

def halton(i, b):
    f, r = 1.0, 0.0
    while i > 0:
        f /= b; r += f * (i % b); i //= b
    return r

def simplify(ring, tolerance):   # Douglas-Peucker. ring = [x0, y0, x1, y1, ...]
    pts = [(ring[i], ring[i + 1]) for i in range(0, len(ring), 2)]
    far = max(range(len(pts)), key=lambda i: math.hypot(pts[i][0] - pts[0][0], pts[i][1] - pts[0][1]))   # 닫힌 고리는 가장 먼 점에서 둘로 나눈다
    keep = [False] * len(pts); keep[0] = keep[-1] = keep[far] = True; stack = [(0, far), (far, len(pts) - 1)]
    while stack:
        a, b = stack.pop(); (ax, ay), (bx, by) = pts[a], pts[b]; best, where = 0.0, -1
        length = math.hypot(bx - ax, by - ay) or 1e-9
        for i in range(a + 1, b):
            d = abs((bx - ax) * (ay - pts[i][1]) - (ax - pts[i][0]) * (by - ay)) / length
            if d > best: best, where = d, i
        if best > tolerance: keep[where] = True; stack += [(a, where), (where, b)]
    return [round(v) for p, k in zip(pts, keep) if k for v in p]

code_of = lambda d: str(d['code']) + '00'   # 법정동 코드 10자리 (시군구 5 + 읍면동 3 + 리 2)
by_name = {d['name']: d for d in G['dongs']}
for d in G['dongs']: d['full'] = code_of(d); assert len(d['full']) == 10, d['full']
assert by_name[HOME_NAME]['full'] == HOME_DONG, by_name[HOME_NAME]['full']

# 홈 동의 실제 자리: 건물 역할표의 건물 무게중심
roles = json.load(open(os.path.join(OUT, 'hongdal-building-roles.json'), encoding='utf-8'))
index = json.load(open(os.path.join(OUT, 'hongdal-buildings.json'), encoding='utf-8'))
origin, mlon, mlat = index['origin'], index['metresPerDegreeLongitude'], index['metresPerDegreeLatitude']
buildings = {b['id']: b for b in index['buildings']}
kind_of = {'restaurant': 'food', 'franchise': 'cafe', 'warehouse': 'store'}
home = [round(v) for v in G['home']]
home_pins = [{'id': 'pin:home:branch', 'kind': 'branch', 'placeId': '', 'title': '사가정 지사', 'x': home[0], 'y': home[1]}]
for role in roles['roles']:
    xz = buildings[role['buildingId']]['xz']; ex, nz = sum(xz[0::2]) / (len(xz) / 2), sum(xz[1::2]) / (len(xz) / 2)
    x, y = metres(origin['latitude'] + nz / mlat, origin['longitude'] + ex / mlon)
    home_pins.append({'id': 'pin:home:' + role['placeId'], 'kind': kind_of[role['business']], 'placeId': role['placeId'], 'title': role['title'], 'x': round(x), 'y': round(y)})

FAR_SCALE = 0.062      # 가장 멀리 축소했을 때 1m가 화면에서 차지하는 점(1280점에 20.7km)

def clear_of_pins(x, y, pins, width, height):
    # 표식은 꼭짓점에서 위로 33점, 좌우 14점을 차지한다. 이름표(width × height)가 그 네모와 겹치지 않아야 한다.
    for px_, py_ in pins:
        dx, dy = abs(x - px_) * FAR_SCALE, (y - py_) * FAR_SCALE       # dy > 0: 이름표가 표식보다 위(북쪽)
        if dx < width / 2 + 16 and -height / 2 - 4 < dy < 33 + height / 2 + 3: return False
    return True

def clear_of_labels(x, y, points, width, height):
    for lx, ly, lw in points:
        if abs(x - lx) * FAR_SCALE < (width + lw) / 2 + 6 and abs(y - ly) * FAR_SCALE < height / 2 + 14: return False
    return True

# 표식 자리: 시가지 칸, 동 안쪽, 서로 떨어지게. 작은 동은 조건을 풀어 가며 채운다
spots, labels = {}, {}
for d in G['dongs']:
    xs, ys = d['rings'][0][0::2], d['rings'][0][1::2]; x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    lst = [home[:]] if d['name'] == HOME_NAME else []
    for edge, sep, cdist in ((200, 460, 330), (120, 360, 260), (70, 260, 200), (40, 180, 150)):
        for i in range(1, 4000):
            if len(lst) >= 6: break
            x = x0 + (x1 - x0) * halton(i + ord(d['name'][0]) % 7, 2); y = y0 + (y1 - y0) * halton(i + ord(d['name'][1]) % 5, 3)
            ok = all(built(x + dx, y + dy) for dx, dy in ((0, 0), (90, 0), (-90, 0), (0, 90), (0, -90))) and \
                all(inside(d['rings'], x + dx, y + dy) for dx, dy in ((0, 0), (edge, 0), (-edge, 0), (0, edge), (0, -edge)))
            if ok and math.hypot(x - d['c'][0], y - d['c'][1]) > cdist and all(math.hypot(p[0] - x, p[1] - y) > sep for p in lst): lst.append([round(x), round(y)])
    spots[d['name']] = [[round(p[0]), round(p[1])] for p in lst]
    # 이름 자리: 무게중심에서 가장 가까우면서, 가장 멀리 축소했을 때에도 이름표가 표식을 가리지 않는 곳
    # 한 동에 꽂히는 표식은 많아야 넷이다(진출 1 · 운영 1~2 · 자리 잡음 1). 자리가 없으면 앞의 둘만 피한다.
    best, score = d['c'], 1e18
    for used in (4, 2):
        avoid = spots[d['name']][:used] + ([[p['x'], p['y']] for p in home_pins] if d['name'] == HOME_NAME else [])
        for gx in range(-1200, 1201, 60):
            for gy in range(-1200, 1201, 60):
                x, y = d['c'][0] + gx, d['c'][1] + gy
                if not inside(d['rings'], x, y) or not clear_of_pins(x, y, avoid, len(d['name']) * 12 + 14, 21): continue
                s = math.hypot(gx, gy) - (60 if gy < 0 else 0)      # 같은 값이면 표식의 아래(남쪽)를 고른다
                if s < score: best, score = (x, y), s
        if score < 1e18: break
    labels[d['name']] = [round(best[0]), round(best[1])]

# 구 이름 자리: 구 안에서 동 이름·표식과 가장 먼 곳
all_pins = [p for d in G['dongs'] for p in spots[d['name']][:4]] + [[p['x'], p['y']] for p in home_pins]
dong_labels = [(labels[d['name']][0], labels[d['name']][1], len(d['name']) * 12 + 14) for d in G['dongs']]
sig_label = {}
for g in G['sigs']:
    best, score = g['c'], 1e18
    if g['code'] in FOCUS:
        width = len(g['name']) * 17 + 14
        for strict in (True, False):       # 자리가 없으면 표식은 덮어도 동 이름은 덮지 않는다
            for gx in range(-2400, 2401, 100):
                for gy in range(-2400, 2401, 100):
                    x, y = g['c'][0] + gx, g['c'][1] + gy
                    if not inside(g['rings'], x, y) or not (800 < x < G['w'] - 800 and 500 < y < G['h'] - 500): continue
                    if strict and not clear_of_pins(x, y, all_pins, width, 26): continue
                    if not clear_of_labels(x, y, dong_labels, width, 26): continue
                    s = math.hypot(gx, gy)
                    if s < score: best, score = (x, y), s
            if score < 1e18: break
    sig_label[g['code']] = [round(best[0]), round(best[1])]

# 봉우리 자리는 등고선에서 찾은 가장 높은 곳(peaks.json), 큰길 이름은 노드링크(roads.json). 없으면 손으로 놓은 자리만 쓴다.
peaks_path, roads_path = os.path.join(SRC, 'peaks.json'), os.path.join(SRC, 'roads.json')
peak_names = [{'text': p['text'], 'kind': 'peak', 'x': p['x'], 'y': p['y'], 'angle': 0} for p in json.load(open(peaks_path, encoding='utf-8'))] \
    if os.path.exists(peaks_path) else [{'text': n, 'kind': 'peak', 'x': round(metres(la, lo)[0]), 'y': round(metres(la, lo)[1]), 'angle': 0} for n, la, lo in PEAKS]
road_names = json.load(open(roads_path, encoding='utf-8'))['names'] if os.path.exists(roads_path) else []

space = {
    'schema': 'hongdal-gu-space.v2', 'unit': '서울 동북 3구(중랑·동대문·광진)와 주변 · 20.7km × 11.6km',
    'west': WEST, 'east': EAST, 'south': SOUTH, 'north': NORTH, 'width': G['w'], 'height': G['h'], 'detail': DETAIL,
    'dongs': [{'code': d['full'], 'name': d['name'], 'sig': d['sig'], 'x': labels[d['name']][0], 'y': labels[d['name']][1],
               'rings': [{'p': [round(v) for v in r]} for r in d['rings']]} for d in G['dongs']],
    'sigs': [{'code': g['code'], 'name': g['name'], 'x': sig_label[g['code']][0], 'y': sig_label[g['code']][1], 'focus': g['code'] in FOCUS,
              'rings': [{'p': simplify(r, 18)} for r in g['rings']] if g['code'] in FOCUS else []} for g in G['sigs']
             if 300 < g['c'][0] < G['w'] - 300 and 300 < g['c'][1] < G['h'] - 300],
    'names': peak_names +
             [{'text': n, 'kind': 'water', 'x': round(metres(la, lo)[0]), 'y': round(metres(la, lo)[1]), 'angle': a} for n, la, lo, a in WATERS],
    'roads': road_names,
}
territory = {
    'schema': 'hongdal-territory.v1',
    'note': '영역 규칙은 임시 값이다(Claude 초안, 2026-11-12). 기획 판본이 정하면 rules만 바꾼다. 시간은 가상 시각(ms)이다.',
    'homeDong': HOME_DONG, 'rules': RULES, 'homePins': home_pins,
    'dongs': [{'code': d['full'], 'name': d['name'], 'sig': d['sig'], 'sigName': FOCUS[d['sig']], 'x': labels[d['name']][0], 'y': labels[d['name']][1],
               'near': [by_name[n]['full'] for n in NEAR[d['name']]], 'spots': [v for p in spots[d['name']] for v in p]} for d in G['dongs']],
}
dump = lambda name, value: open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n').write(json.dumps(value, ensure_ascii=False, separators=(',', ':')))
dump('hongdal-gu-space.json', space); dump('hongdal-territory.json', territory)

# 바탕 그림: 색 판은 조금 밝게(내 영역), 무채색 판은 어둡게(미지)
Image.MAX_IMAGE_PIXELS = None
dim = lambda image: image.convert('L').point(lambda v: int(v * 0.331 + 10.2)).convert('RGB')
overview = Image.open(os.path.join(SRC, 'base_overview.png')).convert('RGB')
detail = Image.open(os.path.join(SRC, 'base_detail.png')).convert('RGB')
assert abs(detail.width / DETAIL['width'] - detail.height / DETAIL['height']) < 0.001, '상세 바탕의 범위가 DETAIL과 다릅니다'
ImageEnhance.Brightness(ImageEnhance.Color(detail).enhance(0.95)).enhance(1.3).save(os.path.join(OUT, 'hongdal-gu-base.bytes'), 'JPEG', quality=88, optimize=True)
dim(overview).save(os.path.join(OUT, 'hongdal-gu-base-grey.bytes'), 'JPEG', quality=85, optimize=True)
dim(detail).save(os.path.join(OUT, 'hongdal-gu-detail-grey.bytes'), 'JPEG', quality=85, optimize=True)
raw = detail

area = sum((max(d['rings'][0][0::2]) - min(d['rings'][0][0::2])) * (max(d['rings'][0][1::2]) - min(d['rings'][0][1::2])) for d in G['dongs']) * (detail.width / DETAIL['width']) ** 2
print('dongs', len(space['dongs']), 'sigs', len(space['sigs']), 'spots', {k: len(v) for k, v in spots.items() if len(v) < 6}, 'home pins', [(p['title'], p['x'], p['y']) for p in home_pins])
print('labels', labels[HOME_NAME], {FOCUS[c]: sig_label[c] for c in FOCUS}, 'sig points', sum(len(r['p']) // 2 for g in space['sigs'] for r in g['rings']))
print('dong fill pixels (bbox sum)', round(area / 1e6, 2), 'M', {n: os.path.getsize(os.path.join(OUT, n)) for n in ('hongdal-gu-base.bytes', 'hongdal-gu-base-grey.bytes', 'hongdal-gu-detail-grey.bytes', 'hongdal-gu-space.json', 'hongdal-territory.json')})
