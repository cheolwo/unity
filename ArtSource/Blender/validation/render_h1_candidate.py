"""Render one supplier FBX as a local, read-only H1 review artifact."""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


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


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    if not points:
        raise RuntimeError("H1_REVIEW_NO_MESH")
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def material(name: str, color: tuple[float, float, float], roughness: float = 0.85) -> bpy.types.Material:
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def atlas_material(texture_path: Path) -> bpy.types.Material:
    value = bpy.data.materials.new("H1검토용_공급사Atlas")
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    image = value.node_tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.interpolation = "Closest"
    value.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    value.node_tree.links.new(image.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.82
    return value


def main() -> None:
    args = arguments()
    if len(args) not in (4, 5, 6):
        raise SystemExit("usage: <source-fbx> <output-dir> <h1-id> <display-name> [atlas-texture] [review-scale]")

    source = Path(args[0]).resolve()
    output = Path(args[1]).resolve()
    h1_id = args[2]
    display_name = args[3]
    atlas_texture = Path(args[4]).resolve() if len(args) == 5 else None
    if len(args) == 6:
        atlas_texture = Path(args[4]).resolve()
    review_scale = float(args[5]) if len(args) == 6 else 1.0
    output.mkdir(parents=True, exist_ok=True)

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    low, high = bounds(meshes)
    center = (low + high) * 0.5
    shift = Vector((-center.x, -center.y, -low.z))
    for obj in meshes:
        obj.location += shift

    if review_scale != 1.0:
        for obj in meshes:
            obj.location *= review_scale
            obj.scale *= review_scale
        bpy.context.view_layer.update()

    low, high = bounds(meshes)
    center = (low + high) * 0.5
    size = high - low
    horizontal = max(size.x, size.y, 0.1)
    vertical = max(size.z, horizontal * 0.08)
    # Frame tall props (sprinklers, posts, towers) without clipping while
    # retaining the same isometric review angle used for horizontal props.
    frame_extent = max(horizontal, vertical * 0.82)

    # Review-only fallback: keep the supplier mesh untouched while making an
    # untextured FBX readable in Blender. This is not an assignment decision.
    fallback = atlas_material(atlas_texture) if atlas_texture else material("H1검토용_토양", (0.22, 0.105, 0.035), 0.95)
    for obj in meshes:
        # Imported Synty FBX files can retain the supplier workstation's PSD
        # path. Replace materials only in this review Scene so a missing image
        # does not turn the candidate magenta. The FBX itself remains read-only.
        obj.data.materials.clear()
        obj.data.materials.append(fallback)

    bpy.ops.mesh.primitive_plane_add(size=frame_extent * 3.6, location=(center.x, center.y, -0.012))
    ground = bpy.context.object
    ground.name = "H1검토바닥"
    ground.data.materials.append(material("H1검토바닥재", (0.09, 0.14, 0.065)))

    bpy.ops.object.light_add(
        type="AREA",
        location=(center.x + frame_extent * 0.7, center.y - frame_extent * 0.7, vertical + frame_extent * 1.0),
    )
    key = bpy.context.object
    key.data.energy = max(0.5, 44.0 * frame_extent * frame_extent)
    key.data.shape = "DISK"
    key.data.size = frame_extent * 0.8
    look_at(key, center)

    bpy.ops.object.light_add(
        type="AREA",
        location=(center.x - frame_extent * 0.8, center.y + frame_extent * 0.4, vertical + frame_extent * 0.5),
    )
    fill = bpy.context.object
    fill.data.energy = max(0.25, 26.0 * frame_extent * frame_extent)
    fill.data.size = frame_extent
    look_at(fill, center)

    bpy.ops.object.camera_add(
        location=(center.x + frame_extent * 1.15, center.y - frame_extent * 1.25, vertical + frame_extent * 1.15)
    )
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = frame_extent * 1.65
    look_at(camera, Vector((center.x, center.y, max(center.z, vertical * 0.2))))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / "candidate.png")
    scene.world.color = (0.025, 0.032, 0.045)
    scene.view_settings.look = "AgX - Medium High Contrast"

    bpy.ops.wm.save_as_mainfile(filepath=str(output / "candidate-review.blend"))
    bpy.ops.render.render(write_still=True)

    result = {
        "status": "BlenderReviewRendered",
        "h1StableId": h1_id,
        "displayName": display_name,
        "sourceOwnership": "SupplierOriginalReadOnly",
        "sourcePath": str(source),
        "sourceSha256": sha256(source),
        "renderPath": str(output / "candidate.png"),
        "blendPath": str(output / "candidate-review.blend"),
        "reviewMaterialSource": str(atlas_texture) if atlas_texture else "NeutralFallback",
        "reviewScale": review_scale,
        "meshObjects": [obj.name for obj in meshes],
        "dimensions": [round(value, 5) for value in size],
        "boundary": "Review image only; not ExactCandidate, Unity placement, or E5 evidence.",
    }
    (output / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("H1_BLENDER_REVIEW_RENDERED")


if __name__ == "__main__":
    main()
