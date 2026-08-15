# 진부면 입고 검수–적재 정보판

## 적용 범위

- Figma `05P1 Warehouse`와 MAUI 창고 화면의 역할 띠, 상태 배지, 요약·근거·다음 단계, Preview·Confirm 문법을 Unity World용 오른쪽 정보판으로 재구성했다.
- 서버 `SimulationWorldUIProjection`의 디자인 프로필, 역할·상태·정보·행동 의미 키를 Unity Theme Catalog가 해석한다.
- 저장된 `SimulationWorldShell`은 Simulation 서버를 권위로 사용하며 검수·적재 Preview, 명시적 Confirm, WorldTick, canonical 정보판 재조회만 수행한다.
- 네트워크 실패는 fixture 성공으로 숨기지 않고 마지막 성공 상태와 stale 경고로 표현한다.
- 이 화면의 fixture는 실제 버튼 배선과 최종 표현을 검증하기 위해서만 명시적으로 사용했다.

## Game View

![진부면 물류 거점 적재 완료 정보판](jinbu-inbound-ui-completed.png)

오른쪽 정보판은 진부면 물류 거점의 `적재 완료` 상태, 업무 단계, 판정 근거, 상태 사본 revision·WorldTick과 네 개의 실제 UGUI 행동 버튼을 보여준다. 왼쪽 World와 NPC·시설 표현은 상태 사본의 시각화이며 실제 운영 입고 완료를 의미하지 않는다.

## 검증

- 서버 HTTP 수직 시험: 1/1 통과
- Unity EditMode: 3/3 통과
- Unity PlayMode: 저장 Scene의 실제 버튼으로 검수 미리보기→확정→WorldTick→적재 미리보기→확정→완료 1/1 통과
- Game View: 실제 Editor Play Mode에서 fixture 완료 상태 저장
- 미검증: 실행 중인 실제 Simulation 서버 Session을 이용한 Unity live HTTP 화면, 운영 API·운영 DB 효과
