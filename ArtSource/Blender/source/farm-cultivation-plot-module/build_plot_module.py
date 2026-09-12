"""Build a small project-owned cultivation plot from a read-only Synty field mesh."""

from __future__ import annotations

import bmesh
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ID = "farm-cultivation-plot-module"
H1_ID = "h1-stock:farm-production"
TARGET_SIZE_METERS = 2.5


def arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def mesh_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def crop_center(obj: bpy.types.Object, size: float) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    low, high = mesh_bounds(obj)
    center = (low + high) * 0.5
    obj.location -= Vector((center.x, center.y, low.z))
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    half = size * 0.5
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    geometry = list(bm.verts) + list(bm.edges) + list(bm.faces)
    for plane_co, plane_no in (
        ((-half, 0.0, 0.0), (1.0, 0.0, 0.0)),
        ((half, 0.0, 0.0), (1.0, 0.0, 0.0)),
        ((0.0, -half, 0.0), (0.0, 1.0, 0.0)),
        ((0.0, half, 0.0), (0.0, 1.0, 0.0)),
    ):
        bmesh.ops.bisect_plane(
            bm,
            geom=geometry,
            plane_co=plane_co,
            plane_no=plane_no,
            dist=0.00001,
        )
        geometry = list(bm.verts) + list(bm.edges) + list(bm.faces)

    outside = [
        vertex
        for vertex in bm.verts
        if abs(vertex.co.x) > half + 0.0001 or abs(vertex.co.y) > half + 0.0001
    ]
    bmesh.ops.delete(bm, geom=outside, context="VERTS")
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def make_material(name: str, color: tuple[float, float, float]) -> bpy.types.Material:
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.94
    return value


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    args = arguments()
    if len(args) != 2:
        raise SystemExit("usage: <source-fbx> <output-dir>")
    source = Path(args[0]).resolve()
    output = Path(args[1]).resolve()
    output.mkdir(parents=True, exist_ok=True)

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"EXPECTED_ONE_MESH:{len(meshes)}")

    plot = meshes[0]
    plot.name = "Farm_CultivationPlot_2_5m"
    plot.data.name = "Farm_CultivationPlot_2_5m_Mesh"
    crop_center(plot, TARGET_SIZE_METERS)
    plot.data.materials.clear()
    plot.data.materials.append(make_material("ProjectOwned_ReviewSoil", (0.24, 0.115, 0.04)))
    bpy.context.view_layer.update()

    low, high = mesh_bounds(plot)
    dimensions = high - low
    center = (low + high) * 0.5

    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, low.z - 0.015))
    ground = bpy.context.object
    ground.name = "ReviewGround"
    ground.data.materials.append(make_material("ReviewGroundMaterial", (0.08, 0.135, 0.06)))

    bpy.ops.object.light_add(type="AREA", location=(3.2, -3.0, 4.5))
    key = bpy.context.object
    key.data.energy = 550
    key.data.size = 3.5
    look_at(key, center)

    bpy.ops.object.light_add(type="AREA", location=(-2.5, 1.8, 2.8))
    fill = bpy.context.object
    fill.data.energy = 240
    fill.data.size = 3.0
    look_at(fill, center)

    bpy.ops.object.camera_add(location=(3.8, -4.1, 3.5))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.2
    look_at(camera, Vector((0.0, 0.0, 0.12)))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / "경작구획-H1-2.5x2.5m-검토.png")
    scene.world.color = (0.025, 0.032, 0.045)
    scene.view_settings.look = "AgX - Medium High Contrast"

    blend_path = output / "경작구획-H1-2.5x2.5m.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.render.render(write_still=True)

    bpy.ops.object.select_all(action="DESELECT")
    plot.select_set(True)
    bpy.context.view_layer.objects.active = plot
    export_path = output / "경작구획-H1-2.5x2.5m.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(export_path),
        use_selection=True,
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
    )

    result = {
        "schemaRevision": "blender-h1-review-variant.r1",
        "projectId": PROJECT_ID,
        "h1StableId": H1_ID,
        "status": "BlenderReviewVariant",
        "sourceOwnership": "SupplierOriginalReadOnly",
        "sourcePath": str(source),
        "sourceSha256": sha256(source),
        "operation": "CenterCropWithoutUniformScale",
        "targetSizeMeters": TARGET_SIZE_METERS,
        "resultDimensions": [round(value, 5) for value in dimensions],
        "blendPath": str(blend_path),
        "fbxPath": str(export_path),
        "renderPath": scene.render.filepath,
        "boundary": "Review variant only; not approved asset, Unity import, World placement, or E5 evidence.",
    }
    (output / "검증결과.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("FARM_CULTIVATION_PLOT_MODULE_RENDERED")


if __name__ == "__main__":
    main()
