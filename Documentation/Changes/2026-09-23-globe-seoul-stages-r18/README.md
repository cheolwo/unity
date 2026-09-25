# 지구본→한반도→서울 실제 Game View r18

2026-09-23, canonical SimulationWorldShell Play Mode. 같은 실행에서 UI 포함 ScreenCapture로 순서대로 촬영했다. 물리 마우스 입력이 아니라 기존 카메라/선택 API를 호출했다.

1. `01-globe.png`: 한반도 초기 자세, 카메라 거리53. 자동 자료 준비 Ready.
2. `02-peninsula.png`: 거리32, 안정된 의미 확대 처리, TerrainZoomBlend=1, IsTerrainPreviewVisible=true. 사진형 지형 금지 유지.
3. `03-seoul.png`: CanEnterFromGlobe=true 확인 후 ShowLevel(0). IsCurved=true, 지역89개, 서울 표시. 같은 지구본 Camera.

촬영 중 자동 회전으로 위치가 달라지는 것을 막기 위해 상호작용 타이머를 120초 유예했다. 제품 기본 자동회전 설정은 바꾸지 않았다. 지구본 복귀 후 Play 종료, Scene 저장 없음.

발견: 한반도 단계의 촘촘한 위경도선·안내 패널이 시야를 가린다. 한반도에서 서울까지 확대 폭이 크며 중간 광역 계층이 부족하다. 서울 단계 지명은 겹침 방지로 일부만 보이고 경계선은 가늘다. 정지 화면은 연속 이동의 자연스러움이나 물리 입력 완주를 증명하지 않는다. Console 오류 0 미검증. 제품 코드 변경·commit·push 없음.
