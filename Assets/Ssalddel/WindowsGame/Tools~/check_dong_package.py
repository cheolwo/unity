"""행정동 디오라마 — Codex 행정동 자료 모듈 연결 준비 점검 (읽기 전용, 2026-11-15).

Codex의 행정동 정밀 자료 모듈(admin-dong-precision-private.r1)과 굽기 자료 보충(admin-dong-diorama-supplement.private.r2)이
지금 굽기 도구(make_dong_modules.py)의 입력(northeast-seoul r2 scope 모듈)을 대신할 수 있는지 숫자로 본다.
아무것도 바꾸지 않는다. 쓰는 것은 마지막 인자의 보고서 한 개뿐이다. 넘파이만 쓴다.

사용: python3 -I -B check_dong_package.py <r1 패키지 폴더> <r2 보충 폴더> <scope-manifest.json> <modules 폴더> <Resources 폴더> <코드,…> <보고서.json>

보는 것
  A. 패키지 상태: 완료 영수증의 상태 · 검증 결과, r2가 가리키는 r1 패키지 해시, 기본/선택 방침.
  B. 파일: 동마다 r1 층 파일의 길이 · SHA-256이 명세와 같은지, r2 굽기 입력이 같은 층 해시를 가리키는지.
  C. 지금 입력과 견주기(동마다): 경계 · 건물(같은 원본 ID, 중심 거리, 넓이, 높이, 용도, 모양) · 도로(링크 ID, 길이).
  D. 녹지: r1 비오톱(EPSG:5186) 넓이 · 중심을 지금 메시의 녹지 겹(생태현황도 zip을 EPSG:5174로 읽어 구운 것)과 견준다.
  E. 좌표: r1 색인의 ENU 미터와 게임 미터(등장방형)의 차이 — 어댑터가 ENU 값을 게임 좌표로 그대로 쓰면 안 되는 까닭.
항목마다 '맞음' · '어댑터 규칙'(어댑터가 지켜야 할 변환) · '결정 필요' · '막힘'을 적는다. 문턱은 아래 상수다. 형식이 달라 읽지 못하면 그 항목은 '막힘'이고 까닭을 남긴다
(Codex가 새 판본에서 형식을 바꾸면 여기서 먼저 드러난다 — 그때 어댑터 계약을 다시 맞춘다).
"""
import gzip, hashlib, json, math, os, sys, traceback
import numpy as np

R1, R2, SCOPE, MODULES, RES, CODES, REPORT = sys.argv[1:8]
CODES = [c.strip() for c in CODES.split(',') if c.strip()]
def passed(s): return isinstance(s, str) and s.upper().startswith('PASS')   # 'PassedPrebakeCandidatesOnly'처럼 단서가 붙은 통과도 통과로, 단서는 따로 적는다
PLAIN_PASS = {'Passed', 'PASS', 'PassedFinal'}
NEAR = 0.05                 # 같은 건물 중심이 이만큼(m) 안이면 같은 자리
AREA_TOL = 0.01             # 건물 넓이 차이 1 %
BOUNDARY_TOL = 0.05         # 경계 두 선 사이 가장 먼 거리(m)
GREEN_AREA_TOL = (0.03, 300.0)   # 녹지 넓이: 3 % + 300 m² (메시 쪽은 삼각형 무게중심으로 자른 값)
GREEN_CENTRE_TOL = 2.0      # 녹지 중심 거리(m)
M_LAT = 111200.0
A_WGS = 6378137.0; F_WGS = 1 / 298.257223563; E2 = F_WGS * (2 - F_WGS)
GREEN_NAMES = {1: '산림지', 2: '조경녹지', 3: '하천', 4: '경작지'}
GREEN_LETTER = {'H': 1, 'E': 2, 'F': 3, 'G': 4}
MATCH, RULE, DECIDE, BLOCKED = '맞음', '어댑터 규칙', '결정 필요', '막힘'


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1 << 20), b''): h.update(block)
    return h.hexdigest()


def jload(path):
    if path.endswith('.gz'):
        with gzip.open(path, 'rt', encoding='utf-8') as f: return json.load(f)
    with open(path, encoding='utf-8') as f: return json.load(f)


def under(root, rel):
    """패키지 안의 상대 경로만 받는다(밖을 가리키면 거부)."""
    p = os.path.realpath(os.path.join(root, rel))
    if not p.startswith(os.path.realpath(root) + os.sep): raise ValueError('패키지 밖 경로: ' + rel)
    return p


def rel_to_root(root, repo_rel):
    """r2가 적은 'artifacts/local/<패키지>/…' 경로를 그 패키지 폴더 기준으로 바꾼다."""
    name = os.path.basename(os.path.normpath(root))
    key = '/' + name + '/'
    i = repo_rel.find(key)
    if i < 0: raise ValueError('다른 패키지를 가리킴: ' + repo_rel)
    return under(root, repo_rel[i + len(key):])


# ── 좌표 (make_dong_modules.py와 같은 식) ──────────────────────────────────
def ecef(lat, lon):
    la, lo = np.radians(lat), np.radians(lon)
    n = A_WGS / np.sqrt(1 - E2 * np.sin(la) ** 2)
    return n * np.cos(la) * np.cos(lo), n * np.cos(la) * np.sin(lo), n * (1 - E2) * np.sin(la)


class Frame:
    def __init__(self, lat0, lon0):
        self.o = [float(v) for v in ecef(np.array(lat0), np.array(lon0))]
        la, lo = math.radians(lat0), math.radians(lon0)
        self.sla, self.cla, self.slo, self.clo = math.sin(la), math.cos(la), math.sin(lo), math.cos(lo)

    def fwd(self, lat, lon):
        x, y, z = ecef(lat, lon)
        dx, dy, dz = x - self.o[0], y - self.o[1], z - self.o[2]
        return -self.slo * dx + self.clo * dy, -self.sla * self.clo * dx - self.sla * self.slo * dy + self.cla * dz


def grs80_tm_to_wgs(x, y, lon0=127.0, lat0=38.0, fe=200000.0, fn=600000.0):
    """EPSG:5186 → 위경도(도). make_dong_modules.py의 같은 이름 함수와 같다."""
    a = 6378137.0; f = 1 / 298.257222101; e2 = 2 * f - f * f; ep2 = e2 / (1 - e2)
    def meridian(p):
        e4, e6 = e2 * e2, e2 ** 3
        return a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * p - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * np.sin(2 * p)
                    + (15 * e4 / 256 + 45 * e6 / 1024) * np.sin(4 * p) - (35 * e6 / 3072) * np.sin(6 * p))
    e1 = (1 - math.sqrt(1 - e2)) / (1 + math.sqrt(1 - e2))
    mu = (meridian(math.radians(lat0)) + (y - fn)) / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 ** 3 / 256))
    p1 = (mu + (3 * e1 / 2 - 27 * e1 ** 3 / 32) * np.sin(2 * mu) + (21 * e1 ** 2 / 16 - 55 * e1 ** 4 / 32) * np.sin(4 * mu)
          + (151 * e1 ** 3 / 96) * np.sin(6 * mu) + (1097 * e1 ** 4 / 512) * np.sin(8 * mu))
    s, c, t = np.sin(p1), np.cos(p1), np.tan(p1)
    C1 = ep2 * c * c; T1 = t * t; N1 = a / np.sqrt(1 - e2 * s * s); R1_ = a * (1 - e2) / (1 - e2 * s * s) ** 1.5
    D = (x - fe) / N1
    lat = p1 - (N1 * t / R1_) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ep2) * D ** 4 / 24
                                 + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ep2 - 3 * C1 * C1) * D ** 6 / 720)
    lon = math.radians(lon0) + (D - (1 + 2 * T1 + C1) * D ** 3 / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ep2 + 24 * T1 * T1) * D ** 5 / 120) / c
    return np.degrees(lat), np.degrees(lon)


# ── 다각형 도우미 ─────────────────────────────────────────────────────────
def ring_ac(r):
    """고리 하나의 넓이(절댓값)와 중심."""
    x, y = r[:, 0], r[:, 1]
    x1, y1 = np.roll(x, -1), np.roll(y, -1)
    cr = x * y1 - x1 * y
    a = cr.sum() / 2.0
    if abs(a) < 1e-12: return 0.0, float(x.mean()), float(y.mean())
    return abs(a), float(((x + x1) * cr).sum() / (6 * a)), float(((y + y1) * cr).sum() / (6 * a))


def poly_ac(rings):
    """바깥 고리 하나 + 안 고리들. 넓이와 중심."""
    a0, cx, cy = ring_ac(rings[0])
    A, SX, SY = a0, a0 * cx, a0 * cy
    for h in rings[1:]:
        ah, hx, hy = ring_ac(h); A -= ah; SX -= ah * hx; SY -= ah * hy
    return (A, SX / A, SY / A) if A > 1e-12 else (0.0, cx, cy)


def polygons(geom):
    """GeoJSON Polygon/MultiPolygon → [[고리 배열, …], …] (좌표는 원본 그대로)."""
    if geom['type'] == 'Polygon': return [[np.array(r, dtype=float) for r in geom['coordinates']]]
    if geom['type'] == 'MultiPolygon': return [[np.array(r, dtype=float) for r in p] for p in geom['coordinates']]
    raise ValueError('다각형이 아님: ' + geom['type'])


def lines(geom):
    if geom['type'] == 'LineString': return [np.array(geom['coordinates'], dtype=float)]
    if geom['type'] == 'MultiLineString': return [np.array(l, dtype=float) for l in geom['coordinates']]
    raise ValueError('선이 아님: ' + geom['type'])


def point_to_segments(p, a, b):
    """점들 p(n,2)에서 선분들 a→b(m,2)까지 가장 가까운 거리(n)."""
    ab = b - a
    L = (ab ** 2).sum(1); L[L == 0] = 1e-12
    t = np.clip(((p[:, None, :] - a[None]) * ab[None]).sum(2) / L[None], 0, 1)
    q = a[None] + t[..., None] * ab[None]
    return np.sqrt(((p[:, None, :] - q) ** 2).sum(2)).min(1)


def ring_gap(r1, r2):
    """두 닫힌 선 사이 가장 먼 거리(꼭짓점 → 상대 선분, 양쪽)."""
    s1a, s1b = r1, np.roll(r1, -1, axis=0)
    s2a, s2b = r2, np.roll(r2, -1, axis=0)
    return float(max(point_to_segments(r1, s2a, s2b).max(), point_to_segments(r2, s1a, s1b).max()))


def stats(v):
    v = np.asarray(v, dtype=float)
    if not len(v): return {'n': 0}
    return {'n': int(len(v)), 'median': round(float(np.median(v)), 4), 'p95': round(float(np.percentile(v, 95)), 4), 'max': round(float(v.max()), 4)}


def presentation_height_r2(b):
    """make_dong_modules.py building_height와 같은 순서의 H(누르기 전)."""
    symbolic = b['evidenceKindCode'].endswith('SymbolicHeightFallback')
    floors = b['aboveGroundFloorCount']
    if not symbolic and b['heightMeters']: return float(b['heightMeters']), 'height'
    if floors: return float(floors) * 3.0, 'floors'
    return float(b['heightMeters'] or 4.0), 'symbolic'


def presentation_height_r1(p):
    """어댑터 계약 v0 안: 관측 높이 → 지상 층수×3 m → 4 m. (r1의 suggestedPresentationHeightMeters는 따로 견준다)"""
    h, floors = p.get('observedHeightMeters'), p.get('aboveGroundFloorCount')
    if h: return float(h), 'height'
    if floors: return float(floors) * 3.0, 'floors'
    return 4.0, 'symbolic'


def read_hdio_greens(path):
    """동 메시(HDIO v2)에서 녹지 겹 삼각형(높이 0.015, uv.x 1~4, 벽 아님)의 갈래별 넓이 · 중심(게임 미터)."""
    buf = open(path, 'rb').read()
    assert buf[:4] == b'HDIO' and np.frombuffer(buf, '<i4', 1, 4)[0] == 2, '메시 형식이 HDIO v2가 아님'
    o = 8 + 48 + 8
    n = int(np.frombuffer(buf, '<i4', 1, o)[0]); o += 4
    pos = np.frombuffer(buf, '<f4', n * 3, o).reshape(n, 3); o += n * 12
    o += n * 4                                                  # 색
    uv = np.frombuffer(buf, '<f4', n * 2, o).reshape(n, 2); o += n * 8
    uv2 = np.frombuffer(buf, '<f4', n * 2, o).reshape(n, 2); o += n * 8
    m = int(np.frombuffer(buf, '<i4', 1, o)[0]); o += 4
    tri = np.frombuffer(buf, '<i4', m, o).reshape(-1, 3)
    good = (uv2[:, 0] < 0.5) & (np.abs(pos[:, 1] - 0.015) < 1e-4) & (uv[:, 0] > 0.5) & (uv[:, 0] < 4.5)
    t = tri[good[tri].all(1)]
    cls = np.rint(uv[t[:, 0], 0]).astype(int)
    p0, p1, p2 = pos[t[:, 0]][:, [0, 2]], pos[t[:, 1]][:, [0, 2]], pos[t[:, 2]][:, [0, 2]]
    ar = np.abs((p1[:, 0] - p0[:, 0]) * (p2[:, 1] - p0[:, 1]) - (p2[:, 0] - p0[:, 0]) * (p1[:, 1] - p0[:, 1])) / 2
    cen = (p0 + p1 + p2) / 3
    out = {}
    for k in GREEN_NAMES:
        s = cls == k
        if s.any(): out[k] = (float(ar[s].sum()), float((ar[s] * cen[s, 0]).sum() / ar[s].sum()), float((ar[s] * cen[s, 1]).sum() / ar[s].sum()))
    return out


# ── 시작 ─────────────────────────────────────────────────────────────────
scope = jload(SCOPE)
fd = scope['coordinateFrame']
LAT0, LON0 = fd['originLatitude'], fd['originLongitude']
M_LON = M_LAT * math.cos(math.radians(LAT0))
frame = Frame(LAT0, LON0)
def game_xz(lat, lon): return (lon - LON0) * M_LON, (lat - LAT0) * M_LAT
scope_by = {e['administrativeAreaStableId']: e for e in scope['modules']}

report = {'schema': 'hongdal-dong-package-check.v0', 'readOnly': True,
          'inputs': {'r1': os.path.basename(os.path.normpath(R1)), 'r2': os.path.basename(os.path.normpath(R2)),
                     'scopeRevision': scope['revision'], 'scopeFileSha256': sha256(SCOPE), 'codes': CODES},
          'thresholds': {'buildingCentreMetres': NEAR, 'buildingAreaShare': AREA_TOL, 'boundaryMetres': BOUNDARY_TOL,
                         'greenArea': list(GREEN_AREA_TOL), 'greenCentreMetres': GREEN_CENTRE_TOL},
          'package': {}, 'dongs': [], 'verdicts': {}}
verdicts = report['verdicts']
def verdict(key, state, why):
    verdicts.setdefault(key, []).append({'state': state, 'why': why})
    print(f'[{state}] {key}: {why}')


# A. 패키지 상태
try:
    c1 = jload(os.path.join(R1, 'completion.json')); c2 = jload(os.path.join(R2, 'completion.json'))
    v1 = {k: v.get('status') for k, v in c1.get('verification', {}).items()}
    v2 = {k: (v.get('status') if isinstance(v, dict) else v) for k, v in c2.get('verification', {}).items()}
    pm_sha = sha256(os.path.join(R1, 'package-manifest.json'))
    bim = jload(os.path.join(R2, 'bake-input-manifest.json'))
    prof = jload(os.path.join(R2, 'module-build-profile.json'))
    report['package'] = {'r1Status': c1.get('status'), 'r1Verification': v1, 'r2Status': c2.get('status'), 'r2Verification': v2,
                         'r1PackageManifestSha256': pm_sha, 'r2BasePackageSha256': bim['basePackage']['sha256'],
                         'moduleUnit': prof.get('moduleUnit'), 'grid500mEnabledByDefault': prof['spatialIndex']['enabledByDefault'],
                         'uncertainCoordinateLayersIncludedByDefault': prof.get('uncertainCoordinateLayersIncludedByDefault'),
                         'historicalModules': c1.get('historicalDataModules'), 'currentAreas': c1.get('currentAdministrativeAreas'),
                         'currentBoundariesCollected': c1.get('currentBoundariesCollected'),
                         'flags': {k: c1.get(k) for k in ('runtimeApplied', 'unityApplied', 'distributionApproved', 'physicalTerrainReady')}}
    allv = list(v1.items()) + list(v2.items())
    bad = [k for k, s in allv if not passed(s)]
    qualified = {k: s for k, s in allv if passed(s) and s not in PLAIN_PASS}
    report['package']['qualifiedPasses'] = qualified
    verdict('패키지 완료', MATCH if c1.get('status', '').endswith('Complete') and c2.get('status', '').endswith('Complete') and not bad else BLOCKED,
            f"r1 {c1.get('status')} · r2 {c2.get('status')} · 검증 {len(allv) - len(bad)}/{len(allv)} 통과" + (f' · 통과 아님 {bad}' if bad else '')
            + (' · 단서 붙은 통과 ' + ', '.join(f'{k}={v}' for k, v in qualified.items()) if qualified else ''))
    verdict('r2가 이 r1을 가리킴', MATCH if pm_sha == bim['basePackage']['sha256'] else BLOCKED, pm_sha[:16])
    verdict('행정동이 기본 단위 · 500 m 구획은 선택', MATCH if prof.get('moduleUnit') == 'AdministrativeDong' and not prof['spatialIndex']['enabledByDefault'] else DECIDE,
            f"{prof.get('moduleUnit')} · 구획 기본 {prof['spatialIndex']['enabledByDefault']}")
    verdict('현재 공식 경계', DECIDE, f"수집 {c1.get('currentBoundariesCollected')}개 — 역사 경계(2023-10-31)로만 굽는다(지금과 같음)")
except Exception as e:
    verdict('패키지 완료', BLOCKED, '읽지 못함: ' + repr(e))


ALL_R2_IDS = set()                  # 점검하는 모든 동의 지금 건물 ID(경계에 걸친 건물이 어느 동에 들었는지 보려고)
for code in CODES:
    try:
        for b in jload(os.path.join(MODULES, code + '.json'))['buildings']: ALL_R2_IDS.add(b['buildingStableId'].split(':')[-1])
    except Exception: pass
for code in CODES:
    d = {'code': code}
    report['dongs'].append(d)
    try:
        hm_path = os.path.join(R1, 'historical-modules', code, 'manifest.json')
        hm = jload(hm_path)
        d['name'] = hm.get('historicalName'); d['district'] = hm.get('district')
        # B. 파일
        layers, bad = {}, []
        for L in hm['layers']:
            p = under(R1, L['path'])
            ok = os.path.exists(p) and os.path.getsize(p) == L['bytes'] and sha256(p) == L['sha256']
            layers[L['layer']] = {'path': p, 'crs': L['crs'], 'count': L['featureCount'], 'ok': ok, 'sha256': L['sha256']}
            if not ok: bad.append(L['layer'])
        bi = jload(os.path.join(R2, 'bake-inputs', 'historical', code + '.json'))
        r2_sha = {L['layer']: L['sha256'] for L in bi['geometryLayers']}
        diff = [k for k in layers if r2_sha.get(k) != layers[k]['sha256']]
        d['layers'] = {k: {kk: v[kk] for kk in ('crs', 'count', 'ok')} for k, v in layers.items()}
        d['optional'] = {'neighbours': bi.get('boundaryNeighborModule', {}).get('neighborCount'),
                         'intersections': bi.get('facilityReferenceLayers', {}).get('intersections', {}).get('featureCount'),
                         'streetlightCandidates': bi.get('candidateLayers', {}).get('streetlights-candidate', {}).get('featureCount'),
                         'restaurantsIncludedByDefault': bi.get('candidateLayers', {}).get('restaurants', {}).get('includedByDefault')}
        verdict(f'{code} 층 파일', MATCH if not bad and not diff else BLOCKED, f"{len(layers)}층" + (f' · 해시 다름 {bad}' if bad else '') + (f' · r2와 다름 {diff}' if diff else ''))
        same_frame = abs(hm['coordinateFrame']['originLatitude'] - LAT0) < 1e-12 and abs(hm['coordinateFrame']['originLongitude'] - LON0) < 1e-12
        verdict(f'{code} 공통 원점', MATCH if same_frame else BLOCKED, f"r1 {hm['coordinateFrame']['originLatitude']}, {hm['coordinateFrame']['originLongitude']}")

        # 지금 입력
        entry = scope_by.get('region:kr:hjd:' + code)
        mp = os.path.join(MODULES, code + '.json')
        m = jload(mp)
        d['currentInput'] = {'fileShaMatchesScope': bool(entry) and sha256(mp).upper() == entry['sha256'].upper(),
                             'buildings': len(m['buildings']), 'roadSegments': len(m['roads']), 'boundaryPoints': len(m['boundary'])}

        # C1. 경계
        bnd = jload(layers['boundary']['path'])['features'][0]['geometry']
        polys = polygons(bnd)
        ext = max((p[0] for p in polys), key=lambda r: ring_ac(r)[0])
        be, bn = frame.fwd(ext[:, 1], ext[:, 0])
        r1b = np.stack([be, bn], 1)
        r2b = np.array([(p['x'], p['z']) for p in m['boundary']], dtype=float)
        a1, a2 = ring_ac(r1b)[0], ring_ac(r2b)[0]
        gap = ring_gap(r1b, r2b)
        gx, gz = game_xz(ext[:, 1], ext[:, 0])
        enu_vs_game = float(np.sqrt((np.asarray(gx) - be) ** 2 + (np.asarray(gz) - bn) ** 2).max())
        d['boundary'] = {'r1Parts': len(polys), 'r1Holes': sum(len(p) - 1 for p in polys), 'r1Points': int(len(ext)), 'r2Points': int(len(r2b)),
                         'areaR1': round(a1, 1), 'areaR2': round(a2, 1), 'maxGapMetres': round(gap, 4), 'enuVsGameMaxMetres': round(enu_vs_game, 3)}
        verdict(f'{code} 경계', MATCH if gap < BOUNDARY_TOL and abs(a1 - a2) <= 1e-3 * a2 else DECIDE,
                f'가장 먼 틈 {gap:.4f} m · 넓이 {a1:,.0f} / {a2:,.0f} m²')
        verdict(f'{code} ENU ≠ 게임 미터', RULE if enu_vs_game > 0.01 else MATCH,
                f'경계에서 최대 {enu_vs_game:.2f} m — 어댑터는 위경도에서 game_xz로 바로 옮긴다(r1 색인의 ENU 값을 게임 좌표로 쓰지 않음)')

        # C2. 건물
        bfeat = jload(layers['buildings']['path'])['features']
        r1_by = {}
        for f in bfeat: r1_by.setdefault(f['properties']['sourceId'], []).append(f)
        r2_by = {}
        for b in m['buildings']:
            sid = b['buildingStableId'].split(':')[-1]
            r2_by.setdefault(sid, []).append(b)
        both = sorted(set(r1_by) & set(r2_by))
        dist, area_bad, area_bad_span, h_same, h_diff, cat_same, sug_same, kinds = [], 0, 0, 0, [], 0, 0, {}
        only1 = set(r1_by) - set(r2_by)
        only1_span = sum(1 for sid in only1 if r1_by[sid][0]['properties'].get('sourceSpansAdministrativeBoundaries'))
        only1_other = sum(1 for sid in only1 if sid in ALL_R2_IDS)
        multi = sum(1 for f in bfeat if f['geometry']['type'] == 'MultiPolygon')
        holes = sum(len(p) - 1 for f in bfeat for p in polygons(f['geometry']))
        spans = sum(1 for f in bfeat if f['properties'].get('sourceSpansAdministrativeBoundaries'))
        for sid in both:
            b, f = r2_by[sid][0], r1_by[sid][0]
            ps = polygons(f['geometry'])
            ext = max((p[0] for p in ps), key=lambda r: ring_ac(r)[0])
            e_, n_ = frame.fwd(ext[:, 1], ext[:, 0])
            a1, x1, y1 = ring_ac(np.stack([e_, n_], 1))
            a2, x2, y2 = ring_ac(np.array([(p['x'], p['z']) for p in b['footprint']], dtype=float))
            dist.append(math.hypot(x1 - x2, y1 - y2))
            if abs(a1 - a2) > AREA_TOL * max(a2, 1.0):
                area_bad += 1
                if f['properties'].get('sourceSpansAdministrativeBoundaries'): area_bad_span += 1
            H2, k2 = presentation_height_r2(b); H1, k1 = presentation_height_r1(f['properties'])
            kinds[k2 + '→' + k1] = kinds.get(k2 + '→' + k1, 0) + 1
            if abs(H1 - H2) < 0.01: h_same += 1
            else: h_diff.append(sid)
            sug = f['properties'].get('suggestedPresentationHeightMeters')
            if sug is not None and abs(float(sug) - H2) < 0.01: sug_same += 1
            c2 = b['categoryCode'].split(':')[-1]; c1 = f['properties'].get('sourceCategoryCode') or 'unknown'
            if c1 == c2: cat_same += 1
        d['buildings'] = {'r2': len(m['buildings']), 'r1': len(bfeat), 'r2Ids': len(r2_by), 'r1Ids': len(r1_by), 'sameIds': len(both),
                          'onlyR2': len(set(r2_by) - set(r1_by)), 'onlyR1': len(only1), 'onlyR1SpanningBoundary': only1_span,
                          'onlyR1BakedInAnotherCheckedDong': only1_other, 'areaOver1pcSpanningBoundary': area_bad_span,
                          'centreMetres': stats(dist), 'centreOverNear': int(sum(1 for v in dist if v > NEAR)), 'areaOver1pc': area_bad,
                          'heightSame': h_same, 'heightDifferent': len(h_diff), 'heightDifferentSample': h_diff[:5], 'heightPath': kinds,
                          'r1SuggestedHeightSameAsNow': sug_same, 'categorySame': cat_same,
                          'r1MultiPolygon': multi, 'r1InteriorRings': holes, 'r1SpansBoundary': spans}
        share = len(both) / max(len(r2_by), 1)
        verdict(f'{code} 건물 ID 이어짐', MATCH if share >= 0.99 and not (set(r1_by) - set(r2_by)) else DECIDE,
                f'같은 원본 ID {len(both)} / 지금 {len(r2_by)} ({share:.1%}) · r1에만 {len(only1)}(경계에 걸친 것 {only1_span}, 지금은 점검한 다른 동에 든 것 {only1_other}) · 지금에만 {len(set(r2_by) - set(r1_by))}')
        verdict(f'{code} 건물 자리 · 넓이', MATCH if dist and np.percentile(dist, 95) < NEAR and area_bad == 0 else DECIDE,
                f"중심 거리 중앙 {d['buildings']['centreMetres'].get('median')} · 95% {d['buildings']['centreMetres'].get('p95')} m · 넓이 1% 넘게 다름 {area_bad}(경계에 걸쳐 잘린 것 {area_bad_span})")
        verdict(f'{code} 건물 높이', MATCH if not h_diff else DECIDE, f'같음 {h_same} · 다름 {len(h_diff)} · 경로 {kinds}')
        verdict(f'{code} 건물 모양', MATCH if multi == 0 and holes == 0 else DECIDE, f'여러 조각 {multi} · 안뜰(안 고리) {holes} · 경계에 걸침 {spans}')

        # C3. 도로
        rfeat = jload(layers['roads']['path'])['features']
        r1_links = {f['properties']['sourceId'] for f in rfeat}
        def link(name):
            parts = name.split(':')
            return parts[2] if len(parts) > 2 and parts[0] == 'nodelink' and parts[1] == 'link' else name
        r2_links = {link(r['roadStableId']) for r in m['roads']}
        len1 = 0.0
        for f in rfeat:
            for l in lines(f['geometry']):
                e_, n_ = frame.fwd(l[:, 1], l[:, 0])
                len1 += float(np.hypot(np.diff(e_), np.diff(n_)).sum())
        len2 = sum(math.hypot(r['to']['x'] - r['from']['x'], r['to']['z'] - r['from']['z']) for r in m['roads'])
        d['roads'] = {'r1Features': len(rfeat), 'r2Segments': len(m['roads']), 'r1Links': len(r1_links), 'r2Links': len(r2_links),
                      'sameLinks': len(r1_links & r2_links), 'lengthR1': round(len1, 1), 'lengthR2': round(len2, 1)}
        verdict(f'{code} 도로', MATCH if r1_links == r2_links and abs(len1 - len2) <= 0.005 * max(len2, 1) else DECIDE,
                f'같은 링크 {len(r1_links & r2_links)} (r1 {len(r1_links)} · 지금 {len(r2_links)}) · 길이 {len1:,.0f} / {len2:,.0f} m')

        # D. 녹지
        gfeat = jload(layers['biotope']['path'])['features']
        r1g = {}
        for f in gfeat:
            k = GREEN_LETTER.get((f['properties'].get('typeCode') or ' ')[0])
            if not k: continue
            for p in polygons(f['geometry']):
                rings = []
                for r in p:
                    la, lo = grs80_tm_to_wgs(r[:, 0], r[:, 1])
                    gx, gz = game_xz(la, lo)
                    rings.append(np.stack([gx, gz], 1))
                A, cx, cy = poly_ac(rings)
                s = r1g.setdefault(k, [0.0, 0.0, 0.0]); s[0] += A; s[1] += A * cx; s[2] += A * cy
        mesh_g = read_hdio_greens(os.path.join(RES, 'hongdal-dong-' + code + '.bytes'))
        gd, gbad = {}, []
        for k in sorted(set(r1g) | set(mesh_g)):
            A1 = r1g.get(k, [0, 0, 0])[0]; A2 = mesh_g.get(k, (0, 0, 0))[0]
            c1 = (r1g[k][1] / A1, r1g[k][2] / A1) if A1 else None; c2 = mesh_g[k][1:] if A2 else None
            cd = math.hypot(c1[0] - c2[0], c1[1] - c2[1]) if c1 and c2 else None
            gd[GREEN_NAMES[k]] = {'r1Area': round(A1), 'meshArea': round(A2), 'centreMetres': None if cd is None else round(cd, 2)}
            if abs(A1 - A2) > GREEN_AREA_TOL[0] * max(A1, A2) + GREEN_AREA_TOL[1] or (cd is not None and A2 > 2000 and cd > GREEN_CENTRE_TOL):
                gbad.append(GREEN_NAMES[k])
        d['greens'] = gd
        verdict(f'{code} 녹지(5186 ↔ 지금 5174 굽기)', MATCH if not gbad else DECIDE,
                (' · '.join(f"{n} {v['r1Area']:,}/{v['meshArea']:,} m² 중심 {v['centreMetres']} m" for n, v in gd.items()) or '두 쪽 모두 녹지 없음') + (f' · 벗어남 {gbad}' if gbad else ''))
    except Exception as e:
        d['error'] = repr(e)
        verdict(f'{code} 점검', BLOCKED, '읽지 못함: ' + repr(e) + ' / ' + traceback.format_exc().splitlines()[-2].strip())

summary = {}
for key, vs in verdicts.items():
    for v in vs: summary[v['state']] = summary.get(v['state'], 0) + 1
report['summary'] = summary
os.makedirs(os.path.dirname(os.path.abspath(REPORT)), exist_ok=True)
with open(REPORT, 'w', encoding='utf-8', newline='\n') as f: json.dump(report, f, ensure_ascii=False, indent=1)
print('summary', summary)
