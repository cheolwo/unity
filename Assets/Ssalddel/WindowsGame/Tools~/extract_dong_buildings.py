"""Codex 서울 건물 입력(r2 SQLite, EPSG:5186)에서 행정동의 건물을 꺼낸다 (2026-11-16, 지도 자료 적용 S1).

표준 라이브러리만 쓴다(사용자 PC 작업 셸에서 돈다). 읽기 전용으로 연다. 원문 값은 바꾸지 않는다.
- 동 소속은 Codex의 building_dong(편집 후보 경계와 면적 교차)을 그대로 따른다. 경계에 걸친 건물은 두 동 모두에 든다.
  그래서 건물마다 교차 넓이가 가장 큰 동(primaryDong)을 같이 적는다. 굽기 입력은 이 동 하나에만 건물을 둔다(r2와 같은 「한 동에 하나」).
- 원문 도형이 유효(ValidSourceGeometry)한 것만 꺼내고, 나머지는 수만 센다.
- 좌표는 원본 EPSG:5186 그대로(소수 둘째 자리), 구멍 고리도 그대로 둔다(굽기 도구가 바깥 고리만 쓴다).
사용: python3 -I -B extract_dong_buildings.py <seoul-building-inputs.sqlite> <codes,...> <out.json.gz> [공식 경계 고리 5186.json.gz]
- 넷째 인자(2026-11-17, 공식 행정동 경계 굽기): 동 소속을 그 경계로 다시 정한다. 경계 상자 안의 건물(R-tree)을 꺼내,
  가장 큰 바깥 고리의 넓이 중심이 그 동 경계(짝홀 규칙) 안에 있으면 그 동 하나에 둔다. 이때 intersectionM2는 None이다.
"""
import sqlite3, struct, json, gzip, sys, hashlib, os
db, codes, out = sys.argv[1], sys.argv[2].split(','), sys.argv[3]
c = sqlite3.connect('file:' + db + '?mode=ro', uri=True)
def polys_of(w):
    o = [0]
    def rd(fmt, n):
        v = struct.unpack_from(fmt, w, o[0]); o[0] += n; return v
    def geom():
        bo = w[o[0]]; o[0] += 1; e = '<' if bo == 1 else '>'
        t, = rd(e + 'I', 4); t %= 1000
        if t == 3:
            nr, = rd(e + 'I', 4); rs = []
            for _ in range(nr):
                n, = rd(e + 'I', 4); pts = rd(e + '%dd' % (2 * n), 16 * n)
                rs.append([[round(pts[2 * i], 2), round(pts[2 * i + 1], 2)] for i in range(n)])
            return [rs]
        if t == 6:
            n, = rd(e + 'I', 4); return [p for _ in range(n) for p in geom()]
        raise ValueError('WKB type %d' % t)
    return geom()
meta = {k: json.loads(v) for k, v in c.execute('select key, value_json from metadata')}
result = {'schema': 'hongdal-dong-building-extract.v1', 'source': os.path.basename(db), 'sourceMetadata': {k: meta.get(k) for k in ('source', 'boundary', 'heightPolicy', 'identityPolicy', 'distributionApproved')},
          'crs': 'EPSG:5186', 'dongs': {}}
OFFICIAL = None
if len(sys.argv) > 4:
    OFFICIAL = json.loads(gzip.open(sys.argv[4], 'rb').read().decode('utf-8'))['rings']
    result['membership'] = {'method': 'CentroidOfLargestExteriorInOfficialBoundary', 'boundaryFile': os.path.basename(sys.argv[4]),
                            'boundarySha256': hashlib.sha256(open(sys.argv[4], 'rb').read()).hexdigest()}
def ring_centroid(r):
    x = [p[0] for p in r]; y = [p[1] for p in r]; n = len(r); a = cx = cy = 0.0
    for i in range(n):
        j = (i + 1) % n; cr = x[i] * y[j] - x[j] * y[i]; a += cr; cx += (x[i] + x[j]) * cr; cy += (y[i] + y[j]) * cr
    if abs(a) < 1e-9: return sum(x) / n, sum(y) / n
    return cx / (3 * a), cy / (3 * a)
def ring_area(r):
    return abs(sum(r[i][0] * r[(i + 1) % len(r)][1] - r[(i + 1) % len(r)][0] * r[i][1] for i in range(len(r)))) / 2
def inside_rings(px, py, rings):
    c = False
    for r in rings:
        n = len(r)
        for i in range(n):
            x1, y1 = r[i]; x2, y2 = r[i - 1]
            if (y1 > py) != (y2 > py) and px < (x2 - x1) * (py - y1) / (y2 - y1) + x1: c = not c
    return c
def official_rows(code):
    rings = OFFICIAL[code]
    xs = [p[0] for r in rings for p in r]; ys = [p[1] for r in rings for p in r]
    return c.execute('''select b.source_row, b.source_id, b.pnu, b.legal_code, b.source_fields_json, b.height_m, b.ground_floors, b.geometry_status, b.area_m2,
                               NULL, b.geometry_wkb
                        from building_bounds r join building b on b.source_row = r.source_row
                        where r.max_x >= ? and r.min_x <= ? and r.max_y >= ? and r.min_y <= ? order by b.source_row''', (min(xs), max(xs), min(ys), max(ys))).fetchall(), rings
for code in codes:
    name = c.execute('select name, district_name from dong where code=?', (code,)).fetchone()
    if OFFICIAL is not None:
        rows, rings = official_rows(code); kept, skipped, outside = [], {}, 0
        for sr, sid, pnu, legal, sf, h, fl, st, area, inter, wkb in rows:
            if st != 'ValidSourceGeometry' or wkb is None:
                skipped[st] = skipped.get(st, 0) + 1; continue
            polys = polys_of(wkb)
            ext = max((p[0] for p in polys), key=ring_area)
            if not inside_rings(*ring_centroid(ext), rings): outside += 1; continue
            f = json.loads(sf)
            kept.append({'primaryDong': code, 'row': sr, 'sourceId': sid, 'pnu': pnu, 'legal': legal, 'use': f.get('A8', ''), 'useName': f.get('A9', ''), 'address': f.get('A4', ''),
                         'heightM': h, 'floors': fl, 'areaM2': area, 'intersectionM2': None, 'polys': polys})
        result['dongs'][code] = {'name': name[0] if name else None, 'district': name[1] if name else None, 'rows': len(rows), 'kept': len(kept), 'skipped': skipped,
                                 'outsideOfficial': outside, 'buildings': kept}
        print(code, name, 'box rows', len(rows), 'kept', len(kept), 'outside', outside, 'skipped', skipped)
        continue
    rows = c.execute('''select b.source_row, b.source_id, b.pnu, b.legal_code, b.source_fields_json, b.height_m, b.ground_floors, b.geometry_status, b.area_m2,
                               d.intersection_area_m2, b.geometry_wkb
                        from building_dong d join building b on b.source_row = d.source_row where d.dong_code = ? order by b.source_row''', (code,)).fetchall()
    kept, skipped = [], {}
    for sr, sid, pnu, legal, sf, h, fl, st, area, inter, wkb in rows:
        if st != 'ValidSourceGeometry' or wkb is None:
            skipped[st] = skipped.get(st, 0) + 1; continue
        f = json.loads(sf)
        prim = c.execute('select dong_code from building_dong where source_row=? order by intersection_area_m2 desc, dong_code limit 1', (sr,)).fetchone()[0]
        kept.append({'primaryDong': prim, 'row': sr, 'sourceId': sid, 'pnu': pnu, 'legal': legal, 'use': f.get('A8', ''), 'useName': f.get('A9', ''), 'address': f.get('A4', ''),
                     'heightM': h, 'floors': fl, 'areaM2': area, 'intersectionM2': inter, 'polys': polys_of(wkb)})
    result['dongs'][code] = {'name': name[0] if name else None, 'district': name[1] if name else None, 'rows': len(rows), 'kept': len(kept), 'skipped': skipped, 'buildings': kept}
    print(code, name, 'rows', len(rows), 'kept', len(kept), 'skipped', skipped)
raw = json.dumps(result, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
with gzip.GzipFile(out, 'wb', mtime=0) as g: g.write(raw)
print('sha256(json)', hashlib.sha256(raw).hexdigest())
