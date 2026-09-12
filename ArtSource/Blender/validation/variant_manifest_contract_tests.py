# SPDX-License-Identifier: GPL-3.0-or-later
"""Focused contract checks for the Blender variant manifest validator."""

from __future__ import annotations

import copy
import sys
from pathlib import Path


TOOLS_DIRECTORY = Path(__file__).resolve().parents[1] / "tools"
sys.path.insert(0, str(TOOLS_DIRECTORY))
from validate_variant_manifest import validate_manifest


def valid_manifest() -> dict:
    project_id = "contract-test-variant"
    return {
        "schemaRevision": "blender-project-owned-variant.r1",
        "projectId": project_id,
        "status": "ReadyForLocalEdit",
        "source": {
            "provider": "Synty",
            "ownership": "SupplierOriginalReadOnly",
            "assetPath": "Assets/Synty/Test/Source.prefab",
            "guid": "0" * 32,
            "sha256": "1" * 64,
            "licenseEvidenceRef": "test-purchase-evidence",
        },
        "lineage": {
            "planningRefs": ["approved-plan.r1"],
            "wiIds": ["WI-TEST-01"],
            "hIds": ["H1-TEST-01"],
            "visualKeys": ["visual:test:variant.r1"],
        },
        "intent": {
            "reuseDecision": "BlenderPlanRequired",
            "changeBudget": "Subtle",
            "playerReadableDifference": "small damaged corner",
            "allowedChanges": ["MeshDamageState"],
            "forbiddenChanges": [
                "SupplierOriginalOverwrite",
                "ExternalAiUpload",
                "UnapprovedBulkAutomation",
            ],
        },
        "outputs": {
            "blendPath": f"ArtSource/Blender/source/{project_id}/variant.blend",
            "exportPath": f"ArtSource/Blender/exports/{project_id}/variant.fbx",
            "validationPath": f"ArtSource/Blender/validation/{project_id}/result.json",
            "unityCandidatePath": f"Assets/SsalddelGenerated/ProjectOwnedVariants/{project_id}/variant.fbx",
        },
        "executionPolicy": {
            "mode": "ManualLocalEditOnly",
            "externalUploadAllowed": False,
            "supplierOriginalMutationAllowed": False,
            "autoexecAllowed": False,
        },
        "acceptance": {
            "preserveUnitScale": True,
            "preserveMaterialSlots": True,
            "preserveRigWhenPresent": True,
            "requireBlendReopen": True,
            "requireFbxRoundTrip": True,
            "requireUnityImportReview": True,
            "requireGameViewReview": True,
        },
    }


def expect_error(label: str, mutation, expected_fragment: str) -> None:
    manifest = valid_manifest()
    mutation(manifest)
    errors = validate_manifest(manifest)
    if not any(expected_fragment in error for error in errors):
        raise AssertionError(f"{label}: expected {expected_fragment!r}, got {errors!r}")


def main() -> int:
    if validate_manifest(valid_manifest()):
        raise AssertionError("valid manifest was rejected")

    expect_error(
        "supplier output",
        lambda manifest: manifest["outputs"].__setitem__("blendPath", "Assets/Synty/Test/Source.prefab"),
        "supplier folder is read-only",
    )
    expect_error(
        "external upload",
        lambda manifest: manifest["executionPolicy"].__setitem__("externalUploadAllowed", True),
        "externalUploadAllowed: must be false",
    )
    expect_error(
        "source mutation",
        lambda manifest: manifest["executionPolicy"].__setitem__("supplierOriginalMutationAllowed", True),
        "supplierOriginalMutationAllowed: must be false",
    )
    expect_error(
        "bulk automation",
        lambda manifest: manifest["intent"]["allowedChanges"].append("BulkAutoRemesh"),
        "unsupported values BulkAutoRemesh",
    )
    expect_error(
        "missing lineage",
        lambda manifest: manifest["lineage"].__setitem__("wiIds", []),
        "lineage.wiIds: a non-empty text array is required",
    )

    copied = copy.deepcopy(valid_manifest())
    copied["status"] = "PlanningOnly"
    copied["source"]["guid"] = "<pending>"
    copied["source"]["sha256"] = "<pending>"
    if validate_manifest(copied):
        raise AssertionError("PlanningOnly manifest should permit unresolved source identifiers")

    print("BLENDER_VARIANT_MANIFEST_CONTRACT_PASS: 7/7")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
