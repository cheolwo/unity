# 전 세계 육지 폴리곤 r22

기존 배포 승인 WorldGlobeCountryCatalog 177개 영역·288개 폴리곤·내부 구멍1개를 1도 셀별로 잘라 원본 윤곽을 보존하는 육지 메시로 구성했다. 공유 꼭짓점37,438개·삼각형63,405개(렌더링 양면 인덱스 별도), 자료1,598,541 bytes.

원본 Natural Earth 1:110M 정확도를 높인 것은 아니며, 기존 사각 표본 육지를 벡터 윤곽대로 표현하도록 교체한 것이다. 원본에 없는 작은 섬/해안·고도는 생성하지 않는다. 국가 조회/공개 계약/정치적 경계 해석은 기존 Catalog를 유지한다.

world-land-polygons.v1은 Catalog 원본 bytes SHA256과 결속한다. 데이터 갱신 후 재생성하지 않으면 hash 불일치로 거부한다. 생성기는 미세한 원본 도형 오류3곳(country:ba, country:kp, country:rs)을 면적 변화 1e-9 제곱도 미만일 때만 정리하며 내역을 사본에 보존한다. 원본 Catalog는 수정하지 않았다.

세계 시험23/23·수도권18/18, 반복 생성 hash 동일. 실제 SimulationWorldShell Play에서 `01-asia.png`, `02-americas.png`, `03-antarctica.png`를 API-direct 카메라 조작으로 캡처했다. 수도권 별도 상세 메시 교체/복귀 시 원본 참조 복원도 확인했다. Play 종료. 물리 입력 완주·빌드·모바일 성능은 미검증. 기존 서버 연결 오류와 해양생활연출 LookRotation 경고가 남아 Console 오류0은 아니다. Scene 저장·commit·push 없음.
