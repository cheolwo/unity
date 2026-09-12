from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def material(name: str, color: tuple[float, float, float], roughness: float = 0.76):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def segment(name: str, start: Vector, end: Vector, radius: float, mat):
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=direction.length, location=(start + end) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    obj.data.materials.append(mat)
    return obj


def prepare_pose(character: Path, animation: Path, frame: int):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(character), use_anim=False)
    target = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    target.name = "벌목작업자_실제리그"
    character_material = material("벌목작업복", (0.16, 0.33, 0.44))
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH" and obj.parent == target:
            obj.name = "벌목작업자_외형"
            obj.data.materials.clear()
            obj.data.materials.append(character_material)

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(animation), use_anim=True)
    source = next(obj for obj in bpy.context.scene.objects if obj not in before and obj.type == "ARMATURE")
    source.name = "공급사동작_참조리그"
    source.hide_render = True
    for obj in bpy.context.scene.objects:
        if obj in before or obj == source:
            continue
        obj.hide_render = True

    bpy.context.scene.frame_set(frame)
    for bone in target.pose.bones:
        if bone.name in source.pose.bones:
            bone.matrix_basis = source.pose.bones[bone.name].matrix_basis.copy()
    bpy.context.view_layer.update()
    target["pose_source"] = f"{animation.name}:frame-{frame}"
    target["woodcutting_adjustment"] = "same-pack rig pose transfer; axe and tree contact composed for E4 review"
    return target


def hand_point(armature, name: str) -> Vector:
    return armature.matrix_world @ armature.pose.bones[name].head


def add_axe_and_tree(armature, state: str):
    left = hand_point(armature, "Hand_L")
    right = hand_point(armature, "Hand_R")
    grip_axis = (left - right).normalized()
    if state == "backswing":
        handle_start = left + grip_axis * 0.12
        handle_end = right - grip_axis * 0.72
    else:
        handle_start = right - grip_axis * 0.16
        handle_end = left + grip_axis * 0.72
    wood = material("도끼자루", (0.31, 0.14, 0.05))
    metal = material("도끼날", (0.28, 0.31, 0.33), 0.34)
    segment("벌목도끼_자루", handle_start, handle_end, 0.031, wood)
    perpendicular = Vector((-grip_axis.z, 0.0, grip_axis.x))
    if perpendicular.length < 0.1:
        perpendicular = Vector((0.0, 0.0, 1.0))
    perpendicular.normalize()
    segment("벌목도끼_날", handle_end - perpendicular * 0.16, handle_end + perpendicular * 0.18, 0.075, metal)

    trunk_center = Vector((-1.18, -0.02, 1.575))
    bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=0.30, depth=3.15, location=(trunk_center.x, trunk_center.y, 1.575))
    tree = bpy.context.object
    tree.name = "벌목대상나무"
    tree.data.materials.append(material("나무껍질", (0.25, 0.105, 0.035)))
    return handle_end, tree.location


def finish_scene(output: Path, filename: str):
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, -0.015))
    bpy.context.object.name = "검토바닥"
    bpy.context.object.data.materials.append(material("검토바닥재", (0.08, 0.15, 0.07)))
    bpy.ops.object.light_add(type="AREA", location=(3.4, -4.6, 5.8))
    bpy.context.object.data.energy = 1100
    bpy.context.object.data.size = 4.0
    bpy.ops.object.light_add(type="AREA", location=(-3.0, 1.4, 3.8))
    bpy.context.object.data.energy = 520
    bpy.context.object.data.size = 3.0
    bpy.ops.object.camera_add(location=(4.1, -6.5, 2.8))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 3.7
    look_at(camera, (-0.25, -0.15, 1.15))
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / filename)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("벌목실제리그World")
    scene.world.color = (0.022, 0.028, 0.038)
    scene.view_settings.look = "AgX - Medium High Contrast"
    blend_name = filename.replace(".png", ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(output / blend_name))
    bpy.ops.render.render(write_still=True)
    return blend_name


def render_state(character: Path, animation: Path, output: Path, frame: int, filename: str, state: str):
    rig = prepare_pose(character, animation, frame)
    contact, tree = add_axe_and_tree(rig, state)
    blend = finish_scene(output, filename)
    return {
        "state": state,
        "frame": frame,
        "image": filename,
        "blend": blend,
        "rig": rig.name,
        "bones": len(rig.data.bones),
        "contactPoint": [round(v, 4) for v in contact],
        "treeLocation": [round(v, 4) for v in tree],
    }


def main():
    values = args()
    if len(values) != 3:
        raise SystemExit("usage: <same-pack-character-fbx> <animation-fbx> <output-dir>")
    character, animation, output = [Path(v).resolve() for v in values]
    output.mkdir(parents=True, exist_ok=True)
    states = [
        render_state(character, animation, output, 20, "01-골반회전백스윙.png", "backswing"),
        render_state(character, animation, output, 44, "02-보폭과감속을쓴타격.png", "contact_follow_through"),
    ]
    result = {
        "status": "BlenderE4SyntyRigPoseStudyRendered",
        "revision": "nature-woodcutting-synty-rig-pose-samples.r3",
        "states": states,
        "inputs": {str(path): sha256(path) for path in (character, animation)},
        "improvements": ["same-pack skinned character", "actual armature", "hip rotation", "knee flexion", "wide stance", "torso follow-through", "two-hand axe composition"],
        "supersedes": ["nature-woodcutting-pose-samples.r1", "nature-woodcutting-rigged-pose-samples.r2 as acceptance candidates"],
        "boundary": "Presentation E4 rig-pose study only; the source is a sword-combat motion adapted for a woodcutting review. It is not an authored logging clip, Unity playback, or E5 evidence."
    }
    (output / "검증결과-r3.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("WOODCUTTING_SYNTY_RIG_POSE_R3_RENDERED")


if __name__ == "__main__":
    main()
