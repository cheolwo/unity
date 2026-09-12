"""기존 AreaSet의 Synty 울타리·문으로 병목형 방어 지점 H1 r2 시안을 만든다."""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


STABLE_ID = "h1-node-candidate:farm-fence-defense-chokepoint"
REVISION = "r2"
SOURCE_ASSET_KEYS = {"wood-gate", "wood-fence"}
CLEAR_PASSAGE_METERS = 4.0
FENCE_SHOULDER_METERS = 2.5
DEFENDER_X_OFFSET_METERS = 1.2
DEFENDER_INSIDE_Y_METERS = 2.0


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def material(name: str, color, metallic: float = 0.0, texture: Path | None = None):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.72
    bsdf.inputs["Metallic"].default_value = metallic
    if texture is not None and texture.exists():
        image_node = value.node_tree.nodes.get("Image Texture") or value.node_tree.nodes.new("ShaderNodeTexImage")
        image_node.image = bpy.data.images.load(str(texture), check_existing=True)
        image_node.interpolation = "Closest"
        value.node_tree.links.new(image_node.outputs["Color"], bsdf.inputs["Base Color"])
        value.node_tree.links.new(image_node.outputs["Alpha"], bsdf.inputs["Alpha"])
    return value


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.users == 0:
            bpy.data.collections.remove(collection)


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("CHOKEPOINT_ASSET_HAS_NO_MESH")
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def import_calibrated(project_root: Path, calibration, name: str, location) -> bpy.types.Object:
    source = project_root / calibration["ModelAssetPath"]
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = bounds(meshes)
    observed_size = high - low
    desired_size = Vector((calibration["BoundsSizeX"], calibration["BoundsSizeZ"], calibration["BoundsSizeY"]))
    ratios = [desired_size[i] / observed_size[i] for i in range(3)]
    factor = sorted(ratios)[1]
    root = bpy.data.objects.new(name, None)
    geometry = bpy.data.objects.new(name + "_형상", None)
    bpy.context.scene.collection.objects.link(root)
    bpy.context.scene.collection.objects.link(geometry)
    geometry.parent = root
    for obj in imported:
        world = obj.matrix_world.copy()
        obj.parent = geometry
        obj.matrix_world = world
    observed_center = (low + high) * 0.5
    desired_center = Vector((calibration["BoundsCenterX"], calibration["BoundsCenterZ"], calibration["BoundsCenterY"]))
    geometry.scale = (factor, factor, factor)
    geometry.location = desired_center - observed_center * factor
    texture = project_root / calibration["TextureAssetPath"] if calibration["TextureAssetPath"] else None
    atlas = material("Synty_" + calibration["AssetKey"], (0.65, 0.65, 0.65, 1.0), texture=texture)
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(atlas)
    root.location = location
    root["h1_review_stable_id"] = STABLE_ID
    root["source_asset_state"] = "SyntySourceUnmodified"
    root["source_asset_key"] = calibration["AssetKey"]
    return root


def isolate_existing_roots() -> dict[str, bpy.types.Object]:
    wanted = {"wood-gate-01", "south-fence-09", "south-fence-10"}
    roots = {
        obj.get("area_set_placement_stable_id"): obj
        for obj in list(bpy.context.scene.objects)
        if obj.get("area_set_placement_stable_id") in wanted
    }
    if set(roots) != wanted:
        raise RuntimeError("CHOKEPOINT_SOURCE_ROOT_MISSING")
    visible = set(roots.values())
    for root in roots.values():
        visible.update(root.children_recursive)
    for obj in list(bpy.context.scene.objects):
        if obj not in visible:
            obj.hide_render = True
            obj.hide_viewport = True
    return roots


def ground_root(root: bpy.types.Object) -> None:
    points = [obj.matrix_world @ Vector(corner) for obj in root.children_recursive if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("CHOKEPOINT_ROOT_HAS_NO_MESH:" + root.name)
    root.location.z -= min(point.z for point in points)


def create_ground():
    bpy.ops.mesh.primitive_plane_add(size=22.0, location=(0.0, 0.0, -0.04))
    ground = bpy.context.object
    ground.name = "병목지점_지면"
    ground.data.materials.append(material("흙길_지면", (0.19, 0.13, 0.075, 1.0)))
    return ground


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def create_route(name: str, points, color, width: float, collection: bpy.types.Collection):
    curve = bpy.data.curves.new(name + "_곡선", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = width
    curve.bevel_resolution = 3
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for target, point in zip(spline.points, points):
        target.co = (*point, 1.0)
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    obj.data.materials.append(material(name + "_재질", color))
    return obj


def create_arrowhead(name: str, location, color, collection: bpy.types.Collection, direction: float = 1.0):
    mesh = bpy.data.meshes.new(name + "_메시")
    mesh.from_pydata(
        [(-0.24, -0.42 * direction, 0.0), (0.24, -0.42 * direction, 0.0), (0.0, 0.42 * direction, 0.0)],
        [],
        [(0, 1, 2)],
    )
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(material(name + "_재질", color))
    return obj


def create_position_marker(name: str, location, color, collection: bpy.types.Collection):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.58, minor_radius=0.07, major_segments=48, minor_segments=12, location=location)
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.data.materials.append(material(name + "_재질", color))
    return obj


def root_meshes(root: bpy.types.Object):
    return [obj for obj in root.children_recursive if obj.type == "MESH"]


def move_root_bbox_center_x(root: bpy.types.Object, target_x: float) -> None:
    low, high = bounds(root_meshes(root))
    root.location.x += target_x - ((low.x + high.x) * 0.5)
    bpy.context.view_layer.update()


def open_gate_door(gate: bpy.types.Object) -> tuple[bpy.types.Object, float, float]:
    door = next((obj for obj in gate.children_recursive if obj.type == "MESH" and "Gate_Door" in obj.name), None)
    if door is None:
        raise RuntimeError("CHOKEPOINT_GATE_DOOR_MISSING")
    low, high = bounds([door])
    closed_width = high.x - low.x
    if closed_width <= 0.0:
        raise RuntimeError("CHOKEPOINT_GATE_DOOR_WIDTH_INVALID")
    gate.scale.x *= CLEAR_PASSAGE_METERS / closed_width
    bpy.context.view_layer.update()
    move_root_bbox_center_x(gate, 0.0)
    ground_root(gate)
    bpy.context.view_layer.update()
    low, high = bounds([door])
    hinge = Vector((low.x, (low.y + high.y) * 0.5, (low.z + high.z) * 0.5))
    door_world = door.matrix_world.copy()
    pivot = bpy.data.objects.new("출입문_안쪽개방Pivot", None)
    bpy.context.scene.collection.objects.link(pivot)
    pivot.location = hinge
    pivot["opens_toward"] = "+Y_FarmInterior"
    pivot["opening_angle_degrees"] = 82.0
    bpy.context.view_layer.update()
    door.parent = pivot
    door.matrix_parent_inverse = pivot.matrix_world.inverted()
    door.matrix_world = door_world
    pivot.rotation_euler.z = math.radians(82.0)
    bpy.context.view_layer.update()
    return door, closed_width, high.x - low.x


def rounded_up_quarter_meter(value: float) -> float:
    return math.ceil(max(0.0, value) * 4.0 - 1e-9) / 4.0


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) != 2:
        raise SystemExit("usage: render_h1_fence_defense_chokepoint.py -- <source-blend> <output-directory>")
    source_blend = Path(args[0]).resolve()
    output_dir = Path(args[1]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    calibration_path = source_blend.parent / "asset-calibrations.json"
    catalog = json.loads(calibration_path.read_text(encoding="utf-8-sig"))
    calibrations = {item["AssetKey"]: item for item in catalog["Items"]}
    if not SOURCE_ASSET_KEYS.issubset(calibrations):
        raise RuntimeError("CHOKEPOINT_CALIBRATION_MISSING")
    roots = isolate_existing_roots()
    gate = roots["wood-gate-01"]
    left = roots["south-fence-09"]
    right = roots["south-fence-10"]
    for root in roots.values():
        root.location.x, root.location.y = 0.0, 0.0
        root.rotation_euler = (0.0, 0.0, 0.0)
        root["h1_review_stable_id"] = STABLE_ID
        root["candidate_revision"] = REVISION
        root["source_asset_state"] = "SyntySourceUnmodified"
        ground_root(root)
    bpy.context.view_layer.update()

    door, door_closed_width_before_scale, door_closed_width_after_scale = open_gate_door(gate)
    frame = next((obj for obj in gate.children_recursive if obj.type == "MESH" and "Gate_01" in obj.name and "Gate_Door" not in obj.name), None)
    if frame is None:
        raise RuntimeError("CHOKEPOINT_GATE_FRAME_MISSING")
    frame_low, frame_high = bounds([frame])
    left_low, left_high = bounds(root_meshes(left))
    right_low, right_high = bounds(root_meshes(right))
    left_width = left_high.x - left_low.x
    right_width = right_high.x - right_low.x
    left.scale.x *= FENCE_SHOULDER_METERS / left_width
    right.scale.x *= FENCE_SHOULDER_METERS / right_width
    bpy.context.view_layer.update()
    left_low, left_high = bounds(root_meshes(left))
    right_low, right_high = bounds(root_meshes(right))
    left_width = left_high.x - left_low.x
    right_width = right_high.x - right_low.x
    move_root_bbox_center_x(left, frame_low.x - left_width * 0.5 + 0.04)
    move_root_bbox_center_x(right, frame_high.x + right_width * 0.5 - 0.04)
    ground_root(left)
    ground_root(right)
    bpy.context.view_layer.update()

    origin = bpy.data.objects.new("울타리방어병목_H1_원점", None)
    bpy.context.scene.collection.objects.link(origin)
    origin.location = (0.0, 0.0, 0.0)
    origin["stable_id"] = STABLE_ID
    origin["candidate_revision"] = REVISION
    origin["forward_axis"] = "+Y_FarmInterior"
    origin["outside_axis"] = "-Y_EnemyApproach"
    origin["clear_passage_meters"] = CLEAR_PASSAGE_METERS
    for name, x in (("통과폭_왼쪽기준", -CLEAR_PASSAGE_METERS * 0.5), ("통과폭_오른쪽기준", CLEAR_PASSAGE_METERS * 0.5)):
        marker = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(marker)
        marker.location = (x, 0.0, 0.0)
        marker["clearance_boundary"] = True

    create_ground()
    overlay = bpy.data.collections.new("설명표식_r2")
    bpy.context.scene.collection.children.link(overlay)
    enemy_color = (0.82, 0.055, 0.03, 1.0)
    return_color = (0.04, 0.36, 0.92, 1.0)
    player_color = (0.02, 0.44, 1.0, 1.0)
    hans_color = (0.08, 0.76, 0.34, 1.0)
    create_route("적_주접근선", [(0.0, -8.4, 0.08), (0.0, -4.0, 0.08), (0.0, -1.15, 0.08)], enemy_color, 0.065, overlay)
    create_arrowhead("적_주접근선_화살촉", (0.0, -0.88, 0.09), enemy_color, overlay, 1.0)
    create_route("방어후_내부복귀선", [(0.0, 1.15, 0.09), (0.0, 3.8, 0.09), (0.0, 6.3, 0.09)], return_color, 0.055, overlay)
    create_arrowhead("방어후_내부복귀선_화살촉", (0.0, 6.55, 0.10), return_color, overlay, 1.0)
    create_position_marker("플레이어_방어위치", (-DEFENDER_X_OFFSET_METERS, DEFENDER_INSIDE_Y_METERS, 0.09), player_color, overlay)
    create_position_marker("한스_방어위치", (DEFENDER_X_OFFSET_METERS, DEFENDER_INSIDE_Y_METERS, 0.09), hans_color, overlay)

    bpy.ops.object.camera_add(location=(4.5, -15.5, 12.5))
    camera = bpy.context.object
    camera.name = "병목형H1_검토Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 14.5
    look_at(camera, (0.0, -0.25, 0.45))
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(-4.0, -5.0, 12.0))
    key = bpy.context.object
    key.name = "병목형H1_주광"
    key.data.energy = 1800
    key.data.shape = "DISK"
    key.data.size = 10.0
    look_at(key, (0.0, 0.0, 0.0))
    bpy.ops.object.light_add(type="AREA", location=(7.0, 5.0, 7.0))
    fill = bpy.context.object
    fill.name = "병목형H1_보조광"
    fill.data.energy = 900
    fill.data.size = 8.0
    look_at(fill, (0.0, 0.0, 0.0))

    scene = bpy.context.scene
    scene["h1_review_stable_id"] = STABLE_ID
    scene["candidate_revision"] = REVISION
    scene["review_state"] = "VisualCandidatePendingUserApproval"
    scene["evidence_boundary"] = "PresentationE4Only_NoUnityE5"
    scene["clear_passage_meters"] = CLEAR_PASSAGE_METERS
    scene["farm_interior_direction"] = "+Y"
    scene["enemy_approach_direction"] = "-Y"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.11, 0.14, 0.17, 1.0)
    background.inputs["Strength"].default_value = 0.65

    blend_path = output_dir / "울타리-방어-병목지점-H1-r2.blend"
    clean_image_path = output_dir / "울타리-방어-병목지점-H1-r2-clean.png"
    overlay_image_path = output_dir / "울타리-방어-병목지점-H1-r2-overlay.png"
    overlay.hide_render = True
    scene.render.filepath = str(clean_image_path)
    bpy.ops.render.render(write_still=True)
    overlay.hide_render = False
    scene.render.filepath = str(overlay_image_path)
    bpy.ops.render.render(write_still=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    physical_low, physical_high = bounds(root_meshes(gate) + [door] + root_meshes(left) + root_meshes(right))
    physical_size = physical_high - physical_low
    result = {
        "schemaVersion": 1,
        "stableId": STABLE_ID,
        "candidateRevision": REVISION,
        "title": "울타리 방어 병목 지점 H1 r2",
        "sourceBlend": str(source_blend),
        "sourceBlendSha256": sha256(source_blend),
        "blend": str(blend_path),
        "blendSha256": sha256(blend_path),
        "cleanImage": str(clean_image_path),
        "cleanImageSha256": sha256(clean_image_path),
        "overlayImage": str(overlay_image_path),
        "overlayImageSha256": sha256(overlay_image_path),
        "sourceAssetKeys": sorted(SOURCE_ASSET_KEYS),
        "sourceAssetState": "SyntySourceUnmodified_DerivedSceneTransformsOnly",
        "spatialContract": {
            "pivot": "passage-center-ground",
            "forward": "+Y_FarmInterior",
            "enemyApproach": "-Y_Outside",
            "clearPassageMeters": CLEAR_PASSAGE_METERS,
            "gateDoorClosedWidthBeforeScaleMeters": round(door_closed_width_before_scale, 4),
            "gateDoorClosedWidthAfterScaleMeters": round(door_closed_width_after_scale, 4),
            "gateDoorOpeningDegrees": 82.0,
            "leftAndRightFenceShouldersMeters": [round(left_width, 4), round(right_width, 4)],
            "defenderPositionsMeters": {
                "player": [-DEFENDER_X_OFFSET_METERS, DEFENDER_INSIDE_Y_METERS],
                "hans": [DEFENDER_X_OFFSET_METERS, DEFENDER_INSIDE_Y_METERS]
            },
            "measuredBoundsMeters": [round(physical_size.x, 4), round(physical_size.y, 4), round(physical_size.z, 4)],
            "placementFootprintMetersRoundedUpTo25cm": [
                rounded_up_quarter_meter(physical_size.x),
                rounded_up_quarter_meter(physical_size.y)
            ]
        },
        "visualLegend": {
            "red": "적 주 접근선",
            "blue": "플레이어 방어 위치와 내부 복귀선",
            "green": "한스 방어 위치"
        },
        "cleanImageOverlayObjectsRendered": False,
        "overlayImageOverlayObjectsRendered": True,
        "reviewStateCode": "VisualCandidatePendingUserApproval",
        "authorityBoundary": "Integrated H1 plus fence-extension Presentation E4 visual candidate only; not Graph authority, Unity placement, runtime defense, or E5 evidence."
    }
    (output_dir / "검증결과-r2.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
