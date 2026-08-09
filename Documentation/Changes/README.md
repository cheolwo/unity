# Unity visual change evidence

Scene, prefab, material, camera, or UI changes that affect the rendered result
must include a final Game View PNG in the same contextual commit as the related
code and Scene files.

- Keep exploratory captures and test output under `artifacts/`.
- Keep only representative final PNG files under `<date>-<topic>/`.
- Prefer a Play Mode Game View. Record the limitation when only Edit Mode can
  be captured.
- Scene View images are supplemental and do not replace Game View evidence.
- Do not capture credentials, personal information, or operational private data.
