# 건물 역할표(S4)를 만든다: 표본 매장을 건물 색인에 묶고, 가상 카페·창고의 자리와 화물 진입 경로를 적는다.
# 사용: python3 -I make_roles.py <buildings.json> <delivery-sample.json> <road-graph.json> <out.json>
import heapq, json, math, sys
buildings_path, sample_path, graph_path, out_path = sys.argv[1:5]
index = json.load(open(buildings_path, encoding='utf-8'))
sample = json.load(open(sample_path, encoding='utf-8'))
graph = json.load(open(graph_path, encoding='utf-8'))
origin, mx, mz = index['origin'], index['metresPerDegreeLongitude'], index['metresPerDegreeLatitude']
by_id = {b['id']: b for b in index['buildings']}

def local(lat, lon): return ((lon - origin['longitude']) * mx, (lat - origin['latitude']) * mz)
def latlon(x, z): return (origin['latitude'] + z / mz, origin['longitude'] + x / mx)
def centre(b):
    p = b['xz']; n = len(p) // 2
    return (sum(p[0::2]) / n, sum(p[1::2]) / n)
xs = [v for b in index['buildings'] for v in b['xz'][0::2]]; zs = [v for b in index['buildings'] for v in b['xz'][1::2]]
bounds = (min(xs), max(xs), min(zs), max(zs))

RESIDENTIAL = {'house', 'detached', 'apartments', 'residential'}
def bind_store(store):
    x, z = local(store['latitude'], store['longitude'])
    if not (bounds[0] <= x <= bounds[1] and bounds[2] <= z <= bounds[3]): return None
    def dist(b): c = centre(b); return math.hypot(c[0] - x, c[1] - z)
    # 이름 없는 비주거 실제 건물이 40m 안에 있으면 그것, 없으면 가장 가까운 예시 건물
    real = sorted((b for b in index['buildings'] if not b['fill'] and not b.get('name') and b.get('kind') not in RESIDENTIAL), key=dist)
    if real and dist(real[0]) <= 40: return real[0]['id']
    fill = sorted((b for b in index['buildings'] if b['fill']), key=dist)
    return fill[0]['id'] if fill and dist(fill[0]) <= 40 else None

roles = []
for store in sample['stores']:
    building = bind_store(store)
    if building is None: continue
    letter = store['id'][-1].upper()
    roles.append({'placeId': 'store:' + store['id'], 'buildingId': building, 'business': 'restaurant', 'owner': 'mine',
                  'title': '매장 ' + letter, 'storeId': store['id']})
VIRTUAL = [('place:cafe-1', 'osm:way/1256615232', 'franchise', '카페 1호점'),
           ('place:urban-warehouse-1', 'osm:way/1256001691', 'warehouse', '도심 창고')]
for place, building, business, title in VIRTUAL:
    b = by_id[building]
    assert not b['fill'] and not b.get('name') and b.get('kind') not in RESIDENTIAL, building
    roles.append({'placeId': place, 'buildingId': building, 'business': business, 'owner': 'mine', 'title': title, 'storeId': ''})

# 화물 진입 경로: 2.5D 자료 범위의 동쪽 끝에서 카페 앞까지 도로 그래프의 최단 경로
nodes = graph['nodes'] if 'nodes' in graph else graph['Nodes']
edges = graph['edges'] if 'edges' in graph else graph['Edges']
def node_ll(n): return (n.get('latitude', n.get('lat')), n.get('longitude', n.get('lon')))
pts = [local(*node_ll(n)) for n in nodes]
adj = {i: [] for i in range(len(pts))}
for e in edges:
    a, b = (e['a'], e['b']) if isinstance(e, dict) else (e[0], e[1])
    d = math.hypot(pts[a][0] - pts[b][0], pts[a][1] - pts[b][1])
    adj[a].append((b, d)); adj[b].append((a, d))
cafe = centre(by_id['osm:way/1256615232'])
inside = [i for i, p in enumerate(pts) if bounds[0] <= p[0] <= bounds[1] and bounds[2] <= p[1] <= bounds[3] and adj[i]]
goal = min(inside, key=lambda i: math.hypot(pts[i][0] - cafe[0], pts[i][1] - cafe[1]))
dist = {goal: 0.0}; prev = {}; heap = [(0.0, goal)]
while heap:
    d, u = heapq.heappop(heap)
    if d > dist.get(u, 1e18): continue
    for v, w in adj[u]:
        nd = d + w
        if nd < dist.get(v, 1e18): dist[v] = nd; prev[v] = u; heapq.heappush(heap, (nd, v))
# 동쪽 끝 가까이(가장 동쪽 60m 띠)에서 카페와 이어진 노드 중 남북으로 카페와 가장 가까운 것
east = max(pts[i][0] for i in inside if i in dist)
start = min((i for i in inside if i in dist and pts[i][0] >= east - 60), key=lambda i: abs(pts[i][1] - cafe[1]))
path = [start]
while path[-1] != goal: path.append(prev[path[-1]])
flat = []
for i in path:
    la, lo = latlon(*pts[i]); flat += [round(la, 7), round(lo, 7)]
la, lo = latlon(*cafe)
out = {'schema': 'hongdal-building-roles.v1',
       'note': '건물 역할표. 매장·카페·창고는 모두 가상이고 실제 건물의 쓰임과 무관하다. 역할은 이름 없는 비주거 건물이나 예시 건물에만 얹는다.',
       'buildingIndex': index.get('schema'), 'roles': roles,
       'routes': [{'id': 'route:center-to-cafe-1', 'toPlaceId': 'place:cafe-1', 'latlon': flat}]}
json.dump(out, open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('roles', [(r['title'], r['buildingId']) for r in roles])
print('route nodes', len(path), 'length m', round(dist[start]), 'start local', [round(v) for v in pts[start]], 'goal', [round(v) for v in pts[goal]], 'cafe', [round(v) for v in cafe])
