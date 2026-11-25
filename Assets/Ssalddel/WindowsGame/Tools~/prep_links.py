"""표준노드링크(국토교통부, 이용허락범위 제한 없음)에서 수도권 링크만 경위도로 꺼낸다."""
import numpy as np, shapefile, warnings
from pyproj import Transformer
warnings.filterwarnings('ignore')
r = shapefile.Reader('/home/claude/cap/raw/nl/MOCT_LINK', encoding='cp949')
names = [f[0] for f in r.fields[1:]]
iL, iR, iT = names.index('LANES'), names.index('ROAD_RANK'), names.index('ROAD_TYPE')
tr = Transformer.from_crs('+proj=tmerc +lat_0=38 +lon_0=127 +k=1 +x_0=200000 +y_0=600000 +ellps=GRS80 +units=m +no_defs', 'EPSG:4326', always_xy=True)
# 수도권 범위(경위도 126.35~127.85, 36.89~38.30)를 이 좌표로 넉넉히
x0, y0 = tr.transform(126.35, 36.89, direction='INVERSE'); x1, y1 = tr.transform(127.85, 38.30, direction='INVERSE')
x0, y0, x1, y1 = min(x0, x1) - 2000, min(y0, y1) - 2000, max(x0, x1) + 2000, max(y0, y1) + 2000
pts, lens, lanes, rank, typ = [], [], [], [], []
for sr in r.iterShapeRecords():
    b = sr.shape.bbox
    if b[2] < x0 or b[0] > x1 or b[3] < y0 or b[1] > y1: continue
    p = sr.shape.points
    if len(p) < 2: continue
    pts.extend(p); lens.append(len(p))
    rec = sr.record
    lanes.append(int(rec[iL] or 1)); rank.append(int(rec[iR] or 108)); typ.append(int(rec[iT] or 0))
xy = np.asarray(pts, dtype=np.float64)
lon, lat = tr.transform(xy[:, 0], xy[:, 1])
np.savez('links_capital.npz', xy=np.column_stack([lon, lat]), lens=np.asarray(lens, np.int32), lanes=np.asarray(lanes, np.int16),
         rank=np.asarray(rank, np.int16), type=np.asarray(typ, np.int16))
print(len(lens), len(xy), np.unique(typ, return_counts=True), np.unique(rank, return_counts=True))
