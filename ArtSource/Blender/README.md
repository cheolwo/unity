# Blender 제작 환경과 프로젝트 소유 파생 자산 흐름

이 폴더는 Unity `Assets` 밖의 편집 원본·전달 대기·검증 경로다. D358 환경 준비만 승인되었으며 실제 캐릭터나 동작 제작은 아직 시작하지 않았다.

## 경로와 소유

- `source/`: 승인 후 작성할 편집 원본 `.blend`. 공급사 원본을 덮어쓰지 않는다.
- `exports/`: 승인 후 내보낼 FBX 대기 경로. 여기에 놓는 것만으로 Unity에 반입되지 않는다.
- `validation/`: 환경 시험과 검증 안내. 실제 시험 산출물은 Hongdal `artifacts/local/blender-setup/`에 둔다.
- `workflows/`: Synty 등 공급사 원본에서 프로젝트 소유 변형을 만드는 작업 명세와 예시.
- `tools/`: 작업 명세의 원본 불변·출력 격리·계보 필드를 검사하는 도구.
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

## 프로젝트 소유 변형의 기본 순환

Synty 자산은 먼저 그대로 재사용할 수 있는지와 Unity 조립만으로 충분한지를 확인한다. Blender는 실루엣·손상 상태·부품·동작 접촉처럼 메시나 키프레임 수정이 실제로 필요한 작은 차이에만 사용한다.

```text
공급사 원본 읽기 전용 고정
  → 원본 경로·GUID·SHA-256·구매/약관 근거 기록
  → 프로젝트 전용 .blend 복사본
  → 명세에 허용된 작은 변경만 수행
  → .blend 저장·재열기와 외곽/축/단위/재질/Rig 검사
  → 별도 FBX 내보내기
  → Unity 프로젝트 소유 후보 경로에서 Import 검사
  → 실제 WI·H·VisualKey 결속과 Game View 검증
```

작업 하나는 `workflows/synty-project-owned-variant.template.json`에서 시작한다. 기존 기획에서 Blender 가능성을 남긴 항목은 현행 `workflows/planned-variants.r2.json`에서 실제 제작 명세와 구별해 조회하며, r1은 이전 판본의 읽기 호환 자료로 보존한다. `tools/validate_variant_manifest.py`는 출력이 `Assets/Synty` 또는 원본 위치를 가리키는지, 원본 hash·기획/WI/H/VisualKey 계보와 변경 예산이 빠졌는지를 검사한다. 이 검사는 모델 품질이나 구매 권리, Unity Import와 실제 화면을 대신하지 않는다.

`validation/run_variant_pipeline_smoke.ps1`는 공급사 파일을 사용하지 않는 저다각형 대체 도형으로 원본 불변 복사·작은 메시 변경·`.blend` 저장/재열기·FBX 내보내기를 확인한다. 표본 성공은 작업 흐름의 도구 검증일 뿐 Synty 자산 가공 승인이나 결과물이 아니다.
