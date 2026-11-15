#!/usr/bin/env python3
"""ITS 표준 노드링크에서 구 지도에 쓸 두 가지를 뽑는다 (2026-11-13, G2).

  1) 서울 밖의 길: 서울시 차선 자료가 없는 곳(구리·하남·남양주)의 바탕 길.
  2) 큰길 이름과 놓을 자리: 길을 따라 일정한 간격으로, 길의 방향에 맞춰.

입력: MOCT_LINK.shp/.dbf (국토교통부 표준노드링크, EPSG:5186), gu.js (구 경계)
출력: roads.json  {outside: [[차로 수, x0, y0, x1, y1, …]], names: [{text, x, y, angle, level}]}
사용: python3 -I prep_gu_roads.py <MOCT_LINK가 있는 폴더> <gu.js가 있는 폴더> <출력 폴더>
"""
import collections, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import gu_geo as g

SRC, MID, OUT = sys.argv[1:4]
text = open(os.path.join(MID, 'gu.js'), encoding='utf-8').read()
G = json.loads(text[text.index('{'):text.rindex('}') + 1])
seoul = [np.array(r, dtype=np.float64).reshape(-1, 2) for s in G['sigs'] if str(s['code']).startswith('11') for r in s['rings']]


def in_seoul(px, py):
    inside = np.zeros(len(px), dtype=bool)
    for ring in seoul:
        ax, ay = ring[:, 0], ring[:, 1]; bx, by = np.roll(ax, 1), np.roll(ay, 1)
        if px.max() < ax.min() or px.min() > ax.max() or py.max() < ay.min() or py.min() > ay.max(): continue
        hit = np.zeros(len(px), dtype=bool)
        for i in range(len(ax)):
            if ay[i] == by[i]: continue
            cross = ((ay[i] > py) != (by[i] > py)) & (px < (bx[i] - ax[i]) * (py - ay[i]) / (by[i] - ay[i]) + ax[i])
            hit ^= cross
        inside |= hit
    return inside


fields, record, count = g.read_dbf(os.path.join(SRC, 'MOCT_LINK.dbf'))
links = []
for index, parts in g.read_shapes(os.path.join(SRC, 'MOCT_LINK.shp'), g.box_in(g.TM_5186)):
    x, y = g.from_5186(parts[0][:, 0], parts[0][:, 1])
    if x.max() < -200 or x.min() > g.WIDTH + 200 or y.max() < -200 or y.min() > g.HEIGHT + 200: continue
    r = record(index)
    links.append(dict(x=x, y=y, name=r['ROAD_NAME'], rank=r['ROAD_RANK'], lanes=int(r['LANES'] or 1), length=float(r['LENGTH'] or 0)))

mid = np.array([[l['x'][len(l['x']) // 2], l['y'][len(l['y']) // 2]] for l in links])
inside = in_seoul(mid[:, 0], mid[:, 1])
outside = [[l['lanes']] + [round(float(v)) for p in zip(l['x'], l['y']) for v in p] for l, s in zip(links, inside) if not s]

# 이름: '길'로 끝나는 골목은 빼고, 길이와 차로 수로 세 단계로 나눈다. 양방향이 따로 들어 있어 길이는 두 배로 잡힌다.
by_name = collections.defaultdict(list)
for l in links:
    n = l['name']
    if not n or n == '-' or n.endswith('길') or n[-1].isdigit(): continue
    by_name[n].append(l)
names = []
for name, group in by_name.items():
    total = sum(l['length'] for l in group) / 2
    lanes = max(l['lanes'] for l in group)
    fast = any(l['rank'] in ('101', '102') for l in group)
    level = 1 if (fast and total >= 3000) or (total >= 5000 and lanes >= 3) else 2 if total >= 2200 and lanes >= 2 else 3 if total >= 1000 else 0
    if level == 0: continue
    spacing = (2200, 1800, 1400)[level - 1]
    # 후보: 범위 안쪽의 곧고 긴 링크 한가운데. 긴 것부터 고르고, 이미 고른 자리와 떨어진 것만 남긴다.
    cands = []
    for l in group:
        x, y = l['x'], l['y']; i = len(x) // 2
        cx, cy = float(x[i]), float(y[i])
        if not (400 < cx < g.WIDTH - 400 and 300 < cy < g.HEIGHT - 300): continue
        dx, dy = float(x[-1] - x[0]), float(y[-1] - y[0]); chord = math.hypot(dx, dy)
        if chord < 60 or chord < 0.9 * l['length']: continue           # 굽은 구간에는 이름을 놓지 않는다
        angle = math.degrees(math.atan2(-dy, dx))                       # 화면은 아래가 +y다
        if angle > 90: angle -= 180
        if angle <= -90: angle += 180
        cands.append((chord, cx, cy, angle))
    cands.sort(reverse=True)
    picked = []
    for chord, cx, cy, angle in cands:
        if all(math.hypot(cx - p[0], cy - p[1]) >= spacing for p in picked): picked.append((cx, cy, angle))
    for cx, cy, angle in picked:
        names.append({'text': name, 'x': round(cx), 'y': round(cy), 'angle': round(angle, 1), 'level': level})

names.sort(key=lambda n: (n['level'], n['text']))
json.dump({'outside': outside, 'names': names}, open(os.path.join(OUT, 'roads.json'), 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
by_level = collections.Counter(n['level'] for n in names)
print('links', len(links), 'outside Seoul', len(outside), 'name anchors', dict(by_level))
print('level 1:', sorted({n['text'] for n in names if n['level'] == 1}))
print('level 2:', sorted({n['text'] for n in names if n['level'] == 2})[:60])
