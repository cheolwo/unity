import hashlib
import json
import math
import os
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
PROJECT = Path("C:/Users/user/ssalddel")
PLACEMENT_PATH = ROOT / "validated-placement-plan.r3.json"
DRAFT_PATH = ROOT / "placement-map.r5.r3.json"
CALIBRATION_PATH = ROOT / "asset-calibrations.r3.json"
SURFACE_PATH = ROOT / "surface-snapshot.r3.json"
BLEND_PATH = ROOT / "forest-edge-hans-farm-natural-world-r5-r3-final.blend"
OVERVIEW_PATH = ROOT / "blender-overview-r3-final.png"
EYE_PATH = ROOT / "blender-eye-level-r3-final.png"
RECEIPT_PATH = ROOT / "blender-render-receipt-r3-final.json"


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def load_json(path):
    with open(path, "r", encoding="utf-8-sig") as stream:
        return json.load(stream)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def make_material(name, color, roughness=0.8):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def make_textured_material(asset_key, texture_path):
    if not texture_path:
        return None
    absolute = PROJECT / texture_path
    if not absolute.exists():
        return None
    material = bpy.data.materials.new("ReviewAtlas_" + asset_key)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = bpy.data.images.load(str(absolute), check_existing=True)
    image_node.interpolation = "Linear"
    links.new(image_node.outputs["Color"], bsdf.inputs["Base Color"])
    if image_node.outputs.get("Alpha"):
        links.new(image_node.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.82
    return material


def import_fbx(path):
    before = set(bpy.data.objects)
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))
    return [obj for obj in bpy.data.objects if obj not in before]


def build_template(asset_key, model_path, material):
    imported = import_fbx(PROJECT / model_path)
    if not imported:
        raise RuntimeError("FBX imported no objects: " + model_path)
    root = bpy.data.objects.new("TEMPLATE_" + asset_key, None)
    bpy.context.scene.collection.objects.link(root)
    roots = [obj for obj in imported if obj.parent is None]
    for obj in roots:
        obj.parent = root
    for obj in imported:
        obj.hide_viewport = True
        obj.hide_render = True
        if material is not None and obj.type == "MESH":
            obj.data.materials.clear()
            obj.data.materials.append(material)
        elif asset_key == "damaged-home" and obj.type == "MESH":
            for slot in obj.material_slots:
                source_name = slot.material.name.lower() if slot.material else ""
                material_name = "ReviewHomeWeathered"
                color = (0.48, 0.34, 0.20)
                if "roof" in source_name:
                    material_name = "ReviewHomeRoof"
                    color = (0.11, 0.085, 0.06)
                elif "glass" in source_name or "window" in source_name:
                    material_name = "ReviewHomeWindow"
                    color = (0.16, 0.36, 0.45)
                elif "wood" in source_name:
                    material_name = "ReviewHomeExposedWood"
                    color = (0.32, 0.16, 0.07)
                replacement = bpy.data.materials.get(material_name)
                if replacement is None:
                    replacement = make_material(material_name, color, 0.82)
                slot.material = replacement
        elif asset_key in {"pine", "large-pine", "bush", "grass"} and obj.type == "MESH":
            for slot in obj.material_slots:
                source_name = slot.material.name.lower() if slot.material else ""
                material_name = "ReviewNatureLeaves"
                color = (0.08, 0.28, 0.075)
                if "trunk" in source_name:
                    material_name = "ReviewNatureTrunk"
                    color = (0.20, 0.105, 0.045)
                elif "grass" in source_name or "plant" in source_name:
                    material_name = "ReviewNatureGrass"
                    color = (0.16, 0.40, 0.08)
                replacement = bpy.data.materials.get(material_name)
                if replacement is None:
                    replacement = make_material(material_name, color, 0.92)
                slot.material = replacement
    root.hide_viewport = True
    root.hide_render = True
    return root, imported


def duplicate_template(asset_key, template_root, template_objects, name):
    mapping = {}
    root = template_root.copy()
    root.name = name
    root.hide_viewport = False
    root.hide_render = False
    bpy.context.scene.collection.objects.link(root)
    mapping[template_root] = root
    for source in template_objects:
        target = source.copy()
        target.data = source.data
        target.hide_viewport = False
        target.hide_render = False
        bpy.context.scene.collection.objects.link(target)
        mapping[source] = target
    for source, target in mapping.items():
        if source is template_root:
            continue
        target.parent = mapping.get(source.parent, root)
        target.matrix_parent_inverse = source.matrix_parent_inverse.copy()
    return root


def build_surface(surface):
    columns = int(surface["Columns"])
    rows = int(surface["Rows"])
    minimum_x = float(surface["MinimumX"])
    maximum_x = float(surface["MaximumX"])
    minimum_z = float(surface["MinimumZ"])
    maximum_z = float(surface["MaximumZ"])
    heights = [float(v) for v in surface["Heights"]]
    vertices = []
    for row in range(rows):
        unity_z = minimum_z + (maximum_z - minimum_z) * row / (rows - 1)
        for column in range(columns):
            unity_x = minimum_x + (maximum_x - minimum_x) * column / (columns - 1)
            unity_y = heights[row * columns + column]
            vertices.append((unity_x, -unity_z, unity_y))
    faces = []
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            b = a + 1
            c = a + columns + 1
            d = a + columns
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new("ContinuousScenarioTerrain_r5")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("ContinuousScenarioTerrain_r5", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(make_material("ReviewTerrain", (0.22, 0.43, 0.16), 0.95))
    return obj


def sample_surface(surface, unity_x, unity_z):
    columns = int(surface["Columns"])
    rows = int(surface["Rows"])
    min_x, max_x = float(surface["MinimumX"]), float(surface["MaximumX"])
    min_z, max_z = float(surface["MinimumZ"]), float(surface["MaximumZ"])
    x_position = (unity_x - min_x) / ((max_x - min_x) / (columns - 1))
    z_position = (unity_z - min_z) / ((max_z - min_z) / (rows - 1))
    xi = min(int(math.floor(x_position)), columns - 2)
    zi = min(int(math.floor(z_position)), rows - 2)
    u, v = x_position - xi, z_position - zi
    heights = surface["Heights"]
    a = float(heights[zi * columns + xi])
    b = float(heights[zi * columns + xi + 1])
    c = float(heights[(zi + 1) * columns + xi])
    d = float(heights[(zi + 1) * columns + xi + 1])
    return a + u * (b - a) + v * (c - a) if u + v <= 1.0 else d + (1.0 - u) * (c - d) + (1.0 - v) * (b - d)


def build_continuous_route(surface, route):
    width = 2.6
    lift = 0.035
    points = [(float(p["X"]), float(p["Z"])) for p in route["Points"]]
    samples = []
    for segment in range(len(points) - 1):
        start, end = points[segment], points[segment + 1]
        distance = math.dist(start, end)
        steps = max(1, math.ceil(distance))
        for step in range(steps):
            t = step / steps
            samples.append((start[0] + (end[0] - start[0]) * t, start[1] + (end[1] - start[1]) * t))
    samples.append(points[-1])
    vertices, faces = [], []
    for index, point in enumerate(samples):
        previous = samples[max(0, index - 1)]
        following = samples[min(len(samples) - 1, index + 1)]
        dx, dz = following[0] - previous[0], following[1] - previous[1]
        length = math.hypot(dx, dz)
        right_x, right_z = dz / length * width * 0.5, -dx / length * width * 0.5
        left = (point[0] - right_x, point[1] - right_z)
        right = (point[0] + right_x, point[1] + right_z)
        vertices.append((left[0], -left[1], sample_surface(surface, *left) + lift))
        vertices.append((right[0], -right[1], sample_surface(surface, *right) + lift))
        if index:
            start = (index - 1) * 2
            faces.append((start, start + 2, start + 3, start + 1))
    suffix = route["RouteStableId"].split(":")[-1].replace("-", "_")
    mesh = bpy.data.meshes.new("HansRoute_Continuous_" + suffix + "_r3")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("RouteSurface_" + suffix + "_Continuous_r3", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(make_material("ReviewForestTrail", (0.36, 0.20, 0.095), 0.96))
    return obj


def point_camera(camera, target):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_camera(name, location, target, lens=48.0):
    camera_data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = location
    camera.data.lens = lens
    camera.data.clip_end = 500.0
    point_camera(camera, target)
    return camera


def render(camera, output_path):
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)


def main():
    placement = load_json(PLACEMENT_PATH)
    draft = load_json(DRAFT_PATH)
    calibration = load_json(CALIBRATION_PATH)
    surface = load_json(SURFACE_PATH)
    clear_scene()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.16, 0.20, 0.28)
    scene.view_settings.look = "AgX - Medium High Contrast"

    world = scene.world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.62, 0.86, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55

    build_surface(surface)
    for route in draft["Routes"]:
        if route["RouteStableId"] in {
            "gm-edge:forest-trail-to-farm-entry",
            "gm-edge:farm-entry-to-hans-house",
            "gm-edge:farm-entry-to-production",
        }:
            build_continuous_route(surface, route)
    items = {item["AssetKey"]: item for item in calibration["Items"]}
    templates = {}
    import_failures = []
    for asset_key, item in items.items():
        try:
            material = None if asset_key in {"pine", "large-pine", "bush", "grass"} else make_textured_material(asset_key, item.get("TextureAssetPath", ""))
            templates[asset_key] = build_template(asset_key, item["ModelAssetPath"], material)
        except Exception as exc:
            import_failures.append({"assetKey": asset_key, "error": str(exc)})

    for index, item in enumerate(placement["ResolvedPlacements"]):
        asset_key = item["AssetKey"]
        if asset_key not in templates:
            continue
        template_root, template_objects = templates[asset_key]
        root = duplicate_template(asset_key, template_root, template_objects, f"P{index:03d}_{asset_key}")
        root.location = (float(item["LocalX"]), -float(item["LocalZ"]), float(item["LocalY"]))
        root.rotation_euler[2] = math.radians(-float(item["RotationYDegrees"]))
        if asset_key == "wood-fence":
            calibration_item = items[asset_key]
            half = float(calibration_item["BoundsSizeX"]) * float(item["UniformScale"]) * 0.5
            yaw = math.radians(float(item["RotationYDegrees"]))
            dx, dz = math.cos(yaw) * half, -math.sin(yaw) * half
            left_y = sample_surface(surface, float(item["LocalX"]) - dx, float(item["LocalZ"]) - dz)
            right_y = sample_surface(surface, float(item["LocalX"]) + dx, float(item["LocalZ"]) + dz)
            root.rotation_euler[1] = math.atan2(right_y - left_y, half * 2.0)
        root.scale = (float(item["UniformScale"]),) * 3
        root["placementStableId"] = item["PlacementStableId"]

    sun_data = bpy.data.lights.new("ReviewSun", "SUN")
    sun = bpy.data.objects.new("ReviewSun", sun_data)
    bpy.context.scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(28), math.radians(-18), math.radians(138))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(12)

    fill_data = bpy.data.lights.new("ReviewFill", "AREA")
    fill = bpy.data.objects.new("ReviewFill", fill_data)
    bpy.context.scene.collection.objects.link(fill)
    fill.location = (-16, 12, 24)
    fill.data.energy = 2200
    fill.data.shape = "DISK"
    fill.data.size = 16
    point_camera(fill, (-2, 0, 1))

    overview = create_camera("ReviewOverview", (37, 40, 38), (0, 0, 1.5), 52)
    eye = create_camera("ReviewEyeLevel", (18, -18, 4.2), (-4, 7, 1.8), 48)
    render(overview, OVERVIEW_PATH)
    render(eye, EYE_PATH)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    receipt = {
        "schemaVersion": 1,
        "status": "Rendered" if not import_failures else "RenderedWithImportFailures",
        "evidenceKind": "BlenderReviewRender",
        "notGameView": True,
        "postProcessingApplied": False,
        "coordinateMapping": "Unity (X,Y,Z) -> Blender (X,-Z,Y); Unity yaw -> negative Blender Z rotation",
        "draftHashSha256": placement["DraftHashSha256"],
        "resolvedPlacementCount": len(placement["ResolvedPlacements"]),
        "instantiatedPlacementCount": len(placement["ResolvedPlacements"]) - sum(1 for p in placement["ResolvedPlacements"] if p["AssetKey"] not in templates),
        "sourceFiles": {
            "validatedPlacementPlan": sha256(PLACEMENT_PATH),
            "placementDraft": sha256(DRAFT_PATH),
            "assetCalibrations": sha256(CALIBRATION_PATH),
            "surfaceSnapshot": sha256(SURFACE_PATH),
        },
        "importFailures": import_failures,
        "outputs": {
            "blend": {"path": BLEND_PATH.name, "sha256": sha256(BLEND_PATH)},
            "overview": {"path": OVERVIEW_PATH.name, "sha256": sha256(OVERVIEW_PATH)},
            "eyeLevel": {"path": EYE_PATH.name, "sha256": sha256(EYE_PATH)},
        },
        "limits": [
            "Static review render only; no Unity runtime, traversal, interaction, or authority evidence.",
            "Terrain is the frozen surface snapshot and is not flattened.",
            "Imported FBX geometry is used; review lighting and atlas materials are presentation-only.",
        ],
    }
    with open(RECEIPT_PATH, "x", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
