# Builds a self-contained, single-file CvarcLogger.exe that can be copied to any 64-bit Windows 10/11
# machine and run directly -- no .NET runtime install required on the target machine.
# Note: Hamlib (rigctld.exe) is still a separate install on each machine that needs CAT control.
# Note: the overview-PDF step below needs Python (with the `markdown` and `Pillow` packages) and
# mermaid-cli (`npm install -g @mermaid-js/mermaid-cli`, plus its bundled Chromium) on the machine
# running this script -- it falls back to the raw .md if either is missing.

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\CvarcLogger.App\CvarcLogger.App.csproj"
$outDir = Join-Path $root "publish\CvarcLogger"
$appVersionFile = Join-Path $root "src\CvarcLogger.App\AppVersion.cs"
$changelogFile = Join-Path $root "src\CvarcLogger.App\CHANGELOG.txt"
$manualFile = Join-Path $root "CvarcLogger User Manual.docx"
$overviewFile = Join-Path $root "Program Overview and Data Flow.md"

$versionMatch = Select-String -Path $appVersionFile -Pattern 'Current\s*=\s*"([\d.]+)"'
if (-not $versionMatch) { throw "Could not read AppVersion.Current from $appVersionFile" }
$version = $versionMatch.Matches[0].Groups[1].Value

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

$exe = Join-Path $outDir "CvarcLogger.exe"

if (Test-Path $changelogFile) {
    Copy-Item -Path $changelogFile -Destination $outDir -Force
} else {
    Write-Warning "CHANGELOG.txt not found at $changelogFile -- publishing without it."
}

# Render the overview doc to PDF (Mermaid diagram included, via mermaid-cli) rather than shipping
# the raw .md -- see render-overview-pdf.py for why (LibreOffice's own HTML->PDF step doesn't scale
# an oversized image on its own, so the diagram is pre-rendered and pre-sized before that step).
# Best-effort like the manual-PDF export below: falls back to the raw .md if the render fails.
$overviewPdf = Join-Path $outDir "Program Overview and Data Flow.pdf"
$overviewRenderScript = Join-Path $root "render-overview-pdf.py"
if ((Test-Path $overviewFile) -and (Test-Path $overviewRenderScript)) {
    & python $overviewRenderScript --md "$overviewFile" --out "$overviewPdf"
    if ($LASTEXITCODE -eq 0 -and (Test-Path $overviewPdf)) {
        Write-Host "Included overview PDF: $overviewPdf"
    } else {
        Write-Warning "Could not render the Program Overview to PDF -- publishing the raw .md instead."
        Copy-Item -Path $overviewFile -Destination $outDir -Force
    }
} elseif (Test-Path $overviewFile) {
    Write-Warning "render-overview-pdf.py not found -- publishing the raw .md instead of a PDF."
    Copy-Item -Path $overviewFile -Destination $outDir -Force
} else {
    Write-Warning "Program Overview and Data Flow.md not found at $overviewFile -- publishing without it."
}

# Export the User Manual (docx) to PDF and include it alongside the exe, so a copied install carries
# its own documentation. Uses LibreOffice headless conversion rather than driving Word via COM --
# Word automation proved unreliable here (hangs on a stuck print-spooler job or a zombie WINWORD
# process, see project_word_com_automation_environment memory), while soffice --headless converts
# directly with no dependency on the print pipeline or a running GUI app. Best-effort -- a machine
# without LibreOffice installed still gets a usable publish, just without the manual. Guarded with a
# timeout regardless, since any external process call in an unattended script should have one.
$sofficeExe = "C:\Program Files\LibreOffice\program\soffice.exe"
if ((Test-Path $manualFile) -and (Test-Path $sofficeExe)) {
    $manualPdf = Join-Path $outDir "CvarcLogger User Manual.pdf"

    $exportProcess = Start-Process -FilePath $sofficeExe `
        -ArgumentList @("--headless", "--convert-to", "pdf", "--outdir", "`"$outDir`"", "`"$manualFile`"") `
        -PassThru -WindowStyle Hidden

    $finished = $exportProcess.WaitForExit(60000)
    if ($finished -and (Test-Path $manualPdf)) {
        Write-Host "Included manual PDF: $manualPdf"
    } else {
        Write-Warning "Could not export the User Manual to PDF within 60s -- publishing without it."
        if (-not $exportProcess.HasExited) { Stop-Process -Id $exportProcess.Id -Force -ErrorAction SilentlyContinue }
    }
} elseif (Test-Path $manualFile) {
    Write-Warning "LibreOffice not found at $sofficeExe -- publishing without the manual PDF."
} else {
    Write-Warning "User manual not found at $manualFile -- publishing without it."
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published: $exe ($sizeMb MB)"

# Every published build gets zipped for export -- see CHANGELOG.txt inside the zip for what changed.
$zipPath = Join-Path $root "publish\CvarcLogger.V$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $outDir -DestinationPath $zipPath -Force
$zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Zipped: $zipPath ($zipSizeMb MB)"
Write-Host "Copy that one file to another 64-bit Windows 10/11 machine and run it directly."

# Also build a proper Windows installer (Apps & Features entry, uninstaller, C:\CvarcLogger install
# path) from the same publish output via Inno Setup -- see CvarcLogger.iss for what it does and why.
# Best-effort like the manual-PDF export above: a machine without Inno Setup installed still gets a
# usable publish (the exe/zip from above), just without this extra installer.
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$issFile = Join-Path $root "CvarcLogger.iss"

if ($iscc -and (Test-Path $issFile)) {
    & $iscc "/DMyAppVersion=$version" $issFile
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Inno Setup compile failed with exit code $LASTEXITCODE -- publish still succeeded without the installer."
    } else {
        $installerPath = Join-Path $root "publish\CvarcLogger-Setup-$version.exe"
        if (Test-Path $installerPath) {
            $installerSizeMb = [math]::Round((Get-Item $installerPath).Length / 1MB, 1)
            Write-Host "Installer: $installerPath ($installerSizeMb MB)"
        }
    }
} elseif (Test-Path $issFile) {
    Write-Warning "Inno Setup (ISCC.exe) not found -- publishing without the installer. Install it (winget install JRSoftware.InnoSetup) to enable this step."
} else {
    Write-Warning "CvarcLogger.iss not found at $issFile -- publishing without the installer."
}
