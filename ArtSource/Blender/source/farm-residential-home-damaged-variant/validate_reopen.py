"""Validate the saved blend and exported FBX after reopening the project file."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


PROJECT_ID = "farm-residential-home-damaged-variant"


def args_after_separator() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def main() -> None:
    args = args_after_separator()
    if len(args) != 1:
        raise SystemExit("usage: blender <blend> --background --python validate_reopen.py -- <validation-dir>")
    validation_dir = Path(args[0]).resolve()
    result_path = validation_dir / "result.json"
    result = json.loads(result_path.read_text(encoding="utf-8"))
    required_objects = {
        "VARIANT_SM_Bld_Farmhouse_02",
        "Damage_RoofCavity",
        "Damage_RoofCavity_Secondary",
        "Damage_RoofCavity_Tertiary",
        "Damage_RoofCavity_MarkedCenter",
        "Damage_RoofCavity_MarkedRight",
        "Damage_RoofCavity_MarkedLower",
        "Damage_LooseRoofBoard_01",
        "Damage_LooseRoofBoard_02",
        "Damage_LooseRoofBoard_03",
    }
    actual_objects = set(bpy.data.objects.keys())
    missing = sorted(required_objects - actual_objects)
    if missing:
        raise RuntimeError("blend reopen missing objects: " + ", ".join(missing))
    scene = bpy.context.scene
    if scene.get("projectId") != PROJECT_ID:
        raise RuntimeError("projectId missing after blend reopen")
    if scene.get("supplierOriginalMutationAllowed") is not False:
        raise RuntimeError("supplier original mutation policy is not preserved")
    export_path = Path(result["outputs"]["fbx"])
    if not export_path.is_file() or export_path.stat().st_size == 0:
        raise RuntimeError("FBX export is missing or empty")
    for render_key in ("before", "after", "closeup"):
        render_path = Path(result["outputs"][render_key])
        if not render_path.is_file() or render_path.stat().st_size == 0:
            raise RuntimeError(f"render is missing or empty: {render_key}")
    result["status"] = "BlenderValidatedCopy"
    result["reopenValidation"] = {
        "passed": True,
        "blendVersion": bpy.app.version_string,
        "requiredObjects": sorted(required_objects),
        "fbxBytes": export_path.stat().st_size,
    }
    result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print("FARM_RESIDENTIAL_HOME_DAMAGED_VARIANT_REOPEN_VALID: PASS")


if __name__ == "__main__":
    main()
