"""Build and render the first reusable state set for the 2.5m cultivation H1.

The output is an E4 visual candidate only.  It does not author a Unity prefab,
change a Scene, or decide authoritative crop state.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


STATE_REVISION = "cultivation-plot-state-sample.r2"
SURFACE_Z = 0.145


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
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def object_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not points:
        raise RuntimeError("H1_CULTIVATION_STATE_NO_MESH")
    return (
        Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
        Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))),
    )


def solid_material(name: str, color: tuple[float, float, float], roughness: float = 0.88):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def atlas_material(texture_path: Path):
    value = bpy.data.materials.new("Synty_PolygonFarm_Atlas")
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    image = value.node_tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.interpolation = "Closest"
    value.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    value.node_tree.links.new(image.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.82
    if hasattr(value, "surface_render_method"):
        value.surface_render_method = "DITHERED"
    return value


def move_to_collection(objects: list[bpy.types.Object], collection: bpy.types.Collection) -> None:
    for obj in objects:
        for source in list(obj.users_collection):
            source.objects.unlink(obj)
        collection.objects.link(obj)


def import_asset(
    path: Path,
    label: str,
    collection: bpy.types.Collection,
    location: tuple[float, float, float],
    assigned_material,
    target_height: float | None = None,
    rotation_z: float = 0.0,
):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    low, high = object_bounds(meshes)
    center = (low + high) * 0.5
    root = bpy.data.objects.new(label, None)
    collection.objects.link(root)
    for obj in imported:
        world = obj.matrix_world.copy()
        world.translation += Vector((-center.x, -center.y, -low.z))
        obj.parent = root
        obj.matrix_world = world
    move_to_collection(imported, collection)
    for obj in meshes:
        original_names = [slot.name if slot else "" for slot in obj.data.materials]
        obj.data.materials.clear()
        if isinstance(assigned_material, tuple):
            farm_material, leaf_material = assigned_material
            for original_name in original_names:
                obj.data.materials.append(leaf_material if "leaves" in original_name.lower() else farm_material)
        else:
            obj.data.materials.append(assigned_material)
    height = max(high.z - low.z, 0.001)
    scale = target_height / height if target_height is not None else 1.0
    root.location = location
    root.rotation_euler.z = rotation_z
    root.scale = (scale, scale, scale)
    return root


def add_metadata_root(collection: bpy.types.Collection, state_code: str, state_name: str):
    root = bpy.data.objects.new(f"상태_{state_code}_{state_name}", None)
    collection.objects.link(root)
    root["h1StableId"] = "h1-stock:farm-production"
    root["variantCode"] = "cultivation-plot-2.5x2.5m"
    root["stateCode"] = state_code
    root["stateNameKo"] = state_name
    root["candidateRevision"] = STATE_REVISION
    root["authorityBoundary"] = "E4VisualCandidateOnly"
    return root


def build_state(
    scene: bpy.types.Scene,
    state_code: str,
    state_name: str,
    plot_path: Path,
    small_plant: Path,
    large_plant: Path,
    harvested_group: Path,
    soil_material,
    crop_material,
    leaf_material,
):
    collection = bpy.data.collections.new(f"{state_code}_{state_name}")
    scene.collection.children.link(collection)
    metadata = add_metadata_root(collection, state_code, state_name)
    plot = import_asset(plot_path, f"{state_name}_경작구획", collection, (0.0, 0.0, 0.0), soil_material)
    plot.parent = metadata

    ridge_x = (-0.78, 0.0, 0.78)
    row_y = (-0.72, 0.0, 0.72)
    planted = []
    harvested = []

    if state_code == "02":
        for ix, x in enumerate(ridge_x):
            for iy, y in enumerate(row_y):
                planted.append(
                    import_asset(
                        small_plant,
                        f"초기발아_{ix + 1}_{iy + 1}",
                        collection,
                        (x, y, SURFACE_Z),
                        (crop_material, leaf_material),
                        target_height=0.16,
                        rotation_z=math.radians((ix * 41 + iy * 67) % 360),
                    )
                )
    elif state_code == "03":
        for ix, x in enumerate(ridge_x):
            for iy, y in enumerate(row_y):
                planted.append(
                    import_asset(
                        large_plant,
                        f"성숙작물_{ix + 1}_{iy + 1}",
                        collection,
                        (x, y, SURFACE_Z),
                        (crop_material, leaf_material),
                        target_height=0.48,
                        rotation_z=math.radians((ix * 43 + iy * 61) % 360),
                    )
                )
    elif state_code == "04":
        # The back half remains mature; the front half is visibly harvested.
        for ix, x in enumerate(ridge_x):
            for iy, y in enumerate((0.15, 0.72)):
                planted.append(
                    import_asset(
                        large_plant,
                        f"부분수확_남은작물_{ix + 1}_{iy + 1}",
                        collection,
                        (x, y, SURFACE_Z),
                        (crop_material, leaf_material),
                        target_height=0.48,
                        rotation_z=math.radians((ix * 37 + iy * 59) % 360),
                    )
                )
        for index, x in enumerate((-0.58, 0.0, 0.58), start=1):
            harvested.append(
                import_asset(
                    harvested_group,
                    f"부분수확_걷어낸감자_{index}",
                    collection,
                    (x, -0.73, SURFACE_Z + 0.005),
                    crop_material,
                    target_height=0.09,
                    rotation_z=math.radians(index * 47),
                )
            )

    for obj in planted + harvested:
        obj.parent = metadata

    return {
        "code": state_code,
        "nameKo": state_name,
        "plantedRootCount": len(planted),
        "harvestedPayloadRootCount": len(harvested),
        "collection": collection,
    }


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_review_scene(scene: bpy.types.Scene, output: Path):
    ground_material = solid_material("검토바닥", (0.24, 0.34, 0.20), 0.95)
    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0.0, 0.0, -0.025))
    ground = bpy.context.object
    ground.name = "검토용바닥_권위없음"
    ground.data.materials.append(ground_material)

    bpy.ops.object.light_add(type="AREA", location=(3.2, -3.4, 5.7))
    key = bpy.context.object
    key.name = "검토주광"
    key.data.energy = 1150
    key.data.shape = "DISK"
    key.data.size = 4.5
    look_at(key, Vector((0.0, 0.0, 0.0)))
    bpy.ops.object.light_add(type="AREA", location=(-3.0, 2.4, 3.1))
    fill = bpy.context.object
    fill.name = "검토보조광"
    fill.data.energy = 650
    fill.data.size = 3.0
    look_at(fill, Vector((0.0, 0.0, 0.2)))

    bpy.ops.object.camera_add(location=(3.6, -4.0, 3.45))
    camera = bpy.context.object
    camera.name = "경작구획_상태검토카메라"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 3.85
    look_at(camera, Vector((0.0, 0.0, 0.18)))
    scene.camera = camera

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.045, 0.055, 0.045)
    scene["candidateRevision"] = STATE_REVISION
    scene["evidenceBoundary"] = "PresentationE4Candidate;NotUnityE5"


def main() -> None:
    args = arguments()
    if len(args) != 1:
        raise SystemExit("usage: <output-dir>")

    output = Path(args[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    unity_root = Path("C:/Users/user/ssalddel")
    models = unity_root / "Assets/Synty/PolygonFarm/Models"
    plot_path = unity_root / "ArtSource/Blender/H1-시각자료/농장/경작구획-2.5x2.5m/경작구획-H1-2.5x2.5m.fbx"
    small_plant = models / "SM_Prop_Plant_Potato_01_S.fbx"
    large_plant = models / "SM_Prop_Plant_Potato_01_L.fbx"
    harvested_group = models / "SM_Prop_Potato_01_Group.fbx"
    texture = unity_root / "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png"
    leaf_texture = unity_root / "Assets/Synty/PolygonFarm/Textures/Leaves_01_Diff.tga"
    sources = [plot_path, small_plant, large_plant, harvested_group, texture, leaf_texture]
    missing = [str(path) for path in sources if not path.exists()]
    if missing:
        raise RuntimeError("H1_CULTIVATION_STATE_SOURCE_MISSING:" + "|".join(missing))

    clear_scene()
    scene = bpy.context.scene
    setup_review_scene(scene, output)
    soil_material = solid_material("경작토양", (0.39, 0.19, 0.075), 0.94)
    crop_material = atlas_material(texture)
    leaf_material = atlas_material(leaf_texture)
    leaf_material.name = "Synty_Leaves_Atlas"

    state_specs = (
        ("01", "빈고랑"),
        ("02", "파종초기발아"),
        ("03", "성숙"),
        ("04", "부분수확"),
    )
    states = [
        build_state(
            scene,
            code,
            name,
            plot_path,
            small_plant,
            large_plant,
            harvested_group,
            soil_material,
            crop_material,
            leaf_material,
        )
        for code, name in state_specs
    ]

    render_files = []
    for active in states:
        for state in states:
            hidden = state is not active
            state["collection"].hide_render = hidden
            state["collection"].hide_viewport = hidden
        render_path = output / f"{active['code']}-{active['nameKo']}.png"
        scene.render.filepath = str(render_path)
        bpy.ops.render.render(write_still=True)
        render_files.append(render_path)

    # Save with the empty-groove state visible as a safe editing baseline.
    for state in states:
        state["collection"].hide_render = state["code"] != "01"
        state["collection"].hide_viewport = state["code"] != "01"
    blend_path = output / "경작구획-H1-상태표본-r2.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    result = {
        "candidateRevision": STATE_REVISION,
        "h1StableId": "h1-stock:farm-production",
        "variantCode": "cultivation-plot-2.5x2.5m",
        "dimensionsMeters": [2.5, 2.5, 0.18834],
        "visualCropExample": "potato",
        "states": [
            {
                "code": state["code"],
                "nameKo": state["nameKo"],
                "plantedRootCount": state["plantedRootCount"],
                "harvestedPayloadRootCount": state["harvestedPayloadRootCount"],
                "image": render_files[index].name,
                "imageSha256": sha256(render_files[index]),
            }
            for index, state in enumerate(states)
        ],
        "placementRule": {
            "cropAlignment": "RidgeCenterLines",
            "ridgeCenterX": [-0.78, 0.0, 0.78],
            "rowY": [-0.72, 0.0, 0.72],
            "plotPivot": "Center",
        },
        "sources": [{"path": str(path), "sha256": sha256(path)} for path in sources],
        "blend": blend_path.name,
        "blendSha256": sha256(blend_path),
        "evidenceBoundary": {
            "presentation": "E4VisualCandidate",
            "unityPrefab": False,
            "unityScene": False,
            "playMode": False,
            "gameView": False,
            "e5": False,
        },
    }
    (output / "검증결과-r2.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
