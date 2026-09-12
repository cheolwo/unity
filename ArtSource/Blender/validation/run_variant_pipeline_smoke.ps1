param(
    [string]$BlenderExe,
    [string]$OutputRoot = "C:/Users/user/ssalddel/artifacts/blender-variant-pipeline"
)

$ErrorActionPreference = "Stop"

$candidates = @(
    $BlenderExe,
    $env:BLENDER_EXE,
    "C:/Users/user/AppData/Local/Programs/Blender Foundation/blender-5.2.1-windows-x64/blender.exe",
    "C:/Users/user/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if ($candidates.Count -eq 0) {
    throw "Blender 5.2.1 LTS executable was not found. Pass -BlenderExe explicitly."
}

$resolvedBlender = (Resolve-Path -LiteralPath $candidates[0]).Path
$workspaceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../../..")).Path
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$workspaceArtifacts = [System.IO.Path]::GetFullPath((Join-Path $workspaceRoot "artifacts"))
if (-not $resolvedOutputRoot.StartsWith($workspaceArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay under the Unity workspace artifacts directory."
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDirectory = Join-Path $resolvedOutputRoot $runId
$scriptPath = Join-Path $PSScriptRoot "variant_pipeline_smoke.py"

& $resolvedBlender `
    --background `
    --factory-startup `
    --offline-mode `
    --disable-autoexec `
    --python-exit-code 1 `
    --python $scriptPath `
    -- $outputDirectory $workspaceRoot

if ($LASTEXITCODE -ne 0) {
    throw "Blender variant pipeline smoke failed with exit code $LASTEXITCODE."
}

$resultPath = Join-Path $outputDirectory "result.json"
if (-not (Test-Path -LiteralPath $resultPath)) {
    throw "Blender variant pipeline result was not created."
}

$result = Get-Content -Raw -Encoding UTF8 -LiteralPath $resultPath | ConvertFrom-Json
if ($result.status -ne "Passed" -or -not $result.sourceMeshPreserved -or -not $result.independentVariantMesh -or -not $result.blendReopened -or -not $result.fbxExported) {
    throw "Blender variant pipeline result did not satisfy the smoke contract."
}

Write-Output "BLENDER_VARIANT_PIPELINE_SMOKE_PASS"
Write-Output "Blender=$resolvedBlender"
Write-Output "Result=$resultPath"
