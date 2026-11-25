"""Codex 서울 입력 → 동 디오라마 굽기 입력(범위 명세 + 동 모듈 JSON) (2026-11-16, 지도 자료 적용 S1).

굽기 도구(make_dong_modules.py)는 r2 동북권 범위 명세와 동 모듈 JSON(ENU 미터)을 읽는다. 이 도구는 Codex가 정리한 서울 입력에서
같은 형식을 만들어, 굽기 도구를 고치지 않고 서울의 다른 동을 굽게 한다. 넘파이만 쓴다(사용자 PC 작업 셸에서도 돈다).

입력
  * 건물: extract_dong_buildings.py가 Codex r2 SQLite(GIS건물통합정보 2026-11-09, EPSG:5186)에서 꺼낸 것.
    건물마다 교차 넓이가 가장 큰 동(primaryDong)에만 둔다(한 건물은 한 동에). 여러 조각이면 가장 넓은 바깥 고리 하나(r2와 같음).
    높이: 원문 높이(0 초과 · 1,000 m 미만)가 있으면 그 값, 없으면 「SymbolicHeightFallback」(굽기 도구가 층수 × 3 m, 그것도 없으면 4 m).
    건물 id: 'al-d010:20261109:11:<원본 행>'(Codex 표기, 그 ZIP 판에 묶인 행 번호). 원본 sourceId는 따로 적는다.
  * 경계: 행정동 편집 후보 2026-10-01(capital-region.geojson.gz, 경위도). 여러 조각이면 가장 넓은 조각의 바깥 고리.
  * 도로: 표준노드링크 MOCT_LINK(EPSG:5186). 동 경계로 잘라 선분 하나씩(r2와 같은 roadStableId 꼴).
좌표: EPSG:5186 → (GRS80) 위경도 → 게임 원점(사가정 37.580912, 127.088502)의 WGS84 ECEF-ENU 미터.
사용: python3 -I make_dong_inputs.py <buildings.json.gz> <capital-region.geojson.gz> <MOCT_LINK.shp> <MOCT_LINK.dbf> <codes,...> <out-dir> [revision]
"""
import sys, os, json, gzip, math, hashlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import gu_geo as g

BLD, BND, LINK_SHP, LINK_DBF, CODES, OUT = sys.argv[1:7]
REVISION = sys.argv[7] if len(sys.argv) > 7 else 'seoul-codex-r2-adapter.r1'
# 여덟째 인자(2026-11-17): 경계 판. 'juso-gemd-202610'이면 도로명주소 구역의도형 행정동 경계(사용자가 받은 주소 자료, 원문은 PC 안에만)로 본다.
PROFILE = sys.argv[8] if len(sys.argv) > 8 else 'admdongkor-20261001'
OFFICIAL = PROFILE == 'juso-gemd-202610'
VINTAGE = ('juso-gemd-202610' if OFFICIAL else 'admdongkor-20261001') + '+al-d010-20261109+nodelink-20260914+'
BOUNDARY_CODE = 'OfficialAddressBoundary202610' if OFFICIAL else 'CandidateBoundary20261001'
BOUNDARY_SOURCE = ({'sourceId': 'source:juso', 'sourceRevision': 'TL_SCCO_GEMD:202610', 'licenseCode': '주소기반산업지원서비스 제공 자료(원문 PC 밖 반출 금지)',
                    'limitationCode': 'OfficialAddressAdminDongBoundary;RawStaysOnPc;DistributionNotApproved'} if OFFICIAL else
                   {'sourceId': 'source:admdongkor', 'sourceRevision': 'ver20261001', 'licenseCode': 'CC-BY-4.0+KOGL-Type1', 'limitationCode': 'EditedCandidateBoundary;NotOfficialLatest;DistributionNotApproved'})
CODES = [c for c in CODES.split(',') if c]
ORIGIN = (37.580912, 127.088502)
A, F = 6378137.0, 1 / 298.257223563; E2 = F * (2 - F)
FRAME = {'method': 'WGS84-ECEF-ENU-at-zero-altitude', 'originLatitude': ORIGIN[0], 'originLongitude': ORIGIN[1],
         'worldOffsetX': 0.0, 'worldOffsetZ': 0.0, 'metersPerUnit': 1.0}

def ecef(lat, lon):
    la, lo = np.radians(lat), np.radians(lon)
    n = A / np.sqrt(1 - E2 * np.sin(la) ** 2)
    return n * np.cos(la) * np.cos(lo), n * np.cos(la) * np.sin(lo), n * (1 - E2) * np.sin(la)
O_ = [float(v) for v in ecef(np.array(ORIGIN[0]), np.array(ORIGIN[1]))]
SLA, CLA = math.sin(math.radians(ORIGIN[0])), math.cos(math.radians(ORIGIN[0]))
SLO, CLO = math.sin(math.radians(ORIGIN[1])), math.cos(math.radians(ORIGIN[1]))
def enu(lat, lon):
    x, y, z = ecef(np.asarray(lat, float), np.asarray(lon, float)); dx, dy, dz = x - O_[0], y - O_[1], z - O_[2]
    return -SLO * dx + CLO * dy, -SLA * CLO * dx - SLA * SLO * dy + CLA * dz
def from5186(xy):
    xy = np.asarray(xy, float); lat, lon = g.tm_inverse(xy[:, 0], xy[:, 1], **g.TM_5186); return enu(lat, lon)
def area(r): r = np.asarray(r); return 0.5 * float(np.sum(r[:, 0] * np.roll(r[:, 1], -1) - np.roll(r[:, 0], -1) * r[:, 1]))
def pts(e, n): return [{'x': round(float(a), 3), 'z': round(float(b), 3)} for a, b in zip(e, n)]
def sha_bytes(b): return hashlib.sha256(b).hexdigest().upper()
def sha_file(p): return sha_bytes(open(p, 'rb').read())
def content_hash(obj): return sha_bytes(json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8'))
def inside(p, ring):
    x, z = p; c = False
    for i in range(len(ring)):
        a, b = ring[i], ring[i - 1]
        if (a[1] > z) != (b[1] > z) and x < (b[0] - a[0]) * (z - a[1]) / (b[1] - a[1]) + a[0]: c = not c
    return c
def clip(a, b, ring):
    """선분 ab를 다각형 ring(닫히지 않은 점 목록)으로 잘라 안쪽 조각들을 낸다."""
    a, b = np.asarray(a, float), np.asarray(b, float); d = b - a; ts = [0.0, 1.0]
    R = np.asarray(ring, float); Q = np.roll(R, -1, axis=0); E = Q - R
    den = d[0] * E[:, 1] - d[1] * E[:, 0]; ok = np.abs(den) > 1e-12
    w = R - a
    t = np.where(ok, (w[:, 0] * E[:, 1] - w[:, 1] * E[:, 0]) / np.where(ok, den, 1), -1)
    u = np.where(ok, (w[:, 0] * d[1] - w[:, 1] * d[0]) / np.where(ok, den, 1), -1)
    ts += [float(v) for v in t[ok & (t > 0) & (t < 1) & (u >= 0) & (u <= 1)]]
    ts = sorted(set(ts)); out = []
    for t0, t1 in zip(ts, ts[1:]):
        if (t1 - t0) * math.hypot(*d) < 0.5: continue
        m = a + d * (t0 + t1) / 2
        if inside(m, ring): out.append((a + d * t0, a + d * t1))
    return out

bld = json.load(gzip.open(BLD, 'rt', encoding='utf-8'))
bnd = json.load(gzip.open(BND, 'rt', encoding='utf-8'))
features = {f['properties']['adm_cd2']: f for f in bnd['features']}
os.makedirs(os.path.join(OUT, 'modules'), exist_ok=True)
_, link_rec, _ = g.read_dbf(LINK_DBF)
entries, receipt = [], {'schema': 'hongdal-dong-inputs-receipt.v1', 'revision': REVISION, 'tool': 'make_dong_inputs.py', 'toolSha256': sha_file(__file__),
                        'inputs': {'buildings': {'file': os.path.basename(BLD), 'sha256': sha_file(BLD)}, 'boundary': {'file': os.path.basename(BND), 'sha256': sha_file(BND)},
                                   'nodeLinkShp': {'file': os.path.basename(LINK_SHP), 'sha256': sha_file(LINK_SHP)}, 'nodeLinkDbf': {'file': os.path.basename(LINK_DBF), 'sha256': sha_file(LINK_DBF)}},
                        'modules': {}}
for code in CODES:
    f = features[code]; name = f['properties']['adm_nm']
    geom = f['geometry']; polys = [geom['coordinates']] if geom['type'] == 'Polygon' else geom['coordinates']
    rings = []
    for poly in polys:
        r = np.asarray(poly[0], float); e, n = enu(r[:, 1], r[:, 0]); rings.append((abs(area(np.stack([e, n], 1))), e, n))
    rings.sort(key=lambda t: -t[0]); _, be, bn = rings[0]
    ring_en = np.stack([be, bn], 1)
    if np.allclose(ring_en[0], ring_en[-1]): ring_open = ring_en[:-1]
    else: ring_open = ring_en; be, bn = np.append(be, be[0]), np.append(bn, bn[0])
    if area(ring_open) < 0: ring_open = ring_open[::-1]
    # 건물
    d = bld['dongs'][code]; buildings = []; dropped = {'otherPrimaryDong': 0, 'tiny': 0}
    for b in d['buildings']:
        if b['primaryDong'] != code: dropped['otherPrimaryDong'] += 1; continue
        parts = sorted(b['polys'], key=lambda p: -abs(area(np.asarray(p[0], float))))
        ext = np.asarray(parts[0][0], float)
        if abs(area(ext)) < 1.0: dropped['tiny'] += 1; continue
        e, n = from5186(ext)
        h = b['heightM']; good_h = h is not None and 0 < h < 1000
        kind = 'OfficialGisBuildingLargestExteriorCandidate' if len(parts) > 1 else 'OfficialGisBuildingFootprintCandidate'
        buildings.append({'buildingStableId': 'al-d010:20261109:11:%d' % b['row'], 'sourceId': b['sourceId'], 'pnu': b['pnu'],
                          'categoryCode': 'al-d010-kind:' + (b['use'] or 'unknown'), 'evidenceKindCode': kind if good_h else kind + ':SymbolicHeightFallback',
                          'aboveGroundFloorCount': b['floors'] if b['floors'] and b['floors'] > 0 else None,
                          'heightMeters': round(float(h), 3) if good_h else 4.0, 'buildingAreaSquareMeters': round(float(b['areaM2'] or 0), 3),
                          'totalFloorAreaSquareMeters': 0.0, 'footprint': pts(e, n),
                          'assignmentPoint': {'x': round(float(e.mean()), 6), 'z': round(float(n.mean()), 6)},
                          'assignmentMethodCode': 'CentroidInOfficialBoundary' if OFFICIAL else 'LargestIntersectionWithCandidateBoundary', 'assignmentConfidenceCode': BOUNDARY_CODE})
    # 도로: 경계 상자 둘레의 링크를 경계로 자른다
    allb = np.concatenate([np.asarray(bb['polys'][0][0], float) for bb in d['buildings']]) if d['buildings'] else None
    box = (allb[:, 0].min() - 600, allb[:, 1].min() - 600, allb[:, 0].max() + 600, allb[:, 1].max() + 600)
    roads = []
    for idx, parts in g.read_shapes(LINK_SHP, box):
        lid = link_rec(idx)['LINK_ID']
        for pi, part in enumerate(parts):
            e, n = from5186(part); P = np.stack([e, n], 1); ci = 0
            for si, (p0, p1) in enumerate(zip(P[:-1], P[1:])):
                for q0, q1 in clip(p0, p1, ring_open):
                    roads.append({'roadStableId': 'nodelink:link:%s:hjd:%s:part:%d:clip:%d:segment:%d' % (lid, code, pi, ci, si),
                                  'from': {'x': round(float(q0[0]), 6), 'z': round(float(q0[1]), 6)}, 'to': {'x': round(float(q1[0]), 6), 'z': round(float(q1[1]), 6)},
                                  'evidenceKindCode': 'OfficialStandardNodeLinkPresentationOnly'}); ci += 1
    module = {'schemaVersion': 'administrative-dong-diorama-batch-input.v1', 'projectionRevision': REVISION, 'desiredReadinessCode': 'WaitingForOfficialBoundary',
              'diagnosticCodes': ([BOUNDARY_CODE] if OFFICIAL else ['CandidateBoundary20261001', 'CurrentAuthoritativeBoundaryUnavailable']) + ['DistributionNotApproved', 'TraversalNotReady', 'GameplayNotReady', 'PreviewOnly'],
              'administrativeAreaStableId': 'region:kr:hjd:' + code, 'displayName': name,
              'sourceVintage': VINTAGE + REVISION, 'generatedAtUtc': '2026-11-16T00:00:00Z',
              'coordinateFrame': FRAME, 'boundary': pts(be, bn), 'legalAreaStableIds': sorted({'region:kr:bjd:' + b['legal'] for b in d['buildings'] if b['legal']}),
              'operationalAreaStableIds': [], 'semanticPlaceStableIds': [],
              'sources': [BOUNDARY_SOURCE,
                          {'sourceId': 'source:data-go-kr', 'datasetId': 'data-go-kr-15083092', 'sourceRevision': 'AL_D010:Seoul:20261109', 'licenseCode': 'KOGL-Type1(portal record)',
                           'limitationCode': 'OfficialGisBuildingGeometry;EPSG5186;LargestExteriorOnly;DistributionNotApproved'},
                          {'sourceId': 'source:its', 'datasetId': 'its-standard-nodelink', 'sourceRevision': 'NodeLink:20260914', 'licenseCode': 'Unrestricted', 'limitationCode': 'PresentationOnly'}],
              'buildings': buildings, 'roads': roads, 'publicBusinesses': [], 'unresolvedBuildingCount': 0}
    module['contentHashSha256'] = content_hash(module)
    path = os.path.join(OUT, 'modules', code + '.json')
    with open(path, 'w', encoding='utf-8', newline='\n') as fh: json.dump(module, fh, ensure_ascii=False, separators=(',', ':'))
    entries.append({'administrativeAreaStableId': 'region:kr:hjd:' + code, 'displayName': name, 'relativePath': 'modules/%s.json' % code, 'sha256': sha_file(path),
                    'contentHashSha256': module['contentHashSha256'], 'buildingCount': len(buildings), 'roadSegmentCount': len(roads), 'unresolvedBuildingCount': 0})
    hs = sum(1 for b in buildings if not b['evidenceKindCode'].endswith('Fallback')); fl = sum(1 for b in buildings if b['evidenceKindCode'].endswith('Fallback') and b['aboveGroundFloorCount'])
    receipt['modules'][code] = {'name': name, 'extracted': d['kept'], 'buildings': len(buildings), 'dropped': dropped, 'heightFromSource': hs, 'floorsOnly': fl,
                                'symbolic': len(buildings) - hs - fl, 'roadSegments': len(roads), 'links': len({r['roadStableId'].split(':')[2] for r in roads}),
                                'boundaryPoints': len(be)}
    print(code, name, receipt['modules'][code])
scope = {'schemaVersion': 'administrative-dong-diorama-batch-scope.v1', 'scopeStableId': 'scope:administrative-dong-diorama:seoul-preview:' + REVISION,
         'revision': REVISION, 'generatedAtUtc': '2026-11-16T00:00:00Z',
         'sourceVintage': VINTAGE + REVISION, 'desiredReadinessCode': 'WaitingForOfficialBoundary',
         'diagnosticCodes': [BOUNDARY_CODE, 'DistributionNotApproved', 'TraversalNotReady', 'GameplayNotReady', 'PreviewOnly'],
         'distributionApproved': False, 'traversalReady': False, 'gameplayReady': False, 'coordinateFrame': FRAME,
         'buildingSource': 'GIS건물통합정보 AL_D010 2026-11-09 (Codex 서울 입력 r2)', 'buildingSourceDate': '2026-11-09', 'modules': entries}
scope['contentHashSha256'] = content_hash(scope)
with open(os.path.join(OUT, 'scope-manifest.json'), 'w', encoding='utf-8', newline='\n') as fh: json.dump(scope, fh, ensure_ascii=False, indent=1)
receipt['scopeSha256'] = sha_file(os.path.join(OUT, 'scope-manifest.json'))
with open(os.path.join(OUT, 'inputs-receipt.json'), 'w', encoding='utf-8', newline='\n') as fh: json.dump(receipt, fh, ensure_ascii=False, indent=1)
