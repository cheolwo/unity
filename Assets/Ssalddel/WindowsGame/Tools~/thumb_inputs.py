"""동 모듈 JSON(게임 원점 ENU)을 make_dong_thumbs.py가 읽는 동별 GeoJSON(경위도)으로 옮긴다 (2026-11-17, 중랑구 J2).
경위도는 게임 명세의 근사(경도 1° = metresPerDegreeLongitude, 위도 1° = 111,200 m)로 되돌린다. 미리보기 그림 전용이다.
사용: python3 -I thumb_inputs.py <inputs/modules> <hongdal-dong-modules.json> <out-folder>
"""
import sys, os, json, gzip, glob
MOD, MAN, OUT = sys.argv[1:4]
man = json.load(open(MAN, encoding='utf-8-sig'))
lat0, lon0 = man['originLatitude'], man['originLongitude']; kx, ky = man['metresPerDegreeLongitude'], man['metresPerDegreeLatitude']
def ll(q): return [round(lon0 + q['x'] / kx, 8), round(lat0 + q['z'] / ky, 8)]
def fc(features): return {'type': 'FeatureCollection', 'features': features}
def dump(obj, path):
    with gzip.open(path, 'wt', encoding='utf-8') as f: json.dump(obj, f, ensure_ascii=False)
for entry in man['modules']:
    m = json.load(open(os.path.join(MOD, entry['code'] + '.json'), encoding='utf-8'))
    d = os.path.join(OUT, entry['code']); os.makedirs(d, exist_ok=True)
    ring = [ll(q) for q in m['boundary']]; ring.append(ring[0])
    dump(fc([{'type': 'Feature', 'properties': {'code': entry['code']}, 'geometry': {'type': 'Polygon', 'coordinates': [ring]}}]), os.path.join(d, 'boundary.geojson.gz'))
    bs = []
    for b in m['buildings']:
        r = [ll(q) for q in b['footprint']]
        if len(r) < 3: continue
        if r[0] != r[-1]: r.append(r[0])
        h = b.get('heightMeters')
        bs.append({'type': 'Feature', 'properties': {'sourceId': b.get('sourceId') or b['buildingStableId'], 'aboveGroundFloorCount': b.get('aboveGroundFloorCount') or None,
                                                     'observedHeightMeters': h if h and 0 < h < 1000 else None}, 'geometry': {'type': 'Polygon', 'coordinates': [r]}})
    dump(fc(bs), os.path.join(d, 'buildings.geojson.gz'))
    rs = [{'type': 'Feature', 'properties': {'id': r['roadStableId']}, 'geometry': {'type': 'LineString', 'coordinates': [ll(r['from']), ll(r['to'])]}} for r in m['roads']]
    dump(fc(rs), os.path.join(d, 'roads.geojson.gz'))
    print(entry['code'], len(bs), len(rs))
