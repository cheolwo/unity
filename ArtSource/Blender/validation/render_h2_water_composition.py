"""Build connected H2 water-use review compositions from read-only supplier FBX files."""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def make_material(name: str, color: tuple[float, float, float], roughness: float = 0.8):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def atlas_material(name: str, texture: Path):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    image = value.node_tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(texture), check_existing=True)
    image.interpolation = "Closest"
    value.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    value.node_tree.links.new(image.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.82
    return value


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    return (
        Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
        Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))),
    )


def import_part(path: Path, label: str, location, material, rotation_z=0.0, scale=1.0):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = object_bounds(meshes)
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
        obj.data.materials.append(material)
    root.location = location
    root.rotation_euler.z = rotation_z
    root.scale = (scale, scale, scale)
    return root


def make_hose(name: str, points: list[tuple[float, float, float]], material, radius=0.045):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, value in zip(spline.bezier_points, points):
        point.co = value
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_scene_finish(output: Path) -> None:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type in {"MESH", "CURVE"} and obj.name != "검토바닥"]
    low, high = object_bounds([obj for obj in meshes if obj.type == "MESH"])
    center = (low + high) * 0.5
    extent = max(high.x - low.x, high.y - low.y, (high.z - low.z) * 0.8, 1.0)

    bpy.ops.mesh.primitive_plane_add(size=extent * 2.3, location=(center.x, center.y, -0.025))
    ground = bpy.context.object
    ground.name = "검토바닥"
    ground.data.materials.append(make_material("농장바닥", (0.105, 0.18, 0.075), 0.95))

    bpy.ops.object.light_add(type="AREA", location=(center.x + extent, center.y - extent, high.z + extent * 1.3))
    key = bpy.context.object
    key.data.energy = 44.0 * extent * extent
    key.data.size = extent
    look_at(key, center)
    bpy.ops.object.light_add(type="AREA", location=(center.x - extent, center.y + extent * 0.4, high.z + extent))
    fill = bpy.context.object
    fill.data.energy = 22.0 * extent * extent
    fill.data.size = extent * 1.2
    look_at(fill, center)

    bpy.ops.object.camera_add(location=(center.x + extent * 1.2, center.y - extent * 1.35, high.z + extent * 1.35))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = extent * 1.55
    look_at(camera, Vector((center.x, center.y, max(0.25, center.z * 0.55))))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / "H2-연결시안.png")
    scene.world.color = (0.025, 0.035, 0.028)
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "H2-연결시안.blend"))
    bpy.ops.render.render(write_still=True)


def main() -> None:
    values = args()
    if len(values) != 2 or values[0] not in {"washing", "irrigation"}:
        raise SystemExit("usage: <washing|irrigation> <output-dir>")
    mode, output_value = values
    output = Path(output_value).resolve()
    output.mkdir(parents=True, exist_ok=True)

    root = Path("C:/Users/user/ssalddel")
    farm_models = root / "Assets/Synty/PolygonFarm/Models"
    town_models = root / "Assets/Synty/PolygonTown/Models/Props"
    farm_atlas = atlas_material("FarmAtlas", root / "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png")
    town_atlas = atlas_material("TownAtlas", root / "Assets/Synty/PolygonTown/Textures/Alts/PolygonTown_01_A.png")
    hose_material = make_material("급수호스", (0.035, 0.12, 0.045), 0.7)

    clear_scene()
    sources = []
    water_source = farm_models / "SM_Bld_WaterTower_01.fbx"
    reel = town_models / "SM_Prop_HoseReel_01.fbx"
    import_part(water_source, "급수원H1_급수탑", (-3.0, 0.7, 0.0), farm_atlas, scale=0.32)
    import_part(reel, "호스릴손잡이H1", (-1.45, 0.45, 0.0), town_atlas, rotation_z=math.radians(15), scale=0.72)
    make_hose("급수원_호스릴_연결Edge", [(-2.55, 0.45, 0.12), (-2.15, 0.8, 0.1), (-1.55, 0.55, 0.12)], make_material("급수연결관", (0.08, 0.2, 0.24), 0.65), 0.035)
    sources.extend([str(water_source), str(reel)])

    if mode == "washing":
        trough = farm_models / "SM_Prop_Trough_01.fbx"
        drain = root / "Assets/Synty/PolygonTown/Models/Props/SM_Prop_Drain_01.fbx"
        import_part(trough, "세척작업점H1", (1.0, 0.15, 0.0), farm_atlas, rotation_z=math.radians(90), scale=0.82)
        import_part(drain, "배수처리점H1_후보", (2.45, -1.0, 0.01), town_atlas, scale=0.8)
        make_hose("호스H1_급수연결", [(-1.1, 0.3, 0.13), (-0.45, 0.72, 0.09), (0.35, 0.55, 0.1), (0.7, 0.15, 0.32)], hose_material)
        make_hose("배수Edge_시안", [(1.55, -0.25, 0.08), (1.85, -0.65, 0.05), (2.35, -0.95, 0.04)], make_material("배수표현", (0.12, 0.32, 0.38), 0.55), 0.025)
        sources.extend([str(trough), str(drain)])
        title = "농산물 세척 공간 H2"
    else:
        sprinkler = farm_models / "SM_Prop_Sprinkler_01.fbx"
        plot = root / "ArtSource/Blender/H1-시각자료/농장/경작구획-2.5x2.5m/경작구획-H1-2.5x2.5m.fbx"
        import_part(plot, "경작구획H1", (1.25, -0.45, 0.0), farm_atlas, scale=1.0)
        import_part(sprinkler, "선택설치_스프링클러H1", (1.15, -0.3, 0.19), farm_atlas, scale=0.72)
        make_hose("호스H1_관수연결", [(-1.1, 0.3, 0.13), (-0.4, 0.82, 0.08), (0.55, 0.55, 0.08), (1.08, -0.18, 0.25)], hose_material)
        sources.extend([str(plot), str(sprinkler)])
        title = "고랑 관수 공간 H2 — 스프링클러 선택 설치 예시"

    add_scene_finish(output)
    result = {
        "status": "BlenderH2CompositionRendered",
        "mode": mode,
        "title": title,
        "sourceOwnership": "SupplierOriginalReadOnlyAndProjectOwnedPlotDerivative",
        "sourcePaths": sources,
        "renderPath": str(output / "H2-연결시안.png"),
        "blendPath": str(output / "H2-연결시안.blend"),
        "boundary": "Connected layout proposal only; not canonical placement, Unity Scene, interaction, or E5 evidence.",
    }
    (output / "검증결과.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("H2_WATER_COMPOSITION_RENDERED")


if __name__ == "__main__":
    main()
