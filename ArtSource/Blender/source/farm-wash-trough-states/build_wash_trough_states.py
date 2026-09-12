"""Create independent Presentation E4 state files for the long farm wash trough."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy


REVISION = "farm-wash-trough-states.r1"


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


def assign_material(obj, value) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(value)


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

    scene = bpy.context.scene
    scene["candidateRevision"] = REVISION
    scene["h1Role"] = "농산물 세척 작업점"
    scene["evidenceBoundary"] = "PresentationE4StateSamples;NotUnityE5"
    water = bpy.data.objects["SM_Prop_Trough_Water_01"]
    clean_water = material("세척수_깨끗함", (0.18, 0.62, 0.74), 0.28)
    dirty_water = material("세척수_오염", (0.24, 0.15, 0.07), 0.86)
    potato = material("세척대상_감자", (0.55, 0.29, 0.09), 0.92)
    carrot = material("세척대상_당근", (0.86, 0.28, 0.045), 0.88)
    sediment = material("오염수_침전물", (0.10, 0.055, 0.018), 0.96)

    states = []

    # 01: the fixture itself remains visible while the water plane is absent.
    water.hide_render = True
    water.hide_viewport = True
    states.append(render_and_save(scene, output, "01", "빈수조"))

    # 02: clean water is present, but no produce has entered the trough.
    water.hide_render = False
    water.hide_viewport = False
    assign_material(water, clean_water)
    states.append(render_and_save(scene, output, "02", "물채움"))

    # 03: a small mixed batch is visibly being washed in the clean water.
    produce_objects = [
        add_produce("세척농산물_감자_01", (-0.85, -0.05, 0.345), (0.13, 0.08, 0.055), potato, (0.2, 0.1, 0.4)),
        add_produce("세척농산물_감자_02", (-0.35, 0.07, 0.345), (0.11, 0.075, 0.06), potato, (-0.1, 0.3, -0.2)),
        add_produce("세척농산물_감자_03", (0.20, -0.07, 0.345), (0.14, 0.085, 0.055), potato, (0.3, -0.2, 0.2)),
        add_produce("세척농산물_감자_04", (0.78, 0.05, 0.345), (0.115, 0.07, 0.052), potato, (-0.2, 0.1, 0.5)),
        add_produce("세척농산물_당근_01", (-0.05, 0.08, 0.355), (0.16, 0.035, 0.035), carrot, (0.0, 0.25, 0.4)),
    ]
    states.append(render_and_save(scene, output, "03", "농산물세척중"))

    # 04: produce has left; muddy water and sediment remain for drain handling.
    for obj in produce_objects:
        obj.hide_render = True
        obj.hide_viewport = True
    assign_material(water, dirty_water)
    add_sediment("오염수침전물_01", (-0.70, -0.10, 0.325), (0.24, 0.07, 0.008), sediment)
    add_sediment("오염수침전물_02", (0.10, 0.10, 0.325), (0.31, 0.06, 0.008), sediment)
    add_sediment("오염수침전물_03", (0.82, -0.08, 0.325), (0.18, 0.05, 0.008), sediment)
    states.append(render_and_save(scene, output, "04", "오염수배출대기"))

    result = {
        "candidateRevision": REVISION,
        "h1Role": "농산물 세척 작업점",
        "sourceBlend": str(source),
        "sourceBlendSha256": source_hash,
        "states": states,
        "independentFilesPerState": True,
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
