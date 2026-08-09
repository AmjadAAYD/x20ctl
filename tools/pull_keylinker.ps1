# Pull the KeyLinker APK (and any split APKs) off a connected Android device.
#
# Prerequisites:
#   1. KeyLinker installed on the phone from Google Play
#   2. Developer Options -> USB debugging enabled
#   3. Phone connected by USB, "Allow USB debugging" accepted on the phone
#
# Usage:  .\tools\pull_keylinker.ps1

$ErrorActionPreference = 'Stop'

$package = 'com.pulsenet.inputset'
$dest    = Join-Path $PSScriptRoot '..\vendor\keylinker'

# Prefer adb on PATH, then the usual SDK location, then any copy bundled with a
# local scrcpy install (which is how it tends to arrive on Windows).
$adb = $null
$cmd = Get-Command adb -ErrorAction SilentlyContinue
if ($cmd) { $adb = $cmd.Source }

if (-not $adb) {
    $sdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
    if (Test-Path $sdk) { $adb = $sdk }
}
if (-not $adb) {
    $adb = Get-ChildItem "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Downloads" `
        -Filter adb.exe -Recurse -ErrorAction SilentlyContinue -Depth 3 |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $adb) {
    throw "adb not found. Install Android platform-tools, or put adb.exe on PATH."
}
Write-Host "adb: $adb" -ForegroundColor DarkGray

Write-Host "`n[1/3] checking device..." -ForegroundColor Cyan
$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' }
if (-not $devices) {
    throw "No device. Connect the phone, enable USB debugging, and accept the prompt on the phone."
}
$devices | ForEach-Object { Write-Host "  $_" }
if ($devices -match 'unauthorized') {
    throw "Device is unauthorized. Unlock the phone and accept the 'Allow USB debugging' dialog."
}

Write-Host "`n[2/3] locating $package..." -ForegroundColor Cyan
$paths = & $adb shell pm path $package 2>$null |
         Where-Object { $_ -match '^package:' } |
         ForEach-Object { ($_ -replace '^package:', '').Trim() }

if (-not $paths) {
    throw "$package is not installed. Install KeyLinker from Google Play on the phone first."
}
Write-Host "  found $($paths.Count) apk(s):"
$paths | ForEach-Object { Write-Host "    $_" }

Write-Host "`n[3/3] pulling to $dest ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dest | Out-Null
foreach ($p in $paths) {
    $name = Split-Path $p -Leaf
    & $adb pull $p (Join-Path $dest $name) | Out-Null
    $f = Get-Item (Join-Path $dest $name)
    "  {0,-32} {1,10:N0} bytes" -f $f.Name, $f.Length
}

Write-Host "`ndone. APKs are in $((Resolve-Path $dest).Path)" -ForegroundColor Green
Write-Host "base.apk is the one that matters; splits are usually resources and native libs." -ForegroundColor DarkGray
