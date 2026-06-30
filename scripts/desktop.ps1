param(
    [ValidateSet("Install", "Sidecar", "Dev", "Build", "BuildMsi")]
    [string] $Target = "Dev"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$desktopRoot = Join-Path $repoRoot "apps\desktop"

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

function Invoke-DesktopNpmScript {
    param(
        [string] $ScriptName
    )

    $vsDevCommand = Get-WindowsNativeToolsCommand

    Invoke-InDesktopRoot {
        if ($vsDevCommand) {
            $command = "call `"$vsDevCommand`" -arch=x64 && npm.cmd run $ScriptName"
            cmd.exe /d /s /c $command
        }
        else {
            npm.cmd run $ScriptName
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Desktop npm script '$ScriptName' failed with exit code $LASTEXITCODE."
    }
}

function Install-DesktopDependencies {
    Assert-Command "npm.cmd" "Install Node.js 24 LTS or newer."

    Invoke-InDesktopRoot {
        if (Test-Path "node_modules") {
            Write-Host "Desktop npm dependencies already installed."
            return
        }

        npm.cmd install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE."
        }
    }
}

function Build-Sidecar {
    Assert-Command "dotnet" "Install the .NET 8 SDK."
    Install-DesktopDependencies

    Invoke-InDesktopRoot {
        npm.cmd run build:sidecar:dev
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

switch ($Target) {
    "Install" { Install-DesktopDependencies }
    "Sidecar" { Build-Sidecar }
    "Dev" { Start-DesktopDev }
    "Build" { Build-DesktopInstaller }
    "BuildMsi" { Build-DesktopMsiInstaller }
}
