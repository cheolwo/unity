"""공개판 행정동 디오라마 굽기 (2026-11-25, 사용자 11:22 「타일 두 번 누르면 디오라마로 … 수도권부터 전국으로」).

주소 자료(주소기반산업지원서비스, 보안각서)를 하나도 쓰지 않는 굽기다. 폰 · 스토어 빌드에 실어도 되는 공개 자료만 쓴다.
  * 건물: 국토교통부 GIS건물통합정보 AL_D010 2026-11-09 — 이용허락범위 제한 없음 (prep_buildings.py가 동별로 나눈 것)
  * 경계: vuski/admdongkor ver20261001 — 통계청 SGIS 공공누리 제1유형 · 추가분 CC BY 4.0
  * 길: 국토교통부 표준노드링크 2026-09-14 — 이용허락범위 제한 없음 (지하차도 · 터널은 뺀다)
  * 녹지 · 물: ESA WorldCover 10 m 2021 v200 — CC BY 4.0
결과는 게임이 이미 읽는 HDIO v2 메시(SagajeongDispatchDiorama.ReadDioramaMesh)를 HDIZ로 싼 것이다. 같은 셰이더(HongdalDiorama)로 그린다.
원점은 동마다 그 동의 가운데다(전국으로 넓혀도 float 정밀도가 버티게). 게임 좌표: x = (경도 − 원점)·111200·cos(원점 위도), z = (위도 − 원점)·111200.

표현값(측정값 아님): 차도 폭(차로 수 × 3.2 m + 0.8 m), 연석 1.1 m, 높이 14 m 위 눌러 그리기, 용도별 색, 빛 방향, 동 판 옆면.
높이: 원본 높이 → 지상 층수 × 3 m → 4 m(상징 높이) 순서.

사용: python3 make_public_dong.py <codes 파일 또는 all> <bld 폴더> <admdong.geojson> <links_capital.npz> <wc_capital.npy> <out 폴더>
"""
import sys, os, json, math, struct, hashlib, zlib, time
import numpy as np
import shapely
from shapely.geometry import shape, Polygon, MultiPolygon
import mapbox_earcut as earcut

CODES_ARG, BLD, ADM, LINKS, WC, OUT = sys.argv[1:7]
os.makedirs(OUT, exist_ok=True)
T0 = time.time()
def log(*a): print(f'[{time.time() - T0:7.1f}s]', *a, flush=True)

M_LAT = 111200.0
C_DONG, C_KERB, C_ROAD, C_SKIRT = 0x21323F, 0x3C4D5C, 0x34434F, 0x16222B
ROOF_RES = [0x6D8193, 0x657A8C, 0x74889A, 0x5F7385]
ROOF_GREEN = [0x4F7F6C, 0x487564, 0x568872]
ROOF_COM = [0x8C8FA1, 0x7F8496, 0x9396A7]
ROOF_APT = [0x8795A5, 0x7D8C9D]
ROOF_CIVIC = [0x7D8F86, 0x86978E]
ROOF_WORK = [0x8A8478, 0x7F7A70]
WALLS = [0xA58B80, 0x9AA6B2, 0xB0A694, 0x8F9BA8, 0xA3ADB7, 0x9C8479]
SHOP_SEED = 1000.0
TO_LIGHT = np.array([-0.931, -0.364])
SHADOW_DIR = np.array([0.931, 0.364])
SKIRT = 14.0
GREEN = {10: (1, 0x234434), 20: (2, 0x2B4E3C), 30: (2, 0x2B4E3C), 40: (4, 0x3B4632), 80: (3, 0x1D3E57), 90: (3, 0x1D3E57), 95: (1, 0x234434)}

def rgb(h): return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], dtype=np.float64)

def roof_and_wall(use, r):
    if use == 1: return (ROOF_GREEN[int(r[1] * 3 * 0.999)] if r[2] < 0.38 else ROOF_RES[int(r[1] * 4 * 0.999)]), WALLS[int(r[3] * 6 * 0.999)]
    if use == 2: return ROOF_APT[int(r[1] * 2 * 0.999)], 0xA9B3BD
    if use in (3, 4, 7, 15, 16): return ROOF_COM[int(r[1] * 3 * 0.999)], 0xA8A29D
    if use in (5, 6, 9, 10, 11, 12, 13, 23, 24, 25, 29): return ROOF_CIVIC[int(r[1] * 2 * 0.999)], 0xA4ACA6
    if use in (8, 14, 17, 18, 19, 20, 21, 22, 26): return ROOF_WORK[int(r[1] * 2 * 0.999)], 0x9E978C
    return ROOF_RES[int(r[1] * 4 * 0.999)], WALLS[int(r[3] * 6 * 0.999)]


class Mesh:
    def __init__(self): self.P, self.C, self.U, self.U2, self.T, self.n = [], [], [], [], [], 0
    def add(self, pos, col, tri, uv=None, uv2=None):
        k = len(pos)
        if k == 0: return
        self.P.append(np.asarray(pos, np.float32)); self.T.append(np.asarray(tri, np.int64) + self.n)
        col = np.asarray(col, np.float64)
        self.C.append(np.broadcast_to(np.clip(np.rint(col), 0, 255).astype(np.uint8), (k, 3)) if col.ndim == 1 else np.clip(np.rint(col), 0, 255).astype(np.uint8))
        self.U.append(np.zeros((k, 2), np.float32) if uv is None else np.broadcast_to(np.asarray(uv, np.float32), (k, 2)))
        self.U2.append(np.zeros((k, 2), np.float32) if uv2 is None else np.broadcast_to(np.asarray(uv2, np.float32), (k, 2)))
        self.n += k
    def arrays(self):
        if not self.P: return (np.zeros((0, 3), '<f4'), np.zeros((0, 4), 'u1'), np.zeros((0, 2), '<f4'), np.zeros((0, 2), '<f4'), np.zeros(0, '<i4'))
        c = np.concatenate(self.C); c = np.column_stack([c, np.full(len(c), 255, np.uint8)])
        return (np.concatenate(self.P).astype('<f4'), c.astype('u1'), np.concatenate(self.U).astype('<f4'),
                np.concatenate(self.U2).astype('<f4'), np.concatenate(self.T).astype('<i4').ravel())


def tri_poly(poly):
    """shapely Polygon(게임 좌표) → (점 N×2, 삼각형 색인)."""
    rings = [np.asarray(poly.exterior.coords)[:-1]] + [np.asarray(h.coords)[:-1] for h in poly.interiors]
    rings = [r for r in rings if len(r) >= 3]
    if not rings: return None, None
    verts = np.concatenate(rings)
    ends = np.cumsum([len(r) for r in rings]).astype(np.uint32)
    idx = earcut.triangulate_float64(verts, ends)
    return verts, np.asarray(idx, np.int64).reshape(-1, 3)


def polys_of(g):
    if g is None or g.is_empty: return []
    if isinstance(g, Polygon): return [g]
    if isinstance(g, MultiPolygon): return list(g.geoms)
    return [p for p in getattr(g, 'geoms', []) if isinstance(p, Polygon)]


def flat(mesh, g, y, colour, uv=None):
    for p in polys_of(g):
        v, t = tri_poly(p)
        if v is None or len(t) == 0: continue
        mesh.add(np.column_stack([v[:, 0], np.full(len(v), y), v[:, 1]]), rgb(colour), t[:, ::-1], uv=uv)


def walls(mesh, ring, y0, y1, colour, H, seed, shop, lit=True, uv2_wall=True):
    a = ring; b = np.roll(ring, -1, axis=0)
    d = b - a; L = np.hypot(d[:, 0], d[:, 1]); ok = L > 1e-3
    a, b, d, L = a[ok], b[ok], d[ok], L[ok]
    if len(a) == 0: return
    nx, nz = d[:, 1] / L, -d[:, 0] / L
    f = 0.60 + 0.40 * np.maximum(0.0, nx * TO_LIGHT[0] + nz * TO_LIGHT[1]) if lit else np.ones(len(a))
    k = len(a)
    pos = np.empty((k, 4, 3)); pos[:, 0] = np.column_stack([a[:, 0], np.full(k, y0), a[:, 1]]); pos[:, 1] = np.column_stack([b[:, 0], np.full(k, y0), b[:, 1]])
    pos[:, 2] = np.column_stack([b[:, 0], np.full(k, y1), b[:, 1]]); pos[:, 3] = np.column_stack([a[:, 0], np.full(k, y1), a[:, 1]])
    col = np.repeat((rgb(colour)[None, :] * f[:, None]), 4, axis=0)
    uv = np.zeros((k, 4, 2)); uv[:, 1, 0] = L; uv[:, 2, 0] = L; uv[:, 2, 1] = H; uv[:, 3, 1] = H
    if uv2_wall:
        s = ((seed * 131 + np.arange(k) * 17) % 997).astype(np.float64) + (SHOP_SEED if shop else 0.0)
        uv2 = np.zeros((k, 4, 2)); uv2[:, :, 0] = 1.0; uv2[:, :, 1] = s[:, None]
        uv2 = uv2.reshape(-1, 2)
    else: uv2 = None
    base = np.arange(k)[:, None] * 4
    tri = np.concatenate([base + np.array([0, 1, 2]), base + np.array([0, 2, 3])], axis=1).reshape(-1, 3)
    mesh.add(pos.reshape(-1, 3), col, tri, uv=uv.reshape(-1, 2), uv2=uv2)


def shadow(smesh, ring, h):
    reach = min(h, 8.0) * 0.95
    off = ring + SHADOW_DIR * reach
    k = len(ring)
    a, b, c, d = ring, np.roll(ring, -1, 0), np.roll(off, -1, 0), off
    pos = np.stack([a, b, c, d], axis=1)                       # k × 4 × 2
    pos3 = np.concatenate([pos[..., :1], np.full((k, 4, 1), 0.11), pos[..., 1:]], axis=2).reshape(-1, 3)
    base = np.arange(k)[:, None] * 4
    tri = np.concatenate([base + np.array([0, 1, 2]), base + np.array([0, 2, 3])], axis=1).reshape(-1, 3)
    smesh.add(pos3, rgb(0), tri)
    idx = np.asarray(earcut.triangulate_float64(off, np.array([k], np.uint32)), np.int64).reshape(-1, 3)
    if len(idx): smesh.add(np.column_stack([off[:, 0], np.full(k, 0.11), off[:, 1]]), rgb(0), idx[:, ::-1])


def area2(r): return float(np.sum(r[:, 0] * np.roll(r[:, 1], -1) - np.roll(r[:, 0], -1) * r[:, 1])) / 2


def simplify_ring(r, tol=0.25):
    if len(r) < 4: return r
    p = shapely.simplify(shapely.linearrings(np.vstack([r, r[:1]])), tol)
    c = np.asarray(shapely.get_coordinates(p))[:-1]
    return c if len(c) >= 3 else r


def write_hdio(path, lat0, lon0, bounds, nb, nr, mesh, smesh):
    pos, col, uv, uv2, tri = mesh.arrays()
    spos, _, _, _, stri = smesh.arrays()
    assert len(pos) < 4000000 and np.isfinite(pos).all() and (tri.size == 0 or (tri.min() >= 0 and tri.max() < len(pos)))
    out = bytearray()
    out += b'HDIO' + struct.pack('<i', 2) + struct.pack('<6d', lat0, lon0, *bounds) + struct.pack('<2i', nb, nr)
    out += struct.pack('<i', len(pos)) + pos.tobytes() + col.tobytes() + uv.tobytes() + uv2.tobytes()
    out += struct.pack('<i', tri.size) + tri.tobytes()
    out += struct.pack('<i', len(spos)) + spos.tobytes() + struct.pack('<i', stri.size) + stri.tobytes()
    out += struct.pack('<i', 0) + struct.pack('<ii', 0, 0) + struct.pack('<ii', 0, 0)
    raw = bytes(out)
    sha = hashlib.sha256(raw).hexdigest().upper()
    c = zlib.compressobj(9, zlib.DEFLATED, -15, 9); body = c.compress(raw) + c.flush()
    packed = b'HDIZ' + struct.pack('<ii', 1, len(raw)) + bytes.fromhex(sha) + body
    open(path, 'wb').write(packed)
    return len(raw), sha, len(packed), hashlib.sha256(packed).hexdigest().upper(), len(pos), tri.size // 3


# ── 공통 자료 ──────────────────────────────────────────────────────────
feats = {f['properties']['adm_cd2']: f for f in json.load(open(ADM, encoding='utf-8'))['features'] if f['properties']['sido'] in ('11', '28', '41')}
if CODES_ARG == 'all': codes = sorted(feats)
else: codes = [c.strip() for c in open(CODES_ARG).read().replace(',', '\n').split() if c.strip()]
L = np.load(LINKS)
lxy, lens = L['xy'], L['lens']; lstart = np.concatenate([[0], np.cumsum(lens)])
keep = (L['type'] != 3) & (L['type'] != 4)
lines_ll = shapely.linestrings(lxy, indices=np.repeat(np.arange(len(lens)), lens))
ltree = shapely.STRtree(lines_ll)
wc = np.load(WC, mmap_mode='r'); wcm = json.load(open(WC.replace('.npy', '.json')))
log('ready', len(codes), 'dongs ·', len(lens), 'links')

summary = []
for n_done, code in enumerate(codes):
    path = os.path.join(OUT, f'hongdal-pdong-{code}.bytes')
    f = feats[code]; props = f['properties']
    geo = shape(f['geometry'])
    w, s, e, n = geo.bounds
    lat0, lon0 = round((s + n) / 2, 6), round((w + e) / 2, 6)
    m_lon = M_LAT * math.cos(math.radians(lat0))
    def to_game(xy): return np.column_stack([(xy[:, 0] - lon0) * m_lon, (xy[:, 1] - lat0) * M_LAT])
    bound = shapely.transform(geo, lambda c: to_game(c))
    bound = shapely.make_valid(bound)
    mesh, smesh = Mesh(), Mesh()
    # 1) 동 판과 옆면
    flat(mesh, bound, 0.01, C_DONG)
    for p in polys_of(bound):
        r = np.asarray(p.exterior.coords)[:-1]
        if area2(r) < 0: r = r[::-1]
        walls(mesh, r, -SKIRT, 0.01, C_SKIRT, SKIRT, 0, False, lit=True, uv2_wall=False)
    # 2) 녹지 · 물 (WorldCover 칸, 동 안의 칸 가운데만)
    c0 = max(0, int((w - wcm['west']) / wcm['step'])); c1 = min(wcm['cols'], int((e - wcm['west']) / wcm['step']) + 1)
    r0 = max(0, int((wcm['north'] - n) / wcm['step'])); r1 = min(wcm['rows'], int((wcm['north'] - s) / wcm['step']) + 1)
    greens = 0
    if c1 > c0 and r1 > r0:
        win = np.asarray(wc[r0:r1, c0:c1])
        cls = np.zeros(win.shape, np.int8)
        for v, (k_, _) in GREEN.items(): cls[win == v] = k_
        rr, cc = np.nonzero(cls)
        if len(rr):
            lon_c = wcm['west'] + (c0 + cc + 0.5) * wcm['step']; lat_c = wcm['north'] - (r0 + rr + 0.5) * wcm['step']
            inside = shapely.contains_xy(geo, lon_c, lat_c)
            mask = np.zeros(win.shape, bool); mask[rr[inside], cc[inside]] = True
            cls = np.where(mask, cls, 0)
            for k_, colour in ((1, 0x234434), (2, 0x2B4E3C), (3, 0x1D3E57), (4, 0x3B4632)):
                m_ = cls == k_
                if not m_.any(): continue
                # 줄마다 이어진 칸을 한 사각형으로
                pad = np.pad(m_, ((0, 0), (1, 1)))
                dif = np.diff(pad.astype(np.int8), axis=1)
                rows_s, cols_s = np.nonzero(dif == 1); rows_e, cols_e = np.nonzero(dif == -1)
                lonA = wcm['west'] + (c0 + cols_s) * wcm['step']; lonB = wcm['west'] + (c0 + cols_e) * wcm['step']
                latT = wcm['north'] - (r0 + rows_s) * wcm['step']; latB = latT - wcm['step']
                xa, xb = (lonA - lon0) * m_lon, (lonB - lon0) * m_lon
                zt, zb = (latT - lat0) * M_LAT, (latB - lat0) * M_LAT
                q = len(xa); y = 0.015
                pos = np.stack([np.column_stack([xa, np.full(q, y), zb]), np.column_stack([xb, np.full(q, y), zb]),
                                np.column_stack([xb, np.full(q, y), zt]), np.column_stack([xa, np.full(q, y), zt])], axis=1).reshape(-1, 3)
                base = np.arange(q)[:, None] * 4
                tri = np.concatenate([base + np.array([0, 2, 1]), base + np.array([0, 3, 2])], axis=1).reshape(-1, 3)
                mesh.add(pos, rgb(colour), tri, uv=(float(k_), 0.0))
                greens += q
    # 3) 길 (노드링크, 차로 수로 폭)
    hit = ltree.query(geo, predicate='intersects')
    hit = hit[keep[hit]]
    roads = 0
    if len(hit):
        segs = [to_game(lxy[lstart[i]:lstart[i + 1]]) for i in hit]
        gl = shapely.linestrings(np.concatenate(segs), indices=np.repeat(np.arange(len(segs)), [len(q) for q in segs]))
        half = L['lanes'][hit].clip(1, 8) * 1.6 + 0.4
        bufs = shapely.buffer(gl, half, quad_segs=2)
        road = shapely.intersection(shapely.union_all(bufs), bound)
        kerb = shapely.intersection(shapely.buffer(road, 1.1, quad_segs=2), bound)
        flat(mesh, kerb, 0.03, C_KERB)
        flat(mesh, road, 0.06, C_ROAD)
        roads = len(hit)
    # 4) 건물
    nb = 0; skipped = 0
    bp = os.path.join(BLD, code + '.npz')
    if os.path.exists(bp):
        B = np.load(bp)
        xy = to_game(B['xy']); rl = B['rl']; bn = B['bn']; hole = B['hole']
        rs = np.concatenate([[0], np.cumsum(rl)]); bs = np.concatenate([[0], np.cumsum(bn)])
        seedbase = int(code) % 9973
        for k in range(len(bn)):
            rings = []
            for q in range(bs[k], bs[k + 1]):
                r = simplify_ring(xy[rs[q]:rs[q + 1]])
                if len(r) < 3: continue
                if hole[q]:
                    if area2(r) > 0: r = r[::-1]
                else:
                    if area2(r) < 0: r = r[::-1]
                rings.append((r, bool(hole[q])))
            outers = [r for r, h_ in rings if not h_]
            if not outers or abs(area2(outers[0])) < 2.0: skipped += 1; continue
            H = float(B['height'][k]); fl = int(B['floors'][k])
            if not (0 < H < 600): H = fl * 3.0 if fl > 0 else 4.0
            h = H if H <= 14 else min(36.0, 14 + (H - 14) * 0.4)
            use = int(B['use'][k])
            rnd = hashlib.sha256(f'{code}:{k}'.encode()).digest()[:4]; r4 = [v / 255.0 for v in rnd]
            top, wall = roof_and_wall(use, r4)
            seed = nb
            for r, h_ in rings: walls(mesh, r, 0.0, h, wall, H, seed, use in (3, 4))
            # 지붕: 바깥 링마다 그 안의 구멍과 함께
            for o in outers:
                po = Polygon(o)
                hs = [r for r, h_ in rings if h_ and po.contains(shapely.points(r[0]))]
                verts = np.concatenate([o] + hs); ends = np.cumsum([len(o)] + [len(x) for x in hs]).astype(np.uint32)
                idx = np.asarray(earcut.triangulate_float64(verts, ends), np.int64).reshape(-1, 3)
                if len(idx): mesh.add(np.column_stack([verts[:, 0], np.full(len(verts), h), verts[:, 1]]), rgb(top), idx[:, ::-1])
                shadow(smesh, o, h)
            nb += 1
    raw_len, raw_sha, packed_len, packed_sha, nv, nt = write_hdio(path, lat0, lon0, (w, s, e, n), nb, roads, mesh, smesh)
    ring = np.asarray(max(polys_of(shapely.make_valid(geo)), key=lambda p: p.area).exterior.simplify(0.00005).coords)
    summary.append({'code': code, 'name': props['adm_nm'].split()[-1], 'sgg': props['sgg'], 'sggName': props['sggnm'], 'sido': props['sido'],
                    'resource': f'hongdal-pdong-{code}', 'bytes': packed_len, 'sha256': packed_sha, 'rawBytes': raw_len, 'rawSha256': raw_sha,
                    'buildings': nb, 'skipped': skipped, 'roads': roads, 'greenCells': greens, 'vertices': nv, 'triangles': nt,
                    'originLatitude': lat0, 'originLongitude': lon0, 'west': w, 'south': s, 'east': e, 'north': n,
                    'outline': [round(v, 6) for pt in ring for v in (pt[1], pt[0])]})
    if n_done % 20 == 0 or n_done == len(codes) - 1:
        log(n_done + 1, '/', len(codes), code, props['adm_nm'], 'buildings', nb, 'roads', roads, 'verts', nv, 'packed', packed_len)
json.dump(summary, open(os.path.join(OUT, f'_summary-{os.getpid()}.json'), 'w', encoding='utf-8'), ensure_ascii=False)
log('done', len(summary), 'total packed MB', round(sum(x['bytes'] for x in summary) / 1e6, 1))
