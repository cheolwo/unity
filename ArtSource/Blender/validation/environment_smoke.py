# SPDX-License-Identifier: GPL-3.0-or-later
"""D358 환경 시험: 공급사 파일 없이 독립 도형의 저장/FBX 왕복만 검사한다."""
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def snapshot(obj):
    corners = [obj.matrix_world @ Vector(point) for point in obj.bound_box]
    return {
        "vertices": len(obj.data.vertices),
        "polygons": len(obj.data.polygons),
        "minimum": [min(point[axis] for point in corners) for axis in range(3)],
        "maximum": [max(point[axis] for point in corners) for axis in range(3)],
    }


def equivalent(expected, actual):
    assert expected["vertices"] == actual["vertices"]
    assert expected["polygons"] == actual["polygons"]
    for key in ("minimum", "maximum"):
        assert all(abs(a - b) < 0.0001 for a, b in zip(expected[key], actual[key])), (key, expected, actual)


args = sys.argv[sys.argv.index("--") + 1:]
assert len(args) == 1, "새 출력 폴더 한 개를 지정해야 한다."
output = Path(args[0]).resolve()
allowed = Path("C:/Users/user/source/repos/Hongdal/artifacts/local/blender-setup").resolve()
assert output.is_relative_to(allowed) and output != allowed
output.mkdir(parents=True, exist_ok=False)
blend_path = output / "environment_probe.blend"
fbx_path = output / "environment_probe.fbx"

# 이 프로세스의 독립 메모리 장면만 초기화하며 외부 프로젝트를 열지 않는다.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0
bpy.ops.mesh.primitive_cube_add(size=1, location=(0.5, 1.0, 1.5))
probe = bpy.context.active_object
probe.name = "EnvironmentProbe"
probe.scale = (1.0, 2.0, 3.0)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bpy.context.view_layer.update()
expected = snapshot(probe)
assert bpy.ops.wm.save_as_mainfile(filepath=str(blend_path)) == {"FINISHED"}
assert bpy.ops.wm.open_mainfile(filepath=str(blend_path)) == {"FINISHED"}
equivalent(expected, snapshot(bpy.data.objects["EnvironmentProbe"]))
options = dict(use_selection=True, object_types={"MESH"}, global_scale=1.0,
               apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
               axis_forward="-Z", axis_up="Y", bake_anim=False,
               add_leaf_bones=False, use_mesh_modifiers=True)
assert bpy.ops.export_scene.fbx(filepath=str(fbx_path), **options) == {"FINISHED"}
assert fbx_path.stat().st_size > 0
bpy.ops.wm.read_factory_settings(use_empty=True)
assert bpy.ops.import_scene.fbx(filepath=str(fbx_path)) == {"FINISHED"}
bpy.context.view_layer.update()
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
assert len(meshes) == 1
actual = snapshot(meshes[0])
equivalent(expected, actual)
assert len(bpy.data.actions) == 0
assert not any(obj.type == "ARMATURE" for obj in bpy.context.scene.objects)
result = {
    "status": "PASS", "scope": "synthetic_mesh_blend_fbx_roundtrip_only",
    "version": bpy.app.version_string,
    "build_hash": bpy.app.build_hash.decode(),
    "user_config": bpy.utils.user_resource("CONFIG"),
    "expected": expected, "roundtrip": actual,
    "export_options": {key: sorted(value) if isinstance(value, set) else value for key, value in options.items()},
    "files": [{"path": str(path), "bytes": path.stat().st_size,
               "sha256": hashlib.sha256(path.read_bytes()).hexdigest()} for path in (blend_path, fbx_path)],
    "unity_import_tested": False, "rig_animation_tested": False,
}
with (output / "result.json").open("x", encoding="utf-8") as stream:
    json.dump(result, stream, indent=2, ensure_ascii=False)
print("D358_ENVIRONMENT_SMOKE_PASS " + json.dumps(result, ensure_ascii=False))
