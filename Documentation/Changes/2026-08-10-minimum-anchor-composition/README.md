# CMP4 최소 거점 Composition A형

- 화면: `AnchorCompositionLibraryPreview`
- 구성: 실제 감자 6×6 필지, 타운 기본주택, 시티 공동주택 가로형, 지역 물류허브 Dock
- 주황 marker: CMP3 도로·Gate로 이어지는 route connector
- 파랑 marker: Simulation·actor·vehicle·cargo·interaction 상태 socket
- 권위 경계: 거점 prefab은 환경 표현과 socket만 소유하며 stable ID, 가격·수량, Simulation Tick, 운영 명령을 소유하지 않는다.
- 검증: 전용 EditMode 6/6, 전체 EditMode 88/88, builder 반복 생성 `4 → 4 → 4`, Preview Scene 저장·Game View 캡처 확인

![최소 거점 Composition Library](minimum-anchor-library.png)
