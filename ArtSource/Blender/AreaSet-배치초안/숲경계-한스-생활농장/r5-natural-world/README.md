# 한스 농장+인접 숲 r5 Blender 검토

`build_blender_review.py`는 동결된 r3 plan/draft/catalog/surface를 읽어 같은 배치와 실제 FBX 형상, Route 기반 연속 흉길을 Blender에 조립한다.

- 좌표 대응: Unity `(X,Y,Z)` → Blender `(X,-Z,Y)`
- 회전 대응: Unity yaw → Blender Z 축 음수 회전
- 지형: `ContinuousScenarioTerrain` 스냅샷을 그대로 메시화하며 평탄화하지 않음
- 재질: 실제 FBX의 다중 material slot을 보존하고, Synty Trees의 정점색 전용 쉘이더는 줄기/잎 의미를 유지한 중립 검토색으로 표시한다.
- 조명: 시각 검토용 밝은 중립 조명; PNG 후처리 없음
- 증거 경계: 정적 Blender 검토 렌더이며 Game View, 실제 보행, 상호작용, 권위 상태의 성공 증거가 아님

스크립트는 `blender-overview.png`, `blender-eye-level.png`, `.blend`, `blender-render-receipt.json`을 같은 폴더에 생성한다. 기존 출력이 있으면 receipt의 `CreateNew` 관문에서 실패하므로, 재실행은 새 판본 경로를 배분받은 뒤에만 한다.
