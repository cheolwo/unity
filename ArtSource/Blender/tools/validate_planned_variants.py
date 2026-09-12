# SPDX-License-Identifier: GPL-3.0-or-later
"""Validate the Presentation E4 Blender planning backlog without editing assets."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any


SCHEMA_REVISION = "blender-variant-planning-backlog.r2"
STATES = {
    "PlanningOnly",
    "BlenderValidatedCopy",
    "BlockedByAssetSelection",
    "BlockedByApprovalOrContract",
    "ConditionalOnUnityAssemblyFailure",
}
PROJECT_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]{2,79}$")
TRANSITION_ID_PATTERN = re.compile(r"^presentation-transition:[a-z0-9-]+\.v[0-9]+$")


def _texts(item: dict[str, Any], key: str, path: str, errors: list[str], *, allow_empty: bool = False) -> list[str]:
    value = item.get(key)
    if not isinstance(value, list) or (not value and not allow_empty):
        errors.append(f"{path}.{key}: {'text array' if allow_empty else 'non-empty text array'} is required")
        return []
    if any(not isinstance(entry, str) or not entry.strip() for entry in value):
        errors.append(f"{path}.{key}: entries must be non-empty text")
        return []
    return [entry.strip() for entry in value]


def validate_backlog(data: dict[str, Any], workspace_root: Path | None = None) -> list[str]:
    errors: list[str] = []
    if data.get("schemaRevision") != SCHEMA_REVISION:
        errors.append(f"schemaRevision: expected {SCHEMA_REVISION}")
    if data.get("authority") != "PresentationE4PlanningReferenceOnly":
        errors.append("authority: expected PresentationE4PlanningReferenceOnly")
    if data.get("fiveElementMetadataPolicy") != "OptionalDiagnosticMetadataOnly":
        errors.append("fiveElementMetadataPolicy: expected OptionalDiagnosticMetadataOnly")

    items = data.get("items")
    if not isinstance(items, list) or not items:
        errors.append("items: non-empty array is required")
        return errors

    project_ids: set[str] = set()
    for index, item in enumerate(items):
        path = f"items[{index}]"
        if not isinstance(item, dict):
            errors.append(f"{path}: object is required")
            continue
        project_id = item.get("projectId")
        if not isinstance(project_id, str) or not PROJECT_ID_PATTERN.fullmatch(project_id):
            errors.append(f"{path}.projectId: stable lowercase project id is required")
        elif project_id in project_ids:
            errors.append(f"{path}.projectId: duplicate {project_id}")
        else:
            project_ids.add(project_id)

        state = item.get("state")
        if state not in STATES:
            errors.append(f"{path}.state: unsupported value {state}")
        transitions = _texts(item, "e4TransitionRefs", path, errors, allow_empty=True)
        for transition in transitions:
            if not TRANSITION_ID_PATTERN.fullmatch(transition):
                errors.append(f"{path}.e4TransitionRefs: invalid transition id {transition}")
        for key in ("planningRefs", "representationFirst", "exactSources", "allowedChangeCandidates", "continuityRequirements"):
            _texts(item, key, path, errors)
        if not isinstance(item.get("intendedDifference"), str) or not item["intendedDifference"].strip():
            errors.append(f"{path}.intendedDifference: non-empty text is required")
        if not isinstance(item.get("nextGate"), str) or not item["nextGate"].strip():
            errors.append(f"{path}.nextGate: non-empty text is required")

        if state == "BlenderValidatedCopy":
            if not transitions:
                errors.append(f"{path}.e4TransitionRefs: validated copy must be linked to an E4 transition")
            if any(source.startswith("Pending") for source in item.get("exactSources", [])):
                errors.append(f"{path}.exactSources: validated copy cannot use Pending source")

        if workspace_root is not None:
            for source in item.get("exactSources", []):
                if not source.startswith("Assets/"):
                    continue
                asset_path, separator, expected_guid = source.partition("#")
                absolute = workspace_root / Path(asset_path)
                if not absolute.is_file():
                    errors.append(f"{path}.exactSources: file not found {asset_path}")
                    continue
                if separator:
                    meta_path = Path(str(absolute) + ".meta")
                    if not meta_path.is_file():
                        errors.append(f"{path}.exactSources: meta not found {asset_path}.meta")
                    else:
                        guid_line = next((line for line in meta_path.read_text(encoding="utf-8").splitlines() if line.startswith("guid:")), "")
                        actual_guid = guid_line.partition(":")[2].strip()
                        if actual_guid != expected_guid:
                            errors.append(f"{path}.exactSources: guid mismatch {asset_path}")

    if not isinstance(data.get("nonEvidenceNotice"), str) or not data["nonEvidenceNotice"].strip():
        errors.append("nonEvidenceNotice: non-empty text is required")
    return errors


def main(argv: list[str]) -> int:
    arguments = list(argv)
    if "--" in arguments:
        arguments = arguments[arguments.index("--") + 1 :]
    if len(arguments) not in {1, 2}:
        print("usage: validate_planned_variants.py <planned-variants.r2.json> [workspace-root]", file=sys.stderr)
        return 2
    path = Path(arguments[0]).resolve()
    workspace_root = Path(arguments[1]).resolve() if len(arguments) == 2 else None
    with path.open("r", encoding="utf-8") as stream:
        data = json.load(stream)
    errors = validate_backlog(data, workspace_root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print(f"BLENDER_PLANNED_VARIANTS_VALID: {len(data['items'])} items")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
