"""행정동 모듈 디오라마 굽기 (r54 첫 조각, 2026-11-13).

기존 r2 행정동 모듈(administrative-dong-diorama-batch-input.v1)을 Windows 게임이 읽는 형태로 바꾼다.
오프라인·읽기 전용이다. 원본 모듈과 scope는 바꾸지 않는다.

  * 동마다 2.5D 메시 한 개: Resources/hongdal-dong-<코드>.bytes (기존 HDIO v2 형식, 예시 채움 건물 없음)
  * 모든 동의 건물 색인 한 개: Resources/hongdal-dong-buildings.json (hongdal-building-index.v1 + 행정동 필드)
  * 목록 한 개: Resources/hongdal-dong-modules.json (판본·해시·경계·준비 상태·역할 대응)

좌표: 모듈은 사가정 원점(scope coordinateFrame)의 WGS84 ECEF→ENU(고도 0) 미터다. 위경도로 되돌린 뒤
게임이 쓰는 등장방형 미터(위도 1도 111200 m, 경도 1도 111200·cos(원점 위도))로 다시 놓는다.
모든 동이 같은 원점을 쓰므로 한 장면에 나란히 놓으면 경계가 그대로 맞물린다.

표현값(측정값 아님): 도로 폭과 자리(노드링크는 폭을 주지 않는다. 건물 사이에 맞춰 깐다), 14 m 위 높이 압축, 용도별 색, 빛 방향.
높이: 원본 높이(상징 높이가 아닐 때) → 지상 층수×3 m → 상징 높이 4 m 순서. 어느 것을 썼는지 색인에 남긴다.

사용: python3 -I make_dong_modules.py <scope-manifest.json> <modules-dir> <codes,...> <osm-index.json> <roles.json> <out-dir> <report.json> [<생태현황도 zip> [<횡단보도 xlsx>]]

구성 (3D 기반 B1, 2026-11-14): 한 동을 굽는 일이 확인(verify_module) · 좌표(Points) · 바닥(bake_ground) · 건물(bake_buildings) ·
도로(bake_roads) · 쓰기(write_module)로 나뉜다. 골목을 그리거나(B3), 건물 외곽을 들이는(B4) 일은 이 함수들을 하나씩 바꾸는 일이다.

길 그래프 (R1, 2026-11-14): 큰길의 가운데와 골목의 가운데 선, 건물마다의 가상 출입구를 Resources/hongdal-dong-routes.bytes로 낸다
(build_routes → write_routes). 기사가 다니는 길이다. 출입구는 공식 자료가 아니라 가장 가까운 길 위의 자리다.

골목 (3D 기반 B3, 2026-11-14): 노드링크에 없는 골목은 건물 배치에서 계산해 그린다(alley_lines → alley_pieces). 자료가 아니라 표현값이다.
이 계산에는 scipy와 scikit-image가 더 필요하다.

지붕 테두리와 1층 가게 불빛 (품5, 2026-11-15 화면 고품질화): 모두 표현값이다. 가까이 볼 때만 보이고 멀리서는 전과 같다.
  * 지붕 테두리: 벽마다 지붕 위로 RIM(0.45 m)만큼 띠를 하나 더 세운다(uv2 = (0, RIM_FLAG)). 셰이더가 멀리서는 지운다.
    실제 난간이 있는지는 자료에 없다.
  * 1층 가게: 용도가 근린생활시설(SHOP_CODES)인 건물의 벽은 uv2.y에 SHOP_SEED를 더한다. 셰이더가 빼서 창 불빛 씨앗은 전과 같고,
    가까이에서만 1층에 넓은 유리와 간판 띠를 그린다. 간판 글자 · 상호는 넣지 않는다.

녹지 (r221 계1, 2026-11-15): 여덟째 인자로 서울시 생태현황도(비오톱유형/평가도 2025, OA-21145, 공공누리 제1유형) zip을 주면
산림지(H) · 조경녹지(E) · 하천(F) · 경작지(G) 폴리곤을 동 경계 안에서 잘라 바닥(0.01 m)과 골목(0.02 m) 사이(GREEN_Y)에 한 겹으로 깐다.
  * 범위는 자료다. 색은 표현값이다. 정점의 uv.x에 갈래 번호(1 산림지 · 2 조경녹지 · 3 하천 · 4 경작지)를 남겨 계절 색(계3)이 쓴다.
  * 좌표: EPSG:5174(Bessel, 수정 중부원점) → 위경도(towgs84 7변수) → 게임 미터. 동 경계로 자르므로 이웃 동의 같은 녹지와 경계에서 맞물린다.
  * 자르기와 채우기: 경계와 녹지 테두리를 GREEN_STEP 간격으로 잘게 나눈 점들의 들로네 삼각형 가운데, 무게중심이 동 안 · 녹지 안인 것만 남긴다.
    넓이를 1 m 격자로 센 값과 견줘 확인한다. 녹지 정점은 메시의 맨 끝에 붙인다(앞의 정점 · 삼각형은 녹지 없이 구운 것과 같다).
  * zip을 주지 않으면 전과 같다.

보도 · 횡단보도 (거리 풍경 보1 · 거1, 2026-11-15): 아홉째 인자로 서울시 교차로 및 횡단보도 시설·위치정보(OA-23081) xlsx를 주면
녹지 뒤에 다섯 겹을 더 붙인다. **멀리서는 전과 똑같다**: 겹마다 정점 색을 그 아래에 이미 보이던 색(바닥 · 연석 띠 · 차도)으로 두고,
셰이더가 가까이 볼 때만 uv.x 표시로 칠한다(5 보도 블록 · 6 횡단보도 줄 · 7 점자블록 · 8 연석선 · 9 시설물대).
  * 보도(높이 0.012): 차도에서 6 m까지, 건물 · 녹지 · 차도가 아닌 곳, 동 경계 안(0.25 m 격자). 바닥과 같은 색.
  * 시설물대(0.031): 연석 띠 조각을 같은 자리 · 같은 색으로 한 번 더(가까이에서 보도 블록으로 칠한다).
  * 연석선(0.032): 차도 가장자리에서 바깥으로 0.3 m 띠. 연석 띠 색.
  * 점자블록(0.034): 횡단보도 양 끝에서 보도 쪽 0.3~0.9 m, 횡단보도 폭. 연석 띠 색.
  * 횡단보도(0.065): 자료 지점을 지나는 가장 짧은 차도 현(동 안)이 건너는 방향, 양 끝 0.35 m 남김, 폭 4 m(14 m 넘는 길 5 m), 줄 0.5 m · 간격 0.5 m. 차도 색.
  * 자리는 자료(OA-23081, EPSG:5186로 보고 옮김), 줄무늬 · 점자블록 모양 · 보도 범위는 표현 · 계산이다. 대각선 · 차도 현을 못 찾은 곳은 세어 둔다.
  * 이 계산에는 openpyxl이 더 필요하다.

도로 맞춤 (3D 기반 B2, 2026-11-14): 도로는 모든 동의 건물을 다 세운 뒤에 깐다(prepare_module → fit_roads → finish_module).
경계의 길은 건너편 동의 건물과 닿고, 큰길의 두 방향 줄은 경계를 사이에 두고 서로 다른 동에 들어 있기 때문이다.
그래서 동 하나만 구워도 되지만, 이웃 동을 함께 구워야 경계의 길이 이웃 건물을 피한다. 필요한 것은 넘파이뿐이다.
"""
import collections, hashlib, json, math, os, struct, sys
import numpy as np

SCOPE, MODULES, CODES, OSM_INDEX, ROLES, OUT, REPORT = sys.argv[1:8]
BIOTOPE = sys.argv[8] if len(sys.argv) > 8 else None
CROSSINGS = sys.argv[9] if len(sys.argv) > 9 else None
CODES = [c.strip() for c in CODES.split(',') if c.strip()]
M_LAT = 111200.0
A, F = 6378137.0, 1 / 298.257223563
E2 = F * (2 - F)

ROAD_HALF = 4.0            # 노드링크는 폭이 없다. 선의 양쪽으로 이만큼(8 m)을 기본 폭으로 하고, 건물 사이에 맞춘다(B2)
KERB = 1.1
C_DONG = 0x21323F           # 행정동 경계 안 바닥 (카메라 배경 0x1D2C38보다 조금 밝게)
C_KERB, C_ROAD = 0x3C4D5C, 0x34434F
ROOF_RES = [0x6D8193, 0x657A8C, 0x74889A, 0x5F7385]
ROOF_GREEN = [0x4F7F6C, 0x487564, 0x568872]
ROOF_COM = [0x8C8FA1, 0x7F8496, 0x9396A7]
ROOF_APT = [0x8795A5, 0x7D8C9D]
ROOF_CIVIC = [0x7D8F86, 0x86978E]
ROOF_WORK = [0x8A8478, 0x7F7A70]
WALLS = [0xA58B80, 0x9AA6B2, 0xB0A694, 0x8F9BA8, 0xA3ADB7, 0x9C8479]
SHOP_CODES = {'03000', '04000'}  # 제1종 · 제2종 근린생활시설 (품5, 표현: 1층 가게 불빛)
SHOP_SEED = 1000.0               # 근린생활시설 벽의 uv2.y에 더하는 값. 씨앗(0~996)과 겹치지 않는다
RIM = 0.45                       # 지붕 테두리의 높이 m (품5, 표현값)
RIM_FLAG = -1.0                  # 지붕 테두리 정점의 uv2.y
TO_LIGHT = (-0.931, -0.364)
SHADOW_DIR = (0.931, 0.364)


def rgb(h): return ((h >> 16) & 255, (h >> 8) & 255, h & 255)
def mul(c, f): return tuple(max(0, min(255, int(round(v * f)))) for v in c)
def rnd4(key):
    d = hashlib.sha256(str(key).encode()).digest()
    return [d[i] / 255.0 for i in range(4)]
def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1 << 20), b''): h.update(block)
    return h.hexdigest().upper()


# ── 좌표: ENU(고도 0) ↔ 위경도, 넘파이로 한꺼번에 ─────────────────────────────
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
        e = -self.slo * dx + self.clo * dy
        n = -self.sla * self.clo * dx - self.sla * self.slo * dy + self.cla * dz
        return e, n

    def inv(self, e, n):
        e, n = np.asarray(e, dtype=float), np.asarray(n, dtype=float)
        lat = self.lat0 + n / 111000.0
        lon = self.lon0 + e / (111000.0 * self.cla)
        d = 1e-6
        for _ in range(10):
            fe, fn = self.fwd(lat, lon)
            e1, n1 = self.fwd(lat + d, lon)
            e2, n2 = self.fwd(lat, lon + d)
            j0, j1, j2, j3 = (e1 - fe) / d, (e2 - fe) / d, (n1 - fn) / d, (n2 - fn) / d
            det = j0 * j3 - j1 * j2
            re, rn = e - fe, n - fn
            lat = lat + (j3 * re - j1 * rn) / det
            lon = lon + (j0 * rn - j2 * re) / det
            if np.max(np.abs(re)) < 1e-6 and np.max(np.abs(rn)) < 1e-6: break
        fe, fn = self.fwd(lat, lon)
        return lat, lon, float(max(np.max(np.abs(fe - e)), np.max(np.abs(fn - n))))


# ── 다각형·메시 도우미 (기존 bake_diorama.py와 같은 방식) ─────────────────────
def area2(p):
    s = 0.0
    for i in range(len(p)):
        a, c = p[i], p[(i + 1) % len(p)]
        s += a[0] * c[1] - c[0] * a[1]
    return s / 2


def clean(p):
    out = []
    for q in p:
        if not out or math.hypot(q[0] - out[-1][0], q[1] - out[-1][1]) > 1e-4: out.append(q)
    if len(out) > 1 and math.hypot(out[0][0] - out[-1][0], out[0][1] - out[-1][1]) <= 1e-4: out.pop()
    if len(out) >= 3 and area2(out) < 0: out.reverse()
    return out


def cross(a, b, c): return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def earclip(poly):
    idx, tris, guard = list(range(len(poly))), [], 0
    def inside(p, a, b_, c):
        if p == a or p == b_ or p == c: return False
        return cross(a, b_, p) >= -1e-9 and cross(b_, c, p) >= -1e-9 and cross(c, a, p) >= -1e-9
    while len(idx) > 3 and guard < 200000:
        guard += 1
        m, found = len(idx), False
        for k in range(m):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % m]
            a, b_, c = poly[i0], poly[i1], poly[i2]
            if cross(a, b_, c) <= 1e-9: continue
            if any(inside(poly[j], a, b_, c) for j in idx if j not in (i0, i1, i2)): continue
            tris.append((i0, i1, i2)); idx.pop(k); found = True
            break
        if found: continue
        for k in range(m):
            if abs(cross(poly[idx[k - 1]], poly[idx[k]], poly[idx[(k + 1) % m]])) <= 1e-9:
                idx.pop(k); found = True
                break
        if not found:
            tris.append((idx[-1], idx[0], idx[1])); idx.pop(0)
    if len(idx) == 3: tris.append(tuple(idx))
    return tris


class MeshOut:
    def __init__(self): self.pos, self.col, self.uv, self.uv2, self.tri = [], [], [], [], []
    def vert(self, x, y, z, c, uv=(0.0, 0.0), uv2=(0.0, 0.0)):
        self.pos.append((x, y, z)); self.col.append((c[0], c[1], c[2], 255)); self.uv.append(uv); self.uv2.append(uv2)
        return len(self.pos) - 1
    def flat(self, pts, y, c):
        tris = earclip(pts)
        base = len(self.pos)
        for p in pts: self.vert(p[0], y, p[1], c)
        for t in tris: self.tri.append((base + t[0], base + t[1], base + t[2]))
    def quad(self, a, b_, c_, d, col, uvs=None, uv2=(0.0, 0.0)):
        uvs = uvs or [(0.0, 0.0)] * 4
        i = [self.vert(p[0], p[1], p[2], col, uvs[k], uv2) for k, p in enumerate((a, b_, c_, d))]
        self.tri.append((i[0], i[1], i[2])); self.tri.append((i[0], i[2], i[3]))


def road_layer(mesh, pieces, y, colour):
    """볼록 조각(사각형·삼각형)들을 한 높이에 올린다."""
    c = rgb(colour)
    for piece in pieces:
        i = [mesh.vert(p[0], y, p[1], c) for p in piece]
        for k in range(1, len(i) - 1): mesh.tri.append((i[0], i[k], i[k + 1]))


def prism(mesh, pts, y0, y1, top, wall, H, seed, shop=False):
    n = len(pts)
    for i in range(n):
        a, b_ = pts[i], pts[(i + 1) % n]
        L = math.hypot(b_[0] - a[0], b_[1] - a[1])
        if L < 1e-4: continue
        nx, nz = (b_[1] - a[1]) / L, -(b_[0] - a[0]) / L
        lit = 0.60 + 0.40 * max(0.0, nx * TO_LIGHT[0] + nz * TO_LIGHT[1])
        uv2 = (1.0, float((seed * 131 + i * 17) % 997) + (SHOP_SEED if shop else 0.0))
        mesh.quad((a[0], y0, a[1]), (b_[0], y0, b_[1]), (b_[0], y1, b_[1]), (a[0], y1, a[1]), mul(rgb(wall), lit),
                  [(0.0, 0.0), (L, 0.0), (L, H), (0.0, H)], uv2)
        mesh.quad((a[0], y1, a[1]), (b_[0], y1, b_[1]), (b_[0], y1 + RIM, b_[1]), (a[0], y1 + RIM, a[1]), mul(rgb(wall), lit),
                  [(0.0, 0.0), (L, 0.0), (L, RIM), (0.0, RIM)], (0.0, RIM_FLAG))        # 지붕 테두리 (품5, 표현)
    mesh.flat(pts, y1, rgb(top))


def cast_shadow(out, pts, h):
    reach = min(h, 8.0) * 0.95
    off = [(p[0] + SHADOW_DIR[0] * reach, p[1] + SHADOW_DIR[1] * reach) for p in pts]
    black = (0, 0, 0)
    n = len(pts)
    for i in range(n):
        a, b_, c_, d = pts[i], pts[(i + 1) % n], off[(i + 1) % n], off[i]
        out.quad((a[0], 0.11, a[1]), (b_[0], 0.11, b_[1]), (c_[0], 0.11, c_[1]), (d[0], 0.11, d[1]), black)
    tris = earclip(off)
    base = len(out.pos)
    for p in off: out.vert(p[0], 0.11, p[1], black)
    for t in tris: out.tri.append((base + t[0], base + t[1], base + t[2]))


def use_code(category):            # 'al-d010-kind:01000' → '01000'
    return category.split(':')[-1] if category else 'unknown'


def roof_and_wall(code, r):
    if code == '01000': return (ROOF_GREEN[int(r[1] * 3 * 0.999)] if r[2] < 0.38 else ROOF_RES[int(r[1] * 4 * 0.999)]), WALLS[int(r[3] * 6 * 0.999)]
    if code == '02000': return ROOF_APT[int(r[1] * 2 * 0.999)], 0xA9B3BD
    if code in ('03000', '04000', '07000', '15000', '16000'): return ROOF_COM[int(r[1] * 3 * 0.999)], 0xA8A29D
    if code in ('05000', '06000', '09000', '10000', '11000', '12000', '13000', '23000', '24000', '25000', '29000'):
        return ROOF_CIVIC[int(r[1] * 2 * 0.999)], 0xA4ACA6
    if code in ('08000', '14000', '17000', '18000', '19000', '20000', '21000', '22000', '26000'):
        return ROOF_WORK[int(r[1] * 2 * 0.999)], 0x9E978C
    return ROOF_RES[int(r[1] * 4 * 0.999)], WALLS[int(r[3] * 6 * 0.999)]


def rdp(points, tolerance):
    if len(points) < 3: return points
    a, b_ = np.array(points[0]), np.array(points[-1])
    ab = b_ - a
    L = np.hypot(*ab)
    pts = np.array(points[1:-1])
    if L < 1e-9: d = np.hypot(*(pts - a).T)
    else: d = np.abs(ab[0] * (pts[:, 1] - a[1]) - ab[1] * (pts[:, 0] - a[0])) / L
    k = int(np.argmax(d))
    if d[k] <= tolerance: return [points[0], points[-1]]
    left = rdp(points[:k + 2], tolerance)
    right = rdp(points[k + 1:], tolerance)
    return left[:-1] + right


# ── 도로를 건물 사이에 맞추기 (3D 기반 B2, 2026-11-14) ─────────────────────────
# 노드링크는 길의 가운데 선도 폭도 아니다. 큰길은 방향마다 한 줄씩 두 줄이고, 그 줄이 건물 앞을 스치거나 지나간다.
# 그래서 선마다 같은 폭의 띠를 깔면 띠가 건물을 덮고, 두 줄 사이에 연석 줄이 남는다.
# 여기서는 선을 따라 ROAD_STEP마다 잘라, 그 자리에서 건물이 없는 폭을 찾아 그 안에만 깐다.
#   * 나란히 가는 짝 줄이 있으면 그 줄까지 차도로 메운다(두 줄이 한 길이 된다).
#   * 짝이 없는 한 줄 길은 한쪽이 건물에 막히면 반대쪽으로 밀어 폭을 지킨다.
#   * 건물 벽에서 ROAD_GAP만큼 뗀다. ROAD_MIN보다 좁게만 남으면 그 자리는 비운다.
# 건물은 굽는 모든 동의 것을 함께 본다(경계의 길은 건너편 동의 건물과도 닿는다). 모두 표현값이고 측정값이 아니다.
ROAD_STEP = 1.0
ROAD_GAP = 0.3
ROAD_MIN = 1.6
ROAD_EASE = 4.0                        # 폭을 정할 때 앞뒤로 이만큼 더 본다. 건물 사이의 좁은 틈으로 도로 가장자리가 들쭉날쭉해지지 않게
ROAD_USUAL = 35                        # 한 링크에서 짝이 없는 쪽 폭은 그 링크의 이 백분위 값까지만 쓴다(빈터마다 길이 불룩해지지 않게)
ROAD_BLOCKED = 5.0                     # 짝 줄까지 가는 사이에 건물이 이만큼까지 걸쳐도 짝으로 본다(줄이 건물 앞을 스치는 것은 봐준다)
ROAD_OPEN = 0.45                       # 또는 사이의 이 비율이 한 덩어리로 비어 있으면 짝으로 본다(두 줄이 모두 건물 속을 지나는 좁은 길)
MATE_REACH = 20.0                      # 짝 줄을 찾는 거리
MATE_MIN = 0.75
MATE_COS = math.cos(math.radians(30.0))
ROAD_WINDOW = max(MATE_REACH, 2 * (ROAD_HALF + KERB)) + 1.0
FAN_STEP = math.radians(22.5)
GRID = 24.0


class Obstacles:
    """모든 동의 건물 외곽. 변과 건물을 격자에 넣어 두고 가까운 것만 꺼낸다."""
    def __init__(self, rings):
        self.rings = [np.array(r, dtype=float) for r in rings]
        self.lo = np.array([r.min(0) for r in self.rings]); self.hi = np.array([r.max(0) for r in self.rings])
        self.e1 = np.concatenate(self.rings)
        self.e2 = np.concatenate([np.roll(r, -1, axis=0) for r in self.rings])
        self.edge_cells, self.ring_cells = {}, {}
        lo = np.floor(np.minimum(self.e1, self.e2) / GRID).astype(int); hi = np.floor(np.maximum(self.e1, self.e2) / GRID).astype(int)
        for k in range(len(self.e1)): self._put(self.edge_cells, lo[k], hi[k], k)
        lo = np.floor(self.lo / GRID).astype(int); hi = np.floor(self.hi / GRID).astype(int)
        for k in range(len(self.rings)): self._put(self.ring_cells, lo[k], hi[k], k)

    @staticmethod
    def _put(cells, lo, hi, k):
        for cx in range(lo[0], hi[0] + 1):
            for cz in range(lo[1], hi[1] + 1): cells.setdefault((cx, cz), []).append(k)

    @staticmethod
    def _near(cells, x0, z0, x1, z1):
        found = set()
        for cx in range(int(math.floor(x0 / GRID)), int(math.floor(x1 / GRID)) + 1):
            for cz in range(int(math.floor(z0 / GRID)), int(math.floor(z1 / GRID)) + 1): found.update(cells.get((cx, cz), ()))
        return np.array(sorted(found), dtype=int)

    def edges_near(self, x0, z0, x1, z1): return self._near(self.edge_cells, x0, z0, x1, z1)
    def rings_near(self, x0, z0, x1, z1): return self._near(self.ring_cells, x0, z0, x1, z1)

    def inside_any(self, pts):
        """점마다: 어느 건물 안에 있는가."""
        pts = np.asarray(pts, dtype=float)
        out = np.zeros(len(pts), dtype=bool)
        if not len(pts): return out
        for k in self.rings_near(pts[:, 0].min(), pts[:, 1].min(), pts[:, 0].max(), pts[:, 1].max()):
            box = ((pts >= self.lo[k]) & (pts <= self.hi[k])).all(1) & ~out
            if box.any(): out[box] |= points_in_ring(pts[box], self.rings[k])
        return out

    def hits(self, poly, gap=0.0):
        """볼록 조각(삼각형·사각형)이 건물과 겹치거나 gap보다 가까운가. 닿기만 해도 겹친 것으로 친다."""
        poly = np.asarray(poly, dtype=float)
        lo, hi = poly.min(0) - gap, poly.max(0) + gap
        edges = self.edges_near(lo[0], lo[1], hi[0], hi[1])
        if len(edges):
            a, b = poly, np.roll(poly, -1, axis=0)
            if (segments_apart(a[:, None], b[:, None], self.e1[edges][None], self.e2[edges][None]) <= gap).any(): return True
        for k in self.rings_near(lo[0], lo[1], hi[0], hi[1]):
            if points_in_ring(poly[:1], self.rings[k])[0]: return True          # 조각이 건물 안에 통째로
            if point_in_convex(self.rings[k][0], poly): return True             # 건물이 조각 안에 통째로
        return False


def points_in_ring(pts, ring):
    x, z = pts[:, 0][:, None], pts[:, 1][:, None]
    ax, az = ring[:, 0][None], ring[:, 1][None]
    bx, bz = np.roll(ring[:, 0], 1)[None], np.roll(ring[:, 1], 1)[None]
    span = (az > z) != (bz > z)
    with np.errstate(divide='ignore', invalid='ignore'):
        cut = x < (bx - ax) * (z - az) / (bz - az) + ax
    return (span & cut).sum(1) % 2 == 1


def point_in_convex(p, poly):
    a, b = poly, np.roll(poly, -1, axis=0)
    side = (b[:, 0] - a[:, 0]) * (p[1] - a[:, 1]) - (b[:, 1] - a[:, 1]) * (p[0] - a[:, 0])
    return bool((side >= 0).all() or (side <= 0).all())


def segments_cross(p1, p2, q1, q2):
    """선분끼리 만나는가(닿는 것 포함). 넘파이 배열끼리 넓혀서 한꺼번에."""
    def turn(a, b, c): return (b[..., 0] - a[..., 0]) * (c[..., 1] - a[..., 1]) - (b[..., 1] - a[..., 1]) * (c[..., 0] - a[..., 0])
    d1, d2, d3, d4 = turn(q1, q2, p1), turn(q1, q2, p2), turn(p1, p2, q1), turn(p1, p2, q2)
    proper = (d1 * d2 < 0) & (d3 * d4 < 0)
    def on(a, b, c, d):   # c가 선분 ab 위에 있는가 (d = turn(a, b, c))
        return (d == 0) & (np.minimum(a[..., 0], b[..., 0]) <= c[..., 0]) & (c[..., 0] <= np.maximum(a[..., 0], b[..., 0])) \
               & (np.minimum(a[..., 1], b[..., 1]) <= c[..., 1]) & (c[..., 1] <= np.maximum(a[..., 1], b[..., 1]))
    return proper | on(q1, q2, p1, d1) | on(q1, q2, p2, d2) | on(p1, p2, q1, d3) | on(p1, p2, q2, d4)


def segments_apart(p1, p2, q1, q2):
    """선분 사이의 가장 가까운 거리. 만나면 0."""
    def reach(p, a, b):   # 점 p에서 선분 ab까지
        ab = b - a
        t = np.clip(((p - a) * ab).sum(-1) / np.maximum((ab * ab).sum(-1), 1e-18), 0.0, 1.0)
        d = p - (a + ab * t[..., None])
        return np.hypot(d[..., 0], d[..., 1])
    apart = np.minimum(np.minimum(reach(p1, q1, q2), reach(p2, q1, q2)), np.minimum(reach(q1, p1, p2), reach(q2, p1, p2)))
    return np.where(segments_cross(p1, p2, q1, q2), 0.0, apart)


def free_pieces(zone_lo, zone_hi):
    """창 [-ROAD_WINDOW, ROAD_WINDOW]에서 막힌 띠들을 뺀 나머지 조각들."""
    pieces, cursor = [], -ROAD_WINDOW
    for k in np.argsort(zone_lo):
        a, b = zone_lo[k], zone_hi[k]
        if b <= cursor: continue
        if a >= ROAD_WINDOW: break
        if a > cursor: pieces.append((cursor, a))
        cursor = max(cursor, b)
    if cursor < ROAD_WINDOW: pieces.append((cursor, ROAD_WINDOW))
    return pieces


def fit_slab(free, mates, usual=None):
    """한 칸의 가로 폭을 정한다. free: 건물이 없는 조각들, mates: 짝 줄 후보의 가로 자리들,
    usual: 이 링크에서 짝이 없는 쪽의 흔한 폭 (오른쪽, 왼쪽). 없으면 정한 폭으로 맞춰 본다(1차).
    돌려주는 값: (연석 낮은 쪽, 연석 높은 쪽, 차도 낮은 쪽, 차도 높은 쪽, 오른쪽에 나란한 줄이 있는가, 왼쪽에 있는가) 또는 None.
    가로 자리는 선의 왼쪽이 +다."""
    def paired(t):
        a, b = min(0.0, t), max(0.0, t)
        clear = [max(0.0, min(b, hi) - max(a, lo)) for lo, hi in free]
        return (b - a) - sum(clear) <= ROAD_BLOCKED or max(clear) >= ROAD_OPEN * (b - a)
    reach = [0.0, 0.0]                       # 오른쪽(-), 왼쪽(+)으로 짝 줄까지의 거리. 사이가 건물로 막힌 줄은 짝이 아니다
    beside = [False, False]                  # 그쪽에 나란한 줄이 있는가(막혔든 아니든)
    for t in mates:
        side = 1 if t > 0 else 0
        beside[side] = True
        if paired(t): reach[side] = max(reach[side], abs(t))
    want_lo = -(max(ROAD_HALF, reach[0]) + (0.0 if reach[0] else KERB))
    want_hi = max(ROAD_HALF, reach[1]) + (0.0 if reach[1] else KERB)
    if usual is not None:
        if not beside[0] and usual[0] is not None: want_lo = max(want_lo, -usual[0])
        if not beside[1] and usual[1] is not None: want_hi = min(want_hi, usual[1])
    best, best_overlap = None, 0.0
    for lo, hi in free:
        overlap = min(hi, want_hi) - max(lo, want_lo)
        if overlap > best_overlap + 1e-9: best, best_overlap = (lo, hi), overlap
    if best is None or best_overlap < ROAD_MIN: return None
    lo, hi = max(want_lo, best[0]), min(want_hi, best[1])
    cut_lo, cut_hi = best[0] > want_lo + 1e-9, best[1] < want_hi - 1e-9
    if usual is None and not beside[0] and not beside[1]:        # 한 줄 길: 막힌 반대쪽으로 밀어 폭을 지킨다(1차에서 자리를 잡는다)
        full = 2 * (ROAD_HALF + KERB)
        if cut_lo and not cut_hi: hi = min(best[1], lo + full)
        elif cut_hi and not cut_lo: lo = max(best[0], hi - full)
    width = hi - lo
    if width < ROAD_MIN: return None
    kerb = min(KERB, 0.25 * width)
    kerb_lo = kerb if (not reach[0] or cut_lo) else 0.0
    kerb_hi = kerb if (not reach[1] or cut_hi) else 0.0
    return lo, hi, lo + kerb_lo, hi - kerb_hi, beside[0], beside[1]


def fit_segment(a, b_, others, obstacles):
    """한 선분을 ROAD_STEP마다 잘라 칸마다 건물이 없는 조각과 짝 줄 후보를 찾는다. others: 다른 모든 선분 (시작 N×2, 끝 N×2).
    돌려주는 값: 칸의 경계 자리(K+1), 칸마다의 빈 조각들, 칸마다의 짝 후보들, 길이 방향 단위, 왼쪽 단위."""
    a, b_ = np.array(a, dtype=float), np.array(b_, dtype=float)
    L = float(np.hypot(*(b_ - a)))
    if L < 1e-3: return None
    u = (b_ - a) / L
    n = np.array([-u[1], u[0]])
    K = max(1, int(math.ceil(L / ROAD_STEP)))
    bounds = np.linspace(0.0, L, K + 1)
    mid = (bounds[:-1] + bounds[1:]) / 2
    pad = ROAD_WINDOW + ROAD_GAP + ROAD_EASE + 1.0
    lo, hi = np.minimum(a, b_) - pad, np.maximum(a, b_) + pad

    # 건물 변이 칸마다 가로로 차지하는 띠
    edges = obstacles.edges_near(lo[0], lo[1], hi[0], hi[1])
    if len(edges):
        s1, t1 = (obstacles.e1[edges] - a) @ u, (obstacles.e1[edges] - a) @ n
        s2, t2 = (obstacles.e2[edges] - a) @ u, (obstacles.e2[edges] - a) @ n
        sa, sb = (bounds[:-1] - ROAD_GAP - ROAD_EASE)[:, None], (bounds[1:] + ROAD_GAP + ROAD_EASE)[:, None]
        c_lo, c_hi = np.maximum(np.minimum(s1, s2)[None], sa), np.minimum(np.maximum(s1, s2)[None], sb)
        ds = (s2 - s1)[None]
        flat = np.abs(ds) < 1e-9
        with np.errstate(divide='ignore', invalid='ignore'):
            ta = np.where(flat, t1[None], t1[None] + (c_lo - s1[None]) / ds * (t2 - t1)[None])
            tb = np.where(flat, t2[None], t1[None] + (c_hi - s1[None]) / ds * (t2 - t1)[None])
        z_lo, z_hi = np.minimum(ta, tb) - ROAD_GAP, np.maximum(ta, tb) + ROAD_GAP
        live = (c_lo <= c_hi) & (z_hi > -ROAD_WINDOW) & (z_lo < ROAD_WINDOW)
    # 짝 줄 후보: 나란히 가고, 이 칸의 가로선과 만나는 다른 선분
    o1, o2 = others
    near = ((np.maximum(o1, o2) >= lo).all(1) & (np.minimum(o1, o2) <= hi).all(1))
    q1, q2 = o1[near], o2[near]
    mate_t = np.full((K, 0), np.nan)
    if len(q1):
        d = q2 - q1
        dl = np.hypot(d[:, 0], d[:, 1])
        keep = (dl > 1e-3) & (np.abs(d @ u) >= MATE_COS * np.maximum(dl, 1e-9))
        q1, q2 = q1[keep], q2[keep]
        if len(q1):
            s1, t1 = (q1 - a) @ u, (q1 - a) @ n
            s2, t2 = (q2 - a) @ u, (q2 - a) @ n
            ds = (s2 - s1)[None]
            with np.errstate(divide='ignore', invalid='ignore'):
                t = t1[None] + (mid[:, None] - s1[None]) / ds * (t2 - t1)[None]
            ok = (np.abs(ds) > 1e-9) & (mid[:, None] >= np.minimum(s1, s2)[None]) & (mid[:, None] <= np.maximum(s1, s2)[None]) \
                 & (np.abs(t) >= MATE_MIN) & (np.abs(t) <= MATE_REACH)
            mate_t = np.where(ok, t, np.nan)

    # 칸마다 건물이 없는 조각을 찾는다: 띠 사이의 틈을 모아, 틈의 가운데가 건물 밖인지 한꺼번에 묻는다
    gaps, owner = [], []
    for i in range(K):
        pieces = free_pieces(z_lo[i][live[i]], z_hi[i][live[i]]) if len(edges) else [(-ROAD_WINDOW, ROAD_WINDOW)]
        for p in pieces:
            if p[1] - p[0] >= 0.2: gaps.append(p); owner.append(i)
    free = [[] for _ in range(K)]
    if gaps:
        g = np.array(gaps)
        centre = a[None] + u[None] * mid[np.array(owner)][:, None] + n[None] * ((g[:, 0] + g[:, 1]) / 2)[:, None]
        outside = ~obstacles.inside_any(centre)
        for k, i in enumerate(owner):
            if outside[k]: free[i].append(gaps[k])
    mates = [[float(t) for t in mate_t[i][~np.isnan(mate_t[i])]] for i in range(K)]
    return bounds, free, mates, u, n


def strip_quads(bounds, slabs, layer):
    """칸들을 이어 붙여 사다리꼴로 만든다. 칸의 경계에서는 두 칸의 폭 가운데 좁은 쪽을 쓴다(그래야 사다리꼴이 제 칸 안에 든다).
    layer: 0 = 연석, 1 = 차도. 돌려주는 값: (s0, s1, lo0, hi0, lo1, hi1) 목록. 같은 기울기로 이어지는 칸은 하나로 합친다."""
    K = len(slabs)
    def own(i): return slabs[i][layer * 2], slabs[i][layer * 2 + 1]
    def joint(i):
        """칸 i와 i+1의 경계에서 쓸 폭. 이어지지 않으면 None."""
        if slabs[i] is None or slabs[i + 1] is None: return None
        k_lo, k_hi = max(slabs[i][0], slabs[i + 1][0]), min(slabs[i][1], slabs[i + 1][1])
        if k_hi - k_lo < 0.2: return None
        if layer == 0: return k_lo, k_hi
        r_lo, r_hi = max(slabs[i][2], slabs[i + 1][2]), min(slabs[i][3], slabs[i + 1][3])
        if r_lo > r_hi: r_lo = r_hi = min(max((r_lo + r_hi) / 2, k_lo), k_hi)
        return r_lo, r_hi
    quads = []
    for i in range(K):
        if slabs[i] is None: continue
        start = (joint(i - 1) if i > 0 else None) or own(i)
        end = (joint(i) if i < K - 1 else None) or own(i)
        q = [bounds[i], bounds[i + 1], start[0], start[1], end[0], end[1]]
        if quads:
            p = quads[-1]
            same = abs(p[1] - q[0]) < 1e-9 and abs(p[4] - q[2]) < 1e-9 and abs(p[5] - q[3]) < 1e-9 and (i > 0 and joint(i - 1) is not None)
            if same:
                lp, lq = p[1] - p[0], q[1] - q[0]
                if abs((p[4] - p[2]) * lq - (q[4] - q[2]) * lp) < 1e-7 and abs((p[5] - p[3]) * lq - (q[5] - q[3]) * lp) < 1e-7:
                    p[1], p[4], p[5] = q[1], q[4], q[5]
                    continue
        quads.append(q)
    return quads


def fit_roads(segs_by_module, links_by_module, obstacles):
    """모든 동의 도로를 건물 사이에 맞춘다. links_by_module: 선분마다 그 선분이 든 링크의 이름.
    돌려주는 값: 동마다 [연석 조각들, 차도 조각들]과 센 값. 조각은 볼록한 점 목록(사각형·삼각형)."""
    flat = [(m, a, b_, link) for m, (segs, links) in enumerate(zip(segs_by_module, links_by_module)) for (a, b_), link in zip(segs, links)]
    o1 = np.array([f[1] for f in flat], dtype=float); o2 = np.array([f[2] for f in flat], dtype=float)
    out = [[[], []] for _ in segs_by_module]
    tracks = []                                 # 차도의 가운데 선(길 그래프에 쓴다): 이어진 칸들의 가운데, 선분의 양 끝이면 노드링크 마디
    ends = {}                                   # 마디 자리 → 그 마디에 닿은 선분 끝들
    stats = {'slabs': 0, 'empty': 0, 'merged': 0, 'narrowed': 0, 'fans': 0, 'fansDropped': 0}
    # 1차: 선분마다 빈 조각과 짝을 찾고, 정한 폭으로 맞춰 본다. 링크마다 짝이 없는 쪽의 폭을 모은다
    found, seen, count = [], {}, {}
    for index, (m, a, b_, link) in enumerate(flat):
        rest = np.ones(len(flat), dtype=bool); rest[index] = False
        fitted = fit_segment(a, b_, (o1[rest], o2[rest]), obstacles)
        found.append(fitted)
        if fitted is None: continue
        for free, mates in zip(fitted[1], fitted[2]):
            slab = fit_slab(free, mates) if free else None
            if slab is None: continue
            count[link] = count.get(link, 0) + 1
            if not slab[4]: seen.setdefault((link, 0), []).append(-slab[0])
            if not slab[5]: seen.setdefault((link, 1), []).append(slab[1])
    # 그 쪽에 짝이 없는 칸이 링크의 절반을 넘을 때만 흔한 폭을 쓴다(짝이 잠깐 끊긴 자리를 좁히지 않는다)
    usual = {key: float(np.percentile(values, ROAD_USUAL)) for key, values in seen.items() if len(values) * 2 > count[key[0]]}
    # 2차: 링크의 흔한 폭 안에서 다시 맞추고 조각을 낸다
    for (m, a, b_, link), fitted in zip(flat, found):
        if fitted is None: continue
        bounds, frees, mates, u, n = fitted
        limit = (usual.get((link, 0)), usual.get((link, 1)))
        slabs = [fit_slab(free, mate, limit) if free else None for free, mate in zip(frees, mates)]
        origin = np.array(a, dtype=float)
        stats['slabs'] += len(slabs); stats['empty'] += sum(s is None for s in slabs)
        stats['merged'] += sum(s is not None and (s[1] - s[0]) > 2 * (ROAD_HALF + KERB) + 1e-6 for s in slabs)
        stats['narrowed'] += sum(s is not None and (s[1] - s[0]) < 2 * (ROAD_HALF + KERB) - 1e-6 for s in slabs)
        run = []
        for i, slab in enumerate(slabs + [None]):
            if slab is not None:
                run.append((i, origin + u * (bounds[i] + bounds[i + 1]) / 2 + n * (slab[2] + slab[3]) / 2))
                continue
            if run:
                key_a = (round(a[0], 2), round(a[1], 2)) if run[0][0] == 0 else None
                key_b = (round(b_[0], 2), round(b_[1], 2)) if run[-1][0] == len(slabs) - 1 else None
                tracks.append({'points': [p for _, p in run], 'start': key_a, 'end': key_b})
            run = []
        for layer in (0, 1):
            for s0, s1, lo0, hi0, lo1, hi1 in strip_quads(bounds, slabs, layer):
                if max(hi0 - lo0, hi1 - lo1) < 1e-6: continue
                p0, p1 = origin + u * s0, origin + u * s1
                out[m][layer].append([tuple(p0 + n * hi0), tuple(p1 + n * hi1), tuple(p1 + n * lo1), tuple(p0 + n * lo0)])
        # 마디에 닿는 끝: 바깥으로 나가는 방향, 그 방향의 왼쪽·오른쪽 폭(연석, 차도)
        for at_start in (True, False):
            slab = slabs[0] if at_start else slabs[-1]
            if slab is None or not (slab[0] <= 0.0 <= slab[1]): continue
            point = a if at_start else b_
            away = u if at_start else -u
            left = (slab[1], slab[3]) if at_start else (-slab[0], -slab[2])
            right = (-slab[0], -slab[2]) if at_start else (slab[1], slab[3])
            ends.setdefault((round(point[0], 2), round(point[1], 2)), []).append((math.atan2(away[1], away[0]), m, left, right, point))
    # 꺾이는 마디의 바깥쪽 쐐기와 막다른 끝을 부채꼴로 메운다. 건물에 닿는 부채 조각은 버린다
    for key in sorted(ends):
        group = sorted(ends[key], key=lambda e: e[0])
        for k, first in enumerate(group):
            second = group[(k + 1) % len(group)]
            gap = (second[0] - first[0]) % (2 * math.pi) if len(group) > 1 else 2 * math.pi
            if gap < 1e-9: gap = 2 * math.pi
            sweep = gap - math.pi
            if sweep < math.radians(2.0): continue
            dead_end = len(group) == 1
            steps = max(1, int(math.ceil(sweep / FAN_STEP)))
            centre = np.array(first[4], dtype=float)
            for layer in (0, 1):
                r0, r1 = first[2][layer], second[3][layer]
                if dead_end: r0, r1 = min(r0, ROAD_HALF + (KERB if layer == 0 else 0.0)), min(r1, ROAD_HALF + (KERB if layer == 0 else 0.0))
                if max(r0, r1) < 0.05: continue
                rim = []
                for j in range(steps + 1):
                    f = j / steps
                    angle = first[0] + math.pi / 2 + sweep * f
                    r = max(0.0, r0 + (r1 - r0) * f)
                    rim.append(centre + np.array([math.cos(angle), math.sin(angle)]) * r)
                for j in range(steps):
                    tri = [tuple(centre), tuple(rim[j + 1]), tuple(rim[j])]
                    if abs(cross(tri[0], tri[1], tri[2])) < 1e-6: continue
                    if obstacles.hits(tri, ROAD_GAP): stats['fansDropped'] += 1; continue
                    out[first[1]][layer].append(tri); stats['fans'] += 1
    return out, stats, tracks


def road_overlaps(pieces, obstacles, gap=0.0):
    """확인: 도로 조각이 덮거나 gap보다 가까이 닿은 건물 수. 맞추는 계산과 따로, 조각과 건물 외곽을 직접 견준다."""
    hit = set()
    for poly in pieces:
        p = np.asarray(poly, dtype=float)
        lo, hi = p.min(0) - gap, p.max(0) + gap
        a, b = p, np.roll(p, -1, axis=0)
        for k in obstacles.rings_near(lo[0], lo[1], hi[0], hi[1]):
            if k in hit: continue
            ring = obstacles.rings[k]
            if (obstacles.hi[k] < lo).any() or (obstacles.lo[k] > hi).any(): continue
            if (segments_apart(a[:, None], b[:, None], ring[None], np.roll(ring, -1, axis=0)[None]) <= gap).any() \
               or points_in_ring(p[:1], ring)[0] or point_in_convex(ring[0], p): hit.add(int(k))
    return len(hit)


# ── 골목: 건물 배치에서 계산한 통로 (3D 기반 B3, 2026-11-14) ───────────────────
# 노드링크에는 큰길만 있다. 건물 사이의 골목은 자료가 없어서, 건물 배치에서 계산해 그린다(표현값이고 도로 자료가 아니다).
# 건물이 없는 땅을 0.5 m 격자에 그리고, 양쪽 건물에서 같은 거리에 있는 가운데 선을 찾는다. 폭이 3 m 이상인 통로만 남긴다.
# 필요한 것: 넘파이, PIL, scipy, scikit-image.
ALLEY_CELL = 0.5
ALLEY_MIN = 1.5                        # 건물까지 이만큼 이상 떨어진 가운데 선만 쓴다(통로 폭 3 m 이상)
ALLEY_NEAR = 14.0                      # 건물에서 이보다 먼 빈터는 골목이 아니다
ALLEY_HALF = 1.6                       # 그리는 반폭의 위쪽 끝(폭 3.2 m)
ALLEY_GAP = 0.4                        # 건물 벽에서 떼는 틈
ALLEY_SPUR = 6.0                       # 이보다 짧은 막다른 가지는 지운다
ALLEY_PIECE = 12.0                     # 이보다 짧은 외딴 조각은 지운다
ALLEY_BEND = 0.4                       # 가운데 선을 꺾은선으로 줄일 때 허용하는 어긋남
C_ALLEY = 0x2A3B49


def alley_lines(boundary, rings, road_pieces):
    """한 동의 골목 가운데 선. 돌려주는 값: 꺾은선 목록. 꺾은선의 점은 (x, z, 건물까지의 거리)."""
    from PIL import Image, ImageDraw
    from scipy import ndimage
    from skimage.morphology import medial_axis, skeletonize
    edge = np.array(boundary)
    x0, z0 = edge.min(0) - 40.0
    x1, z1 = edge.max(0) + 40.0
    W, H = int((x1 - x0) / ALLEY_CELL) + 1, int((z1 - z0) / ALLEY_CELL) + 1
    def mask(polys):
        image = Image.new('1', (W, H), 0)
        draw = ImageDraw.Draw(image)
        for poly in polys:
            p = np.asarray(poly, dtype=float)
            if p[:, 0].max() < x0 or p[:, 0].min() > x1 or p[:, 1].max() < z0 or p[:, 1].min() > z1: continue
            draw.polygon([((q[0] - x0) / ALLEY_CELL, (z1 - q[1]) / ALLEY_CELL) for q in p], fill=1)
        return np.array(image, dtype=bool)
    built = mask(rings)
    apart = ndimage.distance_transform_edt(~built) * ALLEY_CELL          # 건물까지의 거리
    free = ~built & mask([boundary]) & (apart < ALLEY_NEAR)
    middle = medial_axis(free, rng=0) & (apart >= ALLEY_MIN)       # rng를 정해 두지 않으면 구울 때마다 가운데 선이 조금씩 달라진다 & ~ndimage.binary_dilation(mask(road_pieces), iterations=2)
    middle = skeletonize(middle)
    label, count = ndimage.label(middle, structure=np.ones((3, 3)))
    if count:
        size = ndimage.sum(middle, label, range(1, count + 1))
        middle &= np.isin(label, np.nonzero(size * ALLEY_CELL >= ALLEY_PIECE)[0] + 1)
    rows, cols = np.nonzero(middle)
    index = {(int(r), int(c)): k for k, (r, c) in enumerate(zip(rows, cols))}
    near = [[] for _ in index]
    for (r, c), k in index.items():
        for dr in (-1, 0, 1):
            for dc in (-1, 0, 1):
                if not (dr or dc) or (r + dr, c + dc) not in index: continue
                if dr and dc and ((r + dr, c) in index or (r, c + dc) in index): continue   # 모서리로 돌아가는 길이 있으면 대각선은 잇지 않는다
                near[k].append(index[(r + dr, c + dc)])
    def step(a, b): return math.hypot(float(rows[a] - rows[b]), float(cols[a] - cols[b])) * ALLEY_CELL
    done, chains = set(), []
    def walk(start, second):
        path = [start, second]
        done.add((min(start, second), max(start, second)))
        while len(near[path[-1]]) == 2 and path[-1] != start:
            onward = [k for k in near[path[-1]] if k != path[-2]]
            if not onward or (min(path[-1], onward[0]), max(path[-1], onward[0])) in done: break
            done.add((min(path[-1], onward[0]), max(path[-1], onward[0])))
            path.append(onward[0])
        return path
    for k in range(len(near)):                     # 끝과 갈림에서 시작하는 줄
        if len(near[k]) == 2: continue
        for other in near[k]:
            if (min(k, other), max(k, other)) not in done: chains.append(walk(k, other))
    for k in range(len(near)):                     # 끝도 갈림도 없는 고리
        for other in near[k]:
            if (min(k, other), max(k, other)) not in done: chains.append(walk(k, other))
    lines = []
    for path in chains:
        length = sum(step(path[i], path[i + 1]) for i in range(len(path) - 1))
        dead_end = len(near[path[0]]) == 1 or len(near[path[-1]]) == 1
        if dead_end and length < ALLEY_SPUR: continue
        lines.append([(x0 + float(cols[k]) * ALLEY_CELL, z1 - float(rows[k]) * ALLEY_CELL, float(apart[rows[k], cols[k]])) for k in path])
    return lines


def bend_points(points, tolerance):
    """꺾은선을 줄였을 때 남는 점의 번호."""
    keep, stack = {0, len(points) - 1}, [(0, len(points) - 1)]
    xz = np.array([(p[0], p[1]) for p in points])
    while stack:
        a, b = stack.pop()
        if b - a < 2: continue
        ab = xz[b] - xz[a]
        L = float(np.hypot(*ab))
        mid = xz[a + 1:b]
        off = np.hypot(*(mid - xz[a]).T) if L < 1e-9 else np.abs(ab[0] * (mid[:, 1] - xz[a][1]) - ab[1] * (mid[:, 0] - xz[a][0])) / L
        k = int(np.argmax(off))
        if off[k] > tolerance:
            keep.add(a + 1 + k); stack.append((a, a + 1 + k)); stack.append((a + 1 + k, b))
    return sorted(keep)


def alley_pieces(lines, obstacles):
    """가운데 선을 띠 조각(사각형)으로 바꾼다. 폭은 그 자리의 통로 폭에서 틈을 뺀 만큼이고, 건물에 가까운 조각은 버린다."""
    pieces, dropped, length, drawn = [], 0, 0.0, []
    for line in lines:
        kept = bend_points(line, ALLEY_BEND)
        for i, j in zip(kept, kept[1:]):
            a, b_ = np.array(line[i][:2]), np.array(line[j][:2])
            L = float(np.hypot(*(b_ - a)))
            if L < 1e-3: continue
            half = min(ALLEY_HALF, min(p[2] for p in line[i:j + 1]) - ALLEY_GAP - ALLEY_BEND)
            if half < 0.6: dropped += 1; continue
            u = (b_ - a) / L
            n = np.array([-u[1], u[0]])
            centre = (tuple(a), tuple(b_))
            a, b_ = a - u * min(half, 0.6), b_ + u * min(half, 0.6)          # 꺾이는 곳이 벌어지지 않게 조금 겹친다
            quad = [tuple(a + n * half), tuple(b_ + n * half), tuple(b_ - n * half), tuple(a - n * half)]
            if obstacles.hits(quad, ALLEY_GAP * 0.5): dropped += 1; continue
            pieces.append(quad); length += L; drawn.append(centre)
    return pieces, dropped, length, drawn


# ── 길 그래프: 기사가 다니는 길 (R1, 2026-11-14) ───────────────────────────────
# 큰길(fit_roads가 건물 사이에 맞춘 차도의 가운데)과 골목(alley_lines의 가운데 선)을 한 그래프로 묶고,
# 건물마다 가장 가까운 길 위의 자리를 「가상 출입구」로 잡는다. 공식 출입구 자료가 아니다(자료 미확인 · 가상).
# 쓰는 곳: Resources/hongdal-dong-routes.bytes (HRTE v1). 모두 표현값이다.
ROUTE_BEND = 0.3                       # 큰길 가운데 선을 꺾은선으로 줄일 때 허용하는 어긋남
ROUTE_JOIN = 14.0                      # 골목의 막다른 끝을 이 거리 안의 큰길에 잇는다
ROUTE_DOOR = 30.0                      # 건물에서 이 거리 안에 길이 없으면 출입구를 잡지 않는다
KIND_ROAD, KIND_ALLEY, KIND_LINK = 0, 1, 2


def foot_on_segments(points, a, b):
    """점들(P×2)에서 선분들(E×2 두 개)까지: 거리(P×E), 선분 위 자리 t(P×E)."""
    ab = (b - a)[None]
    t = np.clip(((points[:, None] - a[None]) * ab).sum(-1) / np.maximum((ab * ab).sum(-1), 1e-18), 0.0, 1.0)
    d = points[:, None] - (a[None] + ab * t[..., None])
    return np.hypot(d[..., 0], d[..., 1]), t


def clear_line(obstacles, p, q):
    """두 점을 잇는 선이 건물에 닿지 않는가."""
    p, q = np.asarray(p, dtype=float), np.asarray(q, dtype=float)
    L = float(np.hypot(*(q - p)))
    if L < 1e-6: return True
    side = np.array([-(q - p)[1], (q - p)[0]]) / L * 0.05
    return not obstacles.hits([tuple(p + side), tuple(q + side), tuple(q - side), tuple(p - side)])


def build_routes(tracks, alley_segments, rings, obstacles):
    """돌려주는 값: 마디(N×2), 변(E×2), 변의 종류(E), 건물마다 (변 번호, 변 위 자리, 문 x, 문 z), 센 값."""
    nodes, where = [], {}
    def node(p):
        key = (round(float(p[0]), 2), round(float(p[1]), 2))
        if key not in where: where[key] = len(nodes); nodes.append((float(p[0]), float(p[1])))
        return where[key]
    edges, kinds, seen = [], [], set()
    def edge(i, j, kind):
        if i == j or (min(i, j), max(i, j)) in seen: return
        seen.add((min(i, j), max(i, j))); edges.append((i, j)); kinds.append(kind)
    # 큰길: 선분마다 차도 가운데를 잇고, 노드링크 마디에서 만나는 끝들을 한 점으로 모은다
    joins = {}
    for track in tracks:
        points = track['points']
        kept = bend_points([(p[0], p[1], 0.0) for p in points], ROUTE_BEND) if len(points) > 2 else list(range(len(points)))
        ids = [node(points[k]) for k in kept]
        for i, j in zip(ids, ids[1:]): edge(i, j, KIND_ROAD)
        if track['start'] is not None: joins.setdefault(track['start'], []).append(ids[0])
        if track['end'] is not None: joins.setdefault(track['end'], []).append(ids[-1])
    for key in sorted(joins):
        group = sorted(set(joins[key]))
        if len(group) < 2: continue
        centre = np.mean([nodes[i] for i in group], axis=0)
        if obstacles.inside_any(centre[None])[0]:
            for i, j in zip(group, group[1:]):
                if clear_line(obstacles, nodes[i], nodes[j]): edge(i, j, KIND_ROAD)
        else:
            hub = node(centre)
            for i in group:
                if clear_line(obstacles, nodes[i], centre): edge(hub, i, KIND_ROAD)
    road_edges = len(edges)
    # 골목
    for a, b_ in alley_segments: edge(node(a), node(b_), KIND_ALLEY)
    # 골목의 막다른 끝을 가까운 큰길에 잇는다. 큰길의 변은 닿는 자리에서 나눈다
    degree = np.zeros(len(nodes), dtype=int)
    for i, j in edges: degree[i] += 1; degree[j] += 1
    xy = np.array(nodes)
    ra, rb = xy[[e[0] for e in edges[:road_edges]]], xy[[e[1] for e in edges[:road_edges]]]
    cells = {}
    lo = np.floor(np.minimum(ra, rb) / GRID).astype(int); hi = np.floor(np.maximum(ra, rb) / GRID).astype(int)
    for k in range(road_edges): Obstacles._put(cells, lo[k], hi[k], k)
    alley_node = np.zeros(len(nodes), dtype=bool)
    for (i, j), kind in zip(edges, kinds):
        if kind == KIND_ALLEY: alley_node[i] = alley_node[j] = True
    splits, links, linked = {}, [], 0
    for i in np.nonzero(alley_node & (degree == 1))[0]:
        p = xy[i]
        near = Obstacles._near(cells, p[0] - ROUTE_JOIN, p[1] - ROUTE_JOIN, p[0] + ROUTE_JOIN, p[1] + ROUTE_JOIN)
        if not len(near): continue
        dist, t = foot_on_segments(p[None], ra[near], rb[near])
        k = int(np.argmin(dist[0]))
        if dist[0, k] > ROUTE_JOIN or dist[0, k] < 1e-6: continue
        foot = ra[near[k]] + (rb[near[k]] - ra[near[k]]) * t[0, k]
        along = (foot - p) / dist[0, k]
        side = np.array([-along[1], along[0]]) * 0.3
        if obstacles.hits([tuple(p + side), tuple(foot + side), tuple(foot - side), tuple(p - side)]): continue
        splits.setdefault(int(near[k]), []).append((float(t[0, k]), tuple(foot), int(i)))
        linked += 1
    for k, cuts in splits.items():
        a, b_ = edges[k]
        chain = [a]
        for t, foot, alley_end in sorted(cuts):
            mid = a if t < 1e-6 else b_ if t > 1 - 1e-6 else node(foot)
            chain.append(mid); links.append((mid, alley_end))
        chain.append(b_)
        edges[k] = None; seen.discard((min(a, b_), max(a, b_)))
        for i, j in zip(chain, chain[1:]): edge(i, j, KIND_ROAD)
    for i, j in links: edge(i, j, KIND_LINK)
    kept = [(e, k) for e, k in zip(edges, kinds) if e is not None]
    edges, kinds = [e for e, _ in kept], [k for _, k in kept]
    # 이어진 덩어리: 가장 큰 덩어리에만 출입구를 잡는다(어느 건물에서 어느 건물로도 갈 수 있게)
    xy = np.array(nodes)
    ea, eb = np.array([e[0] for e in edges]), np.array([e[1] for e in edges])
    length = np.hypot(*(xy[ea] - xy[eb]).T)
    parent = list(range(len(nodes)))
    def find(i):
        while parent[i] != i: parent[i] = parent[parent[i]]; i = parent[i]
        return i
    for i, j in edges:
        ri, rj = find(i), find(j)
        if ri != rj: parent[ri] = rj
    root = np.array([find(int(i)) for i in ea])
    totals = {}
    for r, l in zip(root, length): totals[int(r)] = totals.get(int(r), 0.0) + float(l)
    main = max(totals, key=totals.get)
    in_main = root == main
    cells = {}
    lo = np.floor(np.minimum(xy[ea], xy[eb]) / GRID).astype(int); hi = np.floor(np.maximum(xy[ea], xy[eb]) / GRID).astype(int)
    for k in np.nonzero(in_main)[0]: Obstacles._put(cells, lo[k], hi[k], int(k))
    doors, far = [], 0
    for ring in rings:
        r = np.asarray(ring, dtype=float)
        near = Obstacles._near(cells, r[:, 0].min() - ROUTE_DOOR, r[:, 1].min() - ROUTE_DOOR, r[:, 0].max() + ROUTE_DOOR, r[:, 1].max() + ROUTE_DOOR)
        if not len(near): doors.append((-1, 0.0, 0.0, 0.0)); far += 1; continue
        a, b_ = xy[ea[near]], xy[eb[near]]
        d1, t1 = foot_on_segments(r, a, b_)                                # 건물 모서리 → 길
        i1 = np.unravel_index(int(np.argmin(d1)), d1.shape)
        ends = np.concatenate([a, b_])                                     # 길의 끝점 → 건물 변
        d2, t2 = foot_on_segments(ends, r, np.roll(r, -1, axis=0))
        i2 = np.unravel_index(int(np.argmin(d2)), d2.shape)
        if d1[i1] <= d2[i2]:
            best, e, t, door = float(d1[i1]), int(near[i1[1]]), float(t1[i1]), r[i1[0]]
        else:
            which = i2[0] % len(near)
            best, e, t = float(d2[i2]), int(near[which]), 0.0 if i2[0] < len(near) else 1.0
            door = r[i2[1]] + (np.roll(r, -1, axis=0)[i2[1]] - r[i2[1]]) * t2[i2]
        if best > ROUTE_DOOR: doors.append((-1, 0.0, 0.0, 0.0)); far += 1; continue
        doors.append((e, t, float(door[0]), float(door[1])))
    stats = {'nodes': len(nodes), 'edges': len(edges), 'roadMetres': round(float(length[np.array(kinds) == KIND_ROAD].sum()), 1),
             'alleyMetres': round(float(length[np.array(kinds) == KIND_ALLEY].sum()), 1), 'linkMetres': round(float(length[np.array(kinds) == KIND_LINK].sum()), 1),
             'alleyEndsLinked': linked, 'pieces': len(totals), 'mainShare': round(totals[main] / float(length.sum()), 4),
             'buildings': len(rings), 'buildingsWithDoor': len(rings) - far,
             'edgesThroughBuildings': sum(0 if clear_line(obstacles, xy[i], xy[j]) else 1 for i, j in edges)}
    return xy, np.array(edges, dtype='<i4'), np.array(kinds, dtype='u1'), doors, stats


def write_routes(path, xy, edges, kinds, doors):
    with open(path, 'wb') as f:
        f.write(b'HRTE'); f.write(struct.pack('<i', 1))
        f.write(struct.pack('<4d', LAT0, LON0, M_LAT, M_LON))
        f.write(struct.pack('<i', len(xy))); f.write(xy.astype('<f4').tobytes())
        f.write(struct.pack('<i', len(edges))); f.write(edges.astype('<i4').tobytes()); f.write(kinds.tobytes())
        f.write(struct.pack('<i', len(doors)))
        f.write(np.array([d[0] for d in doors], dtype='<i4').tobytes())
        f.write(np.array([d[1] for d in doors], dtype='<f4').tobytes())
        f.write(np.array([(d[2], d[3]) for d in doors], dtype='<f4').tobytes())


# ── 입력 확인 ──────────────────────────────────────────────────────────────
scope = json.load(open(SCOPE, encoding='utf-8'))
frame_def = scope['coordinateFrame']
assert frame_def['method'] == 'WGS84-ECEF-ENU-at-zero-altitude' and frame_def['metersPerUnit'] == 1.0
assert frame_def['worldOffsetX'] == 0.0 and frame_def['worldOffsetZ'] == 0.0
LAT0, LON0 = frame_def['originLatitude'], frame_def['originLongitude']
M_LON = M_LAT * math.cos(math.radians(LAT0))
frame = Frame(LAT0, LON0)
by_id = {m['administrativeAreaStableId']: m for m in scope['modules']}
report = {'schema': 'hongdal-dong-module-bake-report.v1', 'scopeRevision': scope['revision'], 'scopeContentHashSha256': scope['contentHashSha256'],
          'scopeFileSha256': sha256(SCOPE), 'origin': {'latitude': LAT0, 'longitude': LON0}, 'gameMetresPerDegree': {'latitude': M_LAT, 'longitude': M_LON},
          'modules': [], 'checks': []}
def check(name, ok, detail=''):
    report['checks'].append({'name': name, 'ok': bool(ok), 'detail': str(detail)})
    if not ok: print('CHECK FAILED', name, detail)

def game_xz(lat, lon): return (lon - LON0) * M_LON, (lat - LAT0) * M_LAT

modules_out, index_buildings, all_shapes = [], [], []
union = [180.0, 90.0, -180.0, -90.0]   # west, south, east, north
max_err = 0.0


# ── 한 동 굽기 (3D 기반 B1, 2026-11-14: 한 덩어리였던 것을 확인 · 좌표 · 바닥 · 건물 · 도로 · 쓰기로 나눴다) ──
# 정점 순서는 바닥 → 건물(벽, 지붕) → 도로(연석, 차도)이고, 그림자는 건물 순서다. 이 순서를 바꾸면 자원 해시가 달라진다.
# B2부터 도로는 모든 동의 건물을 세운 뒤에 깐다. 건물 색인(hongdal-dong-buildings.json)은 B1 때와 바이트까지 같다.

class Points:
    """한 동의 모든 점(경계 · 건물 외곽 · 도로 끝점)을 한 번에 위경도와 게임 좌표로 바꿔 두고, 넣은 순서대로 꺼낸다."""
    def __init__(self, m):
        pts_e, pts_n = [], []
        def add(p): pts_e.append(p['x']); pts_n.append(p['z'])
        for p in m['boundary']: add(p)
        for b in m['buildings']:
            for p in b['footprint']: add(p)
        for r in m['roads']: add(r['from']); add(r['to'])
        self.lat, self.lon, self.err = frame.inv(pts_e, pts_n)
        self.gx, self.gz = (self.lon - LON0) * M_LON, (self.lat - LAT0) * M_LAT
        self.cursor = 0
    def take(self, n):
        s = slice(self.cursor, self.cursor + n); self.cursor += n
        return self.lat[s], self.lon[s], self.gx[s], self.gz[s]
    def used_all(self): return self.cursor == len(self.lat)


def verify_module(code, entry, m, file_sha):
    """원본 모듈이 scope가 가리키는 그 판본인지 확인한다."""
    check(f'{code} 파일 해시가 scope와 같음', entry and file_sha == entry['sha256'].upper(), file_sha)
    check(f'{code} 내용 해시가 scope와 같음', entry and m['contentHashSha256'] == entry['contentHashSha256'], m['contentHashSha256'])
    check(f'{code} 좌표 원점이 scope와 같음', m['coordinateFrame'] == frame_def)
    check(f'{code} 판본이 scope와 같음', m['projectionRevision'] == scope['revision'], m['projectionRevision'])
    check(f'{code} 건물·도로 수가 scope와 같음', len(m['buildings']) == entry['buildingCount'] and len(m['roads']) == entry['roadSegmentCount'])


def bake_ground(mesh, boundary):
    """행정동 경계 안의 바닥."""
    mesh.flat(boundary, 0.01, rgb(C_DONG))


def building_height(b):
    """높이와 그 근거: 원본 높이(상징 높이가 아닐 때) → 지상 층수×3 m → 상징 높이 4 m. 14 m 위는 눌러서 그린다(표현값)."""
    symbolic = b['evidenceKindCode'].endswith('SymbolicHeightFallback')
    floors = b['aboveGroundFloorCount']
    if not symbolic and b['heightMeters']: H, source = float(b['heightMeters']), 'height'
    elif floors: H, source = float(floors) * 3.0, 'floors'
    else: H, source = float(b['heightMeters'] or 4.0), 'symbolic'
    h = H if H <= 14 else min(24.0, 14 + (H - 14) * 0.32)
    return H, h, source, floors


def bake_buildings(mesh, shadow, m, points, region):
    """건물마다 벽과 지붕, 그림자를 세우고 색인 한 줄을 남긴다. 돌려주는 값: 높이 근거별 수, 건너뛴 건물 수."""
    heights = {'height': 0, 'floors': 0, 'symbolic': 0}
    skipped = 0
    for k, b in enumerate(m['buildings']):
        n = len(b['footprint'])
        _, _, fx, fz = points.take(n)
        ring = clean(list(zip(fx.tolist(), fz.tolist())))
        if len(ring) < 3 or abs(area2(ring)) < 2.0: skipped += 1; continue
        H, h, source, floors = building_height(b)
        heights[source] += 1
        code_use = use_code(b['categoryCode'])
        r = rnd4(b['buildingStableId'])
        top, wall = roof_and_wall(code_use, r)
        prism(mesh, ring, 0.0, h, top, wall, H, len(index_buildings), shop=code_use in SHOP_CODES)
        cast_shadow(shadow, ring, h)
        index_buildings.append({'id': b['buildingStableId'], 'fill': False, 'kind': 'al-d010:' + code_use, 'height': round(h, 2),
                                'heightSource': source, 'xz': [round(v, 2) for q in ring for v in q],
                                'road': '', 'number': '', 'postcode': '', 'dong': m['displayName'].split()[-1],
                                'district': m['displayName'].split()[1], 'city': m['displayName'].split()[0], 'name': '',
                                'region': region, 'floors': int(floors or 0), 'area': round(float(b['buildingAreaSquareMeters'] or 0), 1)})
        all_shapes.append((ring, code_use, region, len(index_buildings) - 1))
    return heights, skipped


def road_segments(m, points):
    """도로 선분(노드링크). 폭은 자료에 없다."""
    segs = []
    for r in m['roads']:
        _, _, rx, rz = points.take(2)
        segs.append(((float(rx[0]), float(rz[0])), (float(rx[1]), float(rz[1]))))
    return segs


def road_links(m):
    """선분마다 그 선분이 든 링크의 이름. 'nodelink:link:1060008502:hjd:…' → '1060008502'. 모양이 다르면 이름 전체를 쓴다."""
    def link(name):
        parts = name.split(':')
        return parts[2] if len(parts) > 2 and parts[0] == 'nodelink' and parts[1] == 'link' else name
    return [link(r['roadStableId']) for r in m['roads']]


def bake_roads(mesh, kerb, road, alley):
    """골목 조각 위에 연석 조각, 그 위에 차도 조각. 큰길 조각은 fit_roads가, 골목 조각은 alley_pieces가 낸 것이다."""
    road_layer(mesh, alley, 0.02, C_ALLEY)
    road_layer(mesh, kerb, 0.03, C_KERB)
    road_layer(mesh, road, 0.06, C_ROAD)


# ── 녹지 (r221 계1, 2026-11-15) ────────────────────────────────────────────
GREEN_Y = 0.015                      # 바닥(0.01)과 골목(0.02) 사이
GREEN_STEP = 2.5                     # 테두리를 이 간격(m)으로 잘게 나눈다
GREEN_PAD = 5.0                      # 동 범위 밖으로 이만큼까지 녹지 테두리 점을 쓴다
GREEN_CLASSES = {'H': (1, 0x234434, '산림지'), 'E': (2, 0x2B4E3C, '조경녹지'), 'F': (3, 0x1D3E57, '하천'), 'G': (4, 0x3B4632, '경작지')}   # 색은 표현값
GREEN_SOURCE = '서울특별시 생태현황도(비오톱유형/평가도) 2025년 기준, 서울 열린데이터광장 OA-21145, 공공누리 제1유형(출처 표시)'


def tm5174_to_wgs(x, y):
    """EPSG:5174(Bessel 1841, 수정 중부원점 127.0028902777778, 가산 200000 · 500000) → WGS84 위경도(도). 넘파이 배열."""
    a, f = 6377397.155, 1 / 299.1528128
    e2 = 2 * f - f * f; ep2 = e2 / (1 - e2)
    lat0, lon0, fe, fn = math.radians(38.0), math.radians(127.0028902777778), 200000.0, 500000.0
    def meridian(p):
        e4, e6 = e2 * e2, e2 ** 3
        return a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * p - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * np.sin(2 * p)
                    + (15 * e4 / 256 + 45 * e6 / 1024) * np.sin(4 * p) - (35 * e6 / 3072) * np.sin(6 * p))
    e1 = (1 - math.sqrt(1 - e2)) / (1 + math.sqrt(1 - e2))
    mu = (meridian(lat0) + (y - fn)) / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 ** 3 / 256))
    p1 = (mu + (3 * e1 / 2 - 27 * e1 ** 3 / 32) * np.sin(2 * mu) + (21 * e1 ** 2 / 16 - 55 * e1 ** 4 / 32) * np.sin(4 * mu)
          + (151 * e1 ** 3 / 96) * np.sin(6 * mu) + (1097 * e1 ** 4 / 512) * np.sin(8 * mu))
    s, c, t = np.sin(p1), np.cos(p1), np.tan(p1)
    C1 = ep2 * c * c; T1 = t * t; N1 = a / np.sqrt(1 - e2 * s * s); R1 = a * (1 - e2) / (1 - e2 * s * s) ** 1.5
    D = (x - fe) / N1
    lat = p1 - (N1 * t / R1) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ep2) * D ** 4 / 24
                                + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ep2 - 3 * C1 * C1) * D ** 6 / 720)
    lon = lon0 + (D - (1 + 2 * T1 + C1) * D ** 3 / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ep2 + 24 * T1 * T1) * D ** 5 / 120) / c
    # Bessel → WGS84 (EPSG:5174 towgs84 −115.80, 474.99, 674.11, 1.16″, −2.31″, −1.63″, 6.43 ppm, 위치 벡터)
    tx, ty, tz = -115.80, 474.99, 674.11
    rx, ry, rz = [math.radians(v / 3600) for v in (1.16, -2.31, -1.63)]; k = 6.43e-6
    sl = np.sin(lat); N = a / np.sqrt(1 - e2 * sl * sl)
    X = N * np.cos(lat) * np.cos(lon); Y = N * np.cos(lat) * np.sin(lon); Z = N * (1 - e2) * sl
    X2 = tx + (1 + k) * (X - rz * Y + ry * Z); Y2 = ty + (1 + k) * (rz * X + Y - rx * Z); Z2 = tz + (1 + k) * (-ry * X + rx * Y + Z)
    lon2 = np.arctan2(Y2, X2); p = np.hypot(X2, Y2); lat2 = np.arctan2(Z2, p * (1 - E2))
    for _ in range(5):
        N2 = A / np.sqrt(1 - E2 * np.sin(lat2) ** 2); lat2 = np.arctan2(Z2 + E2 * N2 * np.sin(lat2), p)
    return np.degrees(lat2), np.degrees(lon2)


def load_greens(zip_path, box):
    """zip 안의 shp · dbf에서 네 갈래 녹지 폴리곤을 읽어 게임 미터로 옮긴다. box(서 · 남 · 동 · 북 m)와 겹치는 것만."""
    import zipfile
    z = zipfile.ZipFile(zip_path)
    shp_name = [n for n in z.namelist() if n.lower().endswith('.shp')]
    dbf_name = [n for n in z.namelist() if n.lower().endswith('.dbf')]
    check('생태현황도 zip에 shp · dbf가 하나씩', len(shp_name) == 1 and len(dbf_name) == 1, shp_name + dbf_name)
    shp, dbf = z.read(shp_name[0]), z.read(dbf_name[0])
    count, head_len, rec_len = struct.unpack('<IHH', dbf[4:12])
    code_len = dbf[32 + 16]                                         # 첫 칸(비오톱 코드)의 길이
    codes, boxes, bodies = [], [], []
    pos = 100
    while pos < len(shp):
        _, clen = struct.unpack('>ii', shp[pos:pos + 8]); body = shp[pos + 8: pos + 8 + clen * 2]; pos += 8 + clen * 2
        i = len(codes)
        codes.append(dbf[head_len + i * rec_len + 1: head_len + i * rec_len + 1 + code_len].decode('cp949', 'replace').strip())
        kind = struct.unpack('<i', body[:4])[0]
        boxes.append(struct.unpack('<4d', body[4:36]) if kind == 5 else (0.0, 0.0, 0.0, 0.0))
        bodies.append(body if kind == 5 else None)
    check('생태현황도 shp · dbf 수가 같음', len(codes) == count, [len(codes), count])
    b = np.array(boxes)
    la, lo = tm5174_to_wgs(np.concatenate([b[:, 0], b[:, 2]]), np.concatenate([b[:, 1], b[:, 3]]))
    gx, gz = (lo - LON0) * M_LON, (la - LAT0) * M_LAT
    n = len(b)
    gx0, gx1 = np.minimum(gx[:n], gx[n:]) - GREEN_PAD, np.maximum(gx[:n], gx[n:]) + GREEN_PAD
    gz0, gz1 = np.minimum(gz[:n], gz[n:]) - GREEN_PAD, np.maximum(gz[:n], gz[n:]) + GREEN_PAD
    greens = []
    for i in np.where((gx1 > box[0]) & (gx0 < box[2]) & (gz1 > box[1]) & (gz0 < box[3]))[0]:
        cls = codes[i][:1]
        if cls not in GREEN_CLASSES or bodies[i] is None: continue
        body = bodies[i]
        parts_n, pts_n = struct.unpack('<ii', body[36:44])
        parts = list(struct.unpack('<%di' % parts_n, body[44:44 + 4 * parts_n])) + [pts_n]
        xy = np.frombuffer(body[44 + 4 * parts_n: 44 + 4 * parts_n + 16 * pts_n], dtype='<f8').reshape(-1, 2)
        la, lo = tm5174_to_wgs(xy[:, 0], xy[:, 1])
        g = np.stack([(lo - LON0) * M_LON, (la - LAT0) * M_LAT], axis=1)
        rings = [g[parts[k]:parts[k + 1]] for k in range(parts_n) if parts[k + 1] - parts[k] >= 4]
        if not rings: continue
        allp = np.concatenate(rings)
        greens.append({'cls': cls, 'code': codes[i], 'rings': rings, 'box': (*allp.min(0), *allp.max(0))})
    return greens, len(codes)


def densify(ring, step):
    """닫힌 고리의 변마다 step 간격으로 점을 넣는다."""
    p = np.asarray(ring, dtype=float)
    q = np.roll(p, -1, axis=0)
    out = []
    for a, b_ in zip(p, q):
        k = max(1, int(math.ceil(math.hypot(*(b_ - a)) / step)))
        t = np.arange(k)[:, None] / k
        out.append(a + (b_ - a) * t)
    return np.concatenate(out)


def bake_greens(mesh, boundary, greens):
    """동 경계 안의 녹지를 한 겹으로 깐다. 돌려주는 값: 갈래별 넓이(삼각형), 갈래별 넓이(1 m 격자로 센 값), 정점 수, 삼각형 수."""
    from scipy.spatial import Delaunay
    from skimage.measure import points_in_poly
    from skimage.draw import polygon as fill_polygon
    ring = np.array(boundary, dtype=float)
    x0, z0 = ring.min(0); x1, z1 = ring.max(0)
    names = {cid: name for cid, _, name in GREEN_CLASSES.values()}
    made, counted = {name: 0.0 for name in names.values()}, {name: 0.0 for name in names.values()}
    near = [g for g in greens if g['box'][0] < x1 + GREEN_PAD and g['box'][2] > x0 - GREEN_PAD and g['box'][1] < z1 + GREEN_PAD and g['box'][3] > z0 - GREEN_PAD]
    if not near: return made, counted, 0, 0
    pts = [densify(ring, GREEN_STEP)]
    for g in near:
        for r in g['rings']:
            d = densify(r, GREEN_STEP)
            pts.append(d[(d[:, 0] > x0 - GREEN_PAD) & (d[:, 0] < x1 + GREEN_PAD) & (d[:, 1] > z0 - GREEN_PAD) & (d[:, 1] < z1 + GREEN_PAD)])
    P = np.unique(np.round(np.concatenate(pts), 2), axis=0)
    tri = Delaunay(P).simplices
    T = P[tri]
    C = T.mean(axis=1)
    area = 0.5 * np.abs((T[:, 1, 0] - T[:, 0, 0]) * (T[:, 2, 1] - T[:, 0, 1]) - (T[:, 2, 0] - T[:, 0, 0]) * (T[:, 1, 1] - T[:, 0, 1]))
    inside = points_in_poly(C, ring)
    cls = np.zeros(len(C), dtype=int)
    # 1 m 격자로 넓이를 따로 센다(확인용)
    W, H = int(math.ceil(x1 - x0)) + 1, int(math.ceil(z1 - z0)) + 1
    rr, cc = fill_polygon(ring[:, 1] - z0, ring[:, 0] - x0, (H, W))
    dong_mask = np.zeros((H, W), dtype=bool); dong_mask[rr, cc] = True
    for g in near:
        b = g['box']
        cid = GREEN_CLASSES[g['cls']][0]
        sel = np.where(inside & (cls == 0) & (C[:, 0] >= b[0]) & (C[:, 0] <= b[2]) & (C[:, 1] >= b[1]) & (C[:, 1] <= b[3]))[0]
        hit = np.zeros(len(sel), dtype=bool)
        mask = np.zeros((H, W), dtype=bool)
        for r in g['rings']:
            if len(sel): hit ^= points_in_poly(C[sel], r)
            rr, cc = fill_polygon(r[:, 1] - z0, r[:, 0] - x0, (H, W))
            part = np.zeros((H, W), dtype=bool); part[rr, cc] = True
            mask ^= part
        cls[sel[hit]] = cid
        counted[names[cid]] += float((mask & dong_mask).sum())
    vertices = triangles = 0
    for cid, colour, name in GREEN_CLASSES.values():
        keep = (cls == cid) & (area > 1e-3)
        if not keep.any(): continue
        used, inv = np.unique(tri[keep], return_inverse=True)
        base = len(mesh.pos)
        c = rgb(colour)
        for x, z in P[used]: mesh.vert(float(x), GREEN_Y, float(z), c, uv=(float(cid), 0.0))
        for t in inv.reshape(-1, 3) + base: mesh.tri.append((int(t[0]), int(t[1]), int(t[2])))
        made[name] = float(area[keep].sum())
        vertices += len(used); triangles += int(keep.sum())
    return made, counted, vertices, triangles


# ── 보도 · 횡단보도 (거리 풍경 보1 · 거1, 2026-11-15) ────────────────────────
ST_CELL = 0.25                       # 격자(m)
ST_REACH = 6.0                       # 차도에서 이만큼까지를 보도로 본다(건물 · 녹지가 없을 때)
ST_MIN_AREA = 4.0                    # 이보다 작은 보도 조각은 버린다(m²)
ST_STEP = 1.5                        # 보도 테두리를 이 간격으로 나눈다
ST_KERB_LINE = 0.3                   # 연석선 폭
CW_MARGIN = 0.35                     # 횡단보도 끝에서 남기는 거리
TAG_SIDEWALK, TAG_CROSSWALK, TAG_TACTILE, TAG_KERBLINE, TAG_FURNITURE = 5.0, 6.0, 7.0, 8.0, 9.0
Y_SIDEWALK, Y_FURNITURE, Y_KERBLINE, Y_TACTILE, Y_CROSSWALK = 0.012, 0.031, 0.032, 0.034, 0.065
CROSSINGS_SOURCE = '서울특별시 교차로 및 횡단보도 시설·위치정보(OA-23081), 서울 열린데이터광장, 공공누리 제1유형(출처 표시)'


def grs80_tm_to_wgs(x, y, lon0=127.0, lat0=38.0, fe=200000.0, fn=600000.0):
    """EPSG:5186(GRS80, 중부 127°, 가산 200000 · 600000) → 위경도(도). GRS80과 WGS84는 같게 본다."""
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
    C1 = ep2 * c * c; T1 = t * t; N1 = a / np.sqrt(1 - e2 * s * s); R1 = a * (1 - e2) / (1 - e2 * s * s) ** 1.5
    D = (x - fe) / N1
    lat = p1 - (N1 * t / R1) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ep2) * D ** 4 / 24
                                + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ep2 - 3 * C1 * C1) * D ** 6 / 720)
    lon = math.radians(lon0) + (D - (1 + 2 * T1 + C1) * D ** 3 / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ep2 + 24 * T1 * T1) * D ** 5 / 120) / c
    return np.degrees(lat), np.degrees(lon)


def load_crossings(xlsx_path, box):
    """xlsx의 횡단보도 지점(자치구 무관)을 게임 미터로 옮겨 box 안의 것만."""
    import openpyxl
    rows = list(openpyxl.load_workbook(xlsx_path, read_only=True).worksheets[0].iter_rows(values_only=True))
    def number(v):
        try: return float(v)
        except (TypeError, ValueError): return None
    head, rows = rows[0], [r for r in rows[1:] if number(r[7]) is not None and number(r[8]) is not None]
    check('횡단보도 자료의 칸이 예상과 같음', list(head[:9]) == ['연번', '자치구', '횡단보도관리번호', '교차로관리번호', '횡단보도종류', '보행등유무', '교차로명', 'X좌표', 'Y좌표'], head)
    la, lo = grs80_tm_to_wgs(np.array([float(r[7]) for r in rows]), np.array([float(r[8]) for r in rows]))
    gx, gz = (lo - LON0) * M_LON, (la - LAT0) * M_LAT
    keep = (gx > box[0]) & (gx < box[2]) & (gz > box[1]) & (gz < box[3])
    return [{'x': float(gx[i]), 'z': float(gz[i]), 'kind': rows[i][4], 'ped': rows[i][5] == '유', 'cross': rows[i][3], 'id': rows[i][2]}
            for i in np.where(keep)[0]], len(rows)


def street_layers(code, boundary, road_pieces, kerb_pieces, shapes, greens, crossings):
    """한 동의 보도 · 시설물대 · 연석선 · 점자블록 · 횡단보도 조각을 계산한다. 메시에 붙이는 것은 bake_street가 한다."""
    from scipy import ndimage
    from scipy.spatial import Delaunay
    from skimage.draw import polygon as fill_polygon
    from skimage import measure
    ring = np.array(boundary, dtype=float)
    x0, z0 = ring.min(0) - 8.0; x1, z1 = ring.max(0) + 8.0
    W, H = int(math.ceil((x1 - x0) / ST_CELL)) + 1, int(math.ceil((z1 - z0) / ST_CELL)) + 1
    def paint(mask, poly):
        p = np.asarray(poly, dtype=float)
        if p[:, 0].max() < x0 or p[:, 0].min() > x1 or p[:, 1].max() < z0 or p[:, 1].min() > z1: return
        rr, cc = fill_polygon((p[:, 1] - z0) / ST_CELL, (p[:, 0] - x0) / ST_CELL, (H, W)); mask[rr, cc] = True
    road = np.zeros((H, W), bool); bld = np.zeros((H, W), bool); dong = np.zeros((H, W), bool); green = np.zeros((H, W), bool)
    for piece in road_pieces: paint(road, piece)
    for shape in shapes: paint(bld, shape)
    paint(dong, ring)
    for g in greens or []:
        if g['box'][2] < x0 or g['box'][0] > x1 or g['box'][3] < z0 or g['box'][1] > z1: continue
        m = np.zeros((H, W), bool)
        for r in g['rings']:
            part = np.zeros((H, W), bool); paint(part, r); m ^= part
        green |= m
    d_road = ndimage.distance_transform_edt(~road) * ST_CELL
    side = (~road) & (~bld) & (~green) & dong & (d_road <= ST_REACH)
    lab, n = ndimage.label(side)
    if n:
        sizes = ndimage.sum(side, lab, index=np.arange(1, n + 1)) * ST_CELL * ST_CELL
        side &= np.isin(lab, np.where(sizes >= ST_MIN_AREA)[0] + 1)
    def cell(x, z):
        c, r = int((x - x0) / ST_CELL), int((z - z0) / ST_CELL)
        return (r, c) if 0 <= r < H and 0 <= c < W else None
    def at(mask, x, z):
        rc = cell(x, z); return rc is not None and bool(mask[rc])
    # 보도 면: 테두리 점(나눔) + 들로네, 무게중심이 보도 칸인 삼각형만
    pts = []
    for cc_ in measure.find_contours(np.pad(side, 1).astype(float), 0.5):
        cc_ = measure.approximate_polygon(cc_ - 1, 0.4)
        w = np.stack([x0 + (cc_[:, 1] + 0.5) * ST_CELL, z0 + (cc_[:, 0] + 0.5) * ST_CELL], 1)
        if len(w) >= 3: pts.append(densify(w, ST_STEP))
    tris_xz, sidewalk_area = [], 0.0
    if pts:
        P = np.unique(np.round(np.concatenate(pts), 2), axis=0)
        if len(P) >= 3:
            T = Delaunay(P).simplices; C = P[T].mean(axis=1)
            rc = np.clip(((C[:, 1] - z0) / ST_CELL).astype(int), 0, H - 1), np.clip(((C[:, 0] - x0) / ST_CELL).astype(int), 0, W - 1)
            T = T[side[rc]]
            Q = P[T]
            area = 0.5 * np.abs((Q[:, 1, 0] - Q[:, 0, 0]) * (Q[:, 2, 1] - Q[:, 0, 1]) - (Q[:, 2, 0] - Q[:, 0, 0]) * (Q[:, 1, 1] - Q[:, 0, 1]))
            keep = area > 1e-3
            tris_xz = (P, T[keep]); sidewalk_area = float(area[keep].sum())
    # 연석선: 차도 가장자리를 따라 바깥으로 ST_KERB_LINE, 바깥이 보도일 때만
    kerb_quads = []
    for cc_ in measure.find_contours(np.pad(road, 1).astype(float), 0.5):
        cc_ = measure.approximate_polygon(cc_ - 1, 0.15)
        w = np.stack([x0 + (cc_[:, 1] + 0.5) * ST_CELL, z0 + (cc_[:, 0] + 0.5) * ST_CELL], 1)
        for a, b_ in zip(w[:-1], w[1:]):
            L = float(np.hypot(*(b_ - a)))
            if L < 0.2: continue
            t = (b_ - a) / L; nrm = np.array([-t[1], t[0]])
            mid = (a + b_) / 2
            if at(road, *(mid + nrm * 0.4)): nrm = -nrm
            if not at(side, *(mid + nrm * 0.4)): continue
            kerb_quads.append([a, b_, b_ + nrm * ST_KERB_LINE, a + nrm * ST_KERB_LINE])
    # 횡단보도: 동 안 차도의 가장 짧은 현
    road_in = road & dong
    stripes, tactile, placed, skipped = [], [], [], collections.Counter()
    for cw in crossings:
        if not at(dong, cw['x'], cw['z']): continue
        if cw['kind'] == '대각선횡단보도': skipped['대각선'] += 1; continue
        if not at(road_in, cw['x'], cw['z']):
            near = d_road[cell(cw['x'], cw['z'])] if cell(cw['x'], cw['z']) else 99
            skipped['차도 밖(골목 입구 등)' if near < 6 else '차도에서 멂'] += 1; continue
        best = None
        for k in range(0, 180, 2):
            ang = math.radians(k); dx, dz = math.cos(ang), math.sin(ang)
            a = b_ = 0.0
            while at(road_in, cw['x'] + dx * (a + ST_CELL), cw['z'] + dz * (a + ST_CELL)) and a < 60: a += ST_CELL
            while at(road_in, cw['x'] - dx * (b_ + ST_CELL), cw['z'] - dz * (b_ + ST_CELL)) and b_ < 60: b_ += ST_CELL
            if best is None or a + b_ < best[0]: best = (a + b_, dx, dz, a, b_)
        L, dx, dz, a, b_ = best
        if L < 3 or L > 40: skipped['길이 밖'] += 1; continue
        width = 5.0 if L > 14 else 4.0
        e1 = np.array([cw['x'] + dx * a, cw['z'] + dz * a]); e2 = np.array([cw['x'] - dx * b_, cw['z'] - dz * b_])
        u = (e1 - e2) / max(L, 1e-6); v = np.array([-u[1], u[0]]) * width / 2
        s = CW_MARGIN
        while s + 0.5 <= L - CW_MARGIN + 1e-6:
            p0, p1 = e2 + u * s, e2 + u * (s + 0.5)
            stripes.append([p0 + v, p1 + v, p1 - v, p0 - v]); s += 1.0
        for end, sgn in ((e1, 1), (e2, -1)):
            c0 = end + u * sgn * 0.3; c1 = end + u * sgn * 0.9
            if at(side, *((c0 + c1) / 2)):
                tactile.append([c0 + v, c1 + v, c1 - v, c0 - v])
        placed.append({'id': cw['id'], 'cross': cw['cross'], 'kind': cw['kind'], 'ped': cw['ped'], 'c': [round(cw['x'], 2), round(cw['z'], 2)],
                       'e1': [round(float(e1[0]), 2), round(float(e1[1]), 2)], 'e2': [round(float(e2[0]), 2), round(float(e2[1]), 2)], 'width': width})
    # 시설물대: 이 동의 연석 띠 조각 그대로
    return {'sidewalk': tris_xz, 'sidewalkArea': sidewalk_area, 'kerbLine': kerb_quads, 'furniture': kerb_pieces, 'stripes': stripes,
            'tactile': tactile, 'placed': placed, 'skipped': dict(skipped), 'grid': (x0, z0, H, W), 'masks': (road_in, side, dong)}


def bake_street(mesh, st):
    """street_layers의 결과를 메시 끝에 붙인다. 정점 색은 그 아래에 보이던 색이라 멀리서는 전과 같다."""
    start_v, start_t = len(mesh.pos), len(mesh.tri)
    ground, kerb_c, road_c = rgb(C_DONG), rgb(C_KERB), rgb(C_ROAD)
    if st['sidewalk']:
        P, T = st['sidewalk']
        used, inv = np.unique(T, return_inverse=True)
        base = len(mesh.pos)
        for x, z in P[used]: mesh.vert(float(x), Y_SIDEWALK, float(z), ground, uv=(TAG_SIDEWALK, 0.0))
        for t in inv.reshape(-1, 3) + base: mesh.tri.append((int(t[0]), int(t[1]), int(t[2])))
    def quads(items, y, colour, tag):
        for q in items:
            i = [mesh.vert(float(p[0]), y, float(p[1]), colour, uv=(tag, 0.0)) for p in q]
            for k in range(1, len(i) - 1): mesh.tri.append((i[0], i[k], i[k + 1]))
    quads(st['furniture'], Y_FURNITURE, kerb_c, TAG_FURNITURE)
    quads(st['kerbLine'], Y_KERBLINE, kerb_c, TAG_KERBLINE)
    quads(st['tactile'], Y_TACTILE, kerb_c, TAG_TACTILE)
    quads(st['stripes'], Y_CROSSWALK, road_c, TAG_CROSSWALK)
    return len(mesh.pos) - start_v, len(mesh.tri) - start_t


def write_module(code, out_path, bounds, building_count, road_count, mesh, shadow):
    """메시를 HDIO v2로 쓴다. 돌려주는 값: 정점 수, 삼각형 수."""
    west, south, east, north = bounds
    pos = np.array(mesh.pos, dtype='<f4'); col = np.array(mesh.col, dtype='u1'); uv = np.array(mesh.uv, dtype='<f4'); uv2 = np.array(mesh.uv2, dtype='<f4')
    tri = np.array(mesh.tri, dtype='<i4'); spos = np.array(shadow.pos, dtype='<f4'); stri = np.array(shadow.tri, dtype='<i4')
    check(f'{code} 메시 색인이 범위 안', tri.min() >= 0 and tri.max() < len(pos) and stri.max() < len(spos))
    check(f'{code} 메시 값이 유한', np.isfinite(pos).all() and np.isfinite(spos).all() and np.isfinite(uv).all())
    check(f'{code} 정점 수가 읽기 한도(400만) 안', len(pos) < 4000000 and tri.size < 12000000, len(pos))
    wall_v, rim_v = uv2[:, 0] > 0.5, uv2[:, 1] < -0.5
    shop_v = wall_v & (uv2[:, 1] >= SHOP_SEED - 0.5)
    seed_v = uv2[wall_v, 1] - SHOP_SEED * (uv2[wall_v, 1] >= SHOP_SEED - 0.5)
    check(f'{code} 지붕 테두리가 벽마다 하나', int(rim_v.sum()) == int(wall_v.sum()), [int(rim_v.sum()), int(wall_v.sum())])
    check(f'{code} 벽의 창 씨앗이 전과 같은 범위(0~996)', bool(((seed_v >= 0) & (seed_v < 997)).all()), int(shop_v.sum()) // 4)
    with open(out_path, 'wb') as f:
        f.write(b'HDIO'); f.write(struct.pack('<i', 2))
        f.write(struct.pack('<6d', LAT0, LON0, west, south, east, north))
        f.write(struct.pack('<2i', building_count, road_count))
        f.write(struct.pack('<i', len(pos))); f.write(pos.tobytes()); f.write(col.tobytes()); f.write(uv.tobytes()); f.write(uv2.tobytes())
        f.write(struct.pack('<i', tri.size)); f.write(tri.tobytes())
        f.write(struct.pack('<i', len(spos))); f.write(spos.tobytes())
        f.write(struct.pack('<i', stri.size)); f.write(stri.tobytes())
        f.write(struct.pack('<i', 0))                                  # 예시 채움 건물 없음
        f.write(struct.pack('<i', 0)); f.write(struct.pack('<i', 0))   # 채움 메시: 정점 0, 색인 0
        f.write(struct.pack('<i', 0)); f.write(struct.pack('<i', 0))   # 채움 그림자: 정점 0, 색인 0
    return int(len(pos)), int(len(tri))


def prepare_module(code):
    """한 동의 앞 절반: 확인 → 좌표 → 바닥 → 건물. 도로는 선분만 꺼내 둔다."""
    global union, max_err
    region = 'region:kr:hjd:' + code
    entry = by_id.get(region)
    check(f'{code} scope에 있음', entry is not None)
    path = os.path.join(MODULES, code + '.json')
    file_sha = sha256(path)
    m = json.load(open(path, encoding='utf-8'))
    verify_module(code, entry, m, file_sha)

    points = Points(m)                       # 모든 점을 한 번에 위경도로 되돌린다
    max_err = max(max_err, points.err)
    blat, blon, bx, bz = points.take(len(m['boundary']))
    boundary = clean(list(zip(bx.tolist(), bz.tolist())))
    west, east, south, north = float(blon.min()), float(blon.max()), float(blat.min()), float(blat.max())
    union = [min(union[0], west), min(union[1], south), max(union[2], east), max(union[3], north)]

    mesh, shadow = MeshOut(), MeshOut()
    bake_ground(mesh, boundary)
    heights, skipped = bake_buildings(mesh, shadow, m, points, region)
    segs = road_segments(m, points)
    check(f'{code} 모든 점을 썼음', points.used_all(), points.cursor)
    return {'code': code, 'region': region, 'm': m, 'file_sha': file_sha, 'mesh': mesh, 'shadow': shadow, 'segs': segs, 'links': road_links(m), 'boundary': boundary,
            'heights': heights, 'skipped': skipped, 'bounds': (west, south, east, north), 'blat': blat, 'blon': blon}


def finish_module(prepared, kerb, road, alley, street=None):
    """한 동의 뒤 절반: 도로 → 쓰기. 목록에 넣을 한 줄을 돌려준다."""
    code, region, m, file_sha, mesh, shadow, segs = (prepared[k] for k in ('code', 'region', 'm', 'file_sha', 'mesh', 'shadow', 'segs'))
    heights, skipped, blat, blon = (prepared[k] for k in ('heights', 'skipped', 'blat', 'blon'))
    west, south, east, north = prepared['bounds']
    bake_roads(mesh, kerb, road, alley)
    greens = None
    if GREENS is not None:                          # 녹지 (계1): 메시의 맨 끝에 붙인다
        made, counted, green_v, green_t = bake_greens(mesh, prepared['boundary'], GREENS)
        for name in made:
            check(f'{code} 녹지 {name} 넓이가 1 m 격자로 센 값과 2 % · 300 m² 안', abs(made[name] - counted[name]) <= 0.02 * counted[name] + 300.0,
                  [round(made[name]), round(counted[name])])
        greens = {name: round(v) for name, v in made.items()}
        greens_report = {'areaSquareMetres': greens, 'areaCountedOnGrid': {name: round(v) for name, v in counted.items()}, 'vertices': green_v, 'triangles': green_t}
    street_report = None
    if street is not None:                          # 보도 · 횡단보도 (보1 · 거1): 녹지 뒤, 메시의 맨 끝
        sv, stt = bake_street(mesh, street)
        street_report = {'sidewalkSquareMetres': round(street['sidewalkArea']), 'kerbLineSegments': len(street['kerbLine']),
                         'furniturePieces': len(street['furniture']), 'crosswalks': len(street['placed']), 'stripes': len(street['stripes']),
                         'tactile': len(street['tactile']), 'skipped': street['skipped'], 'vertices': sv, 'triangles': stt}
        print(code, 'street', street_report)

    resource = 'hongdal-dong-' + code
    out_path = os.path.join(OUT, resource + '.bytes')
    vertices, triangles = write_module(code, out_path, (west, south, east, north), len(m['buildings']) - skipped, len(segs), mesh, shadow)
    simple = rdp([(float(a), float(o)) for a, o in zip(blat, blon)], 0.5 / M_LAT)
    name = m['displayName'].split()[-1]
    module = {'regionId': region, 'code': code, 'name': name, 'district': m['displayName'].split()[1], 'resource': resource,
              'bytes': os.path.getsize(out_path), 'sha256': sha256(out_path), 'moduleSha256': file_sha, 'moduleContentHash': m['contentHashSha256'],
              'buildings': len(m['buildings']) - skipped, 'skippedBuildings': skipped, 'roads': len(segs),
              'heightFromSource': heights['height'], 'heightFromFloors': heights['floors'], 'heightSymbolic': heights['symbolic'],
              'west': west, 'south': south, 'east': east, 'north': north,
              'centerLatitude': (south + north) / 2, 'centerLongitude': (west + east) / 2,
              'boundary': [round(v, 7) for q in simple for v in q], 'vertices': vertices, 'triangles': triangles}
    if greens is not None: module['greens'] = greens
    if street_report is not None: module['street'] = {k: street_report[k] for k in ('sidewalkSquareMetres', 'crosswalks', 'tactile')}
    report['modules'].append({k: v for k, v in module.items() if k != 'boundary'} | {'boundaryPoints': len(simple), 'diagnosticCodes': m['diagnosticCodes'],
                                                                                    'roadPieces': len(kerb) + len(road), 'alleyPieces': len(alley)}
                              | ({'greens': greens_report} if greens is not None else {})
                              | ({'street': street_report | {'placed': street['placed']}} if street_report is not None else {}))
    print(code, name, 'buildings', module['buildings'], 'roads', len(segs), 'road pieces', len(kerb) + len(road), 'alley pieces', len(alley), 'verts', vertices, 'bytes', module['bytes'], heights)
    return module


prepared = [prepare_module(code) for code in CODES]
GREENS = None
CROSSING_POINTS = None
if BIOTOPE:                                         # 녹지 (계1): 굽는 동들을 덮는 범위의 녹지만 읽는다
    allb = np.concatenate([np.array(p['boundary']) for p in prepared])
    GREENS, biotope_records = load_greens(BIOTOPE, (*(allb.min(0) - GREEN_PAD), *(allb.max(0) + GREEN_PAD)))
    report['greens'] = {'source': GREEN_SOURCE, 'zip': os.path.basename(BIOTOPE), 'zipSha256': sha256(BIOTOPE), 'records': biotope_records,
                        'polygonsNear': len(GREENS), 'byClass': {GREEN_CLASSES[k][2]: sum(1 for g in GREENS if g['cls'] == k) for k in GREEN_CLASSES},
                        'stepMetres': GREEN_STEP, 'y': GREEN_Y, 'colours': {v[2]: '#%06X' % v[1] for v in GREEN_CLASSES.values()},
                        'note': '범위는 자료, 색은 표현. uv.x = 갈래 번호(1 산림지 · 2 조경녹지 · 3 하천 · 4 경작지)'}
    print('greens', report['greens']['polygonsNear'], report['greens']['byClass'])
if CROSSINGS:                                       # 보도 · 횡단보도 (보1 · 거1)
    allb = np.concatenate([np.array(p['boundary']) for p in prepared])
    CROSSING_POINTS, crossing_rows = load_crossings(CROSSINGS, (*(allb.min(0) - 10), *(allb.max(0) + 10)))
    report['street'] = {'source': CROSSINGS_SOURCE, 'file': os.path.basename(CROSSINGS), 'fileSha256': sha256(CROSSINGS), 'rows': crossing_rows,
                        'pointsNear': len(CROSSING_POINTS), 'cellMetres': ST_CELL, 'reachMetres': ST_REACH, 'kerbLineMetres': ST_KERB_LINE,
                        'layers': {'sidewalk': [Y_SIDEWALK, TAG_SIDEWALK], 'furniture': [Y_FURNITURE, TAG_FURNITURE], 'kerbLine': [Y_KERBLINE, TAG_KERBLINE],
                                   'tactile': [Y_TACTILE, TAG_TACTILE], 'crosswalk': [Y_CROSSWALK, TAG_CROSSWALK]},
                        'note': '자리는 자료(OA-23081), 보도 범위는 계산, 줄무늬 · 점자블록 · 보도 블록 모양은 표현. 멀리서는 전과 같은 색(가까이에서만 셰이더가 칠한다)'}
    print('crossings', len(CROSSING_POINTS), 'of', crossing_rows)
obstacles = Obstacles([shape[0] for shape in all_shapes])       # 굽는 모든 동의 건물
fitted, road_stats, road_tracks = fit_roads([p['segs'] for p in prepared], [p['links'] for p in prepared], obstacles)
alley_stats = {'lines': 0, 'pieces': 0, 'dropped': 0, 'metres': 0.0}
alley_segments = []
for p, (kerb, road) in zip(prepared, fitted):
    covered = road_overlaps(kerb + road, obstacles)
    close = road_overlaps(kerb + road, obstacles, ROAD_GAP - 0.01)
    check(f"{p['code']} 도로가 건물을 덮지 않음", covered == 0, covered)
    check(f"{p['code']} 도로가 건물 벽에서 {ROAD_GAP - 0.01:.2f} m 이상 떨어짐", close == 0, close)
    # 골목: 이 동의 경계 안에서, 모든 동의 건물과 큰길 조각을 피해 가운데 선을 찾는다
    lines = alley_lines(p['boundary'], [shape[0] for shape in all_shapes], [piece for pair in fitted for layer in pair for piece in layer])
    alley, alley_dropped, alley_length, alley_drawn = alley_pieces(lines, obstacles)
    alley_segments += alley_drawn
    check(f"{p['code']} 골목이 건물을 덮지 않고 벽에서 {ALLEY_GAP * 0.5:.1f} m 이상 떨어짐", road_overlaps(alley, obstacles, ALLEY_GAP * 0.5 - 0.01) == 0)
    alley_stats['lines'] += len(lines); alley_stats['pieces'] += len(alley); alley_stats['dropped'] += alley_dropped; alley_stats['metres'] += alley_length
    street = None
    if CROSSING_POINTS is not None:
        street = street_layers(p['code'], p['boundary'], [piece for pair in fitted for piece in pair[1]], kerb,
                               [shape[0] for shape in all_shapes], GREENS, CROSSING_POINTS)
        road_in, side_m, dong_m = street['masks']; sx0, sz0, sH, sW = street['grid']
        def inside_share(quads, mask):
            hit = tot = 0
            for q in quads:
                for t in np.linspace(0.1, 0.9, 5):
                    for s_ in (0.25, 0.75):
                        pnt = (q[0] * (1 - t) + q[1] * t) * (1 - s_) + (q[3] * (1 - t) + q[2] * t) * s_
                        r_, c_ = int((pnt[1] - sz0) / ST_CELL), int((pnt[0] - sx0) / ST_CELL)
                        tot += 1; hit += 1 if 0 <= r_ < sH and 0 <= c_ < sW and mask[r_, c_] else 0
            return hit / tot if tot else 1.0
        share_s = inside_share(street['stripes'], road_in); share_t = inside_share(street['tactile'], side_m | road_in)
        check(f"{p['code']} 횡단보도 줄이 차도 안(90 % 넘게)", share_s >= 0.9, round(share_s, 3))
        check(f"{p['code']} 점자블록이 보도 · 차도 칸 안(85 % 넘게)", share_t >= 0.85, round(share_t, 3))
        street.pop('masks')
    modules_out.append(finish_module(p, kerb, road, alley, street))
report['roads'] = {'stepMetres': ROAD_STEP, 'gapMetres': ROAD_GAP, 'minimumMetres': ROAD_MIN, 'easeMetres': ROAD_EASE, 'usualPercentile': ROAD_USUAL,
                   'mateReachMetres': MATE_REACH, 'blockedMetres': ROAD_BLOCKED, 'openShare': ROAD_OPEN} | {k: int(v) for k, v in road_stats.items()}
report['alleys'] = {'cellMetres': ALLEY_CELL, 'minimumClearMetres': ALLEY_MIN, 'nearMetres': ALLEY_NEAR, 'halfMetres': ALLEY_HALF, 'gapMetres': ALLEY_GAP,
                    'lines': alley_stats['lines'], 'pieces': alley_stats['pieces'], 'dropped': alley_stats['dropped'], 'metres': round(alley_stats['metres'], 1)}
print('roads', report['roads'])
print('alleys', report['alleys'])

# 길 그래프와 가상 출입구 (R1)
route_xy, route_edges, route_kinds, route_doors, route_stats = build_routes(road_tracks, alley_segments, [shape[0] for shape in all_shapes], obstacles)
routes_path = os.path.join(OUT, 'hongdal-dong-routes.bytes')
write_routes(routes_path, route_xy, route_edges, route_kinds, route_doors)
check('길 그래프: 가장 큰 덩어리가 길이의 90% 이상', route_stats['mainShare'] >= 0.9, route_stats['mainShare'])
check('길 그래프: 건물의 95% 이상에 가상 출입구가 있음', route_stats['buildingsWithDoor'] >= 0.95 * route_stats['buildings'], route_stats['buildingsWithDoor'])
check('길 그래프: 건물 수가 색인과 같음', len(route_doors) == len(index_buildings), len(route_doors))
check('길 그래프: 건물을 지나는 변이 없음', route_stats['edgesThroughBuildings'] == 0, route_stats['edgesThroughBuildings'])
report['routes'] = route_stats | {'bytes': os.path.getsize(routes_path), 'sha256': sha256(routes_path), 'label': '가상 출입구 · 공식 출입구 자료 아님 · 내부 검토용'}
print('routes', report['routes'])

check('ENU→위경도 되돌림 오차 1 mm 안', max_err < 1e-3, max_err)
report['inverseProjectionMaxErrorMetres'] = max_err

# ── 건물 역할 대응: 기존 역할표의 OSM 건물 → 같은 자리의 모듈 건물 ─────────────
osm = json.load(open(OSM_INDEX, encoding='utf-8'))
roles = json.load(open(ROLES, encoding='utf-8'))
o_lat, o_lon, o_mlon = osm['origin']['latitude'], osm['origin']['longitude'], osm['metresPerDegreeLongitude']
osm_by_id = {b['id']: b for b in osm['buildings']}
def inside(pt, ring):
    x, z = pt; c = False
    for i in range(len(ring)):
        a, b_ = ring[i], ring[i - 1]
        if (a[1] > z) != (b_[1] > z) and x < (b_[0] - a[0]) * (z - a[1]) / (b_[1] - a[1]) + a[0]: c = not c
    return c
def centroid(ring): return (sum(p[0] for p in ring) / len(ring), sum(p[1] for p in ring) / len(ring))
RESIDENTIAL = {'01000', '02000'}
role_map = []
for role in roles['roles']:
    src = osm_by_id.get(role['buildingId'])
    if src is None:
        role_map.append({'from': role['buildingId'], 'to': '', 'method': 'SourceBuildingMissing', 'distance': -1.0}); continue
    xz = src['xz']; ring = [(xz[i], xz[i + 1]) for i in range(0, len(xz), 2)]
    cx, cz = centroid(ring)
    lat, lon = o_lat + cz / M_LAT, o_lon + cx / o_mlon
    px, pz = game_xz(lat, lon)
    best = None
    for shape, use, region, idx in all_shapes:
        c = centroid(shape)
        d = 0.0 if inside((px, pz), shape) else math.hypot(c[0] - px, c[1] - pz)
        if d > 25.0: continue
        # 같은 자리를 우선하고, 가상 역할은 비주거 건물에 얹는다 (역할표 원칙)
        score = (0 if use not in RESIDENTIAL else 1, 0 if d == 0.0 else 1, d)
        if best is None or score < best[0]: best = (score, idx, d, use)
    if best is None:
        role_map.append({'from': role['buildingId'], 'to': '', 'method': 'NoModuleBuildingWithin25m', 'distance': -1.0}); continue
    _, idx, d, use = best
    method = ('Contains' if d == 0.0 else 'Nearest') + ('NonResidential' if use not in RESIDENTIAL else 'Residential')
    role_map.append({'from': role['buildingId'], 'to': index_buildings[idx]['id'], 'method': method, 'distance': round(d, 1)})
check('역할 4곳이 모두 모듈 건물에 대응', all(r['to'] for r in role_map), [r['method'] for r in role_map])
report['roleMap'] = role_map

index = {'schema': 'hongdal-building-index.v1', 'source': '국토교통부 GIS건물통합정보 AL_D010 (r2 행정동 모듈)', 'sourceDate': '2026-08-09',
         'sourceSha256': report['scopeContentHashSha256'], 'licence': '권리 미해결 · 내부 검토용 · 배포 아님',
         'origin': {'latitude': LAT0, 'longitude': LON0}, 'metresPerDegreeLongitude': M_LON, 'metresPerDegreeLatitude': M_LAT,
         'buildings': index_buildings}
index_path = os.path.join(OUT, 'hongdal-dong-buildings.json')
with open(index_path, 'w', encoding='utf-8', newline='\n') as f: json.dump(index, f, ensure_ascii=False, separators=(',', ':'))
check('색인 건물 id가 겹치지 않음', len({b['id'] for b in index_buildings}) == len(index_buildings))

# ── 띄워 보기 자리 (2026-11-13 사용자 요청: 동을 살짝 떼어 구분) ──────────────────────
# 묶음 가운데에서 각 동의 넓이 중심 쪽으로 SPREAD만큼 밀어낸 뒤, 서로 MIN_GAP보다 가까운 짝은
# 두 중심을 잇는 방향으로 조금씩 더 떼어 낸다(2 m 격자로 확인). 동 안의 모양은 그대로이고 자리만 옮긴다(표현값).
SPREAD, MIN_GAP, CELL_SEP, STEP = 0.09, 24.0, 2.0, 6.0
def ring_xz(module):
    b = module['boundary']
    return [game_xz(b[k], b[k + 1]) for k in range(0, len(b), 2)]
def poly_centroid(r):
    a = cx = cz = 0.0
    for i in range(len(r)):
        x0, z0 = r[i]; x1, z1 = r[(i + 1) % len(r)]
        c = x0 * z1 - x1 * z0; a += c; cx += (x0 + x1) * c; cz += (z0 + z1) * c
    return cx / (3 * a), cz / (3 * a)
from PIL import Image, ImageDraw
from scipy import ndimage
sep_rings = [ring_xz(m) for m in modules_out]
cents = [poly_centroid(r) for r in sep_rings]
ux, uz = sum(c[0] for c in cents) / len(cents), sum(c[1] for c in cents) / len(cents)
offsets = [[(c[0] - ux) * SPREAD, (c[1] - uz) * SPREAD] for c in cents]
pad = 900.0
gx0 = min(x for r in sep_rings for x, _ in r) - pad; gx1 = max(x for r in sep_rings for x, _ in r) + pad
gz0 = min(z for r in sep_rings for _, z in r) - pad; gz1 = max(z for r in sep_rings for _, z in r) + pad
GW, GH = int((gx1 - gx0) / CELL_SEP) + 1, int((gz1 - gz0) / CELL_SEP) + 1
half_gap_px = int(math.ceil(MIN_GAP / 2 / CELL_SEP))
grown = []
for r in sep_rings:
    img = Image.new('1', (GW, GH), 0)
    ImageDraw.Draw(img).polygon([((x - gx0) / CELL_SEP, (gz1 - z) / CELL_SEP) for x, z in r], fill=1)
    grown.append(ndimage.binary_dilation(np.array(img, dtype=bool), iterations=half_gap_px))
def shifted(mask, off):
    return np.roll(np.roll(mask, int(round(off[0] / CELL_SEP)), axis=1), -int(round(off[1] / CELL_SEP)), axis=0)
def touching(i, j):
    return bool(np.any(shifted(grown[i], offsets[i]) & shifted(grown[j], offsets[j])))
rounds = 0
for rounds in range(1, 80):
    moved = False
    for i in range(len(offsets)):
        for j in range(i + 1, len(offsets)):
            if not touching(i, j): continue
            dx = (cents[j][0] + offsets[j][0]) - (cents[i][0] + offsets[i][0])
            dz = (cents[j][1] + offsets[j][1]) - (cents[i][1] + offsets[i][1])
            L = math.hypot(dx, dz) or 1.0
            offsets[i][0] -= dx / L * STEP / 2; offsets[i][1] -= dz / L * STEP / 2
            offsets[j][0] += dx / L * STEP / 2; offsets[j][1] += dz / L * STEP / 2
            moved = True
    if not moved: break
pairs_touching = [(i, j) for i in range(len(offsets)) for j in range(i + 1, len(offsets)) if touching(i, j)]
check(f'띄워 보기: 모든 동 사이가 {MIN_GAP:.0f} m 이상 떨어짐', not pairs_touching, pairs_touching)
check('띄워 보기: 자리 옮김이 300 m 안', all(math.hypot(*o) < 300 for o in offsets), [round(math.hypot(*o)) for o in offsets])
for module, off in zip(modules_out, offsets):
    module['separatedEast'] = round(off[0], 2)
    module['separatedNorth'] = round(off[1], 2)
report['separation'] = {'spread': SPREAD, 'minGapMetres': MIN_GAP, 'gridMetres': CELL_SEP, 'rounds': rounds,
                        'offsets': {m['code']: [m['separatedEast'], m['separatedNorth']] for m in modules_out}}
print('separation rounds', rounds, report['separation']['offsets'])

manifest = {'schema': 'hongdal-dong-modules.v1', 'scopeRevision': scope['revision'], 'scopeStableId': scope['scopeStableId'],
            'scopeContentHash': scope['contentHashSha256'], 'sourceVintage': scope['sourceVintage'],
            'readiness': scope['desiredReadinessCode'], 'distributionApproved': scope['distributionApproved'],
            'traversalReady': scope['traversalReady'], 'gameplayReady': scope['gameplayReady'],
            'label': '2023 역사 경계 후보 · 내부 검토용 · 배포 아님',
            'attribution': ('건물 GIS·노드링크 · 녹지·횡단보도 서울시 · © OSM' if CROSSING_POINTS is not None else '건물 GIS·노드링크 · 녹지 서울시 · 경로 © OSM') if GREENS is not None else '건물 GIS·노드링크 · 경로 © OSM',
            'scopeModuleCount': len(scope['modules']),
            'originLatitude': LAT0, 'originLongitude': LON0, 'metresPerDegreeLatitude': M_LAT, 'metresPerDegreeLongitude': M_LON,
            'west': union[0], 'south': union[1], 'east': union[2], 'north': union[3],
            'buildingIndexResource': 'hongdal-dong-buildings', 'buildingIndexSha256': sha256(index_path),
            'presentationValues': '도로 폭과 자리(노드링크는 폭 없음. 기본 8 m를 건물 사이에 맞추고 나란한 두 줄은 한 길로), 골목(건물 배치에서 계산한 통로. 도로 자료 아님), 14 m 위 높이 압축, 용도별 색, 빛 방향, 띄워 보기 자리(서로 24 m 이상), 지붕 테두리 0.45 m(가까이에서만), 1층 가게 불빛(근린생활시설 · 가까이에서만 · 간판 글자 없음)'
                                  + (', 녹지 색(범위는 서울시 생태현황도 · 색은 표현)' if GREENS is not None else '')
                                  + (', 보도(차도에서 6 m까지 계산) · 연석선 · 시설물대 · 횡단보도 줄무늬(자리는 서울시 자료) · 점자블록(가까이에서만)' if CROSSING_POINTS is not None else ''),
            'modules': modules_out, 'roleMap': role_map}
if GREENS is not None: manifest['greenSource'] = GREEN_SOURCE
if CROSSING_POINTS is not None: manifest['crossingSource'] = CROSSINGS_SOURCE
manifest_path = os.path.join(OUT, 'hongdal-dong-modules.json')
with open(manifest_path, 'w', encoding='utf-8', newline='\n') as f: json.dump(manifest, f, ensure_ascii=False, indent=1)
report['outputs'] = {os.path.basename(p): {'bytes': os.path.getsize(p), 'sha256': sha256(p)} for p in
                     [manifest_path, index_path] + [os.path.join(OUT, m['resource'] + '.bytes') for m in modules_out]}
report['allChecksPassed'] = all(c['ok'] for c in report['checks'])
with open(REPORT, 'w', encoding='utf-8', newline='\n') as f: json.dump(report, f, ensure_ascii=False, indent=1)
print('checks', sum(c['ok'] for c in report['checks']), '/', len(report['checks']), 'max inverse error m', max_err)
print('roles', role_map)
