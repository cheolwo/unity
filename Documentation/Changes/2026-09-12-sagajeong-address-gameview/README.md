# 사가정 주소·높이 표현 재검증

2026-09-12 Unity6000.5.6f1, canonical SimulationWorldShell, 실제 Play Mode/1399×628 Game View의 OnGUI 포함 화면이다.

01~05: 전체→도로명 클릭→11쪽 마지막 페이지→건물번호 선택→확대/이동 후 전체 복귀.
06~07: 직접 선택 API로 높이후보8.3m/19.1m 대상 확인. 실행 Mesh도 대조했다.
08~09: 합성 GUI 입력으로 방어 준비/운영 관찰 전환 시 도로명 UI 분리 확인.

입력은 EditorGUIUtility.QueueGameViewInputEvent이며 물리 마우스/터치 검증은 아니다. 관련 EditMode72/72, 컴파일 오류0. 업무 상태는 Success/0건이며 여섯 업무의 새 실행은 하지 않았다. 다른 영역의 기존 서버/Replay 오류를 전체 정상으로 덮지 않는다. 검증 후 Play 정지·Scene clean, 저장 Scene hash 불변. 커밋/푸시 없음.

상세: Hongdal의 docs/Changes/2026-09-12-sagajeong-address-gameview.md.
