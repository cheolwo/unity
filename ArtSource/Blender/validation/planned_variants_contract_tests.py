# SPDX-License-Identifier: GPL-3.0-or-later
"""Focused tests for the Presentation E4 Blender planning backlog."""

from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from validate_planned_variants import validate_backlog


def expect_error(data: dict, mutation, expected: str) -> None:
    candidate = copy.deepcopy(data)
    mutation(candidate)
    errors = validate_backlog(candidate)
    if not any(expected in error for error in errors):
        raise AssertionError(f"expected {expected!r}, got {errors!r}")


def main() -> int:
    with (ROOT / "workflows" / "planned-variants.r2.json").open("r", encoding="utf-8") as stream:
        data = json.load(stream)
    errors = validate_backlog(data, ROOT.parents[1])
    if errors:
        raise AssertionError(errors)
    if len(data["items"]) != 8:
        raise AssertionError("expected eight surveyed planning candidates")

    expect_error(data, lambda value: value.__setitem__("fiveElementMetadataPolicy", "RequiredModels"), "OptionalDiagnosticMetadataOnly")
    expect_error(data, lambda value: value["items"][0].__setitem__("state", "Ready"), "unsupported value")
    expect_error(data, lambda value: value["items"][0].__setitem__("e4TransitionRefs", []), "validated copy must be linked")
    expect_error(data, lambda value: value["items"][0].__setitem__("exactSources", ["PendingSource"]), "cannot use Pending source")
    expect_error(data, lambda value: value["items"][1].__setitem__("continuityRequirements", []), "non-empty text array")

    print("BLENDER_PLANNED_VARIANTS_CONTRACT_PASS: 6/6;ITEMS=8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
