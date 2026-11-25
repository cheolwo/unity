"""공개판 동 디오라마 1단계: GIS건물통합정보(AL_D010)를 행정동별로 나눈다 (2026-11-25, 사용자 11:22).

  자료: 국토교통부 GIS건물통합정보 AL_D010 2026-11-09(공공데이터포털 · 브이월드, 이용허락범위 제한 없음)
  경계: vuski/admdongkor ver20261001 (통계청 SGIS 공공누리 제1유형 · 추가분 CC BY 4.0)
  주소 자료(주소기반산업지원서비스)는 쓰지 않는다.

사용: python3 prep_buildings.py <AL_D010 zip> <admdong.geojson> <결과 폴더>
결과: <결과>/<행정동코드>.npz — xy(경위도 float64, 점), ring(링 시작 · 끝), bring(건물마다 링 시작 · 끝), hole(링이 구멍인가),
      use(용도 코드 앞 두 자리 정수), height(m, 없으면 0), floors(지상층, 없으면 0)
"""
import sys, os, zipfile, json, time, collections
import numpy as np
import shapefile
from pyproj import Transformer
import shapely
from shapely.geometry import shape

ZIP, ADM, OUT = sys.argv[1:4]
os.makedirs(OUT, exist_ok=True)
T0 = time.time()
def log(*a): print(f'[{time.time() - T0:6.1f}s]', *a, flush=True)

feats = [f for f in json.load(open(ADM, encoding='utf-8'))['features'] if f['properties']['sido'] in ('11', '28', '41')]
codes = [f['properties']['adm_cd2'] for f in feats]
polys = [shape(f['geometry']) for f in feats]
tree = shapely.STRtree(polys)
log('dongs', len(polys))

work = os.path.join(OUT, '_unzip'); os.makedirs(work, exist_ok=True)
z = zipfile.ZipFile(ZIP)
ONLY = set(sys.argv[4].split('|')) if len(sys.argv) > 4 else None     # 한 zip에 셰이프파일이 여럿(경기: 세 조각)
bases = []
for info in z.infolist():
    if info.filename.lower().endswith('.shp'):
        stem = info.filename[:-4]
        if ONLY is None or stem in ONLY: bases.append(stem)
tr = Transformer.from_crs('EPSG:5186', 'EPSG:4326', always_xy=True)

per = collections.defaultdict(list)    # 코드 → [(xy, ring, bring, hole, use, h, fl)] 조각들
CHUNK = 100000
total = kept = unassigned = 0

def flush(buf):
    global kept, unassigned
    if not buf: return
    pts, ring_len, ring_hole, b_nr, use, hh, fl = [], [], [], [], [], [], []
    for points, parts, u, h, f in buf:
        parts = list(parts) + [len(points)]
        nr = 0
        for a, b in zip(parts[:-1], parts[1:]):
            seg = points[a:b]
            if len(seg) >= 2 and seg[0] == seg[-1]: seg = seg[:-1]
            if len(seg) < 3: continue
            arr = np.asarray(seg, dtype=np.float64)
            x, y = arr[:, 0], arr[:, 1]
            s = float(np.sum(x * np.roll(y, -1) - np.roll(x, -1) * y))
            pts.append(arr); ring_len.append(len(arr)); ring_hole.append(s > 0); nr += 1   # 셰이프파일: 바깥은 시계 방향(넓이 음)
        b_nr.append(nr); use.append(u); hh.append(h); fl.append(f)
    xy = np.concatenate(pts)
    lon, lat = tr.transform(xy[:, 0], xy[:, 1])
    xy = np.column_stack([lon, lat])
    ring_len = np.asarray(ring_len); ring_start = np.concatenate([[0], np.cumsum(ring_len)])
    b_nr = np.asarray(b_nr); b_start = np.concatenate([[0], np.cumsum(b_nr)])
    ring_hole = np.asarray(ring_hole)
    # 건물 대표점: 첫 바깥 링의 평균
    cx = np.zeros(len(b_nr)); cy = np.zeros(len(b_nr)); ok = b_nr > 0
    for k in np.nonzero(ok)[0]:
        a = ring_start[b_start[k]]; b = ring_start[b_start[k] + 1]
        cx[k] = xy[a:b, 0].mean(); cy[k] = xy[a:b, 1].mean()
    pidx, didx = tree.query(shapely.points(cx, cy), predicate='within')
    owner = np.full(len(b_nr), -1); owner[pidx] = didx
    unassigned += int(((owner < 0) & ok).sum())
    for d in np.unique(owner[(owner >= 0) & ok]):
        sel = np.nonzero((owner == d) & ok)[0]
        rings = np.concatenate([np.arange(b_start[k], b_start[k + 1]) for k in sel])
        rl = ring_len[rings]
        xys = np.concatenate([xy[ring_start[q]:ring_start[q + 1]] for q in rings])
        per[codes[d]].append((xys, rl, b_nr[sel], ring_hole[rings], np.asarray(use)[sel], np.asarray(hh)[sel], np.asarray(fl)[sel]))
        kept += len(sel)

for stem in bases:
    for ext in ('.shp', '.shx', '.dbf', '.prj', '.cpg'):
        z.extract(stem + ext, work)
    log('unzipped', stem)
    r = shapefile.Reader(os.path.join(work, stem), encoding='euc-kr')
    names = [f[0] for f in r.fields[1:]]
    I_USE, I_H, I_FL = names.index('A8'), names.index('A16'), names.index('A26')
    buf = []
    for sr in r.iterShapeRecords():
        total += 1
        rec, shp = sr.record, sr.shape
        if not shp.points: continue
        try: u = int(str(rec[I_USE] or '0')[:2] or 0)
        except ValueError: u = 0
        h = float(rec[I_H] or 0.0); f = int(rec[I_FL] or 0)
        buf.append((shp.points, shp.parts, u, h, f))
        if len(buf) >= CHUNK:
            flush(buf); buf = []; log('read', total, 'kept', kept, 'unassigned', unassigned)
    flush(buf)
    r.close()
    for ext in ('.shp', '.shx', '.dbf', '.prj', '.cpg'): os.remove(os.path.join(work, stem + ext))
    log('read', total, 'kept', kept, 'unassigned', unassigned)
for code, pieces in per.items():
    xy = np.concatenate([p[0] for p in pieces]); rl = np.concatenate([p[1] for p in pieces]); bn = np.concatenate([p[2] for p in pieces])
    hole = np.concatenate([p[3] for p in pieces]); use = np.concatenate([p[4] for p in pieces]).astype(np.uint8)
    hh = np.concatenate([p[5] for p in pieces]).astype(np.float32); fl = np.concatenate([p[6] for p in pieces]).astype(np.int16)
    path = os.path.join(OUT, code + '.npz')
    if os.path.exists(path):        # 시도 경계를 넘는 동은 없지만, 두 번 돌릴 때를 위해 이어 붙인다
        o = np.load(path)
        xy = np.concatenate([o['xy'], xy]); rl = np.concatenate([o['rl'], rl]); bn = np.concatenate([o['bn'], bn]); hole = np.concatenate([o['hole'], hole])
        use = np.concatenate([o['use'], use]); hh = np.concatenate([o['height'], hh]); fl = np.concatenate([o['floors'], fl])
    np.savez(path, xy=xy, rl=rl.astype(np.int32), bn=bn.astype(np.int32), hole=hole, use=use, height=hh, floors=fl)
log('dongs written', len(per))
os.rmdir(work)
json.dump({'zip': os.path.basename(ZIP), 'members': bases, 'total': total, 'kept': kept, 'unassigned': unassigned, 'dongs': len(per)},
          open(os.path.join(OUT, '_receipt-' + os.path.basename(ZIP) + ('-' + str(len(bases)) + 'parts' if len(bases) > 1 else '') + '.json'), 'w'), ensure_ascii=False)
