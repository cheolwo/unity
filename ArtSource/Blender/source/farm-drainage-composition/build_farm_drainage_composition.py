from __future__ import annotations

import hashlib
import json
import math
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


def mat(name: str, color: tuple[float, float, float], metallic: float = 0.0, roughness: float = 0.82):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def cube(name: str, location, scale, material, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material)
    return obj


def imported_mesh(source: Path, name: str, material, target_longest: float):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    meshes = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"NO_MESH:{source}")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    longest = max(obj.dimensions)
    obj.scale *= target_longest / longest
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def look_at(obj, target: Vector):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render(scene, path: Path, camera_location, target, ortho_scale: float):
    bpy.ops.object.camera_add(location=camera_location)
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale
    look_at(camera, Vector(target))
    scene.camera = camera
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def main():
    values = args()
    if len(values) != 4:
        raise SystemExit("usage: <drain-fbx> <route-pipe-fbx> <outlet-fbx> <output-dir>")
    drain_source, route_source, outlet_source = map(lambda p: Path(p).resolve(), values[:3])
    output = Path(values[3]).resolve()
    output.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    earth = mat("절개토", (0.22, 0.13, 0.065))
    grass = mat("농장잔디", (0.14, 0.25, 0.09))
    slab = mat("세척바닥", (0.42, 0.39, 0.32))
    concrete = mat("배수콘크리트", (0.34, 0.36, 0.35))
    metal = mat("배수구금속", (0.22, 0.25, 0.25), 0.35, 0.52)
    water = mat("배출수", (0.03, 0.28, 0.48), 0.05, 0.28)

    # A shallow wash apron visibly falls toward the inlet at its right edge.
    cube("세척작업바닥_경사", (-2.55, 0.0, 0.23), (1.7, 1.35, 0.12), slab, (0.0, math.radians(2.5), 0.0))
    cube("상부토양", (-2.55, 0.0, -0.06), (1.9, 1.6, 0.24), earth)
    cube("상부잔디", (-2.55, 0.0, 0.195), (1.95, 1.65, 0.025), grass)
    # Keep the apron visible above the grass.
    cube("세척작업면", (-2.55, 0.0, 0.255), (1.7, 1.35, 0.035), slab, (0.0, math.radians(2.5), 0.0))

    drain = imported_mesh(drain_source, "01_바닥배수입구", metal, 0.78)
    drain.location = (-1.18, 0.0, 0.29)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.22, depth=0.62, location=(-1.18, 0.0, -0.02))
    drop = bpy.context.object
    drop.name = "01b_배수구낙차연결관"
    drop.data.materials.append(concrete)

    route = imported_mesh(route_source, "02_지중배수관", concrete, 4.9)
    route.rotation_euler[2] = math.radians(90)
    route.location = (1.18, 0.0, -0.24)

    # Cutaway banks reveal the pipe while retaining terrain context.
    cube("절개토_뒤", (1.15, 1.05, -0.17), (2.55, 0.52, 0.55), earth)
    cube("절개토_앞", (1.15, -1.05, -0.17), (2.55, 0.52, 0.55), earth)
    cube("잔디_뒤", (1.15, 1.05, 0.395), (2.55, 0.52, 0.025), grass)
    cube("잔디_앞", (1.15, -1.05, 0.395), (2.55, 0.52, 0.025), grass)

    outlet = imported_mesh(outlet_source, "03_수변방류구", concrete, 0.82)
    outlet.rotation_euler[2] = math.radians(90)
    outlet.location = (3.64, 0.0, -0.24)

    cube("수변둑", (4.0, 0.0, -0.17), (0.28, 1.6, 0.8), earth)
    cube("수변잔디", (4.0, 0.0, 0.655), (0.3, 1.65, 0.025), grass)
    cube("방류수로", (4.55, 0.0, -0.72), (0.55, 1.55, 0.05), water)

    # Blue flow markers make direction legible without claiming fluid simulation.
    for index, x in enumerate((-0.65, 0.35, 1.35, 2.35, 3.25), start=1):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.09, location=(x, 0.0, -0.23))
        marker = bpy.context.object
        marker.name = f"배수방향표시_{index:02d}"
        marker.scale = (1.8, 0.6, 0.6)
        marker.data.materials.append(water)

    ground = cube("검토바닥", (0.35, 0.0, -0.96), (5.1, 2.45, 0.08), grass)

    bpy.ops.object.light_add(type="AREA", location=(-1.5, -4.0, 7.0))
    bpy.context.object.data.energy = 1500
    bpy.context.object.data.size = 5.0
    bpy.ops.object.light_add(type="AREA", location=(4.0, 3.0, 4.0))
    bpy.context.object.data.energy = 850
    bpy.context.object.data.size = 4.0

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("배수시안World")
    scene.world.color = (0.025, 0.032, 0.045)
    scene.view_settings.look = "AgX - Medium High Contrast"

    blend = output / "농장-배수체계-E4-연결시안-r1.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    render(scene, output / "01-배수체계-공간조감도.png", (7.6, -9.0, 7.2), (0.35, 0.0, -0.05), 9.2)
    render(scene, output / "02-배수체계-절개측면.png", (0.4, -12.0, 2.0), (0.4, 0.0, -0.12), 9.2)

    result = {
        "status": "BlenderE4CompositionRendered",
        "revision": "farm-wastewater-drain-composition.r1",
        "flow": ["DrainEntryH1", "WastewaterDrainRouteEdge", "DischargeOutletH1"],
        "inputs": {str(path): sha256(path) for path in (drain_source, route_source, outlet_source)},
        "images": ["01-배수체계-공간조감도.png", "02-배수체계-절개측면.png"],
        "blend": blend.name,
        "flowMarkersAreDiagrammatic": True,
        "boundary": "Presentation E4 spatial composition only; no hydraulic simulation, discharge permission, Unity placement, or E5 evidence."
    }
    (output / "검증결과-r1.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("FARM_DRAINAGE_E4_RENDERED")


if __name__ == "__main__":
    main()
