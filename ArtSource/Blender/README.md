# Blender 제작 환경 — D358

이 폴더는 Unity `Assets` 밖의 편집 원본·전달 대기·검증 경로다. D358 환경 준비만 승인되었으며 실제 캐릭터나 동작 제작은 아직 시작하지 않았다.

## 경로와 소유

- `source/`: 승인 후 작성할 편집 원본 `.blend`. 공급사 원본을 덮어쓰지 않는다.
- `exports/`: 승인 후 내보낼 FBX 대기 경로. 여기에 놓는 것만으로 Unity에 반입되지 않는다.
- `validation/`: 환경 시험과 검증 안내. 실제 시험 산출물은 Hongdal `artifacts/local/blender-setup/`에 둔다.
- 휴대용 실행 파일: `C:/Users/user/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe`.
- 실행 파일 옆 `portable/`에 사용자 설정을 격리한다. 기본 파일 연결·전역 PATH·Unity Packages는 바꾸지 않는다.

상세 확인 결과와 공식 근거는 `C:/Users/user/source/repos/Hongdal/docs/Reports/Blender환경준비-D358-2026-08-30.md`를 따른다. 해당 보고서와 이 경로의 전문 결과는 먼저 개발 통합 담당에게 반환한다.

## 실제 제작을 시작하기 전

1. 개발에서 대상 WI·승인 기획 revision/hash·배우/도구 원본 경로/GUID/hash·독립 수정 경로를 결속한다.
2. 구매 채널·구매 당시 Synty 약관·팀 사용 범위를 확인한다. 일반 Blender 편집 허용을 AI 재가공 허용으로 해석하지 않는다. 외부 AI 업로드·공급사 자산 자동 재가공은 시작하지 않는다.
3. 동작 길이·손잡이 접촉·타격 시점·이동 허용·중단/Idle 복귀·반복·root motion·뼈 작성자 하나라는 수용 기준을 승인받는다.
4. `.blend`는 원본 계보와 함께 보존하고 FBX는 별도 판본으로 내보낸다. 승인 없는 기존 FBX나 `.meta` 교체는 금지한다.

## 전달 검증 경로

내보내기마다 Blender 버전, 원본/FBX SHA256, 단위·축·배율, 선택 객체, 뼈 계층, bind pose, Action/Clip 이름·프레임/FPS, 애니메이션 bake 옵션을 기록한다. 이 폴더의 시험 옵션은 **도형 환경 시험용**이지 캐릭터용 확정 preset이 아니다.

먼저 Blender에서 다시 열어 크기·방향·형태·뼈·동작 구간을 확인한다. 이후 개발이 별도 승인한 프로젝트 소유 Unity 경로에 전달해 Rig/Avatar, Clip 실제 재생, root motion, 접촉, 중단/복귀와 권위 상태 불변을 검증한다. Editor 점유·반입 경로·기존 Scene dirty 상태는 담당과 조율한다. Game View/보조 Scene View는 공간 담당→개발 경로로 요청한다.

`.blend` 저장, FBX 생성 또는 Blender 내 재읽기 성공은 Unity 호환·Humanoid 검증·실제 게임 동작·E 승격 증거가 아니다.
