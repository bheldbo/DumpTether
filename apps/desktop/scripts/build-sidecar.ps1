param(
    [string] $Runtime = "",
    [string] $SelfContained = "true",
    [string] $IgnoreFailedSources = "true"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$apiProject = Join-Path $repoRoot "src\DumpTether.Api\DumpTether.Api.csproj"
$binaryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\src-tauri\binaries")
$publishRoot = Join-Path $binaryRoot "publish"

function ConvertTo-DumpTetherBooleanString {
    param(
        [string] $Value,
        [string] $Name
    )

    if ($Value -in @("true", "True", "TRUE", "1", "yes", "Yes", "YES")) {
        return "true"
    }

    if ($Value -in @("false", "False", "FALSE", "0", "no", "No", "NO")) {
        return "false"
    }

    throw "$Name must be true or false."
}

function Resolve-DumpTetherRuntime {
    param([string] $RequestedRuntime)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRuntime)) {
        if ($RequestedRuntime -in @("win-x64", "win-arm64", "linux-x64", "linux-arm64")) {
            return $RequestedRuntime
        }

        throw "Runtime must be one of: win-x64, win-arm64, linux-x64, linux-arm64."
    }

    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $architectureSuffix = switch ($architecture) {
        "X64" { "x64" }
        "Arm64" { "arm64" }
        default {
            throw "Unsupported desktop sidecar architecture '$architecture'. Use -Runtime explicitly if this machine can build a supported target."
        }
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        return "win-$architectureSuffix"
    }

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Linux)) {
        return "linux-$architectureSuffix"
    }

    throw "Unsupported desktop sidecar OS. Current supported sidecar runtimes are Windows and Linux."
}

$Runtime = Resolve-DumpTetherRuntime $Runtime
$publishDir = Join-Path $publishRoot $Runtime
$targetTriple = switch ($Runtime) {
    "win-x64" { "x86_64-pc-windows-msvc" }
    "win-arm64" { "aarch64-pc-windows-msvc" }
    "linux-x64" { "x86_64-unknown-linux-gnu" }
    "linux-arm64" { "aarch64-unknown-linux-gnu" }
}

$extension = if ($Runtime.StartsWith("win-")) { ".exe" } else { "" }
$publishedBinary = Join-Path $publishDir "DumpTether.Api$extension"
$sidecarBinary = Join-Path $binaryRoot "dumptether-api-$targetTriple$extension"
$selfContainedValue = ConvertTo-DumpTetherBooleanString $SelfContained "SelfContained"
$ignoreFailedSourcesValue = ConvertTo-DumpTetherBooleanString $IgnoreFailedSources "IgnoreFailedSources"
$userProfileRoot = if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    $env:HOME
}
else {
    $env:USERPROFILE
}
$userPackageCache = if ([string]::IsNullOrWhiteSpace($userProfileRoot)) {
    ""
}
else {
    Join-Path $userProfileRoot ".nuget\packages"
}

if (-not [string]::IsNullOrWhiteSpace($userPackageCache) -and
    [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES) -and
    (Test-Path $userPackageCache)) {
    $env:NUGET_PACKAGES = $userPackageCache
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$restoreArgs = @("restore", $apiProject, "-r", $Runtime)

if ($ignoreFailedSourcesValue -eq "true") {
    $restoreArgs += "--ignore-failed-sources"
}

dotnet @restoreArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet publish $apiProject `
    -c Release `
    -r $Runtime `
    --no-restore `
    --self-contained $selfContainedValue `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishTrimmed=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $publishedBinary)) {
    throw "Expected published API binary was not found at $publishedBinary."
}

Copy-Item -LiteralPath $publishedBinary -Destination $sidecarBinary -Force
Write-Host "Built DumpTether API sidecar: $sidecarBinary"
