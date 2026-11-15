"""행정동 선택창(r236)의 정적 미리보기를 굽는다 (2026-11-15).

이 실행판이 읽는 행정동 모듈 목록(Resources/hongdal-dong-modules.json)의 동마다, 그 동의 실제 경계 · 건물 · 큰길로
같은 시점(게임 기본 30° · 55°)과 같은 규칙의 배율로 한 장씩 그린다. 그림은 PNG 바이트를 .bytes로 Resources에 두고,
어느 모듈 판본(moduleContentHash)과 어느 원본 파일에서 구웠는지를 hongdal-dong-thumbs.json에 적는다.
게임은 모듈의 판본이 표와 다르면 그림을 쓰지 않는다(다른 동 · 다른 판본의 그림을 대신 쓰지 않는다).

원본: Codex 행정동 정밀 모듈 r1(artifacts/local/admin-dong-precision-modules-20261115/geometry/modules/<코드>/
boundary · buildings · roads .geojson.gz, EPSG:4326). 이 여섯 동은 2026-11-15 점검 도구(check_dong_package.py)에서
지금 모듈의 입력과 경계(0.7 mm 안) · 건물 ID가 같음을 확인했다. 내부 검토용이며 배포하지 않는다.

쓰기:  python3 -I -B make_dong_thumbs.py <geometry 폴더> <Resources 폴더>
"""
import gzip
import hashlib
import io
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

VERSION = "make_dong_thumbs.py v1"
W, H, SS = 456, 272, 3
YAW_DEG, PITCH_DEG = 30.0, 55.0          # 디오라마시점.DefaultYawDegrees · DefaultPitchDegrees
YAW, PITCH = math.radians(YAW_DEG), math.radians(PITCH_DEG)
GROUND = (30, 41, 52)
BACK = (17, 26, 36)
ROAD = (70, 84, 96)
WALL = (72, 80, 90)
EDGE = (110, 231, 204)


def rings(geometry):
    kind = geometry["type"]
    if kind == "Polygon":
        return [geometry["coordinates"]]
    if kind == "MultiPolygon":
        return geometry["coordinates"]
    return []


def lines(geometry):
    kind = geometry["type"]
    if kind == "LineString":
        return [geometry["coordinates"]]
    if kind == "MultiLineString":
        return geometry["coordinates"]
    return []


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def render(folder):
    load = lambda name: json.load(gzip.open(folder / f"{name}.geojson.gz", "rt", encoding="utf-8"))
    boundary, buildings, roads = load("boundary"), load("buildings"), load("roads")
    points = [p for f in boundary["features"] for poly in rings(f["geometry"]) for p in poly[0]]
    lon0 = sum(p[0] for p in points) / len(points)
    lat0 = sum(p[1] for p in points) / len(points)
    kx, ky = 111320 * math.cos(math.radians(lat0)), 110540

    # 행정동모듈Policy.Screen과 같은 식: U = cos·동 − sin·북, V = sin(pitch)·(−sin·동 − cos·북), 높이는 화면 위로 cos(pitch)·h
    def project(lon, lat, z=0.0):
        east, north = (lon - lon0) * kx, (lat - lat0) * ky
        u = math.cos(YAW) * east - math.sin(YAW) * north
        depth = math.sin(YAW) * east + math.cos(YAW) * north
        return u, -math.sin(PITCH) * depth - z * math.cos(PITCH), depth

    outline = [project(*p) for p in points]
    min_x, max_x = min(p[0] for p in outline), max(p[0] for p in outline)
    min_y, max_y = min(p[1] for p in outline) - 60, max(p[1] for p in outline)
    scale = min((W - 28) / (max_x - min_x), (H - 24) / (max_y - min_y)) * SS
    off_x = (W * SS - (max_x - min_x) * scale) / 2 - min_x * scale
    off_y = (H * SS - (max_y - min_y) * scale) / 2 - min_y * scale
    screen = lambda p: (p[0] * scale + off_x, p[1] * scale + off_y)

    image = Image.new("RGB", (W * SS, H * SS), BACK)
    draw = ImageDraw.Draw(image)
    for feature in boundary["features"]:
        for poly in rings(feature["geometry"]):
            draw.polygon([screen(project(*p)) for p in poly[0]], fill=GROUND)
    for feature in roads["features"]:
        for line in lines(feature["geometry"]):
            draw.line([screen(project(*p)) for p in line], fill=ROAD, width=2 * SS)
    items = []
    for feature in buildings["features"]:
        props = feature["properties"]
        floors = props.get("aboveGroundFloorCount") or 1
        height = props.get("observedHeightMeters") or floors * 3.2
        for poly in rings(feature["geometry"]):
            ring = poly[0]
            depth = sum(project(*p)[2] for p in ring) / len(ring)
            items.append((depth, props.get("sourceId") or "", ring, height))
    items.sort(key=lambda item: (-item[0], item[1]))          # 먼 것부터, 같으면 원본 ID 순(같은 입력이면 같은 그림)
    for _, _, ring, height in items:
        base = [screen(project(*p)) for p in ring]
        top = [screen(project(p[0], p[1], height)) for p in ring]
        for i in range(len(ring) - 1):
            draw.polygon([base[i], base[i + 1], top[i + 1], top[i]], fill=WALL)
        shade = max(0.0, min(1.0, height / 40))
        draw.polygon(top, fill=(int(150 + 40 * shade), int(158 + 38 * shade), int(168 + 36 * shade)))
    for feature in boundary["features"]:
        for poly in rings(feature["geometry"]):
            ring = [screen(project(*p)) for p in poly[0]]
            draw.line(ring + [ring[0]], fill=EDGE, width=2 * SS)
    image = image.resize((W, H), Image.LANCZOS)
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue(), len(buildings["features"]), len(roads["features"])


def main():
    geometry, resources = Path(sys.argv[1]), Path(sys.argv[2])
    manifest = json.loads((resources / "hongdal-dong-modules.json").read_text(encoding="utf-8-sig"))
    thumbs = []
    for entry in manifest["modules"]:
        folder = geometry / entry["code"]
        png, building_count, road_count = render(folder)
        name = f"hongdal-dong-thumb-{entry['code']}"
        (resources / f"{name}.bytes").write_bytes(png)
        thumbs.append({
            "regionId": entry["regionId"],
            "code": entry["code"],
            "name": entry["name"],
            "district": entry["district"],
            "resource": name,
            "width": W,
            "height": H,
            "pngSha256": hashlib.sha256(png).hexdigest(),
            "moduleContentHash": entry["moduleContentHash"],
            "moduleBuildings": entry.get("buildings"),
            "sourceBuildings": building_count,
            "sourceRoads": road_count,
            "sourceSha256": {kind: sha256(folder / f"{kind}.geojson.gz") for kind in ("boundary", "buildings", "roads")},
        })
        print(entry["code"], entry["name"], len(png), "bytes", building_count, "buildings")
    out = {
        "schema": "hongdal.dong-thumbs.v1",
        "renderer": VERSION,
        "view": {"yawDegrees": YAW_DEG, "pitchDegrees": PITCH_DEG},
        "source": "artifacts/local/admin-dong-precision-modules-20261115/geometry/modules (Codex r1, EPSG:4326)",
        "note": "내부 검토용 · 배포 아님. 모듈 판본(moduleContentHash)이 다르면 게임은 이 그림을 쓰지 않는다.",
        "thumbs": thumbs,
    }
    (resources / "hongdal-dong-thumbs.json").write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
