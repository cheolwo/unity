"""Unity 지형·Prefab 교정 사본으로 Blender AreaSet 편집 초안을 생성한다."""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("AREA_SET_ASSET_HAS_NO_MESH")
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def material(name: str, color, texture: Path | None = None):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.82
    if texture and texture.exists():
        image_node = value.node_tree.nodes.get("Image Texture") or value.node_tree.nodes.new("ShaderNodeTexImage")
        image_node.image = bpy.data.images.load(str(texture), check_existing=True)
        image_node.interpolation = "Closest"
        value.node_tree.links.new(image_node.outputs["Color"], bsdf.inputs["Base Color"])
        value.node_tree.links.new(image_node.outputs["Alpha"], bsdf.inputs["Alpha"])
    return value


def make_surface(snapshot):
    columns = snapshot["Columns"]
    rows = snapshot["Rows"]
    min_x, max_x = snapshot["MinimumX"], snapshot["MaximumX"]
    min_z, max_z = snapshot["MinimumZ"], snapshot["MaximumZ"]
    step_x = (max_x - min_x) / (columns - 1)
    step_z = (max_z - min_z) / (rows - 1)
    vertices = []
    for z_index in range(rows):
        for x_index in range(columns):
            x = min_x + x_index * step_x
            unity_z = min_z + z_index * step_z
            unity_y = snapshot["Heights"][z_index * columns + x_index]
            vertices.append((x, unity_z, unity_y))
    raw = snapshot["Triangles"]
    faces = [(raw[index], raw[index + 1], raw[index + 2]) for index in range(0, len(raw), 3)]
    mesh = bpy.data.meshes.new("Unity지표면Snapshot")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Unity지표면_" + snapshot["SurfaceHashSha256"][:12], mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material("Unity지표면", (0.25, 0.38, 0.19)))
    obj["area_set_surface_hash"] = snapshot["SurfaceHashSha256"]
    obj.hide_select = True
    return obj


def sample_surface(snapshot, x: float, z: float) -> float:
    columns = snapshot["Columns"]
    rows = snapshot["Rows"]
    min_x, max_x = snapshot["MinimumX"], snapshot["MaximumX"]
    min_z, max_z = snapshot["MinimumZ"], snapshot["MaximumZ"]
    if not (min_x <= x <= max_x and min_z <= z <= max_z):
        raise RuntimeError(f"AREA_SET_PLACEMENT_OUTSIDE_SURFACE:{x}:{z}")
    step_x = (max_x - min_x) / (columns - 1)
    step_z = (max_z - min_z) / (rows - 1)
    x_pos = (x - min_x) / step_x
    z_pos = (z - min_z) / step_z
    xi = min(math.floor(x_pos), columns - 2)
    zi = min(math.floor(z_pos), rows - 2)
    u = max(0.0, min(1.0, x_pos - xi))
    v = max(0.0, min(1.0, z_pos - zi))
    heights = snapshot["Heights"]
    at = lambda px, pz: heights[pz * columns + px]
    a, b, c, d = at(xi, zi), at(xi + 1, zi), at(xi, zi + 1), at(xi + 1, zi + 1)
    if u + v <= 1.0:
        return a + u * (b - a) + v * (c - a)
    return d + (1.0 - u) * (c - d) + (1.0 - v) * (b - d)


def create_template(project_root: Path, calibration):
    source = project_root / calibration["ModelAssetPath"]
    if not source.exists():
        raise FileNotFoundError(source)
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = bounds(meshes)
    observed_size = high - low
    desired_size = Vector((calibration["BoundsSizeX"], calibration["BoundsSizeZ"], calibration["BoundsSizeY"]))
    ratios = [desired_size[i] / observed_size[i] for i in range(3) if observed_size[i] > 0.000001]
    if len(ratios) != 3:
        raise RuntimeError("AREA_SET_ASSET_DIMENSION_INVALID:" + calibration["AssetKey"])
    factor = sorted(ratios)[1]
    deviation = max(abs(value - factor) / max(abs(factor), 0.000001) for value in ratios)
    if deviation > 0.08:
        raise RuntimeError(
            f"AREA_SET_ASSET_AXIS_OR_SCALE_MISMATCH:{calibration['AssetKey']}:{ratios}"
        )

    placement_root = bpy.data.objects.new("TEMPLATE_" + calibration["AssetKey"], None)
    geometry_root = bpy.data.objects.new("GEOMETRY_" + calibration["AssetKey"], None)
    bpy.context.scene.collection.objects.link(placement_root)
    bpy.context.scene.collection.objects.link(geometry_root)
    geometry_root.parent = placement_root
    for obj in imported:
        world = obj.matrix_world.copy()
        obj.parent = geometry_root
        obj.matrix_world = world

    observed_center = (low + high) * 0.5
    desired_center = Vector(
        (
            calibration["BoundsCenterX"],
            calibration["BoundsCenterZ"],
            calibration["BoundsCenterY"],
        )
    )
    geometry_root.scale = (factor, factor, factor)
    geometry_root.location = desired_center - observed_center * factor
    texture = project_root / calibration["TextureAssetPath"] if calibration["TextureAssetPath"] else None
    atlas = material("Atlas_" + calibration["AssetKey"], (0.65, 0.65, 0.65), texture)
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(atlas)
    placement_root.hide_render = True
    placement_root.hide_viewport = True
    return placement_root


def duplicate_template(template, name: str):
    root = template.copy()
    root.name = name
    root.hide_render = False
    root.hide_viewport = False
    bpy.context.scene.collection.objects.link(root)
    for child in template.children:
        child_copy = child.copy()
        bpy.context.scene.collection.objects.link(child_copy)
        child_copy.parent = root
        child_copy.location = child.location.copy()
        child_copy.rotation_euler = child.rotation_euler.copy()
        child_copy.scale = child.scale.copy()
        for grandchild in child.children:
            mesh_copy = grandchild.copy()
            mesh_copy.data = grandchild.data
            bpy.context.scene.collection.objects.link(mesh_copy)
            mesh_copy.parent = child_copy
            mesh_copy.location = grandchild.location.copy()
            mesh_copy.rotation_euler = grandchild.rotation_euler.copy()
            mesh_copy.scale = grandchild.scale.copy()
    return root


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) != 2:
        raise SystemExit("usage: render_area_set_draft.py -- <authoring-directory> <output-directory>")
    authoring_dir = Path(args[0]).resolve()
    output_dir = Path(args[1]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    project_root = Path(__file__).resolve().parents[3]
    surface_path = authoring_dir / "surface-snapshot.json"
    catalog_path = authoring_dir / "asset-calibrations.json"
    draft_path = authoring_dir / "blender-draft.json"
    surface = read_json(surface_path)
    catalog = read_json(catalog_path)
    draft = read_json(draft_path)
    if draft["SurfaceHashSha256"].upper() != surface["SurfaceHashSha256"].upper():
        raise RuntimeError("AREA_SET_SURFACE_SNAPSHOT_STALE")
    if draft["CalibrationCatalogHashSha256"].upper() != catalog["CatalogHashSha256"].upper():
        raise RuntimeError("AREA_SET_CALIBRATION_CATALOG_STALE")

    clear_scene()
    make_surface(surface)
    calibrations = {item["AssetKey"]: item for item in catalog["Items"]}
    templates = {key: create_template(project_root, value) for key, value in calibrations.items()}
    rendered = []
    for placement in draft["Placements"]:
        if not placement["Enabled"]:
            continue
        calibration = calibrations[placement["AssetKey"]]
        if placement["AssetFingerprint"].upper() != calibration["AssetFingerprint"].upper():
            raise RuntimeError("AREA_SET_ASSET_FINGERPRINT_STALE:" + placement["PlacementStableId"])
        root = duplicate_template(templates[placement["AssetKey"]], placement["PlacementStableId"])
        surface_height = sample_surface(surface, placement["LocalX"], placement["LocalZ"])
        offset = placement["SurfaceOffsetMeters"] if placement["HeightPolicyCode"] == "SurfaceOffset" else 0.0
        resolved_y = surface_height - calibration["BoundsMinimumY"] * placement["UniformScale"] + offset
        root.location = (placement["LocalX"], placement["LocalZ"], resolved_y)
        root.rotation_euler.z = -math.radians(placement["RotationYDegrees"])
        root.scale = (placement["UniformScale"],) * 3
        root.lock_location[2] = True
        root.lock_rotation[0] = True
        root.lock_rotation[1] = True
        root["area_set_placement_stable_id"] = placement["PlacementStableId"]
        root["area_set_h1_stable_id"] = placement["H1StableId"]
        root["area_set_asset_key"] = placement["AssetKey"]
        root["area_set_asset_fingerprint"] = placement["AssetFingerprint"]
        root["area_set_expected_blender_z"] = resolved_y
        rendered.append(
            {
                "placementStableId": placement["PlacementStableId"],
                "surfaceHeightMeters": round(surface_height, 6),
                "resolvedUnityY": round(resolved_y, 6),
            }
        )

    for template in templates.values():
        bpy.data.objects.remove(template, do_unlink=True)

    camera_recipe = draft["Camera"]
    bpy.ops.object.camera_add(
        location=(camera_recipe["PositionX"], camera_recipe["PositionZ"], camera_recipe["PositionY"])
    )
    camera = bpy.context.object
    camera.name = "Unity동일구도Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = camera_recipe["OrthographicSize"] * 2.0
    look_at(camera, (camera_recipe["TargetX"], camera_recipe["TargetZ"], camera_recipe["TargetY"]))
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(-12.0, -18.0, 30.0))
    key_light = bpy.context.object
    key_light.name = "AreaSet_주광"
    key_light.data.energy = 3200
    key_light.data.shape = "DISK"
    key_light.data.size = 18.0
    look_at(key_light, (0.0, 0.0, 0.0))
    bpy.ops.object.light_add(type="AREA", location=(18.0, 10.0, 18.0))
    fill = bpy.context.object
    fill.name = "AreaSet_보조광"
    fill.data.energy = 1800
    fill.data.size = 14.0
    look_at(fill, (0.0, 0.0, 0.0))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = camera_recipe["PixelWidth"]
    scene.render.resolution_y = camera_recipe["PixelHeight"]
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output_dir / "blender-authoring-overview.png")
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 1.2
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.16, 0.20, 0.24, 1.0)
    background.inputs["Strength"].default_value = 0.7
    scene["area_set_draft_hash"] = sha256(draft_path)
    scene["area_set_surface_hash"] = surface["SurfaceHashSha256"]
    scene["area_set_calibration_hash"] = catalog["CatalogHashSha256"]
    blend_path = output_dir / "forest-edge-farm-area-set-draft-r3.blend"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.render.render(write_still=True)
    receipt = {
        "schemaVersion": 1,
        "draftStableId": draft["DraftStableId"],
        "draftRevision": draft["DraftRevision"],
        "draftFileSha256": sha256(draft_path),
        "surfaceHashSha256": surface["SurfaceHashSha256"],
        "calibrationCatalogHashSha256": catalog["CatalogHashSha256"],
        "coordinateSpaceCode": draft["CoordinateSpaceCode"],
        "axisCode": draft["AxisCode"],
        "metersPerUnit": draft["MetersPerUnit"],
        "placementCount": len(rendered),
        "placements": rendered,
        "authorityBoundary": "Editable E4 authoring draft; not Unity Scene, Game View, or E5 evidence.",
    }
    (output_dir / "blender-render-receipt.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
