"""농촌 판 땅 층 굽기 (B1 · B2, 2026-11-26). 계약: docs/AI/Handoffs/2026-11-26-Claude-계약-농촌판땅층.md

바탕 원장(동 모듈 JSON: 경계 · 건물 · 길)은 읽기만 한다. 팜맵 필지(N1 SQLite)와 NASADEM으로 땅 층 하나를 따로 굽는다.
  * 땅 메시 hongdal-land-<코드>.bytes: HDIZ v1(싼 HDIO v2). 건물 · 도로 · 그림자 0. y = 0.015 m 평면.
  * 색인 hongdal-land-<코드>.json: 갈래 넓이 · 필지 · 하우스 자리 · 보정 규칙 · 검사 · 출처 · 지문.
  * preview-*.png: 땅 층만 그린 확인 그림.
PC 안에서만 돈다(원장의 경계는 주소기반산업지원서비스 자료라 PC 밖 반출 금지). 결과 메시는 게임 자원과 같은 취급.

사용: python3 -B make_rural_land.py <modules.tar.gz> <동코드> <farmmap-rural-n1.sqlite> <NASADEM 폴더[,폴더…]> <결과 폴더>
"""
import sys, os, io, json, math, struct, sqlite3, tarfile, hashlib, time, zlib
import numpy as np
import shapely
from shapely import wkb
from shapely.geometry import Polygon, MultiPolygon, LineString, box
from shapely.ops import unary_union
from shapely import affinity
from pyproj import Transformer
import tifffile

TAR, CODE, FARM_DB, DEM_DIR, OUT = sys.argv[1:6]
os.makedirs(OUT, exist_ok=True)
T0 = time.time()

# ── 상수(계약과 같다) ─────────────────────────────────────────────────────────
M_LAT = 111200.0
A, F = 6378137.0, 1 / 298.257223563
E2 = F * (2 - F)
LAND_Y = 0.015
ROAD_HALF = 4.0                       # 바탕 굽기의 기본 반폭(make_dong_modules.ROAD_HALF)
SLOPE_FOREST = 8.0                    # 도. 이 이상인 빈 땅 = 숲(보정)
GRID = 10.0                           # 빈 땅 경사 판정 격자(m)
WATER_MAX_ELEV = 15.0                 # m. 이 아래이고 경사가 0인 넓은 평지 = 물(보정, NASADEM의 수면은 평평하게 채워져 있다)
WATER_MIN_AREA = 100_000.0            # m². 이보다 작은 평지 덩어리는 물로 보지 않는다
SMOOTH = 10.0                         # 격자 계단을 다듬는 폭(m)
MAJORITY = 5                          # 숲 판정을 이 칸 수(×10 m) 창의 다수결로 다듬는다
SHADE_CELL = 60.0                     # 숲 · 풀밭을 이 크기로 잘라 정점 음영의 해상도를 맞춘다
GH_W, GH_GAP, GH_MIN_L, GH_MIN_A = 7.0, 1.5, 8.0, 40.0
CLASSES = {  # 번호, 이름, 기본색, 출처
    '01': (10, '논', 0x8F8A3E, '원본'), '02': (11, '밭', 0x5F7F3E, '원본'), '03': (12, '과수', 0x3F6A3A, '원본'), '04': (13, '시설', 0xB9C0C4, '원본'),
    'forest': (20, '숲', 0x2C4A32, '보정'), 'grass': (21, '풀밭', 0x46553A, '보정'), 'water': (22, '물', 0x23465E, '보정')}
PRIORITY = ['04', '03', '01', '02']   # 겹치면 앞이 갖는다

def polys(g):
    """어떤 도형이든 그 안의 다각형을 모두 꺼낸다(묶음 속 묶음 포함)."""
    if g is None or g.is_empty: return []
    if g.geom_type == 'Polygon': return [g]
    out = []
    for q in getattr(g, 'geoms', []): out.extend(polys(q))
    return out
def clean(g):
    """선 · 점을 버리고 다각형만 남긴다."""
    ps = polys(shapely.make_valid(g))
    return unary_union(ps) if ps else Polygon()
report = {'schema': 'hongdal-rural-land.v1', 'code': CODE, 'checks': [], 'timings': {}}
def check(name, ok, detail=''):
    report['checks'].append({'name': name, 'ok': bool(ok), 'detail': detail})
    print(('OK   ' if ok else 'FAIL ') + name, detail)
def sha256_file(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for b in iter(lambda: f.read(1 << 20), b''): h.update(b)
    return h.hexdigest().upper()
def lap(k): report['timings'][k] = round(time.time() - T0, 1); print('..', k, report['timings'][k], 's')

# ── 좌표(make_dong_modules.Frame과 같은 식) ────────────────────────────────────
def ecef(lat, lon):
    la, lo = np.radians(lat), np.radians(lon)
    n = A / np.sqrt(1 - E2 * np.sin(la) ** 2)
    return n * np.cos(la) * np.cos(lo), n * np.cos(la) * np.sin(lo), n * (1 - E2) * np.sin(la)
class Frame:
    def __init__(self, lat0, lon0):
        self.lat0, self.lon0 = lat0, lon0
        self.o = [float(v) for v in ecef(np.array(lat0), np.array(lon0))]
        la, lo = math.radians(lat0), math.radians(lon0)
        self.sla, self.cla, self.slo, self.clo = math.sin(la), math.cos(la), math.sin(lo), math.cos(lo)
    def fwd(self, lat, lon):
        x, y, z = ecef(lat, lon)
        dx, dy, dz = x - self.o[0], y - self.o[1], z - self.o[2]
        return -self.slo * dx + self.clo * dy, -self.sla * self.clo * dx - self.sla * self.slo * dy + self.cla * dz
    def inv(self, e, n):
        e, n = np.asarray(e, dtype=float), np.asarray(n, dtype=float)
        lat = self.lat0 + n / 111000.0; lon = self.lon0 + e / (111000.0 * self.cla); d = 1e-6
        for _ in range(10):
            fe, fn = self.fwd(lat, lon); e1, n1 = self.fwd(lat + d, lon); e2, n2 = self.fwd(lat, lon + d)
            j0, j1, j2, j3 = (e1 - fe) / d, (e2 - fe) / d, (n1 - fn) / d, (n2 - fn) / d
            det = j0 * j3 - j1 * j2; re, rn = e - fe, n - fn
            lat = lat + (j3 * re - j1 * rn) / det; lon = lon + (j0 * rn - j2 * re) / det
            if np.max(np.abs(re)) < 1e-6 and np.max(np.abs(rn)) < 1e-6: break
        return lat, lon

# ── 1. 원장 읽기(읽기만) ───────────────────────────────────────────────────────
tar_sha_before = sha256_file(TAR)
with tarfile.open(TAR, 'r:gz') as tf:
    member = next(m for m in tf.getmembers() if m.name.endswith(f'{CODE}.json'))
    mod = json.load(tf.extractfile(member))
fr = mod['coordinateFrame']
assert fr['method'] == 'WGS84-ECEF-ENU-at-zero-altitude' and fr['metersPerUnit'] == 1.0
LAT0, LON0 = fr['originLatitude'], fr['originLongitude']
M_LON = M_LAT * math.cos(math.radians(LAT0))
frame = Frame(LAT0, LON0)
B = shapely.make_valid(Polygon([(p['x'], p['z']) for p in mod['boundary']]))
if B.geom_type != 'Polygon': B = max(getattr(B, 'geoms', [B]), key=lambda g: g.area) if B.geom_type != 'MultiPolygon' else B
bld = [shapely.make_valid(Polygon([(p['x'], p['z']) for p in b['footprint']])) for b in mod['buildings'] if len(b['footprint']) >= 3]
OB = unary_union(bld).intersection(B)
roads = [LineString([(r['from']['x'], r['from']['z']), (r['to']['x'], r['to']['z'])]) for r in mod['roads']]
OR = unary_union([shapely.buffer(LineString(l.coords), ROAD_HALF, cap_style='flat') for l in roads]).intersection(B)
OBS = unary_union([OB, OR])
report['ledger'] = {'module': member.name, 'projectionRevision': mod.get('projectionRevision'), 'sourceVintage': mod.get('sourceVintage'),
                    'contentHashSha256': mod.get('contentHashSha256'), 'tarSha256': tar_sha_before, 'buildings': len(bld), 'roads': len(roads),
                    'displayName': mod.get('displayName')}
report['areas'] = {'boundary_m2': round(B.area, 1), 'buildings_m2': round(OB.area, 1), 'roads_m2': round(OR.area, 1), 'obstacles_m2': round(OBS.area, 1)}
lap('ledger')

# ── 2. 팜맵 필지(EPSG:5179 → 위경도 → ENU) ────────────────────────────────────
db = sqlite3.connect(f'file:{FARM_DB}?mode=ro', uri=True)
rows = db.execute("""select p.parcel_id, p.class_code, p.flight_date, p.geometry_wkb from polygon_dong_overlap o
                     join source_polygon p using(parcel_id) where o.dong_code=?""", (CODE,)).fetchall()
n1_union = {c: a for c, a in db.execute("select class_code, union_m2 from footprint_dong where dong_code=?", (CODE,))}
n1_parcels = db.execute("select parcels from farm_summary where dong_code=?", (CODE,)).fetchone()[0]
src_meta = db.execute("select source_id, edition from source_input").fetchall()
to_wgs = Transformer.from_crs(5179, 4326, always_xy=True)
def to_enu(coords):
    lon, lat = to_wgs.transform(coords[:, 0], coords[:, 1])
    e, n = frame.fwd(np.asarray(lat), np.asarray(lon))
    return np.stack([e, n], 1)
parcels = []
tiny = []
for pid, cls, flight, w in rows:
    if cls not in CLASSES: continue
    g = shapely.make_valid(shapely.transform(wkb.loads(w), to_enu))
    gc = g.intersection(B)
    if gc.is_empty or gc.area < 0.01:
        tiny.append(dict(pid=int(pid), area_m2=round(gc.area, 4))); continue
    parcels.append(dict(pid=int(pid), c=cls, flight=flight, g=gc))
lap('farmmap')
# 검사 1 · 2
clip_union = {c: unary_union([p['g'] for p in parcels if p['c'] == c]).area for c in PRIORITY}
official_area = db.execute("select area_m2 from admin_area where dong_code=?", (CODE,)).fetchone()[0]
boundary_ratio = B.area / official_area
report['boundary'] = {'ledger_m2': round(B.area, 1), 'official_m2': round(official_area, 1), 'ratio': round(boundary_ratio, 4)}
# 원장 경계가 공식 경계보다 작으면(섬 · 떨어진 땅이 원장에 없음) N1 쪽도 원장 경계에 든 필지만으로 비교한다
in_ids = {p['pid'] for p in parcels}
n1_in = {c: 0.0 for c in PRIORITY}
for pid_, cls_, ov_ in db.execute("select o.parcel_id, p.class_code, o.overlap_m2 from polygon_dong_overlap o join source_polygon p using(parcel_id) where o.dong_code=?", (CODE,)):
    if cls_ in n1_in and pid_ in in_ids: n1_in[cls_] += ov_
for c in PRIORITY:
    ref = n1_union.get(c, 0.0)
    ratio = clip_union[c] / ref if ref else (1.0 if clip_union[c] < 1 else 0)
    if boundary_ratio >= 0.99:
        check(f'1 {CLASSES[c][1]} 넓이 대 N1', abs(ratio - 1) <= 0.01, f'{clip_union[c]:.0f} / {ref:.0f} m² = {ratio:.4f}')
    else:
        r2 = clip_union[c] / n1_in[c] if n1_in[c] else (1.0 if clip_union[c] < 1 else 0)
        check(f'1 {CLASSES[c][1]} 넓이 대 N1(원장 경계 안 필지)', 0.98 <= r2 <= 1.01,
              f'경계 {boundary_ratio:.3f} · {clip_union[c]:.0f} / {n1_in[c]:.0f} m² = {r2:.4f} · 공식 경계 전체 {ref:.0f} m²')
cultivated_ids = {p['pid'] for p in parcels}
tiny_ids = {t['pid'] for t in tiny}
check('2 필지 수 대 N1', len(cultivated_ids) + len(tiny_ids - cultivated_ids) == n1_parcels or boundary_ratio < 0.99,
      f'{len(cultivated_ids)} + 경계에 0.01 m² 미만으로 걸쳐 버린 조각 {len(tiny_ids - cultivated_ids)} / {n1_parcels}')
report['droppedSlivers'] = tiny

# ── 3. 깎기: 길 · 건물이 이기고, 갈래는 시설 > 과수 > 논 > 밭 ───────────────────
taken = OBS
final = {}
for c in PRIORITY:
    u = unary_union([p['g'] for p in parcels if p['c'] == c])
    final[c] = clean(u.difference(taken))
    taken = unary_union([taken, final[c]])
for p in parcels:
    p['final'] = clean(p['g'].intersection(final[p['c']]))
farm_all = unary_union([final[c] for c in PRIORITY])
lap('cut')

# ── 4. 보정: 남은 빈 땅 → 숲(경사 ≥ 8°) · 풀밭 ─────────────────────────────────
# NASADEM 1° 타일을 필요한 만큼 읽는다(동이 타일 경계에 걸쳐도 맞게). 타일마다 경사 · 음영을 미리 계산
TILES = {}
def tile(la, lo):
    key = (la, lo)
    if key in TILES: return TILES[key]
    cands = [os.path.join(d_, f'NASADEM_HGT_n{la:02d}e{lo:03d}.tif') for d_ in DEM_DIR.split(',')]
    path = next((c_ for c_ in cands if os.path.exists(c_)), None)
    if path is None: TILES[key] = None; return None
    t = tifffile.TiffFile(path); p = t.pages[0]
    tie = p.tags['ModelTiepointTag'].value; sc = p.tags['ModelPixelScaleTag'].value
    z = p.asarray().astype(float); z[z <= -1000] = np.nan
    ny_, nx_ = z.shape
    lat_rows = tie[4] - (np.arange(ny_) + 0.5) * sc[1]
    dy_m = sc[1] * 111320.0; dx_m = sc[0] * 111320.0 * np.cos(np.radians(lat_rows))[:, None]
    gy, gx = np.gradient(z)
    dzdx = gx / dx_m; dzdn = -gy / dy_m
    slope = np.degrees(np.arctan(np.hypot(dzdx, dzdn)))
    az, alt = math.radians(315.0), math.radians(45.0)
    nn = np.sqrt(dzdx ** 2 + dzdn ** 2 + 1)
    shade = (-dzdx * math.sin(az) * math.cos(alt) - dzdn * math.cos(az) * math.cos(alt) + math.sin(alt)) / nn
    TILES[key] = dict(path=os.path.basename(path), Z=z, SLOPE=slope, SHADE=shade, lon0=tie[3], lat0=tie[4], sx=sc[0], sy=sc[1], ny=ny_, nx=nx_)
    return TILES[key]
def sample(name, lat, lon):
    lat = np.asarray(lat, dtype=float); lon = np.asarray(lon, dtype=float)
    out = np.full(lat.shape, np.nan)
    la_i, lo_i = np.floor(lat).astype(int), np.floor(lon).astype(int)
    for la, lo in set(zip(la_i.ravel().tolist(), lo_i.ravel().tolist())):
        t = tile(la, lo)
        if t is None: continue
        m = (la_i == la) & (lo_i == lo)
        r = np.clip(((t['lat0'] - lat[m]) / t['sy'] - 0.5).round().astype(int), 0, t['ny'] - 1)
        c = np.clip(((lon[m] - t['lon0']) / t['sx'] - 0.5).round().astype(int), 0, t['nx'] - 1)
        out[m] = t[name][r, c]
    return out
SLOPE, SHADE, Z = 'SLOPE', 'SHADE', 'Z'
free = clean(B.difference(unary_union([OBS, farm_all])))
x0, z0, x1, z1 = free.bounds
xs = np.arange(x0, x1, GRID) + GRID / 2; zs = np.arange(z0, z1, GRID) + GRID / 2
EX, NZ = np.meshgrid(xs, zs)
glat, glon = frame.inv(EX.ravel(), NZ.ravel())
steep = (np.nan_to_num(sample(SLOPE, glat, glon)) >= SLOPE_FOREST).reshape(EX.shape)
gelev = sample(Z, glat, glon).reshape(EX.shape)
in_dong = shapely.contains_xy(B, EX.ravel(), NZ.ravel()).reshape(EX.shape)
dem_cover = float(np.isfinite(gelev[in_dong]).mean()) if in_dong.any() else 0.0
check('9 지형(NASADEM)이 동을 덮음', dem_cover >= 0.99, f'{dem_cover:.4f} · 타일 {sorted(k for k, v in TILES.items() if v)}')
gslope = sample(SLOPE, glat, glon).reshape(EX.shape)
from scipy import ndimage
flat = (gslope < 0.05) & (gelev <= WATER_MAX_ELEV)
lab, nlab = ndimage.label(flat)
sizes = ndimage.sum(flat, lab, index=np.arange(1, nlab + 1)) * GRID * GRID
waterm = np.isin(lab, np.where(sizes >= WATER_MIN_AREA)[0] + 1)
def cells_to_geom(mask):
    boxes = []
    for i in range(mask.shape[0]):
        row = mask[i]; j = 0
        while j < len(row):
            if row[j]:
                k = j
                while k + 1 < len(row) and row[k + 1]: k += 1
                boxes.append(box(x0 + j * GRID, z0 + i * GRID, x0 + (k + 1) * GRID, z0 + (i + 1) * GRID)); j = k + 1
            else: j += 1
    if not boxes: return Polygon()
    return unary_union(boxes).buffer(SMOOTH, join_style='round').buffer(-SMOOTH, join_style='round').simplify(2.0)
WATER = cells_to_geom(waterm)
steep = ndimage.median_filter(steep.astype(np.uint8), size=MAJORITY) > 0
STEEP = cells_to_geom(steep & ~waterm)
final['water'] = clean(free.intersection(WATER))
final['forest'] = clean(free.difference(final['water']).intersection(STEEP))
final['grass'] = clean(free.difference(unary_union([final['water'], final['forest']])))
lap('correct')

# ── 5. 하우스 자리(보정) ───────────────────────────────────────────────────────
houses = []
def long_angle(g):
    r = g.minimum_rotated_rectangle
    cs = list(r.exterior.coords) if r.geom_type == 'Polygon' else [(0, 0), (1, 0)]
    best, ang = 0, 0.0
    for (ax, ay), (bx, by) in zip(cs, cs[1:]):
        d = math.hypot(bx - ax, by - ay)
        if d > best: best, ang = d, math.atan2(by - ay, bx - ax)
    return ang
for p in parcels:
    g = p['final']
    p['angle'] = long_angle(g) if not g.is_empty else 0.0
    if p['c'] != '04' or g.is_empty: continue
    ang = p['angle']; cx, cz = g.centroid.x, g.centroid.y
    rg = affinity.rotate(g, -ang, origin=(cx, cz), use_radians=True)
    mnx, mny, mxx, mxy = rg.bounds
    y = mny + GH_GAP / 2
    while y + 1.0 < mxy:
        piece = rg.intersection(box(mnx, y, mxx, y + GH_W))
        for q in getattr(piece, 'geoms', [piece]):
            if q.is_empty or q.geom_type != 'Polygon': continue
            qx0, qy0, qx1, qy1 = q.bounds
            if qx1 - qx0 >= GH_MIN_L and q.area >= GH_MIN_A:
                c = affinity.rotate(q.centroid, ang, origin=(cx, cz), use_radians=True)
                houses.append(dict(pid=p['pid'], e=c.x, n=c.y, ang=ang, L=round(qx1 - qx0, 1), W=round(min(GH_W, qy1 - qy0), 1)))
        y += GH_W + GH_GAP
lap('houses')

# ── 6. 검사 3 · 4 · 5 ─────────────────────────────────────────────────────────
OUT_KEYS = PRIORITY + ['forest', 'grass', 'water']
area_sum = sum(final[k].area for k in OUT_KEYS)
land_union = unary_union([final[k] for k in OUT_KEYS])
check('3 갈래끼리 겹침 0', abs(area_sum - land_union.area) <= max(1.0, land_union.area * 0.001), f'합 {area_sum:.0f} · 합집합 {land_union.area:.0f} m²')
outside = land_union.difference(B.buffer(0.01)).area
check('4 경계 밖', outside < 1.0, f'{outside:.3f} m²')
cross = land_union.intersection(OB).area
check('5 건물 관통', cross < 1.0, f'{cross:.3f} m²')

# ── 7. 메시: 숲 · 풀밭은 60 m 칸으로 잘라 정점 음영 해상도를 맞추고, 모두 제약 들로네로 삼각형(한꺼번에 계산) ─────────
def rgb(h): return ((h >> 16) & 255, (h >> 8) & 255, h & 255)
def chop(g):
    """두 단계로 자른다: 600 m 덩어리 → 60 m 칸(큰 숲을 한 번에 60 m로 자르면 느리다)."""
    if g.is_empty: return []
    out = []
    def grid(geom, cell):
        gx0, gz0, gx1, gz1 = geom.bounds
        xs_ = np.arange(gx0, gx1, cell); zs_ = np.arange(gz0, gz1, cell)
        X_, Z_ = np.meshgrid(xs_, zs_)
        tiles = shapely.box(X_.ravel(), Z_.ravel(), X_.ravel() + cell, Z_.ravel() + cell)
        shapely.prepare(geom)
        hit = shapely.intersects(geom, tiles)
        return [y for x in shapely.intersection(tiles[hit], geom) for y in polys(x)]
    for part in polys(g):
        gx0, gz0, gx1, gz1 = part.bounds
        blocks = [part] if max(gx1 - gx0, gz1 - gz0) <= 600 else grid(part, 600.0)
        for b_ in blocks:
            bx0, bz0, bx1, bz1 = b_.bounds
            if max(bx1 - bx0, bz1 - bz0) <= SHADE_CELL: out.append(b_); continue
            out.extend([y for y in grid(b_, SHADE_CELL) if y.area > 0.05])
    return out
items = [(p['final'], CLASSES[p['c']][0], CLASSES[p['c']][2], p['angle']) for p in parcels if not p['final'].is_empty]
for k in ('forest', 'grass', 'water'):
    cid, _, colour, _ = CLASSES[k]
    items += [(q, cid, colour, 0.0) for q in chop(final[k])]
lap('chop')
geoms = np.array([it[0] for it in items], dtype=object)
cids = np.array([it[1] for it in items], dtype='<f4'); cols = np.array([rgb(it[2]) for it in items], dtype=float); angs = np.array([it[3] for it in items], dtype='<f4')
tris = shapely.constrained_delaunay_triangles(geoms)
parts, idx = shapely.get_parts(tris, return_index=True)
co = shapely.get_coordinates(parts)
n_pts = shapely.get_num_coordinates(parts)
assert (n_pts == 4).all(), set(n_pts.tolist())
T3 = co.reshape(-1, 4, 2)[:, :3]
ar = 0.5 * np.abs((T3[:, 1, 0] - T3[:, 0, 0]) * (T3[:, 2, 1] - T3[:, 0, 1]) - (T3[:, 2, 0] - T3[:, 0, 0]) * (T3[:, 1, 1] - T3[:, 0, 1]))
keep = ar > 1e-4
T3, idx = T3[keep], idx[keep]
tri_area = float(ar[keep].sum())
flat = T3.reshape(-1, 2)
la, lo = frame.inv(flat[:, 0], flat[:, 1])
gx, gz = (lo - LON0) * M_LON, (la - LAT0) * M_LAT
sh = np.clip(0.55 + 0.6 * np.nan_to_num(sample(SHADE, la, lo), nan=0.7), 0.55, 1.15)
vid = np.repeat(idx, 3)
P = np.stack([gx, np.full_like(gx, LAND_Y), gz], 1).astype('<f4')
Cc = np.concatenate([np.clip(cols[vid] * sh[:, None], 0, 255), np.full((len(vid), 1), 255.0)], 1).astype('u1')
U = np.stack([cids[vid], angs[vid]], 1).astype('<f4')
U2 = np.zeros((len(P), 2), dtype='<f4')
v = np.stack([gx, gz], 1).reshape(-1, 3, 2)
crossz = (v[:, 1, 0] - v[:, 0, 0]) * (v[:, 2, 1] - v[:, 0, 1]) - (v[:, 2, 0] - v[:, 0, 0]) * (v[:, 1, 1] - v[:, 0, 1])
base = np.arange(len(v)) * 3
Tr = np.where((crossz > 0)[:, None], np.stack([base, base + 2, base + 1], 1), np.stack([base, base + 1, base + 2], 1)).astype('<i4')
lap('mesh')
check('8 삼각형 넓이 합 대 땅 층 넓이', abs(tri_area / land_union.area - 1) <= 0.002, f'{tri_area:.0f} / {land_union.area:.0f} m² = {tri_area / land_union.area:.4f}')
check('7 메시 색인 · 유한 · 크기', Tr.min() >= 0 and Tr.max() < len(P) and np.isfinite(P).all() and len(P) < 4_000_000, f'정점 {len(P):,} · 삼각형 {len(Tr):,}')
bl, bo = frame.inv(np.array([c[0] for c in B.exterior.coords]), np.array([c[1] for c in B.exterior.coords]))
west, south, east, north = float(bo.min()), float(bl.min()), float(bo.max()), float(bl.max())
buf = io.BytesIO()
buf.write(b'HDIO'); buf.write(struct.pack('<i', 2)); buf.write(struct.pack('<6d', LAT0, LON0, west, south, east, north))
buf.write(struct.pack('<2i', 0, 0))
buf.write(struct.pack('<i', len(P))); buf.write(P.tobytes()); buf.write(Cc.tobytes()); buf.write(U.tobytes()); buf.write(U2.tobytes())
buf.write(struct.pack('<i', Tr.size)); buf.write(Tr.tobytes())
buf.write(struct.pack('<i', 0)); buf.write(struct.pack('<i', 0))          # 그림자 정점 0 · 색인 0
buf.write(struct.pack('<i', 0)); buf.write(struct.pack('<i', 0)); buf.write(struct.pack('<i', 0)); buf.write(struct.pack('<i', 0)); buf.write(struct.pack('<i', 0))
raw = buf.getvalue(); raw_sha = hashlib.sha256(raw).hexdigest().upper()
cz = zlib.compressobj(9, zlib.DEFLATED, -15, 9); body = cz.compress(raw) + cz.flush()
assert zlib.decompress(body, -15) == raw
packed = b'HDIZ' + struct.pack('<ii', 1, len(raw)) + bytes.fromhex(raw_sha) + body
mesh_path = os.path.join(OUT, f'hongdal-land-{CODE}.bytes')
open(mesh_path, 'wb').write(packed)

# ── 8. 원장 불변 · 색인 ───────────────────────────────────────────────────────
check('6 원장 불변', sha256_file(TAR) == tar_sha_before, tar_sha_before[:16])
def gxz(e, n):
    la, lo = frame.inv(np.array([e]), np.array([n])); return round(float((lo[0] - LON0) * M_LON), 2), round(float((la[0] - LAT0) * M_LAT), 2)
report['sources'] = {'farmmap': [dict(id=s, edition=e) for s, e in src_meta], 'dem': sorted(t['path'] for t in TILES.values() if t) + ['NASADEM 30 m'],
                     'boundaryBuildingsRoads': '바탕 원장(읽기만)'}
report['classes'] = [{'id': v[0], 'name': v[1], 'colour': '#%06X' % v[2], 'origin': v[3], 'key': k,
                      'area_m2': round(final[k].area, 1)} for k, v in CLASSES.items()]
report['areas'].update({'farm_clipped_m2': {CLASSES[c][1]: round(clip_union[c], 1) for c in PRIORITY},
                        'farm_final_m2': {CLASSES[c][1]: round(final[c].area, 1) for c in PRIORITY},
                        'farm_cut_by_obstacles_m2': round(sum(clip_union.values()) - sum(final[c].area for c in PRIORITY), 1),
                        'forest_m2': round(final['forest'].area, 1), 'grass_m2': round(final['grass'].area, 1), 'water_m2': round(final['water'].area, 1)})
report['corrections'] = {'roadHalfWidth_m': ROAD_HALF, 'forestSlope_deg': SLOPE_FOREST, 'waterMaxElev_m': WATER_MAX_ELEV, 'waterMinArea_m2': WATER_MIN_AREA, 'smooth_m': SMOOTH, 'forestMajority_cells': MAJORITY, 'slopeGrid_m': GRID, 'shadeCell_m': SHADE_CELL,
                         'greenhouse': {'width_m': GH_W, 'gap_m': GH_GAP, 'minLength_m': GH_MIN_L, 'minArea_m2': GH_MIN_A, 'count': len(houses)},
                         'priority': [CLASSES[c][1] for c in PRIORITY]}
report['parcels'] = [{'pid': p['pid'], 'class': CLASSES[p['c']][0], 'flight': p['flight'], 'area_m2': round(p['g'].area, 1),
                      'final_m2': round(p['final'].area, 1), 'angle': round(p['angle'], 4)} for p in parcels]
report['houses'] = [dict(pid=h['pid'], x=gxz(h['e'], h['n'])[0], z=gxz(h['e'], h['n'])[1], ang=round(h['ang'], 4), L=h['L'], W=h['W']) for h in houses]
report['mesh'] = {'resource': f'hongdal-land-{CODE}', 'vertices': int(len(P)), 'triangles': int(len(Tr)), 'rawBytes': len(raw), 'rawSha256': raw_sha,
                  'bytes': len(packed), 'sha256': hashlib.sha256(packed).hexdigest().upper(), 'y': LAND_Y,
                  'bounds': {'west': west, 'south': south, 'east': east, 'north': north}}
report['label'] = '내부 검토용 · 배포 아님 · 땅 층(팜맵 원본 + 보정). 소유 · 지번 없음'
lap('write')

# ── 9. 확인 그림(땅 층만) ──────────────────────────────────────────────────────
from PIL import Image, ImageDraw
PAL = {'01': (201, 150, 47), '02': (111, 170, 74), '03': (62, 118, 58), '04': (222, 228, 232), 'forest': (40, 74, 50), 'grass': (82, 98, 62), 'water': (44, 86, 118)}
def draw(path, bounds, mpp, with_houses):
    bx0, bz0, bx1, bz1 = bounds
    W, H = int((bx1 - bx0) / mpp), int((bz1 - bz0) / mpp)
    im = Image.new('RGB', (W, H), (16, 24, 30)); d = ImageDraw.Draw(im)
    def ring(cs): return [((x - bx0) / mpp, (bz1 - y) / mpp) for x, y in cs]
    view = box(*bounds)
    for k in ['grass', 'water', 'forest'] + PRIORITY[::-1]:
        g = final[k]
        mask = Image.new('L', (W, H), 0); md = ImageDraw.Draw(mask)
        for poly in polys(g):
            if not poly.intersects(view): continue
            md.polygon(ring(poly.exterior.coords), fill=255)
            for h in poly.interiors: md.polygon(ring(h.coords), fill=0)
        im.paste(Image.new('RGB', (W, H), PAL[k]), (0, 0), mask)
    if with_houses:
        for h in houses:
            ca, sa = math.cos(h['ang']), math.sin(h['ang'])
            pts = []
            for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                ex = h['e'] + ca * sx * h['L'] / 2 - sa * sy * h['W'] / 2; ny_ = h['n'] + sa * sx * h['L'] / 2 + ca * sy * h['W'] / 2
                pts.append(((ex - bx0) / mpp, (bz1 - ny_) / mpp))
            if bx0 <= h['e'] <= bx1 and bz0 <= h['n'] <= bz1: d.polygon(pts, fill=(245, 248, 250), outline=(120, 132, 140))
    im.save(path)
draw(os.path.join(OUT, 'preview-whole.png'), B.bounds, 6.0, False)
# 하우스가 가장 많은 1.2 km 창
if houses:
    hc = np.array([[h['e'], h['n']] for h in houses]); best = max(range(len(hc)), key=lambda i: int(((np.abs(hc - hc[i]) < 600).all(1)).sum()))
    cx, cz = hc[best]
else: cx, cz = B.centroid.x, B.centroid.y
draw(os.path.join(OUT, 'preview-window.png'), (cx - 600, cz - 600, cx + 600, cz + 600), 1.0, True)
lap('preview')
report['ok'] = all(c['ok'] for c in report['checks'])
with open(os.path.join(OUT, f'hongdal-land-{CODE}.json'), 'w', encoding='utf-8', newline='\n') as f: json.dump(report, f, ensure_ascii=False, indent=1)
print('DONE ok' if report['ok'] else 'DONE with FAIL', len(P), 'verts', len(houses), 'houses', report['timings'])
