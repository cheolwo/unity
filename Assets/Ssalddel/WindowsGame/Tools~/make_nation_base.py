"""전국 바탕 지도(무채색 · 조각) 굽기 — 공개 자료만 (2026-11-25, 사용자 13:13 「아까 하던 거 계속 … 며칠씩 걸린다는 거」).

수도권 바탕(make_capital_base.py)을 전국으로 넓힌 것. 띠(조각 한 줄, 4,096 m)마다 셈해 메모리를 아낀다.
  - 땅 덮개: ESA WorldCover 10m 2021 v200 (N33E123 · N33E126 · N33E129 · N36E123 · N36E126 · N36E129) — CC BY 4.0
  - 지형 음영: Copernicus DEM GLO-30 (N33~N38 · E124~E131) — 출처 표시
  - 길: 국토교통부 표준노드링크 2026-09-14 — 이용허락범위 제한 없음
  - 경계 · 이름: vuski/admdongkor ver20261001 (통계청 SGIS 공공누리 제1유형 · 추가분 CC BY 4.0)
대한민국 밖의 땅(북한 · 일본 · 중국)은 어둡게 눌러 둔다(지도는 남쪽 행정동 경계 안이 주인공).
좌표: 구 지도와 같다 — x = (경도-126.94)*88224, y = (위도-37.523)*111320 (미터).
사용: python3 make_nation_base.py <raw 폴더(wc_*.npy · cop_*.tif · nl/MOCT_LINK · admdong.geojson)> <결과 폴더>
"""
import sys, os, json, math, glob, time
import numpy as np
import tifffile, shapefile
from PIL import Image, ImageDraw, ImageFilter
from pyproj import Transformer

RAW, OUT = sys.argv[1:3]
START = int(sys.argv[3]) if len(sys.argv) > 3 else 0      # 이 띠부터(앞 띠의 조각은 그대로 둔다)
os.makedirs(OUT, exist_ok=True)
T0 = time.time()
def log(*a): print(f'[{time.time() - T0:7.1f}s]', *a, flush=True)

WEST, EAST, SOUTH, NORTH = 124.55, 131.90, 33.08, 38.65
OW, OS, MX, MY = 126.94, 37.523, 88224.0, 111320.0
TILE, FINE, COARSE = 512, 8.0, 32.0
X0, Y1 = (WEST - OW) * MX, (NORTH - OS) * MY
W = int(math.ceil(((EAST - OW) * MX - X0) / FINE / TILE)) * TILE
H = int(math.ceil((Y1 - (SOUTH - OS) * MY) / FINE / TILE)) * TILE
NX, NY = W // TILE, H // TILE
log('canvas', W, H, 'tiles', NX, NY)

def lon_of_cols(c): return OW + (X0 + (np.asarray(c) + 0.5) * FINE) / MX
def lat_of_rows(r): return OS + (Y1 - (np.asarray(r) + 0.5) * FINE) / MY
def to_px(lon, lat): return ((np.asarray(lon) - OW) * MX - X0) / FINE, (Y1 - (np.asarray(lat) - OS) * MY) / FINE

# WorldCover: 타일마다 .npy(memmap)
WC = {}
for f in glob.glob(os.path.join(RAW, 'wc_N*E*.npy')):
    b = os.path.basename(f)[3:-4]; WC[(int(b[1:3]), int(b[4:7]))] = np.load(f, mmap_mode='r')
log('worldcover tiles', sorted(WC))
COLS = lon_of_cols(np.arange(W))

# DEM: 필요할 때 읽고 몇 장만 들고 있는다
DEM_FILES = {}
for f in glob.glob(os.path.join(RAW, 'cop_N*_E*.tif')):
    b = os.path.basename(f); DEM_FILES[(int(b[5:7]), int(b[9:12]))] = f
dem_cache = {}
def dem_tile(la, lo):
    k = (la, lo)
    if k not in DEM_FILES: return None
    if k not in dem_cache:
        if len(dem_cache) > 14: dem_cache.pop(next(iter(dem_cache)))
        dem_cache[k] = tifffile.imread(DEM_FILES[k]).astype(np.float32)
    return dem_cache[k]

# 경계: 동마다 픽셀 다각형 · 범위
gj = json.load(open(os.path.join(RAW, 'admdong.geojson'), encoding='utf-8'))
feats = gj['features']
sggs = sorted({f['properties']['sgg'] for f in feats})
sgg_index = {s: i + 1 for i, s in enumerate(sggs)}
sido_of = {f['properties']['sgg']: f['properties']['sido'] for f in feats}
polys = []          # (sgg번호, 바깥[(x,y)], 구멍들, ymin, ymax)
dong_out = []
for f in feats:
    p = f['properties']; g = f['geometry']
    pl = g['coordinates'] if g['type'] == 'MultiPolygon' else [g['coordinates']]
    sx = sy = sa = 0.0
    for poly in pl:
        a = np.asarray(poly[0]); px, py = to_px(a[:, 0], a[:, 1])
        holes = [np.column_stack(to_px(np.asarray(h)[:, 0], np.asarray(h)[:, 1])) for h in poly[1:]]
        polys.append((sgg_index[p['sgg']], np.column_stack([px, py]), holes, py.min(), py.max()))
        x = (a[:, 0] - OW) * MX; y = (a[:, 1] - OS) * MY
        ar = 0.5 * (x[:-1] * y[1:] - x[1:] * y[:-1]).sum()
        if abs(ar) > 0:
            cx = ((x[:-1] + x[1:]) * (x[:-1] * y[1:] - x[1:] * y[:-1])).sum() / (6 * ar)
            cy = ((y[:-1] + y[1:]) * (x[:-1] * y[1:] - x[1:] * y[:-1])).sum() / (6 * ar)
            sx += cx * abs(ar); sy += cy * abs(ar); sa += abs(ar)
    if sa > 0:
        dong_out.append({'code': p['adm_cd2'], 'name': p['adm_nm'].split()[-1], 'sgg': p['sgg'], 'sido': p['sido'], 'x': round(sx / sa), 'y': round(sy / sa), 'km2': round(sa / 1e6, 2)})
sido_arr = np.zeros(65536, np.uint8)
for s, i in sgg_index.items(): sido_arr[i] = int(sido_of[s])
log('dongs', len(dong_out), 'sgg', len(sggs))

# 길
tr = Transformer.from_crs('+proj=tmerc +lat_0=38 +lon_0=127 +k=1 +x_0=200000 +y_0=600000 +ellps=GRS80 +units=m +no_defs', 'EPSG:4326', always_xy=True)
sf = shapefile.Reader(os.path.join(RAW, 'nl', 'MOCT_LINK'), encoding='cp949')
fi = {f[0]: i for i, f in enumerate(sf.fields[1:])}
ROAD = {'101': (3, 74), '102': (3, 70), '103': (2, 64), '104': (2, 58), '105': (1, 55), '106': (1, 52), '107': (1, 50), '108': (1, 48)}
road_pts, road_len, road_w, road_t = [], [], [], []
for sr in sf.iterShapeRecords():
    p = sr.shape.points
    if len(p) < 2: continue
    rec = sr.record
    if int(rec[fi['ROAD_TYPE']] or 0) in (3, 4): continue          # 지하차도 · 터널
    width, tone = ROAD.get(str(rec[fi['ROAD_RANK']]), (1, 48))
    if int(rec[fi['LANES']] or 1) >= 6 and width < 3: width += 1
    road_pts.extend(p); road_len.append(len(p)); road_w.append(width); road_t.append(tone)
rp = np.asarray(road_pts); del road_pts
lon, lat = tr.transform(rp[:, 0], rp[:, 1]); del rp
rx, ry = to_px(lon, lat); del lon, lat
rx = rx.astype(np.float32); ry = ry.astype(np.float32)
rs = np.concatenate([[0], np.cumsum(road_len)])
rymin = np.minimum.reduceat(ry, rs[:-1]); rymax = np.maximum.reduceat(ry, rs[:-1])
road_w = np.asarray(road_w); road_t = np.asarray(road_t)
order = np.argsort(road_t, kind='stable')
log('roads', len(road_len))

TONE = np.full(256, 26.0, np.float32)
TONE[10] = 24.5; TONE[40] = 27.0; TONE[50] = 28.5; TONE[90] = 27.5
kept1 = []; kept0 = []
l0_rows = []
for j in range(START, NY):
    r0 = j * TILE
    rows = np.arange(r0 - 1, r0 + TILE + 1)            # 위아래 한 줄씩 더(경계선 셈)
    lats = lat_of_rows(rows)
    # 1) 땅 덮개
    cover = np.zeros((len(rows), W), np.uint8)
    for (la, lo), arr in WC.items():
        ri = (lats >= la) & (lats < la + 3); ci = (COLS >= lo) & (COLS < lo + 3)
        if not ri.any() or not ci.any(): continue
        rr = np.clip(((la + 3 - lats[ri]) * 12000).astype(np.int64), 0, 35999)
        cc = np.clip(((COLS[ci] - lo) * 12000).astype(np.int64), 0, 35999)
        c0, c1 = cc.min(), cc.max() + 1
        block = np.asarray(arr[rr.min():rr.max() + 1, c0:c1])
        cover[np.ix_(ri, ci)] = block[(rr - rr.min())[:, None], (cc - c0)[None, :]]
    if not cover.any():
        log('band', j, 'sea'); continue
    # 2) 지형 음영(32 m)
    cw = W // 4; crow = np.arange(r0 // 4 - 1, (r0 + TILE) // 4 + 1)
    clat = OS + (Y1 - (crow + 0.5) * COARSE) / MY; clon = OW + (X0 + (np.arange(cw) + 0.5) * COARSE) / MX
    dem = np.zeros((len(crow), cw), np.float32)
    for la in range(int(math.floor(clat.min())), int(math.floor(clat.max())) + 1):
        for lo in range(124, 132):
            arr = dem_tile(la, lo)
            if arr is None: continue
            ri = (clat >= la) & (clat < la + 1); ci = (clon >= lo) & (clon < lo + 1)
            if not ri.any() or not ci.any(): continue
            n, m = arr.shape
            rr = np.clip(((la + 1 - clat[ri]) * n).astype(int), 0, n - 1); cc = np.clip(((clon[ci] - lo) * m).astype(int), 0, m - 1)
            dem[np.ix_(ri, ci)] = arr[rr[:, None], cc[None, :]]
    dem = np.maximum(dem, 0)
    gy, gx = np.gradient(dem, COARSE, COARSE)
    az, alt = math.radians(315), math.radians(45)
    slope = np.arctan(np.hypot(gx, gy) * 1.6); aspect = np.arctan2(-gx, gy)
    shade = np.clip((np.sin(alt) * np.cos(slope) + np.cos(alt) * np.sin(slope) * np.cos(az - aspect)) / np.sin(alt), 0, 2) * 128
    big = np.asarray(Image.fromarray(np.clip(shade, 0, 255).astype(np.uint8)).resize((W, len(crow) * 4), Image.BILINEAR))
    off = (r0 - 1) - crow[0] * 4
    shade_u8 = big[off:off + len(rows)]
    # 3) 칠하기
    g = TONE[cover] * np.clip(1.0 + (shade_u8.astype(np.float32) - 128.0) / 128.0 * 0.8, 0.62, 1.5)
    sea = (cover == 80) | (cover == 0)
    g[sea] = 30.5
    # 4) 경계 · 나라 안팎
    ids = Image.new('I', (W, len(rows)), 0); dr = ImageDraw.Draw(ids)
    top = r0 - 1
    for k, outer, holes, ymin, ymax in polys:
        if ymax < top or ymin > top + len(rows): continue
        dr.polygon([(x, y - top) for x, y in outer], fill=k)
        for h in holes: dr.polygon([(x, y - top) for x, y in h], fill=0)
    idsa = np.asarray(ids, np.int32)
    outside = (idsa == 0) & ~sea
    g[outside] = g[outside] * 0.55 + 30.5 * 0.45 * 0.6          # 나라 밖 땅: 어둡게, 바다와 가깝게
    band = np.clip(g, 0, 255).astype(np.uint8)
    edge = np.zeros(idsa.shape, bool)
    edge[:, 1:] |= idsa[:, 1:] != idsa[:, :-1]; edge[1:, :] |= idsa[1:, :] != idsa[:-1, :]
    sd = sido_arr[idsa]
    sedge = np.zeros(idsa.shape, bool)
    sedge[:, 1:] |= sd[:, 1:] != sd[:, :-1]; sedge[1:, :] |= sd[1:, :] != sd[:-1, :]
    sedge = np.asarray(Image.fromarray(sedge.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(3))) > 0
    px = np.repeat(band[:, :, None], 3, axis=2)
    px[edge & ~sedge & (idsa > 0)] = (44, 44, 47)
    px[sedge] = (56, 56, 60)
    img = Image.fromarray(px[1:-1])          # 띠 본체 512줄
    # 5) 길
    dr = ImageDraw.Draw(img)
    hit = order[(rymax[order] >= r0 - 4) & (rymin[order] <= r0 + TILE + 4)]
    for q in hit:
        a, b = rs[q], rs[q + 1]
        t = int(road_t[q])
        dr.line(list(zip(rx[a:b].tolist(), (ry[a:b] - r0).tolist())), fill=(t, t, t + 2), width=int(road_w[q]))
    # 6) 조각(8 m)
    arr = np.asarray(img)
    for i in range(NX):
        t = arr[:, i * TILE:(i + 1) * TILE]
        if t.max() - t.min() <= 2: continue
        Image.fromarray(t).save(os.path.join(OUT, f'hongdal-nb-1-{i}-{j}.bytes'), 'JPEG', quality=80, optimize=True, subsampling=2)
        kept1.append([i, j])
    if j % 10 == 0: log('band', j, '/', NY, 'roads', len(hit), 'tiles', len(kept1))
# 7) 32 m 조각: 디스크의 8 m 조각 4×4를 모아 줄인다(없는 조각 = 바탕색)
import re
kept1 = []
for name in os.listdir(OUT):
    m = re.match(r'hongdal-nb-1-(\d+)-(\d+)\.bytes$', name)
    if m: kept1.append([int(m.group(1)), int(m.group(2))])
have = {(i, j) for i, j in kept1}
kept0 = []
for J in range(int(math.ceil(NY / 4))):
    for I in range(int(math.ceil(NX / 4))):
        if not any((I * 4 + di, J * 4 + dj) in have for di in range(4) for dj in range(4)): continue
        big = Image.new('RGB', (TILE * 4, TILE * 4), (30, 30, 31))
        for dj in range(4):
            for di in range(4):
                if (I * 4 + di, J * 4 + dj) in have:
                    big.paste(Image.open(os.path.join(OUT, f'hongdal-nb-1-{I * 4 + di}-{J * 4 + dj}.bytes')).convert('RGB'), (di * TILE, dj * TILE))
        t = big.resize((TILE, TILE), Image.LANCZOS); a_ = np.asarray(t)
        if a_.max() - a_.min() <= 2: continue
        t.save(os.path.join(OUT, f'hongdal-nb-0-{I}-{J}.bytes'), 'JPEG', quality=80, optimize=True, subsampling=2)
        kept0.append([I, J])
log('level0', len(kept0), 'level1', len(kept1))
# 8) 이름 자리
sgg_out = []
for s in sggs:
    ds = [d for d in dong_out if d['sgg'] == s]
    a = sum(d['km2'] for d in ds) or 1
    nm = next(f['properties']['sggnm'] for f in feats if f['properties']['sgg'] == s)
    sgg_out.append({'code': s, 'name': nm, 'sido': sido_of[s], 'x': round(sum(d['x'] * d['km2'] for d in ds) / a), 'y': round(sum(d['y'] * d['km2'] for d in ds) / a), 'dongs': len(ds)})
sido_names = {}
for f in feats: sido_names[f['properties']['sido']] = f['properties']['sidonm']
sido_out = []
for code, nm in sorted(sido_names.items()):
    ds = [d for d in dong_out if d['sido'] == code]; a = sum(d['km2'] for d in ds) or 1
    sido_out.append({'code': code, 'name': nm, 'x': round(sum(d['x'] * d['km2'] for d in ds) / a), 'y': round(sum(d['y'] * d['km2'] for d in ds) / a)})
space = {
    'schema': 'hongdal-capital-space.v1', 'revision': 'nation-base-20261125-r1', 'scope': 'nation',
    'unit': '전국 바탕 · 무채색 · 조각', 'distribution': '배포 가능(출처 표시)',
    'frame': {'originLongitude': OW, 'originLatitude': OS, 'metresPerDegreeLongitude': MX, 'metresPerDegreeLatitude': MY},
    'west': WEST, 'east': EAST, 'south': SOUTH, 'north': NORTH,
    'x0': round(X0, 1), 'y1': round(Y1, 1), 'width': W * FINE, 'height': H * FINE,
    'background': [30, 30, 31], 'tilePrefix': 'hongdal-nb',
    'levels': [{'level': 0, 'metresPerPixel': COARSE, 'tile': TILE, 'columns': int(math.ceil(NX / 4)), 'rows': int(math.ceil(NY / 4)), 'tiles': kept0},
               {'level': 1, 'metresPerPixel': FINE, 'tile': TILE, 'columns': NX, 'rows': NY, 'tiles': kept1}],
    'sidos': sido_out, 'sggs': sgg_out, 'dongs': dong_out,
    'attribution': '땅 덮개 © ESA WorldCover 2021 (CC BY 4.0) · 지형 Copernicus DEM GLO-30 © DLR e.V. 2010-2014 · © Airbus Defence and Space GmbH 2014-2018 (EU · ESA Copernicus) · 길 국토교통부 표준노드링크 · 경계 통계청 SGIS(공공누리 제1유형) · vuski/admdongkor (CC BY 4.0)',
    'attributionShort': '© ESA WorldCover CC BY 4.0 · Copernicus DEM · 국토교통부 · 통계청 SGIS · vuski/admdongkor',
}
json.dump(space, open(os.path.join(OUT, 'hongdal-nation-base-space.json'), 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
log('done')
