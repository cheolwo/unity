"""Render a reusable H1 work-yard skeleton with a crop-specific payload module."""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def arguments() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("H1_CROP_PAYLOAD_NO_MESH")
    return (
        Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
        Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))),
    )


def material(name: str, color: tuple[float, float, float], roughness: float = 0.85):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def atlas_material(texture_path: Path):
    value = bpy.data.materials.new("FarmAtlas")
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    image = value.node_tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.interpolation = "Closest"
    value.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    value.node_tree.links.new(image.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.82
    return value


def import_part(path: Path, label: str, location, assigned_material, rotation_z=0.0, scale=1.0):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = bounds(meshes)
    center = (low + high) * 0.5
    root = bpy.data.objects.new(label, None)
    bpy.context.scene.collection.objects.link(root)
    for obj in imported:
        world = obj.matrix_world.copy()
        world.translation += Vector((-center.x, -center.y, -low.z))
        obj.parent = root
        obj.matrix_world = world
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(assigned_material)
    root.location = location
    root.rotation_euler.z = rotation_z
    root.scale = (scale, scale, scale)
    return root


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_box_part(name: str, location, scale, assigned_material, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(assigned_material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_leafy_crate(index: int, location, models: Path, farm_atlas, wood_material):
    x, y, z = location
    root = bpy.data.objects.new(f"작물적재모듈_잎채소상자_{index:02d}", None)
    bpy.context.scene.collection.objects.link(root)
    add_box_part(f"잎채소상자_{index:02d}_바닥", (x, y, z + 0.03), (0.30, 0.22, 0.03), wood_material, root)
    add_box_part(f"잎채소상자_{index:02d}_좌벽", (x - 0.27, y, z + 0.13), (0.03, 0.22, 0.13), wood_material, root)
    add_box_part(f"잎채소상자_{index:02d}_우벽", (x + 0.27, y, z + 0.13), (0.03, 0.22, 0.13), wood_material, root)
    add_box_part(f"잎채소상자_{index:02d}_앞벽", (x, y - 0.19, z + 0.13), (0.30, 0.03, 0.13), wood_material, root)
    add_box_part(f"잎채소상자_{index:02d}_뒤벽", (x, y + 0.19, z + 0.13), (0.30, 0.03, 0.13), wood_material, root)

    leafy_crop = models / "SM_Prop_Cabbage_01_L.fbx"
    offsets = [(-0.14, -0.07), (0.14, -0.07), (0.0, 0.10)]
    for item_index, (ox, oy) in enumerate(offsets, start=1):
        child = import_part(
            leafy_crop,
            f"잎채소상자_{index:02d}_배추_{item_index:02d}",
            (x + ox, y + oy, z + 0.08),
            farm_atlas,
            rotation_z=math.radians((index * 31 + item_index * 47) % 360),
            scale=0.65,
        )
        child.parent = root
    return root, leafy_crop


def add_pear_crate(index: int, location, models: Path, farm_atlas, wood_material):
    x, y, z = location
    root = bpy.data.objects.new(f"작물적재모듈_배상자_{index:02d}", None)
    bpy.context.scene.collection.objects.link(root)
    add_box_part(f"배상자_{index:02d}_바닥", (x, y, z + 0.03), (0.30, 0.22, 0.03), wood_material, root)
    add_box_part(f"배상자_{index:02d}_좌벽", (x - 0.27, y, z + 0.13), (0.03, 0.22, 0.13), wood_material, root)
    add_box_part(f"배상자_{index:02d}_우벽", (x + 0.27, y, z + 0.13), (0.03, 0.22, 0.13), wood_material, root)
    add_box_part(f"배상자_{index:02d}_앞벽", (x, y - 0.19, z + 0.13), (0.30, 0.03, 0.13), wood_material, root)
    add_box_part(f"배상자_{index:02d}_뒤벽", (x, y + 0.19, z + 0.13), (0.30, 0.03, 0.13), wood_material, root)

    pear = models / "SM_Prop_Pear_01.fbx"
    offsets = [(-0.18, -0.09), (0.0, -0.09), (0.18, -0.09), (-0.18, 0.09), (0.0, 0.09), (0.18, 0.09)]
    for item_index, (ox, oy) in enumerate(offsets, start=1):
        child = import_part(
            pear,
            f"배상자_{index:02d}_배_{item_index:02d}",
            (x + ox, y + oy, z + 0.07),
            farm_atlas,
            rotation_z=math.radians((index * 29 + item_index * 53) % 360),
            scale=0.88,
        )
        child.parent = root
    return root, pear


def main() -> None:
    args = arguments()
    if len(args) not in (1, 2):
        raise SystemExit("usage: <output-dir> [potato|leafy|carrot|cucumber|apple|pear|grain-closed|grain-open]")

    output = Path(args[0]).resolve()
    mode = args[1] if len(args) == 2 else "potato"
    if mode not in {"potato", "leafy", "carrot", "cucumber", "apple", "pear", "grain-closed", "grain-open"}:
        raise SystemExit("unsupported crop payload mode")
    output.mkdir(parents=True, exist_ok=True)
    root = Path("C:/Users/user/ssalddel")
    models = root / "Assets/Synty/PolygonFarm/Models"
    stand = models / "SM_Bld_ProduceStand_01.fbx"
    texture = root / "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png"

    clear_scene()
    farm_atlas = atlas_material(texture)
    import_part(stand, "공통골격H1_차양형작업대", (0.0, 0.0, 0.0), farm_atlas)

    # Crop payloads are separate children in the review scene. Replacing these
    # three roots changes the crop without duplicating the work-yard skeleton.
    payload_positions = [(-0.72, -0.45, 0.72), (0.0, -0.45, 0.72), (0.72, -0.45, 0.72)]
    payloads = []
    payload_sources = []
    if mode in {"potato", "carrot", "cucumber", "apple", "grain-closed", "grain-open"}:
        boxed_crop = {
            "potato": ("SM_Prop_Box_Potato_01.fbx", "감자", "crop-payload:potato-box.v1", "감자-적재-상태"),
            "carrot": ("SM_Prop_Box_Carrot_01.fbx", "당근", "crop-payload:carrot-box.v1", "당근-적재-상태"),
            "cucumber": ("SM_Prop_Box_Cucumber_01.fbx", "오이", "crop-payload:cucumber-box.v1", "오이-적재-상태"),
            "apple": ("SM_Prop_Box_Apple_01.fbx", "사과", "crop-payload:apple-box.v1", "사과-적재-상태"),
            "grain-closed": ("SM_Prop_GrainBag_01.fbx", "곡물밀봉포대", "crop-payload:grain-bag-closed.v1", "곡물-밀봉포대-적재-상태"),
            "grain-open": ("SM_Prop_GrainBag_Open_01.fbx", "곡물개봉포대", "crop-payload:grain-bag-open.v1", "곡물-개봉포대-취급-상태"),
        }[mode]
        boxed_crop_path = models / boxed_crop[0]
        for index, position in enumerate(payload_positions, start=1):
            payloads.append(
                import_part(
                    boxed_crop_path,
                    f"작물적재모듈_{boxed_crop[1]}상자_{index:02d}",
                    position,
                    farm_atlas,
                    rotation_z=math.radians(90),
                    scale=0.72,
                )
            )
        payload_code = boxed_crop[2]
        payload_sources = [boxed_crop_path]
        render_name = f"{boxed_crop[3]}.png"
        blend_name = f"{boxed_crop[3]}.blend"
    elif mode == "leafy":
        wood = material("프로젝트수확상자_목재", (0.28, 0.12, 0.045), 0.92)
        lettuce_source = None
        for index, position in enumerate(payload_positions, start=1):
            crate, lettuce_source = add_leafy_crate(index, position, models, farm_atlas, wood)
            payloads.append(crate)
        payload_code = "crop-payload:leafy-cabbage-crate.v1"
        payload_sources = [lettuce_source]
        render_name = "잎채소-적재-상태.png"
        blend_name = "잎채소-적재-상태.blend"
    else:
        wood = material("프로젝트수확상자_목재", (0.28, 0.12, 0.045), 0.92)
        pear_source = None
        for index, position in enumerate(payload_positions, start=1):
            crate, pear_source = add_pear_crate(index, position, models, farm_atlas, wood)
            payloads.append(crate)
        payload_code = "crop-payload:pear-crate.v1"
        payload_sources = [pear_source]
        render_name = "배-적재-상태.png"
        blend_name = "배-적재-상태.blend"

    scene_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    low, high = bounds(scene_meshes)
    center = (low + high) * 0.5
    extent = max(high.x - low.x, high.y - low.y, (high.z - low.z) * 0.85, 1.0)

    bpy.ops.mesh.primitive_plane_add(size=extent * 2.2, location=(center.x, center.y, -0.025))
    ground = bpy.context.object
    ground.name = "H1검토바닥"
    ground.data.materials.append(material("농장바닥", (0.09, 0.14, 0.065), 0.95))

    bpy.ops.object.light_add(type="AREA", location=(center.x + extent, center.y - extent, high.z + extent))
    key = bpy.context.object
    key.data.energy = 42.0 * extent * extent
    key.data.size = extent
    look_at(key, center)
    bpy.ops.object.light_add(type="AREA", location=(center.x - extent, center.y + extent * 0.4, high.z + extent * 0.7))
    fill = bpy.context.object
    fill.data.energy = 20.0 * extent * extent
    fill.data.size = extent
    look_at(fill, center)

    bpy.ops.object.camera_add(location=(center.x + extent * 1.1, center.y - extent * 1.35, high.z + extent * 1.1))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = extent * 1.45
    look_at(camera, Vector((center.x, center.y, max(0.4, center.z * 0.7))))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / render_name)
    scene.world.color = (0.025, 0.032, 0.045)
    scene.view_settings.look = "AgX - Medium High Contrast"

    bpy.ops.wm.save_as_mainfile(filepath=str(output / blend_name))
    bpy.ops.render.render(write_still=True)

    result = {
        "status": "BlenderH1CropPayloadRendered",
        "h1StableId": "h1-stock:farm-work-yard",
        "skeletonSource": str(stand),
        "skeletonSha256": sha256(stand),
        "payloadCode": payload_code,
        "payloadSources": [str(path) for path in payload_sources],
        "payloadSha256": [sha256(path) for path in payload_sources],
        "payloadInstances": len(payloads),
        "renderPath": str(output / render_name),
        "blendPath": str(output / blend_name),
        "boundary": "E4 visual review only; no Unity prefab, state authority, interaction, or E5 evidence.",
    }
    (output / "검증결과.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("H1_CROP_PAYLOAD_RENDERED")


if __name__ == "__main__":
    main()
