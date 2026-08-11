# HarvestLot 판로 예약 작업 재개

재접속한 Unity 생산자 카드가 저장된 Preview를 복원하지 않고 Simulation session snapshot의 예약 Task를 직접 읽는다.

- `TaskStableId`, 상태, 예정 시작 Tick과 완료 Tick을 공식 session 응답에서 읽는다.
- 남은 기간은 `ExpectedEndTick - WorldTick`으로 계산한다.
- allocation이 `Reserved`이고 활성 Task가 일치할 때만 계속 진행할 수 있다.
- 오래되거나 모순된 Task snapshot은 기존 화면을 덮어쓰지 않는다.
- 진행 명령은 현재 revision과 권위 있는 남은 Tick을 기존 `/ticks` API에 전달한다.
- 실제 판매·배송·수출·정산을 실행하지 않는다.

Play Mode에서는 온라인 직접 판매 Task를 1 Tick 진행한 뒤 재접속했다. Preview가 없는 상태에서 남은 2 Tick을 복원했고, 계속 진행 후 WorldTick 15와 revision 16에서 Simulation 시장 공급 반영을 확인했다.

![재접속한 예약 Task와 남은 2 Tick](harvest-route-resume-task.png)
