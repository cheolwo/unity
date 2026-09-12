"""Render closed/open manual irrigation compositions from independent H1 modules.

The output demonstrates valve rotation and a hose connection at Presentation E4.
It does not implement Unity hose deformation, water authority, or E5.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REVISION = "farm-manual-irrigation-connection.r2"


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


def material(name: str, color: tuple[float, float, float], roughness: float = 0.8, metallic: float = 0.0):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
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


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("MANUAL_IRRIGATION_NO_MESH")
    return (
        Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
        Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))),
    )


def import_centered(path: Path, label: str, location, assigned_material=None, rotation_z=0.0, scale=1.0):
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
    if assigned_material is not None:
        for obj in meshes:
            obj.data.materials.clear()
            obj.data.materials.append(assigned_material)
    root.location = location
    root.rotation_euler.z = rotation_z
    root.scale = (scale, scale, scale)
    return root


def import_service_point(path: Path, location):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    root = next(obj for obj in imported if obj.name == "농장_상수도급수점_H1")
    root.location = location
    return root, imported


def make_hose(name: str, points: list[tuple[float, float, float]], assigned_material, radius=0.04):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    curve.resolution_u = 12
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, value in zip(spline.bezier_points, points):
        point.co = value
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(assigned_material)
    return obj


def add_cylinder(name: str, location, radius: float, depth: float, assigned_material, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(assigned_material)
    return obj


def add_cone(name: str, location, radius_back: float, radius_front: float, depth: float, assigned_material, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=16,
        radius1=radius_back,
        radius2=radius_front,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(assigned_material)
    return obj


def add_wet_soil_patch(name: str, location, scale, assigned_material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=8, location=location, scale=scale)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(assigned_material)
    return obj


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_review_scene(scene: bpy.types.Scene, ground_material) -> None:
    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0.0, 0.0, -0.025))
    ground = bpy.context.object
    ground.name = "검토바닥_권위없음"
    ground.data.materials.append(ground_material)

    bpy.ops.object.light_add(type="AREA", location=(3.4, -4.2, 5.3))
    key = bpy.context.object
    key.data.energy = 1350
    key.data.size = 5.0
    look_at(key, Vector((0.0, 0.0, 0.4)))
    bpy.ops.object.light_add(type="AREA", location=(-3.2, 2.3, 3.2))
    fill = bpy.context.object
    fill.data.energy = 720
    fill.data.size = 4.0
    look_at(fill, Vector((0.0, 0.0, 0.35)))

    bpy.ops.object.camera_add(location=(5.1, -6.4, 5.1))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 6.4
    look_at(camera, Vector((0.0, 0.0, 0.36)))
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.035, 0.045, 0.035)
    scene["candidateRevision"] = REVISION
    scene["evidenceBoundary"] = "PresentationE4Composition;NotUnityE5"


def main() -> None:
    args = arguments()
    if len(args) != 1:
        raise SystemExit("usage: <output-dir>")

    output = Path(args[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    unity_root = Path("C:/Users/user/ssalddel")
    service_path = unity_root / "ArtSource/Blender/H1-시각자료/농장/상수도-급수점/기본형-급수함-r2/농장-상수도-급수점-H1-r2.fbx"
    reel_path = unity_root / "Assets/Synty/PolygonTown/Models/Props/SM_Prop_HoseReel_01.fbx"
    plot_path = unity_root / "ArtSource/Blender/H1-시각자료/농장/경작구획-2.5x2.5m/경작구획-H1-2.5x2.5m.fbx"
    farm_hose_reference = unity_root / "Assets/Synty/PolygonFarm/Models/SM_Prop_Sprinkler_Hose_01.fbx"
    town_texture = unity_root / "Assets/Synty/PolygonTown/Textures/Alts/PolygonTown_01_A.png"
    sources = [service_path, reel_path, plot_path, farm_hose_reference, town_texture]
    missing = [str(path) for path in sources if not path.exists()]
    if missing:
        raise RuntimeError("MANUAL_IRRIGATION_SOURCE_MISSING:" + "|".join(missing))

    clear_scene()
    scene = bpy.context.scene
    town_atlas = atlas_material("TownAtlas", town_texture)
    soil = material("경작토양", (0.39, 0.19, 0.075), 0.94)
    source_hose = material("급수원연결호스", (0.05, 0.28, 0.58), 0.68)
    field_hose = material("수동관수호스", (0.06, 0.20, 0.075), 0.72)
    nozzle_material = material("관수손잡이", (0.70, 0.39, 0.08), 0.62, 0.12)
    water_material = material("관수물줄기", (0.12, 0.56, 0.92), 0.22)
    wet_soil_material = material("젖은흙", (0.16, 0.075, 0.026), 0.70)
    ground_material = material("농장바닥", (0.18, 0.29, 0.13), 0.96)

    service_root, service_objects = import_service_point(service_path, (-2.25, 0.78, 0.0))
    reel_root = import_centered(reel_path, "호스릴손잡이H1", (-0.65, 0.55, 0.0), town_atlas, rotation_z=math.radians(12), scale=1.0)
    plot_root = import_centered(plot_path, "경작구획H1", (1.55, -0.38, 0.0), soil)

    hose_out = next(obj for obj in service_objects if obj.name == "Anchor_HoseOut")
    source_start = hose_out.matrix_world.translation.copy()
    # The flexible hose does not enter the pipe as a thin line. A blue coupling sleeve
    # wraps the complete outlet body and narrows into the hose behind the socket.
    coupling_axis = (math.pi / 2, 0.0, 0.0)
    add_cylinder(
        "급수점_호스체결슬리브",
        (source_start.x, source_start.y + 0.065, source_start.z),
        0.126,
        0.17,
        source_hose,
        rotation=coupling_axis,
    )
    add_cone(
        "급수점_호스체결리듀서",
        (source_start.x, source_start.y - 0.075, source_start.z),
        0.122,
        0.058,
        0.12,
        source_hose,
        rotation=coupling_axis,
    )
    flexible_hose_start = Vector((source_start.x, source_start.y - 0.135, source_start.z))
    source_end = Vector((-0.92, 0.57, 0.20))
    make_hose(
        "급수점_호스릴_연결",
        [tuple(flexible_hose_start), (-1.70, 0.32, 0.20), (-1.22, 0.64, 0.12), tuple(source_end)],
        source_hose,
        0.035,
    )

    nozzle = Vector((1.10, -0.15, 0.58))
    make_hose(
        "호스릴_수동손잡이_연결",
        [(-0.42, 0.38, 0.16), (0.00, 0.70, 0.08), (0.55, 0.34, 0.08), (0.88, -0.04, 0.20), tuple(nozzle)],
        field_hose,
        0.045,
    )
    add_cylinder("수동관수손잡이", tuple(nozzle), 0.055, 0.42, nozzle_material, rotation=(0.0, math.radians(63), math.radians(12)))
    nozzle_tip = Vector((1.27, -0.19, 0.40))
    add_cylinder("수동관수노즐", tuple(nozzle_tip), 0.035, 0.20, nozzle_material, rotation=(0.0, math.radians(63), math.radians(12)))

    metadata = bpy.data.objects.new("수동관수_연결시안_r1", None)
    scene.collection.objects.link(metadata)
    metadata["connectionSequence"] = "WaterServicePointH1->HoseReelHandleH1->HoseH1->CultivationPlotH1"
    metadata["closedState"] = "ValveClosed;HoseConnected;NoWaterFlow"
    metadata["openState"] = "ValveRotated45Degrees;FullOutletSleeve;HoseConnected;ManualWatering;WetSoilVisible"
    metadata["authorityBoundary"] = "PresentationE4CompositionOnly"
    add_review_scene(scene, ground_material)

    # Closed: hose is connected, but the valve has not been turned and no water is visible.
    closed_image = output / "01-밸브닫힘-호스연결.png"
    scene.render.filepath = str(closed_image)
    bpy.ops.render.render(write_still=True)
    closed_blend = output / "01-밸브닫힘-호스연결.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(closed_blend))

    # Open: rotate the visible red wheel and spokes, then add a fan of water toward the furrow.
    for name in ("차단밸브휠", "차단밸브가로살", "차단밸브세로살"):
        obj = bpy.data.objects.get(name)
        if obj is None:
            raise RuntimeError("MANUAL_IRRIGATION_VALVE_PART_MISSING:" + name)
        obj.rotation_euler.y += math.radians(45)
    water_paths = [
        [tuple(nozzle_tip), (1.38 + offset, -0.24 + offset * 0.2, 0.30), (1.48 + offset * 1.4, -0.28 + offset, 0.18)]
        for offset in (-0.14, -0.07, 0.0, 0.07, 0.14)
    ]
    for index, points in enumerate(water_paths, start=1):
        make_hose(f"관수물줄기_{index:02d}", points, water_material, 0.012)
    add_wet_soil_patch("젖은흙_중앙_01", (1.48, -0.28, 0.175), (0.32, 0.20, 0.018), wet_soil_material)
    add_wet_soil_patch("젖은흙_중앙_02", (1.67, -0.20, 0.172), (0.22, 0.14, 0.014), wet_soil_material)
    open_image = output / "02-밸브열림-수동관수.png"
    scene.render.filepath = str(open_image)
    bpy.ops.render.render(write_still=True)
    open_blend = output / "02-밸브열림-수동관수.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(open_blend))

    result = {
        "candidateRevision": REVISION,
        "composition": [
            "h1-proposal:farm-pressurized-water-service-point",
            "HoseReelAndHandleH1",
            "HoseH1",
            "h1-stock:farm-production",
        ],
        "states": [
            {"code": "ValveClosedHoseConnected", "image": closed_image.name, "blend": closed_blend.name},
            {"code": "ValveOpenManualWatering", "image": open_image.name, "blend": open_blend.name},
        ],
        "valveOpenVisualRotationDegrees": 45,
        "waterVisibleOnlyWhenOpen": True,
        "wetSoilVisibleOnlyWhenOpen": True,
        "outletCoupling": {
            "type": "FullWrapSleeveAndReducer",
            "sleeveOuterRadiusMeters": 0.126,
            "coveredOutletOuterRadiusMeters": 0.108,
        },
        "connectionAnchors": ["Anchor_HoseOut", "HoseReelInputCandidate", "ManualNozzle", "CultivationPlotFurrowCenter"],
        "sources": [{"path": str(path), "sha256": sha256(path)} for path in sources],
        "files": {
            "closedImageSha256": sha256(closed_image),
            "closedBlendSha256": sha256(closed_blend),
            "openImageSha256": sha256(open_image),
            "openBlendSha256": sha256(open_blend),
        },
        "evidenceBoundary": {
            "presentation": "E4CompositionCandidate",
            "unityScene": False,
            "dynamicHose": False,
            "waterSimulation": False,
            "playMode": False,
            "gameView": False,
            "e5": False,
        },
    }
    (output / "검증결과-r2.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
