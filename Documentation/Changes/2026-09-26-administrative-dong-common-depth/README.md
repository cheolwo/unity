# 30개 행정동 공통 깊이 · 실제 Game View 검토

- Unity: `6000.5.6f1`
- canonical Scene: `Assets/Ssalddel/Scenes/SimulationWorldShell.unity`
- 입력 scope: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`
- 입력 generation: `C6CD2501CD2FC876BFF671FB25C36C139AFDAF14F78EC2FD3F7503E31A1153AC`
- 입력 index SHA-256: `C8D4999494767E9588E998100893A2D4227A8E116DEBD8F8B81611BA94D3E123`
- 캡처 원장 SHA-256: `844F6EF27C1B7A6A1F23DF20E22931D195B5A0648234FDC04E90B3F3C55CF0D7`

`modules/`에는 정확한 30개 행정동의 전체 조망 Game View가 있다. 루트의 `31-1126057500-myeonmok-3-8-detail.png`는 면목제3·8동의 한 건물을 도형으로 선택한 근접 화면이다. 모든 화면 파일은 `capture-manifest.json`의 SHA-256과 다시 대조했다.

이번 검토에서 확인한 것은 역사 경계 bootstrap의 경계·건물·도로 기초 표현이다. 30개 동 모두 결합 Mesh 4개와 Collider 0개로 조립됐고, Scene 파일은 저장하지 않았으며 실행 전후 SHA-256이 같다. `current` pointer, 서버 운영 상태, 통행·게임플레이·배포 권한은 변경하지 않았다.

화면의 공통 깊이 슬롯은 여덟 자리를 모두 표시하지만 현재 근거가 있는 비공개 검토 범위는 자료 근거·기본 공간·도형 선택 세 자리다. 현행 행정동 경계, 주소·필지·출입구, 보행·차량 통행, 생활 Simulation, 사업장 공개 overlay와 환경 표현은 검증 완료 상태가 아니다.

캡처 중 canonical Scene의 다른 구성요소에서 Console 오류 8건이 발생했다. 종류는 replay hash 불일치, 로컬 서버 미실행에 따른 session/request 실패, 다른 World 표현과 종료 정리 오류다. 행정동 검토 코드 stack을 가진 오류는 0건이지만 전체 Scene 오류 0 증거로 사용하지 않는다. 실제 마우스 입력도 별도 검증 대상으로 남긴다.

대표 화면:

- `modules/20-1126057500-overview.png`: 면목제3·8동 전체 조망
- `31-1126057500-myeonmok-3-8-detail.png`: 면목제3·8동 건물 선택 근접
- `modules/18-1126056500-overview.png`: 첫 다른 동 비교 대상인 면목본동
