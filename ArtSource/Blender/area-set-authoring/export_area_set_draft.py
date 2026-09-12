"""열린 Blender 초안의 수평 변환만 비권위 배치 Draft JSON으로 되돌린다."""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy


def preserve_if_numerically_equal(value: float, original: float, epsilon: float = 0.0001) -> float:
    """FBX/Blender 부동소수점 왕복 잡음은 원래 계약값으로 되돌린다."""
    return original if abs(value - original) <= epsilon else round(value, 6)


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) != 2:
        raise SystemExit("usage: export_area_set_draft.py -- <source-draft.json> <output-draft.json>")
    source_path = Path(args[0]).resolve()
    output_path = Path(args[1]).resolve()
    draft = json.loads(source_path.read_text(encoding="utf-8-sig"))
    roots = {
        obj.get("area_set_placement_stable_id"): obj
        for obj in bpy.context.scene.objects
        if obj.get("area_set_placement_stable_id")
    }
    for placement in draft["Placements"]:
        if not placement["Enabled"]:
            continue
        root = roots.get(placement["PlacementStableId"])
        if root is None:
            raise RuntimeError("AREA_SET_PLACEMENT_ROOT_MISSING:" + placement["PlacementStableId"])
        expected_z = float(root["area_set_expected_blender_z"])
        if abs(root.location.z - expected_z) > 0.001:
            raise RuntimeError("AREA_SET_DIRECT_HEIGHT_EDIT_REJECTED:" + placement["PlacementStableId"])
        scales = tuple(float(value) for value in root.scale)
        if max(scales) - min(scales) > 0.001 or min(scales) <= 0.0:
            raise RuntimeError("AREA_SET_NON_UNIFORM_SCALE_REJECTED:" + placement["PlacementStableId"])
        placement["LocalX"] = preserve_if_numerically_equal(
            float(root.location.x), float(placement["LocalX"])
        )
        placement["LocalZ"] = preserve_if_numerically_equal(
            float(root.location.y), float(placement["LocalZ"])
        )
        placement["RotationYDegrees"] = preserve_if_numerically_equal(
            -math.degrees(float(root.rotation_euler.z)), float(placement["RotationYDegrees"])
        )
        placement["UniformScale"] = preserve_if_numerically_equal(
            sum(scales) / 3.0, float(placement["UniformScale"])
        )
    draft["DraftRevision"] = draft["DraftRevision"] + "-blender"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(draft, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
