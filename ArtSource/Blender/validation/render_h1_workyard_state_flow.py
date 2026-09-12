"""Render the E4 visual state flow for the shared farm work-yard H1."""

from __future__ import annotations

import importlib.util
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


HELPER_PATH = Path(__file__).with_name("render_h1_crop_payload.py")
SPEC = importlib.util.spec_from_file_location("h1_crop_payload", HELPER_PATH)
helper = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(helper)


def args() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def add_label(text: str, x: float, color_material) -> None:
    bpy.ops.object.text_add(location=(x - 1.25, -1.55, 0.015), rotation=(0.0, 0.0, 0.0))
    label = bpy.context.object
    label.name = f"상태표시_{text}"
    label.data.body = text
    label.data.align_x = "CENTER"
    label.data.size = 0.35
    label.data.extrude = 0.008
    label.data.materials.append(color_material)


def add_grain_mound(name: str, location, grain_material) -> None:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.22, location=location)
    mound = bpy.context.object
    mound.name = name
    mound.scale = (1.0, 0.75, 0.30)
    mound.data.materials.append(grain_material)


def add_scoop(location, wood_material, metal_material) -> None:
    x, y, z = location
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(x, y, z))
    bowl = bpy.context.object
    bowl.name = "계량삽_받이"
    bowl.scale = (0.13, 0.09, 0.025)
    bowl.rotation_euler.z = math.radians(-20)
    bowl.data.materials.append(metal_material)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.025, depth=0.42, location=(x + 0.17, y + 0.07, z + 0.08))
    handle = bpy.context.object
    handle.name = "계량삽_손잡이"
    handle.rotation_euler = (0.0, math.radians(65), math.radians(-20))
    handle.data.materials.append(wood_material)


def main() -> None:
    values = args()
    if len(values) != 1:
        raise SystemExit("usage: <output-dir>")
    output = Path(values[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)

    root = Path("C:/Users/user/ssalddel")
    models = root / "Assets/Synty/PolygonFarm/Models"
    atlas_path = root / "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png"
    stand_path = models / "SM_Bld_ProduceStand_01.fbx"
    closed_bag = models / "SM_Prop_GrainBag_01.fbx"
    open_bag = models / "SM_Prop_GrainBag_Open_02.fbx"
    rake = models / "SM_Prop_Tool_Rake_01.fbx"

    helper.clear_scene()
    atlas = helper.atlas_material(atlas_path)
    grain = helper.material("노출곡물", (0.72, 0.48, 0.12), 0.95)
    wood = helper.material("계량도구목재", (0.26, 0.10, 0.035), 0.9)
    metal = helper.material("계량도구금속", (0.32, 0.35, 0.38), 0.58)
    label_material = helper.material("상태번호", (0.75, 0.62, 0.22), 0.7)

    state_x = [-6.0, -2.0, 2.0, 6.0]
    for index, x in enumerate(state_x, start=1):
        helper.import_part(stand_path, f"상태{index:02d}_공통작업대골격", (x, 0.0, 0.0), atlas, scale=0.90)

    # 01: empty and available.
    add_label("01 EMPTY", state_x[0], label_material)

    # 02: open bags, visible grain and a scoop communicate active handling.
    for offset in (-0.45, 0.30):
        helper.import_part(open_bag, "취급중_개봉포대", (state_x[1] + offset, -0.40, 0.66), atlas, scale=0.76)
        add_grain_mound("취급중_노출곡물", (state_x[1] + offset, -0.40, 1.00), grain)
    add_scoop((state_x[1] + 0.66, -0.50, 0.80), wood, metal)
    add_label("02 HANDLING", state_x[1], label_material)

    # 03: sealed bags form a clear shipment-ready state.
    for offset in (-0.62, 0.0, 0.62):
        helper.import_part(closed_bag, "출고대기_밀봉포대", (state_x[2] + offset, -0.43, 0.66), atlas, rotation_z=math.radians(90), scale=0.74)
    add_label("03 READY", state_x[2], label_material)

    # 04: no goods remain; a returned rake marks cleanup rather than initial vacancy.
    helper.import_part(rake, "정리완료_반납갈퀴", (state_x[3], -0.50, 0.68), atlas, rotation_z=math.radians(78), scale=0.72)
    add_label("04 CLEARED", state_x[3], label_material)

    scene_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    low, high = helper.bounds(scene_meshes)
    center = (low + high) * 0.5
    width = high.x - low.x
    depth = high.y - low.y

    bpy.ops.mesh.primitive_plane_add(size=max(width, depth) * 1.25, location=(center.x, center.y, -0.025))
    ground = bpy.context.object
    ground.name = "상태흐름검토바닥"
    ground.scale.y = 0.34
    ground.data.materials.append(helper.material("농장바닥", (0.09, 0.14, 0.065), 0.95))

    bpy.ops.object.light_add(type="AREA", location=(center.x + 4.0, center.y - 6.0, high.z + 7.0))
    key = bpy.context.object
    key.data.energy = 3500
    key.data.size = 7.0
    helper.look_at(key, center)
    bpy.ops.object.light_add(type="AREA", location=(center.x - 6.0, center.y + 3.0, high.z + 5.0))
    fill = bpy.context.object
    fill.data.energy = 1800
    fill.data.size = 8.0
    helper.look_at(fill, center)

    bpy.ops.object.camera_add(location=(center.x, center.y - 14.0, high.z + 10.0))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(13.0, depth * 3.8)
    helper.look_at(camera, Vector((center.x, center.y - 0.1, 0.75)))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 760
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / "작업마당-상태흐름-비교.png")
    scene.world.color = (0.025, 0.032, 0.045)
    scene.view_settings.look = "AgX - Medium High Contrast"

    bpy.ops.wm.save_as_mainfile(filepath=str(output / "작업마당-상태흐름-비교.blend"))
    bpy.ops.render.render(write_still=True)
    result = {
        "status": "BlenderH1StateFlowRendered",
        "h1StableId": "h1-stock:farm-work-yard",
        "flow": ["EmptyOrAvailable", "HandlingOrOccupied", "LoadedStoredOrTransferred", "ClearedOrReturned"],
        "handlingReadability": ["OpenBag02", "VisibleGrainMound", "MeasuringScoop"],
        "sourcePaths": [str(stand_path), str(closed_bag), str(open_bag), str(rake)],
        "renderPath": str(output / "작업마당-상태흐름-비교.png"),
        "blendPath": str(output / "작업마당-상태흐름-비교.blend"),
        "boundary": "E4 state-flow review only; labels and geometry are not authority, Unity placement or E5 evidence.",
    }
    (output / "검증결과.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("H1_WORKYARD_STATE_FLOW_RENDERED")


if __name__ == "__main__":
    main()
