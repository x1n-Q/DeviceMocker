param(
  [ValidatePattern("^[A-Za-z0-9.-]+$")]
  [string]$IdentityName = "xinQ.DeviceMocker",

  [ValidatePattern("^CN=.+")]
  [string]$Publisher = "CN=BE0E18EB-1533-486E-B02A-CC16E84B5A53",

  [ValidateLength(1, 50)]
  [string]$PublisherDisplayName = "xinQ",

  [ValidatePattern("^\d+\.\d+\.\d+\.\d+$")]
  [string]$Version = "1.1.1.0"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$runId = "$PID-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
$workDirectory = Join-Path $artifactsDirectory "work\$runId"
$publishDirectory = Join-Path $workDirectory "publish-win-x64"
$stagingDirectory = Join-Path $workDirectory "msix-staging"
$projectPath = Join-Path $repositoryRoot "DeviceMocker\DeviceMocker.csproj"

foreach ($target in @($workDirectory, $publishDirectory, $stagingDirectory)) {
  $resolvedTarget = [System.IO.Path]::GetFullPath($target)
  if (-not $resolvedTarget.StartsWith($artifactsDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a packaging directory outside the repository artifacts folder."
  }
  New-Item -ItemType Directory -Force -Path $resolvedTarget | Out-Null
}

dotnet publish $projectPath `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -p:Version=$($Version.Substring(0, $Version.LastIndexOf("."))) `
  -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed."
}

Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stagingDirectory -Recurse

$assetDirectory = Join-Path $stagingDirectory "Assets"
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null
$sourceAssets = Join-Path $repositoryRoot "branding\store"
foreach ($assetName in @(
  "StoreLogo.png",
  "Square44x44Logo.png",
  "Square150x150Logo.png",
  "Square310x310Logo.png",
  "Wide310x150Logo.png",
  "SplashScreen.png")) {
  Copy-Item -LiteralPath (Join-Path $sourceAssets $assetName) -Destination (Join-Path $assetDirectory $assetName)
}

$manifestTemplate = Get-Content -Raw (Join-Path $repositoryRoot "packaging\AppxManifest.xml.template")
$manifest = $manifestTemplate.Replace("__IDENTITY_NAME__", [System.Security.SecurityElement]::Escape($IdentityName))
$manifest = $manifest.Replace("__PUBLISHER__", [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", [System.Security.SecurityElement]::Escape($PublisherDisplayName))
$manifest = $manifest.Replace("__VERSION__", $Version)
if ($manifest -match "__[A-Z_]+__") {
  throw "The generated package manifest contains an unresolved template value."
}
[System.IO.File]::WriteAllText(
  (Join-Path $stagingDirectory "AppxManifest.xml"),
  $manifest,
  [System.Text.UTF8Encoding]::new($false))

$windowsKitsBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$makeAppx = Get-ChildItem -Path $windowsKitsBin -Filter makeappx.exe -Recurse |
  Where-Object { $_.FullName -match "\\x64\\makeappx\.exe$" } |
  Sort-Object FullName -Descending |
  Select-Object -First 1
if (-not $makeAppx) {
  throw "makeappx.exe was not found. Install the Windows SDK."
}

$outputDirectory = Join-Path $artifactsDirectory "store-submission"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$packagePath = Join-Path $outputDirectory "DeviceMocker_${Version}_x64.msix"
if (Test-Path -LiteralPath $packagePath) {
  Remove-Item -LiteralPath $packagePath -Force
}
& $makeAppx.FullName pack /d $stagingDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
  throw "MSIX packaging failed."
}

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
  "$packagePath.sha256",
  "$hash  $(Split-Path -Leaf $packagePath)`n",
  [System.Text.UTF8Encoding]::new($false))

Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Created $packagePath"
Write-Host "SHA256 $hash"
