# CMP3 도로·Gate Composition A형

## 변경 화면

![도로와 Gate Composition Library](road-gate-library.png)

## 범위

- Farm·Town·City 직선·모서리·T자·십자 도로 A형 12개
- Farm↔Town·Town↔City 사람·차량 경계 Gate 4개
- Farm→Hub·Town→Hub·Hub→City 화물 Gate 6개
- 주황 marker는 외부 확장 connector, 파랑 marker는 Region 안쪽 connector
- Farm 도로는 CMP2 실측값 11.9106m와 `-0.9553m` adapter offset을 사용

## 권위 경계

도로·Gate prefab은 Synty 환경 외형, connector와 Presentation socket만 가진다. 사람·차량 도착이나 connector 통과는 Simulation·Operational 상태를 확정하지 않는다.

## 검증

- builder 연속 2회 실행 뒤 prefab 수 `22 → 22`
- CMP3 집중 EditMode `6/6`
- 전체 EditMode `82/82`
- Town 십자도로 5개 tile의 수평 면적 중첩 없음과 prefab 90도 회전 시 connector 동반 회전 확인
- Console Error `0`
- Preview Scene 저장 후 dirty `false`
