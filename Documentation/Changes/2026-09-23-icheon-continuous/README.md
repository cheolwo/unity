# 이천 지구 결속 확대 검토

기존 지구본 카메라와 GlobeVisualRoot를 사용한 로컬 검토. Tools/Mirror/지구에서 이천 연속 확대에서 Hongdal artifacts의 unity-preview.json 선택. Play Mode 전용, 빌드 제외.

globe.png, peninsula-final.png, local-final.png는 지정 카메라 렌더다. region.png/local.png는 지형 음영 활성화 전 중간 결과. 전체 Game View UI 검증 아님.

API 확대0→0.4→1→0에서 초점 viewport (0.50,0.50) 유지. 직접 휠·EditMode suite 미검증. 같은 지구 위경도에 붙였지만 주변 지도 색/해상도 경계가 보이고 중간 단계 선박 기호가 너무 크다. 구/행정동 경계 결속도 후속이다. 화면 완성 판정하지 않음.

출처 Copernicus DSM/Sentinel-2는 Hongdal r8 자료 기록 참조. 실제 지형 높이 상대1배, 표시 껍질 반경은 기존 음영과 깊이 충돌 방지용으로 절대 지리 고도는 아니다. 자료/Scene 배포·저장·commit/push 없음.
