# archive-build.ps1 — Copy the latest build from obj/ to Compiled/internal/ with timestamp.
# Works even when AutoCAD has bin/x64/Release/FabricationSample.dll locked.
#
# Usage:
#   powershell -File archive-build.ps1           # archives from obj/x64/Release/
#   powershell -File archive-build.ps1 -Public   # also copies to Compiled/public/

param([switch]$Public)

$objDll = Join-Path $PSScriptRoot "obj\x64\Release\FabricationSample.dll"
$internalDir = Join-Path $PSScriptRoot "Compiled\internal"
$publicDir = Join-Path $PSScriptRoot "Compiled\public"

if (-not (Test-Path $objDll)) {
    Write-Host "ERROR: No DLL found at $objDll" -ForegroundColor Red
    Write-Host "Build the solution first (Release|x64)." -ForegroundColor Yellow
    exit 1
}

# Read version from the compiled DLL
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $objDll)).FileVersion
$date = Get-Date -Format "yyyyMMdd-HHmm"
$name = "FabricationSample_${version}_${date}.dll"

# Ensure directories exist
New-Item -ItemType Directory -Force -Path $internalDir | Out-Null

# Archive to internal
Copy-Item $objDll (Join-Path $internalDir $name)
Write-Host "Archived: Compiled\internal\$name" -ForegroundColor Green

if ($Public) {
    New-Item -ItemType Directory -Force -Path $publicDir | Out-Null
    Copy-Item $objDll (Join-Path $publicDir $name)
    Write-Host "Archived: Compiled\public\$name" -ForegroundColor Cyan
}

# Show archive contents
Write-Host "`n--- Internal Archive ---" -ForegroundColor DarkGray
Get-ChildItem $internalDir -Filter "*.dll" | Sort-Object LastWriteTime -Descending |
    ForEach-Object { Write-Host ("  {0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB)) }

if (Test-Path $publicDir) {
    Write-Host "`n--- Public Archive ---" -ForegroundColor DarkGray
    Get-ChildItem $publicDir -Filter "*.dll" | Sort-Object LastWriteTime -Descending |
        ForEach-Object { Write-Host ("  {0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB)) }
}
