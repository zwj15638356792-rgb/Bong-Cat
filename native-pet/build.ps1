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
    $executable = Join-Path $buildDirectory 'Dafeiyu.exe'
    $defaultBuildDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'output/Dafeiyu-Jointed'))
    if ($buildDirectory -eq $defaultBuildDirectory) {
        $checksDirectory = Join-Path $projectRoot 'output/jointed-checks'
    } else {
        $checksDirectory = Join-Path (Split-Path $buildDirectory -Parent) ((Split-Path $buildDirectory -Leaf) + '-checks')
    }
    $assetsDirectory = Join-Path $checksDirectory 'assets'
    $icon = Join-Path $checksDirectory 'app.ico'
    $manifest = Join-Path $PSScriptRoot 'app.manifest'

    # Refuse before changing assets when the destination program is running.
    if (Test-Path -LiteralPath $executable) {
        try {
            $probe = [System.IO.File]::Open($executable, 'Open', 'ReadWrite', 'None')
            $probe.Dispose()
        } catch {
            throw 'The destination executable is in use or not writable; close the pet or choose a different -OutputDirectory'
        }
    }

    New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null
    python scripts/build-jointed-assets.py --output $assetsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Asset generation failed; install Node.js and requirements.txt dependencies' }
    python scripts/build-app-icon.py --output $icon
    if ($LASTEXITCODE -ne 0) { throw 'Application icon generation failed' }
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    }
    $assetNames = @(
        'body.png', 'front-hair.png', 'cuff-hand.png', 'fabric.png',
        'face-blink.png', 'face-happy.png', 'face-surprised.png',
        'mouse.png', 'mouse-left.png', 'mouse-right.png', 'pad.png'
    )
    $compilerArguments = @(
        '/nologo', '/target:winexe', '/optimize+',
        '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
        '/reference:System.Xml.dll',
        "/out:$executable", "/win32icon:$icon", "/win32manifest:$manifest",
        "/resource:$icon,Dafeiyu.Assets.app.ico"
    )
    foreach ($assetName in $assetNames) {
        $assetPath = Join-Path $assetsDirectory $assetName
        if (-not (Test-Path -LiteralPath $assetPath)) { throw "Missing embedded asset: $assetName" }
        $compilerArguments += "/resource:$assetPath,Dafeiyu.Assets.$assetName"
    }
    $sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName })
    & $compiler @compilerArguments @sources
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed; close the pet or choose a different -OutputDirectory' }
    $check = Start-Process -FilePath $executable -ArgumentList '--render', ('"' + $checksDirectory + '"') -WindowStyle Hidden -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw "Joint validation failed; see $buildDirectory/error.log" }
    Copy-Item -LiteralPath native-pet/README.md -Destination (Join-Path $buildDirectory 'README.md')
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $buildDirectory 'LICENSE-BongoCat.txt')
    Get-Content -LiteralPath (Join-Path $checksDirectory 'continuity.txt')
    Write-Output "Built self-contained desktop pet: $executable"
} finally { Pop-Location }
