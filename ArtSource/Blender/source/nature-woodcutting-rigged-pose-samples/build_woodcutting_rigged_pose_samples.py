from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def cli_args() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def make_material(name: str, color: tuple[float, float, float], roughness: float = 0.78):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_segment(name: str, start: Vector, end: Vector, radius: float, mat):
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=direction.length, location=(start + end) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(direction.normalized())
    obj.data.materials.append(mat)
    return obj


def add_joint(name: str, point: Vector, radius: float, mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=point)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def add_bone(edit_bones, name: str, head: Vector, tail: Vector, parent=None):
    bone = edit_bones.new(name)
    bone.head = head
    bone.tail = tail if (tail - head).length > 0.02 else head + Vector((0.0, 0.0, 0.05))
    bone.parent = parent
    return bone


def parent_to_bone(obj, armature, bone_name: str):
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world


def pose_points(kind: str) -> dict[str, Vector]:
    if kind == "ready":
        return {k: Vector(v) for k, v in {
            "root": (0.75, 0.16, 0.05), "hips": (0.75, 0.12, 0.93), "chest": (0.72, 0.02, 1.42), "neck": (0.70, -0.01, 1.63), "head": (0.69, -0.02, 1.84),
            "sl": (0.48, -0.02, 1.48), "el": (0.31, -0.24, 1.22), "hl": (0.19, -0.40, 1.04),
            "sr": (0.96, 0.02, 1.47), "er": (0.80, -0.24, 1.28), "hr": (0.55, -0.39, 1.17),
            "kl": (0.55, 0.03, 0.49), "al": (0.42, -0.12, 0.10), "fl": (0.31, -0.30, 0.05),
            "kr": (0.95, 0.19, 0.49), "ar": (1.02, 0.04, 0.10), "fr": (0.91, -0.15, 0.05),
            "grip_a": (0.20, -0.42, 1.03), "grip_b": (0.60, -0.40, 1.20), "axe_tip": (-0.02, -0.45, 0.68),
        }.items()}
    if kind == "backswing":
        return {k: Vector(v) for k, v in {
            "root": (0.87, 0.25, 0.05), "hips": (0.86, 0.25, 0.91), "chest": (0.96, 0.18, 1.40), "neck": (0.93, 0.12, 1.61), "head": (0.88, 0.07, 1.82),
            "sl": (0.71, 0.08, 1.48), "el": (0.54, 0.15, 1.74), "hl": (0.42, 0.02, 1.91),
            "sr": (1.20, 0.24, 1.43), "er": (1.11, 0.03, 1.73), "hr": (0.82, -0.02, 1.89),
            "kl": (0.55, 0.00, 0.47), "al": (0.36, -0.12, 0.10), "fl": (0.24, -0.33, 0.05),
            "kr": (1.10, 0.33, 0.47), "ar": (1.22, 0.21, 0.10), "fr": (1.16, -0.02, 0.05),
            "grip_a": (0.42, 0.02, 1.91), "grip_b": (0.84, -0.02, 1.88), "axe_tip": (1.33, 0.05, 2.25),
        }.items()}
    return {k: Vector(v) for k, v in {
        "root": (0.71, 0.06, 0.05), "hips": (0.68, 0.02, 0.86), "chest": (0.48, -0.16, 1.29), "neck": (0.43, -0.20, 1.49), "head": (0.40, -0.22, 1.69),
        "sl": (0.24, -0.20, 1.33), "el": (0.02, -0.43, 1.18), "hl": (-0.22, -0.61, 1.03),
        "sr": (0.72, -0.11, 1.32), "er": (0.43, -0.40, 1.18), "hr": (0.10, -0.60, 1.08),
        "kl": (0.43, -0.08, 0.43), "al": (0.25, -0.20, 0.10), "fl": (0.10, -0.40, 0.05),
        "kr": (0.94, 0.14, 0.44), "ar": (1.06, -0.01, 0.10), "fr": (0.94, -0.24, 0.05),
        "grip_a": (-0.22, -0.61, 1.03), "grip_b": (0.12, -0.61, 1.08), "axe_tip": (-0.88, -0.74, 0.90),
    }.items()}


def build_rig(points: dict[str, Vector]):
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    armature = bpy.context.object
    armature.name = "벌목검토리그"
    edit = armature.data.edit_bones
    edit.remove(edit[0])
    root = add_bone(edit, "Root", points["root"], points["hips"])
    hips = add_bone(edit, "Hips", points["hips"], points["chest"], root)
    spine = add_bone(edit, "Spine", points["hips"], points["chest"], hips)
    neck = add_bone(edit, "Neck", points["chest"], points["neck"], spine)
    add_bone(edit, "Head", points["neck"], points["head"], neck)
    for side in ("l", "r"):
        shoulder = add_bone(edit, f"UpperArm_{side.upper()}", points[f"c{side}"] if f"c{side}" in points else points[f"s{side}"], points[f"e{side}"], spine)
        add_bone(edit, f"Forearm_{side.upper()}", points[f"e{side}"], points[f"h{side}"], shoulder)
        thigh = add_bone(edit, f"Thigh_{side.upper()}", points["hips"] + Vector((-0.10 if side == "l" else 0.10, 0, 0)), points[f"k{side}"], hips)
        shin = add_bone(edit, f"Shin_{side.upper()}", points[f"k{side}"], points[f"a{side}"], thigh)
        add_bone(edit, f"Foot_{side.upper()}", points[f"a{side}"], points[f"f{side}"], shin)
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature.data.display_type = "OCTAHEDRAL"
    armature["biomechanics"] = "wide_stance;knee_flexion;hip_hinge;hip_rotation;torso_follow_through;two_hand_grip"
    return armature


def build_actor(points: dict[str, Vector], armature):
    body = make_material("작업복", (0.11, 0.31, 0.43))
    joints = make_material("관절표시", (0.04, 0.11, 0.16))
    skin = make_material("피부", (0.63, 0.40, 0.25))
    boot = make_material("작업화", (0.11, 0.07, 0.04))
    segments = [
        ("몸통", points["hips"], points["chest"], .20, body, "Spine"),
        ("왼팔상부", points["sl"], points["el"], .085, body, "UpperArm_L"), ("왼팔하부", points["el"], points["hl"], .072, body, "Forearm_L"),
        ("오른팔상부", points["sr"], points["er"], .085, body, "UpperArm_R"), ("오른팔하부", points["er"], points["hr"], .072, body, "Forearm_R"),
        ("왼다리상부", points["hips"] + Vector((-.10,0,0)), points["kl"], .105, body, "Thigh_L"), ("왼다리하부", points["kl"], points["al"], .088, body, "Shin_L"),
        ("오른다리상부", points["hips"] + Vector((.10,0,0)), points["kr"], .105, body, "Thigh_R"), ("오른다리하부", points["kr"], points["ar"], .088, body, "Shin_R"),
        ("왼발", points["al"], points["fl"], .09, boot, "Foot_L"), ("오른발", points["ar"], points["fr"], .09, boot, "Foot_R"),
    ]
    for name, start, end, radius, mat, bone in segments:
        parent_to_bone(add_segment(name, start, end, radius, mat), armature, bone)
    for name, point in (("왼손", points["hl"]), ("오른손", points["hr"]), ("왼무릎", points["kl"]), ("오른무릎", points["kr"])):
        parent_to_bone(add_joint(name, point, .082, joints), armature, "Root")
    parent_to_bone(add_joint("머리", points["head"], .19, skin), armature, "Head")


def build_axe(points: dict[str, Vector]):
    wood = make_material("도끼자루", (0.28, 0.12, 0.045))
    metal = make_material("도끼날", (0.22, 0.25, 0.27), .34)
    grip_mid = (points["grip_a"] + points["grip_b"]) * .5
    shaft_end = points["axe_tip"]
    shaft = add_segment("벌목도끼_자루", grip_mid, shaft_end, .035, wood)
    direction = (shaft_end - grip_mid).normalized()
    bpy.ops.mesh.primitive_cube_add(size=1, location=shaft_end)
    head = bpy.context.object
    head.name = "벌목도끼_날"
    head.scale = (.24, .075, .13)
    head.rotation_mode = "QUATERNION"
    head.rotation_quaternion = Vector((1, 0, 0)).rotation_difference(Vector((direction.z, 0, -direction.x)).normalized())
    head.data.materials.append(metal)
    return shaft, head


def build_tree():
    bark = make_material("나무껍질", (0.23, 0.10, 0.035))
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=.32, depth=3.15, location=(-.92, -.76, 1.575))
    tree = bpy.context.object
    tree.name = "벌목대상나무"
    tree.data.materials.append(bark)
    return tree


def render_one(output: Path, kind: str, filename: str):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    points = pose_points(kind)
    rig = build_rig(points)
    build_actor(points, rig)
    build_axe(points)
    build_tree()
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, -.3, -.015))
    bpy.context.object.name = "검토바닥"
    bpy.context.object.data.materials.append(make_material("검토바닥재", (.08, .14, .065)))
    bpy.ops.object.light_add(type="AREA", location=(3.4, -4.7, 6.0)); bpy.context.object.data.energy = 1100; bpy.context.object.data.size = 4
    bpy.ops.object.light_add(type="AREA", location=(-3.2, 1.2, 3.8)); bpy.context.object.data.energy = 520; bpy.context.object.data.size = 3
    bpy.ops.object.camera_add(location=(4.6, -6.5, 3.0))
    camera = bpy.context.object; camera.data.type = "ORTHO"; camera.data.ortho_scale = 4.35; look_at(camera, (0, -.42, 1.18))
    scene = bpy.context.scene; scene.camera = camera; scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960; scene.render.resolution_y = 720; scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"; scene.render.filepath = str(output / filename)
    if scene.world is None: scene.world = bpy.data.worlds.new("벌목리그World")
    scene.world.color = (.022, .028, .038); scene.view_settings.look = "AgX - Medium High Contrast"
    blend = filename.replace(".png", ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(output / blend)); bpy.ops.render.render(write_still=True)
    return {"state": kind, "image": filename, "blend": blend, "rig": rig.name, "bones": len(rig.data.bones)}


def main():
    values = cli_args()
    if len(values) != 1:
        raise SystemExit("usage: <output-dir>")
    output = Path(values[0]).resolve(); output.mkdir(parents=True, exist_ok=True)
    states = [
        render_one(output, "ready", "01-안정된준비자세.png"),
        render_one(output, "backswing", "02-하체로받치는백스윙.png"),
        render_one(output, "contact", "03-골반회전타격접촉.png"),
    ]
    result = {
        "status": "BlenderE4RiggedPoseStudyRendered",
        "revision": "nature-woodcutting-rigged-pose-samples.r2",
        "states": states,
        "biomechanics": ["wide_stance", "knee_flexion", "hip_hinge", "hip_rotation", "torso_follow_through", "two_hand_grip"],
        "supersedes": "nature-woodcutting-pose-samples.r1 as visual acceptance candidate; r1 remains initial blocking history",
        "boundary": "Presentation E4 rigged pose study only; not a final skinned Synty actor, authored animation clip, Unity playback, or E5 evidence."
    }
    (output / "검증결과-r2.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("WOODCUTTING_RIGGED_POSE_R2_RENDERED")


if __name__ == "__main__":
    main()
