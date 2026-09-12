# SPDX-License-Identifier: GPL-3.0-or-later
"""Blender smoke test for a non-destructive project-owned mesh variant."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def _after_separator(arguments: list[str]) -> list[str]:
    if "--" not in arguments:
        return []
    return arguments[arguments.index("--") + 1 :]


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _bounds(obj: bpy.types.Object) -> list[list[float]]:
    points = [obj.matrix_world @ Vector(point) for point in obj.bound_box]
    minimum = [min(point[index] for point in points) for index in range(3)]
    maximum = [max(point[index] for point in points) for index in range(3)]
    return [[round(value, 6) for value in minimum], [round(value, 6) for value in maximum]]


def main() -> int:
    arguments = _after_separator(sys.argv)
    if len(arguments) != 2:
        raise RuntimeError("expected <output-directory> <workspace-root>")

    output_directory = Path(arguments[0]).resolve()
    workspace_root = Path(arguments[1]).resolve()
    if output_directory.exists():
        raise RuntimeError(f"refusing to overwrite existing output: {output_directory}")
    output_directory.mkdir(parents=True)

    tools_directory = Path(__file__).resolve().parents[1] / "tools"
    sys.path.insert(0, str(tools_directory))
    from validate_variant_manifest import validate_manifest

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, 1.0))
    source = bpy.context.object
    source.name = "SupplierLikeSourceProxy_READ_ONLY"
    source.scale = (1.0, 1.5, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    source.data.name = "SupplierLikeSourceProxyMesh_READ_ONLY"
    source_bounds_before = _bounds(source)
    source_vertices_before = [tuple(vertex.co) for vertex in source.data.vertices]

    variant = source.copy()
    variant.data = source.data.copy()
    scene.collection.objects.link(variant)
    variant.name = "ProjectOwnedVariant"
    variant.data.name = "ProjectOwnedVariantMesh"
    variant["source_object"] = source.name
    variant["change_budget"] = "Subtle"
    variant["allowed_change"] = "MeshDamageState"
    for vertex in variant.data.vertices:
        if vertex.co.z > 0 and vertex.co.x > 0:
            vertex.co.z -= 0.18
            vertex.co.x -= 0.08

    if source.data is variant.data:
        raise RuntimeError("variant unexpectedly shares source mesh data")
    if source_vertices_before != [tuple(vertex.co) for vertex in source.data.vertices]:
        raise RuntimeError("source mesh changed while editing variant")
    if source_bounds_before != _bounds(source):
        raise RuntimeError("source bounds changed while editing variant")

    source.hide_render = True
    blend_path = output_directory / "project-owned-variant-smoke.blend"
    fbx_path = output_directory / "project-owned-variant-smoke.fbx"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))

    source = bpy.data.objects.get("SupplierLikeSourceProxy_READ_ONLY")
    variant = bpy.data.objects.get("ProjectOwnedVariant")
    if source is None or variant is None:
        raise RuntimeError("source or variant was missing after .blend reopen")
    if source.data is variant.data:
        raise RuntimeError("source and variant shared mesh data after .blend reopen")

    bpy.ops.object.select_all(action="DESELECT")
    variant.select_set(True)
    bpy.context.view_layer.objects.active = variant
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
    )

    fixture_id = "vendor-free-variant-smoke"
    manifest = {
        "schemaRevision": "blender-project-owned-variant.r1",
        "projectId": fixture_id,
        "status": "ValidationFixture",
        "source": {
            "provider": "GeneratedProxy",
            "ownership": "TestFixture",
            "assetPath": "artifacts/local/blender-variant-pipeline/generated-source-proxy",
            "guid": "not-applicable-test-fixture",
            "sha256": _sha256(blend_path),
            "licenseEvidenceRef": "self-generated-test-geometry",
        },
        "lineage": {
            "planningRefs": ["validation-only"],
            "wiIds": ["NotApplicable: validation fixture"],
            "hIds": ["NotApplicable: validation fixture"],
            "visualKeys": ["NotApplicable: validation fixture"],
        },
        "intent": {
            "reuseDecision": "BlenderPlanRequired",
            "changeBudget": "Subtle",
            "playerReadableDifference": "one clipped upper corner on a generated proxy",
            "allowedChanges": ["MeshDamageState"],
            "forbiddenChanges": [
                "SupplierOriginalOverwrite",
                "ExternalAiUpload",
                "UnapprovedBulkAutomation",
            ],
        },
        "outputs": {
            "blendPath": f"ArtSource/Blender/source/{fixture_id}/variant.blend",
            "exportPath": f"ArtSource/Blender/exports/{fixture_id}/variant.fbx",
            "validationPath": f"ArtSource/Blender/validation/{fixture_id}/result.json",
            "unityCandidatePath": f"Assets/SsalddelGenerated/ProjectOwnedVariants/{fixture_id}/variant.fbx",
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
    errors = validate_manifest(manifest, workspace_root)
    if errors:
        raise RuntimeError("manifest validation failed: " + "; ".join(errors))

    result = {
        "status": "Passed",
        "scope": "Generated proxy only; no Synty input and no Unity validation",
        "blenderVersion": bpy.app.version_string,
        "sourceObject": source.name,
        "variantObject": variant.name,
        "sourceBounds": _bounds(source),
        "variantBounds": _bounds(variant),
        "sourceMeshPreserved": True,
        "independentVariantMesh": True,
        "blendReopened": True,
        "fbxExported": fbx_path.is_file(),
        "blendSha256": _sha256(blend_path),
        "fbxSha256": _sha256(fbx_path),
    }
    (output_directory / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (output_directory / "result.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("BLENDER_VARIANT_PIPELINE_SMOKE_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
