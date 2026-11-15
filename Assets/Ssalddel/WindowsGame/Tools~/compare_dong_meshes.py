"""다시 구운 동 메시 견주기 (품5, 2026-11-15).

전 메시(HDIO v2)와 새 메시를 읽어, 새 메시에서 지붕 테두리(uv2.y = -1) 정점과 그 삼각형을 빼고 근린생활시설 벽의
uv2.y에서 1000을 빼면 전 메시와 바이트까지 같은지 본다. 다시 굽기가 테두리와 가게 표시 말고는 아무것도 바꾸지 않았는지 확인한다.

사용: python3 -I compare_dong_meshes.py <전 .bytes> <새 .bytes>
"""
import struct, sys, numpy as np
def read(path):
    b = open(path, 'rb').read(); o = 0
    assert b[:4] == b'HDIO'; o = 4
    ver, = struct.unpack_from('<i', b, o); o += 4
    head = struct.unpack_from('<6d', b, o); o += 48
    counts = struct.unpack_from('<2i', b, o); o += 8
    n, = struct.unpack_from('<i', b, o); o += 4
    pos = np.frombuffer(b, '<f4', n * 3, o).reshape(n, 3); o += n * 12
    col = np.frombuffer(b, 'u1', n * 4, o).reshape(n, 4); o += n * 4
    uv = np.frombuffer(b, '<f4', n * 2, o).reshape(n, 2); o += n * 8
    uv2 = np.frombuffer(b, '<f4', n * 2, o).reshape(n, 2); o += n * 8
    t, = struct.unpack_from('<i', b, o); o += 4
    tri = np.frombuffer(b, '<i4', t, o); o += t * 4
    rest = b[o:]
    return dict(ver=ver, head=head, counts=counts, pos=pos, col=col, uv=uv, uv2=uv2, tri=tri, rest=rest)
a, b = read(sys.argv[1]), read(sys.argv[2])
keep = b['uv2'][:, 1] > -0.5
remap = -np.ones(len(keep), dtype=np.int64); remap[keep] = np.arange(int(keep.sum()))
tri = b['tri'].reshape(-1, 3)
tri_keep = keep[tri].all(axis=1)
assert (keep[tri].any(axis=1) == tri_keep).all(), 'mixed triangle'
uv2 = b['uv2'][keep].copy()
shop = (uv2[:, 0] > 0.5) & (uv2[:, 1] >= 999.5)
uv2[shop, 1] -= 1000.0
same = (a['ver'] == b['ver'] and a['head'] == b['head'] and a['counts'] == b['counts']
        and np.array_equal(a['pos'], b['pos'][keep]) and np.array_equal(a['col'], b['col'][keep])
        and np.array_equal(a['uv'], b['uv'][keep]) and np.array_equal(a['uv2'], uv2)
        and np.array_equal(a['tri'], remap[tri[tri_keep]].reshape(-1).astype('<i4')) and a['rest'] == b['rest'])
rim = ~keep
print(sys.argv[2].split('/')[-1], 'same-after-removing-rims', same, 'rim verts', int(rim.sum()), 'rim tris', int((~tri_keep).sum()),
      'shop walls', int(shop.sum()) // 4, 'rim y-span ok', bool(np.allclose(np.sort(np.unique(np.round(b['uv'][rim, 1], 3))), [0.0, 0.45])))
