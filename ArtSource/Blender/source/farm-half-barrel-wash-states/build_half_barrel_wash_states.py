"""Create independent Presentation E4 state files for the compact half-barrel wash point."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy


REVISION = "farm-half-barrel-wash-states.r1"


def arguments() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def material(name: str, color: tuple[float, float, float], roughness: float = 0.72):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def flatten_inner_basin() -> None:
    """Lower the source prop's central mound without touching the outer barrel wall."""
    obj = bpy.data.objects["SM_Prop_Barrel_Half_01"]
    inverse = obj.matrix_world.inverted()
    changed = 0
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        if (world.x * world.x + world.y * world.y) ** 0.5 < 0.365 and world.z > 0.35:
            world.z = 0.335
            vertex.co = inverse @ world
            changed += 1
    obj.data.update()
    obj["derivedModification"] = "FlattenedInnerBasinForWashStates"
    obj["flattenedVertexCount"] = changed


def add_water(value):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.345, depth=0.022, location=(0.0, 0.0, 0.425))
    obj = bpy.context.object
    obj.name = "반통_세척수"
    obj.data.materials.append(value)
    obj["presentationRole"] = "WashWater"
    return obj


def add_produce(name: str, location, scale, value, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(value)
    obj["presentationRole"] = "ProduceBeingWashed"
    return obj


def add_sediment(name: str, location, scale, value):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=6, location=location, scale=scale)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(value)
    obj["presentationRole"] = "DirtyWaterSediment"
    return obj


def assign_material(obj, value) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(value)


def render_and_save(scene, output: Path, code: str, label: str) -> dict:
    image = output / f"{code}-{label}.png"
    blend = output / f"{code}-{label}.blend"
    scene.render.filepath = str(image)
    scene["stateCode"] = label
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    return {
        "code": label,
        "image": image.name,
        "blend": blend.name,
        "imageSha256": sha256(image),
        "blendSha256": sha256(blend),
    }


def main() -> None:
    args = arguments()
    if len(args) != 2:
        raise SystemExit("usage: <source-blend> <output-dir>")

    source = Path(args[0]).resolve()
    output = Path(args[1]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    source_hash = sha256(source)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    flatten_inner_basin()

    scene = bpy.context.scene
    scene["candidateRevision"] = REVISION
    scene["h1Role"] = "농산물 세척 작업점"
    scene["formRole"] = "CompactHalfBarrelWashPoint"
    scene["evidenceBoundary"] = "PresentationE4StateSamples;NotUnityE5"

    clean_water = material("반통세척수_깨끗함", (0.18, 0.62, 0.74), 0.28)
    dirty_water = material("반통세척수_오염", (0.24, 0.15, 0.07), 0.86)
    potato = material("반통세척대상_감자", (0.55, 0.29, 0.09), 0.92)
    carrot = material("반통세척대상_당근", (0.86, 0.28, 0.045), 0.88)
    sediment = material("반통오염수_침전물", (0.10, 0.055, 0.018), 0.96)

    states = []

    states.append(render_and_save(scene, output, "01", "빈반통"))

    water = add_water(clean_water)
    states.append(render_and_save(scene, output, "02", "물채움"))

    produce_objects = [
        add_produce("반통세척농산물_감자_01", (-0.17, -0.05, 0.455), (0.105, 0.070, 0.052), potato, (0.2, 0.1, 0.4)),
        add_produce("반통세척농산물_감자_02", (0.13, 0.10, 0.455), (0.095, 0.064, 0.048), potato, (-0.1, 0.3, -0.2)),
        add_produce("반통세척농산물_감자_03", (0.14, -0.14, 0.455), (0.085, 0.060, 0.045), potato, (0.3, -0.2, 0.2)),
        add_produce("반통세척농산물_당근_01", (-0.05, 0.10, 0.462), (0.13, 0.030, 0.030), carrot, (0.0, 0.25, 0.6)),
    ]
    states.append(render_and_save(scene, output, "03", "농산물세척중"))

    for obj in produce_objects:
        obj.hide_render = True
        obj.hide_viewport = True
    assign_material(water, dirty_water)
    add_sediment("반통오염수침전물_01", (-0.13, -0.08, 0.439), (0.12, 0.045, 0.006), sediment)
    add_sediment("반통오염수침전물_02", (0.12, 0.10, 0.439), (0.10, 0.040, 0.006), sediment)
    states.append(render_and_save(scene, output, "04", "오염수배출대기"))

    result = {
        "candidateRevision": REVISION,
        "h1Role": "농산물 세척 작업점",
        "formRole": "CompactHalfBarrelWashPoint",
        "sourceBlend": str(source),
        "sourceBlendSha256": source_hash,
        "states": states,
        "independentFilesPerState": True,
        "derivedModification": "FlattenedInnerBasinForWashStates_SourceAssetUnchanged",
        "stateSequence": ["Empty", "CleanWaterReady", "ProduceWashing", "DirtyWaterAwaitingDrain"],
        "evidenceBoundary": {
            "presentation": "E4StateSamples",
            "canonicalHChanged": False,
            "unityScene": False,
            "waterSimulation": False,
            "drainageSimulation": False,
            "playMode": False,
            "gameView": False,
            "e5": False,
        },
    }
    (output / "검증결과-r1.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
