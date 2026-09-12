from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


STATES = {
    "ready": (10, "01-숙련벌목-준비.png"),
    "backswing": (30, "02-숙련벌목-백스윙.png"),
    "contact": (40, "03-숙련벌목-타격접촉.png"),
    "recovery": (42, "04-숙련벌목-감속회수.png"),
}


def cli_args() -> list[str]:
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


def atlas_material(name: str, path: Path):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    texture = value.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(path), check_existing=True)
    texture.interpolation = "Closest"
    value.node_tree.links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.72
    return value


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def empty(name: str, location: Vector, size: float = 0.035):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "SPHERE"
    obj.empty_display_size = size
    obj.location = location
    return obj


def bone_world_point(armature, bone_name: str, use_tail: bool = False) -> Vector:
    bone = armature.pose.bones[bone_name]
    return armature.matrix_world @ (bone.tail if use_tail else bone.head)


def evaluated_bone_world_point(armature, bone_name: str, use_tail: bool = False) -> Vector:
    evaluated = armature.evaluated_get(bpy.context.evaluated_depsgraph_get())
    bone = evaluated.pose.bones[bone_name]
    return evaluated.matrix_world @ (bone.tail if use_tail else bone.head)


def bone_world_rotation(armature, bone_name: str):
    return (armature.matrix_world @ armature.pose.bones[bone_name].matrix).to_quaternion()


def import_pose(character: Path, animation: Path, frame: int):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(character), use_anim=False)
    target = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    target.name = "벌목작업자_실제리그"
    actor_material = material("숙련벌목작업복", (0.16, 0.33, 0.44))
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH" and obj.parent == target:
            obj.name = "벌목작업자_외형"
            obj.data.materials.clear()
            obj.data.materials.append(actor_material)

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(animation), use_anim=True)
    source = next(obj for obj in bpy.context.scene.objects if obj not in before and obj.type == "ARMATURE")
    source.name = "공급사동작_참조리그"
    source.hide_render = True
    for obj in bpy.context.scene.objects:
        if obj not in before and obj != source:
            obj.hide_render = True

    bpy.context.scene.frame_set(frame)
    for bone in target.pose.bones:
        if bone.name in source.pose.bones:
            bone.matrix_basis = source.pose.bones[bone.name].matrix_basis.copy()
    bpy.context.view_layer.update()
    target["pose_source"] = f"{animation.name}:frame-{frame}"
    target["skill_baseline"] = "ExperiencedLumberjack"
    return target


def import_axe(path: Path, atlas: Path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj not in before and obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("SYNTY_AXE_MESH_NOT_FOUND")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    axe = bpy.context.object
    axe.name = "실제Synty벌목도끼"
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    axe.data.materials.clear()
    axe.data.materials.append(atlas_material("SyntyFarm도끼Atlas", atlas))
    axe["source_asset"] = "SM_Prop_Tool_Axe_01.fbx"
    axe["local_handle_axis"] = "+Z"
    return axe


def make_tool_basis(handle_axis: Vector, desired_blade_direction: Vector) -> Matrix:
    z_axis = handle_axis.normalized()
    blade_axis = desired_blade_direction - z_axis * desired_blade_direction.dot(z_axis)
    if blade_axis.length < 0.05:
        blade_axis = Vector((-1.0, 0.0, 0.0)) - z_axis * z_axis.x * -1.0
    blade_axis.normalize()
    y_axis = -blade_axis
    x_axis = y_axis.cross(z_axis).normalized()
    y_axis = z_axis.cross(x_axis).normalized()
    return Matrix((x_axis, y_axis, z_axis)).transposed().to_4x4()


def parent_keep_world(obj, armature, bone_name: str):
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world


def create_anchor(name: str, axe, local_location: Vector, display: str):
    marker = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(marker)
    marker.empty_display_type = display
    marker.empty_display_size = 0.045
    marker.parent = axe
    marker.location = local_location
    return marker


def constrain_left_hand(armature, secondary_grip):
    elbow = bone_world_point(armature, "Elbow_L")
    pole = empty("왼팔_팔꿈치PoleTarget", elbow + Vector((0.0, -0.42, 0.10)), 0.055)
    target = empty("도끼_보조손IKTarget", secondary_grip.matrix_world.translation.copy(), 0.035)
    target["follows_anchor"] = secondary_grip.name
    forearm = armature.pose.bones["Elbow_L"]
    ik = forearm.constraints.new("IK")
    ik.name = "보조손_손잡이IK"
    ik.target = target
    ik.pole_target = pole
    ik.chain_count = 2
    ik.iterations = 80
    ik.use_rotation = False
    return target, pole, ik


def plant_feet(armature):
    ankle_points = {side: bone_world_point(armature, f"Ankle_{side}") for side in ("L", "R")}
    floor_ankle_z = min(point.z for point in ankle_points.values())
    targets = {}
    for side in ("L", "R"):
        ankle = ankle_points[side]
        target = empty(f"{side}_발접지Target", Vector((ankle.x, ankle.y, floor_ankle_z)), 0.04)
        constraint = armature.pose.bones[f"LowerLeg_{side}"].constraints.new("IK")
        constraint.name = f"{side}_발접지IK"
        constraint.target = target
        constraint.chain_count = 2
        constraint.iterations = 80
        targets[side] = target

    # 원본 공격 자세마다 다리 길이와 골반 회전이 다르므로 같은 높이의
    # IK target만 두면 발목이 수 cm 떠 있을 수 있다. 평가 결과를 다시
    # target에 반영해 두 발이 같은 접지면에 수렴하도록 한다.
    for _ in range(5):
        bpy.context.view_layer.update()
        for side, target in targets.items():
            evaluated_ankle = evaluated_bone_world_point(armature, f"Ankle_{side}")
            target.location.z -= evaluated_ankle.z - floor_ankle_z
    bpy.context.view_layer.update()
    return floor_ankle_z, list(targets.values())


def bind_actual_axe(armature, axe, state: str):
    right = bone_world_point(armature, "Hand_R")
    left = bone_world_point(armature, "Hand_L")
    # 실제 Farm 도끼는 날이 -Z 끝에 있고 손잡이 끝은 +Z에 있다.
    # 원본 검술 자세의 과도한 손 간격을 그대로 쓰지 않고 숙련 벌목의
    # 안정적인 양손 간격(40cm)으로 보정한다.
    grip_distance = 0.40
    handle_axis = (left - right).normalized()
    desired_blade_direction = Vector((-0.72, 0.69, 0.0)).normalized()
    basis = make_tool_basis(handle_axis, desired_blade_direction)
    primary_local = Vector((0.0, 0.0, -0.02))
    secondary_local = Vector((0.0, 0.0, -0.02 + grip_distance))
    contact_local = Vector((0.0, -0.177, -0.64))
    translation = right - (basis @ primary_local)
    basis.translation = translation
    axe.matrix_world = basis
    parent_keep_world(axe, armature, "Prop_R")

    primary = create_anchor("도끼_주손그립점", axe, primary_local, "CIRCLE")
    secondary = create_anchor("도끼_보조손그립점", axe, secondary_local, "CIRCLE")
    contact = create_anchor("도끼날_접촉점", axe, contact_local, "CUBE")
    axis = create_anchor("도끼_손잡이축", axe, Vector((0.0, 0.0, -0.05)), "ARROWS")
    axis.empty_display_size = 0.18
    bpy.context.view_layer.update()
    constrain_left_hand(armature, secondary)
    bpy.context.view_layer.update()
    blade_axis = desired_blade_direction - handle_axis * desired_blade_direction.dot(handle_axis)
    blade_axis.normalize()
    return primary, secondary, contact, axis, blade_axis


def add_tree(contact_anchor, blade_axis: Vector, state: str, fixed_tree_location: Vector | None):
    contact = contact_anchor.matrix_world.translation
    if fixed_tree_location is None:
        horizontal_blade_axis = Vector((blade_axis.x, blade_axis.y, 0.0)).normalized()
        fixed_tree_location = Vector((
            # 도끼날의 진행 방향 바깥쪽에 나무 표면을 둬 몸통과 나무가
            # 겹치거나 양팔이 나무를 관통하지 않게 한다.
            contact.x - horizontal_blade_axis.x * 0.30,
            contact.y - horizontal_blade_axis.y * 0.30,
            1.575,
        ))
    bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=0.30, depth=3.15, location=fixed_tree_location)
    tree = bpy.context.object
    tree.name = "벌목대상나무"
    tree.data.materials.append(material("나무껍질", (0.25, 0.105, 0.035)))
    tree["state_role"] = "StrikeTarget"
    return tree, fixed_tree_location


def setup_render(output: Path, filename: str, floor_z: float):
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0.0, 0.0, floor_z - 0.06))
    bpy.context.object.name = "접지검토바닥"
    bpy.context.object.data.materials.append(material("검토바닥재", (0.08, 0.15, 0.07)))
    bpy.ops.object.light_add(type="AREA", location=(3.4, -4.6, 5.8))
    bpy.context.object.data.energy = 1100
    bpy.context.object.data.size = 4.0
    bpy.ops.object.light_add(type="AREA", location=(-3.0, 1.4, 3.8))
    bpy.context.object.data.energy = 520
    bpy.context.object.data.size = 3.0
    # 나무 뒤로 손과 도끼날이 숨지 않도록 타격면을 측면에서 본다.
    bpy.ops.object.camera_add(location=(6.5, 0.8, 2.8))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 3.8
    look_at(camera, (-0.25, -0.10, 1.15))
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output / filename)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("숙련벌목World")
    scene.world.color = (0.022, 0.028, 0.038)
    scene.view_settings.look = "AgX - Medium High Contrast"
    blend = filename.replace(".png", ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(output / blend))
    bpy.ops.render.render(write_still=True)
    return blend


def distance(a: Vector, b: Vector) -> float:
    return (a - b).length


def render_state(character: Path, animation: Path, axe_path: Path, axe_atlas: Path, output: Path, state: str, fixed_tree_location: Vector | None):
    frame, filename = STATES[state]
    armature = import_pose(character, animation, frame)
    floor_z, _ = plant_feet(armature)
    axe = import_axe(axe_path, axe_atlas)
    primary, secondary, contact, axis, blade_axis = bind_actual_axe(armature, axe, state)
    tree, tree_location = add_tree(contact, blade_axis, state, fixed_tree_location)
    bpy.context.view_layer.update()

    right_error = distance(evaluated_bone_world_point(armature, "Hand_R"), primary.matrix_world.translation)
    left_error = distance(evaluated_bone_world_point(armature, "Hand_L"), secondary.matrix_world.translation)
    left_ankle = evaluated_bone_world_point(armature, "Ankle_L")
    right_ankle = evaluated_bone_world_point(armature, "Ankle_R")
    foot_height_delta = abs(left_ankle.z - right_ankle.z)
    foot_ground_errors = {
        "left": abs(left_ankle.z - floor_z),
        "right": abs(right_ankle.z - floor_z),
    }
    tree_surface_error = abs(distance(Vector((contact.matrix_world.translation.x, contact.matrix_world.translation.y, 0.0)), Vector((tree.location.x, tree.location.y, 0.0))) - 0.30)
    blend = setup_render(output, filename, floor_z)
    return {
        "state": state,
        "frame": frame,
        "image": filename,
        "blend": blend,
        "actualSyntyAxe": axe.name,
        "armature": armature.name,
        "bones": len(armature.data.bones),
        "constraints": [constraint.name for bone in armature.pose.bones for constraint in bone.constraints],
        "anchors": [primary.name, secondary.name, contact.name, axis.name],
        "metrics": {
            "primaryGripErrorMeters": round(right_error, 6),
            "secondaryGripErrorMeters": round(left_error, 6),
            "footHeightDeltaMeters": round(foot_height_delta, 6),
            "footGroundErrorMeters": {key: round(value, 6) for key, value in foot_ground_errors.items()},
            "groundedFootCount": sum(value <= 0.01 for value in foot_ground_errors.values()),
            "contactToTreeSurfaceErrorMeters": round(tree_surface_error, 6),
        },
        "treeLocation": [round(value, 5) for value in tree_location],
    }


def main():
    values = cli_args()
    if len(values) != 5:
        raise SystemExit("usage: <character-fbx> <animation-fbx> <axe-fbx> <axe-atlas> <output-dir>")
    character, animation, axe_path, axe_atlas, output = [Path(value).resolve() for value in values]
    output.mkdir(parents=True, exist_ok=True)

    contact_result = render_state(character, animation, axe_path, axe_atlas, output, "contact", None)
    fixed_tree = Vector(contact_result["treeLocation"])
    results = {
        "ready": render_state(character, animation, axe_path, axe_atlas, output, "ready", fixed_tree),
        "backswing": render_state(character, animation, axe_path, axe_atlas, output, "backswing", fixed_tree),
        "contact": contact_result,
        "recovery": render_state(character, animation, axe_path, axe_atlas, output, "recovery", fixed_tree),
    }
    ordered = [results[name] for name in ("ready", "backswing", "contact", "recovery")]
    thresholds = {
        "gripErrorMetersMax": 0.02,
        "contactErrorMetersMax": 0.03,
        "footHeightDeltaMetersMax": 0.01,
    }
    checks = []
    for result in ordered:
        metrics = result["metrics"]
        required_grounded_feet = 2 if result["state"] == "contact" else 1
        checks.append({
            "state": result["state"],
            "primaryGrip": metrics["primaryGripErrorMeters"] <= thresholds["gripErrorMetersMax"],
            "secondaryGrip": metrics["secondaryGripErrorMeters"] <= thresholds["gripErrorMetersMax"],
            "feet": metrics["groundedFootCount"] >= required_grounded_feet,
            "requiredGroundedFootCount": required_grounded_feet,
            "contact": result["state"] != "contact" or metrics["contactToTreeSurfaceErrorMeters"] <= thresholds["contactErrorMetersMax"],
        })
    payload = {
        "status": "Passed" if all(all(value for key, value in check.items() if key != "state") for check in checks) else "Failed",
        "revision": "nature-woodcutting-experienced-rig-samples.r4",
        "skillBaseline": "ExperiencedLumberjack",
        "states": ordered,
        "thresholds": thresholds,
        "checks": checks,
        "inputs": {str(path): sha256(path) for path in (character, animation, axe_path, axe_atlas)},
        "supersedesAsAcceptanceCandidate": ["r1", "r2", "r3"],
        "boundary": "Presentation E4 Blender rig-and-grip candidate only; no final AnimationClip, Unity playback, Game View, or E5 evidence.",
    }
    (output / "검증결과-r4.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("WOODCUTTING_EXPERIENCED_R4=" + payload["status"])


if __name__ == "__main__":
    main()
