# AreaSet Blender 배치 초안

이 도구는 Blender를 배치 권위로 사용하지 않는다. Unity가 내보낸 실제 지표면 사본과 Prefab 교정값을 읽어 편집 가능한 E4 초안을 만들고, Blender에서는 수평 위치·Y축 회전·균일 축척만 바꾼다.

## 순환

1. Unity 메뉴 `Ssalddel/AreaSet 배치 초안/1. Unity 지형·자산 사본 내보내기`를 실행한다.
2. `render_area_set_draft.py`로 `.blend`와 조감도를 만든다.
3. Blender에서 배치 객체의 X/Y 위치, Z 회전, 균일 축척만 편집한다. Blender Z는 잠겨 있으며 Unity 높이는 지표면과 Prefab 바닥값으로 다시 계산한다.
4. 저장한 `.blend`를 연 상태에서 `export_area_set_draft.py`로 새 초안을 내보낸다.
5. Unity 메뉴 `2. 현재 초안 검사·staging·비교 화면`을 실행한다. 지형·자산 판본, 접지, 경사, 높이 편차, 중첩과 통행 여유가 통과한 초안만 staging Prefab이 된다.

`SurfaceOffset`은 교정 대장에서 허용된 자산과 명시적인 사유가 있는 경우에만 쓸 수 있다. 지형을 자동 평탄화하거나 실패한 객체를 임의로 이동하지 않는다. Blender PNG와 staging PNG는 정합성 비교 자료이며 canonical `SimulationWorldShell`의 실제 Game View 또는 E5 증거가 아니다.
