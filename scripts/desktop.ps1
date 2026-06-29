param(
    [ValidateSet("Install", "Sidecar", "Dev", "Build")]
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

    Invoke-InDesktopRoot {
        npm.cmd run dev
        if ($LASTEXITCODE -ne 0) {
            throw "Tauri dev failed with exit code $LASTEXITCODE."
        }
    }
}

function Build-DesktopInstaller {
    Assert-Command "cargo" "Install the Rust toolchain with Cargo before building Tauri bundles."
    Install-DesktopDependencies

    Invoke-InDesktopRoot {
        npm.cmd run build:desktop
        if ($LASTEXITCODE -ne 0) {
            throw "Tauri build failed with exit code $LASTEXITCODE."
        }
    }
}

switch ($Target) {
    "Install" { Install-DesktopDependencies }
    "Sidecar" { Build-Sidecar }
    "Dev" { Start-DesktopDev }
    "Build" { Build-DesktopInstaller }
}
