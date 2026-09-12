# 사가정역 생활 디오라마 r3

2026-09-12, 커밋 전. canonical `SimulationWorldShell`의 `OperationalOsWorldRoot`에서 기존 사가정역 1km 동결 지리 자료와 여섯 가상 완료 업무를 정적으로 관찰한다. 실제 GPS·실존 업소·움직이는 차량을 뜻하지 않는다.

- [최종 전체 UI](game-frame-layout-final.png) · [상세 UI](game-frame-layout-detail-final.png) · [귀환 UI](game-frame-layout-return-final.png): 실제 ScreenCapture이며 OnGUI를 포함한다. 안내 카드·기존 시점 버튼·업무 라벨 겹침 해소를 확인했다.
- [전체 지도](whole-final.png)
- [업무 근접 보기](detail-final.png)
- [전체 귀환](return-final.png)

지도 세 화면은 Play Mode에서 관찰 카메라를 명시한 렌더링이며 OnGUI는 포함하지 않는다. 검증 메뉴로 전체·근접·귀환을 호출했으며 실제 마우스 입력을 대신하지 않는다. 전체·상세·귀환에서 지도 Renderer 5개, 업무 6건과 관찰 카메라 1개를 확인했다. 검증 종료 후 Editor 정지·Scene clean이며 캡처 중 Scene 저장과 소스 변경은 없었다.

관련 EditMode 41/41, Editor 컴파일 오류 0. 지도/배치/라벨 7개·운영 관찰/재활성화 4개·주소 비공개 회귀 4개·표시 정책 26개다. 지도와 상태 사본의 수명은 분리하며 만료 업무 제거·위치 미연결 차단·개별 객체 동결/회복을 유지한다.

승인·구현 기준은 Hongdal 저장소의 `docs/AI/Planning/시스템/PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001/diorama.r3.md`와 `implementation.r3.md`다. 생성 콘셉트 수준의 세부 자산 시각 마감, 이동 애니메이션, 실제 운영과 Player build는 범위 밖이다. 원본 시험·중간 실패 캡처는 로컬 artifacts에 보존하며 E 승격·commit·push는 하지 않았다.
