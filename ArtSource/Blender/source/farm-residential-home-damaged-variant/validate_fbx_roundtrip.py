"""Round-trip the project-owned FBX in a clean Blender scene."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args_after_separator() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def main() -> None:
    args = args_after_separator()
    if len(args) != 2:
        raise SystemExit("usage: blender --background --python validate_fbx_roundtrip.py -- <fbx> <validation-dir>")
    fbx_path = Path(args[0]).resolve()
    validation_dir = Path(args[1]).resolve()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path), use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) < 4:
        raise RuntimeError(f"expected at least 4 mesh objects after FBX round-trip, found {len(meshes)}")
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    dimensions = high - low
    if min(dimensions) <= 0.0 or max(dimensions) > 100.0:
        raise RuntimeError(f"unexpected FBX round-trip bounds: {tuple(dimensions)}")
    result_path = validation_dir / "result.json"
    result = json.loads(result_path.read_text(encoding="utf-8"))
    result["fbxRoundTripValidation"] = {
        "passed": True,
        "meshObjectCount": len(meshes),
        "boundsMin": list(low),
        "boundsMax": list(high),
        "dimensions": list(dimensions),
        "objectNames": sorted(obj.name for obj in meshes),
    }
    result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print("FARM_RESIDENTIAL_HOME_DAMAGED_VARIANT_FBX_ROUNDTRIP: PASS")


if __name__ == "__main__":
    main()
