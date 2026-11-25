import sys, struct, zlib, numpy as np
from PIL import Image, ImageDraw
def load(path):
    b = open(path, 'rb').read()
    if b[:4] == b'HDIZ': b = zlib.decompress(b[44:], -15)
    o = 8; lat0, lon0, w, s, e, n = struct.unpack_from('<6d', b, o); o += 48; nb, nr = struct.unpack_from('<2i', b, o); o += 8
    nv, = struct.unpack_from('<i', b, o); o += 4
    P = np.frombuffer(b, '<f4', nv * 3, o).reshape(-1, 3); o += nv * 12
    C = np.frombuffer(b, 'u1', nv * 4, o).reshape(-1, 4); o += nv * 4
    o += nv * 16
    nt, = struct.unpack_from('<i', b, o); o += 4
    T = np.frombuffer(b, '<i4', nt, o).reshape(-1, 3); o += nt * 4
    ns, = struct.unpack_from('<i', b, o); o += 4
    SP = np.frombuffer(b, '<f4', ns * 3, o).reshape(-1, 3); o += ns * 12
    nst, = struct.unpack_from('<i', b, o); o += 4
    ST = np.frombuffer(b, '<i4', nst, o).reshape(-1, 3)
    return P, C, T, SP, ST, nb
path, out = sys.argv[1:3]
P, C, T, SP, ST, nb = load(path)
yaw, pitch = np.radians(30), np.radians(45)
def proj(Q):
    x, y, z = Q[:, 0], Q[:, 1], Q[:, 2]
    xr = x * np.cos(yaw) - z * np.sin(yaw); zr = x * np.sin(yaw) + z * np.cos(yaw)
    sx = xr; sy = -(zr * np.sin(pitch) + y * np.cos(pitch)); depth = zr * np.cos(pitch) - y * np.sin(pitch)
    return np.column_stack([sx, sy]), depth
S, D = proj(P); SS, SD = proj(SP)
lo, hi = S.min(0), S.max(0); W = 1400; sc = (W - 40) / max(hi - lo); H = int((hi - lo)[1] * sc) + 40
img = Image.new('RGB', (W, H), (29, 44, 56)); dr = ImageDraw.Draw(img, 'RGBA')
def px(Q): return (Q - lo) * sc + 20
S = px(S); SS = px(SS)
ymax = P[T, 1].max(1)
flat = [(P[t, 1].mean(), 0, i) for i, t in enumerate(T) if ymax[i] < 0.2]
flat.sort(key=lambda t: t[0])
items = [(D[t].mean(), 0, i) for i, t in enumerate(T) if ymax[i] >= 0.2]
items.sort(key=lambda t: -t[0])
items = flat + [(0, 1, i) for i in range(len(ST))] + items
for _, kind, i in items:
    if kind == 0:
        t = T[i]; c = tuple(int(v) for v in C[t[0], :3]); dr.polygon([tuple(S[k]) for k in t], fill=c)
    else:
        t = ST[i]; dr.polygon([tuple(SS[k]) for k in t], fill=(0, 0, 0, 60))
img.save(out); print(out, img.size, 'buildings', nb, 'tris', len(T))
