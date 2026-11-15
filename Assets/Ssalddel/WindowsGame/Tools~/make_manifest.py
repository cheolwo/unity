# 팩 목록(S2 팩 규격 1판)을 만든다. 파일의 SHA-256과 크기를 적는다.
# 사용: python3 -I make_manifest.py <Resources 폴더> <mesh.bytes 경로 또는 -> <out.json>
import hashlib, json, os, sys
root, mesh, out = sys.argv[1:4]
def entry(path, role, source=None):
    full = source or os.path.join(root, path)
    data = open(full, 'rb').read()
    return {'path': path, 'role': role, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
space_files = [entry('hongdal-buildings.json', 'building-index'), entry('hongdal-building-roles.json', 'building-roles'),
               entry('HongdalWindows/road-graph.json', 'road-graph')]
if mesh != '-': space_files.insert(0, entry('hongdal-diorama.bytes', 'diorama-mesh', mesh))
elif os.path.exists(out):
    # 메시가 이 자리에 없으면(기기에만 있다) 지난 목록의 메시 줄을 그대로 둔다.
    for pack in json.load(open(out, encoding='utf-8'))['packs']:
        space_files[:0] = [f for f in pack['files'] if f['role'] == 'diorama-mesh']
manifest = {
 'schema': 'hongdal-pack-manifest.v1',
 'note': '게임은 원본 자료를 직접 읽지 않고 팩만 읽는다. 회차는 시작할 때의 팩 판본에 고정한다. 상위 등록부는 neighborhood-package-registry.v2와 지역 Experience Package이며, 이 목록은 Windows 게임 모듈이 싣고 다니는 사본이다.',
 'packs': [
  {'id': 'space.seoul.jungnang.myeonmok.sagajeong', 'kind': 'space', 'version': '2026.11.12-1',
   'unit': '면목동 사가정 일대 (2.5D 자료 범위 1,089m × 1,888m)',
   'files': space_files,
   'sources': [{'name': 'OpenStreetMap', 'date': '2026-11-10', 'terms': 'ODbL 1.0 · 출처 표시'}],
   'distribution': '배포 가능 (출처 표시)', 'privacy': '개인정보 없음',
   'conversion': 'diorama-bake/bake_diorama.py가 메시와 건물 색인을 함께 굽는다. 건물 역할표는 make_roles.py가 만든다.',
   'gaps': ['법정동 경계', '횡단보도·차선', '공공 건물 자료(용도·층수)']},
  {'id': 'space.seoul.northeast-3gu', 'kind': 'space', 'version': '2026.11.13-1',
   'unit': '서울 동북 3구(중랑·동대문·광진)와 주변 · 20.7km × 11.6km · 북쪽이 위인 평면 · 세 구는 1점 = 2.5m',
   'files': [entry('hongdal-gu-base.bytes', 'gu-detail-color'), entry('hongdal-gu-base-grey.bytes', 'gu-overview-grey'),
             entry('hongdal-gu-detail-grey.bytes', 'gu-detail-grey'), entry('hongdal-gu-space.json', 'gu-space')],
   'sources': [{'name': 'ESA WorldCover 2021 v200 (N36E126)', 'date': '2021', 'terms': 'CC BY 4.0 · 출처 표시'},
               {'name': '서울시 차선 선형 A058_L', 'date': '2021 계약 기준', 'terms': '공공누리 1유형으로 기록됨 · 재확인 필요'},
               {'name': '브이월드 법정구역 AL_D001', 'date': '2026-07-01', 'terms': '이용 조건 미확인'},
               {'name': '국토교통부 표준노드링크 (data.go.kr 15025526)', 'date': '2025-05-12 갱신분', 'terms': '이용허락범위 제한 없음으로 기록됨'},
               {'name': '서울시 등고선·표고점 (열린데이터광장 OA-22241, 2023년 수치지형도)', 'date': '2025-03-20', 'terms': '공공누리 1유형 · 출처 표시'}],
   'distribution': '내부 검토 (경계와 차선의 이용 조건을 확인하기 전에는 배포하지 않는다)', 'privacy': '개인정보 없음',
   'conversion': 'make_gu_packs.py 머리말의 여섯 단계. 중간 자료(gu.js, cover.png)는 mockups/통합구조-20261112/구지도-스크립트/prep.py가 원본에서 만든다.',
   'gaps': ['지하철 노선과 역', '지명·시설 (산·하천 이름은 손으로 붙였다)', '구 전체의 건물 윤곽', '서울 밖의 지형 (음영 없음)', '세 구 밖의 촘촘한 바탕']},
  {'id': 'space.korea.nation-sea-air', 'kind': 'space', 'version': '2026.11.13-1',
   'unit': '전국(한국과 주변 바다) · 경위도 116.5~137.5°E, 29.5~43.0°N · 시안 화면 좌표(위도 1도 = 88점) · 항만 11곳·공항 3곳 × 나라 출입구 6개 × 2025년 12달',
   'files': [entry('hongdal-nation-base.bytes', 'nation-base'), entry('hongdal-nation-space.json', 'nation-space')],
   'sources': [{'name': 'Natural Earth 10m admin-1 states/provinces', 'date': '판 기록 없음 (korea-admin1-context-r1)', 'terms': 'public domain'},
               {'name': '관세청 국가별 입항물동량 (data.go.kr 15151805)', 'date': '2025.01~2025.12', 'terms': '이용 조건 재확인 필요'},
               {'name': '관세청 국가별 출항물동량 (data.go.kr 15151809)', 'date': '2025.01~2025.12', 'terms': '이용 조건 재확인 필요'}],
   'distribution': '내부 검토 (관세청 자료의 이용 조건을 확인하기 전에는 배포하지 않는다)', 'privacy': '개인정보 없음 (항만·나라·월 집계)',
   'conversion': 'Tools~/make_nation_pack.py. BL 건수와 TEU만 쓰고 중량은 단위 확인 전이라 뺀다. 항만·공항 위치, 바다 길목, 출입구 자리와 나라 묶음, 가상 회차 경로는 손으로 정했다. 바탕 PNG는 PC로 옮길 때 다시 압축되어(화소는 같음) 목록의 SHA는 모듈에 놓인 파일 기준이다.',
   'gaps': ['항만·공항 좌표·코드 대장 (지금은 대략)', '정식 해안선·섬 (Natural Earth는 시안 정밀도)', '중량 단위', '부산항·부산신항 포함 관계', "상대국 '한국' 행의 뜻", '바다 상태·입출항 사건·산지 층']},
  {'id': 'game.territory.seoul.northeast-3gu', 'kind': 'game-rules', 'version': '2026.11.12-1',
   'unit': '영역 규칙과 법정동 인접 표 (23개 동) · 홈 동의 실제 자리',
   'files': [entry('hongdal-territory.json', 'territory')],
   'sources': [{'name': '임시 규칙 (Claude 초안)', 'date': '2026-11-12', 'terms': '자체 작성'},
               {'name': '브이월드 법정구역 AL_D001에서 계산한 인접 표', 'date': '2026-07-01', 'terms': '이용 조건 미확인'}],
   'distribution': '내부 검토', 'privacy': '개인정보 없음',
   'conversion': 'make_gu_packs.py. 표식 자리는 토지피복의 시가지 칸에서 고른 가상 위치다.',
   'gaps': ['진출·운영·자리 잡음의 조건 (기획 판본이 정한다)', '가상 매장의 주문·정산', '진출 비용']},
  {'id': 'business.franchise.cafe.first-chain', 'kind': 'business', 'version': '2026.11.12-1',
   'unit': '가맹 카페 첫 인계 사슬',
   'files': [entry('hongdal-business-first-chain.json', 'first-chain')],
   'sources': [{'name': '가상 표본 (PLAN-SYSTEM-FRANCHISE-OPERATIONS r71 첫 모의 검증)', 'date': '2026-11-12', 'terms': '자체 작성'}],
   'distribution': '배포 가능', 'privacy': '개인정보 없음',
   'conversion': '손으로 적은 가상 값. 개인 배달 기록의 분포는 아직 입히지 않았다.',
   'gaps': ['메뉴·준비 시간·시간대 주문 비율의 집계 분포']},
  {'id': 'business.food-delivery.sagajeong-sample', 'kind': 'business', 'version': 'core-sample',
   'unit': '음식 배달 표본 (공통 Core)',
   'files': [entry('HongdalWindows/delivery-sample.json', 'delivery-sample')],
   'sources': [{'name': '공통 Core 표본 (Ssalddel.Sagajeong.Core)', 'date': '', 'terms': 'HongdalWindows/NOTICE.md'}],
   'distribution': '내부 검토', 'privacy': '가상 매장·가상 주문',
   'conversion': 'MobileGame 모듈의 표본을 그대로 옮긴 것', 'gaps': []},
 ]}
json.dump(manifest, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
for p in manifest['packs']:
    print(p['id'], [(f['role'], f['sha256'][:12], f['bytes']) for f in p['files']])
