"""구 지도 굽기의 공통 도구: 범위, 좌표 변환, shp/dbf 읽기 (2026-11-13).

게임의 구 지도 좌표는 범위의 남서쪽 모서리에서 동쪽 x, 북쪽 y (미터)다.
  x = (경도 - WEST) * MX,  y = (위도 - SOUTH) * MY
원본의 좌표계는 두 가지다.
  EPSG:5186  GRS80 중부원점 (차선, ITS 표준 노드링크)
  EPSG:5174  Bessel 수정 중부원점 (등고선·표고점). 세계측지계로 옮길 때 7매개변수 변환을 쓴다.
"""
import math, struct
import numpy as np

WEST, EAST, SOUTH, NORTH = 126.940, 127.175, 37.523, 37.627
MX, MY = 88224.0, 111320.0
WIDTH, HEIGHT = (EAST - WEST) * MX, (NORTH - SOUTH) * MY

GRS80 = (6378137.0, 1 / 298.257222101)
BESSEL = (6377397.155, 1 / 299.1528128)
TM_5186 = dict(ellipsoid=GRS80, lat0=38.0, lon0=127.0, fe=200000.0, fn=600000.0)
TM_5174 = dict(ellipsoid=BESSEL, lat0=38.0, lon0=127.0028902777778, fe=200000.0, fn=500000.0)
# Bessel → WGS84 (PROJ의 +towgs84=-115.80,474.99,674.11,1.16,-2.31,-1.63,6.43, 위치 벡터 방식)
TOWGS84 = (-115.80, 474.99, 674.11, 1.16, -2.31, -1.63, 6.43)


def _meridian(a, e2, phi):
    return a * ((1 - e2 / 4 - 3 * e2 ** 2 / 64 - 5 * e2 ** 3 / 256) * phi - (3 * e2 / 8 + 3 * e2 ** 2 / 32 + 45 * e2 ** 3 / 1024) * np.sin(2 * phi)
                + (15 * e2 ** 2 / 256 + 45 * e2 ** 3 / 1024) * np.sin(4 * phi) - (35 * e2 ** 3 / 3072) * np.sin(6 * phi))


def tm_inverse(x, y, ellipsoid, lat0, lon0, fe, fn):
    """횡축 메르카토르 평면 좌표(배열) → 그 타원체의 위도·경도(도)."""
    a, f = ellipsoid
    e2 = f * (2 - f); ep2 = e2 / (1 - e2); n1 = (1 - math.sqrt(1 - e2)) / (1 + math.sqrt(1 - e2))
    x = np.asarray(x, dtype=np.float64); y = np.asarray(y, dtype=np.float64)
    m = _meridian(a, e2, math.radians(lat0)) + (y - fn)
    mu = m / (a * (1 - e2 / 4 - 3 * e2 ** 2 / 64 - 5 * e2 ** 3 / 256))
    p1 = mu + (3 * n1 / 2 - 27 * n1 ** 3 / 32) * np.sin(2 * mu) + (21 * n1 ** 2 / 16 - 55 * n1 ** 4 / 32) * np.sin(4 * mu) \
        + (151 * n1 ** 3 / 96) * np.sin(6 * mu) + (1097 * n1 ** 4 / 512) * np.sin(8 * mu)
    c1 = ep2 * np.cos(p1) ** 2; t1 = np.tan(p1) ** 2
    nu = a / np.sqrt(1 - e2 * np.sin(p1) ** 2); rho = a * (1 - e2) / (1 - e2 * np.sin(p1) ** 2) ** 1.5; d = (x - fe) / nu
    lat = p1 - (nu * np.tan(p1) / rho) * (d * d / 2 - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * ep2) * d ** 4 / 24
                                           + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * ep2 - 3 * c1 * c1) * d ** 6 / 720)
    lon = math.radians(lon0) + (d - (1 + 2 * t1 + c1) * d ** 3 / 6 + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * ep2 + 24 * t1 * t1) * d ** 5 / 120) / np.cos(p1)
    return np.degrees(lat), np.degrees(lon)


def bessel_to_wgs84(lat, lon):
    """Bessel 위도·경도(도, 배열) → WGS84 위도·경도(도). 높이는 0으로 둔다."""
    a, f = BESSEL; e2 = f * (2 - f)
    phi, lam = np.radians(lat), np.radians(lon)
    nu = a / np.sqrt(1 - e2 * np.sin(phi) ** 2)
    X = nu * np.cos(phi) * np.cos(lam); Y = nu * np.cos(phi) * np.sin(lam); Z = nu * (1 - e2) * np.sin(phi)
    dx, dy, dz, rx, ry, rz, s = TOWGS84
    rx, ry, rz = (math.radians(v / 3600) for v in (rx, ry, rz)); k = 1 + s * 1e-6
    X2 = dx + k * (X - rz * Y + ry * Z); Y2 = dy + k * (rz * X + Y - rx * Z); Z2 = dz + k * (-ry * X + rx * Y + Z)
    a2, f2 = GRS80; e22 = f2 * (2 - f2); b2 = a2 * (1 - f2); ep22 = (a2 * a2 - b2 * b2) / (b2 * b2)
    p = np.hypot(X2, Y2); th = np.arctan2(Z2 * a2, p * b2)
    phi2 = np.arctan2(Z2 + ep22 * b2 * np.sin(th) ** 3, p - e22 * a2 * np.cos(th) ** 3)
    return np.degrees(phi2), np.degrees(np.arctan2(Y2, X2))


def to_metres(lat, lon):
    return (np.asarray(lon) - WEST) * MX, (np.asarray(lat) - SOUTH) * MY


def from_5186(x, y):
    return to_metres(*tm_inverse(x, y, **TM_5186))


def from_5174(x, y):
    return to_metres(*bessel_to_wgs84(*tm_inverse(x, y, **TM_5174)))


def read_dbf(path, encoding='cp949'):
    """(필드 이름 목록, 레코드를 dict로 읽는 함수, 레코드 수)."""
    data = open(path, 'rb').read()
    count, header, length = struct.unpack('<IHH', data[4:12])
    fields, offset, at = [], 1, 32
    while data[at] != 0x0D:
        name = data[at:at + 11].split(b'\0')[0].decode('ascii', 'replace'); size = data[at + 16]
        fields.append((name, offset, size)); offset += size; at += 32

    def record(index):
        start = header + index * length
        return {name: data[start + off:start + off + size].decode(encoding, 'replace').strip() for name, off, size in fields}
    return [f[0] for f in fields], record, count


def read_shapes(path, box=None):
    """shp의 선·점 레코드를 차례로 낸다: (레코드 순번, 부분별 좌표 배열 목록). box = (x0, y0, x1, y1)와 겹치는 것만."""
    data = open(path, 'rb').read()
    at, index, size = 100, 0, len(data)
    while at < size:
        length = struct.unpack('>i', data[at + 4:at + 8])[0] * 2
        kind = struct.unpack('<i', data[at + 8:at + 12])[0]
        body = at + 8
        if kind in (3, 13, 23, 5, 15, 25):
            x0, y0, x1, y1 = struct.unpack('<4d', data[body + 4:body + 36])
            if box is None or not (x1 < box[0] or x0 > box[2] or y1 < box[1] or y0 > box[3]):
                parts, points = struct.unpack('<2i', data[body + 36:body + 44])
                starts = list(struct.unpack('<%di' % parts, data[body + 44:body + 44 + 4 * parts])) + [points]
                xy = np.frombuffer(data, dtype='<f8', count=points * 2, offset=body + 44 + 4 * parts).reshape(-1, 2)
                yield index, [xy[starts[i]:starts[i + 1]] for i in range(parts)]
        elif kind in (1, 11, 21):
            x, y = struct.unpack('<2d', data[body + 4:body + 20])
            if box is None or (box[0] <= x <= box[2] and box[1] <= y <= box[3]):
                yield index, [np.array([[x, y]])]
        at += 8 + length; index += 1


def box_in(tm, margin=1500.0):
    """범위를 그 평면 좌표계에서 넉넉하게 잡은 상자 (정확한 선별은 변환한 뒤에 한다)."""
    lon0 = tm['lon0']; fn = tm['fn']
    x0 = tm['fe'] + (WEST - lon0) * MX - margin; x1 = tm['fe'] + (EAST - lon0) * MX + margin
    y0 = fn + (SOUTH - 38.0) * 110980 - margin; y1 = fn + (NORTH - 38.0) * 110980 + margin
    return (x0, y0, x1, y1)
