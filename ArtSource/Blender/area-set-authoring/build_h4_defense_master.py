"""검증된 r3 AreaSet 초안에서 H4 방어 검토용 Blender 마스터 장면을 파생한다."""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def ensure_collection(name: str, parent: bpy.types.Collection) -> bpy.types.Collection:
    existing = bpy.data.collections.get(name)
    if existing is not None:
        return existing
    collection = bpy.data.collections.new(name)
    parent.children.link(collection)
    return collection


def move_object(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for source in list(obj.users_collection):
        source.objects.unlink(obj)
    collection.objects.link(obj)


def move_hierarchy(root: bpy.types.Object, collection: bpy.types.Collection) -> None:
    move_object(root, collection)
    for child in root.children_recursive:
        move_object(child, collection)


def sample_surface(snapshot, x: float, z: float) -> float:
    columns = snapshot["Columns"]
    rows = snapshot["Rows"]
    min_x, max_x = snapshot["MinimumX"], snapshot["MaximumX"]
    min_z, max_z = snapshot["MinimumZ"], snapshot["MaximumZ"]
    step_x = (max_x - min_x) / (columns - 1)
    step_z = (max_z - min_z) / (rows - 1)
    x_pos = (x - min_x) / step_x
    z_pos = (z - min_z) / step_z
    xi = min(max(math.floor(x_pos), 0), columns - 2)
    zi = min(max(math.floor(z_pos), 0), rows - 2)
    u = max(0.0, min(1.0, x_pos - xi))
    v = max(0.0, min(1.0, z_pos - zi))
    heights = snapshot["Heights"]
    at = lambda px, pz: heights[pz * columns + px]
    a, b, c, d = at(xi, zi), at(xi + 1, zi), at(xi, zi + 1), at(xi + 1, zi + 1)
    if u + v <= 1.0:
        return a + u * (b - a) + v * (c - a)
    return d + (1.0 - u) * (c - d) + (1.0 - v) * (b - d)


def translucent_material(name: str, color) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.surface_render_method = "DITHERED"
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Alpha"].default_value = color[3]
    bsdf.inputs["Roughness"].default_value = 0.65
    return material


def create_placeholder(item, surface, collection, material) -> bpy.types.Object:
    size = item["size"]
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.object
    obj.name = "PLACEHOLDER_" + item["stableId"]
    x, z = item["position"]
    obj.location = (x, z, sample_surface(surface, x, z) + size[2] * 0.5)
    obj.scale = (size[0], size[1], size[2])
    obj.data.materials.append(material)
    obj["h1_review_state"] = "UnapprovedTranslucentPlaceholder"
    obj["h1_intended_stable_id"] = item["stableId"]
    obj.display_type = "TEXTURED"
    move_object(obj, collection)
    return obj


def route_material(name: str, color) -> bpy.types.Material:
    return translucent_material(name, color)


def create_route(route, surface, collection, material) -> bpy.types.Object:
    curve = bpy.data.curves.new("CURVE_" + route["routeCode"], "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = route.get("width", 0.16)
    curve.bevel_resolution = 3
    spline = curve.splines.new("POLY")
    spline.points.add(len(route["points"]) - 1)
    for point, (x, z) in zip(spline.points, route["points"]):
        point.co = (x, z, sample_surface(surface, x, z) + 0.18, 1.0)
    obj = bpy.data.objects.new("ROUTE_" + route["routeCode"], curve)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    obj["graph_route_code"] = route["routeCode"]
    obj["edge_kind_code"] = route["edgeKindCode"]
    obj["activation_condition_code"] = route["activationConditionCode"]
    return obj


def layer_collection(root: bpy.types.LayerCollection, name: str):
    if root.collection.name == name:
        return root
    for child in root.children:
        result = layer_collection(child, name)
        if result is not None:
            return result
    return None


def set_route_visibility(layer: bpy.types.ViewLayer, visible_route_codes: set[str]) -> None:
    for collection in bpy.data.collections:
        if not collection.name.startswith("ROUTE_"):
            continue
        target = layer_collection(layer.layer_collection, collection.name)
        if target is not None:
            target.exclude = collection.name.removeprefix("ROUTE_") not in visible_route_codes


def render_layer(scene, layer_name: str, output_path: Path) -> None:
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True, layer=layer_name)


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) != 2:
        raise SystemExit("usage: build_h4_defense_master.py -- <manifest> <output-directory>")
    manifest_path = Path(args[0]).resolve()
    output_dir = Path(args[1]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest = read_json(manifest_path)
    surface_path = Path(manifest["surfaceSnapshotPath"])
    surface = read_json(surface_path)

    scene = bpy.context.scene
    source_hash = scene.get("area_set_draft_hash")
    if source_hash != manifest["expectedSourceDraftHashSha256"]:
        raise RuntimeError("H4_SOURCE_DRAFT_HASH_STALE")

    root = ensure_collection("H4_숲경계생활농장방어지역", scene.collection)
    support = ensure_collection("H4_저작기준", root)
    h3_collections = {
        "LivingProduction": ensure_collection("H3_생활생산", root),
        "AccessDefense": ensure_collection("H3_접근방어", root),
        "DamageRecoveryReturn": ensure_collection("H3_피해회복복귀", root),
    }

    role_by_placement = manifest["placementH3Roles"]
    placement_roots = [obj for obj in list(scene.objects) if obj.get("area_set_placement_stable_id")]
    for obj in placement_roots:
        role = role_by_placement.get(obj["area_set_placement_stable_id"], "AccessDefense")
        move_hierarchy(obj, h3_collections[role])
        obj["h3_role_code"] = role
        obj["h1_review_state"] = "ConfirmedSourceCandidate"

    for obj in list(scene.objects):
        if obj in placement_roots or obj.parent is not None:
            continue
        if obj.name.startswith("Unity지표면") or obj.type in {"CAMERA", "LIGHT"}:
            move_object(obj, support)

    placeholder_material = translucent_material("미승인H1_반투명", (0.14, 0.55, 0.95, 0.32))
    placeholders = []
    for item in manifest["contextPlaceholders"]:
        placeholders.append(create_placeholder(item, surface, h3_collections[item["h3RoleCode"]], placeholder_material))

    route_colors = {
        "EnemyPrimaryIngress": (0.9, 0.12, 0.08, 0.82),
        "EnemyConditionalBreach": (0.95, 0.35, 0.05, 0.82),
        "PlayerHansDefenseTraversal": (0.1, 0.55, 1.0, 0.82),
        "SupplyRepairHandoff": (0.95, 0.78, 0.1, 0.82),
        "RetreatRecoveryReturn": (0.2, 0.85, 0.48, 0.82),
    }
    route_objects = []
    for route in manifest["routes"]:
        collection = ensure_collection("ROUTE_" + route["routeCode"], root)
        material = route_material("경로_" + route["routeCode"], route_colors[route["routeCode"]])
        route_objects.append(create_route(route, surface, collection, material))

    normal = bpy.context.view_layer
    normal.name = "Normal"
    defense = scene.view_layers.get("Defense") or scene.view_layers.new("Defense")
    breach = scene.view_layers.get("BreachRecovery") or scene.view_layers.new("BreachRecovery")
    set_route_visibility(normal, set())
    set_route_visibility(defense, {"EnemyPrimaryIngress", "PlayerHansDefenseTraversal", "SupplyRepairHandoff"})
    set_route_visibility(breach, {"EnemyConditionalBreach", "RetreatRecoveryReturn"})
    normal["review_intent"] = "평상시 생활·생산 구성"
    defense["review_intent"] = "주 진입로 방어와 플레이어·한스·보급 동선"
    breach["review_intent"] = "울타리 손상 조건부 침입과 후퇴·회복 동선"

    scene["h4_blueprint_stable_id"] = manifest["targetH4BlueprintStableId"]
    scene["h4_instance_stable_id"] = manifest["areaSetInstanceStableId"]
    scene["h4_authoring_revision"] = manifest["revision"]
    scene["evidence_boundary"] = "PresentationE4Only_NoUnityE5"
    scene.render.image_settings.file_format = "PNG"
    blend_path = output_dir / "forest-edge-living-farm-defense-h4-r4.blend"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    render_layer(scene, "Normal", output_dir / "h4-normal-overview.png")
    render_layer(scene, "Defense", output_dir / "h4-defense-overview.png")
    render_layer(scene, "BreachRecovery", output_dir / "h4-breach-recovery-overview.png")

    receipt = {
        "schemaVersion": 1,
        "targetH4BlueprintStableId": manifest["targetH4BlueprintStableId"],
        "areaSetInstanceStableId": manifest["areaSetInstanceStableId"],
        "revision": manifest["revision"],
        "sourceBlendSha256": manifest["sourceBlendSha256"],
        "sourceDraftHashSha256": source_hash,
        "confirmedPlacementCount": len(placement_roots),
        "placeholderCount": len(placeholders),
        "routeCount": len(route_objects),
        "viewLayers": [layer.name for layer in scene.view_layers],
        "outputBlendSha256": sha256(blend_path),
        "authorityBoundary": "Presentation E4 authoring evidence only; not canonical Unity placement, runtime route execution, or E5.",
    }
    (output_dir / "h4-master-receipt.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
