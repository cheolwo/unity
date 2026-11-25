#!/usr/bin/env python3
"""수도권 바탕 지도(무채색 · 조각) 굽기 — 공개 자료만 (2026-11-25, 사용자 10:44·10:50·10:59:
「지금 구지도 같은 형태에서 전국을 모두 관찰할 수 있도록 … 타일로도 쪼개져 있고 … 수도권에서부터 … Play Store에 올리더라도 문제 없을 정도로」).

자료(모두 재배포 가능 · 출처 표시):
  - 땅 덮개: ESA WorldCover 10m 2021 v200, 타일 N36E126 — CC BY 4.0
  - 지형 음영: Copernicus DEM GLO-30 (N36~N38 · E126~E127) — © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA
  - 길: 국토교통부 표준노드링크 2026-09-14 (data.go.kr 15025526) — 이용허락범위 제한 없음
  - 경계 · 이름: vuski/admdongkor ver20261001 (원본 통계청 SGIS, 공공누리 제1유형 · 추가분 CC BY 4.0)
주소기반산업지원서비스 자료(보안각서)와 내부 자료는 쓰지 않는다.

좌표: 구 지도와 같다 — 남서 기준점(126.94, 37.523)에서 동쪽 x, 북쪽 y(미터). x = (경도-126.94)*88224, y = (위도-37.523)*111320.
출력(<out>/):
  hongdal-cap-0-<i>-<j>.bytes   32 m/점 조각(512점 JPEG)
  hongdal-cap-1-<i>-<j>.bytes    8 m/점 조각(512점 JPEG) — 모두 바다 · 한 빛깔인 조각은 쓰지 않는다(목록에 없음)
  hongdal-capital-space.json     범위 · 단계 · 조각 목록 · 시도 · 시군구 · 행정동 이름 자리 · 출처
i = 열(서→동), j = 행(북→남). 조각 (i, j)의 왼쪽 위 = (x0 + i*512*mpp, y1 - j*512*mpp).
사용: python3 make_capital_base.py <raw 폴더> <출력 폴더>
"""
import glob, json, math, os, sys, time
import numpy as np
import shapefile, tifffile
from PIL import Image, ImageDraw, ImageFilter
from pyproj import Transformer

Image.MAX_IMAGE_PIXELS = None
RAW, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
T0 = time.time()
def log(*a): print(f'[{time.time() - T0:6.1f}s]', *a, flush=True)

# 범위: 서울 · 인천(먼 섬 제외) · 경기
WEST, EAST, SOUTH, NORTH = 126.35, 127.85, 36.89, 38.30
OW, OS = 126.94, 37.523                       # 구 지도 기준점
MX, MY = 88224.0, 111320.0
X0, X1 = (WEST - OW) * MX, (EAST - OW) * MX
Y0, Y1 = (SOUTH - OS) * MY, (NORTH - OS) * MY
TILE = 512
FINE = 8.0                                     # 단계 1: 8 m/점
COARSE = 32.0                                  # 단계 0: 32 m/점
W = int(math.ceil((X1 - X0) / FINE / TILE)) * TILE
H = int(math.ceil((Y1 - Y0) / FINE / TILE)) * TILE
X1 = X0 + W * FINE; Y0 = Y1 - H * FINE       # 조각에 맞춰 동 · 남으로 늘린다
log('canvas', W, H, 'metres', round(X1 - X0), round(Y1 - Y0))

def lonlat_of_cols(cols):  # 점 가운데의 경도
    return OW + (X0 + (np.asarray(cols) + 0.5) * FINE) / MX
def lat_of_rows(rows):
    return OS + (Y1 - (np.asarray(rows) + 0.5) * FINE) / MY

# ── 1) 땅 덮개(WorldCover) ──────────────────────────────────────────────
wc_path = os.path.join(RAW, 'wc_N36E126.tif')
wc = tifffile.imread(wc_path)                   # 36000 x 36000 uint8, 북서 (126, 39), 1/12000도
log('worldcover', wc.shape)
cols = np.clip(((lonlat_of_cols(np.arange(W)) - 126.0) * 12000).astype(np.int64), 0, 35999)
rows = np.clip(((39.0 - lat_of_rows(np.arange(H))) * 12000).astype(np.int64), 0, 35999)
cover = wc[rows[:, None], cols[None, :]]
del wc
log('cover sampled', cover.shape, np.bincount(cover.ravel(), minlength=101)[[0, 10, 20, 30, 40, 50, 60, 80, 90]])

# ── 2) 지형 음영(Copernicus DEM, 32 m 격자에서 셈 → 늘림) ─────────────────
tiles = {}
for f in glob.glob(os.path.join(RAW, 'cop_N*_E*.tif')):
    b = os.path.basename(f)
    lat = int(b[5:7]); lon = int(b[12:15])
    tiles[(lat, lon)] = tifffile.imread(f).astype(np.float32)
CW, CH = W // 4, H // 4
clon = OW + (X0 + (np.arange(CW) + 0.5) * COARSE) / MX
clat = OS + (Y1 - (np.arange(CH) + 0.5) * COARSE) / MY
dem = np.zeros((CH, CW), np.float32)
for (la, lo), arr in tiles.items():
    n = arr.shape[0]; m = arr.shape[1]
    ci = (clon >= lo) & (clon < lo + 1); ri = (clat >= la) & (clat < la + 1)
    if not ci.any() or not ri.any(): continue
    cc = np.clip(((clon[ci] - lo) * m).astype(int), 0, m - 1)
    rr = np.clip(((la + 1 - clat[ri]) * n).astype(int), 0, n - 1)
    dem[np.ix_(ri, ci)] = arr[rr[:, None], cc[None, :]]
dem = np.maximum(dem, 0)
gy, gx = np.gradient(dem, COARSE, COARSE)       # 행은 남쪽으로 → gy는 남쪽 기울기
az, alt = math.radians(315), math.radians(45)
slope = np.arctan(np.hypot(gx, gy) * 1.6)
aspect = np.arctan2(-gx, gy)
shade = np.sin(alt) * np.cos(slope) + np.cos(alt) * np.sin(slope) * np.cos(az - aspect)
shade = np.clip(shade / np.sin(alt), 0, 2) * 128
shade_img = Image.fromarray(np.clip(shade, 0, 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)
shade_u8 = np.asarray(shade_img); del shade_img, shade, dem
log('hillshade')

# ── 3) 바탕 칠하기(무채색) — 띠로 나눠 셈(메모리) ─────────────────────────
TONE = np.full(256, 26.0, np.float32)
TONE[10] = 24.5; TONE[40] = 27.0; TONE[50] = 28.5; TONE[90] = 27.5
grey_u8 = np.empty(cover.shape, np.uint8)
for r0 in range(0, H, 1024):
    c = cover[r0:r0 + 1024]
    g = TONE[c] * np.clip(1.0 + (shade_u8[r0:r0 + 1024].astype(np.float32) - 128.0) / 128.0 * 0.8, 0.62, 1.5)
    g[(c == 80) | (c == 0)] = 30.5
    grey_u8[r0:r0 + 1024] = np.clip(g, 0, 255).astype(np.uint8)
del shade_u8, cover
img = Image.fromarray(grey_u8, 'L').convert('RGB')
del grey_u8
log('painted')

# ── 4) 경계(시도 · 시군구): 번호 칸을 칠하고 이웃과 다른 곳을 선으로 ─────────
gj = json.load(open(os.path.join(RAW, 'admdong.geojson'), encoding='utf-8'))
feats = [f for f in gj['features'] if f['properties']['sido'] in ('11', '28', '41')]
def to_px(lon, lat):
    return ((lon - OW) * MX - X0) / FINE, (Y1 - (lat - OS) * MY) / FINE
sggs = sorted({f['properties']['sgg'] for f in feats})
sgg_index = {s: i + 1 for i, s in enumerate(sggs)}
sido_of = {f['properties']['sgg']: f['properties']['sido'] for f in feats}
ids = Image.new('L', (W, H), 0); dr = ImageDraw.Draw(ids)
dong_out = []
for f in feats:
    p = f['properties']; g = f['geometry']
    polys = g['coordinates'] if g['type'] == 'MultiPolygon' else [g['coordinates']]
    sx = sy = sa = 0.0
    for poly in polys:
        outer = [to_px(lon, lat) for lon, lat in poly[0]]
        dr.polygon(outer, fill=sgg_index[p['sgg']])
        for hole in poly[1:]:
            dr.polygon([to_px(lon, lat) for lon, lat in hole], fill=0)
        a = np.array(poly[0]); x = (a[:, 0] - OW) * MX; y = (a[:, 1] - OS) * MY
        ar = 0.5 * (x[:-1] * y[1:] - x[1:] * y[:-1]).sum()
        if abs(ar) > 0:
            cx = ((x[:-1] + x[1:]) * (x[:-1] * y[1:] - x[1:] * y[:-1])).sum() / (6 * ar)
            cy = ((y[:-1] + y[1:]) * (x[:-1] * y[1:] - x[1:] * y[:-1])).sum() / (6 * ar)
            sx += cx * abs(ar); sy += cy * abs(ar); sa += abs(ar)
    if sa > 0:
        dong_out.append({'code': p['adm_cd2'], 'name': p['adm_nm'].split()[-1], 'sgg': p['sgg'], 'x': round(sx / sa), 'y': round(sy / sa), 'km2': round(sa / 1e6, 2)})
idsa = np.asarray(ids); del ids, dr
edge = np.zeros(idsa.shape, bool)
edge[:, 1:] |= idsa[:, 1:] != idsa[:, :-1]
edge[1:, :] |= idsa[1:, :] != idsa[:-1, :]
sido_arr = np.zeros(256, np.uint8)
for s, i in sgg_index.items(): sido_arr[i] = int(sido_of[s])
sd = sido_arr[idsa]
sido_edge = np.zeros(idsa.shape, bool)
sido_edge[:, 1:] |= sd[:, 1:] != sd[:, :-1]
sido_edge[1:, :] |= sd[1:, :] != sd[:-1, :]
sido_edge = np.asarray(Image.fromarray(sido_edge.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(3))) > 0
land_any = idsa > 0
del idsa, sd
px = np.asarray(img).copy()
px[edge & ~sido_edge] = (44, 44, 47)
px[sido_edge] = (56, 56, 60)
img = Image.fromarray(px); del px, edge, sido_edge
log('boundaries', len(feats), 'dongs', len(sggs), 'sgg')

# ── 5) 길(표준노드링크) ─────────────────────────────────────────────────
tr = Transformer.from_crs('+proj=tmerc +lat_0=38 +lon_0=127 +k=1 +x_0=200000 +y_0=600000 +ellps=GRS80 +units=m +no_defs', 'EPSG:4326', always_xy=True)
inv = Transformer.from_crs('EPSG:4326', '+proj=tmerc +lat_0=38 +lon_0=127 +k=1 +x_0=200000 +y_0=600000 +ellps=GRS80 +units=m +no_defs', always_xy=True)
bx0, by0 = inv.transform(WEST - 0.02, SOUTH - 0.02); bx1, by1 = inv.transform(EAST + 0.02, NORTH + 0.02)
bx0b, _ = inv.transform(WEST - 0.02, NORTH + 0.02); bx1b, _ = inv.transform(EAST + 0.02, SOUTH - 0.02)
bx0, bx1 = min(bx0, bx0b), max(bx1, bx1b)
sf = shapefile.Reader(os.path.join(RAW, 'nl', 'MOCT_LINK'), encoding='cp949')
fi = {f[0]: i for i, f in enumerate(sf.fields[1:])}
ROAD = {'101': (3, 74), '102': (3, 70), '103': (2, 64), '104': (2, 58), '105': (1, 55), '106': (1, 52), '107': (1, 50), '108': (1, 48)}
draw = ImageDraw.Draw(img)
order = []
n = 0
for i, shp in enumerate(sf.iterShapes()):
    b = shp.bbox
    if b[2] < bx0 or b[0] > bx1 or b[3] < by0 or b[1] > by1: continue
    rec = sf.record(i)
    rank = str(rec[fi['ROAD_RANK']]); lanes = int(rec[fi['LANES']] or 1)
    width, tone = ROAD.get(rank, (1, 48))
    if lanes >= 6 and width < 3: width += 1
    pts = np.array(shp.points)
    lon, lat = tr.transform(pts[:, 0], pts[:, 1])
    xy = list(zip(((np.asarray(lon) - OW) * MX - X0) / FINE, (Y1 - (np.asarray(lat) - OS) * MY) / FINE))
    order.append((tone, width, xy)); n += 1
order.sort(key=lambda t: t[0])                  # 작은 길부터 깔고 큰길을 위에
for tone, width, xy in order:
    draw.line(xy, fill=(tone, tone, tone + 2), width=width)
del order, draw
log('roads', n)

# ── 6) 조각으로 자르기 ──────────────────────────────────────────────────
def cut(image, mpp, level):
    w, h = image.size; nx, ny = w // TILE, h // TILE
    kept = []
    for j in range(ny):
        for i in range(nx):
            t = image.crop((i * TILE, j * TILE, (i + 1) * TILE, (j + 1) * TILE))
            a = np.asarray(t)
            if a.max() - a.min() <= 2: continue          # 한 빛깔(바다) → 쓰지 않는다
            t.save(os.path.join(OUT, f'hongdal-cap-{level}-{i}-{j}.bytes'), 'JPEG', quality=80, optimize=True, subsampling=2)
            kept.append([i, j])
    return {'level': level, 'metresPerPixel': mpp, 'tile': TILE, 'columns': nx, 'rows': ny, 'tiles': kept}
levels = [None, None]
levels[1] = cut(img, FINE, 1)
log('level 1', len(levels[1]['tiles']), '/', levels[1]['columns'] * levels[1]['rows'])
small = img.resize((W // 4, H // 4), Image.LANCZOS)
levels[0] = cut(small, COARSE, 0)
log('level 0', len(levels[0]['tiles']))
small.resize((small.size[0] // 4, small.size[1] // 4), Image.LANCZOS).save(os.path.join(OUT, 'preview.png'))

# ── 7) 이름 자리 ────────────────────────────────────────────────────────
sgg_out = []
for s in sggs:
    ds = [d for d in dong_out if d['sgg'] == s]
    a = sum(d['km2'] for d in ds) or 1
    nm = next(f['properties']['sggnm'] for f in feats if f['properties']['sgg'] == s)
    sgg_out.append({'code': s, 'name': nm, 'sido': sido_of[s], 'x': round(sum(d['x'] * d['km2'] for d in ds) / a), 'y': round(sum(d['y'] * d['km2'] for d in ds) / a), 'dongs': len(ds)})
space = {
    'schema': 'hongdal-capital-space.v1', 'revision': 'capital-base-20261125-r1',
    'unit': '수도권(서울 · 인천 · 경기) 바탕 · 무채색 · 조각', 'distribution': '배포 가능(출처 표시)',
    'frame': {'originLongitude': OW, 'originLatitude': OS, 'metresPerDegreeLongitude': MX, 'metresPerDegreeLatitude': MY},
    'west': WEST, 'east': EAST, 'south': SOUTH, 'north': NORTH,
    'x0': round(X0, 1), 'y1': round(Y1, 1), 'width': round(X1 - X0, 1), 'height': round(Y1 - Y0, 1),
    'background': [30, 30, 31], 'levels': levels,
    'sidos': [{'code': '11', 'name': '서울특별시'}, {'code': '28', 'name': '인천광역시'}, {'code': '41', 'name': '경기도'}],
    'sggs': sgg_out, 'dongs': dong_out,
    'attribution': '땅 덮개 © ESA WorldCover 2021 (CC BY 4.0) · 지형 Copernicus DEM GLO-30 © DLR e.V. 2010-2014 · © Airbus Defence and Space GmbH 2014-2018 (EU · ESA Copernicus) · 길 국토교통부 표준노드링크 · 경계 통계청 SGIS(공공누리 제1유형) · vuski/admdongkor (CC BY 4.0)',
    'attributionShort': '© ESA WorldCover CC BY 4.0 · Copernicus DEM · 국토교통부 · 통계청 SGIS · vuski/admdongkor',
    'sources': [
        {'what': '땅 덮개', 'name': 'ESA WorldCover 10m 2021 v200 N36E126', 'url': 'https://esa-worldcover.org', 'terms': 'CC BY 4.0'},
        {'what': '지형 음영', 'name': 'Copernicus DEM GLO-30 (N36~N38, E126~E127)', 'url': 'https://registry.opendata.aws/copernicus-dem/', 'terms': 'Copernicus DEM licence — 무료 · 상업 이용 가능 · 출처 표시'},
        {'what': '길', 'name': '국토교통부 표준노드링크 2026-09-14', 'url': 'https://www.data.go.kr/data/15025526/fileData.do', 'terms': '이용허락범위 제한 없음'},
        {'what': '행정동 경계 · 이름', 'name': 'vuski/admdongkor ver20261001 (통계청 SGIS)', 'url': 'https://github.com/vuski/admdongkor', 'terms': '공공누리 제1유형(출처 표시) · 추가분 CC BY 4.0'},
    ],
}
json.dump(space, open(os.path.join(OUT, 'hongdal-capital-space.json'), 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
total = sum(os.path.getsize(f) for f in glob.glob(os.path.join(OUT, 'hongdal-cap-*.bytes')))
log('done', len(dong_out), 'dongs', len(sgg_out), 'sgg', round(total / 1e6, 1), 'MB')
