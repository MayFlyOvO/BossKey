param(
    [Parameter(Position = 0)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionPropsPath = Join-Path $rootDir "build\Version.props"
$appProject = Join-Path $rootDir "BossKey.App\BossKey.App.csproj"
$issScript = Join-Path $rootDir "installer\BossKey.iss"

function Get-ReleaseVersion {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "Version file not found: $Path"
    }

    [xml]$xml = Get-Content -Path $Path -Raw
    $value = $xml.Project.PropertyGroup.AppVersion
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "AppVersion was not found in $Path"
    }

    return $value.Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ReleaseVersion -Path $versionPropsPath
}

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Invalid version format: $Version. Use formats like 1.0.0 or 1.0.0.0."
}

$versionParts = $Version.Split('.')
$fileVersion = if ($versionParts.Count -eq 3) { "$Version.0" } else { $Version }
$isReleaseBuild = $Version -eq (Get-ReleaseVersion -Path $versionPropsPath)

$config = "Release"
$rid = "win-x64"

if ($isReleaseBuild) {
    $outRoot = Join-Path $rootDir "artifacts"
    $buildOut = Join-Path $outRoot "build"
    $publishOut = Join-Path $outRoot "publish"
    $singleOut = Join-Path $outRoot "singlefile"
    $installerOut = Join-Path $outRoot "installer"
    $outputBaseName = "BossKey-Setup"
}
else {
    $outRoot = Join-Path $rootDir "artifacts\test-builds\$Version"
    $buildOut = Join-Path $outRoot "build"
    $publishOut = Join-Path $outRoot "publish"
    $singleOut = Join-Path $outRoot "singlefile"
    $installerOut = Join-Path $outRoot "installer"
    $outputBaseName = "BossKey-Setup-$Version"
}

if (-not (Test-Path $appProject)) {
    throw "Project file not found: $appProject"
}

Write-Host "[INFO] Version: $Version"
Write-Host "[INFO] Stopping running app to avoid file locks..."
Get-Process BossKey.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "[INFO] Cleaning output..."
if (Test-Path $buildOut) { Remove-Item $buildOut -Recurse -Force }
if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force }
if (Test-Path $singleOut) { Remove-Item $singleOut -Recurse -Force }
if (Test-Path $installerOut) { Remove-Item $installerOut -Recurse -Force }

function Invoke-Step {
    param(
        [string]$Title,
        [string[]]$Command
    )

    Write-Host $Title
    & $Command[0] $Command[1..($Command.Length - 1)]
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $($Command -join ' ')"
    }
}

$versionArgs = @(
    "-p:AppVersion=$Version",
    "-p:Version=$Version",
    "-p:FileVersion=$fileVersion",
    "-p:AssemblyVersion=$fileVersion",
    "-p:InformationalVersion=$Version"
)
$buildFlags = @(
    "-p:NuGetAudit=false"
)

Invoke-Step "[1/6] Restore..." (@(
    "dotnet", "restore", $appProject
) + $buildFlags)

$buildCommand = @(
    "dotnet", "build", $appProject,
    "-c", $config,
    "-o", $buildOut
) + $versionArgs + $buildFlags
Invoke-Step "[2/6] Build $Version..." $buildCommand

$publishMultiFileCommand = @(
    "dotnet", "publish", $appProject,
    "-c", $config,
    "-r", $rid,
    "--self-contained", "false",
    "-p:PublishSingleFile=false",
    "-p:UpdateChannel=installer",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $publishOut
) + $versionArgs + $buildFlags
Invoke-Step "[3/6] Publish Multi-File (framework-dependent, $rid)..." $publishMultiFileCommand

$publishSingleFileCommand = @(
    "dotnet", "publish", $appProject,
    "-c", $config,
    "-r", $rid,
    "--self-contained", "false",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:UpdateChannel=singlefile",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $singleOut
) + $versionArgs + $buildFlags
Invoke-Step "[4/6] Publish Single-File (framework-dependent, $rid)..." $publishSingleFileCommand

$singleFileExe = Join-Path $singleOut "BossKey.App.exe"
if (Test-Path $singleFileExe) {
    Copy-Item $singleFileExe (Join-Path $singleOut "BossKey-SingleFile.exe") -Force
}

Write-Host "[5/6] Build Installer (Inno Setup)..."
if (-not (Test-Path $issScript)) {
    Write-Warning "Installer script not found: $issScript"
    Write-Warning "Skipping installer build."
}
elseif (-not (Test-Path (Join-Path $publishOut "BossKey.App.exe"))) {
    Write-Warning "Publish executable not found. Skipping installer build."
}
else {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if (-not $iscc) {
        $isccCandidates = @(
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            "C:\Program Files\Inno Setup 6\ISCC.exe"
        )
        $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if (-not $iscc) {
        Write-Warning "Inno Setup compiler (ISCC.exe) not found. Skipping installer build."
    }
    else {
        New-Item -ItemType Directory -Path $installerOut -Force | Out-Null
        Invoke-Step "    Running ISCC..." @(
            $iscc,
            "/DSourceDir=$publishOut",
            "/DSourceIcon=$(Join-Path $rootDir 'BossKey.App\Assets\BossKey.ico')",
            "/DOutputDir=$installerOut",
            "/DMyAppVersion=$Version",
            "/DOutputBaseName=$outputBaseName",
            $issScript
        )
    }
}

Write-Host "[6/6] Done."
Write-Host ""
Write-Host "Version: $Version"
Write-Host "Build output:"
Write-Host "  $buildOut"
Write-Host ""
Write-Host "Multi-file publish output:"
Write-Host "  $publishOut"
Write-Host ""
Write-Host "Single-file publish output:"
Write-Host "  $(Join-Path $singleOut 'BossKey-SingleFile.exe')"
Write-Host ""
Write-Host "Installer output:"
Write-Host "  $installerOut"
