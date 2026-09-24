param(
    [string]$OutputDirectory = 'output/Dafeiyu-Jointed'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
        $buildDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
    } else {
        $buildDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $assetsDirectory = Join-Path $buildDirectory 'assets'
    $executable = Join-Path $buildDirectory 'Dafeiyu.exe'
    $defaultBuildDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'output/Dafeiyu-Jointed'))
    if ($buildDirectory -eq $defaultBuildDirectory) {
        $checksDirectory = Join-Path $projectRoot 'output/jointed-checks'
    } else {
        $checksDirectory = Join-Path (Split-Path $buildDirectory -Parent) ((Split-Path $buildDirectory -Leaf) + '-checks')
    }

    # Refuse before changing assets when the destination program is running.
    if (Test-Path -LiteralPath $executable) {
        try {
            $probe = [System.IO.File]::Open($executable, 'Open', 'ReadWrite', 'None')
            $probe.Dispose()
        } catch {
            throw 'The destination executable is in use or not writable; close the pet or choose a different -OutputDirectory'
        }
    }

    # Regenerate geometry from the checked-in rig, then extract the required art.
    python scripts/build-jointed-assets.py --output $assetsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Asset generation failed; install Node.js and requirements.txt dependencies' }
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    }
    $source = Join-Path $PSScriptRoot 'JointedPet.cs'
    & $compiler /nologo /target:winexe /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$executable" $source
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed; close the pet or choose a different -OutputDirectory' }
    $check = Start-Process -FilePath $executable -ArgumentList '--render', ('"' + $checksDirectory + '"') -WindowStyle Hidden -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw "Joint validation failed; see $buildDirectory/error.log" }
    Copy-Item -LiteralPath native-pet/README.md -Destination (Join-Path $buildDirectory 'README.md')
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $buildDirectory 'LICENSE-BongoCat.txt')
    Get-Content -LiteralPath (Join-Path $checksDirectory 'continuity.txt')
    Write-Output "Built desktop pet: $executable"
} finally { Pop-Location }
