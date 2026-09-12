from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def args() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def material(name, color, roughness=0.8):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def atlas_material(path: Path):
    value = bpy.data.materials.new("캐릭터검토Atlas")
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    tex = value.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(path), check_existing=True)
    tex.interpolation = "Closest"
    value.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.78
    return value


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def limb(name, start, end, radius, mat):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=direction.length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(direction.normalized())
    obj.data.materials.append(mat)
    return obj


def joint(name, location, radius, mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def build_pose_mannequin(kind: str):
    body = material("자세블로킹_몸", (0.12, 0.34, 0.48))
    joint_mat = material("자세블로킹_관절", (0.07, 0.16, 0.22))
    skin = material("자세블로킹_머리", (0.64, 0.43, 0.27))

    if kind == "carry":
        hips, chest, head = Vector((0.8, 0.1, 0.92)), Vector((0.8, 0.0, 1.43)), Vector((0.8, -0.02, 1.78))
        shoulder_l, elbow_l, hand_l = Vector((0.58, -0.02, 1.48)), Vector((0.45, -0.20, 1.22)), Vector((0.36, -0.30, 1.04))
        shoulder_r, elbow_r, hand_r = Vector((1.02, -0.02, 1.48)), Vector((0.84, -0.22, 1.24)), Vector((0.60, -0.32, 1.18))
        knee_l, foot_l = Vector((0.66, 0.08, 0.48)), Vector((0.58, -0.04, 0.05))
        knee_r, foot_r = Vector((0.96, 0.12, 0.48)), Vector((1.02, -0.02, 0.05))
        axe_a, axe_b = Vector((0.30, -0.34, 0.72)), Vector((0.66, -0.32, 1.44))
    else:
        hips, chest, head = Vector((0.82, 0.12, 0.88)), Vector((0.58, -0.22, 1.36)), Vector((0.48, -0.32, 1.66))
        shoulder_l, elbow_l, hand_l = Vector((0.37, -0.25, 1.40)), Vector((0.08, -0.50, 1.28)), Vector((-0.16, -0.70, 1.14))
        shoulder_r, elbow_r, hand_r = Vector((0.78, -0.18, 1.40)), Vector((0.45, -0.45, 1.30)), Vector((0.08, -0.66, 1.18))
        knee_l, foot_l = Vector((0.58, 0.02, 0.43)), Vector((0.40, -0.22, 0.05))
        knee_r, foot_r = Vector((1.06, 0.22, 0.46)), Vector((1.22, 0.02, 0.05))
        axe_a, axe_b = Vector((0.15, -0.68, 1.18)), Vector((-0.80, -1.02, 0.98))

    limb("몸통", hips, chest, 0.20, body)
    limb("왼팔상부", shoulder_l, elbow_l, 0.085, body)
    limb("왼팔하부", elbow_l, hand_l, 0.070, body)
    limb("오른팔상부", shoulder_r, elbow_r, 0.085, body)
    limb("오른팔하부", elbow_r, hand_r, 0.070, body)
    limb("왼다리상부", hips + Vector((-0.10, 0.0, 0.0)), knee_l, 0.10, body)
    limb("왼다리하부", knee_l, foot_l, 0.085, body)
    limb("오른다리상부", hips + Vector((0.10, 0.0, 0.0)), knee_r, 0.10, body)
    limb("오른다리하부", knee_r, foot_r, 0.085, body)
    for index, point in enumerate((hips, chest, shoulder_l, elbow_l, hand_l, shoulder_r, elbow_r, hand_r, knee_l, foot_l, knee_r, foot_r)):
        joint(f"관절_{index:02d}", point, 0.095 if index < 2 else 0.075, joint_mat)
    joint("머리", head, 0.19, skin)
    return axe_a, axe_b


def import_character(character: Path, atlas: Path):
    bpy.ops.import_scene.fbx(filepath=str(character), use_anim=False)
    armature = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    character_material = atlas_material(atlas)
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH" and obj.parent == armature:
            obj.data.materials.clear()
            obj.data.materials.append(character_material)
    return armature


def import_action(animation: Path):
    before_objects = set(bpy.context.scene.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=str(animation), use_anim=True)
    new_actions = [a for a in bpy.data.actions if a not in before_actions]
    if not new_actions:
        raise RuntimeError("NO_IMPORTED_ACTION")
    action = new_actions[0]
    source_armature = next(
        o for o in bpy.context.scene.objects
        if o not in before_objects and o.type == "ARMATURE"
    )
    return action, source_armature


def import_static(source: Path, name: str, mat, target_height: float | None = None):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    meshes = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    if target_height:
        obj.scale *= target_height / max(obj.dimensions)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def bind_character_to_animation_armature(base_armature, source_armature):
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj.parent != base_armature:
            continue
        world = obj.matrix_world.copy()
        obj.parent = source_armature
        for modifier in obj.modifiers:
            if modifier.type == "ARMATURE":
                modifier.object = source_armature
        obj.matrix_world = world
    base_armature.hide_render = True
    bpy.context.view_layer.update()


def set_axe_on_prop_bone(axe, armature):
    prop = armature.pose.bones["Prop_R"]
    axe.matrix_world = armature.matrix_world @ prop.matrix @ Matrix.Translation((0.0, 0.0, -0.42))
    bpy.context.view_layer.update()


def setup_scene(character, atlas, animation, axe_source, tree_source):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    armature = import_character(character, atlas)
    action, source_armature = import_action(animation)
    bind_character_to_animation_armature(armature, source_armature)
    axe = import_static(axe_source, "벌목도끼", material("도끼재질", (0.28, 0.16, 0.07)))
    tree = import_static(tree_source, "벌목대상나무", material("나무재질", (0.23, 0.11, 0.035)), 3.1)
    tree.location = (-0.65, -1.00, 1.55)
    source_armature.location.x = 0.85
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, -0.4, -0.015))
    bpy.context.object.name = "검토바닥"
    bpy.context.object.data.materials.append(material("검토바닥재", (0.10, 0.17, 0.07)))
    return source_armature, action, axe, tree


def render_pose(output: Path, filename: str, kind: str, axe_source, tree_source):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    axe_a, axe_b = build_pose_mannequin(kind)
    axe = import_static(axe_source, "벌목도끼", material("도끼재질", (0.22, 0.12, 0.045)), 1.02)
    axe.location = (axe_a + axe_b) * 0.5
    axe.rotation_mode = "QUATERNION"
    axe.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference((axe_b - axe_a).normalized())
    tree = import_static(tree_source, "벌목대상나무", material("나무재질", (0.23, 0.11, 0.035)), 3.1)
    tree.location = (-0.65, -1.00, 1.55)
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, -0.4, -0.015))
    bpy.context.object.name = "검토바닥"
    bpy.context.object.data.materials.append(material("검토바닥재", (0.10, 0.17, 0.07)))
    scene = bpy.context.scene

    bpy.ops.object.light_add(type="AREA", location=(3.5, -4.5, 6.0))
    bpy.context.object.data.energy = 1050
    bpy.context.object.data.size = 4.0
    bpy.ops.object.light_add(type="AREA", location=(-3.0, 1.5, 3.5))
    bpy.context.object.data.energy = 500
    bpy.context.object.data.size = 3.0
    bpy.ops.object.camera_add(location=(4.5, -6.3, 3.1))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.6
    look_at(camera, (0.0, -0.55, 1.25))
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / filename)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("벌목자세World")
    scene.world.color = (0.025, 0.032, 0.045)
    scene.view_settings.look = "AgX - Medium High Contrast"
    blend_name = filename.replace(".png", ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(output / blend_name))
    bpy.ops.render.render(write_still=True)
    return {"pose": kind, "image": filename, "blend": blend_name}


def main():
    values = args()
    if len(values) != 6:
        raise SystemExit("usage: <character-fbx> <character-atlas> <animation-fbx> <axe-fbx> <tree-fbx> <output-dir>")
    character, atlas, animation, axe_source, tree_source = [Path(v).resolve() for v in values[:5]]
    output = Path(values[5]).resolve()
    output.mkdir(parents=True, exist_ok=True)

    poses = [
        render_pose(output, "01-도끼휴대준비.png", "carry", axe_source, tree_source),
        render_pose(output, "02-나무타격순간.png", "strike", axe_source, tree_source),
    ]
    result = {
        "status": "BlenderE4PoseSamplesRendered",
        "revision": "nature-woodcutting-pose-samples.r1",
        "poses": poses,
        "inputs": {str(path): sha256(path) for path in (character, atlas, animation, axe_source, tree_source)},
        "animationUse": "Existing clip was inspected but not bound because the available character bind pose was incompatible; source files remain unchanged.",
        "actorRepresentation": "Project-owned low-poly pose blocking mannequin with supplier axe and tree.",
        "boundary": "Presentation E4 pose-blocking samples only; not a final character model, animation clip, grip/contact approval, Unity playback, or E5 evidence."
    }
    (output / "검증결과-r1.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("WOODCUTTING_POSE_SAMPLES_RENDERED")


if __name__ == "__main__":
    main()
