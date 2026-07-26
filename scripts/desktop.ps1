param(
    [ValidateSet("Menu", "Help", "Install", "ConfigureClient", "Sidecar", "Dev", "BuildExe", "Build", "BuildMsi", "BuildLinux")]
    [string] $Target = "Menu"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$desktopRoot = Join-Path $repoRoot "apps\desktop"
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

    $dotenvValues = Read-DumpTetherDotEnvFiles -Paths $envFilePaths
    $deploymentTarget = $dotenvValues["DUMPTETHER_DEPLOYMENT_TARGET"]
    $existingDeploymentTarget = $env:DUMPTETHER_DEPLOYMENT_TARGET

    if (-not [string]::IsNullOrWhiteSpace($deploymentTarget)) {
        $env:DUMPTETHER_DEPLOYMENT_TARGET = $deploymentTarget
    }

    Push-Location $repoRoot
    try {
        & node scripts/configure-client.mjs
        if ($LASTEXITCODE -ne 0) {
            throw "Client configuration failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
        $env:DUMPTETHER_DEPLOYMENT_TARGET = $existingDeploymentTarget
    }
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
}

function Build-DesktopInstaller {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri bundles."
    Install-DesktopDependencies
    Clear-DesktopBundleArtifacts

    Invoke-DesktopNpmScript "build:desktop"
}

function Build-DesktopMsiInstaller {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri bundles."
    Install-DesktopDependencies
    Clear-DesktopBundleArtifacts

    Invoke-DesktopNpmScript "build:desktop:msi"
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
