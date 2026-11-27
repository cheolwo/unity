"""농촌 판 땅 층 비스듬한 미리보기(게임 밖 · 게임 코드 손대지 않음).

사용: python3 -B render_land_oblique.py <hongdal-land-코드.bytes> <hongdal-land-코드.json> <out.png> [창 너비 m=1400] [중심 auto|x,z] [방위 도=25] [기울기 도=42]

* 땅 메시(HDIZ v1 → HDIO v2)를 읽어 정사영 비스듬한 시점으로 그린다. 땅은 평면이라 순서 없이 그리고,
  비닐하우스(보고서 houses: 게임 xz · 각 · 길이 · 폭)만 3 m 높이 반원통 모양 상자로 세워 깊이순으로 그린다.
* 메시 구멍(건물 · 도로 자리)은 바탕색(마당 · 길 회색)으로 보인다.
* 중심 auto = 하우스가 가장 많은 400 m 칸(없으면 땅 층 가운데).
내부 검토용 · 배포 아님.
"""
import sys, io, json, math, struct, zlib, hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFont

BYTES, REPORT, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
WIN = float(sys.argv[4]) if len(sys.argv) > 4 else 1400.0
CENTER = sys.argv[5] if len(sys.argv) > 5 else 'auto'
YAW = math.radians(float(sys.argv[6]) if len(sys.argv) > 6 else 25.0)
PITCH = math.radians(float(sys.argv[7]) if len(sys.argv) > 7 else 42.0)
W_PX, H_PX, SS = 1800, 1100, 2
HOUSE_H = 3.0

# ── 메시 읽기 ─────────────────────────────────────────────────────────────
b = open(BYTES, 'rb').read()
assert b[:4] == b'HDIZ' and struct.unpack_from('<i', b, 4)[0] == 1
rawlen = struct.unpack_from('<i', b, 8)[0]; sha = b[12:44].hex().upper()
raw = zlib.decompress(b[44:], -15)
assert len(raw) == rawlen and hashlib.sha256(raw).hexdigest().upper() == sha
assert raw[:4] == b'HDIO' and struct.unpack_from('<i', raw, 4)[0] == 2
o = 8 + 48 + 8
n = struct.unpack_from('<i', raw, o)[0]; o += 4
P = np.frombuffer(raw, '<f4', n * 3, o).reshape(n, 3); o += n * 12
C = np.frombuffer(raw, 'u1', n * 4, o).reshape(n, 4); o += n * 4
o += n * 8 + n * 8
m = struct.unpack_from('<i', raw, o)[0]; o += 4
T = np.frombuffer(raw, '<i4', m, o).reshape(-1, 3)
rep = json.load(open(REPORT, encoding='utf-8'))
houses = rep.get('houses', [])

# ── 중심 ───────────────────────────────────────────────────────────────
if CENTER != 'auto':
    cx, cz = map(float, CENTER.split(','))
elif houses:
    hx = np.array([h['x'] for h in houses]); hz = np.array([h['z'] for h in houses])
    keys = np.floor(hx / 400).astype(int) * 100000 + np.floor(hz / 400).astype(int)
    u, cnt = np.unique(keys, return_counts=True); k = u[cnt.argmax()]
    sel = keys == k; cx, cz = float(hx[sel].mean()), float(hz[sel].mean())
else:
    cx, cz = float(P[:, 0].mean()), float(P[:, 2].mean())

s = W_PX * SS / WIN                       # px / m
cy, sy = math.cos(YAW), math.sin(YAW); cp, sp = math.cos(PITCH), math.sin(PITCH)
def proj(x, y, z):
    dx, dz = x - cx, z - cz
    u = dx * cy - dz * sy                 # 화면 가로
    d = dx * sy + dz * cy                 # 깊이(멀수록 큼)
    v = -d * sp - y * cp                  # 화면 세로(위가 음)
    return u * s + W_PX * SS / 2, v * s + H_PX * SS / 2, d

# ── 땅 ────────────────────────────────────────────────────────────────
X, Y, D = proj(P[:, 0].astype(float), P[:, 1].astype(float), P[:, 2].astype(float))
tx, ty = X[T], Y[T]
inview = (tx.max(1) >= 0) & (tx.min(1) <= W_PX * SS) & (ty.max(1) >= 0) & (ty.min(1) <= H_PX * SS)
idx = np.nonzero(inview)[0]
img = Image.new('RGB', (W_PX * SS, H_PX * SS), (112, 110, 102))   # 구멍 = 마당 · 길
dr = ImageDraw.Draw(img)
col = C[T[:, 0], :3]
for i in idx:
    dr.polygon([(tx[i, 0], ty[i, 0]), (tx[i, 1], ty[i, 1]), (tx[i, 2], ty[i, 2])], fill=tuple(int(c) for c in col[i]))

# ── 비닐하우스(깊이순: 먼 것부터) ─────────────────────────────────────────
boxes = []
for h in houses:
    a = h['ang']; L, Wd = h['L'], h['W']
    ux, uz = math.cos(a) * L / 2, math.sin(a) * L / 2
    vx, vz = -math.sin(a) * Wd / 2, math.cos(a) * Wd / 2
    corners = [(h['x'] + sx * ux + tx_ * vx, h['z'] + sx * uz + tx_ * vz) for sx, tx_ in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    _, _, d0 = proj(h['x'], 0, h['z'])
    sx_, sy_, _ = proj(h['x'], 0, h['z'])
    if -200 <= sx_ <= W_PX * SS + 200 and -200 <= sy_ <= H_PX * SS + 200:
        boxes.append((d0, corners))
boxes.sort(key=lambda t: -t[0])
for _, cs in boxes:
    base = [proj(x, 0.0, z)[:2] for x, z in cs]
    top = [proj(x, HOUSE_H, z)[:2] for x, z in cs]
    depth = [proj(x, 0.0, z)[2] for x, z in cs]
    for i in range(4):                     # 앞쪽(가까운) 옆면만
        j = (i + 1) % 4
        if depth[i] + depth[j] < sum(depth) / 2:
            dr.polygon([base[i], base[j], top[j], top[i]], fill=(168, 176, 180))
    dr.polygon(top, fill=(226, 232, 236), outline=(190, 198, 202))

img = img.resize((W_PX, H_PX), Image.LANCZOS)
dr = ImageDraw.Draw(img)
try:
    import glob
    f = ImageFont.truetype((glob.glob('/usr/share/fonts/**/*Nanum*', recursive=True) + glob.glob('/usr/share/fonts/**/*CJK*', recursive=True))[0], 22)
except Exception:
    f = ImageFont.load_default()
label = f"{rep.get('code')} · 창 {WIN:.0f} m · 하우스 {len(boxes)} · 땅 층 비스듬히(게임 밖 미리보기) · 내부 검토용 · 배포 아님"
dr.rectangle([0, H_PX - 40, W_PX, H_PX], fill=(20, 22, 26)); dr.text((16, H_PX - 34), label, fill=(225, 225, 225), font=f)
img.save(OUT)
print('OK', OUT, 'tris', len(idx), 'houses', len(boxes), 'center', round(cx), round(cz))
