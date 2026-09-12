"""Create a project-owned farm water-service-point H1 review candidate.

This is a Presentation E4 visual candidate. It is not a Unity Prefab, Scene
placement, authoritative water simulation, or E5 evidence.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REVISION = "farm-water-service-point.r2"


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


def material(name: str, color: tuple[float, float, float], roughness: float = 0.82, metallic: float = 0.0):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return value


def add_cube(name: str, location, dimensions, assigned_material, bevel: float = 0.0, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        modifier = obj.modifiers.new("낮은폴리모서리", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.materials.append(assigned_material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_cylinder(name: str, location, radius: float, depth: float, assigned_material, rotation=(0.0, 0.0, 0.0), vertices=16, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(assigned_material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_torus(name: str, location, major_radius: float, minor_radius: float, assigned_material, rotation=(0.0, 0.0, 0.0), parent=None):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=16,
        minor_segments=6,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(assigned_material)
    if parent is not None:
        obj.parent = parent
    return obj


def add_anchor(name: str, location, parent, role: str):
    anchor = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(anchor)
    anchor.location = location
    anchor.empty_display_type = "SPHERE"
    anchor.empty_display_size = 0.08
    anchor["anchorRole"] = role
    anchor.parent = parent
    return anchor


def object_bounds(objects: list[bpy.types.Object]) -> tuple[list[float], list[float]]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    low = [min(getattr(p, axis) for p in points) for axis in ("x", "y", "z")]
    high = [max(getattr(p, axis) for p in points) for axis in ("x", "y", "z")]
    return low, high


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    args = arguments()
    if len(args) != 1:
        raise SystemExit("usage: <output-dir>")

    output = Path(args[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    unity_root = Path("C:/Users/user/ssalddel")
    style_references = [
        unity_root / "Assets/Synty/PolygonCity/Models/SM_Prop_Pipe_Part_Connect_01.fbx",
        unity_root / "Assets/Synty/PolygonCity/Models/SM_Prop_Pipe_Part_Straight_01.fbx",
        unity_root / "Assets/Synty/PolygonCity/Models/SM_Prop_Pipe_Part_T_01.fbx",
        unity_root / "Assets/Synty/PolygonTown/Models/Props/SM_Prop_HoseReel_01.fbx",
    ]
    missing = [str(path) for path in style_references if not path.exists()]
    if missing:
        raise RuntimeError("WATER_SERVICE_STYLE_REFERENCE_MISSING:" + "|".join(missing))

    clear_scene()
    scene = bpy.context.scene
    concrete = material("기초_회갈색", (0.36, 0.34, 0.29), 0.95)
    body = material("급수함_청록회색", (0.14, 0.29, 0.27), 0.78, 0.15)
    dark_metal = material("배관_짙은금속", (0.10, 0.13, 0.13), 0.58, 0.62)
    blue = material("상수도_청색표식", (0.05, 0.28, 0.62), 0.62, 0.18)
    valve = material("차단밸브_적갈색", (0.55, 0.11, 0.055), 0.68, 0.22)
    glass = material("압력계_유리", (0.64, 0.82, 0.90), 0.24, 0.08)
    ground_material = material("검토바닥", (0.24, 0.34, 0.20), 0.96)

    root = bpy.data.objects.new("농장_상수도급수점_H1", None)
    scene.collection.objects.link(root)
    root["h1StableId"] = "h1-proposal:farm-pressurized-water-service-point"
    root["displayNameKo"] = "농장 상수도 급수점"
    root["candidateRevision"] = REVISION
    root["ownership"] = "ProjectOwnedBlenderModel"
    root["authorityBoundary"] = "PresentationE4VisualCandidateOnly"

    add_cube("기초판", (0.0, 0.0, 0.06), (1.25, 1.00, 0.12), concrete, 0.045, root)
    add_cube("급수함본체", (-0.10, 0.08, 0.57), (0.62, 0.46, 0.90), body, 0.055, root)
    add_cube("빗물차단상판", (-0.10, 0.08, 1.055), (0.74, 0.57, 0.10), dark_metal, 0.035, root)
    add_cube("점검문", (-0.10, -0.158, 0.55), (0.46, 0.035, 0.62), dark_metal, 0.018, root)
    add_cube("점검문손잡이", (0.085, -0.187, 0.55), (0.035, 0.035, 0.17), concrete, 0.01, root)

    # Visible service riser and hose outlet. The buried main enters at the base.
    add_cylinder("상수관상승관", (0.40, 0.10, 0.49), 0.075, 0.74, dark_metal, parent=root)
    add_cylinder("상수관상부엘보", (0.40, -0.04, 0.82), 0.105, 0.16, dark_metal, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_cylinder("호스출수관", (0.40, -0.24, 0.82), 0.070, 0.36, dark_metal, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_cylinder("호스연결구본체", (0.40, -0.445, 0.82), 0.108, 0.10, dark_metal, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_torus("호스연결구청색링", (0.40, -0.505, 0.82), 0.078, 0.025, blue, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_cylinder("호스연결구내부그림자", (0.40, -0.504, 0.82), 0.057, 0.018, dark_metal, rotation=(math.pi / 2, 0.0, 0.0), parent=root)

    # A large front-facing shutoff wheel makes the interaction readable.
    add_torus("차단밸브휠", (-0.17, -0.225, 0.68), 0.145, 0.025, valve, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_cube("차단밸브가로살", (-0.17, -0.225, 0.68), (0.27, 0.025, 0.030), valve, 0.006, root)
    add_cube("차단밸브세로살", (-0.17, -0.225, 0.68), (0.030, 0.025, 0.27), valve, 0.006, root)
    add_cylinder("차단밸브축", (-0.17, -0.205, 0.68), 0.038, 0.10, valve, rotation=(math.pi / 2, 0.0, 0.0), parent=root)

    add_cylinder("압력계본체", (0.07, -0.215, 0.90), 0.105, 0.065, dark_metal, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_cylinder("압력계표면", (0.07, -0.254, 0.90), 0.082, 0.018, glass, rotation=(math.pi / 2, 0.0, 0.0), parent=root)
    add_cube("상수도청색띠", (-0.10, -0.187, 0.35), (0.45, 0.025, 0.085), blue, 0.012, root)

    anchors = [
        add_anchor("Anchor_WaterMainIn", (0.40, 0.10, 0.0), root, "BuriedWaterMainInput"),
        add_anchor("Anchor_HoseOut", (0.40, -0.535, 0.82), root, "PressurizedHoseOutput"),
        add_anchor("Anchor_Service", (-0.17, -0.30, 0.68), root, "PlayerOrNpcServiceInteraction"),
    ]

    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0.0, 0.0, -0.015))
    ground = bpy.context.object
    ground.name = "검토용바닥_권위없음"
    ground.data.materials.append(ground_material)

    bpy.ops.object.light_add(type="AREA", location=(3.0, -3.8, 4.6))
    key = bpy.context.object
    key.data.energy = 1050
    key.data.size = 4.0
    look_at(key, Vector((0.0, 0.0, 0.5)))
    bpy.ops.object.light_add(type="AREA", location=(-2.4, 1.8, 2.8))
    fill = bpy.context.object
    fill.data.energy = 520
    fill.data.size = 3.0
    look_at(fill, Vector((0.0, 0.0, 0.45)))

    bpy.ops.object.camera_add(location=(2.65, -3.25, 2.35))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.05
    look_at(camera, Vector((0.0, 0.0, 0.52)))
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.045, 0.055, 0.045)
    scene["candidateRevision"] = REVISION
    scene["evidenceBoundary"] = "PresentationE4Candidate;NotUnityE5"

    render_path = output / "농장-상수도-급수점-H1-r2.png"
    scene.render.filepath = str(render_path)
    bpy.ops.render.render(write_still=True)

    # The review ground, camera and lights are excluded from the reusable FBX.
    bpy.ops.object.select_all(action="DESELECT")
    reusable = [root] + list(root.children_recursive)
    for obj in reusable:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    fbx_path = output / "농장-상수도-급수점-H1-r2.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        path_mode="AUTO",
    )

    blend_path = output / "농장-상수도-급수점-H1-r2.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    low, high = object_bounds(reusable)
    result = {
        "candidateRevision": REVISION,
        "h1StableId": root["h1StableId"],
        "displayNameKo": root["displayNameKo"],
        "ownership": root["ownership"],
        "dimensionsMetersIncludingBase": [round(high[i] - low[i], 5) for i in range(3)],
        "anchors": [{"name": anchor.name, "role": anchor["anchorRole"], "location": [round(v, 5) for v in anchor.location]} for anchor in anchors],
        "styleReferenceAssets": [{"path": str(path), "sha256": sha256(path)} for path in style_references],
        "files": {
            "image": render_path.name,
            "imageSha256": sha256(render_path),
            "blend": blend_path.name,
            "blendSha256": sha256(blend_path),
            "fbx": fbx_path.name,
            "fbxSha256": sha256(fbx_path),
        },
        "excludedFunctions": ["Drainage", "Sewerage", "WaterTreatment", "AuthoritativeFlowSimulation"],
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
