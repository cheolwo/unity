#requires -Version 7.6
param(
    [Parameter(Mandatory)][string]$HongdalRoot,
    [Parameter(Mandatory)][string]$MeasurementDirectory,
    [Parameter(Mandatory)][string]$CurrentTestBinaryDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# 현행 netstandard2.1 DLL만 소비한다. 구형 후보 net10 프로젝트/생성기는 실행하지 않는다.
# 이 스크립트의 pwsh/.NET 런타임은 검증 호스트일 뿐 제품 프레임워크 변경이 아니다.
$measurementRoot = [IO.Path]::GetFullPath($MeasurementDirectory)
$unityRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $unityRoot 'artifacts/local/validation/farm-h2-measurement')) + [IO.Path]::DirectorySeparatorChar
if (-not $measurementRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputOutsideOwnedMeasurementRoot' }
$output = Join-Path $measurementRoot ('adapter-verification-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '.json')
$dllNames = @('Ssalddel.CodeMetadata','Ssalddel.Contracts','Ssalddel.Interior.Contracts','Ssalddel.Interior.Domain',
    'Ssalddel.Simulation.Contracts','Ssalddel.Simulation.Domain','Ssalddel.Simulation.Application','Ssalddel.Simulation.Tests')
$binaryHashes = @()
foreach ($name in $dllNames) {
    $path = [IO.Path]::GetFullPath((Join-Path $CurrentTestBinaryDirectory ($name + '.dll')))
    $binaryHashes += @{ path = $path; sha256 = (Get-FileHash -LiteralPath $path).Hash }
    [void][Reflection.Assembly]::LoadFrom($path)
}
$adapterType = [Type]::GetType('Ssalddel.Simulation.Application.SimulationFarmH2PlacementAdapter, Ssalddel.Simulation.Application', $true)
$measurementType = [Type]::GetType('Ssalddel.Simulation.Contracts.SimulationFarmH2AssetMeasurement, Ssalddel.Simulation.Contracts', $true)
$testType = [Type]::GetType('Ssalddel.Simulation.Tests.SimulationFarmH2PlacementAdapterTests, Ssalddel.Simulation.Tests', $true)
$requestMethod = $testType.GetMethod('Request', [Reflection.BindingFlags]'NonPublic,Static')
$surfaceType = $testType.GetNestedType('SyntheticSurface', [Reflection.BindingFlags]::NonPublic)
if ($null -eq $requestMethod -or $null -eq $surfaceType) { throw 'CurrentTestFixtureProviderChanged:review instead of silently replacing' }
$measurements = Get-Content -LiteralPath (Join-Path $measurementRoot 'measurements.json') -Raw | ConvertFrom-Json
$seals = @()
$byFile = @{}
foreach ($record in $measurements.records) {
    if ($null -eq $record.adapterMeasurementUnsealed) { throw 'MissingMeasurementRecord' }
    $m = [Activator]::CreateInstance($measurementType)
    foreach ($property in $measurementType.GetProperties()) {
        $value = $record.adapterMeasurementUnsealed.($property.Name)
        $property.SetValue($m, [Convert]::ChangeType($value, $property.PropertyType, [Globalization.CultureInfo]::InvariantCulture))
    }
    $m.MeasurementHashSha256 = $adapterType.GetMethod('ComputeMeasurementHash').Invoke($null, @($m))
    $byFile[[IO.Path]::GetFileName($record.path)] = $m
    $seals += @{ path = $record.path; measurement = $m }
}
$oldFixtureDirectory = $env:SSALDDEL_FARM_H2_FIXTURES
$env:SSALDDEL_FARM_H2_FIXTURES = Join-Path $HongdalRoot 'Ssalddel.Simulation.Tests/Fixtures/FarmH2'
$runs = @()
$fits = @()
try {
    foreach ($fixture in 0..3) {
        foreach ($variant in @('primary', 'alternate-barn', 'corrupt-candidate-hash', 'missing-barn-collider')) {
            $r = $requestMethod.Invoke($null, @([int]$fixture))
            foreach ($binding in $r.Bindings) {
                $file = switch ($binding.VisualKey) {
                    'candidate:farm.red-barn' { if ($variant -eq 'alternate-barn') { 'SM_Bld_Barn_02.prefab' } else { 'SM_Bld_Barn_01.prefab' } }
                    'candidate:farm.crop-plot' { 'SM_Env_Dirt_Rows_01.prefab' }
                    'candidate:farm.water-access' { 'SM_Prop_Well_01.prefab' }
                    'candidate:farm.grass-accent' { 'SM_Generic_Grass_Patch_01.prefab' }
                    default { throw ('UnknownVisualKey:' + $binding.VisualKey) }
                }
                # 각 요청은 독립 측정 사본을 갖는다. 원본 JSON·측정·map은 수정하지 않는다.
                $m = [Activator]::CreateInstance($measurementType)
                foreach ($property in $measurementType.GetProperties()) { $property.SetValue($m, $property.GetValue($byFile[$file])) }
                if ($variant -eq 'missing-barn-collider' -and $binding.VisualKey -eq 'candidate:farm.red-barn') {
                    $m.ActiveCollider = $false
                    $m.MeasurementHashSha256 = $adapterType.GetMethod('ComputeMeasurementHash').Invoke($null, @($m))
                }
                $binding.Measurement = $m
            }
            $originalHash = $r.ExpectedCandidateHashSha256
            if ($variant -eq 'corrupt-candidate-hash') { $r.ExpectedCandidateHashSha256 = ('0' * 64) }
            $surface = [Activator]::CreateInstance($surfaceType, @($r, 'normal'))
            $adapter = [Activator]::CreateInstance($adapterType)
            $reason = ''; $converted = $null
            try { $converted = $adapter.Convert($r, $surface) }
            catch { $reason = $_.Exception.GetBaseException().Message }
            $expectedPrefix = if ($variant -eq 'corrupt-candidate-hash') { 'CandidateHashMismatch' }
                elseif ($fixture -eq 3) { 'CandidateRejected:' }
                elseif ($variant -eq 'missing-barn-collider') { 'AssetMeasurementMissingOrInvalid' }
                else { 'MeasuredEnvelopeExceedsCandidate:' }
            $expectedPrefix = 'FarmH2:' + $expectedPrefix
            $runs += @{ fixture = $fixture; variant = $variant; candidateHash = $originalHash;
                accepted = ($null -ne $converted); reason = $reason; expectedPrefix = $expectedPrefix;
                expectedRejectionObserved = $reason.StartsWith($expectedPrefix, [StringComparison]::Ordinal);
                containsSyntheticAssetMeasurements = @($r.Bindings | Where-Object { $_.Measurement.EvidenceKindCode -eq 'SyntheticFixture' }).Count -gt 0;
                mapSurfaceWorkAreaEvidence = 'ExistingSyntheticTestContext; NOT a real Farm Session or actual ground/WorkArea';
                returnedPlan = $converted }
            if ($variant -eq 'primary') {
                $candidate = $r.CandidateJson | ConvertFrom-Json
                foreach ($p in $candidate.Placements) {
                    $b = @($r.Bindings | Where-Object SourcePlacementStableId -eq $p.StableId)[0]
                    $a = $p.Yaw * [Math]::PI / 180
                    $width = [Math]::Abs([Math]::Cos($a)) * $b.Measurement.SizeX + [Math]::Abs([Math]::Sin($a)) * $b.Measurement.SizeZ
                    $depth = [Math]::Abs([Math]::Sin($a)) * $b.Measurement.SizeX + [Math]::Abs([Math]::Cos($a)) * $b.Measurement.SizeZ
                    $fits += @{ fixture = $fixture; placement = $p.StableId; visualKey = $p.VisualKey; yaw = $p.Yaw;
                        measuredWidth = $width; measuredDepth = $depth; reservedWidth = $p.Bounds.MaxX - $p.Bounds.MinX;
                        reservedDepth = $p.Bounds.MaxZ - $p.Bounds.MinZ; uniformScale = 1;
                        scope = 'Dimension comparison only; authoritative pass/fail is unchanged Adapter' }
                }
            }
        }
    }
}
finally { $env:SSALDDEL_FARM_H2_FIXTURES = $oldFixtureDirectory }
$result = @{ utc = [DateTime]::UtcNow.ToString('O'); binaryHashes = $binaryHashes;
    sourceMeasurementSha256 = (Get-FileHash -LiteralPath (Join-Path $measurementRoot 'measurements.json')).Hash;
    toolSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash;
    seals = $seals; runs = $runs; dimensionComparisons = $fits;
    pass = @($runs | Where-Object { -not $_.expectedRejectionObserved }).Count -eq 0;
    evidence = 'Actual asset measurements + synthetic existing map/surface/WorkArea; no layout acceptance/Session/GameView/E5' }
$result | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $output -Encoding utf8NoBOM
[pscustomobject]@{ output = $output; seals = $seals.Count; rejectionChecks = $runs.Count; pass = $result.pass }
if (-not $result.pass) { throw 'UnexpectedAdapterResult:see output' }
