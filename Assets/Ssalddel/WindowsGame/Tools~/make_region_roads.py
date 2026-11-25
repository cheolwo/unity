#!/usr/bin/env python3
"""수도권 권역 바탕(길)을 굽는다 (2026-11-16, 이어지는 지도 M2·M3 · 정밀 2).

전국 지도를 3배보다 더 확대할 때, 전국 바탕 위에 이 그림들을 깐다. 전국 팩과 같은 투영(make_nation_pack.py의 P())으로
굽기 때문에 전국 지도의 다른 그림과 이음매가 없다. 배경은 투명하고 길만 무채색으로 그린다(바탕은 무채색 · 뜻은 색).
지형 음영은 넣지 않는다: 보유 DSM은 내부 검토 전용이다(이어지는 지도 계획 C3).

  · 넓은 그림(수도권): 세계 1단위당 24점, 가로 2048점
  · 가운데 그림(사가정 둘레, 구 지도 자료와 그 둘레): 세계 1단위당 64점 — 30배 넘게 확대해도 흐려지지 않게
    두 그림은 가운데 상자 가장자리 4% 안에서 서로 흐리게 나눠(넓은 그림 × (1 − m), 가운데 그림 × m) 겹쳐 그려도 이음매가 없다.

입력: MOCT_LINK.shp/.dbf (국토교통부 표준노드링크, EPSG:5186, 이용허락범위 제한 없음)
출력: hongdal-region-roads.bytes       넓은 그림 PNG(RGBA)
      hongdal-region-roads-core.bytes  가운데 그림 PNG(RGBA)
      hongdal-region-space.json        범위(경위도 · 전국 세계 좌표) · 출처 · 등급
사용: python3 -I make_region_roads.py <MOCT_LINK가 있는 폴더> <출력 폴더> [판본 날짜]
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image, ImageDraw
import gu_geo as g

SRC, OUT = sys.argv[1:3]
VERSION = sys.argv[3] if len(sys.argv) > 3 else "2026-09-14"
WIDE = (126.55, 127.75, 37.15, 37.95)        # 서 · 동 · 남 · 북
CORE = (126.85, 127.27, 37.45, 37.70)
WIDE_DENSITY = 2048.0 / ((WIDE[1] - WIDE[0]) * 88.0 * math.cos(math.radians(36.0)))   # ≈ 24점/단위
CORE_DENSITY = 64.0
FEATHER = 0.04
KX = 88.0 * math.cos(math.radians(36.0))           # make_nation_pack.py와 같은 투영
def P(lon, lat): return 600.0 + (lon - 127.6) * KX, 337.0 - (lat - 36.0) * 88.0
assert abs(P(127.0, 37.56)[0] - 557.28) < 0.02 and abs(P(127.0, 37.56)[1] - 199.72) < 0.02, "전국 팩 투영과 다름"
SS = 2                                                # 두 배로 그린 뒤 줄여 가장자리를 부드럽게
# 등급: 101 고속국도 · 102 도시고속 · 103 일반국도 · 104 특별·광역시도 · 105 국지도 · 106 지방도 · 107 시군도. 너비는 넓은 그림 점 기준.
STYLE = {101: (118, 2.4, 255), 102: (112, 2.2, 250), 103: (98, 1.8, 235), 104: (84, 1.3, 215), 105: (76, 1.1, 200), 106: (74, 1.0, 190), 107: (66, 0.8, 165)}

fields, record, count = g.read_dbf(os.path.join(SRC, "MOCT_LINK.dbf"))
box = (140000.0, 500000.0, 280000.0, 610000.0)
lines = {rank: [] for rank in STYLE}
for index, parts in g.read_shapes(os.path.join(SRC, "MOCT_LINK.shp"), box):
    rank = record(index).get("ROAD_RANK", "")
    if not rank.isdigit() or int(rank) not in STYLE: continue
    for part in parts:
        lat, lon = g.tm_inverse(part[:, 0], part[:, 1], **g.TM_5186)
        if lon.max() < WIDE[0] or lon.min() > WIDE[1] or lat.max() < WIDE[2] or lat.min() > WIDE[3]: continue
        wx, wy = P(lon, lat)
        lines[int(rank)].append((wx, wy, lon.min(), lon.max(), lat.min(), lat.max()))

def world_box(b):
    x0, y0 = P(b[0], b[3]); x1, y1 = P(b[1], b[2])
    return x0, y0, x1, y1

def bake(b, density):
    x0, y0, x1, y1 = world_box(b)
    width = int(round((x1 - x0) * density)); height = int(round((y1 - y0) * density))
    img = Image.new("RGBA", (width * SS, height * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    scale = density / WIDE_DENSITY
    for rank in sorted(STYLE, reverse=True):          # 작은 길부터 깔고 큰길을 위에
        grey, w, alpha = STYLE[rank]
        pen = max(1, int(round(w * scale * SS)))
        for wx, wy, lo0, lo1, la0, la1 in lines[rank]:
            if lo1 < b[0] or lo0 > b[1] or la1 < b[2] or la0 > b[3]: continue
            pts = list(zip(((wx - x0) * density * SS).tolist(), ((wy - y0) * density * SS).tolist()))
            if len(pts) >= 2: draw.line(pts, fill=(grey, grey, grey, alpha), width=pen)
    img = img.resize((width, height), Image.LANCZOS)
    return img, (x0, y0, x1, y1)

def core_mask(shape, origin, density, core_world):
    """가운데 상자 안쪽 1, 바깥 0, 가장자리 FEATHER(상자 크기 비율) 안에서 부드럽게."""
    h, w = shape
    xs = origin[0] + (np.arange(w) + 0.5) / density
    ys = origin[1] + (np.arange(h) + 0.5) / density
    cx0, cy0, cx1, cy1 = core_world
    fx = (cx1 - cx0) * FEATHER; fy = (cy1 - cy0) * FEATHER
    mx = np.clip(np.minimum(xs - cx0, cx1 - xs) / fx, 0, 1)
    my = np.clip(np.minimum(ys - cy0, cy1 - ys) / fy, 0, 1)
    m = np.minimum(my[:, None], mx[None, :])
    return m * m * (3 - 2 * m)

os.makedirs(OUT, exist_ok=True)
wide, wb = bake(WIDE, WIDE_DENSITY)
core, cb = bake(CORE, CORE_DENSITY)
wa = np.asarray(wide).astype(np.float32)
wa[..., 3] *= 1.0 - core_mask(wa.shape[:2], (wb[0], wb[1]), WIDE_DENSITY, cb)
ca = np.asarray(core).astype(np.float32)
ca[..., 3] *= core_mask(ca.shape[:2], (cb[0], cb[1]), CORE_DENSITY, cb)
Image.fromarray(np.clip(wa, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "hongdal-region-roads.bytes"), format="PNG", optimize=True)
Image.fromarray(np.clip(ca, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "hongdal-region-roads-core.bytes"), format="PNG", optimize=True)
meta = {
    "schema": "hongdal-region-roads.v2", "version": VERSION,
    "source": "국토교통부 표준노드링크(ITS) " + VERSION + " · 이용허락범위 제한 없음",
    "note": "수도권 권역 바탕(길만). 지형 · 하천은 넣지 않음(자료 준비 전). 전국 팩과 같은 투영. 가운데 그림은 사가정 둘레를 64점/단위로.",
    "west": WIDE[0], "east": WIDE[1], "south": WIDE[2], "north": WIDE[3],
    "worldX": round(wb[0], 4), "worldY": round(wb[1], 4), "worldWidth": round(wb[2] - wb[0], 4), "worldHeight": round(wb[3] - wb[1], 4),
    "width": wide.size[0], "height": wide.size[1], "density": round(WIDE_DENSITY, 3),
    "coreWest": CORE[0], "coreEast": CORE[1], "coreSouth": CORE[2], "coreNorth": CORE[3],
    "coreWorldX": round(cb[0], 4), "coreWorldY": round(cb[1], 4), "coreWorldWidth": round(cb[2] - cb[0], 4), "coreWorldHeight": round(cb[3] - cb[1], 4),
    "coreWidth": core.size[0], "coreHeight": core.size[1], "coreDensity": CORE_DENSITY, "feather": FEATHER,
    "links": {str(rank): len(lines[rank]) for rank in STYLE},
}
json.dump(meta, open(os.path.join(OUT, "hongdal-region-space.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(json.dumps(meta, ensure_ascii=False))
print(os.path.getsize(os.path.join(OUT, "hongdal-region-roads.bytes")), os.path.getsize(os.path.join(OUT, "hongdal-region-roads-core.bytes")))
