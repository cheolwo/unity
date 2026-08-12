# 턴 카드 실제 시뮬레이션 서버 연결

## 결과

`SimulationWorldShell`의 턴 마감 권위를 로컬 Fixture에서 `Ssalddel.Simulation.Server`의 공식 context·Preview·Confirm API로 교체했다. Scene의 `턴마감SceneCompositionRoot`가 서버 사용 여부를 명시하며, 서버 모드에서는 다음 순서로만 상태를 바꾼다.

1. 개발용 Simulation session을 확보한다.
2. 서버의 현재 턴 context를 조회한다.
3. 선택한 카드로 턴 마감 Preview를 요청한다.
4. 사용자의 명시적 Confirm을 서버에 전송한다.
5. Confirm 응답만 화면에 적용하지 않고 canonical session을 다시 조회한다.
6. session stable ID, revision 증가, 완료 Tick과 다음 턴을 검증한 뒤 WorldShell snapshot을 교체한다.

Fixture 경로는 테스트·명시적 오프라인 검증용으로 분리해 남겼으며 서버 오류를 Fixture 성공으로 숨기지 않는다.

## 실제 실행 확인

- 서버: `http://localhost:5104`의 Development Simulation 서버
- session: `simulation-session:706a236b17e544e2a070a0785ae42d19`
- Confirm 전: 2026-04-12, Tick 0, Revision 0
- 서울 생활문화 질문 카드 Confirm 후 canonical 재조회: 2026-04-13, Tick 1, Revision 1
- 다음 턴 효과: `LocalContextAwareness`
- 골든 패스 구간 Console 오류: 0건
- 턴 마감 집중 EditMode: 5/5 통과
- 전체 EditMode: 219/220 통과. 실패 1건은 기존 연구 Scene 기대 개수 27과 현재 28의 불일치다.

![서버 연결 초기 화면](server-context.png)

![문화 카드 Confirm 뒤 다음 날](server-culture-next-day.png)

## 검증 경계

이번 연결은 Development 환경의 메모리 기반 Simulation 서버를 실제 HTTP로 호출한 증거다. 운영 계정 인증, 영속 DB session, 서버 재시작 뒤 복구, 실제 문화 행사 publication이나 운영 상태 변경을 증명하지 않는다. session 자동 생성도 개발용 Simulation bootstrap에만 한정한다.
