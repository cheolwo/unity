# SPDX-License-Identifier: GPL-3.0-or-later
"""Validate a project-owned Blender variant manifest without editing assets."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path, PurePosixPath
from typing import Any


SCHEMA_REVISION = "blender-project-owned-variant.r1"
STATUSES = {
    "PlanningOnly",
    "ReadyForLocalEdit",
    "Edited",
    "BlenderValidatedCopy",
    "UnityImportCandidate",
    "ValidationFixture",
}
ALLOWED_CHANGES = {
    "CompositionOnly",
    "MeshProportion",
    "MeshDamageState",
    "MeshAddition",
    "MaterialSlotReuse",
    "RigKeyframeAdjustment",
}
REQUIRED_FORBIDDEN_CHANGES = {
    "SupplierOriginalOverwrite",
    "ExternalAiUpload",
    "UnapprovedBulkAutomation",
}
PROJECT_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]{2,79}$")
SHA256_PATTERN = re.compile(r"^[0-9a-fA-F]{64}$")
GUID_PATTERN = re.compile(r"^[0-9a-fA-F]{32}$")


def _required_object(data: dict[str, Any], key: str, errors: list[str]) -> dict[str, Any]:
    value = data.get(key)
    if not isinstance(value, dict):
        errors.append(f"{key}: object is required")
        return {}
    return value


def _required_text(data: dict[str, Any], key: str, path: str, errors: list[str]) -> str:
    value = data.get(key)
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{path}.{key}: non-empty text is required")
        return ""
    return value.strip()


def _required_text_list(data: dict[str, Any], key: str, path: str, errors: list[str]) -> list[str]:
    value = data.get(key)
    if not isinstance(value, list) or not value or any(not isinstance(item, str) or not item.strip() for item in value):
        errors.append(f"{path}.{key}: a non-empty text array is required")
        return []
    return [item.strip() for item in value]


def _normalized_relative_path(value: str, path: str, errors: list[str]) -> str:
    normalized = value.replace("\\", "/")
    pure = PurePosixPath(normalized)
    if pure.is_absolute() or ".." in pure.parts:
        errors.append(f"{path}: must be a workspace-relative path without '..'")
    return normalized


def validate_manifest(data: dict[str, Any], workspace_root: Path | None = None) -> list[str]:
    errors: list[str] = []
    if data.get("schemaRevision") != SCHEMA_REVISION:
        errors.append(f"schemaRevision: expected {SCHEMA_REVISION}")

    project_id = _required_text(data, "projectId", "manifest", errors)
    if project_id and not PROJECT_ID_PATTERN.fullmatch(project_id):
        errors.append("projectId: use lowercase letters, digits, and hyphens")

    status = _required_text(data, "status", "manifest", errors)
    if status and status not in STATUSES:
        errors.append(f"status: unsupported value {status}")

    source = _required_object(data, "source", errors)
    source_provider = _required_text(source, "provider", "source", errors)
    ownership = _required_text(source, "ownership", "source", errors)
    source_path = _normalized_relative_path(_required_text(source, "assetPath", "source", errors), "source.assetPath", errors)
    guid = _required_text(source, "guid", "source", errors)
    sha256 = _required_text(source, "sha256", "source", errors)
    _required_text(source, "licenseEvidenceRef", "source", errors)

    if status not in {"PlanningOnly", "ValidationFixture"}:
        if source_provider == "Synty" and ownership != "SupplierOriginalReadOnly":
            errors.append("source.ownership: Synty source must be SupplierOriginalReadOnly")
        if not GUID_PATTERN.fullmatch(guid):
            errors.append("source.guid: expected a 32-character Unity meta GUID")
        if not SHA256_PATTERN.fullmatch(sha256):
            errors.append("source.sha256: expected a 64-character SHA-256")
        if "<" in source_path or ">" in source_path:
            errors.append("source.assetPath: placeholder is not allowed outside PlanningOnly")

    lineage = _required_object(data, "lineage", errors)
    for key in ("planningRefs", "wiIds", "hIds", "visualKeys"):
        _required_text_list(lineage, key, "lineage", errors)

    intent = _required_object(data, "intent", errors)
    _required_text(intent, "reuseDecision", "intent", errors)
    if intent.get("changeBudget") != "Subtle":
        errors.append("intent.changeBudget: only Subtle is supported by this workflow")
    _required_text(intent, "playerReadableDifference", "intent", errors)
    allowed_changes = set(_required_text_list(intent, "allowedChanges", "intent", errors))
    unknown_changes = sorted(allowed_changes - ALLOWED_CHANGES)
    if unknown_changes:
        errors.append("intent.allowedChanges: unsupported values " + ", ".join(unknown_changes))
    forbidden_changes = set(_required_text_list(intent, "forbiddenChanges", "intent", errors))
    missing_forbidden = sorted(REQUIRED_FORBIDDEN_CHANGES - forbidden_changes)
    if missing_forbidden:
        errors.append("intent.forbiddenChanges: missing " + ", ".join(missing_forbidden))

    outputs = _required_object(data, "outputs", errors)
    output_rules = {
        "blendPath": f"ArtSource/Blender/source/{project_id}/",
        "exportPath": f"ArtSource/Blender/exports/{project_id}/",
        "validationPath": f"ArtSource/Blender/validation/{project_id}/",
        "unityCandidatePath": f"Assets/SsalddelGenerated/ProjectOwnedVariants/{project_id}/",
    }
    normalized_outputs: dict[str, str] = {}
    for key, prefix in output_rules.items():
        value = _normalized_relative_path(_required_text(outputs, key, "outputs", errors), f"outputs.{key}", errors)
        normalized_outputs[key] = value
        if project_id and not value.startswith(prefix):
            errors.append(f"outputs.{key}: expected prefix {prefix}")
        if value.startswith("Assets/Synty/"):
            errors.append(f"outputs.{key}: supplier folder is read-only")
        if source_path and value == source_path:
            errors.append(f"outputs.{key}: must not overwrite source.assetPath")

    policy = _required_object(data, "executionPolicy", errors)
    if policy.get("mode") != "ManualLocalEditOnly":
        errors.append("executionPolicy.mode: expected ManualLocalEditOnly")
    for key in ("externalUploadAllowed", "supplierOriginalMutationAllowed", "autoexecAllowed"):
        if policy.get(key) is not False:
            errors.append(f"executionPolicy.{key}: must be false")

    acceptance = _required_object(data, "acceptance", errors)
    for key in (
        "preserveUnitScale",
        "preserveMaterialSlots",
        "preserveRigWhenPresent",
        "requireBlendReopen",
        "requireFbxRoundTrip",
        "requireUnityImportReview",
        "requireGameViewReview",
    ):
        if acceptance.get(key) is not True:
            errors.append(f"acceptance.{key}: must be true")

    if workspace_root is not None and status not in {"PlanningOnly", "ValidationFixture"} and source_path:
        absolute_source = workspace_root / Path(source_path)
        if not absolute_source.is_file():
            errors.append(f"source.assetPath: file not found: {absolute_source}")

    return errors


def main(argv: list[str]) -> int:
    arguments = list(argv)
    if "--" in arguments:
        arguments = arguments[arguments.index("--") + 1 :]
    if not arguments:
        print("usage: validate_variant_manifest.py <manifest.json> [workspace-root]", file=sys.stderr)
        return 2

    manifest_path = Path(arguments[0]).resolve()
    workspace_root = Path(arguments[1]).resolve() if len(arguments) > 1 else None
    with manifest_path.open("r", encoding="utf-8") as stream:
        data = json.load(stream)
    errors = validate_manifest(data, workspace_root)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print(f"BLENDER_VARIANT_MANIFEST_VALID: {data['projectId']} ({data['status']})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
