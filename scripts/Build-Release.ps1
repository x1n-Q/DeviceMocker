param(
  [ValidatePattern("^\d+\.\d+\.\d+$")]
  [string]$Version = "1.1.1"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$releaseDirectory = Join-Path $artifactsDirectory "github-release\v$Version"
$workDirectory = Join-Path $artifactsDirectory "release-work\$PID-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
$mainPublish = Join-Path $workDirectory "DeviceMocker"
$companionPublish = Join-Path $workDirectory "PosHardwareTestApp"
$bundleDirectory = Join-Path $workDirectory "Bundle"

foreach ($target in @($releaseDirectory, $workDirectory, $mainPublish, $companionPublish, $bundleDirectory)) {
  $resolvedTarget = [System.IO.Path]::GetFullPath($target)
  if (-not $resolvedTarget.StartsWith($artifactsDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a release directory outside the repository artifacts folder."
  }
  New-Item -ItemType Directory -Force -Path $resolvedTarget | Out-Null
}

dotnet publish (Join-Path $repositoryRoot "DeviceMocker\DeviceMocker.csproj") `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -p:BundleHardwareTestApp=false `
  -p:Version=$Version -o $mainPublish
if ($LASTEXITCODE -ne 0) { throw "DeviceMocker publish failed." }

dotnet publish (Join-Path $repositoryRoot "Samples\PosHardwareTestApp\PosHardwareTestApp.csproj") `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o $companionPublish
if ($LASTEXITCODE -ne 0) { throw "POS Hardware Test App publish failed." }

foreach ($publishDirectory in @($mainPublish, $companionPublish)) {
  Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination $publishDirectory
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $mainPublish

$mainZip = Join-Path $releaseDirectory "DeviceMocker-v$Version-win-x64.zip"
$companionZip = Join-Path $releaseDirectory "PosHardwareTestApp-v$Version-win-x64.zip"
$bundleZip = Join-Path $releaseDirectory "DeviceMocker-ReleaseBundle-v$Version-win-x64.zip"

Copy-Item -LiteralPath $mainPublish -Destination (Join-Path $bundleDirectory "DeviceMocker") -Recurse
Copy-Item -LiteralPath $companionPublish -Destination (Join-Path $bundleDirectory "PosHardwareTestApp") -Recurse

Compress-Archive -Path (Join-Path $mainPublish "*") -DestinationPath $mainZip -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $companionPublish "*") -DestinationPath $companionZip -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $bundleDirectory "*") -DestinationPath $bundleZip -CompressionLevel Optimal -Force

$checksumLines = foreach ($file in @($mainZip, $companionZip, $bundleZip)) {
  "{0}  {1}" -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $file)
}
[System.IO.File]::WriteAllLines(
  (Join-Path $releaseDirectory "SHA256SUMS.txt"),
  $checksumLines,
  [System.Text.UTF8Encoding]::new($false))

Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Created GitHub release files in $releaseDirectory"
