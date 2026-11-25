# 홍달 모바일 경영 화면

모바일 가로 화면에서 지도와 기사·매장 상태를 표시하는 소비 모듈이다. `MobileGameEntry`가 내장 코어·전용 저장·화면·파일 교환을 연결한다. 기존 공식 Scene과 ProjectSettings는 보존하고 Packages에는 공통 코어 의존성 한 개를 추가했다. UI와 지도는 같은 로컬 회차의 상태 사본만 읽으며 운영 API·외부 프로세스를 사용하지 않는다.

실행 소유자가 `MobileGameBootstrap.Build()`로 화면을 조립하고, 반환된 `MobileGameController.AttachSession(IMobileGameSession)`에 로컬 회차 서비스를 연결한다. 인터페이스는 JSON 요청·응답과 `Changed` 사건, 현재 응답 사본만 제공한다. 시계·백그라운드 정지·저장·복원은 세션 서비스가 맡는다.

조작은 운영 시작 → 기사 탭 → 주문 매장 탭 → 매장 도착 → 업무 연습의 픽업 확인 → 전달·수령·가상 결산이다. 한 손가락 끌기는 지도 이동, 두 손가락은 확대·축소다. 패널 위 터치는 지도로 전달하지 않고 뒤로 가기는 먼저 결산·상세 패널을 닫는다. 순수 입력 회귀는 `MobileTouchStateChecks.Run()`이다.

## 시각 자료 출처

- 지도 표면·bounds는 2026-11-11 기존 `hongdal-unity-operator-expansion-20261111/project/Assets/SagajeongDispatch/Resources/SagajeongDispatch/city-board/`의 파일을 수정 없이 재사용했다. 표면은 기존 공개 OSM 도로·건물 표본의 게임용 2D 표현이다. © OpenStreetMap contributors, ODbL 1.0: https://www.openstreetmap.org/copyright
- 오토바이는 같은 검토판 `motorcycle/rider-b.png`를 수정 없이 재사용했다. 새 이미지 생성은 하지 않았다.
- 도로·매장·주문·배송지는 서비스가 주는 현재 회차 표본을 사용한다. 주문·배송지는 합성 사례이며 실제 고객·현장·교통 상태가 아니다.
- 한국어 폰트는 재배포 조건을 확인한 `Resources/HongdalMobile/Korean` 또는 `ConfigureFont(Font)`로 공급한다. 개발 PC의 설치 폰트 탐색은 검토용 보조이고 Android 한국어 표시 검증을 대체하지 않는다.

`Resources/HongdalMobile`의 배달·도로 JSON과 NOTICE를 함께 유지한다. Nanum Gothic 정규체는 Google Fonts 원본을 수정 없이 사용하며 Korean-OFL.txt와 Korean-NOTICE.txt에 출처·SIL OFL 1.1을 보존한다.

공통 코어 52검사·기존 관련 70회귀·Windows 엔진 동등성 54명령과 모바일 비율 Windows 검토판 43항목을 확인했다. 실제 Android 빌드·터치·SAF·IL2CPP 재생은 미완료다. Android Build Support와 설치 공간이 준비되면 같은 소스를 Android ARM64로 빌드해 등록 기기에서 마감한다. 원본 업무 앱·계정·현실 원장을 게임으로 교체하지 않는다.
