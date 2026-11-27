"""농촌 판 땅 층 차례 굽기(B5 · B6, 2026-11-26).

차례: 대표 12곳(representatives.csv) → 1차(rural-candidates.csv, 농경지 30% 이상 읍 · 면) → 2차(농경지 15% 이상 읍 · 면).
동마다 make_rural_land.py를 따로 띄운다. 한 번 부를 때 시간 예산(초) 안에서만 새 동을 시작하고,
이미 통과한 동(같은 도구 지문 · 검사 모두 통과)은 건너뛴다. 상태는 <결과>/queue-status.json에 남는다.
PC 셸은 호출이 끝나면 뒤에 띄운 일이 멈추므로, 이 실행기를 여러 번 불러 이어 굽는다.

사용: python3 -B bake_rural_queue.py <결과 폴더> <예산 초> [대표|1차|2차|3차]
"""
import sys, os, csv, json, glob, time, subprocess, hashlib
OUT, BUDGET = sys.argv[1], float(sys.argv[2])
STAGE = sys.argv[3] if len(sys.argv) > 3 else '1차'
HOME = os.path.expanduser('~')
LOCAL = os.path.join(HOME, 'mnt/Hongdal/artifacts/local')
N1 = os.path.join(LOCAL, 'public-data/farmmap-rural-n1-20261126')
DB = os.path.join(N1, 'farmmap-rural-n1.sqlite')
DEM = ','.join(os.path.join(LOCAL, 'public-data', d, 'raw') for d in ('nationwide-diorama-terrain-20261116-r1', 'map-precision-terrain-rights-20261116-r1'))   # n37e126 · n37e127은 뒤 폴더에
BAKE = os.path.join(LOCAL, 'hongdal-unity-claude-20261112/dong-bake-national-20261120-r1')
TOOL = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'make_rural_land.py')
T0 = time.time()

def sha(p):
    h = hashlib.sha256(open(p, 'rb').read()); return h.hexdigest().upper()
TOOL_SHA = sha(TOOL)
# 결과 메시가 같고 검사만 바뀐 앞 판(이미 통과한 동은 다시 굽지 않는다). 2026-11-26 16:5x: 검사 1 · 2를 섬 경계에 맞게 고침
COMPAT = {'ED990D294316CCB8'}

def rows(name): return list(csv.DictReader(open(os.path.join(N1, name), encoding='utf-8-sig')))
farm = {r['dong_code']: r for r in rows('dong-farmland.csv')}
reps = [r['dong_code'] for r in rows('representatives.csv')]
first = [r['dong_code'] for r in rows('rural-candidates.csv')]
second = sorted([c for c, r in farm.items() if r['admin_type'] in ('읍', '면') and float(r['farm_share'] or 0) >= 0.15],
                key=lambda c: -float(farm[c]['farm_share']))
# 3차(2026-11-26 20:4x): 다등급 읍·면 농지 0.08~0.15 + 다등급 동 농지 ≥ 0.15(도시 가장자리 농촌 동)
third = sorted([c for c, r in farm.items() if r['grade'] == '다' and (
                   (r['admin_type'] in ('읍', '면') and 0.08 <= float(r['farm_share'] or 0) < 0.15) or
                   (r['admin_type'] == '동' and float(r['farm_share'] or 0) >= 0.15))],
               key=lambda c: -float(farm[c]['farm_share']))
order = {'대표': reps, '1차': reps + first, '2차': reps + first + second, '3차': reps + first + second + third}[STAGE]
seen, queue = set(), []
for c in order:
    if c not in seen: seen.add(c); queue.append(c)

status_path = os.path.join(OUT, 'queue-status.json')
status = json.load(open(status_path, encoding='utf-8')) if os.path.exists(status_path) else {'dongs': {}}
status['tool'] = TOOL_SHA; status['stage'] = STAGE; status['queue'] = len(queue)

TAR_CACHE = {}
def find_tar(code):
    """바탕 입력: <BAKE>/codex-official-<run 앞>-p*/out/<run>/<시군구>/<묶음>/inputs/modules.tar.gz. 묶음 안 modules/<코드>.json으로 확인."""
    r = farm[code]; run = r['bake_run']; sgg = code[:5]
    if run not in TAR_CACHE:
        base = run[:-3] if run.endswith('-r2') else run
        TAR_CACHE[run] = {}
        for g in glob.glob(os.path.join(BAKE, f'codex-official-{base}-p*', 'out', run, sgg, '*', 'inputs', 'ids.json')) + \
                 glob.glob(os.path.join(BAKE, f'codex-official-{base}-p*', 'out', run, sgg, '*', 'inputs', 'scope-manifest.json')):
            d = os.path.dirname(g)
            try:
                txt = open(os.path.join(d, 'scope-manifest.json'), encoding='utf-8').read()
            except OSError: continue
            for c in set(__import__('re').findall(r'hjd:(\d{10})', txt)): TAR_CACHE[run][c] = os.path.join(d, 'modules.tar.gz')
    t = TAR_CACHE[run].get(code)
    return t if t and os.path.exists(t) else None

def done(code):
    j = os.path.join(OUT, code, f'hongdal-land-{code}.json')
    s = status['dongs'].get(code, {})
    return os.path.exists(j) and s.get('ok') and (s.get('tool') == TOOL_SHA or (s.get('tool') or '')[:16] in COMPAT)
def failed_same_tool(code):
    s = status['dongs'].get(code, {})
    return s.get('tool') == TOOL_SHA and s.get('ok') is False and s.get('error') != '시간 초과'

ran = 0
for code in queue:
    if done(code) or failed_same_tool(code): continue
    left = BUDGET - (time.time() - T0)
    prev = status['dongs'].get(code, {})
    area_km2 = float(farm[code]['dong_area_m2']) / 1e6
    est = (max(prev['seconds'] * 1.8, 12 + area_km2 * 1.6) if prev.get('error') == '시간 초과' else prev.get('seconds')) or 12 + area_km2 * 1.1
    if left < min(est * 1.2, BUDGET - 20, 170) or left < 25: continue          # 이 동은 다음 호출에서. 더 작은 동을 먼저 굽는다
    tar = find_tar(code)
    rec = {'name': farm[code]['sigungu_name'] + ' ' + farm[code]['dong_name'], 'tool': TOOL_SHA, 'area_km2': round(float(farm[code]['dong_area_m2']) / 1e6, 1)}
    if not tar:
        rec.update(ok=False, error='바탕 입력(modules.tar.gz)을 찾지 못함'); status['dongs'][code] = rec; continue
    os.makedirs(os.path.join(OUT, code), exist_ok=True)
    t1 = time.time()
    try:
        p = subprocess.run(['python3', '-B', TOOL, tar, code, DB, DEM, os.path.join(OUT, code)], capture_output=True, text=True, timeout=max(20, left - 3))
        tail = (p.stdout + p.stderr).strip().splitlines()[-3:]
        ok = p.returncode == 0 and any(l.startswith('DONE ok') for l in tail)
        rec.update(ok=ok, seconds=round(time.time() - t1, 1), tail=tail)
        if ok:
            j = json.load(open(os.path.join(OUT, code, f'hongdal-land-{code}.json'), encoding='utf-8'))
            rec.update(mesh_bytes=j['mesh']['bytes'], vertices=j['mesh']['vertices'], houses=len(j['houses']),
                       areas={k: round(v / 1e6, 3) for k, v in j['areas'].items() if isinstance(v, (int, float))},
                       failed=[c['name'] for c in j['checks'] if not c['ok']])
    except subprocess.TimeoutExpired:
        rec.update(ok=False, seconds=round(time.time() - t1, 1), error='시간 초과')
    status['dongs'][code] = rec; ran += 1
    print(code, rec['name'], rec.get('ok'), rec.get('seconds'), rec.get('error', ''))
    json.dump(status, open(status_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

okc = sum(1 for c in queue if done(c))
status['progress'] = f'{okc}/{len(queue)}'
json.dump(status, open(status_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('progress', status['progress'], 'ran', ran, 'elapsed', round(time.time() - T0, 1))
