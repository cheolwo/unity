"""수도권 육각 타일(공개판) 굽기 (2026-11-25, 사용자 11:22).

격자: 구 지도 타일(hongdal-tiles-gu.json r1)과 같은 정의 — UTM-K(EPSG:5179) 정육각 · 꼭짓점 위 · 이웃 사이 300 m ·
      번호 = 축 좌표 (q, r) · 원점 = UTM-K (E 1,000,000 · N 2,000,000). 가운데 E = 1e6 + 300(q + r/2), N = 2e6 + 300·(√3/2)·r.
칸마다: 가운데가 든 행정동(가운데가 어느 동에도 들지 않으면 — 바다 — 칸을 두지 않는다), 건물 수, 덮인 몫, 평균 지상층, 주된 쓰임,
        녹지 · 물 몫(WorldCover), 성격(kind — 기존 타일과 같은 번호, 규칙은 아래 KIND, 표현값).
자료: GIS건물통합정보 AL_D010 2026-11-09 · vuski/admdongkor ver20261001 · ESA WorldCover 2021 — 모두 재배포 가능(출처 표시).
결과: hongdal-capital-hex.bytes — 'HHEX' · int32 판(1) · int32 칸 수 · float32 이웃 사이(300) · float32 반지름(구 지도 미터)
      · 칸마다 20바이트(float32 x · float32 y(구 지도 미터) · uint16 동 · uint16 건물 수 · uint8 덮인 몫×255 · uint8 평균층×10
        · uint8 쓰임 · uint8 녹지×255 · uint8 물×255 · uint8 성격 · int16 축 좌표 q(참고)) · int32 JSON 길이 · UTF-8 JSON(동 표 · 출처)
사용: python3 make_capital_hex.py <bld 폴더> <admdong.geojson> <wc_capital.npy> <결과 .bytes>
"""
import sys, os, json, math, struct, time
import numpy as np
import shapely
from shapely.geometry import shape
from pyproj import Transformer

BLD, ADM, WC, OUT = sys.argv[1:5]
T0 = time.time()
def log(*a): print(f'[{time.time() - T0:6.1f}s]', *a, flush=True)
SP = 300.0; R3 = math.sqrt(3)
to_k = Transformer.from_crs('EPSG:4326', 'EPSG:5179', always_xy=True)
to_ll = Transformer.from_crs('EPSG:5179', 'EPSG:4326', always_xy=True)

feats = [f for f in json.load(open(ADM, encoding='utf-8'))['features'] if f['properties']['sido'] in ('11', '28', '41')]
feats.sort(key=lambda f: f['properties']['adm_cd2'])
polys = [shape(f['geometry']) for f in feats]
tree = shapely.STRtree(polys)
dong_of_code = {f['properties']['adm_cd2']: i for i, f in enumerate(feats)}

def axial(E, N):
    x = (E - 1e6) / SP; y = (N - 2e6) / SP
    r = y / (R3 / 2); q = x - r / 2
    # 큐브 반올림
    cx, cz = q, r; cy = -cx - cz
    rx, ry, rz = np.rint(cx), np.rint(cy), np.rint(cz)
    dx, dy, dz = np.abs(rx - cx), np.abs(ry - cy), np.abs(rz - cz)
    fx = (dx > dy) & (dx > dz); fz = ~fx & (dz >= dy)
    rx = np.where(fx, -ry - rz, rx); rz = np.where(fz, -rx - ry, rz)
    return rx.astype(np.int64), rz.astype(np.int64)

# 1) 칸 후보: 수도권 범위를 덮는 모든 축 좌표 → 가운데가 동 안에 드는 칸만
xmin, ymin = to_k.transform(126.35, 36.89); xmax, ymax = to_k.transform(127.85, 38.30)
xmin2, ymax2 = to_k.transform(126.35, 38.30); xmax2, ymin2 = to_k.transform(127.85, 36.89)
E0, E1 = min(xmin, xmin2) - 1000, max(xmax, xmax2) + 1000; N0, N1 = min(ymin, ymin2) - 1000, max(ymax, ymax2) + 1000
r0, r1 = int((N0 - 2e6) / (SP * R3 / 2)) - 1, int((N1 - 2e6) / (SP * R3 / 2)) + 1
qs, rs = [], []
for r in range(r0, r1 + 1):
    qa = int((E0 - 1e6) / SP - r / 2) - 1; qb = int((E1 - 1e6) / SP - r / 2) + 1
    q = np.arange(qa, qb + 1); qs.append(q); rs.append(np.full(len(q), r))
Q = np.concatenate(qs); Rr = np.concatenate(rs)
E = 1e6 + SP * (Q + Rr / 2); N = 2e6 + SP * (R3 / 2) * Rr
lon, lat = to_ll.transform(E, N)
pi, di = tree.query(shapely.points(lon, lat), predicate='within')
owner = np.full(len(Q), -1); owner[pi] = di
sel = owner >= 0
Q, Rr, E, N, lon, lat, owner = Q[sel], Rr[sel], E[sel], N[sel], lon[sel], lat[sel], owner[sel]
log('tiles', len(Q))
key = {(int(a), int(b)): i for i, (a, b) in enumerate(zip(Q, Rr))}

# 2) 건물 → 칸
cnt = np.zeros(len(Q)); area = np.zeros(len(Q)); floors = np.zeros(len(Q)); floored = np.zeros(len(Q)); use_area = np.zeros((len(Q), 30))
for name in sorted(os.listdir(BLD)):
    if not name.endswith('.npz'): continue
    B = np.load(os.path.join(BLD, name))
    xy, rl, bn, hole = B['xy'], B['rl'], B['bn'], B['hole']
    rs_ = np.concatenate([[0], np.cumsum(rl)]); bs = np.concatenate([[0], np.cumsum(bn)])
    first = bs[:-1][bn > 0]
    keep = bn > 0
    a_ = rs_[first]; b_ = rs_[first + 1]
    # 첫 링의 평균점과 넓이(미터, 국소 근사)
    idx = np.repeat(np.arange(len(first)), b_ - a_)
    pts = np.concatenate([xy[s:e] for s, e in zip(a_, b_)])
    cxy = np.zeros((len(first), 2)); np.add.at(cxy, idx, pts); cxy /= (b_ - a_)[:, None]
    lat_c = cxy[:, 1]; mx = 111320 * np.cos(np.radians(lat_c))
    X = (pts[:, 0] - cxy[idx, 0]) * mx[idx]; Y = (pts[:, 1] - cxy[idx, 1]) * 111320
    nxt = np.arange(len(pts)) + 1; ends = np.cumsum(b_ - a_); starts = ends - (b_ - a_)
    nxt[ends - 1] = starts
    cr = X * Y[nxt] - X[nxt] * Y
    ar = np.abs(np.bincount(idx, weights=cr, minlength=len(first))) / 2
    Ek, Nk = to_k.transform(cxy[:, 0], cxy[:, 1])
    qa, ra = axial(np.asarray(Ek), np.asarray(Nk))
    t = np.array([key.get((int(x), int(y)), -1) for x, y in zip(qa, ra)])
    ok = t >= 0
    fl = B['floors'][keep].astype(float); us = B['use'][keep].astype(int).clip(0, 29)
    np.add.at(cnt, t[ok], 1); np.add.at(area, t[ok], ar[ok]); np.add.at(floors, t[ok], fl[ok]); np.add.at(floored, t[ok], (fl[ok] > 0).astype(float))   # 층수가 적힌 건물만 평균
    np.add.at(use_area, (t[ok], us[ok]), ar[ok])
log('buildings', int(cnt.sum()))

# 3) 녹지 · 물: 칸 가운데 둘레 WorldCover 표본(칸 안 정육각 근사: 반지름 150 m 원 안의 칸들)
wc = np.load(WC, mmap_mode='r'); wcm = json.load(open(WC.replace('.npy', '.json')))
green = np.zeros(len(Q)); water = np.zeros(len(Q)); tree_ = np.zeros(len(Q)); crop = np.zeros(len(Q))
offs = [(dr, dc) for dr in range(-16, 17) for dc in range(-20, 21)]
offs = np.array([(dr, dc) for dr, dc in offs if (dr * 9.27) ** 2 + (dc * 7.35) ** 2 <= 150 ** 2])
rr = ((wcm['north'] - lat) / wcm['step']).astype(int); cc = ((lon - wcm['west']) / wcm['step']).astype(int)
CH = 20000
for s in range(0, len(Q), CH):
    R_ = (rr[s:s + CH, None] + offs[None, :, 0]).clip(0, wcm['rows'] - 1); C_ = (cc[s:s + CH, None] + offs[None, :, 1]).clip(0, wcm['cols'] - 1)
    v = np.asarray(wc[R_, C_])
    tree_[s:s + CH] = (v == 10).mean(1); green[s:s + CH] = np.isin(v, (10, 20, 30, 95)).mean(1)
    water[s:s + CH] = np.isin(v, (80, 90)).mean(1); crop[s:s + CH] = (v == 40).mean(1)
log('cover')

HEX_AREA = SP * SP * R3 / 2
cover = (area / HEX_AREA).clip(0, 1)
mean_fl = np.where(floored > 0, floors / np.maximum(floored, 1), 0)
group = {'단독': [1], '공동': [2], '상업': [3, 4, 7, 14, 15, 16], '공공': [5, 6, 9, 10, 11, 12, 13, 23, 24, 25, 29], '공업': [17, 18, 19, 22, 26], '교통': [8, 20], '기타': [0, 21, 27, 28]}
gnames = list(group)
garea = np.column_stack([use_area[:, group[g]].sum(1) for g in gnames])
dom = np.where(area > 0, garea.argmax(1), 6)
kind = np.full(len(Q), 11)
kind[(cnt >= 25) & (dom == 0)] = 10
kind[dom == 3] = 9
kind[(dom == 2) & (cover >= 0.22)] = 8
kind[dom == 1] = 7
kind[dom == 4] = 6
kind[(cnt == 0) | (cover < 0.03)] = 5
kind[(green >= 0.5) & (cover < 0.08)] = 4
kind[(crop >= 0.4) & (cover < 0.08)] = 3
kind[(tree_ >= 0.6) & (cover < 0.05)] = 2
kind[water >= 0.5] = 1
# 구 지도 미터
gx = (lon - 126.94) * 88224; gy = (lat - 37.523) * 111320
out = bytearray(b'HHEX' + struct.pack('<iiff', 1, len(Q), SP, 173.3292))
rec = np.zeros(len(Q), dtype=[('x', '<f4'), ('y', '<f4'), ('d', '<u2'), ('b', '<u2'), ('c', 'u1'), ('f', 'u1'), ('u', 'u1'), ('g', 'u1'), ('w', 'u1'), ('k', 'u1'), ('q', '<i2')])
rec['x'], rec['y'], rec['d'] = gx, gy, owner
rec['b'] = cnt.clip(0, 65535); rec['c'] = np.rint(cover * 255); rec['f'] = np.rint(mean_fl.clip(0, 25.5) * 10)
rec['u'] = dom; rec['g'] = np.rint(green * 255); rec['w'] = np.rint(water * 255); rec['k'] = kind; rec['q'] = Q.astype(np.int16)
assert rec.dtype.itemsize == 20
out += rec.tobytes()
table = {'schema': 'hongdal-capital-hex.v1', 'revision': 'capital-hex-20261125-r1', 'distribution': '배포 가능(출처 표시)',
         'grid': 'UTM-K(EPSG:5179) 정육각 · 꼭짓점 위 · 이웃 사이 300 m · 원점 (E 1,000,000 · N 2,000,000) — 구 지도 타일 r1과 같다',
         'kinds': ['역', '물', '산', '들', '공원', '빈터', '공장·창고', '아파트', '번화가', '학교·공공', '골목', '섞인 동네', '자료 준비 전'], 'uses': gnames,
         'kindRule': '물 ≥ 50 % · 산(나무 ≥ 60 %, 덮인 몫 < 5 %) · 들(경작 ≥ 40 %) · 공원(녹지 ≥ 50 %) · 빈터(덮인 몫 < 3 %) · 그 밖은 건물 넓이가 가장 큰 쓰임(표현 규칙)',
         'sources': ['건물: 국토교통부 GIS건물통합정보 AL_D010 2026-11-09(이용허락범위 제한 없음)', '경계: vuski/admdongkor ver20261001(통계청 SGIS 공공누리 제1유형 · CC BY 4.0)', '녹지 · 물: ESA WorldCover 2021 v200(CC BY 4.0)'],
         'dongs': [{'code': f['properties']['adm_cd2'], 'name': f['properties']['adm_nm'].split()[-1], 'sgg': f['properties']['sggnm'], 'sido': f['properties']['sido']} for f in feats]}
js = json.dumps(table, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
out += struct.pack('<i', len(js)) + js
open(OUT, 'wb').write(out)
log('wrote', OUT, len(out), 'kinds', np.bincount(kind, minlength=13).tolist())
