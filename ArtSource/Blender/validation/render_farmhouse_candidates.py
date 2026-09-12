import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args_after_separator():
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def bounds(objects):
    points = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def make_material(name, color, roughness=0.85):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def add_ground(center, extent):
    bpy.ops.mesh.primitive_plane_add(size=extent * 5.0, location=(center.x, center.y, 0.0))
    ground = bpy.context.object
    ground.name = "검토바닥"
    ground.data.materials.append(make_material("검토바닥재", (0.16, 0.22, 0.13)))


def add_label(text, location, scale):
    bpy.ops.object.text_add(location=location, rotation=(math.radians(72), 0.0, 0.0))
    label = bpy.context.object
    label.name = "후보표시"
    label.data.body = text
    label.data.align_x = "CENTER"
    label.data.size = scale
    label.data.extrude = scale * 0.015
    label.data.materials.append(make_material("표시재", (0.92, 0.85, 0.48), 0.55))


def render_candidate(source_path, output_path):
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source_path), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    low, high = bounds(imported)
    center = (low + high) * 0.5
    size = high - low
    extent = max(size.x, size.y, size.z)

    # Synty FBX is authored in Unity-facing scale. Keep mesh geometry unchanged,
    # and only move the imported duplicate to the review origin.
    shift = Vector((-center.x, -center.y, -low.z))
    for obj in imported:
        obj.location += shift

    low, high = bounds(imported)
    center = (low + high) * 0.5
    extent = max((high - low).x, (high - low).y, (high - low).z)
    add_ground(center, extent)
    add_label(source_path.stem, (center.x, center.y - extent * 0.58, 0.03), extent * 0.055)

    bpy.ops.object.light_add(type="AREA", location=(center.x + extent, center.y - extent, high.z + extent))
    key = bpy.context.object
    key.name = "주광"
    key.data.energy = 1300
    key.data.shape = "DISK"
    key.data.size = extent * 0.9
    look_at(key, center)

    bpy.ops.object.light_add(type="AREA", location=(center.x - extent, center.y + extent * 0.3, high.z + extent * 0.3))
    fill = bpy.context.object
    fill.name = "보조광"
    fill.data.energy = 700
    fill.data.size = extent
    look_at(fill, center)

    bpy.ops.object.camera_add(location=(center.x + extent * 1.55, center.y - extent * 1.65, center.z + extent * 0.95))
    camera = bpy.context.object
    camera.name = "검토카메라"
    camera.data.lens = 52
    look_at(camera, (center.x, center.y, center.z * 0.9))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output_path)
    scene.render.film_transparent = False
    scene.world.color = (0.035, 0.045, 0.06)
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)
    return {
        "source": str(source_path),
        "output": str(output_path),
        "meshObjects": [obj.name for obj in imported],
        "boundsMin": list(low),
        "boundsMax": list(high),
        "dimensions": list(high - low),
        "materials": sorted({slot.material.name for obj in imported for slot in obj.material_slots if slot.material}),
    }


def main():
    args = args_after_separator()
    if len(args) != 2:
        raise SystemExit("usage: blender --background --python render_farmhouse_candidates.py -- <source-dir> <output-dir>")
    source_dir = Path(args[0]).resolve()
    output_dir = Path(args[1]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for name in ("SM_Bld_Farmhouse_01.fbx", "SM_Bld_Farmhouse_02.fbx"):
        results.append(render_candidate(source_dir / name, output_dir / f"{Path(name).stem}.png"))
    (output_dir / "inspection.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
