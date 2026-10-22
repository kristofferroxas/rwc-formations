# RWC Formations: one-click setup (started by "Install or update.bat").
#
# 1. Finds the Mechabellum folder (Steam libraries), or asks for it.
# 2. Installs BepInEx 6 build 788 if it isn't there: downloads the official zip from
#    builds.bepinex.dev, checks its key files against known fingerprints, and only
#    then copies it in. A download that doesn't match is refused.
# 3. Copies RwcFormations.dll (next to this script) into BepInEx\plugins. Running it
#    again later just updates the mod.
# Changes nothing else. Undo: delete winhttp.dll from the game folder (all mods off)
# or BepInEx\plugins\RwcFormations.dll (this mod only).
param(
    [string]$GameDir,       # skip the search (testing, or an unusual install)
    [string]$BepInExZip     # use this local BepInEx zip instead of downloading (testing)
)
$ErrorActionPreference = 'Stop'

$BepInExUrl = 'https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip'
# SHA-256 of key files in the official build 788 zip (taken from a working install).
$Pinned = [ordered]@{
    'winhttp.dll'                              = '8C6CDBC38836DEE87E3368F5DE1994D7C0CCEBF29E4CE7ABA3C0981F9375412C'
    'BepInEx\core\BepInEx.Core.dll'            = '1D492EEECD40CD4FEB7DC35AFF7F08839928AC1637D4993BBDE0060DE36B7E0A'
    'BepInEx\core\BepInEx.Unity.IL2CPP.dll'    = '46CF1EF802BDF8CB6587FC1E4D98F7AF1073A60487B39A64E230E6F6E4C23AEC'
    'BepInEx\core\Il2CppInterop.Runtime.dll'   = '65AB051A681C2C1E52DF7596FF738BE401AAE72CFF882FF18241CF54A3EA38A4'
    'BepInEx\core\BepInEx.Preloader.Core.dll'  = '1A2824268A24D3ECA2931DAA7B7B378D7338A542502BCF9E2261919095B1EB97'
}

function Say($text, $colour = 'Gray') { Write-Host $text -ForegroundColor $colour }
function Fail($text) { Say ''; Say "Stopped: $text" 'Red'; exit 1 }
function Hash($path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }

Say 'RWC Formations setup' 'Cyan'
Say ''

$modDll = Join-Path $PSScriptRoot 'RwcFormations.dll'
if (-not (Test-Path -LiteralPath $modDll)) { Fail 'RwcFormations.dll is missing. Unzip the whole package first, then run this again from the unzipped folder.' }

# --- 1. The game folder -------------------------------------------------------
function Find-Game {
    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $item = Get-ItemProperty -Path $key -ErrorAction Stop
            foreach ($name in 'SteamPath', 'InstallPath') { if ($item.$name) { $roots.Add(($item.$name -replace '/', '\')) } }
        } catch { }
    }
    $roots.Add("${env:ProgramFiles(x86)}\Steam")
    $libraries = New-Object System.Collections.Generic.List[string]
    foreach ($root in ($roots | Select-Object -Unique)) {
        $libraries.Add($root)
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($line in Get-Content -LiteralPath $vdf) {
                if ($line -match '"path"\s+"(.+)"') { $libraries.Add(($Matches[1] -replace '\\\\', '\')) }
            }
        }
    }
    foreach ($lib in ($libraries | Select-Object -Unique)) {
        $dir = Join-Path $lib 'steamapps\common\Mechabellum'
        if (Test-Path -LiteralPath (Join-Path $dir 'Mechabellum.exe')) { return $dir }
    }
    return $null
}

if (-not $GameDir) { $GameDir = Find-Game }
if (-not $GameDir) {
    Say "Couldn't find Mechabellum automatically." 'Yellow'
    Say 'In Steam: right-click Mechabellum > Manage > Browse local files, copy that folder''s address, and paste it here.'
    $GameDir = (Read-Host 'Mechabellum folder').Trim('"', ' ')
}
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Mechabellum.exe'))) { Fail "no Mechabellum.exe in $GameDir" }
Say "Game folder: $GameDir"

# Only the game in THIS folder matters (its files are the ones being replaced).
$gameExe = (Join-Path $GameDir 'Mechabellum.exe')
function Game-Running {
    foreach ($p in Get-Process -Name 'Mechabellum' -ErrorAction SilentlyContinue) {
        try { if ($p.Path -and ((Resolve-Path -LiteralPath $p.Path).Path -eq (Resolve-Path -LiteralPath $gameExe).Path)) { return $true } }
        catch { return $true }   # can't tell where it runs from: assume it's this one
    }
    return $false
}
for ($tries = 0; Game-Running; $tries++) {
    if ($tries -ge 5) { Fail 'Mechabellum is still running. Close the game and run this again.' }
    Say 'Mechabellum is running. Close the game, then press Enter.' 'Yellow'
    [void](Read-Host)
}

# --- 2. BepInEx --------------------------------------------------------------
$core = Join-Path $GameDir 'BepInEx\core\BepInEx.Core.dll'
if (Test-Path -LiteralPath $core) {
    if ((Hash $core) -eq $Pinned['BepInEx\core\BepInEx.Core.dll']) { Say 'BepInEx build 788 is already installed.' 'Green' }
    else { Say 'BepInEx is already installed (a different build). Keeping it; the mod was made for build 788.' 'Yellow' }
} else {
    $work = Join-Path $env:TEMP ('rwc-bepinex-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $zip = $BepInExZip
        if (-not $zip) {
            Say 'Downloading BepInEx build 788 from builds.bepinex.dev ...'
            $zip = Join-Path $work 'bepinex.zip'
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $old = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'   # the progress bar makes the download crawl
            try { Invoke-WebRequest -Uri $BepInExUrl -OutFile $zip -UseBasicParsing }
            catch { Fail "the download failed ($($_.Exception.Message)). Check your internet connection, or install BepInEx by hand (see HOW-TO-INSTALL.txt)." }
            finally { $ProgressPreference = $old }
        }
        Say 'Checking the download ...'
        $unpacked = Join-Path $work 'unpacked'
        Expand-Archive -LiteralPath $zip -DestinationPath $unpacked
        foreach ($file in $Pinned.Keys) {
            $p = Join-Path $unpacked $file
            if (-not (Test-Path -LiteralPath $p)) { Fail "the BepInEx download is missing $file, so nothing was installed." }
            if ((Hash $p) -ne $Pinned[$file]) { Fail "the BepInEx download doesn't match the known build 788 ($file differs), so nothing was installed." }
        }
        Say 'Download checked: it is the official build 788.' 'Green'
        try { Copy-Item -Path (Join-Path $unpacked '*') -Destination $GameDir -Recurse -Force }
        catch [System.UnauthorizedAccessException] { Fail 'Windows refused to write to the game folder. Right-click "Install or update.bat" and choose Run as administrator.' }
        Say 'BepInEx installed.' 'Green'
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- 3. The mod --------------------------------------------------------------
$plugins = Join-Path $GameDir 'BepInEx\plugins'
$target = Join-Path $plugins 'RwcFormations.dll'
try {
    if (-not (Test-Path -LiteralPath $plugins)) { New-Item -ItemType Directory -Path $plugins | Out-Null }
    if ((Test-Path -LiteralPath $target) -and ((Hash $target) -eq (Hash $modDll))) { Say 'RWC Formations is already up to date.' 'Green' }
    else {
        $updating = Test-Path -LiteralPath $target
        Copy-Item -LiteralPath $modDll -Destination $target -Force
        if ((Hash $target) -ne (Hash $modDll)) { Fail 'copying RwcFormations.dll went wrong; run this again.' }
        if ($updating) { Say 'RWC Formations updated.' 'Green' } else { Say 'RWC Formations installed.' 'Green' }
    }
} catch [System.UnauthorizedAccessException] { Fail 'Windows refused to write to the game folder. Right-click "Install or update.bat" and choose Run as administrator.' }

Say ''
Say 'Done. Start Mechabellum. The first start after installing BepInEx takes a few minutes' 'Cyan'
Say 'longer while it sets itself up (it may look stuck: let it finish).' 'Cyan'
Say 'In a game, the panel shows during deployment (F6 shows or hides it).' 'Cyan'
exit 0
