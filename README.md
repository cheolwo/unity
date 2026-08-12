# Ssalddel Unity

농장에서 수확한 생산물이 물류 거점과 판로를 거쳐 도시로 이어지는 과정을, 추적 가능한 상태와 상호작용으로 표현하는 Unity 월드 프로젝트입니다.

![현재 진행 중인 감자 수확물 판로 선택 Game View](Documentation/Changes/2026-08-11-harvest-route-multi-lot/harvest-route-multi-lot-selection.png)

_현재 대표 화면 — 감자 Harvest Lot 300kg의 판로 선택과 작업 예약을 보여주는 Simulation Game View_

## 현재 구현 범위

- 감자 재배·수확부터 포장·상차, 물류 거점 입고·검수, 판로 분배와 도시 도착까지의 연구 Scene
- 생산자 조합 출하, 온라인 직접 판매, 비축 보관, 외부 교역 준비의 명시적 판로 선택
- 서버 revision과 WorldTick을 기준으로 한 Preview → Confirm → 적용 흐름
- 정보 패널의 접기·펼치기·닫기·다시 열기 상호작용
- `감자생산유통`, `생산자판로`, `에셋연구` 맥락별 Scene 구성

## 프로젝트 경계

현재 화면과 데이터는 개발용 Simulation입니다. 실제 판매, 결제, 배차, 수출, 정산을 실행하지 않으며 운영 상태의 최종 권위는 서버에 둡니다.

진행 과정과 Game View 기록은 [`Documentation/Changes`](Documentation/Changes/README.md)에서 확인할 수 있습니다.
