"""Render a presentation-only Farm AreaSet overview for paired H1 review."""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def mesh_bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("AREA_SET_REVIEW_NO_MESH")
    return (
        Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
        Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))),
    )


def solid_material(name, color, roughness=0.82, emission=None):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.5
    return value


def atlas_material(name, texture_path: Path):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    image = value.node_tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.interpolation = "Closest"
    value.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    value.node_tree.links.new(image.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.82
    return value


def add_box(name, location, scale, assigned_material, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(assigned_material)
    if bevel:
        modifier = obj.modifiers.new("모서리완화", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def import_asset(path: Path, label: str, location, assigned_material, rotation_z=0.0, target_max_xy=None, target_height=None):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = mesh_bounds(meshes)
    size = high - low
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
    factor = 1.0
    if target_max_xy is not None and max(size.x, size.y) > 0:
        factor = target_max_xy / max(size.x, size.y)
    if target_height is not None and size.z > 0:
        factor = target_height / size.z
    root.location = location
    root.rotation_euler.z = rotation_z
    root.scale = (factor, factor, factor)
    return root, {"sourceSize": [round(size.x, 4), round(size.y, 4), round(size.z, 4)], "scale": round(factor, 6)}


def duplicate_linked(root, label, location, rotation_z=0.0, scale=1.0):
    copy = root.copy()
    copy.name = label
    bpy.context.scene.collection.objects.link(copy)
    for child in root.children:
        child_copy = child.copy()
        if child.data:
            child_copy.data = child.data
        bpy.context.scene.collection.objects.link(child_copy)
        child_copy.parent = copy
        child_copy.matrix_parent_inverse = child.matrix_parent_inverse.copy()
        child_copy.location = child.location.copy()
        child_copy.rotation_euler = child.rotation_euler.copy()
        child_copy.scale = child.scale.copy()
    copy.location = location
    copy.rotation_euler.z = rotation_z
    copy.scale = tuple(component * scale for component in root.scale)
    return copy


def add_outline(name, center, width, depth, height, material):
    x, y, z = center
    thickness = 0.09
    add_box(f"{name}_앞", (x, y - depth / 2, z), (width / 2, thickness, height), material)
    add_box(f"{name}_뒤", (x, y + depth / 2, z), (width / 2, thickness, height), material)
    add_box(f"{name}_좌", (x - width / 2, y, z), (thickness, depth / 2, height), material)
    add_box(f"{name}_우", (x + width / 2, y, z), (thickness, depth / 2, height), material)


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    if "--" not in sys.argv or len(sys.argv[sys.argv.index("--") + 1 :]) != 1:
        raise SystemExit("usage: <output-dir>")
    output = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    output.mkdir(parents=True, exist_ok=True)

    root = Path("C:/Users/user/ssalddel")
    farm_models = root / "Assets/Synty/PolygonFarm/Models"
    construction_models = root / "Assets/Synty/PolygonConstruction/Models"
    nature_models = root / "Assets/Synty/PolygonNature/Models"
    sources = {
        "farmhouse": farm_models / "SM_Bld_Farmhouse_01.fbx",
        "workYard": farm_models / "SM_Bld_ProduceStand_01.fbx",
        "fence": farm_models / "SM_Prop_Fence_Wood_01.fbx",
        "gate": farm_models / "SM_Prop_Fence_Wood_Gate_01.fbx",
        "road": construction_models / "SM_Env_DirtRoad_Straight_01.fbx",
        "pine": nature_models / "SM_Tree_Pine_01.fbx",
        "largePine": nature_models / "SM_Tree_Pine_Large_01.fbx",
        "cultivationPlot": root / "ArtSource/Blender/H1-시각자료/농장/경작구획-2.5x2.5m/경작구획-H1-2.5x2.5m.fbx",
        "loadingPoint": farm_models / "SM_Bld_ConcreteBase_01.fbx",
    }
    for source in sources.values():
        if not source.exists():
            raise FileNotFoundError(source)

    clear_scene()
    farm_atlas = atlas_material("FarmAtlas", root / "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png")
    construction_atlas = atlas_material("ConstructionAtlas", root / "Assets/Synty/PolygonConstruction/Textures/PolygonConstruction_Texture_01_A.png")
    nature_atlas = atlas_material("NatureAtlas", root / "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png")
    ground_mat = solid_material("생활농장_지면", (0.20, 0.29, 0.16))
    field_mat = solid_material("경작완충_흙", (0.24, 0.14, 0.075))
    highlight_mat = solid_material("현재검토H1", (0.95, 0.52, 0.06), emission=(0.95, 0.28, 0.02))
    path_mat = solid_material("보행연결", (0.42, 0.31, 0.18))

    add_box("AreaSet_생활농장_지면", (0, 0, -0.16), (16.0, 12.5, 0.16), ground_mat, 0.12)
    add_box("밭_완충_흙", (-2.2, 1.9, -0.015), (4.8, 4.5, 0.035), field_mat, 0.05)
    add_box("집과밭_보행연결", (-6.0, 1.8, 0.01), (0.7, 4.0, 0.03), path_mat)
    add_box("밭과작업마당_보행연결", (2.5, 3.8, 0.01), (2.8, 0.65, 0.03), path_mat)
    add_box("작업마당과상차_보행연결", (5.8, 2.0, 0.01), (0.65, 2.2, 0.03), path_mat)

    records = []
    def put(key, label, location, material, rotation=0.0, target_xy=None, target_height=None):
        item, sizing = import_asset(sources[key], label, location, material, rotation, target_xy, target_height)
        records.append({"key": key, "label": label, "location": list(location), **sizing})
        return item

    put("farmhouse", "H1_농장생활주택", (-8.2, 5.6, 0), farm_atlas, math.radians(-8), target_xy=7.0)
    put("workYard", "H1_헛간작업마당", (5.8, 5.2, 0), farm_atlas, math.radians(180), target_xy=4.4)
    # The source is a foundation mesh. Sink it into the terrain in the context
    # draft so the overview evaluates its working surface, not a one-metre plinth.
    put("loadingPoint", "현재H1_농장상차지점", (5.8, -0.9, -0.78), farm_atlas, 0, target_xy=4.5)

    plot_positions = [(-3.7, 4.0, 0.02), (-0.9, 4.0, 0.02), (-3.7, 1.2, 0.02), (-0.9, 1.2, 0.02), (-3.7, -1.6, 0.02), (-0.9, -1.6, 0.02)]
    for index, position in enumerate(plot_positions, start=1):
        put("cultivationPlot", f"H1_경작구획_{index:02d}", position, farm_atlas, 0, target_xy=2.5)

    road_positions = [(9.1, -9.0, 0.0), (9.1, -3.5, 0.0), (9.1, 2.0, 0.0)]
    for index, position in enumerate(road_positions, start=1):
        put("road", f"Edge_비포장진입로_{index:02d}", position, construction_atlas, 0, target_xy=6.0)

    fence_specs = [
        ((-11.8, -8.8, 0), 0), ((-8.5, -8.8, 0), 0), ((-5.2, -8.8, 0), 0),
        ((-1.9, -8.8, 0), 0), ((1.4, -8.8, 0), 0), ((4.7, -8.8, 0), 0),
        ((-13.8, -5.5, 0), math.radians(90)), ((-13.8, -2.2, 0), math.radians(90)),
        ((-13.8, 1.1, 0), math.radians(90)), ((-13.8, 4.4, 0), math.radians(90)),
        ((-13.8, 7.7, 0), math.radians(90)), ((13.8, 6.5, 0), math.radians(90)),
        ((13.8, 3.2, 0), math.radians(90)), ((13.8, -0.1, 0), math.radians(90)),
        ((13.8, -3.4, 0), math.radians(90)),
    ]
    for index, (position, rotation) in enumerate(fence_specs, start=1):
        put("fence", f"H1_울타리_{index:02d}", position, farm_atlas, rotation, target_xy=3.2)
    put("gate", "H1_농장출입구", (9.1, -8.7, 0), farm_atlas, 0, target_xy=3.2)

    tree_specs = [
        ((-12.4, 10.2, 0), 5.6, "largePine"), ((-8.8, 10.5, 0), 4.8, "pine"),
        ((-5.2, 10.4, 0), 5.3, "largePine"), ((-1.6, 10.6, 0), 4.4, "pine"),
        ((2.0, 10.4, 0), 5.4, "largePine"), ((5.6, 10.6, 0), 4.7, "pine"),
        ((9.2, 10.3, 0), 5.2, "largePine"), ((12.6, 9.7, 0), 4.5, "pine"),
    ]
    for index, (position, height, key) in enumerate(tree_specs, start=1):
        put(key, f"H1_숲완충_침엽수_{index:02d}", position, nature_atlas, math.radians(index * 29), target_height=height)

    add_outline("현재검토_농장상차지점", (5.8, -0.9, 0.28), 5.4, 5.4, 0.055, highlight_mat)

    bpy.ops.object.light_add(type="AREA", location=(-5.0, -4.0, 15.0))
    key_light = bpy.context.object
    key_light.name = "주광"
    key_light.data.energy = 1800
    key_light.data.shape = "DISK"
    key_light.data.size = 10.0
    look_at(key_light, (0, 0, 0))
    bpy.ops.object.light_add(type="AREA", location=(10.0, 5.0, 9.0))
    fill_light = bpy.context.object
    fill_light.name = "보조광"
    fill_light.data.energy = 800
    fill_light.data.size = 8.0
    look_at(fill_light, (2, 1, 0))

    world = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.075, 0.10, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.58

    bpy.ops.object.camera_add(location=(25.0, -29.0, 30.0))
    camera = bpy.context.object
    camera.name = "AreaSet_조감카메라"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 37.0
    look_at(camera, (0.5, 0.6, 0.8))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(output / "숲경계-한스-생활농장-AreaSet-조감도.png")
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_percentage = 100

    blend_path = output / "숲경계-한스-생활농장-AreaSet-조감도.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.render.render(write_still=True)

    result = {
        "schemaVersion": 1,
        "reviewStableId": "area-set-review:forest-edge-farm:hans-living-farm.r2",
        "title": "숲 경계 한스 생활 농장 AreaSet 조감도",
        "focusH1": "h1-node-candidate:farm-loading-point",
        "referencePlacementProfile": "placement-map-profile:forest-edge-farm:hans-living-farm",
        "reviewPair": {
            "isolatedH1Image": str(root / "ArtSource/Blender/H1-시각자료/농장/농장-상차-지점/빈-상차지점-H1-01/candidate.png"),
            "areaSetOverviewImage": str(output / "숲경계-한스-생활농장-AreaSet-조감도.png"),
        },
        "placements": records,
        "sourceHashes": {key: sha256(path) for key, path in sources.items()},
        "authorityBoundary": {
            "planningE4ContextReviewOnly": True,
            "canonicalAreaSetChanged": False,
            "graphMapChanged": False,
            "placementMapChanged": False,
            "unityImported": False,
            "unityWorldPlaced": False,
            "evidenceE5": False,
        },
    }
    (output / "검증결과.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
