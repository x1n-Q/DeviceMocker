param()

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$brandingDirectory = Join-Path $repositoryRoot "branding"
$workDirectory = Join-Path $repositoryRoot "artifacts\branding-work"
$storeDirectory = Join-Path $brandingDirectory "store"
$iconSvg = Join-Path $brandingDirectory "DeviceMocker-icon.svg"
$logoSvg = Join-Path $brandingDirectory "DeviceMocker-logo-full.svg"
$rendererProject = Join-Path $PSScriptRoot "BrandingRenderer\BrandingRenderer.csproj"
$rendererAssembly = Join-Path $PSScriptRoot "BrandingRenderer\bin\Release\net8.0-windows\BrandingRenderer.dll"

foreach ($requiredPath in @($iconSvg, $logoSvg)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing branding source: $requiredPath"
    }
}
if (-not (Test-Path -LiteralPath $rendererProject -PathType Leaf)) {
    throw "Branding renderer project was not found at $rendererProject"
}

New-Item -ItemType Directory -Force -Path $workDirectory, $storeDirectory | Out-Null
dotnet build $rendererProject -c Release
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $rendererAssembly -PathType Leaf)) {
    throw "Branding renderer build failed."
}

function Invoke-SvgRender {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][int]$Width,
        [Parameter(Mandatory = $true)][int]$Height,
        [switch]$Fit
    )

    $arguments = @($rendererAssembly, $Source, $Destination, $Width, $Height)
    if ($Fit) {
        $arguments += "fit"
    }
    & dotnet $arguments | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $Destination -PathType Leaf)) {
        throw "SVG rendering failed for $Destination"
    }
}

function New-PngIconFile {
    param(
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][object[]]$Images
    )

    $stream = [System.IO.File]::Open($Destination, [System.IO.FileMode]::Create)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Images.Count)
        $offset = 6 + (16 * $Images.Count)
        foreach ($image in $Images) {
            $dimension = if ($image.Size -eq 256) { [byte]0 } else { [byte]$image.Size }
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$image.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Bytes.Length
        }
        foreach ($image in $Images) {
            $bytes = [byte[]]$image.Bytes
            $writer.BaseStream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$iconPng = Join-Path $brandingDirectory "DeviceMocker-icon-512.png"
$fullLogoPng = Join-Path $brandingDirectory "DeviceMocker-logo-full.png"
Invoke-SvgRender -Source $iconSvg -Destination $iconPng -Width 512 -Height 512
Invoke-SvgRender -Source $logoSvg -Destination $fullLogoPng -Width 1200 -Height 500

$icoImages = foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $path = Join-Path $workDirectory "icon-$size.png"
    Invoke-SvgRender -Source $iconSvg -Destination $path -Width $size -Height $size
    [pscustomobject]@{
        Size = $size
        Bytes = [System.IO.File]::ReadAllBytes($path)
    }
}

$brandIco = Join-Path $brandingDirectory "DeviceMocker.ico"
New-PngIconFile -Destination $brandIco -Images $icoImages

$mainAssets = Join-Path $repositoryRoot "DeviceMocker\Assets"
$sampleAssets = Join-Path $repositoryRoot "Samples\PosHardwareTestApp\Assets"
New-Item -ItemType Directory -Force -Path $mainAssets, $sampleAssets | Out-Null
Copy-Item -LiteralPath $brandIco -Destination (Join-Path $mainAssets "DeviceMocker.ico") -Force
Copy-Item -LiteralPath $brandIco -Destination (Join-Path $sampleAssets "DeviceMocker.ico") -Force
Copy-Item -LiteralPath $iconPng -Destination (Join-Path $mainAssets "DeviceMocker-icon-512.png") -Force

$squareAssets = [ordered]@{
    "StoreLogo.png" = 50
    "Square44x44Logo.png" = 44
    "Square150x150Logo.png" = 150
    "Square310x310Logo.png" = 310
}
foreach ($asset in $squareAssets.GetEnumerator()) {
    Invoke-SvgRender `
        -Source $iconSvg `
        -Destination (Join-Path $storeDirectory $asset.Key) `
        -Width $asset.Value `
        -Height $asset.Value
}
Invoke-SvgRender -Source $logoSvg -Destination (Join-Path $storeDirectory "Wide310x150Logo.png") -Width 310 -Height 150 -Fit
Invoke-SvgRender -Source $logoSvg -Destination (Join-Path $storeDirectory "SplashScreen.png") -Width 620 -Height 300 -Fit

[System.IO.Directory]::Delete($workDirectory, $true)
Write-Host "Generated DeviceMocker branding and Microsoft Store assets from SVG sources."
