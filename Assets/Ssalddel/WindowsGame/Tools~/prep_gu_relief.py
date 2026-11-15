#!/usr/bin/env python3
"""서울시 등고선·표고점에서 산의 음영을 만든다 (2026-11-13, G2).

입력: N3L_F001.shp/.dbf (등고선, CONT = 높이), N3P_F002.shp/.dbf (표고점, NUME = 높이) — 서울 열린데이터광장 OA-22241, EPSG:5174
      gu.js (구 경계 — 서울 안쪽만 음영을 넣는다)
출력: shade.png   범위 전체의 음영(10m 격자, 128 = 평지, 밝을수록 빛을 받는 비탈)
      peaks.json  이름을 아는 산마다 그 근처에서 가장 높은 곳의 자리 (이름은 손으로 붙였다)
서울 밖(구리·하남·남양주)은 자료가 없어 평지로 남는다. 경계가 능선인 곳은 서울 쪽 비탈만 음영이 든다.
사용: python3 -I prep_gu_relief.py <shp가 있는 폴더> <gu.js가 있는 폴더> <출력 폴더>
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image, ImageDraw
from scipy.interpolate import LinearNDInterpolator
from scipy.ndimage import gaussian_filter
from scipy.spatial import cKDTree
import gu_geo as g

SRC, MID, OUT = sys.argv[1:4]
CELL = 10.0
PEAKS = [('용마산', 37.5725, 127.0955), ('아차산', 37.5535, 127.1030), ('망우산', 37.5930, 127.1090), ('봉화산', 37.6075, 127.0870), ('배봉산', 37.5800, 127.0625)]
box = g.box_in(g.TM_5174, 2500.0)

xs, ys, zs = [], [], []
fields, record, count = g.read_dbf(os.path.join(SRC, 'N3L_F001.dbf'))
for index, parts in g.read_shapes(os.path.join(SRC, 'N3L_F001.shp'), box):
    height = float(record(index)['CONT'] or 0)
    for part in parts:
        x, y = g.from_5174(part[:, 0], part[:, 1])
        keep = (x > -600) & (x < g.WIDTH + 600) & (y > -600) & (y < g.HEIGHT + 600)
        if not keep.any(): continue
        # 꼭짓점을 12m쯤마다 하나씩만 남긴다
        along = np.concatenate([[0.0], np.cumsum(np.hypot(np.diff(x), np.diff(y)))])
        step = np.concatenate([[True], np.diff(np.floor(along / 12.0)) > 0])
        take = keep & step
        xs.append(x[take]); ys.append(y[take]); zs.append(np.full(int(take.sum()), height))
contour_points = sum(len(a) for a in xs)
spots = []
fields, record, count = g.read_dbf(os.path.join(SRC, 'N3P_F002.dbf'))
for index, parts in g.read_shapes(os.path.join(SRC, 'N3P_F002.shp'), box):
    x, y = g.from_5174(parts[0][:, 0], parts[0][:, 1])
    if -600 < x[0] < g.WIDTH + 600 and -600 < y[0] < g.HEIGHT + 600:
        height = float(record(index)['NUME'] or 0)
        spots.append((float(x[0]), float(y[0]), height))
        xs.append(x); ys.append(y); zs.append(np.array([height]))
px, py, pz = np.concatenate(xs), np.concatenate(ys), np.concatenate(zs)
print('contour points', contour_points, 'spot heights', len(spots), 'height range', pz.min(), pz.max())

cols, rows = int(round(g.WIDTH / CELL)), int(round(g.HEIGHT / CELL))
gx, gy = np.meshgrid((np.arange(cols) + 0.5) * CELL, (rows - np.arange(rows) - 0.5) * CELL)      # 줄 0이 북쪽
dem = LinearNDInterpolator(np.column_stack([px, py]), pz)(gx, gy)
far = cKDTree(np.column_stack([px, py])).query(np.column_stack([gx.ravel(), gy.ravel()]), k=1)[0].reshape(rows, cols) > 350.0
# 서울 안쪽만 쓴다. 바깥은 자료가 없어 삼각망이 엉뚱한 비탈을 만든다.
text = open(os.path.join(MID, 'gu.js'), encoding='utf-8').read()
G = json.loads(text[text.index('{'):text.rindex('}') + 1])
mask = Image.new('L', (cols, rows), 0); pen = ImageDraw.Draw(mask)
for sig in G['sigs']:
    if not str(sig['code']).startswith('11'): continue
    for ring in sig['rings']:
        pen.polygon([(ring[i] / CELL, rows - ring[i + 1] / CELL) for i in range(0, len(ring), 2)], fill=255)
seoul = np.asarray(mask) > 0
dem[far | np.isnan(dem) | ~seoul] = np.nan
valid = ~np.isnan(dem)
filled = np.where(valid, dem, 0.0)
# 자료가 없는 칸을 건너뛰고 부드럽게 한다 (가중 평균)
weight = gaussian_filter(valid.astype(np.float64), 1.2); smooth = gaussian_filter(filled, 1.2) / np.maximum(weight, 1e-6)
dzdy, dzdx = np.gradient(smooth, CELL)       # 줄이 아래로 갈수록 남쪽이라 북쪽 기울기는 -dzdy
dzdn = -dzdy
z = 1.5                                      # 낮은 산이 많아 기복을 조금 키운다
slope = np.arctan(z * np.hypot(dzdx, dzdn)); aspect = np.arctan2(dzdx, dzdn)      # 내리막이 아니라 오르막의 방위
azimuth, altitude = math.radians(315.0), math.radians(45.0)
shade = np.sin(altitude) * np.cos(slope) + np.cos(altitude) * np.sin(slope) * np.cos(azimuth - aspect - math.pi)
value = 128 + np.clip((shade - math.sin(altitude)) * 210, -84, 84)
edge = gaussian_filter(valid.astype(np.float64), 4.0)          # 자료 가장자리에서는 음영을 서서히 뺀다
value = 128 + (value - 128) * np.clip((edge - 0.5) * 2.2, 0, 1)
Image.fromarray(np.clip(value, 0, 255).astype(np.uint8), 'L').save(os.path.join(OUT, 'shade.png'))

peaks = []
for name, lat, lon in PEAKS:
    x0, y0 = (lon - g.WEST) * g.MX, (lat - g.SOUTH) * g.MY
    # 손으로 놓은 자리에서 500m 안의 가장 높은 칸으로 옮긴다. 높이는 지도에 적지 않는다(등고선 간격만큼 낮게 나온다).
    reach = (np.hypot(gx - x0, gy - y0) < 500) & valid
    if not reach.any():
        peaks.append({'text': name, 'x': round(x0), 'y': round(y0), 'height': 0}); continue
    at = np.argmax(np.where(reach, smooth, -1e9))
    peaks.append({'text': name, 'x': round(float(gx.ravel()[at])), 'y': round(float(gy.ravel()[at])), 'height': round(float(smooth.ravel()[at]))})
json.dump(peaks, open(os.path.join(OUT, 'peaks.json'), 'w', encoding='utf-8'), ensure_ascii=False)
print('grid', cols, rows, 'valid share', round(float(valid.mean()), 3), 'shade range', int(np.nanmin(value)), int(np.nanmax(value)))
print('peaks', [(p['text'], p['height'], p['x'], p['y']) for p in peaks])
