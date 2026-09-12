# Blender 파생 자산 작업 명세

이 폴더는 공급사 자산을 직접 저장하는 곳이 아니라, 프로젝트 소유 변형 하나의 목적·계보·변경 범위·출력·검증을 선언하는 명세를 둔다.

## 상태

- `PlanningOnly`: 정확 원본이나 권리·수용 기준이 아직 결속되지 않은 계획.
- `ReadyForLocalEdit`: 원본 경로·GUID·SHA-256·근거와 전용 출력 경로가 확정되어 로컬 수동 편집을 시작할 수 있음.
- `Edited`: 프로젝트 전용 `.blend`가 생겼지만 전달 검증 전.
- `BlenderValidatedCopy`: 저장·재열기·계보·외곽·축·단위와 대상별 Rig/재질 검사를 통과함.
- `UnityImportCandidate`: 별도 FBX와 검증 기록이 준비되어 개발이 Unity Import를 검토할 수 있음.

상태는 자동 E 승격이 아니다. `UnityImportCandidate`도 실제 Prefab, Scene, WI 입력, Game View 검증 전에는 Presentation E5가 아니다.

`planned-variants.r2.json`은 상태 변화 표현 관문과 연결된 현행 후보 목록이다. 여기서 오행은 선택적인 진단 메타데이터일 뿐이며 오행별 독립 모델을 요구하지 않는다. 각 후보는 실제 WI의 시작·진행·결과와 필요한 중단·회복이 같은 Actor·사물·시설로 자연스럽게 읽히는지를 먼저 확인한다.

- 기존 Prefab·Unity 조립·재질·Animation으로 충분하면 Blender를 열지 않는다.
- Blender는 메시·피벗·실루엣·접촉·키프레임 결손이 확인된 작은 범위만 보완한다.
- `BlenderValidatedCopy`는 Presentation E4 준비 근거가 될 수 있지만 Unity Import·배치·실제 입력을 요구하는 E5는 아니다.
- `planned-variants.r1.json`은 이전 판본의 읽기 호환 자료로 보존한다.

## 작은 변경 예산

기본 `changeBudget`은 `Subtle`이다. 작업 명세의 `allowedChanges`에 적지 않은 변경은 하지 않는다.

- `CompositionOnly`: 원본 메시 수정 없이 프로젝트 소유 조립만 검토.
- `MeshProportion`: 일부 프로젝트 소유 복사 메시의 작은 비율·실루엣 조정.
- `MeshDamageState`: 구멍·깨짐·낡음 같은 명시된 상태 변형.
- `MeshAddition`: 기존 형태를 보조하는 작은 프로젝트 소유 부품 추가.
- `MaterialSlotReuse`: 기존 허용 재질 슬롯을 재배치하며 텍스처를 임의 생성하지 않음.
- `RigKeyframeAdjustment`: 승인된 기존 동작의 접촉·발 고정·복귀를 위한 제한 보정.

캐릭터 전체 재디자인, 무관한 자산 병합, 원본 덮어쓰기, 외부 AI 업로드, 자동 대량 변형은 기본 금지다.

## 시작 방법

1. 템플릿을 새 프로젝트 ID의 JSON으로 복사한다.
2. `PlanningOnly`에서 정확 원본과 근거를 채운다.
3. 검사기를 통과한 뒤에만 `ReadyForLocalEdit`로 바꾼다.
4. `.blend`, FBX, 검증 결과는 각각 명세의 전용 경로에 둔다.
5. Unity 반입은 개발 담당이 승인한 프로젝트 소유 경로로 별도 수행한다.

```powershell
& '<Blender>/blender.exe' --background --factory-startup --offline-mode --disable-autoexec `
  --python-exit-code 1 --python ArtSource/Blender/tools/validate_variant_manifest.py -- `
  ArtSource/Blender/workflows/<project-id>.json C:/Users/user/ssalddel
```

후보 목록 자체는 다음처럼 검사한다.

```powershell
python ArtSource/Blender/tools/validate_planned_variants.py ArtSource/Blender/workflows/planned-variants.r2.json .
python ArtSource/Blender/validation/planned_variants_contract_tests.py
```
