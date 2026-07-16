# Builds a self-contained, single-file CvarcLogger.exe that can be copied to any 64-bit Windows 11
# machine and run directly -- no .NET runtime install required on the target machine.
# Note: Hamlib (rigctld.exe) is still a separate install on each machine that needs CAT control.

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\CvarcLogger.App\CvarcLogger.App.csproj"
$outDir = Join-Path $root "publish\win-x64"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $outDir "CvarcLogger.App.exe"
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published: $exe ($sizeMb MB)"
Write-Host "Copy that one file to another 64-bit Windows 11 machine and run it directly."
