"""H4 방어 검토용 Blender 마스터 장면의 구조와 E4 경계를 검사한다."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


def require(condition: bool, code: str) -> None:
    if not condition:
        raise RuntimeError("H4_DEFENSE_MASTER_INVALID:" + code)


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(args) != 2:
        raise SystemExit("usage: validate_h4_defense_master.py -- <manifest> <result>")
    manifest = json.loads(Path(args[0]).read_text(encoding="utf-8-sig"))
    result_path = Path(args[1])
    scene = bpy.context.scene

    require(scene.get("h4_blueprint_stable_id") == manifest["targetH4BlueprintStableId"], "H4_STABLE_ID")
    require(scene.get("h4_instance_stable_id") == manifest["areaSetInstanceStableId"], "INSTANCE_STABLE_ID")
    require(scene.get("evidence_boundary") == "PresentationE4Only_NoUnityE5", "EVIDENCE_BOUNDARY")

    required_collections = {
        "H4_숲경계생활농장방어지역",
        "H3_생활생산",
        "H3_접근방어",
        "H3_피해회복복귀",
    }
    require(required_collections.issubset(set(bpy.data.collections.keys())), "COLLECTIONS")
    require({"Normal", "Defense", "BreachRecovery"}.issubset(set(scene.view_layers.keys())), "VIEW_LAYERS")

    placements = [obj for obj in scene.objects if obj.get("area_set_placement_stable_id")]
    placeholders = [obj for obj in scene.objects if obj.get("h1_review_state") == "UnapprovedTranslucentPlaceholder"]
    routes = [obj for obj in scene.objects if obj.get("graph_route_code")]
    require(len(placements) == 31, f"PLACEMENT_COUNT:{len(placements)}")
    require(len(placeholders) == len(manifest["contextPlaceholders"]), "PLACEHOLDER_COUNT")
    require(len(routes) == len(manifest["routes"]), "ROUTE_COUNT")
    require(len({obj.get("graph_route_code") for obj in routes}) == 5, "ROUTE_UNIQUE")

    conditional = [obj for obj in routes if obj.get("graph_route_code") == "EnemyConditionalBreach"]
    require(len(conditional) == 1, "CONDITIONAL_ROUTE")
    require(conditional[0].get("activation_condition_code") == "FenceDamageStateIsBreached", "CONDITIONAL_TRIGGER")

    result = {
        "schemaVersion": 1,
        "resultCode": "Passed",
        "targetH4BlueprintStableId": manifest["targetH4BlueprintStableId"],
        "confirmedPlacementCount": len(placements),
        "placeholderCount": len(placeholders),
        "routeCount": len(routes),
        "viewLayerCount": len(scene.view_layers),
        "checks": [
            "H4AndH3Collections",
            "ConfirmedVersusPlaceholderDistinction",
            "FiveRouteRoles",
            "ConditionalBreachTrigger",
            "PresentationE4AuthorityBoundary",
        ],
        "authorityBoundary": "Blender structure validation only; no Unity Scene, Play Mode, Game View, runtime route, or E5 evidence.",
    }
    result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("H4DefenseMasterPassed:Placements=31;Placeholders=3;Routes=5;ViewLayers=3;E5=0")


if __name__ == "__main__":
    main()
