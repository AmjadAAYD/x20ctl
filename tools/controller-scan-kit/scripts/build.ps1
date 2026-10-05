$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (-not [Environment]::Is64BitProcess -or $env:OS -ne 'Windows_NT') {
    throw 'Build requires Windows x64 and 64-bit Python 3.12.'
}
python -m unittest discover -s tests -v
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
python src/controller_scan.py --self-check
if ($LASTEXITCODE -ne 0) { throw 'Source self-check failed' }
python -m PyInstaller --noconfirm --clean ControllerScanKit.spec
if ($LASTEXITCODE -ne 0) { throw 'PyInstaller failed' }
$exe = Join-Path (Get-Location) 'dist/ControllerScanKit.exe'
$selfCheck = & $exe --self-check
if ($LASTEXITCODE -ne 0) { throw 'Packaged self-check failed' }
$selfCheck | Set-Content -Encoding utf8 'dist/SELF_CHECK.json'
foreach ($option in @('--capabilities', '--privacy', '--dry-run')) {
    & $exe $option
    if ($LASTEXITCODE -ne 0) { throw "Packaged command failed: $option" }
}
$signature = Get-AuthenticodeSignature $exe
if ($signature.Status -ne 'NotSigned') { throw "Unexpected signing status: $($signature.Status)" }
$defender = Join-Path $env:ProgramFiles 'Windows Defender/MpCmdRun.exe'
if (-not (Test-Path $defender)) { throw 'Microsoft Defender is unavailable; release blocked' }
& $defender -Scan -ScanType 3 -File $exe -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Defender scan failed with exit code $LASTEXITCODE; release blocked" }
$status = Get-MpComputerStatus
@{ status = 'passed'; exitCode = 0; signatureVersion = $status.AntivirusSignatureVersion;
   scannedAtUtc = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -Encoding utf8 'dist/DEFENDER.json'
python scripts/generate_release.py
if ($LASTEXITCODE -ne 0) { throw 'Release packaging failed' }
& (Join-Path $PSScriptRoot 'verify_release.ps1') -Directory 'dist/release'
