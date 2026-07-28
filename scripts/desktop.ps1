param(
    [ValidateSet("Menu", "Help", "Install", "ConfigureClient", "Sidecar", "Dev", "BuildExe", "Build", "BuildMsi", "BuildLinux")]
    [string] $Target = "Menu"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$desktopRoot = Join-Path $repoRoot "apps\desktop"
$desktopTargetRoot = Join-Path $desktopRoot "src-tauri\target\release"
$releaseRoot = Join-Path $repoRoot "releases\desktop"
$envFilePaths = @(
    (Join-Path $repoRoot ".env"),
    (Join-Path $repoRoot ".env.local")
)

Import-Module (Join-Path $PSScriptRoot "DumpTether.DevTools.psm1") -Force

function Assert-Command {
    param(
        [string] $Name,
        [string] $InstallHint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found. $InstallHint"
    }
}

function Invoke-InDesktopRoot {
    param([scriptblock] $Command)

    Push-Location $desktopRoot
    try {
        & $Command
    }
    finally {
        Pop-Location
    }
}

function Get-NpmCommand {
    if (Get-Command "npm.cmd" -ErrorAction SilentlyContinue) {
        return "npm.cmd"
    }

    if (Get-Command "npm" -ErrorAction SilentlyContinue) {
        return "npm"
    }

    throw "npm was not found. Install Node.js 24 LTS or newer."
}

function Invoke-DesktopNpmScript {
    param(
        [string] $ScriptName
    )

    $vsDevCommand = Get-WindowsNativeToolsCommand
    $npmCommand = Get-NpmCommand
    $deploymentTarget = Get-DesktopDeploymentTarget
    $previousDeploymentTarget = $env:DUMPTETHER_DEPLOYMENT_TARGET
    $previousGeneratedTarget = Get-GeneratedDeploymentTarget
    $env:DUMPTETHER_DEPLOYMENT_TARGET = $deploymentTarget

    try {
        Invoke-InDesktopRoot {
            if ($vsDevCommand) {
                $command = "call `"$vsDevCommand`" -arch=x64 && npm.cmd run $ScriptName"
                cmd.exe /d /s /c $command
            }
            else {
                & $npmCommand run $ScriptName
            }
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Desktop npm script '$ScriptName' failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        $env:DUMPTETHER_DEPLOYMENT_TARGET = $previousDeploymentTarget

        if (-not [string]::IsNullOrWhiteSpace($previousGeneratedTarget) -and
            $previousGeneratedTarget -ne $deploymentTarget) {
            Invoke-ClientConfiguration -Target $previousGeneratedTarget
        }
    }
}

function Get-DesktopDeploymentTarget {
    if (-not [string]::IsNullOrWhiteSpace($env:DUMPTETHER_DEPLOYMENT_TARGET)) {
        return $env:DUMPTETHER_DEPLOYMENT_TARGET
    }

    $dotenvValues = Read-DumpTetherDotEnvFiles -Paths $envFilePaths
    $deploymentTarget = $dotenvValues["DUMPTETHER_DEPLOYMENT_TARGET"]

    if ([string]::IsNullOrWhiteSpace($deploymentTarget)) {
        return "standalone"
    }

    return $deploymentTarget
}

function Get-GeneratedDeploymentTarget {
    $generatedTargetPath = Join-Path $repoRoot "apps\web\src\generated\deploymentTarget.ts"

    if (-not (Test-Path $generatedTargetPath)) {
        return $null
    }

    $content = Get-Content -Raw -LiteralPath $generatedTargetPath
    $match = [regex]::Match($content, '"targetId"\s*:\s*"(?<target>[^"]+)"')

    if (-not $match.Success) {
        return $null
    }

    return $match.Groups["target"].Value
}

function Get-DeploymentTargetVersion {
    param([string] $Target)

    $targetPath = if ([System.IO.Path]::IsPathRooted($Target) -or
        $Target.EndsWith(".json", [StringComparison]::OrdinalIgnoreCase)) {
        $Target
    }
    else {
        Join-Path $repoRoot "deploy\targets\$Target.json"
    }

    if (-not [System.IO.Path]::IsPathRooted($targetPath)) {
        $targetPath = Join-Path $repoRoot $targetPath
    }

    if (-not (Test-Path -LiteralPath $targetPath)) {
        throw "Desktop deployment target was not found: $targetPath"
    }

    $deploymentConfig = Get-Content -Raw -LiteralPath $targetPath | ConvertFrom-Json
    return [string] $deploymentConfig.version
}

function Invoke-ClientConfiguration {
    param([string] $Target)

    Push-Location $repoRoot
    try {
        & node scripts/configure-client.mjs --target $Target
        if ($LASTEXITCODE -ne 0) {
            throw "Client configuration failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Install-DesktopDependencies {
    $npmCommand = Get-NpmCommand

    Invoke-InDesktopRoot {
        if (Test-Path "node_modules") {
            Write-Host "Desktop npm dependencies already installed."
            return
        }

        & $npmCommand install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE."
        }
    }
}

function Set-ClientConfiguration {
    Assert-Command "node" "Install Node.js 24 LTS or newer."

    $deploymentTarget = Get-DesktopDeploymentTarget
    Invoke-ClientConfiguration -Target $deploymentTarget
}

function Build-Sidecar {
    Assert-Command "dotnet" "Install the .NET 8 SDK."
    Install-DesktopDependencies

    Invoke-InDesktopRoot {
        $npmCommand = Get-NpmCommand
        & $npmCommand run build:sidecar
        if ($LASTEXITCODE -ne 0) {
            throw "Desktop sidecar build failed with exit code $LASTEXITCODE."
        }
    }
}

function Start-DesktopDev {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before running Tauri."
    Install-DesktopDependencies

    Invoke-DesktopNpmScript "dev"
}

function Build-DesktopExecutable {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri."
    Install-DesktopDependencies

    Invoke-DesktopNpmScript "build:desktop:exe"
    Export-DesktopReleaseArtifacts -Patterns @(
        "dumptether-desktop.exe",
        "dumptether-api.exe",
        "appsettings.Desktop.json"
    )
}

function Build-DesktopInstaller {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri bundles."
    Install-DesktopDependencies
    Clear-DesktopBundleArtifacts

    Invoke-DesktopNpmScript "build:desktop"
    Export-DesktopReleaseArtifacts -Patterns @(
        "dumptether-desktop.exe",
        "dumptether-api.exe",
        "appsettings.Desktop.json",
        "bundle\nsis\*.exe"
    )
}

function Build-DesktopMsiInstaller {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri bundles."
    Install-DesktopDependencies
    Clear-DesktopBundleArtifacts

    Invoke-DesktopNpmScript "build:desktop:msi"
    Export-DesktopReleaseArtifacts -Patterns @(
        "dumptether-desktop.exe",
        "dumptether-api.exe",
        "appsettings.Desktop.json",
        "bundle\msi\*.msi"
    )
}

function Build-DesktopLinuxBundles {
    $isLinuxHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)

    if (-not $isLinuxHost) {
        throw "Build Linux desktop bundles on a Linux host or Linux CI runner. Tauri Linux bundles are not produced from this Windows helper path."
    }

    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri bundles."
    Install-DesktopDependencies
    Clear-DesktopBundleArtifacts

    Invoke-DesktopNpmScript "build:desktop:linux"
    Export-DesktopReleaseArtifacts -Patterns @(
        "dumptether-desktop",
        "dumptether-api",
        "appsettings.Desktop.json",
        "bundle\appimage\*.AppImage",
        "bundle\deb\*.deb",
        "bundle\rpm\*.rpm"
    )
}

function Export-DesktopReleaseArtifacts {
    param([string[]] $Patterns)

    $deploymentTarget = Get-DesktopDeploymentTarget
    $version = Get-DeploymentTargetVersion -Target $deploymentTarget
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $platform = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        "windows-$architecture"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)) {
        "linux-$architecture"
    }
    else {
        throw "Desktop release collection currently supports Windows and Linux."
    }

    $destination = Join-Path $releaseRoot "v$version\$platform"
    $releaseRootFullPath = [System.IO.Path]::GetFullPath($releaseRoot)
    $destinationFullPath = [System.IO.Path]::GetFullPath($destination)

    if (-not $destinationFullPath.StartsWith(
            "$releaseRootFullPath$([System.IO.Path]::DirectorySeparatorChar)",
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace desktop release artifacts outside $releaseRootFullPath."
    }

    if (Test-Path -LiteralPath $destinationFullPath) {
        Remove-Item -LiteralPath $destinationFullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    $copied = @()
    foreach ($pattern in $Patterns) {
        $sourcePattern = Join-Path $desktopTargetRoot $pattern
        $matches = Get-ChildItem -Path $sourcePattern -File -ErrorAction SilentlyContinue

        foreach ($match in $matches) {
            Copy-Item -LiteralPath $match.FullName -Destination $destination -Force
            $copied += Join-Path $destination $match.Name
        }
    }

    if ($copied.Count -eq 0) {
        throw "Desktop build completed, but no release artifacts matched the expected output patterns."
    }

    $uniqueArtifacts = $copied | Sort-Object -Unique
    $checksumLines = foreach ($artifact in $uniqueArtifacts) {
        $hash = Get-FileHash -LiteralPath $artifact -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($artifact))"
    }
    $checksumPath = Join-Path $destination "SHA256SUMS.txt"
    [System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.Encoding]::ASCII)

    Write-Host ""
    Write-Host "Desktop release artifacts:" -ForegroundColor Green
    foreach ($artifact in $uniqueArtifacts) {
        Write-Host "  $artifact"
    }
    Write-Host "  $checksumPath"
}

function Clear-DesktopBundleArtifacts {
    $targetReleaseRoot = Join-Path $desktopRoot "src-tauri\target\release"
    $artifactPaths = @(
        (Join-Path $targetReleaseRoot "bundle"),
        (Join-Path $targetReleaseRoot "wix")
    )

    foreach ($artifactPath in $artifactPaths) {
        if (-not (Test-Path $artifactPath)) {
            continue
        }

        $resolvedArtifactPath = Resolve-Path $artifactPath
        $resolvedDesktopRoot = Resolve-Path $desktopRoot

        if (-not $resolvedArtifactPath.Path.StartsWith($resolvedDesktopRoot.Path, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to delete generated desktop artifacts outside apps/desktop: $resolvedArtifactPath"
        }

        Remove-Item -LiteralPath $resolvedArtifactPath.Path -Recurse -Force
    }
}

function Get-WindowsNativeToolsCommand {
    $isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)

    if (-not $isWindowsHost) {
        return $null
    }

    $visualStudioRoots = @(
        "${env:ProgramFiles}\Microsoft Visual Studio",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path $_) }

    $vsDevCommands = foreach ($root in $visualStudioRoots) {
        Get-ChildItem `
            -Path $root `
            -Filter "VsDevCmd.bat" `
            -Recurse `
            -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\Common7\\Tools\\VsDevCmd\.bat$" }
    }

    foreach ($vsDevCommand in $vsDevCommands) {
        $toolsRoot = Split-Path -Parent $vsDevCommand.FullName
        $commonRoot = Split-Path -Parent $toolsRoot
        $installationRoot = Split-Path -Parent $commonRoot
        $msvcrtPattern = Join-Path $installationRoot "VC\Tools\MSVC\*\lib\x64\msvcrt.lib"

        if (Test-Path $msvcrtPattern) {
            return $vsDevCommand.FullName
        }
    }

    throw "Desktop build requires the Visual Studio C++ x64 toolchain. Install the Visual Studio workload 'Desktop development with C++' with the MSVC x64/x86 build tools and Windows SDK, then reopen PowerShell. The linker currently cannot find VC\Tools\MSVC\...\lib\x64\msvcrt.lib."
}

function Show-Help {
    Write-Host ""
    Write-Host "DumpTether desktop runner"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\scripts\desktop.ps1 -Target <target>"
    Write-Host ""
    Write-Host "Targets:"
    Write-Host "  Install       Install desktop npm/Tauri CLI dependencies."
    Write-Host "  ConfigureClient Generate web/Tauri/npm/Cargo settings from deploy/targets."
    Write-Host "  Sidecar       Publish the .NET API sidecar binary only."
    Write-Host "  Dev           Run Tauri dev mode with Vite and a local SQLite API sidecar."
    Write-Host "  BuildExe      Build the desktop executable without NSIS/MSI installer bundling."
    Write-Host "  Build         Build the desktop executable and default NSIS installer."
    Write-Host "  BuildMsi      Build an MSI installer."
    Write-Host "  BuildLinux    Build Linux bundles. Must run on a Linux host/runner."
    Write-Host ""
    Write-Host "Release-ready copies are written to releases/desktop/<version>/<platform>."
    Write-Host "Generated Rust/Tauri output lives in apps/desktop/src-tauri/target."
    Write-Host "Clean generated output with:"
    Write-Host "  .\scripts\clean.ps1 -Target Generated"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\scripts\desktop.ps1 -Target Dev"
    Write-Host "  .\scripts\desktop.ps1 -Target BuildExe"
}

function Show-Menu {
    while ($true) {
        Write-Host ""
        Write-Host "DumpTether desktop runner"
        Write-Host "1. Install desktop dependencies"
        Write-Host "2. Configure client from deployment target"
        Write-Host "3. Build .NET API sidecar only"
        Write-Host "4. Run desktop dev shell"
        Write-Host "5. Build desktop executable only"
        Write-Host "6. Build desktop executable + NSIS installer"
        Write-Host "7. Build MSI installer"
        Write-Host "8. Build Linux bundles"
        Write-Host "H. Help"
        Write-Host "Q. Quit"
        $choice = Read-Host "Choose"

        if ($null -eq $choice) {
            return
        }

        if ([string]::IsNullOrWhiteSpace($choice)) {
            continue
        }

        switch ($choice.ToUpperInvariant()) {
            "1" { Install-DesktopDependencies }
            "2" { Set-ClientConfiguration }
            "3" { Build-Sidecar }
            "4" { Start-DesktopDev; return }
            "5" { Build-DesktopExecutable }
            "6" { Build-DesktopInstaller }
            "7" { Build-DesktopMsiInstaller }
            "8" { Build-DesktopLinuxBundles }
            "H" { Show-Help }
            "Q" { return }
            default { Write-Host "Unknown choice." -ForegroundColor Yellow }
        }
    }
}

switch ($Target) {
    "Menu" { Show-Menu }
    "Help" { Show-Help }
    "Install" { Install-DesktopDependencies }
    "ConfigureClient" { Set-ClientConfiguration }
    "Sidecar" { Build-Sidecar }
    "Dev" { Start-DesktopDev }
    "BuildExe" { Build-DesktopExecutable }
    "Build" { Build-DesktopInstaller }
    "BuildMsi" { Build-DesktopMsiInstaller }
    "BuildLinux" { Build-DesktopLinuxBundles }
}
