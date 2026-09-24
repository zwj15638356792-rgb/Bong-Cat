param(
    [string]$ApplicationPath = 'output/Dafeiyu-Jointed/Dafeiyu.exe',
    [string]$OutputDirectory = 'output/shell-checks'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function ProjectPath([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) { return [IO.Path]::GetFullPath($path) }
    return [IO.Path]::GetFullPath((Join-Path $root $path))
}
$app = ProjectPath $ApplicationPath
$outputPath = ProjectPath $OutputDirectory
if (-not (Test-Path -LiteralPath $app)) { throw 'Build the application before running shell checks' }
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$checkExe = Join-Path $outputPath 'AppShellChecks.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
& $compiler /nologo /target:exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$checkExe" (Join-Path $root 'tests/AppShellChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Shell check compilation failed' }
$check = Start-Process -FilePath $checkExe -ArgumentList ('"' + $app + '"'), ('"' + $outputPath + '"') -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw "Shell checks failed; see $outputPath/shell-checks-error.txt" }
Get-Content -LiteralPath (Join-Path $outputPath 'shell-checks.txt')
