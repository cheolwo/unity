# 행정 경계 r15 실제 화면 확인

SimulationWorldShell Play Mode에서 기존 검토 사본을 hash 검증 후 로드했다.
ShowLevel(0), ShowDistrict("11260")를 API로 호출하고 Unity ScreenCapture로 UI를 포함한 Game View를 저장했다. 마우스 클릭 검증은 아니다.

- seoul.png: 서울 25개 구. 지명 버튼이 겹친다.
- jungnang.png: 중랑구 16개 행정동 표시. 지명 버튼 겹침이 남는다.

총 89개 영역 자료(서울 25개 구 + 4개 구의 64개 동). 전국 경계/곡면 연결 완료가 아니다. Console 오류 0 검증은 하지 않았다. 캡처 후 Play를 종료했으며 Scene 저장·commit·push 없음.
