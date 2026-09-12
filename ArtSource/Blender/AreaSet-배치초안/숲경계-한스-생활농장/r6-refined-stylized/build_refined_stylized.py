import hashlib
import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
PROJECT = Path("C:/Users/user/ssalddel")
SOURCE_BLEND = ROOT.parent / "r5-natural-world" / "forest-edge-hans-farm-natural-world-r5-r3-final.blend"
OUTPUT_BLEND = ROOT / "forest-edge-hans-farm-refined-stylized-r6.blend"
OVERVIEW = ROOT / "blender-overview-refined-r1.png"
EYE = ROOT / "blender-eye-level-refined-r1.png"
RECEIPT = ROOT / "blender-refined-receipt.json"
ATLAS = PROJECT / "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def make_plain(name, color, roughness):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def make_weathered_atlas():
    material = bpy.data.materials.get("HansHouseAtlasWeathered_r1") or bpy.data.materials.new("HansHouseAtlasWeathered_r1")
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    image = tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(ATLAS), check_existing=True)
    image.interpolation = "Closest"
    mix = tree.nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "SCREEN"
    mix.inputs[0].default_value = 0.58
    mix.inputs[2].default_value = (0.58, 0.36, 0.18, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.78
    tree.links.new(image.outputs["Color"], mix.inputs[1])
    tree.links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def add_box(parent, name, location, dimensions, material, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = location
    obj.rotation_euler = rotation
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def build():
    if Path(bpy.data.filepath).resolve() != SOURCE_BLEND.resolve():
        raise RuntimeError("UnexpectedSourceBlend")
    if OUTPUT_BLEND.exists() or OVERVIEW.exists() or EYE.exists() or RECEIPT.exists():
        raise RuntimeError("RefinedOutputAlreadyExists")

    house_root = bpy.data.objects.get("P000_damaged-home")
    house_mesh = bpy.data.objects.get("VARIANT_SM_Bld_Farmhouse_02.001")
    if house_root is None or house_mesh is None:
        raise RuntimeError("HansHouseObjectsMissing")
    atlas = make_weathered_atlas()
    house_mesh.data.materials.clear()
    house_mesh.data.materials.append(atlas)
    for obj in bpy.data.objects:
        if obj.name.startswith("Damage_LooseRoofBoard"):
            obj.hide_render = True
            obj.hide_viewport = True

    roof = make_plain("HansHouseRoofWeathered_r1", (0.16, 0.11, 0.07), 0.88)
    wood = make_plain("HansHouseExposedWood_r1", (0.36, 0.20, 0.09), 0.82)
    post = make_plain("HansFencePostWeathered_r1", (0.30, 0.18, 0.08), 0.88)
    route_edge = make_plain("HansRouteEdgeBlend_r1", (0.15, 0.24, 0.09), 0.96)

    detail_root = bpy.data.objects.new("HansFarmRefinedStylized_r1", None)
    bpy.context.scene.collection.objects.link(detail_root)
    detail_root.parent = house_root
    add_box(detail_root, "RoofFascia_Front", (0.95, -3.20, 4.05), (6.10, 0.16, 0.16), roof)
    add_box(detail_root, "RoofFascia_Back", (0.95, 3.20, 4.05), (6.10, 0.16, 0.16), roof)

    fence_posts = 0
    for obj in list(bpy.data.objects):
        if not obj.name.startswith("P") or "wood-fence" not in obj.name:
            continue
        add_box(obj, "RefinedFencePost_r1", (0.0, 0.0, 0.56), (0.18, 0.18, 1.46), post)
        fence_posts += 1

    grass_roots = []
    for obj in bpy.data.objects:
        if obj.name.startswith("P") and "grass" in obj.name:
            obj.scale = tuple(value * 0.62 for value in obj.scale)
            grass_roots.append(obj.name)

    for obj in list(bpy.data.objects):
        if not obj.name.startswith("RouteSurface_") or obj.type != "MESH":
            continue
        edge = obj.copy()
        edge.data = obj.data.copy()
        edge.name = "EdgeBlend_" + obj.name
        bpy.context.scene.collection.objects.link(edge)
        edge.scale = (obj.scale.x * 1.08, obj.scale.y * 1.08, obj.scale.z)
        edge.location.z -= 0.018
        edge.data.materials.clear()
        edge.data.materials.append(route_edge)

    world = bpy.context.scene.world
    world.color = (0.055, 0.075, 0.095)
    sun = bpy.data.objects.get("ReviewSun")
    if sun is not None:
        sun.data.energy = 2.1
        sun.data.color = (1.0, 0.84, 0.68)
    fill = bpy.data.objects.get("ReviewFill")
    if fill is not None:
        fill.data.energy = 760.0
        fill.data.color = (0.62, 0.76, 1.0)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.camera = bpy.data.objects["ReviewOverview"]
    scene.render.filepath = str(OVERVIEW)
    bpy.ops.render.render(write_still=True)
    scene.camera = bpy.data.objects["ReviewEyeLevel"]
    scene.render.filepath = str(EYE)
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND), check_existing=False)

    receipt = {
        "schemaVersion": 1,
        "revision": "hans-farm-refined-stylized.r1",
        "sourceBlend": str(SOURCE_BLEND),
        "sourceBlendSha256": sha256(SOURCE_BLEND),
        "outputBlend": str(OUTPUT_BLEND),
        "outputBlendSha256": sha256(OUTPUT_BLEND),
        "overviewSha256": sha256(OVERVIEW),
        "eyeLevelSha256": sha256(EYE),
        "houseAtlasTexture": str(ATLAS),
        "houseUvLayerCount": len(house_mesh.data.uv_layers),
        "houseVertexColorLayerCount": len(house_mesh.data.color_attributes),
        "fencePostCount": fence_posts,
        "scaledGrassRoots": grass_roots,
        "authorityChanged": False,
        "sourceModified": False,
        "visualApproval": "Pending",
    }
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


build()
