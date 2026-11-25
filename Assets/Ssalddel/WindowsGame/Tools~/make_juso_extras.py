"""주소 자료(주소기반산업지원서비스, 2026-10 기준 서울)에서 굽기에 덧입힐 것을 동별로 뽑는다 (2026-11-17).

사용자가 받은 원문(zip)은 PC 안에서만 읽는다. 이 도구가 쓰는 결과는 게임 좌표(ENU)로 옮겨 동별로 자른 「구운 것」이다.
  - 건물 출입구(TL_SPBD_ENTRC): 동 안의 점 → 굽기 도구가 그 건물의 문으로 쓴다(가상 출입구 대신).
  - 도로명주소(TL_SPBD_BULD + TL_SPRD_MANAGE): 굽기 입력 건물(al-d010 행)의 넓이 중심을 품은 주소 건물의 도로명 · 번호 · 기초구역번호 · 건물명.
  - 지하철역(TL_SPSB_STATN) · 지하철 출입구(TL_SPSB_ENTRC): 동 안의 역 이름 · 자리.
처음 한 번 원문을 읽어 PC 작업 셸의 캐시(~/edit-20261116/juso/cache)에 둔다(사용자 폴더 밖, PC 안).
사용: python3 -I -B make_juso_extras.py <juso raw 폴더> <공식 경계 고리 5186.json.gz> <굽기 입력 건물 json.gz> <codes,...> <out.json.gz>
넘파이만 쓴다.
"""
import gzip, hashlib, json, math, os, struct, sys, zipfile
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gu_geo as g
RAW, RINGS, BLD, CODES, OUT = sys.argv[1:6]
CODES = [c for c in CODES.split(',') if c]
CACHE = os.path.expanduser('~/edit-20261116/juso/cache')
ORIGIN = (37.580912, 127.088502)
A, F = 6378137.0, 1 / 298.257223563; E2 = F * (2 - F)
def ecef(lat, lon):
    la, lo = np.radians(lat), np.radians(lon); n = A / np.sqrt(1 - E2 * np.sin(la) ** 2)
    return n * np.cos(la) * np.cos(lo), n * np.cos(la) * np.sin(lo), n * (1 - E2) * np.sin(la)
O_ = [float(v) for v in ecef(np.array(ORIGIN[0]), np.array(ORIGIN[1]))]
SLA, CLA = math.sin(math.radians(ORIGIN[0])), math.cos(math.radians(ORIGIN[0])); SLO, CLO = math.sin(math.radians(ORIGIN[1])), math.cos(math.radians(ORIGIN[1]))
def enu(lat, lon):
    x, y, z = ecef(np.asarray(lat, float), np.asarray(lon, float)); dx, dy, dz = x - O_[0], y - O_[1], z - O_[2]
    return -SLO * dx + CLO * dy, -SLA * CLO * dx - SLA * SLO * dy + CLA * dz
# UTM-K(EPSG:5179) ↔ 경위도 ↔ EPSG:5186 (GRS80)
ga, gf = 6378137.0, 1 / 298.257222101; ge2 = 2 * gf - gf * gf; gep2 = ge2 / (1 - ge2)
def _M(p):
    e4, e6 = ge2 * ge2, ge2 ** 3
    return ga * ((1 - ge2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * p - (3 * ge2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * np.sin(2 * p)
                 + (15 * e4 / 256 + 45 * e6 / 1024) * np.sin(4 * p) - (35 * e6 / 3072) * np.sin(6 * p))
def tm_fwd(lat, lon, lon0, lat0, k0, FE, FN):
    p, l = np.radians(lat), np.radians(lon); lon0, lat0 = math.radians(lon0), math.radians(lat0)
    N = ga / np.sqrt(1 - ge2 * np.sin(p) ** 2); T = np.tan(p) ** 2; C = gep2 * np.cos(p) ** 2; Aa = (l - lon0) * np.cos(p)
    x = FE + k0 * N * (Aa + (1 - T + C) * Aa ** 3 / 6 + (5 - 18 * T + T * T + 72 * C - 58 * gep2) * Aa ** 5 / 120)
    y = FN + k0 * (_M(p) - _M(lat0) + N * np.tan(p) * (Aa * Aa / 2 + (5 - T + 9 * C + 4 * C * C) * Aa ** 4 / 24 + (61 - 58 * T + T * T + 600 * C - 330 * gep2) * Aa ** 6 / 720))
    return x, y
def tm_inv(x, y, lon0, lat0, k0, FE, FN):
    lon0, lat0 = math.radians(lon0), math.radians(lat0)
    mu = (_M(lat0) + (y - FN) / k0) / (ga * (1 - ge2 / 4 - 3 * ge2 * ge2 / 64 - 5 * ge2 ** 3 / 256)); e1 = (1 - math.sqrt(1 - ge2)) / (1 + math.sqrt(1 - ge2))
    p1 = (mu + (3 * e1 / 2 - 27 * e1 ** 3 / 32) * np.sin(2 * mu) + (21 * e1 ** 2 / 16 - 55 * e1 ** 4 / 32) * np.sin(4 * mu)
          + (151 * e1 ** 3 / 96) * np.sin(6 * mu) + (1097 * e1 ** 4 / 512) * np.sin(8 * mu))
    s, c, t = np.sin(p1), np.cos(p1), np.tan(p1)
    C1 = gep2 * c * c; T1 = t * t; N1 = ga / np.sqrt(1 - ge2 * s * s); R1 = ga * (1 - ge2) / (1 - ge2 * s * s) ** 1.5; D = (x - FE) / (N1 * k0)
    lat = p1 - (N1 * t / R1) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * gep2) * D ** 4 / 24 + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * gep2 - 3 * C1 * C1) * D ** 6 / 720)
    lon = lon0 + (D - (1 + 2 * T1 + C1) * D ** 3 / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * gep2 + 24 * T1 * T1) * D ** 5 / 120) / c
    return np.degrees(lat), np.degrees(lon)
UTMK = (127.5, 38.0, 0.9996, 1000000.0, 2000000.0); G5186 = (127.0, 38.0, 1.0, 200000.0, 600000.0)
def k_to_5186(x, y):
    lat, lon = tm_inv(np.asarray(x, float), np.asarray(y, float), *UTMK); return tm_fwd(lat, lon, *G5186)
def k_to_enu(x, y):
    lat, lon = tm_inv(np.asarray(x, float), np.asarray(y, float), *UTMK); return enu(lat, lon)

def dbf_rows(data, want):
    n = struct.unpack('<I', data[4:8])[0]; hl, rl = struct.unpack('<HH', data[8:12]); pos, fs = 1, {}
    for i in range(32, hl - 1, 32):
        nm = data[i:i + 11].split(b'\0')[0].decode(); ln = data[i + 16]; fs[nm] = (pos, ln); pos += ln
    out = []
    for k in range(n):
        base = hl + k * rl
        out.append([data[base + fs[w][0]: base + fs[w][0] + fs[w][1]].decode('cp949', 'replace').strip() for w in want])
    return out
def shp_records(data):
    off, recs = 100, []
    while off < len(data):
        clen = struct.unpack('>i', data[off + 4: off + 8])[0] * 2; recs.append(data[off + 8: off + 8 + clen]); off += 8 + clen
    return recs
def poly_parts(rec):
    t = struct.unpack('<i', rec[:4])[0]
    if t == 0: return []
    nparts, npts = struct.unpack('<ii', rec[36:44]); parts = list(struct.unpack('<%di' % nparts, rec[44:44 + 4 * nparts])) + [npts]
    pts = np.frombuffer(rec[44 + 4 * nparts: 44 + 4 * nparts + 16 * npts], '<f8').reshape(-1, 2)
    return [pts[parts[i]:parts[i + 1]] for i in range(nparts)]

def build_cache():
    os.makedirs(CACHE, exist_ok=True)
    ez = zipfile.ZipFile(os.path.join(RAW, '전자지도_전체분_서울특별시.zip')); oz = zipfile.ZipFile(os.path.join(RAW, '기타자료_전체분_서울특별시.zip'))
    recs = shp_records(ez.read('11000/TL_SPBD_ENTRC.shp'))
    exy = np.array([struct.unpack('<dd', r[4:20]) if struct.unpack('<i', r[:4])[0] == 1 else (np.nan, np.nan) for r in recs])
    ekey = np.array([a + ':' + b for a, b in dbf_rows(ez.read('11000/TL_SPBD_ENTRC.dbf'), ['SIG_CD', 'BUL_MAN_NO'])])
    np.savez(os.path.join(CACHE, 'entrances.npz'), xy=exy, key=ekey)
    recs = shp_records(ez.read('11000/TL_SPBD_BULD.shp'))
    attrs = dbf_rows(ez.read('11000/TL_SPBD_BULD.dbf'), ['SIG_CD', 'RN_CD', 'BULD_MNNM', 'BULD_SLNO', 'BSI_ZON_NO', 'BULD_NM', 'POS_BUL_NM', 'BDTYP_CD', 'BUL_MAN_NO'])
    coords, offs, starts, box = [], [0], [], []
    for r in recs:
        parts = poly_parts(r); starts.append(len(offs) - 1)
        for p in parts: coords.append(p); offs.append(offs[-1] + len(p))
        b = np.concatenate(parts) if parts else np.zeros((1, 2)); box.append((b[:, 0].min(), b[:, 1].min(), b[:, 0].max(), b[:, 1].max()))
    starts.append(len(offs) - 1)
    np.savez(os.path.join(CACHE, 'buildings.npz'), xy=np.concatenate(coords), offs=np.array(offs), starts=np.array(starts), box=np.array(box))
    roads = {k[0] + ':' + k[1]: k[2] for k in dbf_rows(ez.read('11000/TL_SPRD_MANAGE.dbf'), ['SIG_CD', 'RN_CD', 'RN'])}
    names = [n for n in oz.namelist()]
    st_shp = [n for n in names if n.endswith('TL_SPSB_STATN.11000.shp')][0]; st_dbf = st_shp[:-3] + 'dbf'
    se_shp = [n for n in names if n.endswith('TL_SPSB_ENTRC.11000.shp')][0]; se_dbf = se_shp[:-3] + 'dbf'
    st = []
    for r, (nm,) in zip(shp_records(oz.read(st_shp)), dbf_rows(oz.read(st_dbf), ['KOR_SUB_NM'])):
        parts = poly_parts(r)
        if not parts: continue
        p = max(parts, key=len); st.append([nm, float(p[:, 0].mean()), float(p[:, 1].mean())])
    se = []
    for r, (no,) in zip(shp_records(oz.read(se_shp)), dbf_rows(oz.read(se_dbf), ['ENTRC_NO'])):
        if struct.unpack('<i', r[:4])[0] == 1: x, y = struct.unpack('<dd', r[4:20]); se.append([no, x, y])
    with gzip.open(os.path.join(CACHE, 'attrs.json.gz'), 'wt', encoding='utf-8') as fh:
        json.dump({'buildings': attrs, 'roads': roads, 'stations': st, 'stationEntrances': se}, fh, ensure_ascii=False)
    print('cache built', len(exy), len(recs), len(roads), len(st), len(se), flush=True)

if not os.path.exists(os.path.join(CACHE, 'attrs.json.gz')) or 'key' not in np.load(os.path.join(CACHE, 'entrances.npz')).files: build_cache()
if not CODES: sys.exit(0)
_en = np.load(os.path.join(CACHE, 'entrances.npz')); ent = _en['xy']; ent_key = _en['key']
ent_by_key = {}
for i_, k_ in enumerate(ent_key.tolist()): ent_by_key.setdefault(k_, []).append(i_)
_bz = np.load(os.path.join(CACHE, 'buildings.npz')); bz = {k: _bz[k] for k in ('xy', 'offs', 'starts', 'box')}   # 한 번만 읽는다(NpzFile은 꺼낼 때마다 다시 푼다)
at = json.load(gzip.open(os.path.join(CACHE, 'attrs.json.gz'), 'rt', encoding='utf-8'))
rings_all = json.loads(gzip.open(RINGS, 'rb').read().decode('utf-8'))['rings']
bld = json.load(gzip.open(BLD, 'rt', encoding='utf-8'))

def pip(px, py, rings):
    inside = np.zeros(len(px), bool)
    for r in rings:
        r = np.asarray(r, float); x1, y1 = r[:, 0], r[:, 1]; x2, y2 = np.roll(x1, 1), np.roll(y1, 1)
        for a1, b1, a2, b2 in zip(x1, y1, x2, y2):
            if a1 == a2 and b1 == b2: continue
            m = (b1 > py) != (b2 > py)
            if not m.any(): continue
            xi = (a2 - a1) * (py[m] - b1) / ((b2 - b1) if b2 != b1 else 1e-12) + a1
            inside[np.nonzero(m)[0][px[m] < xi]] ^= True
    return inside
def rnd(v): return [round(float(x), 2) for x in v]

ex, ey = k_to_5186(ent[:, 0], ent[:, 1])
st_xy = np.array([[s[1], s[2]] for s in at['stations']]); sx, sy = k_to_5186(st_xy[:, 0], st_xy[:, 1])
se_xy = np.array([[s[1], s[2]] for s in at['stationEntrances']]); qx, qy = k_to_5186(se_xy[:, 0], se_xy[:, 1])
out = {'schema': 'hongdal-juso-extras.v1', 'note': '주소기반산업지원서비스 2026-10 서울 자료를 동별로 잘라 게임 좌표(ENU)로 옮긴 덧입힘. 원문은 PC 안에만. 내부 검토용 · 배포 아님',
       'coordinateFrame': {'method': 'WGS84-ECEF-ENU-at-zero-altitude', 'originLatitude': ORIGIN[0], 'originLongitude': ORIGIN[1]},
       'membership': os.path.basename(RINGS), 'dongs': {}, 'addresses': {}, 'doors': {}}
for code in CODES:
    rings = rings_all[code]; allr = np.concatenate([np.asarray(r, float) for r in rings]); x0, y0 = allr.min(0); x1, y1 = allr.max(0)
    sel = np.nonzero((ex >= x0) & (ex <= x1) & (ey >= y0) & (ey <= y1))[0]; sel = sel[pip(ex[sel], ey[sel], rings)]
    e_, n_ = k_to_enu(ent[sel, 0], ent[sel, 1])
    ss = np.nonzero((sx >= x0) & (sx <= x1) & (sy >= y0) & (sy <= y1))[0]; ss = ss[pip(sx[ss], sy[ss], rings)] if len(ss) else ss
    st = []
    for i in ss:
        a_, b_ = k_to_enu(np.array([st_xy[i, 0]]), np.array([st_xy[i, 1]])); st.append({'name': at['stations'][i][0], 'x': round(float(a_[0]), 2), 'z': round(float(b_[0]), 2)})
    qs = np.nonzero((qx >= x0) & (qx <= x1) & (qy >= y0) & (qy <= y1))[0]; qs = qs[pip(qx[qs], qy[qs], rings)] if len(qs) else qs
    qe, qn = k_to_enu(se_xy[qs, 0], se_xy[qs, 1]) if len(qs) else (np.zeros(0), np.zeros(0))
    out['dongs'][code] = {'entrances': [rnd(p) for p in zip(e_, n_)], 'stations': st,
                          'stationEntrances': [[round(float(a_), 2), round(float(b_), 2), at['stationEntrances'][i][0]] for a_, b_, i in zip(qe, qn, qs)]}
    # 도로명주소: 굽기 입력 건물의 넓이 중심을 품은 주소 건물
    bl = bld['dongs'][code]['buildings']
    cen = []
    for b in bl:
        ext = max((np.asarray(p[0], float) for p in b['polys']), key=lambda r: abs(np.sum(r[:, 0] * np.roll(r[:, 1], -1) - np.roll(r[:, 0], -1) * r[:, 1])))
        cen.append(ext.mean(0))
    cen = np.array(cen) if cen else np.zeros((0, 2))
    bx0, by0 = k_to_5186(bz['box'][:, 0], bz['box'][:, 1]); bx1, by1 = k_to_5186(bz['box'][:, 2], bz['box'][:, 3])
    near = np.nonzero((bx1 >= x0 - 50) & (bx0 <= x1 + 50) & (by1 >= y0 - 50) & (by0 <= y1 + 50))[0]
    hit = 0
    if len(cen):
        assigned = np.full(len(cen), -1)
        for j in near:
            m = (cen[:, 0] >= bx0[j] - 1) & (cen[:, 0] <= bx1[j] + 1) & (cen[:, 1] >= by0[j] - 1) & (cen[:, 1] <= by1[j] + 1) & (assigned < 0)
            if not m.any(): continue
            idx = np.nonzero(m)[0]
            parts = [bz['xy'][bz['offs'][k]:bz['offs'][k + 1]] for k in range(bz['starts'][j], bz['starts'][j + 1])]
            parts = [np.stack(k_to_5186(p[:, 0], p[:, 1]), 1) for p in parts]
            inn = pip(cen[idx, 0], cen[idx, 1], parts)
            assigned[idx[inn]] = j
        for b, j in zip(bl, assigned):
            if j < 0: continue
            sig, rn, mn, sl, zon, nm, pos, typ, bul = at['buildings'][j]
            eis = ent_by_key.get(sig + ':' + bul, [])          # 그 주소 건물의 출입구(건물관리번호로 잇는다): 굽기 건물의 문
            if eis:
                de_, dn_ = k_to_enu(ent[eis, 0], ent[eis, 1])
                out['doors']['al-d010:20261109:11:%d' % b['row']] = [rnd(q) for q in zip(de_, dn_)]
            road = at['roads'].get(sig + ':' + rn, '')
            if not road: continue
            num = (mn or '').lstrip('0') or '0'
            if sl and sl.lstrip('0'): num += '-' + sl.lstrip('0')
            out['addresses']['al-d010:20261109:11:%d' % b['row']] = [road, num, zon, nm or pos]; hit += 1
    print(code, bld['dongs'][code]['name'], 'doors', sum(1 for b in bl if 'al-d010:20261109:11:%d' % b['row'] in out['doors']), 'entrances', len(sel), 'stations', [s['name'] for s in st], 'stationEntrances', len(qs), 'addresses', hit, '/', len(bl), flush=True)
raw = json.dumps(out, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
with gzip.GzipFile(OUT, 'wb', mtime=0) as fh: fh.write(raw)
print('sha256(json)', hashlib.sha256(raw).hexdigest())
