# 서울 중간 공간 로컬 검토 (2026-09-23)

실제 `SimulationWorldShell` Game View 캡처다. 최종 디자인·최신 경계·운영 완료 증거가 아니다.

- `02-seoul.png`: 서울25구. 최종 depth500 수정 후 Play 재실행 캡처.
- `03-jungnang.png`: 중랑구16행정동. 지명 겹침과 북쪽 일부 잘림 남음.
- `04-station.png`: 사가정 역세권 확대.
- `05-diorama.png`: 기존 사가정 디오라마. 서버 미연결 업무 결과0.
- `01-peninsula.png`: 초기 한반도 구면 단계 캡처. 프레이밍 미완성 참고용.

Play에서 `Tools/Mirror/서울 중간 공간 검토 사본 열기`로 Hongdal의 `artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1/seoul-preview.json`을 선택한다. 사본은 배포 자산에 포함하지 않으며 Play마다 다시 연다. 원본2023-10-31, 서울특별시·서울신용보증재단/공공누리1유형, 북쪽+Z·1단위1km.

신규 시험4/4·지구본22/22 통과. 실제 View 함수 호출로 계층 및 사가정 진입/복귀 확인. 물리 마우스 입력·정식 빌드 미검증. 기존 서버 연결·Replay 오류와 시험 도구 콜백 오류 존재; Console0 아님. Scene 저장·commit·push 없음.

정본 기록: Hongdal `docs/AI/Planning/시스템/PLAN-SYSTEM-GLOBE-FISHERIES-OBSERVATION/seoul-hierarchy-preview.implementation.r26.md`.
