param(
    [string]$BuildDirectory = 'output/Dafeiyu-Jointed',
    [string]$InstallerDirectory = 'output/installer',
    [string]$InnoCompiler,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = '1.1.1'

function Resolve-ProjectPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
}

$buildPath = Resolve-ProjectPath $BuildDirectory
$installerPath = Resolve-ProjectPath $InstallerDirectory
$defaultBuildPath = Resolve-ProjectPath 'output/Dafeiyu-Jointed'
if ($buildPath -eq $defaultBuildPath) {
    $checksPath = Resolve-ProjectPath 'output/jointed-checks'
} else {
    $checksPath = Join-Path (Split-Path $buildPath -Parent) ((Split-Path $buildPath -Leaf) + '-checks')
}
$icon = Join-Path $checksPath 'app.ico'
if ($InnoCompiler) {
    $compiler = Resolve-ProjectPath $InnoCompiler
} else {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $candidates = @(
        (Join-Path $projectRoot '.cache/inno/ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe')
    )
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ($programFilesX86) { $candidates += Join-Path $programFilesX86 'Inno Setup 6/ISCC.exe' }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe' }
    if ($command) { $compiler = $command.Source }
    else { $compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1 }
}
if (-not $compiler -or -not (Test-Path -LiteralPath $compiler)) {
    throw 'Inno Setup 6 compiler not found. Install it from https://jrsoftware.org/isdl.php or pass -InnoCompiler path/to/ISCC.exe'
}
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $buildPath
}
foreach ($file in @('Dafeiyu.exe', 'README.md', 'LICENSE-BongoCat.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $buildPath $file))) { throw "Missing package input: $file" }
}
if (-not (Test-Path -LiteralPath $icon)) { throw 'Generated app icon missing; rerun build.ps1 before packaging' }
New-Item -ItemType Directory -Force -Path $installerPath | Out-Null
& $compiler "/DAppVersion=$version" "/DBuildDir=$buildPath" "/DInstallerDir=$installerPath" "/DAppIcon=$icon" (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$installer = Join-Path $installerPath "Dafeiyu-Setup-$version.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw 'Installer compiler did not produce the expected executable' }
Write-Output "Installer ready: $installer"
Write-Output 'This script creates the installer; it does not install or launch the desktop pet.'
