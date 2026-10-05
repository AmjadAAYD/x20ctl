param([string]$Directory = '.')
$ErrorActionPreference = 'Stop'
$Directory = (Resolve-Path $Directory).Path
$lines = Get-Content (Join-Path $Directory 'SHA256SUMS.txt')
foreach ($line in $lines) {
    if ($line -notmatch '^([a-f0-9]{64})  ([A-Za-z0-9_.-]+)$') { throw 'Invalid checksum line' }
    $expected = $Matches[1]
    $name = $Matches[2]
    $actual = (Get-FileHash (Join-Path $Directory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "Checksum mismatch: $name" }
    Write-Output "Verified $name"
}
