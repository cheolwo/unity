"""Build the project-owned damaged-state variant for the generic farm residence H1.

The Synty FBX is imported read-only. Only duplicated mesh data and project-owned
damage geometry are saved/exported.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ID = "farm-residential-home-damaged-variant"
H_ID = "h1-stock:farm-residential-home"
EXPECTED_SOURCE_GUID = "ea1ba4b74f4bce84d977ef4f2cdaceb3"
EXPECTED_SOURCE_SHA256 = "8dbfc93740cf59125fca4263334c408e81afd55c03291a07335b07e9476a58d7"


def args_after_separator() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def move_to_collection(obj: bpy.types.Object, target: bpy.types.Collection) -> None:
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    target.objects.link(obj)


def duplicate_objects(source: list[bpy.types.Object], target: bpy.types.Collection) -> list[bpy.types.Object]:
    copies: list[bpy.types.Object] = []
    source_to_copy: dict[bpy.types.Object, bpy.types.Object] = {}
    for obj in source:
        copy = obj.copy()
        if obj.data is not None:
            copy.data = obj.data.copy()
        target.objects.link(copy)
        source_to_copy[obj] = copy
        copies.append(copy)
    for source_obj, copied_obj in source_to_copy.items():
        if source_obj.parent in source_to_copy:
            copied_obj.parent = source_to_copy[source_obj.parent]
    return copies


def make_atlas_material(texture_path: Path, weathered: bool) -> bpy.types.Material:
    material = bpy.data.materials.new(
        "ProjectOwned_FarmhouseAtlas_Weathered" if weathered else "SupplierReference_FarmhouseAtlas"
    )
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    image_node = tree.nodes.new("ShaderNodeTexImage")
    image_node.image = bpy.data.images.load(str(texture_path), check_existing=True)
    image_node.interpolation = "Closest"
    if weathered:
        coordinates = tree.nodes.new("ShaderNodeTexCoord")
        noise = tree.nodes.new("ShaderNodeTexNoise")
        weathering_ramp = tree.nodes.new("ShaderNodeValToRGB")
        brown_wash = tree.nodes.new("ShaderNodeMixRGB")
        noise.inputs["Scale"].default_value = 2.6
        noise.inputs["Detail"].default_value = 2.0
        noise.inputs["Roughness"].default_value = 0.82
        weathering_ramp.color_ramp.elements[0].position = 0.34
        weathering_ramp.color_ramp.elements[0].color = (0.08, 0.08, 0.08, 1.0)
        weathering_ramp.color_ramp.elements[1].position = 0.76
        weathering_ramp.color_ramp.elements[1].color = (0.72, 0.72, 0.72, 1.0)
        brown_wash.blend_type = "MULTIPLY"
        brown_wash.inputs[2].default_value = (0.56, 0.36, 0.18, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.90
        tree.links.new(coordinates.outputs["Generated"], noise.inputs["Vector"])
        tree.links.new(noise.outputs["Fac"], weathering_ramp.inputs["Fac"])
        tree.links.new(weathering_ramp.outputs["Color"], brown_wash.inputs["Fac"])
        tree.links.new(image_node.outputs["Color"], brown_wash.inputs[1])
        tree.links.new(brown_wash.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Roughness"].default_value = 0.78
        tree.links.new(image_node.outputs["Color"], bsdf.inputs["Base Color"])
    tree.links.new(image_node.outputs["Alpha"], bsdf.inputs["Alpha"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def make_plain_material(name: str, color: tuple[float, float, float], roughness: float = 0.8) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def assign_review_materials(objects: list[bpy.types.Object], atlas: bpy.types.Material, glass: bpy.types.Material) -> None:
    for obj in objects:
        if obj.type != "MESH":
            continue
        obj.data.materials.clear()
        obj.data.materials.append(glass if "Glass" in obj.name else atlas)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    collection: bpy.types.Collection | None = None,
    material: bpy.types.Material | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if collection is not None:
        move_to_collection(obj, collection)
    if material is not None:
        obj.data.materials.append(material)
    return obj


def boolean_roof_opening(main: bpy.types.Object, low: Vector, high: Vector) -> tuple[int, int]:
    size = high - low
    before = len(main.data.polygons)
    cutters = []
    specifications = [
        # Primary opening: three overlapping cuts leave an irregular, readable hole.
        ((0.23, -0.08, 0.80), (0.115, 0.135, 0.145), (0.0, 0.0, math.radians(6))),
        ((0.29, -0.04, 0.81), (0.090, 0.105, 0.130), (0.0, 0.0, math.radians(-11))),
        ((0.19, 0.00, 0.79), (0.065, 0.080, 0.115), (0.0, 0.0, math.radians(15))),
        # Two smaller clusters spread the damage without collapsing the roof silhouette.
        ((0.39, 0.13, 0.86), (0.060, 0.072, 0.100), (0.0, 0.0, math.radians(-8))),
        ((0.35, 0.17, 0.85), (0.045, 0.055, 0.090), (0.0, 0.0, math.radians(13))),
        ((0.16, -0.29, 0.75), (0.055, 0.066, 0.100), (0.0, 0.0, math.radians(9))),
        ((0.20, -0.25, 0.77), (0.040, 0.050, 0.085), (0.0, 0.0, math.radians(-14))),
        # User-marked central, right, and lower roof patches.
        ((0.51, -0.05, 0.80), (0.055, 0.068, 0.095), (0.0, 0.0, math.radians(7))),
        ((0.54, -0.01, 0.79), (0.030, 0.042, 0.078), (0.0, 0.0, math.radians(-12))),
        ((0.77, 0.02, 0.81), (0.050, 0.062, 0.092), (0.0, 0.0, math.radians(-6))),
        ((0.80, 0.05, 0.80), (0.028, 0.040, 0.075), (0.0, 0.0, math.radians(14))),
        ((0.80, -0.16, 0.73), (0.052, 0.060, 0.090), (0.0, 0.0, math.radians(8))),
        ((0.84, -0.12, 0.74), (0.030, 0.040, 0.075), (0.0, 0.0, math.radians(-13))),
    ]
    for index, (relative, relative_size, rotation) in enumerate(specifications, start=1):
        cutter = add_box(
            f"DamageCutter_{index:02d}",
            (
                low.x + size.x * relative[0],
                low.y + size.y * relative[1] + size.y * 0.5,
                low.z + size.z * relative[2],
            ),
            (size.x * relative_size[0], size.y * relative_size[1], size.z * relative_size[2]),
            rotation,
        )
        cutters.append(cutter)
        bpy.context.view_layer.objects.active = main
        modifier = main.modifiers.new(f"SmallRoofOpening_{index:02d}", "BOOLEAN")
        modifier.operation = "DIFFERENCE"
        modifier.solver = "EXACT"
        modifier.object = cutter
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for cutter in cutters:
        bpy.data.objects.remove(cutter, do_unlink=True)
    return before, len(main.data.polygons)


def add_damage_details(
    target_collection: bpy.types.Collection,
    low: Vector,
    high: Vector,
    dark_material: bpy.types.Material,
    wood_material: bpy.types.Material,
) -> list[bpy.types.Object]:
    size = high - low
    roof_center = Vector((low.x + size.x * 0.24, low.y + size.y * 0.46, low.z + size.z * 0.78))
    cavity_specs = [
        ("Damage_RoofCavity", (0.24, 0.46, 0.705), (0.085, 0.090, 0.014), (0.0, math.radians(-28), math.radians(4))),
        ("Damage_RoofCavity_Secondary", (0.37, 0.66, 0.805), (0.052, 0.058, 0.012), (0.0, math.radians(-28), math.radians(-6))),
        ("Damage_RoofCavity_Tertiary", (0.18, 0.22, 0.665), (0.047, 0.054, 0.012), (0.0, math.radians(-28), math.radians(8))),
        ("Damage_RoofCavity_MarkedCenter", (0.525, 0.47, 0.715), (0.044, 0.050, 0.012), (0.0, math.radians(-28), math.radians(5))),
        ("Damage_RoofCavity_MarkedRight", (0.785, 0.53, 0.725), (0.040, 0.046, 0.012), (0.0, math.radians(-28), math.radians(-5))),
        ("Damage_RoofCavity_MarkedLower", (0.82, 0.36, 0.680), (0.042, 0.046, 0.012), (0.0, math.radians(-28), math.radians(7))),
    ]
    details = []
    for name, relative, relative_size, rotation in cavity_specs:
        cavity = add_box(
            name,
            (low.x + size.x * relative[0], low.y + size.y * relative[1], low.z + size.z * relative[2]),
            (size.x * relative_size[0], size.y * relative_size[1], size.z * relative_size[2]),
            rotation,
            target_collection,
            dark_material,
        )
        details.append(cavity)
    plank_specs = [
        ((0.00, -0.060, 0.018), (0.078, 0.016, 0.013), (0.0, math.radians(-29), math.radians(-8))),
        ((0.02, 0.060, 0.022), (0.065, 0.015, 0.012), (0.0, math.radians(-27), math.radians(11))),
        ((0.06, 0.005, 0.012), (0.042, 0.013, 0.010), (0.0, math.radians(-31), math.radians(3))),
    ]
    for index, (relative, relative_size, rotation) in enumerate(plank_specs, start=1):
        plank = add_box(
            f"Damage_LooseRoofBoard_{index:02d}",
            tuple(roof_center + Vector((size.x * relative[0], size.y * relative[1], size.z * relative[2]))),
            (size.x * relative_size[0], size.y * relative_size[1], size.z * relative_size[2]),
            rotation,
            target_collection,
            wood_material,
        )
        details.append(plank)
    return details


def add_render_scene(center: Vector, low: Vector, high: Vector) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    size = high - low
    extent = max(size.x, size.y, size.z)
    review_collection = bpy.data.collections.new("REVIEW_ONLY")
    bpy.context.scene.collection.children.link(review_collection)
    ground = add_box(
        "ReviewGround",
        (center.x, center.y, low.z - 0.12),
        (extent * 3.5, extent * 3.5, 0.2),
        collection=review_collection,
        material=make_plain_material("ReviewGroundMaterial", (0.18, 0.24, 0.14)),
    )
    bpy.ops.object.light_add(type="AREA", location=(center.x + extent, center.y - extent, high.z + extent))
    key = bpy.context.object
    key.name = "ReviewKeyLight"
    key.data.energy = 1450
    key.data.shape = "DISK"
    key.data.size = extent * 0.9
    look_at(key, center)
    move_to_collection(key, review_collection)
    bpy.ops.object.light_add(type="AREA", location=(center.x - extent, center.y + extent * 0.2, high.z + extent * 0.45))
    fill = bpy.context.object
    fill.name = "ReviewFillLight"
    fill.data.energy = 850
    fill.data.size = extent * 1.1
    look_at(fill, center)
    move_to_collection(fill, review_collection)
    bpy.ops.object.camera_add(location=(center.x + extent * 1.45, center.y - extent * 1.55, center.z + extent * 0.88))
    camera = bpy.context.object
    camera.name = "ReviewCamera"
    camera.data.lens = 55
    look_at(camera, Vector((center.x, center.y, center.z * 0.88)))
    move_to_collection(camera, review_collection)
    bpy.context.scene.camera = camera
    return camera, [ground, key, fill]


def set_collection_visibility(collection: bpy.types.Collection, visible: bool) -> None:
    collection.hide_render = not visible
    collection.hide_viewport = not visible


def render(path: Path) -> None:
    scene = bpy.context.scene
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    args = args_after_separator()
    if len(args) != 3:
        raise SystemExit("usage: blender --background --python build_variant.py -- <source-fbx> <atlas-png> <blender-root>")
    source_fbx = Path(args[0]).resolve()
    atlas_png = Path(args[1]).resolve()
    blender_root = Path(args[2]).resolve()
    if file_sha256(source_fbx) != EXPECTED_SOURCE_SHA256:
        raise RuntimeError("source FBX hash mismatch")

    source_output = blender_root / "source" / PROJECT_ID
    export_output = blender_root / "exports" / PROJECT_ID
    validation_output = blender_root / "validation" / PROJECT_ID
    for path in (source_output, export_output, validation_output):
        path.mkdir(parents=True, exist_ok=True)

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source_fbx), use_anim=False)
    imported = list(bpy.context.selected_objects)
    imported_meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = object_bounds(imported_meshes)
    shift = Vector((-(low.x + high.x) * 0.5, -(low.y + high.y) * 0.5, -low.z))
    for obj in imported:
        obj.location += shift

    reference_collection = bpy.data.collections.new("REFERENCE_SyntyFarmhouse02_ReadOnly")
    bpy.context.scene.collection.children.link(reference_collection)
    for obj in imported:
        move_to_collection(obj, reference_collection)
        obj.name = f"REFERENCE_{obj.name}"
        obj["sourceOwnership"] = "SupplierOriginalReadOnly"

    variant_collection = bpy.data.collections.new("VARIANT_FarmResidentialHome_Damaged")
    bpy.context.scene.collection.children.link(variant_collection)
    variants = duplicate_objects(imported, variant_collection)
    for obj in variants:
        obj.name = obj.name.replace("REFERENCE_", "VARIANT_").removesuffix(".001")
        obj["projectOwnedVariant"] = True
        obj["hStableId"] = H_ID

    reference_atlas = make_atlas_material(atlas_png, weathered=False)
    atlas = make_atlas_material(atlas_png, weathered=True)
    glass = make_plain_material("ProjectOwned_WindowGlass", (0.20, 0.42, 0.48), 0.18)
    dark = make_plain_material("ProjectOwned_RoofCavity", (0.025, 0.018, 0.014), 0.96)
    wood = make_plain_material("ProjectOwned_ExposedWood", (0.30, 0.16, 0.08), 0.94)
    assign_review_materials(imported_meshes, reference_atlas, glass)
    variant_meshes = [obj for obj in variants if obj.type == "MESH"]
    assign_review_materials(variant_meshes, atlas, glass)
    main_variant = next(obj for obj in variant_meshes if obj.name.startswith("VARIANT_SM_Bld_Farmhouse_02") and "Door" not in obj.name and "Glass" not in obj.name)

    low, high = object_bounds(variant_meshes)
    before_faces, after_faces = boolean_roof_opening(main_variant, low, high)
    details = add_damage_details(variant_collection, low, high, dark, wood)
    variant_meshes.extend(details)
    low_after, high_after = object_bounds(variant_meshes)
    center = (low_after + high_after) * 0.5
    camera, _ = add_render_scene(center, low_after, high_after)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.045, 0.055, 0.075)
    scene.view_settings.look = "AgX - Medium High Contrast"

    set_collection_visibility(reference_collection, True)
    set_collection_visibility(variant_collection, False)
    render(validation_output / "before.png")
    set_collection_visibility(reference_collection, False)
    set_collection_visibility(variant_collection, True)
    render(validation_output / "after.png")

    extent = max(*(high_after - low_after))
    camera.location = Vector((center.x + extent * 0.75, center.y - extent * 0.95, center.z + extent * 1.05))
    camera.data.lens = 65
    look_at(camera, Vector((low.x + (high.x - low.x) * 0.24, low.y + (high.y - low.y) * 0.44, low.z + (high.z - low.z) * 0.76)))
    render(validation_output / "damage-closeup.png")

    # Export only project-owned variant geometry. The supplier reference remains
    # in the .blend for comparison and lineage, but is hidden and never exported.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in variant_meshes:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = main_variant
    export_path = export_output / "farm-residential-home-damaged-variant.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(export_path),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
    )

    scene["projectId"] = PROJECT_ID
    scene["hStableId"] = H_ID
    scene["sourceAssetPath"] = "Assets/Synty/PolygonFarm/Models/SM_Bld_Farmhouse_02.fbx"
    scene["sourceGuid"] = EXPECTED_SOURCE_GUID
    scene["sourceSha256"] = EXPECTED_SOURCE_SHA256
    scene["damageCue"] = "MarkedRoofPatchesScatteredOpeningsAndBrownDustVisibleFromForestTrail"
    scene["supplierOriginalMutationAllowed"] = False
    blend_path = source_output / "farm-residential-home-damaged-variant.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    result = {
        "projectId": PROJECT_ID,
        "hStableId": H_ID,
        "status": "BlenderValidatedCopyPendingReopen",
        "source": {
            "path": str(source_fbx),
            "guid": EXPECTED_SOURCE_GUID,
            "sha256": EXPECTED_SOURCE_SHA256,
            "mutated": False,
        },
        "damageCue": "MarkedRoofPatchesScatteredOpeningsAndBrownDustVisibleFromForestTrail",
        "weathering": {
            "treatment": "ProceduralBrownDustWash",
            "scope": "ProjectOwnedVariantMaterialOnly",
            "roughness": 0.90,
        },
        "meshEdit": {
            "mainMeshFacesBefore": before_faces,
            "mainMeshFacesAfter": after_faces,
            "addedDamageObjects": [obj.name for obj in details],
        },
        "outputs": {
            "blend": str(blend_path),
            "fbx": str(export_path),
            "before": str(validation_output / "before.png"),
            "after": str(validation_output / "after.png"),
            "closeup": str(validation_output / "damage-closeup.png"),
        },
        "evidenceLimit": "Blender derived-asset and render evidence only; no Unity import, Scene placement, Game View, or E5 claim.",
    }
    (validation_output / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
